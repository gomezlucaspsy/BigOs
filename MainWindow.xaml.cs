using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UnixBrowser.Models;
using UnixBrowser.Services;

namespace UnixBrowser
{
    public partial class MainWindow : Window
    {
        private BrowserEngine?      _engine;
        private FavoritesManager    _favorites = new();
        private TabManager          _tabs       = new();
        private string?             _currentTabId;
        private string              _currentTitle = string.Empty;

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

                AddressBar.KeyDown        += AddressBar_KeyDown;
                _engine.OnUrlChanged      += url   => Dispatcher.Invoke(() => OnEngineUrlChanged(url));
                _engine.OnTitleChanged    += title => Dispatcher.Invoke(() => _currentTitle = title);

                BackButton.Click    += (s, e) => _engine.GoBack();
                ForwardButton.Click += (s, e) => _engine.GoForward();
                RefreshButton.Click += (s, e) => _engine.Refresh();
                HomeButton.Click    += (s, e) => _engine.Navigate("https://www.google.com");

                _favorites.OnChanged += () => Dispatcher.Invoke(RefreshFavoritesBar);
                _tabs.OnTabsChanged  += () => Dispatcher.Invoke(RefreshTabBar);
                _tabs.OnTabActivated += tab => Dispatcher.Invoke(() => OnTabActivated(tab));

                // Create first tab
                _tabs.CreateNewTab("about:blank", "New Tab");

                RefreshFavoritesBar();
                RefreshTabBar();
                StatusText.Text = "Ready";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
            }
        }

        // ── Tabs ──────────────────────────────────────────────────────────────

        private void NewTabBtn_Click(object sender, RoutedEventArgs e)
        {
            _tabs.CreateNewTab("about:blank", "New Tab");
        }

        private void OnTabActivated(Tab tab)
        {
            _currentTabId = tab.Id;
            AddressBar.Text = tab.Url;
            _currentTitle = tab.Title;
        }

        private void RefreshTabBar()
        {
            TabsPanel.Children.Clear();

            foreach (var tab in _tabs.Tabs)
            {
                var tabLabel = tab.Title.Length > 15 ? tab.Title[..15] + "…" : tab.Title;
                var isActive = tab.Id == _tabs.ActiveTab?.Id;

                // Tab container
                var tabBg = new Border
                {
                    Background = isActive
                        ? new SolidColorBrush(Color.FromRgb(0x2a, 0x2a, 0x2a))
                        : new SolidColorBrush(Color.FromRgb(0x1a, 0x1a, 0x1a)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
                    BorderThickness = new Thickness(0, 0, 1, 0),
                    Padding = new Thickness(2, 0, 0, 0),
                    Height = 26
                };

                var tabGrid = new Grid();
                tabGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                tabGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });

                // Tab label (clickable)
                var tabBtn = new Button
                {
                    Content = tabLabel,
                    Tag = tab.Id,
                    Background = Brushes.Transparent,
                    Foreground = isActive
                        ? new SolidColorBrush(Color.FromRgb(0x00, 0xff, 0x00))
                        : new SolidColorBrush(Color.FromRgb(0xaa, 0xaa, 0xaa)),
                    FontFamily = new FontFamily("Courier New"),
                    FontSize = 10,
                    Padding = new Thickness(6, 3, 3, 0),
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    ToolTip = tab.Url
                };

                tabBtn.Click += (s, e) => _tabs.ActivateTab((string)((Button)s).Tag);
                Grid.SetColumn(tabBtn, 0);
                tabGrid.Children.Add(tabBtn);

                // Close button (X)
                if (_tabs.Tabs.Count > 1)
                {
                    var closeBtn = new Button
                    {
                        Content = "✕",
                        Tag = tab.Id,
                        Background = Brushes.Transparent,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0x66, 0x66)),
                        FontSize = 10,
                        Padding = new Thickness(0, 1, 2, 0),
                        BorderThickness = new Thickness(0),
                        Cursor = System.Windows.Input.Cursors.Hand,
                        Width = 18,
                        Height = 18
                    };

                    closeBtn.Click += (s, e) => _tabs.CloseTab((string)((Button)s).Tag);
                    Grid.SetColumn(closeBtn, 1);
                    tabGrid.Children.Add(closeBtn);
                }

                tabBg.Child = tabGrid;
                TabsPanel.Children.Add(tabBg);
            }
        }

        // ── Address bar ──────────────────────────────────────────────────────

        private void AddressBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                _engine!.Navigate(AddressBar.Text);
                if (_currentTabId != null)
                {
                    _tabs.UpdateTab(_currentTabId, url: AddressBar.Text);
                }
                e.Handled = true;
            }
        }

        private void OnEngineUrlChanged(string url)
        {
            AddressBar.Text = url;

            // Update current tab's URL
            if (_currentTabId != null)
            {
                _tabs.UpdateTab(_currentTabId, url: url);
            }

            // Highlight ★ yellow if already a favourite, grey otherwise
            AddFavoriteBtn.Foreground = _favorites.IsFavorite(url)
                ? new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00))
                : new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
        }

        // ── Favourites ───────────────────────────────────────────────────────

        private void AddFavoriteBtn_Click(object sender, RoutedEventArgs e)
        {
            var url   = AddressBar.Text;
            var title = string.IsNullOrWhiteSpace(_currentTitle) ? url : _currentTitle;

            if (_favorites.IsFavorite(url))
            {
                _favorites.Remove(url);
                StatusText.Text = "Removed from favorites.";
            }
            else
            {
                _favorites.Add(title, url);
                StatusText.Text = $"Saved: {title}";
            }
        }

        private void ToggleFavoritesBtn_Click(object sender, RoutedEventArgs e)
        {
            FavoritesBar.Visibility = FavoritesBar.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void RefreshFavoritesBar()
        {
            FavoritesPanel.Children.Clear();

            if (!_favorites.Favorites.Any())
            {
                FavoritesPanel.Children.Add(new TextBlock
                {
                    Text       = "No favorites yet — click ★ to save a page",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                    FontSize   = 10,
                    FontFamily = new FontFamily("Courier New"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin     = new Thickness(6, 0, 0, 0)
                });
                return;
            }

            foreach (var fav in _favorites.Favorites)
            {
                var label = fav.Title.Length > 22 ? fav.Title[..22] + "…" : fav.Title;

                var btn = new Button
                {
                    Content           = label,
                    Tag               = fav.Url,
                    Background        = Brushes.Transparent,
                    Foreground        = new SolidColorBrush(Color.FromRgb(0xaa, 0xaa, 0xaa)),
                    BorderBrush       = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
                    BorderThickness   = new Thickness(1),
                    FontFamily        = new FontFamily("Courier New"),
                    FontSize          = 10,
                    Padding           = new Thickness(8, 2, 8, 2),
                    Margin            = new Thickness(3, 2, 0, 2),
                    Cursor            = System.Windows.Input.Cursors.Hand,
                    ToolTip           = fav.Url
                };

                btn.Click += (s, e) => _engine!.Navigate((string)((Button)s).Tag);

                // Right-click → Remove
                var menu       = new ContextMenu();
                var removeItem = new MenuItem { Header = "✕  Remove from Favorites" };
                var capturedUrl = fav.Url;
                removeItem.Click += (s, e) => _favorites.Remove(capturedUrl);
                menu.Items.Add(removeItem);
                btn.ContextMenu = menu;

                FavoritesPanel.Children.Add(btn);
            }
        }

        // ── Window controls ──────────────────────────────────────────────────

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState.Minimized;

        private void MaximizeBtn_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;

        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();
    }
}
