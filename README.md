# Mining Tool – von Lina und Execute

**© 2026 DulinaX & Execute** · MIT-Lizenz (siehe `LICENSE`)

Ein kleines Windows-Tool für **Star Citizen**: Es liest die Mining-Signatur-Zahl vom Bildschirm und zeigt sofort, **welches Erz** es ist und **wie viele Steine** dort liegen. Dazu gibt es einen Erz-Finder („Wo finde ich welches Erz?“) mit Orten, Chancen und Schwerkraft.

> Inoffizielles Fan-Tool. Star Citizen ist eine Marke von Cloud Imperium Rights LLC.

## Sicherheit

- Die App liest **nur die Bildschirm-Pixel** im gewählten Rahmen, also wie ein Screenshot.
- Sie öffnet **keine Spieldateien**, liest **keinen Spielspeicher** und schickt **keine Eingaben** ans Spiel.
- Sie läuft komplett **offline**. Die Texterkennung (Tesseract) läuft lokal auf dem PC.

## Funktionen

- Signatur-Scan nur auf dem Monitor, auf dem Star Citizen läuft
- Erz + Anzahl Steine, bei mehreren möglichen Treffern werden alle angezeigt
- Schlankes In-Game-Overlay („● Aktiv“ + Erz)
- Bereich wählen auf eingefrorenem Standbild, damit sich das Schiff dabei nicht dreht
- Erz-Finder: „Nach Erz“ / „Nach Ort“, inklusive Schwerkraft (G)
- Deutsch / English
- Galaxy-Design mit Animationen

## Zwei Versionen

| Version | Name | Bauen mit | Startsprache |
|---|---|---|---|
| Eigene Version | Exi und Lina Mining Tool | `APP BAUEN.cmd` | Deutsch |
| Zum Weitergeben | Mining Tool | `WEITERGABE BAUEN.cmd` | English |

`WEITERGABE BAUEN.cmd` legt `Mining Tool.zip` auf den Desktop. Die ZIP enthält keine persönlichen Einstellungen.

## Bauen (Windows 10/11, 64 Bit)

1. Repo herunterladen (grüner Knopf **Code → Download ZIP**) und entpacken.
2. Doppelklick auf `APP BAUEN.cmd` oder `WEITERGABE BAUEN.cmd`.
   - Beim ersten Mal wird das .NET SDK 8.0.428 einmalig von Microsoft geladen (nur in den Ordner `work/`, keine Installation).
   - Die NuGet-Pakete kommen aus `vendor/nuget`, falls vorhanden, sonst von nuget.org.
3. Fertig: Die App liegt auf dem Desktop.

Zum Starten der fertigen App braucht der PC die **Microsoft Visual C++ 2015–2022 Redistributable (x64)**. Auf den meisten Gaming-PCs ist sie schon installiert.

## Ordner

| Ordner | Inhalt |
|---|---|
| `src/MiningScanner.Windows` | Die App (Oberfläche, Scan, Overlay, Erz-Finder) |
| `src/MiningScanner.Core` | Einstellungen, Signatur-Tabelle, Erkennungs-Logik |
| `data/signatures.json` | Erz-Signaturen (Grundwerte pro Stein) |
| `resources/tessdata` | Lokales Texterkennungs-Modell |
| `scripts/` | Bau-Skripte |
| `tests/` | Tests |
| `licenses/` | Lizenzen der verwendeten Fremd-Bibliotheken |
| `docs/` | Weitere Doku (inkl. `ORIGINAL-README.md` von Executes Mining Scanner 1.0.1) |
