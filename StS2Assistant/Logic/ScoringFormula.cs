// StS2Assistant - Scoring Formula Implementation
// Implements the card scoring formula with all components

using System;
using System.Collections.Generic;
using System.Linq;
using StS2Assistant.Core;

namespace StS2Assistant.Logic;

/// <summary>
/// Implements the card scoring formula:
/// Score = (BaseUtility × SynergyMult) − (EnergyCost × 1.2) + (ThreatLevel × 0.8) + BuildAlignment
/// </summary>
public class ScoringFormula
{
    // Base utility values by card type
    private static readonly Dictionary<CardType, float> BaseUtilityByType = new()
    {
        { CardType.Attack, 3.0f },
        { CardType.Skill, 2.5f },
        { CardType.Power, 4.0f },
        { CardType.Status, 0.5f },
        { CardType.Curse, -2.0f },
        { CardType.Unknown, 1.0f }
    };

    /// <summary>
    /// Calculate base utility from card stats and type
    /// </summary>
    public float CalculateBaseUtility(CardData card, GameState gameState)
    {
        float baseValue = BaseUtilityByType.GetValueOrDefault(card.Type, 1.0f);
        
        // Add value from damage
        if (card.BaseDamage > 0)
        {
            baseValue += card.BaseDamage * 0.3f;
            
            // Bonus for lethal damage
            foreach (var enemy in gameState.Enemies)
            {
                if (!enemy.IsDead && enemy.Hp <= card.BaseDamage)
                {
                    baseValue += 2.0f; // Bonus for potential kill
                    break;
                }
            }
        }
        
        // Add value from block
        if (card.BaseBlock > 0)
        {
            baseValue += card.BaseBlock * 0.25f;
        }
        
        // Add value from magic number (varies by card)
        if (card.BaseMagicNumber > 0)
        {
            baseValue += card.BaseMagicNumber * 0.2f;
        }
        
        // Ethereal penalty (can't play easily)
        if (card.IsEthereal)
        {
            baseValue -= 1.0f;
        }
        
        // Exhaust bonus (one-time powerful effect usually)
        if (card.IsExhausted)
        {
            baseValue += 1.5f;
        }
        
        // Innate bonus (available immediately)
        if (card.IsInnate)
        {
            baseValue += 0.5f;
        }
        
        return baseValue;
    }

    /// <summary>
    /// Calculate synergy multiplier based on active synergies
    /// Formula: 1.0 + 0.15 × number of active synergies
    /// </summary>
    public float CalculateSynergyMultiplier(CardData card, GameState gameState)
    {
        float synergyCount = 0f;
        
        // Check synergy with hand cards
        foreach (var handCard in gameState.HandCards)
        {
            if (handCard.Id == card.Id)
                continue;
                
            if (HasSynergy(card, handCard))
            {
                synergyCount += 0.5f;
            }
        }
        
        // Check synergy with relics
        foreach (var relic in gameState.Relics)
        {
            if (HasRelicSynergy(card, relic))
            {
                synergyCount += 1.0f;
            }
        }
        
        // Check synergy with buffs
        foreach (var buff in gameState.PlayerBuffs)
        {
            if (HasBuffSynergy(card, buff))
            {
                synergyCount += 0.75f;
            }
        }
        
        return 1.0f + (0.15f * synergyCount);
    }

    /// <summary>
    /// Check if two cards have synergy
    /// </summary>
    private bool HasSynergy(CardData card1, CardData card2)
    {
        // Same tag synergy
        var commonTags = card1.Tags.Intersect(card2.Tags).ToList();
        if (commonTags.Count > 0)
            return true;
        
        // Specific card combinations
        var synergyPairs = new HashSet<(string, string)>
        {
            ("Catalyst", "Poison Ivy"),
            ("Burst", "Well-Laid Plans"),
            ("Combo Strike", "Setup"),
            ("Defend", "Juggernaut")
        };
        
        return synergyPairs.Contains((card1.Id, card2.Id)) || 
               synergyPairs.Contains((card2.Id, card1.Id));
    }

    /// <summary>
    /// Check if card has synergy with a relic
    /// </summary>
    private bool HasRelicSynergy(CardData card, RelicData relic)
    {
        // Poison synergies
        if (card.Tags.Contains("poison"))
        {
            var poisonRelics = new[] { "Snecko Eye", "Chemical X", "Poison Ivy" };
            if (poisonRelics.Contains(relic.Name))
                return true;
        }
        
        // Attack synergies
        if (card.Type == CardType.Attack)
        {
            var attackRelics = new[] { "Letter Opener", "Shuriken", "Orichalcum" };
            if (attackRelics.Contains(relic.Name))
                return true;
        }
        
        // Skill synergies
        if (card.Type == CardType.Skill)
        {
            var skillRelics = new[] { "Happy Flower", "Bag of Preparation", "Anchor" };
            if (skillRelics.Contains(relic.Name))
                return true;
        }
        
        // Power synergies
        if (card.Type == CardType.Power)
        {
            var powerRelics = new[] { "Coffee Dripper", "Sozu", "Calling Bell" };
            if (powerRelics.Contains(relic.Name))
                return true;
        }
        
        return false;
    }

    /// <summary>
    /// Check if card has synergy with a buff
    /// </summary>
    private bool HasBuffSynergy(CardData card, BuffData buff)
    {
        // Strength synergy with attacks
        if (buff.Name.Contains("Strength", StringComparison.OrdinalIgnoreCase) && card.Type == CardType.Attack)
            return true;
        
        // Dexterity synergy with skills
        if (buff.Name.Contains("Dexterity", StringComparison.OrdinalIgnoreCase) && card.Type == CardType.Skill)
            return true;
        
        // Artifact synergy with status/curse cards
        if (buff.Name.Contains("Artifact", StringComparison.OrdinalIgnoreCase) && 
            (card.Type == CardType.Status || card.Type == CardType.Curse))
            return true;
        
        return false;
    }

    /// <summary>
    /// Calculate build alignment bonus (+0.5 to +2.0 if card matches build priority)
    /// </summary>
    public float CalculateBuildAlignment(CardData card, BuildProfile build)
    {
        float bonus = 0f;
        
        // Priority cards get maximum bonus
        if (build.PriorityCards.Contains(card.Id))
        {
            bonus += 2.0f;
        }
        else if (build.PriorityCards.Any(pc => pc.Contains(card.Name, StringComparison.OrdinalIgnoreCase)))
        {
            bonus += 1.5f;
        }
        
        // Check tag alignment
        foreach (var priorityTag in build.PriorityTags)
        {
            if (card.Tags.Contains(priorityTag))
            {
                bonus += 0.5f;
            }
        }
        
        // Check type alignment
        if (build.PreferredTypes.Contains(card.Type))
        {
            bonus += 0.3f;
        }
        
        // Penalty for off-build cards (if risk tolerance is low)
        if (build.RiskTolerance < 0.3f && !IsCoreCard(card, build))
        {
            bonus -= 0.5f;
        }
        
        return Math.Max(0f, bonus);
    }

    /// <summary>
    /// Check if card is considered core to the build
    /// </summary>
    private bool IsCoreCard(CardData card, BuildProfile build)
    {
        // Core if it's a priority card or matches multiple criteria
        int matchCount = 0;
        
        if (build.PriorityCards.Contains(card.Id))
            matchCount++;
        
        if (build.PreferredTypes.Contains(card.Type))
            matchCount++;
        
        if (card.Tags.Any(t => build.PriorityTags.Contains(t)))
            matchCount++;
        
        return matchCount >= 2;
    }

    /// <summary>
    /// Get expected value of a card considering future draws
    /// </summary>
    public float CalculateExpectedValue(CardData card, GameState gameState, int turnsAhead = 2)
    {
        float currentValue = CalculateBaseUtility(card, gameState) * CalculateSynergyMultiplier(card, gameState);
        
        // Factor in future turn potential
        float futureBonus = 0f;
        
        if (card.Type == CardType.Power)
        {
            // Powers gain value each turn
            futureBonus = currentValue * turnsAhead * 0.3f;
        }
        else if (card.IsExhausted)
        {
            // Exhaust cards lose future value
            futureBonus = -currentValue * 0.2f;
        }
        
        return currentValue + futureBonus;
    }
}
