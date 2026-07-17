// Views/Dialogs/ManualSourceSearchDialog.cs
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ULM.Core.Models;
using ULM.Core.Services;

namespace ULM.Views.Dialogs
{
    // AppRes (Brush/Style/AddField/AddCategoryCombo-Helfer) liegt bereits im selben Namespace
    // ULM.Views.Dialogs (Views/Dialogs/DownloadDialogs.cs) — kein zusätzliches using nötig.
    //
    // Manuelle Quellen-Suche für Distros, bei denen die automatische Selbstlern-Auflösung
    // (HttpService.ResolveLatestAsync) hartnäckig scheitert — siehe
    // docs/superpowers/specs/2026-07-17-manual-source-search-design.md. Zeigt dieselben Felder wie
    // IsoEditDialog PLUS ein Suchfeld: findet ULM eigene .iso-Kandidaten, erscheinen sie als
    // anklickbare Liste; findet ULM NICHTS, öffnet ein Klick stattdessen den Standard-Browser mit
    // vorausgefüllter DuckDuckGo-Suche.
    public sealed class ManualSourceSearchDialog : Window
    {
        private readonly IsoEntry _entry;
        private readonly TextBox  _tbName, _tbUrl, _tbFilename,
                                  _tbMirror1, _tbMirror2, _tbMirror3,
                                  _tbGhRepo, _tbGhAsset, _tbTip, _tbSearch;
        private readonly ComboBox _cbCat;
        private readonly StackPanel _resultsPanel;
        private readonly TextBlock  _searchStatus;

        public ManualSourceSearchDialog(IsoEntry entry)
        {
            _entry = entry;
            Title  = $"Quelle manuell suchen: {entry.Name}";
            Width  = 640;
            SizeToContent = SizeToContent.Height;
            MaxHeight = SystemParameters.WorkArea.Height - 40;
            ResizeMode = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = AppRes.Brush("BrushBg");

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var root   = new StackPanel { Margin = new Thickness(20) };

            _tbName    = AppRes.AddField(root, "Name *",          entry.Name);
            _cbCat     = AppRes.AddCategoryCombo(root, entry.Category);
            _tbUrl     = AppRes.AddField(root, "Primäre URL",     entry.Url);
            _tbFilename= AppRes.AddField(root, "Dateiname *",     entry.Filename);
            _tbMirror1 = AppRes.AddField(root, "Mirror 1",        entry.Mirror1);
            _tbMirror2 = AppRes.AddField(root, "Mirror 2",        entry.Mirror2);
            _tbMirror3 = AppRes.AddField(root, "Mirror 3",        entry.Mirror3);
            _tbGhRepo  = AppRes.AddField(root, "GitHub Repo",     entry.GithubRepo);
            _tbGhAsset = AppRes.AddField(root, "GitHub Asset",    entry.GithubAsset);
            _tbTip     = AppRes.AddField(root, "Beschreibung",    entry.Tip, multiLine: true);

            root.Children.Add(new Border { Height = 1, Margin = new Thickness(0, 6, 0, 14), Background = AppRes.Brush("BrushBorder") });
            root.Children.Add(new TextBlock { Text = "Manuelle Suche", FontWeight = FontWeights.Bold, FontSize = 13.5, Foreground = AppRes.Brush("BrushHeader"), Margin = new Thickness(0, 0, 0, 8) });

            var searchRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var searchBtn = new Button { Content = "🔍 Suchen", Style = AppRes.Style("BtnPrimary"), Width = 110 };
            DockPanel.SetDock(searchBtn, Dock.Right);
            _tbSearch = new TextBox { Text = $"{entry.Name} iso", Margin = new Thickness(0, 0, 8, 0), VerticalContentAlignment = VerticalAlignment.Center };
            searchRow.Children.Add(searchBtn);
            searchRow.Children.Add(_tbSearch);
            root.Children.Add(searchRow);

            _searchStatus = new TextBlock { FontSize = 11, Foreground = AppRes.Brush("BrushDim"), Margin = new Thickness(0, 0, 0, 6), TextWrapping = TextWrapping.Wrap };
            root.Children.Add(_searchStatus);

            _resultsPanel = new StackPanel();
            root.Children.Add(_resultsPanel);

            searchBtn.Click += async (_, _) => await RunSearchAsync();

            var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            var ok = new Button { Content = "✔ Speichern", Style = AppRes.Style("BtnPrimary"), Width = 110 };
            ok.Click += OkBtn_Click;
            var cancel = new Button { Content = "Abbrechen", Style = AppRes.Style("BtnGhost"), Width = 100, Margin = new Thickness(8, 0, 0, 0) };
            cancel.Click += (_, _) => DialogResult = false;
            btns.Children.Add(ok); btns.Children.Add(cancel);
            root.Children.Add(btns);

            scroll.Content = root;
            Content = scroll;
        }

        private async Task RunSearchAsync()
        {
            string query = _tbSearch.Text.Trim();
            if (string.IsNullOrWhiteSpace(query)) return;
            _resultsPanel.Children.Clear();
            _searchStatus.Text = "🔍 Suche läuft …";
            var hits = await HttpService.Instance.SearchIsoLinksAsync(query);

            if (hits.Count == 0)
            {
                _searchStatus.Text = "Keine Treffer in ULM — öffne Browser-Suche …";
                try
                {
                    string url = $"https://duckduckgo.com/?q={Uri.EscapeDataString(query + " download")}";
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                    _searchStatus.Text = "Keine Treffer in ULM — Browser-Suche geöffnet. Gefundene URL bitte oben manuell eintragen.";
                }
                catch (Exception ex)
                {
                    _searchStatus.Text = $"Keine Treffer, Browser konnte nicht geöffnet werden: {ex.Message}";
                }
                return;
            }

            _searchStatus.Text = $"{hits.Count} Treffer gefunden — auswählen zum Übernehmen:";
            foreach (var hit in hits)
            {
                var row = new Button
                {
                    Content = $"{hit.Filename}  —  {hit.SourcePage}",
                    Style = AppRes.Style("BtnGhost"),
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 4),
                };
                row.Click += (_, _) => { _tbUrl.Text = hit.Url; _tbFilename.Text = hit.Filename; };
                _resultsPanel.Children.Add(row);
            }
        }

        private void OkBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_tbName.Text) || string.IsNullOrWhiteSpace(_tbFilename.Text))
            { MessageBox.Show("Name und Dateiname sind Pflichtfelder.", "Eingabe unvollständig", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            string newName = _tbName.Text.Trim();
            bool nameTaken = IsoDatabaseService.Instance.Entries.Any(other =>
                !ReferenceEquals(other, _entry) && string.Equals(other.Name, newName, StringComparison.OrdinalIgnoreCase));
            if (nameTaken)
            {
                MessageBox.Show($"Der Name \"{newName}\" wird bereits von einem anderen Eintrag verwendet.\n\n" +
                    "Bitte einen eindeutigen Namen vergeben — gleiche Namen können beim Download " +
                    "(z.B. dem \"(schneller)\"-Button) zu Verwechslungen zwischen den Einträgen führen.",
                    "Name bereits vergeben", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _entry.Name = newName; _entry.Category = _cbCat.SelectedItem?.ToString() ?? "Einsteiger";
            _entry.Url = _tbUrl.Text.Trim(); _entry.Filename = _tbFilename.Text.Trim();
            _entry.Mirror1 = _tbMirror1.Text.Trim(); _entry.Mirror2 = _tbMirror2.Text.Trim(); _entry.Mirror3 = _tbMirror3.Text.Trim();
            _entry.GithubRepo = _tbGhRepo.Text.Trim(); _entry.GithubAsset = _tbGhAsset.Text.Trim();
            _entry.Tip = _tbTip.Text.Trim();
            DialogResult = true;
        }
    }
}
