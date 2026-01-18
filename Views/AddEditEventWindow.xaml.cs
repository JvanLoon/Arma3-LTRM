using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using Arma_3_LTRM.Models;
using Arma_3_LTRM.Services;
using MessageBox = System.Windows.MessageBox;
using Button = System.Windows.Controls.Button;

namespace Arma_3_LTRM.Views
{
    public partial class AddEditEventWindow : Window
    {
        private readonly RepositoryManager _repositoryManager;
        private readonly SettingsManager _settingsManager;
        private readonly FtpManager _ftpManager;
        private readonly Arma3ContentScanner _contentScanner;
        public Event Event { get; private set; }
        private Repository? _currentBrowsingRepo;
        private ObservableCollection<ModFolderDisplayItem> _displayItems;
        private ObservableCollection<DlcDisplayItem> _dlcItems;
        private ObservableCollection<WorkshopDisplayItem> _workshopItems;
        private Dictionary<Guid, List<FtpTreeNode>> _repositoryCache;
        private bool _isPreloading = false;

        public AddEditEventWindow(RepositoryManager repositoryManager, SettingsManager settingsManager, Event? existingEvent = null)
        {
            InitializeComponent();
            _repositoryManager = repositoryManager;
            _settingsManager = settingsManager;
            _ftpManager = new FtpManager();
            _contentScanner = new Arma3ContentScanner();
            _displayItems = new ObservableCollection<ModFolderDisplayItem>();
            _dlcItems = new ObservableCollection<DlcDisplayItem>();
            _workshopItems = new ObservableCollection<WorkshopDisplayItem>();
            _repositoryCache = new Dictionary<Guid, List<FtpTreeNode>>();

            RepositoryComboBox.ItemsSource = _repositoryManager.Repositories;

            if (existingEvent != null)
            {
                Event = existingEvent;
                EventNameTextBox.Text = Event.Name;
                UpdateDisplayItems();
            }
            else
            {
                Event = new Event();
            }

            ModFoldersListBox.ItemsSource = _displayItems;
            DlcListBox.ItemsSource = _dlcItems;
            WorkshopListBox.ItemsSource = _workshopItems;

            LoadDlcItems();
            LoadWorkshopItems();

            if (_repositoryManager.Repositories.Count > 0)
            {
                RepositoryComboBox.SelectedIndex = 0;
            }

            // Start preloading all repositories in background
            PreloadAllRepositories();
        }

        private async void PreloadAllRepositories()
        {
            _isPreloading = true;
            
            foreach (var repository in _repositoryManager.Repositories)
            {
                if (_repositoryCache.ContainsKey(repository.Id))
                    continue;

                try
                {
                    var rootItems = await _ftpManager.BrowseDirectoryAsync(
                        repository.Url,
                        repository.Port,
                        repository.Username,
                        repository.Password,
                        "/"
                    );

                    var nodes = new List<FtpTreeNode>();
                    foreach (var item in rootItems.Where(i => i.IsDirectory))
                    {
                        var node = new FtpTreeNode
                        {
                            Name = item.Name,
                            FullPath = item.FullPath,
                            IsDirectory = item.IsDirectory,
                            IsSelectable = item.Name.StartsWith("@")
                        };
                        
                        // Add placeholder for lazy loading
                        node.Children.Add(new FtpTreeNode { Name = "Loading..." });
                        nodes.Add(node);
                    }

                    _repositoryCache[repository.Id] = nodes;
                }
                catch
                {
                    // Silent fail for background loading
                }
            }

            _isPreloading = false;

            // Refresh the current view if a repository is selected
            if (RepositoryComboBox.SelectedItem is Repository selectedRepo)
            {
                LoadCachedRepository(selectedRepo);
            }
        }

        private void LoadDlcItems()
        {
            _dlcItems.Clear();
            var dlcs = _contentScanner.ScanDlcs(_settingsManager.Settings.Arma3ExePath);
            
            foreach (var dlc in dlcs)
            {
                // Check if already in event
                bool isChecked = Event.ModFolders.Any(mf => 
                    mf.ItemType == ModItemType.DLC && 
                    mf.FolderPath.Equals(dlc.FolderName, StringComparison.OrdinalIgnoreCase));

                _dlcItems.Add(new DlcDisplayItem
                {
                    Name = dlc.Name,
                    FolderName = dlc.FolderName,
                    KeyFileName = dlc.KeyFileName,
                    IsChecked = isChecked
                });
            }
        }

        private void LoadWorkshopItems()
        {
            _workshopItems.Clear();
            var workshopItems = _contentScanner.ScanWorkshopItems(_settingsManager.Settings.Arma3ExePath);
            
            foreach (var item in workshopItems)
            {
                // Check if already in event
                bool isChecked = Event.ModFolders.Any(mf => 
                    mf.ItemType == ModItemType.Workshop && 
                    mf.FolderPath.Equals(item.FolderName, StringComparison.OrdinalIgnoreCase));

                _workshopItems.Add(new WorkshopDisplayItem
                {
                    FolderName = item.FolderName,
                    FullPath = item.FullPath,
                    IsChecked = isChecked
                });
            }
        }

        private void UpdateDisplayItems()
        {
            _displayItems.Clear();
            foreach (var modFolder in Event.ModFolders)
            {
                string typeLabel = modFolder.ItemType switch
                {
                    ModItemType.DLC => "DLC",
                    ModItemType.Workshop => "Workshop",
                    _ => Event.Repositories.FirstOrDefault(r => r.Id == modFolder.RepositoryId)?.Name ?? "Repository"
                };

                _displayItems.Add(new ModFolderDisplayItem
                {
                    ModFolder = modFolder,
                    TypeLabel = typeLabel,
                    FolderPath = modFolder.FolderPath
                });
            }
        }

        private void RepositoryComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (RepositoryComboBox.SelectedItem is Repository repository)
            {
                _currentBrowsingRepo = repository;
                LoadCachedRepository(repository);
            }
        }

        private async void LoadCachedRepository(Repository repository)
        {
            FtpTreeView.Items.Clear();
            AddSelectedButton.IsEnabled = false;

            if (_repositoryCache.ContainsKey(repository.Id))
            {
                // Load from cache
                var nodes = _repositoryCache[repository.Id];
                foreach (var node in nodes)
                {
                    FtpTreeView.Items.Add(node);
                }

                var modFolderCount = nodes.Count(n => n.IsSelectable);
                BrowseStatusText.Text = $"Found {modFolderCount} mod folder(s) - only @ folders can be checked";
                AddSelectedButton.IsEnabled = true;
            }
            else if (!_isPreloading)
            {
                // Load if not in cache and not currently preloading
                BrowseStatusText.Text = $"Loading {repository.Name}...";
                
                try
                {
                    var rootItems = await _ftpManager.BrowseDirectoryAsync(
                        repository.Url,
                        repository.Port,
                        repository.Username,
                        repository.Password,
                        "/"
                    );

                    var nodes = new List<FtpTreeNode>();
                    foreach (var item in rootItems.Where(i => i.IsDirectory))
                    {
                        var node = new FtpTreeNode
                        {
                            Name = item.Name,
                            FullPath = item.FullPath,
                            IsDirectory = item.IsDirectory,
                            IsSelectable = item.Name.StartsWith("@")
                        };
                        
                        node.Children.Add(new FtpTreeNode { Name = "Loading..." });
                        nodes.Add(node);
                        FtpTreeView.Items.Add(node);
                    }

                    _repositoryCache[repository.Id] = nodes;

                    var modFolderCount = nodes.Count(n => n.IsSelectable);
                    BrowseStatusText.Text = $"Found {modFolderCount} mod folder(s) - only @ folders can be checked";
                    AddSelectedButton.IsEnabled = true;
                }
                catch (Exception ex)
                {
                    BrowseStatusText.Text = $"Error: {ex.Message}";
                }
            }
            else
            {
                BrowseStatusText.Text = "Loading in background, please wait...";
            }
        }

        private async void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TreeViewItem treeViewItem && 
                treeViewItem.DataContext is FtpTreeNode node)
            {
                if (node.Children.Count == 1 && node.Children[0].Name == "Loading...")
                {
                    node.Children.Clear();
                    
                    if (_currentBrowsingRepo != null)
                    {
                        try
                        {
                            var children = await _ftpManager.BrowseDirectoryAsync(
                                _currentBrowsingRepo.Url,
                                _currentBrowsingRepo.Port,
                                _currentBrowsingRepo.Username,
                                _currentBrowsingRepo.Password,
                                node.FullPath
                            );

                            foreach (var child in children.Where(c => c.IsDirectory))
                            {
                                var childNode = new FtpTreeNode
                                {
                                    Name = child.Name,
                                    FullPath = child.FullPath,
                                    IsDirectory = child.IsDirectory,
                                    IsSelectable = child.Name.StartsWith("@")
                                };
                                
                                childNode.Children.Add(new FtpTreeNode { Name = "Loading..." });
                                node.Children.Add(childNode);
                            }
                        }
                        catch
                        {
                            // Silent fail for child loading
                        }
                    }
                }
            }
        }

        private void AddSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentBrowsingRepo == null)
                return;

            var checkedItems = GetCheckedNodes(FtpTreeView.Items.Cast<FtpTreeNode>());
            int addedCount = 0;

            foreach (var item in checkedItems)
            {
                if (Event.ModFolders.Any(mf => mf.FolderPath == item.FullPath && 
                                               mf.RepositoryId == _currentBrowsingRepo.Id))
                    continue;

                if (!Event.Repositories.Any(r => r.Id == _currentBrowsingRepo.Id))
                {
                    Event.Repositories.Add(_currentBrowsingRepo);
                }

                var modFolder = new ModFolder
                {
                    RepositoryId = _currentBrowsingRepo.Id,
                    FolderPath = item.FullPath,
                    ItemType = ModItemType.RepositoryFolder,
                    IsWorkshop = false
                };

                Event.ModFolders.Add(modFolder);
                item.IsChecked = false;
                addedCount++;
            }

            if (addedCount > 0)
            {
                UpdateDisplayItems();
                MessageBox.Show($"Added {addedCount} folder(s) to the event.", "Folders Added",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void AddDlcButton_Click(object sender, RoutedEventArgs e)
        {
            var checkedDlcs = _dlcItems.Where(d => d.IsChecked).ToList();
            int addedCount = 0;

            foreach (var dlc in checkedDlcs)
            {
                if (Event.ModFolders.Any(mf => mf.ItemType == ModItemType.DLC && 
                                               mf.FolderPath.Equals(dlc.FolderName, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var modFolder = new ModFolder
                {
                    RepositoryId = Guid.Empty,
                    FolderPath = dlc.FolderName,
                    ItemType = ModItemType.DLC,
                    IsWorkshop = false
                };

                Event.ModFolders.Add(modFolder);
                dlc.IsChecked = false;
                addedCount++;
            }

            if (addedCount > 0)
            {
                UpdateDisplayItems();
                MessageBox.Show($"Added {addedCount} DLC(s) to the event.", "DLC Added",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void AddWorkshopButton_Click(object sender, RoutedEventArgs e)
        {
            var checkedWorkshop = _workshopItems.Where(w => w.IsChecked).ToList();
            int addedCount = 0;

            foreach (var workshop in checkedWorkshop)
            {
                if (Event.ModFolders.Any(mf => mf.ItemType == ModItemType.Workshop && 
                                               mf.FolderPath.Equals(workshop.FolderName, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var modFolder = new ModFolder
                {
                    RepositoryId = Guid.Empty,
                    FolderPath = workshop.FolderName,
                    ItemType = ModItemType.Workshop,
                    IsWorkshop = true
                };

                Event.ModFolders.Add(modFolder);
                workshop.IsChecked = false;
                addedCount++;
            }

            if (addedCount > 0)
            {
                UpdateDisplayItems();
                MessageBox.Show($"Added {addedCount} workshop item(s) to the event.", "Workshop Items Added",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private List<FtpTreeNode> GetCheckedNodes(IEnumerable<FtpTreeNode> nodes)
        {
            var checkedNodes = new List<FtpTreeNode>();
            
            foreach (var node in nodes)
            {
                if (node.IsChecked)
                {
                    checkedNodes.Add(node);
                }
                
                if (node.Children.Count > 0)
                {
                    checkedNodes.AddRange(GetCheckedNodes(node.Children));
                }
            }

            return checkedNodes;
        }

        private void RemoveFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ModFolderDisplayItem displayItem)
            {
                Event.ModFolders.Remove(displayItem.ModFolder);
                UpdateDisplayItems();
                
                // Refresh the available items lists so removed items appear again
                if (displayItem.ModFolder.ItemType == ModItemType.DLC)
                {
                    LoadDlcItems();
                }
                else if (displayItem.ModFolder.ItemType == ModItemType.Workshop)
                {
                    LoadWorkshopItems();
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(EventNameTextBox.Text))
            {
                MessageBox.Show("Please enter an event name.", "No Event Name",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (Event.ModFolders.Count == 0)
            {
                MessageBox.Show("Please add at least one mod folder to the event.", "No Mod Folders",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Event.Name = EventNameTextBox.Text.Trim();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class FtpTreeNode : INotifyPropertyChanged
    {
        private bool _isChecked;
        
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public bool IsSelectable { get; set; } = true;
        
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged(nameof(IsChecked));
                }
            }
        }

        public ObservableCollection<FtpTreeNode> Children { get; set; } = new ObservableCollection<FtpTreeNode>();

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ModFolderDisplayItem
    {
        public ModFolder ModFolder { get; set; } = null!;
        public string TypeLabel { get; set; } = string.Empty;
        public string FolderPath { get; set; } = string.Empty;
    }

    public class DlcDisplayItem : INotifyPropertyChanged
    {
        private bool _isChecked;

        public string Name { get; set; } = string.Empty;
        public string FolderName { get; set; } = string.Empty;
        public string KeyFileName { get; set; } = string.Empty;
        
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged(nameof(IsChecked));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class WorkshopDisplayItem : INotifyPropertyChanged
    {
        private bool _isChecked;

        public string FolderName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged(nameof(IsChecked));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}




