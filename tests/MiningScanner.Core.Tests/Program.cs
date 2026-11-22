using System.Text;
using System.Text.Json;
using MiningScanner.Core;

// Behavioral fixtures are deliberately artificial. The optional final checks
// read only our explicitly supplied bundled reference table. No screen or game
// file is accessed. Only isolated temporary configuration JSON is written.
int passed = 0;
var failures = new List<string>();
var groups = new Dictionary<string, int>();
string group = "Normalization";

void Check(string name, Action action)
{
    try
    {
        action();
        passed++;
        groups[group] = groups.GetValueOrDefault(group) + 1;
    }
    catch (Exception exception)
    {
        failures.Add($"{group} / {name}: {exception.GetType().Name}: {exception.Message}");
    }
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"Expected {expected}, got {actual}.");
}

static void True(bool value)
{
    if (!value) throw new Exception("Expected true.");
}

static void False(bool value)
{
    if (value) throw new Exception("Expected false.");
}

static void Reject(Action action)
{
    try { action(); }
    catch (Exception exception) when (exception is InvalidDataException or JsonException or ArgumentException)
    {
        return;
    }
    throw new Exception("Expected input to be rejected.");
}

foreach (var (text, expected) in new (string, int)[]
{
    ("1", 1), ("1234", 1234), ("1,234", 1234), ("12.345", 12345),
    ("123 456", 123456), ("1\u00a0234", 1234), ("1\u202f234", 1234),
    ("1\u2009234", 1234), ("1'234", 1234), ("1\u2019234", 1234),
    ("123,456,789", 123456789), ("12.345.678", 12345678),
    ("1 234 567", 1234567), ("999999999", 999999999),
    ("  12,345  \r\n", 12345)
})
{
    Check("accept " + text, () =>
    {
        True(NumberNormalizer.TryNormalize(text, out int value));
        Equal(expected, value);
    });
}

foreach (string? text in new string?[]
{
    null, "", " ", "0", "00", "01234", "0,001", "000,100", "-1", "+1",
    "1.23", "12,34", "1234,567", "1,234.567", "1 234,567", "1, 234", "1,,234",
    "1'234\u2019567", "1\u00a0234 567", "1,234,", "1234\n5678", "1\t234",
    "1\r234", "1e3", "~1234", "1234?", "1234 RS", "RS: 1234", "3.17k",
    "1000000000", "2,147,483,647", "１２３４", "١٢٣٤", "I234", "1O34",
    "1234 5678", "1/234", "1_234", "1\u200b234", "1,234\n5,678"
})
{
    Check("reject " + text, () => False(NumberNormalizer.TryNormalize(text, out _)));
}
Check("long OCR text rejected", () => False(NumberNormalizer.TryNormalize(new string('1', 65), out _)));

group = "Exact signature matching";
// Synthetic bases 100, 150 and 175 are not shipped Star Citizen reference data.
SignatureEntry Entry(string id, int signature) => new()
{
    Id = id, NameEn = "Test " + id, NameDe = "Prüfstoff " + id,
    BaseSignature = signature, Source = "synthetic-unit-test",
    VerifiedPatch = "TEST ONLY", Verification = "Artificial fixture; not game data"
};
var fixture = new SignatureTable { Entries = [Entry("alpha", 100), Entry("beta", 150), Entry("gamma", 175)] };
Check("single base", () =>
{
    var hits = fixture.Match(175, 12);
    Equal(1, hits.Count);
    Equal("gamma", hits[0].Entry.Id);
    Equal(1, hits[0].Count);
});
Check("integer multiple", () =>
{
    var hits = fixture.Match(200, 12);
    Equal(1, hits.Count);
    Equal(2, hits[0].Count);
});
Check("all ambiguous matches returned", () =>
{
    var hits = fixture.Match(300, 12);
    Equal(2, hits.Count);
    True(hits.Any(hit => hit.Entry.Id == "alpha" && hit.Count == 3));
    True(hits.Any(hit => hit.Entry.Id == "beta" && hit.Count == 2));
});
Check("exact count limit", () => Equal(1, fixture.Match(300, 2).Count));
Check("over count limit", () => Equal(0, fixture.Match(300, 1).Count));
Check("unknown", () => Equal(0, fixture.Match(177, 12).Count));
Check("near miss never rounded", () => Equal(0, fixture.Match(299, 12).Count));
Check("zero invalid", () => Equal(0, fixture.Match(0, 12).Count));
Check("negative invalid", () => Equal(0, fixture.Match(-100, 12).Count));
Check("large invalid", () => Equal(0, fixture.Match(int.MaxValue, 12).Count));
Check("invalid count zero", () => Reject(() => fixture.Match(100, 0)));
Check("invalid count high", () => Reject(() => fixture.Match(100, 1001)));
Check("same base kept ambiguous", () =>
{
    var table = new SignatureTable { Entries = [Entry("one", 100), Entry("two", 100)] };
    Equal(2, table.Match(200, 12).Count);
});
Check("empty table allowed", () => Equal(0, new SignatureTable().Match(100, 12).Count));

group = "Signature JSON validation";
Check("roundtrip", () =>
{
    var loaded = SignatureTable.Parse(fixture.ToJson());
    Equal(3, loaded.Entries.Count);
    Equal(fixture.Entries[1], loaded.Entries[1]);
});
Check("missing schema", () => Reject(() => SignatureTable.Parse("{\"entries\":[]}")));
Check("wrong schema", () => Reject(() => SignatureTable.Parse("{\"schemaVersion\":2,\"entries\":[]}")));
Check("null document", () => Reject(() => SignatureTable.Parse("null")));
Check("null entries", () => Reject(() => SignatureTable.Parse("{\"schemaVersion\":1,\"entries\":null}")));
Check("null entry", () => Reject(() => SignatureTable.Parse("{\"schemaVersion\":1,\"entries\":[null]}")));
Check("missing entries", () => Reject(() => SignatureTable.Parse("{\"schemaVersion\":1}")));
Check("unknown key", () => Reject(() => SignatureTable.Parse("{\"schemaVersion\":1,\"entries\":[],\"download\":true}")));
Check("duplicate key", () => Reject(() => SignatureTable.Parse("{\"schemaVersion\":1,\"schemaVersion\":1,\"entries\":[]}")));
Check("trailing comma", () => Reject(() => SignatureTable.Parse("{\"schemaVersion\":1,\"entries\":[],}")));
Check("comments forbidden", () => Reject(() => SignatureTable.Parse("{/*comment*/\"schemaVersion\":1,\"entries\":[]}")));
Check("duplicate identifiers", () => Reject(() => new SignatureTable { Entries = [Entry("id", 100), Entry("ID", 150)] }.Validate()));
Check("zero base", () => Reject(() => new SignatureTable { Entries = [Entry("id", 0)] }.Validate()));
Check("negative base", () => Reject(() => new SignatureTable { Entries = [Entry("id", -1)] }.Validate()));
Check("too many entries", () => Reject(() => new SignatureTable { Entries = Enumerable.Range(0, 501).Select(index => Entry("id" + index, 100)).ToArray() }.Validate()));
Check("invalid ID", () => Reject(() => new SignatureTable { Entries = [Entry("../bad", 100)] }.Validate()));
Check("missing source", () => Reject(() => new SignatureTable { Entries = [Entry("id", 100) with { Source = "" }] }.Validate()));
Check("control character name", () => Reject(() => new SignatureTable { Entries = [Entry("id", 100) with { NameEn = "Bad\nName" }] }.Validate()));
Check("oversized JSON", () => Reject(() => SignatureTable.Parse(new string(' ', 1_048_577))));

group = "Confidence and stabilization";
Check("starts empty", () =>
{
    var tracker = new RecognitionStabilizer();
    Equal<int?>(null, tracker.State.Value);
    False(tracker.State.HasCurrent);
});
Check("two consecutive frames required", () =>
{
    var tracker = new RecognitionStabilizer();
    var first = tracker.Observe(300, .95, 0);
    False(first.HasCurrent);
    True(first.IsUncertain);
    Equal(1, first.ConsecutiveCount);
    var second = tracker.Observe(300, .95, 250);
    True(second.HasCurrent);
    Equal<int?>(300, second.Value);
    Equal(2, second.ConsecutiveCount);
});
Check("change requires two fresh frames; previous marked stale", () =>
{
    var tracker = new RecognitionStabilizer();
    tracker.Observe(300, .95, 0);
    tracker.Observe(300, .95, 250);
    var changing = tracker.Observe(175, .95, 500);
    Equal<int?>(300, changing.Value);
    True(changing.IsStale);
    False(changing.HasCurrent);
    var changed = tracker.Observe(175, .95, 750);
    Equal<int?>(175, changed.Value);
    True(changed.HasCurrent);
});
Check("different candidates do not stabilize", () =>
{
    var tracker = new RecognitionStabilizer();
    tracker.Observe(300, .95, 0);
    tracker.Observe(301, .95, 250);
    False(tracker.Observe(300, .95, 500).HasCurrent);
});
Check("low confidence discarded", () =>
{
    var tracker = new RecognitionStabilizer();
    tracker.Observe(300, .79, 0);
    var state = tracker.Observe(300, .79, 250);
    False(state.HasCurrent);
    True(state.IsUncertain);
    Equal(0, state.ConsecutiveCount);
});
Check("confidence threshold inclusive", () =>
{
    var tracker = new RecognitionStabilizer();
    tracker.Observe(300, .80, 0);
    True(tracker.Observe(300, .80, 250).HasCurrent);
});
foreach (double confidence in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1d, 1.01 })
    Check("invalid confidence " + confidence, () =>
    {
        var tracker = new RecognitionStabilizer();
        tracker.Observe(300, confidence, 0);
        False(tracker.Observe(300, confidence, 250).HasCurrent);
    });
foreach (int? value in new int?[] { null, 0, -1, int.MaxValue })
    Check("invalid candidate " + value, () =>
    {
        var tracker = new RecognitionStabilizer();
        tracker.Observe(value, 1, 0);
        False(tracker.Observe(value, 1, 250).HasCurrent);
    });
Check("bad frame breaks consecutive chain", () =>
{
    var tracker = new RecognitionStabilizer();
    tracker.Observe(300, .95, 0);
    tracker.Observe(300, .1, 250);
    False(tracker.Observe(300, .95, 500).HasCurrent);
    True(tracker.Observe(300, .95, 750).HasCurrent);
});
Check("empty frame breaks consecutive chain", () =>
{
    var tracker = new RecognitionStabilizer();
    tracker.Observe(300, .95, 0);
    tracker.Observe(null, 0, 250);
    False(tracker.Observe(300, .95, 500).HasCurrent);
});

group = "Expiry and reset";
Check("missing immediately makes accepted result stale", () =>
{
    var tracker = new RecognitionStabilizer();
    tracker.Observe(300, .95, 0);
    tracker.Observe(300, .95, 250);
    var absent = tracker.Observe(null, 0, 500);
    True(absent.IsStale);
    False(absent.HasCurrent);
    Equal<int?>(300, absent.Value);
    False(absent.IsUncertain);
});
Check("expires exactly at configured TTL", () =>
{
    var tracker = new RecognitionStabilizer(expiryMs: 1000);
    tracker.Observe(300, .95, 0);
    tracker.Observe(300, .95, 250);
    tracker.Observe(null, 0, 500);
    Equal<int?>(300, tracker.Tick(1249).Value);
    Equal<int?>(null, tracker.Tick(1250).Value);
});
Check("uncertain values never extend expiry", () =>
{
    var tracker = new RecognitionStabilizer(expiryMs: 1000);
    tracker.Observe(300, .95, 0);
    tracker.Observe(300, .95, 250);
    tracker.Observe(300, .2, 1000);
    Equal<int?>(null, tracker.Observe(300, .2, 1250).Value);
});
Check("new candidate never extends previous expiry", () =>
{
    var tracker = new RecognitionStabilizer(expiryMs: 1000);
    tracker.Observe(300, .95, 0);
    tracker.Observe(300, .95, 250);
    tracker.Observe(175, .95, 1200);
    Equal<int?>(null, tracker.Tick(1250).Value);
});
Check("continuous confident observations extend expiry", () =>
{
    var tracker = new RecognitionStabilizer(expiryMs: 1000);
    tracker.Observe(300, .95, 0);
    tracker.Observe(300, .95, 250);
    tracker.Observe(300, .95, 1000);
    Equal<int?>(300, tracker.Tick(1750).Value);
});
Check("expired candidate needs two new frames", () =>
{
    var tracker = new RecognitionStabilizer(expiryMs: 1000);
    tracker.Observe(300, .95, 0);
    tracker.Tick(1000);
    False(tracker.Observe(300, .95, 1100).HasCurrent);
    True(tracker.Observe(300, .95, 1350).HasCurrent);
});
Check("expired accepted result needs two new frames", () =>
{
    var tracker = new RecognitionStabilizer(expiryMs: 1000);
    tracker.Observe(300, .95, 0);
    tracker.Observe(300, .95, 250);
    tracker.Tick(1250);
    False(tracker.Observe(300, .95, 1500).HasCurrent);
    True(tracker.Observe(300, .95, 1750).HasCurrent);
});
Check("invalidate immediately clears accepted and candidates", () =>
{
    var tracker = new RecognitionStabilizer();
    tracker.Observe(300, .95, 0);
    tracker.Observe(300, .95, 250);
    tracker.Invalidate();
    Equal<int?>(null, tracker.State.Value);
    Equal(0, tracker.State.ConsecutiveCount);
    False(tracker.State.IsStale);
    False(tracker.Observe(300, .95, 500).HasCurrent);
});
Check("negative clock rejected", () => Reject(() => new RecognitionStabilizer().Tick(-1)));
Check("backwards clock rejected", () =>
{
    var tracker = new RecognitionStabilizer();
    tracker.Tick(500);
    Reject(() => tracker.Tick(499));
});

group = "Capture authorization";
var allowed = new CaptureFacts(Running: true, Enabled: true, IsGameForeground: true, IsMinimized: false, RegionValid: true);
Check("all conditions permit", () => True(CaptureGate.Evaluate(allowed).Allowed));
Check("startup disarmed", () => Equal(CaptureBlockReason.Disabled, CaptureGate.Evaluate(allowed with { Enabled = false }).Reason));
Check("stop revokes", () => False(CaptureGate.Evaluate(allowed with { Enabled = false }).Allowed));
Check("focus loss revokes", () => Equal(CaptureBlockReason.GameNotForeground, CaptureGate.Evaluate(allowed with { IsGameForeground = false }).Reason));
Check("minimize revokes", () => Equal(CaptureBlockReason.GameMinimized, CaptureGate.Evaluate(allowed with { IsMinimized = true }).Reason));
Check("game closed or unverifiable revokes", () => False(CaptureGate.Evaluate(allowed with { IsGameForeground = false, RegionValid = false }).Allowed));
Check("invalid crop revokes", () => Equal(CaptureBlockReason.InvalidRegion, CaptureGate.Evaluate(allowed with { RegionValid = false }).Reason));
Check("shutdown revokes", () => Equal(CaptureBlockReason.ShuttingDown, CaptureGate.Evaluate(allowed with { Running = false }).Reason));
Check("return does not enable previously stopped recognition", () => False(CaptureGate.Evaluate(allowed with { Enabled = false, IsGameForeground = true }).Allowed));
Check("return resumes previously enabled recognition", () => True(CaptureGate.Evaluate(allowed with { Enabled = true, IsGameForeground = true }).Allowed));
Check("every blocking combination fails closed", () =>
{
    int allowedCount = 0;
    for (int mask = 0; mask < 32; mask++)
    {
        var facts = new CaptureFacts((mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0, (mask & 8) != 0, (mask & 16) != 0);
        if (CaptureGate.Evaluate(facts).Allowed) allowedCount++;
    }
    Equal(1, allowedCount);
});

group = "Configuration and calibration";
var defaultSettings = new AppSettings();
Check("defaults valid", defaultSettings.Validate);
Check("default interval", () => Equal(250, defaultSettings.IntervalMs));
Check("default confidence", () => Equal(.8, defaultSettings.MinimumConfidence));
Check("no persisted enabled or running fields", () =>
{
    string json = defaultSettings.ToJson();
    False(json.Contains("\"enabled\"", StringComparison.OrdinalIgnoreCase));
    False(json.Contains("\"running\"", StringComparison.OrdinalIgnoreCase));
});
Check("enabled cannot be injected into configuration", () => Reject(() => AppSettings.Parse("{\"schemaVersion\":1,\"enabled\":true}")));
Check("roundtrip settings", () => Equal(defaultSettings, AppSettings.Parse(defaultSettings.ToJson())));
Check("missing schema rejected", () => Reject(() => AppSettings.Parse("{}")));
Check("duplicate config key rejected", () => Reject(() => AppSettings.Parse("{\"schemaVersion\":1,\"intervalMs\":250,\"intervalMs\":500}")));
Check("wrong casing rejected", () => Reject(() => AppSettings.Parse("{\"SchemaVersion\":1}")));
foreach (var setting in new[]
{
    defaultSettings with { SchemaVersion = 2 }, defaultSettings with { IntervalMs = 99 },
    defaultSettings with { IntervalMs = 5001 }, defaultSettings with { ExpiryMs = 249 },
    defaultSettings with { ExpiryMs = 30001 }, defaultSettings with { MinimumConfidence = double.NaN },
    defaultSettings with { MinimumConfidence = .49 }, defaultSettings with { MinimumConfidence = 1.01 },
    defaultSettings with { MaxCount = 0 }, defaultSettings with { MaxCount = 1001 },
    defaultSettings with { OverlayTextSize = 9 }, defaultSettings with { OverlayTextSize = 49 },
    defaultSettings with { OverlayX = double.PositiveInfinity }, defaultSettings with { OverlayX = -.01 },
    defaultSettings with { OverlayY = 1.01 }, defaultSettings with { Filter = "a\nb" },
    defaultSettings with { Filter = new string('a', 201) }, defaultSettings with { Filter = null! }
}) Check("invalid setting " + setting, () => Reject(setting.Validate));
var region = new RegionSettings { X = .1, Y = .2, Width = .1, Height = .05, ClientWidth = 1920, ClientHeight = 1080, Dpi = 96 };
Check("small normalized crop valid", region.Validate);
Check("client matches", () => True(region.MatchesClient(1920, 1080, 96)));
Check("resolution change requires recalibration", () => False(region.MatchesClient(2560, 1440, 96)));
Check("DPI change requires recalibration", () => False(region.MatchesClient(1920, 1080, 144)));
Check("region roundtrip", () => Equal(defaultSettings with { Region = region }, AppSettings.Parse((defaultSettings with { Region = region }).ToJson())));
foreach (var crop in new[]
{
    region with { X = -.1 }, region with { X = .95 }, region with { Y = .99 },
    region with { Width = 0 }, region with { Height = double.NaN },
    region with { Width = .8, X = 0 }, region with { Height = .8, Y = 0 },
    region with { Width = .001 }, region with { Dpi = 0 },
    region with { ClientWidth = 0 }, region with { ClientHeight = 50000 },
    region with { Width = .6, Height = .3, X = 0, Y = 0 }
}) Check("invalid crop " + crop, () => Reject(crop.Validate));

group = "Configuration file I/O";
string temporary = Path.Combine(Path.GetTempPath(), "ExecutesMiningScanner-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temporary);
try
{
    string config = Path.Combine(temporary, "settings.json");
    Check("missing config returns safe defaults without writing", () =>
    {
        var loaded = SettingsStore.Load(config);
        True(loaded.UsedDefaults);
        Equal(defaultSettings, loaded.Settings);
        Equal<string?>(null, loaded.Warning);
        False(File.Exists(config));
    });
    Check("atomic save and load", () =>
    {
        var expected = defaultSettings with { IntervalMs = 500, Region = region };
        SettingsStore.Save(config, expected);
        var loaded = SettingsStore.Load(config);
        False(loaded.UsedDefaults);
        Equal(expected, loaded.Settings);
        Equal(1, Directory.GetFiles(temporary).Length);
    });
    Check("invalid settings never overwrite valid file", () =>
    {
        string previous = File.ReadAllText(config);
        Reject(() => SettingsStore.Save(config, defaultSettings with { IntervalMs = -1 }));
        Equal(previous, File.ReadAllText(config));
    });
    Check("corrupt configuration preserved; safe fallback with warning", () =>
    {
        const string invalid = "{broken JSON";
        File.WriteAllText(config, invalid);
        var loaded = SettingsStore.Load(config);
        True(loaded.UsedDefaults);
        True(!string.IsNullOrWhiteSpace(loaded.Warning));
        Equal(defaultSettings, loaded.Settings);
        Equal(invalid, File.ReadAllText(config));
    });
    Check("oversized config rejected", () =>
    {
        File.WriteAllText(config, new string(' ', 1_048_577));
        True(SettingsStore.Load(config).UsedDefaults);
    });
    Check("invalid settings values return safe defaults", () =>
    {
        File.WriteAllText(config, "{\"schemaVersion\":1,\"intervalMs\":-1}");
        True(SettingsStore.Load(config).UsedDefaults);
    });
    Check("duplicate settings keys return safe defaults", () =>
    {
        File.WriteAllText(config, "{\"schemaVersion\":1,\"intervalMs\":250,\"intervalMs\":500}");
        True(SettingsStore.Load(config).UsedDefaults);
    });
    Check("invalid UTF8 rejected", () =>
    {
        File.WriteAllBytes(config, [0xff, 0xff, 0xff]);
        True(SettingsStore.Load(config).UsedDefaults);
    });
    Check("UTF8 BOM supported", () =>
    {
        File.WriteAllText(config, defaultSettings.ToJson(), new UTF8Encoding(true));
        False(SettingsStore.Load(config).UsedDefaults);
    });
    Check("signature atomic export and import", () =>
    {
        string tablePath = Path.Combine(temporary, "signatures.json");
        fixture.Save(tablePath);
        var loaded = SignatureTable.Load(tablePath);
        Equal(fixture.Entries[0], loaded.Entries[0]);
    });
}
finally
{
    // Only this newly created test-specific directory is deleted.
    Directory.Delete(temporary, recursive: true);
}

if (args.Length == 1)
{
    group = "Shipped reference validation";
    Check("explicit reference JSON path", () =>
    {
        var shipped = SignatureTable.Load(args[0]);
        True(shipped.Entries.Count > 0);
        foreach (var entry in shipped.Entries)
            True(shipped.Match(entry.BaseSignature, 12).Any(hit => hit.Entry.Id == entry.Id && hit.Count == 1));
    });
    Check("7170 matches a two-rock Gold-type cluster from shipped base 3585", () =>
    {
        var shipped = SignatureTable.Load(args[0]);
        var hits = shipped.Match(7170, 12);
        Equal(1, hits.Count);
        Equal("gold", hits[0].Entry.Id);
        Equal(3585, hits[0].Entry.BaseSignature);
        Equal(2, hits[0].Count);
    });
    Check("7170 cluster respects maximum count", () =>
    {
        var shipped = SignatureTable.Load(args[0]);
        Equal(0, shipped.Match(7170, 1).Count);
        Equal(2, shipped.Match(7170, 2).Single().Count);
    });
    Check("7170 cluster requires exact OCR value without numeric correction", () =>
    {
        var shipped = SignatureTable.Load(args[0]);
        False(shipped.Match(7169, 12).Any(hit => hit.Entry.Id == "gold"));
        False(shipped.Match(7171, 12).Any(hit => hit.Entry.Id == "gold"));
        False(shipped.Match(7178, 12).Any(hit => hit.Entry.Id == "gold"));
    });
}

foreach (var item in groups)
    Console.WriteLine($"PASS {item.Key}: {item.Value}");
foreach (string failure in failures)
    Console.Error.WriteLine("FAIL " + failure);
Console.WriteLine($"RESULT: {passed} passed, {failures.Count} failed. No screen or game-file access performed.");
return failures.Count == 0 ? 0 : 1;
