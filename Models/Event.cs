using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Arma_3_LTRM.Models
{
    public class Event : INotifyPropertyChanged
    {
        private string _name;
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
            _modFolders = new ObservableCollection<ModFolder>();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ModFolder : INotifyPropertyChanged
    {
        private string _repositoryName;
        private string _ftpUrl;
        private int _port;
        private string _username;
        private string _password;
        private string _folderPath;

        public string RepositoryName
        {
            get => _repositoryName;
            set
            {
                if (_repositoryName != value)
                {
                    _repositoryName = value;
                    OnPropertyChanged(nameof(RepositoryName));
                }
            }
        }

        public string FtpUrl
        {
            get => _ftpUrl;
            set
            {
                if (_ftpUrl != value)
                {
                    _ftpUrl = value;
                    OnPropertyChanged(nameof(FtpUrl));
                }
            }
        }

        public int Port
        {
            get => _port;
            set
            {
                if (_port != value)
                {
                    _port = value;
                    OnPropertyChanged(nameof(Port));
                }
            }
        }

        public string Username
        {
            get => _username;
            set
            {
                if (_username != value)
                {
                    _username = value;
                    OnPropertyChanged(nameof(Username));
                }
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                if (_password != value)
                {
                    _password = value;
                    OnPropertyChanged(nameof(Password));
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

        public ModFolder()
        {
            _repositoryName = string.Empty;
            _ftpUrl = string.Empty;
            _port = 21;
            _username = string.Empty;
            _password = string.Empty;
            _folderPath = string.Empty;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
