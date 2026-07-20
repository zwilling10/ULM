# Programm-Selbst-Update: automatischer Download + Installation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Erweitert den bestehenden Selbst-Update-Banner um automatischen
Hintergrund-Download und automatisches Installieren/Ersetzen samt
Neustart — statt wie bisher nur manuellen Download + Explorer öffnen.

**Architecture:** Neuer Singleton-Service `SelfUpdateService` (gleiches
`.Instance`-Muster wie `HttpService`/`UsbService`/`IsoDatabaseService`,
kein DI-Container im Projekt) kapselt Varianten-Erkennung
(Installer vs. portable, per `unins000.exe`-Marker), Hintergrund-Download
über das bestehende `HttpService.DownloadAsync` und das
Installieren/Ersetzen (silent Setup-Aufruf mit Inno-Setup-
`CloseApplications`/`RestartApplications` bzw. PowerShell-Self-Replace-
Helper für die portable Variante). `MainViewModel` bekommt einen
`UpdateBannerState`-Zustand (`Available`/`Downloading`/`ReadyToInstall`),
an den Banner-Text und -Button in `MainWindow.xaml` gebunden werden.

**Tech Stack:** C# / .NET 8 (net8.0-windows), WPF (`UseWPF=true`, ein
einziges Projekt `UniversalLinuxManager.csproj`, kein separates
Core-Class-Library-Projekt), xUnit (`ULM.Tests`), Inno Setup
(`installer/ULM.iss`).

## Global Constraints

- Kein DI-Container — jeder Service ist ein Singleton mit privatem
  Konstruktor und `public static X Instance => _lazy.Value`
  (siehe `HttpService.cs:35-36`, `UsbService.cs:26`).
- `internal static` Methoden sind aus `ULM.Tests` heraus testbar
  (`InternalsVisibleTo` in `UniversalLinuxManager.csproj:69`) — reine
  Logik dort als `internal static` auslagern und direkt testen, statt
  Netzwerk-/Prozess-Aufrufe zu mocken.
- Kein periodischer Re-Check während der Laufzeit — nur der bestehende
  einmalige Check beim Programmstart bleibt bestehen.
- Kein "Version überspringen" — der bestehende Schließen-Button (X)
  blendet das Banner weiterhin nur für die laufende Sitzung aus.
- Keine SHA-256-/Signatur-Prüfung der heruntergeladenen Datei (Download
  läuft über TLS direkt von der offiziellen GitHub-Release-API).
- Setup-`.exe` ist unsigniert → SmartScreen erscheint beim ersten
  Ausführen einmalig, das wird NICHT umgangen (siehe
  [docs/RELEASE.md:55-58](../../RELEASE.md)).
- UI-Texte, Log-Zeilen und Code-Kommentare auf Deutsch, im Stil der
  bestehenden Datei (siehe `MainViewModel.cs`, `MainWindow.xaml.cs`).
- Vor jedem Commit: `dotnet build UniversalLinuxManager.csproj -c Debug`
  (0 Fehler/Warnungen) und `dotnet test ULM.Tests` (alle grün) — Standard
  dieses Projekts, siehe [docs/RELEASE.md:17-19](../../RELEASE.md).
- Spec: [docs/superpowers/specs/2026-07-20-program-self-update-auto-install-design.md](../specs/2026-07-20-program-self-update-auto-install-design.md)

---

### Task 1: `SelfUpdateService` — Varianten-Erkennung (`InstallKind`/`DetectInstallKind`)

**Files:**
- Create: `Core/Services/SelfUpdateService.cs`
- Test: `ULM.Tests/SelfUpdateServiceTests.cs`

**Interfaces:**
- Produces: `ULM.Core.Services.InstallKind` (enum: `Portable`, `Installed`),
  `ULM.Core.Services.ISelfUpdateService.DetectInstallKind(string currentExePath) : InstallKind`,
  `ULM.Core.Services.SelfUpdateService.Instance : SelfUpdateService` (Singleton).

- [ ] **Step 1: Write the failing tests**

```csharp
// ULM.Tests/SelfUpdateServiceTests.cs
using System;
using System.IO;
using ULM.Core.Services;
using Xunit;

namespace ULM.Tests;

public class SelfUpdateServiceDetectInstallKindTests
{
    [Fact]
    public void DetectInstallKind_UninstallerPresent_ReturnsInstalled()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"ulm-selfupdate-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            string exePath = Path.Combine(tempDir, "UniversalLinuxManager.exe");
            File.WriteAllText(Path.Combine(tempDir, "unins000.exe"), string.Empty);

            var kind = SelfUpdateService.Instance.DetectInstallKind(exePath);

            Assert.Equal(InstallKind.Installed, kind);
        }
        finally { Directory.Delete(tempDir, true); }
    }

    [Fact]
    public void DetectInstallKind_NoUninstaller_ReturnsPortable()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"ulm-selfupdate-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            string exePath = Path.Combine(tempDir, "UniversalLinuxManager.exe");

            var kind = SelfUpdateService.Instance.DetectInstallKind(exePath);

            Assert.Equal(InstallKind.Portable, kind);
        }
        finally { Directory.Delete(tempDir, true); }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test ULM.Tests --filter SelfUpdateServiceDetectInstallKindTests`
Expected: FAIL — Kompilierfehler, `ULM.Core.Services.SelfUpdateService`/`InstallKind` existieren noch nicht.

- [ ] **Step 3: Create `SelfUpdateService.cs` with the minimal implementation**

```csharp
// Core/Services/SelfUpdateService.cs
using System;
using System.IO;

namespace ULM.Core.Services
{
    // Welche Verteilungsform gerade läuft — bestimmt, welches Release-Asset automatisch
    // heruntergeladen und wie das Update angewendet wird (Silent-Installer vs. Self-Replace).
    public enum InstallKind { Portable, Installed }

    public interface ISelfUpdateService
    {
        InstallKind DetectInstallKind(string currentExePath);
    }

    public sealed class SelfUpdateService : ISelfUpdateService
    {
        private static readonly Lazy<SelfUpdateService> _lazy = new(() => new SelfUpdateService());
        public static SelfUpdateService Instance => _lazy.Value;
        private SelfUpdateService() { }

        // Inno Setup legt den Deinstaller standardmäßig als "unins000.exe" neben die installierte
        // .exe ({app}\unins000.exe) — dank PrivilegesRequired=lowest in installer/ULM.iss landet die
        // Installation immer unter LocalAppData\Programs, eine Registry-Abfrage ist nicht nötig.
        internal const string InstalledMarkerFileName = "unins000.exe";

        public InstallKind DetectInstallKind(string currentExePath)
        {
            string? dir = Path.GetDirectoryName(currentExePath);
            if (!string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, InstalledMarkerFileName)))
                return InstallKind.Installed;
            return InstallKind.Portable;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test ULM.Tests --filter SelfUpdateServiceDetectInstallKindTests`
Expected: PASS (2 Tests)

- [ ] **Step 5: Commit**

```bash
git add Core/Services/SelfUpdateService.cs ULM.Tests/SelfUpdateServiceTests.cs
git commit -m "feat: SelfUpdateService erkennt Installer- vs. Portable-Variante"
```

---

### Task 2: `SelfUpdateService` — Automatischer Hintergrund-Download

**Files:**
- Modify: `Core/Services/SelfUpdateService.cs`
- Test: `ULM.Tests/SelfUpdateServiceTests.cs`

**Interfaces:**
- Consumes: `ULM.Core.Services.UlmUpdateInfo` (`HasUpdate`, `LatestVersion`, `ReleaseUrl`,
  `PortableExeUrl`, `SetupExeUrl` — bereits vorhanden, `HttpService.cs:21-26`),
  `ULM.Core.Services.HttpService.Instance.DownloadAsync(string url, string destinationPath, IProgress<(int Percent, string Detail)>? progress, CancellationToken ct, Action<long>? onTotalKnown = null) : Task<bool>`
  (bereits vorhanden, `HttpService.cs:1121-1125`).
- Produces: `ULM.Core.Services.SelfUpdateService.SelectDownloadUrl(UlmUpdateInfo info, InstallKind kind) : string` (internal static),
  `ULM.Core.Services.ISelfUpdateService.DownloadUpdateAsync(UlmUpdateInfo info, InstallKind kind, string tempDir, IProgress<(int Percent, string Detail)>? progress, CancellationToken ct) : Task<string?>`.

- [ ] **Step 1: Write the failing test**

```csharp
// Anhängen an ULM.Tests/SelfUpdateServiceTests.cs
public class SelfUpdateServiceSelectDownloadUrlTests
{
    [Fact]
    public void SelectDownloadUrl_Installed_ReturnsSetupUrl()
    {
        var info = new UlmUpdateInfo(true, "3.0.0", "https://example.test/release",
            "https://example.test/portable.exe", "https://example.test/setup.exe");

        string url = SelfUpdateService.SelectDownloadUrl(info, InstallKind.Installed);

        Assert.Equal("https://example.test/setup.exe", url);
    }

    [Fact]
    public void SelectDownloadUrl_Portable_ReturnsPortableUrl()
    {
        var info = new UlmUpdateInfo(true, "3.0.0", "https://example.test/release",
            "https://example.test/portable.exe", "https://example.test/setup.exe");

        string url = SelfUpdateService.SelectDownloadUrl(info, InstallKind.Portable);

        Assert.Equal("https://example.test/portable.exe", url);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ULM.Tests --filter SelfUpdateServiceSelectDownloadUrlTests`
Expected: FAIL — Kompilierfehler, `SelfUpdateService.SelectDownloadUrl` existiert noch nicht.

- [ ] **Step 3: Add `SelectDownloadUrl` and `DownloadUpdateAsync` to `SelfUpdateService.cs`**

Interface `ISelfUpdateService` erweitern:

```csharp
    public interface ISelfUpdateService
    {
        InstallKind DetectInstallKind(string currentExePath);
        Task<string?> DownloadUpdateAsync(UlmUpdateInfo info, InstallKind kind, string tempDir,
            IProgress<(int Percent, string Detail)>? progress, CancellationToken ct);
    }
```

Klasse erweitern (nach `DetectInstallKind`):

```csharp
        // Wählt die zur erkannten Verteilungsform passende Asset-URL — reine Logik, testbar ohne
        // Netzwerk. Leer, falls das jeweilige Asset im Release fehlt (siehe UlmUpdateInfo-Doku).
        internal static string SelectDownloadUrl(UlmUpdateInfo info, InstallKind kind)
            => kind == InstallKind.Installed ? info.SetupExeUrl : info.PortableExeUrl;

        // Lädt automatisch die zur Verteilungsform passende Datei nach tempDir herunter (nutzt das
        // bestehende HttpService.DownloadAsync, kein neuer Download-Code). Liefert null, wenn kein
        // passendes Asset existiert oder der Download fehlschlägt — der Aufrufer fällt dann auf den
        // bestehenden manuellen UpdateDownloadDialog zurück.
        public async Task<string?> DownloadUpdateAsync(UlmUpdateInfo info, InstallKind kind, string tempDir,
            IProgress<(int Percent, string Detail)>? progress, CancellationToken ct)
        {
            string url = SelectDownloadUrl(info, kind);
            if (string.IsNullOrWhiteSpace(url)) return null;
            Directory.CreateDirectory(tempDir);
            string fileName = Path.GetFileName(new Uri(url).AbsolutePath);
            string dest = Path.Combine(tempDir, fileName);
            bool ok = await HttpService.Instance.DownloadAsync(url, dest, progress, ct).ConfigureAwait(false);
            return ok ? dest : null;
        }
```

Neue `using`-Zeilen am Dateianfang ergänzen:

```csharp
using System.Threading;
using System.Threading.Tasks;
```

`DownloadUpdateAsync` selbst bekommt keinen eigenen Test (führt einen echten
`HttpClient`-Aufruf über den `HttpService`-Singleton aus — genau wie
`HttpService.DownloadAsync` selbst im Projekt nirgends direkt unit-getestet
wird, nur seine reinen Helfer wie `SelectDownloadUrl`/`MatchUlmReleaseAssets`).

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ULM.Tests --filter SelfUpdateServiceSelectDownloadUrlTests`
Expected: PASS (2 Tests)

- [ ] **Step 5: Run the full test suite to confirm nothing else broke**

Run: `dotnet test ULM.Tests`
Expected: PASS (alle Tests, inkl. der aus Task 1)

- [ ] **Step 6: Commit**

```bash
git add Core/Services/SelfUpdateService.cs ULM.Tests/SelfUpdateServiceTests.cs
git commit -m "feat: SelfUpdateService laedt Updates automatisch im Hintergrund herunter"
```

---

### Task 3: `SelfUpdateService` — Installieren/Ersetzen + Neustart

**Files:**
- Modify: `Core/Services/SelfUpdateService.cs`
- Test: `ULM.Tests/SelfUpdateServiceTests.cs`

**Interfaces:**
- Produces: `ULM.Core.Services.SelfUpdateService.BuildApplyScript(int processId, string newExePath, string targetExePath) : string` (internal static),
  `ULM.Core.Services.ISelfUpdateService.ApplyUpdateAndRestart(string downloadedFilePath, InstallKind kind, string currentExePath) : void`.

- [ ] **Step 1: Write the failing test**

```csharp
// Anhängen an ULM.Tests/SelfUpdateServiceTests.cs
public class SelfUpdateServiceBuildApplyScriptTests
{
    [Fact]
    public void BuildApplyScript_ContainsProcessIdAndBothPaths()
    {
        string script = SelfUpdateService.BuildApplyScript(4242, @"C:\Temp\ULM_Update\new.exe", @"C:\Tools\ULM\UniversalLinuxManager.exe");

        Assert.Contains("-Id 4242", script);
        Assert.Contains(@"C:\Temp\ULM_Update\new.exe", script);
        Assert.Contains(@"C:\Tools\ULM\UniversalLinuxManager.exe", script);
        Assert.Contains("Start-Process", script);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ULM.Tests --filter SelfUpdateServiceBuildApplyScriptTests`
Expected: FAIL — Kompilierfehler, `SelfUpdateService.BuildApplyScript` existiert noch nicht.

- [ ] **Step 3: Add `BuildApplyScript` and `ApplyUpdateAndRestart` to `SelfUpdateService.cs`**

Interface `ISelfUpdateService` erweitern:

```csharp
    public interface ISelfUpdateService
    {
        InstallKind DetectInstallKind(string currentExePath);
        Task<string?> DownloadUpdateAsync(UlmUpdateInfo info, InstallKind kind, string tempDir,
            IProgress<(int Percent, string Detail)>? progress, CancellationToken ct);
        void ApplyUpdateAndRestart(string downloadedFilePath, InstallKind kind, string currentExePath);
    }
```

Klasse erweitern (nach `DownloadUpdateAsync`):

```csharp
        // Installer-Variante: startet das heruntergeladene Setup silent — installer/ULM.iss hat
        // CloseApplications=yes/RestartApplications=yes, das schließt laufende ULM-Instanzen
        // zuverlässig und startet sie nach der Installation automatisch neu. SmartScreen erscheint
        // hier weiterhin einmalig (unsigniertes Setup.exe) — das wird bewusst NICHT umgangen.
        //
        // Portable-Variante: eine laufende .exe kann sich unter Windows nicht selbst überschreiben,
        // daher übernimmt ein per PowerShell gestartetes Hilfsskript das Kopieren nach Prozessende.
        public void ApplyUpdateAndRestart(string downloadedFilePath, InstallKind kind, string currentExePath)
        {
            if (kind == InstallKind.Installed)
            {
                Process.Start(new ProcessStartInfo(downloadedFilePath, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART")
                { UseShellExecute = true });
            }
            else
            {
                string scriptPath = Path.Combine(Path.GetDirectoryName(downloadedFilePath)!, "apply.ps1");
                string script = BuildApplyScript(Environment.ProcessId, downloadedFilePath, currentExePath);
                File.WriteAllText(scriptPath, script);
                Process.Start(new ProcessStartInfo("powershell.exe",
                    $"-WindowStyle Hidden -ExecutionPolicy Bypass -File \"{scriptPath}\"")
                { UseShellExecute = false, CreateNoWindow = true });
            }
            System.Windows.Application.Current.Shutdown();
        }

        // Reine String-Erzeugung, testbar ohne Prozess-/Dateisystem-Zugriff. Wartet auf Prozessende,
        // kopiert die neue Datei über die alte (Retry-Loop, da der Datei-Lock erst kurz nach
        // Prozessende freigegeben wird), startet ULM vom ursprünglichen Pfad neu und räumt auf.
        // Bricht der Kopierversuch dauerhaft ab (Datei weiterhin gesperrt), bleibt die alte .exe
        // unangetastet — kein Datenverlust, der Check läuft beim nächsten Start erneut.
        internal static string BuildApplyScript(int processId, string newExePath, string targetExePath) =>
            $"while (Get-Process -Id {processId} -ErrorAction SilentlyContinue) {{ Start-Sleep -Milliseconds 300 }}\n" +
            "for ($i = 0; $i -lt 20; $i++) {\n" +
            $"    try {{ Copy-Item -Path '{newExePath}' -Destination '{targetExePath}' -Force; break }}\n" +
            "    catch { Start-Sleep -Milliseconds 500 }\n" +
            "}\n" +
            $"Start-Process -FilePath '{targetExePath}'\n" +
            $"Remove-Item -Path '{newExePath}' -ErrorAction SilentlyContinue\n" +
            "Remove-Item -Path $PSCommandPath -ErrorAction SilentlyContinue\n";
```

Neue `using`-Zeile am Dateianfang ergänzen:

```csharp
using System.Diagnostics;
```

`ApplyUpdateAndRestart` selbst bekommt keinen eigenen Test (startet echte
Prozesse und beendet die Anwendung — nicht sinnvoll unit-testbar, analog zu
den bestehenden `Process.Start(...)`-Aufrufen in `MainWindow.xaml.cs`, die
ebenfalls ungetestet sind). Verifikation erfolgt manuell in Task 6.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ULM.Tests --filter SelfUpdateServiceBuildApplyScriptTests`
Expected: PASS

- [ ] **Step 5: Run the full test suite to confirm nothing else broke**

Run: `dotnet test ULM.Tests`
Expected: PASS (alle Tests)

- [ ] **Step 6: Commit**

```bash
git add Core/Services/SelfUpdateService.cs ULM.Tests/SelfUpdateServiceTests.cs
git commit -m "feat: SelfUpdateService installiert/ersetzt Update automatisch und startet neu"
```

---

### Task 4: `installer/ULM.iss` — Inno Setup schließt/startet laufende Instanz automatisch

**Files:**
- Modify: `installer/ULM.iss:18-42` (`[Setup]`-Sektion)

**Interfaces:**
- Consumes: nichts Neues aus vorherigen Tasks.
- Produces: nichts, das andere Tasks konsumieren — reine Installer-Konfiguration.

- [ ] **Step 1: `[Setup]`-Sektion um `AppMutex`/`CloseApplications`/`RestartApplications` ergänzen**

In `installer/ULM.iss`, nach der bestehenden Zeile `PrivilegesRequiredOverridesAllowed=dialog`
(Zeile 42) einfügen:

```ini
; Automatisches Selbst-Update (SelfUpdateService.ApplyUpdateAndRestart): schließt beim Silent-Install
; zuverlässig alle laufenden ULM-Instanzen (Sicherheitsnetz, falls die App sich nicht rechtzeitig
; selbst beendet hat) und startet sie danach automatisch neu — auch im /VERYSILENT-Modus.
AppMutex=UniversalLinuxManagerSingleInstance
CloseApplications=yes
RestartApplications=yes
```

- [ ] **Step 2: Diff sichten und, falls lokal installiert, mit ISCC validieren**

Run (falls `iscc` lokal vorhanden — Standard in diesem Projekt ist, dass ISCC
nur auf dem `windows-latest`-GitHub-Actions-Runner läuft, siehe
[docs/RELEASE.md:99-102](../../RELEASE.md); ist `iscc` lokal nicht
installiert, reicht eine Sichtprüfung des Diffs, der Release-Workflow
validiert das Skript beim nächsten Tag-Push ohnehin):

```bash
iscc installer\ULM.iss /DAppVersion=0.0.0-test
```

Expected: Compile erfolgreich (keine Syntaxfehler durch die drei neuen Zeilen).

- [ ] **Step 3: Commit**

```bash
git add installer/ULM.iss
git commit -m "feat: Installer schliesst/startet laufende ULM-Instanz beim Silent-Update automatisch"
```

---

### Task 5: `MainViewModel` — `UpdateBannerState`

**Files:**
- Modify: `ViewModels/MainViewModel.cs:138-155`
- Test: `ULM.Tests/MainViewModelUpdateBannerTests.cs`

**Interfaces:**
- Consumes: `ULM.Core.Services.UlmUpdateInfo` (bereits vorhanden).
- Produces: `ULM.ViewModels.UpdateBannerState` (enum: `Available`, `Downloading`, `ReadyToInstall`),
  `MainViewModel.UpdateBannerState : UpdateBannerState`,
  `MainViewModel.UpdateBannerButtonText : string`, `MainViewModel.UpdateBannerButtonEnabled : bool`,
  `MainViewModel.DownloadedUpdatePath : string?`,
  `MainViewModel.SetUpdateDownloading() : void`,
  `MainViewModel.SetUpdateReadyToInstall(string downloadedFilePath) : void`.
  (`SetAvailableUpdate(UlmUpdateInfo info)` und `DismissUpdateBanner()` bleiben bestehen, ihr
  Verhalten wird erweitert.)

- [ ] **Step 1: Write the failing test**

```csharp
// ULM.Tests/MainViewModelUpdateBannerTests.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Threading;
using ULM.Core.Models;
using ULM.Core.Services;
using ULM.ViewModels;
using Xunit;

namespace ULM.Tests;

public class MainViewModelUpdateBannerTests
{
    private sealed class FakeHttpService : IHttpService { public string? GitHubToken { get; set; } }
    private sealed class FakeUsbService : IUsbService
    {
        public List<UsbDrive> ListRemovableDrives() => new();
        public Task<(List<UsbService.StickIso> Found, List<UsbService.StickIso> Incomplete)> ScanStickVerifiedAsync(string letter, IReadOnlyList<IsoEntry> entries)
            => Task.FromResult((new List<UsbService.StickIso>(), new List<UsbService.StickIso>()));
    }
    private sealed class FakeIsoDatabaseService : IIsoDatabaseService
    {
        private readonly List<IsoEntry> _entries = new();
        public IReadOnlyList<IsoEntry> Entries => _entries;
        public int Count => _entries.Count;
        public void Load() { }
        public void Save() { }
        public void SaveFilenames() { }
        public void Add(IsoEntry entry) => _entries.Add(entry);
        public void Remove(int index) => _entries.RemoveAt(index);
        public void SaveExpectedSize(IsoEntry entry, long bytes) { }
    }

    private static MainViewModel NewVm() =>
        new(Dispatcher.CurrentDispatcher, new FakeHttpService(), new FakeUsbService(), new FakeIsoDatabaseService());

    [Fact]
    public void SetAvailableUpdate_SetsAvailableStateAndEnabledButton()
    {
        var vm = NewVm();
        var info = new UlmUpdateInfo(true, "3.0.0", "https://example.test", "https://example.test/p.exe", "https://example.test/s.exe");

        vm.SetAvailableUpdate(info);

        Assert.Equal(UpdateBannerState.Available, vm.UpdateBannerState);
        Assert.True(vm.UpdateBannerVisible);
        Assert.True(vm.UpdateBannerButtonEnabled);
    }

    [Fact]
    public void SetUpdateDownloading_SetsDownloadingStateAndDisablesButton()
    {
        var vm = NewVm();
        vm.SetAvailableUpdate(new UlmUpdateInfo(true, "3.0.0", "https://example.test", "https://example.test/p.exe", "https://example.test/s.exe"));

        vm.SetUpdateDownloading();

        Assert.Equal(UpdateBannerState.Downloading, vm.UpdateBannerState);
        Assert.False(vm.UpdateBannerButtonEnabled);
    }

    [Fact]
    public void SetUpdateReadyToInstall_SetsReadyStateAndStoresPath()
    {
        var vm = NewVm();
        vm.SetAvailableUpdate(new UlmUpdateInfo(true, "3.0.0", "https://example.test", "https://example.test/p.exe", "https://example.test/s.exe"));
        vm.SetUpdateDownloading();

        vm.SetUpdateReadyToInstall(@"C:\Temp\ULM_Update\new.exe");

        Assert.Equal(UpdateBannerState.ReadyToInstall, vm.UpdateBannerState);
        Assert.True(vm.UpdateBannerButtonEnabled);
        Assert.Equal(@"C:\Temp\ULM_Update\new.exe", vm.DownloadedUpdatePath);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test ULM.Tests --filter MainViewModelUpdateBannerTests`
Expected: FAIL — Kompilierfehler, `UpdateBannerState`/`SetUpdateDownloading`/`SetUpdateReadyToInstall`/`DownloadedUpdatePath` existieren noch nicht.

- [ ] **Step 3: Replace the update-banner block in `MainViewModel.cs`**

Aktueller Block (`ViewModels/MainViewModel.cs:138-155`):

```csharp
        // Selbst-Update-Banner: nur sichtbar, wenn CheckForUlmUpdateAsync eine neuere Programmversion
        // gefunden hat (SetAvailableUpdate wird vom MainWindow nur dann aufgerufen).
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

Ersetzen durch:

```csharp
        // Selbst-Update-Banner: durchläuft Available -> Downloading -> ReadyToInstall, sobald
        // CheckForUlmUpdateAsync eine neuere Programmversion gefunden hat (SetAvailableUpdate wird
        // vom MainWindow nur dann aufgerufen). Available = automatischer Download läuft noch nicht
        // oder ist fehlgeschlagen (Button öffnet dann den manuellen Fallback-Dialog); Downloading =
        // automatischer Hintergrund-Download läuft (Button deaktiviert); ReadyToInstall = Datei liegt
        // bereit, Button installiert/ersetzt automatisch und startet ULM neu.
        private UlmUpdateInfo? _availableUpdate;
        public UlmUpdateInfo? AvailableUpdate => _availableUpdate;
        private bool _updateBannerVisible;
        public bool UpdateBannerVisible { get => _updateBannerVisible; private set => SetField(ref _updateBannerVisible, value); }
        private string _updateBannerText = string.Empty;
        public string UpdateBannerText { get => _updateBannerText; private set => SetField(ref _updateBannerText, value); }
        private UpdateBannerState _updateBannerState = UpdateBannerState.Available;
        public UpdateBannerState UpdateBannerState { get => _updateBannerState; private set => SetField(ref _updateBannerState, value); }
        private string _updateBannerButtonText = "⬇ Herunterladen …";
        public string UpdateBannerButtonText { get => _updateBannerButtonText; private set => SetField(ref _updateBannerButtonText, value); }
        private bool _updateBannerButtonEnabled = true;
        public bool UpdateBannerButtonEnabled { get => _updateBannerButtonEnabled; private set => SetField(ref _updateBannerButtonEnabled, value); }
        private string? _downloadedUpdatePath;
        public string? DownloadedUpdatePath => _downloadedUpdatePath;

        // Vom MainWindow nach erfolgreichem Update-Check aufgerufen — macht das Banner sichtbar.
        // Auch der Fehler-Fallback (automatischer Download schlägt fehl) ruft dies erneut auf, um
        // zurück in den Available-Zustand mit aktivem "Herunterladen"-Button zu wechseln.
        public void SetAvailableUpdate(UlmUpdateInfo info)
        {
            _availableUpdate = info;
            UpdateBannerState = UpdateBannerState.Available;
            UpdateBannerText = $"🆕 Neue Version verfügbar: v{info.LatestVersion} (installiert: v{Constants.AppVersion})";
            UpdateBannerButtonText = "⬇ Herunterladen …";
            UpdateBannerButtonEnabled = true;
            UpdateBannerVisible = true;
        }
        // Vom MainWindow aufgerufen, sobald der automatische Hintergrund-Download startet.
        public void SetUpdateDownloading()
        {
            UpdateBannerState = UpdateBannerState.Downloading;
            UpdateBannerText = "⬇ Update wird heruntergeladen …";
            UpdateBannerButtonText = "⬇ Wird heruntergeladen …";
            UpdateBannerButtonEnabled = false;
        }
        // Vom MainWindow aufgerufen, sobald der Download fertig und die Datei bereit zur Installation ist.
        public void SetUpdateReadyToInstall(string downloadedFilePath)
        {
            _downloadedUpdatePath = downloadedFilePath;
            UpdateBannerState = UpdateBannerState.ReadyToInstall;
            UpdateBannerText = $"✅ Update bereit — v{_availableUpdate?.LatestVersion}";
            UpdateBannerButtonText = "✅ Jetzt installieren & neu starten";
            UpdateBannerButtonEnabled = true;
        }
        // Blendet das Banner nur für die laufende Sitzung aus (kein persistenter Zustand).
        public void DismissUpdateBanner() => UpdateBannerVisible = false;
```

`UpdateBannerState`-Enum ganz oben in der Datei ergänzen, direkt vor
`public sealed class MainViewModel : ViewModelBase` (Zeile 20):

```csharp
    // Zustand des Selbst-Update-Banners, siehe MainViewModel.SetAvailableUpdate/
    // SetUpdateDownloading/SetUpdateReadyToInstall.
    public enum UpdateBannerState { Available, Downloading, ReadyToInstall }

```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test ULM.Tests --filter MainViewModelUpdateBannerTests`
Expected: PASS (3 Tests)

- [ ] **Step 5: Run the full test suite to confirm nothing else broke**

Run: `dotnet test ULM.Tests`
Expected: PASS (alle Tests)

- [ ] **Step 6: Commit**

```bash
git add ViewModels/MainViewModel.cs ULM.Tests/MainViewModelUpdateBannerTests.cs
git commit -m "feat: MainViewModel bekommt UpdateBannerState fuer automatischen Update-Flow"
```

---

### Task 6: Banner-UI + Verdrahtung in `MainWindow`

**Files:**
- Modify: `Views/MainWindow.xaml:308-310`
- Modify: `Views/MainWindow.xaml.cs:201-270`

**Interfaces:**
- Consumes: `ULM.Core.Services.SelfUpdateService.Instance` (Task 1-3),
  `ULM.ViewModels.MainViewModel.UpdateBannerState/SetUpdateDownloading/SetUpdateReadyToInstall/DownloadedUpdatePath`
  (Task 5).
- Produces: nichts, das weitere Tasks konsumieren — Endpunkt der Kette.

Kein automatisierter Test möglich (XAML-Bindings und `Process.Start`-Aufrufe
sind im Projekt nirgends automatisiert getestet, siehe bestehendes Muster in
`MainWindow.xaml.cs`). Verifikation über Build + manuellen Smoke-Test in
Schritt 6.

- [ ] **Step 1: Banner-Button in `MainWindow.xaml` an den neuen Zustand binden**

Aktuell (`Views/MainWindow.xaml:308-310`):

```xml
                    <Button Grid.Column="1" x:Name="BtnUpdateDownload" Content="⬇ Herunterladen …"
                            Style="{DynamicResource BtnSuccess}" Margin="0,0,8,0"
                            Click="BtnUpdateDownload_Click"/>
```

Ersetzen durch:

```xml
                    <Button Grid.Column="1" x:Name="BtnUpdateDownload" Content="{Binding UpdateBannerButtonText}"
                            IsEnabled="{Binding UpdateBannerButtonEnabled}"
                            Style="{DynamicResource BtnSuccess}" Margin="0,0,8,0"
                            Click="BtnUpdateDownload_Click"/>
```

- [ ] **Step 2: `CheckUlmUpdateAsync` in `MainWindow.xaml.cs` um automatischen Download erweitern**

Aktuell (`Views/MainWindow.xaml.cs:223-236`):

```csharp
        /// <summary>
        /// Rein informativer Hinweis, falls eine neuere ULM-Version auf GitHub verfügbar ist —
        /// läuft unabhängig im Hintergrund (fire-and-forget), blockiert nichts und unterbricht den
        /// Nutzer nicht mit einem Dialog. Ergebnis erscheint nur als Protokollzeile, analog zu den
        /// "🆕 neue Version gefunden"-Meldungen der Distro-Versionschecks.
        /// </summary>
        private async Task CheckUlmUpdateAsync()
        {
            var info = await HttpService.Instance.CheckForUlmUpdateAsync(Constants.AppVersion).ConfigureAwait(true);
            if (!info.HasUpdate) return;
            AppendLog($"🆕 Neue ULM-Version verfügbar: v{info.LatestVersion} (aktuell installiert: v{Constants.AppVersion})");
            if (!string.IsNullOrWhiteSpace(info.ReleaseUrl)) AppendLog($"   {info.ReleaseUrl}");
            _vm.SetAvailableUpdate(info);
        }
```

Ersetzen durch:

```csharp
        /// <summary>
        /// Läuft unabhängig im Hintergrund (fire-and-forget), blockiert nichts und unterbricht den
        /// Nutzer nicht. Findet CheckForUlmUpdateAsync eine neuere Version, lädt ULM die zur
        /// erkannten Installationsart passende Datei automatisch herunter (SelfUpdateService) und
        /// wechselt das Banner in den ReadyToInstall-Zustand. Schlägt der Download fehl, bleibt der
        /// bestehende manuelle UpdateDownloadDialog als Fallback erreichbar (Available-Zustand).
        /// </summary>
        private async Task CheckUlmUpdateAsync()
        {
            var info = await HttpService.Instance.CheckForUlmUpdateAsync(Constants.AppVersion).ConfigureAwait(true);
            if (!info.HasUpdate) return;
            AppendLog($"🆕 Neue ULM-Version verfügbar: v{info.LatestVersion} (aktuell installiert: v{Constants.AppVersion})");
            if (!string.IsNullOrWhiteSpace(info.ReleaseUrl)) AppendLog($"   {info.ReleaseUrl}");
            _vm.SetAvailableUpdate(info);

            _updateCurrentExePath = GetCurrentExePath();
            _updateInstallKind    = SelfUpdateService.Instance.DetectInstallKind(_updateCurrentExePath);
            _vm.SetUpdateDownloading();

            string tempDir = Path.Combine(Path.GetTempPath(), "ULM_Update");
            string? downloaded = null;
            try
            {
                downloaded = await SelfUpdateService.Instance
                    .DownloadUpdateAsync(info, _updateInstallKind, tempDir, null, System.Threading.CancellationToken.None)
                    .ConfigureAwait(true);
            }
            catch (Exception ex) { AppendLog($"⚠ Automatischer Update-Download fehlgeschlagen: {ex.Message}"); }

            if (string.IsNullOrEmpty(downloaded))
            {
                AppendLog("⚠ Automatischer Update-Download fehlgeschlagen — manueller Download bleibt über den Banner-Button möglich.");
                _vm.SetAvailableUpdate(info);
                return;
            }
            AppendLog($"✅ Update heruntergeladen: {downloaded}");
            _vm.SetUpdateReadyToInstall(downloaded);
        }

        // Bevorzugt Environment.ProcessPath (zuverlässig bei Single-File-Publish, siehe
        // UniversalLinuxManager.csproj PublishSingleFile=true) mit Process.MainModule als Fallback.
        private static string GetCurrentExePath() =>
            Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName;
```

Neue Felder in der Klasse ergänzen, direkt nach `_lastDriveSignatureUi`
(`Views/MainWindow.xaml.cs:29`):

```csharp
        private string     _updateCurrentExePath = string.Empty;
        private InstallKind _updateInstallKind    = InstallKind.Portable;
```

- [ ] **Step 3: `BtnUpdateDownload_Click` je nach Banner-Zustand verzweigen**

Aktuell (`Views/MainWindow.xaml.cs:241-270`):

```csharp
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

Ersetzen durch:

```csharp
        private async void BtnUpdateDownload_Click(object sender, RoutedEventArgs e)
        {
            switch (_vm.UpdateBannerState)
            {
                case UpdateBannerState.ReadyToInstall:
                    string? downloaded = _vm.DownloadedUpdatePath;
                    if (string.IsNullOrEmpty(downloaded)) return;
                    SelfUpdateService.Instance.ApplyUpdateAndRestart(downloaded, _updateInstallKind, GetCurrentExePath());
                    return;
                case UpdateBannerState.Downloading:
                    // Button ist waehrend Downloading deaktiviert (IsEnabled-Binding) — hier zur
                    // Sicherheit trotzdem ignorieren, falls der Klick knapp vor der Zustandsaenderung landet.
                    return;
                default:
                    await ManualUpdateDownloadFallbackAsync();
                    return;
            }
        }

        // Fallback, falls der automatische Hintergrund-Download (CheckUlmUpdateAsync) fehlgeschlagen
        // ist: unveraendertes bisheriges Verhalten — Nutzer waehlt manuell Portable/Setup, ULM laedt
        // herunter und oeffnet den Ziel-Ordner im Explorer, Ausfuehren macht der Nutzer selbst.
        private async Task ManualUpdateDownloadFallbackAsync()
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

- [ ] **Step 4: Build**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: 0 Fehler, 0 Warnungen.

- [ ] **Step 5: Vollständige Test-Suite laufen lassen**

Run: `dotnet test ULM.Tests`
Expected: PASS (alle Tests, inkl. Task 1/2/3/5).

- [ ] **Step 6: Manueller Smoke-Test**

`Constants.AppVersion` (in `UniversalLinuxManager.csproj`, `<Version>`) temporär
auf eine niedrigere Nummer als den aktuell veröffentlichten GitHub-Release
setzen (z.B. `0.0.1`), ULM starten:

- Banner erscheint mit "🆕 Neue Version verfügbar …", wechselt kurz danach zu
  "⬇ Update wird heruntergeladen …" (Button deaktiviert), dann zu
  "✅ Update bereit — Jetzt installieren & neu starten" (Button aktiv).
- Klick auf den Button: bei portabler `.exe` (kein `unins000.exe` daneben)
  beendet sich ULM, die `.exe` wird ersetzt, ULM startet automatisch neu mit
  der neuen Version. Bei installierter Variante (`unins000.exe` vorhanden)
  erscheint einmalig SmartScreen, danach läuft die Installation silent durch
  und ULM startet neu.
- Danach `<Version>` in `UniversalLinuxManager.csproj` wieder auf den
  ursprünglichen Wert zurücksetzen (NICHT mitcommitten).

- [ ] **Step 7: Commit**

```bash
git add Views/MainWindow.xaml Views/MainWindow.xaml.cs
git commit -m "feat: Update-Banner installiert/ersetzt automatisch und startet ULM neu"
```

## Self-Review

**Spec coverage:**
- Ziel 1 (automatischer Hintergrund-Download) → Task 2 + Task 6 Step 2.
- Ziel 2 (Varianten-Erkennung) → Task 1 + Task 6 Step 2.
- Ziel 3 (Banner "Jetzt installieren & neu starten") → Task 3, Task 5, Task 6 Step 1+3.
- Installer-Flow (`CloseApplications`/`RestartApplications`, `/VERYSILENT`) → Task 3 Step 3, Task 4.
- Portable-Self-Replace-Skript → Task 3 Step 3 (`BuildApplyScript`).
- Fehlerfälle-Tabelle der Spec → Task 6 Step 2 (Download-Fehler-Fallback auf `SetAvailableUpdate`),
  Task 3 (Self-Replace-Skript bricht ohne Kopieren ab bei dauerhaftem Lock).
- Nicht-Ziele (kein periodischer Re-Check, kein Skip-Version, keine Checksummen) → nirgends
  implementiert, wie gefordert; in Global Constraints festgehalten.

**Placeholder-Scan:** Keine TBD/TODO, jeder Code-Schritt zeigt vollständigen Code.

**Type-Konsistenz geprüft:** `InstallKind`, `UlmUpdateInfo`, `UpdateBannerState`,
`SelfUpdateService.Instance`, `DetectInstallKind`, `SelectDownloadUrl`, `DownloadUpdateAsync`,
`ApplyUpdateAndRestart`, `BuildApplyScript` — Signaturen in Interfaces-Blöcken und Code-Steps stimmen
über alle sechs Tasks hinweg überein.
