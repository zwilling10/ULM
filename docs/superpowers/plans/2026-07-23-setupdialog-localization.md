# SetupDialog lokalisieren (Zweisprachigkeit Phase 2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Alle 31 hart-deutschen Textstellen in `Views/Dialogs/SetupDialogs.cs` (`SetupDialog`) laufen über `LocalizationService.T(Str...)`, damit der Dialog bei `Language = en` — sowohl beim Erststart als auch beim erneuten Öffnen über „⚙ Einstellungen" — vollständig auf Englisch erscheint.

**Architektur:** 31 neue `Str`-Enum-Werte (`Infrastructure/Str.cs`) mit je einem deutschen und englischen Eintrag in den bestehenden `De`/`En`-Dictionaries in `Infrastructure/LocalizationService.cs`. `SetupDialogs.cs` ersetzt jede hartcodierte Zeichenkette 1:1 durch den passenden `LocalizationService.T(Str.Setup_...)`-Aufruf — keine Strukturänderung an Layout/Controls.

**Tech Stack:** C# / .NET 8 (WPF), xUnit, keine neuen NuGet-Pakete.

## Global Constraints

- Sprach-Buttons („🇩🇪 Deutsch" / „🇬🇧 English") bleiben hartcodiert — Sprach-Eigennamen werden nie übersetzt.
- Theme-Buttons ("System"/"Hell"/"Dunkel") werden übersetzt — normale Wörter, keine Eigennamen.
- Fehlermeldung per String-Verkettung: `LocalizationService.T(Str.Setup_Error_FolderCreateFailed) + "\n" + ex.Message` statt neuem `T()`-Platzhalter-Parameter.
- `SetupDialog` wird bei jedem Öffnen neu konstruiert — kein Live-Retexten/Event-Mechanismus nötig, einmaliges `T(...)`-Auslesen beim Aufbau reicht.
- Umfang bleibt strikt auf `SetupDialogs.cs` beschränkt (keine anderen Dialoge, kein Log-Verlauf).
- Kein Unit-Test-Harness für WPF-Dialoge in diesem Projekt (bestehende Konvention) — `SetupDialogs.cs`-Änderungen werden über Build-Erfolg + manuelle Verifikation abgesichert, nicht über neue UI-Tests.
- Der bestehende Vollständigkeitstest (`LocalizationServiceCompletenessTests.AllStrValues_HaveGermanAndEnglishTranslation`) deckt alle neuen `Str`-Werte automatisch ab, sobald sie in `De`/`En` eingetragen sind — kein manuelles Ergänzen dieses Tests nötig.

---

### Task 1: `Str.cs` — 31 neue Enum-Werte

**Files:**
- Modify: `Infrastructure/Str.cs`

**Interfaces:**
- Produziert: 31 neue `Str`-Enum-Werte (`Setup_Title_Welcome` … `Setup_Error_FolderCreateFailed`, exakte Liste siehe Step 1) — werden von Task 2 (Dictionaries) und Task 3–5 (`SetupDialogs.cs`) konsumiert.

- [ ] **Step 1: Enum-Werte ergänzen**

In `Infrastructure/Str.cs` den bestehenden Block

```csharp
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
        Btn_Settings,
        LanguageChangeConfirm_Title,
        LanguageChangeConfirm_Message,
    }
}
```

ersetzen durch:

```csharp
namespace ULM.Infrastructure
{
    // Ein Eintrag pro übersetzbarem Text im Programm. Phase 1 deckt den
    // Hauptfenster-Rahmen ab, Phase 2 zusätzlich SetupDialog (siehe
    // docs/superpowers/specs/2026-07-22-bilingual-ui-infrastructure-design.md,
    // docs/superpowers/specs/2026-07-23-setupdialog-localization-design.md) —
    // weitere Phasen erweitern dieses enum um die restlichen Dialoge und den
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
        Btn_Settings,
        LanguageChangeConfirm_Title,
        LanguageChangeConfirm_Message,

        // SetupDialog: Kopfzeile
        Setup_Title_Welcome,
        Setup_Title_Settings,
        Setup_Header_Welcome,
        Setup_Header_Settings,
        Setup_Subtitle_Welcome,
        Setup_Subtitle_Settings,

        // SetupDialog: Arbeitsordner-Karte
        Setup_Directory_Header,
        Setup_Btn_Browse,
        Setup_FolderDialog_Title,
        Setup_Btn_UseDefaultPath,
        Setup_Directory_ItemsIntro,
        Setup_Directory_ItemDownloads,
        Setup_Directory_ItemDatabase,
        Setup_Directory_ItemLog,

        // SetupDialog: Über-ULM-Karte (nur Erststart)
        Setup_Card_AboutUlm,
        Setup_WelcomeBody,

        // SetupDialog: Modus-Karte
        Setup_Card_Mode,
        Setup_Chk_ExpertMode,
        Setup_Hint_Mode,

        // SetupDialog: Autostart-Karte
        Setup_Card_Autostart,
        Setup_Chk_Autostart,
        Setup_Hint_Autostart,

        // SetupDialog: Design-Karte
        Setup_Card_Design,
        Setup_Theme_System,
        Setup_Theme_Light,
        Setup_Theme_Dark,
        Setup_Hint_Theme,

        // SetupDialog: Sprache-Karte (Buttons selbst bleiben hartcodiert)
        Setup_Card_Language,
        Setup_Hint_Language,

        // SetupDialog: Fußzeile
        Setup_Chk_DontShowAgain,
        Setup_Btn_Apply,

        // SetupDialog: Fehler
        Setup_Error_Title,
        Setup_Error_FolderCreateFailed,
    }
}
```

- [ ] **Step 2: Build prüfen**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: `Build succeeded.` (die neuen Enum-Werte werden noch nirgends verwendet, das ist unschädlich).

- [ ] **Step 3: Commit**

```bash
git add Infrastructure/Str.cs
git commit -m "feat: Str-Enum um 31 SetupDialog-Eintraege erweitert"
```

---

### Task 2: `LocalizationService.cs` — Übersetzungen für die 31 neuen Einträge

**Files:**
- Modify: `Infrastructure/LocalizationService.cs`
- Test: `ULM.Tests/LocalizationServiceTests.cs`

**Interfaces:**
- Konsumiert: die 31 `Str`-Werte aus Task 1.
- Produziert: `LocalizationService.T(Str.Setup_...)` liefert für alle 31 neuen Werte in beiden Sprachen einen nicht-leeren String — wird von Task 3–5 (`SetupDialogs.cs`) konsumiert.

- [ ] **Step 1: Neue Einträge im `De`-Dictionary ergänzen**

In `Infrastructure/LocalizationService.cs` die letzte Zeile vor der schließenden `};` des `De`-Dictionary

```csharp
            [Str.LanguageChangeConfirm_Title]  = "Sprache geändert",
            [Str.LanguageChangeConfirm_Message] = "ULM jetzt neu starten, um die neue Sprache zu übernehmen?",
        };
```

ersetzen durch:

```csharp
            [Str.LanguageChangeConfirm_Title]  = "Sprache geändert",
            [Str.LanguageChangeConfirm_Message] = "ULM jetzt neu starten, um die neue Sprache zu übernehmen?",

            [Str.Setup_Title_Welcome]            = "Universal Linux Manager — Einrichtung",
            [Str.Setup_Title_Settings]           = "Universal Linux Manager — Einstellungen",
            [Str.Setup_Header_Welcome]           = "Willkommen beim Universal Linux Manager",
            [Str.Setup_Header_Settings]          = "Einstellungen",
            [Str.Setup_Subtitle_Welcome]         = "Kurze Einrichtung, dann kann's losgehen.",
            [Str.Setup_Subtitle_Settings]        = "Änderungen wirken nach Klick auf „✔ Übernehmen“.",

            [Str.Setup_Directory_Header]         = "Speicherort für ISO-Downloads und Einstellungsdateien:",
            [Str.Setup_Btn_Browse]               = "📂 Durchsuchen",
            [Str.Setup_FolderDialog_Title]       = "Arbeitsverzeichnis für den Universal Linux Manager wählen",
            [Str.Setup_Btn_UseDefaultPath]       = "Standard-Pfad übernehmen",
            [Str.Setup_Directory_ItemsIntro]     = "Folgende Elemente werden angelegt:",
            [Str.Setup_Directory_ItemDownloads]  = "ISO-Downloads",
            [Str.Setup_Directory_ItemDatabase]   = "ISO-Datenbank",
            [Str.Setup_Directory_ItemLog]        = "Protokolldatei",

            [Str.Setup_Card_AboutUlm]            = "ℹ Über ULM",
            [Str.Setup_WelcomeBody]              =
                "Mit diesem Tool kannst du mühelos 20–30 verschiedene Linux-Distributionen verwalten, " +
                "automatisch die neuesten ISOs herunterladen und diese bootfähig auf deinen Ventoy-USB-Stick übertragen.\n\n" +
                "Features im Überblick:\n" +
                "• Automatisierte URL-Prüfung & Versions-Check\n" +
                "• Integrierte Ventoy-Installation & Secure-Boot-Support\n" +
                "• Parallele Downloads für maximale Performance",

            [Str.Setup_Card_Mode]                = "👤 Modus",
            [Str.Setup_Chk_ExpertMode]           = "Experten-Modus aktivieren (alle Funktionen sichtbar)",
            [Str.Setup_Hint_Mode]                =
                "Bestimmt, wie viele Funktionen und erweiterte Einstellungen im Hauptprogramm angezeigt werden. " +
                "Unmarkiert = Anwender-Modus (empfohlen). Der Modus kann später jederzeit über ⚙ Einstellungen oben rechts geändert werden.",

            [Str.Setup_Card_Autostart]           = "🚀 Autostart",
            [Str.Setup_Chk_Autostart]            = "Mit Windows starten",
            [Str.Setup_Hint_Autostart]           =
                "ULM startet dann automatisch (sichtbares Fenster) bei jeder Windows-Anmeldung. " +
                "Kein Admin-Recht nötig. Kann später hier jederzeit wieder deaktiviert werden.",

            [Str.Setup_Card_Design]              = "🌓 Design",
            [Str.Setup_Theme_System]             = "System",
            [Str.Setup_Theme_Light]              = "Hell",
            [Str.Setup_Theme_Dark]               = "Dunkel",
            [Str.Setup_Hint_Theme]               =
                "\"System\" übernimmt automatisch die aktuelle Windows-Einstellung. Kann später jederzeit " +
                "über ⚙ Einstellungen oben rechts geändert werden — auch live, ohne Neustart.",

            [Str.Setup_Card_Language]            = "🌐 Sprache",
            [Str.Setup_Hint_Language]            = "Wirkt nach einem Neustart von ULM. Kann später jederzeit über ⚙ Einstellungen oben rechts geändert werden.",

            [Str.Setup_Chk_DontShowAgain]        = "Diese Einrichtung beim nächsten Start überspringen (Modus wird gespeichert)",
            [Str.Setup_Btn_Apply]                = "✔ Übernehmen",

            [Str.Setup_Error_Title]              = "Fehler",
            [Str.Setup_Error_FolderCreateFailed] = "Ordner konnte nicht erstellt werden:",
        };
```

- [ ] **Step 2: Neue Einträge im `En`-Dictionary ergänzen**

Die letzte Zeile vor der schließenden `};` des `En`-Dictionary

```csharp
            [Str.LanguageChangeConfirm_Title]  = "Language changed",
            [Str.LanguageChangeConfirm_Message] = "Restart ULM now to apply the new language?",
        };
```

ersetzen durch:

```csharp
            [Str.LanguageChangeConfirm_Title]  = "Language changed",
            [Str.LanguageChangeConfirm_Message] = "Restart ULM now to apply the new language?",

            [Str.Setup_Title_Welcome]            = "Universal Linux Manager — Setup",
            [Str.Setup_Title_Settings]           = "Universal Linux Manager — Settings",
            [Str.Setup_Header_Welcome]           = "Welcome to Universal Linux Manager",
            [Str.Setup_Header_Settings]          = "Settings",
            [Str.Setup_Subtitle_Welcome]         = "Quick setup, then you're ready to go.",
            [Str.Setup_Subtitle_Settings]        = "Changes take effect after clicking “✔ Apply”.",

            [Str.Setup_Directory_Header]         = "Storage location for ISO downloads and settings files:",
            [Str.Setup_Btn_Browse]               = "📂 Browse",
            [Str.Setup_FolderDialog_Title]       = "Choose a working directory for Universal Linux Manager",
            [Str.Setup_Btn_UseDefaultPath]       = "Use default path",
            [Str.Setup_Directory_ItemsIntro]     = "The following items will be created:",
            [Str.Setup_Directory_ItemDownloads]  = "ISO downloads",
            [Str.Setup_Directory_ItemDatabase]   = "ISO database",
            [Str.Setup_Directory_ItemLog]        = "Log file",

            [Str.Setup_Card_AboutUlm]            = "ℹ About ULM",
            [Str.Setup_WelcomeBody]              =
                "With this tool you can effortlessly manage 20–30 different Linux distributions, " +
                "automatically download the latest ISOs, and transfer them to your bootable Ventoy USB stick.\n\n" +
                "Features at a glance:\n" +
                "• Automated URL checking & version detection\n" +
                "• Integrated Ventoy installation & Secure Boot support\n" +
                "• Parallel downloads for maximum performance",

            [Str.Setup_Card_Mode]                = "👤 Mode",
            [Str.Setup_Chk_ExpertMode]           = "Enable expert mode (all features visible)",
            [Str.Setup_Hint_Mode]                =
                "Determines how many features and advanced settings are shown in the main program. " +
                "Unchecked = user mode (recommended). The mode can be changed later at any time via ⚙ Settings in the top right.",

            [Str.Setup_Card_Autostart]           = "🚀 Autostart",
            [Str.Setup_Chk_Autostart]            = "Start with Windows",
            [Str.Setup_Hint_Autostart]           =
                "ULM will then start automatically (visible window) at every Windows login. " +
                "No admin rights required. Can be disabled again here at any time.",

            [Str.Setup_Card_Design]              = "🌓 Theme",
            [Str.Setup_Theme_System]             = "System",
            [Str.Setup_Theme_Light]              = "Light",
            [Str.Setup_Theme_Dark]               = "Dark",
            [Str.Setup_Hint_Theme]               =
                "\"System\" automatically follows the current Windows setting. Can be changed later at any time " +
                "via ⚙ Settings in the top right — even live, without a restart.",

            [Str.Setup_Card_Language]            = "🌐 Language",
            [Str.Setup_Hint_Language]            = "Takes effect after restarting ULM. Can be changed later at any time via ⚙ Settings in the top right.",

            [Str.Setup_Chk_DontShowAgain]        = "Skip this setup on next start (mode will be saved)",
            [Str.Setup_Btn_Apply]                = "✔ Apply",

            [Str.Setup_Error_Title]              = "Error",
            [Str.Setup_Error_FolderCreateFailed] = "Could not create folder:",
        };
```

- [ ] **Step 3: Spot-Tests ergänzen**

In `ULM.Tests/LocalizationServiceTests.cs` in der Klasse `LocalizationServiceTTests` nach der bestehenden `T_Btn_Download_ReturnsCorrectTextForLanguage`-Methode (vor der schließenden `}` der Klasse) einfügen:

```csharp

    [Theory]
    [InlineData(AppLanguage.German, "✔ Übernehmen")]
    [InlineData(AppLanguage.English, "✔ Apply")]
    public void T_Setup_Btn_Apply_ReturnsCorrectTextForLanguage(AppLanguage language, string expected)
    {
        Assert.Equal(expected, LocalizationService.T(Str.Setup_Btn_Apply, language));
    }

    [Theory]
    [InlineData(AppLanguage.German, "Ordner konnte nicht erstellt werden:")]
    [InlineData(AppLanguage.English, "Could not create folder:")]
    public void T_Setup_Error_FolderCreateFailed_ReturnsCorrectTextForLanguage(AppLanguage language, string expected)
    {
        Assert.Equal(expected, LocalizationService.T(Str.Setup_Error_FolderCreateFailed, language));
    }
```

- [ ] **Step 4: Tests laufen lassen**

Run: `dotnet test ULM.Tests`
Expected: alle Tests grün, inklusive der 2 neuen Theory-Tests und des unveränderten `LocalizationServiceCompletenessTests.AllStrValues_HaveGermanAndEnglishTranslation` (deckt jetzt automatisch auch die 31 neuen Werte ab, da beide Dictionaries vollständig befüllt sind).

- [ ] **Step 5: Commit**

```bash
git add Infrastructure/LocalizationService.cs ULM.Tests/LocalizationServiceTests.cs
git commit -m "feat: Uebersetzungen fuer SetupDialog-Str-Eintraege ergaenzt"
```

---

### Task 3: `SetupDialogs.cs` — Kopfzeile + Arbeitsordner-Karte

**Files:**
- Modify: `Views/Dialogs/SetupDialogs.cs`

**Interfaces:**
- Konsumiert: `LocalizationService.T(Str)` mit den in Task 1/2 definierten `Setup_Title_*`, `Setup_Header_*`, `Setup_Subtitle_*`, `Setup_Directory_*`, `Setup_Btn_Browse`, `Setup_FolderDialog_Title`, `Setup_Btn_UseDefaultPath`.
- Produziert: nichts, das andere Tasks konsumieren.

- [ ] **Step 1: Fenstertitel**

```csharp
            Title  = showWelcome ? "Universal Linux Manager — Einrichtung" : "Universal Linux Manager — Einstellungen";
```

ersetzen durch:

```csharp
            Title  = showWelcome ? LocalizationService.T(Str.Setup_Title_Welcome) : LocalizationService.T(Str.Setup_Title_Settings);
```

- [ ] **Step 2: Header-Banner-Titel und Untertitel**

```csharp
            titleStack.Children.Add(new TextBlock
            {
                Text = showWelcome ? "Willkommen beim Universal Linux Manager" : "Einstellungen",
                FontSize = 19, FontWeight = FontWeights.Bold, Foreground = Brushes.White,
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = showWelcome ? "Kurze Einrichtung, dann kann's losgehen." : "Änderungen wirken nach Klick auf „✔ Übernehmen“.",
                FontSize = 12, Foreground = ThemeColors.Dim, Margin = new Thickness(0, 3, 0, 0),
            });
```

ersetzen durch:

```csharp
            titleStack.Children.Add(new TextBlock
            {
                Text = showWelcome ? LocalizationService.T(Str.Setup_Header_Welcome) : LocalizationService.T(Str.Setup_Header_Settings),
                FontSize = 19, FontWeight = FontWeights.Bold, Foreground = Brushes.White,
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = showWelcome ? LocalizationService.T(Str.Setup_Subtitle_Welcome) : LocalizationService.T(Str.Setup_Subtitle_Settings),
                FontSize = 12, Foreground = ThemeColors.Dim, Margin = new Thickness(0, 3, 0, 0),
            });
```

- [ ] **Step 3: Arbeitsordner-Kartenüberschrift und Durchsuchen-Button**

```csharp
                section.Children.Add(new TextBlock
                {
                    Text = "Speicherort für ISO-Downloads und Einstellungsdateien:", FontSize = 12,
                    FontWeight = FontWeights.SemiBold, Foreground = ThemeColors.Header, Margin = new Thickness(0, 0, 0, 8),
                });
```

ersetzen durch:

```csharp
                section.Children.Add(new TextBlock
                {
                    Text = LocalizationService.T(Str.Setup_Directory_Header), FontSize = 12,
                    FontWeight = FontWeights.SemiBold, Foreground = ThemeColors.Header, Margin = new Thickness(0, 0, 0, 8),
                });
```

- [ ] **Step 4: Durchsuchen-Button und Ordner-Dialog-Titel**

```csharp
                var btnBrowse = MakeButton("📂 Durchsuchen", ThemeColors.Card, ThemeColors.Mid, 110, 34);
                btnBrowse.Margin = new Thickness(8, 0, 0, 0);
                btnBrowse.Click += (_, _) =>
                {
                    var dlg = new Microsoft.Win32.OpenFolderDialog
                    {
                        Title = "Arbeitsverzeichnis für den Universal Linux Manager wählen",
                        InitialDirectory = Directory.Exists(txtPathRef.Text) ? txtPathRef.Text : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    };
                    if (dlg.ShowDialog() == true) txtPathRef.Text = dlg.FolderName;
                };
```

ersetzen durch:

```csharp
                var btnBrowse = MakeButton(LocalizationService.T(Str.Setup_Btn_Browse), ThemeColors.Card, ThemeColors.Mid, 110, 34);
                btnBrowse.Margin = new Thickness(8, 0, 0, 0);
                btnBrowse.Click += (_, _) =>
                {
                    var dlg = new Microsoft.Win32.OpenFolderDialog
                    {
                        Title = LocalizationService.T(Str.Setup_FolderDialog_Title),
                        InitialDirectory = Directory.Exists(txtPathRef.Text) ? txtPathRef.Text : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    };
                    if (dlg.ShowDialog() == true) txtPathRef.Text = dlg.FolderName;
                };
```

- [ ] **Step 5: Standard-Pfad-Button, Elemente-Intro und Vorschau-Zeilen**

```csharp
                var btnDefault = MakeButton("Standard-Pfad übernehmen", ThemeColors.Bg, ThemeColors.Mid, 190, 30);
                btnDefault.BorderBrush = ThemeColors.Border; btnDefault.BorderThickness = new Thickness(1);
                btnDefault.HorizontalAlignment = HorizontalAlignment.Left;
                btnDefault.Margin = new Thickness(0, 0, 0, 14);
                btnDefault.Click += (_, _) => txtPathRef.Text = DefaultBase;
                section.Children.Add(btnDefault);

                section.Children.Add(new TextBlock { Text = "Folgende Elemente werden angelegt:", FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = ThemeColors.Header, Margin = new Thickness(0, 0, 0, 6) });
                var previewBorder = new Border
                {
                    Background = ThemeColors.LBlue, BorderBrush = ThemeColors.Border, BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 10, 12, 10),
                };
                var previewGrid = new Grid();
                previewGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                previewGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                for (int i = 0; i < 3; i++) previewGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                AddPreviewRow(previewGrid, 0, "ISO-Downloads",  tbDownloads);
                AddPreviewRow(previewGrid, 1, "ISO-Datenbank",  tbDatabase);
                AddPreviewRow(previewGrid, 2, "Protokolldatei", tbLog);
```

ersetzen durch:

```csharp
                var btnDefault = MakeButton(LocalizationService.T(Str.Setup_Btn_UseDefaultPath), ThemeColors.Bg, ThemeColors.Mid, 190, 30);
                btnDefault.BorderBrush = ThemeColors.Border; btnDefault.BorderThickness = new Thickness(1);
                btnDefault.HorizontalAlignment = HorizontalAlignment.Left;
                btnDefault.Margin = new Thickness(0, 0, 0, 14);
                btnDefault.Click += (_, _) => txtPathRef.Text = DefaultBase;
                section.Children.Add(btnDefault);

                section.Children.Add(new TextBlock { Text = LocalizationService.T(Str.Setup_Directory_ItemsIntro), FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = ThemeColors.Header, Margin = new Thickness(0, 0, 0, 6) });
                var previewBorder = new Border
                {
                    Background = ThemeColors.LBlue, BorderBrush = ThemeColors.Border, BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 10, 12, 10),
                };
                var previewGrid = new Grid();
                previewGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                previewGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                for (int i = 0; i < 3; i++) previewGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                AddPreviewRow(previewGrid, 0, LocalizationService.T(Str.Setup_Directory_ItemDownloads), tbDownloads);
                AddPreviewRow(previewGrid, 1, LocalizationService.T(Str.Setup_Directory_ItemDatabase),  tbDatabase);
                AddPreviewRow(previewGrid, 2, LocalizationService.T(Str.Setup_Directory_ItemLog),       tbLog);
```

- [ ] **Step 6: Build prüfen**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: `Build succeeded.`, keine neuen Warnungen.

- [ ] **Step 7: Commit**

```bash
git add Views/Dialogs/SetupDialogs.cs
git commit -m "feat: SetupDialog Kopfzeile und Arbeitsordner-Karte lokalisiert"
```

---

### Task 4: `SetupDialogs.cs` — Über-ULM-Karte + Modus-Karte + Autostart-Karte

**Files:**
- Modify: `Views/Dialogs/SetupDialogs.cs`

**Interfaces:**
- Konsumiert: `LocalizationService.T(Str)` mit `Setup_Card_AboutUlm`, `Setup_WelcomeBody`, `Setup_Card_Mode`, `Setup_Chk_ExpertMode`, `Setup_Hint_Mode`, `Setup_Card_Autostart`, `Setup_Chk_Autostart`, `Setup_Hint_Autostart`.
- Produziert: nichts, das andere Tasks konsumieren.

- [ ] **Step 1: Über-ULM-Karte**

```csharp
                section.Children.Add(new TextBlock
                {
                    Text = "Mit diesem Tool kannst du mühelos 20–30 verschiedene Linux-Distributionen verwalten, " +
                           "automatisch die neuesten ISOs herunterladen und diese bootfähig auf deinen Ventoy-USB-Stick übertragen.\n\n" +
                           "Features im Überblick:\n" +
                           "• Automatisierte URL-Prüfung & Versions-Check\n" +
                           "• Integrierte Ventoy-Installation & Secure-Boot-Support\n" +
                           "• Parallele Downloads für maximale Performance",
                    TextWrapping = TextWrapping.Wrap, FontSize = 12, LineHeight = 17,
                    Foreground = ThemeColors.Mid,
                });
                body.Children.Add(MakeCard("ℹ Über ULM", section));
```

ersetzen durch:

```csharp
                section.Children.Add(new TextBlock
                {
                    Text = LocalizationService.T(Str.Setup_WelcomeBody),
                    TextWrapping = TextWrapping.Wrap, FontSize = 12, LineHeight = 17,
                    Foreground = ThemeColors.Mid,
                });
                body.Children.Add(MakeCard(LocalizationService.T(Str.Setup_Card_AboutUlm), section));
```

- [ ] **Step 2: Modus-Karte**

```csharp
            var modeSection = new StackPanel();
            var chkExpert = new CheckBox
            {
                Content = "Experten-Modus aktivieren (alle Funktionen sichtbar)",
                FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = ThemeColors.Header,
                VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 8),
                IsChecked = currentExpertMode, // merkt sich die zuletzt gewählte Einstellung
            };
            modeSection.Children.Add(chkExpert);
            modeSection.Children.Add(new TextBlock
            {
                Text = "Bestimmt, wie viele Funktionen und erweiterte Einstellungen im Hauptprogramm angezeigt werden. " +
                       "Unmarkiert = Anwender-Modus (empfohlen). Der Modus kann später jederzeit über ⚙ Einstellungen oben rechts geändert werden.",
                TextWrapping = TextWrapping.Wrap, Foreground = ThemeColors.Dim, FontSize = 11, LineHeight = 16,
            });
            body.Children.Add(MakeCard("👤 Modus", modeSection));
```

ersetzen durch:

```csharp
            var modeSection = new StackPanel();
            var chkExpert = new CheckBox
            {
                Content = LocalizationService.T(Str.Setup_Chk_ExpertMode),
                FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = ThemeColors.Header,
                VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 8),
                IsChecked = currentExpertMode, // merkt sich die zuletzt gewählte Einstellung
            };
            modeSection.Children.Add(chkExpert);
            modeSection.Children.Add(new TextBlock
            {
                Text = LocalizationService.T(Str.Setup_Hint_Mode),
                TextWrapping = TextWrapping.Wrap, Foreground = ThemeColors.Dim, FontSize = 11, LineHeight = 16,
            });
            body.Children.Add(MakeCard(LocalizationService.T(Str.Setup_Card_Mode), modeSection));
```

- [ ] **Step 3: Autostart-Karte**

```csharp
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

ersetzen durch:

```csharp
            var autostartSection = new StackPanel();
            var chkAutostart = new CheckBox
            {
                Content = LocalizationService.T(Str.Setup_Chk_Autostart),
                FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = ThemeColors.Header,
                VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 8),
                IsChecked = AutostartService.IsEnabled(),
            };
            autostartSection.Children.Add(chkAutostart);
            autostartSection.Children.Add(new TextBlock
            {
                Text = LocalizationService.T(Str.Setup_Hint_Autostart),
                TextWrapping = TextWrapping.Wrap, Foreground = ThemeColors.Dim, FontSize = 11, LineHeight = 16,
            });
            body.Children.Add(MakeCard(LocalizationService.T(Str.Setup_Card_Autostart), autostartSection));
```

- [ ] **Step 4: Build prüfen**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: `Build succeeded.`, keine neuen Warnungen.

- [ ] **Step 5: Commit**

```bash
git add Views/Dialogs/SetupDialogs.cs
git commit -m "feat: SetupDialog Ueber-ULM-, Modus- und Autostart-Karte lokalisiert"
```

---

### Task 5: `SetupDialogs.cs` — Design-Karte, Sprache-Karte, Fußzeile, Fehlermeldung

**Files:**
- Modify: `Views/Dialogs/SetupDialogs.cs`

**Interfaces:**
- Konsumiert: `LocalizationService.T(Str)` mit `Setup_Card_Design`, `Setup_Theme_System/Light/Dark`, `Setup_Hint_Theme`, `Setup_Card_Language`, `Setup_Hint_Language`, `Setup_Chk_DontShowAgain`, `Setup_Btn_Apply`, `Setup_Error_Title`, `Setup_Error_FolderCreateFailed`.
- Produziert: nichts, das andere Tasks konsumieren — letzter Code-Task dieses Plans.

- [ ] **Step 1: Design-Karte (Theme-Buttons bleiben mit Emoji-Präfix, Wort wird übersetzt)**

```csharp
            AddThemeButton(AppThemeMode.System, "🌓 System");
            AddThemeButton(AppThemeMode.Light,  "☀ Hell");
            AddThemeButton(AppThemeMode.Dark,   "🌙 Dunkel");
            UpdateThemeButtons();
            themeSection.Children.Add(themeRow);
            themeSection.Children.Add(new TextBlock
            {
                Text = "\"System\" übernimmt automatisch die aktuelle Windows-Einstellung. Kann später jederzeit " +
                       "über ⚙ Einstellungen oben rechts geändert werden — auch live, ohne Neustart.",
                TextWrapping = TextWrapping.Wrap, Foreground = ThemeColors.Dim, FontSize = 11, LineHeight = 16,
            });
            body.Children.Add(MakeCard("🌓 Design", themeSection));
```

ersetzen durch:

```csharp
            AddThemeButton(AppThemeMode.System, "🌓 " + LocalizationService.T(Str.Setup_Theme_System));
            AddThemeButton(AppThemeMode.Light,  "☀ "  + LocalizationService.T(Str.Setup_Theme_Light));
            AddThemeButton(AppThemeMode.Dark,   "🌙 " + LocalizationService.T(Str.Setup_Theme_Dark));
            UpdateThemeButtons();
            themeSection.Children.Add(themeRow);
            themeSection.Children.Add(new TextBlock
            {
                Text = LocalizationService.T(Str.Setup_Hint_Theme),
                TextWrapping = TextWrapping.Wrap, Foreground = ThemeColors.Dim, FontSize = 11, LineHeight = 16,
            });
            body.Children.Add(MakeCard(LocalizationService.T(Str.Setup_Card_Design), themeSection));
```

- [ ] **Step 2: Sprache-Karte (Sprach-Buttons bleiben unverändert hartcodiert)**

```csharp
            AddLangButton(AppLanguage.German,  "🇩🇪 Deutsch");
            AddLangButton(AppLanguage.English, "🇬🇧 English");
            UpdateLangButtons();
            langSection.Children.Add(langRow);
            langSection.Children.Add(new TextBlock
            {
                Text = "Wirkt nach einem Neustart von ULM. Kann später jederzeit über ⚙ Einstellungen oben rechts geändert werden.",
                TextWrapping = TextWrapping.Wrap, Foreground = ThemeColors.Dim, FontSize = 11, LineHeight = 16,
            });
            body.Children.Add(MakeCard("🌐 Sprache", langSection));
```

ersetzen durch:

```csharp
            AddLangButton(AppLanguage.German,  "🇩🇪 Deutsch");
            AddLangButton(AppLanguage.English, "🇬🇧 English");
            UpdateLangButtons();
            langSection.Children.Add(langRow);
            langSection.Children.Add(new TextBlock
            {
                Text = LocalizationService.T(Str.Setup_Hint_Language),
                TextWrapping = TextWrapping.Wrap, Foreground = ThemeColors.Dim, FontSize = 11, LineHeight = 16,
            });
            body.Children.Add(MakeCard(LocalizationService.T(Str.Setup_Card_Language), langSection));
```

- [ ] **Step 3: Fußzeile — „Überspringen"-Checkbox und „Übernehmen"-Button**

```csharp
            var chkDontShowAgain = new CheckBox
            {
                Content = "Diese Einrichtung beim nächsten Start überspringen (Modus wird gespeichert)",
                FontSize = 11, Foreground = ThemeColors.Mid,
                VerticalAlignment = VerticalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(chkDontShowAgain, 0);
            footerGrid.Children.Add(chkDontShowAgain);

            var btnApply = MakeButton("✔ Übernehmen", ThemeColors.Blue, Brushes.White, 160, 40);
```

ersetzen durch:

```csharp
            var chkDontShowAgain = new CheckBox
            {
                Content = LocalizationService.T(Str.Setup_Chk_DontShowAgain),
                FontSize = 11, Foreground = ThemeColors.Mid,
                VerticalAlignment = VerticalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(chkDontShowAgain, 0);
            footerGrid.Children.Add(chkDontShowAgain);

            var btnApply = MakeButton(LocalizationService.T(Str.Setup_Btn_Apply), ThemeColors.Blue, Brushes.White, 160, 40);
```

- [ ] **Step 4: Fehlermeldung bei fehlgeschlagener Ordnererstellung**

```csharp
                    try { Directory.CreateDirectory(chosen); }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ordner konnte nicht erstellt werden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
```

ersetzen durch:

```csharp
                    try { Directory.CreateDirectory(chosen); }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            LocalizationService.T(Str.Setup_Error_FolderCreateFailed) + "\n" + ex.Message,
                            LocalizationService.T(Str.Setup_Error_Title),
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
```

- [ ] **Step 5: Build prüfen**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: `Build succeeded.`, keine neuen Warnungen. `grep -n "\"Übernehmen\"\|\"Deutsch\"\|\"English\"" Views/Dialogs/SetupDialogs.cs` sollte nur noch die beiden hartcodierten Sprach-Buttons zeigen (Zeilen mit `AddLangButton`), keine weiteren Treffer.

- [ ] **Step 6: Commit**

```bash
git add Views/Dialogs/SetupDialogs.cs
git commit -m "feat: SetupDialog Design-, Sprache-Karte, Fusszeile und Fehlermeldung lokalisiert"
```

---

### Task 6: Volle Testsuite + manuelle Zweisprachigkeits-Verifikation

**Files:** keine Code-Änderungen — reine Verifikation.

**Interfaces:** keine.

- [ ] **Step 1: Volle Testsuite laufen lassen**

Run: `dotnet test ULM.Tests`
Expected: alle Tests grün, inklusive `LocalizationServiceCompletenessTests.AllStrValues_HaveGermanAndEnglishTranslation` (deckt jetzt 41 Werte statt 10 ab) und der 2 neuen Spot-Tests aus Task 2.

- [ ] **Step 2: Erststart auf Deutsch prüfen (Regressionscheck)**

`Language`- und `SkipSetupDialog`-Zeile aus der lokalen `ulm_settings.ini` neben der gebauten Debug-EXE entfernen, `Language = de` explizit setzen (oder Zeile ganz weglassen bei deutscher Windows-Systemsprache), EXE starten.

Erwartet: Einrichtungsfenster komplett auf Deutsch, keine sichtbare Abweichung vom Stand vor diesem Plan (Titel, Über-ULM-Text, alle Karten, Fußzeile).

- [ ] **Step 3: Erststart auf Englisch prüfen**

`ulm_settings.ini`: `Language = en`, `SkipSetupDialog`-Zeile entfernt, EXE neu starten.

Erwartet: Fenstertitel "Universal Linux Manager — Setup", Header "Welcome to Universal Linux Manager" mit englischem Untertitel, „ℹ About ULM"-Karte mit englischem Fließtext, Karten „👤 Mode", „🚀 Autostart", „🌓 Theme" (Buttons "System"/"Light"/"Dark"), „🌐 Language" (Buttons bleiben "🇩🇪 Deutsch"/"🇬🇧 English"), Fußzeile-Checkbox und „✔ Apply"-Button auf Englisch. Keine abgeschnittenen/überlappenden Labels.

- [ ] **Step 4: „⚙ Settings" im laufenden Programm auf Englisch prüfen**

Im geöffneten Hauptfenster (jetzt auf Englisch) auf „⚙ Settings" klicken.

Erwartet: Gleicher Dialog im Lite-Modus, Titel "Universal Linux Manager — Settings", Header "Settings" mit "Changes take effect after clicking "✔ Apply"." — keine Über-ULM-Karte, keine Ordner-Auswahl.

- [ ] **Step 5: Fehlermeldung auf Englisch prüfen**

Im „⚙ Settings"-Dialog (Lite-Modus zeigt keine Ordner-Auswahl) stattdessen im Erststart-Dialog (Step 3) einen nicht erstellbaren Pfad eintragen, z.B. `Z:\nichtvorhanden\ulm` bei nicht gemapptem Laufwerk `Z:`, dann „✔ Apply" klicken.

Erwartet: Fehlermeldung "Could not create folder:" gefolgt von der (englischsprachigen, da .NET-Systemtext) `ex.Message` in einer MessageBox mit Titel "Error".

- [ ] **Step 6: Bei Erfolg — nichts weiter zu tun**

Falls einer der Punkte in Step 2–5 nicht stimmt, zurück zu Phase 1 der systematic-debugging-Skill (neue Evidenz sammeln, nicht direkt erneut fixen).

---

## Self-Review

**Spec-Abdeckung:**
- Alle 31 Textstellen aus der Spec-Bestandsaufnahme → Task 1 (Enum) + Task 2 (Übersetzungen) + Task 3–5 (Verwendung in `SetupDialogs.cs`). ✅
- Sprach-Buttons bleiben hartcodiert → Task 5 Step 2 lässt `AddLangButton`-Aufrufe unverändert. ✅
- Theme-Buttons werden übersetzt (im Unterschied zu Sprach-Buttons) → Task 5 Step 1. ✅
- Fehlermeldung per String-Verkettung statt neuem `T()`-Parameter → Task 5 Step 4. ✅
- Kein Live-Retexten nötig (Dialog wird bei jedem Öffnen neu konstruiert) → keine Event-Infrastruktur in irgendeinem Task ergänzt, `T(...)` wird nur beim Konstruktor-Aufbau gelesen. ✅
- Vollständigkeits-Test deckt neue Werte automatisch ab → Task 2 Step 4 verifiziert das, keine Änderung am Test selbst nötig. ✅
- Umfang bleibt auf `SetupDialogs.cs` beschränkt → kein Task berührt andere Dialoge. ✅

**Platzhalter-Scan:** Keine „TBD"/„implement later"/unvollständigen Code-Blöcke — jeder Step enthält vollständigen, copy-paste-fähigen Code oder ein konkretes Kommando mit erwartetem Ergebnis.

**Typkonsistenz:** Alle 31 `Str`-Werte werden in Task 1 exakt so benannt, wie sie in Task 2 (Dictionary-Keys) und Task 3–5 (`LocalizationService.T(Str.Setup_...)`-Aufrufe) verwendet werden — Namen wurden 1:1 aus der Spec übernommen und beim Schreiben dieses Plans gegen den tatsächlichen Wortlaut in `Views/Dialogs/SetupDialogs.cs` geprüft (Stand vor diesem Plan, Commit `28ed8a6`).
