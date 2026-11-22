# Abhängigkeiten und Lizenzen

Die Anwendung ist eine eigenständige Implementierung. Es wurde kein Smart-Citizen-Quellcode übernommen. Die Signaturdaten sind manuell zusammengeführte Fakten aus den in SIGNATURE-SOURCES.md genannten öffentlichen Referenzen.

## Mitgeliefert

| Bestandteil | Version | Lizenz / Herkunft |
|---|---|---|
| Eigener Programmcode | 1.0.1 | MIT, `LICENSE` im Projektstamm |
| .NET Runtime / Windows Desktop Runtime | 8.0.34 | MIT und weitere Hinweise des Herstellers; `licenses/DotNet-*`, `WindowsDesktop-LICENSE.txt` |
| Tesseract .NET Wrapper von Charles Weld | 5.2.0 | Apache-2.0; `licenses/Tesseract-Wrapper-LICENSE.txt`; Copyright 2012–2020 Charles Weld laut NuGet-Metadaten |
| Native Tesseract Engine | Binärdateien aus Tesseract-NuGet 5.2.0 | Apache-2.0; `licenses/Apache-2.0.txt`; Originaldatei `tesseract50.dll` unverändert |
| Leptonica | 1.82.0 | BSD-2-Clause-artige Leptonica-Lizenz; `licenses/Leptonica-1.82.0.txt` |
| Englisches LSTM-Modell | tessdata_fast Tag 4.1.0 | Apache-2.0; [Original](https://github.com/tesseract-ocr/tessdata_fast/tree/4.1.0) |
| Vortice.Direct3D11 / DXGI / DirectX | 3.6.2 | MIT, Amer Koleci und Mitwirkende; `licenses/Vortice-MIT.txt` |
| Vortice.Mathematics | 1.9.2 | MIT, Amer Koleci und Mitwirkende |
| SharpGen.Runtime / SharpGen.Runtime.COM | 2.2.0-beta | MIT; `licenses/SharpGen-MIT.txt`; Copyright Alexandre Mutel, Jeremy Koritzinsky und Amer Koleci laut Paketmetadaten |
| System.Reflection.Emit | 4.7.0 | MIT; `licenses/System.Reflection.Emit-MIT.txt` |
| Windows SDK .NET Projektion / WinRT.Runtime | 10.0.22621.56 | Microsoft Windows SDK-Lizenz; `licenses/Windows-SDK-license.rtf`; [Hersteller-Lizenzlink](https://aka.ms/WinSDKLicenseURL) |

Das OCR-Modell hat SHA-256 `7d4322bd2a7749724879683fc3912cb542f19906c83bcc1a52132556427170b2`. Alle tatsächlich benötigten NuGet-Archive und das Modell sind mit ihren SHA-256-Werten in `resources-manifest.json` erfasst. Native Bibliotheken werden unverändert aus dem fixierten NuGet-Paket übernommen.

Die Leptonica-/Tesseract-Binärdistribution kann Bildcodec-Komponenten statisch enthalten. Ergänzende Upstream-Hinweise für libpng, zlib, libjpeg-turbo/IJG, libtiff, libwebp und OpenJPEG liegen in `licenses/`. Diese Hinweise behaupten keine unabhängig reproduzierte Stückliste des vorgelagerten nativen Builds. Für die Anwendung wird ausschließlich die lokale RAM-Bildlese- und Erkennungsfunktion verwendet; es werden keine URL-/Dateiverarbeitungsfunktionen der OCR aufgerufen.

## Vorhandene Systemvoraussetzung

Die native Tesseract-Bibliothek benötigt die **Microsoft Visual C++ 2015–2022 x64-Laufzeit** (insbesondere MSVCP140, VCRUNTIME140 und VCRUNTIME140_1) sowie die Windows-UCRT. Die Laufzeit ist auf dem Entwicklungs-/Ziel-PC bereits vorhanden; die echte lokale OCR wurde damit erfolgreich getestet.

Es werden **keine DLLs aus dem Windows-Systemordner kopiert oder ungeprüft weiterverteilt** und keine Installer eingebaut. Ein anderer PC ohne diese Voraussetzung kann OCR nicht ausführen. Die Anwendung installiert nichts und lädt nichts nach. Für den hier geprüften PC waren keine Administratorrechte oder Systemänderungen nötig. Universal-Portabilität auf einer frischen Windows-Installation wurde nicht geprüft.

## Nur für Entwicklung und Build

Das Windows-Projektziel lautet `net8.0-windows10.0.22621.0`, die erklärte Mindestversion bleibt über `SupportedOSPlatformVersion` auf `10.0.19041.0`. Die zusätzliche SDK-Projektion erlaubt die offizielle optionale Rahmenfreigabe; auf älteren Windows-Versionen wird die fehlende API erkannt und der Rahmen beibehalten. Das Paket wird lokal mitgeliefert und beim Programmstart nicht heruntergeladen.

- .NET SDK **8.0.428**, fixiert durch `global.json`. Der separate `scripts/bootstrap-sdk.ps1` lädt dieses SDK nur auf ausdrücklichen Start des Skripts herunter und prüft SHA-512; die Anwendung kennt dieses Skript nicht.
- NuGet-Wiederherstellung ausschließlich aus `vendor/nuget`; keine öffentlichen Paketquellen in `NuGet.Config`.
- Das ASP.NET-Core-Runtime-Archiv 8.0.34 wird als vom SDK angeforderte Build-Abhängigkeit vorgehalten. Der Scanner betreibt keinen Webserver und referenziert kein ASP.NET-Core-Framework.
- Keine Testframework-Pakete: Tests sind kleine ausführbare C#-Prüfprogramme. OCR-Testbilder entstehen ausschließlich im RAM.

## Herkunft der Lizenztexte

- Tesseract: <https://github.com/tesseract-ocr/tesseract/blob/5.2.0/LICENSE>
- Wrapper: <https://github.com/charlesw/tesseract/blob/master/LICENSE.txt>
- Leptonica: <https://github.com/DanBloomberg/leptonica/blob/1.82.0/leptonica-license.txt>
- Vortice: <https://github.com/amerkoleci/Vortice.Windows/blob/main/LICENSE>
- SharpGen: <https://github.com/SharpGenTools/SharpGenTools/blob/main/LICENSE.txt>
- .NET-/WindowsDesktop-Lizenzen und .NET-Drittanbieterhinweise: unverändert aus den jeweiligen NuGet-Archiven.

Diese Übersicht dokumentiert die verwendeten Pakete und Hinweise; sie ist keine Behauptung einer CIG-Freigabe oder einer vollständigen juristischen Prüfung fremder Binärdateien.
