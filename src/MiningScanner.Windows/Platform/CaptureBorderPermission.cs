using Windows.Foundation.Metadata;
using Windows.Graphics.Capture;
using Windows.Security.Authorization.AppCapabilityAccess;

namespace MiningScanner.Windows.Platform;

public sealed record CaptureBorderResult(bool Enabled, string Message);

/// <summary>
/// Optional, process-local border preference using only the documented Windows consent API.
/// RequestAsync must be called from an explicit UI action. It never starts a capture.
/// Windows keeps control of the actual indicator and may show it despite our preference.
/// </summary>
public static class CaptureBorderPermission
{
    private static int _enabled;
    private static int _requesting;

    public static bool Enabled => Volatile.Read(ref _enabled) != 0;

    public static bool IsSupported
    {
        get
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20348)) return false;
            try
            {
                return ApiInformation.IsPropertyPresent(
                    "Windows.Graphics.Capture.GraphicsCaptureSession", "IsBorderRequired") &&
                    ApiInformation.IsMethodPresent(
                        "Windows.Graphics.Capture.GraphicsCaptureAccess", "RequestAccessAsync");
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                return false;
            }
        }
    }

    public static void Disable() => Volatile.Write(ref _enabled, 0);

    public static async Task<CaptureBorderResult> RequestAsync()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20348) || !IsSupported)
            return new(false, "Diese Windows-Version unterstützt das Ausblenden des Aufnahmerahmens nicht.");

        if (Interlocked.CompareExchange(ref _requesting, 1, 0) != 0)
            return new(Enabled, "Die Windows-Freigabe wird bereits geprüft.");

        try
        {
            Disable();
            // This can display Windows' consent dialog. Unpackaged full-trust applications
            // may get an immediate answer; we still always use the documented request.
            var access = await GraphicsCaptureAccess.RequestAccessAsync(GraphicsCaptureAccessKind.Borderless);
            if (access != AppCapabilityAccessStatus.Allowed)
            {
                var reason = access switch
                {
                    AppCapabilityAccessStatus.DeniedByUser => "Windows erlaubt das Ausblenden derzeit nicht. Der Rahmen bleibt sichtbar.",
                    AppCapabilityAccessStatus.DeniedBySystem => "Die Windows-Richtlinie erlaubt das Ausblenden nicht. Der Rahmen bleibt sichtbar.",
                    _ => "Windows hat das Ausblenden nicht freigegeben. Der Rahmen bleibt sichtbar."
                };
                return new(false, reason);
            }

            Volatile.Write(ref _enabled, 1);
            return new(true, "Ausblenden für diese Programmsitzung angefordert. Windows oder andere Aufnahmeprogramme können den Rahmen weiterhin anzeigen.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            Disable();
            return new(false, "Die Windows-Freigabe konnte nicht geprüft werden. Der Rahmen bleibt sichtbar.");
        }
        finally
        {
            Volatile.Write(ref _requesting, 0);
        }
    }

    internal static void ApplyTo(GraphicsCaptureSession session)
    {
        if (!Enabled || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20348) || !IsSupported) return;
        try
        {
            // Windows enforces consent and system policy for this property. We do not
            // modify global preferences, registry values, windows, or capture indicators.
            session.IsBorderRequired = false;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // An optional indicator preference must never disable ordinary safe capture.
            Disable();
        }
    }
}
