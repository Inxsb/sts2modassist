using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

namespace StS2Assistant.Config;

/// <summary>
/// DTO профиля билда для JSON сериализации.
/// </summary>
public class BuildProfile
{
    /// <summary>
    /// Название билда
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Приоритетные карты (ID или частичные названия)
    /// </summary>
    public List<string> PriorityCards { get; set; } = new();
    
    /// <summary>
    /// Синергичные реликвии (ID или частичные названия)
    /// </summary>
    public List<string> SynergyRelics { get; set; } = new();
    
    /// <summary>
    /// Стиль игры: aggressive, defensive, control, combo, attrition_control
    /// </summary>
    public string Playstyle { get; set; } = "balanced";
    
    /// <summary>
    /// Толерантность к риску (0.0-1.0)
    /// 0.0 = очень осторожно, 1.0 = агрессивно
    /// </summary>
    public float RiskTolerance { get; set; } = 0.5f;
}

/// <summary>
/// Менеджер конфигурации мода.
/// Загружает/сохраняет JSON конфиги и профили билдов.
/// </summary>
public class ConfigManager
{
    private static readonly string LogPrefix = "[StS2Assistant.ConfigManager]";
    
    /// <summary>
    /// Путь к директории конфигурации мода
    /// </summary>
    private readonly string _configDir;
    
    /// <summary>
    /// Путь к директории профилей билдов
    /// </summary>
    private readonly string _buildsDir;
    
    /// <summary>
    /// Основная конфигурация мода
    /// </summary>
    private ModConfig? _modConfig;

    /// <summary>
    /// Опции сериализации JSON
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ConfigManager()
    {
        // Godot.UserDataDir соответствует user:// в Godot
        _configDir = Path.Combine(Godot.UserDataDir, "StS2Assistant");
        _buildsDir = Path.Combine(_configDir, "builds");
        
        // Создание директорий если не существуют
        Directory.CreateDirectory(_configDir);
        Directory.CreateDirectory(_buildsDir);
    }

    /// <summary>
    /// Загрузка основной конфигурации
    /// </summary>
    public void LoadConfig()
    {
        try
        {
            var configPath = Path.Combine(_configDir, "mod_config.json");
            
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                _modConfig = JsonSerializer.Deserialize<ModConfig>(json, JsonOptions);
                GD.Print($"{LogPrefix} Config loaded from {configPath}");
            }
            else
            {
                // Создание конфигурации по умолчанию
                _modConfig = CreateDefaultConfig();
                SaveConfig();
                GD.Print($"{LogPrefix} Created default config");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{LogPrefix} Failed to load config: {ex.Message}");
            _modConfig = CreateDefaultConfig();
        }
    }

    /// <summary>
    /// Сохранение конфигурации
    /// </summary>
    public void SaveConfig()
    {
        try
        {
            if (_modConfig == null) return;
            
            var configPath = Path.Combine(_configDir, "mod_config.json");
            var json = JsonSerializer.Serialize(_modConfig, JsonOptions);
            File.WriteAllText(configPath, json);
            GD.Print($"{LogPrefix} Config saved to {configPath}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{LogPrefix} Failed to save config: {ex.Message}");
        }
    }

    /// <summary>
    /// Создание конфигурации по умолчанию
    /// </summary>
    private ModConfig CreateDefaultConfig()
    {
        return new ModConfig
        {
            OverlayEnabled = true,
            DebugMode = false,
            AutoUpdateRecommendations = true,
            UpdateIntervalMs = 500,
            DefaultBuildProfile = "poison_silent.json"
        };
    }

    /// <summary>
    /// Загрузка профиля билда из JSON файла
    /// </summary>
    public BuildProfile? LoadBuild(string buildFileName)
    {
        try
        {
            var buildPath = GetBuildPath(buildFileName);
            
            if (!File.Exists(buildPath))
            {
                GD.Print($"{LogPrefix} Build profile not found: {buildFileName}");
                return null;
            }
            
            var json = File.ReadAllText(buildPath);
            var build = JsonSerializer.Deserialize<BuildProfile>(json, JsonOptions);
            
            GD.Print($"{LogPrefix} Loaded build profile: {build?.Name}");
            return build;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{LogPrefix} Failed to load build profile: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Сохранение профиля билда
    /// </summary>
    public void SaveBuild(BuildProfile build, string buildFileName)
    {
        try
        {
            var buildPath = GetBuildPath(buildFileName);
            var json = JsonSerializer.Serialize(build, JsonOptions);
            File.WriteAllText(buildPath, json);
            GD.Print($"{LogPrefix} Saved build profile: {build.Name}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{LogPrefix} Failed to save build profile: {ex.Message}");
        }
    }

    /// <summary>
    /// Получение полного пути к файлу билда
    /// </summary>
    public string GetBuildPath(string buildFileName)
    {
        return Path.Combine(_buildsDir, buildFileName);
    }

    /// <summary>
    /// Список доступных профилей билдов
    /// </summary>
    public List<string> GetAvailableBuilds()
    {
        try
        {
            return Directory.GetFiles(_buildsDir, "*.json")
                .Select(Path.GetFileName)
                .Where(f => !string.IsNullOrEmpty(f))
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// Текущая конфигурация мода
    /// </summary>
    public ModConfig? GetConfig() => _modConfig;
}

/// <summary>
/// Основная конфигурация мода
/// </summary>
public class ModConfig
{
    /// <summary>
    /// Включен ли оверлей
    /// </summary>
    public bool OverlayEnabled { get; set; } = true;
    
    /// <summary>
    /// Режим отладки
    /// </summary>
    public bool DebugMode { get; set; } = false;
    
    /// <summary>
    /// Автоматическое обновление рекомендаций
    /// </summary>
    public bool AutoUpdateRecommendations { get; set; } = true;
    
    /// <summary>
    /// Интервал обновления рекомендаций (мс)
    /// </summary>
    public int UpdateIntervalMs { get; set; } = 500;
    
    /// <summary>
    /// Профиль билда по умолчанию
    /// </summary>
    public string DefaultBuildProfile { get; set; } = "generic.json";
}
