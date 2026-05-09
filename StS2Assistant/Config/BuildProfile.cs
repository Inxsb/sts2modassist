// StS2Assistant - Build Profile DTO
// Data Transfer Object for build configuration

using System;
using System.Collections.Generic;
using StS2Assistant.Core;

namespace StS2Assistant.Config;

/// <summary>
/// Represents a build archetype configuration.
/// Used for scoring card alignment and reward recommendations.
/// </summary>
public class BuildProfile
{
    /// <summary>
    /// Unique identifier for this build
    /// </summary>
    public string Id { get; set; } = "custom";
    
    /// <summary>
    /// Display name of the build
    /// </summary>
    public string Name { get; set; } = "Custom Build";
    
    /// <summary>
    /// Character class this build is for (Ironclad, Silent, Defect, Watcher)
    /// </summary>
    public string CharacterClass { get; set; } = "Any";
    
    /// <summary>
    /// List of card IDs that are priority for this build
    /// </summary>
    public List<string> PriorityCards { get; set; } = new();
    
    /// <summary>
    /// List of relic names that synergize well with this build
    /// </summary>
    public List<string> SynergyRelics { get; set; } = new();
    
    /// <summary>
    /// Card tags that are prioritized (e.g., "poison", "exhaust", "ethereal")
    /// </summary>
    public List<string> PriorityTags { get; set; } = new();
    
    /// <summary>
    /// Preferred card types for this build
    /// </summary>
    public List<CardType> PreferredTypes { get; set; } = new();
    
    /// <summary>
    /// Playstyle description
    /// </summary>
    public string Playstyle { get; set; } = "balanced";
    
    /// <summary>
    /// Risk tolerance (0.0 = very safe, 1.0 = high risk)
    /// Affects scoring of defensive vs offensive cards
    /// </summary>
    public float RiskTolerance { get; set; } = 0.5f;
    
    /// <summary>
    /// Whether this is an active/build-in-progress profile
    /// </summary>
    public bool IsActive { get; set; } = false;
    
    /// <summary>
    /// Notes about this build
    /// </summary>
    public string? Notes { get; set; }
    
    /// <summary>
    /// Version of the build (for tracking updates)
    /// </summary>
    public string Version { get; set; } = "1.0";
    
    /// <summary>
    /// Date this build was last modified
    /// </summary>
    public DateTime LastModified { get; set; } = DateTime.Now;
}

/// <summary>
/// Predefined build archetypes for each character
/// </summary>
public static class PredefinedBuilds
{
    /// <summary>
    /// Poison-focused Silent build
    /// </summary>
    public static BuildProfile PoisonSilent => new()
    {
        Id = "silent_poison",
        Name = "Poison Silent",
        CharacterClass = "Silent",
        PriorityCards = new List<string> 
        { 
            "Noxious Fumes", 
            "Catalyst", 
            "Poisonous Stab",
            "Bane",
            "Deadly Poison"
        },
        SynergyRelics = new List<string>
        {
            "Snecko Eye",
            "Chemical X",
            "Poison Ivy",
            "Violet Lotus"
        },
        PriorityTags = new List<string> { "poison" },
        PreferredTypes = new List<CardType> { CardType.Skill, CardType.Attack },
        Playstyle = "attrition",
        RiskTolerance = 0.4f,
        Notes = "Focus on applying poison and multiplying it with Catalyst"
    };
    
    /// <summary>
    /// Strength-scaling Ironclad build
    /// </summary>
    public static BuildProfile StrengthIronclad => new()
    {
        Id = "ironclad_strength",
        Name = "Strength Stack Ironclad",
        CharacterClass = "Ironclad",
        PriorityCards = new List<string>
        {
            "Inflame",
            "Demon Form",
            "Limit Break",
            "Feel No Pain",
            "Entrench"
        },
        SynergyRelics = new List<string>
        {
            "Brimstone",
            "Letter Opener",
            "Orichalcum",
            "Red Skull"
        },
        PriorityTags = new List<string> { "strength", "exhaust" },
        PreferredTypes = new List<CardType> { CardType.Power, CardType.Skill },
        Playstyle = "scaling",
        RiskTolerance = 0.6f,
        Notes = "Stack strength early, then use Limit Break to double it"
    };
    
    /// <summary>
    /// Orb-focusing Defect build
    /// </summary>
    public static BuildProfile OrbDefect => new()
    {
        Id = "defect_orb",
        Name = "Orb Focus Defect",
        CharacterClass = "Defect",
        PriorityCards = new List<string>
        {
            "Claw",
            "Glacier",
            "Zap",
            "Dualcast",
            "Biased Cognition",
            "Recursion"
        },
        SynergyRelics = new List<string>
        {
            "Snecko Eye",
            "Orb Incubator",
            "Cracked Core",
            "Data Disk"
        },
        PriorityTags = new List<string> { "orb", "lightning", "frost" },
        PreferredTypes = new List<CardType> { CardType.Skill, CardType.Power },
        Playstyle = "orb_generation",
        RiskTolerance = 0.5f,
        Notes = "Generate and evoke orbs efficiently for damage and block"
    };
    
    /// <summary>
    /// Divinity stance Watcher build
    /// </summary>
    public static BuildProfile DivinityWatcher => new()
    {
        Id = "watcher_divinity",
        Name = "Divinity Burst Watcher",
        CharacterClass = "Watcher",
        PriorityCards = new List<string>
        {
            "Wallop",
            "Prostrate",
            "Sanctity",
            "Judgment",
            "Enter the Void"
        },
        SynergyRelics = new List<string>
        {
            "Mark of the Bloom",
            "Golden Idol",
            "Nunchaku",
            "Thread and Needle"
        },
        PriorityTags = new List<string> { "divinity", "wrath" },
        PreferredTypes = new List<CardType> { CardType.Attack, CardType.Skill },
        Playstyle = "burst_damage",
        RiskTolerance = 0.7f,
        Notes = "Enter divinity, deal massive damage with multi-hit attacks"
    };
    
    /// <summary>
    /// Get all predefined builds
    /// </summary>
    public static List<BuildProfile> GetAllPredefined() => new()
    {
        PoisonSilent,
        StrengthIronclad,
        OrbDefect,
        DivinityWatcher
    };
}
