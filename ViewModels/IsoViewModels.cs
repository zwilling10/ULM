// ViewModels/IsoViewModels.cs
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Media;
using ULM.Core.Models;
using ULM.Infrastructure;

namespace ULM.ViewModels
{
    /// <summary>ViewModel für einen einzelnen ISO-Eintrag in der Hauptansicht.</summary>
    public sealed class IsoEntryViewModel : ViewModelBase
    {
        private readonly IsoEntry _entry;
        private readonly string   _downloadDir;

        public IsoEntryViewModel(IsoEntry entry, string downloadDir)
        {
            _entry       = entry;
            _downloadDir = downloadDir;
        }

        public IsoEntry Model => _entry;

        public bool IsSelected
        {
            get => _entry.IsSelected;
            set { _entry.IsSelected = value; OnPropertyChanged(); }
        }

        public string Name => BuildDisplayName();

        /// <summary>
        /// Tooltip über dem Distro-Namen.
        ///
        /// Zeigt:
        ///   1. Erklärungen für alle aktuell sichtbaren Symbole (📥, 🌐✓/✗, 🆕)
        ///   2. Distro-Beschreibung (falls hinterlegt)
        ///
        /// Gibt null zurück wenn weder Symbole noch Beschreibung vorhanden →
        /// leere Tooltips werden so verhindert.
        /// </summary>
        public string? TipTooltip
        {
            get
            {
                var sb = new StringBuilder();

                // ── Sichtbare Symbol-Erklärungen ──────────────────────────
                if (_entry.ImportedFromStick)
                    sb.AppendLine(LocalizationService.T(Str.Row_TipImported));

                if (_entry.UrlChecked)
                    sb.AppendLine(_entry.UrlOk
                        ? LocalizationService.T(Str.Row_TipUrlOk)
                        : LocalizationService.T(Str.Row_TipUrlFail));

                if (_entry.HasResolvedUpdate)
                    sb.AppendLine(string.Format(LocalizationService.T(Str.Row_TipNewVersion), _entry.RemoteVersion));

                bool hasSymbols = sb.Length > 0;

                // ── Distro-Beschreibung (falls vorhanden) ──────────────────
                if (!string.IsNullOrWhiteSpace(_entry.Tip))
                {
                    if (hasSymbols) sb.AppendLine("─────────────────────────");
                    sb.Append(_entry.Tip);
                }

                string result = sb.ToString().Trim();
                return string.IsNullOrEmpty(result) ? null : result;
            }
        }

        public string LocalStatus
        {
            get
            {
                if (_entry.IsLocallyAvailable(_downloadDir))
                {
                    long size = _entry.LocalFileSize(_downloadDir);
                    return $"{LocalizationService.T(Str.Row_Local)} {size / 1_048_576} MB";
                }
                return LocalizationService.T(Str.Row_NotLocal);
            }
        }

        public string UsbStatus => _entry.UsbStatus switch
        {
            Core.Models.UsbStatus.Ok       => $"{LocalizationService.T(Str.Row_Yes)}  {_entry.UsbSize}".Trim(),
            Core.Models.UsbStatus.Outdated => $"{LocalizationService.T(Str.Row_Outdated)}  {_entry.UsbSize}".Trim(),
            Core.Models.UsbStatus.Missing  => LocalizationService.T(Str.Row_No),
            _                              => LocalizationService.T(Str.Row_Unverified),
        };

        public string VersionStatus
        {
            get
            {
                if (_entry.HasResolvedUpdate)
                    return $"{LocalizationService.T(Str.Row_UpdatePrefix)} v{_entry.RemoteVersion}";
                if (_entry.HasOnlineVersionInfo)
                    return $"{LocalizationService.T(Str.Row_CurrentPrefix)} (v{_entry.RemoteVersion})";
                if (_entry.UsbStatus == Core.Models.UsbStatus.Ok)
                    return LocalizationService.T(Str.Row_Yes);
                if (_entry.IsLocallyAvailable(_downloadDir))
                    return LocalizationService.T(Str.Row_LocallyAvailable);
                return "?";
            }
        }

        // Neu in Phase 3: ersetzt den frueher direkt in MainWindow.xaml hartcodierten
        // ToolTip="Quelle manuell suchen/eintragen" auf dem 🔧-Button in EntryTemplate — die
        // DataTemplate wird pro Zeile instanziiert, ApplyLocalizedText() (einmalig fuers ganze
        // Fenster) kann sie nicht erreichen. Siehe
        // docs/superpowers/specs/2026-07-24-mainwindow-localization-design.md Architektur-Korrektur.
        public string ManualSearchTooltip => LocalizationService.T(Str.Row_ManualSearchTooltip);

        // Steuert die Sichtbarkeit des "Quelle manuell suchen/eintragen"-Buttons in der Hauptliste
        // (Views/MainWindow.xaml). Bewusst NUR bei einer zusammenhängenden Fehlschlagsserie der
        // automatischen Auflösung sichtbar (siehe HttpService.ApplyResolveOutcome) — der Button ist
        // ein Sicherheitsnetz für Härtefälle wie Shadowfetch, kein Dauerelement in jeder Zeile.
        public bool ShowManualSearchButton => _entry.FailedResolveStreak >= Constants.ManualSearchFailureThreshold;

        public string StatusBracket
        {
            get
            {
                if (_entry.UsbStatus == Core.Models.UsbStatus.Ok)
                    return string.IsNullOrEmpty(_entry.UsbSize)
                        ? "[OK] USB aktuell" : $"[OK] USB aktuell {_entry.UsbSize}";
                if (_entry.UsbStatus == Core.Models.UsbStatus.Outdated)
                    return string.IsNullOrEmpty(_entry.UsbSize)
                        ? "[!] USB veraltet" : $"[!] USB veraltet {_entry.UsbSize}";
                if (_entry.IsLocallyAvailable(_downloadDir))
                    return "[OK] lokal vorhanden";
                return "[-] nicht auf Stick";
            }
        }

        public Brush StatusBrush
        {
            get
            {
                if (_entry.UsbStatus == Core.Models.UsbStatus.Ok)
                    return ThemeColors.Green;
                if (_entry.UsbStatus == Core.Models.UsbStatus.Outdated)
                    return ThemeColors.Amber;
                if (_entry.IsLocallyAvailable(_downloadDir))
                    return ThemeColors.Mid;
                return ThemeColors.Dim;
            }
        }

        public Brush ForegroundBrush => GetForeground();

        // Hash-Status-Symbol in der Hauptliste: grün = Referenz-Hash vorhanden (lokal berechnet oder
        // offiziell verifiziert), rot = bei der letzten Integritätsprüfung eine Abweichung gefunden,
        // unsichtbar = noch nie heruntergeladen/importiert (kein Hash vorhanden) — bewusst NICHT rot,
        // sonst würde jede noch nicht heruntergeladene ISO fälschlich wie ein Problem aussehen.
        public bool   HasHashStatus   => !string.IsNullOrEmpty(_entry.Sha256);
        public Brush  HashStatusBrush => _entry.HashMismatchDetected ? ThemeColors.Red : ThemeColors.Green;
        public string HashStatusTooltip => _entry.HashMismatchDetected
            ? LocalizationService.T(Str.Row_HashMismatch)
            : _entry.Sha256Source == "OfficialChecksum"
                ? LocalizationService.T(Str.Row_HashVerifiedOfficial)
                : LocalizationService.T(Str.Row_HashLocalOnly);

        private string BuildDisplayName()
        {
            string prefix = _entry.ImportedFromStick ? "📥 " : string.Empty;
            string urlTag  = _entry.UrlChecked
                ? (_entry.UrlOk ? " 🌐✓" : " 🌐✗") : string.Empty;
            string verTag  = _entry.HasResolvedUpdate
                ? $"  🆕 v{_entry.RemoteVersion}" : string.Empty;
            string status  = !string.IsNullOrEmpty(_entry.DownloadStatus)
                ? $"  {_entry.DownloadStatus}" : string.Empty;
            return $"{prefix}{_entry.Name}{urlTag}{verTag}{status}";
        }

        private Brush GetForeground()
        {
            bool isLocal = _entry.IsLocallyAvailable(_downloadDir);

            if (_entry.ImportedFromStick)
                return ThemeColors.Teal;
            if (_entry.UsbStatus == Core.Models.UsbStatus.Ok)
                return ThemeColors.Green;
            if (_entry.HasResolvedUpdate || _entry.UsbStatus == Core.Models.UsbStatus.Outdated)
                return ThemeColors.Amber;
            if (_entry.UrlChecked && !_entry.UrlOk)
                return ThemeColors.Red;
            if (!_entry.UrlChecked && string.IsNullOrEmpty(_entry.Url) &&
                string.IsNullOrEmpty(_entry.GithubRepo))
                return ThemeColors.Dim;
            if (isLocal)
                return ThemeColors.Green;
            if (_entry.HasOnlineVersionInfo)
                return ThemeColors.Mid;
            return ThemeColors.Header; // Standard/Basis-Textfarbe
        }

        public void Refresh()
        {
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(LocalStatus));
            OnPropertyChanged(nameof(UsbStatus));
            OnPropertyChanged(nameof(StatusBracket));
            OnPropertyChanged(nameof(StatusBrush));
            OnPropertyChanged(nameof(VersionStatus));
            OnPropertyChanged(nameof(ForegroundBrush));
            OnPropertyChanged(nameof(IsSelected));
            OnPropertyChanged(nameof(TipTooltip));
            OnPropertyChanged(nameof(HasHashStatus));
            OnPropertyChanged(nameof(HashStatusBrush));
            OnPropertyChanged(nameof(HashStatusTooltip));
            OnPropertyChanged(nameof(ShowManualSearchButton));
        }
    }

    /// <summary>ViewModel für eine Kategorie-Gruppe in der Hauptansicht.</summary>
    public sealed class IsoCategoryViewModel : ViewModelBase
    {
        public string Category       { get; }
        public string CategoryLabel  { get; }
        public bool   IsExpanded     { get; set; } = true;

        // Neu in Phase 3: ersetzt den frueher direkt in MainWindow.xaml hartcodierten
        // ToolTip="Alle Distros dieser Kategorie an-/abwählen" auf der Sammel-Checkbox in
        // CategoryTemplate — dieselbe Begruendung wie IsoEntryViewModel.ManualSearchTooltip oben.
        public string SelectAllTooltip => LocalizationService.T(Str.Row_CategorySelectAllTooltip);

        public ObservableCollection<IsoEntryViewModel> Entries { get; } = new();

        public IsoCategoryViewModel(string category)
        {
            Category      = category;
            CategoryLabel = Constants.CategoryLabel(category);
            Entries.CollectionChanged += Entries_CollectionChanged;
        }

        public bool? AllSelected
        {
            get
            {
                if (Entries.Count == 0) return false;
                int selected = Entries.Count(e => e.IsSelected);
                if (selected == 0) return false;
                if (selected == Entries.Count) return true;
                return null;
            }
            set
            {
                bool newState = value == true;
                foreach (var e in Entries) e.IsSelected = newState;
                OnPropertyChanged();
            }
        }

        private void Entries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (IsoEntryViewModel item in e.NewItems)
                    item.PropertyChanged += Entry_PropertyChanged;
            if (e.OldItems != null)
                foreach (IsoEntryViewModel item in e.OldItems)
                    item.PropertyChanged -= Entry_PropertyChanged;

            OnPropertyChanged(nameof(AllSelected));
        }

        private void Entry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IsoEntryViewModel.IsSelected))
                OnPropertyChanged(nameof(AllSelected));
        }
    }
}
