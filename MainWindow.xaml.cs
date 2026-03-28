using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;

namespace UnixBrowser
{
    public partial class MainWindow : Window
    {
        private BrowserEngine? _engine;

        public MainWindow()
        {
            InitializeComponent();
            MouseLeftButtonDown += (s, e) => DragMove();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _engine = new BrowserEngine(WebViewContainer);
                await _engine.Initialize();

                // Bind events
                AddressBar.KeyDown += AddressBar_KeyDown;
                _engine.OnUrlChanged += url => Dispatcher.Invoke(() => UpdateAddressBar(url));

                BackButton.Click += (s, e) => _engine.GoBack();
                ForwardButton.Click += (s, e) => _engine.GoForward();
                RefreshButton.Click += (s, e) => _engine.Refresh();
                HomeButton.Click += (s, e) => _engine.Navigate("https://www.example.com");

                StatusText.Text = "Ready";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
            }
        }

        private void AddressBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                _engine!.Navigate(AddressBar.Text);
                e.Handled = true;
            }
        }

        public void UpdateAddressBar(string url)
        {
            AddressBar.Text = url;
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeBtn_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
