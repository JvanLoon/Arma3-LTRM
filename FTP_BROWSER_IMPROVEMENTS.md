# UI Improvements - FTP Browser for Events

## Changes Made

### 1. Modernized AddRepositoryWindow
**File**: `Views/AddRepositoryWindow.xaml`

**Changes**:
- Updated to dark theme matching the rest of the application
- Changed from Label/TextBox grid to StackPanel with TextBlock headers
- Improved button styling with hover effects
- Increased window size for better readability (500x350)
- Changed "OK" to "Save" for consistency

**Visual Improvements**:
- Dark background (#1E1E1E)
- Styled TextBox and PasswordBox controls
- Modern button design with rounded corners
- Better spacing and margins

### 2. FTP Browsing Capability
**File**: `Services/FtpManager.cs`

**New Classes**:
```csharp
public class FtpBrowseItem
{
    public string Name { get; set; }
    public string FullPath { get; set; }
    public bool IsDirectory { get; set; }
}
```

**New Method**:
```csharp
public async Task<List<FtpBrowseItem>> BrowseDirectoryAsync(
    string ftpUrl, int port, string username, string password, string path = "/"
)
```

**Purpose**: Allows browsing FTP directory structure without downloading files

### 3. Complete Event Editor Redesign
**Files**: 
- `Views/AddEditEventWindow.xaml`
- `Views/AddEditEventWindow.xaml.cs`

**New UI Layout**:
```
???????????????????????????????????????????????????????
? Event Name: [text box]                              ?
???????????????????????????????????????????????????????
? Repository: [dropdown] [Browse Repository Folders]  ?
???????????????????????????????????????????????????????
? Repository Folders ? Selected Mod Folders in Event  ?
? (Check to add)     ?                                ?
?                    ?                                ?
? ? /@ACE            ? • Repo1 ? /@ACE               ?
? ? /@RHS_USAF       ? • Repo1 ? /@RHS_USAF         ?
? ? /@CBA_A3         ? • Repo2 ? /@ACRE2            ?
?   ? subfolder      ?                                ?
?                    ?                                ?
???????????????????????????????????????????????????????
        [Add Checked Folders to Event]
                    [Save Event] [Cancel]
```

**Features**:

#### A) FTP Tree Browser (Left Panel)
- **TreeView with Checkboxes**: Browse FTP directory structure
- **Lazy Loading**: Child folders loaded on-demand when expanding nodes
- **Visual Feedback**: Loading states and connection status
- **Multi-Select**: Check multiple folders at once

#### B) Selected Folders List (Right Panel)
- Shows all folders added to the event
- Displays repository name and folder path
- Remove button (?) for each folder
- Prevents duplicate additions

#### C) Workflow
1. User selects repository from dropdown
2. Clicks "Browse Repository Folders"
3. Application connects to FTP and lists folders
4. User checks desired folders in tree
5. Clicks "Add Checked Folders to Event"
6. Folders appear in right panel
7. User saves event

### 4. New Tree Node Class
**File**: `Views/AddEditEventWindow.xaml.cs`

```csharp
public class FtpTreeNode : INotifyPropertyChanged
{
    public string Name { get; set; }
    public string FullPath { get; set; }
    public bool IsDirectory { get; set; }
    public bool IsChecked { get; set; }
    public ObservableCollection<FtpTreeNode> Children { get; set; }
}
```

**Features**:
- INotifyPropertyChanged for two-way binding with checkboxes
- Hierarchical structure for tree view
- Full path tracking for FTP operations

## Technical Implementation

### FTP Browsing Flow

```
1. User clicks "Browse Repository Folders"
   ?
2. BrowseButton_Click()
   - Disables button ("Loading...")
   - Calls FtpManager.BrowseDirectoryAsync("/")
   ?
3. FtpManager lists root directory
   - Uses existing GetDirectoryListing() method
   - Returns List<FtpBrowseItem>
   ?
4. Creates FtpTreeNode for each directory
   - Adds placeholder "Loading..." child node
   ?
5. Populates TreeView
   - Enables "Add Checked Folders" button
   ?
6. User expands node (lazy loading)
   - TreeViewCheckBox_Changed event
   - Loads children from FTP
   - Replaces "Loading..." with actual folders
```

### Adding Folders to Event

```
1. User checks folders in tree
   ?
2. Clicks "Add Checked Folders to Event"
   ?
3. AddSelectedButton_Click()
   - Calls GetCheckedNodes() recursively
   - Filters out duplicates
   ?
4. Creates ModFolder for each checked item
   - Copies repository credentials
   - Sets folder path
   ?
5. Adds to Event.ModFolders collection
   - Observable collection updates UI automatically
   - Unchecks tree items
```

## User Experience Improvements

### Before (Manual Input)
```
1. Select repository
2. Type folder path manually (e.g., "/@ACE")
3. Click "Add Folder"
4. Repeat for each folder
```

**Problems**:
- Error-prone (typos in paths)
- Requires knowledge of folder structure
- No validation of folder existence

### After (FTP Browser)
```
1. Select repository
2. Click "Browse Repository Folders"
3. Check desired folders in tree
4. Click "Add Checked Folders to Event"
```

**Benefits**:
- Visual folder structure
- No typos possible
- Real-time FTP validation
- Multi-select capability
- Lazy loading for performance

## Error Handling

### Connection Failures
- Try/catch in BrowseDirectoryAsync
- User-friendly error messages
- Status text updates

### Lazy Loading Failures
- Silent fail for child loading
- Prevents tree expansion errors
- Maintains user experience

### Duplicate Prevention
- Checks existing folders before adding
- Compares FullPath and RepositoryName
- Prevents redundant entries

## Performance Optimizations

### 1. Lazy Loading
- Only loads root folders initially
- Children loaded when node expanded
- Reduces initial connection time

### 2. Async Operations
- All FTP operations are async
- UI remains responsive
- Loading indicators provide feedback

### 3. Caching
- Tree structure cached in memory
- Avoids re-downloading same directories
- Placeholder pattern for unloaded children

## Styling Consistency

All windows now share:
- Dark theme (#1E1E1E background)
- Blue accent color (#0E639C)
- Consistent button styling
- Matching TextBox/ComboBox styles
- Similar spacing and margins

## Testing Checklist

- [ ] Browse repository with valid credentials
- [ ] Browse repository with invalid credentials
- [ ] Lazy load child folders
- [ ] Check/uncheck folders
- [ ] Add folders to event
- [ ] Remove folders from event
- [ ] Prevent duplicate additions
- [ ] Save event with browsed folders
- [ ] Edit existing event with browser
- [ ] Browse repository with deep folder structure
- [ ] Browse repository with no folders
- [ ] Handle connection timeout
- [ ] Handle permission denied folders

## Future Enhancements

### Possible Improvements:
1. **Search/Filter**: Filter tree by folder name
2. **Expand All**: Button to expand entire tree
3. **Preview**: Show folder size and file count
4. **Icons**: Different icons for folder types
5. **Drag & Drop**: Drag folders from tree to list
6. **Recent Folders**: Quick access to commonly used paths
7. **Validation**: Check if folders contain mods (@prefix)
8. **Caching**: Remember folder structure between sessions

## Code Quality

### Improvements Made:
- Clear separation of concerns
- Async/await best practices
- MVVM pattern for tree nodes
- INotifyPropertyChanged implementation
- Proper error handling
- User feedback at every step

### Areas for Future Work:
- Add unit tests for FTP browsing
- Extract tree node logic to separate class
- Add logging for FTP operations
- Implement retry logic for failed connections

---

**Summary**: The event editor now provides a modern, user-friendly way to browse and select mod folders from FTP repositories, eliminating manual path entry and reducing errors.
