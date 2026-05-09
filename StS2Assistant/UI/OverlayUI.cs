using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using StS2Assistant.Logic;

namespace StS2Assistant.UI;

/// <summary>
/// Оверлей UI для отображения рекомендаций.
/// Использует Godot Control nodes для рендеринга.
/// </summary>
public partial class OverlayUI : CanvasLayer
{
    private static readonly string LogPrefix = "[StS2Assistant.UI]";
    
    // Основные элементы UI
    private Control? _mainPanel;
    private RichTextLabel? _recommendationsLabel;
    private RichTextLabel? _drawPredictionLabel;
    private RichTextLabel? _situationLabel;
    private VBoxContainer? _cardRecommendationsContainer;
    
    private bool _isVisible = true;
    private Recommendations? _currentRecommendations;

    public override void _Ready()
    {
        Layer = 100; // Поверх всех игровых элементов
        
        InitializeUI();
        
        GD.Print($"{LogPrefix} Overlay UI initialized");
    }

    /// <summary>
    /// Инициализация UI элементов
    /// </summary>
    private void InitializeUI()
    {
        try
        {
            // Главный контейнер
            _mainPanel = new Control
            {
                Name = "StS2AssistantPanel",
                MouseFilter = Control.MouseFilterEnum.Ignore, // Не блокировать клики
                AnchorsPreset = Control.LayoutPreset.TopLeft,
                Position = new Vector2(20, 20),
                Size = new Vector2(400, 600)
            };
            
            // Стиль панели (полупрозрачный фон)
            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.1f, 0.1f, 0.15f, 0.85f),
                CornerRadius = 10
            };
            _mainPanel.AddThemeStyleboxOverride("panel", style);
            
            // Вертикальный контейнер
            var mainContainer = new VBoxContainer
            {
                Name = "MainContainer",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                ThemeOverrideConstants = new Dictionary<string, int>
                {
                    ["separation"] = 10
                }
            };
            
            // Заголовок
            var titleLabel = new Label
            {
                Text = "🎯 StS2 Assistant",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            titleLabel.AddThemeFontSizeOverride("font_size", 18);
            mainContainer.AddChild(titleLabel);
            
            // Ситуационная оценка
            _situationLabel = new RichTextLabel
            {
                Name = "SituationLabel",
                BbcodeEnabled = true,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            _situationLabel.AddThemeFontSizeOverride("normal_font_size", 14);
            mainContainer.AddChild(_situationLabel);
            
            // Разделитель
            mainContainer.AddChild(CreateSeparator());
            
            // Контейнер рекомендаций по картам
            _cardRecommendationsContainer = new VBoxContainer
            {
                Name = "CardRecsContainer",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            mainContainer.AddChild(_cardRecommendationsContainer);
            
            // Разделитель
            mainContainer.AddChild(CreateSeparator());
            
            // Прогноз добора
            _drawPredictionLabel = new RichTextLabel
            {
                Name = "DrawPredictionLabel",
                BbcodeEnabled = true,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            _drawPredictionLabel.AddThemeFontSizeOverride("normal_font_size", 12);
            mainContainer.AddChild(_drawPredictionLabel);
            
            _mainPanel.AddChild(mainContainer);
            AddChild(_mainPanel);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{LogPrefix} UI initialization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Создание разделителя
    /// </summary>
    private Control CreateSeparator()
    {
        var separator = new Control
        {
            CustomMinimumSize = new Vector2(0, 2)
        };
        
        var style = new StyleBoxFlat { BgColor = new Color(0.3f, 0.3f, 0.4f, 0.5f) };
        separator.AddThemeStyleboxOverride("panel", style);
        
        return separator;
    }

    /// <summary>
    /// Обновление видимости оверлея
    /// </summary>
    public void SetVisible(bool visible)
    {
        _isVisible = visible;
        if (_mainPanel != null)
            _mainPanel.Visible = visible;
    }

    /// <summary>
    /// Обновление рекомендаций в UI
    /// </summary>
    public void UpdateRecommendations(Recommendations recommendations)
    {
        if (!_isVisible || _mainPanel == null)
            return;
        
        _currentRecommendations = recommendations;
        
        try
        {
            // Обновление ситуационной оценки
            UpdateSituationLabel(recommendations.SituationAssessment);
            
            // Обновление рекомендаций по картам
            UpdateCardRecommendations(recommendations.CardRecommendations);
            
            // Обновление прогноза добора
            UpdateDrawPrediction(recommendations.DrawPrediction);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{LogPrefix} Failed to update UI: {ex.Message}");
        }
    }

    /// <summary>
    /// Обновление ситуационной метки
    /// </summary>
    private void UpdateSituationLabel(SituationAssessment assessment)
    {
        if (_situationLabel == null) return;
        
        var healthColor = assessment.HealthStatus switch
        {
            HealthStatus.Good => "#4ade80",
            HealthStatus.Moderate => "#fbbf24",
            HealthStatus.Dangerous => "#fb923c",
            HealthStatus.Critical => "#f87171",
            _ => "#ffffff"
        };
        
        var threatColor = assessment.ThreatLevel switch
        {
            ThreatLevel.Low => "#4ade80",
            ThreatLevel.Moderate => "#fbbf24",
            ThreatLevel.High => "#fb923c",
            ThreatLevel.Critical => "#f87171",
            _ => "#ffffff"
        };
        
        var bbcode = $"[color={healthColor}]{assessment.GetSummary()}[/color]\n" +
                     $"[color=#a78bfa]Стиль: {assessment.SuggestedPlaystyle}[/color]";
        
        _situationLabel.Text = bbcode;
    }

    /// <summary>
    /// Обновление рекомендаций по картам
    /// </summary>
    private void UpdateCardRecommendations(List<CardRecommendation> cardRecs)
    {
        if (_cardRecommendationsContainer == null) return;
        
        // Очистка старых рекомендаций
        foreach (var child in _cardRecommendationsContainer.GetChildren())
        {
            child.QueueFree();
        }
        
        // Добавление топ-3 рекомендаций
        foreach (var rec in cardRecs.Take(3))
        {
            var cardPanel = CreateCardRecommendationPanel(rec);
            _cardRecommendationsContainer.AddChild(cardPanel);
        }
    }

    /// <summary>
    /// Создание панели рекомендации карты
    /// </summary>
    private Control CreateCardRecommendationPanel(CardRecommendation rec)
    {
        var panel = new PanelContainer();
        
        var style = new StyleBoxFlat
        {
            BgColor = rec.Priority switch
            {
                RecommendationPriority.High => new Color(0.2f, 0.3f, 0.2f, 0.6f),
                RecommendationPriority.Medium => new Color(0.25f, 0.25f, 0.2f, 0.5f),
                _ => new Color(0.15f, 0.15f, 0.2f, 0.4f)
            },
            CornerRadius = 5
        };
        panel.AddThemeStyleboxOverride("panel", style);
        
        var container = new VBoxContainer();
        panel.AddChild(container);
        
        // Название карты и скор
        var title = new Label
        {
            Text = $"{rec.CardName} [color=#fbbf24](Score: {rec.Score:F1})[/color]"
        };
        title.AddThemeFontSizeOverride("font_size", 14);
        container.AddChild(title);
        
        // Причина
        if (!string.IsNullOrEmpty(rec.Reason))
        {
            var reason = new Label
            {
                Text = rec.Reason,
                Modulate = new Color(0.8f, 0.8f, 0.8f)
            };
            reason.AddThemeFontSizeOverride("font_size", 11);
            container.AddChild(reason);
        }
        
        return panel;
    }

    /// <summary>
    /// Обновление прогноза добора
    /// </summary>
    private void UpdateDrawPrediction(DrawPrediction? prediction)
    {
        if (_drawPredictionLabel == null || prediction == null) return;
        
        var bbcode = "[color=#60a5fa][b]📊 Прогноз добора:[/b][/color]\n";
        
        foreach (var card in prediction.Cards)
        {
            var probabilityColor = card.Probability switch
            {
                >= 0.7 => "#4ade80",
                >= 0.4 => "#fbbf24",
                _ => "#f87171"
            };
            
            bbcode += $"• {card.CardName}: [color={probabilityColor}]{card.FormattedProbability}[/color] (в колоде: {card.CountInDeck})\n";
        }
        
        bbcode += $"\n[color=#9ca3af]Осталось карт: {prediction.RemainingDeckSize}[/color]";
        
        _drawPredictionLabel.Text = bbcode;
    }

    /// <summary>
    /// Очистка текущей подсказки
    /// </summary>
    public void ClearCurrentHint()
    {
        _currentRecommendations = null;
        
        if (_situationLabel != null)
            _situationLabel.Text = "[color=#6b7280]Нет данных...[/color]";
        
        if (_cardRecommendationsContainer != null)
        {
            foreach (var child in _cardRecommendationsContainer.GetChildren())
            {
                child.QueueFree();
            }
        }
        
        if (_drawPredictionLabel != null)
            _drawPredictionLabel.Text = "";
    }

    public override void _ExitTree()
    {
        GD.Print($"{LogPrefix} Overlay UI cleaned up");
        base._ExitTree();
    }
}
