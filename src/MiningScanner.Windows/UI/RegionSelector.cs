using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using MiningScanner.Core;
using MiningScanner.Windows.Platform;
using MiningScanner.Windows.Runtime;

namespace MiningScanner.Windows.UI;

/// <summary>
/// Auswahlfläche über dem Star-Citizen-Monitor mit eingefrorenem Standbild.
/// Das Fenster holt sich den Fokus (wie Alt-Tab), dadurch bekommt Star Citizen beim Rahmen-Ziehen keine
/// Mausbewegungen und das Schiff dreht sich nicht. Das Spiel selbst wird nicht verändert und bekommt keine Eingaben.
/// Das Standbild liegt nur im Arbeitsspeicher und wird beim Schließen gelöscht.
/// Exi &amp; Lina: Der Rahmen kann überall im Spielfenster und in jede Richtung gezogen werden.
/// Zu große Rahmen werden automatisch passend gemacht, statt einen Fehler zu zeigen.
/// </summary>
internal sealed class RegionSelector : Form
{
    private readonly GameWindow _window;
    private readonly System.Windows.Forms.Timer _guard = new() { Interval = 100 };
    private Point? _start;
    private Rectangle _selection;
    private string? _message;
    private readonly Action<RegionSettings?> _complete;
    private bool _finished;
    private Bitmap? _frozen;
    protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= 0x80; return cp; } }
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsIconic(nint hwnd);

    public RegionSelector(GameWindow window, Action<RegionSettings?> complete)
    {
        _window = window; _complete = complete;
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
        AutoScaleMode = AutoScaleMode.None; StartPosition = FormStartPosition.Manual;
        Bounds = window.ClientBounds; BackColor = Color.FromArgb(20, 8, 46);
        DoubleBuffered = true; Cursor = Cursors.Cross; KeyPreview = true;
        _frozen = Freeze(window.ClientBounds);
        if (_frozen is null) Opacity = .55; // Standbild ging nicht -> wie früher halb durchsichtig
        // Nur abbrechen, wenn das Spielfenster wirklich weg oder minimiert ist – nicht bei kurzem Fokuswechsel.
        _guard.Tick += (_, _) => { if (_window.Handle != 0 && (!IsWindow(_window.Handle) || IsIconic(_window.Handle))) Finish(null); };
        Shown += (_, _) => { RegisterHotKey(Handle, 441, 0x4000, 0x1B); _guard.Start(); Activate(); Focus(); };
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Finish(null); };
        FormClosed += (_, _) =>
        {
            _guard.Stop(); _guard.Dispose(); UnregisterHotKey(Handle, 441);
            _frozen?.Dispose(); _frozen = null;
            if (!_finished) { _finished = true; _complete(null); }
        };
    }

    /// <summary>Standbild des Star-Citizen-Monitors (nur Bildschirm-Pixel, wie ein Screenshot; bleibt im Arbeitsspeicher).</summary>
    private static Bitmap? Freeze(Rectangle area)
    {
        if (area.Width < 1 || area.Height < 1) return null;
        var bmp = new Bitmap(area.Width, area.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(area.Location, Point.Empty, area.Size, CopyPixelOperation.SourceCopy);
            return bmp;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or ArgumentException or ExternalException)
        {
            bmp.Dispose();
            return null;
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (_frozen is null) { base.OnPaintBackground(e); return; }
        e.Graphics.DrawImageUnscaled(_frozen, 0, 0);
        using var dim = new SolidBrush(Color.FromArgb(110, 20, 8, 46));
        e.Graphics.FillRectangle(dim, ClientRectangle);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312 && m.WParam.ToInt32() == 441) { Finish(null); return; }
        base.WndProc(ref m);
    }

    private Point ClampToClient(Point p) =>
        new(Math.Clamp(p.X, 0, Math.Max(0, ClientSize.Width - 1)), Math.Clamp(p.Y, 0, Math.Max(0, ClientSize.Height - 1)));

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Right) { Finish(null); return; }
        if (e.Button != MouseButtons.Left) return;
        _start = ClampToClient(e.Location); _selection = Rectangle.Empty; _message = null; Capture = true; Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_start is not Point p) return;
        var q = ClampToClient(e.Location);
        _selection = Rectangle.FromLTRB(Math.Min(p.X, q.X), Math.Min(p.Y, q.Y), Math.Max(p.X, q.X) + 1, Math.Max(p.Y, q.Y) + 1);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || _start is null) return;
        Capture = false; _start = null;
        if (_selection.Width < 8 || _selection.Height < 8)
        {
            _message = L.T("Etwas zu klein – halte die linke Maustaste gedrückt und zieh einen Rahmen um die Zahl.", "A bit too small – hold the left mouse button and drag a box around the number.");
            _selection = Rectangle.Empty; Invalidate(); return;
        }
        var size = ClientSize;
        var fitted = ScannerController.FitRegion(_selection, size);
        var region = new RegionSettings
        {
            X = (double)fitted.X / size.Width, Y = (double)fitted.Y / size.Height,
            Width = (double)fitted.Width / size.Width, Height = (double)fitted.Height / size.Height,
            ClientWidth = size.Width, ClientHeight = size.Height, Dpi = Math.Clamp(_window.Dpi, 48, 768)
        };
        try { region.Validate(); Finish(region); }
        catch (InvalidDataException)
        {
            _message = L.T("Das hat nicht geklappt – bitte nochmal einen Rahmen um die Zahl ziehen.", "That did not work – please drag a box around the number again.");
            _selection = Rectangle.Empty; Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        string text = _frozen is not null
            ? L.T("BEREICH WÄHLEN  ·  Bild eingefroren\nZieh mit gedrückter linker Maustaste einen Rahmen um die Signatur-Zahl.\nDein Schiff dreht sich dabei nicht – Star Citizen bekommt keine Mausbewegungen.\nRechtsklick oder Esc = Abbrechen",
                  "SELECT AREA  ·  image frozen\nHold the left mouse button and drag a box around the signature number.\nYour ship does not turn – Star Citizen gets no mouse movement meanwhile.\nRight-click or Esc = cancel")
            : L.T("BEREICH WÄHLEN\nZieh mit gedrückter linker Maustaste einen Rahmen um die Signatur-Zahl.\nRechtsklick oder Esc = Abbrechen", "SELECT AREA\nHold the left mouse button and drag a box around the signature number.\nRight-click or Esc = cancel");
        using var font = new Font("Segoe UI", 14, FontStyle.Bold);
        var measured = TextRenderer.MeasureText(text, font);
        var primary = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
        var box = new Rectangle(Math.Max(10, primary.X + (primary.Width - measured.Width) / 2 - 20), Math.Max(10, primary.Y + 24), measured.Width + 40, measured.Height + 24);
        using (var bg = new SolidBrush(Color.FromArgb(235, 30, 12, 64))) g.FillRectangle(bg, box);
        using (var border = new Pen(Color.FromArgb(200, 130, 232, 255), 2)) g.DrawRectangle(border, box);
        TextRenderer.DrawText(g, text, font, box, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        if (_message is not null)
        {
            using var mf = new Font("Segoe UI", 13, FontStyle.Bold);
            var ms = TextRenderer.MeasureText(_message, mf);
            var mbox = new Rectangle(Math.Max(10, box.X + (box.Width - ms.Width) / 2 - 16), box.Bottom + 12, ms.Width + 32, ms.Height + 16);
            using (var mb = new SolidBrush(Color.FromArgb(235, 90, 20, 60))) g.FillRectangle(mb, mbox);
            TextRenderer.DrawText(g, _message, mf, mbox, Color.FromArgb(255, 210, 120), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        if (_selection.Width > 0 && _selection.Height > 0)
        {
            using (var fill = new SolidBrush(Color.FromArgb(70, 130, 232, 255))) g.FillRectangle(fill, _selection);
            using var pen = new Pen(Color.FromArgb(130, 232, 255), 3);
            g.DrawRectangle(pen, _selection);
            string dim = $"{_selection.Width} × {_selection.Height}";
            using var df = new Font("Segoe UI", 10, FontStyle.Bold);
            int ty = _selection.Bottom + 4 + 20 < ClientSize.Height ? _selection.Bottom + 4 : _selection.Top - 22;
            TextRenderer.DrawText(g, dim, df, new Point(_selection.Left, Math.Max(0, ty)), Color.White);
        }
    }

    private void Finish(RegionSettings? region)
    {
        if (_finished) return;
        _finished = true; _guard.Stop(); _complete(region); Close();
    }
}
