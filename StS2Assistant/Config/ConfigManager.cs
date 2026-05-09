// StS2Assistant - Configuration Manager
// Handles loading and saving build profiles and mod settings

using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StS2Assistant.Config;

/// <summary>
/// Manages configuration files for the mod.
/// Stores build profiles and user preferences in user:// directory.
/// </summary>
public class ConfigManager
{
    private static readonly string ModTag = "[StS2Assistant.Config]";
    
    // Configuration paths
    private readonly string _configDir;
    private readonly string _buildsPath;
    private readonly string _settingsPath;
    
    // Current active build
    private BuildProfile? _activeBuild;
    
    // All loaded builds
    private readonly Dictionary<string, BuildProfile> _loadedBuilds = new();
    
    // Mod settings
    private ModSettings _settings;
    
    // JSON serialization options
    private readonly JsonSerializerOptions _jsonOptions;

    public ConfigManager()
    {
        // Setup paths using Godot's user data directory
        _configDir = Path.Combine(Godot.UserDataDir, "StS2Assistant");
        _buildsPath = Path.Combine(_configDir, "builds.json");
        _settingsPath = Path.Combine(_configDir, "settings.json");
        
        // Ensure config directory exists
        if (!Directory.Exists(_configDir))
        {
            Directory.CreateDirectory(_configDir);
            GD.Print($"{ModTag} Created config directory: {_configDir}");
        }
        
        // Setup JSON options
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        
        // Load default settings
        _settings = new ModSettings();
    }

    /// <summary>
    /// Load all configurations from disk
    /// </summary>
    public void LoadConfigurations()
    {
        try
        {
            // Load mod settings
            LoadSettings();
            
            // Load build profiles
            LoadBuildProfiles();
            
            // Load predefined builds
            LoadPredefinedBuilds();
            
            GD.Print($"{ModTag} Configurations loaded successfully");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{ModTag} Error loading configurations: {ex.Message}");
            
            // Create default configs on error
            CreateDefaultConfigs();
        }
    }

    /// <summary>
    /// Load mod settings from file
    /// </summary>
    private void LoadSettings()
    {
        if (File.Exists(_settingsPath))
        {
            try
            {
                var json = File.ReadAllText(_settingsPath);
                _settings = JsonSerializer.Deserialize<ModSettings>(json, _jsonOptions) ?? new ModSettings();
                GD.Print($"{ModTag} Settings loaded from {_settingsPath}");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"{ModTag} Error reading settings: {ex.Message}");
                _settings = new ModSettings();
            }
        }
        else
        {
            GD.Print($"{ModTag} No settings file found, using defaults");
            SaveSettings();
        }
    }

    /// <summary>
    /// Save mod settings to file
    /// </summary>
    public void SaveSettings()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, _jsonOptions);
            File.WriteAllText(_settingsPath, json);
            GD.Print($"{ModTag} Settings saved to {_settingsPath}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{ModTag} Error saving settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Load build profiles from file
    /// </summary>
    private void LoadBuildProfiles()
    {
        if (File.Exists(_buildsPath))
        {
            try
            {
                var json = File.ReadAllText(_buildsPath);
                var builds = JsonSerializer.Deserialize<List<BuildProfile>>(json, _jsonOptions);
                
                if (builds != null)
                {
                    foreach (var build in builds)
                    {
                        _loadedBuilds[build.Id] = build;
                    }
                    GD.Print($"{ModTag} Loaded {_loadedBuilds.Count} custom builds");
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"{ModTag} Error reading builds: {ex.Message}");
            }
        }
        else
        {
            GD.Print($"{ModTag} No builds file found");
        }
    }

    /// <summary>
    /// Load predefined builds into memory
    /// </summary>
    private void LoadPredefinedBuilds()
    {
        var predefined = PredefinedBuilds.GetAllPredefined();
        foreach (var build in predefined)
        {
            if (!_loadedBuilds.ContainsKey(build.Id))
            {
                _loadedBuilds[build.Id] = build;
            }
        }
        GD.Print($"{ModTag} Loaded {predefined.Count} predefined builds");
    }

    /// <summary>
    /// Save current build profiles to file
    /// </summary>
    public void SaveBuildProfiles()
    {
        try
        {
            // Only save custom builds (not predefined)
            var customBuilds = _loadedBuilds.Values
                .Where(b => !b.Id.StartsWith("ironclad_") && 
                           !b.Id.StartsWith("silent_") && 
                           !b.Id.StartsWith("defect_") && 
                           !b.Id.StartsWith("watcher_"))
                .ToList();
            
            var json = JsonSerializer.Serialize(customBuilds, _jsonOptions);
            File.WriteAllText(_buildsPath, json);
            GD.Print($"{ModTag} Saved {customBuilds.Count} custom builds");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{ModTag} Error saving builds: {ex.Message}");
        }
    }

    /// <summary>
    /// Get the currently active build profile
    /// </summary>
    public BuildProfile? GetCurrentBuildProfile()
    {
        return _activeBuild;
    }

    /// <summary>
    /// Set the active build profile
    /// </summary>
    public void SetActiveBuild(string buildId)
    {
        if (_loadedBuilds.TryGetValue(buildId, out var build))
        {
            // Deactivate previous build
            if (_activeBuild != null)
            {
                _activeBuild.IsActive = false;
            }
            
            _activeBuild = build;
            _activeBuild.IsActive = true;
            
            GD.Print($"{ModTag} Active build set to: {build.Name}");
        }
        else
        {
            GD.PrintErr($"{ModTag} Build not found: {buildId}");
        }
    }

    /// <summary>
    /// Get a build profile by ID
    /// </summary>
    public BuildProfile? GetBuildById(string buildId)
    {
        return _loadedBuilds.TryGetValue(buildId, out var build) ? build : null;
    }

    /// <summary>
    /// Get all available build profiles
    /// </summary>
    public List<BuildProfile> GetAllBuilds()
    {
        return _loadedBuilds.Values.ToList();
    }

    /// <summary>
    /// Add or update a custom build profile
    /// </summary>
    public void SaveCustomBuild(BuildProfile build)
    {
        build.LastModified = DateTime.Now;
        _loadedBuilds[build.Id] = build;
        SaveBuildProfiles();
        GD.Print($"{ModTag} Saved custom build: {build.Name}");
    }

    /// <summary>
    /// Delete a custom build profile
    /// </summary>
    public void DeleteBuild(string buildId)
    {
        // Don't allow deleting predefined builds
        if (buildId.StartsWith("ironclad_") || 
            buildId.StartsWith("silent_") || 
            buildId.StartsWith("defect_") || 
            buildId.StartsWith("watcher_"))
        {
            GD.PrintErr($"{ModTag} Cannot delete predefined build: {buildId}");
            return;
        }
        
        if (_loadedBuilds.Remove(buildId))
        {
            if (_activeBuild?.Id == buildId)
            {
                _activeBuild = null;
            }
            SaveBuildProfiles();
            GD.Print($"{ModTag} Deleted build: {buildId}");
        }
    }

    /// <summary>
    /// Create default configuration files
    /// </summary>
    private void CreateDefaultConfigs()
    {
        try
        {
            // Create default settings
            _settings = new ModSettings();
            SaveSettings();
            
            // Create sample builds file
            var sampleBuilds = new List<BuildProfile>
            {
                PredefinedBuilds.PoisonSilent,
                PredefinedBuilds.StrengthIronclad
            };
            
            var json = JsonSerializer.Serialize(sampleBuilds, _jsonOptions);
            File.WriteAllText(_buildsPath, json);
            
            GD.Print($"{ModTag} Created default configuration files");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{ModTag} Error creating default configs: {ex.Message}");
        }
    }

    /// <summary>
    /// Export build profiles to a shareable format
    /// </summary>
    public string ExportBuilds()
    {
        var exportData = new BuildExport
        {
            Version = "1.0",
            ExportDate = DateTime.Now,
            Builds = _loadedBuilds.Values.ToList()
        };
        
        return JsonSerializer.Serialize(exportData, _jsonOptions);
    }

    /// <summary>
    /// Import build profiles from exported JSON
    /// </summary>
    public bool ImportBuilds(string json)
    {
        try
        {
            var exportData = JsonSerializer.Deserialize<BuildExport>(json, _jsonOptions);
            if (exportData?.Builds == null)
                return false;
            
            foreach (var build in exportData.Builds)
            {
                _loadedBuilds[build.Id] = build;
            }
            
            SaveBuildProfiles();
            GD.Print($"{ModTag} Imported {exportData.Builds.Count} builds");
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{ModTag} Error importing builds: {ex.Message}");
            return false;
        }
    }
}

/// <summary>
/// Mod settings stored in settings.json
/// </summary>
public class ModSettings
{
    /// <summary>
    /// Enable/disable the overlay UI
    /// </summary>
    public bool OverlayEnabled { get; set; } = true;
    
    /// <summary>
    /// Enable card highlighting
    /// </summary>
    public bool CardHighlightingEnabled { get; set; } = true;
    
    /// <summary>
    /// Show draw probabilities
    /// </summary>
    public bool ShowDrawProbabilities { get; set; } = true;
    
    /// <summary>
    /// Auto-select build based on character
    /// </summary>
    public bool AutoSelectBuild { get; set; } = true;
    
    /// <summary>
    /// Overlay position (0-3: TopLeft, TopRight, BottomLeft, BottomRight)
    /// </summary>
    public int OverlayPosition { get; set; } = 1; // TopRight
    
    /// <summary>
    /// Overlay opacity (0.0 - 1.0)
    /// </summary>
    public float OverlayOpacity { get; set; } = 0.95f;
    
    /// <summary>
    /// Enable debug logging
    /// </summary>
    public bool DebugMode { get; set; } = false;
    
    /// <summary>
    /// Minimum score threshold for showing recommendations
    /// </summary>
    public float MinRecommendationScore { get; set; } = 3.0f;
    
    /// <summary>
    /// Hotkey for toggling overlay (default: F8)
    /// </summary>
    public string ToggleHotkey { get; set; } = "F8";
}

/// <summary>
/// Data structure for exporting/importing builds
/// </summary>
public class BuildExport
{
    public string Version { get; set; } = "1.0";
    public DateTime ExportDate { get; set; }
    public List<BuildProfile> Builds { get; set; } = new();
}
