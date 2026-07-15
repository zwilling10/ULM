# Autostart-Option Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eine Checkbox "Mit Windows starten" im Einrichtungsfenster (`SetupDialog`), die ULM per `HKCU`-Registry-Run-Key beim Windows-Login normal sichtbar mitstartet.

**Architecture:** Neue statische Klasse `AutostartService` (Infrastructure-Schicht, analog zu `ThemeService`) kapselt Lesen/Schreiben des Registry-Run-Keys. `SetupDialog` bekommt eine weitere Karte mit Checkbox, die beim Öffnen den aktuellen Zustand abfragt und beim "Übernehmen"-Klick den gewünschten Zustand anwendet.

**Tech Stack:** C# / .NET 8 / WPF, `Microsoft.Win32.Registry` (bereits im Projekt genutzt, siehe `Infrastructure/ThemeService.cs:77`), xUnit für Tests.

## Global Constraints

- Nur `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` — kein `HKLM`, kein Admin-Recht
- Registry ist alleinige Quelle der Wahrheit für den Autostart-Zustand — kein Duplikat-Flag in `ulm_settings.ini`
- Registry-Zugriffsfehler dürfen den Setup-Ablauf nicht unterbrechen (try/catch, stiller Fallback, analog zu `ThemeService.IsWindowsDarkModeActive()`)
- Datei-Header-Kommentar `// Pfad/Datei.cs` am Dateianfang (bestehende Konvention in diesem Projekt)
- Tests decken nur reine Logik ohne echten Registry-I/O ab (Projektkonvention, siehe `ULM.Tests/HttpServiceTests.cs` — dort werden ausschließlich pure Funktionen getestet, keine OS-Aufrufe gemockt)

---

### Task 1: `AutostartService` mit testbarer Pfad-Vergleichslogik

**Files:**
- Create: `Infrastructure/AutostartService.cs`
- Test: `ULM.Tests/AutostartServiceTests.cs`

**Interfaces:**
- Produces: `ULM.Infrastructure.AutostartService.IsEnabled(): bool`, `.Enable(): void`, `.Disable(): void`, `internal static AutostartService.IsSameExecutable(string? registryValue, string? currentExePath): bool`

- [ ] **Step 1: Write the failing tests**

Erstelle `ULM.Tests/AutostartServiceTests.cs`:

```csharp
using ULM.Infrastructure;
using Xunit;

namespace ULM.Tests;

public class AutostartServiceIsSameExecutableTests
{
    [Theory]
    [InlineData(@"C:\Tools\UniversalLinuxManager.exe", @"C:\Tools\UniversalLinuxManager.exe", true)]
    [InlineData(@"""C:\Tools\UniversalLinuxManager.exe""", @"C:\Tools\UniversalLinuxManager.exe", true)] // Registry-Wert in Anführungszeichen
    [InlineData(@"C:\Tools\UniversalLinuxManager.exe", @"C:\Tools\universallinuxmanager.exe", true)]     // Windows-Pfade: case-insensitive
    [InlineData(@"C:\Old\UniversalLinuxManager.exe", @"C:\Tools\UniversalLinuxManager.exe", false)]      // EXE wurde verschoben
    [InlineData(null, @"C:\Tools\UniversalLinuxManager.exe", false)]
    [InlineData("", @"C:\Tools\UniversalLinuxManager.exe", false)]
    [InlineData(@"C:\Tools\UniversalLinuxManager.exe", null, false)]
    public void IsSameExecutable_ComparesNormalizedPaths(string? registryValue, string? currentExePath, bool expected)
        => Assert.Equal(expected, AutostartService.IsSameExecutable(registryValue, currentExePath));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj --filter AutostartServiceIsSameExecutableTests`
Expected: Kompilierfehler ("AutostartService" nicht gefunden) — das ist die erwartete Rot-Phase, weil die Klasse noch nicht existiert.

- [ ] **Step 3: Implement `AutostartService`**

Erstelle `Infrastructure/AutostartService.cs`:

```csharp
// Infrastructure/AutostartService.cs
using System;
using Microsoft.Win32;

namespace ULM.Infrastructure
{
    // ═══════════════════════════════════════════════════════════════════
    // AutostartService — verwaltet den optionalen Windows-Autostart über
    // einen HKCU-Run-Key (kein Admin-Recht nötig). Die Registry selbst ist
    // die einzige Quelle der Wahrheit für den aktivierten Zustand — es gibt
    // bewusst KEIN Duplikat-Flag in ulm_settings.ini, damit der Zustand nie
    // auseinanderlaufen kann (z.B. wenn die EXE manuell verschoben oder der
    // Registry-Eintrag extern gelöscht wird).
    // ═══════════════════════════════════════════════════════════════════
    public static class AutostartService
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName  = "UniversalLinuxManager";

        public static bool IsEnabled()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
                string? existing = key?.GetValue(ValueName) as string;
                return IsSameExecutable(existing, Environment.ProcessPath);
            }
            catch { /* Registry nicht lesbar (Gruppenrichtlinie o.ä.) -> als deaktiviert behandeln */ return false; }
        }

        public static void Enable()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
                key?.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
            }
            catch { /* Registry nicht beschreibbar (Gruppenrichtlinie o.ä.) -> bewusst ignoriert, Setup läuft weiter */ }
        }

        public static void Disable()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
                key?.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            catch { /* siehe Enable() */ }
        }

        internal static bool IsSameExecutable(string? registryValue, string? currentExePath)
        {
            if (string.IsNullOrWhiteSpace(registryValue) || string.IsNullOrWhiteSpace(currentExePath))
                return false;
            string trimmed = registryValue.Trim().Trim('"');
            return string.Equals(trimmed, currentExePath, StringComparison.OrdinalIgnoreCase);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj --filter AutostartServiceIsSameExecutableTests`
Expected: `Bestanden! : Fehler: 0, erfolgreich: 7, ...`

- [ ] **Step 5: Commit**

```bash
git add Infrastructure/AutostartService.cs ULM.Tests/AutostartServiceTests.cs
git commit -m "feat: AutostartService für HKCU-Registry-Autostart"
```

---

### Task 2: Autostart-Checkbox im Einrichtungsfenster

**Files:**
- Modify: `Views/Dialogs/SetupDialogs.cs:26` (neue Property)
- Modify: `Views/Dialogs/SetupDialogs.cs:204-238` (neue Karte zwischen "👤 Modus" und "🌓 Design")
- Modify: `Views/Dialogs/SetupDialogs.cs:266-285` (Übernehmen-Handler ruft Enable/Disable auf)

**Interfaces:**
- Consumes: `ULM.Infrastructure.AutostartService.IsEnabled(): bool`, `.Enable(): void`, `.Disable(): void` (aus Task 1)

- [ ] **Step 1: Neue Karte "🚀 Autostart" einfügen**

In `Views/Dialogs/SetupDialogs.cs`, nach dem Block, der `body.Children.Add(MakeCard("👤 Modus", modeSection));` aufruft (aktuell Zeile 204), füge ein:

```csharp
            // ── Autostart ────────────────────────────────────────────
            var autostartSection = new StackPanel();
            var chkAutostart = new CheckBox
            {
                Content = "Mit Windows starten",
                FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = ThemeColors.Header,
                VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 8),
                IsChecked = AutostartService.IsEnabled(),
            };
            autostartSection.Children.Add(chkAutostart);
            autostartSection.Children.Add(new TextBlock
            {
                Text = "ULM startet dann automatisch (sichtbares Fenster) bei jeder Windows-Anmeldung. " +
                       "Kein Admin-Recht nötig. Kann später hier jederzeit wieder deaktiviert werden.",
                TextWrapping = TextWrapping.Wrap, Foreground = ThemeColors.Dim, FontSize = 11, LineHeight = 16,
            });
            body.Children.Add(MakeCard("🚀 Autostart", autostartSection));
```

- [ ] **Step 2: Im Übernehmen-Handler den gewählten Zustand anwenden**

Im `btnApply.Click`-Handler, direkt vor `DialogResult = true;` (aktuell Zeile 283), füge ein:

```csharp
                if (chkAutostart.IsChecked == true) AutostartService.Enable(); else AutostartService.Disable();
```

- [ ] **Step 3: Build ausführen und auf Fehler prüfen**

Run: `dotnet build UniversalLinuxManager.csproj -c Release`
Expected: `0 Fehler, 0 Warnungen`

- [ ] **Step 4: Vorhandene Test-Suite laufen lassen (Regressionscheck)**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj`
Expected: alle Tests grün (38 bisherige + 7 neue aus Task 1 = 45)

- [ ] **Step 5: Manueller Rauchtest**

EXE starten (`dotnet run` reicht nicht für Registry-Pfadvergleich — `Environment.ProcessPath` zeigt sonst auf `dotnet.exe`; stattdessen die gebaute EXE unter `bin/Release/net8.0-windows/win-x64/UniversalLinuxManager.exe` direkt starten, ggf. vorher `ulm_settings.ini` `SkipSetupDialog` auf `0` setzen oder löschen, damit der Dialog erscheint):
1. Checkbox "Mit Windows starten" aktivieren → Übernehmen
2. Windows-Registrierungs-Editor öffnen (`regedit`), zu `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run` navigieren, prüfen dass Wert `UniversalLinuxManager` mit dem korrekten EXE-Pfad existiert
3. ULM erneut starten (Einrichtungsfenster ggf. wieder erzwingen) → Checkbox muss automatisch angehakt sein
4. Checkbox deaktivieren → Übernehmen → Registry-Wert muss verschwunden sein

- [ ] **Step 6: Commit**

```bash
git add Views/Dialogs/SetupDialogs.cs
git commit -m "feat: Autostart-Checkbox im Einrichtungsfenster"
```

---

## Self-Review Notes

- Spec-Abdeckung: "Checkbox im SetupDialog" (Task 2), "AutostartService mit Enable/Disable/IsEnabled" (Task 1), "keine Duplikat-Speicherung in ulm_settings.ini" (Constraint + Task 1 Kommentar), "Fehlerbehandlung bei Registry-Zugriffsfehlern" (Task 1 try/catch) — alle Spec-Punkte abgedeckt.
- Keine Platzhalter — jeder Schritt enthält vollständigen Code bzw. exakte Befehle.
- Typ-/Namenskonsistenz geprüft: `AutostartService.IsEnabled/Enable/Disable` identisch in Task 1 (Produktion) und Task 2 (Konsum) benannt.
