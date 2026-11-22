using MiningScanner.Core;

namespace MiningScanner.Windows.UI;

internal sealed class TableEditor : Form
{
    private readonly TextBox _json = new() { Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = false, AcceptsTab = true, Font = new Font("Consolas", 11), Dock = DockStyle.Fill, MaxLength = 1_048_576 };
    public SignatureTable? Result { get; private set; }
    public TableEditor(SignatureTable table)
    {
        Text = L.T("Erz-Liste bearbeiten", "Edit ore list"); Size = new(960, 680); MinimumSize = new(640, 480); StartPosition = FormStartPosition.CenterParent;
        _json.Text = table.ToJson();
        var help = new Label { Text = L.T("Erz-Liste (JSON). Änderungen werden vor dem Speichern geprüft.\nImport/Export: JSON hier einfügen bzw. kopieren.", "Ore list (JSON). Changes are checked before saving.\nImport/export: paste or copy JSON here."), AutoSize = true, Dock = DockStyle.Top, Padding = new(10) };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft };
        var save = new Button { Text = L.T("Prüfen & übernehmen", "Check & apply"), Width = 190, Height = 34 };
        save.Click += (_, _) => { try { Result = SignatureTable.Parse(_json.Text); DialogResult = DialogResult.OK; Close(); } catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidDataException) { MessageBox.Show(this, ex.Message, L.T("Ungültige Liste", "Invalid list")); } };
        var cancel = new Button { Text = L.T("Abbrechen", "Cancel"), Width = 120, Height = 34, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(save); buttons.Controls.Add(cancel);
        Controls.Add(_json); Controls.Add(help); Controls.Add(buttons); CancelButton = cancel;
    }
}
