# Erkennung roher (buchstabenloser) USB-Sticks — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** USB-Sticks, die Windows keinen Laufwerksbuchstaben zuweist (z.B. mit Rufus im ISO/DD-Modus beschrieben), zuverlässig erkennen, sicher vorbereiten (Buchstabe zuweisen) und nahtlos in den bestehenden, unveränderten "Neuer USB-Stick erkannt"-Ventoy-Dialog überführen.

**Architecture:** Neue `Win32_DiskDrive`-basierte Erkennung (physische Ebene, unabhängig von Laufwerksbuchstaben) in `UsbService.cs`, ergänzt um eine zweistufige Systemdatenträger-Sperre. `MainViewModel` bekommt eine neue `CheckRawUsbDisks()`-Methode, die im selben 8-Sekunden-Timer wie die bestehende Laufwerkserkennung läuft und VOR ihr aufgerufen wird — dadurch sieht die bestehende, unveränderte `RefreshDrives()`/`OnNewDriveInserted()`-Kette den frisch vorbereiteten Stick im selben Tick ganz normal als "neuen Stick mit Buchstabe".

**Tech Stack:** .NET 8, WMI via PowerShell (`Get-CimInstance`, `Get-CimAssociatedInstance`), `diskpart` (bestehendes Muster aus `DoFormat`), xUnit (bestehendes `ULM.Tests`-Projekt).

## Global Constraints

- Nur Windows betroffen (`Core/Services/UsbService.cs`, `ViewModels/MainViewModel.cs`, `Views/MainWindow.xaml.cs`). Linux-GUI (`Linux/`) bleibt unangetastet.
- Ventoys eigene CLI (`VTOYCLI`, siehe `Core/Workers/Workers.cs:110`) akzeptiert nur `/Drive:X:` — der bestehende `VentoyInstallWorker`/`VentoyInstallWindow`-Ablauf bleibt komplett unverändert, bekommt weiterhin nur einen Laufwerksbuchstaben.
- Destruktive Aktion (`diskpart clean`) — Systemdatenträger-Sperre MUSS **zweimal** greifen: beim Auflisten UND unmittelbar vor der Ausführung. Schlägt die Ermittlung des Systemdatenträger-Index fehl (unbekannt), MUSS das Verhalten **fail-closed** sein (keine Kandidaten anbieten / Ausführung verweigern) — NICHT fail-open.
- Build-Befehl Hauptprojekt: `dotnet build UniversalLinuxManager.csproj -c Release`
- Test-Befehl: `dotnet test ULM.Tests/ULM.Tests.csproj -c Release`
- WMI-/`diskpart`-Aufrufe selbst sind ohne echte USB-Hardware nicht automatisiert testbar (wie die bereits bestehende `ListRemovableDrives()`) — nur die pure Logik drumherum wird unit-getestet. Der Nutzer verifiziert den Gesamtablauf abschließend manuell mit echter Hardware.

---

### Task 1: Datenmodell + reine Logik-Helfer (TDD)

**Files:**
- Create: `Core/Models/RawUsbDiskCandidate.cs`
- Modify: `Core/Services/UsbService.cs` (neue `internal static`-Methoden anhängen, ans Ende der Klasse vor der schließenden Klammer)
- Test: `ULM.Tests/UsbServiceRawDiskLogicTests.cs`

**Interfaces:**
- Produces: `ULM.Core.Models.RawUsbDiskCandidate(int DiskIndex, long SizeBytes)` — von Task 2 (Rückgabetyp) und Task 4 (MainViewModel) verwendet.
- Produces: `ULM.Core.Services.UsbService.IsSafeToPrepare(int diskIndex, int? systemDiskIndex) : bool` — von Task 3 verwendet.
- Produces: `ULM.Core.Services.UsbService.FindNewRawDiskIndices(IReadOnlyList<int> previousIndices, IReadOnlyList<int> currentIndices) : List<int>` — von Task 4 verwendet.
- Produces: `ULM.Core.Services.UsbService.FindFreeDriveLetter(IEnumerable<char> usedLetters) : char?` — von Task 4 verwendet.

- [ ] **Step 1: `Core/Models/RawUsbDiskCandidate.cs` anlegen**

```csharp
// Core/Models/RawUsbDiskCandidate.cs
namespace ULM.Core.Models
{
    /// <summary>
    /// Ein physischer USB-Datenträger ohne zugewiesenen Laufwerksbuchstaben (z.B. mit Rufus im
    /// ISO/DD-Modus beschrieben) — erkannt über Win32_DiskDrive statt Win32_LogicalDisk, siehe
    /// UsbService.ListRawUsbDisksWithoutLetter().
    /// </summary>
    public sealed record RawUsbDiskCandidate(int DiskIndex, long SizeBytes);
}
```

- [ ] **Step 2: Fehlschlagende Tests für die drei reinen Helfer schreiben**

```csharp
// ULM.Tests/UsbServiceRawDiskLogicTests.cs
using System.Collections.Generic;
using ULM.Core.Services;
using Xunit;

namespace ULM.Tests
{
    public class UsbServiceRawDiskLogicTests
    {
        [Fact]
        public void IsSafeToPrepare_DifferentFromSystemDisk_ReturnsTrue()
        {
            Assert.True(UsbService.IsSafeToPrepare(diskIndex: 1, systemDiskIndex: 0));
        }

        [Fact]
        public void IsSafeToPrepare_SameAsSystemDisk_ReturnsFalse()
        {
            Assert.False(UsbService.IsSafeToPrepare(diskIndex: 0, systemDiskIndex: 0));
        }

        [Fact]
        public void IsSafeToPrepare_UnknownSystemDiskIndex_FailsClosed()
        {
            // null = Systemdatenträger-Index konnte nicht ermittelt werden — MUSS als unsicher
            // gelten (fail-closed), nicht als "kein Konflikt gefunden also sicher" (fail-open).
            Assert.False(UsbService.IsSafeToPrepare(diskIndex: 1, systemDiskIndex: null));
        }

        [Fact]
        public void FindNewRawDiskIndices_NewIndexAppears_ReturnsOnlyTheNewOne()
        {
            var previous = new List<int> { 1, 2 };
            var current  = new List<int> { 1, 2, 3 };
            var result = UsbService.FindNewRawDiskIndices(previous, current);
            Assert.Equal(new[] { 3 }, result);
        }

        [Fact]
        public void FindNewRawDiskIndices_NothingChanged_ReturnsEmpty()
        {
            var previous = new List<int> { 1, 2 };
            var current  = new List<int> { 1, 2 };
            var result = UsbService.FindNewRawDiskIndices(previous, current);
            Assert.Empty(result);
        }

        [Fact]
        public void FindNewRawDiskIndices_DiskRemoved_ReturnsEmptyNotNegative()
        {
            var previous = new List<int> { 1, 2, 3 };
            var current  = new List<int> { 1 };
            var result = UsbService.FindNewRawDiskIndices(previous, current);
            Assert.Empty(result);
        }

        [Fact]
        public void FindFreeDriveLetter_SkipsUsedLetters_ReturnsFirstFreeFromD()
        {
            var used = new[] { 'C', 'D', 'E' };
            char? result = UsbService.FindFreeDriveLetter(used);
            Assert.Equal('F', result);
        }

        [Fact]
        public void FindFreeDriveLetter_AllLettersUsed_ReturnsNull()
        {
            var used = new List<char>();
            for (char c = 'A'; c <= 'Z'; c++) used.Add(c);
            char? result = UsbService.FindFreeDriveLetter(used);
            Assert.Null(result);
        }
    }
}
```

- [ ] **Step 3: Tests laufen lassen — müssen fehlschlagen (Methoden existieren noch nicht)**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj --filter UsbServiceRawDiskLogicTests`
Expected: Build-Fehler `CS0117: 'UsbService' does not contain a definition for 'IsSafeToPrepare'` (und analog für die anderen zwei Methoden)

- [ ] **Step 4: Die drei Helfer in `Core/Services/UsbService.cs` implementieren**

Direkt vor der schließenden `}` der `UsbService`-Klasse einfügen (nach `RunPowerShell`):

```csharp
        // ── Rohe (buchstabenlose) USB-Datenträger: reine Logik-Helfer ──────
        // systemDiskIndex ist bewusst nullable: konnte der Systemdatenträger-Index nicht ermittelt
        // werden (z.B. WMI-Fehler), MUSS das als "unsicher" gelten (fail-closed) — sonst würde ein
        // WMI-Ausfall versehentlich JEDEN Datenträger als sicher durchwinken (fail-open), bei einer
        // destruktiven Aktion (diskpart clean) inakzeptabel.
        internal static bool IsSafeToPrepare(int diskIndex, int? systemDiskIndex) =>
            systemDiskIndex.HasValue && diskIndex != systemDiskIndex.Value;

        internal static List<int> FindNewRawDiskIndices(IReadOnlyList<int> previousIndices, IReadOnlyList<int> currentIndices) =>
            currentIndices.Except(previousIndices).ToList();

        // A/B (historisch Diskette) und C (i.d.R. System) werden übersprungen — nicht aus
        // Sicherheitsgründen (IsSafeToPrepare deckt das bereits ab), sondern weil ein frisch
        // zugewiesener Buchstabe dort ohnehin von Windows selbst verweigert würde.
        internal static char? FindFreeDriveLetter(IEnumerable<char> usedLetters)
        {
            var used = new HashSet<char>(usedLetters);
            for (char c = 'D'; c <= 'Z'; c++)
                if (!used.Contains(c)) return c;
            return null;
        }
```

- [ ] **Step 5: Tests laufen lassen — müssen bestehen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj --filter UsbServiceRawDiskLogicTests`
Expected: `Passed! - Failed: 0, Passed: 8`

- [ ] **Step 6: Build-Check Hauptprojekt**

Run: `dotnet build UniversalLinuxManager.csproj -c Release`
Expected: `Build succeeded.`

- [ ] **Step 7: Commit**

```bash
git add Core/Models/RawUsbDiskCandidate.cs Core/Services/UsbService.cs ULM.Tests/UsbServiceRawDiskLogicTests.cs
git commit -m "feat: add pure logic helpers for raw USB disk detection (TDD)"
```

---

### Task 2: WMI-Erkennung roher USB-Datenträger

**Files:**
- Modify: `Core/Services/UsbService.cs` (neue `GetSystemDiskIndex()` + `ListRawUsbDisksWithoutLetter()`)
- Modify: `Core/Services/UsbService.cs` (Interface `IUsbService` um neue Methode erweitern)
- Modify: `ULM.Tests/TestDoubles.cs` (`FakeUsbService` implementiert neue Interface-Methode)

**Interfaces:**
- Consumes: `IsSafeToPrepare` (Task 1)
- Produces: `ULM.Core.Services.IUsbService.ListRawUsbDisksWithoutLetter() : List<RawUsbDiskCandidate>` — von Task 4 (`MainViewModel.CheckRawUsbDisks`) konsumiert.

Kein automatisierter Test in diesem Task — reine WMI-Shell-Aufrufe, wie die bereits bestehende `ListRemovableDrives()` ohne Testabdeckung. Build-Check als Verifikation.

- [ ] **Step 1: `IUsbService`-Interface erweitern**

In `Core/Services/UsbService.cs`, im `public interface IUsbService`-Block:

```csharp
    public interface IUsbService
    {
        List<UsbDrive> ListRemovableDrives();
        Task<(List<UsbService.StickIso> Found, List<UsbService.StickIso> Incomplete)> ScanStickVerifiedAsync(string letter, IReadOnlyList<IsoEntry> entries);
        List<RawUsbDiskCandidate> ListRawUsbDisksWithoutLetter();
        bool PrepareRawUsbDisk(int diskIndex, char letter);
    }
```

(`PrepareRawUsbDisk` wird erst in Task 3 implementiert — beide Methoden hier zusammen deklarieren, damit der Build in diesem Task nicht durch eine unvollständige Interface-Implementierung fehlschlägt; Step 2 unten fügt einen Platzhalter-Rumpf für `PrepareRawUsbDisk` ein, der in Task 3 ersetzt wird.)

- [ ] **Step 2: `GetSystemDiskIndex()` + `ListRawUsbDisksWithoutLetter()` implementieren, `PrepareRawUsbDisk`-Rumpf vorerst mit `NotImplementedException`**

Nach der bestehenden `ListSignature`-Methode einfügen:

```csharp
        // ── Rohe (buchstabenlose) USB-Datenträger: Erkennung ───────────────
        // Ermittelt den Win32_DiskDrive.Index des Datenträgers, der die Windows-Systempartition
        // (%SystemDrive%, i.d.R. C:) enthält — Grundlage für die Systemdatenträger-Sperre in
        // IsSafeToPrepare. Gibt null zurück, wenn die Ermittlung fehlschlägt (WMI-Fehler o.ä.) —
        // Aufrufer MÜSSEN das als "unsicher" behandeln (siehe IsSafeToPrepare-Kommentar).
        private static int? GetSystemDiskIndex()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return null;
            const string script = @"
$sys = Get-CimInstance Win32_LogicalDisk -Filter ""DeviceID='$($env:SystemDrive)'""
if ($sys) {
  $sysPart = Get-CimAssociatedInstance -InputObject $sys -Association Win32_LogicalDiskToPartition -ErrorAction SilentlyContinue
  if ($sysPart) {
    $sysDisk = Get-CimAssociatedInstance -InputObject $sysPart -Association Win32_DiskDriveToDiskPartition -ErrorAction SilentlyContinue
    if ($sysDisk) { Write-Output $sysDisk.Index }
  }
}";
            string output = RunPowerShell(script, 8).Trim();
            return int.TryParse(output, out int idx) ? idx : null;
        }

        /// <summary>
        /// Erkennt physische USB-Datenträger, denen Windows (noch) keinen Laufwerksbuchstaben
        /// zugewiesen hat — z.B. mit Rufus im ISO/DD-Modus beschriebene Sticks, die in
        /// ListRemovableDrives() (basiert auf Win32_LogicalDisk, listet nur Datenträger MIT
        /// Buchstabe) nie auftauchen. Win32_DiskDrive arbeitet auf physischer Ebene, unabhängig
        /// von Laufwerksbuchstaben — die WMI-Entsprechung dessen, was Rufus selbst über
        /// SetupDiGetClassDevs/IOCTL_STORAGE_QUERY_PROPERTY auf niedrigerer Ebene macht.
        /// Schlägt die Systemdatenträger-Ermittlung fehl, wird eine leere Liste zurückgegeben
        /// (fail-closed) statt ungeprüft alle USB-Datenträger anzubieten.
        /// </summary>
        public List<RawUsbDiskCandidate> ListRawUsbDisksWithoutLetter()
        {
            var result = new List<RawUsbDiskCandidate>();
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return result;

            int? systemDiskIndex = GetSystemDiskIndex();
            if (systemDiskIndex is null) return result;

            const string script = @"
$disks = Get-CimInstance Win32_DiskDrive | Where-Object { $_.InterfaceType -eq 'USB' }
foreach ($d in $disks) {
  $hasLetter = $false
  $parts = Get-CimAssociatedInstance -InputObject $d -Association Win32_DiskDriveToDiskPartition -ErrorAction SilentlyContinue
  foreach ($p in $parts) {
    $lds = Get-CimAssociatedInstance -InputObject $p -Association Win32_LogicalDiskToPartition -ErrorAction SilentlyContinue
    if ($lds) { $hasLetter = $true }
  }
  if (-not $hasLetter) {
    Write-Output ($d.Index.ToString() + '|' + [int64]$d.Size)
  }
}";
            string output = RunPowerShell(script, 10);
            foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] parts = line.Split('|');
                if (parts.Length < 2) continue;
                if (!int.TryParse(parts[0], out int idx)) continue;
                if (!long.TryParse(parts[1], out long size)) continue;
                if (size < 2_000_000_000) continue;
                if (!IsSafeToPrepare(idx, systemDiskIndex)) continue;
                result.Add(new RawUsbDiskCandidate(idx, size));
            }
            return result;
        }

        public bool PrepareRawUsbDisk(int diskIndex, char letter) => throw new NotImplementedException("siehe Task 3");
```

- [ ] **Step 3: `FakeUsbService` in `ULM.Tests/TestDoubles.cs` um beide neuen Interface-Methoden erweitern**

```csharp
internal sealed class FakeUsbService : IUsbService
{
    public List<UsbDrive> DrivesToReturn { get; set; } = new();
    public List<RawUsbDiskCandidate> RawDisksToReturn { get; set; } = new();
    public List<(int DiskIndex, char Letter)> PrepareCalls { get; } = new();
    public bool PrepareShouldSucceed { get; set; } = true;

    public List<UsbDrive> ListRemovableDrives() => DrivesToReturn;
    public List<RawUsbDiskCandidate> ListRawUsbDisksWithoutLetter() => RawDisksToReturn;
    public bool PrepareRawUsbDisk(int diskIndex, char letter)
    {
        PrepareCalls.Add((diskIndex, letter));
        return PrepareShouldSucceed;
    }
    public Task<(List<UsbService.StickIso> Found, List<UsbService.StickIso> Incomplete)> ScanStickVerifiedAsync(string letter, IReadOnlyList<IsoEntry> entries)
        => Task.FromResult((new List<UsbService.StickIso>(), new List<UsbService.StickIso>()));
}
```

Füge oben in der Datei `using ULM.Core.Models;` hinzu, falls noch nicht vorhanden (für `RawUsbDiskCandidate`) — bereits vorhanden laut bestehendem Datei-Kopf.

- [ ] **Step 4: Build-Check**

Run: `dotnet build UniversalLinuxManager.csproj -c Release`
Expected: `Build succeeded.` (0 Fehler — bestätigt, dass `IUsbService` korrekt implementiert ist, auch wenn `PrepareRawUsbDisk` in der echten `UsbService`-Klasse noch einen Platzhalter hat)

- [ ] **Step 5: Testsuite laufen lassen — bestehende Tests dürfen nicht brechen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj -c Release`
Expected: Alle bisherigen Tests weiterhin grün, `Failed: 0`

- [ ] **Step 6: Commit**

```bash
git add Core/Services/UsbService.cs ULM.Tests/TestDoubles.cs
git commit -m "feat: detect raw USB disks without drive letter via Win32_DiskDrive"
```

---

### Task 3: `diskpart`-Vorbereitung (Buchstabe zuweisen)

**Files:**
- Modify: `Core/Services/UsbService.cs` (`PrepareRawUsbDisk`-Platzhalter aus Task 2 ersetzen)

**Interfaces:**
- Consumes: `IsSafeToPrepare`, `GetSystemDiskIndex` (Task 1+2)
- Produces: `ULM.Core.Services.UsbService.PrepareRawUsbDisk(int diskIndex, char letter) : bool` (vollständige Implementierung) — von Task 4 konsumiert.

Kein automatisierter Test — echter `diskpart`-Aufruf, destruktiv, nicht ohne echte Test-Hardware sinnvoll automatisierbar. Build-Check als Verifikation, Nutzer verifiziert final mit echtem Rufus-Stick.

- [ ] **Step 1: Platzhalter durch echte Implementierung ersetzen**

```csharp
        /// <summary>
        /// Bereitet einen rohen (buchstabenlosen) USB-Datenträger für die normale Erkennung vor:
        /// komplett neu partitionieren und formatieren, damit Windows ihm einen Laufwerksbuchstaben
        /// zuweist. fs=fat32 (nicht exfat) ist bewusst gewählt — dieser Schritt dient NUR dazu,
        /// Windows zur Buchstaben-Zuweisung zu bewegen; Ventoy2Disk formatiert den Stick bei der
        /// eigentlichen Einrichtung ohnehin komplett neu (siehe VentoyInstallWorker).
        ///
        /// SICHERHEIT: Zweite, unabhängige Systemdatenträger-Prüfung unmittelbar vor dem
        /// destruktiven diskpart-Aufruf — zusätzlich zur bereits in ListRawUsbDisksWithoutLetter()
        /// erfolgten Prüfung. Verhindert, dass sich zwischen Anzeige/Erkennung und tatsächlicher
        /// Ausführung etwas an den Datenträger-Indizes geändert hat (z.B. durch ein zwischenzeitlich
        /// weiteres eingestecktes Laufwerk).
        /// </summary>
        public bool PrepareRawUsbDisk(int diskIndex, char letter)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;

            int? systemDiskIndex = GetSystemDiskIndex();
            if (!IsSafeToPrepare(diskIndex, systemDiskIndex)) return false;

            string script =
                $"select disk {diskIndex}\nclean\ncreate partition primary\n" +
                $"format fs=fat32 quick label=ULMPREP\nassign letter={letter}\nexit\n";
            string tempFile = Path.Combine(Path.GetTempPath(), "ulm_diskpart_raw.txt");
            File.WriteAllText(tempFile, script, Encoding.ASCII);
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("diskpart", $"/s \"{tempFile}\"")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc is null) return false;
                proc.WaitForExit(60_000);
                return proc.ExitCode == 0;
            }
            finally { try { File.Delete(tempFile); } catch { } }
        }
```

- [ ] **Step 2: Build-Check**

Run: `dotnet build UniversalLinuxManager.csproj -c Release`
Expected: `Build succeeded.`

- [ ] **Step 3: Testsuite laufen lassen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj -c Release`
Expected: Alle Tests weiterhin grün.

- [ ] **Step 4: Commit**

```bash
git add Core/Services/UsbService.cs
git commit -m "feat: implement diskpart-based raw USB disk preparation"
```

---

### Task 4: `MainViewModel.CheckRawUsbDisks()` (TDD)

**Files:**
- Modify: `ViewModels/MainViewModel.cs`
- Modify: `Infrastructure/Str.cs` (4 neue Log-Strings)
- Modify: `Infrastructure/LocalizationService.cs` (DE+EN Übersetzungen)
- Test: `ULM.Tests/MainViewModelRawUsbDiskTests.cs`

**Interfaces:**
- Consumes: `IUsbService.ListRawUsbDisksWithoutLetter()`, `IUsbService.PrepareRawUsbDisk(int, char)` (Task 2+3), `UsbService.FindNewRawDiskIndices`, `UsbService.FindFreeDriveLetter` (Task 1)
- Produces: `MainViewModel.CheckRawUsbDisks() : void` — von Task 5 (`Views/MainWindow.xaml.cs`) aufgerufen.

- [ ] **Step 1: Neue `Str`-Einträge hinzufügen**

In `Infrastructure/Str.cs`, direkt nach `Log_OperationSucceededLogPrefix,` (Zeile 414) einfügen:

```csharp
        // ── Log-Meldungen: Rohe (buchstabenlose) USB-Datenträger ──────────
        Log_RawUsbDiskDetected, Log_RawUsbDiskPrepared, Log_RawUsbDiskPrepareFailed,
        Log_RawUsbDiskNoFreeLetter,
```

- [ ] **Step 2: Deutsche Übersetzungen ergänzen**

In `Infrastructure/LocalizationService.cs`, im deutschen Dictionary (`De`), direkt nach dem bestehenden `[Str.Log_UsbDrivesDetected]`-Eintrag suchen und danach einfügen:

```csharp
            [Str.Log_RawUsbDiskDetected]      = "🔌 Nicht gemounteter USB-Datenträger erkannt (Datenträger {0}) — bereite Laufwerksbuchstabe vor …",
            [Str.Log_RawUsbDiskPrepared]      = "✅ Datenträger {0} als {1}: eingerichtet.",
            [Str.Log_RawUsbDiskPrepareFailed] = "⚠ Vorbereitung von Datenträger {0} fehlgeschlagen — konnte keinen Laufwerksbuchstaben zuweisen.",
            [Str.Log_RawUsbDiskNoFreeLetter]  = "⚠ Kein freier Laufwerksbuchstabe verfügbar — Datenträger {0} übersprungen.",
```

- [ ] **Step 3: Englische Übersetzungen ergänzen**

Im englischen Dictionary (`En`), an der analogen Stelle:

```csharp
            [Str.Log_RawUsbDiskDetected]      = "🔌 Unmounted USB disk detected (disk {0}) — preparing drive letter …",
            [Str.Log_RawUsbDiskPrepared]      = "✅ Disk {0} set up as {1}:",
            [Str.Log_RawUsbDiskPrepareFailed] = "⚠ Failed to prepare disk {0} — could not assign a drive letter.",
            [Str.Log_RawUsbDiskNoFreeLetter]  = "⚠ No free drive letter available — skipping disk {0}.",
```

- [ ] **Step 4: Fehlschlagenden Test schreiben**

```csharp
// ULM.Tests/MainViewModelRawUsbDiskTests.cs
using System.Windows.Threading;
using ULM.Core.Models;
using Xunit;

namespace ULM.Tests
{
    public class MainViewModelRawUsbDiskTests
    {
        private static (MainViewModel vm, FakeUsbService usb) Build()
        {
            var usb = new FakeUsbService();
            var vm  = new MainViewModel(Dispatcher.CurrentDispatcher, usb: usb);
            return (vm, usb);
        }

        [Fact]
        public void CheckRawUsbDisks_NewCandidate_CallsPrepareWithFreeLetter()
        {
            var (vm, usb) = Build();
            usb.RawDisksToReturn = new() { new RawUsbDiskCandidate(DiskIndex: 3, SizeBytes: 32_000_000_000) };

            vm.CheckRawUsbDisks();

            Assert.Single(usb.PrepareCalls);
            Assert.Equal(3, usb.PrepareCalls[0].DiskIndex);
        }

        [Fact]
        public void CheckRawUsbDisks_SameCandidateTwice_OnlyPreparedOnce()
        {
            var (vm, usb) = Build();
            usb.RawDisksToReturn = new() { new RawUsbDiskCandidate(3, 32_000_000_000) };

            vm.CheckRawUsbDisks();
            vm.CheckRawUsbDisks();

            Assert.Single(usb.PrepareCalls);
        }

        [Fact]
        public void CheckRawUsbDisks_NoCandidates_DoesNotCallPrepare()
        {
            var (vm, usb) = Build();
            usb.RawDisksToReturn = new();

            vm.CheckRawUsbDisks();

            Assert.Empty(usb.PrepareCalls);
        }

        [Fact]
        public void CheckRawUsbDisks_PrepareFails_DoesNotThrow()
        {
            var (vm, usb) = Build();
            usb.RawDisksToReturn = new() { new RawUsbDiskCandidate(3, 32_000_000_000) };
            usb.PrepareShouldSucceed = false;

            var ex = Record.Exception(() => vm.CheckRawUsbDisks());

            Assert.Null(ex);
        }
    }
}
```

- [ ] **Step 5: Test laufen lassen — muss fehlschlagen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj --filter MainViewModelRawUsbDiskTests`
Expected: Build-Fehler `CS1061: 'MainViewModel' does not contain a definition for 'CheckRawUsbDisks'`

- [ ] **Step 6: `CheckRawUsbDisks()` implementieren**

In `ViewModels/MainViewModel.cs`, direkt nach dem bestehenden Feld `private string _lastDriveSignature = string.Empty;` (Zeile 33) ein neues Feld ergänzen:

```csharp
        private List<int> _lastRawDiskIndices = new();
```

Direkt nach der bestehenden `RefreshDrives()`-Methode (nach Zeile 487) einfügen:

```csharp
        /// <summary>
        /// Erkennt physische USB-Datenträger ohne Laufwerksbuchstaben (siehe
        /// IUsbService.ListRawUsbDisksWithoutLetter) und bereitet neu aufgetauchte Kandidaten
        /// automatisch vor (Buchstabe zuweisen). Läuft im selben Timer-Tick wie RefreshDrives(),
        /// bewusst VOR ihr aufgerufen (siehe Views/MainWindow.xaml.cs CheckDriveChanges) — dadurch
        /// sieht der direkt darauffolgende RefreshDrives()-Aufruf den frisch vorbereiteten Stick im
        /// selben Tick bereits mit Buchstabe und behandelt ihn über die bestehende,
        /// unveränderte OnNewDriveInserted()-Kette ganz normal wie jeden anderen neuen Stick.
        /// </summary>
        public void CheckRawUsbDisks()
        {
            var candidates = _usb.ListRawUsbDisksWithoutLetter();
            var currentIndices = candidates.Select(c => c.DiskIndex).ToList();
            var newIndices = UsbService.FindNewRawDiskIndices(_lastRawDiskIndices, currentIndices);
            _lastRawDiskIndices = currentIndices;

            foreach (int idx in newIndices)
            {
                Log(string.Format(LocalizationService.T(Str.Log_RawUsbDiskDetected), idx));

                var usedLetters = System.IO.DriveInfo.GetDrives().Select(d => d.Name[0]);
                char? letter = UsbService.FindFreeDriveLetter(usedLetters);
                if (letter is null)
                {
                    Log(string.Format(LocalizationService.T(Str.Log_RawUsbDiskNoFreeLetter), idx));
                    continue;
                }

                bool ok = _usb.PrepareRawUsbDisk(idx, letter.Value);
                Log(ok
                    ? string.Format(LocalizationService.T(Str.Log_RawUsbDiskPrepared), idx, letter.Value)
                    : string.Format(LocalizationService.T(Str.Log_RawUsbDiskPrepareFailed), idx));
            }
        }
```

- [ ] **Step 7: Test laufen lassen — muss bestehen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj --filter MainViewModelRawUsbDiskTests`
Expected: `Passed! - Failed: 0, Passed: 4`

- [ ] **Step 8: Volle Testsuite laufen lassen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj -c Release`
Expected: Alle Tests weiterhin grün, `Failed: 0`

- [ ] **Step 9: Commit**

```bash
git add ViewModels/MainViewModel.cs Infrastructure/Str.cs Infrastructure/LocalizationService.cs ULM.Tests/MainViewModelRawUsbDiskTests.cs
git commit -m "feat: add MainViewModel.CheckRawUsbDisks with tests"
```

---

### Task 5: Einbindung in den bestehenden Timer-Tick

**Files:**
- Modify: `Views/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `MainViewModel.CheckRawUsbDisks()` (Task 4)

- [ ] **Step 1: `CheckDriveChanges()` um den neuen Aufruf erweitern**

In `Views/MainWindow.xaml.cs`, die bestehende Methode (Zeile 688-694):

```csharp
        private void CheckDriveChanges()
        {
            if (_vm.IsBusy) return;
            string prev = _lastDriveSignatureUi; _vm.RefreshDrives();
            string curr = string.Join(";", _vm.Drives.Select(d => d.Letter)); _lastDriveSignatureUi = curr;
            if (curr != prev && curr.Length > prev.Length) OnNewDriveInserted();
        }
```

ersetzen durch:

```csharp
        private void CheckDriveChanges()
        {
            if (_vm.IsBusy) return;
            // Läuft bewusst VOR RefreshDrives(): wird hier ein roher (buchstabenloser) USB-
            // Datenträger vorbereitet (Buchstabe zugewiesen), sieht der direkt folgende
            // RefreshDrives()-Aufruf ihn im selben Tick bereits als normalen, gemounteten Stick —
            // die bestehende OnNewDriveInserted()-Kette darunter bleibt dadurch unverändert.
            _vm.CheckRawUsbDisks();
            string prev = _lastDriveSignatureUi; _vm.RefreshDrives();
            string curr = string.Join(";", _vm.Drives.Select(d => d.Letter)); _lastDriveSignatureUi = curr;
            if (curr != prev && curr.Length > prev.Length) OnNewDriveInserted();
        }
```

- [ ] **Step 2: Build-Check**

Run: `dotnet build UniversalLinuxManager.csproj -c Release`
Expected: `Build succeeded.`

- [ ] **Step 3: Volle Testsuite laufen lassen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj -c Release`
Expected: Alle Tests weiterhin grün, `Failed: 0`

- [ ] **Step 4: Commit**

```bash
git add Views/MainWindow.xaml.cs
git commit -m "feat: wire CheckRawUsbDisks into existing drive-change timer tick"
```

---

### Task 6: Abschlussverifikation

**Files:** Keine neuen Dateien — reine Verifikation.

- [ ] **Step 1: Komplette Testsuite ausführen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj -c Release`
Expected: Alle Tests grün (bisherige + 8 aus Task 1 + 4 aus Task 4 = 12 neue), `Failed: 0`.

- [ ] **Step 2: Release-Build**

Run: `dotnet build UniversalLinuxManager.csproj -c Release`
Expected: `Build succeeded.`, 0 Fehler, 0 neue Warnungen.

- [ ] **Step 3: Manuelle Verifikation durch den Nutzer (kann nicht automatisiert werden)**

Kein Weg, USB-Hardware/WMI in dieser Entwicklungsumgebung zu simulieren. Der Nutzer testet mit dem
tatsächlich betroffenen, mit Rufus im ISO/DD-Modus beschriebenen Stick:

1. Stick, der bisher nicht erkannt wurde, einstecken.
2. Innerhalb von ~8-16 Sekunden (ein bis zwei Timer-Ticks) sollte im Protokoll
   "🔌 Nicht gemounteter USB-Datenträger erkannt …" gefolgt von "✅ Datenträger … eingerichtet."
   erscheinen.
3. Direkt danach sollte der normale "Neuer USB-Stick erkannt — als Ventoy einrichten?"-Dialog
   erscheinen, wie bei jedem anderen neuen Stick auch.
4. Bestätigen und normalen Ventoy-Einrichtungsablauf durchlaufen — sollte sich in nichts vom
   bisherigen Ablauf unterscheiden.
5. **Sicherheitscheck:** currently kein interner Datenträger sollte jemals betroffen sein — der
   Nutzer sollte NICHT versuchen, dies gezielt zu testen (zu riskant), sondern nur bestätigen,
   dass während normaler Nutzung keine unerwarteten Formatierungs-Angebote für andere Laufwerke
   erscheinen.

- [ ] **Step 4: Commit (nach erfolgreicher manueller Verifikation)**

```bash
git add docs/superpowers/plans/2026-08-10-raw-usb-disk-detection.md
git commit -m "docs: mark raw USB disk detection plan as verified"
```
