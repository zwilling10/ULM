// ViewModels/MainViewModel.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Threading;
using ULM.Core.Models;
using ULM.Core.Services;
using ULM.Core.Workers;
using ULM.Infrastructure;

namespace ULM.ViewModels
{
    public sealed class MainViewModel : ViewModelBase
    {
        private readonly IIsoDatabaseService _db;
        private readonly IHttpService        _http;
        private readonly IUsbService         _usb;
        private readonly AppPaths           _paths = AppPaths.Instance;
        private readonly Dispatcher         _ui;
        private CancellationTokenSource _workerCts = new();
        private object?  _activeWorker;
        private string   _lastDriveSignature = string.Empty;
        // Startphase: unterdrückt den SOFORTIGEN Stick-Scan beim Programmstart (der sonst über den
        // SelectedDrive-Setter → TriggerUsbScan noch VOR dem Online-Versionscheck liefe). Gewünschte
        // Reihenfolge: erst der Online-Versionscheck, danach der Stick-Scan — Letzteren stößt der
        // Abschluss des Versionschecks ohnehin selbst an (siehe TriggerAutoVersionCheck.Completed).
        // Wird nach dem ersten abgeschlossenen Versionscheck aufgehoben; danach scannt ein
        // Laufwerkswechsel/Neu-Einstecken wieder sofort wie gewohnt.
        private bool     _startupPhase = true;
        private bool     _expertMode;

        // Session-Dedup: verhindert, dass derselbe Stick-Fund (Laufwerk+Dateiname) dem Nutzer
        // mehrfach als Dialog/Meldung angeboten wird, wenn TriggerUsbScan mehrfach über denselben
        // Fund läuft. Anwendungszustand ("wurde dieser Fund schon behandelt?"), daher im ViewModel
        // statt in MainWindow.xaml.cs (dort lag es vorher, was gegen MVVM verstieß).
        private readonly HashSet<string> _offeredCopyKeys     = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _importedStickKeys   = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _newerVersionKeys    = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _incompleteStickKeys = new(StringComparer.OrdinalIgnoreCase);

        public bool MarkCopyOffered(string drive, string filename)               => _offeredCopyKeys.Add($"{drive}|{filename}");
        public bool MarkUnknownStickIsoOffered(string drive, string filename)    => _importedStickKeys.Add($"{drive}|{filename}");
        public bool MarkNewerVersionOffered(string drive, string filename)       => _newerVersionKeys.Add($"{drive}|{filename}");
        public bool MarkIncompleteStickIsoOffered(string drive, string filename) => _incompleteStickKeys.Add($"{drive}|{filename}");

        public ObservableCollection<IsoCategoryViewModel> Categories { get; } = new();
        private ObservableCollection<UsbDrive> _drives = new();
        public  ObservableCollection<UsbDrive> Drives  { get => _drives; private set => SetField(ref _drives, value); }

        private UsbDrive? _selectedDrive;
        public  UsbDrive? SelectedDrive
        {
            get => _selectedDrive;
            set { if (SetField(ref _selectedDrive, value)) { OnPropertyChanged(nameof(SelectedDriveLetter)); OnPropertyChanged(nameof(DriveInfoText)); TriggerUsbScan(); } }
        }
        public string SelectedDriveLetter => _selectedDrive?.Letter ?? string.Empty;
        public string DriveInfoText
        {
            get
            {
                if (_selectedDrive is null) return string.Empty;
                bool   v = UsbService.IsVentoyInstalled(_selectedDrive.Letter);
                double f = UsbService.DriveFreeMb(_selectedDrive.Letter);
                double t = UsbService.DriveTotalMb(_selectedDrive.Letter);
                return $"{(v ? "✅ Ventoy" : "⚠ Kein Ventoy")}   Frei: {f/1024:F1} GB / {t/1024:F1} GB";
            }
        }

        private string _statusText = "Bereit."; public string StatusText { get => _statusText; private set => SetField(ref _statusText, value); }
        private int    _progressPercent; public int ProgressPercent { get => _progressPercent; set => SetField(ref _progressPercent, value); }
        private bool   _isBusy; public bool IsBusy { get => _isBusy; private set { if (SetField(ref _isBusy, value)) RelayCommand.RaiseCanExecuteChanged(); } }
        private bool   _onlineScanActive;  public bool OnlineScanActive  { get => _onlineScanActive;  private set { if (SetField(ref _onlineScanActive, value)) NotifyScanHint(); } }
        private int    _onlineScanPercent; public int  OnlineScanPercent { get => _onlineScanPercent; private set => SetField(ref _onlineScanPercent, value); }
        private bool   _usbScanActive;    public bool UsbScanActive     { get => _usbScanActive;     private set { if (SetField(ref _usbScanActive, value)) NotifyScanHint(); } }
        private int    _usbScanPercent;   public int  UsbScanPercent    { get => _usbScanPercent;    private set => SetField(ref _usbScanPercent,    value); }

        private string _nextAutoCheckText = "wird berechnet …";
        public string NextAutoCheckText { get => _nextAutoCheckText; private set => SetField(ref _nextAutoCheckText, value); }
        private string _lastAutoCheckText = "wird berechnet …";
        public string LastAutoCheckText { get => _lastAutoCheckText; private set => SetField(ref _lastAutoCheckText, value); }

        public ObservableCollection<string> ActivityHistory { get; } = new();
        private const int MaxActivityHistoryEntries = 30;

        // Für die "Aktueller Vorgang"-Karte im Status-Reiter (Experten-Modus): Detailinfo zum
        // gerade laufenden manuellen Vorgang (Download/Kopieren/Integritätsprüfung/Ventoy/
        // URL-Check/Update-Check). "—" = keine Angabe für den aktuell laufenden Vorgangstyp
        // verfügbar (z.B. Ventoy-Installation liefert keine Datei-/Zähler-Info).
        private string _currentOperationItem = "—";
        public string CurrentOperationItem { get => _currentOperationItem; private set => SetField(ref _currentOperationItem, value); }
        private string _currentOperationDetail = "—";
        public string CurrentOperationDetail { get => _currentOperationDetail; private set => SetField(ref _currentOperationDetail, value); }
        private string _currentOperationCounter = "—";
        public string CurrentOperationCounter { get => _currentOperationCounter; private set => SetField(ref _currentOperationCounter, value); }

        // Für die "Automatische Hintergrund-Scans"-Karte: welcher Distro-Eintrag der automatische
        // Online-Versionscheck gerade prüft (Stick-Prüfung liefert keine Pro-Datei-Zwischenstände,
        // siehe UsbService.ScanStickVerifiedAsync — deshalb kein Pendant dafür).
        private string _onlineScanCurrentItem = "—";
        public string OnlineScanCurrentItem { get => _onlineScanCurrentItem; private set => SetField(ref _onlineScanCurrentItem, value); }

        // Für den Startphasen-Hinweis (rotierender Spinner + pulsierender Text): sichtbar, solange
        // der Online-Versionscheck ODER der darauf folgende Stick-Scan läuft — damit Anwender/Experte
        // beim Programmstart nicht vorschnell klicken, bevor Datenbank/Stick-Stand vollständig sind.
        public bool ScanInProgress => OnlineScanActive || UsbScanActive;
        public string ScanHintText => OnlineScanActive ? "Online-Scan, bitte warten"
                                    : UsbScanActive     ? "Stick-Scan, bitte warten"
                                    : string.Empty;
        private void NotifyScanHint() { OnPropertyChanged(nameof(ScanInProgress)); OnPropertyChanged(nameof(ScanHintText)); }
        private bool   _healthCheckActive;  public bool HealthCheckActive  { get => _healthCheckActive;  private set => SetField(ref _healthCheckActive,  value); }
        private int    _healthCheckPercent; public int  HealthCheckPercent { get => _healthCheckPercent; private set => SetField(ref _healthCheckPercent, value); }

        public bool ExpertMode { get => _expertMode; set { if (SetField(ref _expertMode, value)) IniService.Write(_paths.SettingsIni, "App", "ExpertMode", value ? "1" : "0"); } }
        private bool _secureBoot = true;
        public  bool SecureBoot { get => _secureBoot; set { if (SetField(ref _secureBoot, value)) IniService.Write(_paths.SettingsIni, "App", "SecureBoot", value ? "1" : "0"); } }
        private bool _showInfoPopup = true;
        public  bool ShowInfoPopup { get => _showInfoPopup; set => SetField(ref _showInfoPopup, value); }

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

        private string _gitHubToken = string.Empty;
        // Optional — hebt nur das API-Limit für GitHub-basierte Resolver/Ventoy-Update-Check von
        // 60 auf 5000 Anfragen/Std an (siehe HttpService.GitHubToken). Ohne Token funktioniert alles
        // wie bisher. _http.GitHubToken wird bei jeder Änderung sofort mit aktualisiert.
        public string GitHubToken
        {
            get => _gitHubToken;
            set { if (SetField(ref _gitHubToken, value)) { IniService.Write(_paths.SettingsIni, "App", "GitHubToken", value); _http.GitHubToken = value; } }
        }

        public RelayCommand DownloadCommand           { get; }
        public RelayCommand CopyToUsbCommand        { get; }
        public RelayCommand CheckUpdatesCommand     { get; }
        public RelayCommand CheckUrlsCommand        { get; }
        public RelayCommand HealthCheckCommand      { get; }
        public RelayCommand VentoyCommand           { get; }
        public RelayCommand CancelCommand           { get; }
        public RelayCommand RefreshDrivesCommand    { get; }
        public RelayCommand VerifyStickIntegrityCommand { get; }

        public event Action<string>?       LogMessage;
        public event Action<string, bool>? ShowMessageBox;
        public event Action<List<(IsoEntry Entry, string OldFilename)>, string>?  StickUpdateAvailable;
        public event Action<List<(IsoEntry Entry, string OldFilename)>, string>? StaleDuplicatesOnStickDetected;
        public event Action<List<IsoEntry>, string>?  MissingOnStickDetected;
        public event Action<List<UsbService.StickIso>, string>? UnknownIsosOnStickDetected;
        public event Action<List<UsbService.StickIso>, string>? IncompleteIsosOnStickDetected;
        public event Action<List<VersionCheckEntryResult>>?     HealthCheckCompleted;
        public event Action<List<(IsoEntry DbEntry, UsbService.StickIso StickIso)>, string>? NewerVersionsOnStickDetected;
        public event Action<string, int, string, bool>? DownloadItemProgress;
        public event Action<int, int, int>?       DownloadBatchCompleted;
        public event Action<string, int, string>? CopyItemProgress;
        public event Action<int>?                 CopyBatchCompleted;
        public event Action<string>?              OperationSucceeded;
        public event Action?                      AutoVersionCheckCompleted;
        public event Action?                      RefreshTree;

        /// <summary>
        /// Wird vom DownloadWorker aufgerufen, wenn alle Mirror einer Distro ausgeschöpft sind, aber
        /// mindestens einer davon nur wegen dauerhafter Langsamkeit (nicht wegen eines echten
        /// Fehlers) abgebrochen wurde — (EntryName, Host) → true = trotzdem fortfahren. Ein Func statt
        /// eines Events, da hier (anders als bei den übrigen View-Benachrichtigungen oben) eine
        /// Antwort vom Anwender zurück in den wartenden Hintergrund-Task fließen muss. Von
        /// MainWindow gesetzt (zeigt die eigentliche MessageBox).
        /// </summary>
        public Func<string, string, bool>? ConfirmSlowDownload;

        public MainViewModel(Dispatcher ui, IHttpService? http = null, IUsbService? usb = null, IIsoDatabaseService? db = null)
        {
            _ui   = ui;
            _http = http ?? HttpService.Instance;
            _usb  = usb  ?? UsbService.Instance;
            _db   = db   ?? IsoDatabaseService.Instance;
            DownloadCommand      = new RelayCommand(OnDownload,     () => !IsBusy);
            CopyToUsbCommand     = new RelayCommand(OnCopyToUsb,    () => !IsBusy);
            CheckUpdatesCommand  = new RelayCommand(OnCheckUpdates, () => !IsBusy);
            CheckUrlsCommand     = new RelayCommand(OnCheckUrls,    () => !IsBusy);
            HealthCheckCommand   = new RelayCommand(OnHealthCheck,  () => !IsBusy && !HealthCheckActive);
            VentoyCommand        = new RelayCommand(OnVentoy,       () => !IsBusy);
            CancelCommand        = new RelayCommand(OnCancel,       () => IsBusy);
            RefreshDrivesCommand = new RelayCommand(RefreshDrives);
            VerifyStickIntegrityCommand = new RelayCommand(() => _ = VerifyStickIntegrityAsync(), () => !IsBusy && !string.IsNullOrEmpty(SelectedDriveLetter));
            _expertMode = IniService.Read(_paths.SettingsIni, "App", "ExpertMode", "0") == "1";
            _secureBoot = IniService.Read(_paths.SettingsIni, "App", "SecureBoot", "1") != "0";
            _gitHubToken = IniService.Read(_paths.SettingsIni, "App", "GitHubToken", string.Empty);
            _http.GitHubToken = _gitHubToken;

            // Speist die "Aktueller Vorgang"-Karte im Status-Reiter aus den bereits vorhandenen
            // Fortschritts-Events, statt jede der zahlreichen DownloadItemProgress/CopyItemProgress-
            // Aufrufstellen einzeln anzufassen — beide Events feuern bereits über _ui.Invoke, hier
            // also kein zusätzliches Thread-Marshalling nötig.
            DownloadItemProgress += (name, _, detail, _) => { CurrentOperationItem = name; CurrentOperationDetail = detail; };
            CopyItemProgress     += (name, _, detail)    => { CurrentOperationItem = name; CurrentOperationDetail = detail; };
        }

        public void Initialize()
        {
            Log("▶ Universal Linux Manager gestartet.");
            Log($"   ISO-Ordner: {_paths.DownloadDir}");
            Log($"   Datenbank:  {_paths.DatabaseIni}");
            _db.Load();
            RemoveKaliEntries();
            DeduplicateEntries();
            SyncStaleNames();
            Log($"   {_db.Count} Distros in der Datenbank geladen.");
            RebuildTree();
            RefreshDrives();
        }

        private void RemoveKaliEntries()
        {
            bool changed = false;
            for (int i = _db.Entries.Count - 1; i >= 0; i--)
            {
                var e = _db.Entries[i];
                if (e.Name.Contains("kali", StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(e.Filename) && e.Filename.Contains("kali", StringComparison.OrdinalIgnoreCase)))
                { Log($"   🗑 DB-Eintrag entfernt: {e.Name}  ({e.Filename})"); _db.Remove(i); changed = true; }
            }
            if (changed) _db.Save();
        }

        private int DeduplicateEntries()
        {
            bool changed = false; int removed = 0;

            // Zuerst EXAKTE Duplikate (identischer Dateiname) entfernen — die AreSameDistro-Logik
            // unten behandelt nur "gleiche Distro, ANDERE Version" und ließe identische Einträge stehen.
            foreach (int i in DistroMatcher.FindExactDuplicateIndicesByFilename(_db.Entries))
            { Log($"   🗑 Exaktes Duplikat entfernt: {_db.Entries[i].Name}  ({_db.Entries[i].Filename})"); _db.Remove(i); changed = true; removed++; }

            var processed = new HashSet<IsoEntry>();
            var snapshot  = _db.Entries.ToList();

            for (int i = 0; i < snapshot.Count; i++)
            {
                var a = snapshot[i]; if (processed.Contains(a)) continue;
                var duplicates = snapshot.Skip(i + 1)
                    .Where(b => !processed.Contains(b) &&
                                !string.Equals(a.Filename, b.Filename, StringComparison.OrdinalIgnoreCase) &&
                                DistroMatcher.AreSameDistro(a, b))
                    .ToList();
                if (duplicates.Count == 0) continue;

                var allEntries = new List<IsoEntry> { a }.Concat(duplicates).ToList();
                var keeper = allEntries.OrderBy(e => e.ImportedFromStick ? 1 : 0).First();
                var newerDup = allEntries
                    .Where(e => e != keeper && !string.IsNullOrWhiteSpace(e.Filename) &&
                                DistroMatcher.IsVersionNewer(
                                    HttpService.ExtractVersion(e.Filename),
                                    HttpService.ExtractVersion(string.IsNullOrWhiteSpace(keeper.Filename) ? keeper.Name : keeper.Filename)))
                    .OrderByDescending(e => HttpService.ExtractVersion(e.Filename))
                    .FirstOrDefault();
                if (newerDup != null)
                {
                    string oldVer = HttpService.ExtractVersion(string.IsNullOrWhiteSpace(keeper.Filename) ? keeper.Name : keeper.Filename);
                    string newVer = HttpService.ExtractVersion(newerDup.Filename);
                    string oldName = keeper.Name;
                    keeper.Filename = newerDup.Filename;
                    if (!string.IsNullOrEmpty(oldVer) && !string.IsNullOrEmpty(newVer) && oldVer != newVer)
                    { int pos = keeper.Name.IndexOf(oldVer, StringComparison.Ordinal); if (pos >= 0) keeper.Name = keeper.Name[..pos] + newVer + keeper.Name[(pos + oldVer.Length)..]; }
                    Log($"   🔄 Zusammengeführt: {oldName} → {keeper.Name}  ({keeper.Filename})");
                }

                // FIX CS1929: IReadOnlyList<T> hat kein IndexOf → .ToList() verwenden.
                // Rückwärts sortiert (höchster Index zuerst) damit Entfernen
                // die Indizes niedrigerer Einträge nicht verschiebt.
                var dupsToRemove = allEntries.Where(e => e != keeper).ToList();
                for (int di = dupsToRemove.Count - 1; di >= 0; di--)
                {
                    var dup = dupsToRemove[di];
                    int idx = _db.Entries.ToList().IndexOf(dup); // ToList() weil IReadOnlyList kein IndexOf hat
                    if (idx >= 0) { Log($"   🗑 Duplikat entfernt: {dup.Name}"); _db.Remove(idx); changed = true; removed++; }
                    processed.Add(dup);
                }
                processed.Add(keeper);
            }
            if (changed) _db.Save();
            return removed;
        }

        private void SyncStaleNames()
        {
            bool changed = false;
            foreach (var e in _db.Entries)
            {
                if (string.IsNullOrWhiteSpace(e.Filename) || string.IsNullOrWhiteSpace(e.Name)) continue;
                string fv = HttpService.ExtractVersion(e.Filename); if (string.IsNullOrEmpty(fv)) continue;
                string nv = HttpService.ExtractVersion(e.Name); if (string.IsNullOrEmpty(nv) || nv == fv) continue;
                int pos = e.Name.IndexOf(nv, StringComparison.Ordinal); if (pos < 0) continue;
                e.Name = e.Name[..pos] + fv + e.Name[(pos + nv.Length)..]; changed = true;
            }
            if (changed) _db.Save();
        }

        public void RebuildTree()
        {
            Categories.Clear();
            var catMap = new Dictionary<string, IsoCategoryViewModel>(StringComparer.OrdinalIgnoreCase);
            foreach (string cat in Constants.Categories) { var vm = new IsoCategoryViewModel(cat); catMap[cat] = vm; Categories.Add(vm); }
            foreach (IsoEntry entry in _db.Entries)
            { string cat = entry.NormalizedCategory; if (!catMap.TryGetValue(cat, out var catVm)) catVm = catMap["Einsteiger"]; catVm.Entries.Add(new IsoEntryViewModel(entry, _paths.DownloadDir)); }
            RefreshTree?.Invoke();
        }

        public void RefreshAllEntries() { foreach (var cat in Categories) foreach (var e in cat.Entries) e.Refresh(); RefreshTree?.Invoke(); }
        public List<IsoEntry> GetSelectedEntries() => _db.Entries.Where(e => e.IsSelected).ToList();
        public List<IsoEntry> GetLocallyAvailableEntries() => _db.Entries.Where(e => e.IsLocallyAvailable(_paths.DownloadDir)).ToList();
        public List<IsoEntry> GetVerifiedCompleteEntriesMissingFromStick() =>
            _db.Entries.Where(e => e.VerifiedComplete && e.IsLocallyAvailable(_paths.DownloadDir) && e.UsbStatus != Core.Models.UsbStatus.Ok).ToList();

        public void AddImportedEntry(IsoEntry e)
        {
            var existing = _db.Entries.FirstOrDefault(d => DistroMatcher.AreSameDistro(d, e));
            if (existing != null)
            {
                Log($"   🔗 {e.Filename} bereits als \"{existing.Name}\" in der DB — Dateiname übernommen statt Duplikat angelegt.");
                existing.Filename = e.Filename;
                existing.ImportedFromStick = true;
                _db.Save();
                return;
            }
            _db.Add(e); Log($"   + [{e.Category}] {e.Name}  ({e.Filename})");
        }

        public void ReplaceEntryVersion(IsoEntry e, string newFn)
        {
            string oldFn  = e.Filename;
            string oldVer = HttpService.ExtractVersion(oldFn);
            if (string.IsNullOrEmpty(oldVer)) oldVer = HttpService.ExtractVersion(e.Name);
            string newVer = HttpService.ExtractVersion(newFn);
            e.Filename = newFn; e.RemoteVersion = string.Empty; e.RemoteUrl = string.Empty;
            e.RemoteFilename = string.Empty; e.UpdateAvailable = false;
            if (!string.IsNullOrEmpty(oldVer) && !string.IsNullOrEmpty(newVer) && oldVer != newVer)
            {
                int pos = e.Name.IndexOf(oldVer, StringComparison.Ordinal);
                if (pos >= 0) { string on = e.Name; e.Name = e.Name[..pos] + newVer + e.Name[(pos + oldVer.Length)..]; Log($"   ✏ {on} → {e.Name}"); }
            }
            Log($"   ↔ {e.Name}: {oldFn} → {newFn}"); _db.Save();
        }

        public IsoEntry AddEntryFromStickVersion(IsoEntry src, UsbService.StickIso si)
        {
            var e = new IsoEntry { Name=src.Name, Category=src.Category, Filename=si.Filename,
                GithubRepo=src.GithubRepo, GithubAsset=src.GithubAsset, Tip=src.Tip, ImportedFromStick=true };
            _db.Add(e); Log($"   + {e.Name}  ({e.Filename})"); _db.Save(); return e;
        }

        public void RefreshDrives()
        {
            var list = _usb.ListRemovableDrives(); string sig = UsbService.ListSignature(list);
            if (sig == _lastDriveSignature) return; _lastDriveSignature = sig;
            string pl = SelectedDrive?.Letter ?? string.Empty; Drives.Clear();
            foreach (var d in list) Drives.Add(d);
            SelectedDrive = Drives.FirstOrDefault(d => d.Letter == pl) ?? (Drives.Count > 0 ? Drives[0] : null);
            OnPropertyChanged(nameof(DriveInfoText));
            if (Drives.Count > 0) Log($"🔌 Laufwerke: {string.Join(", ", Drives.Select(d => $"{d.Letter} ({d.Label})"))}");
        }

        // BUGFIX: Ohne Re-Entrancy-Sperre konnte ein zweiter TriggerUsbScan-Aufruf (z.B. durch eine
        // erneute Laufwerkserkennung während eine Installation läuft) einen zweiten UsbScanWorker
        // parallel zum bereits laufenden starten — doppelte Scan-Ergebnisse, doppelte Folge-Dialoge
        // (unbekannte ISOs, neuere Version auf dem Stick). Ein laufender Scan wird jetzt zu Ende
        // geführt, statt einen weiteren nebenher zu starten.
        public void TriggerUsbScan()
        {
            // Während der Startphase bewusst NICHT scannen — der Stick-Scan folgt erst nach dem
            // Online-Versionscheck (siehe _startupPhase / TriggerAutoVersionCheck.Completed).
            if (_startupPhase) return;
            if (string.IsNullOrEmpty(SelectedDriveLetter) || UsbScanActive) return;
            Log($"💾 Stick-Scan: {SelectedDriveLetter}");
            StatusText = $"Scanne {SelectedDriveLetter}..."; UsbScanActive = true; UsbScanPercent = 0;
            string letter = SelectedDriveLetter;
            var worker = new UsbScanWorker(letter, _db.Entries);
            worker.Completed += (ltr, found, incomplete) => _ui.Invoke(() =>
            {
                UsbScanActive = false; UsbScanPercent = 100;
                StatusText = $"✓ Stick-Scan {ltr}: {found.Count} ISO(s).";
                Log($"💾 Stick-Scan {ltr}: {found.Count} ISO(s) gefunden.");
                if (found.Count > 0) foreach (var iso in found) Log($"   • {iso.Filename}  [{iso.Category}]  {iso.Size/1_073_741_824.0:F2} GB");
                OnPropertyChanged(nameof(DriveInfoText));
                // Manueller Scan hat keinen Versionscheck-Kontext → leeres oldFn (keine Veraltet-/Duplikat-Trennung).
                ProcessStickScanResults(found, incomplete, new Dictionary<string, int>(), ltr);
                // Bewusst KEIN automatischer RunHealthCheck() mehr hier: TriggerUsbScan läuft bei
                // jedem Stick-Einstecken, jeder Ventoy-Installation und jedem Kopiervorgang — ein
                // voller Katalog-Gesundheitscheck (Netzwerk-Requests für JEDEN Eintrag) bei jedem
                // dieser rein lokalen/Hardware-Ereignisse war unnötig und störend (siehe Nutzer-
                // Feedback: Check kam sowohl beim Stick-Erkennen als auch direkt nach erfolgreicher
                // Installation, beides ohne Bezug zur Online-Erreichbarkeit der URLs). Die Aufgabe
                // "sind die Download-Quellen noch gültig?" übernimmt bereits TriggerAutoVersionCheck
                // (läuft beim Start und danach periodisch alle Constants.AutoCheckIntervalDays Tage,
                // ebenfalls mit checkAllEntries:true). RunHealthCheck() wird jetzt gezielt nur noch
                // ausgelöst, wenn NEUE, unverifizierte Einträge zur DB hinzukommen (Stick-Import,
                // manuelles Hinzufügen im DB-Editor, "Hinzufügen" bei neuerer Stick-Version — siehe
                // MainWindow.xaml.cs) sowie weiterhin manuell über den Gesundheitscheck-Button.
            });
            _ = worker.RunAsync();
        }

        /// <summary>
        /// Gemeinsame Auswertung eines Stick-Scan-Ergebnisses für BEIDE Scan-Pfade (Start-Scan in
        /// TriggerAutoVersionCheck und manueller TriggerUsbScan). Vorher hatten beide Pfade eigenen,
        /// auseinandergedrifteten Code — nur TriggerUsbScan erkannte unbekannte ISOs, nur der Start-Scan
        /// die Veraltet-/Duplikat-Trennung. oldFn ist leer, wenn kein Versionscheck-Kontext vorliegt
        /// (manueller Scan) → die Veraltet-/Duplikat-Erkennung liefert dann leere Listen. Muss auf dem
        /// UI-Thread aufgerufen werden (verändert gebundene Zustände und feuert UI-Dialog-Events).
        /// </summary>
        private void ProcessStickScanResults(
            List<UsbService.StickIso> found, List<UsbService.StickIso> incomplete,
            Dictionary<string, int> oldFn, string drive)
        {
            ApplyStickResults(found); RefreshAllEntries();

            if (incomplete.Count > 0)
            {
                Log($"⚠ Stick-Prüfung {drive}: {incomplete.Count} unvollständige ISO(s) erkannt (Online-Größenprüfung).");
                foreach (var s in incomplete) Log($"   ⚠ {s.Filename}  ({FormatGb(s.Size)}) — vermutlich Datenmüll.");
                IncompleteIsosOnStickDetected?.Invoke(incomplete, drive);
            }

            // Hash-Abgleich für versionslose Dateinamen (fire-and-forget — kann bei GB-ISOs dauern).
            _ = Task.Run(async () =>
            {
                var mismatches = await DetectVersionlessHashMismatchesAsync(found).ConfigureAwait(false);
                _ui.Invoke(() =>
                {
                    RefreshAllEntries();
                    if (mismatches.Count == 0) return;
                    Log($"⚠ Stick-Prüfung {drive}: {mismatches.Count} ISO(s) mit versionslosem Namen weichen vom bekannten Referenz-Hash ab.");
                    foreach (var m in mismatches) Log($"   ⚠ {m.Filename} — Hash-Abweichung, vermutlich beschädigt oder ersetzt.");
                    IncompleteIsosOnStickDetected?.Invoke(mismatches, drive);
                });
            });

            // Veraltet-/Duplikat-Trennung (nur mit Versionscheck-Kontext; sonst oldFn leer → leere Listen).
            var stickFn = new HashSet<string>(found.Select(f => f.Filename), StringComparer.OrdinalIgnoreCase);
            var (od, duplicates) = DistroMatcher.SplitOutdatedFromDuplicates(oldFn, _db.Entries, stickFn);
            if (od.Count > 0)
            {
                Log($"💾 {od.Count} veraltete ISO(s) auf {drive}.");
                foreach (var (e, _) in od) Log($"   🆕 {e.Name}: v{e.RemoteVersion}");
                StickUpdateAvailable?.Invoke(od, drive);
            }
            if (duplicates.Count > 0)
            {
                Log($"🗑 {duplicates.Count} veraltete Duplikat-ISO(s) auf {drive} (aktuelle Version bereits vorhanden).");
                foreach (var (e, oldFilename) in duplicates) Log($"   🗑 {e.Name}: {oldFilename}");
                StaleDuplicatesOnStickDetected?.Invoke(duplicates, drive);
            }
            if (od.Count == 0 && duplicates.Count == 0 && found.Count > 0)
                Log($"✅ Alle ISOs auf {drive} aktuell.");

            // Bereits als veraltet/Duplikat gemeldete alte Dateinamen aus der Neuer-/Unbekannt-Erkennung
            // ausschließen, sonst würde dieselbe Datei doppelt gemeldet (einmal "veraltet", einmal "unbekannt").
            var handled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (_, oldFilename) in od)         handled.Add(oldFilename);
            foreach (var (_, oldFilename) in duplicates) handled.Add(oldFilename);

            var newerOnStick = DetectNewerVersionsOnStick(found);
            var newerFnSet   = new HashSet<string>(newerOnStick.Select(x => x.StickIso.Filename), StringComparer.OrdinalIgnoreCase);
            var dbFn         = new HashSet<string>(_db.Entries.Select(e => e.Filename), StringComparer.OrdinalIgnoreCase);
            var initialUnknowns = found.Where(f => !string.IsNullOrWhiteSpace(f.Filename)
                                                && !dbFn.Contains(f.Filename)
                                                && !newerFnSet.Contains(f.Filename)
                                                && !handled.Contains(f.Filename)).ToList();

            var additionalNewer = new List<(IsoEntry DbEntry, UsbService.StickIso StickIso)>();
            var trueUnknowns    = new List<UsbService.StickIso>();
            foreach (var stickIso in initialUnknowns)
            {
                var match = _db.Entries.Where(e => !string.IsNullOrWhiteSpace(e.Name))
                    .FirstOrDefault(e => DistroMatcher.IsLikelySameDistroByName(e.Name, stickIso.Filename) &&
                        (string.IsNullOrWhiteSpace(e.Filename) ||
                         DistroMatcher.IsVersionNewer(HttpService.ExtractVersion(stickIso.Filename), HttpService.ExtractVersion(e.Filename))));
                if (match != null) additionalNewer.Add((match, stickIso));
                else               trueUnknowns.Add(stickIso);
            }

            var allNewer = newerOnStick.Concat(additionalNewer).ToList();
            if (allNewer.Count > 0) NewerVersionsOnStickDetected?.Invoke(allNewer, drive);
            if (trueUnknowns.Count > 0) UnknownIsosOnStickDetected?.Invoke(trueUnknowns, drive);

            var odEntries = new HashSet<IsoEntry>(od.Select(x => x.Entry));
            var missing = GetVerifiedCompleteEntriesMissingFromStick().Where(e => !odEntries.Contains(e)).ToList();
            if (missing.Count > 0) MissingOnStickDetected?.Invoke(missing, drive);
        }

        private List<(IsoEntry DbEntry, UsbService.StickIso StickIso)> DetectNewerVersionsOnStick(List<UsbService.StickIso> found)
        {
            var result = new List<(IsoEntry, UsbService.StickIso)>();
            foreach (var e in _db.Entries.Where(e => e.UsbStatus == Core.Models.UsbStatus.Outdated))
            {
                var si = found.FirstOrDefault(f => DistroMatcher.IsSameDistroDifferentVersion(e.Filename, f.Filename));
                if (si is null) continue;
                if (DistroMatcher.IsVersionNewer(HttpService.ExtractVersion(si.Filename), HttpService.ExtractVersion(e.Filename)))
                    result.Add((e, si));
            }
            return result;
        }

        private void ApplyStickResults(List<UsbService.StickIso> found)
        {
            var byFn = new Dictionary<string, UsbService.StickIso>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in found) if (!byFn.ContainsKey(f.Filename)) byFn[f.Filename] = f;
            foreach (var e in _db.Entries)
            {
                if (!string.IsNullOrEmpty(e.Filename) && byFn.TryGetValue(e.Filename, out var exact))
                { e.UsbStatus = Core.Models.UsbStatus.Ok; e.UsbSize = FormatGb(exact.Size); continue; }
                var other = found.FirstOrDefault(f => DistroMatcher.IsSameDistroDifferentVersion(e.Filename, f.Filename));
                if (other != null) { e.UsbStatus = Core.Models.UsbStatus.Outdated; e.UsbSize = FormatGb(other.Size); }
                else               { e.UsbStatus = Core.Models.UsbStatus.Missing;  e.UsbSize = string.Empty; }
            }
        }

        /// <summary>
        /// Verdachtsfall 1 (siehe Spec): für Einträge mit versionslosem Dateinamen (z. B. Hiren's
        /// BootCD — der Dateiname ändert sich nie, RepresentsGenuineFilenameChange kann also NIE
        /// "veraltet" erkennen) ist ein Namensvergleich wirkungslos. Hier wird stattdessen die
        /// Stick-Kopie gegen den zuletzt lokal verifizierten Referenz-Hash geprüft — erkennt stille
        /// Beschädigung oder eine heimlich andere Datei unter demselben Namen. Sagt NICHTS darüber
        /// aus, ob online eine neuere Version existiert (das bleibt außerhalb des Scopes, siehe Spec
        /// Nicht-Ziele) — nur, ob die Stick-Datei der zuletzt bekannten guten Version entspricht.
        /// </summary>
        private async Task<List<UsbService.StickIso>> DetectVersionlessHashMismatchesAsync(List<UsbService.StickIso> found)
        {
            var mismatches = new List<UsbService.StickIso>();
            var byFn = new Dictionary<string, UsbService.StickIso>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in found) if (!byFn.ContainsKey(f.Filename)) byFn[f.Filename] = f;

            foreach (var e in _db.Entries)
            {
                if (string.IsNullOrEmpty(e.Sha256) || !DistroMatcher.HasVersionlessFilename(e.Filename)) continue;
                if (!byFn.TryGetValue(e.Filename, out var stick)) continue;
                string actual = await IsoEntry.ComputeSha256Async(stick.FullPath).ConfigureAwait(false);
                if (string.IsNullOrEmpty(actual)) continue;
                bool mismatch = !string.Equals(actual, e.Sha256, StringComparison.OrdinalIgnoreCase);
                e.HashMismatchDetected = mismatch;
                if (mismatch) mismatches.Add(stick);
            }
            return mismatches;
        }

        /// <summary>
        /// Manuelle, vollständige Variante von DetectVersionlessHashMismatchesAsync: prüft ALLE
        /// Einträge mit vorhandenem Referenz-Hash (nicht nur versionslose) — bewusst nur auf
        /// Anwender-Wunsch (Button), da das Hashen mehrerer GB-ISOs über USB spürbar dauert.
        /// </summary>
        public async Task VerifyStickIntegrityAsync()
        {
            if (string.IsNullOrEmpty(SelectedDriveLetter)) return;
            SetBusy(true); StatusText = "🔒 Prüfe Integrität …";
            RecordHistory($"🔒 Integritätsprüfung {SelectedDriveLetter} gestartet …"); Log($"🔒 Integritätsprüfung {SelectedDriveLetter} gestartet …");
            // BUGFIX: bislang kein CancellationToken verdrahtet — "Abbrechen" loggte "⛔ Abbruch.",
            // hatte aber keine Wirkung auf diese Schleife, da _activeWorker hier nie gesetzt wurde
            // und OnCancel() nur DownloadWorker/CopyToUsbWorker/UrlCheckWorker/UpdateScanWorker kennt.
            // Frischen Token setzen (analog StartDownload) und in der Hash-Schleife abfragen.
            _workerCts = new CancellationTokenSource(); _activeWorker = null;
            var ct = _workerCts.Token;
            // BUGFIX (finaler Review): try/finally schützt gegen ein hängenbleibendes Busy-UI, falls
            // ScanStickVerifiedAsync/ComputeSha256Async unerwartet wirft — vorher blieb IsBusy=true
            // für immer stehen (SetBusy(false) stand nur im Erfolgspfad ganz am Ende), und über den
            // "async void"-Klick-Handler (BtnVerifyIntegrity_Click) hätte eine Exception zusätzlich
            // die App zum Absturz gebracht; über den Fire-and-forget-Command-Pfad wäre sie sonst
            // still verschluckt worden. Stil analog zu StartVentoyInstall (catch + Log + StatusText).
            try
            {
                var (found, _) = await _usb.ScanStickVerifiedAsync(SelectedDriveLetter, _db.Entries).ConfigureAwait(false);
                var byFn = new Dictionary<string, UsbService.StickIso>(StringComparer.OrdinalIgnoreCase);
                foreach (var f in found) if (!byFn.ContainsKey(f.Filename)) byFn[f.Filename] = f;

                int totalToCheck = _db.Entries.Count(e => !string.IsNullOrEmpty(e.Sha256) && byFn.ContainsKey(e.Filename));
                var mismatches = new List<UsbService.StickIso>(); int checkedCount = 0;
                foreach (var e in _db.Entries)
                {
                    if (ct.IsCancellationRequested) break;
                    if (string.IsNullOrEmpty(e.Sha256) || !byFn.TryGetValue(e.Filename, out var stick)) continue;
                    checkedCount++;
                    // Live-Fortschritt für die "Aktueller Vorgang"-Karte im Status-Reiter: vorher
                    // erfuhr der Nutzer den Zwischenstand ("X von Y geprüft") erst nach Abschluss der
                    // GESAMTEN Prüfung — bei mehreren GB-ISOs über USB potenziell mehrere Minuten ohne
                    // jede Rückmeldung (siehe den ursprünglichen Abbruch-Bug dieser Funktion).
                    _ui.Invoke(() =>
                    {
                        CurrentOperationItem = e.Name;
                        CurrentOperationCounter = $"{checkedCount} von {totalToCheck} geprüft";
                        ProgressPercent = totalToCheck > 0 ? (checkedCount * 100) / totalToCheck : 0;
                    });
                    string actual = await IsoEntry.ComputeSha256Async(stick.FullPath, ct).ConfigureAwait(false);
                    if (ct.IsCancellationRequested || string.IsNullOrEmpty(actual)) continue;
                    bool mismatch = !string.Equals(actual, e.Sha256, StringComparison.OrdinalIgnoreCase);
                    e.HashMismatchDetected = mismatch;
                    if (mismatch) mismatches.Add(stick);
                }
                _ui.Invoke(() =>
                {
                    if (ct.IsCancellationRequested)
                    {
                        StatusText = "Abbruch …";
                        RecordHistory($"⛔ Integritätsprüfung {SelectedDriveLetter} abgebrochen ({checkedCount} geprüft).");
                        Log($"⛔ Integritätsprüfung {SelectedDriveLetter} abgebrochen ({checkedCount} geprüft).");
                        return;
                    }
                    StatusText = mismatches.Count > 0 ? $"⚠ {mismatches.Count} Hash-Abweichung(en)." : $"✅ {checkedCount} ISO(s) verifiziert.";
                    RecordHistory($"🔒 Integritätsprüfung {SelectedDriveLetter}: {checkedCount} geprüft, {mismatches.Count} Abweichung(en).");
                    Log($"🔒 Integritätsprüfung {SelectedDriveLetter}: {checkedCount} geprüft, {mismatches.Count} Abweichung(en).");
                    RefreshAllEntries(); // Hash-Status-Symbol (HashMismatchDetected) in der Liste aktualisieren
                    if (mismatches.Count > 0) IncompleteIsosOnStickDetected?.Invoke(mismatches, SelectedDriveLetter);
                });
            }
            catch (Exception ex)
            {
                _ui.Invoke(() => { Log($"❌ Integritätsprüfung fehlgeschlagen: {ex.Message}"); StatusText = "Fehler."; });
            }
            finally
            {
                _ui.Invoke(() => SetBusy(false));
            }
        }

        private static string FormatGb(long bytes) => $"{bytes / 1_073_741_824.0:F2} GB";

        public void TriggerVentoyMenuUpdate(string drive)
        {
            Log($"📋 Ventoy-Bootmenü auf {drive} wird aktualisiert …");
            string cap = drive; var entries = _db.Entries.ToList();
            _ = Task.Run(() => { UsbService.UpdateVentoyMenu(cap, entries); _ui.Invoke(() => Log($"✅ Ventoy-Bootmenü aktualisiert ({entries.Count} Einträge).")); });
        }

        public void TriggerAutoVersionCheck()
        {
            RecordHistory($"🌐 Online-Versionscheck gestartet — {_db.Count} Distros …"); Log($"🌐 Online-Versionscheck gestartet — {_db.Count} Distros …");
            StatusText = "🌐 Online-Versionscheck läuft …"; OnlineScanActive = true; OnlineScanPercent = 0; OnlineScanCurrentItem = "—";
            var worker = new AutoVersionCheckWorker(_db.Entries);
            worker.Progress += (c, t) => _ui.Invoke(() => OnlineScanPercent = t > 0 ? (c * 100) / t : 0);
            worker.EntryChecked += result => _ui.Invoke(() =>
            {
                OnlineScanCurrentItem = result.Name;
                if (!result.Resolved) { Log($"   ⚠ {result.Name}: nicht erreichbar."); return; }
                Log(result.HasUpdate
                    ? $"   🆕 {result.Name}: v{result.LocalVersion} → v{result.RemoteVersion}"
                    : $"   ✓ {result.Name}: v{result.RemoteVersion} (aktuell)");
                int idx = _db.Entries.ToList().FindIndex(e => e.Name == result.Name);
                if (idx >= 0) RefreshEntry(idx);
            });
            worker.Completed += (resolved, updates) =>
            {
                // BUGFIX: Ohne die RepresentsGenuineFilenameChange-Prüfung wurden Einträge, deren
                // Resolver IMMER denselben statischen Dateinamen liefert (z.B. Hiren's BootCD PE —
                // ResolveHirensAsync gibt konstant "HBCD_PE_x64.iso" zurück), bei JEDEM Versionscheck
                // erneut fälschlich als "auf dem Stick veraltet" gemeldet — der alte und der neue
                // Dateiname sind identisch, die Stick-Kopie IST bereits die aktuelle. Nur ein
                // Eintrag, dessen Dateiname sich durchs Update TATSÄCHLICH ändert, kann eine ältere
                // (unter dem alten Namen gefundene) Stick-Kopie wirklich veraltet machen.
                var oldFn = updates
                    .Where(i => !string.IsNullOrEmpty(_db.Entries[i].RemoteUrl) && !string.IsNullOrEmpty(_db.Entries[i].Filename)
                             && DistroMatcher.RepresentsGenuineFilenameChange(_db.Entries[i].Filename, _db.Entries[i].RemoteFilename))
                    .ToDictionary(i => _db.Entries[i].Filename, i => i, StringComparer.OrdinalIgnoreCase);
                _ui.Invoke(() =>
                {
                    foreach (int i in updates)
                    {
                        var e = _db.Entries[i]; if (string.IsNullOrEmpty(e.RemoteUrl)) continue;
                        string oldVer = HttpService.ExtractVersion(e.Filename);
                        if (string.IsNullOrEmpty(oldVer)) oldVer = HttpService.ExtractVersion(e.Name);
                        string newVer = string.IsNullOrEmpty(e.RemoteVersion)
                            ? HttpService.ExtractVersion(e.RemoteFilename) : e.RemoteVersion;
                        e.Url = e.RemoteUrl; e.Filename = e.RemoteFilename;
                        // Der Eintrag repräsentiert nach der Übernahme selbst die neueste Version →
                        // es gibt kein ausstehendes Update mehr. Ohne dieses Zurücksetzen bliebe das
                        // (vor der Übernahme korrekt gesetzte) Flag veraltet auf true und die
                        // "Aktuell"-Spalte zeigte fälschlich "Update vX" statt "Aktuell (vX)".
                        e.UpdateAvailable = false;
                        if (!string.IsNullOrEmpty(oldVer) && !string.IsNullOrEmpty(newVer) && oldVer != newVer)
                        {
                            int pos = e.Name.IndexOf(oldVer, StringComparison.Ordinal);
                            if (pos >= 0) { string on = e.Name; e.Name = e.Name[..pos] + newVer + e.Name[(pos + oldVer.Length)..]; Log($"   ✏ {on} → {e.Name}"); }
                        }
                    }
                    // BUGFIX: auch speichern, wenn KEIN Versions-Update vorliegt, aber für einen
                    // zuvor URL-losen Eintrag (Import/manuell hinzugefügt) gerade erstmals eine
                    // Quelle gefunden wurde — sonst geht die im Speicher gefundene URL beim nächsten
                    // Start wieder verloren und die aufwändige Auflösung muss komplett neu laufen.
                    if (updates.Count > 0) { _db.Save(); Log($"💾 Datenbank: {updates.Count} neue Version(en) gespeichert."); }
                    else if (worker.AnyUrlDiscovered) { _db.Save(); Log("💾 Datenbank: neu gefundene Download-Quelle(n) gespeichert."); }
                    // Nach dem In-place-Update können mehrere Einträge (z.B. zwei importierte
                    // KDE-neon-Varianten) auf dieselbe aktuelle ISO kollabiert und damit zu identischen
                    // Duplikaten geworden sein — sofort bereinigen, ohne Neustart abzuwarten. RebuildTree
                    // nur bei tatsächlicher Änderung, sonst genügt das RefreshAllEntries() unten.
                    if (DeduplicateEntries() > 0) RebuildTree();
                    // OnlineScanCurrentItem bleibt bewusst stehen (nicht auf "—" zurückgesetzt) —
                    // zeigt im Status-Reiter weiterhin, welcher Eintrag zuletzt geprüft wurde, auch
                    // nachdem der Scan fertig ist; wird erst beim Start des NÄCHSTEN Scans geleert.
                    OnlineScanActive = false; OnlineScanPercent = 100; RefreshAllEntries();
                    // Startphase beendet: ab jetzt darf ein Laufwerkswechsel/Neu-Einstecken wieder
                    // sofort scannen. Der Start-Stick-Scan selbst folgt unten (capturedDrive).
                    _startupPhase = false;
                    StatusText = updates.Count > 0 ? $"🆕 {updates.Count} aktualisiert."
                               : resolved > 0      ? $"✅ Alle {resolved} aktuell." : "⚠ Nicht erreichbar.";
                    RecordHistory($"🌐 Versionscheck: {StatusText}"); Log($"🌐 Versionscheck: {StatusText}"); AutoVersionCheckCompleted?.Invoke();
                });
                // BUGFIX: Der zu scannende Stick wurde bisher als "capturedDrive" VOR dem
                // Versionscheck erfasst — steckte der Anwender einen Stick erst WÄHREND des Checks
                // ein, blieb dieser Nachlauf-Scan auf dem alten (oft leeren) Stand hängen: der
                // reguläre TriggerUsbScan()-Aufruf über den SelectedDrive-Setter war durch
                // _startupPhase blockiert, UND hier wurde weiterhin der veraltete Laufwerksbuchstabe
                // von vor dem Check geprüft — der neu eingesteckte Stick wurde nie gescannt, bis er
                // ab- und wieder eingesteckt wurde. SelectedDrive selbst wird aber auch während
                // _startupPhase korrekt aktualisiert (nur der TriggerUsbScan()-Aufruf im Setter wird
                // unterdrückt) — ein frischer Blick auf SelectedDriveLetter GENAU JETZT (nach dem
                // Zurücksetzen von _startupPhase oben) liefert daher immer den tatsächlich
                // aktuellen Stick, egal ob er schon vor dem Check da war oder währenddessen dazukam.
                string driveToScan = SelectedDriveLetter;
                if (!string.IsNullOrEmpty(driveToScan))
                    _ = Task.Run(async () =>
                    {
                        _ui.Invoke(() => { UsbScanActive = true; UsbScanPercent = 0; Log($"💾 Prüfe Stick {driveToScan} …"); });
                        var (si, incomplete) = await _usb.ScanStickVerifiedAsync(driveToScan, _db.Entries).ConfigureAwait(false);
                        _ui.Invoke(() =>
                        {
                            UsbScanActive = false; UsbScanPercent = 100;
                            RecordHistory($"💾 Stick-Prüfung {driveToScan} abgeschlossen ({si.Count} ISO(s) erkannt).");
                            // Start-Scan HAT Versionscheck-Kontext → oldFn aus den Update-Ergebnissen (oben berechnet).
                            ProcessStickScanResults(si, incomplete, oldFn, driveToScan);
                        });
                    });
            };
            _ = worker.RunAsync();
        }

        private void OnDownload() { }

        public async void StartDownload(List<IsoEntry> queue, string drive, bool copyAfter, bool deleteAfter, int slots)
        {
            if (queue.Count == 0) return;
            SetBusy(true);
            Log($"⬇ Download gestartet: {queue.Count} ISO(s), {slots} parallel" + (string.IsNullOrEmpty(drive) ? "" : $" → {drive}"));
            foreach (var e in queue) Log($"   • {e.Name}");
            var worker = new DownloadWorker(queue, slots, _paths.DownloadDir, _db, drive, copyAfter, deleteAfter);
            _workerCts = new CancellationTokenSource(); _activeWorker = worker; ProgressPercent = 0;
            worker.LogMessage += msg => _ui.Invoke(() => Log(msg));
            // _ui.Invoke blockiert synchron bis zur Anwender-Antwort — das betrifft nur DIESEN
            // Download-Slot (läuft in seinem eigenen Hintergrund-Task), die anderen parallelen
            // Slots laufen unbeeinflusst weiter.
            worker.ConfirmSlowDownloadAnyway = (name, host) => _ui.Invoke(() => ConfirmSlowDownload?.Invoke(name, host) ?? false);

            // Erfolgreich fertige Distros automatisch abwählen (Häkchen entfernen), damit sie beim
            // nächsten Start-Klick nicht versehentlich erneut heruntergeladen werden. Im Pipeline-
            // Modus (Download → Stick-Kopie) geschieht das erst NACH erfolgreicher Kopie (siehe
            // RunPipelineCopyConsumerAsync); im reinen Download-Modus schon nach dem Download.
            bool usePipeline = copyAfter && !string.IsNullOrEmpty(drive);
            if (!usePipeline)
                worker.ItemCompleted += (entry, success) =>
                {
                    if (success) _ui.Invoke(() => { entry.IsSelected = false; RefreshEntry(GetEntryIndex(entry.Name)); });
                };

            Channel<IsoEntry>? pipelineChannel = null; Task<(int Ok, int Failed)>? pipelineTask = null;
            if (usePipeline)
            {
                pipelineChannel = Channel.CreateUnbounded<IsoEntry>(new UnboundedChannelOptions { SingleReader = true });
                var channelReader = pipelineChannel.Reader; var capDrive = drive;
                pipelineTask = Task.Run(() => RunPipelineCopyConsumerAsync(channelReader, capDrive));
                worker.ItemCompleted += (entry, success) =>
                {
                    if (success && entry.IsLocallyAvailable(_paths.DownloadDir))
                    {
                        pipelineChannel.Writer.TryWrite(entry);
                        _ui.Invoke(() => { DownloadItemProgress?.Invoke(entry.Name, 100, "⏳ Warte auf Kopierslot …", false); Log($"   ↪ {entry.Name} → Kopier-Warteschlange."); });
                    }
                };
            }
            worker.OverallProgress += (pct, detail) => _ui.Invoke(() => { ProgressPercent = pct; StatusText = $"⬇ {detail}"; });
            worker.SlotUpdated += p => _ui.Invoke(() => { RefreshEntry(GetEntryIndex(p.IsoName)); DownloadItemProgress?.Invoke(p.IsoName, p.Percent, p.Status, p.CanRequestFasterMirror); });
            worker.Completed += (ok, failed, _) => _ui.Invoke(() =>
            {
                _db.Save(); Log($"⬇ Downloads abgeschlossen: {ok} OK, {failed} fehlgeschlagen.");
                if (pipelineChannel != null && pipelineTask != null)
                {
                    // BUGFIX: DownloadBatchCompleted früher HIER schon ausgelöst — der Download-
                    // Fortschrittsdialog (SetOverallComplete) sprang dadurch fälschlich auf "100% /
                    // ✅ erfolgreich", obwohl die Stick-Kopie erst jetzt beginnt und noch länger
                    // dauern kann. Feuert jetzt erst unten im ContinueWith, wenn die Kopie WIRKLICH
                    // fertig ist — vorher zeigt UpdateCopy/RecomputeOverall den echten Zwischenstand.
                    pipelineChannel.Writer.Complete();
                    StatusText = ok > 0 ? $"⬇ {ok} Downloads fertig — Stick-Kopie läuft …" : "⬇ 0 Downloads …";
                    if (ok > 0) Log("⬇ Downloads fertig. Pipeline-Kopiervorgang läuft weiter …");
                    var capDrive = drive; int totalQueued = queue.Count;
                    pipelineTask.ContinueWith(t => _ui.Invoke(() =>
                    {
                        // BUGFIX (Review): t.Result wirft, wenn RunPipelineCopyConsumerAsync mit einer
                        // unbehandelten Exception endet (z.B. Stick währenddessen abgezogen). Diese
                        // Continuation wird nirgends awaited — ein ungeschütztes t.Result würde die
                        // Exception hier lautlos verschlucken UND verhindern, dass SetBusy(false)/
                        // RefreshAllEntries/DownloadBatchCompleted überhaupt laufen — die UI bliebe
                        // dauerhaft im Busy-Zustand hängen, ohne jede Fehlermeldung.
                        var (copyOk, _) = t.IsFaulted ? (0, 0) : t.Result;
                        if (t.IsFaulted)
                            Log($"❌ Stick-Kopie abgebrochen: {t.Exception?.GetBaseException().Message}");
                        int totalFailed = totalQueued - copyOk;
                        SetBusy(false); RefreshAllEntries(); ProgressPercent = 100;
                        TriggerVentoyMenuUpdate(capDrive); TriggerUsbScan();
                        // BUGFIX: bisher wurden hier die DOWNLOAD-Erfolgszahlen (okC/failedC) gemeldet —
                        // eine komplett fehlgeschlagene Stick-Kopie nach erfolgreichem Download zeigte
                        // trotzdem "X ISO(s) heruntergeladen und kopiert." Jetzt zählen die ECHTEN
                        // Kopier-Ergebnisse (siehe BuildPipelineCompletionMessage/RunPipelineCopyConsumerAsync);
                        // totalFailed erfasst JEDEN nicht vollständig erfolgreichen Eintrag, egal ob der
                        // Download oder erst die anschließende Stick-Kopie fehlschlug.
                        DownloadBatchCompleted?.Invoke(copyOk, totalFailed, 0);
                        string msg = BuildPipelineCompletionMessage(copyOk, totalQueued, capDrive);
                        if (copyOk > 0)
                        {
                            StatusText = $"✅ {copyOk} heruntergeladen und auf {capDrive} kopiert.";
                            Log(StatusText); OperationSucceeded?.Invoke(msg);
                        }
                        else { StatusText = totalFailed > 0 ? $"❌ {totalFailed} ISO(s) fehlgeschlagen (Download oder Stick-Kopie)." : "⬇ Keine Downloads."; Log(StatusText); }
                    }));
                }
                else if (!string.IsNullOrEmpty(drive) && copyAfter && ok > 0)
                { DownloadBatchCompleted?.Invoke(ok, failed, 0); SetBusy(false); StartCopyToStick(queue, drive, deleteAfter); }
                else
                {
                    DownloadBatchCompleted?.Invoke(ok, failed, 0);
                    SetBusy(false); RefreshAllEntries();
                    StatusText = $"{ok}/{queue.Count} heruntergeladen" + (failed > 0 ? $", {failed} fehlgeschlagen" : "");
                    ProgressPercent = 100;
                    if (!string.IsNullOrEmpty(drive)) TriggerUsbScan();
                    if (ok > 0)
                    {
                        string msg = $"{ok} ISO(s) erfolgreich heruntergeladen.\n\nGespeichert unter:\n{_paths.DownloadDir}";
                        if (failed > 0) msg += $"\n\n⚠ {failed} fehlgeschlagen.";
                        OperationSucceeded?.Invoke(msg);
                    }
                }
            });
            await worker.RunAsync();
        }

        /// <summary>
        /// Baut die Abschluss-Meldung des kombinierten "Download → Stick-Kopie"-Modus AUS DEN ECHTEN
        /// Kopier-Erfolgszahlen, nicht aus den Download-Erfolgszahlen. BUGFIX: bisher meldete die App
        /// z.B. "8 ISO(s) heruntergeladen und kopiert.", obwohl nur der Download erfolgreich war und
        /// die anschließende Stick-Kopie komplett fehlschlug (RunPipelineCopyConsumerAsync zählte
        /// Kopier-Fehlschläge bis dahin gar nicht mit). Leerer String = kein einziger Erfolg — der
        /// Aufrufer zeigt dann einen Fehlschlag-Status statt einer Erfolgs-Box.
        /// </summary>
        internal static string BuildPipelineCompletionMessage(int copyOk, int totalQueued, string drive)
        {
            if (copyOk <= 0) return string.Empty;
            int failed = totalQueued - copyOk;
            string msg = $"{copyOk} ISO(s) heruntergeladen und auf {drive} kopiert.\n\n" +
                         "Jede ISO wurde direkt nach dem Download kopiert und lokal gelöscht.\n" +
                         "Das Ventoy-Bootmenü wurde automatisch aktualisiert.";
            if (failed > 0) msg += $"\n\n⚠ {failed} ISO(s) fehlgeschlagen.";
            return msg;
        }

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

        /// <summary>
        /// Reine Formatierungslogik für "zuletzt abgeschlossen" im Status-Reiter — dasselbe
        /// lastCheckUtc wie FormatNextAutoCheckText, nur als lokale Uhrzeit statt Restdauer.
        /// </summary>
        internal static string FormatLastAutoCheckText(DateTime? lastCheckUtc)
        {
            if (lastCheckUtc is null) return "noch nie";
            return lastCheckUtc.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm", System.Globalization.CultureInfo.CurrentCulture);
        }

        internal static string FormatHistoryEntry(string message, DateTime now) => $"[{now:HH:mm:ss}] {message}";

        private async Task<(int Ok, int Failed)> RunPipelineCopyConsumerAsync(ChannelReader<IsoEntry> reader, string drive)
        {
            const int bufSize = 4 * 1024 * 1024;
            byte[] buf = new byte[bufSize];
            int copyOkCount = 0, copyFailedCount = 0;
            await foreach (var entry in reader.ReadAllAsync().ConfigureAwait(false))
            {
                string? srcPath = entry.FindLocalPath(_paths.DownloadDir);
                // BUGFIX (Review): diese frühen Fehlschläge zählten bisher nur intern mit
                // (copyFailedCount++), meldeten dem Fortschritts-Dialog aber nie CopyItemProgress —
                // die Zeile blieb dadurch dauerhaft bei "⏳ Warte auf Kopierslot …" hängen, obwohl die
                // Abschluss-Meldung den Eintrag schon korrekt als fehlgeschlagen zählte.
                if (srcPath is null || !File.Exists(srcPath))
                {
                    _ui.Invoke(() => { CopyItemProgress?.Invoke(entry.Name, 0, "⚠ Quelldatei nicht gefunden"); Log($"   ⚠ {entry.Name}: Quelldatei nicht gefunden."); });
                    copyFailedCount++; continue;
                }
                long fileSize = IsoEntry.GetRobustLength(srcPath);
                if (fileSize < Constants.MinIsoSizeBytes)
                {
                    _ui.Invoke(() => { CopyItemProgress?.Invoke(entry.Name, 0, "⚠ Datei zu klein"); Log($"   ⚠ {entry.Name}: zu klein ({fileSize / 1_048_576} MB)."); });
                    copyFailedCount++; continue;
                }
                string targetDir = Path.Combine(UsbService.DriveRoot(drive), entry.NormalizedCategory);
                Directory.CreateDirectory(targetDir);
                string targetPath = Path.Combine(targetDir, entry.Filename);
                string entryName = entry.Name;

                // Freispeicher-Check vor dem Kopieren dieser einzelnen Datei — im Pipeline-Modus
                // lohnt sich pro Datei zu prüfen statt vorab für die gesamte Warteschlange, da
                // Downloads laufend eintreffen und der Speicherplatz sich zwischen ihnen ändert.
                try
                {
                    var drv = new DriveInfo(Path.GetPathRoot(targetDir) ?? UsbService.DriveRoot(drive));
                    if (drv.IsReady && drv.AvailableFreeSpace < fileSize)
                    {
                        _ui.Invoke(() =>
                        {
                            CopyItemProgress?.Invoke(entryName, 0, "❌ Nicht genug Speicherplatz");
                            Log($"   ❌ {entryName}: nicht genug Speicherplatz auf {drive} (benötigt {fileSize / 1_073_741_824.0:F2} GB, frei {drv.AvailableFreeSpace / 1_073_741_824.0:F2} GB).");
                        });
                        copyFailedCount++; continue;
                    }
                }
                catch { /* Freispeicher-Check ist best-effort */ }

                long copied = 0L; var sw = Stopwatch.StartNew(); long lastMark = 0L; double lastEl = 0.0;
                _ui.Invoke(() =>
                {
                    CopyItemProgress?.Invoke(entryName, 0, "Kopiere auf Stick …");
                    Log($"📋 Kopiere auf Stick: {entryName}  ({fileSize / 1_073_741_824.0:F2} GB)");
                });
                bool copyOk = false;
                try
                {
                    using var src  = new FileStream(srcPath,    FileMode.Open,   FileAccess.Read,  FileShare.Read,  bufSize, FileOptions.SequentialScan | FileOptions.Asynchronous);
                    using var dest = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None,  bufSize, FileOptions.Asynchronous);
                    int read;
                    while ((read = await src.ReadAsync(buf).ConfigureAwait(false)) > 0)
                    {
                        await dest.WriteAsync(buf.AsMemory(0, read)).ConfigureAwait(false);
                        copied += read;
                        double el = sw.Elapsed.TotalSeconds;
                        if (el - lastEl >= 0.4 || copied >= fileSize)
                        {
                            double bps = (copied - lastMark) / Math.Max(0.001, el - lastEl);
                            lastMark = copied; lastEl = el;
                            int    pct = fileSize > 0 ? (int)((copied * 100L) / fileSize) : 0;
                            string det = BuildTransferDetail(bps, copied, fileSize);
                            string nm  = entryName;
                            _ui.Invoke(() => CopyItemProgress?.Invoke(nm, pct, det));
                        }
                    }
                    await dest.FlushAsync().ConfigureAwait(false);
                    long ws = IsoEntry.GetRobustLength(targetPath);
                    copyOk = ws == fileSize;
                    if (!copyOk)
                    {
                        IsoEntry.TryDelete(targetPath);
                        string nm = entryName;
                        _ui.Invoke(() =>
                        {
                            CopyItemProgress?.Invoke(nm, 0, "⚠ Größenprüfung fehlgeschlagen");
                            Log($"   ⚠ {nm}: {ws}/{fileSize} Bytes — entfernt.");
                        });
                    }
                }
                catch (Exception ex)
                {
                    IsoEntry.TryDelete(targetPath);
                    string nm = entryName;
                    _ui.Invoke(() =>
                    {
                        CopyItemProgress?.Invoke(nm, 0, $"Fehler: {ex.Message}");
                        Log($"   ✗ {nm}: {ex.Message}");
                    });
                    copyFailedCount++; continue;
                }
                if (!copyOk) { copyFailedCount++; continue; }
                copyOkCount++;
                long sz = fileSize;
                entry.UsbStatus = Core.Models.UsbStatus.Ok;
                entry.UsbSize   = FormatGb(sz);
                entry.VerifiedComplete = false;
                // Erfolgreich heruntergeladen UND kopiert → Häkchen entfernen (siehe usePipeline-
                // Kommentar in StartDownload).
                entry.IsSelected = false;
                bool localDeleted = IsoEntry.TryDelete(srcPath, msg => _ui.Invoke(() => Log(msg)));
                string fn  = entryName;
                int    idx = GetEntryIndex(fn);
                _ui.Invoke(() =>
                {
                    RefreshEntry(idx);
                    CopyItemProgress?.Invoke(fn, 100,
                        $"✅ Auf Stick · {(localDeleted ? "lokal gelöscht" : "lokal NICHT löschbar")} ({sz / 1_073_741_824.0:F2} GB)");
                    // FIX: ':' statt ',' im ternären Operator
                    Log($"   ✅ {fn}: {sz / 1_073_741_824.0:F2} GB kopiert" +
                        (localDeleted ? ", lokal gelöscht." : "."));
                });
            }
            return (copyOkCount, copyFailedCount);
        }

        private static string BuildTransferDetail(double bps, long done, long total)
        {
            static string FmtB(double b) { string[] u = { "B", "KB", "MB", "GB" }; int i = 0; while (b >= 1024 && i < 3) { b /= 1024; i++; } return i == 0 ? $"{(long)b} B" : $"{b:F1} {u[i]}"; }
            static string FmtEta(double s) { if (s < 1) return "<1s"; var ts = TimeSpan.FromSeconds(s); return ts.TotalHours >= 1 ? $"{(int)ts.TotalHours}h {ts.Minutes}m" : ts.TotalMinutes >= 1 ? $"{(int)ts.TotalMinutes}m {ts.Seconds}s" : $"{ts.Seconds}s"; }
            string speed = FmtB(bps) + "/s";
            if (total <= 0) return $"{speed}  ·  {FmtB(done)}";
            return $"{speed}  ·  noch {FmtEta((total - done) / Math.Max(0.001, bps))}  ·  {FmtB(done)} / {FmtB(total)}";
        }

        public void StartCopyToStick(List<IsoEntry> queue, string drive, bool deleteAfter)
        {
            var toCopy = queue.Where(e => e.IsLocallyAvailable(_paths.DownloadDir)).ToList();
            if (toCopy.Count == 0) { TriggerUsbScan(); return; }
            SetBusy(true);
            Log($"📋 Kopiervorgang auf {drive}: {toCopy.Count} ISO(s)" + (deleteAfter ? " (danach lokal löschen)" : ""));
            foreach (var e in toCopy) Log($"   → {e.Name}  ({e.Filename})");
            var worker = new CopyToUsbWorker(toCopy, drive, false, _paths.DownloadDir); _activeWorker = worker;
            worker.FileProgress += (name, pct, detail) => _ui.Invoke(() => CopyItemProgress?.Invoke(name, pct, detail));
            worker.Progress     += (pct, detail)       => _ui.Invoke(() => { ProgressPercent = pct; StatusText = detail; });
            worker.Completed    += (ok, count, bytes, message) => _ui.Invoke(() =>
            {
                SetBusy(false); RefreshAllEntries();
                // BUGFIX: 'ok' und 'message' wurden bisher verworfen (Discard "_") — ein
                // Abbruchgrund (z.B. der neue Freispeicher-Check unten) wäre nie im Protokoll
                // sichtbar gewesen, stattdessen fälschlich "0 ISO(s) kopiert" ohne Erklärung.
                if (!ok && !string.IsNullOrWhiteSpace(message))
                {
                    Log($"❌ Kopiervorgang abgebrochen: {message}");
                    StatusText = "❌ " + message; ProgressPercent = 0;
                    ShowMessageBox?.Invoke(message, true);
                    return;
                }
                Log($"📋 Kopiervorgang fertig: {count} ISO(s), {bytes / (1024.0 * 1024 * 1024):F2} GB auf {drive}.");
                StatusText = count > 0 ? $"{count} ISO(s) auf {drive} kopiert." : "Nichts zu kopieren.";
                ProgressPercent = 100; CopyBatchCompleted?.Invoke(count);
                if (deleteAfter && count > 0)
                {
                    int del = 0;
                    foreach (var e in toCopy)
                    {
                        string? p = e.FindLocalPath(_paths.DownloadDir);
                        if (p != null && IsoEntry.TryDelete(p, msg => Log(msg))) { del++; Log($"   🗑 Gelöscht: {e.Filename}"); }
                    }
                    if (del > 0) { Log($"   🗑 {del} ISO(s) lokal gelöscht."); RefreshAllEntries(); }
                }
                if (count > 0) TriggerVentoyMenuUpdate(drive);
                TriggerUsbScan();
                if (count > 0)
                {
                    string msg = $"{count} ISO(s) auf {drive} kopiert ({bytes / (1024.0 * 1024 * 1024):F2} GB).\n\n" +
                                 "Das Ventoy-Bootmenü wurde automatisch aktualisiert.";
                    if (deleteAfter) msg += "\n\nDie lokalen ISO-Dateien wurden gelöscht.";
                    OperationSucceeded?.Invoke(msg);
                }
            });
            _ = worker.RunAsync();
        }

        private void OnCopyToUsb()
        { if (string.IsNullOrEmpty(SelectedDriveLetter)) return; var q = GetLocallyAvailableEntries(); if (q.Count == 0) return; StartCopyToStick(q, SelectedDriveLetter, false); }

        private void OnCheckUpdates()
        {
            SetBusy(true); StatusText = "Prüfe auf Updates …"; ProgressPercent = 0; Log("🔄 Manueller Update-Check …");
            var worker = new UpdateScanWorker(_db.Entries, _paths.DownloadDir); _activeWorker = worker;
            worker.EntryChecked += result => _ui.Invoke(() =>
            {
                if (!result.Resolved) return;
                Log(result.HasUpdate
                    ? $"   🆕 {result.Name}: v{result.LocalVersion} → v{result.RemoteVersion}"
                    : $"   ✓ {result.Name}: v{result.RemoteVersion}");
                int idx = _db.Entries.ToList().FindIndex(e => e.Name == result.Name);
                if (idx >= 0) RefreshEntry(idx);
            });
            worker.Completed += (resolved, updates) => _ui.Invoke(() =>
            {
                // BUGFIX: siehe TriggerAutoVersionCheck — auch ohne echtes Update speichern, wenn
                // eine zuvor fehlende Download-Quelle neu gefunden wurde.
                SetBusy(false); if (updates.Count > 0 || worker.AnyUrlDiscovered) _db.Save(); RefreshAllEntries();
                StatusText = updates.Count > 0 ? $"🆕 {updates.Count} Update(s)."
                           : resolved > 0      ? "Alles aktuell." : "Keine lokalen ISOs.";
                ProgressPercent = 100; Log($"🔄 {StatusText}");
            });
            _ = worker.RunAsync();
        }

        private void OnCheckUrls()
        {
            SetBusy(true); StatusText = "Prüfe URLs …"; ProgressPercent = 0; Log("🌐 URL-Check …");
            var worker = new UrlCheckWorker(_db.Entries); _activeWorker = worker;
            worker.EntryChecked += (i, ok) => _ui.Invoke(() =>
            {
                if (i >= 0 && i < _db.Entries.Count)
                    Log($"   {(ok ? "✓" : "✗")} {_db.Entries[i].Name}");
                RefreshEntry(i);
            });
            worker.Completed += (_, _) => _ui.Invoke(() =>
            {
                SetBusy(false); RefreshAllEntries();
                int ok  = _db.Entries.Count(e => e.UrlOk);
                int nok = _db.Entries.Count(e => e.UrlChecked && !e.UrlOk);
                StatusText = $"URL-Check fertig — ✓ {ok} erreichbar, ✗ {nok} nicht erreichbar.";
                ProgressPercent = 100; Log($"🌐 {StatusText}");
            });
            _ = worker.RunAsync();
        }

        /// <summary>
        /// Löst für ALLE DB-Einträge (auch stick-importierte, unabhängig von lokaler Verfügbarkeit)
        /// die aktuelle Download-URL auf und meldet einen vollständigen Erreichbarkeits-Bericht —
        /// macht sonst im Protokoll versteckte Ausfälle sofort sichtbar.
        /// </summary>
        private void OnHealthCheck() => RunHealthCheck();

        /// <summary>
        /// Läuft nach jeder Download- oder Scan-Funktion automatisch (siehe TriggerUsbScan/StartDownload)
        /// UND manuell über HealthCheckCommand. Bereinigt zuerst Duplikate, damit nicht doppelt geprüft
        /// wird, und zeigt den Fortschritt genau wie der Online-Scan über einen eigenen Active/Percent-
        /// Status an — nicht über das generische IsBusy, damit die App währenddessen bedienbar bleibt.
        /// </summary>
        public void RunHealthCheck()
        {
            if (IsBusy || HealthCheckActive) return;
            DeduplicateEntries();
            HealthCheckActive = true; HealthCheckPercent = 0;
            Log($"🩺 DB-Gesundheitscheck gestartet — {_db.Count} Distros …");
            var results = new List<VersionCheckEntryResult>();
            var worker  = new UpdateScanWorker(_db.Entries, _paths.DownloadDir, checkAllEntries: true);
            worker.Progress     += (c, t) => _ui.Invoke(() => HealthCheckPercent = t > 0 ? (c * 100) / t : 0);
            worker.EntryChecked += result => _ui.Invoke(() =>
            {
                results.Add(result);
                Log(result.Resolved ? $"   ✓ {result.Name}: v{result.RemoteVersion}" : $"   ❌ {result.Name}: nicht erreichbar.");
            });
            worker.Completed += (resolved, updates) => _ui.Invoke(() =>
            {
                // BUGFIX: siehe TriggerAutoVersionCheck — genau hier lief der Gesundheitscheck bei
                // frisch importierten Distros ohne URL zwar erfolgreich auf, ohne dass die
                // gefundene Quelle je gespeichert wurde (kein "Update" im Sinne der bisherigen
                // Bedingung, da Version = Version). Der nächste Start musste die komplette,
                // fehleranfällige Auflösungskette (Websuche/DistroWatch/SourceForge) jedes Mal neu
                // durchlaufen, statt die einmal gefundene URL wiederzuverwenden.
                if (updates.Count > 0 || worker.AnyUrlDiscovered) _db.Save(); RefreshAllEntries();
                int failed = results.Count(r => !r.Resolved);
                HealthCheckActive = false; HealthCheckPercent = 100;
                StatusText = failed == 0 ? $"🩺 Alle {results.Count} Distros online erreichbar." : $"🩺 {failed}/{results.Count} nicht erreichbar.";
                Log($"🩺 {StatusText}");
                HealthCheckCompleted?.Invoke(results);
            });
            _ = worker.RunAsync();
        }

        private void OnVentoy() { }

        public async void StartVentoyInstall(bool updateMode)
        {
            if (string.IsNullOrEmpty(SelectedDriveLetter)) return;
            SetBusy(true); string letter = SelectedDriveLetter;
            Log($"⚡ Ventoy-{(updateMode ? "Aktualisierung" : "Installation")} auf {letter}");
            Log("   Startet als Administrator — bitte UAC bestätigen."); StatusText = "Warte auf UAC-Bestätigung …";
            string exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            if (string.IsNullOrEmpty(exePath)) { Log("❌ EXE-Pfad nicht ermittelbar."); _ui.Invoke(() => { SetBusy(false); StatusText = "Fehler."; }); return; }
            string args = $"--ventoy-install {letter} {updateMode.ToString().ToLowerInvariant()} {SecureBoot.ToString().ToLowerInvariant()}";
            var psi = new ProcessStartInfo(exePath, args) { UseShellExecute = true, Verb = "runas" };
            try
            {
                Process? proc = Process.Start(psi);
                if (proc is null) { Log("❌ Admin-Prozess konnte nicht gestartet werden."); _ui.Invoke(() => { SetBusy(false); StatusText = "Fehler."; }); return; }
                Log("   Admin-Prozess läuft …"); StatusText = "Ventoy-Installation läuft …"; ProgressPercent = 50;
                await Task.Run(() => proc.WaitForExit()).ConfigureAwait(false);
                bool success = proc.ExitCode == 0;
                _ui.Invoke(() =>
                {
                    SetBusy(false); ProgressPercent = success ? 100 : 0; OnPropertyChanged(nameof(DriveInfoText));
                    StatusText = success ? $"✅ Ventoy {(updateMode ? "aktualisiert" : "installiert")}." : "❌ Ventoy fehlgeschlagen.";
                    Log($"⚡ {StatusText}  (ExitCode: {proc.ExitCode})");
                    if (success) TriggerUsbScan();
                    else ShowMessageBox?.Invoke("Ventoy-Installation fehlgeschlagen.\nDetails im Ventoy-Installationsfenster.", true);
                });
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            { _ui.Invoke(() => { SetBusy(false); Log("⚠ UAC abgelehnt — abgebrochen."); StatusText = "Abgebrochen."; }); }
            catch (Exception ex)
            { _ui.Invoke(() => { SetBusy(false); Log($"❌ {ex.Message}"); StatusText = "Fehler."; ShowMessageBox?.Invoke($"Fehler: {ex.Message}", true); }); }
        }

        private void OnCancel()
        {
            _workerCts.Cancel();
            if (_activeWorker is DownloadWorker   dw) dw.Cancel();
            if (_activeWorker is CopyToUsbWorker  cw) cw.Cancel();
            if (_activeWorker is UrlCheckWorker   uw) uw.Cancel();
            if (_activeWorker is UpdateScanWorker us) us.Cancel();
            Log("⛔ Abbruch."); StatusText = "Abbruch …"; ProgressPercent = 0;
        }

        // Reicht den Klick auf "(schneller)" im Fortschrittsfenster an den gerade aktiven
        // DownloadWorker weiter (siehe DownloadWorker.RequestFasterMirror). Kein Effekt, wenn gerade
        // kein Download läuft oder der Button für diesen Eintrag bereits ausgeblendet wurde.
        public void RequestFasterMirror(string entryName)
        {
            if (_activeWorker is DownloadWorker dw) dw.RequestFasterMirror(entryName);
        }

        private void SetBusy(bool busy)
        {
            IsBusy = busy;
            if (busy)
            {
                ProgressPercent = 0;
                CurrentOperationItem = "—"; CurrentOperationDetail = "—"; CurrentOperationCounter = "—";
            }
        }

        private void RefreshEntry(int index)
        {
            if (index < 0 || index >= _db.Entries.Count) return;
            var entry = _db.Entries[index];
            foreach (var cat in Categories)
            { var vm = cat.Entries.FirstOrDefault(e => e.Model == entry); vm?.Refresh(); }
        }

        private int GetEntryIndex(string n) => _db.Entries.ToList().FindIndex(e => e.Name == n || e.Filename == n);
        private void Log(string msg) => LogMessage?.Invoke(msg);

        public void RefreshScheduleStatus()
        {
            string raw = IniService.Read(_paths.SettingsIni, "App", "LastAutoCheckUtc", string.Empty);
            DateTime? last = DateTime.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsed) ? parsed : null;
            NextAutoCheckText = FormatNextAutoCheckText(last, Constants.AutoCheckIntervalDays, DateTime.UtcNow);
            LastAutoCheckText = FormatLastAutoCheckText(last);
        }

        private void RecordHistory(string msg)
        {
            ActivityHistory.Insert(0, FormatHistoryEntry(msg, DateTime.Now));
            while (ActivityHistory.Count > MaxActivityHistoryEntries) ActivityHistory.RemoveAt(ActivityHistory.Count - 1);
        }

        public void SaveAndClose()
        {
            Log("▶ Anwendung wird beendet.");
            IniService.Write(_paths.SettingsIni, "App", "ExpertMode", _expertMode ? "1" : "0");
            IniService.Write(_paths.SettingsIni, "App", "SecureBoot", _secureBoot ? "1" : "0");
            _db.SaveFilenames();
        }
    }
}
