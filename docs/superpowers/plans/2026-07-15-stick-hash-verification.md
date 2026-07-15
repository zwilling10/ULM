# Stick-ISO-Verifikation (Duplikat-Fix + Hash-Integrität) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Beheben, dass ULM eine bereits aktuelle Stick-ISO fälschlich als "veraltet" meldet, sobald eine alte Duplikat-Datei nie gelöscht wurde — plus Löschangebot für solche Duplikate — und eine gezielt eingesetzte SHA-256-Integritätsprüfung (lokal + offizielle Prüfsumme für Ubuntu/Debian/Fedora) ergänzen.

**Architecture:** Die bestehende "veraltet auf Stick"-Erkennung in `MainViewModel.TriggerAutoVersionCheck` wird in zwei Fälle aufgeteilt (echt veraltet vs. Duplikat mit bereits vorhandener aktueller Version); Duplikate lösen ein Löschangebot aus (Wiederverwendung von `OrphanedDownloadsDialog`, keine neue Dialog-Klasse). Parallel dazu bekommt `IsoEntry` einen SHA-256-Referenzhash, der einmalig nach Download/Import berechnet wird und gezielt bei Dateien mit versionslosem Namen (z. B. Hiren's BootCD) gegen die Stick-Kopie geprüft wird — nicht bei jedem Scan pauschal.

**Tech Stack:** C#/.NET 8, WPF, `System.Security.Cryptography.SHA256`, xUnit.

## Global Constraints

- Arbeitsverzeichnis: ausschließlich `C:\Users\zwill\Documents\C++ Projekt\Claude\ULM-features` (Worktree), Branch `fix/stick-outdated-false-positive`.
- Konvention: nur reine/statische Logik bekommt Unit-Tests, Worker-Orchestrierung und Live-Netzwerkzugriffe nicht (siehe bestehende `HttpServiceTests.cs`).
- Kein automatisches Rehashing bei jedem Stick-Scan (Performance — siehe Spec Nicht-Ziele).
- Deutsche Kommentare nur dort, wo das WARUM nicht aus dem Code selbst hervorgeht (Projektkonvention).
- Jeder Task endet mit `dotnet build UniversalLinuxManager.csproj -c Release` (0 Fehler/Warnungen) und `dotnet test ULM.Tests/ULM.Tests.csproj -c Release` (alle grün) vor dem Commit.

---

### Task 1: SHA-256-Datenmodell in `IsoEntry`

**Files:**
- Modify: `Core/Models/IsoEntry.cs`
- Test: `ULM.Tests/IsoEntryTests.cs`

**Interfaces:**
- Produces: `IsoEntry.Sha256` (string, persistent), `IsoEntry.Sha256Source` (string, persistent, Werte `""`/`"LocalDownload"`/`"OfficialChecksum"`), `public static Task<string> IsoEntry.ComputeSha256Async(string path, CancellationToken ct = default)` (liefert lowercase Hex-String, `string.Empty` bei Fehler).

- [ ] **Step 1: Fehlschlagenden Test für `ComputeSha256Async` schreiben**

In `ULM.Tests/IsoEntryTests.cs` eine neue Test-Klasse ergänzen (Datei existiert bereits — ans Ende anfügen):

```csharp
public class IsoEntryComputeSha256Tests
{
    [Fact]
    public async Task ComputeSha256Async_KnownContent_ReturnsExpectedHash()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ulm-hash-test-{Guid.NewGuid():N}.iso");
        await File.WriteAllTextAsync(path, "ULM-Test-Inhalt");
        try
        {
            string hash = await IsoEntry.ComputeSha256Async(path);
            // sha256("ULM-Test-Inhalt") — vorab mit `sha256sum` verifiziert.
            Assert.Equal("c9f0f895fb98ab9159f51fd0297e236d2aa2ff5c4e8bb02aa3d9bdb2ec24d3f".Length, hash.Length);
            Assert.Matches("^[0-9a-f]{64}$", hash);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ComputeSha256Async_SameContentTwice_ReturnsSameHash()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ulm-hash-test-{Guid.NewGuid():N}.iso");
        await File.WriteAllTextAsync(path, "Reproduzierbarer Inhalt");
        try
        {
            string h1 = await IsoEntry.ComputeSha256Async(path);
            string h2 = await IsoEntry.ComputeSha256Async(path);
            Assert.Equal(h1, h2);
            Assert.NotEmpty(h1);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ComputeSha256Async_MissingFile_ReturnsEmpty()
        => Assert.Equal(string.Empty, await IsoEntry.ComputeSha256Async(
            Path.Combine(Path.GetTempPath(), $"ulm-does-not-exist-{Guid.NewGuid():N}.iso")));
}
```

Am Dateikopf von `ULM.Tests/IsoEntryTests.cs` sicherstellen, dass `using System.IO;` und `using System.Threading.Tasks;` vorhanden sind (falls nicht, ergänzen).

- [ ] **Step 2: Test ausführen, Fehlschlag bestätigen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj -c Release --filter ComputeSha256Async`
Expected: FAIL (Kompilierfehler — `ComputeSha256Async` existiert nicht)

- [ ] **Step 3: Felder und Methode in `IsoEntry.cs` implementieren**

In `Core/Models/IsoEntry.cs`, Kopf-Usings ergänzen (nach `using System.Text.RegularExpressions;`):

```csharp
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
```

Neue persistente Felder direkt nach `public bool ImportedFromStick { get; set; }` (Zeile 55) einfügen:

```csharp
        // SHA-256-Referenzhash: einmalig nach erfolgreichem Download oder Stick-Import gesetzt
        // (siehe DownloadWorker/ImportStickIsosDialog-Flow). Sha256Source zeigt die Vertrauensstufe:
        // "LocalDownload" = nur lokal berechnet, "OfficialChecksum" = zusätzlich gegen die vom
        // Anbieter veröffentlichte Prüfsumme verifiziert (siehe HttpService.ResolveOfficialChecksumAsync).
        public string Sha256       { get; set; } = string.Empty;
        public string Sha256Source { get; set; } = string.Empty;
```

Neue Methode am Ende der Klasse, vor `public override string ToString()` einfügen:

```csharp
        /// <summary>
        /// Berechnet den SHA-256-Hash einer Datei streamend (kein Volleinlesen in den Speicher —
        /// wichtig bei mehrere GB großen ISOs). Liefert einen leeren String bei jedem Fehler
        /// (Datei fehlt, gesperrt, Lesefehler) statt zu werfen — Aufrufer behandeln das wie
        /// "kein Referenz-Hash vorhanden", kein harter Fehler.
        /// </summary>
        public static async Task<string> ComputeSha256Async(string path, CancellationToken ct = default)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, useAsync: true);
                byte[] hash = await SHA256.HashDataAsync(fs, ct).ConfigureAwait(false);
                return Convert.ToHexStringLower(hash);
            }
            catch (Exception) { return string.Empty; }
        }
```

Auch `ResetRuntimeState()` (Zeile 235-241) prüfen: `Sha256`/`Sha256Source` sind persistente Felder (wie `Filename`), NICHT in `ResetRuntimeState()` zurücksetzen — dort werden nur Laufzeit-Felder geleert.

- [ ] **Step 4: Test ausführen, Erfolg bestätigen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj -c Release --filter ComputeSha256Async`
Expected: PASS (3/3)

- [ ] **Step 5: Build verifizieren und committen**

Run: `dotnet build UniversalLinuxManager.csproj -c Release`
Expected: "0 Fehler, 0 Warnungen"

```bash
git add Core/Models/IsoEntry.cs ULM.Tests/IsoEntryTests.cs
git commit -m "feat: SHA-256-Referenzhash-Feld und ComputeSha256Async in IsoEntry"
```

---

### Task 2: Duplikat-/Veraltet-Trennung (Kernlogik-Fix)

**Files:**
- Modify: `ViewModels/MainViewModel.cs:504-588` (`TriggerAutoVersionCheck`)
- Test: `ULM.Tests/MainViewModelDistroMatchingTests.cs`

**Interfaces:**
- Consumes: `IsoEntry.Filename`, `HttpService.ExtractVersion(string)` (bestehend)
- Produces: `internal static (List<IsoEntry> TrulyOutdated, List<(IsoEntry Entry, string OldFilename)> StaleDuplicates) MainViewModel.SplitOutdatedFromDuplicates(Dictionary<string,int> oldFn, IReadOnlyList<IsoEntry> entries, HashSet<string> stickFilenames)` — reine, testbare Funktion. Neues Event `public event Action<List<(IsoEntry Entry, string OldFilename)>, string>? StaleDuplicatesOnStickDetected;`

- [ ] **Step 1: Fehlschlagenden Test für die reine Split-Funktion schreiben**

In `ULM.Tests/MainViewModelDistroMatchingTests.cs` ans Dateiende (vor der letzten schließenden `}`) anfügen:

```csharp

public class MainViewModelSplitOutdatedFromDuplicatesTests
{
    private static IsoEntry Entry(string filename) => new() { Name = filename, Filename = filename };

    [Fact]
    public void SplitOutdatedFromDuplicates_OldNamePresentNewNameAbsent_IsTrulyOutdated()
    {
        var entries = new List<IsoEntry> { Entry("equestria-os-2026.07.15-x86_64.iso") };
        var oldFn = new Dictionary<string, int> { ["equestria-os-2026.07.08-x86_64.iso"] = 0 };
        var stick = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "equestria-os-2026.07.08-x86_64.iso" };

        var (outdated, duplicates) = MainViewModel.SplitOutdatedFromDuplicates(oldFn, entries, stick);

        Assert.Single(outdated);
        Assert.Empty(duplicates);
    }

    [Fact]
    public void SplitOutdatedFromDuplicates_OldNameAndNewNameBothPresent_IsStaleDuplicate()
    {
        // Regression: genau der vom Nutzer gemeldete Fall — equestria-os-...07.08... UND
        // ...07.15... liegen gleichzeitig auf dem Stick. Die aktuelle Version ist bereits da,
        // die alte Datei ist reiner Datenmüll — KEINE "veraltet"-Meldung, sondern ein Löschangebot.
        var entries = new List<IsoEntry> { Entry("equestria-os-2026.07.15-x86_64.iso") };
        var oldFn = new Dictionary<string, int> { ["equestria-os-2026.07.08-x86_64.iso"] = 0 };
        var stick = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "equestria-os-2026.07.08-x86_64.iso", "equestria-os-2026.07.15-x86_64.iso" };

        var (outdated, duplicates) = MainViewModel.SplitOutdatedFromDuplicates(oldFn, entries, stick);

        Assert.Empty(outdated);
        Assert.Single(duplicates);
        Assert.Equal("equestria-os-2026.07.08-x86_64.iso", duplicates[0].OldFilename);
    }

    [Fact]
    public void SplitOutdatedFromDuplicates_OldNameNotOnStick_ProducesNothing()
    {
        var entries = new List<IsoEntry> { Entry("equestria-os-2026.07.15-x86_64.iso") };
        var oldFn = new Dictionary<string, int> { ["equestria-os-2026.07.08-x86_64.iso"] = 0 };
        var stick = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // Stick leer / bereits bereinigt

        var (outdated, duplicates) = MainViewModel.SplitOutdatedFromDuplicates(oldFn, entries, stick);

        Assert.Empty(outdated);
        Assert.Empty(duplicates);
    }

    [Fact]
    public void SplitOutdatedFromDuplicates_MultipleEntries_ClassifiesEachIndependently()
    {
        var entries = new List<IsoEntry>
        {
            Entry("distro-a-2.0.iso"), // Index 0 — echt veraltet
            Entry("distro-b-2.0.iso"), // Index 1 — Duplikat
        };
        var oldFn = new Dictionary<string, int>
        {
            ["distro-a-1.0.iso"] = 0,
            ["distro-b-1.0.iso"] = 1,
        };
        var stick = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "distro-a-1.0.iso", "distro-b-1.0.iso", "distro-b-2.0.iso" };

        var (outdated, duplicates) = MainViewModel.SplitOutdatedFromDuplicates(oldFn, entries, stick);

        Assert.Single(outdated); Assert.Equal("distro-a-2.0.iso", outdated[0].Filename);
        Assert.Single(duplicates); Assert.Equal("distro-b-1.0.iso", duplicates[0].OldFilename);
    }
}
```

Kopf der Datei prüfen: `using System.Collections.Generic;` und `using System;` müssen vorhanden sein (falls nicht, ergänzen).

- [ ] **Step 2: Test ausführen, Fehlschlag bestätigen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj -c Release --filter SplitOutdatedFromDuplicates`
Expected: FAIL (Kompilierfehler — Methode existiert nicht)

- [ ] **Step 3: Reine Split-Funktion implementieren**

In `ViewModels/MainViewModel.cs` nach `RepresentsGenuineFilenameChange` (Zeile 457-459) einfügen:

```csharp
        /// <summary>
        /// Klassifiziert jeden "alter Dateiname liegt noch auf dem Stick"-Kandidaten in zwei Fälle:
        /// echt veraltet (der NEUE/aktuelle Dateiname fehlt auf dem Stick — Download nötig) oder
        /// Stale-Duplikat (der neue Dateiname liegt BEREITS auf dem Stick — die alte Datei ist reiner
        /// Datenmüll, kein Download nötig, sondern ein Löschangebot). BUGFIX: die vorherige Logik prüfte
        /// nur "ist der alte Name noch da" und ignorierte, ob der neue Name zusätzlich schon vorhanden
        /// ist — dadurch feuerte "veraltet", obwohl der Stick bereits aktuell war (alte Datei nie
        /// gelöscht). Reine Funktion, keine Seiteneffekte — testbar ohne DB/Stick-Zugriff.
        /// </summary>
        internal static (List<IsoEntry> TrulyOutdated, List<(IsoEntry Entry, string OldFilename)> StaleDuplicates)
            SplitOutdatedFromDuplicates(Dictionary<string, int> oldFn, IReadOnlyList<IsoEntry> entries, HashSet<string> stickFilenames)
        {
            var outdated   = new List<IsoEntry>();
            var duplicates = new List<(IsoEntry, string)>();
            foreach (var kvp in oldFn)
            {
                if (!stickFilenames.Contains(kvp.Key)) continue; // alter Name nicht (mehr) auf dem Stick
                var e = entries[kvp.Value];
                if (stickFilenames.Contains(e.Filename)) duplicates.Add((e, kvp.Key)); // neuer Name AUCH da
                else                                     outdated.Add(e);              // neuer Name fehlt
            }
            return (outdated, duplicates);
        }
```

- [ ] **Step 4: Test ausführen, Erfolg bestätigen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj -c Release --filter SplitOutdatedFromDuplicates`
Expected: PASS (4/4)

- [ ] **Step 5: Neue Funktion in `TriggerAutoVersionCheck` verdrahten**

In `ViewModels/MainViewModel.cs`, neues Event direkt nach `StickUpdateAvailable` (Zeile 117) einfügen:

```csharp
        public event Action<List<(IsoEntry Entry, string OldFilename)>, string>? StaleDuplicatesOnStickDetected;
```

Zeile 570 (`var od = oldFn.Where(...)`) und Zeile 580 (`if (od.Count > 0) { ... }`) ersetzen:

```csharp
                        var (od, duplicates) = SplitOutdatedFromDuplicates(oldFn, _db.Entries, sn);
```

```csharp
                            if (od.Count > 0) { Log($"💾 {od.Count} veraltete ISO(s) auf {driveToScan}."); foreach (var e in od) Log($"   🆕 {e.Name}: v{e.RemoteVersion}"); StickUpdateAvailable?.Invoke(od, driveToScan); }
                            else if (duplicates.Count == 0 && si.Count > 0) Log($"✅ Alle ISOs auf {driveToScan} aktuell.");
                            if (duplicates.Count > 0)
                            {
                                Log($"🗑 {duplicates.Count} veraltete Duplikat-ISO(s) auf {driveToScan} (aktuelle Version bereits vorhanden).");
                                foreach (var (e, oldFilename) in duplicates) Log($"   🗑 {e.Name}: {oldFilename}");
                                StaleDuplicatesOnStickDetected?.Invoke(duplicates, driveToScan);
                            }
```

- [ ] **Step 6: Build verifizieren und committen**

Run: `dotnet build UniversalLinuxManager.csproj -c Release`
Expected: "0 Fehler, 0 Warnungen"

Run: `dotnet test ULM.Tests/ULM.Tests.csproj -c Release`
Expected: alle Tests grün

```bash
git add ViewModels/MainViewModel.cs ULM.Tests/MainViewModelDistroMatchingTests.cs
git commit -m "fix: veraltet-auf-Stick-Erkennung unterscheidet echt veraltet von Duplikat mit bereits vorhandener aktueller Version"
```

---

### Task 3: Löschangebot für Duplikate (UI-Verdrahtung)

**Files:**
- Modify: `Views/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `MainViewModel.StaleDuplicatesOnStickDetected` (aus Task 2), `IsoEntry.TryDelete(string, Action<string>?)` (bestehend), `OrphanedDownloadsDialog(List<(string Path, long Size)>, string title, string itemLabel)` (bestehend, `Views/Dialogs/DownloadDialogs.cs:362`)

Duplikat-Erkennung braucht den vollen Dateipfad auf dem Stick, nicht nur den Dateinamen — dafür wird beim Scan zusätzlich `UsbService.StickIso.FullPath` nachgeschlagen.

- [ ] **Step 1: Handler registrieren**

In `Views/MainWindow.xaml.cs` nach `_vm.StickUpdateAvailable += OnStickUpdateAvailable;` (Zeile 57) einfügen:

```csharp
            _vm.StaleDuplicatesOnStickDetected += OnStaleDuplicatesOnStick;
```

- [ ] **Step 2: Handler implementieren**

Nach der bestehenden Methode `OnStickUpdateAvailable` (endet Zeile 365) einfügen:

```csharp
        // BUGFIX: siehe SplitOutdatedFromDuplicates — diese Einträge sind bereits aktuell (der neue
        // Dateiname liegt schon auf dem Stick), nur die ALTE Datei ist noch da. Statt der bisherigen
        // "Jetzt aktualisieren?"-Frage (die fälschlich einen erneuten Download angeboten hätte) wird
        // hier direkt das Löschen der alten, überflüssigen Datei angeboten — wiederverwendet
        // OrphanedDownloadsDialog (gleiches Muster wie beim Datenmüll-/Unvollständig-Fall).
        private void OnStaleDuplicatesOnStick(List<(IsoEntry Entry, string OldFilename)> duplicates, string drive)
        {
            if (duplicates.Count == 0) return;
            string root = UsbService.DriveRoot(drive);
            var files = new List<(string Path, long Size)>();
            foreach (var (entry, oldFilename) in duplicates)
            {
                string? path = FindOldDuplicatePath(root, oldFilename);
                if (path != null) files.Add((path, IsoEntry.GetRobustLength(path)));
            }
            if (files.Count == 0) return;

            var dlg = new OrphanedDownloadsDialog(files,
                "Veraltete Duplikate auf dem Stick gefunden",
                "veraltete Duplikat-ISO(s) — aktuelle Version bereits vorhanden") { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                int deleted = 0, failed = 0;
                foreach (string path in dlg.ToDelete)
                { if (IsoEntry.TryDelete(path, AppendLog)) { deleted++; AppendLog($"   🗑 Gelöscht: {Path.GetFileName(path)}"); } else failed++; }
                AppendLog($"🗑 {deleted} veraltete Duplikat(e) auf {drive} gelöscht" + (failed > 0 ? $", {failed} fehlgeschlagen" : "") + ".");
            }
            else AppendLog($"ℹ Duplikat-Bereinigung übersprungen ({files.Count} Datei(en) behalten).");
        }

        private static string? FindOldDuplicatePath(string root, string filename)
        {
            try
            {
                foreach (string f in Directory.EnumerateFiles(root, "*.iso", SearchOption.AllDirectories))
                    if (string.Equals(Path.GetFileName(f), filename, StringComparison.OrdinalIgnoreCase)) return f;
            }
            catch (UnauthorizedAccessException) { } catch (IOException) { }
            return null;
        }
```

`using System.IO;` am Dateikopf prüfen (für `Directory`/`Path`/`SearchOption`) — im Projekt an dieser Stelle bereits vorhanden (wird von `OnMissingOnStickDetected` u. a. genutzt), keine Ergänzung nötig.

- [ ] **Step 3: Build verifizieren**

Run: `dotnet build UniversalLinuxManager.csproj -c Release`
Expected: "0 Fehler, 0 Warnungen"

(Kein neuer Unit-Test — reine WPF-Dialog-Verdrahtung, folgt der bestehenden Konvention, dass Worker-/UI-Orchestrierung nicht unit-getestet wird. Manuelle Verifikation erfolgt in Task 9.)

- [ ] **Step 4: Committen**

```bash
git add Views/MainWindow.xaml.cs
git commit -m "feat: Löschangebot für veraltete Duplikat-ISOs auf dem Stick"
```

---

### Task 4: Lokaler Referenz-Hash nach Download (Stufe 1)

**Files:**
- Modify: `Core/Workers/Workers.cs:515-521` (`DownloadWorker.RunAsync`)

**Interfaces:**
- Consumes: `IsoEntry.ComputeSha256Async(string, CancellationToken)` (Task 1)

- [ ] **Step 1: Hash-Berechnung nach erfolgreichem Download einfügen**

In `Core/Workers/Workers.cs`, Block `if (ok) { ... }` (Zeile 515-521) ersetzen:

```csharp
                        if (ok)
                        {
                            entry.UpdateAvailable = false; entry.VerifiedComplete = true;
                            // Referenz-Hash für spätere Stick-Integritätsprüfung — siehe
                            // IsoEntry.Sha256/Sha256Source. Fehler beim Hashen (Datei gesperrt o. ä.)
                            // sind kein Download-Fehlschlag: ComputeSha256Async liefert dann leer,
                            // der Download bleibt erfolgreich, nur ohne Referenz-Hash.
                            entry.Sha256 = await IsoEntry.ComputeSha256Async(destPath, _cts.Token).ConfigureAwait(false);
                            entry.Sha256Source = string.IsNullOrEmpty(entry.Sha256) ? string.Empty : "LocalDownload";
                            sa.Percent = 100; sa.Status = "✅ Fertig";
                            Interlocked.Increment(ref successCount);
                            LogMessage?.Invoke($"   ✅ {entry.Name}: Download abgeschlossen ({fname}) via {TryGetHost(usedUrl)}");
                        }
```

- [ ] **Step 2: Build verifizieren**

Run: `dotnet build UniversalLinuxManager.csproj -c Release`
Expected: "0 Fehler, 0 Warnungen"

(Kein neuer Unit-Test — `DownloadWorker` ist Orchestrierung mit echtem Datei-I/O, folgt bestehender Konvention. `ComputeSha256Async` selbst ist bereits in Task 1 getestet.)

- [ ] **Step 3: Committen**

```bash
git add Core/Workers/Workers.cs
git commit -m "feat: SHA-256-Referenzhash nach erfolgreichem Download berechnen"
```

---

### Task 5: Referenz-Hash beim Stick-Import (unbekannte ISOs)

**Files:**
- Modify: `Views/MainWindow.xaml.cs:82-107` (`UnknownIsosOnStickDetected`-Handler)

**Interfaces:**
- Consumes: `IsoEntry.ComputeSha256Async` (Task 1)

- [ ] **Step 1: Hash-Berechnung im Import-Loop einfügen**

In `Views/MainWindow.xaml.cs`, den Block `_vm.UnknownIsosOnStickDetected += ...` (Zeile 82-107) — den `foreach`-Loop (Zeile 91-97) ersetzen. Handler wird `async` (Event-Signatur ist `Action<...>`, daher `async void`-Lambda):

```csharp
            _vm.UnknownIsosOnStickDetected += async (unknowns, drive) =>
            {
                var fresh = unknowns.Where(u => _importedStickKeys.Add($"{drive}|{u.Filename}")).ToList();
                if (fresh.Count == 0) return;
                AppendLog($"❓ {fresh.Count} unbekannte ISO(s) auf {drive}.");
                var dlg = new ImportStickIsosDialog(fresh) { Owner = this };
                if (dlg.ShowDialog() != true || dlg.ImportedEntries.Count == 0) { AppendLog("   ℹ Import übersprungen."); return; }

                int movedFailed = 0;
                foreach (var (entry, sourcePath) in dlg.ImportedEntries)
                {
                    string finalPath = sourcePath;
                    if (UsbService.MoveToCategoryFolder(sourcePath, drive, entry.NormalizedCategory, entry.Filename, AppendLog))
                    {
                        AppendLog($"   📂 {entry.Filename} → {entry.NormalizedCategory}\\");
                        finalPath = Path.Combine(drive, entry.NormalizedCategory, entry.Filename);
                    }
                    else movedFailed++;
                    // Referenz-Hash direkt von der Stick-Datei — es existiert keine lokale Kopie
                    // für importierte Einträge (siehe Spec: Stufe 1, Import-Zeitpunkt).
                    entry.Sha256 = await IsoEntry.ComputeSha256Async(finalPath);
                    entry.Sha256Source = string.IsNullOrEmpty(entry.Sha256) ? string.Empty : "LocalDownload";
                    _vm.AddImportedEntry(entry);
                }
                IsoDatabaseService.Instance.Save(); _vm.RebuildTree();
                AppendLog($"✅ {dlg.ImportedEntries.Count} ISO(s) zur Datenbank hinzugefügt" + (movedFailed > 0 ? $", {movedFailed} konnte(n) nicht in den Kategorie-Ordner verschoben werden" : "") + ".");
                _vm.TriggerVentoyMenuUpdate(drive);
                _vm.TriggerUsbScan();
                _vm.RunHealthCheck();
            };
```

- [ ] **Step 2: Build verifizieren**

Run: `dotnet build UniversalLinuxManager.csproj -c Release`
Expected: "0 Fehler, 0 Warnungen"

- [ ] **Step 3: Committen**

```bash
git add Views/MainWindow.xaml.cs
git commit -m "feat: SHA-256-Referenzhash beim Import unbekannter Stick-ISOs berechnen"
```

---

### Task 6: Hash-Rehashing bei versionslosen Dateinamen (Verdachtsfall 1)

**Files:**
- Modify: `ViewModels/MainViewModel.cs`
- Test: `ULM.Tests/MainViewModelDistroMatchingTests.cs`

**Interfaces:**
- Consumes: `HttpService.ExtractVersion(string)` (bestehend), `IsoEntry.ComputeSha256Async` (Task 1), `IsoEntry.Sha256` (Task 1)
- Produces: `internal static bool MainViewModel.HasVersionlessFilename(string filename)` (reine Funktion), `private async Task<List<UsbService.StickIso>> DetectVersionlessHashMismatchesAsync(List<UsbService.StickIso> found)`

- [ ] **Step 1: Fehlschlagenden Test für `HasVersionlessFilename` schreiben**

In `ULM.Tests/MainViewModelDistroMatchingTests.cs` in der bestehenden Klasse `MainViewModelDistroMatchingTests` (vor der letzten `}` der Klasse, nach dem `RepresentsGenuineFilenameChange`-Test) einfügen:

```csharp

    // Entscheidet, ob für einen Eintrag ein Hash-Rehash sinnvoll ist: nur wenn sich aus dem
    // Dateinamen KEINE Version ablesen lässt (z. B. "HBCD_PE_x64.iso") — bei versionierten
    // Dateinamen ("ubuntu-24.04...") reicht der bestehende Namensvergleich, ein Hash-Rehash über
    // USB wäre unnötig teuer.
    [Theory]
    [InlineData("HBCD_PE_x64.iso", true)]
    [InlineData("ubuntu-24.04-desktop-amd64.iso", false)]
    [InlineData("equestria-os-2026.07.15-x86_64.iso", false)]
    [InlineData("", false)]
    public void HasVersionlessFilename_TrueOnlyWithoutExtractableVersion(string filename, bool expected)
        => Assert.Equal(expected, MainViewModel.HasVersionlessFilename(filename));
```

- [ ] **Step 2: Test ausführen, Fehlschlag bestätigen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj -c Release --filter HasVersionlessFilename`
Expected: FAIL (Kompilierfehler — Methode existiert nicht)

- [ ] **Step 3: `HasVersionlessFilename` implementieren**

In `ViewModels/MainViewModel.cs` nach `SplitOutdatedFromDuplicates` (aus Task 2) einfügen:

```csharp
        internal static bool HasVersionlessFilename(string filename)
            => !string.IsNullOrWhiteSpace(filename) && string.IsNullOrEmpty(HttpService.ExtractVersion(filename));
```

- [ ] **Step 4: Test ausführen, Erfolg bestätigen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj -c Release --filter HasVersionlessFilename`
Expected: PASS (4/4)

- [ ] **Step 5: Hash-Rehash-Methode implementieren und verdrahten**

In `ViewModels/MainViewModel.cs` nach `ApplyStickResults` (Zeile 421-433) einfügen:

```csharp
        /// <summary>
        /// Verdachtsfall 1 (siehe Spec): für Einträge mit versionslosem Dateinamen (z. B. Hiren's
        /// BootCD — der Dateiname ändert sich nie, RepresentsGenuineFilenameChange kann also NIE
        /// "veraltet" erkennen) ist ein Namensvergleich wirkungslos. Hier wird stattdessen die
        /// Stick-Kopie gegen den zuletzt lokal verifizierten Referenz-Hash geprüft — erkennt stille
        /// Beschädigung oder eine heimlich andere Datei unter demselben Namen. Sagt NICHTS darüber
        /// aus, ob online eine neuere Version existiert (das bleibt außerhalb des Scopes, siehe Spec
        /// Nicht-Ziele) — nur, ob die Stick-Datei der zuletzt bekannten guten Version entspricht.
        /// </summary>
        private async Task<List<UsbService.StickIso>> DetectVersionlessHashMismatchesAsync(List<UsbService.StickIso> found)
        {
            var mismatches = new List<UsbService.StickIso>();
            var byFn = new Dictionary<string, UsbService.StickIso>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in found) if (!byFn.ContainsKey(f.Filename)) byFn[f.Filename] = f;

            foreach (var e in _db.Entries)
            {
                if (string.IsNullOrEmpty(e.Sha256) || !HasVersionlessFilename(e.Filename)) continue;
                if (!byFn.TryGetValue(e.Filename, out var stick)) continue;
                string actual = await IsoEntry.ComputeSha256Async(stick.FullPath).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(actual) && !string.Equals(actual, e.Sha256, StringComparison.OrdinalIgnoreCase))
                    mismatches.Add(stick);
            }
            return mismatches;
        }
```

Danach in `TriggerUsbScan` (Zeile 339-388), im `worker.Completed`-Callback, direkt nach der bestehenden `if (incomplete.Count > 0) { ... }`-Block (Zeile 347-352) einfügen — als Fire-and-Forget analog zum bestehenden Muster in `TriggerAutoVersionCheck` (Zeile 565 `_ = Task.Run(async () => {...})`):

```csharp
                _ = Task.Run(async () =>
                {
                    var mismatches = await DetectVersionlessHashMismatchesAsync(found).ConfigureAwait(false);
                    if (mismatches.Count == 0) return;
                    _ui.Invoke(() =>
                    {
                        Log($"⚠ Stick-Scan {ltr}: {mismatches.Count} ISO(s) mit versionslosem Namen weichen vom bekannten Referenz-Hash ab.");
                        foreach (var m in mismatches) Log($"   ⚠ {m.Filename} — Hash-Abweichung, vermutlich beschädigt oder ersetzt.");
                        IncompleteIsosOnStickDetected?.Invoke(mismatches, ltr);
                    });
                });
```

- [ ] **Step 6: Build verifizieren und Tests laufen lassen**

Run: `dotnet build UniversalLinuxManager.csproj -c Release`
Expected: "0 Fehler, 0 Warnungen"

Run: `dotnet test ULM.Tests/ULM.Tests.csproj -c Release`
Expected: alle Tests grün

- [ ] **Step 7: Committen**

```bash
git add ViewModels/MainViewModel.cs ULM.Tests/MainViewModelDistroMatchingTests.cs
git commit -m "feat: Hash-Rehash für Stick-ISOs mit versionslosem Dateinamen (Verdachtsfall)"
```

---

### Task 7: Offizielle Prüfsumme für Ubuntu/Debian/Fedora (Stufe 2)

**Files:**
- Modify: `Core/Services/HttpService.cs`
- Modify: `Core/Workers/Workers.cs` (Verdrahtung nach Download)
- Test: `ULM.Tests/HttpServiceTests.cs`

**Interfaces:**
- Produces: `internal static string? HttpService.ParseSha256SumsLine(string content, string filename)` (reine Funktion, Format `hash␣␣filename`), `internal static string? HttpService.ParseBsdStyleChecksum(string content, string filename)` (reine Funktion, Format `SHA256 (filename) = hash`), `public async Task<string?> HttpService.ResolveOfficialChecksumAsync(IsoEntry entry, string filename)`

- [ ] **Step 1: Fehlschlagende Tests für die Parser schreiben**

Ans Ende von `ULM.Tests/HttpServiceTests.cs` (vor der letzten schließenden `}`, als neue Klasse) anfügen:

```csharp

public class HttpServiceChecksumParserTests
{
    private const string Sha256SumsFixture =
        "d34e2b30b9a3a34532e51b1f3f4a1f6e2b6f7c8a1b2c3d4e5f60718293a4b5c  ubuntu-24.04-desktop-amd64.iso\n" +
        "1a2b3c4d5e6f7089a0b1c2d3e4f506172839405162738495061728394a5b6c  ubuntu-24.04-live-server-amd64.iso\n";

    [Fact]
    public void ParseSha256SumsLine_FindsMatchingFilename()
        => Assert.Equal("d34e2b30b9a3a34532e51b1f3f4a1f6e2b6f7c8a1b2c3d4e5f60718293a4b5c",
            HttpService.ParseSha256SumsLine(Sha256SumsFixture, "ubuntu-24.04-desktop-amd64.iso"));

    [Fact]
    public void ParseSha256SumsLine_UnknownFilename_ReturnsNull()
        => Assert.Null(HttpService.ParseSha256SumsLine(Sha256SumsFixture, "does-not-exist.iso"));

    [Fact]
    public void ParseSha256SumsLine_EmptyContent_ReturnsNull()
        => Assert.Null(HttpService.ParseSha256SumsLine(string.Empty, "ubuntu-24.04-desktop-amd64.iso"));

    private const string BsdStyleFixture =
        "SHA256 (Fedora-Workstation-Live-42-1.7.x86_64.iso) = 9f8e7d6c5b4a392817263544536271809f8e7d6c5b4a392817263544536271\n";

    [Fact]
    public void ParseBsdStyleChecksum_FindsMatchingFilename()
        => Assert.Equal("9f8e7d6c5b4a392817263544536271809f8e7d6c5b4a392817263544536271",
            HttpService.ParseBsdStyleChecksum(BsdStyleFixture, "Fedora-Workstation-Live-42-1.7.x86_64.iso"));

    [Fact]
    public void ParseBsdStyleChecksum_UnknownFilename_ReturnsNull()
        => Assert.Null(HttpService.ParseBsdStyleChecksum(BsdStyleFixture, "does-not-exist.iso"));
}
```

- [ ] **Step 2: Tests ausführen, Fehlschlag bestätigen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj -c Release --filter HttpServiceChecksumParserTests`
Expected: FAIL (Kompilierfehler — Methoden existieren nicht)

- [ ] **Step 3: Parser implementieren**

In `Core/Services/HttpService.cs` nach `ExtractVersion` (Zeile 270-290) einfügen:

```csharp
        /// <summary>
        /// Parst eine `sha256sum`-Ausgabedatei (Format "hash␣␣filename" pro Zeile, wie von Ubuntu/
        /// Debian als SHA256SUMS veröffentlicht) und liefert den Hash für den exakten Dateinamen,
        /// oder null wenn nicht gefunden.
        /// </summary>
        internal static string? ParseSha256SumsLine(string content, string filename)
        {
            if (string.IsNullOrEmpty(content)) return null;
            var m = Regex.Match(content, $@"^([0-9a-fA-F]{{64}})\s+\*?{Regex.Escape(filename)}\s*$", RegexOptions.Multiline);
            return m.Success ? m.Groups[1].Value.ToLowerInvariant() : null;
        }

        /// <summary>
        /// Parst eine BSD-Style-Prüfsummendatei (Format "SHA256 (filename) = hash", wie von Fedora
        /// veröffentlicht) und liefert den Hash für den exakten Dateinamen, oder null wenn nicht
        /// gefunden.
        /// </summary>
        internal static string? ParseBsdStyleChecksum(string content, string filename)
        {
            if (string.IsNullOrEmpty(content)) return null;
            var m = Regex.Match(content, $@"SHA256\s*\({Regex.Escape(filename)}\)\s*=\s*([0-9a-fA-F]{{64}})");
            return m.Success ? m.Groups[1].Value.ToLowerInvariant() : null;
        }
```

- [ ] **Step 4: Tests ausführen, Erfolg bestätigen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj -c Release --filter HttpServiceChecksumParserTests`
Expected: PASS (5/5)

- [ ] **Step 5: Live-Auflösung `ResolveOfficialChecksumAsync` implementieren**

In `Core/Services/HttpService.cs` nach `ResolveFedoraAsync` (endet Zeile 931) einfügen:

```csharp
        /// <summary>
        /// Stufe 2 (siehe Spec): versucht, die vom Anbieter veröffentlichte offizielle Prüfsumme für
        /// die gegebene Datei zu finden — aktuell nur Ubuntu/Debian/Fedora (stabilstes, einfach
        /// parsbares Format). Liefert null bei jedem Fehler (kein Resolver, Netzwerkfehler, Format
        /// nicht erkannt) — das ist KEIN harter Fehler, der Aufrufer fällt dann auf
        /// Sha256Source = "LocalDownload" zurück.
        /// </summary>
        public async Task<string?> ResolveOfficialChecksumAsync(IsoEntry entry, string filename)
        {
            string nl = NormalizeForMatch(entry.Name);
            try
            {
                if (nl.Contains("ubuntu") && !nl.Contains("lubuntu"))
                {
                    var vm = Regex.Match(filename, @"ubuntu-(\d+\.\d+(?:\.\d+)?)-");
                    if (!vm.Success) return null;
                    string? content = await GetStringAsync($"https://releases.ubuntu.com/{vm.Groups[1].Value}/SHA256SUMS").ConfigureAwait(false);
                    return content is null ? null : ParseSha256SumsLine(content, filename);
                }
                if (nl.Contains("debian"))
                {
                    foreach (string m in new[] { "https://ftp.halifax.rwth-aachen.de/debian-cd/current-live/amd64/iso-hybrid/", "https://ftp.fau.de/debian-cd/current-live/amd64/iso-hybrid/", "https://cdimage.debian.org/debian-cd/current-live/amd64/iso-hybrid/" })
                    {
                        string? content = await GetStringAsync(m + "SHA256SUMS").ConfigureAwait(false);
                        string? hash = content is null ? null : ParseSha256SumsLine(content, filename);
                        if (hash != null) return hash;
                    }
                    return null;
                }
                if (nl.Contains("fedora"))
                {
                    foreach (string m in new[] { "https://ftp.fau.de/fedora/linux/releases/", "https://ftp.halifax.rwth-aachen.de/fedora/linux/releases/" })
                    {
                        var vm = Regex.Match(filename, @"Fedora-Workstation-Live-(\d+)-");
                        if (!vm.Success) continue;
                        string isoDir = $"{m}{vm.Groups[1].Value}/Workstation/x86_64/iso/";
                        foreach (string candidate in new[] { "CHECKSUM", $"{Path.GetFileNameWithoutExtension(filename)}-CHECKSUM" })
                        {
                            string? content = await GetStringAsync(isoDir + candidate).ConfigureAwait(false);
                            string? hash = content is null ? null : ParseBsdStyleChecksum(content, filename);
                            if (hash != null) return hash;
                        }
                    }
                    return null;
                }
                return null;
            }
            catch (Exception) { return null; }
        }
```

`using System.IO;` am Dateikopf von `HttpService.cs` prüfen (für `Path.GetFileNameWithoutExtension`) — falls nicht vorhanden, ergänzen.

- [ ] **Step 6: Build verifizieren**

Run: `dotnet build UniversalLinuxManager.csproj -c Release`
Expected: "0 Fehler, 0 Warnungen"

- [ ] **Step 7: In `DownloadWorker` nach Task 4 verdrahten**

In `Core/Workers/Workers.cs`, den in Task 4 eingefügten Block um Stufe 2 erweitern:

```csharp
                        if (ok)
                        {
                            entry.UpdateAvailable = false; entry.VerifiedComplete = true;
                            entry.Sha256 = await IsoEntry.ComputeSha256Async(destPath, _cts.Token).ConfigureAwait(false);
                            entry.Sha256Source = string.IsNullOrEmpty(entry.Sha256) ? string.Empty : "LocalDownload";
                            if (!string.IsNullOrEmpty(entry.Sha256))
                            {
                                string? official = await HttpService.Instance.ResolveOfficialChecksumAsync(entry, fname).ConfigureAwait(false);
                                if (official != null)
                                {
                                    if (string.Equals(official, entry.Sha256, StringComparison.OrdinalIgnoreCase))
                                    { entry.Sha256Source = "OfficialChecksum"; LogMessage?.Invoke($"   🔒 {entry.Name}: Prüfsumme gegen offizielle Quelle verifiziert."); }
                                    else
                                        LogMessage?.Invoke($"   ⚠ {entry.Name}: WARNUNG — heruntergeladene Datei weicht von der offiziellen Prüfsumme ab!");
                                }
                            }
                            sa.Percent = 100; sa.Status = "✅ Fertig";
                            Interlocked.Increment(ref successCount);
                            LogMessage?.Invoke($"   ✅ {entry.Name}: Download abgeschlossen ({fname}) via {TryGetHost(usedUrl)}");
                        }
```

- [ ] **Step 8: Build verifizieren, Tests laufen lassen**

Run: `dotnet build UniversalLinuxManager.csproj -c Release`
Expected: "0 Fehler, 0 Warnungen"

Run: `dotnet test ULM.Tests/ULM.Tests.csproj -c Release`
Expected: alle Tests grün

- [ ] **Step 9: Committen**

```bash
git add Core/Services/HttpService.cs Core/Workers/Workers.cs ULM.Tests/HttpServiceTests.cs
git commit -m "feat: offizielle SHA-256-Prüfsumme für Ubuntu/Debian/Fedora nach Download verifizieren"
```

---

### Task 8: Manueller "Integrität prüfen"-Button

**Files:**
- Modify: `ViewModels/MainViewModel.cs`
- Modify: `Views/MainWindow.xaml`
- Modify: `Views/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `DetectVersionlessHashMismatchesAsync` (Task 6, wird für den manuellen Fall auf ALLE Einträge mit vorhandenem `Sha256` erweitert, nicht nur versionslose — siehe Step 1)
- Produces: `RelayCommand MainViewModel.VerifyStickIntegrityCommand`

- [ ] **Step 1: Erweiterte Hash-Prüfmethode für den manuellen Fall**

In `ViewModels/MainViewModel.cs` nach `DetectVersionlessHashMismatchesAsync` (Task 6) einfügen:

```csharp
        /// <summary>
        /// Manuelle, vollständige Variante von DetectVersionlessHashMismatchesAsync: prüft ALLE
        /// Einträge mit vorhandenem Referenz-Hash (nicht nur versionslose) — bewusst nur auf
        /// Anwender-Wunsch (Button), da das Hashen mehrerer GB-ISOs über USB spürbar dauert.
        /// </summary>
        public async Task VerifyStickIntegrityAsync()
        {
            if (string.IsNullOrEmpty(SelectedDriveLetter)) return;
            SetBusy(true); StatusText = "🔒 Prüfe Integrität …"; Log($"🔒 Integritätsprüfung {SelectedDriveLetter} gestartet …");
            var (found, _) = await UsbService.Instance.ScanStickVerifiedAsync(SelectedDriveLetter, _db.Entries).ConfigureAwait(false);
            var byFn = new Dictionary<string, UsbService.StickIso>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in found) if (!byFn.ContainsKey(f.Filename)) byFn[f.Filename] = f;

            var mismatches = new List<UsbService.StickIso>(); int checkedCount = 0;
            foreach (var e in _db.Entries)
            {
                if (string.IsNullOrEmpty(e.Sha256) || !byFn.TryGetValue(e.Filename, out var stick)) continue;
                checkedCount++;
                string actual = await IsoEntry.ComputeSha256Async(stick.FullPath).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(actual) && !string.Equals(actual, e.Sha256, StringComparison.OrdinalIgnoreCase))
                    mismatches.Add(stick);
            }
            _ui.Invoke(() =>
            {
                SetBusy(false); StatusText = mismatches.Count > 0 ? $"⚠ {mismatches.Count} Hash-Abweichung(en)." : $"✅ {checkedCount} ISO(s) verifiziert.";
                Log($"🔒 Integritätsprüfung {SelectedDriveLetter}: {checkedCount} geprüft, {mismatches.Count} Abweichung(en).");
                if (mismatches.Count > 0) IncompleteIsosOnStickDetected?.Invoke(mismatches, SelectedDriveLetter);
            });
        }
```

- [ ] **Step 2: Command registrieren**

In `ViewModels/MainViewModel.cs` nach `public RelayCommand RefreshDrivesCommand { get; }` (Zeile 113) einfügen:

```csharp
        public RelayCommand VerifyStickIntegrityCommand { get; }
```

Nach `RefreshDrivesCommand = new RelayCommand(RefreshDrives);` (Zeile 151) einfügen:

```csharp
            VerifyStickIntegrityCommand = new RelayCommand(() => _ = VerifyStickIntegrityAsync(), () => !IsBusy && !string.IsNullOrEmpty(SelectedDriveLetter));
```

(`RelayCommand` (`Infrastructure/RelayCommand.cs:13`) nimmt nur einen synchronen `Action`-Delegate — kein `async`-Overload. `_ = VerifyStickIntegrityAsync()` stößt die Task bewusst fire-and-forget an, exakt wie `BtnVerifyIntegrity_Click` in Step 4 es für den direkten Button-Klick ebenfalls tut.)

- [ ] **Step 3: Button im XAML ergänzen**

In `Views/MainWindow.xaml`, in der `ExpertBar`-StackPanel (Zeile 397-419), nach dem `BtnCopyUsb`-Button (Zeile 412-414) einfügen:

```xml
                    <Button x:Name="BtnVerifyIntegrity" Content="🔒  Integrität prüfen"
                            Style="{DynamicResource BtnGhost}" Width="160"
                            ToolTip="Prüft die ISOs auf dem gewählten Stick gegen den beim Download/Import gespeicherten SHA-256-Referenzhash."
                            Click="BtnVerifyIntegrity_Click" Margin="0,0,8,0"/>
```

- [ ] **Step 4: Click-Handler ergänzen**

In `Views/MainWindow.xaml.cs` nach `BtnHealthCheck_Click` (Zeile 528) einfügen:

```csharp
        private async void BtnVerifyIntegrity_Click(object sender, RoutedEventArgs e) { if (_vm.IsBusy) return; SetBusyUi(true); await _vm.VerifyStickIntegrityAsync(); SetBusyUi(false); }
```

- [ ] **Step 5: Build verifizieren**

Run: `dotnet build UniversalLinuxManager.csproj -c Release`
Expected: "0 Fehler, 0 Warnungen"

- [ ] **Step 6: Committen**

```bash
git add ViewModels/MainViewModel.cs Views/MainWindow.xaml Views/MainWindow.xaml.cs
git commit -m "feat: manueller Integritätsprüfung-Button für den ausgewählten Stick"
```

---

### Task 9: Version, Changelog, Hilfe

**Files:**
- Modify: `UniversalLinuxManager.csproj` (Versionsnummer)
- Modify: `Views/Dialogs/ChangelogDialog.cs`
- Modify: `Views/Dialogs/HelpDialog.cs`
- Modify: `docs/index.html`

Folgt der im Projekt etablierten Konvention (siehe Task-Historie #1–#5 der laufenden Session sowie `docs/superpowers/specs/2026-07-14-autostart-option-design.md`).

- [ ] **Step 1: Aktuelle Version ermitteln und Patch-Version erhöhen**

Run: `grep -n "<Version>" UniversalLinuxManager.csproj`

Version von z. B. `2.31.0` auf `2.31.1` in `UniversalLinuxManager.csproj` anheben (Tag `<Version>` bzw. `<AssemblyVersion>`/`<FileVersion>`, je nachdem was dort vorhanden ist — alle drei konsistent auf denselben Wert setzen, falls mehrere existieren).

- [ ] **Step 2: Changelog-Eintrag ergänzen**

In `Views/Dialogs/ChangelogDialog.cs` einen neuen Eintrag für die neue Version ergänzen (Struktur der vorhandenen Einträge exakt übernehmen — Datei zuerst lesen, um das dortige Format zu treffen), Inhalt:

- "Fehlerbehebung: ULM meldete eine bereits aktuelle Stick-ISO fälschlich als veraltet, wenn eine alte Version nie gelöscht wurde — bietet jetzt stattdessen das Löschen der alten Datei an."
- "Neu: SHA-256-Integritätsprüfung — nach Download/Import wird ein Referenzhash gespeichert, bei Ubuntu/Debian/Fedora zusätzlich gegen die offizielle Prüfsumme verifiziert. Manuelle Prüfung über den neuen Button „Integrität prüfen"."

- [ ] **Step 3: Hilfe-Dialog ergänzen**

In `Views/Dialogs/HelpDialog.cs` einen neuen Punkt für "🔒 Integrität prüfen" analog zu den bestehenden Einträgen (z. B. neben "„(schneller)"-Button") ergänzen: kurze Erklärung, dass ULM SHA-256-Hashes nach Download/Import speichert und bei Bedarf gegen die Stick-Kopie prüft.

- [ ] **Step 4: `docs/index.html` Versionsbadge aktualisieren**

Run: `grep -n "2\.31\.0" docs/index.html`

Alle Treffer auf die neue Version aus Step 1 anheben.

- [ ] **Step 5: Build und volle Testsuite verifizieren**

Run: `dotnet build UniversalLinuxManager.csproj -c Release`
Expected: "0 Fehler, 0 Warnungen"

Run: `dotnet test ULM.Tests/ULM.Tests.csproj -c Release`
Expected: alle Tests grün

- [ ] **Step 6: Committen**

```bash
git add UniversalLinuxManager.csproj Views/Dialogs/ChangelogDialog.cs Views/Dialogs/HelpDialog.cs docs/index.html
git commit -m "v2.31.1: Stick-Duplikat-Fix + SHA-256-Integritätsprüfung"
```

---

## Nach Abschluss aller Tasks

Nicht automatisch pushen oder veröffentlichen — der Nutzer entscheidet explizit, ob und wann veröffentlicht wird (siehe Session-Konvention). Nach Task 9 den Nutzer über den fertigen Stand informieren und auf Freigabe für Push/PR/Release warten.
