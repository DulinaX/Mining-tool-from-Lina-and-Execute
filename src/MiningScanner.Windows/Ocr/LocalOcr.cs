using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using MiningScanner.Core;
using Tesseract;

namespace MiningScanner.Windows.Ocr;

/// <summary>
/// Offline Tesseract OCR. Create, read and dispose on one dedicated worker thread.
/// Pixels and encoded image bytes never leave memory. No file renderer or image output is used.
/// Cancellation is cooperative between native OCR calls; the caller must also invalidate its
/// capture generation after focus loss, and must never display a cancelled generation.
/// </summary>
public sealed class LocalOcr : IOcrEngine
{
    private readonly TesseractEngine _engine;
    private readonly double _minimumConfidence;
    private readonly int _threadId = Environment.CurrentManagedThreadId;
    private bool _disposed;

    /// <summary>
    /// Exi &amp; Lina: Schneller Abbruch. Liefert eine Bildvariante eine sichere Zahl, für die diese Prüfung "true" ergibt
    /// (z. B. "passt zur Erz-Liste"), werden die restlichen Varianten übersprungen.
    /// </summary>
    public Func<int, bool>? AcceptEarly { get; set; }

    public LocalOcr(string? tessdataDirectory = null, double minimumConfidence = 0.80)
    {
        if (!double.IsFinite(minimumConfidence) || minimumConfidence < 0.5 || minimumConfidence > 1)
            throw new ArgumentOutOfRangeException(nameof(minimumConfidence));

        _minimumConfidence = minimumConfidence;
        // This is an application resource path, never a game directory or a user-selected image.
        var resourceDirectory = Path.GetFullPath(tessdataDirectory ?? Path.Combine(AppContext.BaseDirectory, "tessdata"));
        // Override inherited OCR paths before the first native-library load. These changes affect
        // only this process; no registry/user/system environment or external application is changed.
        Environment.SetEnvironmentVariable("TESSDATA_PREFIX", resourceDirectory, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("OMP_THREAD_LIMIT", "1", EnvironmentVariableTarget.Process);
        _engine = new TesseractEngine(resourceDirectory,
            "eng", EngineMode.LstmOnly);
        try
        {
            RequireVariable("tessedit_char_whitelist", "0123456789.,' ");
            RequireVariable("tessedit_write_images", "0");
            RequireVariable("debug_file", "NUL");
            RequireVariable("user_defined_dpi", "150");
            // Disable optional adaptive behavior. No language/word correction is performed here.
            _engine.SetVariable("classify_enable_learning", "0");
            _engine.SetVariable("classify_enable_adaptive_matcher", "0");
        }
        catch
        {
            _engine.Dispose();
            throw;
        }
    }

    public OcrReading Read(Bitmap bitmap, CancellationToken cancellation)
    {
        VerifyThread();
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(bitmap);
        cancellation.ThrowIfCancellationRequested();
        if (bitmap.Width < 10 || bitmap.Height < 8 || bitmap.Width > 2048 || bitmap.Height > 512 ||
            (long)bitmap.Width * bitmap.Height > 524288)
            return new(null, 0, true, OcrDiagnostic.InvalidRegion);

        using var prepared = PreparedImage.Create(bitmap, cancellation);
        if (!prepared.HasContent)
            return OcrReading.Empty;

        var confident = new List<(int Value, double Confidence)>();
        var readings = new List<OcrReading>();
        for (var variant = 0; variant < 4; variant++)
        {
            cancellation.ThrowIfCancellationRequested();
            using var processed = prepared.Render(variant, cancellation);
            var read = ReadVariant(processed, cancellation);
            readings.Add(read);
            if (!read.Uncertain && read.Value.HasValue && read.Confidence >= _minimumConfidence)
            {
                confident.Add((read.Value.Value, read.Confidence));
                if (AcceptEarly is { } accept && accept(read.Value.Value))
                {
                    var early = readings.Where(x => x.Value.HasValue).Select(x => (x.Value!.Value, x.Confidence)).ToList();
                    return new(read.Value.Value, read.Confidence, false, OcrDiagnostic.None, early);
                }
            }
        }
        // Alle gelesenen Zahlen (auch unsichere) als Kandidaten für den Abgleich mit der Erz-Liste.
        var candidates = readings.Where(x => x.Value.HasValue).Select(x => (x.Value!.Value, x.Confidence)).ToList();
        cancellation.ThrowIfCancellationRequested();

        // Exi & Lina: Mehrheitsentscheid statt "alle müssen gleich sein".
        // Eine sichere Lesung reicht, solange keine andere sichere Lesung widerspricht.
        // Die Tabelle wird weiterhin nie benutzt, um eine Zahl zu "reparieren".
        if (confident.Count == 0)
        {
            var diagnostic = readings.Any(x => x.Diagnostic == OcrDiagnostic.LowConfidence) ? OcrDiagnostic.LowConfidence : OcrDiagnostic.NoReadableNumber;
            return new(null, readings.Max(x => x.Confidence), true, diagnostic, candidates);
        }
        var groups = confident.GroupBy(x => x.Value)
            .Select(g => (Value: g.Key, Count: g.Count(), Best: g.Max(x => x.Confidence)))
            .OrderByDescending(g => g.Count).ThenByDescending(g => g.Best).ToList();
        if (groups.Count > 1 && groups[1].Count == groups[0].Count)
            return new(null, groups[0].Best, true, OcrDiagnostic.ConflictingReadings, candidates);
        return new(groups[0].Value, groups[0].Best, false, OcrDiagnostic.None, candidates);
    }

    private OcrReading ReadVariant(Bitmap image, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        using var memory = new MemoryStream();
        try
        {
            image.Save(memory, System.Drawing.Imaging.ImageFormat.Png); // RAM stream only; never a filename.
            var encoded = memory.ToArray();
            try
            {
                cancellation.ThrowIfCancellationRequested();
                using var pix = Pix.LoadFromMemory(encoded);
                cancellation.ThrowIfCancellationRequested();
                using var page = _engine.Process(pix, PageSegMode.SingleLine);
                var text = page.GetText();
                cancellation.ThrowIfCancellationRequested();
                if (!NumberNormalizer.TryNormalize(text, out var value))
                    return new(null, 0, true, OcrDiagnostic.NoReadableNumber);

                var confidence = (double)page.GetMeanConfidence();
                using var iterator = page.GetIterator();
                iterator.Begin();
                do
                {
                    var word = iterator.GetText(PageIteratorLevel.Word);
                    if (!string.IsNullOrWhiteSpace(word))
                        confidence = Math.Min(confidence, iterator.GetConfidence(PageIteratorLevel.Word) / 100.0);
                } while (iterator.Next(PageIteratorLevel.Word));
                cancellation.ThrowIfCancellationRequested();
                if (!double.IsFinite(confidence) || confidence < _minimumConfidence)
                    return new(value, double.IsFinite(confidence) ? Math.Max(0, confidence) : 0, true, OcrDiagnostic.LowConfidence);
                return new(value, confidence, false);
            }
            finally
            {
                Array.Clear(encoded);
            }
        }
        finally
        {
            if (memory.TryGetBuffer(out var buffer) && buffer.Array is not null)
                Array.Clear(buffer.Array);
        }
    }

    private void RequireVariable(string name, string value)
    {
        if (!_engine.SetVariable(name, value))
            throw new InvalidOperationException($"OCR-Einstellung konnte nicht gesetzt werden: {name}.");
    }

    private void VerifyThread()
    {
        if (Environment.CurrentManagedThreadId != _threadId)
            throw new InvalidOperationException("Die OCR muss auf ihrem eigenen Worker-Thread verwendet werden.");
    }

    public void Dispose()
    {
        VerifyThread();
        if (_disposed) return;
        _disposed = true;
        _engine.Dispose();
    }

    private sealed class PreparedImage : IDisposable
    {
        private readonly byte[] _pixels;
        private readonly int _width;
        private readonly int _height;
        private readonly int _threshold;
        public bool HasContent { get; }

        private PreparedImage(byte[] pixels, int width, int height, int threshold, bool hasContent)
        {
            _pixels = pixels;
            _width = width;
            _height = height;
            _threshold = threshold;
            HasContent = hasContent;
        }

        public static PreparedImage Create(Bitmap input, CancellationToken cancellation)
        {
            // Keep short numeric HUD crops legible without creating a full-screen-sized buffer.
            var scale = Math.Clamp(96.0 / input.Height, 1.0, 6.0);
            if (input.Width * scale > 2000) scale = Math.Max(1.0, 2000.0 / input.Width);
            var width = Math.Max(1, (int)Math.Round(input.Width * scale));
            var height = Math.Max(1, (int)Math.Round(input.Height * scale));
            using var scaled = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(scaled))
            {
                graphics.Clear(Color.Black);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(input, new Rectangle(0, 0, width, height));
            }

            var pixels = new byte[width * height];
            var histogram = new int[256];
            var data = scaled.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly,
                PixelFormat.Format24bppRgb);
            var row = new byte[Math.Abs(data.Stride)];
            try
            {
                for (var y = 0; y < height; y++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                    for (var x = 0; x < width; x++)
                    {
                        var at = x * 3;
                        var luma = (byte)((row[at + 2] * 77 + row[at + 1] * 150 + row[at] * 29) >> 8);
                        pixels[y * width + x] = luma;
                        histogram[luma]++;
                    }
                }
            }
            catch
            {
                Array.Clear(pixels);
                throw;
            }
            finally
            {
                Array.Clear(row);
                scaled.UnlockBits(data);
            }

            var low = Percentile(histogram, pixels.Length, 0.01);
            var high = Percentile(histogram, pixels.Length, 0.99);
            if (high - low < 24)
                return new(pixels, width, height, 127, false);

            var median = Percentile(histogram, pixels.Length, 0.50);
            var invert = median < (low + high) / 2;
            Array.Clear(histogram);
            for (var i = 0; i < pixels.Length; i++)
            {
                var value = Math.Clamp((pixels[i] - low) * 255 / (high - low), 0, 255);
                pixels[i] = (byte)(invert ? 255 - value : value);
                histogram[pixels[i]]++;
            }
            var threshold = Otsu(histogram, pixels.Length);
            var dark = histogram.Take(threshold + 1).Sum();
            var ratio = (double)dark / pixels.Length;
            return new(pixels, width, height, threshold, ratio is >= 0.003 and <= 0.65);
        }

        public Bitmap Render(int variant, CancellationToken cancellation)
        {
            const int padding = 12;
            var output = new Bitmap(_width + padding * 2, _height + padding * 2, PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(output)) graphics.Clear(Color.White);
            var data = output.LockBits(new Rectangle(padding, padding, _width, _height),
                ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            var row = new byte[Math.Abs(data.Stride)];
            try
            {
                var threshold = variant == 2 ? Math.Clamp(_threshold + 24, 16, 239) : variant == 3 ? Math.Clamp(_threshold - 24, 16, 239) : _threshold;
                for (var y = 0; y < _height; y++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    for (var x = 0; x < _width; x++)
                    {
                        var pixel = _pixels[y * _width + x];
                        var value = variant == 0 ? pixel : (byte)(pixel <= threshold ? 0 : 255);
                        row[x * 3] = row[x * 3 + 1] = row[x * 3 + 2] = value;
                    }
                    // Only the selected rectangle is written; padding remains white.
                    Marshal.Copy(row, 0, IntPtr.Add(data.Scan0, y * data.Stride), _width * 3);
                }
                return output;
            }
            catch
            {
                output.UnlockBits(data);
                output.Dispose();
                data = null!;
                throw;
            }
            finally
            {
                Array.Clear(row);
                if (data is not null) output.UnlockBits(data);
            }
        }

        private static int Percentile(int[] histogram, int total, double percentile)
        {
            var target = Math.Max(1, (int)Math.Ceiling(total * percentile));
            var count = 0;
            for (var i = 0; i < histogram.Length; i++)
            {
                count += histogram[i];
                if (count >= target) return i;
            }
            return 255;
        }

        private static int Otsu(int[] histogram, int total)
        {
            long sum = 0;
            for (var i = 0; i < 256; i++) sum += (long)i * histogram[i];
            long backgroundSum = 0;
            var backgroundCount = 0;
            var best = -1.0;
            var threshold = 127;
            for (var i = 0; i < 255; i++)
            {
                backgroundCount += histogram[i];
                backgroundSum += (long)i * histogram[i];
                if (backgroundCount == 0) continue;
                var foregroundCount = total - backgroundCount;
                if (foregroundCount == 0) break;
                var distance = (double)backgroundSum / backgroundCount - (double)(sum - backgroundSum) / foregroundCount;
                var score = (double)backgroundCount * foregroundCount * distance * distance;
                if (score > best) { best = score; threshold = i; }
            }
            return threshold;
        }

        public void Dispose() => Array.Clear(_pixels);
    }
}
