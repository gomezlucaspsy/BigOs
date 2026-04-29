// ============================================================
// QuickShareDialog.cs — Quick Share UI
// Purpose : Presents four sharing modes (URL, Text, File,
//           Screenshot) each backed by a QR code for connection
//           bootstrapping and a live progress bar for streaming.
// ============================================================
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfButton      = System.Windows.Controls.Button;
using WpfTextBox     = System.Windows.Controls.TextBox;
using WpfProgressBar = System.Windows.Controls.ProgressBar;
using QRCoder;
using UnixBrowser.Services;

namespace UnixBrowser
{
    /// <summary>
    /// Modal dialog that exposes all four Quick Share modes.
    /// The QR code always encodes the target IP:port so the
    /// receiving device can discover what to connect to, while
    /// the actual payload (URL / text / file / screenshot)
    /// streams over the local HTTP channel independently.
    /// </summary>
    public class QuickShareDialog : Window
    {
        private readonly BrowserEngine _engine;
        private LocalhostStreamServer? _streamServer;
        private TelemetryStreamServer? _telemetryServer;

        // UI refs updated across tabs
        private System.Windows.Controls.Image _qrImage       = new();
        private TextBlock      _qrLabel      = new();
        private TextBlock      _statusLabel  = new();
        private WpfProgressBar _progressBar  = new();
        private WpfTextBox     _targetIpBox  = new();
        private WpfTextBox     _textInputBox = new();

        public QuickShareDialog(BrowserEngine engine)
        {
            _engine = engine;
            Title   = "⚡ Quick Share";
            Width   = 500;
            Height  = 640;
            MinWidth  = 460;
            MinHeight = 520;
            ResizeMode              = ResizeMode.CanResize;
            WindowStartupLocation   = WindowStartupLocation.CenterOwner;
            Background              = new SolidColorBrush(Color.FromRgb(0x1a, 0x1a, 0x1a));
            FontFamily              = new FontFamily("Courier New");

            BuildUI();
            WireServiceEvents();
        }

        // ── Layout ────────────────────────────────────────────────────────────

        private void BuildUI()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // header
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // IP row
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // QR
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // QR label
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // tabs
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // tab content
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // progress
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // status
            root.Margin = new Thickness(16);

            // Header
            var header = new TextBlock
            {
                Text       = "⚡ Quick Share",
                Foreground = Brush("#00ff00"),
                FontSize   = 15,
                FontWeight = FontWeights.Bold,
                Margin     = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // Target IP row
            var ipRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            ipRow.Children.Add(new TextBlock { Text = "Target IP: ", Foreground = Brush("#888888"), VerticalAlignment = VerticalAlignment.Center });
            _targetIpBox = new WpfTextBox
            {
                Width       = 160,
                Background  = Brush("#111111"),
                Foreground  = Brush("#00ccff"),
                BorderBrush = Brush("#004444"),
                Text        = "",
                ToolTip     = "Enter the IP of the target device on your network"
            };
            ipRow.Children.Add(_targetIpBox);
            var discoverBtn = MakeButton("🔍 Scan", async () =>
            {
                SetStatus("🔍 Scanning network...");
                var devices = await _engine.DiscoverDevices();
                if (devices.Count > 0)
                {
                    _targetIpBox.Text = devices[0];
                    SetStatus($"✓ Found {devices.Count} device(s) — using {devices[0]}");
                }
                else
                    SetStatus("No devices found on this subnet");
            });
            discoverBtn.Margin = new Thickness(8, 0, 0, 0);
            ipRow.Children.Add(discoverBtn);
            Grid.SetRow(ipRow, 1);
            root.Children.Add(ipRow);

            // QR Code — always shows how to connect to this machine
            _qrImage = new System.Windows.Controls.Image
            {
                Width               = 160,
                Height              = 160,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(0, 0, 0, 6),
                Source              = GenerateQrCode(BuildConnectionQrPayload())
            };
            Grid.SetRow(_qrImage, 2);
            root.Children.Add(_qrImage);

            // QR label
            _qrLabel = new TextBlock
            {
                Text                = $"Scan to connect → {_engine.QuickShare.LocalIP}:{9731}",
                Foreground          = Brush("#888888"),
                FontSize            = 9,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(_qrLabel, 3);
            root.Children.Add(_qrLabel);

            // Tab buttons — wrapped in horizontal ScrollViewer so they never overflow
            var tabRow = new StackPanel { Orientation = Orientation.Horizontal };
            var tabScroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility   = ScrollBarVisibility.Disabled,
                Content = tabRow,
                Margin  = new Thickness(0, 0, 0, 6)
            };
            var tabContent = new Border
            {
                Background  = Brush("#111111"),
                BorderBrush = Brush("#003300"),
                BorderThickness = new Thickness(1),
                Padding     = new Thickness(10),
                MinHeight   = 80
            };

            void SelectTab(string name, UIElement content)
            {
                tabContent.Child = content;
                // Update QR to encode current tab payload
                if (name == "URL")  _qrImage.Source = GenerateQrCode(_engine.CurrentUrl);
                else if (name == "PWA" && _streamServer != null && _streamServer.IsRunning)
                                    _qrImage.Source = GenerateQrCode(_streamServer.StreamUrl + "/pwa");
                else                _qrImage.Source = GenerateQrCode(BuildConnectionQrPayload());
            }

            var tabs = new[] { "🌐 URL", "📝 Text", "📁 File", "📸 Screenshot", "🔗 Stream", "📊 Telemetry", "📱 PWA" };
            foreach (var tab in tabs)
            {
                var btn = MakeTabButton(tab);
                var captured = tab;
                btn.Click += (s, e) =>
                {
                    var name = captured.Split(' ')[1];
                    SelectTab(name, BuildTabContent(name));
                };
                tabRow.Children.Add(btn);
            }
            Grid.SetRow(tabScroll, 4);
            root.Children.Add(tabScroll);

            // Default tab: URL
            tabContent.Child = BuildTabContent("URL");
            _qrImage.Source  = GenerateQrCode(_engine.CurrentUrl);
            Grid.SetRow(tabContent, 5);
            root.Children.Add(tabContent);

            // Progress bar
            _progressBar = new WpfProgressBar
            {
                Height     = 8,
                Minimum    = 0,
                Maximum    = 100,
                Value      = 0,
                Foreground = Brush("#00ff00"),
                Background = Brush("#111111"),
                Margin     = new Thickness(0, 8, 0, 4)
            };
            Grid.SetRow(_progressBar, 6);
            root.Children.Add(_progressBar);

            // Status label
            _statusLabel = new TextBlock
            {
                Text       = "Ready — enter an IP or scan devices",
                Foreground = Brush("#888888"),
                FontSize   = 9,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(_statusLabel, 7);
            root.Children.Add(_statusLabel);

            Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        // ── Tab content builders ──────────────────────────────────────────────

        private UIElement BuildTabContent(string tab) => tab switch
        {
            "URL"        => BuildUrlTab(),
            "Text"       => BuildTextTab(),
            "File"       => BuildFileTab(),
            "Screenshot" => BuildScreenshotTab(),
            "Stream"     => BuildStreamTab(),
            "Telemetry"  => BuildTelemetryTab(),
            "PWA"        => BuildPwaTab(),
            _            => new TextBlock { Text = "Unknown tab", Foreground = Brush("#ff0000") }
        };

        private UIElement BuildUrlTab()
        {
            var panel = new StackPanel { };
            var url   = _engine.CurrentUrl;

            panel.Children.Add(new TextBlock
            {
                Text         = url.Length > 60 ? url[..60] + "…" : url,
                Foreground   = Brush("#00ccff"),
                FontSize     = 10,
                TextWrapping = TextWrapping.Wrap
            });

            var copyBtn = MakeButton("📋 Copy URL", () => Clipboard.SetText(url));
            var sendBtn = MakeButton("📤 Send to IP", async () =>
            {
                var ip = _targetIpBox.Text.Trim();
                if (string.IsNullOrEmpty(ip)) { SetStatus("Enter a target IP first"); return; }
                SetStatus($"📤 Sending URL to {ip}...");
                var ok = await _engine.QuickShare.ShareUrl(ip, url);
                SetStatus(ok ? $"✓ URL sent to {ip}" : $"✗ Failed to reach {ip}");
            });

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal };
            btnRow.Children.Add(copyBtn);
            btnRow.Children.Add(sendBtn);
            panel.Children.Add(btnRow);
            return panel;
        }

        private UIElement BuildTextTab()
        {
            var panel = new StackPanel { };

            _textInputBox = new WpfTextBox
            {
                Background      = Brush("#0a0a0a"),
                Foreground      = Brush("#00ff00"),
                BorderBrush     = Brush("#003300"),
                AcceptsReturn   = true,
                TextWrapping    = TextWrapping.Wrap,
                Height          = 60,
                Padding         = new Thickness(4),
                FontFamily      = new FontFamily("Courier New"),
                FontSize        = 10
            };
            panel.Children.Add(_textInputBox);

            var sendBtn = MakeButton("📤 Send Text", async () =>
            {
                var ip   = _targetIpBox.Text.Trim();
                var text = _textInputBox.Text.Trim();
                if (string.IsNullOrEmpty(ip))   { SetStatus("Enter a target IP first"); return; }
                if (string.IsNullOrEmpty(text))  { SetStatus("Enter some text to send"); return; }
                SetStatus($"📤 Sending text to {ip}...");
                var ok = await _engine.QuickShare.ShareText(ip, text);
                SetStatus(ok ? $"✓ Text sent to {ip}" : $"✗ Failed to reach {ip}");
            });
            panel.Children.Add(sendBtn);
            return panel;
        }

        private UIElement BuildFileTab()
        {
            var panel = new StackPanel { };

            var pathBox = new WpfTextBox
            {
                Background  = Brush("#0a0a0a"),
                Foreground  = Brush("#00ccff"),
                BorderBrush = Brush("#004444"),
                Padding     = new Thickness(4),
                FontSize    = 10,
                IsReadOnly  = true
            };

            var browseBtn = MakeButton("📂 Browse File", () =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Select file to share" };
                if (dlg.ShowDialog() == true)
                    pathBox.Text = dlg.FileName;
            });

            var sendBtn = MakeButton("📤 Stream File", async () =>
            {
                var ip   = _targetIpBox.Text.Trim();
                var path = pathBox.Text.Trim();
                if (string.IsNullOrEmpty(ip))   { SetStatus("Enter a target IP first"); return; }
                if (string.IsNullOrEmpty(path))  { SetStatus("Select a file first"); return; }
                SetStatus($"📤 Streaming {System.IO.Path.GetFileName(path)}...");
                _progressBar.Value = 0;
                var ok = await _engine.QuickShare.ShareFile(ip, path);
                SetStatus(ok ? "✓ File streamed successfully" : "✗ File transfer failed");
            });

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal };
            btnRow.Children.Add(browseBtn);
            btnRow.Children.Add(sendBtn);
            panel.Children.Add(pathBox);
            panel.Children.Add(btnRow);
            return panel;
        }

        private UIElement BuildScreenshotTab()
        {
            var panel = new StackPanel { };

            panel.Children.Add(new TextBlock
            {
                Text       = "Captures the primary screen and streams it as PNG",
                Foreground = Brush("#888888"),
                FontSize   = 9,
                TextWrapping = TextWrapping.Wrap
            });

            var sendBtn = MakeButton("📸 Capture & Stream", async () =>
            {
                var ip = _targetIpBox.Text.Trim();
                if (string.IsNullOrEmpty(ip)) { SetStatus("Enter a target IP first"); return; }
                SetStatus("📸 Capturing and streaming screenshot...");
                _progressBar.Value = 0;
                var ok = await _engine.QuickShare.ShareScreenshot(ip);
                SetStatus(ok ? "✓ Screenshot streamed" : "✗ Screenshot failed");
            });
            panel.Children.Add(sendBtn);
            return panel;
        }

        private UIElement BuildStreamTab()
        {
            var panel = new StackPanel { };

            var desc = new TextBlock
            {
                Text       = "Open low-level localhost streaming endpoint",
                Foreground = Brush("#888888"),
                FontSize   = 9,
                TextWrapping = TextWrapping.Wrap,
                Margin     = new Thickness(0, 0, 0, 6)
            };
            panel.Children.Add(desc);

            // Status label
            var statusLabel = new TextBlock
            {
                Text       = "Ready to start streaming",
                Foreground = Brush("#00ff00"),
                FontSize   = 10,
                Margin     = new Thickness(0, 0, 0, 10)
            };
            panel.Children.Add(statusLabel);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal };

            var startBtn = MakeButton("▶ Start Server", async () =>
            {
                if (_streamServer == null)
                {
                    _streamServer = new LocalhostStreamServer();
                    _streamServer.OnStatusChanged += msg => Dispatcher.Invoke(() => 
                    {
                        statusLabel.Text = msg;
                        SetStatus(msg);
                    });
                }

                await _streamServer.StartAsync();
                statusLabel.Foreground = Brush("#00ff00");

                // Update QR to show localhost URL
                _qrImage.Source = GenerateQrCode(_streamServer.StreamUrl);
                SetStatus($"✓ Stream server started: {_streamServer.StreamUrl}");
            });
            startBtn.Margin = new Thickness(0, 0, 4, 0);
            btnRow.Children.Add(startBtn);

            var stopBtn = MakeButton("⏹ Stop Server", async () =>
            {
                if (_streamServer != null)
                {
                    await _streamServer.StopAsync();
                    statusLabel.Foreground = Brush("#ff6b6b");
                    statusLabel.Text = "Server stopped";
                }
            });
            stopBtn.Margin = new Thickness(0, 0, 4, 0);
            btnRow.Children.Add(stopBtn);

            var openBtn = MakeButton("🌐 Open", () =>
            {
                if (_streamServer != null && _streamServer.IsRunning)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName        = _streamServer.StreamUrl,
                            UseShellExecute = true
                        });
                        SetStatus($"✓ Opened {_streamServer.StreamUrl}");
                    }
                    catch (Exception ex)
                    {
                        SetStatus($"✗ Failed to open: {ex.Message}");
                    }
                }
                else
                {
                    SetStatus("Start server first");
                }
            });
            openBtn.Margin = new Thickness(0, 0, 4, 0);
            btnRow.Children.Add(openBtn);

            var copyBtn = MakeButton("📋 Copy URL", () =>
            {
                if (_streamServer != null && _streamServer.IsRunning)
                {
                    Clipboard.SetText(_streamServer.StreamUrl);
                    SetStatus("✓ URL copied");
                }
            });
            btnRow.Children.Add(copyBtn);

            panel.Children.Add(btnRow);

            var info = new TextBlock
            {
                Text       = "Endpoints: POST /upload • GET /stream • POST /data • GET /exec",
                Foreground = Brush("#666666"),
                FontSize   = 8,
                TextWrapping = TextWrapping.Wrap,
                Margin     = new Thickness(0, 6, 0, 0)
            };
            panel.Children.Add(info);

            return panel;
        }

        private UIElement BuildTelemetryTab()
        {
            var panel = new StackPanel { };

            var desc = new TextBlock
            {
                Text       = "Advanced telemetry, metrics streaming & WebAssembly endpoint",
                Foreground = Brush("#888888"),
                FontSize   = 9,
                TextWrapping = TextWrapping.Wrap,
                Margin     = new Thickness(0, 0, 0, 6)
            };
            panel.Children.Add(desc);

            // Status label
            var statusLabel = new TextBlock
            {
                Text       = "Ready to stream telemetry",
                Foreground = Brush("#00ff00"),
                FontSize   = 10,
                Margin     = new Thickness(0, 0, 0, 10)
            };
            panel.Children.Add(statusLabel);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal };

            var startBtn = MakeButton("▶ Start Telemetry", async () =>
            {
                if (_telemetryServer == null)
                {
                    _telemetryServer = new TelemetryStreamServer();
                    _telemetryServer.OnStatusChanged += msg => Dispatcher.Invoke(() => 
                    {
                        statusLabel.Text = msg;
                        SetStatus(msg);
                    });
                }

                await _telemetryServer.StartAsync();
                statusLabel.Foreground = Brush("#00ff00");

                // Update QR to show telemetry endpoint
                _qrImage.Source = GenerateQrCode(_telemetryServer.StreamUrl);
                SetStatus($"✓ Telemetry started: {_telemetryServer.StreamUrl}");
            });
            startBtn.Margin = new Thickness(0, 0, 4, 0);
            btnRow.Children.Add(startBtn);

            var stopBtn = MakeButton("⏹ Stop Telemetry", async () =>
            {
                if (_telemetryServer != null)
                {
                    await _telemetryServer.StopAsync();
                    statusLabel.Foreground = Brush("#ff6b6b");
                    statusLabel.Text = "Telemetry stopped";
                }
            });
            stopBtn.Margin = new Thickness(0, 0, 4, 0);
            btnRow.Children.Add(stopBtn);

            var openBtn = MakeButton("🌐 Open", () =>
            {
                if (_telemetryServer != null && _telemetryServer.IsRunning)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName        = _telemetryServer.StreamUrl,
                            UseShellExecute = true
                        });
                        SetStatus($"✓ Opened {_telemetryServer.StreamUrl}");
                    }
                    catch (Exception ex)
                    {
                        SetStatus($"✗ Failed to open: {ex.Message}");
                    }
                }
                else
                {
                    SetStatus("Start telemetry first");
                }
            });
            openBtn.Margin = new Thickness(0, 0, 4, 0);
            btnRow.Children.Add(openBtn);

            var copyBtn = MakeButton("📋 Copy URL", () =>
            {
                if (_telemetryServer != null && _telemetryServer.IsRunning)
                {
                    Clipboard.SetText(_telemetryServer.StreamUrl);
                    SetStatus("✓ URL copied");
                }
            });
            btnRow.Children.Add(copyBtn);

            panel.Children.Add(btnRow);

            var info = new TextBlock
            {
                Text       = "Endpoints: /api/metrics • /metrics/stream • /wasm/* • /system/info • /trace/capture",
                Foreground = Brush("#666666"),
                FontSize   = 8,
                TextWrapping = TextWrapping.Wrap,
                Margin     = new Thickness(0, 6, 0, 0)
            };
            panel.Children.Add(info);

            return panel;
        }

        private UIElement BuildPwaTab()
        {
            var panel = new StackPanel { };

            panel.Children.Add(new TextBlock
            {
                Text         = "Download Unix Browser APK for Android.\nStart server → scan QR on your phone → install.",
                Foreground   = Brush("#888888"),
                FontSize     = 9,
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(0, 0, 0, 6)
            });

            var statusLabel = new TextBlock
            {
                Text         = "Start server to generate APK download QR",
                Foreground   = Brush("#00ff00"),
                FontSize     = 10,
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(0, 0, 0, 6)
            };
            panel.Children.Add(statusLabel);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal };

            var startBtn = MakeButton("▶ Start Server", async () =>
            {
                if (_streamServer == null)
                {
                    _streamServer = new LocalhostStreamServer();
                    _streamServer.OnStatusChanged += msg => Dispatcher.Invoke(() =>
                    {
                        statusLabel.Text = msg;
                        SetStatus(msg);
                    });
                }
                await _streamServer.StartAsync();
                var apkUrl = _streamServer.StreamUrl + "/apk";
                statusLabel.Text = $"✓ Scan QR to download APK";
                statusLabel.Foreground = Brush("#00ff00");
                _qrImage.Source = GenerateQrCode(apkUrl);
                _qrLabel.Text   = $"Scan to download APK → {apkUrl}";
                SetStatus($"✓ APK ready: {apkUrl}");
            });
            startBtn.Margin = new Thickness(0, 0, 4, 0);
            btnRow.Children.Add(startBtn);

            var openBtn = MakeButton("🌐 Open in browser", () =>
            {
                if (_streamServer != null && _streamServer.IsRunning)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _streamServer.StreamUrl + "/apk",
                        UseShellExecute = true
                    });
                else SetStatus("Start server first");
            });
            openBtn.Margin = new Thickness(0, 0, 4, 0);
            btnRow.Children.Add(openBtn);

            var copyBtn = MakeButton("📋 Copy URL", () =>
            {
                if (_streamServer != null && _streamServer.IsRunning)
                {
                    Clipboard.SetText(_streamServer.StreamUrl + "/apk");
                    SetStatus("✓ APK URL copied");
                }
                else SetStatus("Start server first");
            });
            btnRow.Children.Add(copyBtn);

            panel.Children.Add(btnRow);

            panel.Children.Add(new TextBlock
            {
                Text         = "Phone: enable 'Install unknown apps' in settings → scan QR → download → install APK\nAPK is the real C# .NET MAUI Unix Browser",
                Foreground   = Brush("#666666"),
                FontSize     = 8,
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(0, 8, 0, 0)
            });

            return panel;
        }

            // ── QR helpers ────────────────────────────────────────────────────────

        /// <summary>
        /// QR payload for connection: encodes the local receiver address
        /// so a scanning device knows where to POST data back.
        /// </summary>
        private string BuildConnectionQrPayload()
            => $"unixbrowser://quickshare?ip={_engine.QuickShare.LocalIP}&port=9731";

        private BitmapSource GenerateQrCode(string payload)
        {
            using var gen  = new QRCodeGenerator();
            using var data = gen.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
            using var code = new PngByteQRCode(data);
            var png = code.GetGraphic(8, new byte[] { 0, 255, 0 }, new byte[] { 26, 26, 26 });
            using var ms = new System.IO.MemoryStream(png);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption  = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        // ── Service event wiring ──────────────────────────────────────────────

        private void WireServiceEvents()
        {
            _engine.QuickShare.OnSendProgress    += p => Dispatcher.Invoke(() => _progressBar.Value = p);
            _engine.QuickShare.OnReceiveProgress += p => Dispatcher.Invoke(() => _progressBar.Value = p);
            _engine.QuickShare.OnStatusChanged   += m => Dispatcher.Invoke(() => SetStatus(m));
        }

        // ── UI factories ──────────────────────────────────────────────────────

        private void SetStatus(string msg)
            => _statusLabel.Text = msg;

        private WpfButton MakeButton(string label, Action onClick)
        {
            var btn = new WpfButton
            {
                Content         = label,
                Background      = Brush("#003300"),
                Foreground      = Brush("#00ff00"),
                BorderBrush     = Brush("#008800"),
                Padding         = new Thickness(10, 4, 10, 4),
                Cursor          = System.Windows.Input.Cursors.Hand,
                FontFamily      = new FontFamily("Courier New"),
                FontSize        = 10
            };
            btn.Click += (s, e) => onClick();
            return btn;
        }

        private WpfButton MakeButton(string label, Func<Task> onClick)
        {
            var btn = new WpfButton
            {
                Content         = label,
                Background      = Brush("#003300"),
                Foreground      = Brush("#00ff00"),
                BorderBrush     = Brush("#008800"),
                Padding         = new Thickness(10, 4, 10, 4),
                Cursor          = System.Windows.Input.Cursors.Hand,
                FontFamily      = new FontFamily("Courier New"),
                FontSize        = 10
            };
            btn.Click += async (s, e) => await onClick();
            return btn;
        }

        private WpfButton MakeTabButton(string label)
        {
            return new WpfButton
            {
                Content     = label,
                Background  = Brush("#002200"),
                Foreground  = Brush("#00cc00"),
                BorderBrush = Brush("#004400"),
                Padding     = new Thickness(8, 3, 8, 3),
                Margin      = new Thickness(0, 0, 4, 0),
                Cursor      = System.Windows.Input.Cursors.Hand,
                FontFamily  = new FontFamily("Courier New"),
                FontSize    = 9
            };
        }

        private static SolidColorBrush Brush(string hex)
        {
            hex = hex.TrimStart('#');
            var c = System.Drawing.ColorTranslator.FromHtml("#" + hex);
            return new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
        }
    }
}

