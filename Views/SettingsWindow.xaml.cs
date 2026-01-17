using System.Windows;
using Arma_3_LTRM.Services;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using OpenFolderDialog = Microsoft.Win32.OpenFolderDialog;

namespace Arma_3_LTRM.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsManager _settingsManager;

        public SettingsWindow(SettingsManager settingsManager)
        {
            InitializeComponent();
            _settingsManager = settingsManager;
            LoadSettings();
        }

        private void LoadSettings()
        {
            Arma3PathTextBox.Text = _settingsManager.Settings.Arma3ExePath;
            BaseDownloadPathTextBox.Text = _settingsManager.Settings.BaseDownloadLocation;
        }

        private void BrowseArma3Button_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Arma 3 Executable|arma3.exe;arma3_x64.exe|All Files|*.*",
                Title = "Select Arma 3 Executable"
            };

            if (dialog.ShowDialog() == true)
            {
                Arma3PathTextBox.Text = dialog.FileName;
            }
        }

        private void BrowseDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Base Download Location"
            };

            if (dialog.ShowDialog() == true)
            {
                BaseDownloadPathTextBox.Text = dialog.FolderName;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _settingsManager.Settings.Arma3ExePath = Arma3PathTextBox.Text;
            _settingsManager.Settings.BaseDownloadLocation = BaseDownloadPathTextBox.Text;
            _settingsManager.SaveSettings();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
