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
        private readonly IsoDatabaseService _db    = IsoDatabaseService.Instance;
        private readonly HttpService        _http  = HttpService.Instance;
        private readonly UsbService         _usb   = UsbService.Instance;
        private readonly AppPaths           _paths = AppPaths.Instance;
        private readonly Dispatcher         _ui;
        private CancellationTokenSource _workerCts = new();
        private object?  _activeWorker;
        private string   _lastDriveSignature = string.Empty;
        private bool     _expertMode;

        private static readonly string[] _platformCodenames =
        {
            "noble", "jammy", "focal", "bionic", "oracular", "mantic", "lunar", "kinetic", "plucky",
            "trixie", "bookworm", "bullseye", "buster", "sid", "testing"
        };
        private static readonly HashSet<string> _genericDistroWords =
            new(StringComparer.OrdinalIgnoreCase)
            { "linux", "os", "live", "desktop", "server", "workstation", "official" };

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
        private bool   _onlineScanActive;  public bool OnlineScanActive  { get => _onlineScanActive;  private set => SetField(ref _onlineScanActive,  value); }
        private int    _onlineScanPercent; public int  OnlineScanPercent { get => _onlineScanPercent; private set => SetField(ref _onlineScanPercent, value); }
        private bool   _usbScanActive;    public bool UsbScanActive     { get => _usbScanActive;     private set => SetField(ref _usbScanActive,     value); }
        private int    _usbScanPercent;   public int  UsbScanPercent    { get => _usbScanPercent;    private set => SetField(ref _usbScanPercent,    value); }
        private bool   _healthCheckActive;  public bool HealthCheckActive  { get => _healthCheckActive;  private set => SetField(ref _healthCheckActive,  value); }
        private int    _healthCheckPercent; public int  HealthCheckPercent { get => _healthCheckPercent; private set => SetField(ref _healthCheckPercent, value); }

        public bool ExpertMode { get => _expertMode; set { if (SetField(ref _expertMode, value)) IniService.Write(_paths.SettingsIni, "App", "ExpertMode", value ? "1" : "0"); } }
        private bool _secureBoot = true;
        public  bool SecureBoot { get => _secureBoot; set { if (SetField(ref _secureBoot, value)) IniService.Write(_paths.SettingsIni, "App", "SecureBoot", value ? "1" : "0"); } }
        private bool _showInfoPopup = true;
        public  bool ShowInfoPopup { get => _showInfoPopup; set => SetField(ref _showInfoPopup, value); }

        public RelayCommand DownloadCommand      { get; }
        public RelayCommand CopyToUsbCommand     { get; }
        public RelayCommand CheckUpdatesCommand  { get; }
        public RelayCommand CheckUrlsCommand     { get; }
        public RelayCommand HealthCheckCommand   { get; }
        public RelayCommand VentoyCommand        { get; }
        public RelayCommand CancelCommand        { get; }
        public RelayCommand RefreshDrivesCommand { get; }

        public event Action<string>?       LogMessage;
        public event Action<string, bool>? ShowMessageBox;
        public event Action<List<IsoEntry>, string>?  StickUpdateAvailable;
        public event Action<List<IsoEntry>, string>?  MissingOnStickDetected;
        public event Action<List<UsbService.StickIso>, string>? UnknownIsosOnStickDetected;
        public event Action<List<UsbService.StickIso>, string>? IncompleteIsosOnStickDetected;
        public event Action<List<VersionCheckEntryResult>>?     HealthCheckCompleted;
        public event Action<List<(IsoEntry DbEntry, UsbService.StickIso StickIso)>, string>? NewerVersionsOnStickDetected;
        public event Action<string, int, string>? DownloadItemProgress;
        public event Action<int, int, int>?       DownloadBatchCompleted;
        public event Action<string, int, string>? CopyItemProgress;
        public event Action<int>?                 CopyBatchCompleted;
        public event Action<string>?              OperationSucceeded;
        public event Action?                      AutoVersionCheckCompleted;
        public event Action?                      RefreshTree;

        public MainViewModel(Dispatcher ui)
        {
            _ui = ui;
            DownloadCommand      = new RelayCommand(OnDownload,     () => !IsBusy);
            CopyToUsbCommand     = new RelayCommand(OnCopyToUsb,    () => !IsBusy);
            CheckUpdatesCommand  = new RelayCommand(OnCheckUpdates, () => !IsBusy);
            CheckUrlsCommand     = new RelayCommand(OnCheckUrls,    () => !IsBusy);
            HealthCheckCommand   = new RelayCommand(OnHealthCheck,  () => !IsBusy && !HealthCheckActive);
            VentoyCommand        = new RelayCommand(OnVentoy,       () => !IsBusy);
            CancelCommand        = new RelayCommand(OnCancel,       () => IsBusy);
            RefreshDrivesCommand = new RelayCommand(RefreshDrives);
            _expertMode = IniService.Read(_paths.SettingsIni, "App", "ExpertMode", "0") == "1";
            _secureBoot = IniService.Read(_paths.SettingsIni, "App", "SecureBoot", "1") != "0";
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

        private void DeduplicateEntries()
        {
            bool changed = false;
            var processed = new HashSet<IsoEntry>();
            var snapshot  = _db.Entries.ToList();

            for (int i = 0; i < snapshot.Count; i++)
            {
                var a = snapshot[i]; if (processed.Contains(a)) continue;
                var duplicates = snapshot.Skip(i + 1)
                    .Where(b => !processed.Contains(b) &&
                                !string.Equals(a.Filename, b.Filename, StringComparison.OrdinalIgnoreCase) &&
                                AreSameDistro(a, b))
                    .ToList();
                if (duplicates.Count == 0) continue;

                var allEntries = new List<IsoEntry> { a }.Concat(duplicates).ToList();
                var keeper = allEntries.OrderBy(e => e.ImportedFromStick ? 1 : 0).First();
                var newerDup = allEntries
                    .Where(e => e != keeper && !string.IsNullOrWhiteSpace(e.Filename) &&
                                IsVersionNewer(
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
                    if (idx >= 0) { Log($"   🗑 Duplikat entfernt: {dup.Name}"); _db.Remove(idx); changed = true; }
                    processed.Add(dup);
                }
                processed.Add(keeper);
            }
            if (changed) _db.Save();
        }

        private static bool AreSameDistro(IsoEntry a, IsoEntry b)
        {
            bool aHas = !string.IsNullOrWhiteSpace(a.Filename); bool bHas = !string.IsNullOrWhiteSpace(b.Filename);
            if (aHas && bHas) return IsSameDistroDifferentVersion(a.Filename, b.Filename);
            if (!aHas && bHas) return IsLikelySameDistroByName(a.Name, b.Filename);
            if (aHas && !bHas) return IsLikelySameDistroByName(b.Name, a.Filename);
            return false;
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
            var existing = _db.Entries.FirstOrDefault(d => AreSameDistro(d, e));
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
            if (string.IsNullOrEmpty(SelectedDriveLetter) || UsbScanActive) return;
            Log($"💾 Stick-Scan: {SelectedDriveLetter}");
            StatusText = $"Scanne {SelectedDriveLetter}..."; UsbScanActive = true; UsbScanPercent = 0;
            string letter = SelectedDriveLetter;
            var worker = new UsbScanWorker(letter, _db.Entries);
            worker.Completed += (ltr, found, incomplete) => _ui.Invoke(() =>
            {
                ApplyStickResults(found); RefreshAllEntries(); UsbScanActive = false; UsbScanPercent = 100;
                StatusText = $"✓ Stick-Scan {ltr}: {found.Count} ISO(s).";
                Log($"💾 Stick-Scan {ltr}: {found.Count} ISO(s) gefunden.");
                if (found.Count > 0) foreach (var iso in found) Log($"   • {iso.Filename}  [{iso.Category}]  {iso.Size/1_073_741_824.0:F2} GB");
                OnPropertyChanged(nameof(DriveInfoText));

                if (incomplete.Count > 0)
                {
                    Log($"⚠ Stick-Scan {ltr}: {incomplete.Count} unvollständige ISO(s) erkannt (Online-Größenprüfung).");
                    foreach (var si in incomplete) Log($"   ⚠ {si.Filename}  ({FormatGb(si.Size)}) — vermutlich Datenmüll.");
                    IncompleteIsosOnStickDetected?.Invoke(incomplete, ltr);
                }

                var newerOnStick = DetectNewerVersionsOnStick(found);
                var newerFnSet   = new HashSet<string>(newerOnStick.Select(x => x.StickIso.Filename), StringComparer.OrdinalIgnoreCase);
                var dbFn         = new HashSet<string>(_db.Entries.Select(e => e.Filename), StringComparer.OrdinalIgnoreCase);
                var initialUnknowns = found.Where(f => !string.IsNullOrWhiteSpace(f.Filename) && !dbFn.Contains(f.Filename) && !newerFnSet.Contains(f.Filename)).ToList();

                var additionalNewer = new List<(IsoEntry DbEntry, UsbService.StickIso StickIso)>();
                var trueUnknowns    = new List<UsbService.StickIso>();
                foreach (var stickIso in initialUnknowns)
                {
                    var match = _db.Entries.Where(e => !string.IsNullOrWhiteSpace(e.Name))
                        .FirstOrDefault(e => IsLikelySameDistroByName(e.Name, stickIso.Filename) &&
                            (string.IsNullOrWhiteSpace(e.Filename) ||
                             IsVersionNewer(HttpService.ExtractVersion(stickIso.Filename), HttpService.ExtractVersion(e.Filename))));
                    if (match != null) additionalNewer.Add((match, stickIso));
                    else               trueUnknowns.Add(stickIso);
                }

                var allNewer = newerOnStick.Concat(additionalNewer).ToList();
                if (allNewer.Count > 0) NewerVersionsOnStickDetected?.Invoke(allNewer, ltr);
                if (trueUnknowns.Count > 0) UnknownIsosOnStickDetected?.Invoke(trueUnknowns, ltr);
                var missing = GetVerifiedCompleteEntriesMissingFromStick();
                if (missing.Count > 0) MissingOnStickDetected?.Invoke(missing, ltr);
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

        private List<(IsoEntry DbEntry, UsbService.StickIso StickIso)> DetectNewerVersionsOnStick(List<UsbService.StickIso> found)
        {
            var result = new List<(IsoEntry, UsbService.StickIso)>();
            foreach (var e in _db.Entries.Where(e => e.UsbStatus == Core.Models.UsbStatus.Outdated))
            {
                var si = found.FirstOrDefault(f => IsSameDistroDifferentVersion(e.Filename, f.Filename));
                if (si is null) continue;
                if (IsVersionNewer(HttpService.ExtractVersion(si.Filename), HttpService.ExtractVersion(e.Filename)))
                    result.Add((e, si));
            }
            return result;
        }

        private static bool IsVersionNewer(string c, string d)
        {
            if (string.IsNullOrWhiteSpace(c) || string.IsNullOrWhiteSpace(d) || string.Equals(c, d, StringComparison.OrdinalIgnoreCase)) return false;
            int[] cP = ParseVersionParts(c), dP = ParseVersionParts(d);
            if (cP.Length > 0 && dP.Length > 0)
            {
                for (int i = 0; i < Math.Max(cP.Length, dP.Length); i++)
                { int a = i < cP.Length ? cP[i] : 0, b = i < dP.Length ? dP[i] : 0; if (a > b) return true; if (a < b) return false; }
                return false;
            }
            return string.Compare(c, d, StringComparison.OrdinalIgnoreCase) > 0;
        }

        private static int[] ParseVersionParts(string v)
        { var n = new List<int>(); foreach (string p in v.Split('.', '-', '_')) { if (int.TryParse(p, out int x)) n.Add(x); else break; } return n.ToArray(); }

        private void ApplyStickResults(List<UsbService.StickIso> found)
        {
            var byFn = new Dictionary<string, UsbService.StickIso>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in found) if (!byFn.ContainsKey(f.Filename)) byFn[f.Filename] = f;
            foreach (var e in _db.Entries)
            {
                if (!string.IsNullOrEmpty(e.Filename) && byFn.TryGetValue(e.Filename, out var exact))
                { e.UsbStatus = Core.Models.UsbStatus.Ok; e.UsbSize = FormatGb(exact.Size); continue; }
                var other = found.FirstOrDefault(f => IsSameDistroDifferentVersion(e.Filename, f.Filename));
                if (other != null) { e.UsbStatus = Core.Models.UsbStatus.Outdated; e.UsbSize = FormatGb(other.Size); }
                else               { e.UsbStatus = Core.Models.UsbStatus.Missing;  e.UsbSize = string.Empty; }
            }
        }

        private static bool IsSameDistroDifferentVersion(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
            if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return false;
            return NormalizeForDistroComparison(a) == NormalizeForDistroComparison(b);
        }

        private static string NormalizeForDistroComparison(string filename)
        {
            string s = Regex.Replace(filename.ToLowerInvariant(), @"[\d.]+", string.Empty);
            foreach (string cn in _platformCodenames)
            { s = s.Replace("." + cn, string.Empty); s = s.Replace("-" + cn, string.Empty); s = s.Replace("_" + cn, string.Empty); }
            return s;
        }

        private static bool IsLikelySameDistroByName(string dbEntryName, string stickFilename)
        {
            if (string.IsNullOrWhiteSpace(dbEntryName) || string.IsNullOrWhiteSpace(stickFilename)) return false;
            string nameLower = dbEntryName.ToLowerInvariant();
            string fileLower = Path.GetFileNameWithoutExtension(stickFilename).ToLowerInvariant();
            string? dw = nameLower
                .Split(new[] { ' ', '-', '_', '.', '(', ')' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(w => w.Length > 4 && !char.IsDigit(w[0]) && !_genericDistroWords.Contains(w));
            return dw != null && fileLower.Contains(dw);
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
            Log($"🌐 Online-Versionscheck gestartet — {_db.Count} Distros …");
            StatusText = "🌐 Online-Versionscheck läuft …"; OnlineScanActive = true; OnlineScanPercent = 0;
            string capturedDrive = SelectedDriveLetter;
            var worker = new AutoVersionCheckWorker(_db.Entries);
            worker.Progress += (c, t) => _ui.Invoke(() => OnlineScanPercent = t > 0 ? (c * 100) / t : 0);
            worker.EntryChecked += result => _ui.Invoke(() =>
            {
                if (!result.Resolved) { Log($"   ⚠ {result.Name}: nicht erreichbar."); return; }
                Log(result.HasUpdate
                    ? $"   🆕 {result.Name}: v{result.LocalVersion} → v{result.RemoteVersion}"
                    : $"   ✓ {result.Name}: v{result.RemoteVersion} (aktuell)");
                int idx = _db.Entries.ToList().FindIndex(e => e.Name == result.Name);
                if (idx >= 0) RefreshEntry(idx);
            });
            worker.Completed += (resolved, updates) =>
            {
                var oldFn = updates
                    .Where(i => !string.IsNullOrEmpty(_db.Entries[i].RemoteUrl) && !string.IsNullOrEmpty(_db.Entries[i].Filename))
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
                        if (!string.IsNullOrEmpty(oldVer) && !string.IsNullOrEmpty(newVer) && oldVer != newVer)
                        {
                            int pos = e.Name.IndexOf(oldVer, StringComparison.Ordinal);
                            if (pos >= 0) { string on = e.Name; e.Name = e.Name[..pos] + newVer + e.Name[(pos + oldVer.Length)..]; Log($"   ✏ {on} → {e.Name}"); }
                        }
                    }
                    if (updates.Count > 0) { _db.Save(); Log($"💾 Datenbank: {updates.Count} neue Version(en) gespeichert."); }
                    OnlineScanActive = false; OnlineScanPercent = 100; RefreshAllEntries();
                    StatusText = updates.Count > 0 ? $"🆕 {updates.Count} aktualisiert."
                               : resolved > 0      ? $"✅ Alle {resolved} aktuell." : "⚠ Nicht erreichbar.";
                    Log($"🌐 Versionscheck: {StatusText}"); AutoVersionCheckCompleted?.Invoke();
                });
                if (!string.IsNullOrEmpty(capturedDrive))
                    _ = Task.Run(async () =>
                    {
                        _ui.Invoke(() => { UsbScanActive = true; UsbScanPercent = 0; Log($"💾 Prüfe Stick {capturedDrive} …"); });
                        var (si, incomplete) = await UsbService.Instance.ScanStickVerifiedAsync(capturedDrive, _db.Entries).ConfigureAwait(false);
                        var sn = new HashSet<string>(si.Select(s => s.Filename), StringComparer.OrdinalIgnoreCase);
                        var od = oldFn.Where(kvp => sn.Contains(kvp.Key)).Select(kvp => _db.Entries[kvp.Value]).ToList();
                        _ui.Invoke(() =>
                        {
                            ApplyStickResults(si); UsbScanActive = false; UsbScanPercent = 100; RefreshAllEntries();
                            if (incomplete.Count > 0)
                            {
                                Log($"⚠ Stick-Prüfung {capturedDrive}: {incomplete.Count} unvollständige ISO(s) erkannt (Online-Größenprüfung).");
                                foreach (var s in incomplete) Log($"   ⚠ {s.Filename}  ({FormatGb(s.Size)}) — vermutlich Datenmüll.");
                                IncompleteIsosOnStickDetected?.Invoke(incomplete, capturedDrive);
                            }
                            if (od.Count > 0) { Log($"💾 {od.Count} veraltete ISO(s) auf {capturedDrive}."); foreach (var e in od) Log($"   🆕 {e.Name}: v{e.RemoteVersion}"); StickUpdateAvailable?.Invoke(od, capturedDrive); }
                            else if (si.Count > 0) Log($"✅ Alle ISOs auf {capturedDrive} aktuell.");
                            var missing = GetVerifiedCompleteEntriesMissingFromStick().Where(e => !od.Contains(e)).ToList();
                            if (missing.Count > 0) MissingOnStickDetected?.Invoke(missing, capturedDrive);
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
            Channel<IsoEntry>? pipelineChannel = null; Task? pipelineTask = null;
            if (copyAfter && !string.IsNullOrEmpty(drive))
            {
                pipelineChannel = Channel.CreateUnbounded<IsoEntry>(new UnboundedChannelOptions { SingleReader = true });
                var channelReader = pipelineChannel.Reader; var capDrive = drive;
                pipelineTask = Task.Run(() => RunPipelineCopyConsumerAsync(channelReader, capDrive));
                worker.ItemCompleted += (entry, success) =>
                {
                    if (success && entry.IsLocallyAvailable(_paths.DownloadDir))
                    {
                        pipelineChannel.Writer.TryWrite(entry);
                        _ui.Invoke(() => { DownloadItemProgress?.Invoke(entry.Name, 100, "⏳ Warte auf Kopierslot …"); Log($"   ↪ {entry.Name} → Kopier-Warteschlange."); });
                    }
                };
            }
            worker.OverallProgress += (pct, detail) => _ui.Invoke(() => { ProgressPercent = pct; StatusText = $"⬇ {detail}"; });
            worker.SlotUpdated += p => _ui.Invoke(() => { RefreshEntry(GetEntryIndex(p.IsoName)); DownloadItemProgress?.Invoke(p.IsoName, p.Percent, p.Status); });
            worker.Completed += (ok, failed, _) => _ui.Invoke(() =>
            {
                _db.Save(); Log($"⬇ Downloads abgeschlossen: {ok} OK, {failed} fehlgeschlagen."); DownloadBatchCompleted?.Invoke(ok, failed, 0);
                if (pipelineChannel != null && pipelineTask != null)
                {
                    pipelineChannel.Writer.Complete();
                    StatusText = ok > 0 ? $"⬇ {ok} Downloads fertig — Stick-Kopie läuft …" : "⬇ 0 Downloads …";
                    if (ok > 0) Log("⬇ Downloads fertig. Pipeline-Kopiervorgang läuft weiter …");
                    var capDrive = drive; int okC = ok; int failedC = failed;
                    pipelineTask.ContinueWith(_ => _ui.Invoke(() =>
                    {
                        SetBusy(false); RefreshAllEntries(); ProgressPercent = 100;
                        TriggerVentoyMenuUpdate(capDrive); TriggerUsbScan();
                        if (okC > 0)
                        {
                            string msg = $"{okC} ISO(s) heruntergeladen und auf {capDrive} kopiert.\n\n" +
                                         "Jede ISO wurde direkt nach dem Download kopiert und lokal gelöscht.\n" +
                                         "Das Ventoy-Bootmenü wurde automatisch aktualisiert.";
                            if (failedC > 0) msg += $"\n\n⚠ {failedC} ISO(s) fehlgeschlagen.";
                            StatusText = $"✅ {okC} heruntergeladen und auf {capDrive} kopiert.";
                            Log(StatusText); OperationSucceeded?.Invoke(msg);
                        }
                        else { StatusText = failedC > 0 ? $"❌ {failedC} Download(s) fehlgeschlagen." : "⬇ Keine Downloads."; Log(StatusText); }
                    }));
                }
                else if (!string.IsNullOrEmpty(drive) && copyAfter && ok > 0)
                { SetBusy(false); StartCopyToStick(queue, drive, deleteAfter); }
                else
                {
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

        private async Task RunPipelineCopyConsumerAsync(ChannelReader<IsoEntry> reader, string drive)
        {
            const int bufSize = 4 * 1024 * 1024;
            byte[] buf = new byte[bufSize];
            await foreach (var entry in reader.ReadAllAsync().ConfigureAwait(false))
            {
                string? srcPath = entry.FindLocalPath(_paths.DownloadDir);
                if (srcPath is null || !File.Exists(srcPath))
                { _ui.Invoke(() => Log($"   ⚠ {entry.Name}: Quelldatei nicht gefunden.")); continue; }
                long fileSize = IsoEntry.GetRobustLength(srcPath);
                if (fileSize < Constants.MinIsoSizeBytes)
                { _ui.Invoke(() => Log($"   ⚠ {entry.Name}: zu klein ({fileSize / 1_048_576} MB).")); continue; }
                string targetDir = Path.Combine(UsbService.DriveRoot(drive), entry.NormalizedCategory);
                Directory.CreateDirectory(targetDir);
                string targetPath = Path.Combine(targetDir, entry.Filename);
                long   copied = 0L; var sw = Stopwatch.StartNew(); long lastMark = 0L; double lastEl = 0.0;
                string entryName = entry.Name;
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
                    continue;
                }
                if (!copyOk) continue;
                long sz = fileSize;
                entry.UsbStatus = Core.Models.UsbStatus.Ok;
                entry.UsbSize   = FormatGb(sz);
                entry.VerifiedComplete = false;
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
            worker.Completed    += (_, count, bytes, _) => _ui.Invoke(() =>
            {
                SetBusy(false); RefreshAllEntries();
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
                SetBusy(false); if (updates.Count > 0) _db.Save(); RefreshAllEntries();
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
                if (updates.Count > 0) _db.Save(); RefreshAllEntries();
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

        private void SetBusy(bool busy) { IsBusy = busy; if (busy) ProgressPercent = 0; }

        private void RefreshEntry(int index)
        {
            if (index < 0 || index >= _db.Entries.Count) return;
            var entry = _db.Entries[index];
            foreach (var cat in Categories)
            { var vm = cat.Entries.FirstOrDefault(e => e.Model == entry); vm?.Refresh(); }
        }

        private int GetEntryIndex(string n) => _db.Entries.ToList().FindIndex(e => e.Name == n || e.Filename == n);
        private void Log(string msg) => LogMessage?.Invoke(msg);

        public void SaveAndClose()
        {
            Log("▶ Anwendung wird beendet.");
            IniService.Write(_paths.SettingsIni, "App", "ExpertMode", _expertMode ? "1" : "0");
            IniService.Write(_paths.SettingsIni, "App", "SecureBoot", _secureBoot ? "1" : "0");
            _db.SaveFilenames();
        }
    }
}
