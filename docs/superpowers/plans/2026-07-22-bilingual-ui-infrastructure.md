# Zweisprachigkeit (Deutsch/Englisch) Phase 1 — Infrastruktur Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ULM bekommt ein eigenes, typsicheres Text-Übersetzungssystem (Deutsch/Englisch) plus einen Sprach-Umschalter im Hauptfenster — bewiesen an einer repräsentativen Auswahl des Hauptfenster-Rahmens (Tab-Header, vier Kern-Buttons, zwei Tooltips).

**Architektur:** `LocalizationService` (statische Klasse, Pendant zu `ThemeService`) hält die aktuelle Sprache, lädt sie beim Start aus `ulm_settings.ini` (Fallback: Windows-Systemsprache) und liefert Texte über `T(Str key)` aus zwei `Dictionary<Str,string>`-Tabellen. Ein Sprachwechsel wird sofort gespeichert, wirkt aber erst nach einem Neustart von ULM (kein Live-Retexten).

**Tech Stack:** C# / .NET 8 (WPF), keine neuen NuGet-Pakete, keine neuen Build-Schritte.

## Global Constraints

- Persistenz über `ulm_settings.ini`, Schlüssel `Language` unter `[App]`, Werte `de`/`en` — analog zu `ThemeMode`.
- Kein Live-Umschalten — ein Sprachwechsel wirkt erst nach einem ULM-Neustart (Nutzer bestätigt per Dialog).
- Keine .NET-RESX-Ressourcendateien/Satellite-Assemblies — eigene, typsichere Text-Tabelle (`Str`-enum + zwei `Dictionary<Str,string>`), um Konflikte mit `PublishSingleFile` zu vermeiden.
- Keine neuen NuGet-Pakete oder Build-Schritte.
- Phase 1 beschränkt sich auf Infrastruktur + eine repräsentative Migration (3 Tab-Header, 4 Buttons, 2 Tooltips, neuer Sprach-Button). Alle Dialoge und der komplette Log-/Aktivitätsverlauf sind spätere, eigene Phasen — siehe `docs/superpowers/specs/2026-07-22-bilingual-ui-infrastructure-design.md`.
- Deutsche Kommentare, bestehender Codestil (siehe `Infrastructure/ThemeService.cs` als direktes Vorbild).

**Abweichung von der Spec (bewusst, YAGNI):** Die Spec skizzierte
`T(Str key, params object[] args)` mit Format-Platzhaltern für spätere,
interpolierte Log-Meldungen. Kein Str-Wert in Phase 1 braucht das — die
`params`-Variante würde ungetesteten Code erzeugen. Phase 1 implementiert
nur `T(Str key)` / `T(Str key, AppLanguage language)`. Eine
Format-Argument-Overload kommt in einer späteren Phase als
rückwärtskompatible Ergänzung dazu, sobald ein Str-Wert sie tatsächlich
braucht.

---

### Task 1: `LocalizationService` — Text-Tabelle, Lookup, Spracherkennung

**Files:**
- Create: `Infrastructure/AppLanguage.cs`
- Create: `Infrastructure/Str.cs`
- Create: `Infrastructure/LocalizationService.cs`
- Test: `ULM.Tests/LocalizationServiceTests.cs`

**Interfaces:**
- Konsumiert: `Infrastructure/IniService.cs` (`IniService.Read(path, section, key, default)`, `IniService.Write(path, section, key, value)` — bereits vorhanden), `Infrastructure/AppPaths.cs` (`AppPaths.Instance.SettingsIni` — bereits vorhanden).
- Produziert: `ULM.Infrastructure.AppLanguage` (enum: `German`, `English`), `ULM.Infrastructure.Str` (enum, 11 Werte, siehe unten), `LocalizationService.Current : AppLanguage`, `LocalizationService.Initialize() : void`, `LocalizationService.SetLanguage(AppLanguage) : void`, `LocalizationService.T(Str) : string`, `LocalizationService.T(Str, AppLanguage) : string` — werden von Task 3 (MainWindow) und Task 2 (App-Startup) konsumiert.

- [ ] **Step 1: Fehlschlagende Tests schreiben**

Datei `ULM.Tests/LocalizationServiceTests.cs`:

```csharp
using System;
using System.Globalization;
using System.IO;
using ULM.Infrastructure;
using Xunit;

namespace ULM.Tests;

public class LocalizationServiceTTests
{
    [Theory]
    [InlineData(AppLanguage.German, "❓ Hilfe")]
    [InlineData(AppLanguage.English, "❓ Help")]
    public void T_ReturnsCorrectTextForLanguage(AppLanguage language, string expected)
    {
        Assert.Equal(expected, LocalizationService.T(Str.Btn_Help, language));
    }

    [Theory]
    [InlineData(AppLanguage.German, "⬇  Herunterladen")]
    [InlineData(AppLanguage.English, "⬇  Download")]
    public void T_Btn_Download_ReturnsCorrectTextForLanguage(AppLanguage language, string expected)
    {
        Assert.Equal(expected, LocalizationService.T(Str.Btn_Download, language));
    }
}

public class LocalizationServiceDetectFromCultureTests
{
    [Fact]
    public void DetectFromCulture_German_ReturnsGerman()
    {
        Assert.Equal(AppLanguage.German, LocalizationService.DetectFromCulture(new CultureInfo("de-DE")));
    }

    [Fact]
    public void DetectFromCulture_NonGerman_ReturnsEnglish()
    {
        Assert.Equal(AppLanguage.English, LocalizationService.DetectFromCulture(new CultureInfo("fr-FR")));
    }
}

public class LocalizationServiceLoadFromIniTests
{
    [Fact]
    public void LoadFromIni_SavedDe_ReturnsGerman()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"ulm-loc-{Guid.NewGuid():N}.ini");
        try
        {
            IniService.Write(tempFile, "App", "Language", "de");
            Assert.Equal(AppLanguage.German, LocalizationService.LoadFromIni(tempFile));
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void LoadFromIni_SavedEn_ReturnsEnglish()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"ulm-loc-{Guid.NewGuid():N}.ini");
        try
        {
            IniService.Write(tempFile, "App", "Language", "en");
            Assert.Equal(AppLanguage.English, LocalizationService.LoadFromIni(tempFile));
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void LoadFromIni_MissingFile_FallsBackToCultureDetection()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"ulm-loc-{Guid.NewGuid():N}.ini");
        // Datei existiert bewusst nicht — IniService.Read liefert den uebergebenen Default "" zurueck,
        // LoadFromIni faellt dann auf DetectFromCulture(CurrentUICulture) zurueck.
        AppLanguage expected = LocalizationService.DetectFromCulture(CultureInfo.CurrentUICulture);
        Assert.Equal(expected, LocalizationService.LoadFromIni(tempFile));
    }
}

public class LocalizationServiceSetLanguageTests
{
    [Fact]
    public void SetLanguage_WritesToIniAndUpdatesCurrent()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"ulm-loc-{Guid.NewGuid():N}.ini");
        try
        {
            LocalizationService.SetLanguage(AppLanguage.English, tempFile);

            Assert.Equal(AppLanguage.English, LocalizationService.Current);
            Assert.Equal("en", IniService.Read(tempFile, "App", "Language", ""));
        }
        finally { File.Delete(tempFile); }
    }
}

public class LocalizationServiceCompletenessTests
{
    [Fact]
    public void AllStrValues_HaveGermanAndEnglishTranslation()
    {
        foreach (Str key in Enum.GetValues<Str>())
        {
            string de = LocalizationService.T(key, AppLanguage.German);
            string en = LocalizationService.T(key, AppLanguage.English);
            Assert.False(string.IsNullOrWhiteSpace(de), $"Fehlende deutsche Übersetzung für {key}");
            Assert.False(string.IsNullOrWhiteSpace(en), $"Fehlende englische Übersetzung für {key}");
        }
    }
}
```

- [ ] **Step 2: Tests laufen lassen, Fehlschlag bestätigen**

Run: `dotnet test ULM.Tests --filter "FullyQualifiedName~LocalizationService"`
Expected: Kompilierfehler — `AppLanguage`, `Str` und `LocalizationService` existieren noch nicht.

- [ ] **Step 3: `AppLanguage.cs` erstellen**

```csharp
// Infrastructure/AppLanguage.cs
namespace ULM.Infrastructure
{
    public enum AppLanguage { German, English }
}
```

- [ ] **Step 4: `Str.cs` erstellen**

```csharp
// Infrastructure/Str.cs
namespace ULM.Infrastructure
{
    // Ein Eintrag pro übersetzbarem Text im Programm. Phase 1 deckt nur den
    // Hauptfenster-Rahmen ab (siehe
    // docs/superpowers/specs/2026-07-22-bilingual-ui-infrastructure-design.md) —
    // weitere Phasen erweitern dieses enum um Dialoge und den
    // Log-/Aktivitätsverlauf.
    public enum Str
    {
        Tab_IsoSelection,
        Tab_Log,
        Tab_Status,
        Btn_Download,
        Btn_CheckForUpdates,
        Btn_Cancel,
        Btn_Help,
        Tooltip_ThemeToggle,
        Tooltip_LanguageToggle,
        LanguageChangeConfirm_Title,
        LanguageChangeConfirm_Message,
    }
}
```

- [ ] **Step 5: `LocalizationService.cs` erstellen**

```csharp
// Infrastructure/LocalizationService.cs
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ULM.Infrastructure
{
    // Pendant zu ThemeService: statische Klasse, Initialize() beim Programmstart,
    // Persistenz über IniService/ulm_settings.ini. Ein Sprachwechsel wirkt bewusst
    // NICHT live (anders als ThemeService) — siehe Design-Entscheidung in
    // docs/superpowers/specs/2026-07-22-bilingual-ui-infrastructure-design.md:
    // "Neustart-Hinweis reicht" statt vollständigem Live-Retexten.
    public static class LocalizationService
    {
        public static AppLanguage Current { get; private set; } = AppLanguage.German;

        public static void Initialize() => Current = LoadFromIni(AppPaths.Instance.SettingsIni);

        // Testbar ohne Application.Current/UI-Zugriff — nur Datei-IO über IniService.
        internal static AppLanguage LoadFromIni(string settingsIniPath)
        {
            string saved = IniService.Read(settingsIniPath, "App", "Language", "");
            return saved switch
            {
                "de" => AppLanguage.German,
                "en" => AppLanguage.English,
                _    => DetectFromCulture(CultureInfo.CurrentUICulture),
            };
        }

        // Reine Logik, testbar ohne die echte Systemsprache umstellen zu müssen.
        internal static AppLanguage DetectFromCulture(CultureInfo culture) =>
            string.Equals(culture.TwoLetterISOLanguageName, "de", StringComparison.OrdinalIgnoreCase)
                ? AppLanguage.German
                : AppLanguage.English;

        public static void SetLanguage(AppLanguage lang) => SetLanguage(lang, AppPaths.Instance.SettingsIni);

        internal static void SetLanguage(AppLanguage lang, string settingsIniPath)
        {
            Current = lang;
            IniService.Write(settingsIniPath, "App", "Language", lang == AppLanguage.German ? "de" : "en");
        }

        public static string T(Str key) => T(key, Current);

        public static string T(Str key, AppLanguage language) =>
            language == AppLanguage.German ? De[key] : En[key];

        private static readonly Dictionary<Str, string> De = new()
        {
            [Str.Tab_IsoSelection]             = "ISO-Auswahl",
            [Str.Tab_Log]                      = "Protokoll",
            [Str.Tab_Status]                   = "Status",
            [Str.Btn_Download]                 = "⬇  Herunterladen",
            [Str.Btn_CheckForUpdates]          = "↻  Updates prüfen",
            [Str.Btn_Cancel]                   = "✕  Stopp",
            [Str.Btn_Help]                     = "❓ Hilfe",
            [Str.Tooltip_ThemeToggle]          = "Erscheinungsbild umschalten: System / Hell / Dunkel",
            [Str.Tooltip_LanguageToggle]       = "Sprache wechseln",
            [Str.LanguageChangeConfirm_Title]  = "Sprache geändert",
            [Str.LanguageChangeConfirm_Message] = "ULM jetzt neu starten, um die neue Sprache zu übernehmen?",
        };

        private static readonly Dictionary<Str, string> En = new()
        {
            [Str.Tab_IsoSelection]             = "ISO Selection",
            [Str.Tab_Log]                      = "Log",
            [Str.Tab_Status]                   = "Status",
            [Str.Btn_Download]                 = "⬇  Download",
            [Str.Btn_CheckForUpdates]          = "↻  Check for Updates",
            [Str.Btn_Cancel]                   = "✕  Stop",
            [Str.Btn_Help]                     = "❓ Help",
            [Str.Tooltip_ThemeToggle]          = "Switch appearance: System / Light / Dark",
            [Str.Tooltip_LanguageToggle]       = "Change language",
            [Str.LanguageChangeConfirm_Title]  = "Language changed",
            [Str.LanguageChangeConfirm_Message] = "Restart ULM now to apply the new language?",
        };
    }
}
```

- [ ] **Step 6: Tests laufen lassen, Erfolg bestätigen**

Run: `dotnet test ULM.Tests --filter "FullyQualifiedName~LocalizationService"`
Expected: alle Tests grün.

- [ ] **Step 7: Vollständige Testsuite laufen lassen**

Run: `dotnet test ULM.Tests`
Expected: alle Tests grün (keine Regression in bestehenden Tests).

- [ ] **Step 8: Commit**

```bash
git add Infrastructure/AppLanguage.cs Infrastructure/Str.cs Infrastructure/LocalizationService.cs ULM.Tests/LocalizationServiceTests.cs
git commit -m "feat: LocalizationService fuer Deutsch/Englisch-Texttabelle mit Spracherkennung"
```

---

### Task 2: App-Startup — Sprache initialisieren

**Files:**
- Modify: `App.xaml.cs`

**Interfaces:**
- Konsumiert: `LocalizationService.Initialize()` aus Task 1.
- Produziert: nichts, das andere Tasks konsumieren — reine Startup-Verdrahtung, analog zu `ThemeService.Initialize()`.

- [ ] **Step 1: `LocalizationService.Initialize()` in `OnStartup` einbauen**

In `App.xaml.cs`, direkt nach der bestehenden Zeile

```csharp
            ThemeService.Initialize();
```

folgende Zeile einfügen:

```csharp
            LocalizationService.Initialize();
```

- [ ] **Step 2: Build prüfen**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: `Build succeeded.`, keine neuen Warnungen.

- [ ] **Step 3: Commit**

```bash
git add App.xaml.cs
git commit -m "feat: Sprache wird beim Programmstart initialisiert"
```

---

### Task 3: MainWindow — Sprach-Umschalter + Beispiel-Migration

**Files:**
- Modify: `Views/MainWindow.xaml:389` (TabItem "ISO-Auswahl" — `x:Name` ergänzen)
- Modify: `Views/MainWindow.xaml:280-283` (neuer `BtnLanguageToggle` direkt nach `BtnThemeToggle`)
- Modify: `Views/MainWindow.xaml.cs` (`ApplyLocalizedText()`, `UpdateLanguageButtonLabel()`, `BtnLanguageToggle_Click`, Aufruf im Konstruktor)

**Interfaces:**
- Konsumiert: `LocalizationService.T(Str)`, `LocalizationService.T(Str, AppLanguage)`, `LocalizationService.Current`, `LocalizationService.SetLanguage(AppLanguage)` aus Task 1; `GetCurrentExePath()` (bereits vorhanden in `MainWindow.xaml.cs`, genutzt vom Selbst-Update-Feature).
- Produziert: nichts, das andere Tasks konsumieren — UI-Endpunkt.

- [ ] **Step 1: `x:Name` an TabItem "ISO-Auswahl" ergänzen**

In `Views/MainWindow.xaml`, Zeile 389, den bestehenden Block

```xml
            <TabItem Header="ISO-Auswahl">
```

ersetzen durch:

```xml
            <TabItem x:Name="IsoTab" Header="ISO-Auswahl">
```

- [ ] **Step 2: Sprach-Umschalter-Button in XAML einfügen**

In `Views/MainWindow.xaml`, den bestehenden Block (Zeilen 280-283)

```xml
                    <Button x:Name="BtnThemeToggle" Style="{DynamicResource BtnGhost}"
                            Foreground="White" BorderBrush="#4A6785"
                            ToolTip="Erscheinungsbild umschalten: System / Hell / Dunkel"
                            Click="BtnThemeToggle_Click" Width="130" Margin="0,0,8,0"/>
```

ersetzen durch (neuer Button direkt danach eingefügt, Tooltip wird ab jetzt aus
`LocalizationService` gesetzt statt hart in XAML zu stehen):

```xml
                    <Button x:Name="BtnThemeToggle" Style="{DynamicResource BtnGhost}"
                            Foreground="White" BorderBrush="#4A6785"
                            Click="BtnThemeToggle_Click" Width="130" Margin="0,0,8,0"/>

                    <Button x:Name="BtnLanguageToggle" Style="{DynamicResource BtnGhost}"
                            Foreground="White" BorderBrush="#4A6785"
                            Click="BtnLanguageToggle_Click" Width="110" Margin="0,0,8,0"/>
```

- [ ] **Step 3: `ApplyLocalizedText()`, `UpdateLanguageButtonLabel()` und `BtnLanguageToggle_Click` in `Views/MainWindow.xaml.cs` einbauen**

Den bestehenden Block

```csharp
        private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            AppThemeMode next = ThemeService.CurrentMode switch
            {
                AppThemeMode.System => AppThemeMode.Light,
                AppThemeMode.Light  => AppThemeMode.Dark,
                _                   => AppThemeMode.System,
            };
            ThemeService.SetMode(next);
        }
```

ersetzen durch (bestehender Block unverändert, drei neue Methoden direkt danach):

```csharp
        private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            AppThemeMode next = ThemeService.CurrentMode switch
            {
                AppThemeMode.System => AppThemeMode.Light,
                AppThemeMode.Light  => AppThemeMode.Dark,
                _                   => AppThemeMode.System,
            };
            ThemeService.SetMode(next);
        }

        // ── Sprache (Deutsch/Englisch) ──────────────────────────────────────
        // Wirkt bewusst NICHT live (anders als der Theme-Umschalter oben) — ein
        // Sprachwechsel wird sofort gespeichert, greift aber erst nach einem
        // Neustart von ULM. Siehe docs/superpowers/specs/2026-07-22-bilingual-ui-infrastructure-design.md.
        private void ApplyLocalizedText()
        {
            IsoTab.Header    = LocalizationService.T(Str.Tab_IsoSelection);
            LogTab.Header    = LocalizationService.T(Str.Tab_Log);
            StatusTab.Header = LocalizationService.T(Str.Tab_Status);
            BtnDownload.Content = LocalizationService.T(Str.Btn_Download);
            BtnUpdates.Content  = LocalizationService.T(Str.Btn_CheckForUpdates);
            BtnCancel.Content   = LocalizationService.T(Str.Btn_Cancel);
            BtnHelp.Content     = LocalizationService.T(Str.Btn_Help);
            BtnThemeToggle.ToolTip = LocalizationService.T(Str.Tooltip_ThemeToggle);
            UpdateLanguageButtonLabel();
        }

        // Zeigt die JEWEILS ANDERE Sprache als Klick-Ziel an (Sprachnamen werden
        // immer in der eigenen Sprache angezeigt, unabhängig von der aktuell
        // aktiven UI-Sprache — üblicherweise Konvention bei Sprachumschaltern).
        private void UpdateLanguageButtonLabel()
        {
            BtnLanguageToggle.Content = LocalizationService.Current == AppLanguage.German ? "🌐 English" : "🌐 Deutsch";
            BtnLanguageToggle.ToolTip = LocalizationService.T(Str.Tooltip_LanguageToggle);
        }

        private void BtnLanguageToggle_Click(object sender, RoutedEventArgs e)
        {
            AppLanguage oldLang = LocalizationService.Current;
            AppLanguage newLang = oldLang == AppLanguage.German ? AppLanguage.English : AppLanguage.German;

            string title   = LocalizationService.T(Str.LanguageChangeConfirm_Title, oldLang);
            string message = LocalizationService.T(Str.LanguageChangeConfirm_Message, oldLang);

            LocalizationService.SetLanguage(newLang);
            UpdateLanguageButtonLabel();

            if (MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo(GetCurrentExePath()) { UseShellExecute = true });
                Application.Current.Shutdown();
            }
        }
```

- [ ] **Step 4: `ApplyLocalizedText()` im Konstruktor aufrufen**

Den bestehenden Block

```csharp
            Title = Constants.AppFullTitle;
            UpdateThemeButtonLabel();
            _vm = new MainViewModel(Dispatcher);
```

ersetzen durch:

```csharp
            Title = Constants.AppFullTitle;
            UpdateThemeButtonLabel();
            ApplyLocalizedText();
            _vm = new MainViewModel(Dispatcher);
```

- [ ] **Step 5: Build prüfen**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: `Build succeeded.`, keine neuen Warnungen — insbesondere keine Fehler zu `IsoTab`/`BtnLanguageToggle` (beide müssen von `InitializeComponent()` als generierte Felder erkannt werden, das passiert automatisch über `x:Name` in der XAML).

- [ ] **Step 6: Volle Testsuite laufen lassen**

Run: `dotnet test ULM.Tests`
Expected: alle Tests grün.

- [ ] **Step 7: Commit**

```bash
git add Views/MainWindow.xaml Views/MainWindow.xaml.cs
git commit -m "feat: Sprach-Umschalter im Hauptfenster (Deutsch/Englisch), Beispiel-Migration Tab-Header + 4 Buttons"
```

---

### Task 4: Manuelle End-to-End-Verifikation

**Files:** keine Code-Änderungen — reine Verifikation.

**Interfaces:** keine.

- [ ] **Step 1: App mit Deutsch starten und Ausgangszustand prüfen**

Sicherstellen, dass `ulm_settings.ini` (Pfad: entweder `<ULM_Data>\ulm_settings.ini`
im portablen Modus oder gemäß `BaseDirectory`-Eintrag) entweder keinen
`Language`-Schlüssel hat oder `Language = de` — dann `dotnet run
--project UniversalLinuxManager.csproj -c Debug` starten (oder die
gebaute EXE aus `bin/Debug/net8.0-windows/win-x64/` direkt ausführen).

Erwartet:
- Tab-Header: „ISO-Auswahl“, „Protokoll“, „Status“
- Buttons: „⬇  Herunterladen“, „↻  Updates prüfen“, „❓ Hilfe“
- Sprach-Button zeigt „🌐 English“ (Ziel-Sprache)
- Theme-Button-Tooltip (Hover): „Erscheinungsbild umschalten: System / Hell / Dunkel“

- [ ] **Step 2: Sprach-Button klicken, Dialog prüfen**

Klick auf „🌐 English“.

Erwartet: Dialog mit Titel „Sprache geändert“ und Text „ULM jetzt neu
starten, um die neue Sprache zu übernehmen?“ (auf Deutsch, da vor dem
Klick Deutsch aktiv war), Ja/Nein-Buttons.

- [ ] **Step 3: Neustart bestätigen, Englisch prüfen**

Klick auf „Ja“.

Erwartet:
- ULM schließt sich und startet automatisch neu (gleicher Ablauf wie beim
  Selbst-Update-Neustart, hier aber ohne Datei-Austausch — nur derselbe
  Prozess wird neu gestartet).
- Nach dem Neustart: Tab-Header „ISO Selection“, „Log“, „Status“; Buttons
  „⬇  Download“, „↻  Check for Updates“, „❓ Help“; Sprach-Button zeigt jetzt
  „🌐 Deutsch“.
- `ulm_settings.ini` enthält jetzt `Language = en`.

- [ ] **Step 4: Zurück zu Deutsch wechseln**

Klick auf „🌐 Deutsch“, Dialog erscheint diesmal auf Englisch („Language
changed“ / „Restart ULM now to apply the new language?“), „Yes“ klicken,
Neustart abwarten.

Erwartet: alles wieder auf Deutsch wie in Step 1, `ulm_settings.ini`
enthält wieder `Language = de`.

- [ ] **Step 5: Bei Erfolg — nichts weiter zu tun**

Falls einer der Punkte in Step 1–4 nicht stimmt, zurück zu Phase 1 der
systematic-debugging-Skill (neue Evidenz sammeln, nicht direkt erneut
fixen).

---

## Self-Review

**Spec-Abdeckung:**
- `LocalizationService`/`Str`/`AppLanguage` (Architektur-Abschnitt der Spec) → Task 1. ✅
- Persistenz in `ulm_settings.ini` unter `[App] Language` (Spec-Abschnitt "Persistenz") → Task 1 (`LoadFromIni`/`SetLanguage`) + Task 2 (Aufruf beim Start). ✅
- Windows-Spracherkennung als Fallback (Spec-Abschnitt "Persistenz") → Task 1 (`DetectFromCulture`). ✅
- Sprach-Umschalter-Button + Neustart-Hinweis (Spec-Abschnitt "UI: Sprach-Umschalter") → Task 3. ✅
- Beispiel-Migration Hauptfenster-Rahmen (Spec-Abschnitt "Migration") → Task 3 (Tab-Header + 4 Buttons + 2 Tooltips als repräsentative, in der echten XAML vorhandene Auswahl). ✅
- Vollständigkeits-Test (Spec-Abschnitt "Testing") → Task 1 (`LocalizationServiceCompletenessTests`). ✅
- Manuelle Verifikation (Spec-Abschnitt "Manuelle Verifikation") → Task 4. ✅
- Spätere Phasen (Dialoge, Log-Verlauf) bewusst NICHT Teil dieses Plans — siehe Global Constraints. ✅

**Platzhalter-Scan:** Keine "TBD"/"implement later"/unvollständigen Code-Blöcke — jeder Step enthält vollständigen, copy-paste-fähigen Code oder ein konkretes Kommando mit erwartetem Ergebnis. Die eine bewusste Abweichung von der Spec (kein `params object[] args`) ist explizit begründet, keine offene Frage.

**Typkonsistenz:** `LocalizationService.T(Str)`, `T(Str, AppLanguage)`, `LoadFromIni(string)`, `DetectFromCulture(CultureInfo)`, `SetLanguage(AppLanguage)`, `SetLanguage(AppLanguage, string)` werden in Task 1 exakt so definiert und in Task 1 (Tests), Task 2 (`Initialize()`-Aufruf) und Task 3 (`T(Str)`, `T(Str, AppLanguage)`, `Current`, `SetLanguage(AppLanguage)`) konsistent mit derselben Signatur verwendet.
