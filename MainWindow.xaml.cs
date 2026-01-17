using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Diagnostics;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using DataFormats = System.Windows.DataFormats;
using TreeView = System.Windows.Controls.TreeView;
using TreeViewItem = System.Windows.Controls.TreeViewItem;
using MessageBox = System.Windows.MessageBox;
using Point = System.Windows.Point;

namespace Arma_3_LTRM
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string ARMA3_EXE_NAME = "arma3.exe";
        private const string ARMA3_LTSYNC_FOLDER_NAME = "ARMA3_LTSYNC";

        private readonly ModManager _modManager;
        private readonly LaunchParametersManager _launchParametersManager;
        private string _armaExeLocation = string.Empty;
        private Point _dragStartPoint;
        private bool _isDragging;

        public MainWindow()
        {
            InitializeComponent();
            _modManager = new ModManager();
            _launchParametersManager = new LaunchParametersManager();
            _launchParametersManager.ParametersChanged += OnParametersChanged;
            SelectArmaExecutable();
            UpdateRunParametersDisplay();
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void LaunchOption_Changed(object sender, RoutedEventArgs e)
        {
            _launchParametersManager.SetParameter("windowed", WindowedCheckBox.IsChecked == true);
            _launchParametersManager.SetParameter("world", EmptyWorldCheckBox.IsChecked == true);
            _launchParametersManager.SetParameter("skipIntro", SkipIntroCheckBox.IsChecked == true);
            _launchParametersManager.SetParameter("nosplash", NoSplashCheckBox.IsChecked == true);
            _launchParametersManager.SetParameter("noPause", NoPauseCheckBox.IsChecked == true);
            _launchParametersManager.SetParameter("noPauseAudio", NoPauseAudioCheckBox.IsChecked == true);
            _launchParametersManager.SetParameter("noLogs", NoLogsCheckBox.IsChecked == true);
            _launchParametersManager.SetParameter("showScriptErrors", ShowScriptErrorsCheckBox.IsChecked == true);
            _launchParametersManager.SetParameter("filePatching", FilePatchingCheckBox.IsChecked == true);
            UpdateModParameters();
        }

        private void AdditionalParametersTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateRunParametersDisplay();
        }

        private void OnParametersChanged(object? sender, EventArgs e)
        {
            UpdateRunParametersDisplay();
        }

        private void UpdateRunParametersDisplay()
        {
            var parameters = _launchParametersManager.GetParametersList();
            
            if (!string.IsNullOrWhiteSpace(AdditionalParametersTextBox?.Text))
            {
                var additionalParams = AdditionalParametersTextBox.Text
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrWhiteSpace(p));
                parameters.AddRange(additionalParams);
            }

            RunParametersTextBlock.Text = string.Join(Environment.NewLine, parameters);
        }

        private void UpdateModParameters()
        {
            var checkedMods = _modManager.GetCheckedStartupMods(StarupModsListTreeView);
            _launchParametersManager.UpdateModsList(checkedMods);
        }

        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_armaExeLocation) || !File.Exists(_armaExeLocation))
            {
                MessageBox.Show("Arma 3 executable not found. Please restart the application and select a valid arma3.exe file.", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                var parameterString = _launchParametersManager.GetParametersString();
                
                if (!string.IsNullOrWhiteSpace(AdditionalParametersTextBox?.Text))
                {
                    var additionalParams = AdditionalParametersTextBox.Text
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .Where(p => !string.IsNullOrWhiteSpace(p));
                    parameterString += " " + string.Join(" ", additionalParams);
                }

                if (AdditionalParametersTextBox!= null && !string.IsNullOrWhiteSpace(AdditionalParametersTextBox.Text))
                {
                    var additionalParams = AdditionalParametersTextBox.Text
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .Where(p => !string.IsNullOrWhiteSpace(p));
                    parameterString += " " + string.Join(" ", additionalParams);
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = _armaExeLocation,
                    Arguments = parameterString,
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(_armaExeLocation)
                };

                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch Arma 3: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectArmaExecutable()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Arma 3 Executable",
                Filter = "Arma 3 Executable|arma3.exe|All Executables|*.exe",
                FileName = ARMA3_EXE_NAME
            };

            if (dialog.ShowDialog() == true)
            {
                if (Path.GetFileName(dialog.FileName).Equals(ARMA3_EXE_NAME, StringComparison.OrdinalIgnoreCase))
                {
                    _armaExeLocation = dialog.FileName;
                    _modManager.Arma3ExeLocation = Path.GetDirectoryName(dialog.FileName) ?? dialog.FileName;
                    
                    if (_modManager.Arma3ExeLocation != null)
                    {
                        _modManager.AddMod(_modManager.Arma3ExeLocation);
                        _modManager.RefreshModListTreeView(ModListTreeView);
                    }
                }
                else
                {
                    MessageBox.Show("Please select the arma3.exe file.", "Invalid File", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                _modManager.AddMod(dialog.FolderName);
                _modManager.RefreshModListTreeView(ModListTreeView);
                _modManager.PopulateAvailableAddonsTreeView(AvailableAddonsTreeView);
            }
        }

        private void Remove_Folder_To_Modlist_Click(object sender, RoutedEventArgs e)
        {

            if (ModListTreeView.SelectedItem is TreeViewItem selectedItem)
            {
                string modPath = selectedItem.Tag as string;
                if (!string.IsNullOrEmpty(modPath))
                {
                    _modManager.RemoveMod(modPath);
                    _modManager.RefreshModListTreeView(ModListTreeView);
                    _modManager.PopulateAvailableAddonsTreeView(AvailableAddonsTreeView);
                }
            }
            else
            {
                MessageBox.Show("Please select a mod folder to remove.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void AvailableAddonsTreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _isDragging = false;
        }

        private void AvailableAddonsTreeView_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
            {
                Point currentPosition = e.GetPosition(null);
                Vector diff = _dragStartPoint - currentPosition;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isDragging = true;
                    
                    if (sender is TreeView treeView && treeView.SelectedItem is TreeViewItem selectedItem)
                    {
                        string modPath = _modManager.FindModStartDirectory(selectedItem);
                        if (modPath != null)
                        {
                            DragDrop.DoDragDrop(treeView, modPath, DragDropEffects.Copy);
                            _isDragging = false;
                        }
                    }
                }
            }
        }

        private void StarupModsListTreeView_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.StringFormat) 
                ? DragDropEffects.Copy 
                : DragDropEffects.None;
            e.Handled = true;
        }

        private void StarupModsListTreeView_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.StringFormat))
            {
                string modPath = (string)e.Data.GetData(DataFormats.StringFormat);
                _modManager.AddStartupMod(modPath);
                _modManager.RefreshStartupModsTreeView(StarupModsListTreeView);
                WireUpStartupModCheckboxes();
                UpdateModParameters();
            }
        }

        private void WireUpStartupModCheckboxes()
        {
            foreach (TreeViewItem item in StarupModsListTreeView.Items)
            {
                if (item.Header is System.Windows.Controls.CheckBox checkBox)
                {
                    checkBox.Checked -= StartupModCheckBox_Changed;
                    checkBox.Unchecked -= StartupModCheckBox_Changed;
                    checkBox.Checked += StartupModCheckBox_Changed;
                    checkBox.Unchecked += StartupModCheckBox_Changed;
                }
            }
        }

        private void StartupModCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateModParameters();
        }
    }
}