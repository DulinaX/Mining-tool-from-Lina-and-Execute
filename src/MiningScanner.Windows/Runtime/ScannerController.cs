using System.Diagnostics;
using MiningScanner.Core;
using MiningScanner.Windows.Ocr;
using MiningScanner.Windows.Platform;

namespace MiningScanner.Windows.Runtime;

public sealed record ScanDisplay(string Status, int? Value = null, bool Stale = false,
    IReadOnlyList<SignatureMatch>? Matches = null, long ObservedMs = 0, string? Detail = null);

/// <summary>One OCR worker, no frame queue. The UI watchdog revokes leases every 25 ms.
/// Every native boundary also checks current metadata. A revoked lease never revives.</summary>
public sealed class ScannerController : IDisposable
{
    private readonly object _sync = new();
    private readonly CancellationTokenSource _shutdown = new();
    private CancellationTokenSource _lease = new();
    private readonly AutoResetEvent _wake = new(false);
    private readonly Thread _worker;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly CaptureServices _services;
    private AppSettings _settings;
    private SignatureTable _table;
    private bool _enabled, _closing, _allowed;
    private long _generation;
    private GameWindow? _window;
    private Rectangle _roi;
    private Bitmap? _preview;
    private ScanDisplay _display = new("Gestoppt");
    private string? _failure;

    public ScannerController(AppSettings settings, SignatureTable table)
        : this(settings, table, CaptureServices.Production)
    {
    }

    internal ScannerController(AppSettings settings, SignatureTable table, CaptureServices services)
    {
        settings.Validate(); table.Validate();
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        _settings = settings;
        _table = table;
        _worker = new Thread(Work) { Name = "MiningScanner.LocalOCR", IsBackground = true };
        _worker.Start();
    }

    public bool Enabled { get { lock (_sync) return _enabled; } }
    public bool IsWorkerAlive => _worker.IsAlive;
    public long NowMs => _clock.ElapsedMilliseconds;

    public void SetEnabled(bool enabled)
    {
        lock (_sync)
        {
            if (_closing) return;
            _enabled = enabled;
            _failure = null;
            RevokeLocked();
            _display = new(enabled ? "Pausiert – Spiel nicht im Vordergrund" : "Gestoppt");
        }
        RefreshGate();
        _wake.Set();
    }

    public void Configure(AppSettings settings, SignatureTable table)
    {
        settings.Validate(); table.Validate();
        lock (_sync)
        {
            _settings = settings; _table = table;
            RevokeLocked();
            _display = new(_enabled ? "Keine Signatur" : "Gestoppt");
        }
        _wake.Set();
    }

    public static bool MapRegion(AppSettings settings, GameWindow window, out Rectangle roi)
    {
        roi = Rectangle.Empty;
        var r = settings.Region;
        if (r is null) return false;
        // Exi & Lina: Der Bereich ist relativ gespeichert. Ändert sich die Fenstergröße oder DPI,
        // wird er einfach mitskaliert statt eine neue Auswahl zu verlangen.
        int cw = window.ClientBounds.Width, ch = window.ClientBounds.Height;
        if (cw < 100 || ch < 100) return false;
        if (!double.IsFinite(r.X) || !double.IsFinite(r.Y) || !double.IsFinite(r.Width) || !double.IsFinite(r.Height) || r.Width <= 0 || r.Height <= 0) return false;
        roi = FitRegion(new Rectangle((int)Math.Round(r.X * cw), (int)Math.Round(r.Y * ch),
            (int)Math.Round(r.Width * cw), (int)Math.Round(r.Height * ch)), new Size(cw, ch));
        return roi.Width >= 8 && roi.Height >= 8;
    }

    /// <summary>Hält einen Bereich innerhalb des Fensters und der erlaubten Größe (max. 1200 × 400, 240.000 Pixel).</summary>
    public static Rectangle FitRegion(Rectangle r, Size client)
    {
        int w = Math.Min(r.Width, 1200), h = Math.Min(r.Height, 400);
        if ((long)w * h > 240_000) w = Math.Max(8, 240_000 / Math.Max(1, h));
        int cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
        var fitted = new Rectangle(cx - w / 2, cy - h / 2, w, h);
        fitted.Width = Math.Min(fitted.Width, client.Width); fitted.Height = Math.Min(fitted.Height, client.Height);
        fitted.X = Math.Clamp(fitted.X, 0, Math.Max(0, client.Width - fitted.Width));
        fitted.Y = Math.Clamp(fitted.Y, 0, Math.Max(0, client.Height - fitted.Height));
        return fitted;
    }

    public void RefreshGate()
    {
        var window = _services.GetGame();
        bool game = window is not null;
        lock (_sync)
        {
            Rectangle roi = default;
            bool valid = game && MapRegion(_settings, window!, out roi);
            var gate = CaptureGate.Evaluate(new(!_closing, _enabled, game, false, valid));
            bool allowed = gate.Allowed && _failure is null;
            bool changed = _allowed != allowed || (allowed && (_window != window || _roi != roi));
            if (changed) RevokeLocked();
            _allowed = allowed; _window = allowed ? window : null; _roi = roi;
            if (!allowed)
            {
                string status = _failure ?? (!_enabled || _closing ? "Gestoppt" :
                    !game ? "Pausiert – Spiel nicht im Vordergrund" : "Pausiert – Aufnahmebereich neu auswählen");
                _display = new(status);
            }
            else if (changed) _display = new("Aktiv");
            if (_display.Value.HasValue && _clock.ElapsedMilliseconds - _display.ObservedMs >= _settings.ExpiryMs)
                _display = new("Keine Signatur");
        }
    }

    public ScanDisplay ReadDisplay(out Bitmap? preview)
    {
        lock (_sync)
        {
            preview = _preview; _preview = null;
            return _display;
        }
    }

    public bool TryGetActiveWindow(out GameWindow? window)
    {
        lock (_sync) { window = _allowed ? _window : null; return window is not null; }
    }

    private void RevokeLocked()
    {
        _allowed = false; _window = null; _generation++;
        _lease.Cancel(); _lease.Dispose(); _lease = new();
        _preview?.Dispose(); _preview = null;
    }

    private bool Current(long generation, GameWindow window)
    {
        lock (_sync)
            if (!_allowed || _closing || !_enabled || generation != _generation) return false;
        if (!_services.IsCurrent(window))
        {
            RefreshGate();
            return false;
        }
        return true;
    }

    private void Work()
    {
        IOcrEngine? ocr = null;
        IFrameCapture? capture = null;
        long lastGeneration = -1;
        RecognitionStabilizer? stabilizer = null;
        double confidence = -1;
        try
        {
            while (!_shutdown.IsCancellationRequested)
            {
                RefreshGate();
                AppSettings settings; SignatureTable table; GameWindow? window;
                Rectangle roi; long generation; CancellationToken token;
                lock (_sync)
                {
                    settings = _settings; table = _table; window = _allowed ? _window : null;
                    roi = _roi; generation = _generation; token = _lease.Token;
                }
                if (window is null) { _wake.WaitOne(25); continue; }
                long started = _clock.ElapsedMilliseconds;
                if (lastGeneration != generation)
                {
                    stabilizer = new(settings.ExpiryMs, settings.MinimumConfidence);
                    lastGeneration = generation;
                }
                try
                {
                    if (!Current(generation, window)) continue;
                    if (ocr is null || confidence != settings.MinimumConfidence)
                    {
                        ocr?.Dispose(); ocr = _services.CreateOcr(settings);
                        confidence = settings.MinimumConfidence;
                    }
                    capture ??= _services.CreateCapture();
                    using Bitmap? frame = capture.TryCapture(window, roi, () => Current(generation, window), token);
                    if (!Current(generation, window) || token.IsCancellationRequested) continue;
                    if (frame is null)
                    {
                        stabilizer!.Invalidate();
                        Publish(generation, new("Pausiert – Aufnahme nicht verfügbar / Bereich verdeckt"), null);
                    }
                    else
                    {
                        // A second fresh authorization check BEFORE OCR, and again after it.
                        if (!Current(generation, window)) continue;
                        if (ocr is LocalOcr localOcr)
                        {
                            var acceptTable = table; int acceptMax = settings.MaxCount;
                            localOcr.AcceptEarly = v => v >= 1 && acceptTable.Match(v, acceptMax).Count > 0;
                        }
                        var reading = PreferTableValue(ocr.Read(frame, token), table, settings);
                        if (!Current(generation, window) || token.IsCancellationRequested) continue;
                        long now = _clock.ElapsedMilliseconds;
                        var state = stabilizer!.Observe(reading.Uncertain ? null : reading.Value, reading.Confidence, now);
                        string status = state.IsStale ? "Erkennung unsicher – letzter Wert veraltet" :
                            state.HasCurrent ? "Aktiv" : reading.Uncertain || state.IsUncertain ? "Erkennung unsicher" : "Keine Signatur";
                        var matches = state.Value.HasValue ? table.Match(state.Value.Value, settings.MaxCount) : null;
                        // Previous values retain their original timestamp, never extended by failures.
                        long observed = now;
                        lock (_sync) if (state.IsStale) observed = _display.ObservedMs;
                        Bitmap? preview = settings.PreviewEnabled ? (Bitmap)frame.Clone() : null;
                        string? detail = DescribeReading(reading, state);
                        Publish(generation, new(status, state.Value, state.IsStale, matches, observed, detail), preview);
                    }
                }
                catch (OperationCanceledException) { stabilizer?.Invalidate(); }
                catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
                {
                    // No raw exception payload, pixels, OCR text, or log file is written.
                    lock (_sync)
                    {
                        if (generation == _generation)
                        {
                            _failure = "Gestoppt – OCR/Aufnahme nicht verfügbar; Ressourcen prüfen";
                            _enabled = false; RevokeLocked(); _display = new(_failure);
                        }
                    }
                    ocr?.Dispose(); ocr = null; capture?.Dispose(); capture = null;
                }
                int remaining = (int)Math.Max(0, settings.IntervalMs - (_clock.ElapsedMilliseconds - started));
                if (remaining > 0) _wake.WaitOne(remaining);
            }
        }
        finally { ocr?.Dispose(); capture?.Dispose(); }
    }

    /// <summary>
    /// Exi &amp; Lina: Abgleich mit der Erz-Liste. Hat die Texterkennung mehrere mögliche Zahlen gelesen,
    /// wird die genommen, die zu einem Erz aus der Liste passt. Es wird nur aus Zahlen gewählt,
    /// die die Erkennung wirklich gelesen hat – es wird nichts erfunden oder gerundet.
    /// </summary>
    private static OcrReading PreferTableValue(OcrReading reading, SignatureTable table, AppSettings settings)
    {
        bool Fits(int v) => v >= 1 && table.Match(v, settings.MaxCount).Count > 0;
        if (!reading.Uncertain && reading.Value is int direct && Fits(direct)) return reading;
        if (reading.Candidates is { Count: > 0 } candidates)
        {
            var best = candidates.Where(c => Fits(c.Value))
                .GroupBy(c => c.Value)
                .Select(g => (Value: g.Key, Count: g.Count(), Conf: g.Max(c => c.Confidence)))
                .OrderByDescending(g => g.Count).ThenByDescending(g => g.Conf)
                .ToList();
            if (best.Count > 0 && (best.Count == 1 || best[0].Count > best[1].Count || best[0].Conf > best[1].Conf + 0.1))
                return new(best[0].Value, Math.Max(best[0].Conf, settings.MinimumConfidence), false, OcrDiagnostic.None, candidates);
        }
        return reading;
    }

    private static string? DescribeReading(OcrReading reading, RecognitionState state)
    {
        if (state.HasCurrent) return null;
        if (!reading.Uncertain && reading.Value.HasValue && state.ConsecutiveCount == 1)
            return MiningScanner.Windows.UI.L.T("Bestätigung 1/2 – zweite sichere Lesung abwarten", "Confirming 1/2 – waiting for a second reliable reading");
        return reading.Diagnostic switch
        {
            OcrDiagnostic.NoContent => MiningScanner.Windows.UI.L.T("Keine lesbaren Ziffern / zu wenig Kontrast", "No readable digits / too little contrast"),
            OcrDiagnostic.InvalidRegion => MiningScanner.Windows.UI.L.T("Zahlenbereich neu auswählen", "Select the number area again"),
            OcrDiagnostic.NoReadableNumber => MiningScanner.Windows.UI.L.T("Keine eindeutige Zahl – nur die Signatur einrahmen", "No clear number – box in only the signature"),
            OcrDiagnostic.LowConfidence => MiningScanner.Windows.UI.L.T("Erkennung zu unsicher – Rahmen und HUD prüfen", "Recognition too uncertain – check the box and HUD"),
            OcrDiagnostic.ConflictingReadings => MiningScanner.Windows.UI.L.T("Leseversuche widersprechen sich", "Readings disagree"),
            OcrDiagnostic.InsufficientAgreement => MiningScanner.Windows.UI.L.T("Zu wenige übereinstimmende Leseversuche", "Too few matching readings"),
            _ => reading.Uncertain ? MiningScanner.Windows.UI.L.T("Zahl nicht sicher lesbar – Vorschau prüfen", "Number not readable reliably – check the preview") : null
        };
    }

    private void Publish(long generation, ScanDisplay display, Bitmap? preview)
    {
        lock (_sync)
        {
            if (_closing || !_allowed || generation != _generation) { preview?.Dispose(); return; }
            _display = display;
            _preview?.Dispose(); _preview = preview;
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_closing) return;
            _closing = true; _enabled = false; RevokeLocked();
        }
        _shutdown.Cancel(); _wake.Set();
        // Normal path drains the single worker. The app close handler handles a
        // hypothetical stalled native driver by terminating only its own process.
        if (!_worker.Join(TimeSpan.FromSeconds(5))) throw new TimeoutException("Der Aufnahme-Worker antwortet nicht.");
        lock (_sync) { _lease.Dispose(); _preview?.Dispose(); _preview = null; }
        _wake.Dispose(); _shutdown.Dispose();
    }
}
