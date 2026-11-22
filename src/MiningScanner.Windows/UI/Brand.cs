namespace MiningScanner.Windows.UI;

/// <summary>
/// Name und Copyright der App an einer Stelle.
/// Normal gebaut = "Exi und Lina Mining Tool" (eigene Version).
/// Mit "WEITERGABE BAUEN.cmd" (Schalter SHARE) = "Mining Tool" zum Weitergeben.
/// </summary>
internal static class Brand
{
#if SHARE
    public const string AppName = "Mining Tool";
    public const string Title = "Mining Tool";
    public const string TitleEn = "Mining Tool";
    public const string Subtitle = "S I G N A T U R E    S C A N N E R";
#else
    public const string AppName = "Exi und Lina Mining Tool";
    public const string Title = "Exi und Lina";
    public const string TitleEn = "Exi and Lina";
    public const string Subtitle = "M I N I N G    T O O L";
#endif
    public const string Copyright = "© 2026 DulinaX & Execute";
}
