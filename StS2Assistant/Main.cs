using Godot;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using StS2Assistant.Core;
using StS2Assistant.Logic;
using StS2Assistant.UI;
using StS2Assistant.Config;

namespace StS2Assistant;

/// <summary>
/// Точка входа мода для Slay the Spire 2.
/// Наследуется от Godot.Node и инициализирует все компоненты мода.
/// </summary>
public partial class Main : Node
{
    private static Main _instance = null!;
    public static Main Instance => _instance ?? throw new InvalidOperationException("Main not initialized");

    private Harmony? _harmony;
    private StateTracker? _stateTracker;
    private AnalysisEngine? _analysisEngine;
    private RecommendationSystem? _recommendationSystem;
    private OverlayUI? _overlayUI;
    private ConfigManager? _configManager;
    
    private bool _isOverlayVisible = true;
    private bool _isDebugEnabled = false;
    
    /// <summary>
    /// Текущее состояние игры (обновляется через StateTracker)
    /// </summary>
    public GameState CurrentState { get; private set; } = GameState.Empty;
    
    /// <summary>
    /// Активный профиль билда
    /// </summary>
    public BuildProfile? ActiveBuild { get; private set; }

    public override void _Ready()
    {
        _instance = this;
        
        GD.Print("[StS2Assistant] === Initializing StS2 Assistant Mod ===");
        
        try
        {
            // Инициализация конфигурации
            _configManager = new ConfigManager();
            _configManager.LoadConfig();
            
            // Загрузка профиля билда по умолчанию
            LoadDefaultBuild();
            
            // Инициализация Harmony для патчинга
            InitializeHarmony();
            
            // Инициализация трекера состояния
            _stateTracker = new StateTracker();
            _stateTracker.Initialize();
            
            // Инициализация движка анализа
            _analysisEngine = new AnalysisEngine();
            
            // Инициализация системы рекомендаций
            _recommendationSystem = new RecommendationSystem(_analysisEngine);
            
            // Инициализация UI
            _overlayUI = new OverlayUI();
            GetTree().Root.AddChild(_overlayUI);
            
            // Подписка на сигналы Godot
            SetupSignals();
            
            GD.Print("[StS2Assistant] === Initialization Complete ===");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[StS2Assistant] Critical error during initialization: {ex}");
        }
    }

    public override void _Process(double delta)
    {
        if (!_isOverlayVisible || _overlayUI == null)
            return;
        
        // Обновление UI с рекомендациями (не каждый кадр, а с ограничением)
        if (Engine.GetFramesDrawn() % 10 == 0)
        {
            UpdateRecommendations();
        }
    }

    public override void _Input(InputEvent @event)
    {
        base._Input(@event);
        
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            // F8 - toggle оверлея
            if (keyEvent.Keycode == Key.F8)
            {
                _isOverlayVisible = !_isOverlayVisible;
                _overlayUI?.SetVisible(_isOverlayVisible);
                GD.Print($"[StS2Assistant] Overlay visibility: {_isOverlayVisible}");
            }
            
            // F9 - вкл/выкл отладки
            if (keyEvent.Keycode == Key.F9)
            {
                _isDebugEnabled = !_isDebugEnabled;
                GD.Print($"[StS2Assistant] Debug mode: {_isDebugEnabled}");
            }
            
            // Esc - сброс подсказки
            if (keyEvent.Keycode == Key.Escape)
            {
                _overlayUI?.ClearCurrentHint();
            }
        }
    }

    /// <summary>
    /// Инициализация Harmony для патчинга методов игры
    /// </summary>
    private void InitializeHarmony()
    {
        try
        {
            _harmony = new Harmony("com.yourname.sts2assistant");
            
            GD.Print("[StS2Assistant] Harmony initialized successfully");
            
            // Патчинг будет выполнен через StateTracker
            // Методы для патчинга (примеры на основе StS1, требуют уточнения для StS2):
            // - Card.Play() -> OnCardPlayed
            // - AbstractPlayer.Draw(int) -> OnCardDrawn
            // - AbstractMonster.ApplyTurnIntent() -> OnEnemyIntentChange
            // - RewardScreen.Open() -> OnRewardScreenOpen
            
            _stateTracker?.ApplyPatches(_harmony);
            
            GD.Print("[StS2Assistant] Harmony patches applied");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[StS2Assistant] Harmony initialization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Настройка подписок на сигналы Godot/BaseLib
    /// </summary>
    private void SetupSignals()
    {
        // Пример подписки на сигналы BaseLib (если существуют)
        // В StS2 могут быть другие сигналы, требуется адаптация
        
        try
        {
            // Сигнал начала боя
            // SignalBus.Connect("battle_start", Callable.From(OnBattleStart));
            
            // Сигнал конца хода
            // SignalBus.Connect("turn_end", Callable.From(OnTurnEnd));
            
            GD.Print("[StS2Assistant] Signals configured");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[StS2Assistant] Signal setup warning: {ex.Message}");
        }
    }

    /// <summary>
    /// Загрузка профиля билда по умолчанию
    /// </summary>
    private void LoadDefaultBuild()
    {
        try
        {
            var defaultBuildPath = _configManager?.GetBuildPath("poison_silent.json");
            if (defaultBuildPath != null && System.IO.File.Exists(defaultBuildPath))
            {
                ActiveBuild = _configManager.LoadBuild("poison_silent.json");
                GD.Print($"[StS2Assistant] Loaded build profile: {ActiveBuild?.Name}");
            }
            else
            {
                // Профиль по умолчанию в коде
                ActiveBuild = new BuildProfile
                {
                    Name = "Generic Aggro",
                    PriorityCards = new List<string> { "Strike", "Defend" },
                    SynergyRelics = new List<string>(),
                    Playstyle = "aggressive",
                    RiskTolerance = 0.5f
                };
                GD.Print("[StS2Assistant] Using default generic build profile");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[StS2Assistant] Failed to load build profile: {ex.Message}");
        }
    }

    /// <summary>
    /// Обновление рекомендаций в UI
    /// </summary>
    private void UpdateRecommendations()
    {
        if (_analysisEngine == null || _recommendationSystem == null || _overlayUI == null)
            return;
        
        try
        {
            // Асинхронный расчёт рекомендаций
            Task.Run(() =>
            {
                var recommendations = _recommendationSystem.GenerateRecommendations(
                    CurrentState, 
                    ActiveBuild
                );
                
                // Передача результата в главный поток Godot
                InvokeOnMainThread(() =>
                {
                    _overlayUI.UpdateRecommendations(recommendations);
                });
            });
        }
        catch (Exception ex)
        {
            if (_isDebugEnabled)
                GD.PrintErr($"[StS2Assistant] Recommendation update error: {ex.Message}");
        }
    }

    /// <summary>
    /// Выполнение действия в главном потоке Godot
    /// </summary>
    private void InvokeOnMainThread(Action action)
    {
        if (IsInsideTree())
        {
            CallDeferred(nameof(ExecuteAction), action);
        }
        else
        {
            action.Invoke();
        }
    }

    [Obsolete("Используется только для CallDeferred")]
    private void ExecuteAction(Action action)
    {
        action?.Invoke();
    }

    /// <summary>
    /// Обновление состояния из StateTracker
    /// </summary>
    internal void UpdateGameState(GameState newState)
    {
        CurrentState = newState;
        
        if (_isDebugEnabled)
        {
            GD.Print($"[StS2Assistant] Game state updated: Energy={newState.PlayerEnergy}, Hand={newState.PlayerHand?.Count ?? 0}");
        }
    }

    public override void _ExitTree()
    {
        try
        {
            _harmony?.UnpatchSelf();
            GD.Print("[StS2Assistant] Harmony patches removed");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[StS2Assistant] Cleanup error: {ex.Message}");
        }
        
        base._ExitTree();
    }
}
