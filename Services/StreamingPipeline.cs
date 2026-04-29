using Microsoft.Web.WebView2.Core;

namespace UnixBrowser.Services
{
    /// <summary>
    /// Streaming Pipeline - real-time data stream from the browser engine
    /// Inspired by Apollo 11: every byte of telemetry matters
    /// Feng Shi: clean, one-directional data flow
    /// </summary>
    public class StreamingPipeline
    {
        private bool _isStreaming = false;

        public bool IsStreaming => _isStreaming;

        // Stream events
        public event Action<StreamEvent>? OnStreamEvent;
        public event Action<int>? OnProgressChanged;    // 0-100
        public event Action<bool>? OnStreamingChanged;  // started/stopped
        public event Action<WebAssemblyInfo>? OnWebAssemblyDetected;

        public void Attach(CoreWebView2 coreWebView)
        {
            coreWebView.NavigationStarting   += OnNavigationStarting;
            coreWebView.NavigationCompleted  += OnNavigationCompleted;
            coreWebView.WebResourceRequested += OnWebResourceRequested;
            coreWebView.AddWebResourceRequestedFilter("*.wasm", CoreWebView2WebResourceContext.All);
        }

        private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            _isStreaming = true;
            OnStreamingChanged?.Invoke(true);
            OnProgressChanged?.Invoke(10);
            Emit(StreamEventType.NavigationStarted, e.Uri);
        }

        private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            OnProgressChanged?.Invoke(100);
            Emit(StreamEventType.NavigationCompleted, e.IsSuccess ? "ok" : "failed");

            if (e.IsSuccess && sender is CoreWebView2 core)
            {
                // Detect WebAssembly usage on the page
                var wasmInfo = await DetectWebAssembly(core);
                if (wasmInfo.IsPresent)
                    OnWebAssemblyDetected?.Invoke(wasmInfo);
            }

            await Task.Delay(400);
            OnProgressChanged?.Invoke(0);
            _isStreaming = false;
            OnStreamingChanged?.Invoke(false);
        }

        private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            var uri = e.Request.Uri;

            if (uri.EndsWith(".wasm", StringComparison.OrdinalIgnoreCase))
                Emit(StreamEventType.WebAssemblyResource, uri);
            else if (uri.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                Emit(StreamEventType.ScriptResource, uri);

            // Simulate streaming progress for heavy resources
            var estimated = EstimateProgress(uri);
            if (estimated > 0)
                OnProgressChanged?.Invoke(estimated);
        }

        private async Task<WebAssemblyInfo> DetectWebAssembly(CoreWebView2 core)
        {
            try
            {
                var result = await core.ExecuteScriptAsync(@"
                    JSON.stringify({
                        hasWasm: typeof WebAssembly !== 'undefined',
                        hasStreaming: typeof WebAssembly.instantiateStreaming !== 'undefined',
                        isWasmSite: document.querySelector('script[src*="".wasm""]') !== null ||
                                    performance.getEntriesByType('resource')
                                        .some(r => r.name.includes('.wasm'))
                    })");

                var clean = result.Trim('"').Replace("\\\"", "\"");
                var hasWasm = clean.Contains("\"hasWasm\":true");
                var hasStreaming = clean.Contains("\"hasStreaming\":true");
                var isWasmSite = clean.Contains("\"isWasmSite\":true");

                return new WebAssemblyInfo
                {
                    IsPresent = hasWasm,
                    HasStreaming = hasStreaming,
                    IsWasmSite = isWasmSite
                };
            }
            catch
            {
                return new WebAssemblyInfo();
            }
        }

        private int EstimateProgress(string uri)
        {
            if (uri.Contains(".wasm"))  return 60;
            if (uri.Contains(".js"))    return 40;
            if (uri.Contains(".css"))   return 30;
            if (uri.Contains(".png") || uri.Contains(".jpg")) return 50;
            return 0;
        }

        private void Emit(StreamEventType type, string data)
        {
            OnStreamEvent?.Invoke(new StreamEvent
            {
                Type = type,
                Data = data,
                Timestamp = DateTime.Now
            });
        }
    }

    public class StreamEvent
    {
        public StreamEventType Type { get; set; }
        public string Data { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public enum StreamEventType
    {
        NavigationStarted,
        NavigationCompleted,
        WebAssemblyResource,
        ScriptResource
    }

    public class WebAssemblyInfo
    {
        public bool IsPresent { get; set; }
        public bool HasStreaming { get; set; }
        public bool IsWasmSite { get; set; }
    }
}
