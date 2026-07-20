# Programm-Selbst-Update: automatischer Download + Installation — Design

## Kontext

Der bestehende Selbst-Update-Banner ([2026-07-16-program-self-update-banner-design.md](2026-07-16-program-self-update-banner-design.md),
umgesetzt) prüft beim Start auf eine neuere ULM-Version, zeigt ein Banner
und lässt den Nutzer manuell zwischen portabler `.exe` und Setup-`.exe`
wählen. ULM lädt danach nur herunter und öffnet den Ziel-Ordner im
Explorer — Ausführen/Installieren macht der Nutzer komplett selbst.

**Wunsch:** Der Update-Flow soll näher an das bringen, was bei
Programm-Aktualisierungen sonst üblich ist (z.B. VS Code/Chrome) —
automatischer Hintergrund-Download plus automatisches
Installieren/Ersetzen samt Neustart, ohne dass der Nutzer manuell eine
heruntergeladene Datei suchen und starten muss.

## Wichtige Einschränkung

`installer/ULM.iss` baut ein **unsigniertes** Setup-`.exe`
([docs/RELEASE.md:55-58](../../RELEASE.md)) → Windows SmartScreen blockiert
es beim ersten Ausführen mit "Unbekannter Herausgeber", der Nutzer muss
einmalig manuell auf "Weitere Informationen → Trotzdem ausführen" klicken.
Ein lückenlos automatisches Update wie bei signierten Programmen ist damit
nicht erreichbar — SmartScreen wird nicht umgangen. Dieser eine Klick
bleibt bei der Installer-Variante bestehen.

## Ziel

1. Sobald ein Update erkannt ist, lädt ULM die passende Datei automatisch
   im Hintergrund herunter (kein manueller Auswahl-Dialog mehr nötig).
2. ULM erkennt selbst, ob es aktuell als installierte Version (per
   Setup-Installer) oder als portable `.exe` läuft, und wählt automatisch
   die passende Asset-URL.
3. Nach dem Download bietet das Banner "Jetzt installieren & neu starten"
   an — Klick installiert/ersetzt automatisch und startet ULM in der
   neuen Version neu.

## Nicht-Ziele

- Kein periodischer Re-Check während der Laufzeit — der bestehende
  einmalige Check beim Programmstart bleibt wie er ist.
- Kein "Diese Version überspringen" — der bestehende Schließen-Button (X)
  blendet das Banner weiterhin nur für die laufende Sitzung aus.
- Keine SHA-256-/Signatur-Prüfung der heruntergeladenen Datei — der
  Download läuft bereits über TLS direkt von der offiziellen
  GitHub-Release-API, dieselbe Vertrauensbasis wie beim bisherigen
  manuellen Download. Echte Checksummen wären eine separate Erweiterung
  der Release-Pipeline (`build-release.sh`/`release.yml`).
- Kein Umgehen von SmartScreen (siehe oben).

## Technischer Entwurf

### 1. Neuer `SelfUpdateService` (`Core/Services/SelfUpdateService.cs`)

Kapselt Varianten-Erkennung, Hintergrund-Download und
Installieren/Ersetzen. Wird von `MainWindow.CheckUlmUpdateAsync()`
anstelle des heutigen `UpdateDownloadDialog`-Aufrufs genutzt.

```csharp
public enum InstallKind { Portable, Installed }

public interface ISelfUpdateService
{
    InstallKind DetectInstallKind();
    Task<string> DownloadUpdateAsync(UlmUpdateInfo info, InstallKind kind,
        IProgress<double>? progress, CancellationToken ct);
    void ApplyUpdateAndRestart(string downloadedFilePath, InstallKind kind);
}
```

### 2. Varianten-Erkennung

`DetectInstallKind()` prüft, ob neben der laufenden `.exe`
(`Process.GetCurrentProcess().MainModule!.FileName`) eine `unins000.exe`
liegt — Standard-Ablageort des Inno-Setup-Deinstallers. Dank
`PrivilegesRequired=lowest` in `ULM.iss` landet die Installation immer
unter `LocalAppData\Programs`, keine Registry-Abfrage nötig.

- Vorhanden → `InstallKind.Installed` → `info.SetupExeUrl` verwenden.
- Nicht vorhanden → `InstallKind.Portable` → `info.PortableExeUrl`
  verwenden.

### 3. Automatischer Hintergrund-Download

`DownloadUpdateAsync` lädt die per `DetectInstallKind()` gewählte URL
nach `%TEMP%\ULM_Update\` — über das bestehende
`HttpService.Instance.DownloadAsync` (kein neuer Download-Code). Banner
zeigt währenddessen "⬇ Update wird heruntergeladen …".

Schlägt der Download fehl (Exception oder `DownloadAsync` liefert
`false`): Fallback auf das **heutige Verhalten** — Banner
"Update verfügbar" bleibt stehen, `UpdateDownloadDialog` bleibt als
manueller Weg erreichbar (Retry-Möglichkeit, keine automatische
Installation eines unvollständigen Downloads).

### 4. Installieren — Setup-Installer-Variante

`ApplyUpdateAndRestart` bei `InstallKind.Installed`:

```csharp
Process.Start(setupExePath, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART");
Application.Current.Shutdown();
```

`installer/ULM.iss` `[Setup]`-Sektion erhält zusätzlich:

```ini
AppMutex=UniversalLinuxManagerSingleInstance
CloseApplications=yes
RestartApplications=yes
```

Sicherheitsnetz von Inno Setup selbst: schließt alle laufenden
ULM-Instanzen zuverlässig (auch falls `Shutdown()` aus irgendeinem Grund
verzögert greift) und startet sie nach der Installation automatisch neu.
(`/NORESTART` und `RestartApplications=yes` betreffen unterschiedliche
Dinge und stehen sich nicht im Weg: `/NORESTART` unterdrückt nur einen
eventuellen Windows-**System**-Neustart, den dieser Installer ohnehin nie
braucht; `RestartApplications` startet die zuvor geschlossene **ULM-App**
neu.)
SmartScreen erscheint hier weiterhin einmalig (siehe oben) — technisch
nicht vermeidbar, das ist der einzige verbleibende manuelle Schritt.

### 5. Installieren — Portable-Variante (Self-Replace-Helper)

Eine laufende `.exe` kann sich unter Windows nicht selbst überschreiben.
`ApplyUpdateAndRestart` bei `InstallKind.Portable` schreibt ein kleines
PowerShell-Skript nach `%TEMP%\ULM_Update\apply.ps1`:

```powershell
param($Pid, $NewExe, $TargetExe)
while (Get-Process -Id $Pid -ErrorAction SilentlyContinue) { Start-Sleep -Milliseconds 300 }
for ($i = 0; $i -lt 20; $i++) {
    try { Copy-Item -Path $NewExe -Destination $TargetExe -Force; break }
    catch { Start-Sleep -Milliseconds 500 }
}
Start-Process -FilePath $TargetExe
Remove-Item -Path $NewExe -ErrorAction SilentlyContinue
Remove-Item -Path $PSCommandPath -ErrorAction SilentlyContinue
```

ULM startet es unsichtbar/detached:

```csharp
Process.Start(new ProcessStartInfo("powershell.exe",
    $"-WindowStyle Hidden -ExecutionPolicy Bypass -File \"{scriptPath}\" " +
    $"-Pid {currentPid} -NewExe \"{downloadedExe}\" -TargetExe \"{originalExePath}\"")
{ UseShellExecute = false, CreateNoWindow = true });
Application.Current.Shutdown();
```

Kein SmartScreen-Schritt nötig — PowerShell ist bereits ein
vertrauenswürdiger, signierter Windows-Prozess; das Skript führt nur
Datei-Kopieroperationen aus, keinen fremden/unsignierten Code.

Bricht der Kopierversuch nach den Retries dauerhaft ab (z.B. Datei
weiterhin gesperrt), beendet sich das Skript ohne zu kopieren/starten —
ULM bleibt beim nächsten manuellen Start auf der alten Version, kein
Datenverlust. Der Update-Check läuft beim nächsten Start erneut.

### 6. Banner-Zustände (`MainWindow.xaml`, `MainViewModel.cs`)

Bestehendes Banner ([MainWindow.xaml:298](../../../Views/MainWindow.xaml))
durchläuft, kein neuer Dialog:

`"🆕 Update verfügbar"` → `"⬇ Wird heruntergeladen …"` →
`"✅ Update bereit — Jetzt installieren & neu starten"`

Neue `MainViewModel`-Eigenschaft `UpdateBannerState` (Enum:
`Available`, `Downloading`, `ReadyToInstall`) steuert Text und
Button-Beschriftung/-Handler. Der bestehende Schließen-Button (X) bleibt
in jedem Zustand nutzbar, blendet weiterhin nur für die laufende Sitzung
aus (`DismissUpdateBanner()`, unverändert).

`BtnUpdateDownload_Click` in `MainWindow.xaml.cs` entfällt in der
heutigen Form (öffnete bisher `UpdateDownloadDialog`); neuer Handler löst
je nach `UpdateBannerState` entweder nichts aus (während `Downloading`)
oder `ApplyUpdateAndRestart` (bei `ReadyToInstall`). `UpdateDownloadDialog`
bleibt als Klasse bestehen — wird nur noch im Download-Fehler-Fallback
(Punkt 3) verwendet, dort unverändert wie heute.

## Fehlerfälle

| Fall | Verhalten |
|---|---|
| GitHub nicht erreichbar / kein Release | Kein Banner, kein Fehler (wie bisher) |
| Release ohne passendes Asset für die erkannte Variante | Banner erscheint, aber Download-Fehler-Fallback greift sofort (Punkt 3) — `UpdateDownloadDialog` zeigt "Zur Release-Seite öffnen" |
| Automatischer Download schlägt fehl | Fallback auf manuellen `UpdateDownloadDialog`, Log-Fehlermeldung |
| Setup-Installer-Aufruf schlägt fehl (z.B. Datei beschädigt) | ULM ist zu dem Zeitpunkt bereits beendet — Inno Setups eigenes Fehlerverhalten/Logging greift, kein ULM-seitiges Abfangen |
| Self-Replace-Skript: Datei dauerhaft gesperrt | Skript bricht ohne Kopieren/Neustart ab, alte ULM-Version bleibt unangetastet, nächster Start prüft erneut |
| Nutzer beendet ULM manuell, bevor Download fertig ist | Kein Install-Versuch, kein Datenverlust — beim nächsten Start beginnt der Check von vorn |

## Betroffene Dateien

- `Core/Services/SelfUpdateService.cs` (neu: `ISelfUpdateService`,
  Varianten-Erkennung, Download, Installieren/Ersetzen)
- `installer/ULM.iss` (`AppMutex`/`CloseApplications`/
  `RestartApplications` in `[Setup]`)
- `ViewModels/MainViewModel.cs` (`UpdateBannerState`-Enum statt reinem
  `bool`, Text-/Button-Logik pro Zustand)
- `Views/MainWindow.xaml` (Banner-Button-Text/-Binding je Zustand)
- `Views/MainWindow.xaml.cs` (`CheckUlmUpdateAsync` ruft
  `SelfUpdateService` statt Dialog direkt zu öffnen; neuer
  Click-Handler für `ReadyToInstall`)
- `App.xaml.cs` (DI-Registrierung `ISelfUpdateService`, analog
  bestehender Service-Registrierungen)
- Tests: `DetectInstallKind()` gegen Fixture-Verzeichnis mit/ohne
  `unins000.exe`; Fehlerfälle aus der Tabelle oben als Unit-Tests gegen
  eine gemockte `IHttpService`/`ISelfUpdateService` (Muster analog
  `MainViewModelServiceInjectionTests.cs`)
