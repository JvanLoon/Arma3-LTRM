# Changelog

## [2.0.0] - Major Redesign

### Added
- **Event System**: Create custom mod collections from repository folders
- **Settings Window**: Centralized configuration for Arma3.exe and download paths
- **Multi-threaded Downloads**: Up to 4 concurrent file downloads
- **Modern UI**: Dark theme with tabbed interface (Home, Manage Repositories, Manage Events)
- **Download Options**: Separate "Download", "Launch", and "Download & Launch" actions
- **Event Management**: Add, edit, delete event configurations
- **Repository Management UI**: Dedicated tab for managing FTP servers
- **SimpleProgressWindow**: Clean download progress display
- **JSON Event Storage**: One file per event in `/Events` folder
- **Settings Persistence**: `settings.json` for app configuration
- **BaseDownloadLocation**: Configurable base path for all downloads
- **Event Download Isolation**: Each event downloads to its own folder
- **Repository Download Isolation**: Each repository downloads to its own folder

### Changed
- **Complete UI Redesign**: From multi-panel window to tabbed interface
- **Workflow Simplification**: Removed addon browser, drag-drop, startup groups
- **Repository Model**: Removed `IsEnabled` property
- **FtpManager**: Changed to async with parallel downloads
- **Download Location**: Now organized as `Downloads/Repositories/` and `Downloads/Events/`
- **Launch Process**: Simplified to scan for `@` folders and build mod parameters
- **README**: Complete rewrite with new architecture documentation

### Removed
- **Addon Browser**: Tree view of available addons
- **Mod Folders Management**: Manual folder adding UI
- **Startup Groups**: Drag-and-drop addon activation
- **Launcher Options Tab**: Individual launch parameter toggles
- **Custom Parameters**: Additional command-line arguments UI
- **Enable/Disable Repositories**: Now selection-based
- **Automatic Arma3.exe Prompt**: Now in Settings menu
- **ModManager Service**: No longer needed
- **LaunchParametersManager Service**: Replaced with simple launch logic
- **ModListSelectionWindow**: No longer needed
- **DownloadProgressWindow**: Replaced with SimpleProgressWindow

### Fixed
- FTP download efficiency with concurrent processing
- Ambiguous reference errors with proper using directives
- XML entity errors in XAML (ampersands)

### Technical
- Upgraded to .NET 10
- Added `SemaphoreSlim` for download concurrency control
- Implemented async/await throughout FTP operations
- Added EventManager and SettingsManager services
- Created Event and AppSettings models
- Added ModFolder model for event configuration

### Breaking Changes
- **No Migration**: v1.x configurations not compatible
- **New File Structure**: Different JSON schemas
- **Removed Features**: Must adapt workflow to event system
- **Different Download Paths**: Files stored in new locations

### Migration Guide
1. Note your v1.x repository details and mod folders
2. Install v2.0
3. Add repositories in "Manage Repositories" tab
4. Create events in "Manage Events" tab
5. Add mod folders to events using repository + folder path
6. Configure Arma3.exe path in Settings (File ? Settings)
7. Download and test events from Home tab

## [1.0.0] - Initial Release

### Added
- FTP repository management
- Intelligent file synchronization
- Addon browser with tree view
- Drag-and-drop addon activation
- Startup groups
- Launch parameter configuration
- Repository enable/disable toggle
- Download progress tracking
- Connection testing
