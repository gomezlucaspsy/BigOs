using Microsoft.Web.WebView2.Core;

namespace UnixBrowser.Services
{
    /// <summary>
    /// Ad blocker - blocks ad/tracker requests at the byte level before they load
    /// Philosophy: Pure 0s and 1s, no corporate noise injected in between
    /// </summary>
    public class AdBlocker
    {
        private bool _enabled = true;
        private int _blockedCount = 0;

        public bool IsEnabled => _enabled;
        public int BlockedCount => _blockedCount;

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

        public void Attach(CoreWebView2 coreWebView)
        {
            coreWebView.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            coreWebView.WebResourceRequested += OnWebResourceRequested;
        }

        public void Detach(CoreWebView2 coreWebView)
        {
            coreWebView.WebResourceRequested -= OnWebResourceRequested;
        }

        private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            if (!_enabled) return;

            var uri = e.Request.Uri;
            if (ShouldBlock(uri))
            {
                e.Response = (sender as CoreWebView2)?.Environment.CreateWebResourceResponse(
                    null, 204, "No Content", string.Empty);
                _blockedCount++;
                OnBlockedCountChanged?.Invoke(_blockedCount);
                System.Diagnostics.Debug.WriteLine($"[AdBlocker] Blocked: {uri}");
            }
        }

        private bool ShouldBlock(string uri)
        {
            if (string.IsNullOrEmpty(uri)) return false;

            // Check blocked domains
            foreach (var domain in BlockedDomains)
            {
                if (uri.Contains(domain, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // Check blocked patterns
            foreach (var pattern in BlockedPatterns)
            {
                if (uri.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public void Toggle()
        {
            _enabled = !_enabled;
        }

        public void ResetCount()
        {
            _blockedCount = 0;
            OnBlockedCountChanged?.Invoke(_blockedCount);
        }
    }
}
