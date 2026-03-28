using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using UnixBrowser.Services;
using UnixBrowser.Config;

namespace UnixBrowser
{
    public class BrowserEngine
    {
        private WebView2? _webView;
        private readonly Grid _container;
        private CoreWebView2? _coreWebView;
        private PWAManager? _pwaManager;
        private ReactNativeManager? _reactNativeManager;

        public event Action<string>? OnUrlChanged;
        public event Action<string>? OnTitleChanged;

        public string CurrentTitle => _coreWebView?.DocumentTitle ?? string.Empty;
        public string CurrentUrl   => _coreWebView?.Source        ?? string.Empty;

        public BrowserEngine(Grid container)
        {
            _container = container;
        }

        public async Task Initialize()
        {
            // Create WebView2 control
            _webView = new WebView2();

            _container.Children.Add(_webView);

            // Initialize WebView2
            await _webView.EnsureCoreWebView2Async();
            _coreWebView = _webView.CoreWebView2;

            // Initialize managers
            _pwaManager = new PWAManager(_coreWebView);
            _reactNativeManager = new ReactNativeManager(_coreWebView);

            // Configure settings for performance
            ConfigureSettings();

            // Subscribe to events
            _coreWebView.NavigationStarting   += CoreWebView_NavigationStarting;
            _coreWebView.NavigationCompleted  += CoreWebView_NavigationCompleted;
            _coreWebView.SourceChanged        += CoreWebView_SourceChanged;
            _coreWebView.DocumentTitleChanged += (s, e) => OnTitleChanged?.Invoke(_coreWebView.DocumentTitle);

            // Navigate to home
            _webView!.Source = new Uri("about:blank");
        }

        private void ConfigureSettings()
        {
            var settings = _coreWebView!.Settings;

            // Performance optimization
            settings.AreDefaultContextMenusEnabled = true;
            settings.AreDevToolsEnabled = false; // Disable for better performance in release
            settings.IsScriptEnabled = true;
            settings.IsWebMessageEnabled = true;
            settings.IsStatusBarEnabled = false;
            settings.AreDefaultScriptDialogsEnabled = true;
            settings.IsZoomControlEnabled = false;
            settings.IsPinchZoomEnabled = false;

            // Set custom user agent for app detection
            _coreWebView.Settings.UserAgent = BrowserConfig.GetUserAgent();
        }

        private async void CoreWebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                // Enable PWA features
                if (BrowserConfig.EnablePWASupport)
                {
                    await _pwaManager!.EnablePWAFeatures();
                }

                // Enable React Native support
                if (BrowserConfig.EnableReactNativeDetection)
                {
                    await _reactNativeManager!.EnableReactNativeSupport();
                }
            }
        }

        private void CoreWebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Navigation starting: {e.Uri}");
        }

        private void CoreWebView_SourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
        {
            OnUrlChanged?.Invoke(_coreWebView!.Source);
        }

        public void Navigate(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            // Add https:// if no protocol specified
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            try
            {
                _webView!.Source = new Uri(url);
            }
            catch (UriFormatException)
            {
                // Invalid URL, navigate to search
                _webView!.Source = new Uri($"https://www.google.com/search?q={Uri.EscapeDataString(url)}");
            }
        }

        public void GoBack()
        {
            if (_webView!.CanGoBack)
                _webView.GoBack();
        }

        public void GoForward()
        {
            if (_webView!.CanGoForward)
                _webView.GoForward();
        }

        public void Refresh()
        {
            _webView!.Reload();
        }

        public void HardRefresh()
        {
            _coreWebView!.Reload();
        }
    }
}
