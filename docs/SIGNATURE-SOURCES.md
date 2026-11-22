# Herkunft und Grenzen der Signaturtabelle

Öffentliche Referenzen wurden am **16. November 2026** abgeglichen. Es wurden weder lokale Spieldateien noch Logs oder Spielspeicher benutzt. Die Anwendung ruft diese Quellen nicht ab; URLs in der JSON-Datei sind ausschließlich Herkunftsangaben.

Die mitgelieferte Tabelle enthält 26 Basissignaturen für Ship-Mining und zwei ausdrücklich generische Einträge für Handmining beziehungsweise ROC-Vorkommen. Alle Einträge tragen den Status `community-reference`. Das bedeutet einen Abgleich von veröffentlichten Community-Tabellen, **keine eigene Prüfung im laufenden Spiel und keine Bestätigung durch CIG**.

## Referenzen

| Quelle | Verwendete Angaben | Patch-/Prüfstatus der Quelle |
| --- | --- | --- |
| [STARVEIN: Signature Reference](https://starvein.app/en/signatures) | Basissignaturen aller 26 Ship-Mining-Typen; generische 3000-/4000-Einträge | Beschreibt die Werte als durch die Community im Spiel geprüft; auf dieser Seite kein eindeutig datierter Patchstand. |
| [Codiga: Signatures radar des minerais](https://codiga.fr/wiki/signatures-radar-des-minerais) | Unabhängiger Tabellenabgleich aller Basiswerte; generische FPS-/ROC-Kategorien | Seite nennt Patch **4.10** und eine Aktualisierung vom **5. Oktober 2026**. Diese Patchangabe wird mit Herkunftshinweis übernommen. |
| [Jokers Gambit: Mining & salvage](https://jokersgambit.org/mining) | Zusätzlicher Abgleich aller Basiswerte | Die beim Abruf gerenderte Seite nannte **4.10.2-LIVE.12881860**, ein Suchindex-Auszug dagegen 4.10.3. Deshalb keine Behauptung einer von uns bestätigten 4.10.3-Kompatibilität. |

Diese Community-Seiten sind die verwendeten öffentlichen Veröffentlichungen. Jokers Gambit nennt seinerseits eine Datamine als Herkunft; deren Dateien wurden für dieses Projekt nicht heruntergeladen oder geöffnet. Quellcode, Gestaltung und Implementierung von Smart Citizen oder den genannten Websites wurden nicht übernommen.

## Abweichungen und bewusste Entscheidungen

- Codiga führt für Aluminium beim Fünffachen den Wert `21452` auf. Der Basiswert `4285` stimmt mit den anderen Referenzen überein; das mathematische Fünffache ist `21425`. Es wird ausschließlich der abgeglichene Basiswert gespeichert. Vielfache berechnet die Anwendung selbst.
- Die Referenzen zeigen unterschiedliche maximale Clustergrößen, beispielsweise bei Bexalite. Deshalb gelten die dargestellten Vielfachen in diesem Programm als **rechnerische Kandidaten innerhalb der vom Benutzer gewählten Obergrenze**, nicht als Beweis einer derzeit möglichen Spawn-Größe. Das Programm modelliert keine Spawnwahrscheinlichkeiten.
- `3000` und `4000` identifizieren keine bestimmten Edelsteine. Die generischen Bezeichnungen sagen dies ausdrücklich. STARVEIN weist zudem bei `3000` auf eine Überschneidung mit großen Wracktrümmern hin. Der Mining-Kontext muss vom Benutzer beurteilt werden; die Tabelle ist kein vollständiger Katalog aller Radarobjekte.
- Die deutschen Namen für bekannte Stoffe (etwa Kupfer, Wolfram und Korund) sind beschreibende Übersetzungen. Fiktive Eigennamen bleiben unverändert. Es wird keine offizielle deutsche Spiellokalisierung behauptet.
- Keine Prozentanteile, Reinheit, Materialqualität, Preise, Fundorte oder vollständigen Gesteinszusammensetzungen werden aus einer Signatur abgeleitet.
- Gleichlautende Werte oder Vielfache sind mehrdeutig. Beispielsweise ist `19200 = 6 × 3200 = 5 × 3840` und `12000 = 4 × 3000 = 3 × 4000`. Die Anwendung darf keine dieser Alternativen stillschweigend als sicher auswählen.
- Ein abweichender OCR-Wert wird nicht auf einen Tabellenwert gerundet. Ein exakter rechnerischer Treffer bleibt eine Referenzzuordnung und ist kein Nachweis des tatsächlichen Inhalts.

## Manuelle Pflege

`data/signatures.json` ist eine lokale, editierbare Datei. Pro Eintrag werden `id`, englischer und deutscher Name, `baseSignature`, `source`, `verifiedPatch` und `verification` gespeichert. Neue oder veränderte Werte sollten eine nachvollziehbare Quelle und ihren tatsächlichen Prüfstatus erhalten. Ein unbekannter Patchstand muss als unbekannt stehen bleiben. Die referenzierten URLs werden von der fertigen Anwendung nicht automatisch geöffnet oder aktualisiert.

Die veröffentlichten Basiswerte werden als sachliche Angaben in einer eigenständig erstellten Datenstruktur genutzt. Fremder Programmcode und längere fremde Textpassagen sind nicht Bestandteil dieses Projekts.
