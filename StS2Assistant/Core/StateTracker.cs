using Godot;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StS2Assistant.Core;

namespace StS2Assistant.Core;

/// <summary>
/// Отвечает за отслеживание состояния игры через Harmony-патчи и Godot сигналы.
/// Обновляет GameState при изменениях в игре.
/// </summary>
public partial class StateTracker : Node
{
    private static readonly string LogPrefix = "[StS2Assistant.StateTracker]";
    
    /// <summary>
    /// Кэш рефлексии для быстрого доступа к типам StS2
    /// </summary>
    private ReflectionCache _reflectionCache = null!;
    
    /// <summary>
    /// Текущее отслеживаемое состояние
    /// </summary>
    private GameState _currentState = GameState.Empty;
    
    /// <summary>
    /// Флаг инициализации
    /// </summary>
    private bool _isInitialized = false;

    public override void _Ready()
    {
        _reflectionCache = new ReflectionCache();
    }

    /// <summary>
    /// Инициализация трекера
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized)
        {
            GD.Print($"{LogPrefix} Already initialized");
            return;
        }
        
        try
        {
            _reflectionCache.Initialize();
            GD.Print($"{LogPrefix} Initialization complete");
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{LogPrefix} Initialization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Применение Harmony-патчей
    /// </summary>
    public void ApplyPatches(Harmony harmony)
    {
        if (harmony == null)
        {
            GD.PrintErr($"{LogPrefix} Harmony instance is null");
            return;
        }
        
        try
        {
            // Патч начала боя
            PatchMethod(harmony, "AbstractBattle.Start", typeof(StateTracker), nameof(OnBattleStart_Postfix));
            
            // Патч конца боя
            PatchMethod(harmony, "AbstractBattle.End", typeof(StateTracker), nameof(OnBattleEnd_Postfix));
            
            // Патч начала хода игрока
            PatchMethod(harmony, "GameActions.TurnAction.StartTurnAction", typeof(StateTracker), nameof(OnPlayerTurnStart_Postfix));
            
            // Патч конца хода игрока
            PatchMethod(harmony, "GameActions.TurnAction.EndTurnAction", typeof(StateTracker), nameof(OnPlayerTurnEnd_Postfix));
            
            // Патч розыгрыша карты
            PatchMethod(harmony, "Card.Use", typeof(StateTracker), nameof(OnCardPlayed_Postfix));
            
            // Патч добора карты
            PatchMethod(harmony, "AbstractPlayer.Draw", typeof(StateTracker), nameof(OnCardDrawn_Postfix));
            
            // Патч изменения намерения врага
            PatchMethod(harmony, "AbstractMonster.ApplyTurnIntent", typeof(StateTracker), nameof(OnEnemyIntentChange_Postfix));
            
            // Патч открытия экрана наград
            PatchMethod(harmony, "RewardScreen.Open", typeof(StateTracker), nameof(OnRewardScreenOpen_Postfix));
            
            GD.Print($"{LogPrefix} Applied {harmony.GetPatchedMethods().Count()} patches");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{LogPrefix} Patch application failed: {ex}");
        }
    }

    /// <summary>
    /// Универсальный метод патчинга с обработкой ошибок
    /// </summary>
    private void PatchMethod(Harmony harmony, string methodName, Type patchType, string patchMethodName)
    {
        try
        {
            // Поиск метода через рефлексию
            var methodInfo = FindMethod(methodName);
            if (methodInfo == null)
            {
                GD.Print($"{LogPrefix} Method not found: {methodName} (это нормально для ранней версии StS2)");
                return;
            }
            
            var patchMethod = patchType.GetMethod(patchMethodName, 
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            
            if (patchMethod == null)
            {
                GD.PrintErr($"{LogPrefix} Patch method not found: {patchMethodName}");
                return;
            }
            
            harmony.Patch(methodInfo, postfix: new HarmonyMethod(patchMethod));
            GD.Print($"{LogPrefix} Patched: {methodName}");
        }
        catch (Exception ex)
        {
            GD.Print($"{LogPrefix} Failed to patch {methodName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Поиск метода по имени с использованием кэша рефлексии
    /// </summary>
    private MethodInfo? FindMethod(string methodName)
    {
        return _reflectionCache.FindMethod(methodName);
    }

    #region Harmony Patches

    // === Battle Events ===
    
    public static void OnBattleStart_Postfix(object __instance)
    {
        GD.Print($"{LogPrefix} Battle started");
        Main.Instance?.UpdateGameState(BuildCurrentState());
    }
    
    public static void OnBattleEnd_Postfix(object __instance, bool __result)
    {
        GD.Print($"{LogPrefix} Battle ended: {(bool)__result}");
    }

    // === Turn Events ===
    
    public static void OnPlayerTurnStart_Postfix()
    {
        GD.Print($"{LogPrefix} Player turn started");
        Main.Instance?.UpdateGameState(BuildCurrentState());
    }
    
    public static void OnPlayerTurnEnd_Postfix()
    {
        GD.Print($"{LogPrefix} Player turn ended");
    }

    // === Card Events ===
    
    public static void OnCardPlayed_Postfix(object __instance)
    {
        var cardName = __instance?.GetType().GetProperty("name")?.GetValue(__instance)?.ToString() ?? "Unknown";
        GD.Print($"{LogPrefix} Card played: {cardName}");
        Main.Instance?.UpdateGameState(BuildCurrentState());
    }
    
    public static void OnCardDrawn_Postfix(object __instance, int amount)
    {
        GD.Print($"{LogPrefix} Cards drawn: {amount}");
        Main.Instance?.UpdateGameState(BuildCurrentState());
    }

    // === Enemy Events ===
    
    public static void OnEnemyIntentChange_Postfix(object __instance)
    {
        var enemyName = __instance?.GetType().GetProperty("name")?.GetValue(__instance)?.ToString() ?? "Unknown";
        GD.Print($"{LogPrefix} Enemy intent changed: {enemyName}");
        Main.Instance?.UpdateGameState(BuildCurrentState());
    }

    // === Reward Events ===
    
    public static void OnRewardScreenOpen_Postfix()
    {
        GD.Print($"{LogPrefix} Reward screen opened");
        Main.Instance?.UpdateGameState(BuildCurrentState());
    }

    #endregion

    /// <summary>
    /// Построение текущего GameState из данных игры
    /// </summary>
    private static GameState BuildCurrentState()
    {
        try
        {
            // Попытка получить данные из игры через рефлексию
            // В реальной реализации нужно использовать BaseLib API или прямую рефлексию
            
            var playerState = BuildPlayerState();
            var enemies = BuildEnemyStates();
            
            return new GameState
            {
                Player = playerState,
                Enemies = enemies,
                TurnNumber = GetTurnNumber(),
                CurrentPhase = GetCurrentPhase(),
                Floor = GetFloor(),
                RoomType = GetRoomType()
            };
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{LogPrefix} Failed to build game state: {ex.Message}");
            return GameState.Empty;
        }
    }

    /// <summary>
    /// Построение состояния игрока
    /// </summary>
    private static PlayerState BuildPlayerState()
    {
        try
        {
            // Пример получения данных через рефлексию
            // В реальности нужно использовать актуальные типы StS2
            
            var playerObj = GetPlayerObject();
            if (playerObj == null)
                return PlayerState.Empty;
            
            return new PlayerState
            {
                CurrentHp = GetProperty<int>(playerObj, "currentHealth") ?? 0,
                MaxHp = GetProperty<int>(playerObj, "maxHealth") ?? 100,
                Block = GetProperty<int>(playerObj, "block") ?? 0,
                Energy = GetProperty<int>(playerObj, "energy") ?? 3,
                MaxEnergy = GetProperty<int>(playerObj, "maxEnergy") ?? 3,
                Hand = GetCardsFromPile(playerObj, "hand"),
                DrawPile = GetCardsFromPile(playerObj, "drawPile"),
                DiscardPile = GetCardsFromPile(playerObj, "discardPile"),
                ExhaustPile = GetCardsFromPile(playerObj, "exhaustPile"),
                Relics = GetRelicIds(playerObj),
                Potions = GetPotions(playerObj),
                Buffs = GetBuffs(playerObj, false),
                Debuffs = GetBuffs(playerObj, true)
            };
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{LogPrefix} Failed to build player state: {ex.Message}");
            return PlayerState.Empty;
        }
    }

    /// <summary>
    /// Построение состояний врагов
    /// </summary>
    private static IReadOnlyList<EnemyState> BuildEnemyStates()
    {
        try
        {
            var enemies = new List<EnemyState>();
            var enemyList = GetEnemyObjects();
            
            if (enemyList == null)
                return enemies;
            
            foreach (var enemyObj in enemyList)
            {
                enemies.Add(new EnemyState
                {
                    Id = GetProperty<string>(enemyObj, "id") ?? string.Empty,
                    Name = GetProperty<string>(enemyObj, "name") ?? "Unknown",
                    CurrentHp = GetProperty<int>(enemyObj, "currentHealth") ?? 0,
                    MaxHp = GetProperty<int>(enemyObj, "maxHealth") ?? 100,
                    Block = GetProperty<int>(enemyObj, "block") ?? 0,
                    CurrentIntent = GetCurrentIntent(enemyObj),
                    IsBoss = GetProperty<bool>(enemyObj, "isBoss") ?? false
                });
            }
            
            return enemies;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{LogPrefix} Failed to build enemy states: {ex.Message}");
            return Array.Empty<EnemyState>();
        }
    }

    #region Helper Methods

    private static object? GetPlayerObject()
    {
        // TODO: Реализовать получение объекта игрока через BaseLib или рефлексию
        return null;
    }

    private static IEnumerable<object>? GetEnemyObjects()
    {
        // TODO: Реализовать получение списка врагов
        return null;
    }

    private static IReadOnlyList<CardState> GetCardsFromPile(object playerObj, string pileName)
    {
        // TODO: Реализовать получение карт из колоды
        return Array.Empty<CardState>();
    }

    private static IReadOnlyList<string> GetRelicIds(object playerObj)
    {
        // TODO: Реализовать получение ID реликвий
        return Array.Empty<string>();
    }

    private static IReadOnlyList<PotionState> GetPotions(object playerObj)
    {
        // TODO: Реализовать получение зелий
        return Array.Empty<PotionState>();
    }

    private static IReadOnlyList<BuffState> GetBuffs(object playerObj, bool isDebuff)
    {
        // TODO: Реализовать получение баффов/дебаффов
        return Array.Empty<BuffState>();
    }

    private static IntentData? GetCurrentIntent(object enemyObj)
    {
        // TODO: Реализовать получение текущего намерения врага
        return null;
    }

    private static int GetTurnNumber()
    {
        // TODO: Реализовать получение номера хода
        return 1;
    }

    private static TurnPhase GetCurrentPhase()
    {
        // TODO: Реализовать определение фазы хода
        return TurnPhase.PlayerTurn;
    }

    private static int GetFloor()
    {
        // TODO: Реализовать получение этажа
        return 1;
    }

    private static RoomType GetRoomType()
    {
        // TODO: Реализовать определение типа комнаты
        return RoomType.Battle;
    }

    private static T? GetProperty<T>(object obj, string propertyName)
    {
        try
        {
            var prop = obj.GetType().GetProperty(propertyName);
            if (prop != null && prop.GetValue(obj) is T value)
                return value;
        }
        catch
        {
            // Игнорируем ошибки рефлексии
        }
        return default;
    }

    #endregion
}
