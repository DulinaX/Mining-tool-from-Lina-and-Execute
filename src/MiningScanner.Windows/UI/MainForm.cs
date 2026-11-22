using MiningScanner.Core;
using MiningScanner.Windows.Platform;
using MiningScanner.Windows.Runtime;

namespace MiningScanner.Windows.UI;

internal sealed class MainForm : GalaxyForm
{
    public const string AppName = Brand.AppName;

    private AppSettings _settings;
    private SignatureTable _table;
    private readonly string _settingsPath, _tablePath;
    private readonly ScannerController _scanner;
    private readonly OverlayForm _overlay = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 25 };
    private readonly Label _value = new(), _hint = new(), _regionLabel = new(), _matches = new();
    private readonly PillLabel _status = new(), _rarity = new() { ShowDot = false };
    private readonly GlowButton _toggle = new(), _select = new(), _move = new(), _border = new();
    private readonly PictureBox _preview = new() { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(14, 8, 34) };
    private readonly NumericUpDown _interval = Number(100, 5000, 50), _expiry = Number(250, 30000, 250), _maxCount = Number(1, 1000, 1), _textSize = Number(10, 48, 1), _confidence = Number(50, 100, 1), _x = Number(0, 100, 1), _y = Number(0, 100, 1);
    private readonly TextBox _filter = new() { Width = 220, MaxLength = 200, BackColor = Galaxy.Field, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
    private readonly CheckBox _previewEnabled = new() { Text = L.T("Vorschau-Bild anzeigen (nur im Arbeitsspeicher)", "Show preview image (memory only)"), AutoSize = true, BackColor = Color.Transparent, ForeColor = Galaxy.Text };
    private GalaxyForm? _settingsDialog;
    private RegionSelector? _selector;
    private int _pending; // 1 region, 2 overlay
    private GameWindow? _editingWindow;
    private bool _resumeAfterSetup, _closing, _closed;
    private string? _lastOverlayText;

    public MainForm(AppSettings settings, SignatureTable table, string settingsPath, string tablePath, string? warning)
    {
        _settings = settings; _table = table; _settingsPath = settingsPath; _tablePath = tablePath;
        // Ein Bereich aus der alten Version (bezogen auf das Spielfenster) passt nicht mehr – dann neu wählen lassen.
        var vs = ScreenArea.MonitorBounds;
        if (_settings.Region is { } oldRegion && (oldRegion.ClientWidth != vs.Width || oldRegion.ClientHeight != vs.Height))
            _settings = _settings with { Region = null };
        // Alte, sehr strenge Standardwerte auf die neuen, toleranteren Werte umstellen.
        if (_settings.MinimumConfidence >= 0.79 && _settings.ExpiryMs == 1500)
            _settings = _settings with { MinimumConfidence = 0.60, ExpiryMs = 4000 };
        if (_settings.MaxCount == 12) _settings = _settings with { MaxCount = 10 };
        // Zurück zum bewährten Abtastintervall (die schnelle 120-ms-Einstellung hat schlechter erkannt).
        if (_settings.IntervalMs == 120) _settings = _settings with { IntervalMs = 250 };
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = AppName; StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        ClientSize = new Size(860, 684);
        Font = new Font("Segoe UI", 10);
        BuildUi();
        ResumeLayout(false);
        PopulateSettings(); UpdateRegionLabel();
        _scanner = new(_settings, table);
        _timer.Tick += (_, _) => TickUi();
        _toggle.Click += (_, _) => { if (_pending != 0 || _selector is not null || _editingWindow is not null) CancelSetup(); _scanner.SetEnabled(!_scanner.Enabled); };
        _select.Click += (_, _) => BeginSetup(1);
        _move.Click += (_, _) => BeginSetup(2);
        _overlay.PositionCommitted += position =>
        {
            if (_editingWindow is not { } w) return;
            _settings = _settings with { OverlayX = (double)position.X / w.ClientBounds.Width, OverlayY = (double)position.Y / w.ClientBounds.Height };
            PopulateSettings(); SaveSettings();
            CancelSetup(); _hint.Text = L.T("Anzeige-Position gespeichert.", "Overlay position saved.");
        };
        FormClosing += OnClosing;
        Shown += (_, _) => { _timer.Start(); if (warning is not null) _hint.Text = warning; };
    }

    private static NumericUpDown Number(int min, int max, int step) => new()
    {
        Minimum = min, Maximum = max, Increment = step, Width = 110, ThousandsSeparator = true,
        BackColor = Galaxy.Field, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle
    };

    private static Label MakeLabel(string text, Rectangle bounds, Font font, Color color) => new()
    {
        Text = text, Bounds = bounds, Font = font, ForeColor = color, BackColor = Color.Transparent, AutoSize = false, UseMnemonic = false
    };

    private static GlowButton MakeButton(GlowButton b, string text, Rectangle bounds, Color a, Color c)
    {
        b.Text = text; b.Bounds = bounds; b.ColorA = a; b.ColorB = c; b.UseMnemonic = false;
        return b;
    }

    private void BuildUi()
    {
        // Kopfzeile
        var title = new GradientLabel { Text = L.T(Brand.Title, Brand.TitleEn), Bounds = new Rectangle(14, 2, 600, 56), Font = new Font("Segoe UI", 27, FontStyle.Bold) };
        var sub = new GradientLabel { Text = Brand.Subtitle, Bounds = new Rectangle(20, 58, 560, 26), Font = new Font("Segoe UI", 11, FontStyle.Bold), GlowPad = 6, ColorSpeed = 45f, PulseSeconds = 3.2f };
        var safe = MakeLabel(L.T("Offline  ·  liest nur Bildschirm-Pixel  ·  Star Citizen bleibt unberührt", "Offline  ·  reads screen pixels only  ·  Star Citizen stays untouched"), new Rectangle(28, 84, 620, 20), new Font("Segoe UI", 9), Galaxy.Caption);
        Controls.Add(title); Controls.Add(sub); Controls.Add(safe);

        // Schritt 1-2-3
        var steps = new GlassPanel { Bounds = new Rectangle(24, 112, 812, 104) };
        MakeButton(_select, L.T("1   Bereich wählen", "1   Select area"), new Rectangle(16, 14, 252, 46), Color.FromArgb(90, 60, 190), Color.FromArgb(50, 100, 200));
        MakeButton(_toggle, L.T("2   Scan starten", "2   Start scan"), new Rectangle(280, 14, 252, 46), Color.FromArgb(205, 60, 200), Color.FromArgb(115, 70, 240));
        MakeButton(_move, L.T("3   Overlay verschieben", "3   Move overlay"), new Rectangle(544, 14, 252, 46), Color.FromArgb(60, 70, 170), Color.FromArgb(40, 120, 190));
        var c1 = MakeLabel(L.T("Rahmen eng um die Signatur-Zahl ziehen", "Drag a tight box around the signature number"), new Rectangle(16, 66, 252, 32), new Font("Segoe UI", 9), Galaxy.SoftText);
        var c2 = MakeLabel(L.T("Liest die Zahl live und zeigt das Erz", "Reads the number live and shows the ore"), new Rectangle(280, 66, 252, 32), new Font("Segoe UI", 9), Galaxy.SoftText);
        var c3 = MakeLabel(L.T("Optional: Anzeige auf dem Bildschirm verschieben", "Optional: move the on-screen display"), new Rectangle(544, 66, 252, 32), new Font("Segoe UI", 9), Galaxy.SoftText);
        foreach (var l in new[] { c1, c2, c3 }) l.TextAlign = ContentAlignment.TopCenter;
        steps.Controls.AddRange([_select, _toggle, _move, c1, c2, c3]);
        Controls.Add(steps);

        // Status
        _status.Bounds = new Rectangle(24, 222, 812, 34); _status.Font = new Font("Segoe UI", 11.5f, FontStyle.Bold);
        _status.ForeColor = Galaxy.SoftText; _status.Text = L.T("Gestoppt", "Stopped");
        Controls.Add(_status);

        // Ergebnis
        var result = new GlassPanel { Bounds = new Rectangle(24, 260, 520, 250) };
        var cap = MakeLabel(L.T("SIGNATUR", "SIGNATURE"), new Rectangle(20, 14, 300, 20), new Font("Segoe UI", 9, FontStyle.Bold), Galaxy.Caption);
        _value.Bounds = new Rectangle(16, 34, 488, 64); _value.Font = new Font("Segoe UI", 32, FontStyle.Bold);
        _value.BackColor = Color.Transparent; _value.ForeColor = Color.White; _value.Text = "—"; _value.UseMnemonic = false;
        _rarity.Bounds = new Rectangle(18, 100, 300, 28); _rarity.Font = new Font("Segoe UI", 9, FontStyle.Bold); _rarity.Visible = false;
        _matches.Bounds = new Rectangle(20, 134, 484, 108); _matches.Font = new Font("Segoe UI", 13, FontStyle.Bold);
        _matches.BackColor = Color.Transparent; _matches.ForeColor = Galaxy.Cyan; _matches.UseMnemonic = false; _matches.AutoEllipsis = true;
        _matches.Text = L.T("Noch nichts gescannt.", "Nothing scanned yet.");
        result.Controls.AddRange([cap, _value, _rarity, _matches]);
        Controls.Add(result);

        // Vorschau
        var previewCard = new GlassPanel { Bounds = new Rectangle(556, 260, 280, 250) };
        var pcap = MakeLabel(L.T("VORSCHAU", "PREVIEW"), new Rectangle(18, 14, 240, 20), new Font("Segoe UI", 9, FontStyle.Bold), Galaxy.Caption);
        _preview.Bounds = new Rectangle(16, 40, 248, 194);
        var pnote = MakeLabel(L.T("Einschalten unter\n„Einstellungen“", "Turn on in\n'Settings'"), new Rectangle(16, 110, 248, 50), new Font("Segoe UI", 9), Galaxy.SoftText);
        pnote.TextAlign = ContentAlignment.MiddleCenter;
        previewCard.Controls.Add(pcap); previewCard.Controls.Add(_preview);
        _preview.Controls.Add(pnote); pnote.Location = new Point(0, 70); pnote.Width = _preview.Width;
        Controls.Add(previewCard);

        // Bereich-Info
        _regionLabel.Bounds = new Rectangle(28, 518, 808, 22); _regionLabel.Font = new Font("Segoe UI", 9.5f);
        _regionLabel.BackColor = Color.Transparent; _regionLabel.ForeColor = Galaxy.SoftText; _regionLabel.UseMnemonic = false;
        Controls.Add(_regionLabel);

        // Werkzeuge
        var settingsBtn = MakeButton(new GlowButton(), L.T("Einstellungen", "Settings"), new Rectangle(24, 548, 200, 40), Color.FromArgb(70, 50, 140), Color.FromArgb(45, 70, 150));
        settingsBtn.Click += (_, _) => ShowSettings();
        var edit = MakeButton(new GlowButton(), L.T("Steine-Liste bearbeiten", "Edit ore list"), new Rectangle(236, 548, 230, 40), Color.FromArgb(70, 50, 140), Color.FromArgb(45, 70, 150));
        edit.Click += (_, _) => EditTable();
        var guide = MakeButton(new GlowButton(), L.T("Wo finde ich welches Erz?", "Where do I find which ore?"), new Rectangle(478, 548, 358, 40), Color.FromArgb(205, 60, 200), Color.FromArgb(115, 70, 240));
        guide.Click += (_, _) => ShowOreGuide();
        Controls.Add(settingsBtn); Controls.Add(edit); Controls.Add(guide);

        // Hinweis
        _hint.Bounds = new Rectangle(28, 598, 808, 54); _hint.Font = new Font("Segoe UI", 9);
        _hint.BackColor = Color.Transparent; _hint.ForeColor = Galaxy.SoftText; _hint.UseMnemonic = false;
        _hint.Text = L.T("So geht's: 1. Bereich wählen (Rahmen um die Zahl ziehen)  →  2. Scan starten  →  fertig.\nDieses Fenster darf die Zahl nicht verdecken. Star Citizen am besten im Rahmenlos- oder Fenster-Modus spielen.", "How it works: 1. Select area (drag a box around the number)  →  2. Start scan  →  done.\nThis window must not cover the number. Play Star Citizen in borderless or windowed mode.");
        Controls.Add(_hint);

        // Copyright
        var copyright = MakeLabel(Brand.Copyright, new Rectangle(28, 656, 808, 22), new Font("Segoe UI", 9), Galaxy.Caption);
        copyright.TextAlign = ContentAlignment.MiddleRight;
        Controls.Add(copyright);

        _previewEnabled.CheckedChanged += (_, _) => pnote.Visible = !_previewEnabled.Checked;
        pnote.Visible = !_settings.PreviewEnabled;
    }

    private OreGuideForm? _oreGuide;

    private void ShowOreGuide()
    {
        if (_oreGuide is null || _oreGuide.IsDisposed) _oreGuide = new OreGuideForm();
        if (!_oreGuide.Visible) _oreGuide.Show(this);
        if (_oreGuide.WindowState == FormWindowState.Minimized) _oreGuide.WindowState = FormWindowState.Normal;
        _oreGuide.Activate();
    }

    private readonly ComboBox _language = new() { DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Galaxy.Field, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };

    private void SwitchLanguage(bool english)
    {
        if (!L.Save(english))
        {
            MessageBox.Show(this, L.T("Die Sprache konnte nicht gespeichert werden (Programmordner nicht beschreibbar).", "The language could not be saved (program folder is not writable)."), AppName);
            return;
        }
        MessageBox.Show(this, english ? "The app will now restart in English." : "Die App startet jetzt auf Deutsch neu.", AppName);
        try
        {
            if (Environment.ProcessPath is { } exe) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }
        Close();
    }

    private void ShowSettings()
    {
        if (_settingsDialog is null || _settingsDialog.IsDisposed)
        {
            var d = new GalaxyForm
            {
                Text = AppName + L.T(" – Einstellungen", " – Settings"), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
                StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false, Font = new Font("Segoe UI", 10)
            };
            d.SuspendLayout();
            d.AutoScaleDimensions = new SizeF(96F, 96F);
            d.AutoScaleMode = AutoScaleMode.Dpi;
            d.ClientSize = new Size(560, 510);
            var card = new GlassPanel { Bounds = new Rectangle(20, 20, 520, 410) };
            int y = 18;
            void Row(string name, Control control)
            {
                card.Controls.Add(MakeLabel(name, new Rectangle(20, y + 3, 300, 24), new Font("Segoe UI", 10), Galaxy.Text));
                control.Location = new Point(330, y); card.Controls.Add(control); y += 38;
            }
            _language.Width = 170;
            Row("Sprache / Language", _language);
            Row(L.T("Wie oft lesen (Millisekunden)", "Read interval (milliseconds)"), _interval);
            Row(L.T("Anzeige veraltet nach (Millisekunden)", "Value expires after (milliseconds)"), _expiry);
            Row(L.T("Maximale Anzahl Felsen", "Maximum number of rocks"), _maxCount);
            Row(L.T("Mindest-Sicherheit der Erkennung (%)", "Minimum recognition confidence (%)"), _confidence);
            Row(L.T("Schriftgröße im Spiel-Overlay", "In-game overlay font size"), _textSize);
            Row(L.T("Overlay-Position links/rechts (%)", "Overlay position left/right (%)"), _x);
            Row(L.T("Overlay-Position oben/unten (%)", "Overlay position top/bottom (%)"), _y);
            Row(L.T("Nur dieses Erz zeigen (leer = alle)", "Only show this ore (empty = all)"), _filter);
            _filter.Width = 170;
            _previewEnabled.Location = new Point(20, y + 4); card.Controls.Add(_previewEnabled);
            var save = MakeButton(new GlowButton(), L.T("Speichern", "Save"), new Rectangle(240, 446, 140, 42), Color.FromArgb(205, 60, 200), Color.FromArgb(115, 70, 240));
            var close = MakeButton(new GlowButton(), L.T("Schließen", "Close"), new Rectangle(394, 446, 146, 42), Color.FromArgb(70, 50, 140), Color.FromArgb(45, 70, 150));
            save.Click += (_, _) =>
            {
                ApplySettings(); d.Hide();
                bool english = _language.SelectedIndex == 1;
                if (english != L.En) SwitchLanguage(english);
            };
            close.Click += (_, _) => { PopulateSettings(); d.Hide(); };
            d.FormClosing += (_, e) => { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; PopulateSettings(); d.Hide(); } };
            d.Controls.Add(card); d.Controls.Add(save); d.Controls.Add(close);
            d.ResumeLayout(false);
            _settingsDialog = d;
        }
        PopulateSettings();
        if (!_settingsDialog.Visible) _settingsDialog.Show(this);
        _settingsDialog.Activate();
    }

    private async Task ToggleCaptureBorder()
    {
        if (_pending != 0 || _selector is not null || _editingWindow is not null) CancelSetup();
        bool resume = _scanner.Enabled;
        _scanner.SetEnabled(false); _overlay.SetPreview(null); _overlay.Hide();
        _toggle.Enabled = _select.Enabled = _move.Enabled = _border.Enabled = false;
        try
        {
            if (CaptureBorderPermission.Enabled)
            {
                CaptureBorderPermission.Disable();
                _hint.Text = L.T("Gelber Windows-Aufnahmerahmen wieder eingeschaltet.", "Yellow Windows capture border turned back on.");
            }
            else
            {
                var result = await CaptureBorderPermission.RequestAsync();
                if (_closing || IsDisposed) return;
                _hint.Text = result.Message;
            }
            _border.Text = CaptureBorderPermission.Enabled ? L.T("Gelben Rahmen anzeigen", "Show yellow border") : L.T("Gelben Rahmen ausblenden", "Hide yellow border");
        }
        finally
        {
            if (!_closing && !IsDisposed)
            {
                _toggle.Enabled = _select.Enabled = _move.Enabled = _border.Enabled = true;
                if (resume) _scanner.SetEnabled(true);
            }
        }
    }

    private void PopulateSettings()
    {
        if (_language.Items.Count == 0) _language.Items.AddRange(new object[] { "Deutsch", "English" });
        _language.SelectedIndex = L.En ? 1 : 0;
        _interval.Value = _settings.IntervalMs; _expiry.Value = _settings.ExpiryMs; _maxCount.Value = _settings.MaxCount;
        _textSize.Value = _settings.OverlayTextSize; _confidence.Value = (decimal)(_settings.MinimumConfidence * 100);
        _x.Value = (decimal)(_settings.OverlayX * 100); _y.Value = (decimal)(_settings.OverlayY * 100);
        _previewEnabled.Checked = _settings.PreviewEnabled; _filter.Text = _settings.Filter;
    }
    private void ApplySettings()
    {
        _settings = _settings with { IntervalMs = (int)_interval.Value, ExpiryMs = (int)_expiry.Value, MaxCount = (int)_maxCount.Value,
            OverlayTextSize = (int)_textSize.Value, MinimumConfidence = (double)_confidence.Value / 100,
            OverlayX = (double)_x.Value / 100, OverlayY = (double)_y.Value / 100, PreviewEnabled = _previewEnabled.Checked, Filter = _filter.Text.Trim() };
        _scanner.Configure(_settings, _table); SaveSettings(); _lastOverlayText = null;
        _hint.Text = L.T("Einstellungen gespeichert. Eine neue Zahl wird angezeigt, sobald sie zweimal hintereinander sicher gelesen wurde.", "Settings saved. A new number is shown once it has been read reliably twice in a row.");
    }
    private void SaveSettings()
    {
        try { SettingsStore.Save(_settingsPath, _settings); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        { _hint.Text = L.T("Einstellungen gelten nur bis zum Schließen: Der Programmordner ist nicht beschreibbar oder die Werte sind ungültig.", "Settings only apply until you close the app: the program folder is not writable or the values are invalid."); }
    }
    private void EditTable()
    {
        bool resume = _scanner.Enabled; _scanner.SetEnabled(false);
        using var editor = new TableEditor(_table);
        if (editor.ShowDialog(this) == DialogResult.OK && editor.Result is { } table)
        {
            try { table.Save(_tablePath); _table = table; _scanner.Configure(_settings, _table); _hint.Text = L.T($"{table.Entries.Count} Steine in der Liste gespeichert.", $"{table.Entries.Count} ores saved in the list."); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { MessageBox.Show(this, L.T("Die Steine-Liste konnte nicht gespeichert werden.", "The ore list could not be saved."), AppName); }
        }
        if (resume) _scanner.SetEnabled(true);
    }
    private void BeginSetup(int kind)
    {
        if (_pending != 0 || _selector is not null || _editingWindow is not null) CancelSetup();
        _resumeAfterSetup = _scanner.Enabled; _scanner.SetEnabled(false); _overlay.Hide();
        var screen = ScreenArea.Current();
        if (kind == 1)
        {
            _hint.Text = ScreenArea.GameFound
                ? L.T("Zieh jetzt auf dem Star-Citizen-Monitor einen Rahmen eng um die Signatur-Zahl. Rechtsklick oder Esc bricht ab.", "Now drag a tight box around the signature number on the Star Citizen monitor. Right-click or Esc cancels.")
                : L.T("Star Citizen läuft gerade nicht – die Auswahl erscheint auf dem Hauptmonitor. Rechtsklick oder Esc bricht ab.", "Star Citizen is not running – the selection appears on the main monitor. Right-click or Esc cancels.");
            WindowState = FormWindowState.Minimized;
            // Kurz warten, bis dieses Fenster weg ist – sonst wäre es auf dem Standbild zu sehen.
            Application.DoEvents(); Thread.Sleep(250);
            _selector = new RegionSelector(screen, region =>
            {
                _selector = null;
                if (region is not null)
                {
                    _settings = _settings with { Region = region }; SaveSettings(); _scanner.Configure(_settings, _table); UpdateRegionLabel();
                    _hint.Text = L.T("Bereich gespeichert! Jetzt Schritt 2: „Scan starten“.", "Area saved! Now step 2: 'Start scan'.");
                }
                else _hint.Text = L.T("Bereichsauswahl abgebrochen – der alte Bereich bleibt erhalten.", "Selection cancelled – the previous area is kept.");
                if (_resumeAfterSetup && !_closing) _scanner.SetEnabled(true); _resumeAfterSetup = false;
                if (!_closing && !IsDisposed) { WindowState = FormWindowState.Normal; Activate(); }
            });
            _selector.Show();
        }
        else
        {
            _editingWindow = screen;
            _hint.Text = L.T("Die Anzeige ist jetzt verschiebbar: mit der Maus an die gewünschte Stelle ziehen und loslassen.", "The display can now be moved: drag it with the mouse to where you want it and release.");
        }
    }
    private void CancelSetup()
    {
        _pending = 0; _editingWindow = null;
        var selector = _selector; _selector = null; selector?.Close(); _overlay.Hide();
        if (_resumeAfterSetup && !_closing) _scanner.SetEnabled(true);
        _resumeAfterSetup = false;
    }
    private void UpdateRegionLabel()
    {
        var r = _settings.Region;
        _regionLabel.Text = r is null ? L.T("Scan-Bereich:  noch nicht gewählt  →  zuerst Schritt 1", "Scan area:  not selected yet  →  do step 1 first") :
            L.T($"Scan-Bereich:  {Math.Round(r.Width * r.ClientWidth)} × {Math.Round(r.Height * r.ClientHeight)} px   ·   Monitor {r.ClientWidth} × {r.ClientHeight}", $"Scan area:  {Math.Round(r.Width * r.ClientWidth)} × {Math.Round(r.Height * r.ClientHeight)} px   ·   monitor {r.ClientWidth} × {r.ClientHeight}");
    }


    private void SetStatus(string status)
    {
        // Farbe nach dem (deutschen) Status des Scanners, Text in der gewählten Sprache.
        string de = status, en = status;
        Color c = Galaxy.SoftText;
        if (status.Contains("Aufnahmebereich neu auswählen", StringComparison.Ordinal)) { de = "Bitte zuerst Schritt 1: Bereich wählen"; en = "Please do step 1 first: select area"; c = Galaxy.Amber; }
        else if (status.StartsWith("Pausiert – Aufnahme nicht verfügbar", StringComparison.Ordinal)) { de = "Bildschirm konnte gerade nicht gelesen werden – versucht es weiter"; en = "Screen could not be read right now – retrying"; c = Galaxy.Amber; }
        else if (status.StartsWith("Pausiert", StringComparison.Ordinal)) { de = "Startet …"; en = "Starting …"; c = Galaxy.Amber; }
        else if (status == "Aktiv") { de = "Aktiv – liest die Zahl"; en = "Active – reading the number"; c = Galaxy.Green; }
        else if (status == "Keine Signatur") { de = "Aktiv – gerade keine Zahl im Rahmen"; en = "Active – no number in the box right now"; c = Galaxy.Cyan; }
        else if (status.StartsWith("Erkennung unsicher – letzter Wert veraltet", StringComparison.Ordinal)) { en = "Recognition uncertain – last value is old"; c = Galaxy.Amber; }
        else if (status.StartsWith("Erkennung unsicher", StringComparison.Ordinal)) { en = "Recognition uncertain"; c = Galaxy.Amber; }
        else if (status.StartsWith("Gestoppt –", StringComparison.Ordinal)) { en = "Stopped – OCR/capture not available; check resources"; c = Color.FromArgb(255, 130, 150); }
        else if (status == "Gestoppt") { en = "Stopped"; }
        else if (status.StartsWith("Wird beendet", StringComparison.Ordinal)) { en = "Closing …"; }
        string text = L.T(de, en);
        if (_status.Text != text) _status.Text = text;
        if (_status.ForeColor != c) _status.ForeColor = c;
    }

    private static void SetText(Control c, string text) { if (c.Text != text) c.Text = text; }

    private void TickUi()
    {
        if (_closing) return;
        _scanner.RefreshGate();
        if (_editingWindow is { } editWindow)
        {
            if (!ScreenArea.IsCurrent(editWindow)) { CancelSetup(); }
            else { ScannerController.MapRegion(_settings, editWindow, out var editRoi); _overlay.Present(editWindow, editRoi, _settings, "", true); }
        }

        // Hinweis, wenn dieses Fenster genau über dem Scan-Bereich liegt (dann sieht die App nur sich selbst).
        if (_settings.Region is { } reg && WindowState == FormWindowState.Normal && _selector is null)
        {
            var vs = ScreenArea.MonitorBounds;
            var scanRect = new Rectangle(vs.X + (int)(reg.X * vs.Width), vs.Y + (int)(reg.Y * vs.Height), (int)(reg.Width * vs.Width), (int)(reg.Height * vs.Height));
            string covered = L.T("Achtung: Dieses Fenster liegt über dem Scan-Bereich – bitte zur Seite oder auf den anderen Monitor schieben.", "Warning: this window is covering the scan area – please move it aside or to the other monitor.");
            if (Bounds.IntersectsWith(scanRect)) { if (_hint.Text != covered) _hint.Text = covered; }
            else if (_hint.Text == covered) _hint.Text = L.T("Gut – der Scan-Bereich ist frei.", "Good – the scan area is clear.");
        }

        var display = _scanner.ReadDisplay(out var preview);
        if (!_scanner.TryGetActiveWindow(out var window))
        {
            preview?.Dispose(); preview = null;
            _preview.Image?.Dispose(); _preview.Image = null;
            _overlay.SetPreview(null);
            if (_editingWindow is null) _overlay.Hide();
        }
        else if (preview is not null)
        {
            _overlay.SetPreview((Bitmap)preview.Clone());
            _preview.Image?.Dispose(); _preview.Image = preview;
        }
        else if (!_settings.PreviewEnabled || display.Status.StartsWith("Pausiert", StringComparison.Ordinal))
        { _preview.Image?.Dispose(); _preview.Image = null; _overlay.SetPreview(null); }
        SetText(_toggle, _scanner.Enabled ? L.T("2   Scan stoppen", "2   Stop scan") : L.T("2   Scan starten", "2   Start scan"));
        SetStatus(display.Status);
        string valueText = display.Value.HasValue ? display.Value.Value.ToString("N0") + (display.Stale ? L.T("   (alt)", "   (old)") : "") : "—";
        SetText(_value, valueText);
        var allMatches = display.Matches ?? [];
        var shown = allMatches.Where(x => string.IsNullOrWhiteSpace(_settings.Filter) || x.Entry.NameEn.Contains(_settings.Filter, StringComparison.OrdinalIgnoreCase) || x.Entry.NameDe.Contains(_settings.Filter, StringComparison.OrdinalIgnoreCase)).ToArray();
        string body;
        if (!display.Value.HasValue) body = L.T("Noch nichts gescannt.", "Nothing scanned yet.");
        else if (allMatches.Count == 0) body = L.T("Unbekannte Signatur\n(passt zu keinem Stein in der Liste)", "Unknown signature\n(does not match any ore in the list)");
        else if (shown.Length == 0) body = L.T($"{allMatches.Count} mögliche Treffer – keiner passt zum Filter.", $"{allMatches.Count} possible matches – none fits the filter.");
        else
        {
            var lines = shown.Select(x => $"{L.Ore(x.Entry)}   ·   {L.Rocks(x.Count)}").ToList();
            body = shown.Length == 1 ? lines[0] : L.T("Mehrere Möglichkeiten:\n", "Several possibilities:\n") + string.Join("\n", lines);
            if (shown.Length < allMatches.Count) body += L.T($"\n+ {allMatches.Count - shown.Length} weitere (durch Filter ausgeblendet)", $"\n+ {allMatches.Count - shown.Length} more (hidden by filter)");
        }
        SetText(_matches, display.Detail is null ? body : display.Detail + "\n" + body);
        var valueColor = display.Stale ? Galaxy.Amber : Color.White;
        if (_value.ForeColor != valueColor) _value.ForeColor = valueColor;
        // Seltenheit des besten Treffers als farbiges Badge, Erz-Text in der Seltenheitsfarbe
        Color matchColor = display.Value.HasValue && allMatches.Count == 0 ? Galaxy.Amber : Galaxy.Cyan;
        if (display.Value.HasValue && shown.Length > 0 && OreRarity.TryGet(shown[0].Entry.NameEn, out var rarityLabel, out var rarityColor))
        {
            SetText(_rarity, rarityLabel + (shown.Length > 1 ? L.T("  ·  mehrere möglich", "  ·  several possible") : ""));
            if (_rarity.ForeColor != rarityColor) _rarity.ForeColor = rarityColor;
            if (!_rarity.Visible) _rarity.Visible = true;
            if (shown.Length == 1) matchColor = rarityColor;
        }
        else if (_rarity.Visible) _rarity.Visible = false;
        if (_matches.ForeColor != matchColor) _matches.ForeColor = matchColor;
        if (window is not null && _editingWindow is null && _selector is null && _pending == 0 && ScreenArea.IsCurrent(window))
        {
            ScannerController.MapRegion(_settings, window, out var roi);
            // Overlay im Spiel: nur "Aktiv" und das Erz – keine Zusatzinfos.
            string ore = !display.Value.HasValue ? L.T("Suche Signatur …", "Searching signature …")
                : allMatches.Count == 0 ? L.T("Unbekannt", "Unknown")
                : string.Join("\n", (shown.Length > 0 ? shown : allMatches.ToArray()).Take(2).Select(x => $"{L.Ore(x.Entry)}  ·  {L.Rocks(x.Count)}"));
            string text = L.T("● Aktiv\n", "● Active\n") + ore;
            if (!_overlay.Present(window, roi, _settings, text)) _hint.Text = L.T("Kein freier Platz für die Anzeige. Schritt 3 „Overlay verschieben“ benutzen.", "No free space for the display. Use step 3 'Move overlay'.");
            _lastOverlayText = text;
        }
    }

    private async void OnClosing(object? sender, FormClosingEventArgs e)
    {
        if (_closed) return;
        e.Cancel = true;
        if (_closing) return;
        _closing = true; _timer.Stop(); _resumeAfterSetup = false; CancelSetup();
        _overlay.Close(); _preview.Image?.Dispose(); _preview.Image = null; Enabled = false;
        if (_settingsDialog is not null && !_settingsDialog.IsDisposed) _settingsDialog.Hide();
        SetStatus("Wird beendet …");
        try { await Task.Run(_scanner.Dispose); }
        catch (TimeoutException) { Environment.Exit(0); return; }
        _timer.Dispose(); _closed = true; Close();
    }
}
