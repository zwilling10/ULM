# Status-Reiter (Hintergrund-Transparenz) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Einen neuen Reiter "Status" (nur im Experten-Modus sichtbar) einführen, der alle im Hintergrund laufenden bzw. geplanten ULM-Vorgänge sichtbar macht, ohne dass der Nutzer den Task-Manager öffnen muss.

**Architecture:** Kein neues Worker-Subsystem. Der Reiter bindet ausschließlich an bereits vorhandene `MainViewModel`-Zustände (`IsBusy`/`StatusText`/`ProgressPercent` für manuell gestartete Vorgänge wie Download, Kopieren, Integritätsprüfung, Ventoy-Installation, URL-/Update-Check; `OnlineScanActive`/`OnlineScanPercent` und `UsbScanActive`/`UsbScanPercent` für die automatisch laufenden Hintergrund-Scans) und ergänzt zwei neue, kleine Bausteine: eine berechnete "nächste geplante Aktion"-Anzeige (aus dem bereits gespeicherten `LastAutoCheckUtc` + `Constants.AutoCheckIntervalDays`) und eine kompakte, strukturierte Verlaufs-Chronik (`ActivityHistory`) der letzten Hintergrund-Meilensteine (Start/Ende von Integritätsprüfung, automatischem Versionscheck, automatischem Stick-Scan). Die beiden always-on-Timer (`_driveTimer`, `_autoCheckTimer`) werden als statische Info-Zeilen angezeigt.

**Tech Stack:** C# / WPF (.NET 8), MVVM (bestehendes `MainViewModel` + `RelayCommand`), xUnit (ULM.Tests).

## Global Constraints

- Reiter-Sichtbarkeit folgt exakt dem bestehenden Muster aus `UpdateUiMode()` in [Views/MainWindow.xaml.cs:238](../../../Views/MainWindow.xaml.cs:238) (Visibility.Visible/Collapsed per Code-behind, kein neuer Converter).
- Keine neuen externen NuGet-Pakete.
- Neue reine Logik als `internal static` Methode auf `MainViewModel` (Testkonvention dieses Projekts, siehe `BuildPipelineCompletionMessage` in [ViewModels/MainViewModel.cs:848](../../../ViewModels/MainViewModel.cs:848)) — Tests liegen in `ULM.Tests` und rufen diese Methoden direkt als `MainViewModel.MethodName(...)` auf, ohne das ViewModel zu instanziieren (dessen Konstruktor lädt echte Singletons wie `AppPaths.Instance`).
- Deutsche UI-Texte, gleicher Ton/Emoji-Stil wie das bestehende Protokoll (🔒 🌐 💾 ⛔ ✅).

---

### Task 1: Geplante-Aktion-Anzeige (nächster automatischer Versionscheck)

**Files:**
- Modify: `ViewModels/MainViewModel.cs` (neue Property + Methode)
- Test: `ULM.Tests/MainViewModelScheduleStatusTests.cs` (neu)

**Interfaces:**
- Produces: `internal static string MainViewModel.FormatNextAutoCheckText(DateTime? lastCheckUtc, int intervalDays, DateTime nowUtc)` — reine Formatierungslogik, von Task 4 (XAML-Binding) über die Instanz-Property `NextAutoCheckText` konsumiert.
- Produces: `public string MainViewModel.NextAutoCheckText { get; }` — Instanz-Property, aktualisiert durch `public void RefreshScheduleStatus()`.
- Produces: `public void MainViewModel.RefreshScheduleStatus()` — liest `LastAutoCheckUtc` aus der Settings-INI und setzt `NextAutoCheckText`; wird von Task 3 (MainWindow.xaml.cs) aufgerufen.

- [ ] **Step 1: Write the failing test**

```csharp
using System;
using ULM.ViewModels;
using Xunit;

namespace ULM.Tests;

/// <summary>
/// FormatNextAutoCheckText ist die reine Formatierungslogik hinter dem "Status"-Reiter
/// (Abschnitt "Geplante automatische Aktionen") — zeigt, wann der nächste automatische
/// Online-Versionscheck fällig ist, ohne dass der Nutzer dafür den Task-Manager oder das
/// Protokoll durchsuchen muss.
/// </summary>
public class MainViewModelScheduleStatusTests
{
    private static readonly DateTime Now = new(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NoPriorCheck_ReturnsUnknown()
    {
        string text = MainViewModel.FormatNextAutoCheckText(null, intervalDays: 3, Now);
        Assert.Equal("unbekannt (noch kein Check gelaufen)", text);
    }

    [Fact]
    public void CheckDueInFuture_ShowsRemainingDays()
    {
        DateTime last = Now.AddDays(-1); // vor 1 Tag, Intervall 3 Tage -> noch 2 Tage
        string text = MainViewModel.FormatNextAutoCheckText(last, intervalDays: 3, Now);
        Assert.Equal("in ca. 2 Tag(en)", text);
    }

    [Fact]
    public void CheckOverdue_ReportsDue()
    {
        DateTime last = Now.AddDays(-5); // vor 5 Tagen, Intervall 3 Tage -> überfällig
        string text = MainViewModel.FormatNextAutoCheckText(last, intervalDays: 3, Now);
        Assert.Equal("jetzt fällig", text);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ULM.Tests --filter FullyQualifiedName~MainViewModelScheduleStatusTests`
Expected: FAIL — `MainViewModel` does not contain a definition for `FormatNextAutoCheckText`.

- [ ] **Step 3: Write minimal implementation**

In `ViewModels/MainViewModel.cs`, add the new property next to the other scan-state properties (immediately after the `UsbScanPercent` property, around line 77):

```csharp
        private string _nextAutoCheckText = "wird berechnet …";
        public string NextAutoCheckText { get => _nextAutoCheckText; private set => SetField(ref _nextAutoCheckText, value); }
```

Then add the static helper next to `BuildPipelineCompletionMessage` (after its closing brace, around line 857):

```csharp
        /// <summary>
        /// Reine Formatierungslogik für die "Status"-Reiter-Anzeige "Nächste geplante Aktion".
        /// lastCheckUtc kommt aus der Settings-INI (LastAutoCheckUtc, siehe CheckAutoRecheckDue in
        /// MainWindow.xaml.cs) — null bedeutet: seit Installation/Reset noch kein Check gelaufen.
        /// </summary>
        internal static string FormatNextAutoCheckText(DateTime? lastCheckUtc, int intervalDays, DateTime nowUtc)
        {
            if (lastCheckUtc is null) return "unbekannt (noch kein Check gelaufen)";
            double remainingDays = intervalDays - (nowUtc - lastCheckUtc.Value).TotalDays;
            if (remainingDays <= 0) return "jetzt fällig";
            return $"in ca. {Math.Ceiling(remainingDays):0} Tag(en)";
        }
```

Then add the instance refresh method next to `Log()` (around line 1197):

```csharp
        public void RefreshScheduleStatus()
        {
            string raw = IniService.Read(_paths.SettingsIni, "App", "LastAutoCheckUtc", string.Empty);
            DateTime? last = DateTime.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsed) ? parsed : null;
            NextAutoCheckText = FormatNextAutoCheckText(last, Constants.AutoCheckIntervalDays, DateTime.UtcNow);
        }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ULM.Tests --filter FullyQualifiedName~MainViewModelScheduleStatusTests`
Expected: PASS (3 tests)

- [ ] **Step 5: Commit**

```bash
git add ViewModels/MainViewModel.cs ULM.Tests/MainViewModelScheduleStatusTests.cs
git commit -m "feat: NextAutoCheckText fuer geplante Versionscheck-Anzeige"
```

---

### Task 2: Aktivitäts-Verlauf (strukturierte Hintergrund-Chronik)

**Files:**
- Modify: `ViewModels/MainViewModel.cs` (neue Collection + Helper + Wiring an 3 Stellen)
- Test: `ULM.Tests/MainViewModelActivityHistoryTests.cs` (neu)

**Interfaces:**
- Consumes: nichts Neues aus Task 1.
- Produces: `internal static string MainViewModel.FormatHistoryEntry(string message, DateTime now)` — reine Formatierung, getestet isoliert.
- Produces: `public ObservableCollection<string> MainViewModel.ActivityHistory { get; }` — von Task 4 (XAML `ItemsControl`-Binding) konsumiert.
- Produces: `private void MainViewModel.RecordHistory(string msg)` — nur intern verwendet, an den drei Meilenstein-Stellen unten aufgerufen.

- [ ] **Step 1: Write the failing test**

```csharp
using System;
using ULM.ViewModels;
using Xunit;

namespace ULM.Tests;

/// <summary>
/// FormatHistoryEntry ist die reine Formatierung hinter dem "Verlauf"-Abschnitt im Status-Reiter —
/// stempelt jede Hintergrund-Meilenstein-Meldung (Start/Ende von Integritätspruefung,
/// automatischem Versionscheck, automatischem Stick-Scan) mit einer Uhrzeit.
/// </summary>
public class MainViewModelActivityHistoryTests
{
    [Fact]
    public void FormatsMessageWithTimestamp()
    {
        var now = new DateTime(2026, 7, 16, 8, 26, 3);
        string entry = MainViewModel.FormatHistoryEntry("🔒 Integritätsprüfung D: gestartet …", now);
        Assert.Equal("[08:26:03] 🔒 Integritätsprüfung D: gestartet …", entry);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ULM.Tests --filter FullyQualifiedName~MainViewModelActivityHistoryTests`
Expected: FAIL — `MainViewModel` does not contain a definition for `FormatHistoryEntry`.

- [ ] **Step 3: Write minimal implementation**

Add the collection next to `NextAutoCheckText` (from Task 1, around line 79):

```csharp
        public ObservableCollection<string> ActivityHistory { get; } = new();
        private const int MaxActivityHistoryEntries = 30;
```

Add the static helper next to `FormatNextAutoCheckText` (from Task 1):

```csharp
        internal static string FormatHistoryEntry(string message, DateTime now) => $"[{now:HH:mm:ss}] {message}";
```

Add the instance helper next to `RefreshScheduleStatus()` (from Task 1):

```csharp
        private void RecordHistory(string msg)
        {
            ActivityHistory.Insert(0, FormatHistoryEntry(msg, DateTime.Now));
            while (ActivityHistory.Count > MaxActivityHistoryEntries) ActivityHistory.RemoveAt(ActivityHistory.Count - 1);
        }
```

Wire it into the three silent/automatic background operations (manually triggered ones like Download/Copy/URL-Check are already covered live via `IsBusy`+`StatusText` in Task 4, so they are intentionally NOT duplicated into the history here):

1. **Integritätsprüfung** in `VerifyStickIntegrityAsync` ([ViewModels/MainViewModel.cs:489](../../../ViewModels/MainViewModel.cs:489)) — change:

```csharp
            SetBusy(true); StatusText = "🔒 Prüfe Integrität …"; Log($"🔒 Integritätsprüfung {SelectedDriveLetter} gestartet …");
```
to:
```csharp
            SetBusy(true); StatusText = "🔒 Prüfe Integrität …";
            RecordHistory($"🔒 Integritätsprüfung {SelectedDriveLetter} gestartet …"); Log($"🔒 Integritätsprüfung {SelectedDriveLetter} gestartet …");
```

and in the `_ui.Invoke` completion block ([ViewModels/MainViewModel.cs:520-532](../../../ViewModels/MainViewModel.cs:520)), change the cancel branch:
```csharp
                    if (ct.IsCancellationRequested)
                    {
                        StatusText = "Abbruch …";
                        Log($"⛔ Integritätsprüfung {SelectedDriveLetter} abgebrochen ({checkedCount} geprüft).");
                        return;
                    }
```
to:
```csharp
                    if (ct.IsCancellationRequested)
                    {
                        StatusText = "Abbruch …";
                        RecordHistory($"⛔ Integritätsprüfung {SelectedDriveLetter} abgebrochen ({checkedCount} geprüft).");
                        Log($"⛔ Integritätsprüfung {SelectedDriveLetter} abgebrochen ({checkedCount} geprüft).");
                        return;
                    }
```
and the success line:
```csharp
                    StatusText = mismatches.Count > 0 ? $"⚠ {mismatches.Count} Hash-Abweichung(en)." : $"✅ {checkedCount} ISO(s) verifiziert.";
                    Log($"🔒 Integritätsprüfung {SelectedDriveLetter}: {checkedCount} geprüft, {mismatches.Count} Abweichung(en).");
```
to:
```csharp
                    StatusText = mismatches.Count > 0 ? $"⚠ {mismatches.Count} Hash-Abweichung(en)." : $"✅ {checkedCount} ISO(s) verifiziert.";
                    RecordHistory($"🔒 Integritätsprüfung {SelectedDriveLetter}: {checkedCount} geprüft, {mismatches.Count} Abweichung(en).");
                    Log($"🔒 Integritätsprüfung {SelectedDriveLetter}: {checkedCount} geprüft, {mismatches.Count} Abweichung(en).");
```

2. **Automatischer Online-Versionscheck** in `TriggerAutoVersionCheck` ([ViewModels/MainViewModel.cs:627](../../../ViewModels/MainViewModel.cs:627)) — change:
```csharp
            Log($"🌐 Online-Versionscheck gestartet — {_db.Count} Distros …");
```
to:
```csharp
            RecordHistory($"🌐 Online-Versionscheck gestartet — {_db.Count} Distros …"); Log($"🌐 Online-Versionscheck gestartet — {_db.Count} Distros …");
```
and near the completion ([ViewModels/MainViewModel.cs:686](../../../ViewModels/MainViewModel.cs:686)):
```csharp
                    Log($"🌐 Versionscheck: {StatusText}"); AutoVersionCheckCompleted?.Invoke();
```
to:
```csharp
                    RecordHistory($"🌐 Versionscheck: {StatusText}"); Log($"🌐 Versionscheck: {StatusText}"); AutoVersionCheckCompleted?.Invoke();
```

3. **Automatischer Stick-Scan** (folgt direkt auf den Versionscheck, [ViewModels/MainViewModel.cs:709](../../../ViewModels/MainViewModel.cs:709)) — change:
```csharp
                            ApplyStickResults(si); UsbScanActive = false; UsbScanPercent = 100; RefreshAllEntries();
```
to:
```csharp
                            ApplyStickResults(si); UsbScanActive = false; UsbScanPercent = 100; RefreshAllEntries();
                            RecordHistory($"💾 Stick-Prüfung {driveToScan} abgeschlossen ({si.Count} ISO(s) erkannt).");
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ULM.Tests --filter FullyQualifiedName~MainViewModelActivityHistoryTests`
Expected: PASS

Then run the full test suite to make sure the wiring changes didn't break anything:

Run: `dotnet test ULM.Tests`
Expected: all tests PASS (no regressions in existing `MainViewModelDistroMatchingTests`, `MainViewModelPipelineSummaryTests`, etc.)

- [ ] **Step 5: Commit**

```bash
git add ViewModels/MainViewModel.cs ULM.Tests/MainViewModelActivityHistoryTests.cs
git commit -m "feat: strukturierten Aktivitaets-Verlauf fuer Hintergrund-Vorgaenge einfuehren"
```

---

### Task 3: Zeitplan-Anzeige aktuell halten (MainWindow.xaml.cs)

**Files:**
- Modify: `Views/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `public void MainViewModel.RefreshScheduleStatus()` (Task 1).

- [ ] **Step 1: Wire RefreshScheduleStatus into the existing 30-minute timer and startup**

In `CheckAutoRecheckDue()` ([Views/MainWindow.xaml.cs:202-210](../../../Views/MainWindow.xaml.cs:202)), call it at the top so the "Status"-Reiter reflects the current countdown on every tick, whether or not a check actually fires:

```csharp
        private void CheckAutoRecheckDue()
        {
            _vm.RefreshScheduleStatus();
            if (_vm.IsBusy) return;
            string raw = IniService.Read(AppPaths.Instance.SettingsIni, "App", "LastAutoCheckUtc", string.Empty);
            if (!DateTime.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime last)) return;
            if ((DateTime.UtcNow - last).TotalDays < Constants.AutoCheckIntervalDays) return;
            AppendLog($"🌐 Hintergrund-Check fällig (alle {Constants.AutoCheckIntervalDays} Tage) — suche erreichbare Server für die aktuellsten Versionen …");
            _vm.TriggerAutoVersionCheck();
        }
```

In `OnLoaded` ([Views/MainWindow.xaml.cs:169-182](../../../Views/MainWindow.xaml.cs:169)), call it once right after `LoadSettings(); _vm.Initialize();` so the Status-Reiter shows a real value immediately at startup instead of "wird berechnet …":

```csharp
        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            LoadSettings(); _vm.Initialize();
            _vm.RefreshScheduleStatus();
            ShowChangelogIfUpdated();
```

- [ ] **Step 2: Build to verify no compile errors**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: `0 errors, 0 warnings`

- [ ] **Step 3: Commit**

```bash
git add Views/MainWindow.xaml.cs
git commit -m "feat: Zeitplan-Anzeige bei Start und 30-Minuten-Tick aktualisieren"
```

---

### Task 4: "Status"-Reiter (XAML) + Experten-Modus-Sichtbarkeit

**Files:**
- Modify: `Views/MainWindow.xaml` (neuer TabItem)
- Modify: `Views/MainWindow.xaml.cs` (Sichtbarkeit + Verlauf-leeren-Button)

**Interfaces:**
- Consumes: `NextAutoCheckText`, `ActivityHistory`, `IsBusy`, `StatusText`, `ProgressPercent`, `OnlineScanActive`, `OnlineScanPercent`, `UsbScanActive`, `UsbScanPercent` (alle bereits vorhanden bzw. aus Task 1/2).

- [ ] **Step 1: Add the "Status" TabItem to MainWindow.xaml**

In `Views/MainWindow.xaml`, insert a new `TabItem` directly after the closing `</TabItem>` of "Protokoll" ([Views/MainWindow.xaml:395](../../../Views/MainWindow.xaml:395)), before `</TabControl>`:

```xml
            <TabItem Header="Status" x:Name="StatusTab">
                <ScrollViewer VerticalScrollBarVisibility="Auto" Background="{DynamicResource BrushWhite}">
                    <StackPanel Margin="12">

                        <TextBlock Text="Aktueller Vorgang" FontWeight="Bold" FontSize="13"
                                   Foreground="{DynamicResource BrushMid}" Margin="0,0,0,4"/>
                        <TextBlock Margin="0,0,0,12" FontSize="12">
                            <TextBlock.Style>
                                <Style TargetType="TextBlock">
                                    <Setter Property="Text" Value="{Binding StatusText}"/>
                                    <Style.Triggers>
                                        <DataTrigger Binding="{Binding IsBusy}" Value="False">
                                            <Setter Property="Text" Value="Kein manueller Vorgang aktiv."/>
                                        </DataTrigger>
                                    </Style.Triggers>
                                </Style>
                            </TextBlock.Style>
                        </TextBlock>

                        <TextBlock Text="Automatische Hintergrund-Scans" FontWeight="Bold" FontSize="13"
                                   Foreground="{DynamicResource BrushMid}" Margin="0,0,0,4"/>
                        <TextBlock Margin="0,0,0,2" FontSize="12">
                            <Run Text="🌐 Online-Versionscheck: "/>
                            <Run Text="{Binding OnlineScanActive, Converter={StaticResource BoolToRunningTextConverter}}"/>
                        </TextBlock>
                        <TextBlock Margin="0,0,0,12" FontSize="12">
                            <Run Text="💾 Stick-Prüfung: "/>
                            <Run Text="{Binding UsbScanActive, Converter={StaticResource BoolToRunningTextConverter}}"/>
                        </TextBlock>

                        <TextBlock Text="Geplante automatische Aktionen" FontWeight="Bold" FontSize="13"
                                   Foreground="{DynamicResource BrushMid}" Margin="0,0,0,4"/>
                        <TextBlock Margin="0,0,0,2" FontSize="12">
                            <Run Text="🌐 Nächster automatischer Online-Versionscheck: "/>
                            <Run Text="{Binding NextAutoCheckText}"/>
                        </TextBlock>
                        <TextBlock Margin="0,0,0,12" FontSize="12" Foreground="{DynamicResource BrushMid}"
                                   Text="🔌 Laufwerks-Überwachung: läuft laufend im Hintergrund (Prüfung alle 4 Sekunden)."/>

                        <Grid Margin="0,0,0,4">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="0" Text="Verlauf" FontWeight="Bold" FontSize="13"
                                       Foreground="{DynamicResource BrushMid}" VerticalAlignment="Center"/>
                            <Button Grid.Column="1" Content="Verlauf leeren" Style="{DynamicResource BtnGhost}"
                                    Click="BtnClearHistory_Click"/>
                        </Grid>
                        <ItemsControl ItemsSource="{Binding ActivityHistory}">
                            <ItemsControl.ItemTemplate>
                                <DataTemplate>
                                    <TextBlock Text="{Binding}" FontFamily="Consolas, Courier New" FontSize="11" Margin="0,1"/>
                                </DataTemplate>
                            </ItemsControl.ItemTemplate>
                        </ItemsControl>

                    </StackPanel>
                </ScrollViewer>
            </TabItem>
```

- [ ] **Step 2: Add the BoolToRunningTextConverter resource**

Find the `Window.Resources` section at the top of `Views/MainWindow.xaml` (same place other converters/templates like `CategoryTemplate` are declared) and add:

```xml
    <Window.Resources>
        <!-- ... existing resources ... -->
        <BooleanToVisibilityConverter x:Key="BoolToVis"/>
```

Instead of a full `IValueConverter` class for two words, use a `DataTrigger`-based `Style` (consistent with the "Aktueller Vorgang" block above) rather than a converter, so no new C# class is needed. Replace the two `Run Text="{Binding OnlineScanActive, ...}"` bindings from Step 1 with this equivalent that needs no converter:

```xml
                        <TextBlock Margin="0,0,0,2" FontSize="12">
                            <Run Text="🌐 Online-Versionscheck: "/>
                            <Run Text="läuft …" Foreground="{DynamicResource BrushBlue}">
                                <Run.Style>
                                    <Style TargetType="Run">
                                        <Style.Triggers>
                                            <DataTrigger Binding="{Binding OnlineScanActive}" Value="False">
                                                <Setter Property="Text" Value="inaktiv"/>
                                            </DataTrigger>
                                        </Style.Triggers>
                                    </Style>
                                </Run.Style>
                            </Run>
                        </TextBlock>
                        <TextBlock Margin="0,0,0,12" FontSize="12">
                            <Run Text="💾 Stick-Prüfung: "/>
                            <Run Text="läuft …" Foreground="{DynamicResource BrushBlue}">
                                <Run.Style>
                                    <Style TargetType="Run">
                                        <Style.Triggers>
                                            <DataTrigger Binding="{Binding UsbScanActive}" Value="False">
                                                <Setter Property="Text" Value="inaktiv"/>
                                            </DataTrigger>
                                        </Style.Triggers>
                                    </Style>
                                </Run.Style>
                            </Run>
                        </TextBlock>
```

(This replaces the `Run`/`BoolToRunningTextConverter` bindings from Step 1 — no separate converter resource needed, skip adding anything to `Window.Resources`.)

- [ ] **Step 3: Add the clear-history click handler in MainWindow.xaml.cs**

Next to `BtnClearLog_Click` ([Views/MainWindow.xaml.cs:630](../../../Views/MainWindow.xaml.cs:630)):

```csharp
        private void BtnClearLog_Click(object sender, RoutedEventArgs e) => LogBox.Clear();
        private void BtnClearHistory_Click(object sender, RoutedEventArgs e) => _vm.ActivityHistory.Clear();
```

- [ ] **Step 4: Toggle Status-tab visibility with Expert Mode**

In `UpdateUiMode()` ([Views/MainWindow.xaml.cs:238-243](../../../Views/MainWindow.xaml.cs:238)):

```csharp
        private void UpdateUiMode()
        {
            BtnModeToggle.Content = _vm.ExpertMode ? "Modus: Experte 🛠" : "Modus: Anwender 👤";
            Visibility vis = _vm.ExpertMode ? Visibility.Visible : Visibility.Collapsed;
            BtnVentoy.Visibility = vis; ChkSecureBoot.Visibility = vis; ExpertBar.Visibility = vis; LogTab.Visibility = Visibility.Visible;
            StatusTab.Visibility = vis;
        }
```

- [ ] **Step 5: Build to verify no compile/XAML errors**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: `0 errors, 0 warnings`

- [ ] **Step 6: Commit**

```bash
git add Views/MainWindow.xaml Views/MainWindow.xaml.cs
git commit -m "feat: Status-Reiter fuer Hintergrund-Transparenz im Experten-Modus"
```

---

### Task 5: Manuelle End-to-End-Verifikation

**Files:** none (Verifikation only)

- [ ] **Step 1: App starten und Experten-Modus umschalten**

Run: `dotnet run --project UniversalLinuxManager.csproj`
Prüfen: Im Anwender-Modus ist kein "Status"-Reiter sichtbar. Nach Klick auf "Modus: Anwender 👤" (→ Experte) erscheint der Reiter "Status" neben "ISO-Auswahl" und "Protokoll".

- [ ] **Step 2: Startzustand prüfen**

Direkt nach dem Start im "Status"-Reiter prüfen: "Aktueller Vorgang" zeigt "Kein manueller Vorgang aktiv." (sobald der automatische Startup-Versionscheck durchgelaufen ist), "Online-Versionscheck" zeigt kurz "läuft …" dann "inaktiv", "Nächster automatischer Online-Versionscheck" zeigt einen konkreten Text (z. B. "jetzt fällig" oder "in ca. N Tag(en)") statt "wird berechnet …".

- [ ] **Step 3: Den ursprünglichen Bug nachstellen — Abbruch der Integritätsprüfung**

Einen Stick mit mehreren ISOs mit vorhandenem SHA-256-Referenzhash einstecken, "Integritätsprüfung" starten, nach 2-3 Sekunden auf "Abbrechen" klicken.
Erwartet: Die Prüfung stoppt innerhalb von höchstens 1-2 Sekunden (nicht erst nach vollständigem Durchlauf). Protokoll zeigt "⛔ Integritätsprüfung {Laufwerk} abgebrochen (N geprüft)." mit N kleiner als der Gesamtzahl der ISOs mit Referenzhash. Der "Status"-Reiter zeigt denselben Eintrag oben im "Verlauf".

- [ ] **Step 4: Verlauf-leeren-Button prüfen**

Im "Status"-Reiter auf "Verlauf leeren" klicken. Erwartet: Die Liste wird leer, das Hauptprotokoll (Reiter "Protokoll") bleibt unverändert.

- [ ] **Step 5: Vollständigen Testlauf der Suite durchführen**

Run: `dotnet test ULM.Tests`
Expected: alle Tests PASS.

---

## Self-Review Notes

- **Spec-Abdeckung:** Vorschlag 1 (aktiver Vorgang) → Task 4 Abschnitt "Aktueller Vorgang" + "Automatische Hintergrund-Scans". Vorschlag 2 (geplante Aktionen) → Task 1 + 3 + Task 4 Abschnitt "Geplante automatische Aktionen". Vorschlag 3 (Verlaufs-Chronik) → Task 2 + Task 4 Abschnitt "Verlauf". Vorschlag 4 (externe Prozesse/PID) ist bewusst NICHT umgesetzt — Ventoy-Installation und Downloads laufen bereits über eigene, für den Nutzer sichtbare Fortschrittsfenster; eine zusätzliche PID-Anzeige bräuchte neue Prozess-Tracking-Infrastruktur ohne zusätzlichen Transparenz-Gewinn gegenüber dem bereits sichtbaren `IsBusy`/`StatusText`. Der ursprüngliche Abbruch-Bug (bereits separat gefixt) ist über Task 5 Step 3 mit abgedeckt.
- **Platzhalter-Scan:** keine TBD/TODO-Marker, jeder Code-Schritt enthält vollständigen Code.
- **Typkonsistenz:** `NextAutoCheckText` (Task 1) und `ActivityHistory`/`RecordHistory` (Task 2) werden in Task 3 und 4 mit identischen Namen/Signaturen wiederverwendet.
