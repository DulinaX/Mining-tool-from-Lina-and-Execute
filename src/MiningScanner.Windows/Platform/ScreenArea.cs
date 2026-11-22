using System.Diagnostics;
using System.Drawing;

namespace MiningScanner.Windows.Platform;

/// <summary>
/// Exi &amp; Lina: Der Scan-Bereich ist der Monitor, auf dem Star Citizen läuft.
/// Dafür wird nur nachgeschaut, ob ein Prozess "StarCitizen" existiert und auf welchem Monitor sein Fenster ist
/// (dieselbe Info, die auch der Task-Manager zeigt). Spieldateien, Spielspeicher und Eingaben werden NIE angefasst.
/// Läuft Star Citizen nicht, wird der Hauptmonitor benutzt.
/// </summary>
internal static class ScreenArea
{
    private static readonly object Sync = new();
    private static Rectangle _cached;
    private static bool _cachedFound;
    private static long _cachedAt = long.MinValue;

    /// <summary>True, wenn das Star-Citizen-Fenster gefunden wurde (sonst Hauptmonitor).</summary>
    public static bool GameFound { get { Refresh(); lock (Sync) return _cachedFound; } }

    public static Rectangle MonitorBounds { get { Refresh(); lock (Sync) return _cached; } }

    public static GameWindow Current() => new(0, 0, MonitorBounds, 96);

    public static bool IsCurrent(GameWindow? window) =>
        window is not null && window.Handle == 0 && window.ClientBounds == MonitorBounds;

    private static void Refresh()
    {
        long now = Environment.TickCount64;
        lock (Sync)
        {
            if (_cachedAt != long.MinValue && now - _cachedAt < 2000) return;
            _cachedAt = now;
        }
        Rectangle bounds = System.Windows.Forms.Screen.PrimaryScreen?.Bounds ?? System.Windows.Forms.SystemInformation.VirtualScreen;
        bool found = false;
        try
        {
            foreach (var process in Process.GetProcessesByName("StarCitizen"))
            {
                using (process)
                {
                    try
                    {
                        nint hwnd = process.MainWindowHandle;
                        if (hwnd != 0 && !found)
                        {
                            bounds = System.Windows.Forms.Screen.FromHandle(hwnd).Bounds;
                            found = true;
                        }
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        lock (Sync) { _cached = bounds; _cachedFound = found; }
    }
}
