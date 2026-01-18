using System.Windows;
using System.Windows.Threading;
using MessageBox = System.Windows.MessageBox;

namespace Arma_3_LTRM.Views
{
    public partial class DownloadProgressWindow : Window
    {
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isCompleted = false;
        private int _downloadedCount = 0;
        private int _skippedCount = 0;
        private int _deletedCount = 0;
        
        public CancellationToken CancellationToken => _cancellationTokenSource?.Token ?? CancellationToken.None;

        public DownloadProgressWindow()
        {
            InitializeComponent();
            _cancellationTokenSource = new CancellationTokenSource();
            UpdateCounters();
        }

        public void AppendLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressTextBlock.Text += message + Environment.NewLine;
                
                // Parse the message to update counters
                ParseAndUpdateCounters(message);
                
                Dispatcher.InvokeAsync(() =>
                {
                    var scrollViewer = FindScrollViewer(ProgressTextBlock);
                    scrollViewer?.ScrollToEnd();
                }, DispatcherPriority.Background);
            });
        }

        private void ParseAndUpdateCounters(string message)
        {
            // Look for download completion messages
            if (message.Contains("downloaded, ") && message.Contains("up-to-date"))
            {
                // Parse: "? Sync completed: X downloaded, Y up-to-date, Z files deleted"
                // or: "Folder download completed: X files downloaded, Y files up-to-date, Z files deleted"
                
                var parts = message.Split(new[] { "downloaded, ", "up-to-date" }, StringSplitOptions.None);
                if (parts.Length >= 2)
                {
                    // Extract downloaded count
                    var downloadedPart = parts[0];
                    var downloadedMatch = System.Text.RegularExpressions.Regex.Match(downloadedPart, @"(\d+)\s*$");
                    if (downloadedMatch.Success && int.TryParse(downloadedMatch.Groups[1].Value, out int downloaded))
                    {
                        _downloadedCount = downloaded;
                    }
                    
                    // Extract skipped count
                    var skippedPart = parts[1];
                    var skippedMatch = System.Text.RegularExpressions.Regex.Match(skippedPart, @"^\s*(\d+)");
                    if (skippedMatch.Success && int.TryParse(skippedMatch.Groups[1].Value, out int skipped))
                    {
                        _skippedCount = skipped;
                    }
                    
                    // Extract deleted count if present
                    if (message.Contains("deleted"))
                    {
                        var deletedMatch = System.Text.RegularExpressions.Regex.Match(message, @"(\d+)\s+files?\s+deleted");
                        if (deletedMatch.Success && int.TryParse(deletedMatch.Groups[1].Value, out int deleted))
                        {
                            _deletedCount = deleted;
                        }
                    }
                    
                    UpdateCounters();
                }
            }
            // Track individual file deletions in real-time
            else if (message.StartsWith("Deleted (not in FTP):") || message.StartsWith("Deleted directory (not in FTP):"))
            {
                _deletedCount++;
                UpdateCounters();
            }
            // Track individual downloads in real-time
            else if (message.StartsWith("Downloading:"))
            {
                _downloadedCount++;
                UpdateCounters();
            }
        }

        private void UpdateCounters()
        {
            DownloadedCountText.Text = _downloadedCount.ToString();
            SkippedCountText.Text = _skippedCount.ToString();
            DeletedCountText.Text = _deletedCount.ToString();
        }

        public void MarkCompleted()
        {
            Dispatcher.Invoke(() =>
            {
                _isCompleted = true;
                CancelButton.Content = "Close";
                CancelButton.IsEnabled = true;
            });
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (CancelButton.Content.ToString() == "Close")
            {
                Close();
            }
            else
            {
                _cancellationTokenSource?.Cancel();
                CancelButton.IsEnabled = false;
                CancelButton.Content = "Cancelling...";
                AppendLog("Cancellation requested - stopping download...");
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // If download is completed, allow closing without warning
            if (_isCompleted)
            {
                return;
            }

            // If download is still in progress, ask for confirmation
            if (_cancellationTokenSource != null && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                var result = MessageBox.Show("Download is in progress. Are you sure you want to cancel?", 
                    "Cancel Download", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
                
                _cancellationTokenSource.Cancel();
            }
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
    }
}

