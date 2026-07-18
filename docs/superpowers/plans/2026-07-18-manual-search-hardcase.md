# Härtefall-Erkennung für den Quelle-manuell-suchen-Button — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Der 🔧-Button ("Quelle manuell suchen/eintragen") erscheint nur noch bei Einträgen, deren
automatische Selbstlern-Auflösung nachweislich wiederholt (≥3x in Folge) keine Quelle findet und die
über keinen dedizierten Resolver verfügen — bei allen anderen Einträgen bleibt er verborgen.

**Architecture:** Ein neues persistentes Zählfeld `IsoEntry.FailedResolveStreak` wird zentral in
`HttpService.ResolveLatestAsync` gepflegt (Erfolg → Reset auf 0, Fehlschlag ohne dedizierten Resolver
→ Inkrement, Fehlschlag MIT dedizierten Resolver → unverändert). Die Sichtbarkeit des Buttons wird
über eine neue, an die Bestehende `BoolToVis`-Converter-Infrastruktur gebundene ViewModel-Property
gesteuert statt fest auf sichtbar zu stehen.

**Tech Stack:** C# / .NET 8, WPF (XAML-Bindings), xUnit (`ULM.Tests`).

## Global Constraints

- Referenz-Spec: `docs/superpowers/specs/2026-07-18-manual-search-hardcase-design.md` — bei
  Widersprüchen zwischen Plan und Spec gilt die Spec.
- Keine Änderung an der automatischen Auflösungskette selbst (`ResolveGenericAsync`, dedizierte
  Resolver-Methoden) — nur ein zusätzliches, beobachtendes Zählfeld drumherum.
- `ulm_isos.ini`-Rückwärtskompatibilität ist zwingend: alte Dateien ohne den neuen Key müssen
  klaglos mit `FailedResolveStreak = 0` laden (gleiches Muster wie `Sha256`/`ExpectedSizeBytes`).
- Schwellenwert ist `3` (Konstante, kein Magic Number im Code).

---

### Task 1: `IsoEntry.FailedResolveStreak` + Persistenz in `ulm_isos.ini`

**Files:**
- Modify: `Core/Models/IsoEntry.cs` (neues Feld, im Block "Laufzeit-Felder" — es ist zwar
  persistent, aber inhaltlich näher an `UrlOk`/`UrlChecked` als an den DB-Editor-Feldern, deshalb
  direkt darunter platzieren)
- Modify: `Core/Services/IsoDatabaseService.cs` — `Save()` (Zeile ~114-152) und `LoadFromIni()`
  (Zeile ~64-101)
- Test: `ULM.Tests/IsoDatabaseServiceTests.cs`

**Interfaces:**
- Produces: `IsoEntry.FailedResolveStreak` (`int`, öffentliches Property, Default `0`) — wird von
  Task 2 (Zähl-/Reset-Logik) und Task 3 (ViewModel-Property) gelesen/geschrieben.

- [ ] **Step 1: Feld zu `IsoEntry.cs` hinzufügen**

In `Core/Models/IsoEntry.cs`, direkt nach `public bool ImportedFromStick { get; set; }` (aktuell
Zeile 58) einfügen:

```csharp
        // Zählt aufeinanderfolgende Fehlschläge der automatischen Selbstlern-Auflösung
        // (HttpService.ResolveLatestAsync) für Einträge OHNE dedizierten Resolver — treibt die
        // Sichtbarkeit des "Quelle manuell suchen/eintragen"-Buttons in der Hauptliste (nur ab
        // Constants.ManualSearchFailureThreshold sichtbar). Jeder Erfolg (gleich über welchen
        // Auflösungspfad) setzt den Zähler zurück auf 0 — der Button soll nur bei einer
        // ZUSAMMENHÄNGENDEN Fehlschlagsserie erscheinen, nicht kumulativ über die gesamte
        // Lebenszeit des Eintrags. Siehe docs/superpowers/specs/2026-07-18-manual-search-hardcase-design.md.
        public int FailedResolveStreak { get; set; }
```

- [ ] **Step 2: Fehlschlagenden Round-Trip-Test schreiben**

In `ULM.Tests/IsoDatabaseServiceTests.cs`, direkt nach `SaveThenLoad_PreservesSha256AndSha256Source`
(nach der schließenden `}` bei Zeile 76, vor der nächsten `finally`-losen Methode) einfügen:

```csharp
    [Fact]
    public void SaveThenLoad_PreservesFailedResolveStreak()
    {
        AppPaths paths = AppPaths.Instance;
        IsoDatabaseService db = IsoDatabaseService.Instance;

        string originalBase = paths.BaseDirectory;
        var originalEntries = db.Entries.ToList();
        string tempDir = Path.Combine(Path.GetTempPath(), $"ulm-db-streak-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempDir);
            paths.SetPaths(tempDir);

            while (db.Count > 0) db.Remove(0);

            var entry = new IsoEntry { Name = "Streak-Test-Distro", FailedResolveStreak = 2 };
            db.Add(entry);
            db.Save();

            db.Load();

            IsoEntry? reloaded = db.Entries.SingleOrDefault(e => e.Name == "Streak-Test-Distro");
            Assert.NotNull(reloaded);
            Assert.Equal(2, reloaded!.FailedResolveStreak);
        }
        finally
        {
            while (db.Count > 0) db.Remove(0);
            foreach (var e in originalEntries) db.Add(e);
            paths.SetPaths(originalBase);
            try { Directory.Delete(tempDir, recursive: true); } catch { /* Best-effort Cleanup */ }
        }
    }
```

- [ ] **Step 3: Test ausführen, Fehlschlag bestätigen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj --filter SaveThenLoad_PreservesFailedResolveStreak`
Expected: FAIL — `Assert.Equal(2, reloaded!.FailedResolveStreak)` schlägt fehl, weil
`reloaded.FailedResolveStreak` noch `0` ist (Save()/LoadFromIni() schreiben/lesen das Feld noch nicht).

- [ ] **Step 4: `Save()` erweitern**

In `Core/Services/IsoDatabaseService.cs`, in `Save()` nach der Zeile
`sb.AppendLine($"ExpectedSizeBytes = {e.ExpectedSizeBytes}");` (aktuell Zeile 147) einfügen:

```csharp
                sb.AppendLine($"FailedResolveStreak = {e.FailedResolveStreak}");
```

- [ ] **Step 5: `LoadFromIni()` erweitern**

In `Core/Services/IsoDatabaseService.cs`, in `LoadFromIni()` nach der Zeile, die
`ExpectedSizeBytes` liest (aktuell Zeile 99-100, endet mit `esb) ? esb : 0L,`) das neue Feld
ergänzen — migrationsfrei über `GetValueOrDefault` wie bei den Sha256-Feldern:

```csharp
                    FailedResolveStreak = int.TryParse(d.GetValueOrDefault("FailedResolveStreak", "0"), out int frs) ? frs : 0,
```

- [ ] **Step 6: Test ausführen, Erfolg bestätigen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj --filter SaveThenLoad_PreservesFailedResolveStreak`
Expected: PASS

- [ ] **Step 7: Rückwärtskompatibilitäts-Test schreiben und bestätigen**

In `ULM.Tests/IsoDatabaseServiceTests.cs`, direkt nach
`LoadFromIni_MissingShaKeys_DefaultsToEmpty_BackwardCompatibleWithOldDatabases` einfügen:

```csharp
    [Fact]
    public void LoadFromIni_MissingFailedResolveStreakKey_DefaultsToZero_BackwardCompatibleWithOldDatabases()
    {
        AppPaths paths = AppPaths.Instance;
        IsoDatabaseService db = IsoDatabaseService.Instance;

        string originalBase = paths.BaseDirectory;
        var originalEntries = db.Entries.ToList();
        string tempDir = Path.Combine(Path.GetTempPath(), $"ulm-db-streak-oldformat-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempDir);
            paths.SetPaths(tempDir);

            // Simuliert eine ALTE ulm_isos.ini ohne den FailedResolveStreak-Key (vor diesem Fix).
            string oldFormatIni =
                "[General]\r\n" +
                "Count = 1\r\n" +
                "\r\n" +
                "[ISO_0]\r\n" +
                "Name        = Alte Distro Ohne Streak\r\n" +
                "Category    = Einsteiger\r\n" +
                "URL         = \r\n" +
                "Filename    = alte-distro.iso\r\n";
            File.WriteAllText(paths.DatabaseIni, oldFormatIni);

            db.Load();

            IsoEntry? loaded = db.Entries.SingleOrDefault(e => e.Name == "Alte Distro Ohne Streak");
            Assert.NotNull(loaded);
            Assert.Equal(0, loaded!.FailedResolveStreak);
        }
        finally
        {
            while (db.Count > 0) db.Remove(0);
            foreach (var e in originalEntries) db.Add(e);
            paths.SetPaths(originalBase);
            try { Directory.Delete(tempDir, recursive: true); } catch { /* Best-effort Cleanup */ }
        }
    }
```

Run: `dotnet test ULM.Tests/ULM.Tests.csproj --filter FailedResolveStreak`
Expected: PASS (beide neuen Tests)

- [ ] **Step 8: Commit**

```bash
git add Core/Models/IsoEntry.cs Core/Services/IsoDatabaseService.cs ULM.Tests/IsoDatabaseServiceTests.cs
git commit -m "feat: FailedResolveStreak-Feld auf IsoEntry, persistent in ulm_isos.ini"
```

---

### Task 2: Zähl-/Reset-Logik in `HttpService.ResolveLatestAsync`

**Files:**
- Modify: `Core/Services/HttpService.cs` (`ResolveLatestAsync`, aktuell Zeile 406-471)
- Test: `ULM.Tests/HttpServiceTests.cs`

**Interfaces:**
- Consumes: `IsoEntry.FailedResolveStreak` (aus Task 1), `HttpService.NormalizeForMatch(string)`
  (bereits vorhanden, [HttpService.cs:403](../../../Core/Services/HttpService.cs))
- Produces: `HttpService.HasDedicatedResolver(IsoEntry entry) : bool` (internal static, pur, kein
  Netzwerkzugriff) und `HttpService.ApplyResolveOutcome(IsoEntry entry, bool succeeded) : void`
  (internal static, pur) — beide werden NUR intern von `ResolveLatestAsync` genutzt, aber als
  `internal static` deklariert, damit sie ohne Netzwerk-Mocking direkt testbar sind (gleiches Muster
  wie `NormalizeForMatch`/`ExtractVersion`, die `ULM.Tests` bereits direkt aufruft).

- [ ] **Step 1: Fehlschlagende Tests für `HasDedicatedResolver` schreiben**

In `ULM.Tests/HttpServiceTests.cs`, nach der Klasse `HttpServiceNormalizeForMatchTests` (Datei
endet aktuell nach deren Tests) eine neue Test-Klasse anhängen:

```csharp
public class HttpServiceHasDedicatedResolverTests
{
    [Theory]
    [InlineData("Ubuntu 26.04 Desktop", "")]
    [InlineData("Linux Mint 22.3 Cinnamon", "")]
    [InlineData("Fedora Workstation", "")]
    [InlineData("Linux Kodachi", "")]
    [InlineData("Rescuezilla", "rescuezilla/rescuezilla")] // dediziert über GithubRepo, nicht Namen
    public void HasDedicatedResolver_ReturnsTrue_ForKnownDistros(string name, string githubRepo)
    {
        var entry = new IsoEntry { Name = name, GithubRepo = githubRepo };
        Assert.True(HttpService.HasDedicatedResolver(entry));
    }

    [Theory]
    // Regression: genau der Fall aus der Spec — eine Distro ohne jeden dedizierten Resolver.
    [InlineData("Shadowfetch Linux")]
    [InlineData("Irgendeine Ganz Neue Distro")]
    public void HasDedicatedResolver_ReturnsFalse_ForUnknownDistros(string name)
    {
        var entry = new IsoEntry { Name = name };
        Assert.False(HttpService.HasDedicatedResolver(entry));
    }
}

public class HttpServiceApplyResolveOutcomeTests
{
    [Fact]
    public void ApplyResolveOutcome_Success_ResetsStreakToZero()
    {
        var entry = new IsoEntry { Name = "Shadowfetch Linux", FailedResolveStreak = 2 };
        HttpService.ApplyResolveOutcome(entry, succeeded: true);
        Assert.Equal(0, entry.FailedResolveStreak);
    }

    [Fact]
    public void ApplyResolveOutcome_FailureWithoutDedicatedResolver_IncrementsStreak()
    {
        var entry = new IsoEntry { Name = "Shadowfetch Linux", FailedResolveStreak = 2 };
        HttpService.ApplyResolveOutcome(entry, succeeded: false);
        Assert.Equal(3, entry.FailedResolveStreak);
    }

    [Fact]
    public void ApplyResolveOutcome_FailureWithDedicatedResolver_DoesNotIncrementStreak()
    {
        // Regression: ein transienter Netzwerk-Hänger bei einer fest unterstützten Distro (Ubuntu)
        // darf NICHT als "Härtefall" gezählt werden — dafür existiert ein funktionierender Resolver.
        var entry = new IsoEntry { Name = "Ubuntu 26.04 Desktop", FailedResolveStreak = 0 };
        HttpService.ApplyResolveOutcome(entry, succeeded: false);
        Assert.Equal(0, entry.FailedResolveStreak);
    }
}
```

- [ ] **Step 2: Tests ausführen, Fehlschlag bestätigen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj --filter HasDedicatedResolver|ApplyResolveOutcome`
Expected: Kompilierfehler — `HttpService.HasDedicatedResolver` und `HttpService.ApplyResolveOutcome`
existieren noch nicht.

- [ ] **Step 3: `HasDedicatedResolver` und `ApplyResolveOutcome` implementieren**

In `Core/Services/HttpService.cs`, direkt vor `ResolveLatestAsync` (vor Zeile 406) einfügen:

```csharp
        /// <summary>
        /// Prüft rein namens-/konfigurationsbasiert (kein Netzwerkzugriff), ob für diesen Eintrag
        /// ein dedizierter Resolver zuständig wäre — unabhängig davon, ob dessen Aufruf gerade
        /// erfolgreich ist. Bildet exakt dieselben Bedingungen ab wie die else-if-Kette in
        /// ResolveLatestAsync, damit ein transienter Netzwerk-Fehlschlag bei einer fest
        /// unterstützten Distro (z.B. Ubuntu) nicht denselben "Härtefall"-Zähler erhöht wie eine
        /// Distro ganz ohne automatische Auflösungsmöglichkeit (z.B. Shadowfetch).
        /// </summary>
        internal static bool HasDedicatedResolver(IsoEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(entry.GithubRepo)) return true;
            string nl = NormalizeForMatch(entry.Name);
            string fl = NormalizeForMatch(entry.Filename);
            string rawFl = entry.Filename.ToLowerInvariant();
            return nl.Contains("gamepack") || nl.Contains("lubuntu") || nl.Contains("ubuntu")
                || nl.Contains("linuxmint") || fl.Contains("linuxmint") || nl.Contains("debian")
                || nl.Contains("tails") || nl.Contains("fedora") || nl.Contains("ultramarine")
                || nl.Contains("parrot") || nl.Contains("zorin") || nl.Contains("popos")
                || nl.Contains("manjaro") || nl.Contains("mxlinux") || rawFl.StartsWith("mx-")
                || nl.Contains("nobara") || nl.Contains("hiren") || nl.Contains("drweb")
                || nl.Contains("finnix") || nl.Contains("cachyos") || nl.Contains("endeavour")
                || nl.Contains("systemrescue") || nl.Contains("gparted") || nl.Contains("clonezilla")
                || nl.Contains("kodachi");
        }

        /// <summary>
        /// Pflegt IsoEntry.FailedResolveStreak nach einem Auflösungsversuch: Erfolg setzt IMMER
        /// zurück auf 0 (gleich über welchen Pfad gefunden), ein Fehlschlag zählt nur hoch, wenn
        /// KEIN dedizierter Resolver zuständig wäre — siehe HasDedicatedResolver.
        /// </summary>
        internal static void ApplyResolveOutcome(IsoEntry entry, bool succeeded)
        {
            if (succeeded) { entry.FailedResolveStreak = 0; return; }
            if (!HasDedicatedResolver(entry)) entry.FailedResolveStreak++;
        }
```

- [ ] **Step 4: Tests ausführen, Erfolg bestätigen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj --filter HasDedicatedResolver|ApplyResolveOutcome`
Expected: PASS (alle 7 neuen Tests)

- [ ] **Step 5: `ResolveLatestAsync` an den beiden Rückgabepunkten verdrahten**

In `Core/Services/HttpService.cs`, `ResolveLatestAsync` (Zeile 406-471):

Ersetze
```csharp
            if (result != Empty) return result;

            var generic = await ResolveGenericAsync(entry).ConfigureAwait(false);
            // Kein dedizierter Resolver hat gegriffen — typischerweise ein manuell hinzugefügter
            // oder vom Stick importierter Eintrag ohne konfigurierte Url/GithubRepo. Eine hier
            // erfolgreich entdeckte Quelle dauerhaft im (persistenten) Url-Feld merken: künftige
            // Prüfungen starten dann direkt darüber statt erneut die langsame generische
            // Auflösung/Websuche zu durchlaufen — TryDiscoverNewerVersionAsync findet über diese
            // gemerkte URL automatisch auch künftige Versions-Updates, sobald der Anbieter eine
            // Verzeichnis-Auflistung nutzt (wie jeder dedizierte Resolver es täte, nur generisch).
            if (generic != Empty && string.IsNullOrWhiteSpace(entry.Url) && string.IsNullOrWhiteSpace(entry.GithubRepo))
                entry.Url = generic.Item2;
            return generic;
        }
```
durch
```csharp
            if (result != Empty)
            {
                ApplyResolveOutcome(entry, succeeded: true);
                return result;
            }

            var generic = await ResolveGenericAsync(entry).ConfigureAwait(false);
            // Kein dedizierter Resolver hat gegriffen — typischerweise ein manuell hinzugefügter
            // oder vom Stick importierter Eintrag ohne konfigurierte Url/GithubRepo. Eine hier
            // erfolgreich entdeckte Quelle dauerhaft im (persistenten) Url-Feld merken: künftige
            // Prüfungen starten dann direkt darüber statt erneut die langsame generische
            // Auflösung/Websuche zu durchlaufen — TryDiscoverNewerVersionAsync findet über diese
            // gemerkte URL automatisch auch künftige Versions-Updates, sobald der Anbieter eine
            // Verzeichnis-Auflistung nutzt (wie jeder dedizierte Resolver es täte, nur generisch).
            if (generic != Empty && string.IsNullOrWhiteSpace(entry.Url) && string.IsNullOrWhiteSpace(entry.GithubRepo))
                entry.Url = generic.Item2;
            ApplyResolveOutcome(entry, succeeded: generic != Empty);
            return generic;
        }
```

- [ ] **Step 6: Vollen Testlauf ausführen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj`
Expected: PASS (keine Regression in den bestehenden Tests)

- [ ] **Step 7: Commit**

```bash
git add Core/Services/HttpService.cs ULM.Tests/HttpServiceTests.cs
git commit -m "feat: FailedResolveStreak-Zaehl-/Reset-Logik in ResolveLatestAsync"
```

---

### Task 3: `Constants.ManualSearchFailureThreshold` + `IsoEntryViewModel.ShowManualSearchButton`

**Files:**
- Modify: `Core/Models/Constants.cs`
- Modify: `ViewModels/IsoViewModels.cs` (`IsoEntryViewModel`, siehe Zeile 98-112 `VersionStatus` für
  Stil, `Refresh()` Zeile 192-206)
- Test: neu `ULM.Tests/IsoEntryViewModelTests.cs`

**Interfaces:**
- Consumes: `IsoEntry.FailedResolveStreak` (aus Task 1)
- Produces: `Constants.ManualSearchFailureThreshold` (`const int`), `IsoEntryViewModel.ShowManualSearchButton` (`bool`, öffentliche Property) — wird von Task 4 (XAML-Binding) konsumiert.

- [ ] **Step 1: Fehlschlagenden Test schreiben**

Neue Datei `ULM.Tests/IsoEntryViewModelTests.cs`:

```csharp
using ULM.Core.Models;
using ULM.ViewModels;
using Xunit;

namespace ULM.Tests;

public class IsoEntryViewModelShowManualSearchButtonTests
{
    [Fact]
    public void ShowManualSearchButton_False_BelowThreshold()
    {
        var entry = new IsoEntry { Name = "Shadowfetch Linux", FailedResolveStreak = 2 };
        var vm = new IsoEntryViewModel(entry, downloadDir: "");
        Assert.False(vm.ShowManualSearchButton);
    }

    [Fact]
    public void ShowManualSearchButton_True_AtThreshold()
    {
        var entry = new IsoEntry { Name = "Shadowfetch Linux", FailedResolveStreak = 3 };
        var vm = new IsoEntryViewModel(entry, downloadDir: "");
        Assert.True(vm.ShowManualSearchButton);
    }

    [Fact]
    public void ShowManualSearchButton_False_ForWellSupportedDistroWithoutFailures()
    {
        var entry = new IsoEntry { Name = "Ubuntu 26.04 Desktop", FailedResolveStreak = 0 };
        var vm = new IsoEntryViewModel(entry, downloadDir: "");
        Assert.False(vm.ShowManualSearchButton);
    }
}
```

- [ ] **Step 2: Test ausführen, Fehlschlag bestätigen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj --filter ShowManualSearchButton`
Expected: Kompilierfehler — `IsoEntryViewModel.ShowManualSearchButton` existiert noch nicht.

- [ ] **Step 3: Konstante hinzufügen**

In `Core/Models/Constants.cs`, direkt nach `public const int AutoCheckIntervalDays = 3;`
(aktuell Zeile 31) einfügen:

```csharp
        public const int  ManualSearchFailureThreshold = 3; // ab so vielen Fehlschlagversuchen in Folge gilt ein Eintrag als Haertefall
```

- [ ] **Step 4: Property implementieren**

In `ViewModels/IsoViewModels.cs`, direkt nach der `VersionStatus`-Property (nach der schließenden
`}` bei aktuell Zeile 112) einfügen:

```csharp
        // Steuert die Sichtbarkeit des "Quelle manuell suchen/eintragen"-Buttons in der Hauptliste
        // (Views/MainWindow.xaml). Bewusst NUR bei einer zusammenhängenden Fehlschlagsserie der
        // automatischen Auflösung sichtbar (siehe HttpService.ApplyResolveOutcome) — der Button ist
        // ein Sicherheitsnetz für Härtefälle wie Shadowfetch, kein Dauerelement in jeder Zeile.
        public bool ShowManualSearchButton => _entry.FailedResolveStreak >= Constants.ManualSearchFailureThreshold;
```

- [ ] **Step 5: `Refresh()` um die neue Property ergänzen**

In `ViewModels/IsoViewModels.cs`, in `Refresh()` (aktuell Zeile 192-206) nach
`OnPropertyChanged(nameof(HashStatusTooltip));` einfügen:

```csharp
            OnPropertyChanged(nameof(ShowManualSearchButton));
```

Ohne diese Zeile würde der Button nach einem Hintergrund-Check (`UrlCheckWorker`/
`AutoVersionCheckWorker`, die `FailedResolveStreak` über bereits existierende
`IsoEntryViewModel`-Instanzen ändern) erst nach einem kompletten Neuaufbau der Liste
(`RebuildTree()`) erscheinen/verschwinden statt sofort.

- [ ] **Step 6: Test ausführen, Erfolg bestätigen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj --filter ShowManualSearchButton`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add Core/Models/Constants.cs ViewModels/IsoViewModels.cs ULM.Tests/IsoEntryViewModelTests.cs
git commit -m "feat: ShowManualSearchButton-Property fuer Haertefall-Schwellenwert"
```

---

### Task 4: Button-Sichtbarkeit in `MainWindow.xaml` + Reset bei manueller Reparatur

**Files:**
- Modify: `Views/MainWindow.xaml` (Zeile 94-99)
- Modify: `Views/MainWindow.xaml.cs` (`BtnManualSearch_Click`, Zeile 546-553)

**Interfaces:**
- Consumes: `IsoEntryViewModel.ShowManualSearchButton` (aus Task 3), bereits registrierter
  `BoolToVis`-Converter ([MainWindow.xaml:10](../../../Views/MainWindow.xaml))

Kein automatisierter Test möglich (reines XAML-Binding + Click-Handler, kein WPF-UI-Testrig im
Projekt vorhanden) — Verifikation erfolgt manuell in Task 5.

- [ ] **Step 1: Button-Visibility binden**

In `Views/MainWindow.xaml`, Zeile 94-99, ersetze

```xml
                    <Button Grid.Column="6" Content="🔧" FontSize="12"
                            Width="24" Height="24" Padding="0"
                            ToolTip="Quelle manuell suchen/eintragen"
                            Click="BtnManualSearch_Click"
                            Style="{DynamicResource BtnGhost}"
                            VerticalAlignment="Center" HorizontalAlignment="Center"/>
```
durch
```xml
                    <Button Grid.Column="6" Content="🔧" FontSize="12"
                            Width="24" Height="24" Padding="0"
                            ToolTip="Quelle manuell suchen/eintragen"
                            Click="BtnManualSearch_Click"
                            Style="{DynamicResource BtnGhost}"
                            Visibility="{Binding ShowManualSearchButton, Converter={StaticResource BoolToVis}}"
                            VerticalAlignment="Center" HorizontalAlignment="Center"/>
```

- [ ] **Step 2: Reset bei erfolgreicher manueller Reparatur**

In `Views/MainWindow.xaml.cs`, `BtnManualSearch_Click` (Zeile 546-553), ersetze

```csharp
        private void BtnManualSearch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.DataContext is not IsoEntryViewModel vm) return;
            var dlg = new ManualSourceSearchDialog(vm.Model) { Owner = this };
            if (dlg.ShowDialog() != true) return;
            IsoDatabaseService.Instance.Save();
            _vm.RebuildTree();
        }
```
durch
```csharp
        private void BtnManualSearch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.DataContext is not IsoEntryViewModel vm) return;
            var dlg = new ManualSourceSearchDialog(vm.Model) { Owner = this };
            if (dlg.ShowDialog() != true) return;
            // Der Nutzer hat den Haertefall gerade selbst geloest — ohne diesen Reset bliebe der
            // Button bis zum naechsten automatischen Check faelschlich sichtbar, obwohl bereits
            // eine URL eingetragen wurde (siehe docs/superpowers/specs/2026-07-18-manual-search-hardcase-design.md).
            if (!string.IsNullOrWhiteSpace(vm.Model.Url)) vm.Model.FailedResolveStreak = 0;
            IsoDatabaseService.Instance.Save();
            _vm.RebuildTree();
        }
```

- [ ] **Step 3: Build ausführen**

Run: `dotnet build ULM.sln` (oder das jeweilige Haupt-`.csproj`, falls keine `.sln` im Root liegt —
vorher mit `ls *.sln *.csproj` prüfen)
Expected: Build erfolgreich, keine XAML-Binding-Fehler in der Konsole.

- [ ] **Step 4: Commit**

```bash
git add Views/MainWindow.xaml Views/MainWindow.xaml.cs
git commit -m "fix: Quelle-manuell-suchen-Button nur noch bei Haertefaellen sichtbar"
```

---

### Task 5: Manuelle Verifikation in der laufenden App

**Files:** keine Code-Änderungen — reiner Verifikationsschritt.

- [ ] **Step 1: App bauen und starten**

Run: `dotnet build ULM.sln` gefolgt vom Start der gebauten `.exe` (oder `dotnet run`, je nach
Hauptprojekt — mit `ls *.csproj` das Startprojekt identifizieren, falls unklar).

- [ ] **Step 2: Bestehende Einträge prüfen**

Beim ersten Start nach dem Update lädt die App die vorhandene `ulm_isos.ini` ohne den neuen Key —
laut Task 1 defaultet `FailedResolveStreak` dann auf `0`. Erwartung: der 🔧-Button ist bei **keiner**
Zeile sichtbar (alle Einträge starten bei 0, unter dem Schwellenwert 3).

- [ ] **Step 3: Härtefall künstlich simulieren**

Temporär in `ViewModels/IsoViewModels.cs` `Constants.ManualSearchFailureThreshold` NICHT ändern —
stattdessen einen Testeintrag direkt in `ulm_isos.ini` (im `%LocalAppData%`- oder Programmordner,
siehe `AppPaths`) mit `FailedResolveStreak = 3` versehen (App vorher schließen, Datei mit
Texteditor bearbeiten, App neu starten). Erwartung: bei genau diesem Eintrag erscheint der
🔧-Button, bei allen anderen nicht.

- [ ] **Step 4: Reset nach manueller Reparatur prüfen**

Auf den sichtbaren 🔧-Button klicken, im Dialog eine beliebige gültige URL eintragen und speichern.
Erwartung: der Button verschwindet sofort (ohne Neustart der App) aus dieser Zeile, da
`FailedResolveStreak` beim Speichern zurückgesetzt wird (Task 4, Step 2) und `Refresh()`/
`RebuildTree()` die UI aktualisiert.

- [ ] **Step 5: Rückmeldung**

Ergebnis (inkl. eventueller Abweichungen von den Erwartungen aus Step 2/3/4) an den Nutzer
zurückmelden — kein Commit in diesem Task, da rein verifizierend.
