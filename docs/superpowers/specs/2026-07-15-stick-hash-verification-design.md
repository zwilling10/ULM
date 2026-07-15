# Stick-ISO-Verifikation (Duplikat-Fix + Hash-Integrität) — Design

## Kontext

Nach dem Fix für den statischen Hiren's-BootCD-Dateinamen
([2026-07-14, Commit db5e49f](../../../ULM.Tests/MainViewModelDistroMatchingTests.cs))
meldete der Nutzer einen weiteren, davon unabhängigen Fall: nach dem Import
einer frisch heruntergeladenen Equestria-OS-ISO über die Stick-Suche zeigte
ULM erneut "veraltet" an, obwohl die tatsächlich aktuelle Version
(`equestria-os-2026.07.15-x86_64.iso`) bereits auf dem Stick lag — nur die
ältere Datei (`equestria-os-2026.07.08-x86_64.iso`) war nie entfernt worden.
Zusätzlich bot ULM an keiner Stelle an, diese alte Duplikat-Datei zu löschen.

## Root Cause

`TriggerAutoVersionCheck` (`ViewModels/MainViewModel.cs`) berechnet die
Liste "veraltet auf Stick" (`od`) ausschließlich über die Frage "ist der
ALTE Dateiname noch auf dem Stick vorhanden?" — ohne zu prüfen, ob der NEUE
(aktuelle) Dateiname zusätzlich bereits vorhanden ist. Liegen beide
Versionen gleichzeitig auf dem Stick (alte Datei nie gelöscht), feuert die
Meldung fälschlich, obwohl der Stick bereits aktuell ist. Dieselbe Lücke ist
der Grund, warum die alte Datei nie zum Löschen vorgeschlagen wird — es gibt
schlicht keine Erkennung für "zwei Versionen derselben Distro liegen
gleichzeitig auf dem Stick".

Diese beiden Symptome sind reine Logikfehler im Versions-/Duplikat-Abgleich,
kein Dateiintegritätsproblem — ein Hash-Wert wäre hierfür nicht die Ursache
der Lösung. Der Nutzer hat zusätzlich vorgeschlagen, die Stick-Prüfung
generell über Hash-Werte statt reinen Dateinamen-Abgleich genauer zu machen;
dieses Feature wird im selben Design mit umgesetzt, da es echten Mehrwert
für Fälle mit versionslosen Dateinamen (z. B. Hiren's BootCD) sowie für die
Erkennung stiller Beschädigung bringt.

## Ziel

1. Die "veraltet"-Meldung unterscheidet korrekt zwischen "echt veraltet"
   (alter Name da, neuer fehlt) und "Duplikat" (alter UND neuer Name da) und
   bietet im Duplikat-Fall das Löschen der alten Datei an statt eines
   erneuten Downloads.
2. Lokale Hash-Integritätsprüfung (SHA-256) als zusätzliche, gezielt
   eingesetzte Absicherung — ersetzt NICHT den bestehenden
   Dateinamen-/Versions-Abgleich, ergänzt ihn nur in Verdachtsfällen.
3. Wo möglich (Ubuntu, Debian, Fedora), wird der lokale Hash zusätzlich
   gegen die vom Anbieter veröffentlichte offizielle Prüfsumme verifiziert.

## Nicht-Ziele

- Kein Ersatz der Dateinamen-/Versionslogik durch Hash-Vergleich als
  Standardpfad — Hash wird nur in klar begrenzten Verdachtsfällen berechnet
  (Performance: mehrere-GB-ISOs über USB sind langsam zu hashen).
- Kein automatisches Rehashing bei jedem Stick-Scan.
- Keine vollständige Abdeckung aller Distros mit offizieller
  Prüfsummen-Quelle — Architektur ist erweiterbar, Stufe 2 startet mit drei
  Distros mit stabilem, einfach parsbarem Format.
- Kein neuer eigenständiger Dialog für Duplikate — Integration in den
  bestehenden `OnStickUpdateAvailable`-Dialog.

## Technischer Entwurf

### 1. Korrektur der Veraltet-/Duplikat-Erkennung

In `TriggerAutoVersionCheck` (`ViewModels/MainViewModel.cs`) wird die
`od`-Berechnung in zwei Gruppen aufgeteilt:

```csharp
// bisher: od = oldFn.Where(kvp => sn.Contains(kvp.Key))...
// neu: pro Kandidat zusätzlich prüfen, ob der NEUE Dateiname ebenfalls da ist
var trulyOutdated = new List<IsoEntry>();
var staleDuplicates = new List<(IsoEntry Entry, string OldFilename)>();
foreach (var kvp in oldFn.Where(kvp => sn.Contains(kvp.Key)))
{
    var e = _db.Entries[kvp.Value];
    if (sn.Contains(e.Filename)) staleDuplicates.Add((e, kvp.Key)); // neuer Name auch da
    else                          trulyOutdated.Add(e);              // neuer Name fehlt
}
```

`StickUpdateAvailable` bekommt eine zweite Nutzlast (Duplikat-Liste inkl.
altem Dateipfad zum Löschen) — Event-Signatur wird um die
Duplikat-Liste erweitert.

### 2. Dialog-Erweiterung (`OnStickUpdateAvailable`, `Views/MainWindow.xaml.cs`)

- Nur `trulyOutdated` → bisheriger Text/Ablauf unverändert.
- Nur `staleDuplicates` → neuer Text: "Auf {drive} liegen {n} veraltete
  Datei(en), deren aktuelle Version bereits vorhanden ist: … Jetzt
  löschen?" — Ja löscht die alten Dateien (`IsoEntry.TryDelete`, wie beim
  bestehenden Datenmüll-Dialog).
- Beide gleichzeitig → ein Dialog mit zwei Abschnitten (angelehnt an
  `OrphanedDownloadsDialog`), eine gemeinsame Bestätigung, getrennte
  Checkbox-Listen pro Abschnitt.

### 3. Hash-Datenmodell (`Core/Models/IsoEntry.cs`)

Neue Felder:

```csharp
public string Sha256       { get; set; } = string.Empty; // Hex, lowercase
public string Sha256Source { get; set; } = string.Empty;  // "", "LocalDownload", "OfficialChecksum"
```

Neue Utility:

```csharp
public static async Task<string> ComputeSha256Async(string path, CancellationToken ct = default)
```

Streamt die Datei blockweise (`SHA256.HashDataAsync` über einen
`FileStream`), kein Volleinlesen in den Speicher.

### 4. Wann wird gehasht (Stufe 1 — lokale Integrität)

Referenz-Hash wird **einmalig** gesetzt:

- Nach erfolgreichem lokalem Download, direkt nach Abschluss in
  `DownloadWorker`, vor dem Kopieren auf den Stick →
  `Sha256Source = "LocalDownload"`.
- Beim Import unbekannter Stick-ISOs (`ImportStickIsosDialog`-Flow) — Hash
  wird direkt von der Stick-Datei berechnet, da keine lokale Kopie
  existiert.

Kein automatisches Rehashing bei jedem Scan. Ein erneuter Hash-Vergleich der
Stick-Datei wird gezielt nur ausgelöst bei:

1. **Versionslose Dateinamen** — erkannt daran, dass
   `HttpService.ExtractVersion()` weder aus dem alten noch aus dem neuen
   Dateinamen eine Version extrahieren kann (z. B. `ResolveHirensAsync`,
   die immer denselben Dateinamen ohne Versionsnummer liefert) — UND ein
   Referenz-Hash liegt vor → Hash entscheidet eindeutig, ob die Stick-Kopie
   inhaltlich der aktuellen Version entspricht, statt sich auf den
   nichtssagenden Dateinamen zu verlassen.
2. **Manuell** über einen neuen Button "Integrität prüfen" im
   Stick-Kontextmenü.

Ein Hash-Mismatch wird wie eine unvollständige/beschädigte ISO behandelt —
nutzt den bestehenden `IncompleteIsosOnStickDetected`-Dialog-Pfad, keinen
neuen.

### 5. Offizielle Prüfsumme (Stufe 2)

Neue optionale Erweiterung pro Resolver in `Core/Services/HttpService.cs`:

```csharp
private async Task<string?> ResolveOfficialChecksumAsync(string distroKey, string filename)
```

Implementiert zunächst für **Ubuntu, Debian, Fedora** (stabilstes,
einfachst parsbares `SHA256SUMS`/`CHECKSUM`-Format). Weitere Distros lassen
sich später nach demselben Muster ergänzen, ohne die Architektur zu ändern.

Ablauf nach Download (nur für Distros mit Resolver-Erweiterung):

- Offizielle Prüfsumme abrufen, mit lokal berechnetem Hash vergleichen.
- Übereinstimmung → `Sha256Source = "OfficialChecksum"`.
- Abweichung → Warnung in Log + Dialog (kein stiller Fallback — ernstes
  Signal für manipulierte/fehlerhafte Datei).
- Keine offizielle Quelle verfügbar (kein Resolver, Netzwerkfehler) →
  Fallback auf `Sha256Source = "LocalDownload"`, kein Fehler.

## Fehlerfälle

| Fall | Verhalten |
|---|---|
| Hash-Berechnung schlägt fehl (Datei gesperrt, Lesefehler) | Log-Warnung, Vorgang wird wie "kein Referenz-Hash vorhanden" behandelt — kein harter Fehler |
| Offizielle Prüfsumme nicht erreichbar/Format geändert | Fallback auf `LocalDownload`, kein Nutzer-sichtbarer Fehler |
| Hash-Mismatch gegen offizielle Prüfsumme | Sichtbare Warnung (Log + Dialog) — bewusst NICHT stillschweigend ignoriert |
| Duplikat- und Veraltet-Liste gleichzeitig nicht leer | Ein gemeinsamer Dialog mit zwei Abschnitten, siehe Abschnitt 2 |

## Betroffene Dateien

- `ViewModels/MainViewModel.cs` (Duplikat-/Veraltet-Trennung, Event-Signatur,
  Hash-Trigger bei versionslosen Resolvern)
- `Views/MainWindow.xaml.cs` (Dialog-Erweiterung `OnStickUpdateAvailable`)
- `Core/Models/IsoEntry.cs` (`Sha256`, `Sha256Source`, `ComputeSha256Async`)
- `Core/Services/HttpService.cs` (`ResolveOfficialChecksumAsync` für
  Ubuntu/Debian/Fedora)
- `Core/Services/UsbService.cs` (ggf. Hash-Aufruf im Scan-Pfad für
  Verdachtsfälle)
- `Core/Workers/Workers.cs` (`DownloadWorker`: Hash nach Abschluss)
- `Views/Dialogs/DatabaseDialogs.cs` (`ImportStickIsosDialog`: Hash beim
  Import)
- Neue Tests analog bestehender Konvention (nur reine/statische Logik,
  keine Worker-Orchestrierung): Duplikat-/Veraltet-Trennung,
  `ComputeSha256Async`, Checksum-Parser (Ubuntu/Debian/Fedora-Fixtures)
