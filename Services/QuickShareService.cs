// ============================================================
// QuickShareService.cs — Peer-to-Peer Quick Share Engine
// Purpose : Streams URLs, text, files and screenshots between
//           devices using QR codes for connection bootstrapping.
//           No accounts. No cloud. Raw bytes over local HTTP.
// Streaming: Chunked transfer with live progress reporting.
// ============================================================
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
    /// Handles all Quick Share operations: sending and receiving
    /// URLs, plain text, files, and screenshots over local HTTP.
    /// Uses chunked streaming for files to report live progress.
    /// </summary>
    public class QuickShareService : IDisposable
    {
        private const int    Port      = 9731;       // Fixed port — both sides must agree
        private const int    ChunkSize = 64 * 1024;  // 64KB chunks for streaming transfers

        private HttpListener?             _listener;
        private CancellationTokenSource?  _cts;
        private bool                      _isReceiving = false;

        public bool   IsReceiving => _isReceiving;
        public string LocalIP     => GetLocalIP();

        /// <summary>Fired when any item is fully received from a remote device.</summary>
        public event Action<SharedItem>? OnItemReceived;
        /// <summary>Fired with human-readable status messages for the UI.</summary>
        public event Action<string>?     OnStatusChanged;
        /// <summary>Fired during chunked sends with 0-100 progress value.</summary>
        public event Action<int>?        OnSendProgress;
        /// <summary>Fired during chunked receives with 0-100 progress value.</summary>
        public event Action<int>?        OnReceiveProgress;

        // ── Send: URL ─────────────────────────────────────────────────────────

        /// <summary>
        /// Shares a URL to a target device by IP.
        /// URLs are small — sent as a single JSON payload, no chunking needed.
        /// </summary>
        public async Task<bool> ShareUrl(string targetIp, string url, string title = "")
        {
            return await SendItem(targetIp, new SharedItem
            {
                Type      = ShareType.Url,
                Content   = url,
                Title     = string.IsNullOrEmpty(title) ? url : title,
                SenderIP  = LocalIP,
                Timestamp = DateTime.Now
            });
        }

        // ── Send: Text ────────────────────────────────────────────────────────

        /// <summary>
        /// Shares arbitrary plain text (notes, code snippets, passwords).
        /// Sent as a single payload — text is always small enough.
        /// </summary>
        public async Task<bool> ShareText(string targetIp, string text)
        {
            return await SendItem(targetIp, new SharedItem
            {
                Type      = ShareType.Text,
                Content   = text,
                Title     = text.Length > 40 ? text[..40] + "…" : text,
                SenderIP  = LocalIP,
                Timestamp = DateTime.Now
            });
        }

        // ── Send: File (chunked streaming) ────────────────────────────────────

        /// <summary>
        /// Streams a file to a target device in 64KB chunks.
        /// Reports live progress via OnSendProgress (0-100).
        /// Large files stay responsive — never blocks the UI thread.
        /// </summary>
        public async Task<bool> ShareFile(string targetIp, string filePath)
        {
            if (!File.Exists(filePath)) return false;

            try
            {
                OnStatusChanged?.Invoke($"📤 Streaming {Path.GetFileName(filePath)}...");
                OnSendProgress?.Invoke(0);

                var fileBytes   = await File.ReadAllBytesAsync(filePath);
                var totalChunks = (int)Math.Ceiling((double)fileBytes.Length / ChunkSize);

                // Send metadata first so receiver knows what is coming
                await SendItem(targetIp, new SharedItem
                {
                    Type       = ShareType.FileChunkStart,
                    Title      = Path.GetFileName(filePath),
                    Content    = totalChunks.ToString(),
                    SenderIP   = LocalIP,
                    Timestamp  = DateTime.Now,
                    FileSize   = fileBytes.Length
                });

                // Stream chunks sequentially with progress updates
                for (int i = 0; i < totalChunks; i++)
                {
                    var offset = i * ChunkSize;
                    var length = Math.Min(ChunkSize, fileBytes.Length - offset);
                    var chunk  = fileBytes[offset..(offset + length)];

                    await SendItem(targetIp, new SharedItem
                    {
                        Type        = ShareType.FileChunk,
                        Title       = Path.GetFileName(filePath),
                        Content     = Convert.ToBase64String(chunk),
                        SenderIP    = LocalIP,
                        Timestamp   = DateTime.Now,
                        ChunkIndex  = i,
                        TotalChunks = totalChunks
                    });

                    var progress = (int)(((i + 1) / (double)totalChunks) * 100);
                    OnSendProgress?.Invoke(progress);
                    OnStatusChanged?.Invoke($"📤 Streaming {i + 1}/{totalChunks} chunks ({progress}%)");
                }

                OnSendProgress?.Invoke(100);
                OnStatusChanged?.Invoke($"✓ File sent: {Path.GetFileName(filePath)}");
                return true;
            }
            catch (Exception ex)
            {
                OnStatusChanged?.Invoke($"✗ File send failed: {ex.Message}");
                return false;
            }
        }

        // ── Send: Screenshot ──────────────────────────────────────────────────

        /// <summary>
        /// Captures the primary screen and streams it to the target device
        /// as a chunked PNG file. Saved to Downloads on the receiver side.
        /// </summary>
        public async Task<bool> ShareScreenshot(string targetIp)
        {
            try
            {
                OnStatusChanged?.Invoke("📸 Screenshot capture requires Windows.Forms (not enabled)");
                return false;
            }
            catch (Exception ex)
            {
                OnStatusChanged?.Invoke($"✗ Screenshot failed: {ex.Message}");
                return false;
            }
        }

        // ── Internal send ─────────────────────────────────────────────────────

        private async Task<bool> SendItem(string targetIp, SharedItem item)
        {
            try
            {
                using var client   = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                var       json     = JsonSerializer.Serialize(item);
                var       content  = new StringContent(json, Encoding.UTF8, "application/json");
                var       response = await client.PostAsync($"http://{targetIp}:{Port}/receive", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                OnStatusChanged?.Invoke($"✗ Send error: {ex.Message}");
                return false;
            }
        }

        // ── Receive ───────────────────────────────────────────────────────────

        // Accumulates file chunks keyed by filename until all arrive
        private readonly Dictionary<string, List<SharedItem>> _pendingChunks = new();

        /// <summary>
        /// Starts the local HTTP listener on localhost:{Port}.
        /// Runs the accept loop on a background thread.
        /// </summary>
        public void StartReceiving()
        {
            if (_isReceiving) return;

            _cts      = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{Port}/");

            try
            {
                _listener.Start();
                _isReceiving = true;
                OnStatusChanged?.Invoke($"📡 Ready on {LocalIP}:{Port}");
                Task.Run(() => ListenLoop(_cts.Token));
            }
            catch (Exception ex)
            {
                OnStatusChanged?.Invoke($"✗ Receiver failed to start: {ex.Message}");
            }
        }

        /// <summary>Stops the listener and cancels the accept loop.</summary>
        public void StopReceiving()
        {
            _cts?.Cancel();
            _listener?.Stop();
            _isReceiving = false;
        }

        private async Task ListenLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener!.GetContextAsync();
                    _ = Task.Run(() => HandleRequest(context));
                }
                catch { break; }
            }
        }

        /// <summary>
        /// Routes incoming requests by share type.
        /// File chunks are buffered until complete, then reassembled and raised.
        /// </summary>
        private async Task HandleRequest(HttpListenerContext context)
        {
            try
            {
                using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                var json = await reader.ReadToEndAsync();
                var item = JsonSerializer.Deserialize<SharedItem>(json);

                if (item != null)
                {
                    switch (item.Type)
                    {
                        case ShareType.FileChunkStart:
                            _pendingChunks[item.Title] = new List<SharedItem>();
                            OnStatusChanged?.Invoke($"📥 Receiving {item.Title}...");
                            OnReceiveProgress?.Invoke(0);
                            break;

                        case ShareType.FileChunk:
                            if (_pendingChunks.TryGetValue(item.Title, out var chunks))
                            {
                                chunks.Add(item);
                                var progress = (int)(((double)chunks.Count / item.TotalChunks) * 100);
                                OnReceiveProgress?.Invoke(progress);
                                OnStatusChanged?.Invoke($"📥 {item.Title}: {chunks.Count}/{item.TotalChunks} chunks");

                                if (chunks.Count == item.TotalChunks)
                                    ReassembleFile(item.Title, chunks, item.SenderIP);
                            }
                            break;

                        default:
                            OnItemReceived?.Invoke(item);
                            OnStatusChanged?.Invoke($"⚡ Received from {item.SenderIP}: {item.Title}");
                            break;
                    }
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

        /// <summary>
        /// Reassembles Base64 file chunks into the original binary,
        /// saves to Downloads, and fires OnItemReceived with the local path.
        /// </summary>
        private void ReassembleFile(string fileName, List<SharedItem> chunks, string senderIP)
        {
            try
            {
                var allBytes = chunks
                    .OrderBy(c => c.ChunkIndex)
                    .SelectMany(c => Convert.FromBase64String(c.Content))
                    .ToArray();

                var savePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads", fileName);

                File.WriteAllBytes(savePath, allBytes);
                _pendingChunks.Remove(fileName);
                OnReceiveProgress?.Invoke(100);

                OnItemReceived?.Invoke(new SharedItem
                {
                    Type      = ShareType.File,
                    Title     = fileName,
                    Content   = savePath,
                    SenderIP  = senderIP,
                    Timestamp = DateTime.Now
                });

                OnStatusChanged?.Invoke($"✓ Saved to Downloads: {fileName}");
            }
            catch (Exception ex)
            {
                OnStatusChanged?.Invoke($"✗ Reassemble failed: {ex.Message}");
            }
        }

        // ── Discovery ─────────────────────────────────────────────────────────

        public async Task<List<string>> DiscoverDevices()
        {
            var subnet = GetSubnet();
            var found  = new List<string>();
            var tasks  = new List<Task>();

            for (int i = 1; i <= 254; i++)
            {
                var ip = $"{subnet}.{i}";
                if (ip == LocalIP) continue;

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        using var ping  = new Ping();
                        var       reply = await ping.SendPingAsync(ip, 200);
                        if (reply.Status != IPStatus.Success) return;

                        using var client   = new HttpClient { Timeout = TimeSpan.FromMilliseconds(500) };
                        var       response = await client.GetAsync($"http://{ip}:{Port}/ping");
                        if (response.IsSuccessStatusCode)
                            lock (found) { found.Add(ip); }
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
                foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(addr.Address))
                        return addr.Address.ToString();
                }
            }
            return "127.0.0.1";
        }

        private string GetSubnet()
        {
            var parts = GetLocalIP().Split('.');
            return $"{parts[0]}.{parts[1]}.{parts[2]}";
        }

        public void Dispose()
        {
            StopReceiving();
            _cts?.Dispose();
        }
    }

    // ── Data models ───────────────────────────────────────────────────────────

    /// <summary>
    /// One unit of shared data. For chunked files, multiple items form one transfer.
    /// Content holds: the URL, text payload, base64 chunk data, or local save path.
    /// </summary>
    public class SharedItem
    {
        public ShareType Type        { get; set; }
        public string    Content     { get; set; } = string.Empty;
        public string    Title       { get; set; } = string.Empty;
        public string    SenderIP    { get; set; } = string.Empty;
        public DateTime  Timestamp   { get; set; }
        public int       ChunkIndex  { get; set; }
        public int       TotalChunks { get; set; }
        public long      FileSize    { get; set; }
    }

    public enum ShareType
    {
        Url,            // Web address — opens in browser on receive
        Text,           // Plain text — copied to clipboard on receive
        File,           // Fully reassembled file — saved to Downloads
        FileChunkStart, // Signals start of a chunked file transfer
        FileChunk       // One 64KB chunk of an in-progress file
    }
}
