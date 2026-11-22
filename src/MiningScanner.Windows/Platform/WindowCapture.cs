using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace MiningScanner.Windows.Platform;

/// <summary>
/// HWND-bound Windows Graphics Capture. Windows transports a whole-window GPU surface;
/// only the selected small rectangle is copied into a CPU-readable texture/Bitmap.
/// No desktop capture, GDI fallback, file writes, injection, or game graphics hooks.
/// Every sample has its own session, one frame buffer, and bounded lifetime.
/// </summary>
public sealed class WindowCapture : IDisposable
{
    private ID3D11Device? device;
    private ID3D11DeviceContext? context;
    private IDirect3DDevice? winrtDevice;
    private bool disposed;

    public Bitmap? TryCapture(GameWindow window, Rectangle clientRegion, Func<bool> stillAllowed, CancellationToken token)
    {
        if (disposed) throw new ObjectDisposedException(nameof(WindowCapture));
        bool Allowed() => !token.IsCancellationRequested && stillAllowed() && WindowProbe.IsCurrent(window) && WindowProbe.IsRegionVisible(window, clientRegion);
        if (!WindowProbe.IsValidRegion(window, clientRegion) || !Allowed()) return null;
        if (!GraphicsCaptureSession.IsSupported()) throw new NotSupportedException("Windows Graphics Capture wird von diesem System nicht unterstützt.");
        EnsureDevice();
        if (!Allowed()) return null;

        GraphicsCaptureItem item = CaptureInterop.CreateForWindow(window.Handle);
        var size = item.Size;
        if (size.Width <= 0 || size.Height <= 0 || !WindowProbe.TryMapCaptureRegion(window, clientRegion, size.Width, size.Height, out var crop)) return null;
        using var pool = Direct3D11CaptureFramePool.CreateFreeThreaded(winrtDevice!, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, size);
        using var session = pool.CreateCaptureSession(item);
        session.IsCursorCaptureEnabled = false;
        // Retain the indicator by default. Only an explicit UI consent action can enable
        // the optional Windows-supported border preference; the OS remains authoritative.
        CaptureBorderPermission.ApplyTo(session);
        if (!Allowed()) return null;
        session.StartCapture();
        var deadline = Stopwatch.StartNew();
        while (deadline.ElapsedMilliseconds < 250)
        {
            if (!Allowed()) return null;
            using var frame = pool.TryGetNextFrame();
            if (frame is null) { Thread.Sleep(5); continue; }
            if (!Allowed() || frame.ContentSize.Width != size.Width || frame.ContentSize.Height != size.Height) return null;
            return CopyCrop(frame, crop, Allowed, deadline);
        }
        return null;
    }

    private void EnsureDevice()
    {
        if (device is not null) return;
        D3D11.D3D11CreateDevice(null, DriverType.Hardware, DeviceCreationFlags.BgraSupport,
            new[] { FeatureLevel.Level_11_1, FeatureLevel.Level_11_0 }, out var created, out _, out var createdContext).CheckError();
        try
        {
            using var dxgi = created.QueryInterface<IDXGIDevice>();
            Marshal.ThrowExceptionForHR(CaptureInterop.CreateDirect3D11DeviceFromDXGIDevice(dxgi.NativePointer, out nint inspectable));
            try { winrtDevice = MarshalInterface<IDirect3DDevice>.FromAbi(inspectable); }
            finally { Marshal.Release(inspectable); }
            device = created;
            context = createdContext;
        }
        catch { createdContext.Dispose(); created.Dispose(); throw; }
    }

    private unsafe Bitmap? CopyCrop(Direct3D11CaptureFrame frame, Rectangle crop, Func<bool> allowed, Stopwatch deadline)
    {
        var access = frame.Surface.As<CaptureInterop.IDirect3DDxgiInterfaceAccess>();
        Guid iid = typeof(ID3D11Texture2D).GUID;
        nint texturePointer;
        try { Marshal.ThrowExceptionForHR(access.GetInterface(ref iid, out texturePointer)); }
        finally
        {
            // The custom COM interface owns a reference to the frame surface. Do not
            // leave that GPU image alive until a later managed garbage collection.
            if (Marshal.IsComObject(access)) Marshal.ReleaseComObject(access);
        }
        using var texture = new ID3D11Texture2D(texturePointer);
        var description = new Texture2DDescription
        {
            Width = (uint)crop.Width, Height = (uint)crop.Height,
            MipLevels = 1, ArraySize = 1, Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0), Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None, CPUAccessFlags = CpuAccessFlags.Read, MiscFlags = ResourceOptionFlags.None
        };
        using var staging = device!.CreateTexture2D(description);
        if (!allowed()) return null;
        context!.CopySubresourceRegion(staging, 0, 0, 0, 0, texture, 0,
            new Box(crop.Left, crop.Top, 0, crop.Right, crop.Bottom, 1));
        context.Flush();
        MappedSubresource mapped;
        while (true)
        {
            if (!allowed() || deadline.ElapsedMilliseconds >= 350) return null;
            var result = context.Map(staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.DoNotWait, out mapped);
            if (result.Success) break;
            // DXGI_ERROR_WAS_STILL_DRAWING: bounded retry without blocking GPU readback.
            if (result.Code != unchecked((int)0x887A000A)) result.CheckError();
            Thread.Sleep(2);
        }
        try
        {
            if (!allowed()) return null;
            var bitmap = new Bitmap(crop.Width, crop.Height, PixelFormat.Format32bppArgb);
            try
            {
                var bits = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    for (int row = 0; row < crop.Height; row++)
                    {
                        byte* src = (byte*)mapped.DataPointer + (long)row * mapped.RowPitch;
                        byte* dst = (byte*)bits.Scan0 + row * bits.Stride;
                        Buffer.MemoryCopy(src, dst, bits.Stride, crop.Width * 4);
                    }
                }
                finally { bitmap.UnlockBits(bits); }
                if (!allowed()) { bitmap.Dispose(); return null; }
                return bitmap;
            }
            catch { bitmap.Dispose(); throw; }
        }
        finally { context.Unmap(staging, 0); }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        winrtDevice?.Dispose(); winrtDevice = null;
        context?.Dispose(); context = null;
        device?.Dispose(); device = null;
    }
}

internal static class CaptureInterop
{
    private static readonly Guid ItemIid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    [ComImport, Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        [PreserveSig] int CreateForWindow(nint window, ref Guid iid, out nint item);
        [PreserveSig] int CreateForMonitor(nint monitor, ref Guid iid, out nint item);
    }
    [ComImport, Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDirect3DDxgiInterfaceAccess
    {
        [PreserveSig] int GetInterface(ref Guid iid, out nint result);
    }

    internal static GraphicsCaptureItem CreateForWindow(nint hwnd)
    {
        const string runtimeClass = "Windows.Graphics.Capture.GraphicsCaptureItem";
        Marshal.ThrowExceptionForHR(WindowsCreateString(runtimeClass, runtimeClass.Length, out nint className));
        nint factory = 0;
        try
        {
            Guid interopIid = typeof(IGraphicsCaptureItemInterop).GUID;
            Marshal.ThrowExceptionForHR(RoGetActivationFactory(className, ref interopIid, out factory));
            var interop = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factory);
            try
            {
                Guid itemIid = ItemIid;
                Marshal.ThrowExceptionForHR(interop.CreateForWindow(hwnd, ref itemIid, out nint item));
                try { return MarshalInspectable<GraphicsCaptureItem>.FromAbi(item); }
                finally { Marshal.Release(item); }
            }
            finally { Marshal.ReleaseComObject(interop); }
        }
        finally { if (factory != 0) Marshal.Release(factory); WindowsDeleteString(className); }
    }

    [DllImport("d3d11.dll", ExactSpelling = true)] internal static extern int CreateDirect3D11DeviceFromDXGIDevice(nint dxgiDevice, out nint graphicsDevice);
    [DllImport("combase.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern int WindowsCreateString(string text, int length, out nint hstring);
    [DllImport("combase.dll", ExactSpelling = true)] private static extern int WindowsDeleteString(nint hstring);
    [DllImport("combase.dll", ExactSpelling = true)] private static extern int RoGetActivationFactory(nint className, ref Guid iid, out nint factory);
}
