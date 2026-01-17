using System.IO;
using System.Text.Json;
using Arma_3_LTRM.Models;

namespace Arma_3_LTRM.Services
{
    public class SettingsManager
    {
        private const string SETTINGS_FILE = "settings.json";
        public AppSettings Settings { get; private set; }

        public SettingsManager()
        {
            Settings = new AppSettings();
            LoadSettings();
        }

        public void SaveSettings()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                var json = JsonSerializer.Serialize(Settings, options);
                File.WriteAllText(SETTINGS_FILE, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(SETTINGS_FILE))
                {
                    var json = File.ReadAllText(SETTINGS_FILE);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        Settings = settings;
                    }
                }
                else
                {
                    SaveSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            }
        }
    }
}
