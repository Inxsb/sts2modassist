# StS2 Assistant Mod

AI-powered helper mod for Slay the Spire 2 (Godot 4.5.1 Mono) that provides real-time battle analysis, card recommendations, and build-based reward suggestions.

## Features

- **Real-time Battle Analysis**: Analyzes your hand, energy, and enemy intents to suggest optimal plays
- **Card Scoring System**: Uses a comprehensive formula considering base utility, synergies, threat level, and build alignment
- **Draw Probability Calculator**: Shows hypergeometric probability of drawing needed cards next turn
- **Enemy Intent Counterplay**: Warns about lethal threats and suggests defensive options
- **Build-Based Recommendations**: Load custom build profiles to get personalized card/reward suggestions
- **Non-Intrusive UI**: Overlay renders above game without blocking mouse input

## Installation

### Prerequisites

1. **.NET 9.0 SDK** - Download from https://dotnet.microsoft.com/download
2. **Godot 4.5.1 Mono** - Ensure you have the Mono version installed
3. **Slay the Spire 2** - With modding support enabled

### Build Steps

1. **Clone or copy this mod folder** to your workspace:
   ```bash
   cd /path/to/StS2Assistant
   ```

2. **Set up Godot Sharp references**:
   
   **Option A: GodotSharp в папке игры (рекомендуется)**
   
   Если GodotSharp.dll находится в папке `data_sts2_windows_x86_64` рядом с проектом:
   ```bash
   # Убедитесь, что структура папок выглядит так:
   # StS2Assistant/
   # └── ../data_sts2_windows_x86_64/GodotSharp.dll
   ```
   
   **Option B: Создать local.props файл**
   
   Скопируйте `local.props.example` в `local.props` и укажите путь:
   ```xml
   <Project>
     <PropertyGroup>
       <!-- Путь к папке с GodotSharp.dll -->
       <GODOT_SHARP_PATH>../data_sts2_windows_x86_64</GODOT_SHARP_PATH>
     </PropertyGroup>
   </Project>
   ```
   
   **Option C: Переменная окружения**
   ```bash
   # Windows
   setx GODOT_SHARP_PATH "C:\Path\To\Slay the Spire 2\data_sts2_windows_x86_64"
   
   # Linux
   export GODOT_SHARP_PATH=~/.steam/steam/steamapps/common/Slay the Spire 2/data_sts2_linux_x86_64
   ```

3. **Restore NuGet packages**:
   ```bash
   dotnet restore
   ```

4. **Build the mod**:
   ```bash
   dotnet publish -c Release -o bin/publish
   ```

5. **Copy to game mods folder**:
   ```bash
   # Создайте папку мода в директории игры
   mkdir -p "Slay the Spire 2/mods/StS2Assistant"
   
   # Скопируйте DLL и манифест
   cp bin/publish/StS2Assistant.dll "Slay the Spire 2/mods/StS2Assistant/"
   cp mod.json "Slay the Spire 2/mods/StS2Assistant/"
   ```

   Итоговая структура должна быть:
   ```
   Slay the Spire 2/mods/StS2Assistant/
   ├── StS2Assistant.dll
   └── mod.json
   ```

6. **Launch the game** and enable the mod in Settings → General → Modding

## Usage

### Hotkeys

| Key | Action |
|-----|--------|
| F8 | Toggle overlay on/off |
| F9 | Toggle debug information |
| Ctrl+Esc | Reset recommendation cache |

### UI Elements

The overlay displays:
- **Recommendations**: Top suggested plays with priority indicators
- **Battle State**: Current HP, energy, and block values
- **Next Turn Draws**: Probability of drawing key cards

### Build Profiles

Build profiles define your preferred archetype for personalized recommendations.

#### Using Predefined Builds

The mod includes 4 predefined builds:
- **Poison Silent** - Attrition gameplay with poison stacking
- **Strength Ironclad** - Scale strength for massive damage
- **Orb Defect** - Generate and evoke orbs efficiently
- **Divinity Watcher** - Burst damage with divinity stance

#### Creating Custom Builds

1. Navigate to `user://StS2Assistant/` (Godot's user data directory)
2. Edit `builds.json` or create a new JSON file with this structure:

```json
{
  "Id": "my_custom_build",
  "Name": "My Custom Build",
  "CharacterClass": "Silent",
  "PriorityCards": ["Catalyst", "Noxious Fumes"],
  "SynergyRelics": ["Snecko Eye", "Chemical X"],
  "PriorityTags": ["poison"],
  "PreferredTypes": ["Skill", "Attack"],
  "Playstyle": "attrition",
  "RiskTolerance": 0.4
}
```

## Configuration

Edit `user://StS2Assistant/settings.json`:

```json
{
  "OverlayEnabled": true,
  "CardHighlightingEnabled": true,
  "ShowDrawProbabilities": true,
  "AutoSelectBuild": true,
  "OverlayPosition": 1,
  "OverlayOpacity": 0.95,
  "DebugMode": false,
  "MinRecommendationScore": 3.0,
  "ToggleHotkey": "F8"
}
```

## Technical Details

### Card Scoring Formula

```
Score = (BaseUtility × SynergyMult) − (EnergyCost × 1.2) + (ThreatLevel × 0.8) + BuildAlignment
```

Where:
- **BaseUtility**: Value from card stats (damage, block, magic number)
- **SynergyMult**: 1.0 + 0.15 × number of active synergies
- **EnergyCost**: Card energy cost (penalized)
- **ThreatLevel**: Normalized expected enemy damage (0.0–1.0)
- **BuildAlignment**: Bonus for matching build priorities (+0.5 to +2.0)

### Draw Probability

Uses hypergeometric distribution:
```
P(X=k) = C(K,k) × C(N-K, n-k) / C(N, n)
```

Where:
- N = deck size
- K = copies of desired card
- n = cards drawn next turn
- k = desired copies (usually 1)

## Troubleshooting

### Mod doesn't load
1. Check that `mod.json` is in the correct location
2. Verify `godot_version` matches your game version (4.5.1)
3. Check game logs for error messages

### No recommendations showing
1. Ensure you're in a battle (mod only works during combat)
2. Check that the overlay is enabled (F8)
3. Verify the mod has loaded correctly in the mod menu

### Build profiles not loading
1. Check `user://StS2Assistant/builds.json` exists
2. Validate JSON syntax
3. Restart the game after adding new builds

## Development

### Project Structure

```
StS2Assistant/
├── Main.cs                 # Entry point (Godot Node)
├── Core/
│   ├── GameState.cs        # Immutable state records
│   ├── StateTracker.cs     # Harmony patch management
│   └── ReflectionCache.cs  # Safe reflection access
├── Logic/
│   ├── AnalysisEngine.cs   # Card scoring
│   ├── ScoringFormula.cs   # Score calculation
│   ├── DrawProbability.cs  # Hypergeometric calc
│   └── RecommendationSystem.cs
├── UI/
│   ├── OverlayUI.cs        # Main overlay display
│   └── CardHighlighter.cs  # Card visual effects
├── Config/
│   ├── BuildProfile.cs     # Build DTOs
│   └── ConfigManager.cs    # Config file handling
└── default_builds/         # Sample build JSONs
```

### Building for Debug

```bash
dotnet build -c Debug
```

Debug builds include additional logging output visible in the Godot console.

## Dependencies

- **Alchyr.Sts2.BaseLib** (NuGet) - Access to StS2 game types
- **Lib.Harmony** (NuGet) - Method patching framework
- **System.Text.Json** (NuGet) - JSON serialization
- **GodotSharp** - Godot 4.5.1 Mono bindings

## License

This mod is provided as-is for educational purposes. Use at your own risk.

## Credits

- Built with Godot 4.5.1 Mono
- Uses Harmony 2.x for patching
- Inspired by community mods for StS 1

---

For issues or questions, check the game's modding Discord or forums.
