using MiningScanner.Core;
using MiningScanner.Windows.UI;

namespace MiningScanner.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // DPI-awareness is established before ANY HWND/client-coordinate query.
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        L.Load();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, _) =>
        {
            MessageBox.Show(L.T("Ein interner Fehler ist aufgetreten. Das Programm wird vollständig beendet.", "An internal error occurred. The program will close."), MainForm.AppName);
            Environment.Exit(1);
        };
        if (!Environment.Is64BitProcess || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        {
            MessageBox.Show("Windows 10 ab Version 2004 oder Windows 11 (64 Bit) ist erforderlich.");
            return;
        }
        try
        {
            string root = AppContext.BaseDirectory;
            string settingsPath = Path.Combine(root, "settings.json");
            string tablePath = Path.Combine(root, "data", "signatures.json");
            var loaded = SettingsStore.Load(settingsPath);
            var table = SignatureTable.Load(tablePath);
            Application.Run(new MainForm(loaded.Settings, table, settingsPath, tablePath, loaded.Warning));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            MessageBox.Show(L.T("Das Programm konnte nicht sicher weiterlaufen. Bitte den vollständigen Ordner an einen beschreibbaren Ort legen. Die Erz-Liste muss gültiges JSON sein.\n\nDas Programm wird beendet.", "The program could not continue safely. Please put the complete folder in a writable location. The ore list must be valid JSON.\n\nThe program will close."), MainForm.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(1);
        }
    }
}
