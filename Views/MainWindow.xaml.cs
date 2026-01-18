using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Data;
using Arma_3_LTRM.Models;
using Arma_3_LTRM.Services;
using EventManager = Arma_3_LTRM.Services.EventManager;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using Button = System.Windows.Controls.Button;
using OpenFolderDialog = Microsoft.Win32.OpenFolderDialog;

namespace Arma_3_LTRM.Views
{
    public partial class MainWindow : Window
    {
        private readonly RepositoryManager _repositoryManager;
        private readonly EventManager _eventManager;
        private readonly ServerManager _serverManager;
        private readonly SettingsManager _settingsManager;
        private readonly FtpManager _ftpManager;
        private readonly LaunchParametersManager _launchParametersManager;
        private ObservableCollection<string> _downloadLocations;
        private bool _isUpdatingParameters = false;

        public MainWindow()
        {
            InitializeComponent();

            _repositoryManager = new RepositoryManager();
            _eventManager = new EventManager();
            _eventManager.Initialize(_repositoryManager);
            _serverManager = new ServerManager();
            _settingsManager = new SettingsManager();
            _ftpManager = new FtpManager();
            _launchParametersManager = new LaunchParametersManager(_settingsManager.Settings.LaunchParameters);
            _launchParametersManager.ParametersChanged += (s, e) => 
            {
                UpdateParametersDisplay();
                SaveLaunchParameters();
            };
            _downloadLocations = new ObservableCollection<string>();

            LoadData();
            InitializeParametersUI();
        }

        private void LoadData()
        {
            RepositoriesListBox.ItemsSource = _repositoryManager.Repositories;
            EventsListBox.ItemsSource = _eventManager.Events;
            ServersListBox.ItemsSource = _serverManager.Servers;
            ManageRepositoriesListBox.ItemsSource = _repositoryManager.Repositories;
            ManageEventsListBox.ItemsSource = _eventManager.Events;
            ManageServersListBox.ItemsSource = _serverManager.Servers;
            LoadSettings();
        }

        private void LoadSettings()
        {
            SettingsArma3PathTextBox.Text = _settingsManager.Settings.Arma3ExePath;
            
            _downloadLocations.Clear();
            
            // Ensure BaseDownloadLocations is never null
            if (_settingsManager.Settings.BaseDownloadLocations != null)
            {
                foreach (var location in _settingsManager.Settings.BaseDownloadLocations)
                {
                    _downloadLocations.Add(location);
                }
            }
            
            SettingsDownloadLocationsListBox.ItemsSource = _downloadLocations;
        }

        private void SaveLaunchParameters()
        {
            _settingsManager.Settings.LaunchParameters = _launchParametersManager.LaunchParameters;
            _settingsManager.SaveSettings();
        }

        private void InitializeParametersUI()
        {
            // Load available profiles
            var profiles = _launchParametersManager.GetAvailableProfiles();
            ProfileComboBox.ItemsSource = profiles;
            NameComboBox.ItemsSource = profiles;
            
            // Bind controls to LaunchParameters model
            UseNameCheckBox.DataContext = _launchParametersManager.LaunchParameters;
            NameComboBox.DataContext = _launchParametersManager.LaunchParameters;
            UseProfileCheckBox.DataContext = _launchParametersManager.LaunchParameters;
            ProfileComboBox.DataContext = _launchParametersManager.LaunchParameters;
            UnitTextBox.DataContext = _launchParametersManager.LaunchParameters;
            UseMissionCheckBox.DataContext = _launchParametersManager.LaunchParameters;
            MissionPathTextBox.DataContext = _launchParametersManager.LaunchParameters;
            WindowedCheckBox.DataContext = _launchParametersManager.LaunchParameters;
            NoSplashCheckBox.DataContext = _launchParametersManager.LaunchParameters;
            SkipIntroCheckBox.DataContext = _launchParametersManager.LaunchParameters;
            EmptyWorldCheckBox.DataContext = _launchParametersManager.LaunchParameters;
            EnableHTCheckBox.DataContext = _launchParametersManager.LaunchParameters;
            ShowScriptErrorsCheckBox.DataContext = _launchParametersManager.LaunchParameters;
            NoPauseCheckBox.DataContext = _launchParametersManager.LaunchParameters;
            NoPauseAudioCheckBox.DataContext = _launchParametersManager.LaunchParameters;
            NoLogsCheckBox.DataContext = _launchParametersManager.LaunchParameters;
            NoFreezeCheckCheckBox.DataContext = _launchParametersManager.LaunchParameters;
            NoFilePatchingCheckBox.DataContext = _launchParametersManager.LaunchParameters;
            DebugCheckBox.DataContext = _launchParametersManager.LaunchParameters;
            ConfigTextBox.DataContext = _launchParametersManager.LaunchParameters;
            BePathTextBox.DataContext = _launchParametersManager.LaunchParameters;
            
            // Set up bindings
            var useNameBinding = new System.Windows.Data.Binding("UseName") { Mode = BindingMode.TwoWay };
            UseNameCheckBox.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, useNameBinding);
            
            var nameBinding = new System.Windows.Data.Binding("Name") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged };
            NameComboBox.SetBinding(System.Windows.Controls.ComboBox.SelectedItemProperty, nameBinding);
            
            var useProfileBinding = new System.Windows.Data.Binding("UseProfile") { Mode = BindingMode.TwoWay };
            UseProfileCheckBox.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, useProfileBinding);
            
            var profileBinding = new System.Windows.Data.Binding("ProfilePath") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged };
            ProfileComboBox.SetBinding(System.Windows.Controls.ComboBox.SelectedItemProperty, profileBinding);
            
            var unitBinding = new System.Windows.Data.Binding("Unit") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged };
            UnitTextBox.SetBinding(System.Windows.Controls.TextBox.TextProperty, unitBinding);
            
            var useMissionBinding = new System.Windows.Data.Binding("UseMission") { Mode = BindingMode.TwoWay };
            UseMissionCheckBox.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, useMissionBinding);
            
            var missionPathBinding = new System.Windows.Data.Binding("MissionPath") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged };
            MissionPathTextBox.SetBinding(System.Windows.Controls.TextBox.TextProperty, missionPathBinding);
            
            var windowedBinding = new System.Windows.Data.Binding("Windowed") { Mode = BindingMode.TwoWay };
            WindowedCheckBox.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, windowedBinding);
            
            var noSplashBinding = new System.Windows.Data.Binding("NoSplash") { Mode = BindingMode.TwoWay };
            NoSplashCheckBox.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, noSplashBinding);
            
            var skipIntroBinding = new System.Windows.Data.Binding("SkipIntro") { Mode = BindingMode.TwoWay };
            SkipIntroCheckBox.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, skipIntroBinding);
            
            var emptyWorldBinding = new System.Windows.Data.Binding("EmptyWorld") { Mode = BindingMode.TwoWay };
            EmptyWorldCheckBox.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, emptyWorldBinding);
            
            var enableHTBinding = new System.Windows.Data.Binding("EnableHT") { Mode = BindingMode.TwoWay };
            EnableHTCheckBox.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, enableHTBinding);
            
            var showScriptErrorsBinding = new System.Windows.Data.Binding("ShowScriptErrors") { Mode = BindingMode.TwoWay };
            ShowScriptErrorsCheckBox.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, showScriptErrorsBinding);
            
            var noPauseBinding = new System.Windows.Data.Binding("NoPause") { Mode = BindingMode.TwoWay };
            NoPauseCheckBox.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, noPauseBinding);
            
            var noPauseAudioBinding = new System.Windows.Data.Binding("NoPauseAudio") { Mode = BindingMode.TwoWay };
            NoPauseAudioCheckBox.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, noPauseAudioBinding);
            
            var noLogsBinding = new System.Windows.Data.Binding("NoLogs") { Mode = BindingMode.TwoWay };
            NoLogsCheckBox.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, noLogsBinding);
            
            var noFreezeCheckBinding = new System.Windows.Data.Binding("NoFreezeCheck") { Mode = BindingMode.TwoWay };
            NoFreezeCheckCheckBox.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, noFreezeCheckBinding);
            
            var noFilePatchingBinding = new System.Windows.Data.Binding("NoFilePatching") { Mode = BindingMode.TwoWay };
            NoFilePatchingCheckBox.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, noFilePatchingBinding);
            
            var debugBinding = new System.Windows.Data.Binding("Debug") { Mode = BindingMode.TwoWay };
            DebugCheckBox.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, debugBinding);
            
            var configBinding = new System.Windows.Data.Binding("Config") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged };
            ConfigTextBox.SetBinding(System.Windows.Controls.TextBox.TextProperty, configBinding);
            
            var bePathBinding = new System.Windows.Data.Binding("BePath") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged };
            BePathTextBox.SetBinding(System.Windows.Controls.TextBox.TextProperty, bePathBinding);
            
            UpdateParametersDisplay();
        }

        private void CustomParametersTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateParametersDisplay();
        }

        private void RepositoryCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkBox && checkBox.IsChecked == true)
            {
                // Uncheck all events when a repository is checked
                foreach (var evt in _eventManager.Events)
                {
                    evt.IsChecked = false;
                }
            }
            
            UpdateParametersDisplay();
        }

        private void EventCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkBox && checkBox.IsChecked == true)
            {
                // Uncheck all repositories when an event is checked
                foreach (var repo in _repositoryManager.Repositories)
                {
                    repo.IsChecked = false;
                }
            }
            
            UpdateParametersDisplay();
        }

        private void ServerCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkBox && checkBox.IsChecked == true)
            {
                // Uncheck all other servers when one is checked
                foreach (var server in _serverManager.Servers)
                {
                    if (server != checkBox.DataContext)
                    {
                        server.IsChecked = false;
                    }
                }
            }
            
            UpdateParametersDisplay();
        }

        private void UpdateParametersDisplay()
        {
            if (_isUpdatingParameters) return;
            
            try
            {
                _isUpdatingParameters = true;
                
                // Get mod folders from checked repositories and events
                var modFolders = new List<string>();

                var checkedRepos = _repositoryManager.Repositories.Where(r => r.IsChecked).ToList();
                var checkedEvents = _eventManager.Events.Where(e => e.IsChecked).ToList();

                // If repositories are checked, find their mod folders in download locations
                foreach (var repo in checkedRepos)
                {
                    var repoPath = FindRepositoryInLocations(repo.Name);
                    if (repoPath != null && Directory.Exists(repoPath))
                    {
                        var directories = Directory.GetDirectories(repoPath, "*", SearchOption.AllDirectories);
                        foreach (var dir in directories)
                        {
                            if (Path.GetFileName(dir).StartsWith("@"))
                            {
                                modFolders.Add(dir);
                            }
                        }
                    }
                }

                // If events are checked, get their mod folder paths
                foreach (var evt in checkedEvents)
                {
                    var eventPath = FindEventInLocations(evt);
                    if (eventPath != null)
                    {
                        var arma3Dir = Path.GetDirectoryName(_settingsManager.Settings.Arma3ExePath);
                        
                        foreach (var modFolder in evt.ModFolders)
                        {
                            if (modFolder.ItemType == ModItemType.DLC && !string.IsNullOrEmpty(arma3Dir))
                            {
                                var dlcPath = Path.Combine(arma3Dir, modFolder.FolderPath);
                                if (Directory.Exists(dlcPath))
                                {
                                    modFolders.Add(modFolder.FolderPath);
                                }
                            }
                            else if (modFolder.ItemType == ModItemType.Workshop && !string.IsNullOrEmpty(arma3Dir))
                            {
                                var workshopPath = Path.Combine(arma3Dir, "!Workshop", modFolder.FolderPath);
                                if (Directory.Exists(workshopPath))
                                {
                                    modFolders.Add(workshopPath);
                                }
                            }
                            else
                            {
                                var relativePath = modFolder.FolderPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                                var localPath = Path.Combine(eventPath, relativePath);
                                if (Directory.Exists(localPath))
                                {
                                    modFolders.Add(localPath);
                                }
                            }
                        }
                    }
                }

                // Update mod paths in LaunchParametersManager
                _launchParametersManager.UpdateModsList(modFolders);

                // Get custom parameters and generate final command
                var customParams = CustomParametersTextBox.Text;
                var finalParams = _launchParametersManager.GetParametersString(customParams);
                
                // Add server connection parameters if a server is selected
                var checkedServer = _serverManager.Servers.FirstOrDefault(s => s.IsChecked);
                if (checkedServer != null)
                {
                    var serverParams = $"-connect={checkedServer.Address} -port={checkedServer.Port}";
                    if (!string.IsNullOrWhiteSpace(checkedServer.Password))
                    {
                        serverParams += $" -password={checkedServer.Password}";
                    }
                    
                    if (string.IsNullOrWhiteSpace(finalParams))
                    {
                        finalParams = serverParams;
                    }
                    else
                    {
                        finalParams += " " + serverParams;
                    }
                }
                
                // Format parameters with each one on a new line
                if (string.IsNullOrWhiteSpace(finalParams))
                {
                    FinalParametersTextBox.Text = "(No parameters selected)";
                    HomeFinalParametersTextBox.Text = "(No parameters selected)";
                }
                else
                {
                    // Replace space before each parameter with newline for better readability
                    var formattedParams = finalParams.Replace(" -", Environment.NewLine + "-");
                    FinalParametersTextBox.Text = formattedParams;
                    HomeFinalParametersTextBox.Text = formattedParams;
                }
            }
            finally
            {
                _isUpdatingParameters = false;
            }
        }

        private void SettingsBrowseArma3_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Arma 3 Executable|arma3_x64.exe;arma3.exe|All Files|*.*",
                Title = "Select Arma 3 Executable"
            };

            if (dialog.ShowDialog() == true)
            {
                SettingsArma3PathTextBox.Text = dialog.FileName;
                _settingsManager.Settings.Arma3ExePath = dialog.FileName;
                _settingsManager.SaveSettings();
            }
        }

        private void SettingsAddLocationButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Download Location"
            };

            if (dialog.ShowDialog() == true)
            {
                if (!_downloadLocations.Contains(dialog.FolderName))
                {
                    _downloadLocations.Add(dialog.FolderName);
                    _settingsManager.Settings.BaseDownloadLocations = _downloadLocations.ToList();
                    _settingsManager.SaveSettings();
                }
                else
                {
                    MessageBox.Show("This location is already in the list.", "Duplicate Location", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void SettingsRemoveLocationButton_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsDownloadLocationsListBox.SelectedItem is string selectedLocation)
            {
                if (_downloadLocations.Count <= 1)
                {
                    MessageBox.Show("You must have at least one download location.", "Cannot Remove", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                _downloadLocations.Remove(selectedLocation);
                _settingsManager.Settings.BaseDownloadLocations = _downloadLocations.ToList();
                _settingsManager.SaveSettings();
            }
        }

        private void SettingsMoveUpButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedIndex = SettingsDownloadLocationsListBox.SelectedIndex;
            if (selectedIndex > 0)
            {
                var item = _downloadLocations[selectedIndex];
                _downloadLocations.RemoveAt(selectedIndex);
                _downloadLocations.Insert(selectedIndex - 1, item);
                SettingsDownloadLocationsListBox.SelectedIndex = selectedIndex - 1;
                _settingsManager.Settings.BaseDownloadLocations = _downloadLocations.ToList();
                _settingsManager.SaveSettings();
            }
        }

        private void SettingsMoveDownButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedIndex = SettingsDownloadLocationsListBox.SelectedIndex;
            if (selectedIndex >= 0 && selectedIndex < _downloadLocations.Count - 1)
            {
                var item = _downloadLocations[selectedIndex];
                _downloadLocations.RemoveAt(selectedIndex);
                _downloadLocations.Insert(selectedIndex + 1, item);
                SettingsDownloadLocationsListBox.SelectedIndex = selectedIndex + 1;
                _settingsManager.Settings.BaseDownloadLocations = _downloadLocations.ToList();
                _settingsManager.SaveSettings();
            }
        }

        private void BrowseMission_Click(object sender, RoutedEventArgs e)
        {
            var userName = Environment.UserName;
            var defaultPath = Path.Combine("C:\\Users", userName, "Documents", "Arma 3 - Other Profiles");
            
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Arma 3 Mission Files|*.sqm|All Files|*.*",
                Title = "Select Mission File",
                InitialDirectory = Directory.Exists(defaultPath) ? defaultPath : null
            };

            if (dialog.ShowDialog() == true)
            {
                MissionPathTextBox.Text = dialog.FileName;
            }
        }

        private void BrowseConfig_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Config Files|*.cfg|All Files|*.*",
                Title = "Select Server Config File"
            };

            if (dialog.ShowDialog() == true)
            {
                ConfigTextBox.Text = dialog.FileName;
            }
        }

        private void BrowseBePath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select BattlEye Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                BePathTextBox.Text = dialog.FolderName;
            }
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Arma 3 LTRM - Lowlands Tactical Repo Manager\n\n" +
                          "A modern FTP-based mod repository manager for Arma 3.\n\n" +
                          "Version 2.0\n" +
                          "© 2024 Lowlands Tactical",
                          "About", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void DownloadLaunchRepository_Click(object sender, RoutedEventArgs e)
        {
            var selectedRepos = _repositoryManager.Repositories.Where(r => r.IsChecked).ToList();
            if (selectedRepos.Count == 0)
            {
                MessageBox.Show("Please check at least one repository.", "No Selection", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ValidateArma3Path())
                return;

            var selectedLocation = SelectDownloadLocation();
            if (selectedLocation == null)
                return;

            foreach (var repo in selectedRepos)
            {
                var progressWindow = new DownloadProgressWindow();
                progressWindow.Owner = this;

                var progress = new Progress<string>(message => progressWindow.AppendLog(message));

                progressWindow.Show();
                var success = await _ftpManager.DownloadRepositoryAsync(repo, selectedLocation, progress);
                progressWindow.Close();

                if (!success)
                {
                    MessageBox.Show($"Failed to download repository '{repo.Name}'.", "Download Failed", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            LaunchArma3();
        }

        private async void DownloadRepository_Click(object sender, RoutedEventArgs e)
        {
            var selectedRepos = _repositoryManager.Repositories.Where(r => r.IsChecked).ToList();
            if (selectedRepos.Count == 0)
            {
                MessageBox.Show("Please check at least one repository.", "No Selection", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedLocation = SelectDownloadLocation();
            if (selectedLocation == null)
                return;

            foreach (var repo in selectedRepos)
            {
                var progressWindow = new DownloadProgressWindow();
                progressWindow.Owner = this;

                var progress = new Progress<string>(message => progressWindow.AppendLog(message));

                progressWindow.Show();
                await _ftpManager.DownloadRepositoryAsync(repo, selectedLocation, progress, progressWindow.CancellationToken);
                progressWindow.MarkCompleted();
            }

            MessageBox.Show("Repository download completed!", "Download Complete", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LaunchRepository_Click(object sender, RoutedEventArgs e)
        {
            var selectedRepos = _repositoryManager.Repositories.Where(r => r.IsChecked).ToList();
            if (selectedRepos.Count == 0)
            {
                MessageBox.Show("Please check at least one repository.", "No Selection", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ValidateArma3Path())
                return;

            // Verify all repositories exist in download locations
            foreach (var repo in selectedRepos)
            {
                var foundPath = FindRepositoryInLocations(repo.Name);
                if (foundPath == null)
                {
                    MessageBox.Show($"Repository '{repo.Name}' has not been downloaded to any configured location.", 
                        "Repository Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            LaunchArma3();
        }

        private async void DownloadLaunchEvent_Click(object sender, RoutedEventArgs e)
        {
            var selectedEvents = _eventManager.Events.Where(e => e.IsChecked).ToList();
            if (selectedEvents.Count == 0)
            {
                MessageBox.Show("Please check at least one event.", "No Selection", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ValidateArma3Path())
                return;

            var selectedLocation = SelectDownloadLocation();
            if (selectedLocation == null)
                return;

            foreach (var evt in selectedEvents)
            {
                var progressWindow = new DownloadProgressWindow();
                progressWindow.Owner = this;
                var progress = new Progress<string>(message => progressWindow.AppendLog(message));
                progressWindow.Show();

                foreach (var modFolder in evt.ModFolders)
                {
                    // Skip DLC and Workshop items - they don't need to be downloaded
                    if (modFolder.ItemType == ModItemType.DLC)
                    {
                        ((IProgress<string>)progress).Report($"Skipping DLC: {modFolder.FolderPath}");
                        continue;
                    }
                    if (modFolder.ItemType == ModItemType.Workshop)
                    {
                        ((IProgress<string>)progress).Report($"Skipping Workshop Item: {modFolder.FolderPath}");
                        continue;
                    }

                    var repository = evt.Repositories.FirstOrDefault(r => r.Id == modFolder.RepositoryId);
                    if (repository == null)
                    {
                        progressWindow.Close();
                        MessageBox.Show($"Repository not found for folder '{modFolder.FolderPath}'.\n\nPlease check your repository configuration.", 
                            "Repository Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Maintain full folder structure from FTP path
                    // e.g., /mods/xyz/@ACE becomes eventBasePath/mods/xyz/@ACE
                    var relativePath = modFolder.FolderPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                    var localPath = Path.Combine(selectedLocation, relativePath);
                    
                    await _ftpManager.DownloadFolderAsync(
                        repository.Url,
                        repository.Port,
                        repository.Username,
                        repository.Password,
                        modFolder.FolderPath,
                        localPath,
                        progress
                    );
                }

                progressWindow.Close();
            }

            LaunchArma3();
        }

        private async void DownloadEvent_Click(object sender, RoutedEventArgs e)
        {
            var selectedEvents = _eventManager.Events.Where(e => e.IsChecked).ToList();
            if (selectedEvents.Count == 0)
            {
                MessageBox.Show("Please check at least one event.", "No Selection", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedLocation = SelectDownloadLocation();
            if (selectedLocation == null)
                return;

            foreach (var evt in selectedEvents)
            {
                var progressWindow = new DownloadProgressWindow();
                progressWindow.Owner = this;
                var progress = new Progress<string>(message => progressWindow.AppendLog(message));
                progressWindow.Show();

                try
                {
                    foreach (var modFolder in evt.ModFolders)
                    {
                        // Skip DLC and Workshop items - they don't need to be downloaded
                        if (modFolder.ItemType == ModItemType.DLC)
                        {
                            ((IProgress<string>)progress).Report($"Skipping DLC: {modFolder.FolderPath}");
                            continue;
                        }
                        if (modFolder.ItemType == ModItemType.Workshop)
                        {
                            ((IProgress<string>)progress).Report($"Skipping Workshop Item: {modFolder.FolderPath}");
                            continue;
                        }

                        var repository = evt.Repositories.FirstOrDefault(r => r.Id == modFolder.RepositoryId);
                        if (repository == null)
                        {
                            progressWindow.Close();
                            MessageBox.Show($"Repository not found for folder '{modFolder.FolderPath}'.\n\nPlease check your repository configuration.", 
                                "Repository Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        // Maintain full folder structure from FTP path
                        // e.g., /mods/xyz/@ACE becomes eventBasePath/mods/xyz/@ACE
                        var relativePath = modFolder.FolderPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                        var localPath = Path.Combine(selectedLocation, relativePath);
                        
                        await _ftpManager.DownloadFolderAsync(
                            repository.Url,
                            repository.Port,
                            repository.Username,
                            repository.Password,
                            modFolder.FolderPath,
                            localPath,
                            progress,
                            progressWindow.CancellationToken
                        );
                    }

                    progressWindow.MarkCompleted();
                }
                catch (OperationCanceledException)
                {
                    progressWindow.Close();
                    ((IProgress<string>)progress).Report("Download cancelled by user.");
                    return;
                }
            }

            MessageBox.Show("Event download completed!", "Download Complete", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LaunchEvent_Click(object sender, RoutedEventArgs e)
        {
            var checkedEvents = _eventManager.Events.Where(e => e.IsChecked).ToList();
            if (checkedEvents.Count == 0)
            {
                MessageBox.Show("Please check at least one event.", "No Selection", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ValidateArma3Path())
                return;

            // Verify all events exist in download locations
            foreach (var evt in checkedEvents)
            {
                var foundPath = FindEventInLocations(evt);
                if (foundPath == null)
                {
                    MessageBox.Show($"Event '{evt.Name}' has not been downloaded to any configured location.", 
                        "Event Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            LaunchArma3();
        }

        private async void DownloadRepositoryItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Repository repo)
            {
                var selectedLocation = SelectDownloadLocation();
                if (selectedLocation == null)
                    return;

                var progressWindow = new DownloadProgressWindow();
                progressWindow.Owner = this;

                var progress = new Progress<string>(message => progressWindow.AppendLog(message));

                progressWindow.Show();
                await _ftpManager.DownloadRepositoryAsync(repo, selectedLocation, progress, progressWindow.CancellationToken);
                progressWindow.MarkCompleted();

                MessageBox.Show($"Repository '{repo.Name}' download completed!", "Download Complete", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void DownloadEventItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Event evt)
            {
                var selectedLocation = SelectDownloadLocation();
                if (selectedLocation == null)
                    return;

                var progressWindow = new DownloadProgressWindow();
                progressWindow.Owner = this;
                var progress = new Progress<string>(message => progressWindow.AppendLog(message));
                progressWindow.Show();

                try
                {
                    foreach (var modFolder in evt.ModFolders)
                    {
                        // Skip DLC and Workshop items - they don't need to be downloaded
                        if (modFolder.ItemType == ModItemType.DLC)
                        {
                            ((IProgress<string>)progress).Report($"Skipping DLC: {modFolder.FolderPath}");
                            continue;
                        }
                        if (modFolder.ItemType == ModItemType.Workshop)
                        {
                            ((IProgress<string>)progress).Report($"Skipping Workshop Item: {modFolder.FolderPath}");
                            continue;
                        }

                        var repository = evt.Repositories.FirstOrDefault(r => r.Id == modFolder.RepositoryId);
                        if (repository == null)
                        {
                            progressWindow.Close();
                            MessageBox.Show($"Repository not found for folder '{modFolder.FolderPath}'.\n\nPlease check your repository configuration.", 
                                "Repository Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        // Maintain full folder structure from FTP path
                        // e.g., /mods/xyz/@ACE becomes eventBasePath/mods/xyz/@ACE
                        var relativePath = modFolder.FolderPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                        var localPath = Path.Combine(selectedLocation, relativePath);
                        
                        await _ftpManager.DownloadFolderAsync(
                            repository.Url,
                            repository.Port,
                            repository.Username,
                            repository.Password,
                            modFolder.FolderPath,
                            localPath,
                            progress,
                            progressWindow.CancellationToken
                        );
                    }

                    progressWindow.MarkCompleted();
                    MessageBox.Show($"Event '{evt.Name}' download completed!", "Download Complete", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (OperationCanceledException)
                {
                    progressWindow.Close();
                    ((IProgress<string>)progress).Report("Download cancelled by user.");
                }
            }
        }

        private void AddRepository_Click(object sender, RoutedEventArgs e)
        {
            var addRepoWindow = new AddRepositoryWindow();
            addRepoWindow.Owner = this;
            if (addRepoWindow.ShowDialog() == true && addRepoWindow.Repository != null)
            {
                _repositoryManager.AddRepository(addRepoWindow.Repository);
            }
        }

        private void EditRepository_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Repository repo)
            {
                var editRepoWindow = new AddRepositoryWindow(repo);
                editRepoWindow.Owner = this;
                if (editRepoWindow.ShowDialog() == true)
                {
                    _repositoryManager.UpdateRepository(repo);
                    UpdateParametersDisplay();
                }
            }
        }

        private void DeleteRepository_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Repository repo)
            {
                var result = MessageBox.Show($"Are you sure you want to delete repository '{repo.Name}'?", 
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    _repositoryManager.RemoveRepository(repo);
                    UpdateParametersDisplay();
                }
            }
        }

        private async void TestRepositoryConnection_Click(object sender, RoutedEventArgs e)
        {
            var selectedRepo = ManageRepositoriesListBox.SelectedItem as Repository;
            if (selectedRepo == null)
            {
                MessageBox.Show("Please select a repository to test.", "No Selection", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var button = sender as Button;
            if (button != null)
            {
                button.IsEnabled = false;
                button.Content = "Testing...";
            }

            bool success = await Task.Run(() => _ftpManager.TestConnection(selectedRepo));

            if (button != null)
            {
                button.IsEnabled = true;
                button.Content = "Test Connection";
            }

            if (success)
            {
                MessageBox.Show($"Successfully connected to '{selectedRepo.Name}'!", 
                    "Connection Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"Failed to connect to '{selectedRepo.Name}'.\n\nPlease check the connection settings.", 
                    "Connection Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddEvent_Click(object sender, RoutedEventArgs e)
        {
            if (_repositoryManager.Repositories.Count == 0)
            {
                MessageBox.Show("Please add at least one repository before creating an event.", 
                    "No Repositories", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var addEventWindow = new AddEditEventWindow(_repositoryManager, _settingsManager);
            addEventWindow.Owner = this;
            if (addEventWindow.ShowDialog() == true && addEventWindow.Event != null)
            {
                _eventManager.AddEvent(addEventWindow.Event);
            }
        }

        private void EditEvent_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Event evt)
            {
                var editEventWindow = new AddEditEventWindow(_repositoryManager, _settingsManager, evt);
                editEventWindow.Owner = this;
                if (editEventWindow.ShowDialog() == true)
                {
                    _eventManager.UpdateEvent(evt);
                    UpdateParametersDisplay();
                }
            }
        }

        private void DeleteEvent_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Event evt)
            {
                var result = MessageBox.Show($"Are you sure you want to delete event '{evt.Name}'?", 
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    _eventManager.RemoveEvent(evt);
                    UpdateParametersDisplay();
                }
            }
        }

        private void AddServer_Click(object sender, RoutedEventArgs e)
        {
            var addServerWindow = new AddEditServerWindow();
            addServerWindow.Owner = this;
            if (addServerWindow.ShowDialog() == true && addServerWindow.Server != null)
            {
                _serverManager.AddServer(addServerWindow.Server);
            }
        }

        private void EditServer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Server server)
            {
                var editServerWindow = new AddEditServerWindow(server);
                editServerWindow.Owner = this;
                if (editServerWindow.ShowDialog() == true)
                {
                    _serverManager.UpdateServer(server);
                    UpdateParametersDisplay();
                }
            }
        }

        private void DeleteServer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Server server)
            {
                var result = MessageBox.Show($"Are you sure you want to delete server '{server.Name}'?", 
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    _serverManager.RemoveServer(server);
                    UpdateParametersDisplay();
                }
            }
        }

        private bool ValidateArma3Path()
        {
            if (string.IsNullOrWhiteSpace(_settingsManager.Settings.Arma3ExePath) || 
                !File.Exists(_settingsManager.Settings.Arma3ExePath))
            {
                MessageBox.Show("Arma 3 executable path is not set or invalid.\n\nPlease configure it in the Settings tab.", 
                    "Arma 3 Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        private string? SelectDownloadLocation()
        {
            if (_settingsManager.Settings.BaseDownloadLocations == null || _settingsManager.Settings.BaseDownloadLocations.Count == 0)
            {
                MessageBox.Show("No download locations configured.\n\nPlease add at least one download location in Settings.", 
                    "No Download Locations", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            if (_settingsManager.Settings.BaseDownloadLocations.Count == 1)
            {
                return _settingsManager.Settings.BaseDownloadLocations[0];
            }

            var selectLocationWindow = new SelectDownloadLocationWindow(_settingsManager.Settings.BaseDownloadLocations);
            selectLocationWindow.Owner = this;
            if (selectLocationWindow.ShowDialog() == true)
            {
                return selectLocationWindow.SelectedLocation;
            }

            return null;
        }

        private string? FindRepositoryInLocations(string repositoryName)
        {
            foreach (var location in _settingsManager.Settings.BaseDownloadLocations)
            {
                if (Directory.Exists(location))
                {
                    return location;
                }
            }
            return null;
        }

        private string? FindEventInLocations(Event evt)
        {
            foreach (var location in _settingsManager.Settings.BaseDownloadLocations)
            {
                bool allFoldersExist = true;
                foreach (var modFolder in evt.ModFolders)
                {
                    // Skip DLC and Workshop items - they're not downloaded to locations
                    if (modFolder.ItemType == ModItemType.DLC || modFolder.ItemType == ModItemType.Workshop)
                        continue;

                    var relativePath = modFolder.FolderPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                    var localPath = Path.Combine(location, relativePath);
                    if (!Directory.Exists(localPath))
                    {
                        allFoldersExist = false;
                        break;
                    }
                }
                
                if (allFoldersExist)
                {
                    return location;
                }
            }
            return null;
        }

        private void LaunchArma3()
        {
            try
            {
                // Get the final parameters that are already built by UpdateParametersDisplay
                var customParams = CustomParametersTextBox.Text;
                var arguments = _launchParametersManager.GetParametersString(customParams).Replace(Environment.NewLine, " ");

                // Add server connection parameters if a server is selected
                var checkedServer = _serverManager.Servers.FirstOrDefault(s => s.IsChecked);
                if (checkedServer != null)
                {
                    arguments += $" -connect={checkedServer.Address} -port={checkedServer.Port}";
                    if (!string.IsNullOrWhiteSpace(checkedServer.Password))
                    {
                        arguments += $" -password={checkedServer.Password}";
                    }
                }

                var processInfo = new ProcessStartInfo
                {
                    FileName = _settingsManager.Settings.Arma3ExePath,
                    Arguments = arguments,
                    UseShellExecute = false
                };

                Process.Start(processInfo);

                var message = "Arma 3 launched successfully!";
                if (checkedServer != null)
                {
                    message += $"\nConnecting to: {checkedServer.Name}";
                }

                MessageBox.Show(message, "Launch Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch Arma 3: {ex.Message}", 
                    "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class PasswordMaskConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string password && !string.IsNullOrEmpty(password))
            {
                return new string('?', password.Length);
            }
            return "(none)";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}


