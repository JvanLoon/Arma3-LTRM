# Launch Parameters System

## Overview
The Launch Parameters system provides comprehensive control over Arma 3 startup configuration, allowing users to customize game behavior, performance, and developer options through an intuitive interface.

## Architecture

### Models/LaunchParameters.cs
A complete data model implementing `INotifyPropertyChanged` for reactive UI binding.

**Categories:**

#### Profile Options
- **ProfilePath** (`string`): Custom profile directory path
  - Generates: `-profiles="{path}"`
- **Unit** (`string`): Custom unit name
  - Generates: `-unit={name}`

#### Mission
- **UseMission** (`bool`): Enable mission loading on startup
- **MissionPath** (`string`): Path to mission file
  - Generates: `"{missionPath}"` (when UseMission is true)

#### Display Options
- **Windowed** (`bool`): Run in borderless window mode
  - Generates: `-window -noWindowBorder`

#### Game Loading Speedup
- **NoSplash** (`bool`): Skip splash screens
  - Generates: `-nosplash`
- **SkipIntro** (`bool`): Skip intro videos
  - Generates: `-skipIntro`
- **EmptyWorld** (`bool`): Start with empty world for faster loading
  - Generates: `-world=empty`
- **EnableHT** (`bool`): Enable hyper-threading
  - Generates: `-enableHT`

#### Miscellaneous Options
- **ShowScriptErrors** (`bool`): Display script error messages
  - Generates: `-showScriptErrors`
- **NoPause** (`bool`): Disable pause when window loses focus
  - Generates: `-noPause`
- **NoPauseAudio** (`bool`): Continue audio when window loses focus
  - Generates: `-noPauseAudio`
- **NoLogs** (`bool`): Disable log file creation
  - Generates: `-noLogs`
- **NoFreezeCheck** (`bool`): Disable freeze detection
  - Generates: `-noFreezeCheck`
- **NoFilePatching** (`bool`): Disable file patching
  - Generates: `-noFilePatching`
- **Debug** (`bool`): Enable debug mode
  - Generates: `-debug`

#### Server Options
- **Config** (`string`): Custom server config file path
  - Generates: `-config="{path}"`
- **BePath** (`string`): Custom BattlEye directory
  - Generates: `-bePath="{path}"`

### Services/LaunchParametersManager.cs
Manages launch parameter configuration, mod paths, and command-line generation.

**Key Features:**

#### Profile Discovery
```csharp
public ObservableCollection<string> GetAvailableProfiles()
```
Scans for Arma 3 profiles in:
- `C:\Users\{UserName}\Documents\Arma 3`
- `C:\Users\{UserName}\Documents\Arma 3 - Other Profiles\*`

Returns an `ObservableCollection<string>` of discovered profile directories.

#### Mod Path Management
```csharp
public void UpdateModsList(List<string> modPaths)
```
Updates the internal mod paths list and triggers `ParametersChanged` event.

#### Parameter String Generation
```csharp
public string GetParametersString(string customParameters = "")
```
Generates complete command-line parameter string with:
1. Profile settings
2. Unit configuration
3. Mission path (if enabled)
4. Display options
5. Game loading speedup flags
6. Miscellaneous developer options
7. Server configuration
8. Custom parameters (parsed and formatted)
9. Mod paths (as `-mod="{path}"` for each)

**Custom Parameter Parsing:**
- Accepts multiline input
- Splits by `\r` and `\n`
- Trims whitespace
- Automatically prefixes with `-` if not present
- Skips empty lines

**Output Format:**
Each parameter on a new line, ready for process startup:
```
-profiles="C:\Users\Example\Documents\Arma 3"
-unit=MyUnit
-window
-noWindowBorder
-nosplash
-skipIntro
-mod="@CBA_A3"
-mod="@ace"
```

#### Event System
```csharp
public event EventHandler? ParametersChanged
```
Fires when:
- LaunchParameters property changes
- Any LaunchParameters sub-property changes
- Mod paths are updated

## Integration with Main Application

### Workflow
1. User configures launch parameters through UI (bound to LaunchParameters model)
2. User selects repositories or events to launch
3. Main application collects mod paths from downloaded folders
4. Calls `LaunchParametersManager.UpdateModsList(modPaths)`
5. Calls `LaunchParametersManager.GetParametersString(customParams)`
6. Launches Arma 3 with generated command-line arguments

### Usage Example
```csharp
var launchParamsManager = new LaunchParametersManager();

// Configure parameters
launchParamsManager.LaunchParameters.NoSplash = true;
launchParamsManager.LaunchParameters.SkipIntro = true;
launchParamsManager.LaunchParameters.Windowed = true;
launchParamsManager.LaunchParameters.ProfilePath = @"C:\Users\Example\Documents\Arma 3";

// Update mod paths
var modPaths = new List<string>
{
    @"C:\Games\Arma3\@CBA_A3",
    @"C:\Games\Arma3\@ace"
};
launchParamsManager.UpdateModsList(modPaths);

// Generate command-line parameters
string customParams = "filePatching\nexThreads=7";
string parameters = launchParamsManager.GetParametersString(customParams);

// Launch Arma 3
Process.Start(new ProcessStartInfo
{
    FileName = arma3ExePath,
    Arguments = parameters,
    UseShellExecute = true
});
```

## Benefits

### User Experience
- **Intuitive Configuration**: All options clearly labeled and categorized
- **Profile Management**: Automatic discovery of Arma 3 profiles
- **Flexibility**: Support for custom command-line parameters
- **Persistence**: Settings maintained across application sessions (when saved)

### Developer Experience
- **Type Safety**: Strongly typed boolean flags and string properties
- **Data Binding**: INotifyPropertyChanged enables reactive UI
- **Event-Driven**: ParametersChanged event for custom handling
- **Clean Separation**: Model and service clearly separated
- **Testability**: Service methods are easily unit-testable

### Performance
- **Efficient**: Only generates parameter string when needed
- **Concurrent**: Profile scanning can be optimized if needed
- **Memory**: Lightweight models with minimal overhead

## Future Enhancements

Potential improvements:
1. **Per-Event Parameters**: Save different launch configs for different events
2. **Parameter Profiles**: Save/load named parameter configurations
3. **Validation**: Ensure paths exist before launching
4. **Tooltips**: Add descriptive help text for each parameter
5. **Advanced Options**: Additional parameters like `-cpuCount`, `-maxMem`, etc.
6. **Server Launch**: Dedicated server-specific parameters
7. **Parameter Presets**: Quick presets for "Performance", "Debugging", etc.

## Related Documentation
- [V2_REDESIGN_SUMMARY.md](V2_REDESIGN_SUMMARY.md) - Overall v2.0 architecture
- [README.md](../README.md) - User-facing documentation
