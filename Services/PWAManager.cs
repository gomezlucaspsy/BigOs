using Microsoft.Web.WebView2.Core;
using System.Diagnostics;

namespace UnixBrowser.Services
{
    public class PWAManager
    {
        private readonly CoreWebView2 _coreWebView;

        public PWAManager(CoreWebView2 coreWebView)
        {
            _coreWebView = coreWebView;
        }

        public async Task EnablePWAFeatures()
        {
            string pwaScript = @"
                // Install prompt event
                let deferredPrompt;
                window.addEventListener('beforeinstallprompt', (e) => {
                    e.preventDefault();
                    deferredPrompt = e;
                    window.unixBrowser?.notifyPWAInstallReady();
                });

                // App installed event
                window.addEventListener('appinstalled', () => {
                    console.log('PWA installed successfully');
                    window.unixBrowser?.notifyAppInstalled();
                });

                // Standalone mode detection
                if (window.navigator.standalone === true) {
                    console.log('App running in standalone mode');
                }

                // Service Worker registration
                if ('serviceWorker' in navigator) {
                    navigator.serviceWorker.register('/sw.js', { scope: '/' })
                        .then(reg => console.log('Service Worker registered'))
                        .catch(err => console.log('Service Worker registration failed:', err));
                }

                // Expose install function
                window.installApp = () => {
                    if (deferredPrompt) {
                        deferredPrompt.prompt();
                        deferredPrompt.userChoice.then(choiceResult => {
                            if (choiceResult.outcome === 'accepted') {
                                console.log('User accepted the install prompt');
                            }
                            deferredPrompt = null;
                        });
                    }
                };
            ";

            try
            {
                await _coreWebView.ExecuteScriptAsync(pwaScript);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PWA setup error: {ex.Message}");
            }
        }

        public async Task<bool> CheckServiceWorkerStatus()
        {
            try
            {
                string script = @"
                    (async () => {
                        if ('serviceWorker' in navigator) {
                            const registrations = await navigator.serviceWorker.getRegistrations();
                            return registrations.length > 0;
                        }
                        return false;
                    })();
                ";
                
                var result = await _coreWebView.ExecuteScriptAsync(script);
                return result?.Contains("true") ?? false;
            }
            catch
            {
                return false;
            }
        }
    }
}
