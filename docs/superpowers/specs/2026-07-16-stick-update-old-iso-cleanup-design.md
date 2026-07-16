# Alte ISO nach Stick-Aktualisierung löschen — Design

## Kontext

Der Nutzer hatte manuell eine ISO auf den Stick kopiert
(`neon-user-desktop-20260709-0522.iso`). ULM erkannte beim automatischen
Versionscheck ein Update, fragte "Jetzt aktualisieren?", der Nutzer
bestätigte, Download und Stick-Kopie liefen wie erwartet — doch danach lagen
zwei ISOs auf dem Stick: die alte und die neue
(`neon-testing-bigscreen-20260712-1906.iso`). ULM hat an keiner Stelle
gefragt, ob die alte Datei gelöscht oder behalten werden soll.

Das unterscheidet sich vom bereits behobenen Duplikat-Fall
([2026-07-15-stick-hash-verification-design.md](2026-07-15-stick-hash-verification-design.md)):
dort lag die alte Datei bereits VOR dem Scan neben einer schon vorhandenen
neuen Version auf dem Stick (kein Download nötig). Hier läuft der Download
gerade erst über ULM selbst — die alte Datei wird laut `SplitOutdatedFromDuplicates`
korrekt als `TrulyOutdated` eingestuft (neuer Name fehlte auf dem Stick), und
genau für diesen Zweig wird nach dem Kopieren nie nach dem Löschen gefragt.

## Root Cause

`SplitOutdatedFromDuplicates` (`ViewModels/MainViewModel.cs:625`) kennt den
alten Dateinamen für jeden `TrulyOutdated`-Kandidaten (er steckt im
durchlaufenen `oldFn`-Dictionary), gibt ihn aber nur für die
`StaleDuplicates`-Liste zurück; für `TrulyOutdated` wird er verworfen. Der
nachgelagerte Dialog `OnStickUpdateAvailable` (`Views/MainWindow.xaml.cs:378`)
stößt Download + Stick-Kopie an, hat danach aber keine Information mehr
darüber, welche Datei durch das Update ersetzt wurde — und fragt deshalb nie
nach dem Löschen.

## Ziel

Wenn ULM selbst ein Update vorschlägt und der Nutzer zustimmt, wird nach
erfolgreichem Download + Stick-Kopie IMMER gefragt, ob die dadurch überflüssig
gewordene alte ISO auf dem Stick gelöscht oder behalten werden soll.

## Nicht-Ziele

- Kein automatisches Löschen ohne Nachfrage.
- Keine Änderung am bereits bestehenden Duplikat-Lösch-Dialog
  (`OnStaleDuplicatesOnStick`) — der deckt einen anderen Erkennungspfad ab.
- Kein neuer Dialog-Typ — Wiederverwendung von `OrphanedDownloadsDialog`
  (gleiches Muster wie beim Duplikat-Fall), Standard-Checkbox-Zustand:
  angehakt (löschen), analog zum bestehenden Duplikat-Dialog.

## Technischer Entwurf

### 1. Alten Dateinamen durch die TrulyOutdated-Liste tragen

`SplitOutdatedFromDuplicates` liefert für `TrulyOutdated` künftig ebenfalls
`(IsoEntry Entry, string OldFilename)` statt nur `IsoEntry`:

```csharp
internal static (List<(IsoEntry Entry, string OldFilename)> TrulyOutdated,
                  List<(IsoEntry Entry, string OldFilename)> StaleDuplicates)
    SplitOutdatedFromDuplicates(...)
```

Der `StickUpdateAvailable`-Event sowie `TriggerAutoVersionCheck` werden auf
die neue Tupel-Form umgestellt (Name/Log-Ausgaben greifen weiterhin über
`.Entry.Name` zu — keine sichtbare Verhaltensänderung an dieser Stelle).

### 2. Nach erfolgreicher Aktualisierung nach Löschen fragen

`OnStickUpdateAvailable` (`Views/MainWindow.xaml.cs`) merkt sich beim
Bestätigen des Updates die alten Dateinamen und hängt sich einmalig
(self-unsubscribing) an `_vm.DownloadBatchCompleted` — dieses Event feuert
bereits heute erst NACH dem eigentlichen Stick-Kopiervorgang mit den echten
Kopier-Erfolgszahlen (siehe Kommentar bei `MainViewModel.cs:832`), ist also
der richtige Zeitpunkt.

Feuert das Event mit `copyOk > 0`, wird analog zu `OnStaleDuplicatesOnStick`
per `FindOldDuplicatePath` nach den alten Dateien auf dem Stick gesucht und
bei Fund ein `OrphanedDownloadsDialog` gezeigt ("Alte ISO-Version(en) auf dem
Stick" / "alte ISO(s), die durch das Update ersetzt wurden"). Bestätigung
löscht die ausgewählten Dateien über `IsoEntry.TryDelete` (wie beim
bestehenden Duplikat-Pfad), Abbruch loggt lediglich "behalten".

Wird `copyOk == 0` (Update fehlgeschlagen), wird nicht nach dem Löschen
gefragt — die alte Datei bleibt unangetastet, da keine funktionierende neue
Version vorhanden ist.

## Fehlerfälle

| Fall | Verhalten |
|---|---|
| Alte Datei wurde zwischenzeitlich manuell vom Stick entfernt | `FindOldDuplicatePath` findet nichts → kein Dialog, kein Fehler |
| Update schlägt fehl (`copyOk == 0`) | Kein Lösch-Dialog — alte Datei bleibt erhalten |
| Nutzer bricht den Lösch-Dialog ab | Alte Datei bleibt erhalten, Log-Eintrag |
| Mehrere Einträge gleichzeitig aktualisiert, nur einige erfolgreich | Lösch-Dialog erscheint trotzdem (sobald `copyOk > 0`), listet nur die alten Dateien der tatsächlich vorhandenen `TrulyOutdated`-Kandidaten — für fehlgeschlagene Einzel-Updates bleibt die zugehörige alte Datei ggf. in der Liste, auch wenn ihr Update nicht klappte (bewusst in Kauf genommen: seltener Fall, Nutzer sieht die Datei im Dialog und kann sie abwählen) |

## Betroffene Dateien

- `ViewModels/MainViewModel.cs` (`SplitOutdatedFromDuplicates`-Signatur,
  `TriggerAutoVersionCheck`, `StickUpdateAvailable`-Event-Signatur)
- `Views/MainWindow.xaml.cs` (`OnStickUpdateAvailable`-Erweiterung, neue
  private Hilfsmethode für den Lösch-Dialog)
- Bestehende Tests zu `SplitOutdatedFromDuplicates` in
  `ULM.Tests/MainViewModelDistroMatchingTests.cs` auf neue Signatur anpassen
