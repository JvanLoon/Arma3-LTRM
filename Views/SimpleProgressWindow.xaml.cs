using System.Windows;

namespace Arma_3_LTRM.Views
{
    public partial class SimpleProgressWindow : Window
    {
        public SimpleProgressWindow()
        {
            InitializeComponent();
        }

        public void AppendLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                LogTextBlock.Text += message + Environment.NewLine;
                ScrollViewer.ScrollToEnd();
            });
        }
    }
}
