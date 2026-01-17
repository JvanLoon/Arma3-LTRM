using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace Arma_3_LTRM
{
    public partial class AddRepositoryWindow : Window
    {
        public Repository? Repository { get; private set; }

        public AddRepositoryWindow()
        {
            InitializeComponent();
        }

        public AddRepositoryWindow(Repository repository) : this()
        {
            Repository = repository;
            RepositoryNameTextBox.Text = repository.Name;
            UrlTextBox.Text = repository.Url;
            PortTextBox.Text = repository.Port.ToString();
            UsernameTextBox.Text = repository.Username;
            PasswordBox.Password = repository.Password;
            Title = "Edit Repository";
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(RepositoryNameTextBox.Text))
            {
                MessageBox.Show("Please enter a repository name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(UrlTextBox.Text))
            {
                MessageBox.Show("Please enter a URL.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(PortTextBox.Text, out int port) || port < 1 || port > 65535)
            {
                MessageBox.Show("Please enter a valid port number (1-65535).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (Repository == null)
            {
                Repository = new Repository();
            }

            Repository.Name = RepositoryNameTextBox.Text;
            Repository.Url = UrlTextBox.Text;
            Repository.Port = port;
            Repository.Username = UsernameTextBox.Text;
            Repository.Password = PasswordBox.Password;

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
