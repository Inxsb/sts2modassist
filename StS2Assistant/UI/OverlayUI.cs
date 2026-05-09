// StS2Assistant - Overlay UI Component
// Godot 4.x Control node for displaying recommendations

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using StS2Assistant.Core;
using StS2Assistant.Logic;

namespace StS2Assistant.UI;

/// <summary>
/// Main overlay UI that displays recommendations and game info.
/// Uses Godot 4.x Control system with CanvasLayer for rendering above game.
/// </summary>
public partial class OverlayUI : Control
{
    private static readonly string ModTag = "[StS2Assistant.UI]";
    
    // UI Nodes
    private PanelContainer? _mainPanel;
    private RichTextLabel? _recommendationsLabel;
    private RichTextLabel? _gameStateLabel;
    private RichTextLabel? _probabilityLabel;
    private VBoxContainer? _recommendationsContainer;
    private CheckBox? _enabledCheckBox;
    
    // Configuration
    private readonly ConfigManager _configManager;
    private bool _showDebugInfo;
    private bool _isVisible = true;
    
    // Styling
    private const string CriticalColor = "#ff4444";
    private const string HighColor = "#ff8800";
    private const string MediumColor = "#ffcc00";
    private const string LowColor = "#88cc88";
    private const string InfoColor = "#88ccff";

    public OverlayUI(ConfigManager configManager)
    {
        _configManager = configManager;
        _showDebugInfo = false;
    }

    public override void _Ready()
    {
        SetupUI();
    }

    private void SetupUI()
    {
        // Set anchor to top-right corner
        SetAnchorsPreset(LayoutPreset.TopRight);
        
        // Don't block mouse input (let clicks pass through to game)
        MouseFilter = MouseFilterEnum.Ignore;
        
        // Position offset from edge
        Position = new Vector2(-320, 10);
        
        // Create main panel
        _mainPanel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(280, 400),
            MouseFilter = MouseFilterEnum.Ignore
        };
        
        // Create scroll container for content
        var scrollContainer = new ScrollContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalScrollBehavior = ScrollContainer.ScrollBehaviorEnum.Disabled,
            VerticalScrollBehavior = ScrollContainer.ScrollBehaviorEnum.Auto
        };
        
        // Main vertical layout
        var mainLayout = new VBoxContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        
        // Header
        var headerLabel = new Label
        {
            Text = "🎯 StS2 Assistant",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        headerLabel.AddThemeStyleboxOverride("normal", GetHeaderStyle());
        mainLayout.AddChild(headerLabel);
        
        // Separator
        mainLayout.AddChild(CreateSeparator());
        
        // Recommendations section
        var recHeader = new Label { Text = "Recommendations" };
        mainLayout.AddChild(recHeader);
        
        _recommendationsContainer = new VBoxContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        mainLayout.AddChild(_recommendationsContainer);
        
        // Initial recommendation placeholder
        AddRecommendationItem("Waiting for battle data...", RecommendationPriority.Info);
        
        // Separator
        mainLayout.AddChild(CreateSeparator());
        
        // Game state section
        var stateHeader = new Label { Text = "Battle State" };
        mainLayout.AddChild(stateHeader);
        
        _gameStateLabel = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
        };
        _gameStateLabel.Text = "[color=#888888]HP: --/-- | Energy: --/3 | Block: --[/color]";
        mainLayout.AddChild(_gameStateLabel);
        
        // Separator
        mainLayout.AddChild(CreateSeparator());
        
        // Draw probability section
        var probHeader = new Label { Text = "Next Turn Draws" };
        mainLayout.AddChild(probHeader);
        
        _probabilityLabel = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _probabilityLabel.Text = "[color=#888888]--[/color]";
        mainLayout.AddChild(_probabilityLabel);
        
        // Separator
        mainLayout.AddChild(CreateSeparator());
        
        // Controls footer
        var footerContainer = new HBoxContainer();
        
        _enabledCheckBox = new CheckBox
        {
            Text = "Enabled",
            ButtonPressed = true,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _enabledCheckBox.Toggled += OnEnabledToggled;
        footerContainer.AddChild(_enabledCheckBox);
        
        var debugButton = new Button
        {
            Text = "🐛 Debug",
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.Expand
        };
        debugButton.Pressed += () => ToggleDebug();
        footerContainer.AddChild(debugButton);
        
        mainLayout.AddChild(footerContainer);
        
        // Assemble hierarchy
        scrollContainer.AddChild(mainLayout);
        _mainPanel.AddChild(scrollContainer);
        AddChild(_mainPanel);
        
        GD.Print($"{ModTag} UI setup complete");
    }

    private Control CreateSeparator()
    {
        var separator = new HSeparator
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        return separator;
    }

    private StyleBox GetHeaderStyle()
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.2f, 0.3f, 0.5f),
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8
        };
        style.SetContentMarginAll(8);
        return style;
    }

    /// <summary>
    /// Update display with current game state and recommendations
    /// </summary>
    public void UpdateDisplay(GameState gameState, List<Recommendation>? recommendations)
    {
        if (!_isVisible)
            return;
        
        try
        {
            // Update game state label
            UpdateGameStateLabel(gameState);
            
            // Update recommendations
            UpdateRecommendations(recommendations);
            
            // Update draw probabilities
            UpdateDrawProbabilities(gameState);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{ModTag} Error updating UI: {ex.Message}");
        }
    }

    private void UpdateGameStateLabel(GameState gameState)
    {
        if (_gameStateLabel == null)
            return;
        
        var hpPercent = (float)gameState.PlayerHp / gameState.PlayerMaxHp;
        var hpColor = hpPercent switch
        {
            > 0.6f => "#44ff44",
            > 0.3f => "#ffff44",
            _ => "#ff4444"
        };
        
        var bbcode = $"[color={hpColor}]HP: {gameState.PlayerHp}/{gameState.PlayerMaxHp}[/color] | " +
                     $"[color=#44aaff]Energy: {gameState.PlayerEnergy}/{gameState.PlayerMaxEnergy}[/color] | " +
                     $"[color=#aaaa44]Block: {gameState.PlayerBlock}[/color]";
        
        if (_showDebugInfo)
        {
            bbcode += $"\n[color=#888888]Turn: {gameState.CurrentTurn} | " +
                     $"Cards Played: {gameState.CardsPlayedThisTurn} | " +
                     $"Hand: {gameState.HandCards.Count}[/color]";
        }
        
        _gameStateLabel.Text = bbcode;
    }

    private void UpdateRecommendations(List<Recommendation>? recommendations)
    {
        if (_recommendationsContainer == null)
            return;
        
        // Clear existing items
        foreach (var child in _recommendationsContainer.GetChildren())
        {
            _recommendationsContainer.RemoveChild(child);
            child.QueueFree();
        }
        
        if (recommendations == null || recommendations.Count == 0)
        {
            AddRecommendationItem("No recommendations", RecommendationPriority.Info);
            return;
        }
        
        // Show top 5 recommendations
        foreach (var rec in recommendations.Take(5))
        {
            AddRecommendationItem(rec.Title, rec.Priority, rec.Description);
        }
    }

    private void AddRecommendationItem(string title, RecommendationPriority priority, string? description = null)
    {
        if (_recommendationsContainer == null)
            return;
        
        var color = priority switch
        {
            RecommendationPriority.Critical => CriticalColor,
            RecommendationPriority.High => HighColor,
            RecommendationPriority.Medium => MediumColor,
            RecommendationPriority.Low => LowColor,
            _ => InfoColor
        };
        
        var indicator = priority switch
        {
            RecommendationPriority.Critical => "⚠️",
            RecommendationPriority.High => "↑",
            RecommendationPriority.Medium => "→",
            _ => "•"
        };
        
        var itemContainer = new VBoxContainer();
        
        var titleLabel = new RichTextLabel
        {
            BbcodeEnabled = true,
            Text = $"[color={color}]{indicator} {title}[/color]",
            FitContent = true
        };
        itemContainer.AddChild(titleLabel);
        
        if (!string.IsNullOrEmpty(description))
        {
            var descLabel = new Label
            {
                Text = description,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            descLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            itemContainer.AddChild(descLabel);
        }
        
        _recommendationsContainer.AddChild(itemContainer);
    }

    private void UpdateDrawProbabilities(GameState gameState)
    {
        if (_probabilityLabel == null)
            return;
        
        // Calculate probabilities for key cards
        var drawProbability = new DrawProbability();
        
        // Get unique card IDs in deck
        var deckCards = gameState.DrawPile
            .Concat(gameState.DiscardPile)
            .GroupBy(c => c.Id)
            .Select(g => new { CardId = g.Key, Count = g.Count(), Name = g.First().Name })
            .OrderByDescending(c => c.Count)
            .Take(3)
            .ToList();
        
        if (deckCards.Count == 0)
        {
            _probabilityLabel.Text = "[color=#888888]No cards in deck[/color]";
            return;
        }
        
        var bbcodeLines = new List<string>();
        foreach (var card in deckCards)
        {
            float prob = drawProbability.CalculateProbability(
                gameState.DrawPile.Count + gameState.DiscardPile.Count,
                card.Count,
                5 // Standard hand size
            );
            
            var probColor = prob switch
            {
                > 0.7f => "#44ff44",
                > 0.4f => "#ffff44",
                _ => "#ff8844"
            };
            
            bbcodeLines.Add($"[color={probColor}]{card.Name}: {prob * 100:F0}%[/color]");
        }
        
        _probabilityLabel.Text = string.Join("\n", bbcodeLines);
    }

    /// <summary>
    /// Toggle debug information display
    /// </summary>
    public void ToggleDebug()
    {
        _showDebugInfo = !_showDebugInfo;
        GD.Print($"{ModTag} Debug mode: {(_showDebugInfo ? "ON" : "OFF")}");
    }

    private void OnEnabledToggled(bool pressed)
    {
        _isVisible = pressed;
        if (_mainPanel != null)
        {
            _mainPanel.Visible = pressed;
        }
        GD.Print($"{ModTag} UI {(pressed ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Highlight a specific card in the player's hand
    /// Called by CardHighlighter
    /// </summary>
    public void HighlightCard(string cardId, bool highlight)
    {
        // This would integrate with the game's actual card rendering
        // For now, just log the request
        GD.Print($"{ModTag} Highlight request for {cardId}: {highlight}");
    }
}
