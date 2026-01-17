using System.Collections.Generic;
using System.Linq;

namespace Arma_3_LTRM
{
    public class LaunchParametersManager
    {
        private readonly Dictionary<string, bool> _parameters = new Dictionary<string, bool>();
        private readonly List<string> _modPaths = new List<string>();

        public event EventHandler? ParametersChanged;

        public LaunchParametersManager()
        {
            _parameters["windowed"] = false;
            _parameters["world"] = false;
            _parameters["skipIntro"] = false;
            _parameters["nosplash"] = false;
            _parameters["noPause"] = false;
            _parameters["noPauseAudio"] = false;
            _parameters["noLogs"] = false;
            _parameters["showScriptErrors"] = false;
            _parameters["filePatching"] = false;
        }

        public void SetParameter(string parameterName, bool enabled)
        {
            if (_parameters.ContainsKey(parameterName))
            {
                _parameters[parameterName] = enabled;
                OnParametersChanged();
            }
        }

        public void UpdateModsList(List<string> modPaths)
        {
            _modPaths.Clear();
            _modPaths.AddRange(modPaths);
            OnParametersChanged();
        }

        public List<string> GetParametersList()
        {
            var parameters = new List<string>();

            foreach (var param in _parameters.Where(p => p.Value))
            {
                parameters.Add($"-{param.Key}");
            }

            foreach (var modPath in _modPaths)
            {
                parameters.Add($"-mod={modPath}");
            }

            return parameters;
        }

        public string GetParametersString()
        {
            return string.Join(" ", GetParametersList());
        }

        private void OnParametersChanged()
        {
            ParametersChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
