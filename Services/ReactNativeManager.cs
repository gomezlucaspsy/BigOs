using Microsoft.Web.WebView2.Core;

namespace UnixBrowser.Services
{
    public class ReactNativeManager
    {
        private readonly CoreWebView2 _coreWebView;

        public ReactNativeManager(CoreWebView2 coreWebView)
        {
            _coreWebView = coreWebView;
        }

        public async Task EnableReactNativeSupport()
        {
            string reactNativeScript = @"
                // Expose UnixBrowser API to React Native apps
                window.unixBrowser = {
                    version: '1.0.0',
                    platform: 'windows',
                    isStandalone: false,
                    supportsNotifications: true,
                    supportsPWA: true,
                    
                    // Check if running as PWA
                    checkStandalone: () => {
                        return window.navigator.standalone === true;
                    },
                    
                    // Request notification permission
                    requestNotificationPermission: async () => {
                        if ('Notification' in window) {
                            const permission = await Notification.requestPermission();
                            return permission === 'granted';
                        }
                        return false;
                    },
                    
                    // Get device info
                    getDeviceInfo: () => ({
                        userAgent: navigator.userAgent,
                        platform: navigator.platform,
                        language: navigator.language,
                        hardwareConcurrency: navigator.hardwareConcurrency,
                        deviceMemory: navigator.deviceMemory || 'unknown'
                    }),
                    
                    // Show install prompt
                    promptInstall: async () => {
                        if (window.installApp) {
                            window.installApp();
                        }
                    },
                    
                    notifyPWAInstallReady: () => {
                        console.log('PWA install prompt ready');
                    },
                    
                    notifyAppInstalled: () => {
                        console.log('App installed as PWA');
                    }
                };

                // Set viewport for mobile web apps
                if (!document.querySelector('meta[name=""viewport""]')) {
                    const viewport = document.createElement('meta');
                    viewport.name = 'viewport';
                    viewport.content = 'width=device-width, initial-scale=1.0, viewport-fit=cover';
                    document.head.appendChild(viewport);
                }

                // Detect React Native web environment
                window.__ReactNativeWebEnvironment__ = {
                    isUnixBrowser: true,
                    browserVersion: '1.0.0'
                };

                // Support for React Native's Platform.select
                Object.defineProperty(window.navigator, 'browserVersion', {
                    value: '1.0.0',
                    writable: false
                });
            ";

            try
            {
                await _coreWebView.ExecuteScriptAsync(reactNativeScript);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"React Native support setup error: {ex.Message}");
            }
        }

        public async Task<dynamic?> GetDeviceInfo()
        {
            try
            {
                var result = await _coreWebView.ExecuteScriptAsync(
                    @"JSON.stringify(window.unixBrowser?.getDeviceInfo?.() || {})"
                );
                return result;
            }
            catch
            {
                return null;
            }
        }
    }
}
