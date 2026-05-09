// StS2Assistant - Card Highlighter
// Handles visual highlighting of recommended cards in hand

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using StS2Assistant.Core;
using StS2Assistant.Logic;

namespace StS2Assistant.UI;

/// <summary>
/// Manages visual highlighting of cards in the player's hand.
/// Uses Godot tweening for smooth highlight effects.
/// </summary>
public class CardHighlighter
{
    private static readonly string ModTag = "[StS2Assistant.Highlighter]";
    
    // Currently highlighted cards
    private readonly Dictionary<string, Control> _highlightedCards = new();
    private readonly Dictionary<string, Tween> _cardTweens = new();
    
    // Highlight colors
    private static readonly Color BestCardColor = new(0.2f, 0.8f, 0.2f, 0.5f);  // Green
    private static readonly Color GoodCardColor = new(0.8f, 0.6f, 0.2f, 0.4f);  // Gold
    private static readonly Color ThreatColor = new(0.9f, 0.2f, 0.2f, 0.3f);    // Red
    
    /// <summary>
    /// Update card highlights based on current analysis
    /// </summary>
    public void UpdateHighlights(AnalysisEngine analysisEngine, RecommendationSystem recommendationSystem)
    {
        try
        {
            // Get current state from tracker
            var gameState = analysisEngine.GetType()
                .GetField("_stateTracker", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                .GetValue(analysisEngine);
            
            if (gameState == null)
                return;
            
            // This would integrate with actual game UI to find card controls
            // For now, this is a placeholder showing the intended behavior
            
            GD.Print($"{ModTag} Would update highlights for {GetHandCardCount(gameState)} cards");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{ModTag} Error updating highlights: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Highlight a specific card control
    /// </summary>
    public void HighlightCard(Control cardControl, string cardId, float score, bool isBestCard)
    {
        if (cardControl == null)
            return;
        
        // Remove existing tween
        if (_cardTweens.TryGetValue(cardId, out var existingTween))
        {
            existingTween.Kill();
            _cardTweens.Remove(cardId);
        }
        
        // Determine highlight color
        var targetColor = isBestCard ? BestCardColor : 
                         score > 10 ? GoodCardColor : 
                         new Color(0.5f, 0.5f, 0.5f, 0.2f);
        
        // Create tween for smooth animation
        var tween = cardControl.CreateTween();
        tween.SetLoops();
        tween.TweenProperty(cardControl, "modulate:a", targetColor.A, 0.5f);
        tween.TweenProperty(cardControl, "modulate:a", 0.2f, 0.5f);
        
        _cardTweens[cardId] = tween;
        _highlightedCards[cardId] = cardControl;
        
        GD.Print($"{ModTag} Highlighting {cardId} (score: {score:F1}, best: {isBestCard})");
    }
    
    /// <summary>
    /// Remove highlight from a card
    /// </summary>
    public void RemoveHighlight(string cardId)
    {
        if (_cardTweens.TryGetValue(cardId, out var tween))
        {
            tween.Kill();
            _cardTweens.Remove(cardId);
        }
        
        if (_highlightedCards.TryGetValue(cardId, out var control))
        {
            // Reset modulate
            control.Modulate = new Color(1, 1, 1, 1);
            _highlightedCards.Remove(cardId);
        }
    }
    
    /// <summary>
    /// Clear all highlights
    /// </summary>
    public void ClearAllHighlights()
    {
        foreach (var kvp in _cardTweens.ToList())
        {
            kvp.Value.Kill();
        }
        
        foreach (var kvp in _highlightedCards.ToList())
        {
            if (kvp.Value != null)
            {
                kvp.Value.Modulate = new Color(1, 1, 1, 1);
            }
        }
        
        _cardTweens.Clear();
        _highlightedCards.Clear();
        
        GD.Print($"{ModTag} All highlights cleared");
    }
    
    /// <summary>
    /// Apply threat indicator to cards that counter enemy intent
    /// </summary>
    public void MarkThreatResponse(CardData[] cards, IntentType threatType)
    {
        var threatColor = threatType switch
        {
            IntentType.Attack => new Color(0.9f, 0.2f, 0.2f, 0.4f),
            IntentType.Debuff => new Color(0.6f, 0.2f, 0.8f, 0.4f),
            _ => new Color(0.8f, 0.8f, 0.2f, 0.4f)
        };
        
        foreach (var card in cards)
        {
            // Would find and mark the actual card control
            GD.Print($"{ModTag} Marking {card.Name} as threat response");
        }
    }
    
    /// <summary>
    /// Cleanup on mod unload
    /// </summary>
    public void Cleanup()
    {
        ClearAllHighlights();
        GD.Print($"{ModTag} CardHighlighter cleanup complete");
    }
    
    // Helper to get hand card count from gameState object via reflection
    private int GetHandCardCount(object gameState)
    {
        try
        {
            var handCardsProp = gameState.GetType().GetProperty("HandCards");
            if (handCardsProp != null)
            {
                var handCards = handCardsProp.GetValue(gameState) as System.Collections.IEnumerable;
                if (handCards != null)
                {
                    int count = 0;
                    foreach (var _ in handCards) count++;
                    return count;
                }
            }
        }
        catch { }
        return 0;
    }
}
