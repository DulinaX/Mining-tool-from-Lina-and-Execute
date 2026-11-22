using System.Runtime.InteropServices;
using MiningScanner.Core;
using MiningScanner.Windows.Platform;
using MiningScanner.Windows.Runtime;

namespace MiningScanner.Windows.UI;

internal sealed class OverlayForm : Form
{
    private readonly Label _text = new() { AutoSize = false, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(238, 226, 255), Padding = new(12) };
    private bool _editing;
    private readonly PictureBox _preview = new() { Dock = DockStyle.Bottom, Height = 110,
        SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(12, 6, 30), Visible = false };
    private Font? _ownedFont;
    private Point? _drag;
    private Rectangle _client, _forbidden;
    public event Action<Point>? PositionCommitted;
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000 | 0x00000080 | 0x00080000; // NOACTIVATE, TOOLWINDOW, LAYERED
            if (!_editing) cp.ExStyle |= 0x20; // WS_EX_TRANSPARENT
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        GalaxyChrome.Apply(this); // runde Ecken + lila Rahmen (Windows 11)
    }

    public OverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
        BackColor = Color.FromArgb(22, 10, 48); Opacity = .9;
        AutoScaleMode = AutoScaleMode.None; StartPosition = FormStartPosition.Manual;
        Controls.Add(_text); Controls.Add(_preview);
        _text.MouseDown += (_, e) => { if (_editing && e.Button == MouseButtons.Left) { _drag = e.Location; _text.Capture = true; } };
        _text.MouseMove += (_, _) =>
        {
            if (!_editing || _drag is null) return;
            Point mouse = Cursor.Position;
            var desired = new Rectangle(mouse.X - _drag.Value.X, mouse.Y - _drag.Value.Y, Width, Height);
            desired.X = Math.Clamp(desired.X, _client.Left, Math.Max(_client.Left, _client.Right - Width));
            desired.Y = Math.Clamp(desired.Y, _client.Top, Math.Max(_client.Top, _client.Bottom - Height));
            if (!desired.IntersectsWith(_forbidden)) Location = desired.Location;
        };
        _text.MouseUp += (_, _) =>
        {
            if (_drag is null) return;
            _drag = null; _text.Capture = false;
            PositionCommitted?.Invoke(new(Left - _client.Left, Top - _client.Top));
        };
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0021) { m.Result = (IntPtr)3; return; } // MA_NOACTIVATE
        base.WndProc(ref m);
    }

    // Takes ownership of a RAM-only clone; never saves the image or keeps a history.
    public void SetPreview(Bitmap? image)
    {
        var old = _preview.Image;
        _preview.Image = image;
        old?.Dispose();
    }

    public bool Present(GameWindow window, Rectangle roi, AppSettings settings, string content, bool editing = false)
    {
        if (!ScreenArea.IsCurrent(window)) { Hide(); return false; }
        if (_editing != editing) { _editing = editing; RecreateHandle(); }
        _preview.Visible = !editing && settings.PreviewEnabled && _preview.Image is not null;
        _client = window.ClientBounds;
        _forbidden = new(roi.X + _client.X, roi.Y + _client.Y, roi.Width, roi.Height);
        _forbidden.Inflate(8, 8);
        if (_text.Font.Size != settings.OverlayTextSize)
        {
            var old = _ownedFont; _ownedFont = new Font("Segoe UI", settings.OverlayTextSize, FontStyle.Regular, GraphicsUnit.Point); _text.Font = _ownedFont; old?.Dispose();
        }
        _text.Text = editing ? L.T("ANZEIGE VERSCHIEBEN\nHier ziehen und loslassen", "MOVE DISPLAY\nDrag here and release") : content;
        var measured = TextRenderer.MeasureText(_text.Text, _text.Font, new Size(Math.Min(680, _client.Width - 24), 1000), TextFormatFlags.WordBreak);
        Size = new(Math.Min(_client.Width, Math.Max(280, measured.Width + 30)),
            Math.Min(_client.Height, measured.Height + 30 + (_preview.Visible ? _preview.Height : 0)));
        if (_drag is null)
        {
            Point preferred = new(_client.Left + (int)(settings.OverlayX * _client.Width), _client.Top + (int)(settings.OverlayY * _client.Height));
            preferred.X = Math.Clamp(preferred.X, _client.Left, Math.Max(_client.Left, _client.Right - Width));
            preferred.Y = Math.Clamp(preferred.Y, _client.Top, Math.Max(_client.Top, _client.Bottom - Height));
            var candidates = new[] { preferred, new Point(_client.Left + 8, _client.Top + 8), new Point(_client.Right - Width - 8, _client.Top + 8), new Point(_client.Left + 8, _client.Bottom - Height - 8), new Point(_client.Right - Width - 8, _client.Bottom - Height - 8) };
            var safe = candidates.FirstOrDefault(p => _client.Contains(new Rectangle(p, Size)) && !new Rectangle(p, Size).IntersectsWith(_forbidden), new Point(int.MinValue, 0));
            if (safe.X == int.MinValue) { Hide(); return false; }
            Location = safe;
        }
        if (!Visible) Show();
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) SetPreview(null);
        base.Dispose(disposing);
        if (disposing) { _ownedFont?.Dispose(); _ownedFont = null; }
    }
}
