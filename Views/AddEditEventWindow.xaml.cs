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
        private readonly FtpManager _ftpManager;
        public Event Event { get; private set; }
        private Repository? _currentBrowsingRepo;
        private ObservableCollection<ModFolderDisplayItem> _displayItems;

        public AddEditEventWindow(RepositoryManager repositoryManager, Event? existingEvent = null)
        {
            InitializeComponent();
            _repositoryManager = repositoryManager;
            _ftpManager = new FtpManager();
            _displayItems = new ObservableCollection<ModFolderDisplayItem>();

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

            if (_repositoryManager.Repositories.Count > 0)
            {
                RepositoryComboBox.SelectedIndex = 0;
            }
        }

        private void UpdateDisplayItems()
        {
            _displayItems.Clear();
            foreach (var modFolder in Event.ModFolders)
            {
                var repo = _repositoryManager.Repositories.FirstOrDefault(r => r.Id == modFolder.RepositoryId);
                _displayItems.Add(new ModFolderDisplayItem
                {
                    ModFolder = modFolder,
                    RepositoryName = repo?.Name ?? "Unknown Repository",
                    FolderPath = modFolder.FolderPath
                });
            }
        }

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            if (RepositoryComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select a repository first.", "No Repository Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _currentBrowsingRepo = (Repository)RepositoryComboBox.SelectedItem;

            BrowseButton.IsEnabled = false;
            BrowseButton.Content = "Loading...";
            BrowseStatusText.Text = $"Connecting to {_currentBrowsingRepo.Name}...";
            FtpTreeView.Items.Clear();

            try
            {
                var rootItems = await _ftpManager.BrowseDirectoryAsync(
                    _currentBrowsingRepo.Url,
                    _currentBrowsingRepo.Port,
                    _currentBrowsingRepo.Username,
                    _currentBrowsingRepo.Password,
                    "/"
                );

                if (rootItems.Count == 0)
                {
                    BrowseStatusText.Text = "No folders found or connection failed";
                }
                else
                {
                    var modFolderCount = rootItems.Count(i => i.IsDirectory && i.Name.StartsWith("@"));
                    BrowseStatusText.Text = $"Found {modFolderCount} mod folder(s) - only @ folders can be checked";
                    
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
                        
                        FtpTreeView.Items.Add(node);
                    }

                    AddSelectedButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                BrowseStatusText.Text = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to browse repository: {ex.Message}",
                    "Browse Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BrowseButton.IsEnabled = true;
                BrowseButton.Content = "Browse Repository Folders";
            }
        }

        private async void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            // Lazy load children when expanding a node
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
                // Check if already added
                if (Event.ModFolders.Any(mf => mf.FolderPath == item.FullPath && 
                                               mf.RepositoryId == _currentBrowsingRepo.Id))
                    continue;

                var modFolder = new ModFolder
                {
                    RepositoryId = _currentBrowsingRepo.Id,
                    FolderPath = item.FullPath
                };

                Event.ModFolders.Add(modFolder);
                item.IsChecked = false; // Uncheck after adding
                addedCount++;
            }

            if (addedCount > 0)
            {
                UpdateDisplayItems();
                MessageBox.Show($"Added {addedCount} folder(s) to the event.", "Folders Added",
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

    // Tree node class for FTP browsing
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

    // Display wrapper for ModFolder to show repository name
    public class ModFolderDisplayItem
    {
        public ModFolder ModFolder { get; set; } = null!;
        public string RepositoryName { get; set; } = string.Empty;
        public string FolderPath { get; set; } = string.Empty;
    }
}


