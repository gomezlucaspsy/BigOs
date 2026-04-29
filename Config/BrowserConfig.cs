/* Unix Browser - Configuration */
namespace UnixBrowser.Config
{
    public static class BrowserConfig
    {
        // PWA Configuration
        public const bool EnablePWASupport = true;
        public const bool EnableServiceWorker = true;
        public const bool EnableOfflineMode = true;

        // React Native Support
        public const bool EnableReactNativeDetection = true;

        // Performance Settings
        public const int CacheSize = 100_000_000; // 100MB
        public const bool EnableHardwareAcceleration = true;
        public const bool EnableJIT = true;

        // UI Theme
        public const string ThemeBackground = "#1a1a1a";
        public const string ThemeForeground = "#e0e0e0";
        public const string ThemeAccent = "#00ff00";
        public const string ThemeBorder = "#333333";

        // User Agent for React Native detection
        public static string GetUserAgent() => 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) UnixBrowser/1.0 (PWA-Capable; ReactNative-Compatible)";
    }

    /// <summary>
    /// Minimal "Beast" version - simplified browser with core features only
    /// Like a basic TODO app with just Create/Read/Update/Delete operations
    /// Disables: PWA, React Native, Service Workers, Offline Mode
    /// </summary>
    public static class BeastConfig
    {
        // Disable advanced features
        public const bool EnablePWASupport = false;
        public const bool EnableServiceWorker = false;
        public const bool EnableOfflineMode = false;
        public const bool EnableReactNativeDetection = false;

        // Minimal performance settings
        public const int CacheSize = 50_000_000; // 50MB (half the standard)
        public const bool EnableHardwareAcceleration = false;
        public const bool EnableJIT = false;

        // Core features only
        public const bool EnableBasicNavigation = true;  // Forward/Back/Refresh
        public const bool EnableTabs = true;             // Create/Close tabs
        public const bool EnableFavorites = true;        // Add/Remove favorites
        public const bool EnableHistory = false;         // Disabled

        // UI Theme (same as standard)
        public const string ThemeBackground = "#1a1a1a";
        public const string ThemeForeground = "#e0e0e0";
        public const string ThemeAccent = "#00ff00";
        public const string ThemeBorder = "#333333";

        // Minimal user agent
        public static string GetUserAgent() => 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) UnixBrowser/1.0-Beast";
    }
}
