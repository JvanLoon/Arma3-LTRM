# AddEditEvent Window UX Improvements

## Changes Made

### 1. Hide Checkboxes for Non-Selectable Items

**Problem:** Checkboxes were shown for all items in the repository tree, but only folders starting with `@` (mod folders) can be selected. This was confusing because non-mod folders showed a disabled checkbox.

**Solution:**
- Added `BooleanToVisibilityConverter` to the window resources
- Bound checkbox `Visibility` property to `IsSelectable`
- Now only folders starting with `@` show a checkbox
- Other folders (navigation folders) don't show a checkbox at all

**XAML Change:**
```xaml
<CheckBox IsChecked="{Binding IsChecked}" 
          IsEnabled="{Binding IsSelectable}"
          Visibility="{Binding IsSelectable, Converter={StaticResource BooleanToVisibilityConverter}}"
          Margin="0,0,5,0"/>
```

### 2. Prevent Expanding @ Folders (Mod Folders)

**Problem:** Users could click the expand arrow on `@` folders (mod folders) and try to navigate into them. This doesn't make sense because:
- Mod folders are the items you select
- You don't need to see the contents of mod folders (they contain .pbo files and other mod data)
- It created unnecessary FTP requests

**Solution:**
- Modified `TreeViewItem_Expanded` to check if the folder name starts with `@`
- If it does, clear children and return immediately (prevents loading)
- Modified all places where tree nodes are created to NOT add "Loading..." placeholder for `@` folders
- This removes the expand arrow completely from mod folders

**Code Changes:**
```csharp
// In TreeViewItem_Expanded
if (node.Name.StartsWith("@"))
{
    node.Children.Clear();
    return;
}

// When creating nodes
if (!item.Name.StartsWith("@"))
{
    node.Children.Add(new FtpTreeNode { Name = "Loading..." });
}
```

## User Experience

### Before:
```
?? mods (can expand)
  ?? @ACE (can expand - but shouldn't)
    ?? addons (confusing - why would you select this?)
  ?? @CBA (can expand - but shouldn't)
  ? config (disabled checkbox - confusing)
```

### After:
```
?? mods (can expand)
  ? @ACE (no expand arrow - just select it)
  ? @CBA (no expand arrow - just select it)
  config (no checkbox, can expand to find more @ folders)
```

## Benefits

1. **Clearer UI** - Only selectable items show checkboxes
2. **Less Confusion** - Users can't try to expand mod folders
3. **Better Performance** - No unnecessary FTP requests to browse inside mod folders
4. **Intuitive Navigation** - Expand folders to find mods, check mods to add them
5. **Visual Clarity** - The tree structure now clearly shows:
   - Folders without checkboxes = navigation (browse to find mods)
   - Folders with checkboxes = mods (select to add to event)

## Technical Details

### Files Modified:
- `Views\AddEditEventWindow.xaml` - Added converter, modified TreeView template
- `Views\AddEditEventWindow.xaml.cs` - Updated three methods:
  - `PreloadAllRepositories()` - Don't add placeholder for @ folders
  - `LoadCachedRepository()` - Don't add placeholder for @ folders  
  - `TreeViewItem_Expanded()` - Block expansion of @ folders, don't add placeholder for children that are @ folders

### Build Status:
? Build successful - No compilation errors

## Testing Recommendations

1. Open "Manage Events" ? Add Event
2. Select a repository with nested folders
3. **Verify:**
   - ? Only @ folders show checkboxes
   - ? Non-@ folders don't show checkboxes
   - ? @ folders have no expand arrow
   - ? Non-@ folders can still be expanded
   - ? Can navigate through folder structure to find @ folders
   - ? Can check multiple @ folders and add them to event
