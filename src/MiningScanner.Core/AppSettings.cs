using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;

namespace MiningScanner.Core;

public sealed record RegionSettings
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public int ClientWidth { get; init; }
    public int ClientHeight { get; init; }
    public int Dpi { get; init; } = 96;

    public void Validate()
    {
        if (!double.IsFinite(X) || !double.IsFinite(Y) || !double.IsFinite(Width) || !double.IsFinite(Height)
            || X < 0 || Y < 0 || Width <= 0 || Height <= 0 || X + Width > 1.000000001 || Y + Height > 1.000000001)
            throw new InvalidDataException("Der Aufnahmebereich muss vollständig innerhalb des Spielfensters liegen.");
        if (ClientWidth is < 100 or > 32768 || ClientHeight is < 100 or > 32768 || Dpi is < 48 or > 768)
            throw new InvalidDataException("Ungültige Fenstergröße oder DPI-Skalierung des Aufnahmebereichs.");
        // This application captures a small numeric field, never an entire frame.
        double pixelWidth = Width * ClientWidth;
        double pixelHeight = Height * ClientHeight;
        if (pixelWidth < 8 || pixelHeight < 8 || pixelWidth > 1200 || pixelHeight > 400 || pixelWidth * pixelHeight > 240_000)
            throw new InvalidDataException("Der Aufnahmebereich muss mindestens 8 × 8 und höchstens 1200 × 400 Pixel (240.000 Pixel insgesamt) groß sein.");
    }

    /// <summary>Changes to size or DPI require explicit recalibration; moving is safe.</summary>
    public bool MatchesClient(int width, int height, int dpi) =>
        width == ClientWidth && height == ClientHeight && dpi == Dpi;
}

public sealed record AppSettings
{
    [JsonRequired]
    public int SchemaVersion { get; init; } = 1;
    public int IntervalMs { get; init; } = 250;
    public int ExpiryMs { get; init; } = 4000;
    public double MinimumConfidence { get; init; } = 0.60;
    public int MaxCount { get; init; } = 10;
    public int OverlayTextSize { get; init; } = 18;
    public double OverlayX { get; init; } = 0.03;
    public double OverlayY { get; init; } = 0.05;
    public bool PreviewEnabled { get; init; }
    public string Filter { get; init; } = "";
    public RegionSettings? Region { get; init; }

    // Intentional: no Enabled/Running field. Every launch starts disarmed.
    public void Validate()
    {
        if (SchemaVersion != 1)
            throw new InvalidDataException("Nicht unterstützte Einstellungs-Version.");
        if (IntervalMs is < 100 or > 5000)
            throw new InvalidDataException("Das Abtastintervall muss zwischen 100 und 5000 ms liegen.");
        if (ExpiryMs is < 250 or > 30_000)
            throw new InvalidDataException("Die Verfallszeit muss zwischen 250 und 30.000 ms liegen.");
        if (!double.IsFinite(MinimumConfidence) || MinimumConfidence is < 0.5 or > 1.0)
            throw new InvalidDataException("Die Mindestkonfidenz muss zwischen 0,5 und 1,0 liegen.");
        if (MaxCount is < 1 or > 1000)
            throw new InvalidDataException("Die maximale Anzahl muss zwischen 1 und 1000 liegen.");
        if (OverlayTextSize is < 10 or > 48)
            throw new InvalidDataException("Die Textgröße muss zwischen 10 und 48 liegen.");
        if (!double.IsFinite(OverlayX) || !double.IsFinite(OverlayY) || OverlayX is < 0 or > 1 || OverlayY is < 0 or > 1)
            throw new InvalidDataException("Die Overlayposition muss innerhalb des Spielfensters liegen.");
        JsonStorage.ValidateText(Filter, "Rohstofffilter", 200, allowEmpty: true);
        Region?.Validate();
    }

    public static AppSettings Parse(string json)
    {
        var settings = JsonStorage.Parse<AppSettings>(json);
        settings.Validate();
        return settings;
    }

    public string ToJson()
    {
        Validate();
        return JsonSerializer.Serialize(this, JsonStorage.Options);
    }
}

public sealed record SettingsLoadResult(AppSettings Settings, bool UsedDefaults, string? Warning);

public static class SettingsStore
{
    public static SettingsLoadResult Load(string path)
    {
        try
        {
            return new SettingsLoadResult(AppSettings.Parse(JsonStorage.ReadText(path)), false, null);
        }
        catch (FileNotFoundException)
        {
            return new SettingsLoadResult(new AppSettings(), true, null);
        }
        catch (DirectoryNotFoundException)
        {
            return new SettingsLoadResult(new AppSettings(), true, null);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            // The original is never silently rewritten here. Do not emit any log.
            return new SettingsLoadResult(new AppSettings(), true,
                "Einstellungen konnten nicht sicher geladen werden. Sichere Standardwerte werden verwendet; die Erkennung bleibt gestoppt.");
        }
    }

    public static void Save(string path, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        JsonStorage.SaveAtomic(path, settings);
    }
}
