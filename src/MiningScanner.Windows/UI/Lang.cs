namespace MiningScanner.Windows.UI;

/// <summary>Einfache Sprachauswahl (Deutsch / English). Gespeichert in language.txt neben der App.</summary>
internal static class L
{
    public static bool En { get; private set; }
    private const bool DefaultEnglish = true; // startet auf Englisch, umstellbar in den Einstellungen
    private static string FilePath => Path.Combine(AppContext.BaseDirectory, "language.txt");

    public static void Load()
    {
        // Ohne gespeicherte Auswahl: Englisch.
        try { En = File.Exists(FilePath) ? File.ReadAllText(FilePath).Trim().Equals("en", StringComparison.OrdinalIgnoreCase) : DefaultEnglish; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { En = DefaultEnglish; }
    }

    public static bool Save(bool english)
    {
        En = english;
        try { File.WriteAllText(FilePath, english ? "en" : "de"); return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    public static string T(string de, string en) => En ? en : de;

    public static string Ore(MiningScanner.Core.SignatureEntry e) => En ? e.NameEn : e.NameDe;

    public static string Rocks(int n) => En ? (n == 1 ? "1 rock" : $"{n} rocks") : (n == 1 ? "1 Fels" : $"{n} Felsen");
}
