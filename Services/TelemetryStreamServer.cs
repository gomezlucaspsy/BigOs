using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Diagnostics;

namespace UnixBrowser.Services
{
    public class TelemetryStreamServer : IDisposable
    {
        private const int PreferredPort = 9732;
        private int _actualPort = PreferredPort;
        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private bool _isRunning = false;
        private readonly DateTime _startTime = DateTime.Now;

        public bool IsRunning => _isRunning;
        public string LocalIP => GetLocalIP();
        public string StreamUrl => $"http://{GetLocalIP()}:{_actualPort}";

        public event Action<string>? OnStatusChanged;

        public async Task StartAsync()
        {
            if (_isRunning) return;
            try
            {
                _actualPort = FindFreePort(PreferredPort);
                _listener = new TcpListener(IPAddress.Any, _actualPort);
                _listener.Start();
                _isRunning = true;
                OnStatusChanged?.Invoke($"Telemetry online: {StreamUrl}");
                _cts = new CancellationTokenSource();
                _ = ListenAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                OnStatusChanged?.Invoke($"Error: {ex.Message}");
            }
        }

        private async Task ListenAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _isRunning)
            {
                try
                {
                    var client = await _listener!.AcceptTcpClientAsync(ct);
                    _ = HandleClientAsync(client);
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch { }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using (client)
                {
                    var stream = client.GetStream();
                    var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

                    var requestLine = await reader.ReadLineAsync() ?? "";
                    var parts = requestLine.Split(' ');
                    var rawPath = parts.Length > 1 ? parts[1] : "/";
                    var path = rawPath.Contains('?') ? rawPath.Split('?')[0] : rawPath;

                    // drain headers
                    string? hdr;
                    while (!string.IsNullOrEmpty(hdr = await reader.ReadLineAsync())) { }

                    string body; string ct2 = "text/html; charset=utf-8"; int status = 200;

                    if (path == "/" || path == "/index.html")
                    {
                        body = "<html><body style='background:#0d0d0d;color:#00ff00;font-family:Courier New,monospace;padding:20px'>"
                             + "<h1>⚡ Telemetry Server</h1>"
                             + "<div style='border:1px solid #00ff00;padding:10px;margin:10px 0'>"
                             + "<div>CPU: <span id='cpu'>--</span>%</div>"
                             + "<div>Memory: <span id='mem'>--</span> MB</div>"
                             + "<div>Uptime: <span id='up'>--</span> s</div>"
                             + "<div style='margin-top:8px'>Endpoints: /api/metrics &nbsp; /system/info</div></div>"
                             + "<script>setInterval(()=>fetch('/api/metrics').then(r=>r.json()).then(d=>{document.getElementById('cpu').textContent=(d.cpu||0).toFixed(1);document.getElementById('mem').textContent=(d.memoryMB||0).toFixed(0);document.getElementById('up').textContent=(d.uptime||0).toFixed(0);}),1000);</script>"
                             + "</body></html>";
                    }
                    else if (path == "/api/metrics" || path == "/metrics")
                    {
                        body = JsonSerializer.Serialize(new { cpu = 0.0, memoryMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0), uptime = (DateTime.Now - _startTime).TotalSeconds });
                        ct2 = "application/json";
                    }
                    else if (path == "/system/info")
                    {
                        body = JsonSerializer.Serialize(new { os = Environment.OSVersion.ToString(), cores = Environment.ProcessorCount, runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription });
                        ct2 = "application/json";
                    }
                    else
                    {
                        status = 404; body = "404 Not Found"; ct2 = "text/plain";
                    }

                    var bodyBytes = Encoding.UTF8.GetBytes(body);
                    var header = $"HTTP/1.1 {status} OK\r\nContent-Type: {ct2}\r\nContent-Length: {bodyBytes.Length}\r\nAccess-Control-Allow-Origin: *\r\nConnection: close\r\n\r\n";
                    var headerBytes = Encoding.UTF8.GetBytes(header);
                    await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
                    await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length);
                }
            }
            catch { }
        }

        public async Task StopAsync()
        {
            _isRunning = false;
            _cts?.Cancel();
            _listener?.Stop();
            await Task.Delay(100);
            OnStatusChanged?.Invoke("✓ Telemetry stopped");
        }

        private static int FindFreePort(int preferred)
        {
            for (int port = preferred; port < preferred + 100; port++)
            {
                try { var t = new TcpListener(IPAddress.Any, port); t.Start(); t.Stop(); return port; }
                catch { }
            }
            var tmp = new TcpListener(IPAddress.Any, 0); tmp.Start();
            int p = ((System.Net.IPEndPoint)tmp.LocalEndpoint).Port; tmp.Stop(); return p;
        }

        private string GetLocalIP()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65432);
                return (socket.LocalEndPoint as IPEndPoint)?.Address.ToString() ?? "127.0.0.1";
            }
            catch { return "127.0.0.1"; }
        }

        public void Dispose()
        {
            StopAsync().Wait(5000);
            _listener?.Stop();
            _cts?.Dispose();
        }
    }

    public class TelemetryData
    {
        public DateTime Timestamp { get; set; }
        public double Uptime { get; set; }
        public double CPU { get; set; }
        public double MemoryMB { get; set; }
    }
}
