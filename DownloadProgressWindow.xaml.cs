using System.Windows;
using System.Windows.Threading;

namespace Arma_3_LTRM
{
    public partial class DownloadProgressWindow : Window
    {
        private readonly List<Repository> _repositories;
        private readonly string _destinationPath;
        private readonly FtpManager _ftpManager;

        public bool DownloadSuccessful { get; private set; }

        public DownloadProgressWindow(List<Repository> repositories, string destinationPath)
        {
            InitializeComponent();
            _repositories = repositories;
            _destinationPath = destinationPath;
            _ftpManager = new FtpManager();
            
            Loaded += DownloadProgressWindow_Loaded;
        }

        private async void DownloadProgressWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                await Task.Delay(100);
                
                DownloadSuccessful = true;

                var progress = new Progress<string>(message =>
                {
                    ProgressTextBlock.Text += message + Environment.NewLine;
                    
                    Dispatcher.InvokeAsync(() =>
                    {
                        var scrollViewer = FindScrollViewer(ProgressTextBlock);
                        scrollViewer?.ScrollToEnd();
                    }, DispatcherPriority.Background);
                });

                foreach (var repository in _repositories)
                {
                    ProgressTextBlock.Text += $"{Environment.NewLine}=== Downloading from repository: {repository.Name} ==={Environment.NewLine}";
                    
                    await Task.Delay(50);

                    var result = await _ftpManager.DownloadRepositoryAsync(repository, _destinationPath, progress);
                    
                    if (!result)
                    {
                        DownloadSuccessful = false;
                    }

                    ProgressTextBlock.Text += $"=== Repository {repository.Name} completed ==={Environment.NewLine}";
                }

                if (DownloadSuccessful)
                {
                    ProgressTextBlock.Text += $"{Environment.NewLine}All downloads completed successfully!";
                }
                else
                {
                    ProgressTextBlock.Text += $"{Environment.NewLine}Some downloads failed. Please check the log above.";
                }
                
                CloseButton.IsEnabled = true;
            }, DispatcherPriority.Background);
        }

        private System.Windows.Controls.ScrollViewer? FindScrollViewer(DependencyObject element)
        {
            if (element == null)
                return null;

            var parent = System.Windows.Media.VisualTreeHelper.GetParent(element);
            
            if (parent is System.Windows.Controls.ScrollViewer scrollViewer)
                return scrollViewer;

            return FindScrollViewer(parent);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
