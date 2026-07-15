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
                    sb.AppendLine("📥  Vom USB-Stick importiert");

                if (_entry.UrlChecked)
                    sb.AppendLine(_entry.UrlOk
                        ? "🌐✓  URL erreichbar — Download-Server antwortet"
                        : "🌐✗  URL nicht erreichbar — Download-Server antwortet nicht");

                if (_entry.HasResolvedUpdate)
                    sb.AppendLine($"🆕  Neue Version verfügbar: v{_entry.RemoteVersion}  (jetzt herunterladen)");

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
                    return $"Lokal {size / 1_048_576} MB";
                }
                return "nicht lokal";
            }
        }

        public string UsbStatus => _entry.UsbStatus switch
        {
            Core.Models.UsbStatus.Ok       => $"Ja  {_entry.UsbSize}".Trim(),
            Core.Models.UsbStatus.Outdated => $"Veraltet  {_entry.UsbSize}".Trim(),
            Core.Models.UsbStatus.Missing  => "Nein",
            _                              => "Ungeprüft",
        };

        public string VersionStatus
        {
            get
            {
                if (_entry.HasResolvedUpdate)
                    return $"Update v{_entry.RemoteVersion}";
                if (_entry.HasOnlineVersionInfo)
                    return $"Aktuell (v{_entry.RemoteVersion})";
                if (_entry.UsbStatus == Core.Models.UsbStatus.Ok)
                    return "Ja";
                if (_entry.IsLocallyAvailable(_downloadDir))
                    return "Lokal vorhanden";
                return "?";
            }
        }

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
            ? "⚠ Hash-Abweichung — Datei weicht von der zuletzt gespeicherten Prüfsumme ab (evtl. beschädigt oder ersetzt)."
            : _entry.Sha256Source == "OfficialChecksum"
                ? "✅ Prüfsumme gegen die offiziell vom Anbieter veröffentlichte Prüfsumme verifiziert."
                : "✅ Referenz-Prüfsumme lokal beim Download/Import berechnet (keine offizielle Gegenprüfung).";

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
        }
    }

    /// <summary>ViewModel für eine Kategorie-Gruppe in der Hauptansicht.</summary>
    public sealed class IsoCategoryViewModel : ViewModelBase
    {
        public string Category       { get; }
        public string CategoryLabel  { get; }
        public bool   IsExpanded     { get; set; } = true;

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
