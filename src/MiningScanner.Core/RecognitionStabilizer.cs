namespace MiningScanner.Core;

public readonly record struct RecognitionState(
    int? Value,
    bool IsStale,
    bool IsUncertain,
    int ConsecutiveCount)
{
    public bool HasCurrent => Value.HasValue && !IsStale;
}

/// <summary>
/// A single-thread-owned state machine. Supply monotonic milliseconds from a
/// Stopwatch, never wall-clock time. No image, queue, timer, worker or I/O here.
/// </summary>
public sealed class RecognitionStabilizer
{
    private readonly int _expiryMs;
    private readonly double _minimumConfidence;
    private int? _candidate;
    private int _consecutive;
    private int? _accepted;
    private long? _lastAcceptedAt;
    private long? _lastObservedAt;
    private long? _lastCandidateAt;
    private bool _isStale;
    private bool _isUncertain;

    public RecognitionStabilizer(int expiryMs = 1500, double minimumConfidence = 0.80)
    {
        if (expiryMs is < 1 or > 30_000)
            throw new ArgumentOutOfRangeException(nameof(expiryMs));
        if (!double.IsFinite(minimumConfidence) || minimumConfidence is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(minimumConfidence));
        _expiryMs = expiryMs;
        _minimumConfidence = minimumConfidence;
    }

    public RecognitionState State => new(_accepted, _isStale, _isUncertain, _consecutive);

    public RecognitionState Observe(int? value, double confidence, long nowMs)
    {
        ValidateClock(nowMs);
        Expire(nowMs);
        bool valid = value is >= 1 and <= NumberNormalizer.MaximumValue
                     && double.IsFinite(confidence) && confidence >= _minimumConfidence && confidence <= 1;
        if (!valid)
        {
            _candidate = null;
            _consecutive = 0;
            _lastCandidateAt = null;
            _isUncertain = value.HasValue;
            _isStale = _accepted.HasValue;
            return State;
        }

        if (_candidate == value)
            _consecutive = Math.Min(_consecutive + 1, 2);
        else
        {
            _candidate = value;
            _consecutive = 1;
        }
        _lastCandidateAt = nowMs;

        if (_consecutive >= 2)
        {
            _accepted = value;
            _lastAcceptedAt = nowMs;
            _isStale = false;
            _isUncertain = false;
        }
        else
        {
            _isUncertain = true;
            // Even an old identical value needs two fresh readings after a gap.
            _isStale = _accepted.HasValue;
        }
        return State;
    }

    public RecognitionState Tick(long nowMs)
    {
        ValidateClock(nowMs);
        Expire(nowMs);
        return State;
    }

    /// <summary>Call on stop, focus loss, minimized/closed game, or calibration changes.</summary>
    public void Invalidate()
    {
        _candidate = null;
        _consecutive = 0;
        _accepted = null;
        _lastAcceptedAt = null;
        _lastObservedAt = null;
        _lastCandidateAt = null;
        _isStale = false;
        _isUncertain = false;
    }

    private void Expire(long nowMs)
    {
        if (_lastCandidateAt.HasValue && nowMs - _lastCandidateAt.Value >= _expiryMs)
        {
            _candidate = null;
            _consecutive = 0;
            _lastCandidateAt = null;
            _isUncertain = false;
        }
        if (_lastAcceptedAt.HasValue && nowMs - _lastAcceptedAt.Value >= _expiryMs)
        {
            _accepted = null;
            _lastAcceptedAt = null;
            _isStale = false;
        }
    }

    private void ValidateClock(long nowMs)
    {
        if (nowMs < 0)
            throw new ArgumentOutOfRangeException(nameof(nowMs));
        if (_lastObservedAt.HasValue && nowMs < _lastObservedAt.Value)
            throw new ArgumentOutOfRangeException(nameof(nowMs), "Eine monotone Uhr ist erforderlich.");
        _lastObservedAt = nowMs;
    }
}
