namespace MiningScanner.Windows.UI;

/// <summary>
/// Exi &amp; Lina: Nachschlage-Fenster "Wo finde ich welches Erz?".
/// Ansicht "Nach Erz": Seltenheit, Signatur, beste Fundorte, Raffinerie-Bonus.
/// Ansicht "Nach Ort": alle Orte (Planeten, Monde, Gürtel, Ringe) mit den Erz-Chancen für Schiff, ROC und Handabbau.
/// Reine Offline-Daten (Quellen: Tabelle von Ironanvil Gaming und scminer.rocks "Ore by Location", Patch 4.10.3).
/// </summary>
internal sealed class OreGuideForm : GalaxyForm
{
    private sealed record OreInfo(string Name, string Key, string Kind, string Rarity, string Signature, string Locations, string Refinery);
    private sealed record OreChance(string Ore, string Method, int Chance);
    private sealed record Place(string System, string Name, string Orbit, string Type, double Gravity, string Environment, IReadOnlyList<OreChance> Ores, string Refinery);

    private static readonly OreInfo[] Ores =
    [
        new("Quantainium", "Quantainium", "Schiff", "Legendär", "3170", "Aberdeen, Daymar, Clio, Wala, Lyria", "5 %"),
        new("Stileron", "Stileron", "Schiff", "Legendär", "3185", "Monox", "0 %"),
        new("Savrilium", "Savrilium", "Schiff", "Legendär", "3200", "Glaciem Ring, Keeger Belt, QV Breaker", "1 %"),
        new("Ouratite", "Ouratite", "Schiff", "Episch", "3370", "Aberdeen", "0 %"),
        new("Riccite", "Riccite", "Schiff", "Episch", "3385", "Ignis", "0 %"),
        new("Lindinium", "Lindinium", "Schiff", "Episch", "3400", "Glaciem Ring, Keeger Belt", "4 %"),
        new("Beryll (Beryl)", "Beryl", "Schiff", "Selten", "3540", "Wala", "8 %"),
        new("Taranite", "Taranite", "Schiff", "Selten", "3555", "Yela, Clio", "8 %"),
        new("Borase", "Borase", "Schiff", "Selten", "3570", "Fuego, Adir", "8 %"),
        new("Gold", "Gold", "Schiff", "Selten", "3585", "Terminus, Ignis, Vatra", "5 %"),
        new("Bexalite", "Bexalite", "Schiff", "Selten", "3600", "Glaciem Ring", "8 %"),
        new("Laranite", "Laranite", "Schiff", "Ungewöhnlich", "3825", "Wala, Lyria", "7 %"),
        new("Aslarite", "Aslarite", "Schiff", "Ungewöhnlich", "3840", "Fuego", "0 %"),
        new("Titan (Titanium)", "Titanium", "Schiff", "Ungewöhnlich", "3855", "Aberdeen", "8 %"),
        new("Wolfram (Tungsten)", "Tungsten", "Schiff", "Ungewöhnlich", "3870", "Fairo", "8 %"),
        new("Agricium", "Agricium", "Schiff", "Ungewöhnlich", "3885", "Vuur, Terminus", "8 %"),
        new("Torite", "Torite", "Schiff", "Ungewöhnlich", "3900", "Glaciem Ring", "1 %"),
        new("Hephaestanite", "Hephaestanite", "Schiff", "Gewöhnlich", "4180", "Fuego, Vuur, Monox", "8 %"),
        new("Zinn (Tin)", "Tin", "Schiff", "Gewöhnlich", "4195", "Pyro 1, Aberdeen, Arial, Ita", "9 %"),
        new("Quarz (Quartz)", "Quartz", "Schiff", "Gewöhnlich", "4210", "Daymar, Cellin, Yela", "8 %"),
        new("Korund (Corundum)", "Corundum", "Schiff", "Gewöhnlich", "4225", "Arial", "8 %"),
        new("Kupfer (Copper)", "Copper", "Schiff", "Gewöhnlich", "4240", "Terminus, Clio, Euterpe", "8 %"),
        new("Silizium (Raw Silicon)", "Silicon", "Schiff", "Gewöhnlich", "4255", "Daymar, Ignis", "8 %"),
        new("Eisen (Iron)", "Iron", "Schiff", "Gewöhnlich", "4270", "Fuego, Calliope, Lyria, Wala, Glaciem Ring", "8 %"),
        new("Aluminium (Aluminum)", "Aluminium", "Schiff", "Gewöhnlich", "4285", "Aberdeen, Arial, Ita, Magda, Keeger Belt", "8 %"),
        new("Eis (Raw Ice)", "Ice", "Schiff", "Gewöhnlich", "4300", "Terminus, Clio, Euterpe, Glaciem Ring", "1 %"),

        new("Hadanite", "Hadanite", "Hand (FPS)", "–", "3000 je Knoten", "Aberdeen", "–"),
        new("Dolivine", "Dolivine", "Hand (FPS)", "–", "3000 je Knoten", "Pyro 1, Monox, Ignis, Fuego, Wala, Clio, Ita", "–"),
        new("Aphorite", "Aphorite", "Hand (FPS)", "–", "3000 je Knoten", "Alle Monde und Planeten in Pyro und Stanton", "–"),
        new("Janalite", "Janalite", "Hand (FPS)", "–", "3000 je Knoten", "Höhlen: Hurston, Cellin, Daymar, Ignis, Fuego", "–"),

        new("Glacosite", "Glacosite", "Fahrzeug (ROC)", "–", "4000 je Knoten", "Yela, Monox", "–"),
        new("Beradom", "Beradom", "Fahrzeug (ROC)", "–", "4000 je Knoten", "Pyro 1, Bloom", "–"),
        new("Feynmaline", "Feynmaline", "Fahrzeug (ROC)", "–", "4000 je Knoten", "Pyro 4", "–"),

        new("Carinite", "Carinite", "Event", "–", "–", "Hathor", "–"),
        new("Saldynium", "Saldynium", "Event", "–", "–", "Hathor", "–"),
        new("Jaclium", "Jaclium", "Event", "–", "–", "Hathor", "–"),
        new("Sadaryx", "Sadaryx", "Event", "–", "–", "QV Breaker Stations", "–"),
    ];

    // System|Ort|umkreist|Typ|Schwerkraft|Umgebung|Schiff|ROC|Hand|Beste Raffinerie   (scminer.rocks, Patch 4.10.3)
    private static readonly string[] RawPlaces =
    [
        "Stanton|Aaron Halo|Stanton|Belt|0.00|Vacuum Belt|Beryl 18, Agricium 14, Aslarite 14, Titanium 14, Ice 10, Aluminium 10, Copper 10, Corundum 10, Hephaestanite 10, Iron 10, Silicon 10, Tin 10, Quantainium 2|||MIC-L5 +13% (Titanium)",
        "Stanton|Aberdeen|Hurston|Moon|0.38|Extreme Heat|Aluminium 30, Corundum 30, Agricium 29, Aslarite 29, Titanium 29, Ouratite 10, Beryl 2, Quantainium 2|Feynmaline 64, Glacosite 32, Beradom 4|Aphorite 59, Dolivine 35, Hadanite 6|MIC-L5 +13% (Titanium)",
        "Stanton|Arial|Hurston|Moon|0.32|Extreme Heat|Aluminium 22, Copper 22, Corundum 22, Hephaestanite 22, Quartz 22, Silicon 22, Tin 22, Agricium 10, Ouratite 10, Beryl 2, Quantainium 2|Feynmaline 52, Glacosite 32, Beradom 16|Aphorite 59, Dolivine 35, Hadanite 6|ARC-L1 +11% (Quartz)",
        "Stanton|ARC-L1|ArcCorp|Belt|0.00|Vacuum Belt|Tungsten 20, Taranite 13, Corundum 12, Aluminium 12, Hephaestanite 12|||MIC-L2 +9% (Tungsten)",
        "Stanton|ARC-L2|ArcCorp|Belt|0.00|Vacuum Belt|Tungsten 20, Taranite 13, Corundum 12, Aluminium 12, Hephaestanite 12|||MIC-L2 +9% (Tungsten)",
        "Stanton|ARC-L3|ArcCorp|Belt|0.00|Vacuum Belt|Agricium 9, Beryl 6, Iron 6, Copper 6, Ice 6|||MIC-L5 +9% (Copper)",
        "Stanton|ARC-L4|ArcCorp|Belt|0.00|Vacuum Belt|Tungsten 20, Taranite 13, Corundum 12, Aluminium 12, Hephaestanite 12|||MIC-L2 +9% (Tungsten)",
        "Stanton|ARC-L5|ArcCorp|Belt|0.00|Vacuum Belt|Aslarite 16, Gold 10, Iron 10, Copper 10, Ice 10|||MIC-L5 +9% (Copper)",
        "Nyx|Breaker Stations Interior|Nyx|Moon|0.35||||Aphorite 75, Sadaryx 25|",
        "Nyx|Breaker Stations Large Geode|Nyx|Moon|0.35||Savrilium 100, Torite 100|||MIC-L5 +8% (Torite)",
        "Stanton|Calliope|microTech|Moon|0.35|Extreme Cold|Iron 33, Hephaestanite 33, Ice 33, Quartz 33, Silicon 33, Beryl 2, Quantainium 2|Glacosite 84, Beradom 12, Feynmaline 4|Aphorite 61, Dolivine 37, Janalite 2|ARC-L1 +11% (Quartz)",
        "Stanton|Cellin|Crusader|Moon|0.34|Volcanic|Agricium 29, Aslarite 29, Titanium 29, Hephaestanite 26, Silicon 26, Quartz 26, Taranite 18, Beryl 2, Quantainium 2|Beradom 56, Glacosite 32, Feynmaline 12|Aphorite 59, Dolivine 35, Hadanite 6|MIC-L5 +13% (Titanium)",
        "Stanton|Clio|microTech|Moon|0.35|Extreme Cold|Copper 40, Ice 40, Tin 40, Taranite 18, Beryl 2, Quantainium 2|Glacosite 68, Beradom 20, Feynmaline 12|Aphorite 61, Dolivine 37, Janalite 2|Levski +9% (Tin)",
        "Stanton|CRU-L1|Crusader|Belt|0.00|Vacuum Belt|Titanium 15, Bexalite 10, Iron 9, Copper 9, Ice 9|||MIC-L5 +13% (Titanium)",
        "Stanton|CRU-L2|Crusader|Belt|0.00|Vacuum Belt|Titanium 15, Bexalite 10, Iron 9, Copper 9, Ice 9|||MIC-L5 +13% (Titanium)",
        "Stanton|CRU-L3|Crusader|Belt|0.00|Vacuum Belt|Torite 11, Aluminium 9, Corundum 9, Hephaestanite 9|||Levski +8% (Aluminium)",
        "Stanton|CRU-L4|Crusader|Belt|0.00|Vacuum Belt|Aslarite 16, Gold 10, Iron 10, Copper 10, Ice 10|||MIC-L5 +9% (Copper)",
        "Stanton|CRU-L5|Crusader|Belt|0.00|Vacuum Belt|Agricium 9, Beryl 6, Iron 6, Copper 6, Ice 6|||MIC-L5 +9% (Copper)",
        "Stanton|Daymar|Crusader|Moon|0.35|Dense Atmosphere|Hephaestanite 35, Silicon 35, Quartz 35, Agricium 29, Aslarite 29, Titanium 29, Beryl 2, Quantainium 2|Beradom 48, Feynmaline 40, Glacosite 12|Aphorite 59, Dolivine 35, Hadanite 6|MIC-L5 +13% (Titanium)",
        "Stanton|Euterpe|microTech|Moon|0.34|Extreme Cold|Copper 40, Ice 40, Tin 40, Taranite 18, Beryl 2, Quantainium 2|Glacosite 88, Beradom 12, Feynmaline 0|Aphorite 61, Dolivine 37, Janalite 2|Levski +9% (Tin)",
        "Stanton|Hathor Caves|Stanton|Moon|0.35||Carinite 100||Aphorite 30, Jaclium 19, Dolivine 18, Saldynium 16, Hadanite 3, Janalite 1|",
        "Stanton|HUR-L1|Hurston|Belt|0.00|Vacuum Belt|Laranite 13, Borase 8, Aluminium 8, Corundum 8, Hephaestanite 8|||MIC-L5 +9% (Borase)",
        "Stanton|HUR-L2|Hurston|Belt|0.00|Vacuum Belt|Tungsten 20, Taranite 13, Corundum 12, Aluminium 12, Hephaestanite 12|||MIC-L2 +9% (Tungsten)",
        "Stanton|HUR-L3|Hurston|Belt|0.00|Vacuum Belt|Titanium 15, Bexalite 10, Iron 9, Copper 9, Ice 9|||MIC-L5 +13% (Titanium)",
        "Stanton|HUR-L4|Hurston|Belt|0.00|Vacuum Belt|Laranite 13, Borase 8, Aluminium 8, Corundum 8, Hephaestanite 8|||MIC-L5 +9% (Borase)",
        "Stanton|HUR-L5|Hurston|Belt|0.00|Vacuum Belt|Torite 11, Aluminium 9, Corundum 9, Hephaestanite 9|||Levski +8% (Aluminium)",
        "Stanton|Hurston|Stanton|Planet|1.00|Toxic Atmosphere|Aluminium 44, Copper 44, Corundum 44, Tin 44, Agricium 10, Ouratite 10, Beryl 2, Quantainium 2|Beradom 64, Glacosite 32, Feynmaline 4|Aphorite 59, Dolivine 35, Hadanite 6|Levski +9% (Tin)",
        "Stanton|Ita|Hurston|Moon|0.35||Copper 35, Tin 35, Aluminium 35, Corundum 35, Agricium 29, Aslarite 29, Titanium 29, Beryl 2, Quantainium 2|Glacosite 69, Feynmaline 27, Beradom 4|Aphorite 59, Dolivine 35, Hadanite 6|MIC-L5 +13% (Titanium)",
        "Stanton|Lyria|ArcCorp|Moon|0.34|Extreme Cold|Copper 35, Iron 35, Tin 35, Laranite 28, Tungsten 28, Beryl 2, Quantainium 2|Beradom 48, Glacosite 48, Feynmaline 4|Aphorite 61, Dolivine 37, Janalite 2|Levski +9% (Tin)",
        "Stanton|Magda|Hurston|Moon|0.32|Low Gravity|Aluminium 35, Corundum 35, Iron 35, Agricium 14, Aslarite 14, Titanium 14, Beryl 2, Quantainium 2|Glacosite 59, Feynmaline 37, Beradom 4|Aphorite 59, Dolivine 35, Hadanite 6|MIC-L5 +13% (Titanium)",
        "Stanton|MIC-L1|microTech|Belt|0.00|Vacuum Belt|Torite 11, Aluminium 9, Corundum 9, Hephaestanite 9|||Levski +8% (Aluminium)",
        "Stanton|MIC-L2|microTech|Belt|0.00|Vacuum Belt|Torite 11, Aluminium 9, Corundum 9, Hephaestanite 9|||Levski +8% (Aluminium)",
        "Stanton|MIC-L3|microTech|Belt|0.00|Vacuum Belt|Aslarite 16, Gold 10, Iron 10, Copper 10, Ice 10|||MIC-L5 +9% (Copper)",
        "Stanton|MIC-L4|microTech|Belt|0.00|Vacuum Belt|Agricium 9, Beryl 6, Iron 6, Copper 6, Ice 6|||MIC-L5 +9% (Copper)",
        "Stanton|MIC-L5|microTech|Belt|0.00|Vacuum Belt|Torite 11, Aluminium 9, Corundum 9, Hephaestanite 9|||Levski +8% (Aluminium)",
        "Stanton|microTech|Stanton|Planet|1.00|Extreme Cold|Hephaestanite 33, Ice 33, Quartz 33, Silicon 33, Iron 33, Beryl 2, Quantainium 2|Glacosite 68, Beradom 28, Feynmaline 4|Aphorite 61, Dolivine 37, Janalite 2|ARC-L1 +11% (Quartz)",
        "Stanton|Wala|ArcCorp|Moon|0.31|Low Gravity|Laranite 29, Tungsten 29, Copper 26, Tin 26, Iron 26, Beryl 18, Quantainium 2|Beradom 56, Glacosite 40, Feynmaline 4|Aphorite 61, Dolivine 37, Janalite 2|Levski +9% (Tin)",
        "Stanton|Yela|Crusader|Moon|0.35|Extreme Cold|Agricium 29, Aslarite 29, Titanium 29, Hephaestanite 26, Silicon 26, Quartz 26, Taranite 18, Beryl 2, Quantainium 2|Glacosite 68, Beradom 24, Feynmaline 8|Aphorite 59, Dolivine 35, Hadanite 6|MIC-L5 +13% (Titanium)",
        "Stanton|Yela Asteroid Belt|Stanton|Belt|0.00|Vacuum Belt|Agricium 29, Aslarite 29, Titanium 29, Copper 21, Ice 21, Iron 21, Tin 21, Ouratite 10|||MIC-L5 +13% (Titanium)",
        "Pyro|Akiro Cluster|Pyro|Belt|0.00|Vacuum Belt|Torite 29, Copper 15, Hephaestanite 15, Ice 15, Silicon 15, Tin 15, Iron 15, Laranite 10, Riccite 10, Stileron 2, Taranite 2|||Levski +9% (Tin)",
        "Pyro|Pyro I|Pyro|Planet|0.85|Extreme Heat|Iron 33, Copper 33, Tin 33, Stileron 2, Taranite 2|Beradom 100|Aphorite 59, Dolivine 35, Hadanite 6|Levski +9% (Tin)",
        "Pyro|Pyro II (Monox)|Pyro|Planet|0.90|Toxic Atmosphere|Hephaestanite 33, Quartz 33, Silicon 33, Copper 33, Iron 33, Tin 33, Stileron 2, Taranite 2|Glacosite 100|Aphorite 59, Dolivine 35, Hadanite 6|ARC-L1 +11% (Quartz)",
        "Pyro|Pyro III (Bloom)|Pyro|Planet|0.95|Volcanic|Hephaestanite 35, Iron 35, Quartz 35, Silicon 35, Bexalite 18, Borase 18, Gold 18, Laranite 10, Riccite 10, Stileron 2, Taranite 2|Beradom 100|Aphorite 61, Dolivine 37, Janalite 2|MIC-L5 +12% (Bexalite)",
        "Pyro|Pyro IV|Pyro|Planet|0.70|Low Gravity|Laranite 29, Tungsten 29, Copper 26, Iron 26, Tin 26, Bexalite 18, Borase 18, Gold 18, Stileron 2, Taranite 2|Feynmaline 100|Aphorite 59, Dolivine 35, Hadanite 6|MIC-L5 +12% (Bexalite)",
        "Pyro|Pyro V-a (Ignis)|Pyro V|Moon|0.35|Extreme Heat|Copper 36, Hephaestanite 36, Silicon 36, Tin 36, Bexalite 18, Borase 18, Gold 18, Laranite 10, Riccite 10|Feynmaline 100|Aphorite 59, Dolivine 35, Hadanite 6|MIC-L5 +12% (Bexalite)",
        "Pyro|Pyro V-b (Vatra)|Pyro V|Moon|0.34|Volcanic|Hephaestanite 36, Iron 36, Silicon 36, Bexalite 18, Borase 18, Gold 18, Laranite 10, Riccite 10|Beradom 50, Glacosite 50|Aphorite 59, Dolivine 35, Hadanite 6|MIC-L5 +12% (Bexalite)",
        "Pyro|Pyro V-c (Adir)|Pyro V|Moon|0.32|Extreme Heat|Iron 44, Laranite 29, Tungsten 29, Bexalite 18, Borase 18, Gold 18, Riccite 10|Glacosite 100|Aphorite 59, Dolivine 35, Hadanite 6|MIC-L5 +12% (Bexalite)",
        "Pyro|Pyro V-d (Fairo)|Pyro V|Moon|0.33|Volcanic|Laranite 29, Tungsten 29, Hephaestanite 27, Silicon 27, Iron 27, Bexalite 9, Borase 9, Gold 9|Beradom 100|Aphorite 59, Dolivine 35, Hadanite 6|MIC-L5 +12% (Bexalite)",
        "Pyro|Pyro V-e (Fuego)|Pyro V|Moon|0.35|Toxic Atmosphere|Agricium 29, Aslarite 29, Titanium 29, Iron 27, Hephaestanite 27, Quartz 27, Silicon 27, Bexalite 9, Borase 9, Gold 9|Feynmaline 100|Aphorite 61, Dolivine 37, Janalite 2|MIC-L5 +13% (Titanium)",
        "Pyro|Pyro V-f (Vuur)|Pyro V|Moon|0.33|Volcanic|Hephaestanite 27, Iron 27, Quartz 27, Silicon 27, Bexalite 18, Borase 18, Gold 18, Agricium 14, Aslarite 14, Titanium 14|Glacosite 100|Aphorite 59, Dolivine 35, Hadanite 6|MIC-L5 +13% (Titanium)",
        "Pyro|Pyro VI (Terminus)|Pyro|Planet|0.80|Extreme Cold|Copper 21, Ice 21, Tin 21, Bexalite 18, Borase 18, Gold 18, Agricium 14, Aslarite 14, Titanium 14, Laranite 10, Riccite 10, Stileron 2, Taranite 2|Feynmaline 100|Aphorite 61, Dolivine 37, Janalite 2|MIC-L5 +13% (Titanium)",
        "Nyx|Glaciem Ring|Nyx|Belt|0.00|Vacuum Belt|Torite 29, Ice 21, Iron 21, Bexalite 18, Borase 18, Gold 18, Lindinium 10, Tungsten 10, Savrilium 2|||MIC-L5 +12% (Bexalite)",
        "Nyx|Keeger Belt|Nyx|Belt|0.00|Vacuum Belt|Torite 29, Aluminium 21, Corundum 21, Ice 21, Bexalite 18, Borase 18, Gold 18, Lindinium 10, Tungsten 10, Savrilium 2|||MIC-L5 +12% (Bexalite)"
    ];

    private static readonly Place[] Places = RawPlaces.Select(ParsePlace).ToArray();

    private static Place ParsePlace(string line)
    {
        var p = line.Split('|');
        var ores = new List<OreChance>();
        void Add(string list, string method)
        {
            foreach (var part in list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int space = part.LastIndexOf(' ');
                if (space > 0 && int.TryParse(part[(space + 1)..], out int chance))
                    ores.Add(new OreChance(part[..space], method, chance));
            }
        }
        Add(p[6], "Schiff"); Add(p[7], "Fahrzeug (ROC)"); Add(p[8], "Hand (FPS)");
        double.TryParse(p[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double g);
        return new Place(p[0], p[1], p[2], p[3], g, p[5], ores, p[9]);
    }

    private readonly ListView _list = new SmoothListView();
    private readonly ListView _places = new SmoothListView();
    private readonly ListView _placeOres = new SmoothListView();
    private readonly Label _placeInfo = new();
    private readonly TextBox _search = new();
    private readonly Label _count = new();
    private readonly GlowButton _byOre = new(), _byPlace = new();
    private readonly Font _headerFont = new("Segoe UI", 9.5f, FontStyle.Bold);
    private bool _placeMode;
    private readonly SplitGrip _grip = new();
    private bool _dragging;
    private int _dragOffset;

    public OreGuideForm()
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = MainForm.AppName + L.T(" – Wo finde ich welches Erz?", " – Where do I find which ore?");
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 10);
        ClientSize = new Size(1000, 680);
        MinimumSize = new Size(820, 520);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;

        var title = new GradientLabel { Text = L.T("Wo finde ich welches Erz?", "Where do I find which ore?"), Bounds = new Rectangle(14, 6, 700, 54), Font = new Font("Segoe UI", 21, FontStyle.Bold) };
        var searchLabel = new Label { Text = L.T("Suchen – Erz oder Ort eintippen (z. B. „Gold“, „Aberdeen“ oder „Ring“):", "Search – type an ore or a location (e.g. \"Gold\", \"Aberdeen\" or \"Ring\"):"), Bounds = new Rectangle(24, 64, 560, 22), ForeColor = Galaxy.SoftText, BackColor = Color.Transparent };
        _search.Bounds = new Rectangle(24, 90, 330, 30);
        _search.Font = new Font("Segoe UI", 11);
        _search.BackColor = Galaxy.Field; _search.ForeColor = Color.White; _search.BorderStyle = BorderStyle.FixedSingle;
        _count.Bounds = new Rectangle(364, 95, 230, 24); _count.ForeColor = Galaxy.Caption; _count.BackColor = Color.Transparent; _count.AutoEllipsis = true;

        _byOre.Text = L.T("Nach Erz", "By ore"); _byOre.Bounds = new Rectangle(604, 86, 180, 36);
        _byPlace.Text = L.T("Nach Ort", "By location"); _byPlace.Bounds = new Rectangle(796, 86, 180, 36);
        _byOre.Anchor = _byPlace.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _byOre.Click += (_, _) => SetMode(false);
        _byPlace.Click += (_, _) => SetMode(true);

        // Ansicht "Nach Erz"
        StyleList(_list);
        _list.Bounds = new Rectangle(24, 132, 952, 476);
        _list.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _list.Columns.Add(L.T("Erz", "Ore"), 165);
        _list.Columns.Add(L.T("Abbau mit", "Mined with"), 105);
        _list.Columns.Add(L.T("Seltenheit", "Rarity"), 100);
        _list.Columns.Add(L.T("Signatur 1×", "Signature 1×"), 95);
        _list.Columns.Add(L.T("Beste Fundorte (Chance, Schwerkraft)", "Best locations (chance, gravity)"), 400);
        _list.Columns.Add("Levski", 80);
        _list.ItemActivate += (_, _) =>
        {
            if (_list.SelectedItems.Count == 0 || _list.SelectedItems[0].Tag is not OreInfo ore) return;
            SetMode(true);
            _search.Text = ore.Key;
        };

        // Ansicht "Nach Ort"
        StyleList(_places);
        _places.Bounds = new Rectangle(24, 132, 380, 476);
        _places.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
        _places.Columns.Add(L.T("Ort", "Location"), 140);
        _places.Columns.Add("System", 62);
        _places.Columns.Add(L.T("Schwerkraft", "Gravity"), 92);
        _places.Columns.Add(L.T("Chance", "Chance"), 66);
        _places.SelectedIndexChanged += (_, _) => ShowPlace();
        // Spalten füllen immer die ganze Breite -> keine weiße Fläche in der Kopfzeile
        _list.ClientSizeChanged += (_, _) => FitColumns(_list, 4);
        _places.ClientSizeChanged += (_, _) => FitColumns(_places, 0);
        _placeOres.ClientSizeChanged += (_, _) => FitColumns(_placeOres, 0);

        _placeInfo.Bounds = new Rectangle(418, 132, 558, 64);
        _placeInfo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _placeInfo.ForeColor = Galaxy.Text; _placeInfo.BackColor = Color.Transparent; _placeInfo.Font = new Font("Segoe UI", 10.5f);
        _placeInfo.UseMnemonic = false;

        StyleList(_placeOres);
        _placeOres.Bounds = new Rectangle(418, 200, 558, 408);

        // Griff zwischen Ortsliste und Details: mit der Maus ziehen = Liste breiter/schmaler
        _grip.Bounds = new Rectangle(406, 132, 10, 476);
        _grip.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
        _grip.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { _dragging = true; _dragOffset = e.X; _grip.Capture = true; } };
        _grip.MouseMove += (_, e) => { if (_dragging) MoveSplit(_grip.Left + e.X - _dragOffset); };
        _grip.MouseUp += (_, _) => { _dragging = false; _grip.Capture = false; };
        _grip.DoubleClick += (_, _) => MoveSplit(_places.Left + (int)(380 * DeviceDpi / 96f));
        _placeOres.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _placeOres.Columns.Add(L.T("Erz", "Ore"), 230);
        _placeOres.Columns.Add(L.T("Abbau mit", "Mined with"), 150);
        _placeOres.Columns.Add(L.T("Chance", "Chance"), 90);
        _placeOres.Columns.Add(L.T("Seltenheit", "Rarity"), 120);

        var source = new Label
        {
            Text = L.T("Quellen: Tabelle von Ironanvil Gaming und scminer.rocks „Ore by Location“ (Patch 4.10.3). Chance = Wahrscheinlichkeit für dieses Erz am Ort.\nDoppelklick auf ein Erz zeigt alle Orte dafür. Werte können sich mit neuen Patches ändern.",
                         "Sources: table by Ironanvil Gaming and scminer.rocks \"Ore by Location\" (patch 4.10.3). Chance = probability of this ore at the location.\nDouble-click an ore to see all its locations. Values may change with new patches."),
            Bounds = new Rectangle(24, 616, 952, 52), ForeColor = Galaxy.SoftText, BackColor = Color.Transparent,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, Font = new Font("Segoe UI", 9)
        };

        Controls.AddRange([title, searchLabel, _search, _count, _byOre, _byPlace, _list, _places, _grip, _placeInfo, _placeOres, source]);
        _search.TextChanged += (_, _) => Fill();
        SetMode(false);
        ResumeLayout(false);
    }

    private void StyleList(ListView list)
    {
        list.View = View.Details;
        list.FullRowSelect = true;
        list.HideSelection = false;
        list.MultiSelect = false;
        list.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        list.BorderStyle = BorderStyle.None;
        list.BackColor = Color.FromArgb(16, 10, 40);
        list.ForeColor = Galaxy.Text;
        list.Font = new Font("Segoe UI", 10.5f);
        list.ShowItemToolTips = true;
        list.OwnerDraw = true;
        list.DrawColumnHeader += (_, e) =>
        {
            using (var b = new SolidBrush(Color.FromArgb(44, 26, 88))) e.Graphics.FillRectangle(b, e.Bounds);
            var r = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 8), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? "", _headerFont, r, Galaxy.Caption,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        };
        list.DrawItem += (_, _) => { };
        list.DrawSubItem += (_, e) => e.DrawDefault = true;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        float scale = DeviceDpi / 96f;
        if (scale > 1.01f)
            foreach (var list in new[] { _list, _places, _placeOres })
                foreach (ColumnHeader column in list.Columns) column.Width = (int)(column.Width * scale);
        FitColumns(_list, 4); FitColumns(_places, 0); FitColumns(_placeOres, 0);
        _search.Focus();
    }

    private static bool _fitting;

    /// <summary>Eine Spalte so breit machen, dass alle Spalten zusammen genau die Liste füllen.</summary>
    private static void FitColumns(ListView list, int stretch)
    {
        if (_fitting || list.Columns.Count <= stretch || !list.IsHandleCreated) return;
        _fitting = true;
        try
        {
            int others = 0;
            for (int i = 0; i < list.Columns.Count; i++) if (i != stretch) others += list.Columns[i].Width;
            int width = Math.Max((int)(90 * list.DeviceDpi / 96f), list.ClientSize.Width - others);
            if (list.Columns[stretch].Width != width) list.Columns[stretch].Width = width;
        }
        finally { _fitting = false; }
    }

    private void SetMode(bool placeMode)
    {
        _placeMode = placeMode;
        _list.Visible = !placeMode;
        _places.Visible = _grip.Visible = _placeInfo.Visible = _placeOres.Visible = placeMode;
        _byOre.SetColors(placeMode ? Color.FromArgb(70, 50, 140) : Color.FromArgb(205, 60, 200), placeMode ? Color.FromArgb(45, 70, 150) : Color.FromArgb(115, 70, 240));
        _byPlace.SetColors(placeMode ? Color.FromArgb(205, 60, 200) : Color.FromArgb(70, 50, 140), placeMode ? Color.FromArgb(115, 70, 240) : Color.FromArgb(45, 70, 150));
        Fill();
    }

    private static Color RarityColor(string rarity, string kind) => rarity switch
    {
        "Legendär" => Color.FromArgb(255, 160, 60),
        "Episch" => Color.FromArgb(205, 110, 255),
        "Selten" => Color.FromArgb(110, 155, 255),
        "Ungewöhnlich" => Color.FromArgb(95, 210, 110),
        "Gewöhnlich" => Color.FromArgb(240, 205, 95),
        _ => kind == "Event" ? Galaxy.Cyan : Color.FromArgb(255, 120, 140)
    };

    private static readonly Dictionary<string, string> English = new()
    {
        ["Schiff"] = "Ship", ["Hand (FPS)"] = "Hand (FPS)", ["Fahrzeug (ROC)"] = "Vehicle (ROC)", ["Event"] = "Event",
        ["Legendär"] = "Legendary", ["Episch"] = "Epic", ["Selten"] = "Rare", ["Ungewöhnlich"] = "Uncommon", ["Gewöhnlich"] = "Common",
        ["3000 je Knoten"] = "3000 per node", ["4000 je Knoten"] = "4000 per node",
        ["Alle Monde und Planeten in Pyro und Stanton"] = "All moons and planets in Pyro and Stanton",
        ["Höhlen: Hurston, Cellin, Daymar, Ignis, Fuego"] = "Caves: Hurston, Cellin, Daymar, Ignis, Fuego",
        ["Beryll (Beryl)"] = "Beryl", ["Titan (Titanium)"] = "Titanium", ["Wolfram (Tungsten)"] = "Tungsten",
        ["Zinn (Tin)"] = "Tin", ["Quarz (Quartz)"] = "Quartz", ["Korund (Corundum)"] = "Corundum", ["Kupfer (Copper)"] = "Copper",
        ["Silizium (Raw Silicon)"] = "Raw Silicon", ["Eisen (Iron)"] = "Iron", ["Aluminium (Aluminum)"] = "Aluminum", ["Eis (Raw Ice)"] = "Raw Ice",
    };

    private static readonly Dictionary<string, (string De, string En)> Words = new()
    {
        ["Belt"] = ("Gürtel", "Belt"), ["Moon"] = ("Mond", "Moon"), ["Planet"] = ("Planet", "Planet"),
        ["Extreme Heat"] = ("Extreme Hitze", "Extreme heat"), ["Extreme Cold"] = ("Extreme Kälte", "Extreme cold"),
        ["Dense Atmosphere"] = ("Dichte Atmosphäre", "Dense atmosphere"), ["Toxic Atmosphere"] = ("Giftige Atmosphäre", "Toxic atmosphere"),
        ["Volcanic"] = ("Vulkanisch", "Volcanic"), ["Low Gravity"] = ("Geringe Schwerkraft", "Low gravity"), ["Vacuum Belt"] = ("Vakuum", "Vacuum"),
    };

    private static string Tr(string text) => L.En && English.TryGetValue(text, out var en) ? en : text;
    private static string Word(string text) => Words.TryGetValue(text, out var w) ? (L.En ? w.En : w.De) : text;
    private static OreInfo? Info(string key) => Ores.FirstOrDefault(o => o.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
    private static string OreLabel(string key) => Info(key) is { } o ? Tr(o.Name) : key;

    private static bool OreMatches(OreInfo ore, string q) =>
        ore.Key.Contains(q, StringComparison.OrdinalIgnoreCase) || ore.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || Tr(ore.Name).Contains(q, StringComparison.OrdinalIgnoreCase);

    private static string BestLocations(OreInfo ore)
    {
        var best = Places.SelectMany(p => p.Ores.Where(o => o.Ore.Equals(ore.Key, StringComparison.OrdinalIgnoreCase) && o.Chance > 0).Select(o => (Place: p.Name, o.Chance, p.Gravity)))
            .GroupBy(x => x.Place).Select(g => (Place: g.Key, Chance: g.Max(x => x.Chance), Gravity: g.First().Gravity))
            .OrderByDescending(x => x.Chance).ThenBy(x => x.Place).ToList();
        if (best.Count == 0) return Tr(ore.Locations);
        string text = string.Join(", ", best.Take(4).Select(x => $"{x.Place} {x.Chance} %" + (x.Gravity > 0 ? $" ({Grav(x.Gravity)})" : "")));
        return best.Count > 4 ? text + L.T($"  (+{best.Count - 4} weitere)", $"  (+{best.Count - 4} more)") : text;
    }

    private void Fill()
    {
        string q = _search.Text.Trim();
        if (_placeMode) { FillPlaces(q); return; }
        _list.BeginUpdate();
        _list.Items.Clear();
        int shown = 0;
        foreach (var ore in Ores)
        {
            string all = string.Join(" | ", ore.Name, ore.Key, ore.Kind, ore.Rarity, ore.Locations, Tr(ore.Name), Tr(ore.Kind), Tr(ore.Rarity), Tr(ore.Locations), BestLocations(ore));
            if (q.Length > 0 && !all.Contains(q, StringComparison.OrdinalIgnoreCase)) continue;
            var item = new ListViewItem(new[] { Tr(ore.Name), Tr(ore.Kind), Tr(ore.Rarity), Tr(ore.Signature), BestLocations(ore), ore.Refinery })
            {
                ForeColor = RarityColor(ore.Rarity, ore.Kind),
                UseItemStyleForSubItems = true,
                ToolTipText = Tr(ore.Name) + ": " + BestLocations(ore),
                Tag = ore
            };
            _list.Items.Add(item);
            shown++;
        }
        _list.EndUpdate();
        FitColumns(_list, 4);
        _count.Text = q.Length == 0 ? L.T($"{shown} Erze", $"{shown} ores") : shown == 0 ? L.T("Nichts gefunden.", "Nothing found.") : L.T($"{shown} Treffer", $"{shown} results");
    }

    private void FillPlaces(string q)
    {
        var matchingOres = q.Length == 0 ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : Ores.Where(o => OreMatches(o, q)).Select(o => o.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rows = new List<(Place Place, int Chance)>();
        foreach (var place in Places)
        {
            int chance = place.Ores.Where(o => matchingOres.Contains(o.Ore)).Select(o => o.Chance).DefaultIfEmpty(-1).Max();
            bool nameHit = q.Length == 0 || place.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || place.Orbit.Contains(q, StringComparison.OrdinalIgnoreCase)
                           || place.System.Contains(q, StringComparison.OrdinalIgnoreCase) || Word(place.Type).Contains(q, StringComparison.OrdinalIgnoreCase);
            if (nameHit || chance > 0) rows.Add((place, chance));
        }
        if (matchingOres.Count > 0) rows = rows.OrderByDescending(r => r.Chance).ThenBy(r => r.Place.Name).ToList();
        string? selected = _places.SelectedItems.Count > 0 ? _places.SelectedItems[0].Text : null;
        _places.BeginUpdate();
        _places.Items.Clear();
        foreach (var (place, chance) in rows)
        {
            var item = new ListViewItem(new[] { place.Name, place.System, place.Gravity > 0 ? Grav(place.Gravity) : "0 G", chance > 0 ? $"{chance} %" : "" })
            {
                Tag = place,
                ForeColor = place.System switch { "Pyro" => Color.FromArgb(255, 170, 110), "Nyx" => Color.FromArgb(170, 200, 255), _ => Galaxy.Text },
                UseItemStyleForSubItems = true,
                ToolTipText = $"{place.Name} – {Word(place.Type)}, {place.System}  ·  " + GravHint(place.Gravity)
            };
            _places.Items.Add(item);
        }
        _places.EndUpdate();
        FitColumns(_places, 0);
        _count.Text = rows.Count == 0 ? L.T("Nichts gefunden.", "Nothing found.") : L.T($"{rows.Count} Orte", $"{rows.Count} locations");
        ListViewItem? toSelect = _places.Items.Cast<ListViewItem>().FirstOrDefault(i => i.Text == selected) ?? (_places.Items.Count > 0 ? _places.Items[0] : null);
        if (toSelect is not null) { toSelect.Selected = true; toSelect.EnsureVisible(); }
        else ShowPlace();
    }

    private void ShowPlace()
    {
        _placeOres.BeginUpdate();
        _placeOres.Items.Clear();
        if (_places.SelectedItems.Count == 0 || _places.SelectedItems[0].Tag is not Place place)
        {
            _placeInfo.Text = L.T("Links einen Ort auswählen.", "Select a location on the left.");
            _placeOres.EndUpdate();
            return;
        }
        string orbit = place.Orbit == place.System ? "" : L.T($" von {place.Orbit}", $" of {place.Orbit}");
        string env = place.Environment.Length > 0 ? "  ·  " + Word(place.Environment) : "";
        string grav = "  ·  " + GravHint(place.Gravity);
        string refinery = place.Refinery.Length > 0 ? L.T("\nBeste Raffinerie: ", "\nBest refinery: ") + place.Refinery : "";
        _placeInfo.Text = $"{place.Name}  ·  {Word(place.Type)}{orbit}  ·  {place.System}{grav}{env}{refinery}";

        string q = _search.Text.Trim();
        foreach (var ore in place.Ores.OrderBy(o => o.Method == "Schiff" ? 0 : o.Method.StartsWith("Fahrzeug", StringComparison.Ordinal) ? 1 : 2).ThenByDescending(o => o.Chance))
        {
            var info = Info(ore.Ore);
            bool highlight = q.Length > 0 && info is not null && OreMatches(info, q);
            var item = new ListViewItem(new[] { OreLabel(ore.Ore), Tr(ore.Method), $"{ore.Chance} %", info is null ? "" : Tr(info.Rarity) })
            {
                ForeColor = info is null ? Galaxy.Text : RarityColor(info.Rarity, info.Kind),
                BackColor = highlight ? Color.FromArgb(70, 40, 110) : Color.FromArgb(16, 10, 40),
                UseItemStyleForSubItems = true
            };
            _placeOres.Items.Add(item);
        }
        _placeOres.EndUpdate();
        FitColumns(_placeOres, 0);
    }

    /// <summary>Trennlinie verschieben: links die Ortsliste, rechts Ort-Infos und Erz-Liste.</summary>
    private void MoveSplit(int x)
    {
        float s = DeviceDpi / 96f;
        int min = _places.Left + (int)(220 * s);
        int max = ClientSize.Width - (int)(320 * s);
        x = Math.Clamp(x, min, Math.Max(min, max));
        int gap = (int)(2 * s), right = ClientSize.Width - (int)(24 * s);
        SuspendLayout();
        _places.Width = x - gap - _places.Left;
        _grip.Left = x;
        int left = x + _grip.Width + gap;
        _placeInfo.SetBounds(left, _placeInfo.Top, Math.Max(50, right - left), _placeInfo.Height);
        _placeOres.SetBounds(left, _placeOres.Top, Math.Max(50, right - left), _placeOres.Height);
        ResumeLayout(true);
        FitColumns(_places, 0);
        FitColumns(_placeOres, 0);
        Invalidate();
    }

    /// <summary>Schmaler Zieh-Griff mit Punkten (Maus wird zum ↔-Pfeil).</summary>
    private sealed class SplitGrip : Control
    {
        public SplitGrip()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.SizeWE;
            BackColor = Color.FromArgb(28, 18, 62);
        }

        protected override void OnMouseEnter(EventArgs e) { BackColor = Color.FromArgb(70, 45, 140); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { BackColor = Color.FromArgb(28, 18, 62); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            using var dot = new SolidBrush(Color.FromArgb(190, 160, 255));
            int cx = Width / 2, cy = Height / 2, d = Math.Max(3, Width / 3);
            for (int i = -2; i <= 2; i++) e.Graphics.FillEllipse(dot, cx - d / 2, cy + i * d * 2 - d / 2, d, d);
        }
    }

    /// <summary>Schwerkraft als Text, z. B. "0,38 G" (Deutsch) bzw. "0.38 G" (English).</summary>
    private static string Grav(double g)
    {
        var culture = L.En ? System.Globalization.CultureInfo.InvariantCulture : System.Globalization.CultureInfo.GetCultureInfo("de-DE");
        return g.ToString("0.00", culture) + " G";
    }

    /// <summary>Schwerkraft mit Umrechnung: Anteil der Erd-Schwerkraft und m/s².</summary>
    private static string GravHint(double g)
    {
        if (g <= 0) return L.T("0 G (Schwerelosigkeit)", "0 G (zero gravity)");
        var culture = L.En ? System.Globalization.CultureInfo.InvariantCulture : System.Globalization.CultureInfo.GetCultureInfo("de-DE");
        string percent = (g * 100).ToString("0", culture);
        string ms2 = (g * 9.81).ToString("0.0", culture);
        return L.T($"{Grav(g)} ({percent} % der Erde, {ms2} m/s²)", $"{Grav(g)} ({percent} % of Earth, {ms2} m/s²)");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _headerFont.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>Liste mit Doppelpufferung – kein Flackern beim Scrollen oder Aktualisieren.</summary>
internal sealed class SmoothListView : ListView
{
    public SmoothListView()
    {
        DoubleBuffered = true;
    }
}
