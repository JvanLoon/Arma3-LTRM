using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Arma_3_LTRM.Models
{
    public class Event : INotifyPropertyChanged
    {
        private string _name;
        private bool _isChecked = false;
        private ObservableCollection<Repository> _repositories;
        private ObservableCollection<ModFolder> _modFolders;

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

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

        public ObservableCollection<Repository> Repositories
        {
            get => _repositories;
            set
            {
                if (_repositories != value)
                {
                    _repositories = value;
                    OnPropertyChanged(nameof(Repositories));
                }
            }
        }

        public ObservableCollection<ModFolder> ModFolders
        {
            get => _modFolders;
            set
            {
                if (_modFolders != value)
                {
                    _modFolders = value;
                    OnPropertyChanged(nameof(ModFolders));
                }
            }
        }

        public Event()
        {
            _name = string.Empty;
            _repositories = new ObservableCollection<Repository>();
            _modFolders = new ObservableCollection<ModFolder>();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum ModItemType
    {
        RepositoryFolder,
        DLC,
        Workshop
    }

    public class ModFolder : INotifyPropertyChanged
    {
        private Guid _repositoryId;
        private string _folderPath;
        private ModItemType _itemType;
        private bool _isWorkshop;

        public Guid RepositoryId
        {
            get => _repositoryId;
            set
            {
                if (_repositoryId != value)
                {
                    _repositoryId = value;
                    OnPropertyChanged(nameof(RepositoryId));
                }
            }
        }

        public string FolderPath
        {
            get => _folderPath;
            set
            {
                if (_folderPath != value)
                {
                    _folderPath = value;
                    OnPropertyChanged(nameof(FolderPath));
                }
            }
        }

        public ModItemType ItemType
        {
            get => _itemType;
            set
            {
                if (_itemType != value)
                {
                    _itemType = value;
                    OnPropertyChanged(nameof(ItemType));
                }
            }
        }

        public bool IsWorkshop
        {
            get => _isWorkshop;
            set
            {
                if (_isWorkshop != value)
                {
                    _isWorkshop = value;
                    OnPropertyChanged(nameof(IsWorkshop));
                }
            }
        }

        public ModFolder()
        {
            _repositoryId = Guid.Empty;
            _folderPath = string.Empty;
            _itemType = ModItemType.RepositoryFolder;
            _isWorkshop = false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
