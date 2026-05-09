// StS2Assistant - Card Analysis Engine
// Implements card scoring and evaluation logic

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StS2Assistant.Core;

namespace StS2Assistant.Logic;

/// <summary>
/// Analyzes cards and game state to generate scores and recommendations.
/// Uses the scoring formula from requirements:
/// Score = (BaseUtility × SynergyMult) − (EnergyCost × 1.2) + (ThreatLevel × 0.8) + BuildAlignment
/// </summary>
public class AnalysisEngine
{
    private static readonly string ModTag = "[StS2Assistant.Analysis]";
    
    private readonly StateTracker _stateTracker;
    private readonly ScoringFormula _scoringFormula;
    private readonly DrawProbability _drawProbability;
    
    // Cached calculations to avoid recomputing every frame
    private Dictionary<string, float> _cachedCardScores = new();
    private Dictionary<string, float> _cachedDrawProbabilities = new();
    private DateTime _lastCacheUpdate;
    private const double CacheDurationSeconds = 1.0;

    public AnalysisEngine(StateTracker stateTracker)
    {
        _stateTracker = stateTracker;
        _scoringFormula = new ScoringFormula();
        _drawProbability = new DrawProbability();
        _lastCacheUpdate = DateTime.MinValue;
    }

    /// <summary>
    /// Analyze all cards in hand and return scored recommendations
    /// </summary>
    public async Task<List<CardScore>> AnalyzeHandCards(GameState gameState, BuildProfile? currentBuild = null)
    {
        if (gameState.HandCards.Count == 0)
            return new List<CardScore>();
        
        // Check cache validity
        if (DateTime.Now - _lastCacheUpdate < TimeSpan.FromSeconds(CacheDurationSeconds))
        {
            // Return cached results if still valid
            return GetCachedScores(gameState.HandCards);
        }
        
        // Calculate scores asynchronously for heavy computations
        return await Task.Run(() =>
        {
            var scores = new List<CardScore>();
            
            foreach (var card in gameState.HandCards)
            {
                try
                {
                    var score = CalculateCardScore(card, gameState, currentBuild);
                    scores.Add(score);
                    
                    // Cache the score
                    _cachedCardScores[card.Id] = score.TotalScore;
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"{ModTag} Error scoring card {card.Name}: {ex.Message}");
                }
            }
            
            // Sort by score descending
            scores.Sort((a, b) => b.TotalScore.CompareTo(a.TotalScore));
            
            _lastCacheUpdate = DateTime.Now;
            return scores;
        });
    }

    /// <summary>
    /// Calculate score for a single card using the formula
    /// </summary>
    public CardScore CalculateCardScore(CardData card, GameState gameState, BuildProfile? build = null)
    {
        // Base utility based on card type and stats
        float baseUtility = _scoringFormula.CalculateBaseUtility(card, gameState);
        
        // Synergy multiplier based on active synergies with other cards/relics
        float synergyMult = _scoringFormula.CalculateSynergyMultiplier(card, gameState);
        
        // Energy cost penalty
        float energyCost = Math.Max(0, card.Cost);
        float energyPenalty = energyCost * 1.2f;
        
        // Threat level from enemies
        float threatLevel = CalculateThreatLevel(gameState);
        float threatBonus = threatLevel * 0.8f;
        
        // Build alignment bonus
        float buildAlignment = build != null ? _scoringFormula.CalculateBuildAlignment(card, build) : 0f;
        
        // Final score calculation
        float totalScore = (baseUtility * synergyMult) - energyPenalty + threatBonus + buildAlignment;
        
        return new CardScore(
            CardId: card.Id,
            CardName: card.Name,
            TotalScore: totalScore,
            BaseUtility: baseUtility,
            SynergyMultiplier: synergyMult,
            EnergyPenalty: energyPenalty,
            ThreatBonus: threatBonus,
            BuildAlignmentBonus: buildAlignment,
            Reasoning: GenerateReasoning(card, gameState, build)
        );
    }

    /// <summary>
    /// Calculate normalized threat level (0.0 - 1.0) based on enemy intents
    /// </summary>
    private float CalculateThreatLevel(GameState gameState)
    {
        if (gameState.Enemies.Count == 0)
            return 0f;
        
        float maxThreat = 0f;
        
        foreach (var enemy in gameState.Enemies)
        {
            if (enemy.IsDead || enemy.CurrentIntent == null)
                continue;
            
            var intent = enemy.CurrentIntent;
            float expectedDamage = 0f;
            
            switch (intent.Type)
            {
                case IntentType.Attack:
                    // Calculate expected damage after block
                    int rawDamage = intent.Amount * intent.Multiplier;
                    if (intent.IsMultiHit)
                        rawDamage *= intent.HitCount;
                    
                    int damageAfterBlock = Math.Max(0, rawDamage - gameState.PlayerBlock);
                    expectedDamage = damageAfterBlock;
                    break;
                    
                case IntentType.KillPlayer:
                    expectedDamage = gameState.PlayerHp + 100; // Maximum threat
                    break;
                    
                case IntentType.Debuff:
                    expectedDamage = gameState.PlayerMaxHp * 0.15f; // Estimate debuff value
                    break;
            }
            
            // Normalize by player max HP
            float normalizedThreat = Math.Min(1f, expectedDamage / gameState.PlayerMaxHp);
            maxThreat = Math.Max(maxThreat, normalizedThreat);
        }
        
        return maxThreat;
    }

    /// <summary>
    /// Get cached scores for cards
    /// </summary>
    private List<CardScore> GetCachedScores(IReadOnlyList<CardData> cards)
    {
        var scores = new List<CardScore>();
        
        foreach (var card in cards)
        {
            if (_cachedCardScores.TryGetValue(card.Id, out var score))
            {
                scores.Add(new CardScore(
                    CardId: card.Id,
                    CardName: card.Name,
                    TotalScore: score,
                    BaseUtility: 0,
                    SynergyMultiplier: 0,
                    EnergyPenalty: 0,
                    ThreatBonus: 0,
                    BuildAlignmentBonus: 0,
                    Reasoning: new List<string> { "Cached result" }
                ));
            }
        }
        
        return scores;
    }

    /// <summary>
    /// Generate human-readable reasoning for card score
    /// </summary>
    private List<string> GenerateReasoning(CardData card, GameState gameState, BuildProfile? build)
    {
        var reasoning = new List<string>();
        
        // Type-based reasoning
        switch (card.Type)
        {
            case CardType.Attack:
                if (gameState.Enemies.Any(e => !e.IsDead && e.Hp <= card.BaseDamage))
                    reasoning.Add("Can finish off an enemy");
                break;
                
            case CardType.Skill:
                if (card.BaseBlock > 0)
                {
                    var threat = CalculateThreatLevel(gameState);
                    if (threat > 0.5f)
                        reasoning.Add("Good defensive option against high threat");
                }
                break;
                
            case CardType.Power:
                reasoning.Add("Long-term value investment");
                break;
        }
        
        // Cost-based reasoning
        if (card.Cost > gameState.PlayerEnergy)
        {
            reasoning.Add($"Cannot afford ({card.Cost} energy, have {gameState.PlayerEnergy})");
        }
        else if (card.Cost == 0)
        {
            reasoning.Add("Free card - good value");
        }
        
        // Build alignment
        if (build != null && build.PriorityCards.Contains(card.Id))
        {
            reasoning.Add("Matches build priority!");
        }
        
        // Synergy notes
        if (card.Tags.Contains("poison") && HasPoisonSynergy(gameState))
        {
            reasoning.Add("Strong poison synergy active");
        }
        
        return reasoning;
    }

    /// <summary>
    /// Check if player has poison-related synergies
    /// </summary>
    private bool HasPoisonSynergy(GameState gameState)
    {
        // Check for poison relics
        var poisonRelics = new[] { "Snecko Eye", "Chemical X", "Poison Ivy" };
        return gameState.Relics.Any(r => poisonRelics.Contains(r.Name));
    }

    /// <summary>
    /// Calculate probability of drawing specific cards next turn
    /// </summary>
    public float CalculateDrawProbability(string cardId, GameState gameState)
    {
        var cacheKey = $"{cardId}_{gameState.DrawPile.Count}_{gameState.CurrentTurn}";
        
        if (_cachedDrawProbabilities.TryGetValue(cacheKey, out var cached))
            return cached;
        
        // Count copies of card in draw pile
        int cardsInDeck = gameState.DrawPile.Count(c => c.Id == cardId);
        int deckSize = gameState.DrawPile.Count + gameState.DiscardPile.Count;
        int handSizeNextTurn = 5; // Standard draw
        
        if (deckSize == 0 || cardsInDeck == 0)
            return 0f;
        
        // Use hypergeometric distribution
        float probability = _drawProbability.CalculateProbability(
            populationSize: deckSize,
            successStates: cardsInDeck,
            draws: handSizeNextTurn,
            desiredSuccesses: 1
        );
        
        _cachedDrawProbabilities[cacheKey] = probability;
        return probability;
    }

    /// <summary>
    /// Get best card to play right now
    /// </summary>
    public CardScore? GetBestCardToPlay(GameState gameState, BuildProfile? build = null)
    {
        var affordableCards = gameState.HandCards
            .Where(c => c.Cost <= gameState.PlayerEnergy)
            .ToList();
        
        if (affordableCards.Count == 0)
            return null;
        
        var scores = affordableCards
            .Select(c => CalculateCardScore(c, gameState, build))
            .OrderByDescending(s => s.TotalScore)
            .ToList();
        
        return scores.FirstOrDefault();
    }

    /// <summary>
    /// Clear all cached calculations
    /// </summary>
    public void ClearCache()
    {
        _cachedCardScores.Clear();
        _cachedDrawProbabilities.Clear();
        _lastCacheUpdate = DateTime.MinValue;
        GD.Print($"{ModTag} Analysis cache cleared");
    }
}

/// <summary>
/// Card score with detailed breakdown
/// </summary>
public sealed record CardScore(
    string CardId,
    string CardName,
    float TotalScore,
    float BaseUtility,
    float SynergyMultiplier,
    float EnergyPenalty,
    float ThreatBonus,
    float BuildAlignmentBonus,
    List<string> Reasoning
);
