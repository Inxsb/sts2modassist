// StS2Assistant - Reflection Cache for Safe Game Data Access
// Provides cached reflection access to game fields and properties

using Godot;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace StS2Assistant.Core;

/// <summary>
/// Cached reflection data for accessing game internals safely.
/// All game field/property access goes through this class to handle
/// version changes and provide fallback logging.
/// </summary>
public static class ReflectionCache
{
    private static readonly Dictionary<string, Type?> _cachedTypes = new();
    private static readonly Dictionary<string, FieldInfo?> _cachedFields = new();
    private static readonly Dictionary<string, PropertyInfo?> _cachedProperties = new();
    private static readonly Dictionary<string, MethodInfo?> _cachedMethods = new();
    
    private static readonly string ModTag = "[StS2Assistant.Reflection]";

    /// <summary>
    /// Gets a type by name, with caching
    /// </summary>
    public static Type? GetTypeByName(string typeName)
    {
        if (_cachedTypes.TryGetValue(typeName, out var cached))
            return cached;
        
        try
        {
            var type = AccessTools.TypeByName(typeName);
            _cachedTypes[typeName] = type;
            
            if (type == null)
            {
                GD.Print($"{ModTag} Warning: Type '{typeName}' not found");
            }
            
            return type;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{ModTag} Error getting type '{typeName}': {ex.Message}");
            _cachedTypes[typeName] = null;
            return null;
        }
    }

    /// <summary>
    /// Gets a field info by type and name, with caching
    /// </summary>
    public static FieldInfo? GetField(string typeName, string fieldName)
    {
        var cacheKey = $"{typeName}.{fieldName}";
        
        if (_cachedFields.TryGetValue(cacheKey, out var cached))
            return cached;
        
        try
        {
            var type = GetTypeByName(typeName);
            if (type == null)
            {
                _cachedFields[cacheKey] = null;
                return null;
            }
            
            var field = AccessTools.Field(type, fieldName);
            _cachedFields[cacheKey] = field;
            
            if (field == null)
            {
                GD.Print($"{ModTag} Warning: Field '{fieldName}' not found on '{typeName}'");
            }
            
            return field;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{ModTag} Error getting field '{fieldName}': {ex.Message}");
            _cachedFields[cacheKey] = null;
            return null;
        }
    }

    /// <summary>
    /// Gets a property info by type and name, with caching
    /// </summary>
    public static PropertyInfo? GetProperty(string typeName, string propertyName)
    {
        var cacheKey = $"{typeName}.{propertyName}";
        
        if (_cachedProperties.TryGetValue(cacheKey, out var cached))
            return cached;
        
        try
        {
            var type = GetTypeByName(typeName);
            if (type == null)
            {
                _cachedProperties[cacheKey] = null;
                return null;
            }
            
            var prop = AccessTools.Property(type, propertyName);
            _cachedProperties[cacheKey] = prop;
            
            if (prop == null)
            {
                GD.Print($"{ModTag} Warning: Property '{propertyName}' not found on '{typeName}'");
            }
            
            return prop;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{ModTag} Error getting property '{propertyName}': {ex.Message}");
            _cachedProperties[cacheKey] = null;
            return null;
        }
    }

    /// <summary>
    /// Gets a method info by type and name, with caching
    /// </summary>
    public static MethodInfo? GetMethod(string typeName, string methodName, Type[]? paramTypes = null)
    {
        var cacheKey = $"{typeName}.{methodName}.{(paramTypes != null ? string.Join(",", paramTypes.Select(t => t.Name)) : "")}";
        
        if (_cachedMethods.TryGetValue(cacheKey, out var cached))
            return cached;
        
        try
        {
            var type = GetTypeByName(typeName);
            if (type == null)
            {
                _cachedMethods[cacheKey] = null;
                return null;
            }
            
            MethodInfo? method;
            if (paramTypes != null && paramTypes.Length > 0)
            {
                method = AccessTools.Method(type, methodName, paramTypes);
            }
            else
            {
                method = AccessTools.Method(type, methodName);
            }
            
            _cachedMethods[cacheKey] = method;
            
            if (method == null)
            {
                GD.Print($"{ModTag} Warning: Method '{methodName}' not found on '{typeName}'");
            }
            
            return method;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{ModTag} Error getting method '{methodName}': {ex.Message}");
            _cachedMethods[cacheKey] = null;
            return null;
        }
    }

    /// <summary>
    /// Safely gets a field value from an object
    /// </summary>
    public static T? GetFieldValue<T>(object obj, string typeName, string fieldName, T? defaultValue = default)
    {
        try
        {
            var field = GetField(typeName, fieldName);
            if (field == null)
                return defaultValue;
            
            var value = field.GetValue(obj);
            if (value == null)
                return defaultValue;
            
            return (T)value;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{ModTag} Error getting field value '{fieldName}': {ex.Message}");
            return defaultValue;
        }
    }

    /// <summary>
    /// Safely gets a property value from an object
    /// </summary>
    public static T? GetPropertyValue<T>(object obj, string typeName, string propertyName, T? defaultValue = default)
    {
        try
        {
            var prop = GetProperty(typeName, propertyName);
            if (prop?.GetGetMethod() == null)
                return defaultValue;
            
            var value = prop.GetValue(obj);
            if (value == null)
                return defaultValue;
            
            return (T)value;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{ModTag} Error getting property value '{propertyName}': {ex.Message}");
            return defaultValue;
        }
    }

    /// <summary>
    /// Safely invokes a method on an object
    /// </summary>
    public static T? InvokeMethod<T>(object obj, string typeName, string methodName, object[]? parameters = null)
    {
        try
        {
            var paramTypes = parameters?.Select(p => p.GetType()).ToArray() ?? Array.Empty<Type>();
            var method = GetMethod(typeName, methodName, paramTypes);
            if (method == null)
                return default;
            
            var result = method.Invoke(obj, parameters);
            if (result == null)
                return default;
            
            return (T)result;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{ModTag} Error invoking method '{methodName}': {ex.Message}");
            return default;
        }
    }

    /// <summary>
    /// Clears all cached reflection data (useful for debugging or mod reloads)
    /// </summary>
    public static void ClearCache()
    {
        _cachedTypes.Clear();
        _cachedFields.Clear();
        _cachedProperties.Clear();
        _cachedMethods.Clear();
        GD.Print($"{ModTag} Reflection cache cleared");
    }

    /// <summary>
    /// Pre-caches commonly used types for better performance
    /// </summary>
    public static void PreCacheCommonTypes()
    {
        GD.Print($"{ModTag} Pre-caching common types...");
        
        // Player-related types
        GetTypeByName("Player");
        GetTypeByName("AbstractPlayer");
        
        // Card-related types
        GetTypeByName("AbstractCard");
        GetTypeByName("CardGroup");
        GetTypeByName("HandCard");
        
        // Enemy-related types
        GetTypeByName("AbstractEnemy");
        GetTypeByName("Enemy");
        GetTypeByName("Intent");
        
        // Battle-related types
        GetTypeByName("BattleScene");
        GetTypeByName("DungeonMap");
        GetTypeByName("AbstractRoom");
        
        // Relic and potion types
        GetTypeByName("AbstractRelic");
        GetTypeByName("AbstractPotion");
        
        // Buff/debuff types
        GetTypeByName("AbstractBuff");
        GetTypeByName("Power");
        
        GD.Print($"{ModTag} Pre-caching complete");
    }
}
