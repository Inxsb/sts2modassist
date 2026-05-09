// StS2Assistant - State Tracker with Harmony Patches
// Monitors game events and maintains current game state

using Godot;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace StS2Assistant.Core;

/// <summary>
/// Tracks game state through Harmony patches on game events.
/// Maintains a cached GameState that is updated when relevant events occur.
/// </summary>
public class StateTracker
{
    private static readonly string ModTag = "[StS2Assistant.StateTracker]";
    
    // Current game state (immutable, replaced on updates)
    public GameState? CurrentState { get; private set; }
    
    // Cached game object references
    private object? _playerInstance;
    private object? _battleScene;
    
    // Event subscribers
    public event Action? OnStateUpdated;
    
    // Patch tracking
    private bool _isPatched;
    private Harmony? _harmony;
    
    /// <summary>
    /// Initialize the state tracker with Harmony instance
    /// </summary>
    public void Initialize(Harmony harmony)
    {
        _harmony = harmony;
        ApplyPatches();
        ReflectionCache.PreCacheCommonTypes();
    }
    
    /// <summary>
    /// Apply all Harmony patches for state tracking
    /// </summary>
    private void ApplyPatches()
    {
        if (_isPatched || _harmony == null)
            return;
        
        try
        {
            GD.Print($"{ModTag} Applying Harmony patches...");
            
            // Battle lifecycle patches
            PatchMethod("BattleScene", "BeginBattle", prefix: true);
            PatchMethod("BattleScene", "OnBattleEnd", prefix: false);
            
            // Turn management
            PatchMethod("TurnManager", "StartTurn", prefix: false);
            PatchMethod("TurnManager", "EndTurn", prefix: false);
            
            // Card-related events
            PatchMethod("GameActions", "AddToHandAction", postfix: true);
            PatchMethod("GameActions", "PlayCardAction", prefix: true);
            PatchMethod("GameActions", "PlayCardAction", postfix: true);
            PatchMethod("CardGroup", "MoveToDiscardPile", postfix: true);
            PatchMethod("CardGroup", "MoveToExhaustPile", postfix: true);
            
            // Enemy intent changes
            PatchMethod("AbstractEnemy", "SetIntent", postfix: true);
            PatchMethod("AbstractEnemy", "TakeTurn", prefix: true);
            PatchMethod("AbstractEnemy", "TakeTurn", postfix: true);
            
            // Player state changes
            PatchMethod("AbstractPlayer", "UseEnergy", postfix: true);
            PatchMethod("AbstractPlayer", "LoseHp", postfix: true);
            PatchMethod("AbstractPlayer", "GainBlock", postfix: true);
            
            // Reward screen
            PatchMethod("RewardScreen", "Open", prefix: false);
            PatchMethod("RewardScreen", "Close", prefix: true);
            
            _isPatched = true;
            GD.Print($"{ModTag} Harmony patches applied successfully");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{ModTag} Failed to apply patches: {ex.Message}");
            GD.PrintErr(ex.StackTrace);
            _isPatched = false;
        }
    }
    
    /// <summary>
    /// Helper to patch a method safely
    /// </summary>
    private void PatchMethod(string typeName, string methodName, bool prefix = true, bool postfix = false)
    {
        if (_harmony == null)
            return;
        
        try
        {
            var type = ReflectionCache.GetTypeByName(typeName);
            if (type == null)
            {
                GD.Print($"{ModTag} Skipping patch for {typeName}.{methodName} - type not found");
                return;
            }
            
            var methodInfo = AccessTools.Method(type, methodName);
            if (methodInfo == null)
            {
                GD.Print($"{ModTag} Skipping patch for {typeName}.{methodName} - method not found");
                return;
            }
            
            var harmonyMethod = new HarmonyMethod(typeof(StateTracker).GetMethod(
                prefix ? $"{methodName}_Prefix" : $"{methodName}_Postfix",
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public
            ));
            
            if (harmonyMethod.method == null)
            {
                GD.Print($"{ModTag} Skipping patch for {typeName}.{methodName} - handler not found");
                return;
            }
            
            if (prefix)
            {
                _harmony.Patch(methodInfo, prefix: harmonyMethod);
            }
            else if (postfix)
            {
                _harmony.Patch(methodInfo, postfix: harmonyMethod);
            }
            
            GD.Print($"{ModTag} Patched {typeName}.{methodName}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{ModTag} Failed to patch {typeName}.{methodName}: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Force refresh the current game state from game objects
    /// </summary>
    public void RefreshState()
    {
        try
        {
            var newState = BuildGameState();
            if (newState != null)
            {
                CurrentState = newState;
                OnStateUpdated?.Invoke();
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{ModTag} Error refreshing state: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Build a new GameState from current game data
    /// </summary>
    private GameState? BuildGameState()
    {
        try
        {
            // Get player instance
            _playerInstance ??= BaseLibUtils.GetPlayerData();
            if (_playerInstance == null)
                return null;
            
            // Extract player stats using reflection
            var playerHp = ReflectionCache.GetPropertyValue<int>(_playerInstance, "Player", "CurrentHp", 0);
            var playerMaxHp = ReflectionCache.GetPropertyValue<int>(_playerInstance, "Player", "MaxHp", 100);
            var playerBlock = ReflectionCache.GetPropertyValue<int>(_playerInstance, "Player", "Block", 0);
            var playerEnergy = ReflectionCache.GetPropertyValue<int>(_playerInstance, "Player", "Energy", 3);
            var playerMaxEnergy = ReflectionCache.GetPropertyValue<int>(_playerInstance, "Player", "MaxEnergy", 3);
            
            // Get card zones
            var handCards = GetCardZone("hand") ?? new List<CardData>();
            var drawPile = GetCardZone("drawPile") ?? new List<CardData>();
            var discardPile = GetCardZone("discardPile") ?? new List<CardData>();
            var exhaustPile = GetCardZone("exhaustPile") ?? new List<CardData>();
            var playedThisTurn = GetCardZone("playedThisTurn") ?? new List<CardData>();
            
            // Get relics and potions
            var relics = GetRelics() ?? new List<RelicData>();
            var potions = GetPotions() ?? new List<PotionData>();
            
            // Get buffs/debuffs
            var playerBuffs = GetBuffs(false) ?? new List<BuffData>();
            var playerDebuffs = GetBuffs(true) ?? new List<BuffData>();
            
            // Get enemies
            var enemies = GetEnemies() ?? new List<EnemyData>();
            
            // Get turn info
            var currentTurn = GetCurrentTurn();
            var isPlayerTurn = GetIsPlayerTurn();
            var cardsPlayedThisTurn = GetCardsPlayedThisTurn();
            
            // Get battle metadata
            var roomType = GetCurrentRoomType();
            var floorNumber = GetCurrentFloor();
            
            return new GameState(
                PlayerHp: playerHp,
                PlayerMaxHp: playerMaxHp,
                PlayerBlock: playerBlock,
                PlayerEnergy: playerEnergy,
                PlayerMaxEnergy: playerMaxEnergy,
                HandCards: handCards,
                DrawPile: drawPile,
                DiscardPile: discardPile,
                ExhaustPile: exhaustPile,
                PlayedThisTurn: playedThisTurn,
                Relics: relics,
                Potions: potions,
                PlayerBuffs: playerBuffs,
                PlayerDebuffs: playerDebuffs,
                Enemies: enemies,
                CurrentTurn: currentTurn,
                IsPlayerTurn: isPlayerTurn,
                CardsPlayedThisTurn: cardsPlayedThisTurn,
                CurrentRoomType: roomType,
                FloorNumber: floorNumber,
                LastUpdated: DateTime.Now
            );
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{ModTag} Error building game state: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Get cards from a specific zone
    /// </summary>
    private List<CardData>? GetCardZone(string zoneName)
    {
        try
        {
            if (_playerInstance == null)
                return null;
            
            var cardGroup = ReflectionCache.GetFieldValue<object>(_playerInstance, "Player", zoneName);
            if (cardGroup == null)
                return null;
            
            var cards = new List<CardData>();
            
            // Try to get cards list from CardGroup
            var cardsList = ReflectionCache.GetFieldValue<System.Collections.IList>(cardGroup, "CardGroup", "cards");
            if (cardsList == null)
                return cards;
            
            foreach (var cardObj in cardsList)
            {
                if (cardObj != null)
                {
                    var cardData = ConvertToCardData(cardObj);
                    if (cardData != null)
                        cards.Add(cardData);
                }
            }
            
            return cards;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{ModTag} Error getting card zone '{zoneName}': {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Convert a game card object to CardData
    /// </summary>
    private CardData? ConvertToCardData(object cardObj)
    {
        try
        {
            var cardId = ReflectionCache.GetPropertyValue<string>(cardObj, "AbstractCard", "cardID", "unknown");
            var cardName = ReflectionCache.GetPropertyValue<string>(cardObj, "AbstractCard", "name", "Unknown Card");
            var cost = ReflectionCache.GetPropertyValue<int>(cardObj, "AbstractCard", "costForTurn", -1);
            var cardType = GetCardType(cardObj);
            var rarity = GetCardRarity(cardObj);
            var color = GetCardColor(cardObj);
            var description = ReflectionCache.GetPropertyValue<string>(cardObj, "AbstractCard", "description", "");
            var upgradeLevel = ReflectionCache.GetPropertyValue<int>(cardObj, "AbstractCard", "upgradeLevel", 0);
            var isExhausted = ReflectionCache.GetPropertyValue<bool>(cardObj, "AbstractCard", "isExhausted", false);
            var isEthereal = ReflectionCache.GetPropertyValue<bool>(cardObj, "AbstractCard", "isEthereal", false);
            var isInnate = ReflectionCache.GetPropertyValue<bool>(cardObj, "AbstractCard", "isInnate", false);
            
            var baseDamage = ReflectionCache.GetPropertyValue<float>(cardObj, "AbstractCard", "baseDamage", 0);
            var baseBlock = ReflectionCache.GetPropertyValue<float>(cardObj, "AbstractCard", "baseBlock", 0);
            var baseMagicNumber = ReflectionCache.GetPropertyValue<float>(cardObj, "AbstractCard", "baseMagicNumber", 0);
            
            return new CardData(
                Id: cardId ?? "unknown",
                Name: cardName ?? "Unknown",
                Cost: cost,
                Type: cardType,
                Rarity: rarity,
                Color: color,
                Description: description,
                UpgradeLevel: upgradeLevel,
                IsExhausted: isExhausted,
                IsEthereal: isEthereal,
                IsInnate: isInnate,
                Tags: new List<string>(),
                BaseDamage: baseDamage,
                BaseBlock: baseBlock,
                BaseMagicNumber: baseMagicNumber
            );
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{ModTag} Error converting card: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Determine card type from card object
    /// </summary>
    private CardType GetCardType(object cardObj)
    {
        try
        {
            var typeEnum = ReflectionCache.GetPropertyValue<object>(cardObj, "AbstractCard", "type");
            if (typeEnum != null)
            {
                var typeName = typeEnum.ToString();
                return typeName switch
                {
                    "Attack" => CardType.Attack,
                    "Skill" => CardType.Skill,
                    "Power" => CardType.Power,
                    "Status" => CardType.Status,
                    "Curse" => CardType.Curse,
                    _ => CardType.Unknown
                };
            }
        }
        catch { }
        
        return CardType.Unknown;
    }
    
    /// <summary>
    /// Determine card rarity from card object
    /// </summary>
    private CardRarity GetCardRarity(object cardObj)
    {
        try
        {
            var rarityEnum = ReflectionCache.GetPropertyValue<object>(cardObj, "AbstractCard", "rarity");
            if (rarityEnum != null)
            {
                var rarityName = rarityEnum.ToString();
                return rarityName switch
                {
                    "Basic" => CardRarity.Basic,
                    "Common" => CardRarity.Common,
                    "Uncommon" => CardRarity.Uncommon,
                    "Rare" => CardRarity.Rare,
                    "Special" => CardRarity.Special,
                    _ => CardRarity.Unknown
                };
            }
        }
        catch { }
        
        return CardRarity.Unknown;
    }
    
    /// <summary>
    /// Determine card color from card object
    /// </summary>
    private CardColor GetCardColor(object cardObj)
    {
        try
        {
            var colorEnum = ReflectionCache.GetPropertyValue<object>(cardObj, "AbstractCard", "color");
            if (colorEnum != null)
            {
                var colorName = colorEnum.ToString();
                return colorName switch
                {
                    "Red" => CardColor.Red,
                    "Green" => CardColor.Green,
                    "Blue" => CardColor.Blue,
                    "Purple" => CardColor.Purple,
                    "Colorless" => CardColor.Colorless,
                    "Curse" => CardColor.Curse,
                    _ => CardColor.Unknown
                };
            }
        }
        catch { }
        
        return CardColor.Unknown;
    }
    
    /// <summary>
    /// Get relics from player
    /// </summary>
    private List<RelicData>? GetRelics()
    {
        // Implementation similar to GetCardZone
        // Simplified for brevity - would use reflection to get relic data
        return new List<RelicData>();
    }
    
    /// <summary>
    /// Get potions from player
    /// </summary>
    private List<PotionData>? GetPotions()
    {
        return new List<PotionData>();
    }
    
    /// <summary>
    /// Get player buffs/debuffs
    /// </summary>
    private List<BuffData>? GetBuffs(bool isDebuff)
    {
        return new List<BuffData>();
    }
    
    /// <summary>
    /// Get all enemies in battle
    /// </summary>
    private List<EnemyData>? GetEnemies()
    {
        try
        {
            var enemies = new List<EnemyData>();
            
            // Try to get enemy list from battle scene
            var enemyList = GetEnemyObjects();
            if (enemyList == null)
                return enemies;
            
            int index = 0;
            foreach (var enemyObj in enemyList)
            {
                if (enemyObj != null)
                {
                    var enemyData = ConvertToEnemyData(enemyObj, index++);
                    if (enemyData != null)
                        enemies.Add(enemyData);
                }
            }
            
            return enemies;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{ModTag} Error getting enemies: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Get enemy objects from battle
    /// </summary>
    private System.Collections.IList? GetEnemyObjects()
    {
        // Would use reflection to get enemy list from BattleScene
        return new List<object>();
    }
    
    /// <summary>
    /// Convert enemy object to EnemyData
    /// </summary>
    private EnemyData? ConvertToEnemyData(object enemyObj, int index)
    {
        try
        {
            var enemyId = ReflectionCache.GetPropertyValue<string>(enemyObj, "AbstractEnemy", "id", "unknown");
            var enemyName = ReflectionCache.GetPropertyValue<string>(enemyObj, "AbstractEnemy", "name", "Unknown Enemy");
            var hp = ReflectionCache.GetPropertyValue<int>(enemyObj, "AbstractEnemy", "CurrentHp", 0);
            var maxHp = ReflectionCache.GetPropertyValue<int>(enemyObj, "AbstractEnemy", "MaxHp", 100);
            var block = ReflectionCache.GetPropertyValue<int>(enemyObj, "AbstractEnemy", "Block", 0);
            var isDead = ReflectionCache.GetPropertyValue<bool>(enemyObj, "AbstractEnemy", "IsDead", false);
            var isHalfDead = ReflectionCache.GetPropertyValue<bool>(enemyObj, "AbstractEnemy", "IsHalfDead", false);
            
            var intentData = GetIntentData(enemyObj);
            
            return new EnemyData(
                Id: enemyId ?? "unknown",
                Name: enemyName ?? "Unknown",
                Hp: hp,
                MaxHp: maxHp,
                Block: block,
                CurrentIntent: intentData,
                Buffs: new List<BuffData>(),
                Debuffs: new List<BuffData>(),
                EnemyIndex: index,
                IsDead: isDead,
                IsHalfDead: isHalfDead,
                NextMoveDescription: intentData?.Description
            );
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{ModTag} Error converting enemy: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Get intent data from enemy
    /// </summary>
    private IntentData? GetIntentData(object enemyObj)
    {
        try
        {
            var intentObj = ReflectionCache.GetFieldValue<object>(enemyObj, "AbstractEnemy", "intent");
            if (intentObj == null)
                return null;
            
            var intentType = GetIntentType(intentObj);
            var amount = ReflectionCache.GetPropertyValue<int>(intentObj, "Intent", "baseAmount", 0);
            var multiplier = ReflectionCache.GetPropertyValue<int>(intentObj, "Intent", "multiplier", 1);
            var description = ReflectionCache.GetPropertyValue<string>(intentObj, "Intent", "description", "");
            var isMultiHit = ReflectionCache.GetPropertyValue<bool>(intentObj, "Intent", "isMultiHit", false);
            var hitCount = ReflectionCache.GetPropertyValue<int>(intentObj, "Intent", "hitCount", 1);
            
            return new IntentData(
                Type: intentType,
                Amount: amount,
                Multiplier: multiplier,
                Description: description,
                IsMultiHit: isMultiHit,
                HitCount: hitCount
            );
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// Determine intent type from intent object
    /// </summary>
    private IntentType GetIntentType(object intentObj)
    {
        try
        {
            var typeEnum = ReflectionCache.GetPropertyValue<object>(intentObj, "Intent", "type");
            if (typeEnum != null)
            {
                var typeName = typeEnum.ToString();
                return typeName switch
                {
                    "Attack" => IntentType.Attack,
                    "Buff" => IntentType.Buff,
                    "Debuff" => IntentType.Debuff,
                    "Heal" => IntentType.Heal,
                    "Block" => IntentType.Block,
                    "Stun" => IntentType.Stun,
                    "Nothing" => IntentType.Nothing,
                    _ => IntentType.Unknown
                };
            }
        }
        catch { }
        
        return IntentType.Unknown;
    }
    
    /// <summary>
    /// Get current turn number
    /// </summary>
    private int GetCurrentTurn()
    {
        return ReflectionCache.GetPropertyValue<int>(_playerInstance!, "Player", "turn", 1);
    }
    
    /// <summary>
    /// Check if it's player's turn
    /// </summary>
    private bool GetIsPlayerTurn()
    {
        return ReflectionCache.GetPropertyValue<bool>(_playerInstance!, "Player", "isPlayerTurn", true);
    }
    
    /// <summary>
    /// Get number of cards played this turn
    /// </summary>
    private int GetCardsPlayedThisTurn()
    {
        return ReflectionCache.GetPropertyValue<int>(_playerInstance!, "Player", "cardsPlayedThisTurn", 0);
    }
    
    /// <summary>
    /// Get current room type
    /// </summary>
    private string? GetCurrentRoomType()
    {
        return ReflectionCache.GetPropertyValue<string>(_playerInstance!, "Player", "currentRoomType", "Combat");
    }
    
    /// <summary>
    /// Get current floor number
    /// </summary>
    private int GetCurrentFloor()
    {
        return ReflectionCache.GetPropertyValue<int>(_playerInstance!, "Player", "floorNumber", 1);
    }
    
    // ============ HARMONY PATCH HANDLERS ============
    
    // These are called by Harmony when patched methods are invoked
    
    private static void BeginBattle_Prefix()
    {
        GD.Print("[StS2Assistant] Battle started - will refresh state");
    }
    
    private static void OnBattleEnd_Postfix()
    {
        GD.Print("[StS2Assistant] Battle ended");
    }
    
    private static void StartTurn_Postfix()
    {
        GD.Print("[StS2Assistant] Turn started");
    }
    
    private static void EndTurn_Postfix()
    {
        GD.Print("[StS2Assistant] Turn ended");
    }
    
    private static void PlayCardAction_Prefix()
    {
        GD.Print("[StS2Assistant] Card being played");
    }
    
    private static void PlayCardAction_Postfix()
    {
        GD.Print("[StS2Assistant] Card played");
    }
    
    private static void SetIntent_Postfix()
    {
        GD.Print("[StS2Assistant] Enemy intent changed");
    }
    
    private static void UseEnergy_Postfix()
    {
        GD.Print("[StS2Assistant] Energy used");
    }
    
    private static void Open_Postfix()
    {
        GD.Print("[StS2Assistant] Reward screen opened");
    }
    
    /// <summary>
    /// Unpatch all methods (called on mod unload)
    /// </summary>
    public void UnpatchAll()
    {
        if (_harmony != null && _isPatched)
        {
            _harmony.UnpatchSelf();
            _isPatched = false;
            GD.Print($"{ModTag} All patches removed");
        }
    }
}
