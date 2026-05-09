using System;
using System.Collections.Generic;
using System.Linq;
using StS2Assistant.Core;

namespace StS2Assistant.Logic;

/// <summary>
/// Система генерации рекомендаций на основе анализа состояния.
/// Объединяет данные от AnalysisEngine в понятные подсказки для игрока.
/// </summary>
public class RecommendationSystem
{
    private static readonly string LogPrefix = "[StS2Assistant.RecommendationSystem]";
    
    private readonly AnalysisEngine _analysisEngine;

    public RecommendationSystem(AnalysisEngine analysisEngine)
    {
        _analysisEngine = analysisEngine ?? throw new ArgumentNullException(nameof(analysisEngine));
    }

    /// <summary>
    /// Генерация полного набора рекомендаций для текущего состояния
    /// </summary>
    public Recommendations GenerateRecommendations(GameState state, BuildProfile? build)
    {
        var recommendations = new Recommendations();
        
        if (state == null || state.Player.Hand.Count == 0)
            return recommendations;
        
        try
        {
            // 1. Рекомендации по картам в руке
            recommendations.CardRecommendations = GenerateCardRecommendations(state, build);
            
            // 2. Прогноз добора
            recommendations.DrawPrediction = _analysisEngine.PredictNextDraw(state);
            
            // 3. Контр-стратегии против врагов
            recommendations.CounterplayTips = _analysisEngine.GetCounterplayRecommendations(state).ToList();
            
            // 4. Общая оценка ситуации
            recommendations.SituationAssessment = AssessSituation(state, build);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{LogPrefix} Error generating recommendations: {ex.Message}");
        }
        
        return recommendations;
    }

    /// <summary>
    /// Генерация рекомендаций по картам в руке
    /// </summary>
    private List<CardRecommendation> GenerateCardRecommendations(GameState state, BuildProfile? build)
    {
        var cardRecs = new List<CardRecommendation>();
        
        foreach (var card in state.Player.Hand)
        {
            var score = _analysisEngine.CalculateScore(card, state, build);
            
            cardRecs.Add(new CardRecommendation
            {
                CardId = card.Id,
                CardName = card.Name,
                Score = score,
                Reason = GenerateCardReason(card, score, state, build),
                Priority = GetPriorityFromScore(score)
            });
        }
        
        // Сортировка по убыванию скоринга
        cardRecs.Sort((a, b) => b.Score.CompareTo(a.Score));
        
        return cardRecs;
    }

    /// <summary>
    /// Генерация текстового обоснования для карты
    /// </summary>
    private string GenerateCardReason(CardState card, float score, GameState state, BuildProfile? build)
    {
        var reasons = new List<string>();
        
        // Высокий скоринг
        if (score >= 5.0f)
            reasons.Add("Отличный выбор");
        else if (score >= 3.0f)
            reasons.Add("Хороший ход");
        else if (score >= 1.0f)
            reasons.Add("Средне");
        else
            reasons.Add("Низкий приоритет");
        
        // Синергии
        var synergyCount = CountSynergies(card, state);
        if (synergyCount > 0)
            reasons.Add($"{synergyCount} синергий");
        
        // Соответствие билду
        if (build != null && build.PriorityCards.Any(c => 
            card.Name.Contains(c, StringComparison.OrdinalIgnoreCase)))
            reasons.Add("Приоритет билда");
        
        // Угроза
        var threat = _analysisEngine.GetType().GetMethod("CalculateThreatLevel", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        return string.Join(", ", reasons);
    }

    /// <summary>
    /// Подсчёт синергий карты
    /// </summary>
    private int CountSynergies(CardState card, GameState state)
    {
        var count = 0;
        
        foreach (var relic in state.Player.Relics)
        {
            if (HasRelicSynergy(card, relic))
                count++;
        }
        
        return count;
    }

    /// <summary>
    /// Проверка синергии с реликвией
    /// </summary>
    private bool HasRelicSynergy(CardState card, string relicId)
    {
        // Примеры синергий
        if (relicId.Contains("Snecko", StringComparison.OrdinalIgnoreCase) && card.Cost >= 3)
            return true;
        
        if (relicId.Contains("Chemical", StringComparison.OrdinalIgnoreCase) && card.Type == CardType.Skill)
            return true;
        
        return false;
    }

    /// <summary>
    /// Определение приоритета из скоринга
    /// </summary>
    private RecommendationPriority GetPriorityFromScore(float score)
    {
        if (score >= 5.0f) return RecommendationPriority.High;
        if (score >= 3.0f) return RecommendationPriority.Medium;
        if (score >= 1.0f) return RecommendationPriority.Low;
        return RecommendationPriority.Low;
    }

    /// <summary>
    /// Общая оценка ситуации
    /// </summary>
    private SituationAssessment AssessSituation(GameState state, BuildProfile? build)
    {
        var assessment = new SituationAssessment();
        
        // Оценка здоровья
        var hpPercent = (float)state.Player.CurrentHp / Math.Max(1, state.Player.MaxHp);
        assessment.HealthStatus = hpPercent switch
        {
            >= 0.75f => HealthStatus.Good,
            >= 0.5f => HealthStatus.Moderate,
            >= 0.25f => HealthStatus.Dangerous,
            _ => HealthStatus.Critical
        };
        
        // Оценка энергии
        assessment.EnergyAvailable = state.Player.Energy;
        assessment.EnergyStatus = state.Player.Energy switch
        {
            >= 3 => EnergyStatus.Good,
            >= 2 => EnergyStatus.Moderate,
            _ => EnergyStatus.Low
        };
        
        // Оценка угроз
        var maxEnemyIntent = state.Enemies
            .Select(e => e.CurrentIntent?.Value ?? 0)
            .Max();
        
        assessment.ThreatLevel = maxEnemyIntent switch
        {
            >= 20 => ThreatLevel.Critical,
            >= 12 => ThreatLevel.High,
            >= 6 => ThreatLevel.Moderate,
            _ => ThreatLevel.Low
        };
        
        // Рекомендация по стилю игры
        assessment.SuggestedPlaystyle = DetermineSuggestedPlaystyle(state, build);
        
        return assessment;
    }

    /// <summary>
    /// Определение рекомендуемого стиля игры
    /// </summary>
    private PlayStyleHint DetermineSuggestedPlaystyle(GameState state, BuildProfile? build)
    {
        var hpPercent = (float)state.Player.CurrentHp / Math.Max(1, state.Player.MaxHp);
        
        // Если мало HP - играем осторожно
        if (hpPercent < 0.3f)
            return PlayStyleHint.Defensive;
        
        // Если есть возможность убить врага - агрессивно
        foreach (var enemy in state.Enemies)
        {
            var totalDamage = state.Player.Hand
                .Where(c => c.Type == CardType.Attack)
                .Sum(c => c.Damage);
            
            if (totalDamage >= enemy.CurrentHp && state.Player.Energy >= 2)
                return PlayStyleHint.Aggressive;
        }
        
        // По умолчанию - согласно билду
        return build?.Playstyle?.ToLower() switch
        {
            "aggressive" => PlayStyleHint.Aggressive,
            "attrition_control" => PlayStyleHint.Control,
            "combo" => PlayStyleHint.Combo,
            _ => PlayStyleHint.Balanced
        };
    }
}

/// <summary>
/// Полный набор рекомендаций
/// </summary>
public record Recommendations
{
    /// <summary>
    /// Рекомендации по картам в руке
    /// </summary>
    public List<CardRecommendation> CardRecommendations { get; init; } = new();
    
    /// <summary>
    /// Прогноз добора на следующий ход
    /// </summary>
    public DrawPrediction? DrawPrediction { get; init; }
    
    /// <summary>
    /// Советы по контр-игре против врагов
    /// </summary>
    public List<CounterplayRecommendation> CounterplayTips { get; init; } = new();
    
    /// <summary>
    /// Общая оценка ситуации
    /// </summary>
    public SituationAssessment SituationAssessment { get; init; } = new();
}

/// <summary>
/// Рекомендация по конкретной карте
/// </summary>
public record CardRecommendation
{
    public string CardId { get; init; } = string.Empty;
    public string CardName { get; init; } = string.Empty;
    public float Score { get; init; }
    public string Reason { get; init; } = string.Empty;
    public RecommendationPriority Priority { get; init; }
}

/// <summary>
/// Оценка ситуации
/// </summary>
public record SituationAssessment
{
    public HealthStatus HealthStatus { get; init; }
    public EnergyStatus EnergyStatus { get; init; }
    public ThreatLevel ThreatLevel { get; init; }
    public int EnergyAvailable { get; init; }
    public PlayStyleHint SuggestedPlaystyle { get; init; }
    
    public string GetSummary()
    {
        var healthEmoji = HealthStatus switch
        {
            HealthStatus.Good => "💚",
            HealthStatus.Moderate => "💛",
            HealthStatus.Dangerous => "🧡",
            HealthStatus.Critical => "❤️‍🩹"
        };
        
        var threatEmoji = ThreatLevel switch
        {
            ThreatLevel.Low => "😌",
            ThreatLevel.Moderate => "😐",
            ThreatLevel.High => "😨",
            ThreatLevel.Critical => "😱"
        };
        
        return $"{healthEmoji} HP | {threatEmoji} Угроза | ⚡ {EnergyAvailable} энергии";
    }
}

/// <summary>
/// Статус здоровья
/// </summary>
public enum HealthStatus
{
    Good,
    Moderate,
    Dangerous,
    Critical
}

/// <summary>
/// Статус энергии
/// </summary>
public enum EnergyStatus
{
    Good,
    Moderate,
    Low
}

/// <summary>
/// Уровень угрозы
/// </summary>
public enum ThreatLevel
{
    Low,
    Moderate,
    High,
    Critical
}

/// <summary>
/// Подсказка по стилю игры
/// </summary>
public enum PlayStyleHint
{
    Aggressive,
    Defensive,
    Control,
    Combo,
    Balanced
}
