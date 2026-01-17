using System.IO;
using System.Windows.Controls;
using TreeView = System.Windows.Controls.TreeView;
using TreeViewItem = System.Windows.Controls.TreeViewItem;
using CheckBox = System.Windows.Controls.CheckBox;

namespace Arma_3_LTRM
{
    public class ModManager
    {
        public List<string> Modlist { get; } = new List<string>();
        public List<string> StartupModsList { get; } = new List<string>();

        public string Arma3ExeLocation { get; set; } = string.Empty;

        public void AddMod(string modPath)
        {
            if (!Modlist.Contains(modPath))
            {
                Modlist.Add(modPath);
            }
        }

        public void RemoveMod(string modPath)
        {
            if (Modlist.Contains(modPath))
            {
                Modlist.Remove(modPath);
            }
        }

        public void AddStartupMod(string modPath)
        {
            if (!StartupModsList.Contains(modPath))
            {
                StartupModsList.Add(modPath);
            }
        }

        public void PopulateAvailableAddonsTreeView(TreeView treeView)
        {
            treeView.Items.Clear();

            foreach (string modPath in Modlist)
            {
                if (Directory.Exists(modPath))
                {
                    var rootItem = new TreeViewItem
                    {
                        Header = modPath,
                        Tag = modPath
                    };

                    ScanForAddons(modPath, rootItem);

                    if (rootItem.Items.Count > 0 || Path.GetFileName(modPath).StartsWith('@'))
                    {
                        if (rootItem.Tag.ToString() != Arma3ExeLocation)
                        {
                            treeView.Items.Add(rootItem);
                        }
                    }
                }
            }
        }

        public void RefreshModListTreeView(TreeView treeView)
        {
            treeView.Items.Clear();

            foreach (string modPath in Modlist)
            {
                var treeViewItem = new TreeViewItem
                {
                    Header = Path.GetFileName(modPath) ?? modPath,
                    Tag = modPath
                };
                treeView.Items.Add(treeViewItem);
            }
        }

        public void RefreshStartupModsTreeView(TreeView treeView)
        {
            treeView.Items.Clear();

            foreach (string modPath in StartupModsList)
            {
                var checkBox = new CheckBox
                {
                    Content = modPath,
                    Tag = modPath,
                    IsChecked = true
                };

                var treeViewItem = new TreeViewItem
                {
                    Header = checkBox,
                    Tag = modPath
                };

                treeView.Items.Add(treeViewItem);
            }
        }

        public List<string> GetCheckedStartupMods(TreeView treeView)
        {
            var checkedMods = new List<string>();

            foreach (TreeViewItem item in treeView.Items)
            {
                if (item.Header is CheckBox checkBox && checkBox.IsChecked == true)
                {
                    checkedMods.Add(checkBox.Tag as string ?? string.Empty);
                }
            }

            return checkedMods;
        }

        public string FindModStartDirectory(TreeViewItem item)
        {
            string path = item.Tag as string;
            if (path == null) return null;

            if (Path.GetFileName(path).StartsWith('@'))
            {
                return path;
            }

            while (item.Parent is TreeViewItem parentItem)
            {
                string parentPath = parentItem.Tag as string;
                if (parentPath != null && Path.GetFileName(parentPath).StartsWith('@'))
                {
                    return parentPath;
                }
                item = parentItem;
            }

            return path;
        }

        private void ScanForAddons(string directoryPath, TreeViewItem parentItem)
        {
            try
            {
                var directories = Directory.GetDirectories(directoryPath);

                foreach (string dir in directories)
                {
                    string folderName = Path.GetFileName(dir);

                    var childItem = new TreeViewItem
                    {
                        Header = folderName,
                        Tag = dir
                    };

                    parentItem.Items.Add(childItem);

                    if (!folderName.StartsWith('@'))
                    {
                        ScanForAddons(dir, childItem);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }
        }
    }
}
