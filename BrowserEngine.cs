// ============================================================
// BrowserEngine.cs — Unix Browser Core Engine
// Version : 1.1.0 (Apollo 11)
// Purpose : Owns and orchestrates all browser subsystems.
//           Every subsystem is attached here and only here.
//           Think of this as Mission Control — it does not
//           fly the rocket, it coordinates those who do.
// ============================================================
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using UnixBrowser.Services;
using UnixBrowser.Config;

namespace UnixBrowser
{
    /// <summary>
    /// The core browser engine. Owns the WebView2 instance and
    /// wires together all subsystems: streaming, ad blocking,
    /// PWA support, React Native support, and Quick Share.
    /// </summary>
    public class BrowserEngine
    {
        // ── Private subsystems ────────────────────────────────
        private WebView2?            _webView;             // The Chromium rendering control
        private CoreWebView2?        _coreWebView;         // Low-level WebView2 API surface
        private PWAManager?          _pwaManager;          // Progressive Web App support
        private ReactNativeManager?  _reactNativeManager;  // React Native web app support
        private readonly Grid        _container;           // WPF container that hosts WebView2
        private readonly AdBlocker         _adBlocker   = new(); // Blocks ads/trackers at request level
        private readonly QuickShareService _quickShare  = new(); // P2P URL sharing via QR
        private readonly StreamingPipeline _stream      = new(); // Real-time page load telemetry

        // Apollo 11 standard: version is always explicit and visible
        public const string Version = "1.1.0";

        // ── Public events ───────────────────────────────────
        // Consumers subscribe to these to react to browser state changes.
        // No polling — pure event-driven architecture.
        public event Action<string>?         OnUrlChanged;           // Fired on every URL change
        public event Action<string>?         OnTitleChanged;         // Fired when page title changes
        public event Action<int>?            OnAdsBlocked;           // Fired with running blocked count
        public event Action<SharedItem>?     OnItemReceived;         // Fired when QR share is received
        public event Action<string>?         OnShareStatus;          // Fired with share status message
        public event Action<int>?            OnProgressChanged;      // Fired with load progress 0-100
        public event Action<WebAssemblyInfo>? OnWebAssemblyDetected; // Fired when WASM site detected

        // ── Public read-only state ───────────────────────────
        // Expose only what consumers need. Implementation stays private.
        public string             CurrentTitle  => _coreWebView?.DocumentTitle ?? string.Empty;
        public string             CurrentUrl    => _coreWebView?.Source        ?? string.Empty;
        public bool               AdBlockOn     => _adBlocker.IsEnabled;
        public int                AdsBlocked    => _adBlocker.BlockedCount;
        public string             LocalIP       => _quickShare.LocalIP;
        public CoreWebView2?      CoreWebView2  => _coreWebView;
        /// <summary>Direct access to the Quick Share service for the dialog.</summary>
        public QuickShareService  QuickShare    => _quickShare;

        /// <summary>
        /// Accepts the WPF Grid that will host the WebView2 control.
        /// The engine does not create UI — it is given a container.
        /// </summary>
        public BrowserEngine(Grid container)
        {
            _container = container;
        }

        /// <summary>
        /// Bootstraps the engine in the correct order:
        /// 1. Create and mount WebView2
        /// 2. Configure performance settings
        /// 3. Attach subsystems (stream → adblock → share)
        /// 4. Subscribe to navigation events
        /// Order matters — subsystems must attach after CoreWebView2 exists.
        /// </summary>
        public async Task Initialize()
        {
            _webView = new WebView2();
            _container.Children.Add(_webView);

            // Wait for the Chromium process to be ready before doing anything
            await _webView.EnsureCoreWebView2Async();
            _coreWebView = _webView.CoreWebView2;

            // Initialize feature managers that need the core WebView2 reference
            _pwaManager          = new PWAManager(_coreWebView);
            _reactNativeManager  = new ReactNativeManager(_coreWebView);

            // Apply performance and security settings before first navigation
            ConfigureSettings();

            // Streaming must attach first — it observes all resource requests
            _stream.Attach(_coreWebView);
            _stream.OnProgressChanged     += pct  => OnProgressChanged?.Invoke(pct);
            _stream.OnWebAssemblyDetected += info => OnWebAssemblyDetected?.Invoke(info);

            // Ad blocker also intercepts resource requests — attaches after stream
            _adBlocker.Attach(_coreWebView);
            _adBlocker.OnBlockedCountChanged += count => OnAdsBlocked?.Invoke(count);

            // Quick share listens on localhost for incoming shared items
            _quickShare.OnItemReceived   += item => OnItemReceived?.Invoke(item);
            _quickShare.OnStatusChanged  += msg  => OnShareStatus?.Invoke(msg);
            _quickShare.StartReceiving();

            // Navigation events bubble up to the UI layer via public events
            _coreWebView.NavigationStarting   += OnNavigationStarting;
            _coreWebView.NavigationCompleted  += OnNavigationCompleted;
            _coreWebView.SourceChanged        += OnSourceChanged;
            _coreWebView.DocumentTitleChanged += (s, e) => OnTitleChanged?.Invoke(_coreWebView.DocumentTitle);

            // Start on blank — the user or caller decides where to go first
            _webView.Source = new Uri("about:blank");
        }

        /// <summary>
        /// Applies WebView2 settings optimized for a minimal, fast browser.
        /// Disables features that add overhead without user value.
        /// </summary>
        private void ConfigureSettings()
        {
            var settings = _coreWebView!.Settings;

            settings.AreDefaultContextMenusEnabled  = true;   // Keep right-click menus
            settings.AreDevToolsEnabled             = false;  // No DevTools in production
            settings.IsScriptEnabled                = true;   // JS must run for modern sites
            settings.IsWebMessageEnabled            = true;   // Required for PWA messaging
            settings.IsStatusBarEnabled             = false;  // We have our own status bar
            settings.AreDefaultScriptDialogsEnabled = true;   // Allow alert/confirm dialogs
            settings.IsZoomControlEnabled           = false;  // Prevents accidental zoom
            settings.IsPinchZoomEnabled             = false;  // Desktop app, not touch

            // Custom user agent identifies this as Unix Browser and enables
            // React Native web apps to detect and adapt their layout
            _coreWebView.Settings.UserAgent = BrowserConfig.GetUserAgent();
        }

        // ── Navigation event handlers ────────────────────────────

        /// <summary>
        /// Called when navigation begins. Currently used for diagnostics.
        /// Future: could intercept and redirect certain protocols.
        /// </summary>
        private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[Engine] Navigation starting → {e.Uri}");
        }

        /// <summary>
        /// Called when navigation finishes. Activates PWA and React Native
        /// support after the page DOM is ready to receive injected scripts.
        /// </summary>
        private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess) return;

            if (BrowserConfig.EnablePWASupport)
                await _pwaManager!.EnablePWAFeatures();

            if (BrowserConfig.EnableReactNativeDetection)
                await _reactNativeManager!.EnableReactNativeSupport();
        }

        /// <summary>
        /// Called on every URL change including hash/fragment changes.
        /// Bubbles the new URL up to the UI layer.
        /// </summary>
        private void OnSourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
        {
            OnUrlChanged?.Invoke(_coreWebView!.Source);
        }

        // ── Public navigation commands ──────────────────────────

        /// <summary>
        /// Navigates to a URL. Automatically prepends https:// if no
        /// protocol is given. Falls back to a Google search if the
        /// input is not a valid URL — just like a real browser.
        /// </summary>
        public void Navigate(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            // Treat bare input like "google.com" as a web address
            if (!url.StartsWith("http://",  StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("about:",   StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            try
            {
                _webView!.Source = new Uri(url);
            }
            catch (UriFormatException)
            {
                // Input is not a valid URL — treat it as a search query
                _webView!.Source = new Uri($"https://www.google.com/search?q={Uri.EscapeDataString(url)}");
            }
        }

        /// <summary>Go back one step in the session history if possible.</summary>
        public void GoBack()    { if (_webView!.CanGoBack)    _webView.GoBack(); }

        /// <summary>Go forward one step in the session history if possible.</summary>
        public void GoForward() { if (_webView!.CanGoForward) _webView.GoForward(); }

        /// <summary>Reload the current page.</summary>
        public void Refresh()   { _webView!.Reload(); }

        /// <summary>Toggle the ad blocker on or off.</summary>
        public void ToggleAdBlocker() => _adBlocker.Toggle();

        /// <summary>Share the current page URL via QR to the target IP.</summary>
        public async Task ShareCurrentUrl(string targetIp)
            => await _quickShare.ShareUrl(targetIp, CurrentUrl, CurrentTitle);

        /// <summary>Scan the local subnet for other Unix Browser instances.</summary>
        public async Task<List<string>> DiscoverDevices()
            => await _quickShare.DiscoverDevices();
    }
}
