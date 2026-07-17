# Stick-Scan-Verbesserungen & Programm-Selbst-Update Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Drei zusammenhängende Verbesserungen am ULM: (A) nach einem von ULM
selbst vorgeschlagenen Stick-Update das Löschen der alten ISO anbieten,
(B) beide Stick-Scan-Pfade auf eine gemeinsame Auswertung vereinheitlichen,
damit unbekannte ISOs auch beim Programmstart erkannt werden, und (C) ein
dauerhaftes Update-Banner mit Download-Möglichkeit für neue Programmversionen.

**Architecture:** WPF-Desktop-App (MVVM). Scan-Logik in
`ViewModels/MainViewModel.cs`, Dialoge/UI-Verdrahtung in
`Views/MainWindow.xaml(.cs)` und `Views/Dialogs/`, HTTP/GitHub in
`Core/Services/HttpService.cs`. Reine Logik wird per xUnit getestet
(`ULM.Tests`), UI-Verdrahtung per Build + manuellem Lauf verifiziert.

**Tech Stack:** C# / .NET 8 (net8.0-windows), WPF, xUnit.

## Global Constraints

- Sprache aller nutzersichtbaren Texte und Log-Ausgaben: **Deutsch**.
- Versionsnummer-Quelle ist ausschließlich `<Version>` in der `.csproj`
  (gelesen über `Constants.AppVersion`) — niemals hart kodieren.
- Kein automatisches Ausführen heruntergeladener .exe-Dateien; der Anwender
  startet Installation/Programm selbst.
- Bestehendes Namens-/Log-Stilmuster beibehalten (Emoji-Präfixe wie
  `💾`, `🆕`, `🗑`, `⚠`, `✅` wie im umgebenden Code).
- Build-Verifikation: `dotnet build UniversalLinuxManager.csproj -c Debug`.
- Test-Verifikation: `dotnet test ULM.Tests/ULM.Tests.csproj`.

---

## Phase A — Alte ISO nach Stick-Update löschen (Spec: 2026-07-16-stick-update-old-iso-cleanup-design.md)

### Task A1: `SplitOutdatedFromDuplicates` liefert alten Dateinamen auch für „veraltet"

**Files:**
- Modify: `ViewModels/MainViewModel.cs:625-638`
- Test: `ULM.Tests/MainViewModelDistroMatchingTests.cs:118-187`

**Interfaces:**
- Produces: `SplitOutdatedFromDuplicates(Dictionary<string,int> oldFn, IReadOnlyList<IsoEntry> entries, HashSet<string> stickFilenames)` →
  `(List<(IsoEntry Entry, string OldFilename)> TrulyOutdated, List<(IsoEntry Entry, string OldFilename)> StaleDuplicates)`

- [ ] **Step 1: Bestehende Tests auf die neue Tupel-Signatur anpassen (rot machen)**

In `ULM.Tests/MainViewModelDistroMatchingTests.cs` die vier Tests der
inneren Klasse `MainViewModelSplitOutdatedFromDuplicatesTests` so ändern,
dass `outdated` jetzt Tupel enthält. Konkret die Assertions, die auf
`outdated[0].Filename` zugreifen, auf `outdated[0].Entry.Filename` umstellen
und im `TrulyOutdated`-Fall zusätzlich den alten Dateinamen prüfen:

```csharp
[Fact]
public void SplitOutdatedFromDuplicates_OldNamePresentNewNameAbsent_IsTrulyOutdated()
{
    var entries = new List<IsoEntry> { Entry("equestria-os-2026.07.15-x86_64.iso") };
    var oldFn = new Dictionary<string, int> { ["equestria-os-2026.07.08-x86_64.iso"] = 0 };
    var stick = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "equestria-os-2026.07.08-x86_64.iso" };

    var (outdated, duplicates) = MainViewModel.SplitOutdatedFromDuplicates(oldFn, entries, stick);

    Assert.Single(outdated);
    Assert.Equal("equestria-os-2026.07.08-x86_64.iso", outdated[0].OldFilename);
    Assert.Empty(duplicates);
}
```

Und im Mehrfach-Test die veraltet-Assertion auf Tupel umstellen:

```csharp
Assert.Single(outdated); Assert.Equal("distro-a-2.0.iso", outdated[0].Entry.Filename);
Assert.Equal("distro-a-1.0.iso", outdated[0].OldFilename);
Assert.Single(duplicates); Assert.Equal("distro-b-1.0.iso", duplicates[0].OldFilename);
```

- [ ] **Step 2: Tests bauen — müssen wegen Signaturänderung NICHT kompilieren**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj`
Expected: Compile-FEHLER (`outdated[0].OldFilename` existiert noch nicht) — bestätigt, dass die Produktivsignatur noch alt ist.

- [ ] **Step 3: Produktivmethode auf Tupel umstellen**

In `ViewModels/MainViewModel.cs` die Methode ersetzen:

```csharp
internal static (List<(IsoEntry Entry, string OldFilename)> TrulyOutdated,
                  List<(IsoEntry Entry, string OldFilename)> StaleDuplicates)
    SplitOutdatedFromDuplicates(Dictionary<string, int> oldFn, IReadOnlyList<IsoEntry> entries, HashSet<string> stickFilenames)
{
    var outdated   = new List<(IsoEntry, string)>();
    var duplicates = new List<(IsoEntry, string)>();
    foreach (var kvp in oldFn)
    {
        if (!stickFilenames.Contains(kvp.Key)) continue; // alter Name nicht (mehr) auf dem Stick
        var e = entries[kvp.Value];
        if (stickFilenames.Contains(e.Filename)) duplicates.Add((e, kvp.Key)); // neuer Name AUCH da
        else                                     outdated.Add((e, kvp.Key));   // neuer Name fehlt
    }
    return (outdated, duplicates);
}
```

- [ ] **Step 4: Aufrufstelle im Start-Scan an Tupel anpassen (nur damit es kompiliert)**

In `ViewModels/MainViewModel.cs:767` die veraltet-Schleife und das
`missing`-Filter anpassen (die Event-Signatur folgt in Task A2 — hier nur
kompilierbar halten):

```csharp
if (od.Count > 0) { Log($"💾 {od.Count} veraltete ISO(s) auf {driveToScan}."); foreach (var (e, _) in od) Log($"   🆕 {e.Name}: v{e.RemoteVersion}"); StickUpdateAvailable?.Invoke(od.Select(x => x.Entry).ToList(), driveToScan); }
```

Und `MainViewModel.cs:775`:

```csharp
var missing = GetVerifiedCompleteEntriesMissingFromStick().Where(e => !od.Any(x => x.Entry == e)).ToList();
```

- [ ] **Step 5: Tests grün**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj`
Expected: PASS (alle Split-Tests grün, Rest unverändert grün).

- [ ] **Step 6: Commit**

```bash
git add ViewModels/MainViewModel.cs ULM.Tests/MainViewModelDistroMatchingTests.cs
git commit -m "feat: SplitOutdatedFromDuplicates liefert alten Dateinamen auch fuer veraltete ISOs"
```

---

### Task A2: `StickUpdateAvailable`-Event trägt den alten Dateinamen

**Files:**
- Modify: `ViewModels/MainViewModel.cs:143` (Event-Signatur), `:767` (Invoke)

**Interfaces:**
- Produces: `event Action<List<(IsoEntry Entry, string OldFilename)>, string>? StickUpdateAvailable`
- Consumes: `SplitOutdatedFromDuplicates` (Task A1)

- [ ] **Step 1: Event-Signatur ändern**

In `ViewModels/MainViewModel.cs:143`:

```csharp
public event Action<List<(IsoEntry Entry, string OldFilename)>, string>?  StickUpdateAvailable;
```

- [ ] **Step 2: Invoke ohne `.Select` (volle Tupel-Liste weitergeben)**

In `ViewModels/MainViewModel.cs:767` das in A1/Step 4 eingesetzte
`.Select(x => x.Entry).ToList()` entfernen — jetzt `od` direkt übergeben:

```csharp
if (od.Count > 0) { Log($"💾 {od.Count} veraltete ISO(s) auf {driveToScan}."); foreach (var (e, _) in od) Log($"   🆕 {e.Name}: v{e.RemoteVersion}"); StickUpdateAvailable?.Invoke(od, driveToScan); }
```

- [ ] **Step 3: Build erwartet FEHLER im Consumer (`OnStickUpdateAvailable`)**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: Compile-FEHLER in `Views/MainWindow.xaml.cs` (Signatur von `OnStickUpdateAvailable` passt nicht mehr) — wird in Task A3 behoben.

- [ ] **Step 4: (kein Commit — zusammen mit A3, da Build erst dort wieder grün)**

---

### Task A3: Nach erfolgreicher Aktualisierung das Löschen der alten ISO anbieten

**Files:**
- Modify: `Views/MainWindow.xaml.cs:378-386` (`OnStickUpdateAvailable`)
- Create (Methode): `Views/MainWindow.xaml.cs` — neue private Methode `OfferDeleteOldIsosAfterUpdate`

**Interfaces:**
- Consumes: `StickUpdateAvailable` (A2), `_vm.DownloadBatchCompleted` (`event Action<int,int,int>`), `FindOldDuplicatePath(string root, string filename)` (vorhanden, `MainWindow.xaml.cs:418`), `OrphanedDownloadsDialog(List<(string Path,long Size)>, string title, string itemLabel)`, `IsoEntry.TryDelete`, `IsoEntry.GetRobustLength`, `UsbService.DriveRoot`.

- [ ] **Step 1: `OnStickUpdateAvailable` auf Tupel-Liste umstellen und Lösch-Nachlauf anhängen**

In `Views/MainWindow.xaml.cs` die Methode ersetzen:

```csharp
private void OnStickUpdateAvailable(List<(IsoEntry Entry, string OldFilename)> outdated, string drive)
{
    if (outdated.Count == 0) return;
    var sb = new StringBuilder(); sb.AppendLine($"Auf {drive} wurden {outdated.Count} veraltete ISO(s) gefunden:"); sb.AppendLine();
    foreach (var (entry, _) in outdated) sb.AppendLine($"  • {entry.Name}"); sb.AppendLine(); sb.AppendLine("Jetzt aktualisieren?");
    if (MessageBox.Show(sb.ToString(), "💾 Stick-Aktualisierung", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

    var entries      = outdated.Select(x => x.Entry).ToList();
    var oldFilenames = outdated.Select(x => x.OldFilename).ToList();
    foreach (var e in entries) e.IsSelected = true; _vm.RefreshAllEntries();

    // Nach ERFOLGREICHEM Download + Stick-Kopie (DownloadBatchCompleted feuert erst danach mit den
    // echten Kopier-Erfolgszahlen) das Löschen der jetzt überflüssigen alten Datei anbieten. Der
    // Handler entfernt sich beim ersten Feuern selbst — Downloads laufen serialisiert (SetBusy),
    // das erste Batch-Ende nach dem Abonnieren gehört daher zu genau diesem Update.
    void OnBatchDone(int ok, int failed, int _unused)
    {
        _vm.DownloadBatchCompleted -= OnBatchDone;
        if (ok <= 0) return; // Update fehlgeschlagen → alte Datei unangetastet lassen
        OfferDeleteOldIsosAfterUpdate(oldFilenames, drive);
    }
    _vm.DownloadBatchCompleted += OnBatchDone;

    StartDownloadWithProgressDialog(entries, drive, copyAfter: true, deleteAfter: false, slots: Math.Min(entries.Count, Constants.MaxParallelSlots));
}

// Bietet nach einer von ULM selbst durchgeführten Aktualisierung das Löschen der alten,
// jetzt ersetzten ISO-Datei(en) an — Standard: löschen (angehakt), wie beim Duplikat-Dialog.
private void OfferDeleteOldIsosAfterUpdate(List<string> oldFilenames, string drive)
{
    string root = UsbService.DriveRoot(drive);
    var files = new List<(string Path, long Size)>();
    foreach (string fn in oldFilenames)
    {
        if (string.IsNullOrWhiteSpace(fn)) continue;
        string? path = FindOldDuplicatePath(root, fn);
        if (path != null) files.Add((path, IsoEntry.GetRobustLength(path)));
    }
    if (files.Count == 0) return; // alte Datei schon weg / nie gefunden

    var dlg = new OrphanedDownloadsDialog(files,
        "Alte ISO-Version(en) auf dem Stick",
        "alte ISO(s), die durch das Update ersetzt wurden") { Owner = this };
    if (dlg.ShowDialog() == true)
    {
        int deleted = 0, failed = 0;
        foreach (string path in dlg.ToDelete)
        { if (IsoEntry.TryDelete(path, AppendLog)) { deleted++; AppendLog($"   🗑 Gelöscht: {Path.GetFileName(path)}"); } else failed++; }
        AppendLog($"🗑 {deleted} alte ISO-Version(en) auf {drive} gelöscht" + (failed > 0 ? $", {failed} fehlgeschlagen" : "") + ".");
        if (deleted > 0) _vm.TriggerVentoyMenuUpdate(drive);
    }
    else AppendLog($"ℹ Alte ISO-Version(en) behalten ({files.Count} Datei(en)).");
}
```

- [ ] **Step 2: Build grün**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: Build erfolgreich (0 Fehler).

- [ ] **Step 3: Tests grün (keine Regression)**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add ViewModels/MainViewModel.cs Views/MainWindow.xaml.cs
git commit -m "feat: nach ULM-Update Loeschen der alten ISO auf dem Stick anbieten"
```

---

## Phase B — Scan-Pfade vereinheitlichen (Spec: 2026-07-16-startup-scan-unknown-iso-detection-design.md)

### Task B1: Gemeinsame `ProcessStickScanResults`-Methode einführen

**Files:**
- Modify: `ViewModels/MainViewModel.cs` — neue private Methode; `TriggerUsbScan` (`:364-440`) und Start-Scan-Block in `TriggerAutoVersionCheck` (`:750-778`) rufen sie auf.

**Interfaces:**
- Produces: `private void ProcessStickScanResults(List<UsbService.StickIso> found, List<UsbService.StickIso> incomplete, Dictionary<string,int> oldFn, string drive)` — muss auf dem UI-Thread laufen.
- Consumes: `ApplyStickResults`, `DetectVersionlessHashMismatchesAsync`, `SplitOutdatedFromDuplicates` (A1), `DetectNewerVersionsOnStick`, `IsLikelySameDistroByName`, `IsVersionNewer`, `HttpService.ExtractVersion`, `GetVerifiedCompleteEntriesMissingFromStick`, sowie die Events `IncompleteIsosOnStickDetected`, `StickUpdateAvailable`, `StaleDuplicatesOnStickDetected`, `NewerVersionsOnStickDetected`, `UnknownIsosOnStickDetected`, `MissingOnStickDetected`.

- [ ] **Step 1: Neue Methode einfügen**

In `ViewModels/MainViewModel.cs` direkt nach `TriggerUsbScan` (vor
`DetectNewerVersionsOnStick`, also vor `MainViewModel.cs:442`) einfügen:

```csharp
/// <summary>
/// Gemeinsame Auswertung eines Stick-Scan-Ergebnisses für BEIDE Scan-Pfade (Start-Scan in
/// TriggerAutoVersionCheck und manueller TriggerUsbScan). Vorher hatten beide Pfade eigenen,
/// auseinandergedrifteten Code — nur TriggerUsbScan erkannte unbekannte ISOs, nur der Start-Scan
/// die Veraltet-/Duplikat-Trennung. oldFn ist leer, wenn kein Versionscheck-Kontext vorliegt
/// (manueller Scan) → die Veraltet-/Duplikat-Erkennung liefert dann leere Listen. Muss auf dem
/// UI-Thread aufgerufen werden (verändert gebundene Zustände und feuert UI-Dialog-Events).
/// </summary>
private void ProcessStickScanResults(
    List<UsbService.StickIso> found, List<UsbService.StickIso> incomplete,
    Dictionary<string, int> oldFn, string drive)
{
    ApplyStickResults(found); RefreshAllEntries();

    if (incomplete.Count > 0)
    {
        Log($"⚠ Stick-Prüfung {drive}: {incomplete.Count} unvollständige ISO(s) erkannt (Online-Größenprüfung).");
        foreach (var s in incomplete) Log($"   ⚠ {s.Filename}  ({FormatGb(s.Size)}) — vermutlich Datenmüll.");
        IncompleteIsosOnStickDetected?.Invoke(incomplete, drive);
    }

    // Hash-Abgleich für versionslose Dateinamen (fire-and-forget — kann bei GB-ISOs dauern).
    _ = Task.Run(async () =>
    {
        var mismatches = await DetectVersionlessHashMismatchesAsync(found).ConfigureAwait(false);
        _ui.Invoke(() =>
        {
            RefreshAllEntries();
            if (mismatches.Count == 0) return;
            Log($"⚠ Stick-Prüfung {drive}: {mismatches.Count} ISO(s) mit versionslosem Namen weichen vom bekannten Referenz-Hash ab.");
            foreach (var m in mismatches) Log($"   ⚠ {m.Filename} — Hash-Abweichung, vermutlich beschädigt oder ersetzt.");
            IncompleteIsosOnStickDetected?.Invoke(mismatches, drive);
        });
    });

    // Veraltet-/Duplikat-Trennung (nur mit Versionscheck-Kontext; sonst oldFn leer → leere Listen).
    var stickFn = new HashSet<string>(found.Select(f => f.Filename), StringComparer.OrdinalIgnoreCase);
    var (od, duplicates) = SplitOutdatedFromDuplicates(oldFn, _db.Entries, stickFn);
    if (od.Count > 0)
    {
        Log($"💾 {od.Count} veraltete ISO(s) auf {drive}.");
        foreach (var (e, _) in od) Log($"   🆕 {e.Name}: v{e.RemoteVersion}");
        StickUpdateAvailable?.Invoke(od, drive);
    }
    if (duplicates.Count > 0)
    {
        Log($"🗑 {duplicates.Count} veraltete Duplikat-ISO(s) auf {drive} (aktuelle Version bereits vorhanden).");
        foreach (var (e, oldFilename) in duplicates) Log($"   🗑 {e.Name}: {oldFilename}");
        StaleDuplicatesOnStickDetected?.Invoke(duplicates, drive);
    }
    if (od.Count == 0 && duplicates.Count == 0 && found.Count > 0)
        Log($"✅ Alle ISOs auf {drive} aktuell.");

    // Bereits als veraltet/Duplikat gemeldete alte Dateinamen aus der Neuer-/Unbekannt-Erkennung
    // ausschließen, sonst würde dieselbe Datei doppelt gemeldet (einmal "veraltet", einmal "unbekannt").
    var handled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var (_, oldFilename) in od)         handled.Add(oldFilename);
    foreach (var (_, oldFilename) in duplicates) handled.Add(oldFilename);

    var newerOnStick = DetectNewerVersionsOnStick(found);
    var newerFnSet   = new HashSet<string>(newerOnStick.Select(x => x.StickIso.Filename), StringComparer.OrdinalIgnoreCase);
    var dbFn         = new HashSet<string>(_db.Entries.Select(e => e.Filename), StringComparer.OrdinalIgnoreCase);
    var initialUnknowns = found.Where(f => !string.IsNullOrWhiteSpace(f.Filename)
                                        && !dbFn.Contains(f.Filename)
                                        && !newerFnSet.Contains(f.Filename)
                                        && !handled.Contains(f.Filename)).ToList();

    var additionalNewer = new List<(IsoEntry DbEntry, UsbService.StickIso StickIso)>();
    var trueUnknowns    = new List<UsbService.StickIso>();
    foreach (var stickIso in initialUnknowns)
    {
        var match = _db.Entries.Where(e => !string.IsNullOrWhiteSpace(e.Name))
            .FirstOrDefault(e => IsLikelySameDistroByName(e.Name, stickIso.Filename) &&
                (string.IsNullOrWhiteSpace(e.Filename) ||
                 IsVersionNewer(HttpService.ExtractVersion(stickIso.Filename), HttpService.ExtractVersion(e.Filename))));
        if (match != null) additionalNewer.Add((match, stickIso));
        else               trueUnknowns.Add(stickIso);
    }

    var allNewer = newerOnStick.Concat(additionalNewer).ToList();
    if (allNewer.Count > 0) NewerVersionsOnStickDetected?.Invoke(allNewer, drive);
    if (trueUnknowns.Count > 0) UnknownIsosOnStickDetected?.Invoke(trueUnknowns, drive);

    var odEntries = new HashSet<IsoEntry>(od.Select(x => x.Entry));
    var missing = GetVerifiedCompleteEntriesMissingFromStick().Where(e => !odEntries.Contains(e)).ToList();
    if (missing.Count > 0) MissingOnStickDetected?.Invoke(missing, drive);
}
```

- [ ] **Step 2: Build grün (Methode noch ungenutzt)**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: Build erfolgreich (evtl. Warnung „ungenutzt" ist ok — nächste Tasks nutzen sie).

- [ ] **Step 3: Commit**

```bash
git add ViewModels/MainViewModel.cs
git commit -m "refactor: gemeinsame ProcessStickScanResults-Methode fuer beide Stick-Scan-Pfade"
```

---

### Task B2: Manuellen `TriggerUsbScan` auf die gemeinsame Methode umstellen

**Files:**
- Modify: `ViewModels/MainViewModel.cs:374-438` (Completed-Handler des `UsbScanWorker`)

**Interfaces:**
- Consumes: `ProcessStickScanResults` (B1)

- [ ] **Step 1: Completed-Handler ersetzen**

In `ViewModels/MainViewModel.cs` den `worker.Completed += ...`-Block in
`TriggerUsbScan` (aktuell `:374-438`) ersetzen durch:

```csharp
worker.Completed += (ltr, found, incomplete) => _ui.Invoke(() =>
{
    UsbScanActive = false; UsbScanPercent = 100;
    StatusText = $"✓ Stick-Scan {ltr}: {found.Count} ISO(s).";
    Log($"💾 Stick-Scan {ltr}: {found.Count} ISO(s) gefunden.");
    if (found.Count > 0) foreach (var iso in found) Log($"   • {iso.Filename}  [{iso.Category}]  {iso.Size/1_073_741_824.0:F2} GB");
    OnPropertyChanged(nameof(DriveInfoText));
    // Manueller Scan hat keinen Versionscheck-Kontext → leeres oldFn (keine Veraltet-/Duplikat-Trennung).
    ProcessStickScanResults(found, incomplete, new Dictionary<string, int>(), ltr);
});
```

- [ ] **Step 2: Build grün**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: Build erfolgreich.

- [ ] **Step 3: Manuell verifizieren (Regression manueller Scan)**

Programm starten (`dotnet run --project UniversalLinuxManager.csproj`),
Stick nach Programmstart ab- und wieder einstecken bzw. im Laufwerks-Dropdown
neu wählen. Erwartung im Protokoll: unbekannte ISOs (z.B. neon-Dateien)
werden als „❓ N unbekannte ISO(s)" gemeldet und der Import-Dialog erscheint.

- [ ] **Step 4: Commit**

```bash
git add ViewModels/MainViewModel.cs
git commit -m "refactor: TriggerUsbScan nutzt gemeinsame ProcessStickScanResults"
```

---

### Task B3: Start-Scan auf die gemeinsame Methode umstellen (behebt „unbekannte ISOs beim Start")

**Files:**
- Modify: `ViewModels/MainViewModel.cs:750-778` (Start-Scan-`Task.Run`-Block in `TriggerAutoVersionCheck`)

**Interfaces:**
- Consumes: `ProcessStickScanResults` (B1)

- [ ] **Step 1: Start-Scan-Block ersetzen**

In `ViewModels/MainViewModel.cs` den `if (!string.IsNullOrEmpty(driveToScan))
_ = Task.Run(async () => { ... });`-Block ersetzen durch:

```csharp
string driveToScan = SelectedDriveLetter;
if (!string.IsNullOrEmpty(driveToScan))
    _ = Task.Run(async () =>
    {
        _ui.Invoke(() => { UsbScanActive = true; UsbScanPercent = 0; Log($"💾 Prüfe Stick {driveToScan} …"); });
        var (si, incomplete) = await UsbService.Instance.ScanStickVerifiedAsync(driveToScan, _db.Entries).ConfigureAwait(false);
        _ui.Invoke(() =>
        {
            UsbScanActive = false; UsbScanPercent = 100;
            RecordHistory($"💾 Stick-Prüfung {driveToScan} abgeschlossen ({si.Count} ISO(s) erkannt).");
            // Start-Scan HAT Versionscheck-Kontext → oldFn aus den Update-Ergebnissen (oben berechnet).
            ProcessStickScanResults(si, incomplete, oldFn, driveToScan);
        });
    });
```

Hinweis: Die lokale Variable `oldFn` wird im umgebenden `worker.Completed`
bereits berechnet (`MainViewModel.cs:696-699`) und ist in dieser Closure
sichtbar — nicht duplizieren. Die alten Inline-Blöcke für `incomplete`,
`od`/`duplicates`, `StickUpdateAvailable`, `StaleDuplicatesOnStickDetected`
und `missing` entfallen vollständig (jetzt in `ProcessStickScanResults`).

- [ ] **Step 2: Build grün**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: Build erfolgreich.

- [ ] **Step 3: Tests grün**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj`
Expected: PASS.

- [ ] **Step 4: Manuell verifizieren (Kern-Bugfix)**

Arbeitsordner-`ULM_Data` löschen (Ausgangssituation), zwei nicht im Katalog
enthaltene ISOs (z.B. neon) auf den Stick legen, Programm starten und den
Stick **eingesteckt lassen**. Erwartung: nach dem Online-Versionscheck +
Start-Stick-Scan erscheint der „❓ unbekannte ISO(s)"-Import-Dialog für die
neon-Dateien — ohne dass der Stick neu gesteckt werden muss.

- [ ] **Step 5: Commit**

```bash
git add ViewModels/MainViewModel.cs
git commit -m "fix: Start-Scan erkennt unbekannte ISOs auf dem Stick (gemeinsame Auswertung)"
```

---

## Phase C — Programm-Selbst-Update-Banner (Spec: 2026-07-16-program-self-update-banner-design.md)

### Task C1: Release-Asset-Zuordnung als reine, testbare Logik

**Files:**
- Modify: `Core/Services/HttpService.cs` — neue static-Methode `MatchUlmReleaseAssets`
- Test: `ULM.Tests/HttpServiceTests.cs`

**Interfaces:**
- Produces: `internal static (string PortableUrl, string SetupUrl) MatchUlmReleaseAssets(IEnumerable<(string Name, string Url)> assets)`

- [ ] **Step 1: Failing-Test schreiben**

Am Ende von `ULM.Tests/HttpServiceTests.cs` neue Testklasse ergänzen:

```csharp
public class HttpServiceMatchUlmReleaseAssetsTests
{
    [Fact]
    public void MatchUlmReleaseAssets_SeparatesPortableAndSetup()
    {
        var assets = new List<(string, string)>
        {
            ("UniversalLinuxManager-v2.34.0-win-x64.exe",       "https://x/portable.exe"),
            ("UniversalLinuxManager-Setup-v2.34.0-win-x64.exe", "https://x/setup.exe"),
            ("UniversalLinuxManager-v2.34.0-win-x64.zip",       "https://x/ignore.zip"),
        };
        var (portable, setup) = HttpService.MatchUlmReleaseAssets(assets);
        Assert.Equal("https://x/portable.exe", portable);
        Assert.Equal("https://x/setup.exe",    setup);
    }

    [Fact]
    public void MatchUlmReleaseAssets_MissingSetup_LeavesSetupEmpty()
    {
        var assets = new List<(string, string)>
        { ("UniversalLinuxManager-v2.34.0-win-x64.exe", "https://x/portable.exe") };
        var (portable, setup) = HttpService.MatchUlmReleaseAssets(assets);
        Assert.Equal("https://x/portable.exe", portable);
        Assert.Equal("", setup);
    }
}
```

- [ ] **Step 2: Test rot (kompiliert nicht — Methode fehlt)**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj`
Expected: Compile-FEHLER (`MatchUlmReleaseAssets` existiert nicht).

- [ ] **Step 3: Methode implementieren**

In `Core/Services/HttpService.cs` (in der `HttpService`-Klasse, in der Nähe
von `CheckForUlmUpdateAsync`) einfügen:

```csharp
/// <summary>
/// Ordnet die Assets eines GitHub-Releases den beiden ULM-Windows-Downloads zu: portable EXE
/// (…-win-x64.exe ohne "-Setup-") und Installer (…-Setup-…-win-x64.exe). Reine Logik, testbar
/// ohne Netzwerk. Fehlt ein Typ, bleibt dessen URL leer.
/// </summary>
internal static (string PortableUrl, string SetupUrl) MatchUlmReleaseAssets(IEnumerable<(string Name, string Url)> assets)
{
    string portable = string.Empty, setup = string.Empty;
    foreach (var (name, url) in assets)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url)) continue;
        if (!name.EndsWith("-win-x64.exe", StringComparison.OrdinalIgnoreCase)) continue;
        bool isSetup = name.Contains("-Setup-", StringComparison.OrdinalIgnoreCase);
        if (isSetup) { if (string.IsNullOrEmpty(setup))    setup    = url; }
        else         { if (string.IsNullOrEmpty(portable)) portable = url; }
    }
    return (portable, setup);
}
```

- [ ] **Step 4: Test grün**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Core/Services/HttpService.cs ULM.Tests/HttpServiceTests.cs
git commit -m "feat: MatchUlmReleaseAssets ordnet Release-Assets portable/Setup zu"
```

---

### Task C2: `CheckForUlmUpdateAsync` liefert Asset-URLs (`UlmUpdateInfo`)

**Files:**
- Modify: `Core/Services/HttpService.cs:248-268`

**Interfaces:**
- Produces: `public sealed record UlmUpdateInfo(bool HasUpdate, string LatestVersion, string ReleaseUrl, string PortableExeUrl, string SetupExeUrl)` mit `static readonly UlmUpdateInfo None`; `public async Task<UlmUpdateInfo> CheckForUlmUpdateAsync(string currentVersion, string repo = "zwilling10/ULM")`
- Consumes: `MatchUlmReleaseAssets` (C1)

- [ ] **Step 1: Record definieren**

In `Core/Services/HttpService.cs` im Namespace `ULM.Core.Services` (z.B.
direkt über der `HttpService`-Klasse) einfügen:

```csharp
public sealed record UlmUpdateInfo(
    bool HasUpdate, string LatestVersion, string ReleaseUrl,
    string PortableExeUrl, string SetupExeUrl)
{
    public static readonly UlmUpdateInfo None = new(false, string.Empty, string.Empty, string.Empty, string.Empty);
}
```

- [ ] **Step 2: Methode umstellen**

`CheckForUlmUpdateAsync` (`:248-268`) ersetzen:

```csharp
public async Task<UlmUpdateInfo> CheckForUlmUpdateAsync(string currentVersion, string repo = "zwilling10/ULM")
{
    try
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repo}/releases/latest");
        req.Headers.TryAddWithoutValidation("Accept",     "application/vnd.github.v3+json");
        req.Headers.TryAddWithoutValidation("User-Agent", $"ULM/{currentVersion}");
        AddGitHubAuthHeader(req);
        using var cts  = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var resp = await _client.SendAsync(req, cts.Token).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) return UlmUpdateInfo.None;
        string json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        string tag = doc.RootElement.TryGetProperty("tag_name", out var t) ? t.GetString() ?? string.Empty : string.Empty;
        string url = doc.RootElement.TryGetProperty("html_url", out var u) ? u.GetString() ?? string.Empty : string.Empty;
        string latest = tag.TrimStart('v', 'V');
        if (string.IsNullOrWhiteSpace(latest)) return UlmUpdateInfo.None;

        var assetList = new List<(string, string)>();
        if (doc.RootElement.TryGetProperty("assets", out var assets))
            foreach (var a in assets.EnumerateArray())
            {
                string n = a.TryGetProperty("name", out var nn) ? nn.GetString() ?? string.Empty : string.Empty;
                string au = a.TryGetProperty("browser_download_url", out var uu) ? uu.GetString() ?? string.Empty : string.Empty;
                assetList.Add((n, au));
            }
        var (portable, setup) = MatchUlmReleaseAssets(assetList);
        return new UlmUpdateInfo(IsVersionNewer(latest, currentVersion), latest, url, portable, setup);
    }
    catch (Exception ex) { Debug.WriteLine($"[UlmUpdateCheck] {ex.Message}"); return UlmUpdateInfo.None; }
}
```

- [ ] **Step 3: Build erwartet FEHLER im Consumer**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: Compile-FEHLER in `Views/MainWindow.xaml.cs:206` (alte Tupel-Destrukturierung) — wird in C4 behoben.

- [ ] **Step 4: (Commit zusammen mit C4)**

---

### Task C3: ViewModel-Zustand für das Update-Banner

**Files:**
- Modify: `ViewModels/MainViewModel.cs` (Felder/Properties + Methoden)

**Interfaces:**
- Produces: `bool UpdateBannerVisible`, `string UpdateBannerText`, `UlmUpdateInfo? AvailableUpdate`, `void SetAvailableUpdate(UlmUpdateInfo info)`, `void DismissUpdateBanner()`
- Consumes: `UlmUpdateInfo` (C2), `Constants.AppVersion`

- [ ] **Step 1: Felder + Properties + Methoden einfügen**

In `ViewModels/MainViewModel.cs` (bei den übrigen Properties, z.B. nach
`ShowInfoPopup`, `:119`) einfügen:

```csharp
private UlmUpdateInfo? _availableUpdate;
public UlmUpdateInfo? AvailableUpdate => _availableUpdate;
private bool _updateBannerVisible;
public bool UpdateBannerVisible { get => _updateBannerVisible; private set => SetField(ref _updateBannerVisible, value); }
private string _updateBannerText = string.Empty;
public string UpdateBannerText { get => _updateBannerText; private set => SetField(ref _updateBannerText, value); }

// Vom MainWindow nach erfolgreichem Update-Check aufgerufen — macht das Banner sichtbar.
public void SetAvailableUpdate(UlmUpdateInfo info)
{
    _availableUpdate = info;
    UpdateBannerText = $"🆕 Neue Version verfügbar: v{info.LatestVersion} (installiert: v{Constants.AppVersion})";
    UpdateBannerVisible = true;
}
// Blendet das Banner nur für die laufende Sitzung aus (kein persistenter Zustand).
public void DismissUpdateBanner() => UpdateBannerVisible = false;
```

- [ ] **Step 2: Build grün (Consumer C4 fehlt noch, aber VM kompiliert eigenständig — außer C2-Consumer-Fehler in MainWindow bleibt bestehen)**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: weiterhin nur der bekannte Fehler in `MainWindow.xaml.cs:206` (C2) — keine NEUEN Fehler im ViewModel.

- [ ] **Step 3: (Commit zusammen mit C4)**

---

### Task C4: Update-Auswahl-Dialog + Banner-Verdrahtung + Download

**Files:**
- Create: `Views/Dialogs/UpdateDownloadDialog.cs`
- Modify: `Views/MainWindow.xaml` (neue Banner-Row + Row-Index-Verschiebung)
- Modify: `Views/MainWindow.xaml.cs` (`CheckUlmUpdateAsync`, neue Click-Handler, `using System.Diagnostics;`)

**Interfaces:**
- Consumes: `MainViewModel.SetAvailableUpdate`/`AvailableUpdate`/`DismissUpdateBanner` (C3), `UlmUpdateInfo` (C2), `HttpService.DownloadAsync`, `AppPaths.Instance.DownloadDir`
- Produces (Dialog): `UpdateDownloadDialog(UlmUpdateInfo info)` mit `string ChosenUrl`, `bool OpenReleasePageInstead`

- [ ] **Step 1: Dialog anlegen**

Neue Datei `Views/Dialogs/UpdateDownloadDialog.cs`:

```csharp
using System;
using System.Windows;
using System.Windows.Controls;
using ULM.Core.Services;

namespace ULM.Views.Dialogs
{
    // AppRes (Brush/Style-Helfer) liegt bereits im selben Namespace ULM.Views.Dialogs
    // (Views/Dialogs/DownloadDialogs.cs) — kein zusätzliches using nötig.
    // Kleiner Auswahldialog nach Klick auf "Herunterladen …" im Update-Banner: bietet die portable
    // EXE und/oder den Setup-Installer an — je nachdem, welche Assets im Release vorhanden sind.
    // Fehlt beides, bleibt nur "Zur Release-Seite öffnen" (OpenReleasePageInstead = true).
    public sealed class UpdateDownloadDialog : Window
    {
        public string ChosenUrl { get; private set; } = string.Empty;
        public bool   OpenReleasePageInstead { get; private set; }

        public UpdateDownloadDialog(UlmUpdateInfo info)
        {
            Title  = "Programm-Update herunterladen";
            Width  = 460; SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Background = AppRes.Brush("BrushBg");

            var root = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
            root.Children.Add(new TextBlock
            {
                Text = $"Version v{info.LatestVersion} steht bereit.\nWie möchtest du sie beziehen?",
                TextWrapping = TextWrapping.Wrap, FontSize = 12.5,
                Margin = new Thickness(0, 0, 0, 16), Foreground = AppRes.Brush("BrushHeader"),
            });

            bool any = false;
            if (!string.IsNullOrWhiteSpace(info.PortableExeUrl))
            {
                any = true;
                var b = new Button { Content = "⬇ Portable .exe (ohne Installation)", Style = AppRes.Style("BtnPrimary"), Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(10, 8, 10, 8) };
                b.Click += (_, _) => { ChosenUrl = info.PortableExeUrl; DialogResult = true; Close(); };
                root.Children.Add(b);
            }
            if (!string.IsNullOrWhiteSpace(info.SetupExeUrl))
            {
                any = true;
                var b = new Button { Content = "⬇ Setup-Installer (.exe)", Style = AppRes.Style("BtnSuccess"), Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(10, 8, 10, 8) };
                b.Click += (_, _) => { ChosenUrl = info.SetupExeUrl; DialogResult = true; Close(); };
                root.Children.Add(b);
            }
            if (!any)
            {
                var b = new Button { Content = "🌐 Zur Release-Seite öffnen", Style = AppRes.Style("BtnGhost"), Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(10, 8, 10, 8) };
                b.Click += (_, _) => { OpenReleasePageInstead = true; DialogResult = true; Close(); };
                root.Children.Add(b);
            }

            var bCancel = new Button { Content = "Abbrechen", Style = AppRes.Style("BtnGhost"), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
            bCancel.Click += (_, _) => { DialogResult = false; Close(); };
            root.Children.Add(bCancel);

            Content = root;
        }
    }
}
```

- [ ] **Step 2: Banner-Row in die XAML einfügen und nachfolgende Rows verschieben**

In `Views/MainWindow.xaml` in `Grid.RowDefinitions` (`:118-124`) nach der
Header-Row (`Height="64"`) eine neue Auto-Row einfügen:

```xml
<Grid.RowDefinitions>
    <RowDefinition Height="64"/>
    <RowDefinition Height="Auto"/>   <!-- Update-Banner -->
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="*"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="28"/>
</Grid.RowDefinitions>
```

Direkt NACH dem schließenden `</Border>` der Header-Zeile (nach
`MainWindow.xaml:281`) das Banner einfügen:

```xml
<!-- ── Update-Banner (nur sichtbar, wenn eine neuere Programmversion verfügbar ist) ── -->
<Border Grid.Row="1" Background="#1E4620" Padding="14,8"
        Visibility="{Binding UpdateBannerVisible, Converter={StaticResource BoolToVis}}">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="Auto"/>
            <ColumnDefinition Width="Auto"/>
        </Grid.ColumnDefinitions>
        <TextBlock Grid.Column="0" Text="{Binding UpdateBannerText}"
                   Foreground="White" FontWeight="SemiBold" FontSize="12.5"
                   VerticalAlignment="Center"/>
        <Button Grid.Column="1" x:Name="BtnUpdateDownload" Content="⬇ Herunterladen …"
                Style="{DynamicResource BtnSuccess}" Margin="0,0,8,0"
                Click="BtnUpdateDownload_Click"/>
        <Button Grid.Column="2" x:Name="BtnUpdateDismiss" Content="Ausblenden"
                Style="{DynamicResource BtnGhost}" Foreground="White" BorderBrush="#4A6785"
                Click="BtnUpdateDismiss_Click"/>
    </Grid>
</Border>
```

Danach die `Grid.Row`-Werte der vier direkten Kinder des Root-Grids um 1
erhöhen:
- Toolbar-`<Border Grid.Row="1" …>` (`:284`) → `Grid.Row="2"`
- `<TabControl Grid.Row="2" …>` (`:332`) → `Grid.Row="3"`
- Aktions-`<Border Grid.Row="3" …>` (`:518`) → `Grid.Row="4"`
- Statusleiste-`<Border Grid.Row="4" …>` (`:588`) → `Grid.Row="5"`

- [ ] **Step 3: `CheckUlmUpdateAsync` setzt das Banner + Handler ergänzen**

In `Views/MainWindow.xaml.cs` oben `using System.Diagnostics;` ergänzen
(`:2-16`). `CheckUlmUpdateAsync` (`:204-210`) ersetzen:

```csharp
private async Task CheckUlmUpdateAsync()
{
    var info = await HttpService.Instance.CheckForUlmUpdateAsync(Constants.AppVersion).ConfigureAwait(true);
    if (!info.HasUpdate) return;
    AppendLog($"🆕 Neue ULM-Version verfügbar: v{info.LatestVersion} (aktuell installiert: v{Constants.AppVersion})");
    if (!string.IsNullOrWhiteSpace(info.ReleaseUrl)) AppendLog($"   {info.ReleaseUrl}");
    _vm.SetAvailableUpdate(info);
}
```

Und neue Handler (z.B. direkt nach `CheckUlmUpdateAsync`) einfügen:

```csharp
private void BtnUpdateDismiss_Click(object sender, RoutedEventArgs e) => _vm.DismissUpdateBanner();

private async void BtnUpdateDownload_Click(object sender, RoutedEventArgs e)
{
    var info = _vm.AvailableUpdate;
    if (info is null) return;
    var dlg = new UpdateDownloadDialog(info) { Owner = this };
    if (dlg.ShowDialog() != true) return;

    if (dlg.OpenReleasePageInstead)
    {
        try { Process.Start(new ProcessStartInfo(info.ReleaseUrl) { UseShellExecute = true }); }
        catch (Exception ex) { AppendLog($"⚠ Release-Seite konnte nicht geöffnet werden: {ex.Message}"); }
        return;
    }

    string url  = dlg.ChosenUrl;
    string name = Path.GetFileName(new Uri(url).AbsolutePath);
    string dest = Path.Combine(AppPaths.Instance.DownloadDir, name);
    AppendLog($"⬇ Lade Programm-Update: {name} …");
    bool ok;
    try { ok = await HttpService.Instance.DownloadAsync(url, dest, null, System.Threading.CancellationToken.None).ConfigureAwait(true); }
    catch (Exception ex) { AppendLog($"❌ Update-Download fehlgeschlagen: {ex.Message}"); ok = false; }
    if (!ok)
    {
        MessageBox.Show("Der Download des Programm-Updates ist fehlgeschlagen.", Constants.AppTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
    }
    AppendLog($"✅ Update gespeichert: {dest}");
    try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{dest}\"") { UseShellExecute = true }); }
    catch (Exception ex) { AppendLog($"⚠ Ordner konnte nicht geöffnet werden: {ex.Message}"); }
}
```

- [ ] **Step 4: Build grün**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: Build erfolgreich (0 Fehler).

- [ ] **Step 5: Tests grün**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj`
Expected: PASS.

- [ ] **Step 6: Manuell verifizieren**

Programm starten. Ist das aktuelle Release neuer als `Constants.AppVersion`,
erscheint das grüne Banner unter dem Header. Klick auf „Herunterladen …"
zeigt den Auswahldialog (portable / Setup je nach Release), nach Wahl lädt
ULM die Datei in den Download-Ordner und öffnet den Explorer mit markierter
Datei. „Ausblenden" versteckt das Banner. (Falls kein neueres Release
existiert: temporär eine ältere `<Version>` in der `.csproj` setzen, bauen,
prüfen, danach zurücksetzen — NICHT committen.)

- [ ] **Step 7: Commit**

```bash
git add Core/Services/HttpService.cs ViewModels/MainViewModel.cs Views/MainWindow.xaml Views/MainWindow.xaml.cs Views/Dialogs/UpdateDownloadDialog.cs
git commit -m "feat: Update-Banner mit Download-Auswahl (portable/Setup) fuer neue Programmversionen"
```

---

## Abschluss

- [ ] **Graph aktualisieren**

Run: `graphify update .`

- [ ] **Gesamt-Verifikation**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug && dotnet test ULM.Tests/ULM.Tests.csproj`
Expected: Build 0 Fehler, alle Tests grün.
