using System.Collections.Concurrent;
using System.Diagnostics;
using MiningScanner.Core;
using MiningScanner.Windows.Ocr;
using MiningScanner.Windows.Platform;
using MiningScanner.Windows.Runtime;

namespace MiningScanner.Integration.Tests;

/// <summary>Only injected services and newly allocated blank RAM Bitmaps.
/// No native window probes, desktop UI/captures, real game metadata, OCR resources,
/// file I/O or network operations occur in these controller tests.</summary>
internal static class ControllerTests
{
    private static readonly List<string> Failures = [];
    private static int _passed;

    private static int Main()
    {
        Check("starts disarmed even with valid synthetic foreground window", () =>
        {
            using var env = new FakeEnvironment();
            using var controller = env.Create();
            Wait(() => env.ProbeCalls > 0);
            False(controller.Enabled); True(controller.IsWorkerAlive);
            AssertEmpty(controller, "Gestoppt");
            False(controller.TryGetActiveWindow(out _));
            Equal(0, env.CaptureCalls); Equal(0, env.OcrCalls); Equal(0, env.OcrCreated);
        });
        Check("settings roundtrip never enables controller", () =>
        {
            using var env = new FakeEnvironment();
            using var controller = env.Create(AppSettings.Parse(Settings().ToJson()));
            controller.RefreshGate(); False(controller.Enabled); AssertEmpty(controller, "Gestoppt");
        });
        Check("invalid constructor configuration rejected before worker", () =>
        {
            using var env = new FakeEnvironment();
            bool rejected = false;
            try { using var controller = env.Create(new AppSettings { IntervalMs = -1 }); }
            catch (InvalidDataException) { rejected = true; }
            True(rejected); Equal(0, env.ProbeCalls);
        });
        var geometry = new GameWindow((nint)1, 1, new Rectangle(-1920, -180, 1920, 1080), 96);
        var geometrySettings = Settings() with
        {
            Region = new RegionSettings { X = .1, Y = .2, Width = .1, Height = .05, ClientWidth = 1920, ClientHeight = 1080, Dpi = 96 }
        };
        Check("negative monitor origin keeps ROI client-relative", () =>
        {
            True(ScannerController.MapRegion(geometrySettings, geometry, out var roi)); Equal(new Rectangle(192, 216, 192, 54), roi);
        });
        Check("window movement preserves relative ROI", () =>
        {
            True(ScannerController.MapRegion(geometrySettings, geometry with { ClientBounds = new Rectangle(300, 200, 1920, 1080) }, out var roi)); Equal(new Rectangle(192, 216, 192, 54), roi);
        });
        Check("resolution change blocks ROI", () => False(ScannerController.MapRegion(geometrySettings, geometry with { ClientBounds = new Rectangle(0, 0, 2560, 1440) }, out _)));
        Check("DPI change blocks ROI", () => False(ScannerController.MapRegion(geometrySettings, geometry with { Dpi = 144 }, out _)));
        Check("out-of-client ROI rejected", () => False(ScannerController.MapRegion(geometrySettings with { Region = geometrySettings.Region! with { X = .95 } }, geometry, out _)));
        Check("explicit enable cannot capture without region", () =>
        {
            using var env = new FakeEnvironment(); using var controller = env.Create(Settings() with { Region = null });
            controller.SetEnabled(true); Wait(() => env.ProbeCalls >= 4);
            True(controller.Enabled); False(controller.TryGetActiveWindow(out _)); AssertNoResult(controller); Equal(0, env.CaptureCalls);
        });
        Check("missing foreground game blocks capture", () =>
        {
            using var env = new FakeEnvironment { CurrentWindow = null }; using var controller = env.Create();
            controller.SetEnabled(true); Wait(() => env.ProbeCalls >= 4); AssertNoResult(controller); Equal(0, env.CaptureCalls);
        });
        Check("stop clears eligibility and result", () =>
        {
            using var env = new FakeEnvironment(); using var controller = env.Create(Settings() with { Region = null });
            controller.SetEnabled(true); controller.SetEnabled(false); False(controller.Enabled); False(controller.TryGetActiveWindow(out _)); AssertEmpty(controller, "Gestoppt");
        });
        Check("reconfigure keeps stopped controller stopped", () =>
        {
            using var env = new FakeEnvironment(); using var controller = env.Create();
            controller.Configure(Settings() with { PreviewEnabled = true }, SyntheticTable()); controller.RefreshGate();
            False(controller.Enabled); AssertEmpty(controller, "Gestoppt");
        });
        Check("invalid reconfiguration rejected before work", () =>
        {
            using var env = new FakeEnvironment(); using var controller = env.Create(); bool rejected = false;
            try { controller.Configure(new AppSettings { IntervalMs = -1 }, SyntheticTable()); }
            catch (InvalidDataException) { rejected = true; }
            True(rejected); False(controller.Enabled);
        });
        Check("idle dispose joins worker and is idempotent", () =>
        {
            using var env = new FakeEnvironment(); var controller = env.Create(); var clock = Stopwatch.StartNew();
            controller.Dispose(); False(controller.IsWorkerAlive); False(controller.Enabled); True(clock.Elapsed < TimeSpan.FromSeconds(2)); controller.Dispose();
        });
        Check("one frame insufficient; second equal frame accepted", () =>
        {
            using var env = new FakeEnvironment(); env.OcrStep(new(300, .99, false));
            var second = env.OcrStep(new(300, .99, false), blocked: true); env.DefaultReading = new(300, .99, false);
            using var controller = env.Create(); controller.SetEnabled(true);
            second.WaitEntered(); AssertNoValue(controller); second.Release();
            Wait(() => Read(controller).Value == 300); Equal(3, Read(controller).Matches![0].Count);
        });
        Check("stop during pending capture discards returned frame", () => CheckPendingCapture(focusLoss: false));
        Check("focus loss during pending capture discards returned frame", () => CheckPendingCapture(focusLoss: true));
        Check("stop during pending OCR discards reading and preview", () => CheckPendingOcr(focusLoss: false));
        Check("focus loss during pending OCR discards reading and preview", () => CheckPendingOcr(focusLoss: true));
        Check("focus return rejects old generation and needs two new frames", () => CheckGenerationTransition(focusLoss: true));
        Check("stop/start rejects old generation and needs two new frames", () => CheckGenerationTransition(focusLoss: false));
        Check("focus return never restarts explicitly stopped recognition", () =>
        {
            using var env = new FakeEnvironment { CurrentWindow = null }; using var controller = env.Create();
            controller.SetEnabled(false); env.CurrentWindow = FakeEnvironment.Game; controller.RefreshGate();
            Wait(() => env.ProbeCalls >= 4); False(controller.Enabled); Equal(0, env.CaptureCalls);
        });
        Check("low-confidence readings never stabilize", () =>
        {
            using var env = new FakeEnvironment { DefaultReading = new(300, .2, true) }; using var controller = env.Create();
            controller.SetEnabled(true); Wait(() => env.OcrCalls >= 3); AssertNoValue(controller);
            True(Read(controller).Status.StartsWith("Erkennung unsicher", StringComparison.Ordinal));
        });
        Check("explicit uncertainty rejects even a high-confidence integer", () =>
        {
            using var env = new FakeEnvironment { DefaultReading = new(300, .99, true, OcrDiagnostic.ConflictingReadings) };
            using var controller = env.Create(); controller.SetEnabled(true);
            Wait(() => env.OcrCalls >= 3); AssertNoValue(controller);
            True(Read(controller).Status.StartsWith("Erkennung unsicher", StringComparison.Ordinal));
        });
        Check("explicitly uncertain reading breaks consecutive confirmation", () =>
        {
            using var env = new FakeEnvironment();
            env.OcrStep(new(300, .99, false));
            env.OcrStep(new(300, .99, true, OcrDiagnostic.ConflictingReadings));
            var firstFresh = env.OcrStep(new(300, .99, false), blocked: true);
            var secondFresh = env.OcrStep(new(300, .99, false), blocked: true);
            env.DefaultReading = new(300, .99, false);
            using var controller = env.Create(); controller.SetEnabled(true);
            firstFresh.WaitEntered(); AssertNoValue(controller);
            firstFresh.Release(); secondFresh.WaitEntered(); AssertNoValue(controller);
            secondFresh.Release(); Wait(() => Read(controller).Value == 300);
        });
        Check("different OCR rejection reasons have distinct diagnostic details", () =>
        {
            using var env = new FakeEnvironment();
            env.OcrStep(new(null, .95, true, OcrDiagnostic.ConflictingReadings));
            var lowConfidence = env.OcrStep(new(null, .5, true, OcrDiagnostic.LowConfidence), blocked: true);
            var afterLowConfidence = env.OcrStep(OcrReading.Empty, blocked: true);
            using var controller = env.Create(); controller.SetEnabled(true);
            lowConfidence.WaitEntered(); var conflict = Read(controller);
            True(!string.IsNullOrWhiteSpace(conflict.Detail)); True(conflict.Value is null);
            lowConfidence.Release(); afterLowConfidence.WaitEntered(); var low = Read(controller);
            True(!string.IsNullOrWhiteSpace(low.Detail)); True(low.Value is null);
            False(string.Equals(conflict.Detail, low.Detail, StringComparison.Ordinal));
        });
        Check("confirmed result clears rejection detail after fresh two-frame confirmation", () =>
        {
            using var env = new FakeEnvironment();
            env.OcrStep(new(null, .95, true, OcrDiagnostic.ConflictingReadings));
            var firstFresh = env.OcrStep(new(300, .99, false), blocked: true);
            var secondFresh = env.OcrStep(new(300, .99, false), blocked: true);
            var afterConfirmation = env.OcrStep(new(300, .99, false), blocked: true);
            using var controller = env.Create(); controller.SetEnabled(true);
            firstFresh.WaitEntered(); True(!string.IsNullOrWhiteSpace(Read(controller).Detail));
            firstFresh.Release(); secondFresh.WaitEntered(); var pending = Read(controller);
            True(pending.Value is null); True(pending.Detail?.Contains("1/2", StringComparison.Ordinal) == true);
            secondFresh.Release(); afterConfirmation.WaitEntered(); var accepted = Read(controller);
            Equal<int?>(300, accepted.Value); Equal("Aktiv", accepted.Status);
            True(string.IsNullOrEmpty(accepted.Detail)); False(accepted.Stale);
        });
        Check("missing readings mark prior value stale then remove it", () =>
        {
            using var env = new FakeEnvironment { DefaultReading = new(300, .99, false) };
            using var controller = env.Create(Settings() with { ExpiryMs = 500 }); controller.SetEnabled(true);
            Wait(() => Read(controller).Value == 300); env.DefaultReading = OcrReading.Empty;
            Wait(() => Read(controller).Stale); Wait(() => Read(controller).Value is null);
        });
        Check("null capture skips OCR and clears value", () =>
        {
            using var env = new FakeEnvironment { ReturnNullFrame = true }; using var controller = env.Create();
            controller.SetEnabled(true); Wait(() => env.CaptureCalls >= 2); Equal(0, env.OcrCalls); AssertNoResult(controller);
        });
        Check("dispose cancels pending capture and joins/disposes services", () => CheckDisposePending(inOcr: false));
        Check("dispose cancels pending OCR and joins/disposes services", () => CheckDisposePending(inOcr: true));
        Check("repeated creation and close leave no owned workers", () =>
        {
            for (int i = 0; i < 12; i++)
            {
                using var env = new FakeEnvironment(); var controller = env.Create(Settings() with { Region = null });
                controller.SetEnabled(i % 2 == 0); controller.Dispose(); False(controller.IsWorkerAlive); False(controller.Enabled);
            }
        });
        foreach (string failure in Failures) Console.Error.WriteLine("FAIL " + failure);
        Console.WriteLine($"RESULT: {_passed} passed, {Failures.Count} failed.");
        Console.WriteLine("Scope: injected synthetic window metadata/capture/OCR; blank RAM-only frames.");
        Console.WriteLine("No real window probes, UI automation, desktop captures, game access, OCR resources or network/file tracing.");
        return Failures.Count == 0 ? 0 : 1;
    }

    private static void CheckPendingCapture(bool focusLoss)
    {
        using var env = new FakeEnvironment(); var blocked = env.CaptureStep(blocked: true, ignoreCancellation: true);
        using var controller = env.Create(Settings() with { PreviewEnabled = true }); controller.SetEnabled(true); blocked.WaitEntered();
        if (focusLoss) { env.CurrentWindow = null; controller.RefreshGate(); } else controller.SetEnabled(false);
        True(blocked.Token.IsCancellationRequested); False(controller.TryGetActiveWindow(out _));
        Equal(focusLoss, controller.Enabled); blocked.Release();
        // Bitmap/GDI+ is not thread-safe. Join the owner before inspecting disposal;
        // polling Width while the worker calls Dispose can race inside native GDI+.
        controller.Dispose(); False(controller.IsWorkerAlive);
        Equal(1, env.Frames.Count); True(IsDisposedAfterJoin(env.Frames.Single()));
        Equal(0, env.OcrCalls); AssertNoResult(controller);
    }
    private static void CheckPendingOcr(bool focusLoss)
    {
        using var env = new FakeEnvironment(); var blocked = env.OcrStep(new(300, .99, false), blocked: true, ignoreCancellation: true);
        using var controller = env.Create(Settings() with { PreviewEnabled = true }); controller.SetEnabled(true); blocked.WaitEntered();
        if (focusLoss) { env.CurrentWindow = null; controller.RefreshGate(); } else controller.SetEnabled(false);
        True(blocked.Token.IsCancellationRequested); False(controller.TryGetActiveWindow(out _));
        Equal(focusLoss, controller.Enabled); blocked.Release();
        controller.Dispose(); False(controller.IsWorkerAlive);
        Equal(1, env.Frames.Count); True(IsDisposedAfterJoin(env.Frames.Single())); AssertNoResult(controller);
    }
    private static void CheckGenerationTransition(bool focusLoss)
    {
        using var env = new FakeEnvironment(); env.OcrStep(new(100, .99, false)); env.OcrStep(new(100, .99, false));
        var oldPending = env.OcrStep(new(300, .99, false), blocked: true, ignoreCancellation: true);
        var newFirst = env.OcrStep(new(300, .99, false), blocked: true); var newSecond = env.OcrStep(new(300, .99, false), blocked: true);
        env.DefaultReading = new(300, .99, false);
        using var controller = env.Create(Settings() with { PreviewEnabled = true }); controller.SetEnabled(true);
        oldPending.WaitEntered(); Equal<int?>(100, Read(controller).Value);
        if (focusLoss)
        {
            env.CurrentWindow = null; controller.RefreshGate(); AssertNoResult(controller);
            env.CurrentWindow = FakeEnvironment.Game; controller.RefreshGate();
        }
        else { controller.SetEnabled(false); AssertNoResult(controller); controller.SetEnabled(true); }
        True(oldPending.Token.IsCancellationRequested); oldPending.Release();
        newFirst.WaitEntered(); AssertNoResult(controller); // Old generation published nothing.
        newFirst.Release(); newSecond.WaitEntered(); AssertNoValue(controller); // One fresh read is insufficient.
        newSecond.Release(); Wait(() => Read(controller).Value == 300);
    }
    private static void CheckDisposePending(bool inOcr)
    {
        using var env = new FakeEnvironment(); var step = inOcr ? env.OcrStep(new(300, .99, false), blocked: true) : env.CaptureStep(blocked: true);
        var controller = env.Create();
        try
        {
            controller.SetEnabled(true); step.WaitEntered(); var clock = Stopwatch.StartNew(); controller.Dispose();
            True(step.Token.IsCancellationRequested); False(controller.IsWorkerAlive); False(controller.Enabled);
            True(clock.Elapsed < TimeSpan.FromSeconds(2)); Equal(1, env.CaptureDisposed); Equal(1, env.OcrDisposed); True(env.Frames.All(IsDisposedAfterJoin));
        }
        finally { step.Release(); controller.Dispose(); }
    }
    private static AppSettings Settings() => new()
    {
        IntervalMs = 100,
        Region = new RegionSettings { X = .1, Y = .2, Width = 64d / 1920, Height = 24d / 1080, ClientWidth = 1920, ClientHeight = 1080, Dpi = 96 }
    };
    private static SignatureTable SyntheticTable() => new()
    {
        Entries = [new SignatureEntry { Id = "synthetic-test", NameEn = "Test only", NameDe = "Nur Test", BaseSignature = 100,
            Source = "synthetic-unit-test", VerifiedPatch = "TEST ONLY", Verification = "Artificial fixture; not game data" }]
    };
    private static ScanDisplay Read(ScannerController controller)
    {
        var result = controller.ReadDisplay(out var preview); preview?.Dispose(); return result;
    }
    private static void AssertNoValue(ScannerController controller) => True(Read(controller).Value is null);
    private static void AssertNoResult(ScannerController controller)
    {
        var state = controller.ReadDisplay(out var preview);
        using (preview) { True(preview is null); True(state.Value is null); False(state.Stale); }
    }
    private static void AssertEmpty(ScannerController controller, string expectedStatus)
    {
        AssertNoResult(controller); Equal(expectedStatus, Read(controller).Status);
    }
    // Only call after controller.Dispose has joined the worker owning these images.
    private static bool IsDisposedAfterJoin(Bitmap bitmap)
    {
        try { _ = bitmap.Width; return false; } catch (ArgumentException) { return true; }
    }
    private static void Wait(Func<bool> condition)
    {
        if (!SpinWait.SpinUntil(condition, TimeSpan.FromSeconds(3))) throw new TimeoutException("Expected condition did not arrive within 3 seconds.");
    }
    private static void Check(string name, Action action)
    {
        try { action(); _passed++; Console.WriteLine("PASS " + name); }
        catch (Exception exception) { Failures.Add($"{name}: {exception.GetType().Name}: {exception.Message}"); }
    }
    private static void True(bool value) { if (!value) throw new InvalidOperationException("Expected true."); }
    private static void False(bool value) => True(!value);
    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }

    private sealed class FakeEnvironment : IDisposable
    {
        internal static readonly GameWindow Game = new((nint)42, 123, new Rectangle(0, 0, 1920, 1080), 96);
        private GameWindow? _current = Game;
        private OcrReading _reading = OcrReading.Empty;
        private readonly ConcurrentQueue<Step> _captureSteps = new(), _ocrSteps = new();
        private readonly List<Step> _ownedSteps = [];
        private int _probes, _captures, _ocrCalls, _ocrCreated, _captureDisposed, _ocrDisposed;
        internal ConcurrentQueue<Bitmap> Frames { get; } = new();
        internal bool ReturnNullFrame { get; init; }
        internal GameWindow? CurrentWindow { get => Volatile.Read(ref _current); set => Volatile.Write(ref _current, value); }
        internal OcrReading DefaultReading { get => Volatile.Read(ref _reading); set => Volatile.Write(ref _reading, value); }
        internal int ProbeCalls => Volatile.Read(ref _probes);
        internal int CaptureCalls => Volatile.Read(ref _captures);
        internal int OcrCalls => Volatile.Read(ref _ocrCalls);
        internal int OcrCreated => Volatile.Read(ref _ocrCreated);
        internal int CaptureDisposed => Volatile.Read(ref _captureDisposed);
        internal int OcrDisposed => Volatile.Read(ref _ocrDisposed);
        internal ScannerController Create(AppSettings? settings = null) => new(settings ?? Settings(), SyntheticTable(), new CaptureServices(
            () => { Interlocked.Increment(ref _probes); return CurrentWindow; }, window => CurrentWindow == window,
            _ => { Interlocked.Increment(ref _ocrCreated); return new FakeOcr(this); }, () => new FakeCapture(this)));
        internal Step CaptureStep(bool blocked = false, bool ignoreCancellation = false)
        {
            var step = new Step(OcrReading.Empty, blocked, ignoreCancellation); _ownedSteps.Add(step); _captureSteps.Enqueue(step); return step;
        }
        internal Step OcrStep(OcrReading reading, bool blocked = false, bool ignoreCancellation = false)
        {
            var step = new Step(reading, blocked, ignoreCancellation); _ownedSteps.Add(step); _ocrSteps.Enqueue(step); return step;
        }
        private sealed class FakeCapture(FakeEnvironment owner) : IFrameCapture
        {
            public Bitmap? TryCapture(GameWindow window, Rectangle region, Func<bool> stillAllowed, CancellationToken token)
            {
                Interlocked.Increment(ref owner._captures); if (owner._captureSteps.TryDequeue(out var step)) step.Execute(token);
                if (owner.ReturnNullFrame) return null;
                // Return even after cancellation to test independent controller checks.
                var bitmap = new Bitmap(region.Width, region.Height); owner.Frames.Enqueue(bitmap); return bitmap;
            }
            public void Dispose() => Interlocked.Increment(ref owner._captureDisposed);
        }
        private sealed class FakeOcr(FakeEnvironment owner) : IOcrEngine
        {
            public OcrReading Read(Bitmap bitmap, CancellationToken token)
            {
                Interlocked.Increment(ref owner._ocrCalls);
                if (!owner._ocrSteps.TryDequeue(out var step)) return owner.DefaultReading;
                step.Execute(token); return step.Reading;
            }
            public void Dispose() => Interlocked.Increment(ref owner._ocrDisposed);
        }
        public void Dispose()
        {
            foreach (var step in _ownedSteps) step.Dispose();
            // The controller has joined first. Double disposal also cleans failed tests.
            foreach (var frame in Frames) frame.Dispose();
        }
    }
    private sealed class Step(OcrReading reading, bool blocked, bool ignoreCancellation) : IDisposable
    {
        private readonly ManualResetEventSlim _entered = new(false), _released = new(!blocked);
        internal OcrReading Reading { get; } = reading;
        internal CancellationToken Token { get; private set; }
        internal void Execute(CancellationToken token)
        {
            Token = token; _entered.Set();
            if (ignoreCancellation)
            {
                if (!_released.Wait(TimeSpan.FromSeconds(3))) throw new TimeoutException("Synthetic stage was not released.");
            }
            else if (WaitHandle.WaitAny([_released.WaitHandle, token.WaitHandle], TimeSpan.FromSeconds(3)) == WaitHandle.WaitTimeout)
                throw new TimeoutException("Synthetic stage was not released or cancelled.");
        }
        internal void WaitEntered() { if (!_entered.Wait(TimeSpan.FromSeconds(3))) throw new TimeoutException("Synthetic stage was not entered."); }
        internal void Release() => _released.Set();
        public void Dispose() { _released.Dispose(); _entered.Dispose(); }
    }
}
