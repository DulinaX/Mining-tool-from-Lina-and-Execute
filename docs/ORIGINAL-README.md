# Execute's Mining Scanner 1.0.1

Eigenständiger Windows-Scanner für numerische Star-Citizen-Mining-Signaturen: **ausgewählter Bildausschnitt → lokale OCR → lokale Referenztabelle → separates Overlay**. Die Anwendung startet immer mit **gestoppter Erkennung**.

## Start der portablen Ausgabe

Ein Update in einen **neuen Ordner** entpacken. Eine vorhandene Ausgabe mit eigenen Einstellungen oder einer korrigierten Signaturtabelle nicht überschreiben. Die Anwendung übernimmt keine Daten aus anderen Programmordnern automatisch. Bei Bedarf die eigenen `settings.json` und `data/signatures.json` manuell übernehmen; nach Auflösungs- oder DPI-Änderungen den Aufnahmebereich neu auswählen.

1. Das gesamte portable ZIP in einen eigenen, beschreibbaren Ordner entpacken. Nicht in das Spielverzeichnis legen. EXE und Unterordner müssen zusammenbleiben.
2. `ExecutesMiningScanner.exe` starten. Keine Anmeldung, keine Administratorrechte, kein Installationsprogramm.
3. Windows 10 ab Version 2004 (Build 19041) oder Windows 11, 64 Bit, verwenden. Borderless oder Fenstermodus wählen. Exklusives Vollbild wird nicht als unterstützt zugesichert.
4. Auf **Aufnahmebereich auswählen** klicken und innerhalb von 20 Sekunden selbst zu Star Citizen wechseln. Nur die vollständige Signaturzahl samt Tausendertrennzeichen umrahmen, mit einigen Pixeln Rand um die Ziffern. `RS`, Entfernungen, Einheiten und andere Zahlen ausschließen. Mindestens 8 × 8, höchstens 1200 × 400 Pixel und insgesamt höchstens 240.000 Pixel.
5. Die Auswahl mit Loslassen der linken Maustaste abschließen. Rechtsklick oder Escape bricht ab. Bei Fokusverlust wird die Auswahl verworfen.
6. Zurück im Scanner **Erkennung starten** drücken und selbst ins Spiel wechseln. Der Scanner sendet keinerlei Eingaben an das Spiel. Ein Start ohne gültigen Bereich bleibt pausiert.

Die normale Anwendung enthält weder Downloader noch Updatefunktion. Das OCR-Modell, die Tesseract-Bibliotheken und die .NET-Laufzeit liegen der portablen Ausgabe lokal bei. Eine separat installierte .NET-Laufzeit ist nicht erforderlich.

Die native OCR setzt zusätzlich die **Microsoft Visual C++ 2015–2022 x64-Laufzeit** voraus. Sie ist auf diesem PC bereits vorhanden und wurde von den OCR-Tests erfolgreich verwendet. Microsoft-System-DLLs werden nicht ungeprüft weiterverteilt. Auf einem anderen PC ohne diese Laufzeit kann OCR deshalb nicht starten; die Anwendung lädt oder installiert sie nicht. Eine universelle Portabilität auf einer frischen Windows-Installation wird nicht behauptet.

## Anzeige und Einstellungen

- **Gestoppt:** Benutzerfreigabe fehlt. Keine Aufnahme.
- **Pausiert – Spiel nicht im Vordergrund:** Alt-Tab, geschlossenes/minimiertes Spiel oder unsichere Fensteridentität. Overlay und RAM-Vorschau verschwinden; letzte Ergebnisse werden verworfen.
- **Pausiert – Aufnahmebereich neu auswählen:** Bereich fehlt oder Fensterauflösung/DPI haben sich geändert. Ein Verschieben des Fensters mit gleicher Größe und DPI ist möglich; Größe oder Skalierung erfordern bewusst eine neue Auswahl.
- **Pausiert – Aufnahme nicht verfügbar / Bereich verdeckt:** Windows liefert keine passende sichere Aufnahme. Es gibt keinen Rückfall auf Desktopaufnahmen. Prüfen, ob der Zahlenbereich durch ein anderes Fenster verdeckt ist.
- **Aktiv:** Aufnahme ist freigegeben. Erst nach zwei ausreichend sicheren, aufeinanderfolgenden identischen Lesungen erscheint eine Zahl.
- **Keine Signatur / Erkennung unsicher:** Die aktuellen Pixel liefern keine belastbare Zahl. Der letzte Treffer wird sofort als **veraltet** gekennzeichnet und nach der eingestellten Frist gelöscht.

Standardwerte: 250 ms Abtastintervall, 1500 ms Verfallszeit, mindestens 80 % OCR-Konfidenz und maximal 12 untersuchte Objekte. Langsamere Rechner dürfen länger benötigen; es gibt keine Warteschlange von Bildern und keine parallelen OCR-Aufträge. Konfidenzwerte sind Schätzwerte der OCR, keine mathematische Garantie.

**Bekannte OCR-Grenze:** Ein künstlicher Test mit sehr kleiner Consolas-Schrift (14 px) und durchgestrichener Null wurde trotz übereinstimmender Vorverarbeitung als `7178` statt `7170` gelesen. Diese Grenze wurde nicht behoben. Kleine oder stilisierte Ziffern können also auch bei hoher Konfidenz falsch gelesen werden. Die Vorschau und die angezeigte Zahl mit dem HUD vergleichen; einen vollständigen scharfen Zahlenbereich auswählen und, falls im Spiel möglich, die HUD-Schrift vergrößern. Die neue Version verbessert die Diagnose, ist aber keine bestätigte Behebung des gemeldeten echten HUD-Falls.

**Einstellungen speichern** übernimmt Intervall, Verfallszeit, Mindestkonfidenz, maximale Anzahl, Schriftgröße, relative Overlayposition, optionalen Rohstofffilter und RAM-Vorschau. Der Filter blendet Treffer aus, nennt aber die Anzahl weiterer möglicher Zuordnungen. Er ändert nicht die mathematische Zuordnung. Alle Treffer sind ohne Filter in der scrollbaren Ergebnisliste erreichbar.

**Overlay verschieben** startet nach einem bewussten Wechsel ins Spiel einen Einrichtungsmodus. Das Overlay ziehen und loslassen; Alt-Tab beendet den Modus. Währenddessen pausiert OCR. Das normale Overlay ist halbtransparent, klickdurchlässig und nimmt keinen Fokus. Es wird innerhalb des Spielfensters außerhalb des OCR-Bereichs platziert; bei Überlappung wird eine freie Ecke verwendet oder es wird ausgeblendet.

Die optionale Vorschau erscheint im Hauptfenster und **direkt im Overlay**. Sie wird nur im RAM gehalten und bei Fokusverlust aus beiden Fenstern entfernt. Zum Prüfen des Zahlenbereichs **Vorschau (nur RAM)** aktivieren, **Einstellungen speichern**, Erkennung starten und ins Spiel wechseln. Es gibt keinen globalen Start-Hotkey in dieser Version. Das **X** beendet das gesamte Programm, einschließlich Worker und Overlay; kein Tray-Weiterlaufen.

Bei **Erkennung unsicher** nennt eine zusätzliche Zeile den beobachteten Grund, beispielsweise zu niedrige OCR-Konfidenz, widersprüchliche Lesungen oder zu wenige übereinstimmende Lesungen. **Bestätigung 1/2** bedeutet, dass noch eine zweite sichere Lesung fehlt. Das ist keine Aussage über die Erzqualität. Der Hinweis zur Tabellenableitung erscheint erst zusammen mit einer tatsächlich übernommenen Zahl. Die genaue Ursache einer unsicheren echten HUD-Lesung lässt sich aus der Meldung allein nicht abschließend bestimmen. In der Vorschau prüfen, ob sämtliche Ziffern scharf und vollständig zu sehen sind und keine andere Zahl im Bereich liegt. Bei Bedarf den Bereich neu auswählen; die Mindestkonfidenz nicht allein zur Erzwingung eines Treffers herabsetzen.

**Gelben Rahmen ausblenden** fragt über die offizielle Windows-Funktion die Freigabe für eine Aufnahme ohne farbigen Rand ab. Ab Build 20348 kann Windows dies unterstützen; die Funktion ist nicht für jedes System zugesichert. Windows kann eine Zustimmung verlangen oder die Anfrage aufgrund seiner Einstellungen ablehnen. Bei Ablehnung, fehlender Unterstützung oder einem Fehler bleibt der normale Rahmen bestehen. Die Auswahl gilt nur für den laufenden Programmprozess und kann mit **Gelben Rahmen anzeigen** zurückgenommen werden. Nach einem Neustart ist der Rahmen wieder eingeschaltet. Andere aufnehmende Anwendungen können weiterhin einen Rahmen verlangen. Der Scanner erzeugt weiterhin nur kurze Aufnahmesitzungen pro Abtastung; der Rahmen kann deshalb blinken, solange er eingeschaltet ist.

Technische Referenzen: [Microsoft: IsBorderRequired](https://learn.microsoft.com/en-us/uwp/api/windows.graphics.capture.graphicscapturesession.isborderrequired) und [Microsoft: RequestAccessAsync](https://learn.microsoft.com/en-us/uwp/api/windows.graphics.capture.graphicscaptureaccess.requestaccessasync).

## Referenztabelle

`data/signatures.json` ist editierbares JSON. **Signaturtabelle bearbeiten** öffnet einen lokalen JSON-Editor mit Validierung. Für einen manuellen Import JSON in den Editor einfügen; für Export den Inhalt kopieren. Die Anwendung öffnet dafür keine fremden Dateien oder Verzeichnisse.

Jeder Eintrag enthält ID, englischen/deutschen Namen, ganzzahlige Basissignatur, Quellen, Referenzpatch und Verifikationshinweis. Die mitgelieferten 28 Einträge wurden am 16. November 2026 mit öffentlichen Community-Referenzen abgeglichen. **Referenzpatch 4.10 laut Quelle; keine eigene In-Game-Verifikation und keine Garantie für spätere Patches.** Details und Quellenkonflikte stehen in [docs/SIGNATURE-SOURCES.md](docs/SIGNATURE-SOURCES.md).

Die Zuordnung verwendet ausschließlich exakte ganzzahlige Vielfache. Beispielsweise kann 19200 sowohl Savrilium ×6 als auch Aslarite ×5 entsprechen. Es gibt keine heimliche Zahlkorrektur, Rundung oder Toleranz. `12854` wird nicht als `12855` behandelt. `Unbekannte Signatur` ist ein gültiges Ergebnis. Tausendergruppen wie `12,855`, `12.855` und `12 855` werden unterstützt; fehlerhafte oder gemischte Gruppierung wird abgelehnt.

Ein Treffer ist lediglich eine **mögliche Tabellenableitung**, kein Nachweis für Erzreinheit, Qualität oder vollständige Zusammensetzung. Die Anzahl ist ebenfalls abgeleitet und kann mehrdeutig sein. Generische ROC-/FPS-Signaturen bestimmen keinen konkreten Rohstoff. Die Ausgabe löst keine gemischten Summen verschiedener Basistypen in vermeintlich sichere Zusammensetzungen auf.

**Cluster-Beispiel 7170:** Die lokale Tabelle enthält für Gold die Basissignatur **3585**. `7170 = 2 × 3585` ergibt daher **Gold, 2× möglich**, sofern die maximale Anzahl mindestens 2 beträgt und die OCR die Zahl zweimal sicher bestätigt. Das ist eine Ableitung für ein Vielfaches desselben Basistyps, kein Beweis für zwei reine Goldgesteine und keine Berechnung einer gemischten Clusterzusammensetzung.

## Datenschutz und technische Grenzen

- Keine Spieldateien werden geöffnet, durchsucht oder verändert. Insbesondere kein Zugriff auf `Data.p4k`, `Game.log`, sonstige Spieldateien oder das Spielverzeichnis.
- Kein Lesen/Schreiben von Spielspeicher, kein Debugger, keine DLL-Injection, keine Grafik-API-Hooks, kein Paketmitschnitt, keine Spielsteuerung und keine Anti-Cheat-Umgehung.
- Die Fensterprüfung verwendet normale Windows-Metadaten: Vordergrundfenster, Sichtbarkeit/Minimierungszustand, Fensterkoordinaten, DPI, Prozess-ID und Prozessabbildname. Der Name wird über `PROCESS_QUERY_LIMITED_INFORMATION` abgefragt; die genannte EXE-Datei wird nicht geöffnet.
- Die Aufnahme bindet Windows Graphics Capture an genau das bestätigte Spiel-HWND. Windows stellt dabei technisch eine GPU-Fläche des Spielfensters bereit. **Nur der explizit ausgewählte Teil wird in eine CPU-Bitmap für OCR kopiert.** Es gibt weder eine Vollbild-CPU-Aufnahme noch einen Desktop-Fallback. Die vollständige GPU-Fläche ist ein erforderliches Zwischenprodukt der Windows-API, kein gespeichertes Bild.
- Pro Abtastung existiert nur eine kurzlebige Sitzung mit einem Frame-Slot. Sitzung, Frames und GPU-Ressourcen werden im Aufruf geschlossen. Fokus und Bereich werden davor, während des Wartens, vor OCR und nach OCR erneut geprüft. Ein Widerruf erzeugt eine neue Generation; Ergebnisse alter Generationen können nicht veröffentlicht werden.
- Eine 25-ms-Überwachung ergänzt die Prüfungen an den Verarbeitungsschritten. „Sofort pausieren“ bedeutet die nächste beobachtete Fokusänderung/Prüfung; Windows-Fokuswechsel und CPU-Ausführung sind nicht atomar. Bereits laufende native OCR kann nur an ihren Rückkehrpunkten abgebrochen werden; ihr Ergebnis wird verworfen. Die HWND-Bindung verhindert einen Wechsel zur Aufnahme von Browser oder Desktop.
- Windows kann einen normalen Aufnahmeindikator am Spielfenster anzeigen. Die ausdrückliche Option zum Ausblenden verwendet ausschließlich die offizielle Windows-Freigabe; eine Ablehnung wird respektiert. Nicht unterstützte oder gesperrte Aufnahme führt zur Pause.
- Keine Cloud-OCR, Netzwerk-API, Telemetrie, Werbung, Updates, Dienste, Aufgabenplanung oder Autostart. Es werden keine Hilfsprozesse vom laufenden Scanner gestartet.
- Die Anwendung schreibt ausschließlich eigene JSON-Einstellungen und die lokale Signaturtabelle, einschließlich kurzlebiger atomarer JSON-Speicherdateien. Bilder, OCR-Rohtexte und Serien landen nicht in Dateien, Logs oder Fehlerberichten. Die OCR erhält ihre Bilder über einen RAM-Stream; Debug-Bildausgaben sind deaktiviert.
- Windows selbst kann Prozessspeicher unabhängig von der Anwendung in Auslagerungs-/Diagnosefunktionen verwalten. Die Anwendung erzeugt selbst keine Dumps oder Screenshotdateien und ändert keine Systemeinstellungen.

Es wird **keine CIG-Genehmigung oder „Bansicherheit“ behauptet**. Bildschirmaufnahme ohne Spielspeicherzugriff ist keine automatische offizielle Freigabe. Virenschutz oder Anti-Cheat sollen nicht deaktiviert werden.

## Quellcode und Build

Stack: C#/.NET 8, WinForms, HWND-gebundene Windows Graphics Capture, Direct3D11-Interop für den GPU-Ausschnitt und lokale Tesseract-OCR. Keine Smart-Citizen-Quellcodeübernahme.

```
src/MiningScanner.Core/           Normalisierung, Tabelle, Konfiguration, Stabilisierung, Freigabelogik
src/MiningScanner.Windows/Platform/ Fenster-/Fokusprüfung und fenstergebundene Aufnahme
src/MiningScanner.Windows/Ocr/    Lokale OCR und RAM-Bildvorverarbeitung
src/MiningScanner.Windows/Runtime/ Ein Worker, Widerruf und Ergebnisübergabe
src/MiningScanner.Windows/UI/     Deutsche Oberfläche, Auswahl und Overlay
tests/                           Logik-, OCR- und Lebenszyklustests
resources/tessdata/               Mitgeliefertes OCR-Modell
vendor/nuget/                    Fixierte lokale Build-Abhängigkeiten
```

Benötigt für den Build: Windows x64 und **.NET SDK 8.0.428**. Der portable Scanner selbst benötigt das SDK nicht. Die lokale Windows-SDK-Projektion ist `10.0.22621.56`; das Projektziel `net8.0-windows10.0.22621.0` wird mit Mindestbetriebssystem `10.0.19041.0` gebaut. Die neuere optionale Rahmenfunktion wird vor der Verwendung auf Unterstützung geprüft.

```powershell
# Nur falls das SDK noch fehlt; diese ausdrücklich gestartete Entwicklungshilfe lädt es herunter:
powershell -File .\scripts\bootstrap-sdk.ps1

# Offline-Wiederherstellung aus vendor/nuget, Tests und selbstständige portable Ausgabe:
powershell -File .\scripts\build.ps1
```

Alternativ `-SdkPath 'C:\Pfad\zu\dotnet.exe'`. Die SDK-Version ist durch `global.json` fixiert; NuGet verwendet ausschließlich den lokalen Paketordner. Modell- und Paketprüfsummen werden vor dem Build geprüft. Ergebnisse liegen in `outputs/ExecutesMiningScanner-1.0.1-win-x64/`. `-SkipTests` ist nur für bewusste Entwicklungsdurchläufe; den tatsächlichen Teststand dieser Version nennt der Testbericht. `-FrameworkDependent` erzeugt eine Ausgabe, die eine separat installierte passende .NET-Desktop-Laufzeit benötigt.

Der Build lädt selbst keine Pakete. Der separate SDK-Bootstrap ist eine reine Entwicklungshilfe und liegt nicht als Funktion in der EXE vor. Abhängigkeiten und Lizenzen: [docs/DEPENDENCIES.md](docs/DEPENDENCIES.md), vollständige Hinweise in `licenses/`.

## Teststand

Die konkreten ausgeführten Tests und ausdrücklich noch offenen Prüfungen stehen in [docs/TEST-REPORT.md](docs/TEST-REPORT.md). Synthetische OCR-Tests erzeugen Zahlenbilder ausschließlich im Arbeitsspeicher. Die manuelle Prüfung der Datei- und Netzwerkzugriffspfade ist in [docs/PRIVACY-AUDIT.md](docs/PRIVACY-AUDIT.md) dokumentiert; sie ersetzt kein vollständiges Betriebssystem-Tracing.
