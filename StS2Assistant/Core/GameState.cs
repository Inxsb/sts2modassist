// StS2Assistant - Immutable Game State Record
// Represents a snapshot of the current battle state

using System;
using System.Collections.Generic;

namespace StS2Assistant.Core;

/// <summary>
/// Immutable record representing the current game state.
/// Updated only when game events occur (via Harmony patches).
/// </summary>
public sealed record GameState(
    // Player state
    int PlayerHp,
    int PlayerMaxHp,
    int PlayerBlock,
    int PlayerEnergy,
    int PlayerMaxEnergy,
    
    // Card zones
    IReadOnlyList<CardData> HandCards,
    IReadOnlyList<CardData> DrawPile,
    IReadOnlyList<CardData> DiscardPile,
    IReadOnlyList<CardData> ExhaustPile,
    IReadOnlyList<CardData> PlayedThisTurn,
    
    // Player resources
    IReadOnlyList<RelicData> Relics,
    IReadOnlyList<PotionData> Potions,
    IReadOnlyList<BuffData> PlayerBuffs,
    IReadOnlyList<BuffData> PlayerDebuffs,
    
    // Enemy state (supports multiple enemies for StS2)
    IReadOnlyList<EnemyData> Enemies,
    
    // Turn info
    int CurrentTurn,
    bool IsPlayerTurn,
    int CardsPlayedThisTurn,
    
    // Battle metadata
    string? CurrentRoomType,
    int FloorNumber,
    DateTime LastUpdated
);

/// <summary>
/// Represents a card in any zone
/// </summary>
public sealed record CardData(
    string Id,
    string Name,
    int Cost,
    CardType Type,
    CardRarity Rarity,
    CardColor Color,
    string? Description,
    int UpgradeLevel,
    bool IsExhausted,
    bool IsEthereal,
    bool IsInnate,
    IReadOnlyList<string> Tags,
    float BaseDamage = 0,
    float BaseBlock = 0,
    float BaseMagicNumber = 0,
    int UpgradesDamage = 0,
    int UpgradesBlock = 0,
    int UpgradesMagicNumber = 0
);

/// <summary>
/// Card classification types
/// </summary>
public enum CardType
{
    Attack,
    Skill,
    Power,
    Status,
    Curse,
    Unknown
}

/// <summary>
/// Card rarity levels
/// </summary>
public enum CardRarity
{
    Basic,
    Common,
    Uncommon,
    Rare,
    Special,
    Unknown
}

/// <summary>
/// Card color/class identity
/// </summary>
public enum CardColor
{
    Red,      // Ironclad
    Green,    // Silent
    Blue,     // Defect
    Purple,   // Watcher
    Colorless,
    Curse,
    Unknown
}

/// <summary>
/// Represents an enemy in battle
/// </summary>
public sealed record EnemyData(
    string Id,
    string Name,
    int Hp,
    int MaxHp,
    int Block,
    IntentData? CurrentIntent,
    IReadOnlyList<BuffData> Buffs,
    IReadOnlyList<BuffData> Debuffs,
    int EnemyIndex,
    bool IsDead,
    bool IsHalfDead,
    string? NextMoveDescription
);

/// <summary>
/// Enemy intent information
/// </summary>
public sealed record IntentData(
    IntentType Type,
    int Amount,
    int Multiplier,
    string? Description,
    bool IsMultiHit,
    int HitCount
);

/// <summary>
/// Types of enemy intents
/// </summary>
public enum IntentType
{
    Attack,
    Buff,
    Debuff,
    Heal,
    DamageBuff,
    DamageDebuff,
    Block,
    BlockBuff,
    BlockDebuff,
    Stun,
    KillPlayer,
    Sleep,
    Nothing,
    Unknown
}

/// <summary>
/// Represents a relic
/// </summary>
public sealed record RelicData(
    string Id,
    string Name,
    RelicTier Tier,
    string? Description,
    bool IsObtainedInCombat,
    int Counter
);

/// <summary>
/// Relic rarity tiers
/// </summary>
public enum RelicTier
{
    Starter,
    Common,
    Uncommon,
    Rare,
    Boss,
    Shop,
    Special
}

/// <summary>
/// Represents a potion
/// </summary>
public sealed record PotionData(
    string Id,
    string Name,
    PotionRarity Rarity,
    string? Description,
    int UsesRemaining
);

/// <summary>
/// Potion rarity levels
/// </summary>
public enum PotionRarity
{
    Common,
    Uncommon,
    Rare,
    Special
}

/// <summary>
/// Represents a buff or debuff
/// </summary>
public sealed record BuffData(
    string Id,
    string Name,
    int Amount,
    bool IsDebuff,
    bool CanBeCleared,
    int TurnsRemaining,
    string? Description
);

/// <summary>
/// Reward item data for recommendation system
/// </summary>
public sealed record RewardData(
    RewardType Type,
    CardData? Card,
    RelicData? Relic,
    GoldReward? Gold,
    bool IsBossReward
);

/// <summary>
/// Gold reward data
/// </summary>
public sealed record GoldReward(
    int Amount,
    bool IsElite,
    bool IsBoss
);

/// <summary>
/// Type of reward
/// </summary>
public enum RewardType
{
    Card,
    Relic,
    Gold,
    RemoveCard,
    SmithHammer,
    RestSite
}

/// <summary>
/// Recommendation result from analysis
/// </summary>
public sealed record Recommendation(
    string Title,
    string Description,
    RecommendationPriority Priority,
    RecommendationCategory Category,
    List<string> SuggestedCards,
    List<string> Reasoning,
    float Confidence,
    int? TargetEnemyIndex
);

/// <summary>
/// Priority levels for recommendations
/// </summary>
public enum RecommendationPriority
{
    Critical,
    High,
    Medium,
    Low,
    Info
}

/// <summary>
/// Categories of recommendations
/// </summary>
public enum RecommendationCategory
{
    CardPlay,
    Defense,
    Offense,
    ResourceManagement,
    ThreatResponse,
    BuildSynergy,
    RewardSelection,
    General
}
