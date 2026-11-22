using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using MiningScanner.Core;
using MiningScanner.Windows.Ocr;
using MiningScanner.Windows.Platform;

namespace MiningScanner.Windows.Runtime;

/// <summary>
/// Exi &amp; Lina: Einfacher Bildschirm-Modus. Liest nur die Pixel im gewählten Rahmen (wie ein Screenshot),
/// egal welches Fenster vorne ist. Kein Zugriff auf Spieldateien, Spielspeicher oder Eingaben.
/// </summary>
internal sealed record CaptureServices(
    Func<GameWindow?> GetGame,
    Func<GameWindow, bool> IsCurrent,
    Func<AppSettings, IOcrEngine> CreateOcr,
    Func<IFrameCapture> CreateCapture)
{
    internal static CaptureServices Production { get; } = new(
        () => ScreenArea.Current(),
        window => ScreenArea.IsCurrent(window),
        settings => new LocalOcr(minimumConfidence: settings.MinimumConfidence),
        () => new ScreenFrameCapture());

    private sealed class ScreenFrameCapture : IFrameCapture
    {
        public Bitmap? TryCapture(GameWindow window, Rectangle region, Func<bool> stillAllowed, CancellationToken token)
        {
            if (token.IsCancellationRequested || !stillAllowed() || region.Width < 1 || region.Height < 1) return null;
            var bitmap = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
            try
            {
                using (var g = Graphics.FromImage(bitmap))
                    g.CopyFromScreen(window.ClientBounds.X + region.X, window.ClientBounds.Y + region.Y, 0, 0, region.Size, CopyPixelOperation.SourceCopy);
                return bitmap;
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or ArgumentException or ExternalException)
            {
                bitmap.Dispose();
                return null;
            }
        }

        public void Dispose() { }
    }
}

internal interface IFrameCapture : IDisposable
{
    Bitmap? TryCapture(GameWindow window, Rectangle region, Func<bool> stillAllowed, CancellationToken token);
}
