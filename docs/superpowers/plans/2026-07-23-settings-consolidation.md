# Einstellungen konsolidieren (Design/Sprache/Modus) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Die drei Kopfzeilen-Buttons „Modus: Experte", „🌓 Design: …" und „🌐 …" (Sprache) werden durch einen einzigen `⚙ Einstellungen`-Button ersetzt, der das bestehende `SetupDialog` im Lite-Modus (ohne Willkommenstext/Ordner-Auswahl) mit einer neuen Sprach-Karte erneut öffnet.

**Architektur:** `SetupDialog` bekommt eine neue „🌐 Sprache"-Karte (Button-Gruppe wie die bestehende Design-Karte) plus eine neue `ChosenLanguage`-Property. Der Erststart-Dialog (`App.xaml.cs`) und ein neuer `BtnSettings_Click`-Handler in `MainWindow.xaml.cs` nutzen denselben, bereits existierenden Dialog — kein neues, paralleles Einstellungs-UI.

**Tech Stack:** C# / .NET 8 (WPF), keine neuen NuGet-Pakete, keine neuen Build-Schritte.

## Global Constraints

- `SetupDialog`s bestehendes „Sammeln + ein Übernehmen-Button"-Muster bleibt erhalten und wird wiederverwendet — kein neues, abweichendes „jede Checkbox wirkt sofort"-Panel.
- Design + Modus wirken nach Klick auf „✔ Übernehmen" sofort/live (wie bisher). Sprache löst den Neustart-Bestätigungsdialog NUR aus, wenn sie sich tatsächlich vom vorherigen Wert unterscheidet.
- Kein separates Kontextmenü — der sichtbare `⚙`-Button ist der einzige Zugang.
- „❓ Hilfe" bleibt unverändert ein eigener Button, wird nicht Teil der Konsolidierung.
- Der race-freie Neustart-Mechanismus (`SelfUpdateService.BuildRestartAfterInstallScript`, siehe Commit 45fe972) wird eins zu eins aus dem bisherigen `BtnLanguageToggle_Click` übernommen, nicht neu erfunden.
- Bestehender Codestil beibehalten: deutsche Kommentare, gleiche Einrückung/Muster wie umgebender Code (siehe `AddThemeButton`/`UpdateThemeButtons` in `SetupDialogs.cs` als direktes Vorbild für die neue Sprach-Karte).
- Kein Unit-Test-Harness für WPF-Dialoge/-Fenster in diesem Projekt (bestehende Konvention) — Verifikation über Build-Erfolg + manuelle Verifikation in Task 4.

---

### Task 1: `SetupDialog` — Sprach-Karte hinzufügen

**Files:**
- Modify: `Views/Dialogs/SetupDialogs.cs`

**Interfaces:**
- Konsumiert: `ULM.Infrastructure.AppLanguage` (enum `German`/`English`, bereits vorhanden aus der Zweisprachigkeits-Infrastruktur).
- Produziert: `SetupDialog.ChosenLanguage : AppLanguage` (neue Property), neuer Konstruktor-Parameter `AppLanguage currentLanguage = AppLanguage.German` — wird von Task 2 (App.xaml.cs) und Task 3 (MainWindow.xaml.cs) konsumiert.

- [ ] **Step 1: Property + Konstruktor-Parameter ergänzen**

In `Views/Dialogs/SetupDialogs.cs` den bestehenden Block

```csharp
        public string       ChosenDirectory  { get; private set; } = string.Empty;
        public bool         DontShowAgain    { get; private set; }
        public bool         ExpertModeChosen { get; private set; }
        public AppThemeMode ChosenThemeMode  { get; private set; }

        private static string DefaultBase =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "UniversalLinuxManager");

        public SetupDialog(bool showDirectory, bool showWelcome, bool currentExpertMode = false, AppThemeMode currentThemeMode = AppThemeMode.System)
        {
            ChosenThemeMode = currentThemeMode;
```

ersetzen durch:

```csharp
        public string       ChosenDirectory  { get; private set; } = string.Empty;
        public bool         DontShowAgain    { get; private set; }
        public bool         ExpertModeChosen { get; private set; }
        public AppThemeMode ChosenThemeMode  { get; private set; }
        public AppLanguage  ChosenLanguage   { get; private set; }

        private static string DefaultBase =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "UniversalLinuxManager");

        public SetupDialog(bool showDirectory, bool showWelcome, bool currentExpertMode = false,
            AppThemeMode currentThemeMode = AppThemeMode.System, AppLanguage currentLanguage = AppLanguage.German)
        {
            ChosenThemeMode = currentThemeMode;
            ChosenLanguage  = currentLanguage;
```

- [ ] **Step 2: Hinweistext bei „Modus" aktualisieren**

Den bestehenden Text

```csharp
                Text = "Bestimmt, wie viele Funktionen und erweiterte Einstellungen im Hauptprogramm angezeigt werden. " +
                       "Unmarkiert = Anwender-Modus (empfohlen). Der Modus kann später jederzeit oben rechts gewechselt werden.",
```

ersetzen durch:

```csharp
                Text = "Bestimmt, wie viele Funktionen und erweiterte Einstellungen im Hauptprogramm angezeigt werden. " +
                       "Unmarkiert = Anwender-Modus (empfohlen). Der Modus kann später jederzeit über ⚙ Einstellungen oben rechts geändert werden.",
```

- [ ] **Step 3: Hinweistext bei „Design" aktualisieren und neue Sprach-Karte einfügen**

Den bestehenden Block

```csharp
            themeSection.Children.Add(new TextBlock
            {
                Text = "\"System\" übernimmt automatisch die aktuelle Windows-Einstellung. Kann später jederzeit " +
                       "oben rechts im Hauptfenster gewechselt werden — auch live, ohne Neustart.",
                TextWrapping = TextWrapping.Wrap, Foreground = ThemeColors.Dim, FontSize = 11, LineHeight = 16,
            });
            body.Children.Add(MakeCard("🌓 Design", themeSection));

            scroll.Content = body;
```

ersetzen durch:

```csharp
            themeSection.Children.Add(new TextBlock
            {
                Text = "\"System\" übernimmt automatisch die aktuelle Windows-Einstellung. Kann später jederzeit " +
                       "über ⚙ Einstellungen oben rechts geändert werden — auch live, ohne Neustart.",
                TextWrapping = TextWrapping.Wrap, Foreground = ThemeColors.Dim, FontSize = 11, LineHeight = 16,
            });
            body.Children.Add(MakeCard("🌓 Design", themeSection));

            // ── Sprache (Deutsch / English) ─────────────────────────────
            var langSection = new StackPanel();
            var langRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var langButtons = new System.Collections.Generic.Dictionary<AppLanguage, Button>();
            void UpdateLangButtons()
            {
                foreach (var (lang, btn) in langButtons)
                {
                    bool active = lang == ChosenLanguage;
                    btn.Background = active ? ThemeColors.Blue : ThemeColors.Card;
                    btn.Foreground = active ? Brushes.White : ThemeColors.Mid;
                }
            }
            void AddLangButton(AppLanguage lang, string label)
            {
                var btn = MakeButton(label, ThemeColors.Card, ThemeColors.Mid, 130, 32);
                btn.Margin = new Thickness(0, 0, 8, 0);
                btn.Click += (_, _) => { ChosenLanguage = lang; UpdateLangButtons(); };
                langButtons[lang] = btn;
                langRow.Children.Add(btn);
            }
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

            scroll.Content = body;
```

- [ ] **Step 4: Build prüfen**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: `Build succeeded.`, keine neuen Warnungen. Der bestehende Aufruf in `App.xaml.cs:121` (noch ohne `currentLanguage`-Argument) kompiliert weiterhin unverändert, da der neue Parameter einen Default-Wert hat.

- [ ] **Step 5: Commit**

```bash
git add Views/Dialogs/SetupDialogs.cs
git commit -m "feat: SetupDialog bekommt Sprach-Karte (Deutsch/English)"
```

---

### Task 2: App-Startup — Sprache im Erststart-Dialog mit anwenden

**Files:**
- Modify: `App.xaml.cs:121-135`

**Interfaces:**
- Konsumiert: `SetupDialog(..., currentLanguage: AppLanguage)` und `SetupDialog.ChosenLanguage` aus Task 1; `LocalizationService.Current`/`SetLanguage(AppLanguage)` (bereits vorhanden).
- Produziert: nichts, das andere Tasks konsumieren — reine Startup-Verdrahtung.

- [ ] **Step 1: `currentLanguage` übergeben und `ChosenLanguage` nach Dialog-Schluss anwenden**

In `App.xaml.cs` den bestehenden Block

```csharp
                var setupDlg = new SetupDialog(showDirectory: isFirstRun, showWelcome: isFirstRun, currentExpertMode: lastExpert, currentThemeMode: ThemeService.CurrentMode)
                { WindowStartupLocation = WindowStartupLocation.CenterScreen };
                if (setupDlg.ShowDialog() != true) { Shutdown(); return; }

                if (isFirstRun)
                {
                    paths.Apply(setupDlg.ChosenDirectory);
                    IniService.Write(paths.SettingsIni, "App", "BaseDirectory", setupDlg.ChosenDirectory);
                }
                if (setupDlg.DontShowAgain)
                    IniService.Write(paths.SettingsIni, "App", "SkipSetupDialog", "1");
                lastExpert = setupDlg.ExpertModeChosen;
                // Erst NACH Dialog-Schluss anwenden (nicht live während der Auswahl im Dialog
                // selbst) — MainWindow wird direkt danach mit der korrekten Palette konstruiert.
                ThemeService.SetMode(setupDlg.ChosenThemeMode);
```

ersetzen durch:

```csharp
                var setupDlg = new SetupDialog(showDirectory: isFirstRun, showWelcome: isFirstRun, currentExpertMode: lastExpert,
                    currentThemeMode: ThemeService.CurrentMode, currentLanguage: LocalizationService.Current)
                { WindowStartupLocation = WindowStartupLocation.CenterScreen };
                if (setupDlg.ShowDialog() != true) { Shutdown(); return; }

                if (isFirstRun)
                {
                    paths.Apply(setupDlg.ChosenDirectory);
                    IniService.Write(paths.SettingsIni, "App", "BaseDirectory", setupDlg.ChosenDirectory);
                }
                if (setupDlg.DontShowAgain)
                    IniService.Write(paths.SettingsIni, "App", "SkipSetupDialog", "1");
                lastExpert = setupDlg.ExpertModeChosen;
                // Erst NACH Dialog-Schluss anwenden (nicht live während der Auswahl im Dialog
                // selbst) — MainWindow wird direkt danach mit der korrekten Palette konstruiert.
                ThemeService.SetMode(setupDlg.ChosenThemeMode);
                // Kein Neustart-Dialog noetig: MainWindow existiert an dieser Stelle noch gar
                // nicht, die gewaehlte Sprache wird direkt beim allerersten Aufbau wirksam.
                LocalizationService.SetLanguage(setupDlg.ChosenLanguage);
```

- [ ] **Step 2: Build prüfen**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: `Build succeeded.`, keine neuen Warnungen.

- [ ] **Step 3: Commit**

```bash
git add App.xaml.cs
git commit -m "feat: Erststart-Dialog wendet gewaehlte Sprache direkt an"
```

---

### Task 3: MainWindow — Ein `⚙ Einstellungen`-Button statt drei

**Files:**
- Modify: `Views/MainWindow.xaml:276-286`
- Modify: `Views/MainWindow.xaml.cs:368-460`

**Interfaces:**
- Konsumiert: `SetupDialog` mit `ChosenLanguage` (Task 1), `LocalizationService.T/Current/SetLanguage`, `ThemeService.CurrentMode/SetMode`, `SelfUpdateService.BuildRestartAfterInstallScript`, `GetCurrentExePath()` (alle bereits vorhanden).
- Produziert: nichts, das andere Tasks konsumieren — UI-Endpunkt.

- [ ] **Step 1: Drei Buttons durch einen ersetzen (XAML)**

In `Views/MainWindow.xaml` den bestehenden Block

```xml
                    <Button x:Name="BtnModeToggle" Style="{DynamicResource BtnGhost}"
                            Foreground="White" BorderBrush="#4A6785"
                            Click="BtnModeToggle_Click" Width="160" Margin="0,0,8,0"/>

                    <Button x:Name="BtnThemeToggle" Style="{DynamicResource BtnGhost}"
                            Foreground="White" BorderBrush="#4A6785"
                            Click="BtnThemeToggle_Click" Width="130" Margin="0,0,8,0"/>

                    <Button x:Name="BtnLanguageToggle" Style="{DynamicResource BtnGhost}"
                            Foreground="White" BorderBrush="#4A6785"
                            Click="BtnLanguageToggle_Click" Width="110" Margin="0,0,8,0"/>

```

ersetzen durch:

```xml
                    <Button x:Name="BtnSettings" Content="⚙ Einstellungen" Style="{DynamicResource BtnGhost}"
                            Foreground="White" BorderBrush="#4A6785"
                            Click="BtnSettings_Click" Width="140" Margin="0,0,8,0"/>

```

(Der direkt danach folgende `BtnHelp`-Button bleibt unverändert stehen.)

- [ ] **Step 2: Code-Behind konsolidieren**

In `Views/MainWindow.xaml.cs` den bestehenden Block von `private void UpdateUiMode()` bis zum Ende von `BtnLanguageToggle_Click` (schließende `}` direkt vor dem Kommentar `// ── Hilfe-Dialog ──`)

```csharp
        private void UpdateUiMode()
        {
            BtnModeToggle.Content = _vm.ExpertMode ? "Modus: Experte 🛠" : "Modus: Anwender 👤";
            Visibility vis = _vm.ExpertMode ? Visibility.Visible : Visibility.Collapsed;
            BtnVentoy.Visibility = vis; ChkSecureBoot.Visibility = vis; ExpertBar.Visibility = vis; LogTab.Visibility = Visibility.Visible;
            StatusTab.Visibility = vis;
        }

        private void BtnModeToggle_Click(object sender, RoutedEventArgs e) { _vm.ExpertMode = !_vm.ExpertMode; UpdateUiMode(); }

        // ── Design (Hell/Dunkel) ────────────────────────────────────────────
        // Schaltet live um (kein Neustart nötig): ThemeService tauscht die gemergte
        // ResourceDictionary aus, DynamicResource-Bindungen in dieser XAML sowie implizite
        // Styles (TextBox, ComboBox, TabItem, …) reagieren automatisch. Die Zeilenfarben in der
        // Distro-Liste sind dagegen normale C#-Properties (ForegroundBrush) — die werden erst
        // durch den expliziten RefreshAllEntries()-Aufruf im ThemeChanged-Handler neu ausgelesen.
        private void UpdateThemeButtonLabel()
        {
            BtnThemeToggle.Content = ThemeService.CurrentMode switch
            {
                AppThemeMode.Light => "☀ Design: Hell",
                AppThemeMode.Dark  => "🌙 Design: Dunkel",
                _                  => "🌓 Design: System",
            };
        }

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
                // BUGFIX: Process.Start(neue Instanz) direkt gefolgt von Shutdown() startete die neue
                // Instanz, WAEHREND die alte noch lief und in OnClosed()/SaveAndClose() dieselbe
                // ulm_isos.ini schrieb -> Race Condition, beide Prozesse griffen gleichzeitig auf die
                // Datei zu ("IOException: wird bereits von einem anderen Prozess verwendet"). Wie beim
                // Selbst-Update-Neustart (SelfUpdateService.BuildRestartAfterInstallScript) uebernimmt
                // ein externes, unabhaengiges Skript den Neustart: es wartet, bis DIESER Prozess
                // wirklich beendet ist, bevor die neue Instanz startet.
                string scriptDir  = Path.Combine(Path.GetTempPath(), "ULM_LanguageRestart");
                Directory.CreateDirectory(scriptDir);
                string scriptPath = Path.Combine(scriptDir, "restart.ps1");
                File.WriteAllText(scriptPath, SelfUpdateService.BuildRestartAfterInstallScript(Environment.ProcessId, GetCurrentExePath()));
                Process.Start(new ProcessStartInfo("powershell.exe",
                    $"-WindowStyle Hidden -ExecutionPolicy Bypass -File \"{scriptPath}\"")
                { UseShellExecute = false, CreateNoWindow = true });
                Application.Current.Shutdown();
            }
        }
```

ersetzen durch:

```csharp
        private void UpdateUiMode()
        {
            Visibility vis = _vm.ExpertMode ? Visibility.Visible : Visibility.Collapsed;
            BtnVentoy.Visibility = vis; ChkSecureBoot.Visibility = vis; ExpertBar.Visibility = vis; LogTab.Visibility = Visibility.Visible;
            StatusTab.Visibility = vis;
        }

        // ── Sprache (Deutsch/Englisch) ──────────────────────────────────────
        // Wirkt bewusst NICHT live (anders als Design/Modus) — ein Sprachwechsel wird sofort
        // gespeichert, greift aber erst nach einem Neustart von ULM. Siehe
        // docs/superpowers/specs/2026-07-22-bilingual-ui-infrastructure-design.md.
        private void ApplyLocalizedText()
        {
            IsoTab.Header    = LocalizationService.T(Str.Tab_IsoSelection);
            LogTab.Header    = LocalizationService.T(Str.Tab_Log);
            StatusTab.Header = LocalizationService.T(Str.Tab_Status);
            BtnDownload.Content = LocalizationService.T(Str.Btn_Download);
            BtnUpdates.Content  = LocalizationService.T(Str.Btn_CheckForUpdates);
            BtnCancel.Content   = LocalizationService.T(Str.Btn_Cancel);
            BtnHelp.Content     = LocalizationService.T(Str.Btn_Help);
        }

        // ── Einstellungen (Design/Sprache/Modus) ─────────────────────────────
        // Konsolidiert die frueher drei einzelnen Umschalter-Buttons (Modus/Design/Sprache) in
        // einen einzigen Button, der das bestehende SetupDialog im "Lite"-Modus (kein
        // Willkommenstext/keine Ordner-Auswahl) erneut oeffnet. Siehe
        // docs/superpowers/specs/2026-07-23-settings-consolidation-design.md.
        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            AppLanguage oldLang = LocalizationService.Current;
            var dlg = new SetupDialog(showDirectory: false, showWelcome: false, currentExpertMode: _vm.ExpertMode,
                currentThemeMode: ThemeService.CurrentMode, currentLanguage: oldLang)
            { Owner = this };
            if (dlg.ShowDialog() != true) return;

            _vm.ExpertMode = dlg.ExpertModeChosen;
            UpdateUiMode();
            ThemeService.SetMode(dlg.ChosenThemeMode);

            if (dlg.ChosenLanguage == oldLang) return;

            string title   = LocalizationService.T(Str.LanguageChangeConfirm_Title, oldLang);
            string message = LocalizationService.T(Str.LanguageChangeConfirm_Message, oldLang);

            LocalizationService.SetLanguage(dlg.ChosenLanguage);

            if (MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                // Race-freier Neustart (siehe Commit 45fe972 / SelfUpdateService.BuildRestartAfterInstallScript):
                // ein externes, unabhaengiges Skript wartet, bis DIESER Prozess wirklich beendet ist
                // (Datei-Lock auf ulm_isos.ini u.ae. freigegeben), bevor die neue Instanz startet.
                string scriptDir  = Path.Combine(Path.GetTempPath(), "ULM_LanguageRestart");
                Directory.CreateDirectory(scriptDir);
                string scriptPath = Path.Combine(scriptDir, "restart.ps1");
                File.WriteAllText(scriptPath, SelfUpdateService.BuildRestartAfterInstallScript(Environment.ProcessId, GetCurrentExePath()));
                Process.Start(new ProcessStartInfo("powershell.exe",
                    $"-WindowStyle Hidden -ExecutionPolicy Bypass -File \"{scriptPath}\"")
                { UseShellExecute = false, CreateNoWindow = true });
                Application.Current.Shutdown();
            }
        }
```

- [ ] **Step 3: Build prüfen**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: `Build succeeded.`, keine neuen Warnungen — insbesondere keine Fehler zu entfernten Bezeichnern (`BtnModeToggle`, `BtnThemeToggle`, `BtnLanguageToggle` dürfen nirgends mehr referenziert werden; `grep -rn "BtnModeToggle\|BtnThemeToggle\|BtnLanguageToggle" Views/` sollte keine Treffer mehr liefern).

- [ ] **Step 4: Volle Testsuite laufen lassen**

Run: `dotnet test ULM.Tests`
Expected: alle Tests grün (keine Regression — dieser Task ändert keine testbare Logik, nur UI-Verdrahtung).

- [ ] **Step 5: Commit**

```bash
git add Views/MainWindow.xaml Views/MainWindow.xaml.cs
git commit -m "feat: Design/Sprache/Modus zu einem Einstellungen-Button konsolidiert"
```

---

### Task 4: Manuelle End-to-End-Verifikation

**Files:** keine Code-Änderungen — reine Verifikation.

**Interfaces:** keine.

- [ ] **Step 1: Erststart prüfen**

`Language`-Zeile (und optional `SkipSetupDialog`) aus der lokalen `ulm_settings.ini` neben der gebauten Debug-EXE entfernen, dann `dotnet build` + EXE starten.

Erwartet: Einrichtungsfenster zeigt jetzt vier Karten „👤 Modus", „🚀 Autostart", „🌓 Design", „🌐 Sprache" (keine Willkommenskarte/Ordner-Auswahl nur, falls dies kein echter Erststart ist — bei echtem Erststart zusätzlich „🚀 Willkommen"/Ordner-Auswahl). Sprache auf „🇬🇧 English" stellen, „✔ Übernehmen" klicken.

Erwartet: Hauptfenster öffnet sich direkt auf Englisch, kein Neustart-Dialog (MainWindow existierte ja noch nicht).

- [ ] **Step 2: „⚙ Einstellungen" im laufenden Programm öffnen, Design/Modus ändern**

Im laufenden Hauptfenster auf „⚙ Einstellungen" klicken.

Erwartet: Gleicher Dialog, diesmal OHNE Willkommenskarte/Ordner-Auswahl. Design auf „🌙 Dunkel" stellen, Experten-Modus ankreuzen, Sprache NICHT anfassen, „✔ Übernehmen" klicken.

Erwartet: Design wechselt sofort sichtbar auf Dunkel, Experten-Bereich (URLs prüfen, ISO suchen, etc.) wird sichtbar — beides ohne Neustart-Dialog, da Sprache unverändert blieb.

- [ ] **Step 3: Sprache zusätzlich ändern**

Erneut „⚙ Einstellungen" öffnen, Sprache zurück auf „🇩🇪 Deutsch" stellen, „✔ Übernehmen" klicken.

Erwartet: Neustart-Bestätigungsdialog erscheint (auf Englisch, da Englisch vor dem Klick aktiv war). Bei „Yes": derselbe race-freie Neustart wie beim vorherigen Sprach-Umschalter — ULM schließt sich, startet nach kurzer Zeit sauber neu, jetzt auf Deutsch, Design weiterhin Dunkel, Experten-Modus weiterhin aktiv (unabhängig von der Sprache gespeichert).

- [ ] **Step 4: Autostart-Bonus prüfen**

„⚙ Einstellungen" erneut öffnen, „Mit Windows starten" ankreuzen, „✔ Übernehmen" klicken. Windows-Autostart-Ordner/Registry-Eintrag prüfen (`AutostartService.IsEnabled()` sollte jetzt `true` liefern — z.B. per kurzem Scratch-Aufruf oder durch erneutes Öffnen des Dialogs: Checkbox sollte jetzt vorangehakt sein).

Erwartet: Vorher war dieser Weg nach einmal gesetztem „Diese Einrichtung beim nächsten Start überspringen" gar nicht mehr erreichbar — jetzt funktioniert er wieder.

- [ ] **Step 5: Bei Erfolg — nichts weiter zu tun**

Falls einer der Punkte in Step 1–4 nicht stimmt, zurück zu Phase 1 der systematic-debugging-Skill (neue Evidenz sammeln, nicht direkt erneut fixen).

---

## Self-Review

**Spec-Abdeckung:**
- Neue Sprach-Karte in `SetupDialog` (Spec-Ziel 2) → Task 1. ✅
- Ein `⚙ Einstellungen`-Button statt drei (Spec-Ziel 1) → Task 3 Step 1. ✅
- Design/Modus/Autostart sofort nach Übernehmen, Sprache nur bei tatsächlicher Änderung mit Neustart-Dialog (Spec-Ziel 3) → Task 3 Step 2 (`BtnSettings_Click`). ✅
- Erststart-Dialog bekommt Sprach-Karte, direkte Anwendung ohne Neustart-Dialog (Spec-Ziel 4) → Task 2. ✅
- Autostart-Bonus (bisher nach Erststart nicht mehr erreichbar) → Task 4 Step 4 verifiziert den Nebeneffekt explizit. ✅
- Race-freier Neustart-Mechanismus wiederverwendet, nicht dupliziert → Task 3 Step 2 übernimmt den Code eins zu eins aus dem entfallenden `BtnLanguageToggle_Click`. ✅

**Platzhalter-Scan:** Keine „TBD"/„implement later"/unvollständigen Code-Blöcke — jeder Step enthält vollständigen, copy-paste-fähigen Code oder ein konkretes Kommando mit erwartetem Ergebnis.

**Typkonsistenz:** `SetupDialog.ChosenLanguage : AppLanguage` wird in Task 1 definiert und in Task 2 (`setupDlg.ChosenLanguage`) sowie Task 3 (`dlg.ChosenLanguage`) konsistent verwendet. Der neue Konstruktor-Parameter `currentLanguage: AppLanguage = AppLanguage.German` wird in Task 1 definiert, in Task 2 mit `LocalizationService.Current` und in Task 3 mit der lokalen Variable `oldLang` (ebenfalls `LocalizationService.Current`) aufgerufen — konsistent.
