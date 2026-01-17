using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Arma_3_LTRM.Models;

namespace Arma_3_LTRM.Services
{
    public class RepositoryManager
    {
        private const string SETTINGS_FOLDER = "Settings";
        private const string REPOSITORIES_FILE = "repositories.json";
        public ObservableCollection<Repository> Repositories { get; private set; }

        public RepositoryManager()
        {
            Repositories = new ObservableCollection<Repository>();
            LoadRepositories();
        }

        public void AddRepository(Repository repository)
        {
            Repositories.Add(repository);
            SaveRepositories();
        }

        public void RemoveRepository(Repository repository)
        {
            Repositories.Remove(repository);
            SaveRepositories();
        }

        public void UpdateRepository(Repository repository)
        {
            SaveRepositories();
            UpdateRepositoryInAllEvents(repository);
        }

        private void UpdateRepositoryInAllEvents(Repository repository)
        {
            try
            {
                var eventsPath = Path.Combine(SETTINGS_FOLDER, "Events");
                if (!Directory.Exists(eventsPath))
                {
                    return;
                }

                var eventFiles = Directory.GetFiles(eventsPath, "*.json");
                foreach (var eventFile in eventFiles)
                {
                    try
                    {
                        var json = File.ReadAllText(eventFile);
                        var eventItem = JsonSerializer.Deserialize<Event>(json);
                        
                        if (eventItem != null)
                        {
                            bool updated = false;
                            var repoToUpdate = eventItem.Repositories.FirstOrDefault(r => r.Id == repository.Id);
                            if (repoToUpdate != null)
                            {
                                var index = eventItem.Repositories.IndexOf(repoToUpdate);
                                eventItem.Repositories[index] = repository;
                                updated = true;
                            }

                            if (updated)
                            {
                                var options = new JsonSerializerOptions
                                {
                                    WriteIndented = true
                                };
                                var updatedJson = JsonSerializer.Serialize(eventItem, options);
                                File.WriteAllText(eventFile, updatedJson);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error updating repository in event file {eventFile}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating repository in events: {ex.Message}");
            }
        }

        public List<Repository> GetEnabledRepositories()
        {
            return Repositories.Where(r => r.IsEnabled).ToList();
        }

        private void SaveRepositories()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                var json = JsonSerializer.Serialize(Repositories, options);
                File.WriteAllText(REPOSITORIES_FILE, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving repositories: {ex.Message}");
            }
        }

        private void LoadRepositories()
        {
            try
            {
                if (File.Exists(REPOSITORIES_FILE))
                {
                    var json = File.ReadAllText(REPOSITORIES_FILE);
                    var repositories = JsonSerializer.Deserialize<List<Repository>>(json);
                    if (repositories != null)
                    {
                        foreach (var repo in repositories)
                        {
                            Repositories.Add(repo);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading repositories: {ex.Message}");
            }
        }
    }
}
