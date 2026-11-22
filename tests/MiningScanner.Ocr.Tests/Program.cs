using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using MiningScanner.Windows.Ocr;

// Synthetic test fixtures are drawn directly into RAM. No real capture is used or saved.
internal static class Program
{
    private static int _passed;
    private static int _failed;
    private static int _knownLimitations;

    private static int Main(string[] args)
    {
        var dataDirectory = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(AppContext.BaseDirectory, "tessdata");
        try
        {
            using var engine = new LocalOcr(dataDirectory);
            Check("plain black on white", () => ReadExpected(engine, "3885", 3885, Color.Black, Color.White));
            Check("bright HUD on dark background", () => ReadExpected(engine, "3885", 3885, Color.FromArgb(234, 242, 178), Color.FromArgb(16, 23, 28)));
            Check("comma thousands grouping", () => ReadExpected(engine, "12,855", 12855, Color.White, Color.FromArgb(22, 35, 38)));
            Check("period thousands grouping", () => ReadExpected(engine, "21.425", 21425, Color.Black, Color.White));
            Check("7170 cluster value", () => ReadExpected(engine, "7170", 7170, Color.White, Color.FromArgb(16, 23, 28)));
            foreach (var fontName in new[] { "Arial", "Segoe UI" })
            foreach (var fontSize in new[] { 14, 18, 24 })
            {
                var selectedFont = fontName;
                var selectedSize = fontSize;
                Check($"small cyan HUD 7170 {fontName} {fontSize}px", () =>
                {
                    using var picture = SyntheticHud("7170", selectedFont, selectedSize, false);
                    Expect(engine, picture, 7170);
                });
            }
            Check("grouped cyan HUD 7,170", () =>
            {
                using var picture = SyntheticHud("7,170", "Segoe UI", 18, false);
                Expect(engine, picture, 7170);
            });
            Check("cyan HUD with mild landscape variation", () =>
            {
                using var picture = SyntheticHud("7170", "Arial", 18, true);
                Expect(engine, picture, 7170);
            });
            Check("uncertain slashed-zero font rejected with reason", () =>
            {
                using var picture = SyntheticHud("7170", "Consolas", 24, false);
                var reading = engine.Read(picture, CancellationToken.None);
                Assert(reading.Value is null && reading.Uncertain && reading.Diagnostic == OcrDiagnostic.InsufficientAgreement,
                    $"Expected insufficient agreement; got {reading}.");
            });
            Check("invalid image bounds report a reason", () =>
            {
                using var picture = new Bitmap(9, 9);
                var reading = engine.Read(picture, CancellationToken.None);
                Assert(reading.Value is null && reading.Uncertain && reading.Diagnostic == OcrDiagnostic.InvalidRegion,
                    "Invalid bounds were not reported.");
            });
            // This exploratory fixture is deliberately separate from passing regression tests.
            // Tesseract's model can confidently confuse a tiny slashed zero with an eight.
            // Never hide that result by substituting a value from the signature table.
            using (var picture = SyntheticHud("7170", "Consolas", 14, false))
            {
                var reading = engine.Read(picture, CancellationToken.None);
                if (reading.Value.HasValue && reading.Value != 7170)
                {
                    _knownLimitations++;
                    Console.WriteLine($"KNOWN LIMITATION (not a pass): synthetic Consolas 14px 7170 read as {reading.Value}, confidence {reading.Confidence:F3}.");
                }
            }
            Check("blank white rejected", () => CheckBlank(engine, Color.White));
            Check("blank black rejected", () => CheckBlank(engine, Color.Black));
            Check("low contrast rejected", () =>
            {
                using var picture = Synthetic("3885", Color.FromArgb(107, 107, 107), Color.FromArgb(100, 100, 100));
                var reading = engine.Read(picture, CancellationToken.None);
                Assert(reading.Value is null, "Low contrast produced a value.");
            });
            Check("malformed grouped digits rejected", () =>
            {
                using var picture = Synthetic("12,34", Color.Black, Color.White);
                var reading = engine.Read(picture, CancellationToken.None);
                Assert(reading.Value is null, "Malformed grouping produced a value.");
            });
            Check("cancelled before evaluation", () =>
            {
                using var picture = Synthetic("3885", Color.Black, Color.White);
                using var cancellation = new CancellationTokenSource();
                cancellation.Cancel();
                try { engine.Read(picture, cancellation.Token); }
                catch (OperationCanceledException) { return; }
                throw new Exception("Cancellation was ignored.");
            });
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"OCR initialization/test infrastructure failed: {exception.GetType().Name}: {exception.Message}");
            return 2;
        }

        // Exercise actual initialization and deterministic destruction on a dedicated worker.
        Check("native OCR worker closes cleanly", () =>
        {
            Exception? failure = null;
            var worker = new Thread(() =>
            {
                try
                {
                    using var engine = new LocalOcr(dataDirectory);
                    using var picture = Synthetic("3885", Color.Black, Color.White);
                    Assert(engine.Read(picture, CancellationToken.None).Value == 3885, "Worker did not recognize fixture.");
                }
                catch (Exception exception) { failure = exception; }
            }) { IsBackground = false, Name = "Synthetic-OCR-test" };
            worker.Start();
            Assert(worker.Join(TimeSpan.FromSeconds(20)), "Worker did not exit within 20 seconds.");
            if (failure is not null) throw failure;
            Assert(!worker.IsAlive, "Worker is still running after Join.");
        });

        Console.WriteLine($"Synthetic OCR tests: {_passed} passed, {_failed} failed; {_knownLimitations} known model limitations observed separately. No screenshots or test images were written.");
        return _failed == 0 ? 0 : 1;
    }

    private static void ReadExpected(LocalOcr engine, string text, int expected, Color ink, Color paper)
    {
        using var picture = Synthetic(text, ink, paper);
        Expect(engine, picture, expected);
    }

    private static void Expect(LocalOcr engine, Bitmap picture, int expected)
    {
        var reading = engine.Read(picture, CancellationToken.None);
        Assert(reading.Value == expected && !reading.Uncertain && reading.Confidence >= 0.80,
            $"Expected {expected}; got {reading.Value?.ToString() ?? "null"}, confidence {reading.Confidence:F3}, uncertain {reading.Uncertain}.");
    }


    private static Bitmap SyntheticHud(string text, string fontName, int fontSize, bool landscape)
    {
        using var font = new Font(fontName, fontSize, FontStyle.Regular, GraphicsUnit.Pixel);
        using var measure = new Bitmap(1, 1);
        SizeF extent;
        using (var graphics = Graphics.FromImage(measure))
            extent = graphics.MeasureString(text, font, int.MaxValue, StringFormat.GenericTypographic);
        var picture = new Bitmap((int)Math.Ceiling(extent.Width) + 8, fontSize + 8, PixelFormat.Format24bppRgb);
        using var canvas = Graphics.FromImage(picture);
        canvas.Clear(Color.FromArgb(13, 22, 29));
        if (landscape)
        {
            using var gradient = new LinearGradientBrush(new Rectangle(Point.Empty, picture.Size),
                Color.FromArgb(11, 16, 19), Color.FromArgb(71, 61, 40), 23f);
            canvas.FillRectangle(gradient, new Rectangle(Point.Empty, picture.Size));
            using var rock = new SolidBrush(Color.FromArgb(42, 43, 31));
            canvas.FillEllipse(rock, picture.Width / 3, -5, picture.Width, picture.Height * 2);
        }
        canvas.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var ink = new SolidBrush(Color.FromArgb(155, 224, 233));
        canvas.DrawString(text, font, ink, 4, 2, StringFormat.GenericTypographic);
        return picture;
    }

    private static void CheckBlank(LocalOcr engine, Color paper)
    {
        using var picture = new Bitmap(240, 64, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(picture)) graphics.Clear(paper);
        var reading = engine.Read(picture, CancellationToken.None);
        Assert(reading.Value is null && !reading.Uncertain && reading.Diagnostic == OcrDiagnostic.NoContent,
            "Blank pixels did not return an empty result with a reason.");
    }

    private static Bitmap Synthetic(string text, Color ink, Color paper)
    {
        var picture = new Bitmap(280, 64, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(picture);
        graphics.Clear(paper);
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var font = new Font("Arial", 36, FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(ink);
        graphics.DrawString(text, font, brush, 12, 8, StringFormat.GenericTypographic);
        return picture;
    }

    private static void Check(string name, Action test)
    {
        try { test(); _passed++; Console.WriteLine($"PASS {name}"); }
        catch (Exception exception) { _failed++; Console.Error.WriteLine($"FAIL {name}: {exception.Message}"); }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
