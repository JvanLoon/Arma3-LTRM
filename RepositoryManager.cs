using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace Arma_3_LTRM
{
    public class RepositoryManager
    {
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
