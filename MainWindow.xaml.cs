using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Arma_3_LTRM
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly string _ARMA3EXENAME = "arma3.exe";
        private string _armaExeLocation = string.Empty;
        //list of directorie paths to be used as mods
        public List<string> Modlist = new List<string>();

        public MainWindow()
        {
            InitializeComponent();
            GetInitialArmaLocation();

            PopulateAvailableAddons();
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void GetInitialArmaLocation()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Arma 3 Executable",
                Filter = "Arma 3 Executable|arma3.exe|All Executables|*.exe",
                FileName = _ARMA3EXENAME
            };

            if (dialog.ShowDialog() == true)
            {
                string selectedFile = dialog.FileName;

                // Validate that the selected file is arma3.exe
                if (System.IO.Path.GetFileName(selectedFile).Equals("arma3.exe", StringComparison.OrdinalIgnoreCase))
                {
                    _armaExeLocation = selectedFile;

                    if (selectedFile != null && !Modlist.Contains(selectedFile))
                    {
                        Modlist.Add(selectedFile);
                        Add_ModListTreeView(selectedFile);
                    }
                }
                else
                {
                    System.Windows.MessageBox.Show("Please select the arma3.exe file.", "Invalid File", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void Add_Folder_To_Modlist_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Mod Folder",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                string selectedFolder = dialog.FolderName;

                if (!Modlist.Contains(selectedFolder))
                {
                    Modlist.Add(selectedFolder);
                    Add_ModListTreeView(selectedFolder);
                    PopulateAvailableAddons();
                }
            }
        }

        private void RefreshModListTreeView()
        {
            ModListTreeView.Items.Clear();

            foreach (string modPath in Modlist)
            {
                var treeViewItem = new TreeViewItem
                {
                    Header = System.IO.Path.GetFileName(modPath),
                    Tag = modPath // Store full path in Tag for later use
                };
                ModListTreeView.Items.Add(treeViewItem);
            }
        }

        private void Add_ModListTreeView(string path)
        {
            var treeViewItem = new TreeViewItem
            {
                Header = path,
                Tag = path // Store full path in Tag for later use
            };
            ModListTreeView.Items.Add(treeViewItem);

        }

        private void PopulateAvailableAddons()
        {
            AvailableAddonsTreeView.Items.Clear();

            foreach (string modPath in Modlist)
            {
                if (System.IO.Directory.Exists(modPath))
                {
                    var rootItem = new TreeViewItem
                    {
                        Header = System.IO.Path.GetFileName(modPath) ?? modPath,
                        Tag = modPath
                    };

                    ScanForAddons(modPath, rootItem);

                    if (rootItem.Items.Count > 0 || System.IO.Path.GetFileName(modPath).StartsWith('@'))
                    {
                        AvailableAddonsTreeView.Items.Add(rootItem);
                    }
                }
            }
        }

        private void ScanForAddons(string directoryPath, TreeViewItem parentItem)
        {
            try
            {
                var directories = System.IO.Directory.GetDirectories(directoryPath);

                foreach (string dir in directories)
                {
                    string folderName = System.IO.Path.GetFileName(dir);

                    var childItem = new TreeViewItem
                    {
                        Header = folderName,
                        Tag = dir
                    };

                    parentItem.Items.Add(childItem);

                    // If folder starts with '@', stop searching deeper
                    if (!folderName.StartsWith('@'))
                    {
                        ScanForAddons(dir, childItem);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we don't have permission to access
            }
            catch (System.IO.DirectoryNotFoundException)
            {
                // Skip directories that no longer exist
            }
        }
    }
}