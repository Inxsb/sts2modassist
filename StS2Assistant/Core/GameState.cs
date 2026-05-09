using System;
using System.Collections.Generic;
using System.Linq;

namespace StS2Assistant.Core;

/// <summary>
/// Immutable DTO состояния игры.
/// Содержит всю необходимую информацию для анализа.
/// </summary>
public record GameState
{
    /// <summary>
    /// Состояние игрока
    /// </summary>
    public PlayerState Player { get; init; } = PlayerState.Empty;
    
    /// <summary>
    /// Список врагов (для StS2 может быть несколько)
    /// </summary>
    public IReadOnlyList<EnemyState> Enemies { get; init; } = Array.Empty<EnemyState>();
    
    /// <summary>
    /// Текущий ход (номер хода в бою)
    /// </summary>
    public int TurnNumber { get; init; }
    
    /// <summary>
    /// Фаза хода (PlayerTurn, EnemyTurn, etc.)
    /// </summary>
    public TurnPhase CurrentPhase { get; init; }
    
    /// <summary>
    /// Этаж подземелья
    /// </summary>
    public int Floor { get; init; }
    
    /// <summary>
    /// Тип текущей комнаты (Battle, Shop, Rest, etc.)
    /// </summary>
    public RoomType RoomType { get; init; }
    
    /// <summary>
    /// Пустое состояние по умолчанию
    /// </summary>
    public static GameState Empty => new();
}

/// <summary>
/// Состояние игрока
/// </summary>
public record PlayerState
{
    /// <summary>
    /// Текущее HP
    /// </summary>
    public int CurrentHp { get; init; }
    
    /// <summary>
    /// Максимальное HP
    /// </summary>
    public int MaxHp { get; init; }
    
    /// <summary>
    /// Текущий блок
    /// </summary>
    public int Block { get; init; }
    
    /// <summary>
    /// Текущая энергия
    /// </summary>
    public int Energy { get; init; }
    
    /// <summary>
    /// Максимальная энергия за ход
    /// </summary>
    public int MaxEnergy { get; init; }
    
    /// <summary>
    /// Карты в руке
    /// </summary>
    public IReadOnlyList<CardState> Hand { get; init; } = Array.Empty<CardState>();
    
    /// <summary>
    /// Карты в колоде добора
    /// </summary>
    public IReadOnlyList<CardState> DrawPile { get; init; } = Array.Empty<CardState>();
    
    /// <summary>
    /// Карты в сбросе
    /// </summary>
    public IReadOnlyList<CardState> DiscardPile { get; init; } = Array.Empty<CardState>();
    
    /// <summary>
    /// Исчерпанные карты
    /// </summary>
    public IReadOnlyList<CardState> ExhaustPile { get; init; } = Array.Empty<CardState>();
    
    /// <summary>
    /// Сыгранные карты в этом ходу
    /// </summary>
    public IReadOnlyList<CardState> PlayedThisTurn { get; init; } = Array.Empty<CardState>();
    
    /// <summary>
    /// Активные реликвии (ID)
    /// </summary>
    public IReadOnlyList<string> Relics { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// Активные зелья (ID)
    /// </summary>
    public IReadOnlyList<PotionState> Potions { get; init; } = Array.Empty<PotionState>();
    
    /// <summary>
    /// Баффы игрока
    /// </summary>
    public IReadOnlyList<BuffState> Buffs { get; init; } = Array.Empty<BuffState>();
    
    /// <summary>
    /// Дебаффы игрока
    /// </summary>
    public IReadOnlyList<BuffState> Debuffs { get; init; } = Array.Empty<BuffState>();
    
    /// <summary>
    /// Намерения игрока (выбранные цели для карт)
    /// </summary>
    public IReadOnlyList<IntentState> Intents { get; init; } = Array.Empty<IntentState>();
    
    public static PlayerState Empty => new();
}

/// <summary>
/// Состояние врага
/// </summary>
public record EnemyState
{
    /// <summary>
    /// Уникальный ID врага
    /// </summary>
    public string Id { get; init; } = string.Empty;
    
    /// <summary>
    /// Имя врага
    /// </summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>
    /// Текущее HP
    /// </summary>
    public int CurrentHp { get; init; }
    
    /// <summary>
    /// Максимальное HP
    /// </summary>
    public int MaxHp { get; init; }
    
    /// <summary>
    /// Текущий блок
    /// </summary>
    public int Block { get; init; }
    
    /// <summary>
    /// Текущее намерение врага
    /// </summary>
    public IntentData? CurrentIntent { get; init; }
    
    /// <summary>
    /// История намерений (для предсказания паттернов)
    /// </summary>
    public IReadOnlyList<IntentData> IntentHistory { get; init; } = Array.Empty<IntentData>();
    
    /// <summary>
    /// Баффы врага
    /// </summary>
    public IReadOnlyList<BuffState> Buffs { get; init; } = Array.Empty<BuffState>();
    
    /// <summary>
    /// Дебаффы врага
    /// </summary>
    public IReadOnlyList<BuffState> Debuffs { get; init; } = Array.Empty<BuffState>();
    
    /// <summary>
    /// Является ли боссом
    /// </summary>
    public bool IsBoss { get; init; }
}

/// <summary>
/// Данные о намерении врага
/// </summary>
public record IntentData
{
    /// <summary>
    /// Тип намерения
    /// </summary>
    public IntentType Type { get; init; }
    
    /// <summary>
    /// Ожидаемое значение (урон, блок, etc.)
    /// </summary>
    public int Value { get; init; }
    
    /// <summary>
    /// Количество ударов (для многоударных атак)
    /// </summary>
    public int HitCount { get; init; } = 1;
    
    /// <summary>
    /// Описание намерения (для отображения)
    /// </summary>
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// Тип намерения врага
/// </summary>
public enum IntentType
{
    Unknown,
    Attack,
    Buff,
    Debuff,
    Special,
    Defend,
    Escape,
    Sleep,
    Stun
}

/// <summary>
/// Состояние карты
/// </summary>
public record CardState
{
    /// <summary>
    /// Уникальный ID карты
    /// </summary>
    public string Id { get; init; } = string.Empty;
    
    /// <summary>
    /// Название карты
    /// </summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>
    /// Тип карты
    /// </summary>
    public CardType Type { get; init; }
    
    /// <summary>
    /// Стоимость энергии
    /// </summary>
    public int Cost { get; init; }
    
    /// <summary>
    /// Базовый урон (для атак)
    /// </summary>
    public int Damage { get; init; }
    
    /// <summary>
    /// Базовый блок (для защитных карт)
    /// </summary>
    public int Block { get; init; }
    
    /// <summary>
    /// Магическое число (дополнительный эффект)
    /// </summary>
    public int MagicNumber { get; init; }
    
    /// <summary>
    /// Улучшена ли карта
    /// </summary>
    public bool IsUpgraded { get; init; }
    
    /// <summary>
    /// Исчерпывается ли при использовании
    /// </summary>
    public bool IsExhaust { get; init; }
    
    /// <summary>
    /// Этчируется ли (возвращается в руку)
    /// </summary>
    public bool IsEthereal { get; init; }
    
    /// <summary>
    /// Требуется ли выбор цели
    /// </summary>
    public bool RequiresTarget { get; init; }
    
    /// <summary>
    /// Текущая стоимость (с учётом модификаторов)
    /// </summary>
    public int CurrentCost { get; init; }
}

/// <summary>
/// Тип карты
/// </summary>
public enum CardType
{
    Attack,
    Skill,
    Power,
    Status,
    Curse
}

/// <summary>
/// Состояние зелья
/// </summary>
public record PotionState
{
    /// <summary>
    /// ID зелья
    /// </summary>
    public string Id { get; init; } = string.Empty;
    
    /// <summary>
    /// Название зелья
    /// </summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>
    /// Слот зелья (0-3)
    /// </summary>
    public int Slot { get; init; }
    
    /// <summary>
    /// Доступно ли для использования
    /// </summary>
    public bool CanUse { get; init; }
}

/// <summary>
/// Состояние баффа/дебаффа
/// </summary>
public record BuffState
{
    /// <summary>
    /// ID эффекта
    /// </summary>
    public string Id { get; init; } = string.Empty;
    
    /// <summary>
    /// Название эффекта
    /// </summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>
    /// Количество стаков
    /// </summary>
    public int Amount { get; init; }
    
    /// <summary>
    /// Это дебафф?
    /// </summary>
    public bool IsDebuff { get; init; }
}

/// <summary>
/// Намерение игрока (цель для карты)
/// </summary>
public record IntentState
{
    /// <summary>
    /// ID целевого врага
    /// </summary>
    public string TargetId { get; init; } = string.Empty;
    
    /// <summary>
    /// ID карты
    /// </summary>
    public string CardId { get; init; } = string.Empty;
}

/// <summary>
/// Фаза хода
/// </summary>
public enum TurnPhase
{
    PlayerTurn,
    EnemyTurn,
    TurnTransition,
    BattleStart,
    BattleEnd
}

/// <summary>
/// Тип комнаты
/// </summary>
public enum RoomType
{
    Unknown,
    Battle,
    Elite,
    Boss,
    Shop,
    Rest,
    Treasure,
    Event,
    Campfire
}
