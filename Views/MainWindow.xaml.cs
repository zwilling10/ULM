// Views/MainWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ULM.Core.Models;
using ULM.Core.Services;
using ULM.Infrastructure;
using ULM.ViewModels;
using ULM.Views.Dialogs;

namespace ULM.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel   _vm;
        private readonly DispatcherTimer _driveTimer     = new();
        private readonly DispatcherTimer _autoCheckTimer = new();

        private DownloadProgressDialog? _downloadProgressDialog;
        private bool   _orphanCheckDone;
        private string _lastDriveSignatureUi = string.Empty;

        private string     _updateCurrentExePath = string.Empty;
        private InstallKind _updateInstallKind    = InstallKind.Portable;

        // Ziel-Laufwerk/Kopier-Optionen des aktuell laufenden bzw. zuletzt gestarteten Download-
        // Batches — von StartDownloadWithProgressDialog gesetzt. Ermöglicht OpenManualSearchFromDownloadFailure,
        // nach erfolgreicher manueller Quellen-Eintragung GENAU DIESEN Eintrag mit denselben
        // Einstellungen (Ziel-Stick, Kopieren-danach) automatisch neu herunterzuladen.
        private string _activeDownloadDrive = string.Empty;
        private bool   _activeDownloadCopyAfter, _activeDownloadDeleteAfter;

        public MainWindow()
        {
            InitializeComponent();
            // BUGFIX: der Fenstertitel stand bisher als fester String in der XAML ("... v2.27") und
            // wurde bei Versions-Releases nie mitgepflegt — Constants.AppFullTitle liest die Version
            // bereits dynamisch aus der Assembly, das galt aber nur für den HelpDialog-Titel, nicht
            // für das Hauptfenster selbst.
            Title = Constants.AppFullTitle;
            UpdateThemeButtonLabel();
            ApplyLocalizedText();
            _vm = new MainViewModel(Dispatcher);
            DataContext = _vm;
            ThemeService.ThemeChanged += () =>
            {
                UpdateThemeButtonLabel();
                _vm.RefreshAllEntries();
            };
            _vm.LogMessage += AppendLog;
            _vm.ShowMessageBox += (msg, isErr) => MessageBox.Show(msg, Constants.AppTitle, MessageBoxButton.OK, isErr ? MessageBoxImage.Warning : MessageBoxImage.Information);
            _vm.ConfirmSlowDownload = (name, host) => MessageBox.Show(
                $"{name}: Es wurde kein schnellerer Mirror gefunden — {host} überträgt weiterhin nur sehr langsam.\n\n" +
                "Trotzdem mit dieser Quelle fortfahren? (Das kann sehr lange dauern.)",
                "⚠ Langsamer Download", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
            _vm.StickUpdateAvailable += OnStickUpdateAvailable;
            _vm.StaleDuplicatesOnStickDetected += OnStaleDuplicatesOnStick;

            _vm.NewerVersionsOnStickDetected += (matches, drive) =>
            {
                var fresh = matches.Where(m => _vm.MarkNewerVersionOffered(drive, m.StickIso.Filename)).ToList();
                if (fresh.Count == 0) return;
                AppendLog($"📥 {fresh.Count} ISO(s) auf {drive} neuer als DB-Eintrag.");
                var dlg = new NewerVersionOnStickDialog(fresh) { Owner = this };
                if (dlg.ShowDialog() != true) { AppendLog("   ℹ Keine Änderung."); return; }
                int replaced = 0, added = 0;
                foreach (var (dbEntry, stickIso, choice) in dlg.Results)
                    switch (choice)
                    {
                        case NewerVersionChoice.Replace: _vm.ReplaceEntryVersion(dbEntry, stickIso.Filename); replaced++; break;
                        case NewerVersionChoice.Add:     _vm.AddEntryFromStickVersion(dbEntry, stickIso); added++; break;
                        case NewerVersionChoice.Skip:    AppendLog($"   ⏭ {dbEntry.Name}"); break;
                    }
                if (replaced > 0 || added > 0) { _vm.RebuildTree(); AppendLog($"✅ DB: {(replaced > 0 ? $"{replaced} ersetzt" : "")}{(replaced > 0 && added > 0 ? ", " : "")}{(added > 0 ? $"{added} hinzugefügt" : "")}."); }
                // Gesundheitscheck NUR wenn tatsächlich neue, unverifizierte Einträge entstanden
                // sind ("Hinzufügen" legt einen neuen Eintrag ohne geprüfte Url an) — "Ersetzen"
                // aktualisiert nur den Dateinamen eines bereits bekannten Eintrags, dafür braucht es
                // keinen erneuten Katalog-weiten Check.
                if (added > 0) _vm.RunHealthCheck();
            };

            _vm.UnknownIsosOnStickDetected += async (unknowns, drive) =>
            {
                var fresh = unknowns.Where(u => _vm.MarkUnknownStickIsoOffered(drive, u.Filename)).ToList();
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

            _vm.IncompleteIsosOnStickDetected += (incomplete, drive) =>
            {
                var fresh = incomplete.Where(i => _vm.MarkIncompleteStickIsoOffered(drive, i.Filename)).ToList();
                if (fresh.Count == 0) return;
                AppendLog($"🗑 {fresh.Count} unvollständige ISO(s) auf {drive} erkannt (Online-Größenprüfung).");
                var files = fresh.Select(i => (i.FullPath, i.Size)).ToList();
                var dlg = new OrphanedDownloadsDialog(files, "Unvollständige ISOs auf dem Stick gefunden", "unvollständige ISO-Datei(en) auf dem Stick") { Owner = this };
                if (dlg.ShowDialog() == true)
                {
                    int deleted = 0, failed = 0;
                    foreach (string path in dlg.ToDelete)
                    { if (IsoEntry.TryDelete(path, AppendLog)) { deleted++; AppendLog($"   🗑 Gelöscht: {Path.GetFileName(path)}"); } else failed++; }
                    AppendLog($"🗑 {deleted} Datei(en) auf {drive} gelöscht" + (failed > 0 ? $", {failed} fehlgeschlagen" : "") + ".");
                }
                else AppendLog($"ℹ Stick-Wartung übersprungen ({fresh.Count} Datei(en) behalten).");
            };

            _vm.DownloadItemProgress   += (name, pct, status, canFaster, noUrlFound) => _downloadProgressDialog?.UpdateDownload(name, pct, status, canFaster, noUrlFound);
            // BUGFIX: die Kopfzeile wurde bisher blind aus ok/failed DIESES EINEN DownloadBatchCompleted-
            // Events gebaut — nach einem Einzel-Retry über "🔧 Quelle manuell suchen" (siehe
            // OpenManualSearchFromDownloadFailure) feuert für nur den einen nachträglich erfolgreichen
            // Eintrag ein NEUER Event mit NUR dessen eigenen Zahlen (z.B. "1 erfolgreich"), der die
            // ursprüngliche Zusammenfassung (z.B. "0 erfolgreich, 1 fehlgeschlagen") komplett überschrieb
            // statt sie zu korrigieren. GetOverallCounts() zählt stattdessen den ECHTEN Stand über ALLE
            // je in diesem Fenster gezeigten Zeilen.
            _vm.DownloadBatchCompleted += (_, _, _) =>
            {
                if (_downloadProgressDialog is null) return;
                var (succeeded, total) = _downloadProgressDialog.GetOverallCounts();
                int failedTotal = total - succeeded;
                _downloadProgressDialog.SetOverallComplete($"{succeeded} erfolgreich" + (failedTotal > 0 ? $", {failedTotal} fehlgeschlagen" : "") + ".");
            };
            _vm.CopyItemProgress       += (name, pct, detail) => _downloadProgressDialog?.UpdateCopy(name, pct, detail);
            _vm.CopyBatchCompleted     += count => { if (_downloadProgressDialog is null) return; _downloadProgressDialog.SetOverallComplete(count > 0 ? $"{count} ISO(s) auf den Stick kopiert." : "Nichts kopiert."); };

            _vm.OperationSucceeded += message =>
            {
                _downloadProgressDialog?.Close(); _downloadProgressDialog = null;
                AppendLog($"✅ {message.Split('\n')[0]}");
                MessageBox.Show(message, "✅ Vorgang abgeschlossen", MessageBoxButton.OK, MessageBoxImage.Information);
            };

            // Unauffällige Bestätigung für URL-/Update-/Integritätsprüfung (nicht modal, schließt
            // sich nach 5s von selbst) — siehe QuickCheckSucceeded-Dokumentation im ViewModel.
            _vm.QuickCheckSucceeded += message => new QuickConfirmationWindow(message) { Owner = this }.Show();

            // Härtefall-Hinweis für GENAU EINEN neu betroffenen Eintrag — bei mehreren gleichzeitig
            // übernimmt stattdessen das Härtefall-Banner (siehe MainViewModel.ReportHardCases).
            _vm.HardCaseNoticeRequested += name => new QuickConfirmationWindow(
                $"🔧 Manuelle Quellen-Suche jetzt möglich für: {name}") { Owner = this }.Show();

            _vm.HealthCheckCompleted += results => new DbHealthCheckDialog(results) { Owner = this }.ShowDialog();

            _vm.AutoVersionCheckCompleted += async () =>
            {
                // Zeitstempel bei JEDEM abgeschlossenen Check aktualisieren (Start-Check UND
                // spätere Hintergrund-Checks) — Grundlage für CheckAutoRecheckDue().
                IniService.Write(AppPaths.Instance.SettingsIni, "App", "LastAutoCheckUtc", DateTime.UtcNow.ToString("o"));
                // BUGFIX: RefreshScheduleStatus() lief bisher nur beim Programmstart (VOR dem ersten
                // Check) und alle 30 Minuten per Timer — der Status-Reiter zeigte deshalb nach dem
                // automatischen Start-Check trotzdem weiter "unbekannt (noch kein Check gelaufen)",
                // bis zu 30 Minuten lang, obwohl der Zeitstempel oben längst geschrieben war.
                _vm.RefreshScheduleStatus();
                if (_orphanCheckDone) return;
                _orphanCheckDone = true;
                await RunLocalFileMaintenanceAsync();
            };

            // 8s statt vormals 4s: Hot-Plug-Erkennung (Stick rein/raus) bleibt dadurch laufend
            // aktiv, reagiert aber spürbar seltener statt praktisch alle 4 Sekunden zu pollen.
            _driveTimer.Interval = TimeSpan.FromSeconds(8);
            _driveTimer.Tick    += (_, _) => CheckDriveChanges();

            // Hintergrund-Suche nach erreichbaren Servern/neuen Versionen: läuft nicht nur
            // einmalig beim Start, sondern prüft auch während einer länger laufenden Sitzung
            // regelmäßig, ob seit dem letzten Check bereits Constants.AutoCheckIntervalDays
            // vergangen sind — Ziel ist, immer mit den aktuellsten veröffentlichten ISOs zu
            // arbeiten, auch wenn ULM tagelang durchläuft statt neu gestartet zu werden.
            _autoCheckTimer.Interval = TimeSpan.FromMinutes(30);
            _autoCheckTimer.Tick    += (_, _) => CheckAutoRecheckDue();

            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            LoadSettings(); _vm.Initialize();
            _vm.RefreshScheduleStatus();
            ShowChangelogIfUpdated();
            _lastDriveSignatureUi = string.Join(";", _vm.Drives.Select(d => d.Letter));
            // Stecken beim Programmstart bereits mehrere Sticks, wählt RefreshDrives() (aufgerufen
            // aus _vm.Initialize() oben) stillschweigend Drives[0] als SelectedDrive — derselbe
            // "welchen Stick meinte ich eigentlich?"-Fall wie beim Hinzustecken eines zweiten Sticks
            // während der Laufzeit (siehe OnNewDriveInserted), nur dass hier nie ein "neuer Stick"-
            // Ereignis feuert, das den Auswahldialog auslösen könnte.
            OfferDriveChoiceIfMultiple();
            FooterLbl.Text = $"ISO-Ordner: {AppPaths.Instance.DownloadDir}";
            _driveTimer.Start();
            _autoCheckTimer.Start();
            await Task.Delay(1000);
            AppendLog("🌐 Automatischer Online-Versionscheck wird gestartet …");
            _vm.TriggerAutoVersionCheck();
            _ = CheckUlmUpdateAsync();
        }

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

        private void BtnUpdateDismiss_Click(object sender, RoutedEventArgs e) => _vm.DismissUpdateBanner();
        private void BtnHardCaseDismiss_Click(object sender, RoutedEventArgs e) => _vm.DismissHardCaseBanner();

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

        // Wird alle 30 Minuten geprüft: löst TriggerAutoVersionCheck() erneut aus, sobald seit
        // dem letzten Check (Start ODER vorheriger Hintergrund-Lauf) Constants.AutoCheckIntervalDays
        // vergangen sind. Übersprungen wird nur, wenn gerade ein anderer Vorgang läuft — dann
        // greift der nächste 30-Minuten-Tick.
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

        public void SetInitialMode(bool expertMode) { _vm.ExpertMode = expertMode; UpdateUiMode(); }

        private void LoadSettings()
        {
            AppPaths paths = AppPaths.Instance;
            _vm.ExpertMode = IniService.Read(paths.SettingsIni, "App", "ExpertMode", "0") == "1";
            _vm.SecureBoot = IniService.Read(paths.SettingsIni, "App", "SecureBoot", "1") != "0";
            ChkSecureBoot.IsChecked = _vm.SecureBoot;
            UpdateUiMode();
        }

        /// <summary>
        /// Zeigt den "Was ist neu?"-Dialog genau einmal pro neuer Version. Leeres LastSeenVersion
        /// (echter Erststart) zeigt NICHTS — der SetupDialog deckt das Onboarding bereits ab, ein
        /// Changelog vergangener Bugfixes wäre für einen brandneuen Nutzer irrelevant. Erst ab dem
        /// zweiten Start mit einer geänderten Version erscheint der Dialog.
        /// </summary>
        private void ShowChangelogIfUpdated()
        {
            AppPaths paths = AppPaths.Instance;
            string lastSeen = IniService.Read(paths.SettingsIni, "App", "LastSeenVersion", string.Empty);
            if (!string.IsNullOrEmpty(lastSeen) && lastSeen != Constants.AppVersion)
                new ChangelogDialog(lastSeen, Constants.AppVersion) { Owner = this }.ShowDialog();
            IniService.Write(paths.SettingsIni, "App", "LastSeenVersion", Constants.AppVersion);
        }

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
                Process.Start(new ProcessStartInfo(GetCurrentExePath()) { UseShellExecute = true });
                Application.Current.Shutdown();
            }
        }

        // ── Hilfe-Dialog ──────────────────────────────────────────────────
        // Öffnet den vollständigen HelpDialog mit allen Programm-Erklärungen.
        // Ersetzt die frühere einzeilige MessageBox.
        private void BtnHelp_Click(object sender, RoutedEventArgs e) =>
            new HelpDialog { Owner = this }.ShowDialog();

        // ══════════════════════════════════════════════════════════════════
        // Lokale Datei-Wartung — NULL-MÜLL-GARANTIE
        // ══════════════════════════════════════════════════════════════════
        private async Task RunLocalFileMaintenanceAsync()
        {
            try
            {
                string dir = AppPaths.Instance.DownloadDir;
                if (!Directory.Exists(dir)) { AppendLog($"⚠ Arbeitsordner nicht gefunden: {dir}"); return; }

                var dbEntries  = IsoDatabaseService.Instance.Entries;
                var byFilename = new Dictionary<string, IsoEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (var e in dbEntries)
                    if (!string.IsNullOrWhiteSpace(e.Filename) && !byFilename.ContainsKey(e.Filename))
                        byFilename[e.Filename] = e;

                var candidates = new List<(string Path, long Size)>();
                AppendLog($"🔍 Scanne ISO-Ordner rekursiv: {dir}");

                string[] isoFiles;
                try   { isoFiles = Directory.GetFiles(dir, "*.iso", SearchOption.AllDirectories); }
                catch (Exception ex) { AppendLog($"⚠ Scan-Fehler: {ex.Message}"); isoFiles = Array.Empty<string>(); }
                AppendLog($"   {isoFiles.Length} ISO-Datei(en) gefunden.");

                foreach (string f in isoFiles)
                {
                    string name = Path.GetFileName(f);
                    long size = IsoEntry.GetRobustLength(f);

                    if (size == 0)
                    { AppendLog($"   ⚠ Leer (0 Bytes): {RelativePath(dir, f)}"); candidates.Add((f, 0)); continue; }

                    if (!byFilename.TryGetValue(name, out var entry))
                    { AppendLog($"   ⚠ Verwaist: {RelativePath(dir, f)}  ({FmtSize(size)})"); candidates.Add((f, size)); continue; }

                    // Online-Größenprüfung: RemoteUrl → Url → Mirror1-5 (erste bekannte
                    // Content-Length gewinnt). Zuverlässiger als der feste 300-MB-Schwellwert,
                    // da auch unvollständige Downloads oberhalb dieser Schwelle erkannt werden.
                    long expected = await HttpService.Instance.GetExpectedSizeAsync(entry).ConfigureAwait(true);
                    if (expected > 0 && size < expected * 0.98)
                    { AppendLog($"   ⚠ Unvollständig: {RelativePath(dir, f)}  ({FmtSize(size)} / {FmtSize(expected)} erwartet)"); candidates.Add((f, size)); }
                    else if (expected > 0)
                    { entry.VerifiedComplete = true; AppendLog($"   ✓ Vollständig: {RelativePath(dir, f)}  ({FmtSize(size)})"); }
                    else if (size < Constants.MinIsoSizeBytes)
                    { AppendLog($"   ⚠ Zu klein (Online-Größe nicht ermittelbar): {RelativePath(dir, f)}  ({FmtSize(size)})"); candidates.Add((f, size)); }
                    else
                    { entry.VerifiedComplete = true; AppendLog($"   ✓ OK (ungeprüft): {RelativePath(dir, f)}  ({FmtSize(size)})"); }
                }

                try
                {
                    foreach (string f in Directory.GetFiles(dir, "*.part", SearchOption.AllDirectories))
                    { long size = IsoEntry.GetRobustLength(f); AppendLog($"   ⚠ Abgebrochen: {RelativePath(dir, f)}  ({FmtSize(size)})"); candidates.Add((f, size)); }
                }
                catch (Exception ex) { AppendLog($"   ⚠ .part-Suche: {ex.Message}"); }

                if (candidates.Count == 0)
                    AppendLog("✅ Kein Datenmüll im ISO-Ordner — alles sauber.");
                else
                {
                    AppendLog($"🗑 {candidates.Count} Datei(en) als Datenmüll eingestuft.");
                    var dlg = new OrphanedDownloadsDialog(candidates) { Owner = this };
                    if (dlg.ShowDialog() == true)
                    {
                        int deleted = 0, failed = 0;
                        foreach (string path in dlg.ToDelete)
                        { if (IsoEntry.TryDelete(path, AppendLog)) { deleted++; AppendLog($"   🗑 Gelöscht: {RelativePath(dir, path)}"); } else failed++; }
                        AppendLog($"🗑 {deleted} Datei(en) gelöscht" + (failed > 0 ? $", {failed} fehlgeschlagen" : "") + ".");
                        if (deleted > 0) _vm.RefreshAllEntries();
                    }
                    else AppendLog($"ℹ Wartung übersprungen ({candidates.Count} Datei(en) behalten).");
                }

                if (!string.IsNullOrEmpty(_vm.SelectedDriveLetter))
                {
                    var missing = _vm.GetVerifiedCompleteEntriesMissingFromStick();
                    if (missing.Count > 0) OnMissingOnStickDetected(missing, _vm.SelectedDriveLetter);
                }
            }
            catch (Exception ex) { AppendLog($"⚠ Datei-Wartung: {ex.Message}"); }
        }

        private void OnStickUpdateAvailable(List<(IsoEntry Entry, string OldFilename)> outdated, string drive)
        {
            if (outdated.Count == 0) return;
            var sb = new StringBuilder(); sb.AppendLine($"Auf {drive} wurden {outdated.Count} veraltete ISO(s) gefunden:"); sb.AppendLine();
            foreach (var (entry, _) in outdated) sb.AppendLine($"  • {entry.Name}"); sb.AppendLine(); sb.AppendLine("Jetzt aktualisieren?");
            if (MessageBox.Show(sb.ToString(), "💾 Stick-Aktualisierung", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            var entries = outdated.Select(x => x.Entry).ToList();
            foreach (var e in entries) e.IsSelected = true; _vm.RefreshAllEntries();

            // Die jetzt überflüssige ALTE Datei wird hier bewusst NICHT mehr extra behandelt: der
            // automatische Stick-Scan nach Download+Kopie (TriggerUsbScan in MainViewModel.StartDownload)
            // erkennt sie über DistroMatcher.FindKnownDistroForStickFile selbst als "bekannt, überholte
            // Version" und bietet das Löschen über OnStaleDuplicatesOnStick unten an — kontextfrei,
            // funktioniert also unabhängig davon, über welchen Weg der Download/die Kopie angestoßen wurde.
            StartDownloadWithProgressDialog(entries, drive, copyAfter: true, deleteAfter: false, slots: Math.Min(entries.Count, Constants.MaxParallelSlots));
        }

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

        private void OnMissingOnStickDetected(List<IsoEntry> entries, string drive)
        {
            var fresh = entries.Where(e => _vm.MarkCopyOffered(drive, e.Filename)).ToList();
            if (fresh.Count == 0) return;
            var sb = new StringBuilder(); sb.AppendLine($"{fresh.Count} ISO(s) vollständig lokal, NICHT auf {drive}:"); sb.AppendLine();
            foreach (var e in fresh) sb.AppendLine($"  • {e.Name}"); sb.AppendLine(); sb.AppendLine("Jetzt kopieren?");
            if (MessageBox.Show(sb.ToString(), "💾 Vollständige ISOs nicht auf dem Stick", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            bool del = MessageBox.Show("Lokale Dateien danach löschen?", "Dateien löschen?", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
            OpenProgressDialog(fresh.Select(q => q.Name), hasDownload: false, hasCopy: true);
            _vm.StartCopyToStick(fresh, drive, del);
        }

        private void EntryRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;
            if (sender is FrameworkElement fe && fe.DataContext is IsoEntryViewModel vm && vm.Model != null && !string.IsNullOrWhiteSpace(vm.Model.Tip))
                MessageBox.Show(vm.Model.Tip, vm.Model.Name, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Öffnet ManualSourceSearchDialog für genau die Zeile, in der der Button liegt — siehe
        // docs/superpowers/specs/2026-07-17-manual-source-search-design.md. Kein neues Auswahl-
        // Konzept nötig, da der Button direkt im DataContext (IsoEntryViewModel) der eigenen Zeile
        // sitzt (gleiches Muster wie EntryRow_MouseLeftButtonDown oben).
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

        private void BtnRefreshDrives_Click(object sender, RoutedEventArgs e) => CheckDriveChanges();

        // BUGFIX: Während einer laufenden Ventoy-Installation/-Aktualisierung (IsBusy) kann das
        // Ziel-Laufwerk beim Formatieren/Partitionieren kurzzeitig aus der WMI-Aufzählung
        // verschwinden und wieder auftauchen. Ohne diese Sperre wertete CheckDriveChanges das als
        // "neuer Stick eingesteckt" und zeigte die "Automatisch als Ventoy-Stick einrichten?"-Abfrage
        // ein zweites Mal an — was eine ZWEITE, parallele Installation auf demselben Laufwerk
        // gestartet hätte (doppelte Fenster, Race Condition auf dem Ziel-Laufwerk). Der komplette
        // Timer-Tick wird deshalb übersprungen, solange IsBusy true ist — RefreshDrives() wird
        // nicht einmal aufgerufen, damit während der Installation keinerlei Enumeration/Zugriff auf
        // das Laufwerk mit Ventoy2Disk.exe konkurriert.
        private void CheckDriveChanges()
        {
            if (_vm.IsBusy) return;
            string prev = _lastDriveSignatureUi; _vm.RefreshDrives();
            string curr = string.Join(";", _vm.Drives.Select(d => d.Letter)); _lastDriveSignatureUi = curr;
            if (curr != prev && curr.Length > prev.Length) OnNewDriveInserted();
        }

        private void OnNewDriveInserted()
        {
            UsbDrive? nd = _vm.Drives.Count == 1 ? _vm.Drives[0] : null;
            if (nd is null)
            {
                AppendLog($"🔌 {_vm.Drives.Count} USB-Laufwerke erkannt: " + string.Join(", ", _vm.Drives.Select(d => $"{d.Letter} ({d.Label})")));
                // BUGFIX: bisher wurde hier stillschweigend Drives[0] übernommen (RefreshDrives()
                // oben hat das bereits als Vorbelegung getan) — der Nutzer erfuhr nie, WELCHEN der
                // mehreren Sticks ULM gerade als Ziel gewählt hat, bevor er z.B. auf "Ventoy
                // einrichten" oder "Auf Stick kopieren" klickte. Jetzt aktiv nachfragen.
                OfferDriveChoiceIfMultiple();
                return;
            }
            if (UsbService.IsVentoyInstalled(nd.Letter)) { StatusLbl.Text = $"✅ Ventoy-Stick: {nd.Letter}"; _vm.SelectedDrive = nd; return; }
            if (MessageBox.Show($"Neuer USB-Stick: {nd.Letter}\nLabel: {(string.IsNullOrWhiteSpace(nd.Label) ? "—" : nd.Label)}   Größe: {nd.SizeBytes / 1_073_741_824.0:F0} GB\n\nAutomatisch als Ventoy-Stick einrichten?\n\n⚠ ALLE DATEN AUF DIESEM STICK WERDEN GELÖSCHT!", "USB-Stick erkannt — Datenverlust!", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            _vm.SelectedDrive = nd; AppendLog($"⚡ Ventoy-Installation auf {nd.Letter} wird gestartet …"); _vm.StartVentoyInstall(updateMode: false);
        }

        /// <summary>
        /// Fragt aktiv nach, mit welchem Stick gearbeitet werden soll, wenn mehr als einer erkannt
        /// wird — sowohl beim Programmstart (bereits mehrere Sticks angeschlossen, siehe OnLoaded)
        /// als auch während der Laufzeit (siehe OnNewDriveInserted). Vorbelegt mit der aktuellen
        /// SelectedDrive (von RefreshDrives() bereits auf Drives[0] gesetzt), damit "Abbrechen" bzw.
        /// direktes Bestätigen ohne Umschalten schlicht bei der bisherigen Auswahl bleibt.
        /// </summary>
        private void OfferDriveChoiceIfMultiple()
        {
            if (_vm.Drives.Count <= 1) return;
            var dlg = new DriveSelectDialog(_vm.Drives,
                headerText: $"Es sind {_vm.Drives.Count} USB-Sticks angeschlossen. Mit welchem möchtest du arbeiten?",
                preselect: _vm.SelectedDrive)
            { Owner = this };
            if (dlg.ShowDialog() == true && dlg.SelectedDrive is not null) _vm.SelectedDrive = dlg.SelectedDrive;
        }

        private void BtnVentoy_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.IsBusy) return;
            UsbDrive? target = SelectTargetDrive(); if (target is null) return;
            string letter = target.Letter; string label = string.IsNullOrWhiteSpace(target.Label) ? "Kein Name" : target.Label; double gb = target.SizeBytes / 1_073_741_824.0;
            bool installed = UsbService.IsVentoyInstalled(letter);
            string warn = installed
                ? $"Ventoy auf\n\n   {letter}  {label}  ({gb:F0} GB)\n\naktualisieren?\n\n✅ Bestehende ISO-Dateien bleiben erhalten."
                : $"⚠ ACHTUNG — DATENVERLUST!\n\nAlle Daten auf\n\n   {letter}  {label}  ({gb:F0} GB)\n\nwerden unwiderruflich gelöscht!";
            if (MessageBox.Show(warn, installed ? "Ventoy aktualisieren" : "⚠ Ventoy installieren — Datenverlust!", MessageBoxButton.OKCancel, installed ? MessageBoxImage.Question : MessageBoxImage.Warning) != MessageBoxResult.OK) return;
            _vm.SelectedDrive = target; AppendLog($"⚡ Ventoy-{(installed ? "Aktualisierung" : "Installation")} auf {letter} …");
            SetBusyUi(true); _vm.StartVentoyInstall(updateMode: installed); SetBusyUi(false);
        }

        private UsbDrive? SelectTargetDrive()
        {
            if (_vm.Drives.Count == 0) { MessageBox.Show("Kein USB-Laufwerk erkannt.", Constants.AppTitle, MessageBoxButton.OK, MessageBoxImage.Information); return null; }
            if (_vm.Drives.Count == 1) return _vm.Drives[0];
            var dlg = new DriveSelectDialog(_vm.Drives) { Owner = this };
            return dlg.ShowDialog() == true ? dlg.SelectedDrive : null;
        }

        private void ChkSecureBoot_Changed(object sender, RoutedEventArgs e) => _vm.SecureBoot = ChkSecureBoot.IsChecked == true;

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.IsBusy) return;
            List<IsoEntry> queue = _vm.GetSelectedEntries();
            if (queue.Count == 0) { MessageBox.Show("Bitte mindestens eine Distribution markieren!", Constants.AppTitle, MessageBoxButton.OK, MessageBoxImage.Information); return; }
            string drive = _vm.SelectedDriveLetter; bool copy = false, del = false;
            if (!string.IsNullOrEmpty(drive))
            {
                var r = MessageBox.Show($"USB-Stick erkannt: {drive}\n\nHerunterladen UND direkt auf Stick kopieren?", "Download-Modus", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (r == MessageBoxResult.Cancel) return; copy = r == MessageBoxResult.Yes;
                if (copy && !UsbService.IsVentoyInstalled(drive)) if (MessageBox.Show($"Kein Ventoy auf {drive}. Trotzdem kopieren?", "Ventoy nicht gefunden", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                if (copy) del = MessageBox.Show("Lokale Dateien nach dem Kopieren löschen?", "Dateien löschen?", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
            }
            else if (MessageBox.Show($"Kein USB-Stick.\n\nISOs gespeichert in:\n{AppPaths.Instance.DownloadDir}\n\nFortfahren?", "Kein Stick erkannt", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) return;

            if (!await ConfirmEnoughFreeSpaceAsync(queue, copy ? drive : null)) return;

            int slots = 1;
            if (queue.Count > 1) { var sd = new DownloadSlotsDialog(queue.Count, Constants.MaxParallelSlots) { Owner = this }; if (sd.ShowDialog() != true) return; slots = sd.ChosenSlots; }
            StartDownloadWithProgressDialog(queue, drive, copy, del, slots);
        }

        /// <summary>
        /// Summiert die online ermittelbare Größe ALLER markierten Distros (parallel, via
        /// HttpService.GetExpectedSizeAsync — dieselbe Quelle wie der bestehende Freispeicher-Check
        /// in HttpService.DownloadAsync) und vergleicht sie VOR Start des Downloads gegen den
        /// freien Speicher am Ziel. Der bestehende Check dort greift nur PRO DATEI, kurz bevor sie
        /// geschrieben wird — bei mehreren parallelen Slots kann das dazu führen, dass mehrere große
        /// Downloads gleichzeitig starten, weil jeder einzeln (noch) genug Platz sieht, obwohl die
        /// Summe aller ausgewählten Distros den Stick/die Platte gar nicht erst füllt. Best-effort:
        /// Größen, die sich online nicht ermitteln lassen (-1), fließen nicht in die Summe ein, statt
        /// den Download deswegen zu blockieren — die Warnung weist in diesem Fall explizit auf die
        /// verbleibende Unsicherheit hin.
        /// </summary>
        private async Task<bool> ConfirmEnoughFreeSpaceAsync(List<IsoEntry> queue, string? stickDrive)
        {
            SetBusyUi(true); AppendLog("🔍 Prüfe benötigten Speicherplatz …");
            long[] sizes;
            try { sizes = await Task.WhenAll(queue.Select(e => HttpService.Instance.GetExpectedSizeAsync(e))); }
            finally { SetBusyUi(false); }

            long totalBytes  = sizes.Where(s => s > 0).Sum();
            int  unknownCount = sizes.Count(s => s <= 0);
            if (totalBytes == 0) return true; // keine einzige Größe ermittelbar — nichts zu warnen, Best-effort endet hier

            static string Gb(long b) => (b / 1_073_741_824.0).ToString("F2") + " GB";
            static string GbMb(double mb) => (mb / 1024.0).ToString("F2") + " GB";

            bool WarnIfTooSmall(string label, string root)
            {
                double freeMb;
                try { var d = new DriveInfo(root); if (!d.IsReady) return true; freeMb = d.AvailableFreeSpace / 1_048_576.0; }
                catch { return true; }
                if (totalBytes <= freeMb * 1_048_576.0) return true;

                string msg = $"Die {queue.Count} ausgewählten Distros benötigen zusammen ca. {Gb(totalBytes)}" +
                             (unknownCount > 0 ? $" (bei {unknownCount} Distro(s) war die Größe online nicht ermittelbar — evtl. mehr)" : "") +
                             $",\naber auf {label} sind nur {GbMb(freeMb)} frei.\n\nTrotzdem fortfahren?";
                return MessageBox.Show(msg, "⚠ Nicht genug Speicherplatz", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
            }

            if (!WarnIfTooSmall($"dem Arbeitsordner ({AppPaths.Instance.DownloadDir})", Path.GetPathRoot(AppPaths.Instance.DownloadDir) ?? AppPaths.Instance.DownloadDir))
                return false;
            if (!string.IsNullOrEmpty(stickDrive) && !WarnIfTooSmall($"dem Stick {stickDrive}", UsbService.DriveRoot(stickDrive)))
                return false;
            return true;
        }

        private async void StartDownloadWithProgressDialog(List<IsoEntry> queue, string drive, bool copyAfter, bool deleteAfter, int slots)
        {
            _activeDownloadDrive = drive; _activeDownloadCopyAfter = copyAfter; _activeDownloadDeleteAfter = deleteAfter;
            OpenProgressDialog(queue.Select(q => q.Name), hasDownload: true, hasCopy: copyAfter); SetBusyUi(true);
            await Task.Run(() => _vm.StartDownload(queue, drive, copyAfter, deleteAfter, slots));
            SetBusyUi(false);
        }

        private void OpenProgressDialog(IEnumerable<string> names, bool hasDownload, bool hasCopy)
        {
            _downloadProgressDialog?.Close();
            var nameList = names.ToList();
            _downloadProgressDialog = new DownloadProgressDialog(nameList, hasDownload, hasCopy) { Owner = this };
            _downloadProgressDialog.CancelRequested += () => _vm.CancelCommand.Execute(null);
            _downloadProgressDialog.FasterMirrorRequested += name => _vm.RequestFasterMirror(name);
            _downloadProgressDialog.ManualSearchRequested += OpenManualSearchFromDownloadFailure;
            _downloadProgressDialog.Closed += (_, _) => _downloadProgressDialog = null;
            // Reiner Kopiervorgang (kein Download davor): Zeilen sofort als "Kopiere auf Stick" labeln.
            if (hasCopy && !hasDownload) foreach (string n in nameList) _downloadProgressDialog.SetPhaseLabel(n, "Kopiere auf Stick");
            _downloadProgressDialog.Show();
        }

        // Öffnet ManualSourceSearchDialog direkt aus dem Download-Fortschritt-Fenster heraus, wenn
        // ResolveLatestAsync für DIESEN Versuch gar keine URL fand (DownloadProgressDialog.
        // ManualSearchRequested, siehe UpdateDownload/NoUrlFound). Ergänzt den 🔧-Button in der
        // Hauptliste (BtnManualSearch_Click), der erst ab Constants.ManualSearchFailureThreshold
        // aufeinanderfolgenden automatischen Fehlschlägen sichtbar wird — ohne diesen direkten Weg
        // müsste ein frisch über "ISO suchen" hinzugefügter, sofort fehlschlagender Eintrag erst auf
        // zwei weitere App-Starts warten, bis ein Ausweg sichtbar ist. Gleiches Speicherverhalten wie
        // BtnManualSearch_Click (Streak-Reset bei eingetragener URL, Save, RebuildTree).
        private async void OpenManualSearchFromDownloadFailure(string name)
        {
            var entry = IsoDatabaseService.Instance.Entries
                .FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
            if (entry is null) return;
            var dlg = new ManualSourceSearchDialog(entry) { Owner = _downloadProgressDialog };
            if (dlg.ShowDialog() != true) return;
            bool urlNowSet = !string.IsNullOrWhiteSpace(entry.Url);
            if (urlNowSet) entry.FailedResolveStreak = 0;
            IsoDatabaseService.Instance.Save();
            _vm.RebuildTree();
            if (!urlNowSet) return;

            // Sofort-Retry: der Download war NUR wegen der fehlenden URL gescheitert — jetzt, wo eine
            // URL manuell hinterlegt wurde, den Download für GENAU diesen Eintrag automatisch neu
            // anstoßen (dasselbe Ziel-Laufwerk/Kopieren-danach wie der ursprüngliche Batch), statt den
            // Nutzer Fenster schließen → Hauptliste → erneut "Herunterladen" klicken zu lassen. Läuft
            // die ursprüngliche Stapel-Verarbeitung noch (weitere parallele Slots aktiv, z.B. ein
            // parallel laufender Gesundheitscheck setzt zwar NICHT IsBusy, ein noch laufender zweiter
            // Download-Slot schon), wird NICHT automatisch neu gestartet — ein zweiter gleichzeitiger
            // StartDownload-Aufruf würde den Worker-/Busy-Zustand des noch laufenden Batches
            // durcheinanderbringen; die neu hinterlegte URL wird dann einfach beim nächsten regulären
            // Download-Versuch verwendet.
            if (_vm.IsBusy || _downloadProgressDialog is null)
            {
                AppendLog($"🔧 {entry.Name}: Quelle manuell hinterlegt — wird beim nächsten Download automatisch verwendet.");
                return;
            }
            AppendLog($"🔧 {entry.Name}: Quelle manuell hinterlegt — Download wird automatisch erneut versucht …");
            SetBusyUi(true);
            await Task.Run(() => _vm.StartDownload(new List<IsoEntry> { entry }, _activeDownloadDrive, _activeDownloadCopyAfter, _activeDownloadDeleteAfter, 1));
            SetBusyUi(false);
        }

        private async void BtnUpdates_Click(object sender, RoutedEventArgs e) { if (_vm.IsBusy) return; SetBusyUi(true); await Task.Run(() => _vm.CheckUpdatesCommand.Execute(null)); SetBusyUi(false); }
        private async void BtnCheckUrls_Click(object sender, RoutedEventArgs e) { if (_vm.IsBusy) return; SetBusyUi(true); await Task.Run(() => _vm.CheckUrlsCommand.Execute(null)); SetBusyUi(false); }
        private async void BtnHealthCheck_Click(object sender, RoutedEventArgs e) { if (_vm.IsBusy || _vm.HealthCheckActive) return; SetBusyUi(true); await Task.Run(() => _vm.HealthCheckCommand.Execute(null)); SetBusyUi(false); }
        private async void BtnVerifyIntegrity_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.IsBusy) return;
            if (string.IsNullOrEmpty(_vm.SelectedDriveLetter)) { MessageBox.Show("Bitte zuerst ein USB-Laufwerk auswählen!", Constants.AppTitle, MessageBoxButton.OK, MessageBoxImage.Information); return; }
            SetBusyUi(true); await _vm.VerifyStickIntegrityAsync(); SetBusyUi(false);
        }
        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new IsoSearchDialog { Owner = this };
            dlg.ShowDialog();
            if (dlg.AddedEntries.Count == 0) return;

            foreach (var entry in dlg.AddedEntries) IsoDatabaseService.Instance.Add(entry);
            IsoDatabaseService.Instance.Save();
            _vm.RebuildTree();
            AppendLog($"✅ {dlg.AddedEntries.Count} ISO(s) aus der Online-Suche zur Datenbank hinzugefügt.");
            // Frisch aus der Online-Suche übernommene Einträge haben nie eine geprüfte Url — wie bei
            // Stick-Importen lohnt sich hier der volle Gesundheitscheck sofort.
            _vm.RunHealthCheck();

            if (dlg.ToDownload.Count == 0) return;
            foreach (var entry in dlg.ToDownload) entry.IsSelected = true;
            BtnDownload_Click(sender, e);
        }
        private void BtnEditDb_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.IsBusy) { MessageBox.Show("Bitte warten …"); return; }
            var dlg = new IsoListDialog(IsoDatabaseService.Instance) { Owner = this };
            if (dlg.ShowDialog() != true) return;
            _vm.RebuildTree();
            // Nur bei manuell neu angelegten Einträgen prüfen — die haben (fast) nie eine bereits
            // verifizierte Url. Reines Bearbeiten/Löschen/Umsortieren braucht keinen Check.
            if (dlg.AnyEntryAdded) _vm.RunHealthCheck();
        }

        private void BtnGitHubToken_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new GitHubTokenDialog(_vm.GitHubToken) { Owner = this };
            if (dlg.ShowDialog() != true) return;
            _vm.GitHubToken = dlg.Token;
            AppendLog(string.IsNullOrEmpty(dlg.Token) ? "🔑 GitHub-Token entfernt." : "🔑 GitHub-Token gespeichert.");
        }

        private void BtnCopyUsb_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.IsBusy) return;
            if (string.IsNullOrEmpty(_vm.SelectedDriveLetter)) { MessageBox.Show("Bitte zuerst ein USB-Laufwerk auswählen!", Constants.AppTitle, MessageBoxButton.OK, MessageBoxImage.Information); return; }
            List<IsoEntry> queue = _vm.GetLocallyAvailableEntries();
            if (queue.Count == 0) { MessageBox.Show("Keine lokal heruntergeladenen ISOs vorhanden.", Constants.AppTitle, MessageBoxButton.OK, MessageBoxImage.Information); return; }
            bool del = MessageBox.Show("Lokale Dateien nach dem Kopieren löschen?", "Dateien löschen?", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
            OpenProgressDialog(queue.Select(q => q.Name), hasDownload: false, hasCopy: true);
            SetBusyUi(true); _vm.StartCopyToStick(queue, _vm.SelectedDriveLetter, del); SetBusyUi(false);
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) { AppendLog("⛔ Vorgang wird abgebrochen …"); _vm.CancelCommand.Execute(null); }
        private void BtnClearLog_Click(object sender, RoutedEventArgs e) => LogBox.Clear();
        private void BtnClearHistory_Click(object sender, RoutedEventArgs e) => _vm.ActivityHistory.Clear();

        private void SetBusyUi(bool busy)
        {
            BtnDownload.IsEnabled = !busy; BtnUpdates.IsEnabled = !busy; BtnCheckUrls.IsEnabled = !busy;
            BtnCopyUsb.IsEnabled  = !busy; BtnEditDb.IsEnabled  = !busy; BtnVentoy.IsEnabled    = !busy;
            BtnCancel.Visibility  = busy ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AppendLog(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}]  {msg}\n");
                LogBox.ScrollToEnd();
                try
                {
                    RotateLogIfTooLarge(AppPaths.Instance.LogFile);
                    File.AppendAllText(AppPaths.Instance.LogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\n", System.Text.Encoding.UTF8);
                }
                catch { }
            });
        }

        /// <summary>
        /// ulm_log.txt wuchs bisher unbegrenzt bei Dauerbetrieb. Überschreitet die Datei
        /// Constants.MaxLogSizeBytes, wird sie einmal zu "ulm_log.txt.old" verschoben (eine
        /// vorherige .old-Datei wird dabei überschrieben) — File.AppendAllText erzeugt danach
        /// automatisch eine neue, leere Datei. Behält so maximal ~2× die Grenzgröße an
        /// Protokoll-Historie, statt die Datei ins Unendliche wachsen zu lassen.
        /// </summary>
        private static void RotateLogIfTooLarge(string logFile)
        {
            if (!File.Exists(logFile)) return;
            var info = new FileInfo(logFile);
            if (info.Length < Constants.MaxLogSizeBytes) return;
            string oldFile = logFile + ".old";
            try { if (File.Exists(oldFile)) File.Delete(oldFile); File.Move(logFile, oldFile); }
            catch { /* Rotation ist best-effort — ein fehlgeschlagener Versuch darf das Loggen selbst nicht verhindern */ }
        }

        private static string RelativePath(string baseDir, string fullPath)
        { try { return Path.GetRelativePath(baseDir, fullPath); } catch { return Path.GetFileName(fullPath); } }

        private static string FmtSize(long bytes)
        {
            if (bytes <= 0)        return "0 Bytes";
            if (bytes < 1_024)     return $"{bytes} Bytes";
            if (bytes < 1_048_576) return $"{bytes / 1_024} KB";
            return $"{bytes / 1_048_576} MB";
        }

        protected override void OnClosed(EventArgs e) { _driveTimer.Stop(); _vm.SaveAndClose(); Application.Current.Shutdown(); base.OnClosed(e); }
    }
}
