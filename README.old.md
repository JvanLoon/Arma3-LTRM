# Arma 3 LTRM (Lowlands Tactical Repo Manager)

A modern Windows desktop application for managing and synchronizing Arma 3 mod repositories via FTP, designed to streamline the process of keeping game modifications up-to-date for multiplayer gaming communities.

## Overview

Arma 3 LTRM provides a user-friendly interface to:
- Connect to multiple FTP repositories hosting Arma 3 mods
- Intelligently sync mod files (downloads only new/changed files)
- Manage addon groups and startup parameters
- Launch Arma 3 with custom configurations

## Features

### Repository Management
- **Multiple Repository Support**: Add and manage multiple FTP servers
- **Intelligent Syncing**: Only downloads new or modified files based on size and timestamp comparison
- **Progress Tracking**: Real-time download progress with detailed logging
- **Connection Testing**: Verify repository connectivity before syncing

### Mod Management
- **Folder Organization**: Add multiple mod directories to your collection
- **Addon Browser**: Visual tree view of available addons in your mod folders
- **Drag & Drop**: Easy mod activation via drag-and-drop interface
- **Startup Groups**: Create and manage groups of mods to load on game launch

### Launch Parameters
- **Predefined Options**: Quick toggles for common Arma 3 launch parameters:
  - Windowed mode
  - Empty world
  - Skip intro
  - No splash screens
  - No pause on focus loss
  - File patching (for mission development)
  - Script error display
  - And more...
- **Custom Parameters**: Add additional command-line arguments
- **Real-time Preview**: See generated launch parameters before starting the game

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

### Key Technologies
- **FTP Protocol**: Legacy `FtpWebRequest` for maximum compatibility
  - List directory and directory details parsing
  - File size and timestamp retrieval
  - Binary file transfer
- **JSON Serialization**: Repository configuration persistence (`System.Text.Json`)
- **File I/O**: Efficient file operations with buffered streams
- **Drag & Drop**: Native Windows drag-and-drop for intuitive UX

### Project Structure
```
Arma 3 LTRM/
??? Models/
?   ??? Repository.cs          - Data model for FTP repository configuration
??? Services/
?   ??? FtpManager.cs          - FTP operations and file synchronization
?   ??? ModManager.cs          - Mod folder and addon management
?   ??? RepositoryManager.cs   - Repository persistence and CRUD
?   ??? LaunchParametersManager.cs - Game launch parameter generation
??? Views/
?   ??? MainWindow.xaml(.cs)   - Primary application window
?   ??? AddRepositoryWindow.xaml(.cs) - Repository add/edit dialog
?   ??? ModListSelectionWindow.xaml(.cs) - Mod folder selection
?   ??? DownloadProgressWindow.xaml(.cs) - Download progress display
??? App.xaml(.cs)              - Application entry point
??? AssemblyInfo.cs            - Assembly metadata
```

## How It Works

### FTP Synchronization Algorithm
1. **Connect**: Establish FTP connection using provided credentials
2. **List**: Retrieve directory listing (tries `LIST` then falls back to `LIST -l`)
3. **Parse**: Extract file metadata (name, size, timestamp, directory flag)
4. **Compare**: Check local files against remote:
   - If file doesn't exist locally ? download
   - If sizes differ ? download
   - If remote is newer (with 2-second tolerance for FTP precision) ? download
   - Otherwise ? skip (already up-to-date)
5. **Download**: Stream files in 8KB chunks with progress reporting
6. **Timestamp Sync**: Set local file timestamps to match remote files
7. **Recurse**: Process subdirectories recursively

### Launch Process
1. User enables repositories and selects download location
2. Application syncs all enabled repositories sequentially
3. After successful sync (or user confirmation if partial failure)
4. Application builds command-line arguments from:
   - Selected launch options (checkboxes)
   - Activated mod paths (from startup groups)
   - Custom parameters (text input)
5. Launches `arma3.exe` with generated parameters

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
1. Navigate to the **Repositories** tab
2. Click **Add Repository**
3. Enter repository details:
   - Name (e.g., "Lowlands Tactical Mods")
   - FTP URL (without `ftp://` prefix)
   - Port (default: 21)
   - Username and Password
4. Click **OK**
5. Enable the repository checkbox to include it in syncs

### Managing Mods
1. Go to the **Addons** tab
2. Click **+** to add mod folders from your system
3. Browse available addons in the left tree view
4. Drag addons to the **Addon Groups** on the right to activate them
5. Check/uncheck addons in groups to control which load on launch

### Launching Arma 3
1. Configure your desired **Launcher Options**
2. Review **Run Parameters** preview
3. Click **Launch**
4. If repositories are enabled, select a sync destination
5. Wait for sync to complete
6. Arma 3 launches automatically with your configuration

## Configuration Files

### repositories.json
Stored in the application directory, contains:
```json
[
  {
    "IsEnabled": true,
    "Name": "Example Repository",
    "Url": "ftp.example.com",
    "Port": 21,
    "Username": "user",
    "Password": "pass"
  }
]
```

## Known Limitations
- FTP only (no SFTP/FTPS support currently)
- Passwords stored in plain text in `repositories.json`
- Windows-only (WPF framework limitation)
- No automatic update checking
- Single-threaded FTP downloads (sequential file processing)

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
