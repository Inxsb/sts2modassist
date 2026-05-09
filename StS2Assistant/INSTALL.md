# Инструкция по установке StS2 Assistant Mod

## Предварительные требования

### 1. Установка .NET 9.0 SDK

**Windows:**
```powershell
# Скачайте установщик с https://dotnet.microsoft.com/download/dotnet/9.0
# Или через winget:
winget install Microsoft.DotNet.SDK.9
```

**Linux (Ubuntu/Debian):**
```bash
wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update && sudo apt-get install -y dotnet-sdk-9.0
```

**macOS:**
```bash
brew install --cask dotnet-sdk
```

**Проверка установки:**
```bash
dotnet --version  # Должно показать 9.0.x
```

### 2. Установка Godot 4.5.1 Mono

**Важно:** Требуется именно версия **4.5.1 Mono**, не стандартная!

**Windows:**
1. Скачайте с https://godotengine.org/download/archive/4.5.1-stable/
2. Выберите `Godot_v4.5.1-stable_mono_win64.zip`
3. Распакуйте в удобное место

**Linux:**
```bash
# Скачайте tar.xz архив для Linux
wget https://github.com/godotengine/godot/releases/download/4.5.1-stable/Godot_v4.5.1-stable_mono_linux.x86_64.tar.xz
tar -xf Godot_v4.5.1-stable_mono_linux.x86_64.tar.xz
```

**Проверка:**
```bash
godot --version  # Должно показать 4.5.1.stable.mono
```

### 3. Установка GUMM в StS2

1. Убедитесь, что у вас установлена Slay the Spire 2
2. Скачайте GUMM с https://github.com/KoBeWi/Godot-Universal-Mod-Manager
3. Скопируйте файлы GUMM в папку с игрой:
   ```
   <StS2 Installation>/
   ```
4. Запустите игру - должно появиться меню модов

## Сборка мода

### Шаг 1: Клонирование/копирование файлов

Убедитесь, что все файлы мода находятся в одной папке:
```
StS2Assistant/
├── Main.cs
├── StS2Assistant.csproj
├── mod.cfg
├── Core/
├── Logic/
├── UI/
└── Config/
```

### Шаг 2: Восстановление зависимостей

```bash
cd StS2Assistant
dotnet restore
```

Ожидаемый вывод:
```
  Determining projects to restore...
  Restored StS2Assistant.csproj (in 5 sec).
  Restore succeeded.
```

### Шаг 3: Сборка

**Debug сборка (для разработки):**
```bash
dotnet build
```

**Release сборка (для публикации):**
```bash
dotnet publish -c Release -o ./publish
```

### Шаг 4: Установка в GUMM

**Вариант A: Ручное копирование**

Скопируйте содержимое папки `publish/` в:
```
<StS2 Installation>/mods/StS2Assistant/
```

**Вариант B: Автоматически (с local.props)**

1. Скопируйте `local.props.example` в `local.props`
2. Отредактируйте пути под вашу систему
3. Выполните:
   ```bash
   dotnet publish -c Release
   ```

## Активация мода

1. Запустите Slay the Spire 2
2. В главном меню откройте **Mods** (GUMM)
3. Найдите **StS2 Assistant** в списке
4. Включите мод (toggle)
5. Перезапустите игру если требуется

## Проверка работы

После запуска игры с активным модом:

1. Зайдите в любой бой
2. Нажмите **F8** - должен появиться оверлей помощника
3. Проверьте логи Godot на наличие ошибок:
   ```bash
   # Linux
   tail -f ~/.local/share/godot/app_userdata/SlayTheSpire2/logs/log.txt
   
   # Windows
   Get-Content "$env:APPDATA\Godot\app_userdata\SlayTheSpire2\logs\log.txt" -Wait
   ```

Ожидаемые сообщения в логе:
```
[StS2Assistant] === Initializing StS2 Assistant Mod ===
[StS2Assistant] Harmony initialized successfully
[StS2Assistant] === Initialization Complete ===
[StS2Assistant.UI] Overlay UI initialized
```

## Настройка профилей билдов

Профили хранятся в:
```
user://StS2Assistant/builds/
```

**Windows:** `%APPDATA%\Godot\app_userdata\SlayTheSpire2\StS2Assistant\builds\`
**Linux:** `~/.local/share/godot/app_userdata/SlayTheSpire2/StS2Assistant/builds/`

### Создание своего профиля

1. Скопируйте пример из `default_builds/`
2. Отредактируйте JSON:
   ```json
   {
     "Name": "My Custom Build",
     "PriorityCards": ["Card1", "Card2"],
     "SynergyRelics": ["Relic1"],
     "Playstyle": "aggressive",
     "RiskTolerance": 0.6
   }
   ```
3. Сохраните в папку builds
4. Выберите в конфиге мода

## Горячие клавиши

| Клавиша | Действие |
|---------|----------|
| **F8** | Вкл/Выкл оверлей |
| **F9** | Режим отладки (логи) |
| **Esc** | Сброс текущей подсказки |

## Решение проблем

### Ошибка: "Could not load file or assembly 'Alchyr.Sts2.BaseLib'"

**Решение:**
```bash
dotnet clean
dotnet restore
dotnet build
```

### Ошибка: "Godot version mismatch"

Убедитесь, что используете Godot **4.5.1 Mono**, не стандартную версию.

### Мод не появляется в GUMM

1. Проверьте структуру папок - `mod.cfg` должен быть в корне папки мода
2. Проверьте `mod.cfg` на корректность
3. Перезапустите игру

### Оверлей не отображается

1. Нажмите F8 для включения
2. Проверьте логи на ошибки UI
3. Убедитесь, что вы в бою (оверлей работает только в бою)

### Синтаксические ошибки при сборке

Проверьте версию .NET:
```bash
dotnet --version  # Должно быть 9.0.x
```

Если версия старее - обновите SDK.

## Дополнительные ресурсы

- [BaseLib Wiki](https://alchyr.github.io/BaseLib-Wiki/)
- [GUMM Documentation](https://github.com/KoBeWi/Godot-Universal-Mod-Manager)
- [Godot 4.x Docs](https://docs.godotengine.org/en/stable/)

## Поддержка

При возникновении проблем:
1. Проверьте логи Godot
2. Включите режим отладки (F9)
3. Создайте issue с логами и описанием проблемы
