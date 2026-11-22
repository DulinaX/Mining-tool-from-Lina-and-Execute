# Testbericht - Execute's Mining Scanner 1.0.1

Datum: 16. November 2026. Windows x64, .NET SDK 8.0.428.

## Aktueller Stand 1.0.1

**Abschließender Build, Testdurchlauf und Windows-x64-Publish erfolgreich ausgeführt.** Der Build hatte keine Warnungen oder Compilerfehler. Die Wiederherstellung verwendete ausschließlich die lokalen NuGet-Pakete; alle 13 Ressourcen-Prüfsummen stimmen.

| Suite 1.0.1 | Bestandene Regressionstests | Fehlgeschlagene Regressionstests |
|---|---:|---:|
| Core | 185 | 0 |
| Lokale Tesseract-OCR mit künstlichen RAM-Bildern | 21 | 0 |
| Controller-Integration mit internen Testdiensten | 32 | 0 |
| **Gesamt** | **238** | **0** |

Zusätzlich wurde der unten beschriebene fehlerhafte OCR-Fall beobachtet. Er ist ausdrücklich **kein bestandener Test**. Die Zahlen sind deshalb keine Aussage, dass alle untersuchten Bilder richtig erkannt wurden.

Für 1.0.1 geändert: konkrete OCR-Diagnosegründe und Bestätigung 1/2; Qualitäts-/Tabellenhinweis nur bei übernommener Zahl; optionale RAM-Vorschau zusätzlich im Overlay; ausdrückliche offizielle Windows-Anfrage zum Ausblenden des Aufnahmerahmens. Ablehnung oder fehlende API behalten den Rahmen bei. Die Windows-SDK-Projektion wurde auf 10.0.22621.56 erhöht, die Mindestbetriebssystemversion bleibt Build 19041. Die kurzlebigen Aufnahmesitzungen und Sicherheitsprüfungen bleiben erhalten.

Zusätzliche bestandene Regressionstests behandeln unsichere OCR-Ergebnisse trotz hoher numerischer Konfidenz, das Unterbrechen einer Bestätigungsfolge und die lokale Clusterableitung `7170 = 2 × 3585` für Gold.

Die OCR-Suite prüft unter anderem `7170` und `7,170` mit heller/cyanfarbener Schrift, mehreren künstlichen Schriftgrößen und einem variierenden Hintergrund. Das ist keine Prüfung echter Bildschirmbilder. Ein nebenläufiger Bitmap-Zugriff im Testharness wurde beseitigt: Freigaben werden erst nach Ende des Workers geprüft. Der abschließende vollständige Durchlauf war danach erfolgreich.

**Separater bekannter Fehlerfall, ausdrücklich kein bestandener Erkennungstest:** Künstlich erzeugte Consolas-Ziffern bei 14 px mit durchgestrichener Null: Sollwert `7170`, akzeptierter OCR-Wert `7178`, Konfidenz ungefähr 0,85; die drei Vorverarbeitungsvarianten stimmten dabei in der falschen Lesung überein. Dieser Versuch belegt, dass Konfidenz und Variantenübereinstimmung keine Fehlerfreiheit garantieren. Der Fehlerfall wurde nicht behoben und wird nicht in die bestandenen Prüfungen eingerechnet. Die Diagnose-Erweiterung verändert weder die Mindestkonfidenz noch die numerische Tabellenableitung; es gibt keine Sonderkorrektur von `7178` zu `7170`.

Der Benutzer meldet bei einer sichtbaren Signatur `7170` im Spiel **Erkennung unsicher**. Dies wurde nicht anhand echter HUD-Pixel reproduziert. Die Tabellenzuordnung ist davon getrennt; sie kennt das entsprechende Gold-Vielfache bereits. Eine Behebung der konkreten HUD-Erkennung wird ohne einen erfolgreichen In-Game-Test nicht behauptet.

## Historisch automatisiert geprüft: 1.0.0

Die folgenden Tests wurden für 1.0.0 tatsächlich ausgeführt. Testbilder wurden ausschließlich künstlich im RAM erzeugt.

| Suite | Bestanden | Fehlgeschlagen | Inhalt |
|---|---:|---:|---|
| Core | 182 | 0 | Zahlen-/Tausenderformate, ungültige Werte, exakte/mehrdeutige/unbekannte Zuordnung, JSON-Validierung, Mindestkonfidenz, Zwei-Lesungen-Stabilisierung, Alterung/Löschen, Freigabelogik, Konfiguration, DPI-/Größenregeln, mitgelieferte Tabelle |
| Echte lokale Tesseract-OCR | 10 | 0 | Helle/dunkle künstliche Ziffern, 3885, 12,855, 21.425, leere Bilder, zu wenig Kontrast, ungültige Gruppierung, Abbruch und Ende eines echten OCR-Workers |
| Controller-Integration mit Testdiensten | 28 | 0 | Start deaktiviert, fehlender Bereich, Fokusverlust/Stopp während wartender Aufnahme oder OCR, Verwerfen alter Generationen/Vorschauen, zwei neue Lesungen nach Rückkehr, Ablaufzeiten, Ressourcenfreigabe und beendete Worker |
| **Gesamt** | **220** | **0** | |

Die Integrationstests ersetzen Fensterprüfung, Capture und OCR über eine **interne** Testschnittstelle. Es gibt keine entsprechende Einstellung, Kommandozeilenoption oder UI-Funktion in der Anwendung. Die Tests lesen keine realen Fenster, Spieldaten oder Bildschirmaufnahmen.

Der Windows-Anwendungscode von 1.0.0 wurde erfolgreich kompiliert, ohne Compilerfehler und ohne Warnungen. NuGet-Wiederherstellung verwendet ausschließlich die lokalen Paketarchive. Modell- und Paketprüfsummen werden geprüft. Die portable Veröffentlichung enthält .NET selbst sowie Modell und OCR-Bibliotheken; ihre abschließende Paketprüfung ist Bestandteil des Build-/Verpackungsdurchlaufs.

## Manuell geprüft

Quellcodekontrolle der Datei-/Netzwerkzugriffspfade, der Metadatenberechtigungen, von Bildlebensdauer, Abbruchgenerationen und dem Verhalten beim Schließen. Einzelheiten: `PRIVACY-AUDIT.md`.

**Keine abgeschlossene grafische Bedienungsprüfung.** Ein vorgesehener synthetischer Fenstertest wurde nicht bis zur Aufnahme/OCR durchgeführt; anschließend wurde auf weitere Desktopbedienung verzichtet. Daraus wird kein erfolgreich getesteter Aufnahme-/Overlaybetrieb abgeleitet.

## Noch am Zielsystem zu prüfen

1. Aufnahmebereich im echten Spiel auswählen, abbrechen und nach Größen-/DPI-Wechsel neu einrichten.
2. HWND-gebundene Aufnahme mit dem verwendeten Grafiktreiber und Borderless-/Fenstermodus; richtige Client-/Framekoordinaten.
3. Wechselwirkung zwischen sichtbarem Windows-Aufnahmerahmen und konservativer Verdeckungsprüfung. Unklare Fälle pausieren sicher, können aber die Aufnahme auf einzelnen Systemen verhindern.
4. Overlay-Lesbarkeit, Klickdurchlässigkeit, Positionierung sowie mehrere Monitore und Skalierungen in der tatsächlichen Konfiguration.
5. Alt-Tab/Minimieren/Spielende und Rückkehr in einem echten Spielablauf. Die entsprechende Zustandslogik wurde simuliert getestet, der reale Ablauf nicht.
6. OCR-Erkennungsqualität auf tatsächlichen Star-Citizen-HUD-Pixeln und unterschiedlichen Hintergründen. Die synthetischen Tests ersetzen keine solche Messung.
7. Netzwerk physisch getrennt; vollständiges natives Datei-/Netzwerk-Tracing, falls eine zusätzliche unabhängige Prüfung gewünscht ist.
8. Start auf einem zweiten, frischen Windows-PC. Die Visual-C++-Laufzeit ist Voraussetzung; ihre Anwesenheit wurde nur auf dem aktuellen PC bestätigt.
9. Die neue Schaltfläche **Gelben Rahmen ausblenden** auf einem unterstützten Windows-System: erlaubte und abgelehnte Anfrage, Zurückschalten sowie neuer Programmstart. Eine sichtbare Windows-Zustimmungsabfrage kann je nach App-/Systemkontext entfallen; maßgeblich ist das API-Ergebnis. Die tatsächliche Anfrage wurde hier nicht ausgelöst.
10. Neue OCR-Diagnosezeilen, Bestätigung 1/2 und RAM-Vorschau direkt im Overlay: Lesbarkeit, Größe, sichere Position außerhalb des OCR-Bereichs und Entfernen beim Fokuswechsel.

Keine Anti-Cheat-Prüfung, keine CIG-Genehmigung und keine Aussage über „Bansicherheit“.

## Wiederholen

`scripts/build.ps1` führt Ressourcenprüfung, alle drei Testprogramme und den Windows-Publish aus. Das SDK ist in `global.json` fixiert; Abhängigkeiten befinden sich unter `vendor/nuget`. `-SkipTests` überspringt Tests bewusst und ist nicht als neue Testbestätigung zu verstehen.
