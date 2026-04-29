// ============================================================
// AdBlocker.cs — Request-level Ad & Tracker Blocker
// Purpose : Intercepts every HTTP request before it leaves
//           the browser and drops known ad/tracker URLs.
//           No bytes sent = no data leaked = pure browsing.
// Philosophy: The web is 0s and 1s. Ads are noise injected
//             between those bits. We filter them out.
// ============================================================
using Microsoft.Web.WebView2.Core;

namespace UnixBrowser.Services
{
    /// <summary>
    /// Blocks ad networks, trackers, and fingerprinters at the
    /// network request level — before any bytes leave the machine.
    /// Operates as a blacklist: anything not on the list passes through.
    /// </summary>
    public class AdBlocker
    {
        private bool _enabled     = true;  // On by default — user can toggle off
        private int  _blockedCount = 0;    // Running total for status bar display

        public bool IsEnabled    => _enabled;
        public int  BlockedCount => _blockedCount;

        /// <summary>Fired every time a request is blocked. Carries the new running total.</summary>
        public event Action<int>? OnBlockedCountChanged;

        // Known ad/tracker domains to block
        private static readonly HashSet<string> BlockedDomains = new(StringComparer.OrdinalIgnoreCase)
        {
            // Ad networks
            "doubleclick.net", "googlesyndication.com", "googleadservices.com",
            "adnxs.com", "ads.yahoo.com", "advertising.com", "adroll.com",
            "amazon-adsystem.com", "adsafeprotected.com", "adsrvr.org",
            "adtech.de", "adtechus.com", "adform.net", "adblade.com",
            "media.net", "outbrain.com", "taboola.com", "revcontent.com",
            "mgid.com", "zergnet.com", "contentad.net", "adbuff.com",

            // Trackers
            "google-analytics.com", "googletagmanager.com", "hotjar.com",
            "mixpanel.com", "segment.com", "amplitude.com", "fullstory.com",
            "logrocket.com", "mouseflow.com", "crazyegg.com", "optimizely.com",
            "quantserve.com", "scorecardresearch.com", "comscore.com",
            "statcounter.com", "chartbeat.com", "parsely.com", "newrelic.com",

            // Social trackers
            "facebook.com/tr", "connect.facebook.net", "platform.twitter.com",
            "ads.linkedin.com", "snap.licdn.com", "analytics.tiktok.com",

            // Fingerprinting
            "fingerprintjs.com", "fpjs.io", "clarity.ms",
        };

        // Ad URL patterns to block
        private static readonly string[] BlockedPatterns =
        {
            "/ads/", "/ad/", "/advertisement/", "/banner/", "/sponsored/",
            "/tracking/", "/tracker/", "/pixel/", "/beacon/",
            "doubleclick", "pagead", "adserv", "adclick",
            "analytics", "telemetry", "metrics", "/stat/",
        };

        /// <summary>
        /// Registers the filter and hooks into the WebView2 request pipeline.
        /// Must be called after CoreWebView2 is initialized.
        /// </summary>
        public void Attach(CoreWebView2 coreWebView)
        {
            // "*" filter tells WebView2 to surface every request to our handler
            coreWebView.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            coreWebView.WebResourceRequested += OnWebResourceRequested;
        }

        /// <summary>Unhooks the blocker. Call when disposing or disabling permanently.</summary>
        public void Detach(CoreWebView2 coreWebView)
        {
            coreWebView.WebResourceRequested -= OnWebResourceRequested;
        }

        /// <summary>
        /// Called for every outgoing request. Returns a 204 No Content
        /// response for blocked URLs so the page does not error out.
        /// </summary>
        private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            if (!_enabled) return;

            if (ShouldBlock(e.Request.Uri))
            {
                // 204 No Content is the cleanest way to silently drop a request
                e.Response = (sender as CoreWebView2)?.Environment
                    .CreateWebResourceResponse(null, 204, "No Content", string.Empty);
                _blockedCount++;
                OnBlockedCountChanged?.Invoke(_blockedCount);
            }
        }

        /// <summary>
        /// Returns true if the URI matches a known ad domain or URL pattern.
        /// Two-pass check: domain list first (fast), then pattern list (slower).
        /// </summary>
        private bool ShouldBlock(string uri)
        {
            if (string.IsNullOrEmpty(uri)) return false;

            foreach (var domain in BlockedDomains)
                if (uri.Contains(domain, StringComparison.OrdinalIgnoreCase)) return true;

            foreach (var pattern in BlockedPatterns)
                if (uri.Contains(pattern, StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }

        /// <summary>Flip the enabled state. Blocked count is preserved across toggles.</summary>
        public void Toggle() => _enabled = !_enabled;

        /// <summary>Reset the blocked counter to zero (e.g. on new tab).</summary>
        public void ResetCount()
        {
            _blockedCount = 0;
            OnBlockedCountChanged?.Invoke(_blockedCount);
        }
    }
}
