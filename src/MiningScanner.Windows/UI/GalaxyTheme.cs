using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace MiningScanner.Windows.UI;

// Galaxy-Design für "Exi und Lina Mining Tool": Hintergrund, Glas-Karten und leuchtende Buttons.
internal static class Galaxy
{
    public static readonly Color Text = Color.FromArgb(236, 230, 255);
    public static readonly Color SoftText = Color.FromArgb(190, 178, 232);
    public static readonly Color Caption = Color.FromArgb(178, 150, 245);
    public static readonly Color Cyan = Color.FromArgb(130, 232, 255);
    public static readonly Color Pink = Color.FromArgb(255, 150, 230);
    public static readonly Color Amber = Color.FromArgb(255, 196, 110);
    public static readonly Color Green = Color.FromArgb(120, 240, 190);
    public static readonly Color Deep = Color.FromArgb(10, 6, 28);
    public static readonly Color Field = Color.FromArgb(26, 18, 58);

    public static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
        if (d <= 0.5f) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    private static void Glow(Graphics g, float cx, float cy, float rx, float ry, Color c)
    {
        if (rx < 1f || ry < 1f) return;
        using var path = new GraphicsPath();
        path.AddEllipse(cx - rx, cy - ry, rx * 2f, ry * 2f);
        using var pb = new PathGradientBrush(path) { CenterColor = c, SurroundColors = [Color.FromArgb(0, c)] };
        g.FillPath(pb, path);
    }

    public static Bitmap Render(int w, int h, float ui)
    {
        w = Math.Max(20, w); h = Math.Max(20, h);
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var bg = new LinearGradientBrush(new Rectangle(0, 0, w, h), Color.Black, Color.Black, 70f))
        {
            bg.InterpolationColors = new ColorBlend
            {
                Colors = [Color.FromArgb(4, 3, 16), Color.FromArgb(17, 8, 44), Color.FromArgb(7, 13, 40)],
                Positions = [0f, 0.55f, 1f]
            };
            g.FillRectangle(bg, 0, 0, w, h);
        }

        // Nebel
        Glow(g, w * 0.85f, h * 0.12f, w * 0.60f, h * 0.38f, Color.FromArgb(115, 140, 40, 190));
        Glow(g, w * 0.05f, h * 0.60f, w * 0.55f, h * 0.45f, Color.FromArgb(95, 30, 70, 200));
        Glow(g, w * 0.55f, h * 0.65f, w * 0.60f, h * 0.12f, Color.FromArgb(65, 220, 70, 170));
        Glow(g, w * 0.30f, h * 0.18f, w * 0.30f, h * 0.18f, Color.FromArgb(50, 40, 190, 220));
        Glow(g, w * 0.92f, h * 0.92f, w * 0.45f, h * 0.32f, Color.FromArgb(85, 100, 30, 160));

        // Milchstraßen-Band
        var st = g.Save();
        g.TranslateTransform(w * 0.5f, h * 0.5f);
        g.RotateTransform(-22f);
        Glow(g, 0, 0, w * 0.85f, h * 0.09f, Color.FromArgb(50, 200, 180, 255));
        Glow(g, 0, 0, w * 0.50f, h * 0.035f, Color.FromArgb(40, 255, 230, 255));
        g.Restore(st);

        // Sterne (fester Startwert, damit sie beim Neuzeichnen gleich bleiben)
        var rnd = new Random(20261116);
        Color[] tints = [Color.White, Color.FromArgb(200, 215, 255), Color.FromArgb(255, 205, 240), Color.FromArgb(185, 255, 250), Color.FromArgb(255, 240, 200)];
        int count = w * h / 1000;
        for (int i = 0; i < count; i++)
        {
            float x = (float)(rnd.NextDouble() * w), y = (float)(rnd.NextDouble() * h);
            double roll = rnd.NextDouble();
            float s = (roll < 0.82 ? 0.9f : roll < 0.96 ? 1.5f : 2.3f) * ui;
            var tint = tints[rnd.Next(tints.Length)];
            using (var sb = new SolidBrush(Color.FromArgb(70 + rnd.Next(186), tint))) g.FillEllipse(sb, x, y, s, s);
            if (roll >= 0.986)
            {
                float cx = x + s / 2f, cy = y + s / 2f;
                Glow(g, cx, cy, 7f * ui, 7f * ui, Color.FromArgb(90, tint));
                using var p = new Pen(Color.FromArgb(110, tint), 1f);
                g.DrawLine(p, cx - 9f * ui, cy, cx + 9f * ui, cy);
                g.DrawLine(p, cx, cy - 9f * ui, cx, cy + 9f * ui);
            }
        }

        return bmp;
    }

    // ---------- Animierter Planet (wird nicht ins Hintergrundbild gebacken, sondern live gezeichnet) ----------

    /// <summary>Grundposition des Planeten (oben rechts) für eine Fenstergröße.</summary>
    public static PointF PlanetHome(int w, float ui) => new(w - 64f * ui, 58f * ui);

    /// <summary>Bereich, der sich durch die Animation verändern kann (zum gezielten Neuzeichnen).</summary>
    public static Rectangle PlanetArea(int w, float ui)
    {
        var c = PlanetHome(w, ui); float pr = 30f * ui;
        return Rectangle.Round(new RectangleF(c.X - pr * 2.9f, c.Y - pr * 2.5f, pr * 5.8f, pr * 5.0f));
    }

    /// <summary>Zeichnet Planet, Ring und kleinen Mond. t = Zeit in Sekunden.</summary>
    public static void DrawPlanet(Graphics g, int w, float ui, double t)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var home = PlanetHome(w, ui);
        float pr = 30f * ui;
        float px = home.X + (float)Math.Sin(t * 0.45) * 3f * ui;
        float py = home.Y + (float)Math.Sin(t * 0.8) * 4f * ui;
        float tilt = -18f + (float)Math.Sin(t * 0.35) * 7f;
        double moonAngle = t * 0.9;
        float mx = px + (float)Math.Cos(moonAngle) * pr * 2.35f;
        float my = py + (float)Math.Sin(moonAngle) * pr * 0.75f - (float)Math.Cos(moonAngle) * pr * 0.35f;
        bool moonBehind = Math.Sin(moonAngle) < 0;
        float mr = 5.5f * ui;

        Glow(g, px, py, pr * 2.2f, pr * 2.2f, Color.FromArgb(60, 160, 90, 255));
        if (moonBehind) DrawMoon(g, mx, my, mr);
        DrawRings(g, px, py, pr, ui, tilt, t, back: true);
        using (var planet = new GraphicsPath())
        {
            planet.AddEllipse(px - pr, py - pr, pr * 2f, pr * 2f);
            // Lichtpunkt wandert leicht -> wirkt, als würde sich der Planet drehen
            float sx = (float)Math.Sin(t * 0.3) * 0.12f;
            using var pb = new PathGradientBrush(planet)
            {
                CenterPoint = new PointF(px - pr * (0.35f + sx), py - pr * 0.4f),
                CenterColor = Color.FromArgb(255, 235, 170, 255),
                SurroundColors = [Color.FromArgb(255, 40, 20, 90)]
            };
            g.FillPath(pb, planet);
            // zarte Wolkenbänder
            var clip = g.Save();
            g.SetClip(planet);
            using (var band = new Pen(Color.FromArgb(40, 255, 220, 255), 3f * ui))
            {
                float shift = (float)((t * 6.0) % (pr * 2.0));
                for (int i = -1; i <= 1; i++)
                {
                    float y = py + i * pr * 0.45f;
                    g.DrawArc(band, px - pr * 1.6f + shift - pr, y - pr * 0.2f, pr * 3.2f, pr * 0.4f, 0, 180);
                }
            }
            // Schatten des Rings auf dem Planeten
            var shadowState = g.Save();
            g.TranslateTransform(px, py + pr * 0.10f);
            g.RotateTransform(tilt);
            using (var shadow = new Pen(Color.FromArgb(70, 10, 0, 30), pr * 0.16f))
                g.DrawArc(shadow, -pr * 1.7f, -pr * 0.42f, pr * 3.4f, pr * 0.84f, 0, 180);
            g.Restore(shadowState);
            g.Restore(clip);
        }
        DrawRings(g, px, py, pr, ui, tilt, t, back: false);
        if (!moonBehind) DrawMoon(g, mx, my, mr);
    }

    private static void DrawMoon(Graphics g, float x, float y, float r)
    {
        Glow(g, x, y, r * 2.4f, r * 2.4f, Color.FromArgb(55, 160, 220, 255));
        using var path = new GraphicsPath();
        path.AddEllipse(x - r, y - r, r * 2f, r * 2f);
        using var pb = new PathGradientBrush(path)
        {
            CenterPoint = new PointF(x - r * 0.4f, y - r * 0.4f),
            CenterColor = Color.FromArgb(255, 220, 240, 255),
            SurroundColors = [Color.FromArgb(255, 70, 90, 150)]
        };
        g.FillPath(pb, path);
    }

    // Ringbänder: (innen, außen) als Vielfache des Planetenradius, Farbe innen/außen
    private static readonly (float Inner, float Outer, Color A, Color B)[] RingBands =
    [
        (1.30f, 1.48f, Color.FromArgb(70, 170, 140, 255), Color.FromArgb(40, 120, 90, 220)),
        (1.50f, 1.80f, Color.FromArgb(150, 255, 175, 235), Color.FromArgb(120, 200, 140, 255)),
        // Lücke (wie die Cassini-Teilung beim Saturn)
        (1.88f, 2.12f, Color.FromArgb(120, 150, 220, 255), Color.FromArgb(80, 190, 160, 255)),
        (2.15f, 2.26f, Color.FromArgb(55, 230, 210, 255), Color.FromArgb(25, 160, 140, 255)),
    ];

    private const float RingFlatten = 0.26f;

    /// <summary>Zeichnet mehrfarbige Ringbänder mit Lücke, Glanz und funkelnden Ringteilchen (hintere oder vordere Hälfte).</summary>
    private static void DrawRings(Graphics g, float px, float py, float pr, float ui, float tilt, double t, bool back)
    {
        var state = g.Save();
        g.TranslateTransform(px, py);
        g.RotateTransform(tilt);
        float big = pr * 4f;
        // hintere Hälfte = obere Hälfte der Ellipse (liegt hinter dem Planeten)
        g.SetClip(back ? new RectangleF(-big, -big, big * 2, big) : new RectangleF(-big, 0, big * 2, big), CombineMode.Intersect);

        foreach (var band in RingBands)
        {
            float ro = band.Outer * pr, ri = band.Inner * pr;
            using var path = new GraphicsPath(FillMode.Alternate);
            path.AddEllipse(-ro, -ro * RingFlatten, ro * 2, ro * 2 * RingFlatten);
            path.AddEllipse(-ri, -ri * RingFlatten, ri * 2, ri * 2 * RingFlatten);
            using var brush = new LinearGradientBrush(new RectangleF(-ro, -ro, ro * 2, ro * 2), band.A, band.B, 0f)
            {
                // Mitte heller, Ränder dunkler -> wirkt wie Licht von vorne
                InterpolationColors = new ColorBlend
                {
                    Colors = [Shade(band.B, 0.55f), band.A, Shade(band.A, 1.15f), band.A, Shade(band.B, 0.55f)],
                    Positions = [0f, 0.3f, 0.5f, 0.7f, 1f]
                }
            };
            g.FillPath(brush, path);
        }

        // Wandernder Glanz auf dem Hauptband
        double shineAngle = t * 0.6;
        float sr = 1.65f * pr;
        float sx = (float)Math.Cos(shineAngle) * sr, sy = (float)Math.Sin(shineAngle) * sr * RingFlatten;
        if ((sy < 0) == back)
            Glow(g, sx, sy, pr * 0.55f, pr * 0.16f, Color.FromArgb(120, 255, 240, 255));

        // Funkelnde Ringteilchen (Eis/Staub), die langsam um den Planeten kreisen
        for (int i = 0; i < 36; i++)
        {
            double a = i * 2.39996 + t * (0.25 + (i % 5) * 0.03);
            float rad = pr * (1.35f + (i * 37 % 90) / 100f);
            float x = (float)Math.Cos(a) * rad, y = (float)Math.Sin(a) * rad * RingFlatten;
            if ((y < 0) != back) continue;
            float twinkle = (float)((Math.Sin(t * 3 + i * 1.7) + 1) / 2);
            float size = (0.8f + 1.4f * twinkle) * ui;
            using var dot = new SolidBrush(Color.FromArgb((int)(90 + 165 * twinkle), 255, 245, 255));
            g.FillEllipse(dot, x - size / 2, y - size / 2, size, size);
        }

        // feine helle Kante am äußeren Rand
        using (var edge = new Pen(Color.FromArgb(back ? 60 : 120, 240, 225, 255), 1f * ui))
        {
            float ro = RingBands[^1].Outer * pr;
            g.DrawEllipse(edge, -ro, -ro * RingFlatten, ro * 2, ro * 2 * RingFlatten);
        }
        g.Restore(state);
    }

    private static Color Shade(Color c, float f) =>
        Color.FromArgb(c.A, Math.Clamp((int)(c.R * f), 0, 255), Math.Clamp((int)(c.G * f), 0, 255), Math.Clamp((int)(c.B * f), 0, 255));

    public static Icon MakeIcon()
    {
        using var b = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(b))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            PointF[] gem = [new(16, 2), new(29, 12), new(16, 30), new(3, 12)];
            using (var br = new LinearGradientBrush(new Rectangle(0, 0, 32, 32), Color.FromArgb(255, 120, 230), Color.FromArgb(90, 200, 255), 45f)) g.FillPolygon(br, gem);
            using var p = new Pen(Color.FromArgb(230, 255, 255, 255), 1.5f);
            g.DrawPolygon(p, gem);
            g.DrawLine(p, 3, 12, 29, 12);
        }
        return Icon.FromHandle(b.GetHicon());
    }
}

// Fenster mit Galaxy-Hintergrund
internal class GalaxyForm : Form
{
    private Bitmap? _galaxy;
    private readonly System.Windows.Forms.Timer _animation = new() { Interval = 33 };
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    /// <summary>Planet oben rechts animieren (schweben, Ring kippen, Mond kreist).</summary>
    protected bool AnimatePlanet { get; set; } = true;
    /// <summary>Hintergrund animieren (funkelnde Sterne, Sternschnuppen).</summary>
    protected bool AnimateBackground { get; set; } = true;

    private readonly record struct Twinkle(PointF Pos, float Size, float Phase, float Speed, Color Tint);
    private sealed record Shooting(double Start, double Duration, PointF From, PointF Dir, float Length, float Tail);
    private Twinkle[] _twinkles = [];
    private Shooting? _shooting;
    private Rectangle _lastShootRect;
    private double _nextShootAt = 3;
    private readonly Random _rnd = new();
    private int _frame;
    private readonly System.Windows.Forms.Timer _rebuild = new() { Interval = 120 };
    protected float Ui { get; private set; } = 1f;

    public GalaxyForm()
    {
        DoubleBuffered = true;
        BackColor = Galaxy.Deep;
        ForeColor = Galaxy.Text;
        _rebuild.Tick += (_, _) => { _rebuild.Stop(); RebuildGalaxy(); };
        _animation.Tick += (_, _) => AnimateFrame();
        VisibleChanged += (_, _) => { if (Visible) _animation.Start(); else _animation.Stop(); };
        try { Icon = Galaxy.MakeIcon(); } catch { }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        GalaxyChrome.Apply(this);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Ui = Math.Max(1f, DeviceDpi / 96f);
        RebuildGalaxy();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        Ui = Math.Max(1f, e.DeviceDpiNew / 96f);
        _rebuild.Stop(); _rebuild.Start();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _rebuild.Stop(); _rebuild.Start();
    }

    private void AnimateFrame()
    {
        if (!Visible || WindowState == FormWindowState.Minimized || _galaxy is null) return;
        double t = _clock.Elapsed.TotalSeconds;
        _frame++;
        if (AnimatePlanet) InvalidateArea(Galaxy.PlanetArea(ClientSize.Width, Ui));
        if (!AnimateBackground) return;

        // Funkelnde Sterne: nur ihre kleinen Bereiche neu zeichnen (15 Bilder pro Sekunde reichen)
        if (_frame % 2 == 0)
            foreach (var star in _twinkles) InvalidateArea(StarRect(star));

        // Sternschnuppe ab und zu
        if (_shooting is null && t >= _nextShootAt) SpawnShootingStar(t);
        if (_shooting is { } sh)
        {
            var rect = ShootRect(sh, t);
            InvalidateArea(_lastShootRect.IsEmpty ? rect : Rectangle.Union(rect, _lastShootRect));
            _lastShootRect = rect;
            if (t > sh.Start + sh.Duration + 0.35)
            {
                _shooting = null;
                InvalidateArea(_lastShootRect);
                _lastShootRect = Rectangle.Empty;
                _nextShootAt = t + 5 + _rnd.NextDouble() * 9;
            }
        }
    }

    /// <summary>Undurchsichtige Steuerelemente (Listen, Textfelder, Bilder) zeichnen ihren eigenen Hintergrund – die nie neu zeichnen lassen, sonst flackern sie.</summary>
    private static bool IsOpaque(Control c) => c is ListView or TextBoxBase or PictureBox or ComboBox or UpDownBase or DataGridView or ListBox;

    /// <summary>Nur das Fenster und durchsichtige Elemente in diesem Bereich neu zeichnen – Listen usw. bleiben in Ruhe.</summary>
    private void InvalidateArea(Rectangle area)
    {
        Invalidate(area, false);
        InvalidateTransparentChildren(this, area);
    }

    private static void InvalidateTransparentChildren(Control parent, Rectangle areaInParent)
    {
        foreach (Control child in parent.Controls)
        {
            if (!child.Visible || IsOpaque(child)) continue;
            var hit = Rectangle.Intersect(areaInParent, child.Bounds);
            if (hit.IsEmpty) continue;
            hit.Offset(-child.Left, -child.Top);
            child.Invalidate(hit, false);
            InvalidateTransparentChildren(child, hit);
        }
    }

    /// <summary>Bereiche, die von undurchsichtigen Elementen verdeckt sind (dort lohnt kein funkelnder Stern).</summary>
    private List<Rectangle> OpaqueAreas()
    {
        var list = new List<Rectangle>();
        void Walk(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                if (!c.Visible && !(c is ListView)) continue;
                if (IsOpaque(c)) list.Add(RectangleToClient(c.RectangleToScreen(c.ClientRectangle)));
                else Walk(c);
            }
        }
        if (IsHandleCreated) Walk(this);
        return list;
    }

    private Rectangle StarRect(Twinkle s)
    {
        float r = 11f * Ui;
        return Rectangle.Round(new RectangleF(s.Pos.X - r, s.Pos.Y - r, r * 2, r * 2));
    }

    private void SpawnShootingStar(double t)
    {
        var cs = ClientSize;
        float angle = (float)((18 + _rnd.NextDouble() * 20) * Math.PI / 180);
        var dir = new PointF((float)Math.Cos(angle), (float)Math.Sin(angle));
        var from = new PointF((float)(cs.Width * (0.05 + _rnd.NextDouble() * 0.55)), (float)(cs.Height * (0.03 + _rnd.NextDouble() * 0.45)));
        _shooting = new Shooting(t, 0.9 + _rnd.NextDouble() * 0.6, from, dir, (float)(cs.Width * (0.3 + _rnd.NextDouble() * 0.25)), (90f + (float)_rnd.NextDouble() * 60f) * Ui);
    }

    private static (PointF Head, PointF TailEnd, float Alpha) ShootGeometry(Shooting s, double t)
    {
        double p = Math.Clamp((t - s.Start) / s.Duration, 0, 1);
        double eased = 1 - Math.Pow(1 - p, 2);
        var head = new PointF(s.From.X + s.Dir.X * s.Length * (float)eased, s.From.Y + s.Dir.Y * s.Length * (float)eased);
        float tail = s.Tail * (float)Math.Min(1, p * 3);
        var tailEnd = new PointF(head.X - s.Dir.X * tail, head.Y - s.Dir.Y * tail);
        double fade = t - s.Start - s.Duration;
        float alpha = fade > 0 ? (float)Math.Max(0, 1 - fade / 0.35) : (float)Math.Min(1, p * 4);
        return (head, tailEnd, alpha);
    }

    private Rectangle ShootRect(Shooting s, double t)
    {
        var (head, tailEnd, _) = ShootGeometry(s, t);
        var r = RectangleF.FromLTRB(Math.Min(head.X, tailEnd.X), Math.Min(head.Y, tailEnd.Y), Math.Max(head.X, tailEnd.X), Math.Max(head.Y, tailEnd.Y));
        r.Inflate(14f * Ui, 14f * Ui);
        return Rectangle.Round(r);
    }

    private void BuildTwinkles(int w, int h)
    {
        var rnd = new Random(1116 + w * 7 + h);
        int count = Math.Max(14, w * h / 15000);
        var planet = Galaxy.PlanetArea(w, Ui);
        var opaque = OpaqueAreas();
        Color[] tints = [Color.White, Color.FromArgb(200, 220, 255), Color.FromArgb(255, 200, 240), Color.FromArgb(180, 255, 250)];
        var list = new List<Twinkle>();
        for (int i = 0; i < count * 3 && list.Count < count; i++)
        {
            var pos = new PointF((float)(rnd.NextDouble() * w), (float)(rnd.NextDouble() * h));
            if (planet.Contains(Point.Round(pos))) continue;
            if (opaque.Any(o => o.Contains(Point.Round(pos)))) continue;
            list.Add(new Twinkle(pos, (1.6f + (float)rnd.NextDouble() * 1.6f) * Ui, (float)(rnd.NextDouble() * Math.PI * 2),
                0.8f + (float)rnd.NextDouble() * 1.8f, tints[rnd.Next(tints.Length)]));
        }
        _twinkles = list.ToArray();
    }

    private void DrawBackgroundEffects(Graphics g, Rectangle clip, double t)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        foreach (var star in _twinkles)
        {
            if (!clip.IntersectsWith(StarRect(star))) continue;
            float b = (float)((Math.Sin(t * star.Speed + star.Phase) + 1) / 2);
            b = b * b * b;
            int a = (int)(25 + 230 * b);
            float size = star.Size * (0.6f + 0.7f * b);
            if (b > 0.35f)
            {
                using var halo = new SolidBrush(Color.FromArgb((int)(70 * b), star.Tint));
                float hr = 5f * Ui * b + size;
                g.FillEllipse(halo, star.Pos.X - hr, star.Pos.Y - hr, hr * 2, hr * 2);
                using var ray = new Pen(Color.FromArgb((int)(150 * b), star.Tint), 1f);
                float len = (3f + 7f * b) * Ui;
                g.DrawLine(ray, star.Pos.X - len, star.Pos.Y, star.Pos.X + len, star.Pos.Y);
                g.DrawLine(ray, star.Pos.X, star.Pos.Y - len, star.Pos.X, star.Pos.Y + len);
            }
            using var core = new SolidBrush(Color.FromArgb(a, star.Tint));
            g.FillEllipse(core, star.Pos.X - size / 2, star.Pos.Y - size / 2, size, size);
        }

        if (_shooting is { } sh && clip.IntersectsWith(ShootRect(sh, t)))
        {
            var (head, tailEnd, alpha) = ShootGeometry(sh, t);
            if (alpha > 0.01f && (Math.Abs(head.X - tailEnd.X) > 0.5f || Math.Abs(head.Y - tailEnd.Y) > 0.5f))
            {
                using var brush = new LinearGradientBrush(tailEnd, head, Color.FromArgb(0, 200, 170, 255), Color.FromArgb((int)(235 * alpha), 255, 245, 255));
                using var pen = new Pen(brush, 2.2f * Ui) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(pen, tailEnd, head);
                float hr = 6f * Ui;
                using var glow = new SolidBrush(Color.FromArgb((int)(90 * alpha), 210, 180, 255));
                g.FillEllipse(glow, head.X - hr, head.Y - hr, hr * 2, hr * 2);
                using var dot = new SolidBrush(Color.FromArgb((int)(255 * alpha), 255, 255, 255));
                g.FillEllipse(dot, head.X - 1.8f * Ui, head.Y - 1.8f * Ui, 3.6f * Ui, 3.6f * Ui);
            }
        }
    }

    private void RebuildGalaxy()
    {
        var cs = ClientSize;
        if (cs.Width < 20 || cs.Height < 20) return;
        var old = _galaxy;
        _galaxy = Galaxy.Render(cs.Width, cs.Height, Ui);
        old?.Dispose();
        BuildTwinkles(cs.Width, cs.Height);
        Invalidate(true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (_galaxy is not null)
        {
            e.Graphics.DrawImageUnscaled(_galaxy, 0, 0);
            if (AnimateBackground) DrawBackgroundEffects(e.Graphics, e.ClipRectangle, _clock.Elapsed.TotalSeconds);
            if (AnimatePlanet && e.ClipRectangle.IntersectsWith(Galaxy.PlanetArea(ClientSize.Width, Ui)))
                Galaxy.DrawPlanet(e.Graphics, ClientSize.Width, Ui, _clock.Elapsed.TotalSeconds);
            var cs = ClientSize;
            if (cs.Width > _galaxy.Width || cs.Height > _galaxy.Height)
            {
                using var b = new SolidBrush(Galaxy.Deep);
                if (cs.Width > _galaxy.Width) e.Graphics.FillRectangle(b, _galaxy.Width, 0, cs.Width - _galaxy.Width, cs.Height);
                if (cs.Height > _galaxy.Height) e.Graphics.FillRectangle(b, 0, _galaxy.Height, cs.Width, cs.Height - _galaxy.Height);
            }
        }
        else base.OnPaintBackground(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _rebuild.Dispose(); _animation.Dispose(); _galaxy?.Dispose(); _galaxy = null; }
        base.Dispose(disposing);
    }
}

// Halbdurchsichtige Glas-Karte
internal sealed class GlassPanel : Panel
{
    public Color Tint { get; set; } = Color.FromArgb(115, 18, 12, 46);

    public GlassPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        float ui = DeviceDpi / 96f;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var p = Galaxy.Rounded(new RectangleF(1f, 1f, Width - 3f, Height - 3f), 16f * ui);
        using (var fillBrush = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, Width), Math.Max(1, Height)),
                   Color.FromArgb(Math.Min(255, Tint.A + 25), Math.Min(255, Tint.R + 22), Math.Min(255, Tint.G + 10), Math.Min(255, Tint.B + 30)),
                   Color.FromArgb(Tint.A, Math.Max(0, Tint.R - 6), Math.Max(0, Tint.G - 4), Math.Max(0, Tint.B - 8)), 90f))
            g.FillPath(fillBrush, p);
        using (var shine = new Pen(Color.FromArgb(55, 255, 255, 255), 1f))
            g.DrawLine(shine, 16f * ui, 2.5f, Width - 16f * ui, 2.5f);
        using var lb = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, Width), Math.Max(1, Height)), Color.FromArgb(200, 200, 120, 255), Color.FromArgb(200, 90, 220, 255), 0f);
        using var pen = new Pen(lb, 1.4f * ui);
        g.DrawPath(pen, p);
    }
}

// Leuchtender, abgerundeter Button
internal sealed class GlowButton : Button
{
    private bool _hover, _down;
    public Color ColorA { get; set; } = Color.FromArgb(140, 60, 220);
    public Color ColorB { get; set; } = Color.FromArgb(60, 120, 230);

    public GlowButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.Transparent;
        ForeColor = Color.White;
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
    }

    public void SetColors(Color a, Color b) { ColorA = a; ColorB = b; Invalidate(); }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
    protected override bool ShowFocusCues => false;

    protected override void OnPaint(PaintEventArgs e)
    {
        // Hintergrund des Eltern-Elements (Galaxie/Glas) durchscheinen lassen
        InvokePaintBackground(this, e);
        float ui = DeviceDpi / 96f;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new RectangleF(1f, 1f, Width - 3f, Height - 3f);
        if (_down) r.Inflate(-1f, -1f);
        int alpha = !Enabled ? 85 : _hover ? 255 : 215;
        using (var p = Galaxy.Rounded(r, Height / 2.2f))
        {
            using (var lb = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, Width), Math.Max(1, Height)), Color.FromArgb(alpha, ColorA), Color.FromArgb(alpha, ColorB), 0f))
                g.FillPath(lb, p);
            using var pen = new Pen(Color.FromArgb(_hover ? 235 : 140, 235, 210, 255), 1.2f * ui);
            g.DrawPath(pen, p);
        }
        TextRenderer.DrawText(g, Text, Font, Rectangle.Round(r), Enabled ? ForeColor : Color.FromArgb(170, 170, 190),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }
}

// ---------- Exi & Lina: Design-Extras ----------

internal static class GalaxyChrome
{
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    private static int ColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

    /// <summary>Dunkle, lila Titelleiste und runde Ecken (Windows 11; unter Windows 10 nur dunkle Leiste).</summary>
    public static void Apply(Form form, bool round = true)
    {
        if (!form.IsHandleCreated) return;
        nint h = form.Handle;
        try
        {
            int on = 1; DwmSetWindowAttribute(h, 20, ref on, sizeof(int));                       // dunkler Modus
            int caption = ColorRef(Color.FromArgb(22, 12, 50)); DwmSetWindowAttribute(h, 35, ref caption, sizeof(int));
            int text = ColorRef(Color.FromArgb(236, 226, 255)); DwmSetWindowAttribute(h, 36, ref text, sizeof(int));
            int border = ColorRef(Color.FromArgb(150, 95, 235)); DwmSetWindowAttribute(h, 34, ref border, sizeof(int));
            if (round) { int corner = 2; DwmSetWindowAttribute(h, 33, ref corner, sizeof(int)); }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
    }
}

internal static class OreRarity
{
    private static readonly Dictionary<string, int> Tier = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Quantainium"] = 4, ["Stileron"] = 4, ["Savrilium"] = 4,
        ["Ouratite"] = 3, ["Riccite"] = 3, ["Lindinium"] = 3,
        ["Beryl"] = 2, ["Taranite"] = 2, ["Borase"] = 2, ["Gold"] = 2, ["Bexalite"] = 2,
        ["Laranite"] = 1, ["Aslarite"] = 1, ["Titanium"] = 1, ["Tungsten"] = 1, ["Agricium"] = 1, ["Torite"] = 1,
        ["Hephaestanite"] = 0, ["Tin"] = 0, ["Quartz"] = 0, ["Corundum"] = 0, ["Copper"] = 0, ["Silicon"] = 0,
        ["Iron"] = 0, ["Aluminum"] = 0, ["Aluminium"] = 0, ["Ice"] = 0,
    };

    public static bool TryGet(string nameEn, out string label, out Color color)
    {
        label = ""; color = Galaxy.Cyan;
        if (!Tier.TryGetValue(nameEn, out int t)) return false;
        (label, color) = t switch
        {
            4 => (L.T("LEGENDÄR", "LEGENDARY"), Color.FromArgb(255, 160, 60)),
            3 => (L.T("EPISCH", "EPIC"), Color.FromArgb(205, 110, 255)),
            2 => (L.T("SELTEN", "RARE"), Color.FromArgb(110, 155, 255)),
            1 => (L.T("UNGEWÖHNLICH", "UNCOMMON"), Color.FromArgb(95, 210, 110)),
            _ => (L.T("GEWÖHNLICH", "COMMON"), Color.FromArgb(240, 205, 95)),
        };
        return true;
    }
}

/// <summary>
/// Animierte Überschrift: Die Farben (pink → lila → cyan) wandern durch den Text,
/// und ein Leuchten wird langsam stärker und wieder schwächer.
/// </summary>
internal sealed class GradientLabel : Label
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 40 };
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

    /// <summary>Abstand für das Leuchten rund um den Text (in 96-DPI-Pixeln).</summary>
    public int GlowPad { get; set; } = 8;
    /// <summary>Geschwindigkeit des Farbwanderns.</summary>
    public float ColorSpeed { get; set; } = 60f;
    /// <summary>Dauer eines Leuchtzyklus in Sekunden (an → aus).</summary>
    public float PulseSeconds { get; set; } = 3.2f;

    public GradientLabel()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent; UseMnemonic = false;
        _timer.Tick += (_, _) => { if (Visible && IsHandleCreated) Invalidate(); };
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); _timer.Start(); }
    protected override void OnHandleDestroyed(EventArgs e) { _timer.Stop(); base.OnHandleDestroyed(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (string.IsNullOrEmpty(Text)) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float ui = DeviceDpi / 96f;
        double t = _clock.Elapsed.TotalSeconds;
        float pad = GlowPad * ui;
        float emSize = Font.SizeInPoints * DeviceDpi / 72f;

        using var path = new GraphicsPath();
        path.AddString(Text, Font.FontFamily, (int)Font.Style, emSize, new PointF(pad, pad * 0.6f), StringFormat.GenericTypographic);
        var bounds = path.GetBounds();
        if (bounds.Width < 1 || bounds.Height < 1) return;

        // Leuchten: pulsiert weich zwischen fast aus und hell
        float pulse = (float)((Math.Sin(t * 2 * Math.PI / Math.Max(0.5f, PulseSeconds)) + 1) / 2); // 0..1
        pulse = pulse * pulse * (3 - 2 * pulse); // weicher Übergang
        Color glowColor = Color.FromArgb(200, 120, 255);
        foreach (var (width, alpha) in new[] { (14f, 22), (9f, 38), (5f, 60) })
        {
            int a = (int)(alpha * (0.15f + 0.85f * pulse));
            using var glowPen = new Pen(Color.FromArgb(a, glowColor), width * ui) { LineJoin = LineJoin.Round };
            g.DrawPath(glowPen, path);
        }

        // Wandernder Farbverlauf (nahtlos wiederholt)
        float span = Math.Max(120f * ui, bounds.Width * 0.8f);
        using var brush = new LinearGradientBrush(new RectangleF(bounds.X, 0, span, Math.Max(1, Height)), Color.White, Color.White, 0f)
        {
            InterpolationColors = new ColorBlend
            {
                Colors = [Color.FromArgb(255, 150, 230), Color.FromArgb(190, 150, 255), Color.FromArgb(120, 225, 255), Color.FromArgb(190, 150, 255), Color.FromArgb(255, 150, 230)],
                Positions = [0f, 0.25f, 0.5f, 0.75f, 1f]
            },
            WrapMode = WrapMode.Tile
        };
        brush.TranslateTransform((float)(t * ColorSpeed * ui % span), 0);
        g.FillPath(brush, path);

        // feiner heller Rand, der beim Leuchten stärker wird
        using var edge = new Pen(Color.FromArgb((int)(40 + 80 * pulse), 255, 255, 255), 0.8f * ui);
        g.DrawPath(edge, path);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>Abgerundete "Pille" mit leuchtendem Punkt (Status) oder als Badge (Seltenheit).</summary>
internal sealed class PillLabel : Label
{
    public bool ShowDot { get; set; } = true;

    public PillLabel()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent; UseMnemonic = false;
    }

    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
    protected override void OnForeColorChanged(EventArgs e) { Invalidate(); base.OnForeColorChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (string.IsNullOrEmpty(Text)) return;
        float ui = DeviceDpi / 96f;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var textSize = TextRenderer.MeasureText(g, Text, Font, Size.Empty, TextFormatFlags.NoPadding);
        float pad = 12f * ui, dot = ShowDot ? 10f * ui : 0f, gap = ShowDot ? 8f * ui : 0f;
        float w = Math.Min(Width - 2f, pad * 2 + dot + gap + textSize.Width);
        var pill = new RectangleF(1f, 1f, w, Height - 2f);
        using (var path = Galaxy.Rounded(pill, pill.Height / 2f))
        {
            using (var fill = new SolidBrush(Color.FromArgb(60, ForeColor))) g.FillPath(fill, path);
            using var pen = new Pen(Color.FromArgb(170, ForeColor), 1.2f * ui);
            g.DrawPath(pen, path);
        }
        float x = pill.X + pad;
        if (ShowDot)
        {
            float cy = pill.Y + pill.Height / 2f;
            using (var halo = new SolidBrush(Color.FromArgb(70, ForeColor))) g.FillEllipse(halo, x - 3f * ui, cy - dot / 2f - 3f * ui, dot + 6f * ui, dot + 6f * ui);
            using (var b = new SolidBrush(ForeColor)) g.FillEllipse(b, x, cy - dot / 2f, dot, dot);
            x += dot + gap;
        }
        var tr = new Rectangle((int)x, 0, Math.Max(1, (int)(pill.Right - x)), Height);
        TextRenderer.DrawText(g, Text, Font, tr, ShowDot ? Color.White : ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
    }
}
