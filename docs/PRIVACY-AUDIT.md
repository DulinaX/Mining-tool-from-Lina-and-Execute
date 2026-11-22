# Prüfung der Zugriffspfade

Stand: 16. November 2026, Version 1.0.1. Die ursprünglichen Zugriffspfade und die nachfolgend beschriebenen Änderungen wurden im Quellcode geprüft; Build, automatisierte Regressionstests und Publish sind abgeschlossen. Dies ist eine **manuelle Quellcodeprüfung** mit ergänzenden automatisierten Verhaltenstests, kein vollständiges Kernel-/Netzwerk-Tracing.

## Zulässige Dateioperationen im Anwendungscode

| Stelle | Zweck / Pfad |
|---|---|
| `Program.cs` | Eigene `settings.json` und `data/signatures.json` relativ zur EXE laden |
| `Core/JsonStorage.cs` | Begrenzt große UTF-8-JSON-Dateien lesen; eigene Einstellungen/Tabelle atomar speichern |
| `UI/MainForm.cs` | Genau die beim Start bestimmten eigenen JSON-Pfade speichern |
| `UI/TableEditor.cs` | JSON ausschließlich im Textfeld bearbeiten; kein Explorer-/Dateiauswahldialog, kein fremder Dateiimport |
| `Ocr/LocalOcr.cs` | Eigenes mitgeliefertes `tessdata/eng.traineddata` und native Programmbibliotheken laden |

Es gibt keine Suche nach einer Star-Citizen-Installation und keinen Datei-Watcher. Die Anwendung konstruiert weder einen Pfad zu `Data.p4k` noch zu `Game.log`; sie öffnet diese Dateien nicht. Der Prozessabbildpfad wird ausschließlich über die normale Windows-Metadatenabfrage ermittelt und nur als Zeichenkette mit dem erwarteten Dateinamen verglichen. Die Datei selbst wird dabei nicht geöffnet.

Die generische JSON-Speicherschicht ist wiederverwendbar, aber sämtliche produktiven Aufrufer erhalten ausschließlich die oben aufgeführten eigenen Pfade. Im Tabelleneditor sind Einfügen/Kopieren von JSON möglich, keine automatische Suche in fremden Ordnern.

## Aufnahme und Bildlebensdauer

- `WindowProbe` öffnet Prozesshandles ausschließlich mit `PROCESS_QUERY_LIMITED_INFORMATION`, ohne Speicherleserechte. Fenster-/Desktop-/Monitorabfragen sind Metadaten.
- `WindowCapture` bindet Windows Graphics Capture an das bestätigte HWND. Es gibt keine Desktop-Aufnahmefunktion oder GDI-Aufnahme als Rückfall.
- Windows liefert eine ganze Spielfenster-GPU-Fläche. Nur das konfigurierte kleine Rechteck wird in ein CPU-lesbares Staging-Objekt und danach in ein RAM-Bitmap kopiert. Dieser technisch notwendige Unterschied wird auch in der README offengelegt.
- Frame-Pool und Sitzung sind auf den einzelnen Capture-Aufruf begrenzt. Frames, Texturen, Pixelpuffer und Bitmaps werden freigegeben. Es gibt keine Bildwarteschlange.
- Die optionale Vorschau verwendet zusätzlich im Overlay einen RAM-Bitmap-Klon. Beim Ersetzen wird das alte Bild freigegeben; Fokusverlust entfernt die Vorschau aus Hauptfenster und Overlay. Es wird keine Bildserie angelegt.
- `CaptureBorderPermission` fragt nur nach ausdrücklichem UI-Klick `GraphicsCaptureAccess.RequestAccessAsync(Borderless)` ab. Nur das Windows-Ergebnis `Allowed` schaltet die prozesslokale Option zum Ausblenden frei; sonst bleibt der Rahmen bestehen. `WindowCapture` setzt dann die offizielle Eigenschaft `IsBorderRequired`. Dies ändert weder die Freigabebedingungen noch die kurze Sitzungslebensdauer. Der Scanner speichert die Rahmenoption nicht, ändert keine Registry-/Systemeinstellung und umgeht keine Windows-Ablehnung. Windows selbst kann eine erteilte Capability-Zustimmung verwalten.
- Die zusätzlichen OCR-Diagnosen enthalten nur einen lokal erzeugten Statusgrund. Sie schreiben weder OCR-Rohtexte noch Bilder in Dateien.
- Tesseract erhält einen PNG-**MemoryStream**, keinen Bilddateinamen. Debug-Bilder sind ausgeschaltet. Der Debug-Ausgabekanal ist das Windows-Nullgerät. Der Wrapper ruft beim Freigeben einer Page die native Clear-Funktion auf; dies wurde anhand des gelieferten Wrapper-Assemblies geprüft.
- Prozesslokale OCR-Umgebungsvariablen begrenzen den Modellpfad und OpenMP-Threads. Es gibt keine System-/Benutzer-Umgebungsänderung.
- Keine Anwendungskomponente speichert Screenshots, OCR-Rohtexte, Fehlerberichte oder Crash-Dumps. Der optionale .NET-Dump-Helfer wird aus der neuen portablen Veröffentlichung ausgeschlossen. Betriebssystemseitige Auslagerungs-/Diagnosefunktionen werden nicht umkonfiguriert.

## Netz, Spielsteuerung und Hintergrundbetrieb

Im produktiven Anwendungscode werden keine HTTP-, Socket-, Telemetrie-, Update- oder Download-Schnittstellen aufgerufen. Es gibt keine gestarteten Hilfsprozesse, Dienste, Aufgabenplanung oder Autostart. Die Native-Tesseract-Binärdatei importiert allerdings unter anderem `WS2_32.dll`; allein die Importliste beweist also keine vollständige Abwesenheit netzwerkfähigen Codes. Der verwendete Anwendungspfad verarbeitet ausschließlich ein lokales Pix-Objekt im RAM und ruft keine URL-/Remote-Funktionen auf.

Es gibt keine Spielspeicherzugriffe, Remote-Thread-Erstellung, Debugger-Anbindung, DLL-/Grafik-Hooks, simulierte Tastatur-/Mauseingaben oder Paketmitschnitte. Der temporäre Escape-Hotkey gehört ausschließlich zur eigenen Rechteckauswahl. Normale Mausklicks und Drag-Ereignisse werden nur in den eigenen UI-Fenstern verarbeitet.

Die Anwendung startet immer deaktiviert. Deaktivieren, Fokusverlust und Schließen widerrufen die aktive Verarbeitungsgeneration. Nachträglich ankommende Ergebnisse und Vorschauen werden verworfen. Diese Fälle sind mit kontrollierten Testdiensten geprüft. Die UI überwacht den Fokus alle 25 ms; native OCR kann während eines einzelnen Aufrufs nicht hart unterbrochen werden, ihr Ergebnis wird nach Widerruf aber nicht veröffentlicht.

## Grenzen dieser Prüfung

- Kein vollständiges Betriebssystem-Tracing sämtlicher nativer Datei-/Netzwerkzugriffe durchgeführt.
- Kein Test mit physisch getrenntem Netzwerk durchgeführt. Erfolgreich war die Paketwiederherstellung aus rein lokalen Quellen.
- Keine vollständige Prüfung fremder nativer Bibliotheken auf Binärebene.
- Kein In-Game-, Anti-Cheat- oder CIG-Freigabetest.
- Keine tatsächliche Windows-Abfrage zur rahmenlosen Aufnahme und keine manuelle Prüfung der neuen Overlay-Vorschau während dieses Updates. Der Benutzerbericht über eine unsichere `7170`-Lesung ist keine reproduzierte HUD-OCR-Prüfung.
- Ein zusätzliches experimentelles PowerShell-Prüfskript war in dieser Umgebung nicht les-/ausführbar. Es wird nicht für den Build benötigt oder mit dem Quellcodepaket ausgeliefert. Es wurden keine Schutzfunktionen deaktiviert und keine Sperren umgangen.

Aus der Quellcodeprüfung folgt: **Der implementierte Anwendungspfad hat keine Spieldatei-, Spielspeicher- oder Netzwerklesefunktion.** Eine lückenlose Laufzeitbeobachtung aller Betriebssystem- und Drittanbieterkomponenten wird ausdrücklich nicht behauptet.
