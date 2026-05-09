# StS2 Assistant Mod - Готовый к компиляции мод-помощник для Slay the Spire 2

## 📋 Описание

Мод предоставляет AI-помощника для Slay the Spire 2, который:
- **Анализирует боевую ситуацию** в реальном времени
- **Подсказывает розыгрыш карт** на основе скоринга
- **Прогнозирует доброс** карт (гипергеометрическое распределение)
- **Читает интенты врагов** и советует контр-стратегии
- **Рекомендует награды** под выбранный билд

## 🏗️ Архитектура

```
StS2Assistant/
├── Main.cs                    # Точка входа мода
├── Core/
│   ├── GameState.cs           # DTO состояния игры
│   ├── StateTracker.cs        # Harmony-хуки + отслеживание
│   └── ReflectionCache.cs     # Кэш рефлексии
├── Logic/
│   ├── AnalysisEngine.cs      # Скоринг карт, вероятности
│   └── RecommendationSystem.cs # Генерация подсказок
├── UI/
│   └── OverlayUI.cs           # Godot UI оверлей
├── Config/
│   ├── ConfigManager.cs       # JSON конфигурация
│   └── BuildProfile.cs        # DTO билда
├── default_builds/            # Примеры профилей
│   ├── poison_silent.json
│   ├── strength_warrior.json
│   └── power_shiv_rogue.json
├── StS2Assistant.csproj       # Проект .NET 9.0
├── mod.cfg                    # GUMM manifest
└── README.md                  # Этот файл
```

## ⚙️ Технические требования

### Обязательные компоненты:
1. **.NET 9.0 SDK** - https://dotnet.microsoft.com/download/dotnet/9.0
2. **Godot 4.5.1 Mono** - https://godotengine.org/download/archive/4.5.1-stable/
3. **GUMM (Godot Universal Mod Manager)** - установлен в StS2

### NuGet зависимости (автоматически):
- `Alchyr.Sts2.BaseLib` v1.0.0
- `Alchyr.Sts2.ModAnalyzers` v1.0.0
- `System.Text.Json` v9.0.0
- `Lib.Harmony` v2.3.3

## 📦 Установка

### Шаг 1: Подготовка окружения

```bash
# Проверка .NET SDK
dotnet --version  # Должно быть 9.0.x

# Проверка Godot
godot --version   # Должно быть 4.5.1
```

### Шаг 2: Сборка мода

```bash
cd StS2Assistant

# Восстановление зависимостей
dotnet restore

# Сборка Debug
dotnet build

# Сборка Release для публикации
dotnet publish -c Release -o ./publish
```

### Шаг 3: Установка в GUMM

1. Скопируйте содержимое папки `publish/` в:
   ```
   <StS2 Installation>/mods/StS2Assistant/
   ```

2. Или создайте PCK-пакет:
   ```bash
   godot --headless --build-solutions --export-release "StS2Assistant" StS2Assistant.pck
   ```

3. Активируйте мод в GUMM меню игры

## 🎮 Использование

### Горячие клавиши:
| Клавиша | Действие |
|---------|----------|
| **F8** | Вкл/Выкл оверлей |
| **F9** | Режим отладки |
| **Esc** | Сброс текущей подсказки |

### Настройка билдов

Профили билдов хранятся в:
```
user://StS2Assistant/builds/
```

Пример профиля (`poison_silent.json`):
```json
{
  "Name": "Poison Silent",
  "PriorityCards": ["Noxious Fumes", "Catalytic Flower", "Bane"],
  "SynergyRelics": ["Snecko Eye", "Chemical X", "Toxic Egg"],
  "Playstyle": "attrition_control",
  "RiskTolerance": 0.4
}
```

### Формула скоринга карт

```
Score = (BaseUtility × SynergyMult) − (EnergyCost × 1.2) + (ThreatLevel × 0.8) + BuildAlignment
```

Где:
- **BaseUtility**: урон/блок/контроль в зависимости от типа карты
- **SynergyMult**: 1.0 + 0.15 × кол-во активных синергий
- **ThreatLevel**: нормализованный ожидаемый урон врага (0.0-1.0)
- **BuildAlignment**: бонус за соответствие приоритетам билда

## 🔧 Разработка

### Структура проекта

#### Core/
- `GameState.cs` - Immutable DTO с состоянием игрока, врагов, карт
- `StateTracker.cs` - Harmony патчи для отслеживания событий игры
- `ReflectionCache.cs` - Кэширование反射 для производительности

#### Logic/
- `AnalysisEngine.cs` - Расчёт скоринга, вероятностей добора, угроз
- `RecommendationSystem.cs` - Генерация понятных подсказок

#### UI/
- `OverlayUI.cs` - Godot Control nodes для оверлея

### Добавление новых патчей

В `StateTracker.cs` добавьте новые Harmony-патчи:

```csharp
public void ApplyPatches(Harmony harmony)
{
    // Пример патча
    PatchMethod(harmony, "AbstractPlayer.Draw", 
                typeof(StateTracker), nameof(OnCardDrawn_Postfix));
}

public static void OnCardDrawn_Postfix(object __instance, int amount)
{
    GD.Print($"[StS2Assistant] Cards drawn: {amount}");
    Main.Instance?.UpdateGameState(BuildCurrentState());
}
```

### Логирование

Все логи используют префикс `[StS2Assistant]`:
```csharp
GD.Print("[StS2Assistant] Initialization complete");
GD.PrintErr("[StS2Assistant] Error: " + ex.Message);
```

## 📝 Конфигурация

### mod_config.json (в user://StS2Assistant/)

```json
{
  "OverlayEnabled": true,
  "DebugMode": false,
  "AutoUpdateRecommendations": true,
  "UpdateIntervalMs": 500,
  "DefaultBuildProfile": "poison_silent.json"
}
```

## ⚠️ Ограничения

1. **Только GUMM API + Harmony** - никакого ReadProcessMemory
2. **Godot 4.5.1 Mono** - строго эта версия для совместимости
3. **Не блокировать клики** - `MouseFilter = Ignore` для UI
4. **Асинхронные расчёты** - тяжёлые вычисления в `Task.Run`

## 🐛 Отладка

### Включение режима отладки:
1. Нажмите **F9** в игре
2. Или установите `"DebugMode": true` в config

### Просмотр логов:
```bash
# Логи Godot
tail -f ~/.local/share/godot/app_userdata/SlayTheSpire2/logs/
```

## 📚 Ресурсы

- [BaseLib Wiki](https://alchyr.github.io/BaseLib-Wiki/)
- [Mod Template StS2](https://github.com/Alchyr/ModTemplate-StS2)
- [GUMM Documentation](https://github.com/KoBeWi/Godot-Universal-Mod-Manager)
- [HelloWorld Example](https://github.com/giulianoconte/slay-the-spire-2-mod-guide)

## 📄 Лицензия

MIT License - свободное использование и модификация.

## 👥 Авторы

Создано как пример мода-помощника для StS2 на Godot 4.5.1 Mono.
