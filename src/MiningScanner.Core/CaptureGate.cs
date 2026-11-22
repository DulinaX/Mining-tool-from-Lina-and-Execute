namespace MiningScanner.Core;

public readonly record struct CaptureFacts(
    bool Running,
    bool Enabled,
    bool IsGameForeground,
    bool IsMinimized,
    bool RegionValid);

public enum CaptureBlockReason
{
    None,
    ShuttingDown,
    Disabled,
    GameNotForeground,
    GameMinimized,
    InvalidRegion
}

public readonly record struct CaptureDecision(bool Allowed, CaptureBlockReason Reason);

/// <summary>
/// All facts must be freshly obtained before capture AND before recognition.
/// Unknown/unverifiable facts must be supplied as false (or minimized=true).
/// </summary>
public static class CaptureGate
{
    public static CaptureDecision Evaluate(CaptureFacts facts)
    {
        if (!facts.Running) return new(false, CaptureBlockReason.ShuttingDown);
        if (!facts.Enabled) return new(false, CaptureBlockReason.Disabled);
        if (!facts.IsGameForeground) return new(false, CaptureBlockReason.GameNotForeground);
        if (facts.IsMinimized) return new(false, CaptureBlockReason.GameMinimized);
        if (!facts.RegionValid) return new(false, CaptureBlockReason.InvalidRegion);
        return new(true, CaptureBlockReason.None);
    }
}
