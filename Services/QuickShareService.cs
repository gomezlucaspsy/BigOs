using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace UnixBrowser.Services
{
    /// <summary>
    /// Quick Share - Pure peer-to-peer file/URL sharing over local network
    /// Philosophy: No corporate overhead, no Samsung/Google accounts needed
    /// Just raw bytes over the wire, like the old days
    /// </summary>
    public class QuickShareService : IDisposable
    {
        private const int Port = 9731;
        private HttpListener? _listener;
        private CancellationTokenSource? _cts;
        private bool _isReceiving = false;

        public bool IsReceiving => _isReceiving;
        public string LocalIP => GetLocalIP();

        public event Action<SharedItem>? OnItemReceived;
        public event Action<string>? OnStatusChanged;

        // ── Send ─────────────────────────────────────────────────────────────

        public async Task<bool> ShareUrl(string targetIp, string url, string title = "")
        {
            return await SendItem(targetIp, new SharedItem
            {
                Type = ShareType.Url,
                Content = url,
                Title = string.IsNullOrEmpty(title) ? url : title,
                SenderIP = LocalIP,
                Timestamp = DateTime.Now
            });
        }

        public async Task<bool> ShareFile(string targetIp, string filePath)
        {
            if (!File.Exists(filePath)) return false;

            var bytes = await File.ReadAllBytesAsync(filePath);
            return await SendItem(targetIp, new SharedItem
            {
                Type = ShareType.File,
                Content = Convert.ToBase64String(bytes),
                Title = Path.GetFileName(filePath),
                SenderIP = LocalIP,
                Timestamp = DateTime.Now
            });
        }

        private async Task<bool> SendItem(string targetIp, SharedItem item)
        {
            try
            {
                var json = JsonSerializer.Serialize(item);
                var data = Encoding.UTF8.GetBytes(json);

                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"http://{targetIp}:{Port}/receive", content);

                OnStatusChanged?.Invoke(response.IsSuccessStatusCode
                    ? $"✓ Shared to {targetIp}"
                    : $"✗ Failed to share to {targetIp}");

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                OnStatusChanged?.Invoke($"✗ Error: {ex.Message}");
                return false;
            }
        }

        // ── Receive ───────────────────────────────────────────────────────────

        public void StartReceiving()
        {
            if (_isReceiving) return;

            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{Port}/");

            try
            {
                _listener.Start();
                _isReceiving = true;
                OnStatusChanged?.Invoke($"📡 Listening on {LocalIP}:{Port}");

                Task.Run(() => ListenLoop(_cts.Token));
            }
            catch (Exception ex)
            {
                OnStatusChanged?.Invoke($"✗ Cannot start receiver: {ex.Message}");
            }
        }

        public void StopReceiving()
        {
            _cts?.Cancel();
            _listener?.Stop();
            _isReceiving = false;
            OnStatusChanged?.Invoke("Receiver stopped");
        }

        private async Task ListenLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener!.GetContextAsync();
                    _ = HandleRequest(context);
                }
                catch { break; }
            }
        }

        private async Task HandleRequest(HttpListenerContext context)
        {
            try
            {
                using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                var json = await reader.ReadToEndAsync();
                var item = JsonSerializer.Deserialize<SharedItem>(json);

                if (item != null)
                {
                    OnItemReceived?.Invoke(item);
                    System.Diagnostics.Debug.WriteLine($"[QuickShare] Received from {item.SenderIP}: {item.Title}");
                }

                context.Response.StatusCode = 200;
                context.Response.Close();
            }
            catch
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
            }
        }

        // ── Discover ──────────────────────────────────────────────────────────

        public async Task<List<string>> DiscoverDevices()
        {
            var subnet = GetSubnet();
            var found = new List<string>();
            var tasks = new List<Task>();

            for (int i = 1; i <= 254; i++)
            {
                var ip = $"{subnet}.{i}";
                if (ip == LocalIP) continue;

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        using var ping = new Ping();
                        var reply = await ping.SendPingAsync(ip, 200);
                        if (reply.Status == IPStatus.Success)
                        {
                            using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(500) };
                            var response = await client.GetAsync($"http://{ip}:{Port}/ping");
                            if (response.IsSuccessStatusCode)
                                lock (found) { found.Add(ip); }
                        }
                    }
                    catch { }
                }));
            }

            await Task.WhenAll(tasks);
            return found;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private string GetLocalIP()
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(ip.Address))
                        return ip.Address.ToString();
                }
            }
            return "127.0.0.1";
        }

        private string GetSubnet()
        {
            var ip = GetLocalIP();
            var parts = ip.Split('.');
            return $"{parts[0]}.{parts[1]}.{parts[2]}";
        }

        public void Dispose()
        {
            StopReceiving();
            _cts?.Dispose();
        }
    }

    public class SharedItem
    {
        public ShareType Type { get; set; }
        public string Content { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string SenderIP { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public enum ShareType
    {
        Url,
        File,
        Text
    }
}
