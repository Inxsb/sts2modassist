// StS2Assistant - Main Entry Point
// Godot 4.5.1 Mono Mod for Slay the Spire 2

using Godot;
using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using Alchyr.Sts2.BaseLib;
using StS2Assistant.Core;
using StS2Assistant.Logic;
using StS2Assistant.UI;
using StS2Assistant.Config;

namespace StS2Assistant;

/// <summary>
/// Main entry point for the StS2 Assistant mod.
/// Inherits from Godot.Node to integrate with the Godot scene tree.
/// </summary>
public partial class Main : Node
{
    private static readonly string ModTag = "[StS2Assistant]";
    
    // Core components
    private StateTracker? _stateTracker;
    private AnalysisEngine? _analysisEngine;
    private RecommendationSystem? _recommendationSystem;
    private ConfigManager? _configManager;
    
    // UI components
    private OverlayUI? _overlayUI;
    private CardHighlighter? _cardHighlighter;
    
    // Harmony instance for patching
    private Harmony? _harmony;
    
    // State flags
    private bool _isEnabled = true;
    private bool _isInitialized;
    
    // Hotkey timing
    private double _lastHotkeyTime;
    private const double HotkeyCooldown = 0.3f;

    public override void _Ready()
    {
        GD.Print($"{ModTag} Initializing StS2 Assistant v1.0.0...");
        
        try
        {
            // Initialize configuration manager
            _configManager = new ConfigManager();
            _configManager.LoadConfigurations();
            
            // Initialize Harmony for patching
            _harmony = new Harmony("com.yourname.sts2assistant");
            GD.Print($"{ModTag} Harmony instance created: {_harmony.Id}");
            
            // Initialize state tracker (sets up Harmony patches)
            _stateTracker = new StateTracker();
            _stateTracker.Initialize(_harmony);
            GD.Print($"{ModTag} State tracker initialized with Harmony patches");
            
            // Initialize analysis engine
            _analysisEngine = new AnalysisEngine(_stateTracker);
            GD.Print($"{ModTag} Analysis engine initialized");
            
            // Initialize recommendation system
            _recommendationSystem = new RecommendationSystem(_analysisEngine, _configManager);
            GD.Print($"{ModTag} Recommendation system initialized");
            
            // Setup UI (will be added to scene when in battle)
            SetupUI();
            
            _isInitialized = true;
            GD.Print($"{ModTag} Initialization complete!");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{ModTag} Initialization failed: {ex.Message}");
            GD.PrintErr(ex.StackTrace);
            _isInitialized = false;
        }
    }

    public override void _Process(double delta)
    {
        if (!_isInitialized || !_isEnabled)
            return;
        
        // Handle hotkey input
        HandleHotkeys();
        
        // Update UI overlay if visible
        if (_overlayUI != null && _overlayUI.Visible)
        {
            UpdateOverlay();
        }
        
        // Update card highlights if in battle
        if (_cardHighlighter != null && IsInBattle())
        {
            _cardHighlighter.UpdateHighlights(_analysisEngine!, _recommendationSystem!);
        }
    }

    public override void _ExitTree()
    {
        GD.Print($"{ModTag} Cleaning up...");
        
        // Unpatch all Harmony patches
        if (_harmony != null)
        {
            _harmony.UnpatchSelf();
            GD.Print($"{ModTag} Harmony patches removed");
        }
        
        // Clean up UI
        _overlayUI?.QueueFree();
        _cardHighlighter?.Cleanup();
        
        GD.Print($"{ModTag} Cleanup complete");
    }

    private void SetupUI()
    {
        // Create overlay UI as CanvasLayer (renders on top of everything)
        var canvasLayer = new CanvasLayer
        {
            Layer = 100 // High layer to render above game UI
        };
        
        _overlayUI = new OverlayUI(_configManager!);
        canvasLayer.AddChild(_overlayUI);
        
        // Create card highlighter
        _cardHighlighter = new CardHighlighter();
        
        // Add to scene tree
        AddChild(canvasLayer);
        
        GD.Print($"{ModTag} UI components created");
    }

    private void HandleHotkeys()
    {
        var currentTime = Time.GetTicksMsec() / 1000.0;
        if (currentTime - _lastHotkeyTime < HotkeyCooldown)
            return;
        
        // F8 - Toggle overlay
        if (Input.IsActionJustPressed("ui_accept") || Input.IsKeyPressed(Key.F8))
        {
            // Check for F8 specifically
            if (Input.IsKeyPressed(Key.F8))
            {
                _isEnabled = !_isEnabled;
                if (_overlayUI != null)
                {
                    _overlayUI.Visible = _isEnabled;
                }
                GD.Print($"{ModTag} Overlay {( _isEnabled ? "enabled" : "disabled")}");
                _lastHotkeyTime = currentTime;
            }
        }
        
        // F9 - Debug info toggle
        if (Input.IsKeyPressed(Key.F9))
        {
            if (_overlayUI != null)
            {
                _overlayUI.ToggleDebug();
            }
            _lastHotkeyTime = currentTime;
        }
        
        // Escape - Reset recommendations (when pressed in combination)
        if (Input.IsKeyPressed(Key.Escape) && Input.IsKeyPressed(Key.Ctrl))
        {
            _recommendationSystem?.ResetCache();
            GD.Print($"{ModTag} Recommendation cache reset");
            _lastHotkeyTime = currentTime;
        }
    }

    private void UpdateOverlay()
    {
        if (_overlayUI == null || _stateTracker == null)
            return;
        
        var gameState = _stateTracker.CurrentState;
        if (gameState == null)
            return;
        
        // Get current recommendations
        var recommendations = _recommendationSystem?.GetRecommendations(gameState);
        
        // Update overlay with current data
        _overlayUI.UpdateDisplay(gameState, recommendations);
    }

    private bool IsInBattle()
    {
        // Check if we're currently in a battle scene
        // This uses BaseLib utilities if available, otherwise falls back to scene checking
        try
        {
            // Attempt to use BaseLib's battle state detection
            var battleScene = BaseLibUtils.GetCurrentBattleScene();
            return battleScene != null;
        }
        catch
        {
            // Fallback: check scene tree for battle-related nodes
            var currentScene = GetTree().CurrentScene;
            return currentScene?.Name.Contains("Battle", StringComparison.OrdinalIgnoreCase) ?? false;
        }
    }

    /// <summary>
    /// Called by StateTracker when game state changes
    /// </summary>
    public void OnGameStateUpdated()
    {
        if (_overlayUI != null && _isEnabled)
        {
            // Force UI refresh on next frame
            CallDeferred(nameof(UpdateOverlay));
        }
    }
}

/// <summary>
/// Utility class for accessing BaseLib functionality safely
/// </summary>
public static class BaseLibUtils
{
    private static Type? _battleSceneType;
    private static MethodInfo? _getInstanceMethod;
    
    /// <summary>
    /// Attempts to get the current battle scene using BaseLib
    /// </summary>
    public static object? GetCurrentBattleScene()
    {
        try
        {
            // Try to find BattleScene type through reflection
            _battleSceneType ??= AccessTools.TypeByName("BattleScene");
            
            if (_battleSceneType == null)
                return null;
            
            // Try to get instance via singleton pattern or static property
            _getInstanceMethod ??= _battleSceneType.GetProperty("Instance", 
                BindingFlags.Public | BindingFlags.Static)?.GetMethod;
            
            if (_getInstanceMethod != null)
            {
                return _getInstanceMethod.Invoke(null, null);
            }
            
            // Fallback: try to find via Godot scene tree
            var tree = Engine.GetMainLoop();
            if (tree is SceneTree sceneTree)
            {
                return sceneTree.CurrentScene;
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[StS2Assistant] Failed to get battle scene: {ex.Message}");
        }
        
        return null;
    }
    
    /// <summary>
    /// Safely get player data through BaseLib or reflection
    /// </summary>
    public static object? GetPlayerData()
    {
        try
        {
            var playerType = AccessTools.TypeByName("Player");
            if (playerType == null)
                return null;
                
            var instanceProp = playerType.GetProperty("Instance", 
                BindingFlags.Public | BindingFlags.Static);
            return instanceProp?.GetValue(null);
        }
        catch
        {
            return null;
        }
    }
}
