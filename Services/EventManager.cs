using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Arma_3_LTRM.Models;

namespace Arma_3_LTRM.Services
{
    public class EventManager
    {
        private const string SETTINGS_FOLDER = "Settings";
        private const string EVENTS_FOLDER = "Events";
        public ObservableCollection<Event> Events { get; private set; }

        public EventManager()
        {
            Events = new ObservableCollection<Event>();
            LoadEvents();
        }

        public void AddEvent(Event eventItem)
        {
            Events.Add(eventItem);
            SaveEvent(eventItem);
        }

        public void RemoveEvent(Event eventItem)
        {
            Events.Remove(eventItem);
            DeleteEvent(eventItem);
        }

        public void UpdateEvent(Event eventItem)
        {
            SaveEvent(eventItem);
        }

        private void SaveEvent(Event eventItem)
        {
            try
            {
                var eventsPath = Path.Combine(SETTINGS_FOLDER, EVENTS_FOLDER);
                if (!Directory.Exists(eventsPath))
                {
                    Directory.CreateDirectory(eventsPath);
                }

                var fileName = Path.Combine(eventsPath, $"{SanitizeFileName(eventItem.Name)}.json");
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                var json = JsonSerializer.Serialize(eventItem, options);
                File.WriteAllText(fileName, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving event: {ex.Message}");
            }
        }

        private void DeleteEvent(Event eventItem)
        {
            try
            {
                var fileName = Path.Combine(EVENTS_FOLDER, $"{SanitizeFileName(eventItem.Name)}.json");
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting event: {ex.Message}");
            }
        }

        private void LoadEvents()
        {
            try
            {
                if (!Directory.Exists(EVENTS_FOLDER))
                {
                    Directory.CreateDirectory(EVENTS_FOLDER);
                    return;
                }

                var eventFiles = Directory.GetFiles(EVENTS_FOLDER, "*.json");
                foreach (var file in eventFiles)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var eventItem = JsonSerializer.Deserialize<Event>(json);
                        if (eventItem != null)
                        {
                            Events.Add(eventItem);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading event file {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading events: {ex.Message}");
            }
        }

        private string SanitizeFileName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return string.Join("_", name.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        }

        public string GetEventDownloadPath(Event eventItem, string baseDownloadLocation)
        {
            return Path.Combine(baseDownloadLocation, "Events", SanitizeFileName(eventItem.Name));
        }
    }
}
