using System.Drawing;

namespace MiningScanner.Windows.Ocr;

/// <summary>A result derived only from the supplied pixels. No text/image is retained.</summary>
public enum OcrDiagnostic
{
    None,
    NoContent,
    InvalidRegion,
    NoReadableNumber,
    LowConfidence,
    ConflictingReadings,
    InsufficientAgreement
}

public sealed record OcrReading(int? Value, double Confidence, bool Uncertain,
    OcrDiagnostic Diagnostic = OcrDiagnostic.None, IReadOnlyList<(int Value, double Confidence)>? Candidates = null)
{
    public static OcrReading Empty { get; } = new(null, 0, false, OcrDiagnostic.NoContent);
}

public interface IOcrEngine : IDisposable
{
    OcrReading Read(Bitmap bitmap, CancellationToken cancellation);
}
