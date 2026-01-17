using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace Arma_3_LTRM.Models
{
    public class AppSettings : INotifyPropertyChanged
    {
        private string _arma3ExePath;
        private string _baseDownloadLocation;
        private List<string> _hiddenRepositories;
        private List<string> _hiddenEvents;

        public string Arma3ExePath
        {
            get => _arma3ExePath;
            set
            {
                if (_arma3ExePath != value)
                {
                    _arma3ExePath = value;
                    OnPropertyChanged(nameof(Arma3ExePath));
                }
            }
        }

        public string BaseDownloadLocation
        {
            get => _baseDownloadLocation;
            set
            {
                if (_baseDownloadLocation != value)
                {
                    _baseDownloadLocation = value;
                    OnPropertyChanged(nameof(BaseDownloadLocation));
                }
            }
        }

        public List<string> HiddenRepositories
        {
            get => _hiddenRepositories;
            set
            {
                if (_hiddenRepositories != value)
                {
                    _hiddenRepositories = value;
                    OnPropertyChanged(nameof(HiddenRepositories));
                }
            }
        }

        public List<string> HiddenEvents
        {
            get => _hiddenEvents;
            set
            {
                if (_hiddenEvents != value)
                {
                    _hiddenEvents = value;
                    OnPropertyChanged(nameof(HiddenEvents));
                }
            }
        }

        public AppSettings()
        {
            _arma3ExePath = string.Empty;
            _baseDownloadLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads");
            _hiddenRepositories = new List<string>();
            _hiddenEvents = new List<string>();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
