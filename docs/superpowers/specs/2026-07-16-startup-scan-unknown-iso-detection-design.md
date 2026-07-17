# Start-Scan erkennt unbekannte/neuere ISOs auf dem Stick — Design

## Kontext

Beim Testen der [Alte-ISO-Löschfrage-Spec](2026-07-16-stick-update-old-iso-cleanup-design.md)
ließ der Nutzer den Stick eingesteckt und startete das Programm neu. Zwei
manuell kopierte KDE-neon-ISOs (nicht Teil der 27 Katalog-Distros) tauchten
im Start-Protokoll nirgends auf — weder als unbekannt/importierbar noch
sonst wie. Anforderung des Nutzers: **beim Programmstart soll immer der
volle Stick-Scan laufen**, nicht nur die abgespeckte Variante.

## Root Cause

`UsbService.Instance.ScanStickVerifiedAsync(...)` liefert in beiden Fällen
dieselbe rohe Trefferliste (`found`/`incomplete`) — sowohl `UsbScanWorker`
(genutzt von `TriggerUsbScan()`, `MainViewModel.cs:373`) als auch der
Start-Scan-Block in `TriggerAutoVersionCheck` (`MainViewModel.cs:754`) rufen
exakt dieselbe Methode auf. Die beiden Pfade werten das Ergebnis danach aber
mit **komplett unabhängigem, eigenem Code** aus und decken dabei
unterschiedliche, sich nicht überschneidende Teilmengen der Klassifizierung
ab:

| Erkennung | `TriggerUsbScan()` | Start-Scan (`TriggerAutoVersionCheck`) |
|---|---|---|
| Unvollständige ISOs (`IncompleteIsosOnStickDetected`) | ✅ | ✅ |
| Hash-Mismatch bei versionslosen Dateinamen | ✅ | ❌ fehlt |
| "Echt veraltet" / Duplikat via `SplitOutdatedFromDuplicates` (`StickUpdateAvailable`/`StaleDuplicatesOnStickDetected`) | ❌ fehlt | ✅ |
| Neuere Version bereits auf Stick (`DetectNewerVersionsOnStick`, `NewerVersionsOnStickDetected`) | ✅ | ❌ fehlt |
| **Komplett unbekannte ISO (`UnknownIsosOnStickDetected`)** | ✅ | ❌ fehlt — genau der gemeldete Bug |
| Fehlend auf Stick (`MissingOnStickDetected`) | ✅ | ✅ |

Beide Implementierungen sind unabhängig voneinander gewachsen (die
Duplikat-/Veraltet-Trennung kam mit dem Versionscheck-Kontext, die
Unbekannt-/Neuer-Erkennung separat mit `TriggerUsbScan()`) und sind seither
auseinandergedriftet. Das ist derselbe Mechanismus, der schon zum
Duplikat-Bug vom 15.07. geführt hat — nur diesmal auf Ebene der beiden
Scan-Pfade statt innerhalb einer einzelnen Methode.

## Ziel

Eine einzige, gemeinsame Klassifizierungs-Methode wertet die Scan-Ergebnisse
(`found`, `incomplete`) aus und wird von **beiden** Aufrufstellen genutzt —
Start-Scan und manueller `TriggerUsbScan()`. Damit laufen ab sofort alle
Erkennungen (inkl. unbekannter ISOs) bei jedem Scan, unabhängig davon, ob er
beim Programmstart oder manuell ausgelöst wurde.

## Nicht-Ziele

- Keine Zusammenlegung der beiden *Auslöser* (Start-Scan bleibt an den
  Versionscheck gekoppelt inkl. `oldFn`-Kontext für die
  Duplikat-/Veraltet-Trennung; `TriggerUsbScan()` bleibt der eigenständige,
  jederzeit auslösbare Rescan) — nur die *Auswertung* der Scan-Ergebnisse
  wird zusammengeführt.
- Kein doppelter Stick-Scan beim Start (kein zusätzlicher `TriggerUsbScan()`-
  Aufruf hinterher) — die bereits vorhandene Trefferliste wird einmal
  vollständig ausgewertet.
- `SplitOutdatedFromDuplicates`/`StickUpdateAvailable`/
  `StaleDuplicatesOnStickDetected` bleiben auf den Fall beschränkt, in dem
  ein `oldFn`-Kontext (aus einem gerade gelaufenen Versionscheck) vorliegt.
  Für einen manuellen `TriggerUsbScan()` ohne vorausgehenden Versionscheck
  ist `oldFn` leer — die Methode muss das ohne Fehler abfangen (führt dann
  einfach zu leeren `od`/`duplicates`-Listen, wie bisher bei
  `TriggerUsbScan()` implizit der Fall).

## Technischer Entwurf

Neue private Methode, sinngemäß:

```csharp
private void ProcessStickScanResults(
    List<UsbService.StickIso> found,
    List<UsbService.StickIso> incomplete,
    Dictionary<string, int> oldFn,   // leeres Dictionary, wenn kein Versionscheck-Kontext vorliegt
    string drive)
```

Zusammengeführter Ablauf (Vereinigung beider bisherigen Implementierungen,
keine der bestehenden Einzel-Prüfungen entfällt):

1. `ApplyStickResults(found)`, `RefreshAllEntries()`.
2. Unvollständige ISOs → `IncompleteIsosOnStickDetected` (wie bisher in
   beiden Pfaden identisch).
3. Hash-Mismatch bei versionslosen Dateinamen (`DetectVersionlessHashMismatchesAsync`,
   bisher nur in `TriggerUsbScan()`) → jetzt auch beim Start-Scan aktiv.
4. `SplitOutdatedFromDuplicates(oldFn, ...)` → `StickUpdateAvailable` /
   `StaleDuplicatesOnStickDetected` (bisher nur im Start-Scan) → jetzt auch
   bei jedem manuellen `TriggerUsbScan()` aktiv, sofern `oldFn` zum
   Aufrufzeitpunkt gefüllt ist.
5. `DetectNewerVersionsOnStick` + Unbekannt-Erkennung (`initialUnknowns`/
   `additionalNewer`/`trueUnknowns`, bisher nur in `TriggerUsbScan()`) →
   jetzt auch beim Start-Scan aktiv. Dateien, die bereits über Schritt 4
   als `od`/`duplicates` erfasst wurden, werden hier ausgeschlossen (sonst
   könnte dieselbe Datei doppelt gemeldet werden — z. B. einmal als
   "veraltet" und einmal als "unbekannt").
6. `GetVerifiedCompleteEntriesMissingFromStick()` (abzüglich der bereits in
   `od` erfassten Einträge) → `MissingOnStickDetected`.

Beide Aufrufstellen rufen `ProcessStickScanResults` mit denselben
`found`/`incomplete` auf, die sie ohnehin schon aus
`ScanStickVerifiedAsync` erhalten:

- `TriggerUsbScan()` (`MainViewModel.cs:364`): `oldFn` = leeres
  `Dictionary<string, int>()` (kein Versionscheck lief gerade).
- Start-Scan-Block in `TriggerAutoVersionCheck` (`MainViewModel.cs:750`):
  `oldFn` wie bisher aus den Versionscheck-Ergebnissen berechnet.

Log-Texte dürfen sich dabei leicht angleichen (z. B. einheitliches
"✅ Alle ISOs auf {drive} aktuell." in beiden Pfaden) — das ist eine
gewollte Konsequenz der Vereinheitlichung, keine Regression.

## Fehlerfälle

| Fall | Verhalten |
|---|---|
| `oldFn` leer (manueller Scan ohne vorherigen Versionscheck) | `SplitOutdatedFromDuplicates` liefert leere Listen, kein Fehler — Schritt 4 entfällt praktisch, alles andere läuft normal |
| Dieselbe Stick-Datei würde sowohl als "veraltet/Duplikat" als auch als "unbekannt" klassifiziert | Schritt 5 schließt bereits über `oldFn`/`od`/`duplicates` erfasste Dateinamen explizit aus — keine doppelte Meldung |
| Hash-Berechnung (Schritt 3) schlägt fehl | Wie bisher: Log-Warnung, kein harter Fehler (unverändertes Verhalten aus `DetectVersionlessHashMismatchesAsync`) |

## Betroffene Dateien

- `ViewModels/MainViewModel.cs` (neue `ProcessStickScanResults`-Methode;
  `TriggerUsbScan()` und der Start-Scan-Block in `TriggerAutoVersionCheck`
  rufen sie auf statt ihrer bisherigen, eigenen Auswertung)
- Bestehende Tests, die sich auf die bisherige Aufteilung stützen
  (`ULM.Tests/MainViewModelDistroMatchingTests.cs`), ggf. um einen Test für
  die kombinierte Ausschluss-Logik (Schritt 5) ergänzen
