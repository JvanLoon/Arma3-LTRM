using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace Arma_3_LTRM
{
    public partial class ModListSelectionWindow : Window
    {
        public class ModListLocation
        {
            public string FolderName { get; set; } = string.Empty;
            public string FullPath { get; set; } = string.Empty;
        }

        public string? SelectedPath { get; private set; }

        public ModListSelectionWindow(List<string> modListPaths)
        {
            InitializeComponent();

            var modListLocations = modListPaths.Select(path => new ModListLocation
            {
                FolderName = System.IO.Path.GetFileName(path) ?? path,
                FullPath = path
            }).ToList();

            ModListComboBox.ItemsSource = modListLocations;
            if (modListLocations.Any())
            {
                ModListComboBox.SelectedIndex = 0;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (ModListComboBox.SelectedItem is ModListLocation selectedLocation)
            {
                SelectedPath = selectedLocation.FullPath;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Please select a mod list location.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
