using System;
using System.Collections.Generic;
using System.Linq;
using StS2Assistant.Core;

namespace StS2Assistant.Logic;

/// <summary>
/// Основной движок анализа игровой ситуации.
/// Вычисляет скоринг карт, вероятности добора, оценку угроз.
/// </summary>
public class AnalysisEngine
{
    private static readonly string LogPrefix = "[StS2Assistant.AnalysisEngine]";
    
    /// <summary>
    /// Кэш расчётов вероятностей (для производительности)
    /// </summary>
    private readonly Dictionary<(int N, int K, int n), double> _probabilityCache = new();
    
    /// <summary>
    /// Коэффициенты для формулы скоринга
    /// </summary>
    private const float EnergyCostMultiplier = 1.2f;
    private const float ThreatLevelMultiplier = 0.8f;
    private const float SynergyBonusPerRelic = 0.15f;

    /// <summary>
    /// Расчёт скоринга карты по формуле:
    /// Score = (BaseUtility × SynergyMult) − (EnergyCost × 1.2) + (ThreatLevel × 0.8) + BuildAlignment
    /// </summary>
    public float CalculateScore(CardState card, GameState state, BuildProfile? build)
    {
        if (card == null) return 0f;
        
        // Базовая полезность в зависимости от типа карты
        var baseUtility = CalculateBaseUtility(card, state);
        
        // Множитель синергии с реликвиями и зельями
        var synergyMult = 1.0f + (SynergyBonusPerRelic * CountActiveSynergies(card, state));
        
        // Уровень угрозы от врагов
        var threatLevel = CalculateThreatLevel(state);
        
        // Бонус соответствия билду
        var buildBonus = CalculateBuildAlignment(card, build);
        
        // Финальная формула
        var score = (baseUtility * synergyMult) - (card.Cost * EnergyCostMultiplier) + 
                    (threatLevel * ThreatLevelMultiplier) + buildBonus;
        
        return score;
    }

    /// <summary>
    /// Расчёт базовой полезности карты
    /// </summary>
    private float CalculateBaseUtility(CardState card, GameState state)
    {
        return card.Type switch
        {
            CardType.Attack => CalculateAttackUtility(card, state),
            CardType.Skill => CalculateSkillUtility(card, state),
            CardType.Power => CalculatePowerUtility(card, state),
            CardType.Status => -0.5f, // Статусы обычно вредны
            CardType.Curse => -2.0f,  // Проклятия очень вредны
            _ => 1.0f
        };
    }

    /// <summary>
    /// Полезность атакующей карты
    /// </summary>
    private float CalculateAttackUtility(CardState card, GameState state)
    {
        var damage = card.Damage;
        
        // Учёт уязвимости врага
        foreach (var enemy in state.Enemies)
        {
            if (enemy.Debuffs.Any(b => b.Id.Equals("Vulnerable", StringComparison.OrdinalIgnoreCase)))
                damage = (int)(damage * 1.5f);
        }
        
        // Учёт слабости игрока
        if (state.Player.Debuffs.Any(b => b.Id.Equals("Weak", StringComparison.OrdinalIgnoreCase)))
            damage = (int)(damage * 0.75f);
        
        // Бонус за добивание врага
        foreach (var enemy in state.Enemies)
        {
            if (enemy.CurrentHp <= damage && enemy.CurrentHp > 0)
                return damage * 1.3f; // Бонус за килл
        }
        
        return damage;
    }

    /// <summary>
    /// Полезность карты навыка
    /// </summary>
    private float CalculateSkillUtility(CardState card, GameState state)
    {
        var utility = 0f;
        
        // Блок
        if (card.Block > 0)
        {
            utility += card.Block * 0.8f;
            
            // Бонус за избыточный блок если есть реликвии
            if (state.Player.Relics.Any(r => r.Contains("Plate", StringComparison.OrdinalIgnoreCase)))
                utility += card.Block * 0.2f;
        }
        
        // Добор карт (магическое число)
        if (card.MagicNumber > 0)
        {
            utility += card.MagicNumber * 0.5f;
        }
        
        // Энергия/ресурсы
        if (card.Name.Contains("Energy", StringComparison.OrdinalIgnoreCase) ||
            card.Name.Contains("Draw", StringComparison.OrdinalIgnoreCase))
        {
            utility += 1.5f;
        }
        
        return utility;
    }

    /// <summary>
    /// Полезность карты силы
    /// </summary>
    private float CalculatePowerUtility(CardState card, GameState state)
    {
        // Карты силы имеют высокую ценность в долгих боях
        var baseValue = 2.5f;
        
        // Бонус за раннюю игру
        if (state.TurnNumber <= 3)
            baseValue += 0.5f;
        
        // Бонус против босса
        if (state.Enemies.Any(e => e.IsBoss))
            baseValue += 0.3f;
        
        return baseValue;
    }

    /// <summary>
    /// Подсчёт активных синергий карты с текущим состоянием
    /// </summary>
    private int CountActiveSynergies(CardState card, GameState state)
    {
        var synergies = 0;
        
        // Проверка синергий с реликвиями
        foreach (var relic in state.Player.Relics)
        {
            if (HasSynergyWithRelic(card, relic))
                synergies++;
        }
        
        // Проверка синергий с зельями
        foreach (var potion in state.Player.Potions)
        {
            if (HasSynergyWithPotion(card, potion))
                synergies++;
        }
        
        return synergies;
    }

    /// <summary>
    /// Проверка синергии карты с реликвией
    /// </summary>
    private bool HasSynergyWithRelic(CardState card, string relicId)
    {
        // Примеры синергий (требуют уточнения для StS2)
        if (relicId.Contains("Snecko", StringComparison.OrdinalIgnoreCase) && card.Cost > 2)
            return true; // Snecko Eye любит дорогие карты
        
        if (relicId.Contains("Chemical", StringComparison.OrdinalIgnoreCase) && card.Type == CardType.Skill)
            return true; // Chemical X усиливает навыки
        
        if (relicId.Contains("Toxic", StringComparison.OrdinalIgnoreCase) && 
            card.Name.Contains("Poison", StringComparison.OrdinalIgnoreCase))
            return true; // Toxic Egg и яд
        
        return false;
    }

    /// <summary>
    /// Проверка синергии карты с зельем
    /// </summary>
    private bool HasSynergyWithPotion(CardState card, PotionState potion)
    {
        // Примеры синергий
        if (potion.Id.Contains("Strength", StringComparison.OrdinalIgnoreCase) && card.Type == CardType.Attack)
            return true;
        
        if (potion.Id.Contains("Block", StringComparison.OrdinalIgnoreCase) && card.Block > 0)
            return true;
        
        return false;
    }

    /// <summary>
    /// Расчёт уровня угрозы на основе намерений врагов
    /// Нормализованное значение 0.0-1.0
    /// </summary>
    private float CalculateThreatLevel(GameState state)
    {
        if (state.Enemies.Count == 0)
            return 0f;
        
        var maxThreat = 0f;
        
        foreach (var enemy in state.Enemies)
        {
            if (enemy.CurrentIntent == null)
                continue;
            
            var intent = enemy.CurrentIntent;
            var threat = 0f;
            
            switch (intent.Type)
            {
                case IntentType.Attack:
                    // Ожидаемый урон с учётом блока игрока
                    var expectedDamage = Math.Max(0, intent.Value * intent.HitCount - state.Player.Block);
                    var lethalThreshold = state.Player.CurrentHp * 0.3f;
                    threat = expectedDamage / Math.Max(1, lethalThreshold);
                    break;
                    
                case IntentType.Debuff:
                    threat = 0.4f; // Дебаффы средне опасны
                    break;
                    
                case IntentType.Buff:
                    threat = 0.6f; // Баффы врага опаснее
                    break;
                    
                case IntentType.Special:
                    threat = 0.5f; // Специальные атаки вариативны
                    break;
            }
            
            maxThreat = Math.Max(maxThreat, threat);
        }
        
        // Нормализация 0.0-1.0
        return Math.Min(1.0f, maxThreat);
    }

    /// <summary>
    /// Расчёт соответствия карты выбранному билду
    /// </summary>
    private float CalculateBuildAlignment(CardState card, BuildProfile? build)
    {
        if (build == null)
            return 0f;
        
        // Приоритетные карты билда
        if (build.PriorityCards.Any(c => 
            card.Name.Contains(c, StringComparison.OrdinalIgnoreCase)))
            return 1.5f;
        
        // Синергия со стилем игры
        switch (build.Playstyle?.ToLower())
        {
            case "aggressive":
                if (card.Type == CardType.Attack)
                    return 0.5f;
                break;
                
            case "attrition_control":
                if (card.Name.Contains("Poison", StringComparison.OrdinalIgnoreCase) ||
                    card.Name.Contains("Weak", StringComparison.OrdinalIgnoreCase) ||
                    card.Name.Contains("Vulnerable", StringComparison.OrdinalIgnoreCase))
                    return 0.8f;
                break;
                
            case "combo":
                if (card.Cost <= 1)
                    return 0.4f; // Дешёвые карты для комбо
                break;
        }
        
        return 0f;
    }

    /// <summary>
    /// Расчёт вероятности добора нужной карты (гипергеометрическое распределение)
    /// P(X=k) = C(K,k) × C(N-K, n-k) / C(N, n)
    /// </summary>
    /// <param name="deckSize">Размер колоды добора (N)</param>
    /// <param name="targetCards">Количество нужных карт в колоде (K)</param>
    /// <param name="drawSize">Размер добора (n)</param>
    /// <returns>Вероятность добора хотя бы одной нужной карты</returns>
    public double CalculateDrawProbability(int deckSize, int targetCards, int drawSize)
    {
        if (deckSize <= 0 || targetCards <= 0 || drawSize <= 0)
            return 0.0;
        
        if (targetCards > deckSize)
            return 1.0;
        
        var cacheKey = (deckSize, targetCards, drawSize);
        if (_probabilityCache.TryGetValue(cacheKey, out var cached))
            return cached;
        
        // Вероятность НЕ вытянуть ни одну нужную карту
        var probabilityOfNone = HypergeometricNone(deckSize, targetCards, drawSize);
        
        // Вероятность вытянуть хотя бы одну
        var result = 1.0 - probabilityOfNone;
        
        _probabilityCache[cacheKey] = result;
        return result;
    }

    /// <summary>
    /// Гипергеометрическая вероятность вытянуть 0 нужных карт
    /// </summary>
    private double HypergeometricNone(int N, int K, int n)
    {
        // C(N-K, n) / C(N, n)
        return Combinations(N - K, n) / Combinations(N, n);
    }

    /// <summary>
    /// Вычисление биномиального коэффициента C(n, k)
    /// </summary>
    private double Combinations(int n, int k)
    {
        if (k < 0 || k > n) return 0;
        if (k == 0 || k == n) return 1;
        
        k = Math.Min(k, n - k);
        double result = 1;
        
        for (int i = 0; i < k; i++)
        {
            result = result * (n - i) / (i + 1);
        }
        
        return result;
    }

    /// <summary>
    /// Прогноз добора на следующий ход
    /// </summary>
    public DrawPrediction PredictNextDraw(GameState state, int nextDrawSize = 5)
    {
        var predictions = new List<CardPrediction>();
        
        // Группировка карт в колоде по типам/именам
        var cardGroups = state.Player.DrawPile
            .GroupBy(c => c.Name)
            .ToDictionary(g => g.Key, g => g.Count());
        
        foreach (var kvp in cardGroups)
        {
            var probability = CalculateDrawProbability(
                state.Player.DrawPile.Count,
                kvp.Value,
                nextDrawSize
            );
            
            predictions.Add(new CardPrediction
            {
                CardName = kvp.Key,
                Probability = probability,
                CountInDeck = kvp.Value
            });
        }
        
        // Сортировка по убыванию вероятности
        predictions.Sort((a, b) => b.Probability.CompareTo(a.Probability));
        
        return new DrawPrediction
        {
            Cards = predictions.Take(5).ToList(), // Топ-5 наиболее вероятных
            ExpectedDrawSize = nextDrawSize,
            RemainingDeckSize = state.Player.DrawPile.Count
        };
    }

    /// <summary>
    /// Рекомендации по контр-игре против намерений врага
    /// </summary>
    public IEnumerable<CounterplayRecommendation> GetCounterplayRecommendations(GameState state)
    {
        var recommendations = new List<CounterplayRecommendation>();
        
        foreach (var enemy in state.Enemies)
        {
            if (enemy.CurrentIntent == null)
                continue;
            
            var intent = enemy.CurrentIntent;
            
            switch (intent.Type)
            {
                case IntentType.Attack:
                    var expectedDamage = intent.Value * intent.HitCount;
                    var playerDefense = state.Player.Block + state.Player.CurrentHp;
                    
                    if (expectedDamage > playerDefense * 0.7f)
                    {
                        recommendations.Add(new CounterplayRecommendation
                        {
                            Priority = RecommendationPriority.High,
                            Message = $"⚠️ Враг нанесёт ~{expectedDamage} урона. Рекомендуется блок или неуязвимость.",
                            SuggestedActions = new[] { "Play block cards", "Use defensive potions", "Apply Weak to enemy" }
                        });
                    }
                    break;
                    
                case IntentType.Buff:
                    recommendations.Add(new CounterplayRecommendation
                    {
                        Priority = RecommendationPriority.Medium,
                        Message = $"🔮 Враг планирует бафф. Рассмотрите прерывание или контроль.",
                        SuggestedActions = new[] { "Use interrupt abilities", "Apply Vulnerable", "Save burst damage" }
                    });
                    break;
                    
                case IntentType.Debuff:
                    recommendations.Add(new CounterplayRecommendation
                    {
                        Priority = RecommendationPriority.Medium,
                        Message = $"☠️ Враг наложит дебафф. Используйте очищение или предотвратите.",
                        SuggestedActions = new[] { "Use cleanse effects", "Play ethereal cards", "End turn early" }
                    });
                    break;
            }
        }
        
        return recommendations.OrderByDescending(r => r.Priority);
    }
}

/// <summary>
/// Предсказание добора карт
/// </summary>
public record DrawPrediction
{
    public List<CardPrediction> Cards { get; init; } = new();
    public int ExpectedDrawSize { get; init; }
    public int RemainingDeckSize { get; init; }
}

/// <summary>
/// Предсказание для отдельной карты
/// </summary>
public record CardPrediction
{
    public string CardName { get; init; } = string.Empty;
    public double Probability { get; init; }
    public int CountInDeck { get; init; }
    
    public string FormattedProbability => $"{Probability:P1}";
}

/// <summary>
/// Рекомендация по контр-игре
/// </summary>
public record CounterplayRecommendation
{
    public RecommendationPriority Priority { get; init; }
    public string Message { get; init; } = string.Empty;
    public IEnumerable<string> SuggestedActions { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Приоритет рекомендации
/// </summary>
public enum RecommendationPriority
{
    Low,
    Medium,
    High,
    Critical
}
