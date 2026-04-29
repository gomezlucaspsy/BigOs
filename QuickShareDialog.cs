using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QRCoder;

namespace UnixBrowser
{
    /// <summary>
    /// Quick Share via QR Code - scan with any phone camera to open the URL instantly
    /// No accounts, no IP typing, no network scanning needed
    /// </summary>
    public class QuickShareDialog : Window
    {
        private readonly BrowserEngine _engine;

        public QuickShareDialog(BrowserEngine engine)
        {
            _engine = engine;
            Title = "⚡ Quick Share — Scan QR Code";
            Width = 380;
            Height = 460;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(0x1a, 0x1a, 0x1a));
            FontFamily = new FontFamily("Courier New");

            BuildUI();
        }

        private void BuildUI()
        {
            var url = _engine.CurrentUrl;

            var stack = new StackPanel { Margin = new Thickness(20) };

            // Header
            stack.Children.Add(new TextBlock
            {
                Text = "⚡ Quick Share",
                Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xff, 0x00)),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 6)
            });

            stack.Children.Add(new TextBlock
            {
                Text = "Scan with your phone camera to open this page",
                Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                FontSize = 10,
                Margin = new Thickness(0, 0, 0, 16),
                TextWrapping = TextWrapping.Wrap
            });

            // QR Code image
            var qrImage = new System.Windows.Controls.Image
            {
                Width = 280,
                Height = 280,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16),
                Source = GenerateQrCode(url)
            };
            stack.Children.Add(qrImage);

            // URL label
            stack.Children.Add(new TextBlock
            {
                Text = url.Length > 50 ? url[..50] + "…" : url,
                Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff)),
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            });

            // Copy URL button
            var copyBtn = new Button
            {
                Content = "📋 Copy URL to Clipboard",
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = new SolidColorBrush(Color.FromRgb(0x00, 0x33, 0x00)),
                Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xff, 0x00)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x88, 0x00)),
                Padding = new Thickness(16, 6, 16, 6),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            copyBtn.Click += (s, e) =>
            {
                Clipboard.SetText(url);
                copyBtn.Content = "✓ Copied!";
            };
            stack.Children.Add(copyBtn);

            Content = new ScrollViewer { Content = stack };
        }

        private BitmapSource GenerateQrCode(string url)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrData);

            var pngBytes = qrCode.GetGraphic(10, new byte[] { 0, 255, 0 }, new byte[] { 26, 26, 26 });

            using var ms = new System.IO.MemoryStream(pngBytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
    }
}
