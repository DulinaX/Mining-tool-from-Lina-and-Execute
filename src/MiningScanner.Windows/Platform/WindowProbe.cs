using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace MiningScanner.Windows.Platform;

/// <summary>Physical-pixel snapshot of an explicitly foreground game window.</summary>
public sealed record GameWindow(nint Handle, uint ProcessId, Rectangle ClientBounds, int Dpi);

/// <summary>Only window/process metadata. Never opens an executable or any game file.</summary>
public static class WindowProbe
{
    public static bool TryGetGame(out GameWindow window)
    {
        window = null!;
        // Lock screen/UAC/another input desktop means foreground state is not authoritative.
        if (!NativeWindow.IsOnInputDesktop()) return false;
        nint hwnd = NativeWindow.GetForegroundWindow();
        if (hwnd == 0 || !NativeWindow.IsWindow(hwnd) || !NativeWindow.IsWindowVisible(hwnd) || NativeWindow.IsIconic(hwnd)) return false;
        if (!NativeWindow.IsUncloaked(hwnd)) return false;
        if (NativeWindow.GetWindowThreadProcessId(hwnd, out uint pid) == 0 || pid == 0) return false;

        // PROCESS_QUERY_LIMITED_INFORMATION permits image-name metadata only. No VM_READ access.
        nint process = NativeWindow.OpenProcess(0x1000, false, pid);
        if (process == 0) return false;
        try
        {
            var imageName = new StringBuilder(32768);
            uint length = (uint)imageName.Capacity;
            if (!NativeWindow.QueryFullProcessImageName(process, 0, imageName, ref length)) return false;
            string metadataPath = imageName.ToString();
            int lastSlash = Math.Max(metadataPath.LastIndexOf('\\'), metadataPath.LastIndexOf('/'));
            if (!metadataPath.AsSpan(lastSlash + 1).Equals("StarCitizen.exe".AsSpan(), StringComparison.OrdinalIgnoreCase)) return false;
        }
        finally { NativeWindow.CloseHandle(process); }

        if (!NativeWindow.GetClientRect(hwnd, out var rect) || rect.Right <= 0 || rect.Bottom <= 0) return false;
        var origin = new NativeWindow.Point();
        if (!NativeWindow.ClientToScreen(hwnd, ref origin)) return false;
        uint dpi = NativeWindow.GetDpiForWindow(hwnd);
        if (dpi is < 48 or > 960 || rect.Right > 32768 || rect.Bottom > 32768) return false;
        if (NativeWindow.GetForegroundWindow() != hwnd || NativeWindow.IsIconic(hwnd)) return false;
        window = new GameWindow(hwnd, pid, new Rectangle(origin.X, origin.Y, rect.Right, rect.Bottom), (int)dpi);
        return true;
    }

    public static bool IsCurrent(GameWindow snapshot) => snapshot is not null && TryGetGame(out var current) && current == snapshot;

    /// <summary>Only map a frame whose dimensions identify its desktop origin unambiguously.</summary>
    internal static bool TryMapCaptureRegion(GameWindow window, Rectangle clientRegion, int frameWidth, int frameHeight, out Rectangle crop)
    {
        crop = Rectangle.Empty;
        var candidates = new HashSet<Point>();
        if (frameWidth == window.ClientBounds.Width && frameHeight == window.ClientBounds.Height)
            candidates.Add(window.ClientBounds.Location);
        if (NativeWindow.GetWindowRect(window.Handle, out var raw) && raw.Right - raw.Left == frameWidth && raw.Bottom - raw.Top == frameHeight)
            candidates.Add(new Point(raw.Left, raw.Top));
        if (NativeWindow.DwmGetWindowAttributeRect(window.Handle, 9, out var visible, Marshal.SizeOf<NativeWindow.Rect>()) == 0 && visible.Right - visible.Left == frameWidth && visible.Bottom - visible.Top == frameHeight)
            candidates.Add(new Point(visible.Left, visible.Top));
        if (candidates.Count != 1) return false;
        Point frameOrigin = candidates.Single();
        crop = new Rectangle(window.ClientBounds.Left + clientRegion.Left - frameOrigin.X,
            window.ClientBounds.Top + clientRegion.Top - frameOrigin.Y, clientRegion.Width, clientRegion.Height);
        return new Rectangle(0, 0, frameWidth, frameHeight).Contains(crop);
    }

    internal static bool IsValidRegion(GameWindow window, Rectangle region)
    {
        if (window.Handle == 0 || region.Width < 8 || region.Height < 8 || region.Width > 1200 || region.Height > 400 || (long)region.Width * region.Height > 240000)
            return false;
        return new Rectangle(Point.Empty, window.ClientBounds.Size).Contains(region);
    }

    /// <summary>Visible-pixel eligibility only; actual capture is separately HWND isolated.</summary>
    internal static bool IsRegionVisible(GameWindow window, Rectangle clientRegion)
    {
        Rectangle screenRegion = new(window.ClientBounds.Left + clientRegion.Left,
            window.ClientBounds.Top + clientRegion.Top, clientRegion.Width, clientRegion.Height);
        // Subtract each monitor from the requested area, also handling gaps in monitor layouts.
        var uncovered = new List<Rectangle> { screenRegion };
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            var next = new List<Rectangle>();
            foreach (Rectangle remaining in uncovered)
            {
                Rectangle intersection = Rectangle.Intersect(remaining, screen.Bounds);
                if (intersection.IsEmpty) { next.Add(remaining); continue; }
                if (remaining.Top < intersection.Top) next.Add(Rectangle.FromLTRB(remaining.Left, remaining.Top, remaining.Right, intersection.Top));
                if (intersection.Bottom < remaining.Bottom) next.Add(Rectangle.FromLTRB(remaining.Left, intersection.Bottom, remaining.Right, remaining.Bottom));
                if (remaining.Left < intersection.Left) next.Add(Rectangle.FromLTRB(remaining.Left, intersection.Top, intersection.Left, intersection.Bottom));
                if (intersection.Right < remaining.Right) next.Add(Rectangle.FromLTRB(intersection.Right, intersection.Top, remaining.Right, intersection.Bottom));
            }
            uncovered = next;
        }
        if (uncovered.Count != 0) return false;
        var visited = new HashSet<nint>();
        for (nint above = NativeWindow.GetTopWindow(0); above != 0 && visited.Count < 4096; above = NativeWindow.GetWindow(above, 2))
        {
            if (above == window.Handle) return true;
            if (!visited.Add(above)) return false;
            if (!NativeWindow.IsWindowVisible(above) || NativeWindow.IsIconic(above)) continue;
            // durchsichtige Overlays (NVIDIA, Discord, Steam, eigene Anzeige) blockieren den Scan nicht.
            if (NativeWindow.IsPassiveOverlay(above)) continue;
            if (NativeWindow.DwmGetWindowAttributeInt(above, 14, out int cloaked, sizeof(int)) != 0) return false;
            if (cloaked != 0) continue;
            if (!NativeWindow.GetWindowRect(above, out var rect)) return false;
            if (Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom).IntersectsWith(screenRegion)) return false;
        }
        return false;
    }
}

internal static class NativeWindow
{
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern nint GetTopWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern nint GetWindow(nint hwnd, uint command);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetClientRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool ClientToScreen(nint hwnd, ref Point point);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("kernel32.dll", SetLastError = true)] internal static extern nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);
    [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder name, ref uint length);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern nint GetThreadDesktop(uint threadId);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint OpenInputDesktop(uint flags, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint access);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseDesktop(nint desktop);
    [DllImport("user32.dll", EntryPoint = "GetUserObjectInformationW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetUserObjectInformation(nint handle, int index, StringBuilder name, uint size, out uint needed);
    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")] internal static extern int DwmGetWindowAttributeInt(nint hwnd, int attribute, out int value, int size);
    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")] internal static extern int DwmGetWindowAttributeRect(nint hwnd, int attribute, out Rect value, int size);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    internal static bool IsPassiveOverlay(nint hwnd)
    {
        if (GetWindowThreadProcessId(hwnd, out uint pid) != 0 && pid == (uint)Environment.ProcessId) return true;
        long ex = GetWindowLongPtr(hwnd, -20);
        const long Transparent = 0x20, Layered = 0x80000, ToolWindow = 0x80, NoActivate = 0x08000000;
        if ((ex & Transparent) != 0) return true;
        if ((ex & Layered) != 0 && ((ex & ToolWindow) != 0 || (ex & NoActivate) != 0)) return true;
        return false;
    }
    internal static bool IsUncloaked(nint hwnd) => DwmGetWindowAttributeInt(hwnd, 14, out int cloaked, sizeof(int)) == 0 && cloaked == 0;

    internal static bool IsOnInputDesktop()
    {
        nint input = OpenInputDesktop(0, false, 0x0001); // DESKTOP_READOBJECTS, metadata only.
        if (input == 0) return false;
        try
        {
            nint current = GetThreadDesktop(GetCurrentThreadId());
            if (current == 0) return false;
            var inputName = new StringBuilder(256);
            var currentName = new StringBuilder(256);
            return GetUserObjectInformation(input, 2, inputName, 512, out _) &&
                GetUserObjectInformation(current, 2, currentName, 512, out _) &&
                string.Equals(inputName.ToString(), currentName.ToString(), StringComparison.Ordinal);
        }
        finally { CloseDesktop(input); }
    }
}
