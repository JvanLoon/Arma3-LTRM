using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Arma_3_LTRM.Models;

namespace Arma_3_LTRM.Services
{
    public class LaunchParametersManager
    {
        private readonly List<string> _modPaths = new List<string>();
        private LaunchParameters _launchParameters;

        public event EventHandler? ParametersChanged;

        public LaunchParameters LaunchParameters
        {
            get => _launchParameters;
            set
            {
                _launchParameters = value;
                _launchParameters.PropertyChanged += (s, e) => OnParametersChanged();
            }
        }

        public LaunchParametersManager()
        {
            _launchParameters = new LaunchParameters();
            _launchParameters.PropertyChanged += (s, e) => OnParametersChanged();
        }

        public LaunchParametersManager(LaunchParameters savedParameters)
        {
            _launchParameters = savedParameters ?? new LaunchParameters();
            _launchParameters.PropertyChanged += (s, e) => OnParametersChanged();
        }

        public ObservableCollection<string> GetAvailableProfiles()
        {
            var profiles = new ObservableCollection<string>();
            var userName = Environment.UserName;
            
            var defaultProfilePath = Path.Combine("C:\\Users", userName, "Documents", "Arma 3");
            var otherProfilesPath = Path.Combine("C:\\Users", userName, "Documents", "Arma 3 - Other Profiles");

            if (Directory.Exists(defaultProfilePath))
            {
                profiles.Add(defaultProfilePath);
            }

            if (Directory.Exists(otherProfilesPath))
            {
                var directories = Directory.GetDirectories(otherProfilesPath);
                foreach (var dir in directories)
                {
                    profiles.Add(dir);
                }
            }

            return profiles;
        }

        public void UpdateModsList(List<string> modPaths)
        {
            _modPaths.Clear();
            _modPaths.AddRange(modPaths);
            OnParametersChanged();
        }

        public string GetParametersString(string customParameters = "")
        {
            var parameters = new List<string>();

            if (!string.IsNullOrWhiteSpace(_launchParameters.ProfilePath))
            {
                parameters.Add($"-profiles=\"{_launchParameters.ProfilePath}\"");
            }

            if (!string.IsNullOrWhiteSpace(_launchParameters.Unit))
            {
                parameters.Add($"-unit={_launchParameters.Unit}");
            }

            if (_launchParameters.UseMission && !string.IsNullOrWhiteSpace(_launchParameters.MissionPath))
            {
                parameters.Add($"\"{_launchParameters.MissionPath}\"");
            }

            if (_launchParameters.Windowed)
            {
                parameters.Add("-window");
                parameters.Add("-noWindowBorder");
            }

            if (_launchParameters.NoSplash)
            {
                parameters.Add("-nosplash");
            }

            if (_launchParameters.SkipIntro)
            {
                parameters.Add("-skipIntro");
            }

            if (_launchParameters.EmptyWorld)
            {
                parameters.Add("-world=empty");
            }

            if (_launchParameters.EnableHT)
            {
                parameters.Add("-enableHT");
            }

            if (_launchParameters.ShowScriptErrors)
            {
                parameters.Add("-showScriptErrors");
            }

            if (_launchParameters.NoPause)
            {
                parameters.Add("-noPause");
            }

            if (_launchParameters.NoPauseAudio)
            {
                parameters.Add("-noPauseAudio");
            }

            if (_launchParameters.NoLogs)
            {
                parameters.Add("-noLogs");
            }

            if (_launchParameters.NoFreezeCheck)
            {
                parameters.Add("-noFreezeCheck");
            }

            if (_launchParameters.NoFilePatching)
            {
                parameters.Add("-noFilePatching");
            }

            if (_launchParameters.Debug)
            {
                parameters.Add("-debug");
            }

            if (!string.IsNullOrWhiteSpace(_launchParameters.Config))
            {
                parameters.Add($"-config=\"{_launchParameters.Config}\"");
            }

            if (!string.IsNullOrWhiteSpace(_launchParameters.BePath))
            {
                parameters.Add($"-bePath=\"{_launchParameters.BePath}\"");
            }

            if (!string.IsNullOrWhiteSpace(customParameters))
            {
                var lines = customParameters.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        if (!trimmed.StartsWith("-"))
                        {
                            parameters.Add($"-{trimmed}");
                        }
                        else
                        {
                            parameters.Add(trimmed);
                        }
                    }
                }
            }

            // Combine all mod paths into a single -mod= parameter with semicolon separation
            if (_modPaths.Count > 0)
            {
                var modParam = string.Join(";", _modPaths);
                parameters.Add($"-mod=\"{modParam}\"");
            }

            return string.Join(Environment.NewLine, parameters);
        }

        private void OnParametersChanged()
        {
            ParametersChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
