// Views/MainWindow.xaml.cs
using System;
using System.Collections.Generic;
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

        private readonly HashSet<string> _offeredCopyKeys     = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _importedStickKeys   = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _newerVersionKeys    = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _incompleteStickKeys = new(StringComparer.OrdinalIgnoreCase);

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel(Dispatcher);
            DataContext = _vm;
            _vm.LogMessage += AppendLog;
            _vm.ShowMessageBox += (msg, isErr) => MessageBox.Show(msg, Constants.AppTitle, MessageBoxButton.OK, isErr ? MessageBoxImage.Warning : MessageBoxImage.Information);
            _vm.StickUpdateAvailable += OnStickUpdateAvailable;

            _vm.NewerVersionsOnStickDetected += (matches, drive) =>
            {
                var fresh = matches.Where(m => _newerVersionKeys.Add($"{drive}|{m.StickIso.Filename}")).ToList();
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
            };

            _vm.UnknownIsosOnStickDetected += (unknowns, drive) =>
            {
                var fresh = unknowns.Where(u => _importedStickKeys.Add($"{drive}|{u.Filename}")).ToList();
                if (fresh.Count == 0) return;
                AppendLog($"❓ {fresh.Count} unbekannte ISO(s) auf {drive}.");
                var dlg = new ImportStickIsosDialog(fresh) { Owner = this };
                if (dlg.ShowDialog() != true || dlg.ImportedEntries.Count == 0) { AppendLog("   ℹ Import übersprungen."); return; }

                int movedFailed = 0;
                foreach (var (entry, sourcePath) in dlg.ImportedEntries)
                {
                    if (UsbService.MoveToCategoryFolder(sourcePath, drive, entry.NormalizedCategory, entry.Filename, AppendLog))
                        AppendLog($"   📂 {entry.Filename} → {entry.NormalizedCategory}\\");
                    else movedFailed++;
                    _vm.AddImportedEntry(entry);
                }
                IsoDatabaseService.Instance.Save(); _vm.RebuildTree();
                AppendLog($"✅ {dlg.ImportedEntries.Count} ISO(s) zur Datenbank hinzugefügt" + (movedFailed > 0 ? $", {movedFailed} konnte(n) nicht in den Kategorie-Ordner verschoben werden" : "") + ".");
                _vm.TriggerVentoyMenuUpdate(drive);
                _vm.TriggerUsbScan();
            };

            _vm.IncompleteIsosOnStickDetected += (incomplete, drive) =>
            {
                var fresh = incomplete.Where(i => _incompleteStickKeys.Add($"{drive}|{i.Filename}")).ToList();
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

            _vm.DownloadItemProgress   += (name, pct, status) => _downloadProgressDialog?.UpdateItem(name, pct, status);
            _vm.DownloadBatchCompleted += (ok, failed, _) => { if (_downloadProgressDialog is null) return; _downloadProgressDialog.SetOverallComplete($"{ok} erfolgreich" + (failed > 0 ? $", {failed} fehlgeschlagen" : "") + "."); };
            _vm.CopyItemProgress       += (name, pct, detail) => { _downloadProgressDialog?.SetPhaseLabel(name, pct >= 100 ? "Fertig" : "Kopiere auf Stick"); _downloadProgressDialog?.UpdateItem(name, pct, detail); };
            _vm.CopyBatchCompleted     += count => { if (_downloadProgressDialog is null) return; _downloadProgressDialog.SetOverallComplete(count > 0 ? $"{count} ISO(s) auf den Stick kopiert." : "Nichts kopiert."); };

            _vm.OperationSucceeded += message =>
            {
                _downloadProgressDialog?.Close(); _downloadProgressDialog = null;
                AppendLog($"✅ {message.Split('\n')[0]}");
                MessageBox.Show(message, "✅ Vorgang abgeschlossen", MessageBoxButton.OK, MessageBoxImage.Information);
            };

            _vm.HealthCheckCompleted += results => new DbHealthCheckDialog(results) { Owner = this }.ShowDialog();

            _vm.AutoVersionCheckCompleted += async () =>
            {
                // Zeitstempel bei JEDEM abgeschlossenen Check aktualisieren (Start-Check UND
                // spätere Hintergrund-Checks) — Grundlage für CheckAutoRecheckDue().
                IniService.Write(AppPaths.Instance.SettingsIni, "App", "LastAutoCheckUtc", DateTime.UtcNow.ToString("o"));
                if (_orphanCheckDone) return;
                _orphanCheckDone = true;
                await RunLocalFileMaintenanceAsync();
            };

            _driveTimer.Interval = TimeSpan.FromSeconds(4);
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
            _lastDriveSignatureUi = string.Join(";", _vm.Drives.Select(d => d.Letter));
            FooterLbl.Text = $"ISO-Ordner: {AppPaths.Instance.DownloadDir}";
            _driveTimer.Start();
            _autoCheckTimer.Start();
            await Task.Delay(1000);
            AppendLog("🌐 Automatischer Online-Versionscheck wird gestartet …");
            _vm.TriggerAutoVersionCheck();
        }

        // Wird alle 30 Minuten geprüft: löst TriggerAutoVersionCheck() erneut aus, sobald seit
        // dem letzten Check (Start ODER vorheriger Hintergrund-Lauf) Constants.AutoCheckIntervalDays
        // vergangen sind. Übersprungen wird nur, wenn gerade ein anderer Vorgang läuft — dann
        // greift der nächste 30-Minuten-Tick.
        private void CheckAutoRecheckDue()
        {
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

        private void UpdateUiMode()
        {
            BtnModeToggle.Content = _vm.ExpertMode ? "Modus: Experte 🛠" : "Modus: Anwender 👤";
            Visibility vis = _vm.ExpertMode ? Visibility.Visible : Visibility.Collapsed;
            BtnVentoy.Visibility = vis; ChkSecureBoot.Visibility = vis; ExpertBar.Visibility = vis; LogTab.Visibility = Visibility.Visible;
        }

        private void BtnModeToggle_Click(object sender, RoutedEventArgs e) { _vm.ExpertMode = !_vm.ExpertMode; UpdateUiMode(); }

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

        private void OnStickUpdateAvailable(List<IsoEntry> outdated, string drive)
        {
            if (outdated.Count == 0) return;
            var sb = new StringBuilder(); sb.AppendLine($"Auf {drive} wurden {outdated.Count} veraltete ISO(s) gefunden:"); sb.AppendLine();
            foreach (var e in outdated) sb.AppendLine($"  • {e.Name}"); sb.AppendLine(); sb.AppendLine("Jetzt aktualisieren?");
            if (MessageBox.Show(sb.ToString(), "💾 Stick-Aktualisierung", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            foreach (var e in outdated) e.IsSelected = true; _vm.RefreshAllEntries();
            StartDownloadWithProgressDialog(outdated, drive, copyAfter: true, deleteAfter: false, slots: Math.Min(outdated.Count, Constants.MaxParallelSlots));
        }

        private void OnMissingOnStickDetected(List<IsoEntry> entries, string drive)
        {
            var fresh = entries.Where(e => _offeredCopyKeys.Add($"{drive}|{e.Filename}")).ToList();
            if (fresh.Count == 0) return;
            var sb = new StringBuilder(); sb.AppendLine($"{fresh.Count} ISO(s) vollständig lokal, NICHT auf {drive}:"); sb.AppendLine();
            foreach (var e in fresh) sb.AppendLine($"  • {e.Name}"); sb.AppendLine(); sb.AppendLine("Jetzt kopieren?");
            if (MessageBox.Show(sb.ToString(), "💾 Vollständige ISOs nicht auf dem Stick", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            bool del = MessageBox.Show("Lokale Dateien danach löschen?", "Dateien löschen?", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
            OpenProgressDialog(fresh.Select(q => q.Name), copyPhase: true);
            _vm.StartCopyToStick(fresh, drive, del);
        }

        private void EntryRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;
            if (sender is FrameworkElement fe && fe.DataContext is IsoEntryViewModel vm && vm.Model != null && !string.IsNullOrWhiteSpace(vm.Model.Tip))
                MessageBox.Show(vm.Model.Tip, vm.Model.Name, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnRefreshDrives_Click(object sender, RoutedEventArgs e) => CheckDriveChanges();

        private void CheckDriveChanges()
        {
            string prev = _lastDriveSignatureUi; _vm.RefreshDrives();
            string curr = string.Join(";", _vm.Drives.Select(d => d.Letter)); _lastDriveSignatureUi = curr;
            if (curr != prev && curr.Length > prev.Length) OnNewDriveInserted();
        }

        private void OnNewDriveInserted()
        {
            UsbDrive? nd = _vm.Drives.Count == 1 ? _vm.Drives[0] : null;
            if (nd is null) { AppendLog($"🔌 {_vm.Drives.Count} USB-Laufwerke erkannt: " + string.Join(", ", _vm.Drives.Select(d => $"{d.Letter} ({d.Label})"))); _vm.SelectedDrive = _vm.Drives[0]; return; }
            if (UsbService.IsVentoyInstalled(nd.Letter)) { StatusLbl.Text = $"✅ Ventoy-Stick: {nd.Letter}"; _vm.SelectedDrive = nd; return; }
            if (MessageBox.Show($"Neuer USB-Stick: {nd.Letter}\nLabel: {(string.IsNullOrWhiteSpace(nd.Label) ? "—" : nd.Label)}   Größe: {nd.SizeBytes / 1_073_741_824.0:F0} GB\n\nAutomatisch als Ventoy-Stick einrichten?\n\n⚠ ALLE DATEN AUF DIESEM STICK WERDEN GELÖSCHT!", "USB-Stick erkannt — Datenverlust!", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            _vm.SelectedDrive = nd; AppendLog($"⚡ Ventoy-Installation auf {nd.Letter} wird gestartet …"); _vm.StartVentoyInstall(updateMode: false);
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

        private void BtnDownload_Click(object sender, RoutedEventArgs e)
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

            int slots = 1;
            if (queue.Count > 1) { var sd = new DownloadSlotsDialog(queue.Count, Constants.MaxParallelSlots) { Owner = this }; if (sd.ShowDialog() != true) return; slots = sd.ChosenSlots; }
            StartDownloadWithProgressDialog(queue, drive, copy, del, slots);
        }

        private async void StartDownloadWithProgressDialog(List<IsoEntry> queue, string drive, bool copyAfter, bool deleteAfter, int slots)
        {
            OpenProgressDialog(queue.Select(q => q.Name), copyPhase: false); SetBusyUi(true);
            await Task.Run(() => _vm.StartDownload(queue, drive, copyAfter, deleteAfter, slots));
            SetBusyUi(false);
        }

        private void OpenProgressDialog(IEnumerable<string> names, bool copyPhase)
        {
            _downloadProgressDialog?.Close();
            _downloadProgressDialog = new DownloadProgressDialog(names) { Owner = this };
            _downloadProgressDialog.CancelRequested += () => _vm.CancelCommand.Execute(null);
            _downloadProgressDialog.Closed += (_, _) => _downloadProgressDialog = null;
            if (copyPhase) foreach (string n in names) _downloadProgressDialog.SetPhaseLabel(n, "Kopiere auf Stick");
            _downloadProgressDialog.Show();
        }

        private async void BtnUpdates_Click(object sender, RoutedEventArgs e) { if (_vm.IsBusy) return; SetBusyUi(true); await Task.Run(() => _vm.CheckUpdatesCommand.Execute(null)); SetBusyUi(false); }
        private async void BtnCheckUrls_Click(object sender, RoutedEventArgs e) { if (_vm.IsBusy) return; SetBusyUi(true); await Task.Run(() => _vm.CheckUrlsCommand.Execute(null)); SetBusyUi(false); }
        private async void BtnHealthCheck_Click(object sender, RoutedEventArgs e) { if (_vm.IsBusy || _vm.HealthCheckActive) return; SetBusyUi(true); await Task.Run(() => _vm.HealthCheckCommand.Execute(null)); SetBusyUi(false); }
        private void BtnSearch_Click(object sender, RoutedEventArgs e) => new IsoSearchDialog(IsoDatabaseService.Instance) { Owner = this }.ShowDialog();
        private void BtnEditDb_Click(object sender, RoutedEventArgs e) { if (_vm.IsBusy) { MessageBox.Show("Bitte warten …"); return; } var dlg = new IsoListDialog(IsoDatabaseService.Instance) { Owner = this }; if (dlg.ShowDialog() == true) _vm.RebuildTree(); }

        private void BtnCopyUsb_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.IsBusy) return;
            if (string.IsNullOrEmpty(_vm.SelectedDriveLetter)) { MessageBox.Show("Bitte zuerst ein USB-Laufwerk auswählen!", Constants.AppTitle, MessageBoxButton.OK, MessageBoxImage.Information); return; }
            List<IsoEntry> queue = _vm.GetLocallyAvailableEntries();
            if (queue.Count == 0) { MessageBox.Show("Keine lokal heruntergeladenen ISOs vorhanden.", Constants.AppTitle, MessageBoxButton.OK, MessageBoxImage.Information); return; }
            bool del = MessageBox.Show("Lokale Dateien nach dem Kopieren löschen?", "Dateien löschen?", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
            OpenProgressDialog(queue.Select(q => q.Name), copyPhase: true);
            SetBusyUi(true); _vm.StartCopyToStick(queue, _vm.SelectedDriveLetter, del); SetBusyUi(false);
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) { AppendLog("⛔ Vorgang wird abgebrochen …"); _vm.CancelCommand.Execute(null); }
        private void BtnClearLog_Click(object sender, RoutedEventArgs e) => LogBox.Clear();

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
                try { File.AppendAllText(AppPaths.Instance.LogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\n", System.Text.Encoding.UTF8); }
                catch { }
            });
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
