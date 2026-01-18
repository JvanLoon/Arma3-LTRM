# Arma 3 LTRM (Lowlands Tactical Repo Manager)

A modern Windows desktop application for managing and synchronizing Arma 3 mod repositories via FTP, designed to streamline the process of keeping game modifications up-to-date for multiplayer gaming communities.

## Overview

Arma 3 LTRM provides a user-friendly interface to:
- Connect to multiple FTP repositories hosting Arma 3 mods
- Create custom event configurations with specific mod selections
- Intelligently sync mod files (downloads only new/changed files)
- Browse FTP servers with an interactive tree view
- Launch Arma 3 with automatically configured mods

## Features

### Repository Management
- **Multiple Repository Support**: Add and manage multiple FTP servers
- **Intelligent Syncing**: Only downloads new or modified files based on size and timestamp comparison
- **Multi-threaded Downloads**: Up to 8 concurrent file downloads for faster synchronization
- **Directory Caching**: Pre-scan entire repository structure for optimized downloads
- **Progress Tracking**: Real-time download progress with detailed logging
- **Connection Testing**: Verify repository connectivity before syncing
- **MLSD Support**: Modern FTP listing with automatic fallback to legacy modes

### Event Management
- **Custom Event Creation**: Define collections of mods from one or more repositories
- **FTP Browser**: Interactive tree view to browse and select folders from repositories
- **Multi-Repository Events**: Combine mod folders from different FTP servers in a single event
- **Selective Downloads**: Download only the folders you need for specific events
- **Event Persistence**: Events saved as JSON files for easy management and sharing

## Technologies Used

### Framework & Language
- **.NET 10**: Latest .NET framework for modern C# features
- **C# 14.0**: Taking advantage of latest language features
- **WPF (Windows Presentation Foundation)**: Rich desktop UI with XAML

### Architecture & Patterns
- **MVVM-inspired**: Separation of concerns with Models, Services, and Views
- **Async/Await**: Non-blocking UI during file downloads and operations
- **IProgress<T>**: Real-time progress reporting for long-running operations
- **INotifyPropertyChanged**: Data binding for reactive UI updates
- **ObservableCollection**: Automatic UI synchronization for collection changes
- **Parallel Processing**: ConcurrentQueue and ConcurrentBag for thread-safe operations
- **Semaphore-based Throttling**: SemaphoreSlim for download concurrency control

### Key Technologies
- **FTP Protocol**: Legacy `FtpWebRequest` for maximum compatibility
  - MLSD support for modern FTP servers (structured listing)
  - LIST and LIST -al fallback for legacy servers
  - File size and timestamp retrieval
  - Binary file transfer with 64KB buffer
  - Directory caching for optimized scanning
- **JSON Serialization**: Configuration persistence (`System.Text.Json`)
- **File I/O**: Efficient file operations with buffered streams
- **Multi-threading**: Up to 8 concurrent downloads with semaphore limiting

### Project Structure
```
Arma 3 LTRM/
│   Models/
│   ├── Repository.cs          - Data model for FTP repository configuration
│   ├── Event.cs               - Event and ModFolder data models
│   ├── AppSettings.cs         - Application settings model
│   ├── LaunchParameters.cs    - Arma 3 launch parameters configuration
│   Services/
│   ├── FtpManager.cs          - FTP operations, file synchronization, and browsing
│   ├── RepositoryManager.cs   - Repository persistence and CRUD
│   ├── EventManager.cs        - Event persistence and CRUD
│   ├── SettingsManager.cs     - Application settings management
│   ├── LaunchParametersManager.cs - Launch parameter configuration and generation
│   Views/
│   ├── MainWindow.xaml(.cs)   - Primary tabbed interface (Home, Manage Repos, Events, Settings)
│   ├── AddRepositoryWindow.xaml(.cs) - Repository add/edit dialog
│   ├── AddEditEventWindow.xaml(.cs) - Event creation with FTP browsing
│   ├── SettingsWindow.xaml(.cs) - Settings configuration dialog
│   └── DownloadProgressWindow.xaml(.cs) - Download progress display
├── App.xaml(.cs)              - Application entry point
└── AssemblyInfo.cs            - Assembly metadata
```

## How It Works

### FTP Synchronization Algorithm
1. **Connect**: Establish FTP connection using provided credentials
2. **Cache Build**: Recursively scan entire repository structure upfront (up to 8 concurrent operations)
3. **List**: Retrieve directory listing using best available method:
   - Try MLSD (modern structured listing) first
   - Fall back to LIST -al (detailed listing)
   - Fall back to LIST with parallel size checks if needed
4. **Parse**: Extract file metadata (name, size, timestamp, directory flag)
5. **Compare**: Check local files against remote:
   - If file doesn't exist locally ? download
   - If sizes differ ? download
   - If remote is newer (with 2-second tolerance for FTP precision) ? download
   - Otherwise ? skip (already up-to-date)
6. **Download**: Stream files in 64KB chunks with up to 8 concurrent downloads
7. **Timestamp Sync**: Set local file timestamps to match remote files
8. **Recurse**: Process subdirectories from cache

### Launch Process
1. User selects repositories or events from Home tab
2. Configure launch parameters (optional):
   - Profile settings (custom profile path, unit name)
   - Mission selection
   - Display options (windowed mode)
   - Game loading optimizations (no splash, skip intro, etc.)
   - Developer/debug options
   - Server configuration paths
   - Custom command-line parameters
3. Choose action: Download, Launch, or Download & Launch
4. For downloads:
   - Repositories: Download entire FTP structure to `Downloads/Repositories/{RepoName}/`
   - Events: Download specific folders from multiple repositories maintaining full path structure
5. For launch:
   - Recursively scan download paths for folders starting with `@`
   - Build `-mod=` parameter with all found mod folders
   - Apply all configured launch parameters
   - Generate complete command-line argument string
6. Launches `arma3_x64.exe` or `arma3.exe` with generated mod and launch parameters

## Installation

### Prerequisites
- Windows 10/11 (64-bit)
- .NET 10 Runtime ([Download](https://dotnet.microsoft.com/download/dotnet/10.0))
- Arma 3 installed on your system

### Setup
1. Download the latest release from the [Releases](https://github.com/BrainlessBen/Arma3-LTRM/releases) page
2. Extract the archive to your preferred location
3. Run `Arma 3 LTRM.exe`
4. On first launch, select your `arma3.exe` location

## Usage

### Adding a Repository
1. Navigate to the **Manage Repositories** tab
2. Click **Add Repository**
3. Enter repository details:
   - Name (e.g., "Lowlands Tactical Mods")
   - FTP URL (without `ftp://` prefix)
   - Port (default: 21)
   - Username and Password
4. Click **OK**
5. Optionally test the connection using **Test Connection** button

### Creating Events
1. Go to the **Manage Events** tab
2. Click **Add Event**
3. Enter an event name
4. Select a repository from the dropdown
5. Click **Browse Repository Folders** to explore the FTP server
6. Check folders you want to include (only @ folders are selectable)
7. Click **Add Selected** to add folders to the event
8. Repeat steps 4-7 to add folders from other repositories
9. Click **Save** to create the event

### Launching Arma 3
1. Go to the **Home** tab
2. For repositories:
   - Select one or more repositories from the list
   - Click **Download & Launch** (or **Download** only, or **Launch** with existing files)
3. For events:
   - Select one or more events from the list
   - Click **Download & Launch** (or **Download** only, or **Launch** with existing files)
4. Wait for download to complete (if applicable)
5. Arma 3 launches automatically with all @ folders found in the download path(s)

### Configuring Launch Parameters
The application supports extensive Arma 3 launch parameter configuration:

**Profile Options:**
- Profile Path: Specify custom profile directory
- Unit: Set custom unit name

**Mission:**
- Load Mission: Launch directly into a specific mission file

**Display Options:**
- Windowed Mode: Run in borderless window mode

**Game Loading Speedup:**
- No Splash: Skip splash screens
- Skip Intro: Skip intro videos
- Empty World: Start with empty world for faster loading
- Enable HT: Enable hyper-threading

**Developer & Debug Options:**
- Show Script Errors: Display script error messages
- No Pause: Disable pause when window loses focus
- No Pause Audio: Continue audio when window loses focus
- No Logs: Disable log file creation
- No Freeze Check: Disable freeze detection
- No File Patching: Disable file patching for better performance
- Debug: Enable debug mode

**Server Options:**
- Config Path: Custom server config file location
- BattlEye Path: Custom BattlEye directory

**Custom Parameters:**
- Add any additional command-line parameters as needed

All configured parameters are automatically applied when launching Arma 3 with your selected mods.

## Configuration Files

### repositories.json
Stored in the application directory, contains:
```json
[
  {
    "Id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "Name": "Example Repository",
    "Url": "ftp.example.com",
    "Port": 21,
    "Username": "user",
    "Password": "pass"
  }
]
```

### settings.json
Stored in the application directory, contains:
```json
{
  "Arma3ExePath": "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Arma 3\\arma3_x64.exe",
  "BaseDownloadLocation": "C:\\Users\\YourName\\Documents\\Arma3-LTRM\\Downloads",
  "HiddenRepositories": [],
  "HiddenEvents": []
}
```

### Settings/Events/{EventName}.json
One file per event in the Settings/Events directory:
```json
{
  "Name": "Weekly Ops",
  "Repositories": [
    {
      "Id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "Name": "Main Repo",
      "Url": "ftp.example.com",
      "Port": 21,
      "Username": "user",
      "Password": "pass"
    }
  ],
  "ModFolders": [
    {
      "RepositoryId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "FolderPath": "/@ace"
    },
    {
      "RepositoryId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "FolderPath": "/@CBA_A3"
    }
  ]
}
```

## Known Limitations
- FTP only (no SFTP/FTPS support currently)
- Passwords stored in plain text in configuration files
- Windows-only (WPF framework limitation)
- No automatic update checking for the application itself
- Download paths maintain full FTP structure (event folders are not isolated in separate subdirectories)

## Contributing
Contributions are welcome! Please feel free to submit pull requests or open issues for bugs and feature requests.

## License
This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgments
- Built for the Lowlands Tactical Arma 3 community
- Inspired by the need for simple, reliable mod synchronization
- Thanks to all contributors and testers

## Support
For issues, questions, or suggestions:
- Open an issue on [GitHub](https://github.com/BrainlessBen/Arma3-LTRM/issues)
- Contact the development team through the Lowlands Tactical community

---

**Note**: This tool is not affiliated with or endorsed by Bohemia Interactive, the developers of Arma 3.
