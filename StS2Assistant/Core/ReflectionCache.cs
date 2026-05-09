using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace StS2Assistant.Core;

/// <summary>
/// Кэширование рефлексии для быстрого доступа к типам и методам StS2.
/// Избегает повторных дорогостоящих вызовов反射.
/// </summary>
public class ReflectionCache
{
    private static readonly string LogPrefix = "[StS2Assistant.ReflectionCache]";
    
    /// <summary>
    /// Кэш найденных типов
    /// </summary>
    private readonly ConcurrentDictionary<string, Type?> _typeCache = new();
    
    /// <summary>
    /// Кэш найденных методов (ключ: "TypeName.MethodName")
    /// </summary>
    private readonly ConcurrentDictionary<string, MethodInfo?> _methodCache = new();
    
    /// <summary>
    /// Список возможных пространств имён StS2
    /// </summary>
    private static readonly string[] PossibleNamespaces = new[]
    {
        "", // Глобальное пространство
        "Sts2",
        "Sts2.Game",
        "Sts2.Battle",
        "Sts2.Cards",
        "Sts2.Characters",
        "Sts2.Monsters",
        "Sts2.Actions",
        "Sts2.Screens",
        "Alchyr.Sts2",
        "BaseLib"
    };

    /// <summary>
    /// Инициализация кэша
    /// </summary>
    public void Initialize()
    {
        GD.Print($"{LogPrefix} Initializing reflection cache...");
        
        // Предварительная загрузка часто используемых типов
        CacheType("AbstractPlayer");
        CacheType("AbstractMonster");
        CacheType("Card");
        CacheType("AbstractBattle");
        CacheType("RewardScreen");
        CacheType("GameActions");
        
        GD.Print($"{LogPrefix} Reflection cache initialized with {_typeCache.Count} types");
    }

    /// <summary>
    /// Поиск типа по имени
    /// </summary>
    public Type? GetType(string typeName)
    {
        if (_typeCache.TryGetValue(typeName, out var cachedType))
            return cachedType;
        
        // Попытка найти тип в различных пространствах имён
        foreach (var ns in PossibleNamespaces)
        {
            var fullName = string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}";
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name == typeName || t.FullName == fullName);
            
            if (type != null)
            {
                _typeCache[fullName] = type;
                GD.Print($"{LogPrefix} Cached type: {fullName}");
                return type;
            }
        }
        
        _typeCache[typeName] = null;
        return null;
    }

    /// <summary>
    /// Кэширование типа
    /// </summary>
    private void CacheType(string typeName)
    {
        GetType(typeName);
    }

    /// <summary>
    /// Поиск метода по строковому идентификатору "TypeName.MethodName"
    /// </summary>
    public MethodInfo? FindMethod(string methodIdentifier)
    {
        if (_methodCache.TryGetValue(methodIdentifier, out var cachedMethod))
            return cachedMethod;
        
        var parts = methodIdentifier.Split('.');
        if (parts.Length < 2)
        {
            GD.PrintErr($"{LogPrefix} Invalid method identifier: {methodIdentifier}");
            return null;
        }
        
        var typeName = string.Join(".", parts.Take(parts.Length - 1));
        var methodName = parts.Last();
        
        var type = GetType(typeName);
        if (type == null)
        {
            _methodCache[methodIdentifier] = null;
            return null;
        }
        
        // Поиск метода с различными сигнатурами
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | 
                                      BindingFlags.Instance | BindingFlags.Static |
                                      BindingFlags.FlattenHierarchy);
        
        var method = methods.FirstOrDefault(m => m.Name == methodName);
        
        if (method != null)
        {
            _methodCache[methodIdentifier] = method;
            GD.Print($"{LogPrefix} Cached method: {methodIdentifier} ({method.ReturnType.Name})");
            return method;
        }
        
        _methodCache[methodIdentifier] = null;
        GD.Print($"{LogPrefix} Method not found: {methodIdentifier}");
        return null;
    }

    /// <summary>
    /// Получение свойства типа
    /// </summary>
    public PropertyInfo? FindProperty(string typeName, string propertyName)
    {
        var type = GetType(typeName);
        if (type == null)
            return null;
        
        return type.GetProperty(propertyName, 
            BindingFlags.Public | BindingFlags.NonPublic | 
            BindingFlags.Instance | BindingFlags.Static);
    }

    /// <summary>
    /// Получение поля типа
    /// </summary>
    public FieldInfo? FindField(string typeName, string fieldName)
    {
        var type = GetType(typeName);
        if (type == null)
            return null;
        
        return type.GetField(fieldName,
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static);
    }

    /// <summary>
    /// Создание экземпляра типа
    /// </summary>
    public object? CreateInstance(string typeName, params object[] args)
    {
        var type = GetType(typeName);
        if (type == null)
            return null;
        
        try
        {
            return Activator.CreateInstance(type, args);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{LogPrefix} Failed to create instance of {typeName}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Очистка кэша (для отладки)
    /// </summary>
    public void Clear()
    {
        _typeCache.Clear();
        _methodCache.Clear();
        GD.Print($"{LogPrefix} Reflection cache cleared");
    }

    /// <summary>
    /// Статистика кэша
    /// </summary>
    public string GetStats()
    {
        return $"Types: {_typeCache.Count}, Methods: {_methodCache.Count}";
    }
}
