using System.Diagnostics;
using System.IO;
using System.Windows;
using Arma_3_LTRM.Models;
using Arma_3_LTRM.Services;
using EventManager = Arma_3_LTRM.Services.EventManager;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using Button = System.Windows.Controls.Button;

namespace Arma_3_LTRM.Views
{
    public partial class MainWindow : Window
    {
        private readonly RepositoryManager _repositoryManager;
        private readonly EventManager _eventManager;
        private readonly SettingsManager _settingsManager;
        private readonly FtpManager _ftpManager;

        public MainWindow()
        {
            InitializeComponent();

            _repositoryManager = new RepositoryManager();
            _eventManager = new EventManager();
            _settingsManager = new SettingsManager();
            _ftpManager = new FtpManager();

            LoadData();
        }

        private void LoadData()
        {
            RepositoriesListBox.ItemsSource = _repositoryManager.Repositories;
            EventsListBox.ItemsSource = _eventManager.Events;
            ManageRepositoriesListBox.ItemsSource = _repositoryManager.Repositories;
            ManageEventsListBox.ItemsSource = _eventManager.Events;
        }

        private void MenuSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(_settingsManager);
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
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

            var downloadPaths = new List<string>();
            foreach (var repo in selectedRepos)
            {
                var repoPath = Path.Combine(_settingsManager.Settings.BaseDownloadLocation, "Repositories", repo.Name);
                downloadPaths.Add(repoPath);

                var progressWindow = new SimpleProgressWindow();
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

            foreach (var repo in selectedRepos)
            {
                var repoPath = Path.Combine(_settingsManager.Settings.BaseDownloadLocation, "Repositories", repo.Name);

                var progressWindow = new SimpleProgressWindow();
                progressWindow.Owner = this;

                var progress = new Progress<string>(message => progressWindow.AppendLog(message));

                progressWindow.Show();
                await _ftpManager.DownloadRepositoryAsync(repo, repoPath, progress);
                progressWindow.Close();
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
                var repoPath = Path.Combine(_settingsManager.Settings.BaseDownloadLocation, "Repositories", repo.Name);
                if (!Directory.Exists(repoPath))
                {
                    MessageBox.Show($"Repository '{repo.Name}' has not been downloaded yet.\n\nPath: {repoPath}", 
                        "Repository Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                downloadPaths.Add(repoPath);
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

            var eventPaths = new List<string>();
            foreach (var evt in selectedEvents)
            {
                var progressWindow = new SimpleProgressWindow();
                progressWindow.Owner = this;
                var progress = new Progress<string>(message => progressWindow.AppendLog(message));
                progressWindow.Show();

                foreach (var modFolder in evt.ModFolders)
                {
                    // Maintain full folder structure from FTP path
                    // e.g., /mods/xyz/@ACE becomes eventBasePath/mods/xyz/@ACE
                    var relativePath = modFolder.FolderPath.TrimStart('/');
                    var localPath = Path.Combine(_settingsManager.Settings.BaseDownloadLocation, relativePath);
                    
                    await _ftpManager.DownloadFolderAsync(
                        modFolder.FtpUrl,
                        modFolder.Port,
                        modFolder.Username,
                        modFolder.Password,
                        modFolder.FolderPath,
                        localPath,
                        progress
                    );
                }

                progressWindow.Close();
                eventPaths.Add(_settingsManager.Settings.BaseDownloadLocation);
            }

            LaunchArma3(eventPaths);
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

            foreach (var evt in selectedEvents)
            {
                var progressWindow = new SimpleProgressWindow();
                progressWindow.Owner = this;
                var progress = new Progress<string>(message => progressWindow.AppendLog(message));
                progressWindow.Show();

                foreach (var modFolder in evt.ModFolders)
                {
                    // Maintain full folder structure from FTP path
                    // e.g., /mods/xyz/@ACE becomes eventBasePath/mods/xyz/@ACE
                    var relativePath = modFolder.FolderPath.TrimStart('/');
                    var localPath = Path.Combine(_settingsManager.Settings.BaseDownloadLocation, relativePath);
                    
                    await _ftpManager.DownloadFolderAsync(
                        modFolder.FtpUrl,
                        modFolder.Port,
                        modFolder.Username,
                        modFolder.Password,
                        modFolder.FolderPath,
                        localPath,
                        progress
                    );
                }

                progressWindow.Close();
            }

            MessageBox.Show("Event download completed!", "Download Complete", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LaunchEvent_Click(object sender, RoutedEventArgs e)
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

            var eventPaths = new List<string>();
            foreach (var evt in selectedEvents)
            {
                if (!Directory.Exists(_settingsManager.Settings.BaseDownloadLocation))
                {
                    MessageBox.Show($"Event '{evt.Name}' has not been downloaded yet.\n\nPath: {_settingsManager.Settings.BaseDownloadLocation}", 
                        "Event Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                eventPaths.Add(_settingsManager.Settings.BaseDownloadLocation);
            }

            LaunchArma3(eventPaths);
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

            var addEventWindow = new AddEditEventWindow(_repositoryManager);
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
                var editEventWindow = new AddEditEventWindow(_repositoryManager, evt);
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
                MessageBox.Show("Arma 3 executable path is not set or invalid.\n\nPlease configure it in Settings (File ? Settings).", 
                    "Arma 3 Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
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
