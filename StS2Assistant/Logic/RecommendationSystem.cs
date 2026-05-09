// StS2Assistant - Recommendation System
// Generates contextual recommendations based on analysis

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StS2Assistant.Core;

namespace StS2Assistant.Logic;

/// <summary>
/// Main recommendation engine that combines analysis results
/// into actionable advice for the player.
/// </summary>
public class RecommendationSystem
{
    private static readonly string ModTag = "[StS2Assistant.Recommendation]";
    
    private readonly AnalysisEngine _analysisEngine;
    private readonly ConfigManager _configManager;
    
    // Cached recommendations
    private List<Recommendation>? _cachedRecommendations;
    private DateTime _lastRecommendationTime;
    private const double RecommendationCacheDuration = 0.5; // seconds

    public RecommendationSystem(AnalysisEngine analysisEngine, ConfigManager configManager)
    {
        _analysisEngine = analysisEngine;
        _configManager = configManager;
        _lastRecommendationTime = DateTime.MinValue;
    }

    /// <summary>
    /// Get all current recommendations based on game state
    /// </summary>
    public async Task<List<Recommendation>> GetRecommendationsAsync(GameState gameState)
    {
        // Check cache
        if (_cachedRecommendations != null && 
            DateTime.Now - _lastRecommendationTime < TimeSpan.FromSeconds(RecommendationCacheDuration))
        {
            return _cachedRecommendations;
        }
        
        return await Task.Run(() => GetRecommendations(gameState));
    }

    /// <summary>
    /// Get recommendations (synchronous version)
    /// </summary>
    public List<Recommendation> GetRecommendations(GameState gameState)
    {
        var recommendations = new List<Recommendation>();
        
        try
        {
            // Get current build profile
            var currentBuild = _configManager.GetCurrentBuildProfile();
            
            // 1. Generate card play recommendations
            recommendations.AddRange(GenerateCardRecommendations(gameState, currentBuild));
            
            // 2. Generate threat response recommendations
            recommendations.AddRange(GenerateThreatResponses(gameState));
            
            // 3. Generate resource management recommendations
            recommendations.AddRange(GenerateResourceRecommendations(gameState));
            
            // 4. Generate build synergy recommendations
            if (currentBuild != null)
            {
                recommendations.AddRange(GenerateBuildSynergyRecommendations(gameState, currentBuild));
            }
            
            // Sort by priority
            recommendations.Sort((a, b) => 
                ((int)b.Priority).CompareTo((int)a.Priority));
            
            _cachedRecommendations = recommendations;
            _lastRecommendationTime = DateTime.Now;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{ModTag} Error generating recommendations: {ex.Message}");
        }
        
        return recommendations;
    }

    /// <summary>
    /// Generate card play recommendations
    /// </summary>
    private List<Recommendation> GenerateCardRecommendations(GameState gameState, BuildProfile? build)
    {
        var recommendations = new List<Recommendation>();
        
        if (gameState.HandCards.Count == 0 || !gameState.IsPlayerTurn)
            return recommendations;
        
        // Score all affordable cards
        var affordableCards = gameState.HandCards
            .Where(c => c.Cost <= gameState.PlayerEnergy)
            .ToList();
        
        if (affordableCards.Count == 0)
        {
            recommendations.Add(new Recommendation(
                Title: "No playable cards",
                Description: "No cards can be played with current energy",
                Priority: RecommendationPriority.Info,
                Category: RecommendationCategory.ResourceManagement,
                SuggestedCards: new List<string>(),
                Reasoning: new List<string> { $"Energy: {gameState.PlayerEnergy}/{gameState.PlayerMaxEnergy}" },
                Confidence: 1.0f,
                TargetEnemyIndex: null
            ));
            return recommendations;
        }
        
        // Get top scored cards
        var scores = affordableCards
            .Select(c => _analysisEngine.CalculateCardScore(c, gameState, build))
            .OrderByDescending(s => s.TotalScore)
            .Take(3)
            .ToList();
        
        if (scores.Count > 0)
        {
            var bestCard = scores[0];
            
            // Determine priority based on situation
            var priority = DetermineRecommendationPriority(bestCard, gameState);
            
            recommendations.Add(new Recommendation(
                Title: $"Play {bestCard.CardName}",
                Description: GetCardPlayDescription(bestCard, gameState),
                Priority: priority,
                Category: RecommendationCategory.CardPlay,
                SuggestedCards: new List<string> { bestCard.CardId },
                Reasoning: bestCard.Reasoning,
                Confidence: Math.Min(1.0f, bestCard.TotalScore / 10f),
                TargetEnemyIndex: GetBestTarget(gameState)
            ));
            
            // Add combo suggestion if multiple cards work well together
            if (scores.Count >= 2 && scores[0].TotalScore + scores[1].TotalScore > 15)
            {
                recommendations.Add(new Recommendation(
                    Title: $"Combo: {scores[0].CardName} + {scores[1].CardName}",
                    Description: "These cards synergize well together",
                    Priority: RecommendationPriority.Medium,
                    Category: RecommendationCategory.CardPlay,
                    SuggestedCards: new List<string> { scores[0].CardId, scores[1].CardId },
                    Reasoning: new List<string> { "Combined score is high" },
                    Confidence: 0.7f,
                    TargetEnemyIndex: null
                ));
            }
        }
        
        return recommendations;
    }

    /// <summary>
    /// Generate threat response recommendations
    /// </summary>
    private List<Recommendation> GenerateThreatResponses(GameState gameState)
    {
        var recommendations = new List<Recommendation>();
        
        foreach (var enemy in gameState.Enemies)
        {
            if (enemy.IsDead || enemy.CurrentIntent == null)
                continue;
            
            var intent = enemy.CurrentIntent;
            
            // High damage attack incoming
            if (intent.Type == IntentType.Attack)
            {
                int rawDamage = intent.Amount * intent.Multiplier;
                if (intent.IsMultiHit)
                    rawDamage *= intent.HitCount;
                
                int damageAfterBlock = Math.Max(0, rawDamage - gameState.PlayerBlock);
                
                // Critical: lethal damage
                if (damageAfterBlock >= gameState.PlayerHp)
                {
                    var blockCards = gameState.HandCards
                        .Where(c => c.BaseBlock > 0 || c.Tags.Contains("block"))
                        .Select(c => c.Id)
                        .ToList();
                    
                    recommendations.Add(new Recommendation(
                        Title: $"⚠️ LETHAL THREAT from {enemy.Name}",
                        Description: $"Enemy will deal ~{rawDamage} damage! You need {damageAfterBlock - gameState.PlayerBlock + 1} more block!",
                        Priority: RecommendationPriority.Critical,
                        Category: RecommendationCategory.ThreatResponse,
                        SuggestedCards: blockCards,
                        Reasoning: new List<string> 
                        { 
                            $"Current HP: {gameState.PlayerHp}",
                            $"Incoming damage: {rawDamage}",
                            $"Current block: {gameState.PlayerBlock}"
                        },
                        Confidence: 1.0f,
                        TargetEnemyIndex: enemy.EnemyIndex
                    ));
                }
                // High: significant damage
                else if (damageAfterBlock > gameState.PlayerHp * 0.3f)
                {
                    var blockCards = gameState.HandCards
                        .Where(c => c.BaseBlock > 0)
                        .Select(c => c.Id)
                        .ToList();
                    
                    recommendations.Add(new Recommendation(
                        Title: $"High threat from {enemy.Name}",
                        Description: $"Consider blocking - enemy deals ~{rawDamage} damage",
                        Priority: RecommendationPriority.High,
                        Category: RecommendationCategory.Defense,
                        SuggestedCards: blockCards,
                        Reasoning: new List<string> 
                        { 
                            $"Expected damage after block: {damageAfterBlock}"
                        },
                        Confidence: 0.9f,
                        TargetEnemyIndex: enemy.EnemyIndex
                    ));
                }
            }
            
            // Debuff incoming - suggest removal/prevention
            if (intent.Type == IntentType.Debuff)
            {
                var debuffPrevention = gameState.HandCards
                    .Where(c => c.Tags.Contains("artifact") || c.Tags.Contains("cleanse"))
                    .Select(c => c.Id)
                    .ToList();
                
                if (debuffPrevention.Count > 0)
                {
                    recommendations.Add(new Recommendation(
                        Title: $"{enemy.Name} applying debuff",
                        Description: "Consider using artifact or cleanse effect",
                        Priority: RecommendationPriority.Medium,
                        Category: RecommendationCategory.ThreatResponse,
                        SuggestedCards: debuffPrevention,
                        Reasoning: new List<string> { "Debuff prevention available" },
                        Confidence: 0.7f,
                        TargetEnemyIndex: enemy.EnemyIndex
                    ));
                }
            }
        }
        
        return recommendations;
    }

    /// <summary>
    /// Generate resource management recommendations
    /// </summary>
    private List<Recommendation> GenerateResourceRecommendations(GameState gameState)
    {
        var recommendations = new List<Recommendation>();
        
        // Low energy warning
        if (gameState.PlayerEnergy == 0 && gameState.HandCards.Count > 0)
        {
            var freeCards = gameState.HandCards
                .Where(c => c.Cost == 0)
                .Select(c => c.Id)
                .ToList();
            
            if (freeCards.Count > 0)
            {
                recommendations.Add(new Recommendation(
                    Title: "No energy remaining",
                    Description: "Consider playing free cards",
                    Priority: RecommendationPriority.Low,
                    Category: RecommendationCategory.ResourceManagement,
                    SuggestedCards: freeCards,
                    Reasoning: new List<string> { "Free cards available" },
                    Confidence: 0.8f,
                    TargetEnemyIndex: null
                ));
            }
        }
        
        // Hand size warning (might discard soon)
        if (gameState.HandCards.Count >= 10)
        {
            recommendations.Add(new Recommendation(
                Title: "Hand nearly full",
                Description: "You have 10+ cards - may discard on end turn",
                Priority: RecommendationPriority.Medium,
                Category: RecommendationCategory.ResourceManagement,
                SuggestedCards: new List<string>(),
                Reasoning: new List<string> { $"Hand size: {gameState.HandCards.Count}" },
                Confidence: 1.0f,
                TargetEnemyIndex: null
            ));
        }
        
        return recommendations;
    }

    /// <summary>
    /// Generate build synergy recommendations
    /// </summary>
    private List<Recommendation> GenerateBuildSynergyRecommendations(GameState gameState, BuildProfile build)
    {
        var recommendations = new List<Recommendation>();
        
        // Check for priority cards in hand
        var priorityCardsInHand = gameState.HandCards
            .Where(c => build.PriorityCards.Contains(c.Id))
            .ToList();
        
        if (priorityCardsInHand.Count > 0)
        {
            recommendations.Add(new Recommendation(
                Title: "Build card available!",
                Description: $"You have {priorityCardsInHand.Count} priority card(s) for {build.Name}",
                Priority: RecommendationPriority.High,
                Category: RecommendationCategory.BuildSynergy,
                SuggestedCards: priorityCardsInHand.Select(c => c.Id).ToList(),
                Reasoning: new List<string> { "Matches your selected build" },
                Confidence: 1.0f,
                TargetEnemyIndex: null
            ));
        }
        
        // Check for missing key pieces
        var missingPriorityCards = build.PriorityCards
            .Except(gameState.HandCards.Select(c => c.Id))
            .Except(gameState.DrawPile.Select(c => c.Id))
            .ToList();
        
        if (missingPriorityCards.Count > 0 && gameState.DrawPile.Count > 0)
        {
            // Calculate probability of drawing them
            float drawProb = _analysisEngine.CalculateDrawProbability(missingPriorityCards[0], gameState);
            
            if (drawProb > 0.5f)
            {
                recommendations.Add(new Recommendation(
                    Title: "Key card might be drawn soon",
                    Description: $"Chance to draw priority cards next turn: {drawProb * 100:F0}%",
                    Priority: RecommendationPriority.Info,
                    Category: RecommendationCategory.BuildSynergy,
                    SuggestedCards: missingPriorityCards,
                    Reasoning: new List<string> { "Good draw probability" },
                    Confidence: drawProb,
                    TargetEnemyIndex: null
                ));
            }
        }
        
        return recommendations;
    }

    /// <summary>
    /// Determine recommendation priority based on card score and situation
    /// </summary>
    private RecommendationPriority DetermineRecommendationPriority(CardScore score, GameState gameState)
    {
        if (score.TotalScore >= 15)
            return RecommendationPriority.Critical;
        if (score.TotalScore >= 10)
            return RecommendationPriority.High;
        if (score.TotalScore >= 5)
            return RecommendationPriority.Medium;
        if (score.TotalScore >= 2)
            return RecommendationPriority.Low;
        return RecommendationPriority.Info;
    }

    /// <summary>
    /// Get description for card play recommendation
    /// </summary>
    private string GetCardPlayDescription(CardScore score, GameState gameState)
    {
        var parts = new List<string>();
        
        if (score.BaseUtility > 5)
            parts.Add("High impact");
        
        if (score.SynergyMultiplier > 1.3f)
            parts.Add("Strong synergy");
        
        if (score.EnergyPenalty == 0)
            parts.Add("Free!");
        
        if (score.BuildAlignmentBonus > 1f)
            parts.Add("Perfect for build");
        
        return parts.Count > 0 ? string.Join(", ", parts) : "Solid play";
    }

    /// <summary>
    /// Get best target enemy index for attacks
    /// </summary>
    private int? GetBestTarget(GameState gameState)
    {
        // Target lowest HP enemy for kills
        var weakEnemy = gameState.Enemies
            .Where(e => !e.IsDead)
            .OrderBy(e => e.Hp)
            .FirstOrDefault();
        
        return weakEnemy?.EnemyIndex;
    }

    /// <summary>
    /// Get reward recommendations for reward screen
    /// </summary>
    public List<Recommendation> GetRewardRecommendations(List<RewardData> rewards, BuildProfile? build)
    {
        var recommendations = new List<Recommendation>();
        
        if (build == null || rewards.Count == 0)
            return recommendations;
        
        foreach (var reward in rewards)
        {
            float score = 0f;
            var reasoning = new List<string>();
            
            if (reward.Type == RewardType.Card && reward.Card != null)
            {
                // Score card rewards based on build alignment
                score = _analysisEngine.CalculateCardScore(reward.Card, CreateDummyState(), build).TotalScore;
                
                if (build.PriorityCards.Contains(reward.Card.Id))
                {
                    reasoning.Add("Priority card for your build!");
                    score += 5f;
                }
            }
            else if (reward.Type == RewardType.Relic && reward.Relic != null)
            {
                // Score relic rewards
                if (build.SynergyRelics.Contains(reward.Relic.Name))
                {
                    reasoning.Add("Perfect synergy relic!");
                    score = 20f;
                }
                else
                {
                    score = 5f; // Default relic value
                }
            }
            
            var priority = score switch
            {
                >= 15 => RecommendationPriority.Critical,
                >= 10 => RecommendationPriority.High,
                >= 5 => RecommendationPriority.Medium,
                _ => RecommendationPriority.Low
            };
            
            recommendations.Add(new Recommendation(
                Title: GetRewardTitle(reward),
                Description: string.Join(". ", reasoning),
                Priority: priority,
                Category: RecommendationCategory.RewardSelection,
                SuggestedCards: new List<string>(),
                Reasoning: reasoning,
                Confidence: Math.Min(1f, score / 20f),
                TargetEnemyIndex: null
            ));
        }
        
        return recommendations;
    }

    private string GetRewardTitle(RewardData reward)
    {
        return reward.Type switch
        {
            RewardType.Card => reward.Card?.Name ?? "Unknown Card",
            RewardType.Relic => reward.Relic?.Name ?? "Unknown Relic",
            RewardType.Gold => $"{reward.Gold?.Amount} Gold",
            RewardType.RemoveCard => "Remove a card",
            RewardType.SmithHammer => "Upgrade a card",
            RewardType.RestSite => "Rest site",
            _ => "Unknown reward"
        };
    }

    /// <summary>
    /// Create a dummy game state for reward evaluation (when not in combat)
    /// </summary>
    private GameState CreateDummyState()
    {
        return new GameState(
            PlayerHp: 50,
            PlayerMaxHp: 75,
            PlayerBlock: 0,
            PlayerEnergy: 3,
            PlayerMaxEnergy: 3,
            HandCards: new List<CardData>(),
            DrawPile: new List<CardData>(),
            DiscardPile: new List<CardData>(),
            ExhaustPile: new List<CardData>(),
            PlayedThisTurn: new List<CardData>(),
            Relics: new List<RelicData>(),
            Potions: new List<PotionData>(),
            PlayerBuffs: new List<BuffData>(),
            PlayerDebuffs: new List<BuffData>(),
            Enemies: new List<EnemyData>(),
            CurrentTurn: 1,
            IsPlayerTurn: true,
            CardsPlayedThisTurn: 0,
            CurrentRoomType: "Reward",
            FloorNumber: 10,
            LastUpdated: DateTime.Now
        );
    }

    /// <summary>
    /// Clear cached recommendations
    /// </summary>
    public void ResetCache()
    {
        _cachedRecommendations = null;
        _lastRecommendationTime = DateTime.MinValue;
        GD.Print($"{ModTag} Recommendation cache reset");
    }
}
