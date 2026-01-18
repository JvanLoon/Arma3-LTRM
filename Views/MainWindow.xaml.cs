using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Collections.ObjectModel;
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
        private readonly SettingsManager _settingsManager;
        private readonly FtpManager _ftpManager;
        private readonly LaunchParametersManager _launchParametersManager;
        private ObservableCollection<string> _downloadLocations;

        public MainWindow()
        {
            InitializeComponent();

            _repositoryManager = new RepositoryManager();
            _eventManager = new EventManager();
            _eventManager.Initialize(_repositoryManager);
            _settingsManager = new SettingsManager();
            _ftpManager = new FtpManager();
            _launchParametersManager = new LaunchParametersManager();
            _downloadLocations = new ObservableCollection<string>();

            LoadData();
            InitializeParametersUI();
        }

        private void LoadData()
        {
            RepositoriesListBox.ItemsSource = _repositoryManager.Repositories;
            EventsListBox.ItemsSource = _eventManager.Events;
            ManageRepositoriesListBox.ItemsSource = _repositoryManager.Repositories;
            ManageEventsListBox.ItemsSource = _eventManager.Events;
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

        private void InitializeParametersUI()
        {
            // Create checkboxes for predefined parameters
            var predefinedParams = _launchParametersManager.GetPredefinedParameters();
            var paramDescriptions = new Dictionary<string, string>
            {
                { "windowed", "Windowed - Run in windowed mode" },
                { "world", "World - Show empty world" },
                { "skipIntro", "Skip Intro - Skip intro videos" },
                { "nosplash", "No Splash - Skip splash screens" },
                { "noPause", "No Pause - Don't pause when window loses focus" },
                { "noPauseAudio", "No Pause Audio - Don't pause audio when window loses focus" },
                { "noLogs", "No Logs - Don't create RPT log files" },
                { "showScriptErrors", "Show Script Errors - Show script errors" },
                { "filePatching", "File Patching - Enable file patching" }
            };

            PredefinedParametersPanel.Children.Clear();
            foreach (var param in predefinedParams.OrderBy(p => p.Key))
            {
                var checkBox = new System.Windows.Controls.CheckBox
                {
                    Content = paramDescriptions.ContainsKey(param.Key) ? paramDescriptions[param.Key] : param.Key,
                    IsChecked = param.Value,
                    Tag = param.Key,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE0, 0xE0, 0xE0)),
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                checkBox.Checked += ParameterCheckBox_Changed;
                checkBox.Unchecked += ParameterCheckBox_Changed;
                PredefinedParametersPanel.Children.Add(checkBox);
            }

            UpdateParametersDisplay();
        }

        private void ParameterCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkBox && checkBox.Tag is string paramName)
            {
                _launchParametersManager.SetParameter(paramName, checkBox.IsChecked == true);
                UpdateParametersDisplay();
            }
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

        private void UpdateParametersDisplay()
        {
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
                                modFolders.Add(dlcPath);
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
                            var relativePath = modFolder.FolderPath.TrimStart('/');
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
            
            FinalParametersTextBox.Text = string.IsNullOrWhiteSpace(finalParams) 
                ? "(No parameters selected)" 
                : finalParams;
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
            }
        }

        private void SettingsSave_Click(object sender, RoutedEventArgs e)
        {
            _settingsManager.Settings.Arma3ExePath = SettingsArma3PathTextBox.Text;
            _settingsManager.Settings.BaseDownloadLocations = _downloadLocations.ToList();
            _settingsManager.SaveSettings();
            MessageBox.Show("Settings saved successfully!", "Settings Saved", 
                MessageBoxButton.OK, MessageBoxImage.Information);
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
            var selectedRepos = RepositoriesListBox.SelectedItems.Cast<Repository>().ToList();
            if (selectedRepos.Count == 0)
            {
                MessageBox.Show("Please select at least one repository.", "No Selection", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ValidateArma3Path())
                return;

            var selectedLocation = SelectDownloadLocation();
            if (selectedLocation == null)
                return;

            var downloadPaths = new List<string>();
            foreach (var repo in selectedRepos)
            {
                var repoPath = Path.Combine(selectedLocation, "Repositories", repo.Name);
                downloadPaths.Add(repoPath);

                var progressWindow = new DownloadProgressWindow();
                progressWindow.Owner = this;

                var progress = new Progress<string>(message => progressWindow.AppendLog(message));

                progressWindow.Show();
                var success = await _ftpManager.DownloadRepositoryAsync(repo, repoPath, progress);
                progressWindow.Close();

                if (!success)
                {
                    MessageBox.Show($"Failed to download repository '{repo.Name}'.", "Download Failed", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            LaunchArma3(downloadPaths);
        }

        private async void DownloadRepository_Click(object sender, RoutedEventArgs e)
        {
            var selectedRepos = RepositoriesListBox.SelectedItems.Cast<Repository>().ToList();
            if (selectedRepos.Count == 0)
            {
                MessageBox.Show("Please select at least one repository.", "No Selection", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedLocation = SelectDownloadLocation();
            if (selectedLocation == null)
                return;

            foreach (var repo in selectedRepos)
            {
                var repoPath = Path.Combine(selectedLocation, "Repositories", repo.Name);

                var progressWindow = new DownloadProgressWindow();
                progressWindow.Owner = this;

                var progress = new Progress<string>(message => progressWindow.AppendLog(message));

                progressWindow.Show();
                await _ftpManager.DownloadRepositoryAsync(repo, repoPath, progress, progressWindow.CancellationToken);
                progressWindow.MarkCompleted();
            }

            MessageBox.Show("Repository download completed!", "Download Complete", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LaunchRepository_Click(object sender, RoutedEventArgs e)
        {
            var selectedRepos = RepositoriesListBox.SelectedItems.Cast<Repository>().ToList();
            if (selectedRepos.Count == 0)
            {
                MessageBox.Show("Please select at least one repository.", "No Selection", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ValidateArma3Path())
                return;

            var downloadPaths = new List<string>();
            foreach (var repo in selectedRepos)
            {
                var foundPath = FindRepositoryInLocations(repo.Name);
                if (foundPath == null)
                {
                    MessageBox.Show($"Repository '{repo.Name}' has not been downloaded to any configured location.", 
                        "Repository Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                downloadPaths.Add(foundPath);
            }

            LaunchArma3(downloadPaths);
        }

        private async void DownloadLaunchEvent_Click(object sender, RoutedEventArgs e)
        {
            var selectedEvents = EventsListBox.SelectedItems.Cast<Event>().ToList();
            if (selectedEvents.Count == 0)
            {
                MessageBox.Show("Please select at least one event.", "No Selection", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ValidateArma3Path())
                return;

            var selectedLocation = SelectDownloadLocation();
            if (selectedLocation == null)
                return;

            var eventPaths = new List<string>();
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
                    var relativePath = modFolder.FolderPath.TrimStart('/');
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
                eventPaths.Add(selectedLocation);
            }

            LaunchArma3WithEvent(selectedEvents[0], eventPaths[0]);
        }

        private async void DownloadEvent_Click(object sender, RoutedEventArgs e)
        {
            var selectedEvents = EventsListBox.SelectedItems.Cast<Event>().ToList();
            if (selectedEvents.Count == 0)
            {
                MessageBox.Show("Please select at least one event.", "No Selection", 
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
                        var relativePath = modFolder.FolderPath.TrimStart('/');
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

            foreach (var evt in checkedEvents)
            {
                var foundPath = FindEventInLocations(evt);
                if (foundPath == null)
                {
                    MessageBox.Show($"Event '{evt.Name}' has not been downloaded to any configured location.", 
                        "Event Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                LaunchArma3WithEvent(evt, foundPath);
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
                var repoPath = Path.Combine(location, "Repositories", repositoryName);
                if (Directory.Exists(repoPath))
                {
                    return repoPath;
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

                    var relativePath = modFolder.FolderPath.TrimStart('/');
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

        private void LaunchArma3(List<string> modPaths)
        {
            try
            {
                var modFolders = new List<string>();
                
                foreach (var basePath in modPaths)
                {
                    if (Directory.Exists(basePath))
                    {
                        var directories = Directory.GetDirectories(basePath, "*", SearchOption.AllDirectories);
                        foreach (var dir in directories)
                        {
                            if (Path.GetFileName(dir).StartsWith("@"))
                            {
                                modFolders.Add(dir);
                            }
                        }
                    }
                }

                if (modFolders.Count == 0)
                {
                    MessageBox.Show("No mod folders found (folders starting with '@').", 
                        "No Mods Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _launchParametersManager.UpdateModsList(modFolders);
                var customParams = CustomParametersTextBox.Text;
                var arguments = _launchParametersManager.GetParametersString(customParams).Replace(Environment.NewLine, " ");

                var processInfo = new ProcessStartInfo
                {
                    FileName = _settingsManager.Settings.Arma3ExePath,
                    Arguments = arguments,
                    UseShellExecute = false
                };

                Process.Start(processInfo);

                MessageBox.Show($"Arma 3 launched with {modFolders.Count} mod(s)!", 
                    "Launch Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch Arma 3: {ex.Message}", 
                    "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LaunchArma3WithEvent(Event evt, string basePath)
        {
            try
            {
                var arma3Dir = Path.GetDirectoryName(_settingsManager.Settings.Arma3ExePath);
                if (string.IsNullOrEmpty(arma3Dir))
                {
                    MessageBox.Show("Invalid Arma 3 executable path.", 
                        "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var modFolders = new List<string>();
                
                foreach (var modFolder in evt.ModFolders)
                {
                    if (modFolder.ItemType == ModItemType.DLC)
                    {
                        // DLC folders are in the Arma 3 directory
                        var dlcPath = Path.Combine(arma3Dir, modFolder.FolderPath);
                        if (Directory.Exists(dlcPath))
                        {
                            modFolders.Add(dlcPath);
                        }
                    }
                    else if (modFolder.ItemType == ModItemType.Workshop)
                    {
                        // Workshop items are in the !Workshop folder
                        var workshopPath = Path.Combine(arma3Dir, "!Workshop", modFolder.FolderPath);
                        if (Directory.Exists(workshopPath))
                        {
                            modFolders.Add(workshopPath);
                        }
                    }
                    else
                    {
                        // Repository folders
                        var relativePath = modFolder.FolderPath.TrimStart('/');
                        var localPath = Path.Combine(basePath, relativePath);
                        if (Directory.Exists(localPath))
                        {
                            modFolders.Add(localPath);
                        }
                    }
                }

                if (modFolders.Count == 0)
                {
                    MessageBox.Show("No mod folders found for this event.", 
                        "No Mods Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var modParams = string.Join(";", modFolders);
                var arguments = $"-mod={modParams}";

                var processInfo = new ProcessStartInfo
                {
                    FileName = _settingsManager.Settings.Arma3ExePath,
                    Arguments = arguments,
                    UseShellExecute = false
                };

                Process.Start(processInfo);

                MessageBox.Show($"Arma 3 launched with {modFolders.Count} mod(s)!", 
                    "Launch Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch Arma 3: {ex.Message}", 
                    "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
