// Views/Dialogs/DatabaseDialogs.cs
// IsoListDialog, IsoEditDialog, IsoSearchDialog, ImportStickIsosDialog, NewerVersionOnStickDialog
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ULM.Core.Models;
using ULM.Core.Services;
using ULM.Core.Workers;
using ULM.Infrastructure;

namespace ULM.Views.Dialogs
{
    // ═══════════════════════════════════════════════════════════════════
    // IsoListDialog
    // ═══════════════════════════════════════════════════════════════════
    public sealed class IsoListDialog : Window
    {
        private readonly IsoDatabaseService _db;
        private readonly ListBox            _list;

        /// <summary>Mindestens ein neuer Eintrag wurde über "Neu" angelegt (nicht nur bearbeitet/
        /// gelöscht/verschoben) — der Aufrufer kann das nutzen, um gezielt nur dann einen
        /// Gesundheitscheck für den neuen, unverifizierten Eintrag auszulösen.</summary>
        public bool AnyEntryAdded { get; private set; }

        public IsoListDialog(IsoDatabaseService db)
        {
            _db = db;
            Title  = LocalizationService.T(Str.Db_ListDialog_Title);
            Width  = 780; Height = 620;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = (Brush)Application.Current.Resources["BrushBg"];

            var root = new Grid { Margin = new Thickness(12) };
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _list = new ListBox
            {
                BorderBrush = (Brush)Application.Current.Resources["BrushBorder"],
                BorderThickness = new Thickness(1),
                Background = (Brush)Application.Current.Resources["BrushWhite"],
                Padding = new Thickness(2),
            };
            FillList();
            Grid.SetRow(_list, 0);

            var btns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            AddBtn(btns, LocalizationService.T(Str.Db_Btn_New),      BtnAdd_Click,    "BtnSuccess");
            AddBtn(btns, LocalizationService.T(Str.Db_Btn_Edit),     BtnEdit_Click,   "BtnPrimary");
            AddBtn(btns, LocalizationService.T(Str.Db_Btn_Delete),   BtnDelete_Click, "BtnDanger");
            AddBtn(btns, LocalizationService.T(Str.Db_Btn_MoveUp),   BtnUp_Click,     "BtnGhost");
            AddBtn(btns, LocalizationService.T(Str.Db_Btn_MoveDown), BtnDown_Click,   "BtnGhost");

            var ok = new Button
            {
                Content = LocalizationService.T(Str.Db_Btn_Close),
                Style = (Style)Application.Current.Resources["BtnPrimary"],
                Margin = new Thickness(20, 0, 0, 0), Width = 100,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            ok.Click += (_, _) => { _db.Save(); DialogResult = true; };
            btns.Children.Add(ok);
            Grid.SetRow(btns, 1);

            root.Children.Add(_list);
            root.Children.Add(btns);
            Content = root;
        }

        // Gruppiert nach Kategorie (in der festen Constants.Categories-Reihenfolge, mit
        // Kategorie-Überschriften) statt einer flachen, unsortierten Liste — die relative
        // Reihenfolge INNERHALB einer Kategorie bleibt dabei exakt die der Rohdaten
        // (_db.Entries), damit Hoch/Runter weiterhin sichtbar wirken und 1:1 der Reihenfolge
        // entspricht, in der die Hauptliste die Einträge anzeigt (MainViewModel.RebuildTree).
        private void FillList()
        {
            _list.Items.Clear();
            var byCat = new Dictionary<string, List<(int Index, IsoEntry Entry)>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _db.Count; i++)
            {
                var e = _db.Entries[i];
                string cat = Constants.Categories.Contains(e.Category, StringComparer.OrdinalIgnoreCase) ? e.Category : "Einsteiger";
                if (!byCat.TryGetValue(cat, out var members)) byCat[cat] = members = new List<(int, IsoEntry)>();
                members.Add((i, e));
            }
            foreach (string cat in Constants.Categories)
            {
                if (!byCat.TryGetValue(cat, out var members) || members.Count == 0) continue;
                _list.Items.Add(MakeCategoryHeader($"{Constants.CategoryLabel(cat)}   ·   {members.Count}"));
                foreach (var (idx, e) in members)
                    _list.Items.Add(new ListBoxItem
                    {
                        Content = "     " + e.Name, Tag = idx,
                        ToolTip = string.IsNullOrWhiteSpace(e.Filename) ? LocalizationService.T(Str.Db_NoFilenameTooltip) : e.Filename,
                    });
            }
        }

        private static ListBoxItem MakeCategoryHeader(string text) => new()
        {
            Content = text,
            FontWeight = FontWeights.Bold,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["BrushBlue"],
            Background = (Brush)Application.Current.Resources["BrushCard"],
            Padding = new Thickness(8, 5, 8, 5),
            Margin = new Thickness(0, 6, 0, 2),
            IsHitTestVisible = false,   // Überschrift ist nicht auswählbar/klickbar
            Focusable = false,
        };

        private void AddBtn(Panel p, string label, RoutedEventHandler h, string style)
        {
            var btn = new Button { Content = label, Style = (Style)Application.Current.Resources[style], Margin = new Thickness(0, 0, 6, 0), Width = 110 };
            btn.Click += h;
            p.Children.Add(btn);
        }

        private int SelectedIndex() => _list.SelectedItem is ListBoxItem li && li.Tag is int i ? i : -1;

        // Wählt den ListBoxItem-Eintrag anhand des Roh-Index in _db.Entries aus (nicht per
        // ListBox-Position — durch die Kategorie-Überschriften stimmen beide seit der
        // Gruppierung nicht mehr überein).
        private void SelectByEntryIndex(int entryIndex)
        {
            foreach (var obj in _list.Items)
                if (obj is ListBoxItem li && li.Tag is int t && t == entryIndex) { _list.SelectedItem = li; li.BringIntoView(); return; }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var entry = new IsoEntry { Category = "Einsteiger" };
            var dlg = new IsoEditDialog(entry, isNew: true) { Owner = this };
            if (dlg.ShowDialog() != true) return;
            _db.Add(entry); AnyEntryAdded = true; FillList(); SelectByEntryIndex(_db.Count - 1);
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            int idx = SelectedIndex(); if (idx < 0) return;
            var dlg = new IsoEditDialog(_db.Entries[idx], isNew: false) { Owner = this };
            if (dlg.ShowDialog() == true) { FillList(); SelectByEntryIndex(idx); }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            int idx = SelectedIndex(); if (idx < 0) return;
            if (MessageBox.Show(string.Format(LocalizationService.T(Str.Db_DeleteEntryConfirm_Body), _db.Entries[idx].Name), LocalizationService.T(Str.Db_DeleteEntryConfirm_Title), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            _db.Remove(idx); FillList();
        }

        private void BtnUp_Click(object sender, RoutedEventArgs e)
        { int idx = SelectedIndex(); if (idx <= 0) return; _db.MoveUp(idx); FillList(); SelectByEntryIndex(idx - 1); }

        private void BtnDown_Click(object sender, RoutedEventArgs e)
        { int idx = SelectedIndex(); if (idx < 0 || idx >= _db.Count - 1) return; _db.MoveDown(idx); FillList(); SelectByEntryIndex(idx + 1); }
    }

    // ═══════════════════════════════════════════════════════════════════
    // IsoEditDialog
    // ═══════════════════════════════════════════════════════════════════
    public sealed class IsoEditDialog : Window
    {
        private readonly IsoEntry _entry;
        private readonly TextBox  _tbName, _tbUrl, _tbFilename,
                                  _tbMirror1, _tbMirror2, _tbMirror3,
                                  _tbGhRepo, _tbGhAsset, _tbTip, _tbTipEn;
        private readonly ComboBox _cbCat;

        public IsoEditDialog(IsoEntry entry, bool isNew)
        {
            _entry = entry;
            Title  = isNew ? LocalizationService.T(Str.Db_EditDialog_Title_New) : string.Format(LocalizationService.T(Str.Db_EditDialog_Title_Edit), entry.Name);
            // Höhe wächst mit dem Inhalt (alle Felder ohne Scrollen sichtbar), aber nie über die
            // verfügbare Bildschirmhöhe hinaus — der ScrollViewer im Content greift automatisch als
            // Fallback, sobald MaxHeight erreicht ist (z.B. auf kleinen/niedrig aufgelösten Displays).
            Width  = 620;
            SizeToContent = SizeToContent.Height;
            MaxHeight = SystemParameters.WorkArea.Height - 40;
            ResizeMode = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = (Brush)Application.Current.Resources["BrushBg"];

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var root   = new StackPanel { Margin = new Thickness(20) };

            _tbName    = AppRes.AddField(root, LocalizationService.T(Str.Db_Field_Name),        entry.Name);
            _cbCat     = AppRes.AddCategoryCombo(root, entry.Category);
            _tbUrl     = AppRes.AddField(root, LocalizationService.T(Str.Db_Field_PrimaryUrl),  entry.Url);
            _tbFilename= AppRes.AddField(root, LocalizationService.T(Str.Db_Field_Filename),    entry.Filename);
            _tbMirror1 = AppRes.AddField(root, LocalizationService.T(Str.Db_Field_Mirror1),     entry.Mirror1);
            _tbMirror2 = AppRes.AddField(root, LocalizationService.T(Str.Db_Field_Mirror2),     entry.Mirror2);
            _tbMirror3 = AppRes.AddField(root, LocalizationService.T(Str.Db_Field_Mirror3),     entry.Mirror3);
            _tbGhRepo  = AppRes.AddField(root, LocalizationService.T(Str.Db_Field_GithubRepo),  entry.GithubRepo);
            _tbGhAsset = AppRes.AddField(root, LocalizationService.T(Str.Db_Field_GithubAsset), entry.GithubAsset);
            _tbTip     = AppRes.AddField(root, LocalizationService.T(Str.Db_Field_Description), entry.Tip, multiLine: true);
            _tbTipEn   = AppRes.AddField(root, LocalizationService.T(Str.Db_Field_DescriptionEn), entry.TipEn, multiLine: true);

            var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            var ok = new Button { Content = LocalizationService.T(Str.Db_Btn_Save), Style = (Style)Application.Current.Resources["BtnPrimary"], Width = 110 };
            ok.Click += OkBtn_Click;
            var cancel = new Button { Content = LocalizationService.T(Str.Db_Btn_Cancel), Style = (Style)Application.Current.Resources["BtnGhost"], Width = 100, Margin = new Thickness(8, 0, 0, 0) };
            cancel.Click += (_, _) => DialogResult = false;
            btns.Children.Add(ok); btns.Children.Add(cancel);
            root.Children.Add(btns);
            scroll.Content = root;
            Content = scroll;
        }

        private void OkBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_tbName.Text) || string.IsNullOrWhiteSpace(_tbFilename.Text))
            { MessageBox.Show(LocalizationService.T(Str.Db_RequiredFields_Body), LocalizationService.T(Str.Db_RequiredFields_Title), MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            // BUGFIX: Bisher gab es keinerlei Prüfung, ob der Name bereits von einem ANDEREN Eintrag
            // verwendet wird. Zwei Einträge mit identischem Namen sorgten dafür, dass sowohl das
            // Download-Fortschrittsfenster als auch DownloadWorkers interne Mirror-Verfolgung (beide
            // nur nach Name schlüsseln) sie nicht mehr unterscheiden konnten — ein Klick auf den
            // "(schneller)"-Button einer Zeile konnte dadurch den Mirror-Wechsel der falschen, gleich
            // benannten Distro auslösen. Wird hier an der Quelle verhindert, statt an mehreren
            // Stellen im Download-Code nachträglich zu versuchen, gleiche Namen zu unterscheiden.
            string newName = _tbName.Text.Trim();
            bool nameTaken = IsoDatabaseService.Instance.Entries.Any(other =>
                !ReferenceEquals(other, _entry) && string.Equals(other.Name, newName, StringComparison.OrdinalIgnoreCase));
            if (nameTaken)
            {
                MessageBox.Show(string.Format(LocalizationService.T(Str.Db_NameTaken_Body), newName),
                    LocalizationService.T(Str.Db_NameTaken_Title), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _entry.Name = newName; _entry.Category = AppRes.SelectedCategory(_cbCat);
            _entry.Url = _tbUrl.Text.Trim(); _entry.Filename = _tbFilename.Text.Trim();
            _entry.Mirror1 = _tbMirror1.Text.Trim(); _entry.Mirror2 = _tbMirror2.Text.Trim(); _entry.Mirror3 = _tbMirror3.Text.Trim();
            _entry.GithubRepo = _tbGhRepo.Text.Trim(); _entry.GithubAsset = _tbGhAsset.Text.Trim();
            _entry.Tip = _tbTip.Text.Trim(); _entry.TipEn = _tbTipEn.Text.Trim(); DialogResult = true;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // IsoSearchDialog — 2 Reiter, beides Online-Abfragen gegen DistroWatch
    // ("Aktuellste"/"Beliebteste", nur Live-Medium-Distros — siehe DiscoveryService).
    // Die frühere lokale Textsuche entfällt hier bewusst (dafür gibt es "🗃 Datenbank" /
    // IsoListDialog). Neu übernommene Distros landen in AddedEntries (der Aufrufer fügt sie
    // zur DB hinzu); ist ToDownload nicht leer, sollen genau diese zusätzlich sofort
    // heruntergeladen werden.
    // ═══════════════════════════════════════════════════════════════════
    public sealed class IsoSearchDialog : Window
    {
        public List<IsoEntry> AddedEntries { get; } = new();
        public HashSet<IsoEntry> ToDownload { get; } = new();

        private sealed class DiscoveryRow
        {
            public required Grid             Row;
            public required CheckBox         Chk;
            public required ComboBox         CatCb;
            public required TextBlock        NameTb;
            public required DiscoveredDistro Distro;
        }

        private sealed class DiscoveryTab
        {
            public required StackPanel RowsPanel;
            public required TextBlock  StatusTb;
            public required CheckBox   AlsoDownloadChk;
            public readonly List<DiscoveryRow> Rows = new();
            public bool Loaded;
        }

        private readonly DiscoveryTab _latestTab  = MakeTabState();
        private readonly DiscoveryTab _popularTab = MakeTabState();

        private static DiscoveryTab MakeTabState() => new()
        {
            RowsPanel       = new StackPanel(),
            StatusTb        = new TextBlock { FontSize = 10.5, Foreground = (Brush)Application.Current.Resources["BrushDim"], Margin = new Thickness(0, 0, 0, 8) },
            AlsoDownloadChk = new CheckBox { Content = LocalizationService.T(Str.Db_Chk_DownloadImmediately), VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)Application.Current.Resources["BrushHeader"] },
        };

        public IsoSearchDialog()
        {
            Title = LocalizationService.T(Str.Db_SearchDialog_Title); Width = 640; Height = 580;
            ResizeMode = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = (Brush)Application.Current.Resources["BrushBg"];

            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var tabs = new TabControl { Background = (Brush)Application.Current.Resources["BrushTransparent"], BorderThickness = new Thickness(0) };
            tabs.Items.Add(BuildDiscoveryTab(LocalizationService.T(Str.Db_Tab_Latest), _latestTab, forceRefresh => DiscoveryService.Instance.GetLatestAdditionsAsync(forceRefresh)));
            tabs.Items.Add(BuildDiscoveryTab(LocalizationService.T(Str.Db_Tab_Popular), _popularTab, forceRefresh => DiscoveryService.Instance.GetMostPopularAsync(forceRefresh)));
            Grid.SetRow(tabs, 0);

            var closeBtn = new Button { Content = LocalizationService.T(Str.Db_Btn_CloseSimple), Style = (Style)Application.Current.Resources["BtnGhost"], Width = 100, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            closeBtn.Click += (_, _) => { DialogResult = true; Close(); };
            Grid.SetRow(closeBtn, 1);

            root.Children.Add(tabs); root.Children.Add(closeBtn);
            Content = root;

            // Erster Reiter lädt sofort beim Öffnen, der zweite erst bei seinem ersten Anklicken
            // (SelectionChanged) — kein unnötiger Netzwerk-Roundtrip, falls der Nutzer nur eine der
            // beiden Listen braucht.
            Loaded += async (_, _) => { if (!_latestTab.Loaded) await LoadDiscoveryTabAsync(_latestTab, forceRefresh: false, DiscoveryService.Instance.GetLatestAdditionsAsync); };
            tabs.SelectionChanged += async (_, _) =>
            {
                if (tabs.SelectedIndex == 1 && !_popularTab.Loaded) await LoadDiscoveryTabAsync(_popularTab, forceRefresh: false, DiscoveryService.Instance.GetMostPopularAsync);
            };
        }

        // ── Online-Abfrage (DistroWatch, nur Live-Medium-Distros) ───────────
        private TabItem BuildDiscoveryTab(string header, DiscoveryTab tab, Func<bool, Task<DiscoveryService.DiscoveryResult>> fetch)
        {
            var root = new Grid { Margin = new Thickness(12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var headerRow = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var refreshBtn = new Button { Content = LocalizationService.T(Str.Db_Btn_Refresh), Style = (Style)Application.Current.Resources["BtnGhost"], Width = 130 };
            DockPanel.SetDock(refreshBtn, Dock.Right);
            headerRow.Children.Add(refreshBtn);
            headerRow.Children.Add(tab.StatusTb);
            Grid.SetRow(headerRow, 0);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            scroll.Content = tab.RowsPanel;
            Grid.SetRow(scroll, 1);

            var footer = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
            var takeBtn = new Button { Content = LocalizationService.T(Str.Db_Btn_TakeOver), Style = (Style)Application.Current.Resources["BtnPrimary"], Width = 140 };
            DockPanel.SetDock(takeBtn, Dock.Right);
            takeBtn.Click += (_, _) => TakeSelected(tab);
            footer.Children.Add(takeBtn);
            footer.Children.Add(tab.AlsoDownloadChk);
            Grid.SetRow(footer, 2);

            refreshBtn.Click += async (_, _) => await LoadDiscoveryTabAsync(tab, forceRefresh: true, fetch);

            root.Children.Add(headerRow); root.Children.Add(scroll); root.Children.Add(footer);
            return new TabItem { Header = header, Content = root };
        }

        private async Task LoadDiscoveryTabAsync(DiscoveryTab tab, bool forceRefresh, Func<bool, Task<DiscoveryService.DiscoveryResult>> fetch)
        {
            tab.StatusTb.Text = LocalizationService.T(Str.Db_Loading);
            tab.RowsPanel.Children.Clear(); tab.Rows.Clear();
            try
            {
                var result = await fetch(forceRefresh).ConfigureAwait(true);
                tab.Loaded = true;
                if (result.Items.Count == 0)
                {
                    tab.StatusTb.Text = LocalizationService.T(Str.Db_NoDiscoveryResults);
                    return;
                }
                tab.StatusTb.Text = (result.FromCache ? LocalizationService.T(Str.Db_FromCache) : LocalizationService.T(Str.Db_FreshlyLoaded))
                    + string.Format(LocalizationService.T(Str.Db_DiscoveryStatusSuffix), result.FetchedAtUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm"));
                foreach (var d in result.Items)
                {
                    var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    ApplyRowHighlight(row, d);

                    var chk = new CheckBox { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 4, 4, 4), IsEnabled = !d.AlreadyInDb };
                    Grid.SetColumn(chk, 0); row.Children.Add(chk);

                    var nameTb = new TextBlock
                    {
                        Text = d.AlreadyInDb ? string.Format(LocalizationService.T(Str.Db_NameAlreadyInDb), d.Name) : d.Name,
                        VerticalAlignment = VerticalAlignment.Center, FontSize = 12, Margin = new Thickness(0, 4, 0, 4),
                        Foreground = (Brush)Application.Current.Resources[d.AlreadyInDb ? "BrushDim" : "BrushHeader"],
                        ToolTip = d.AlreadyInDb ? null : BuildInfoTooltip(d),
                    };
                    Grid.SetColumn(nameTb, 1); row.Children.Add(nameTb);

                    var previewBtn = new Button
                    {
                        Content = "🔍", Width = 26, Height = 24, Padding = new Thickness(0),
                        Style = (Style)Application.Current.Resources["BtnGhost"],
                        FontSize = 11, Margin = new Thickness(0, 2, 6, 2),
                        ToolTip = LocalizationService.T(Str.Preview_OpenTooltip),
                    };
                    previewBtn.Click += (_, _) => new DistroPreviewDialog(d.Name, d.Slug, d.Tags) { Owner = this }.ShowDialog();
                    Grid.SetColumn(previewBtn, 2); row.Children.Add(previewBtn);

                    var catCb = new ComboBox { Margin = new Thickness(6, 2, 6, 2), IsEnabled = !d.AlreadyInDb };
                    AppRes.FillCategoryCombo(catCb, d.SuggestedCategory);
                    Grid.SetColumn(catCb, 3); row.Children.Add(catCb);

                    var infoTb = new TextBlock { Text = d.Info, FontSize = 10.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 4, 4), Foreground = (Brush)Application.Current.Resources["BrushDim"] };
                    Grid.SetColumn(infoTb, 4); row.Children.Add(infoTb);

                    tab.RowsPanel.Children.Add(row);
                    tab.Rows.Add(new DiscoveryRow { Row = row, Chk = chk, CatCb = catCb, NameTb = nameTb, Distro = d });
                }
            }
            catch (Exception ex)
            {
                tab.StatusTb.Text = string.Format(LocalizationService.T(Str.Db_DiscoveryError), ex.Message);
            }
        }

        private void TakeSelected(DiscoveryTab tab)
        {
            bool alsoDownload = tab.AlsoDownloadChk.IsChecked == true;
            int taken = 0;
            foreach (var row in tab.Rows)
            {
                if (row.Distro.AlreadyInDb || row.Chk.IsChecked != true) continue;
                string category = AppRes.SelectedCategory(row.CatCb);
                var entry = new IsoEntry { Name = row.Distro.Name, Category = category };
                AddedEntries.Add(entry);
                if (alsoDownload) ToDownload.Add(entry);

                row.Distro.AlreadyInDb = true;
                row.Chk.IsChecked = false; row.Chk.IsEnabled = false; row.CatCb.IsEnabled = false;
                row.NameTb.Text = string.Format(LocalizationService.T(Str.Db_NameAlreadyInDb), row.Distro.Name);
                row.NameTb.Foreground = (Brush)Application.Current.Resources["BrushDim"];
                row.NameTb.ToolTip = null;
                ApplyRowHighlight(row.Row, row.Distro);
                taken++;
            }
            if (taken > 0) tab.StatusTb.Text = string.Format(LocalizationService.T(Str.Db_TakenOverStatus), taken, tab.StatusTb.Text);
        }

        // Bereits in der DB vorhandene Distros werden farblich hervorgehoben (dezenter Blauton,
        // wie bei anderen "informativen" — weder Erfolgs- noch Fehler- — Zuständen im restlichen
        // Programm), statt sich nur über den gedimmten Namenstext zu erschließen.
        private static void ApplyRowHighlight(Grid row, DiscoveredDistro d) =>
            row.Background = d.AlreadyInDb ? (Brush)Application.Current.Resources["BrushLBlue"] : (Brush)Application.Current.Resources["BrushTransparent"];

        private static string BuildInfoTooltip(DiscoveredDistro d)
        {
            var lines = new List<string> { d.Name, d.Info, string.Format(LocalizationService.T(Str.Db_SuggestedCategory), Constants.CategoryLabel(d.SuggestedCategory)) };
            if (d.Tags.Count > 0) lines.Add(string.Format(LocalizationService.T(Str.Db_DistrowatchTags), string.Join(", ", d.Tags)));
            lines.Add($"distrowatch.com/{d.Slug}");
            return string.Join("\n", lines);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // DistroPreviewDialog — Kurzvorschau (Screenshot, Kurzfakten, Beschreibung) für eine per
    // IsoSearchDialog gefundene Distro, bevor der Nutzer sie übernimmt/herunterlädt. Lädt
    // on-demand erst beim Öffnen (kein Vorab-Laden für die ganze Liste) über
    // DistroPreviewService. Rein informativ — keine Download-/Übernehmen-Aktion hier, das bleibt
    // bei der Checkbox + "Übernehmen"-Button in der Zeile dahinter.
    // ═══════════════════════════════════════════════════════════════════
    public sealed class DistroPreviewDialog : Window
    {
        private readonly string _name;
        private readonly string _slug;
        private readonly IReadOnlyList<string> _tags;
        private readonly StackPanel _contentPanel;

        public DistroPreviewDialog(string name, string slug, IReadOnlyList<string> tags)
        {
            _name = name; _slug = slug; _tags = tags;
            Title = string.Format(LocalizationService.T(Str.Preview_DialogTitle), name);
            Width = 380; SizeToContent = SizeToContent.Height; MaxHeight = 640;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = (Brush)Application.Current.Resources["BrushBg"];

            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 560 };
            _contentPanel = new StackPanel();
            _contentPanel.Children.Add(new TextBlock
            {
                Text = LocalizationService.T(Str.Db_Loading),
                Foreground = (Brush)Application.Current.Resources["BrushDim"],
                FontSize = 12, Margin = new Thickness(0, 24, 0, 24), HorizontalAlignment = HorizontalAlignment.Center,
            });
            scroll.Content = _contentPanel;
            Grid.SetRow(scroll, 0);

            var footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
            var closeBtn = new Button { Content = LocalizationService.T(Str.Db_Btn_CloseSimple), Style = (Style)Application.Current.Resources["BtnGhost"], Width = 110 };
            closeBtn.Click += (_, _) => Close();
            DockPanel.SetDock(closeBtn, Dock.Right);
            var openBtn = new Button { Content = LocalizationService.T(Str.Preview_OpenInBrowser), Style = (Style)Application.Current.Resources["BtnGhost"] };
            openBtn.Click += (_, _) =>
            {
                try { Process.Start(new ProcessStartInfo($"https://distrowatch.com/{_slug}") { UseShellExecute = true }); }
                catch { /* kein Absturz, falls kein Standardbrowser konfiguriert ist */ }
            };
            footer.Children.Add(closeBtn);
            footer.Children.Add(openBtn);
            Grid.SetRow(footer, 1);

            root.Children.Add(scroll); root.Children.Add(footer);
            Content = root;

            Loaded += async (_, _) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            var preview = await DistroPreviewService.Instance.GetPreviewAsync(_name, _slug).ConfigureAwait(true);
            _contentPanel.Children.Clear();

            if (preview is null)
            {
                _contentPanel.Children.Add(new TextBlock
                {
                    Text = string.Format(LocalizationService.T(Str.Preview_LoadError), _slug),
                    Foreground = (Brush)Application.Current.Resources["BrushRed"],
                    FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 20, 0, 20),
                });
                return;
            }

            if (!string.IsNullOrEmpty(preview.ScreenshotUrl))
            {
                byte[]? bytes = await HttpService.Instance.GetBytesAsync(preview.ScreenshotUrl).ConfigureAwait(true);
                var bmp = bytes is null ? null : LoadImage(bytes);
                if (bmp != null)
                    _contentPanel.Children.Add(new Image { Source = bmp, Height = 130, Stretch = Stretch.UniformToFill, Margin = new Thickness(0, 0, 0, 12) });
            }

            var factsGrid = new Grid();
            factsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            factsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            int row = 0;
            void AddFact(string label, string value)
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                factsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var lbl = new TextBlock { Text = label, FontSize = 11.5, Foreground = (Brush)Application.Current.Resources["BrushDim"], Margin = new Thickness(0, 2, 12, 2) };
                var val = new TextBlock { Text = value, FontSize = 11.5, Foreground = (Brush)Application.Current.Resources["BrushHeader"], Margin = new Thickness(0, 2, 0, 2), TextWrapping = TextWrapping.Wrap };
                Grid.SetRow(lbl, row); Grid.SetColumn(lbl, 0);
                Grid.SetRow(val, row); Grid.SetColumn(val, 1);
                factsGrid.Children.Add(lbl); factsGrid.Children.Add(val);
                row++;
            }
            AddFact(LocalizationService.T(Str.Preview_Label_BasedOn),      preview.BasedOn);
            AddFact(LocalizationService.T(Str.Preview_Label_Desktop),      preview.Desktop);
            AddFact(LocalizationService.T(Str.Preview_Label_Origin),       preview.Origin);
            AddFact(LocalizationService.T(Str.Preview_Label_Architecture), preview.Architecture);
            if (preview.IsActive.HasValue)
                AddFact(LocalizationService.T(Str.Preview_Label_Status),
                    LocalizationService.T(preview.IsActive.Value ? Str.Preview_Status_Active : Str.Preview_Status_Inactive));
            if (preview.PopularityRank > 0)
                AddFact(LocalizationService.T(Str.Preview_Label_Popularity),
                    string.Format(LocalizationService.T(Str.Preview_PopularityValue), preview.PopularityRank, preview.PopularityHitsPerDay));
            _contentPanel.Children.Add(factsGrid);

            if (!string.IsNullOrWhiteSpace(preview.Description))
            {
                _contentPanel.Children.Add(new Border
                {
                    Background = (Brush)Application.Current.Resources["BrushCard"],
                    BorderBrush = (Brush)Application.Current.Resources["BrushBorder"],
                    BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                    Margin = new Thickness(0, 12, 0, 12), Padding = new Thickness(10),
                    Child = new TextBlock
                    {
                        Text = preview.Description, FontSize = 11.5, TextWrapping = TextWrapping.Wrap,
                        Foreground = (Brush)Application.Current.Resources["BrushHeader"],
                    },
                });
            }

            if (_tags.Count > 0)
            {
                var tagsPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
                foreach (var tag in _tags)
                {
                    tagsPanel.Children.Add(new Border
                    {
                        Background = (Brush)Application.Current.Resources["BrushLBlue"],
                        CornerRadius = new CornerRadius(10),
                        Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(8, 3, 8, 3),
                        Child = new TextBlock { Text = tag, FontSize = 10, Foreground = (Brush)Application.Current.Resources["BrushHeader"] },
                    });
                }
                _contentPanel.Children.Add(tagsPanel);
            }
        }

        private static BitmapImage? LoadImage(byte[] bytes)
        {
            try
            {
                using var ms = new MemoryStream(bytes);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // ImportStickIsosDialog
    // ═══════════════════════════════════════════════════════════════════
    public sealed class ImportStickIsosDialog : Window
    {
        public List<(IsoEntry Entry, string SourcePath)> ImportedEntries { get; } = new();

        private sealed class ImportRow
        {
            public required CheckBox            Chk;
            public required TextBox             NameTb;
            public required ComboBox            CatCb;
            public required TextBox             UrlTb;
            public required UsbService.StickIso Iso;
        }

        private readonly List<ImportRow> _rows = new();

        public ImportStickIsosDialog(IReadOnlyList<UsbService.StickIso> unknownIsos)
        {
            Title = LocalizationService.T(Str.Db_ImportDialog_Title);
            Width = 660; Height = 520;
            ResizeMode = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = (Brush)Application.Current.Resources["BrushBg"];

            var root = new Grid { Margin = new Thickness(18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            root.Children.Add(new TextBlock
            {
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 14), FontSize = 12.5,
                Foreground = (Brush)Application.Current.Resources["BrushHeader"],
                Text = string.Format(LocalizationService.T(Str.Db_ImportDialog_Info), unknownIsos.Count),
            });

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var list   = new StackPanel();

            var colH = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            colH.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            colH.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            colH.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            void AddCH(int c, string t) { var tb = new TextBlock { Text = t, FontWeight = FontWeights.SemiBold, FontSize = 10.5, Foreground = (Brush)Application.Current.Resources["BrushMid"] }; Grid.SetColumn(tb, c); colH.Children.Add(tb); }
            AddCH(1, LocalizationService.T(Str.Db_ColHeader_NameEdit)); AddCH(2, LocalizationService.T(Str.Db_ColHeader_Category));
            list.Children.Add(colH);

            foreach (var iso in unknownIsos)
            {
                string suggested = Path.GetFileNameWithoutExtension(iso.Filename).Replace('-', ' ').Replace('_', ' ').Trim();
                var rg = new Grid { Margin = new Thickness(0, 4, 0, 4) };
                rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
                rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });

                var chk    = new CheckBox { IsChecked = true, VerticalAlignment = VerticalAlignment.Center, ToolTip = iso.Filename };
                var nameTb = new TextBox  { Text = suggested, MinHeight = 28, Padding = new Thickness(4, 3, 4, 3), Margin = new Thickness(4, 0, 4, 0) };
                string defCat = Constants.Categories.Contains(iso.Category, StringComparer.OrdinalIgnoreCase) ? Constants.Categories.First(c => c.Equals(iso.Category, StringComparison.OrdinalIgnoreCase)) : "Einsteiger";
                var catCb = new ComboBox { Margin = new Thickness(0) };
                AppRes.FillCategoryCombo(catCb, defCat);

                var urlTb = new TextBox { MinHeight = 24, FontSize = 10.5, Padding = new Thickness(4, 2, 4, 2), Margin = new Thickness(32, 2, 0, 6) };

                chk.Checked   += (_, _) => { nameTb.IsEnabled = catCb.IsEnabled = urlTb.IsEnabled = true; };
                chk.Unchecked += (_, _) => { nameTb.IsEnabled = catCb.IsEnabled = urlTb.IsEnabled = false; };

                Grid.SetColumn(chk, 0); Grid.SetColumn(nameTb, 1); Grid.SetColumn(catCb, 2);
                rg.Children.Add(chk); rg.Children.Add(nameTb); rg.Children.Add(catCb);
                list.Children.Add(rg);
                list.Children.Add(new TextBlock { Text = string.Format(LocalizationService.T(Str.Db_FileSizeLine), iso.Filename, (iso.Size / 1_073_741_824.0).ToString("F2")), FontSize = 9.5, Foreground = (Brush)Application.Current.Resources["BrushDim"], Margin = new Thickness(32, 0, 0, 0) });
                list.Children.Add(new TextBlock { Text = LocalizationService.T(Str.Db_SourceUrlLabel), FontSize = 9, Foreground = (Brush)Application.Current.Resources["BrushDim"], Margin = new Thickness(32, 4, 0, 0) });
                list.Children.Add(urlTb);
                list.Children.Add(new Border { Height = 1, Margin = new Thickness(0, 6, 0, 2), Background = (Brush)Application.Current.Resources["BrushBorder"] });
                _rows.Add(new ImportRow { Chk = chk, NameTb = nameTb, CatCb = catCb, UrlTb = urlTb, Iso = iso });
            }

            scroll.Content = list;
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);

            var br = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            var bAll  = new Button { Content = LocalizationService.T(Str.Db_Btn_SelectAll),   Style = (Style)Application.Current.Resources["BtnGhost"], Width = 130, Margin = new Thickness(0, 0, 6, 0) };
            bAll.Click  += (_, _) => { foreach (var r in _rows) { r.Chk.IsChecked = true;  r.NameTb.IsEnabled = r.CatCb.IsEnabled = true;  } };
            var bNone = new Button { Content = LocalizationService.T(Str.Db_Btn_DeselectAll),    Style = (Style)Application.Current.Resources["BtnGhost"], Width = 120, Margin = new Thickness(0, 0, 14, 0) };
            bNone.Click += (_, _) => { foreach (var r in _rows) { r.Chk.IsChecked = false; r.NameTb.IsEnabled = r.CatCb.IsEnabled = false; } };
            var bSkip = new Button { Content = LocalizationService.T(Str.Db_Btn_Skip),     Style = (Style)Application.Current.Resources["BtnGhost"], Width = 120, Margin = new Thickness(0, 0, 8, 0) };
            bSkip.Click += (_, _) => { DialogResult = false; Close(); };
            var bImp  = new Button { Content = LocalizationService.T(Str.Db_Btn_Import),   Style = (Style)Application.Current.Resources["BtnSuccess"], Width = 130 };
            bImp.Click += BtnImport_Click;
            br.Children.Add(bAll); br.Children.Add(bNone); br.Children.Add(bSkip); br.Children.Add(bImp);
            Grid.SetRow(br, 2);
            root.Children.Add(br);
            Content = root;
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var errors = new List<string>();
            foreach (var row in _rows)
            {
                if (row.Chk.IsChecked != true) continue;
                string name = row.NameTb.Text.Trim();
                if (string.IsNullOrWhiteSpace(name)) { errors.Add(string.Format(LocalizationService.T(Str.Db_ImportError_NoName), row.Iso.Filename)); continue; }
                var entry = new IsoEntry { Name = name, Category = AppRes.SelectedCategory(row.CatCb), Filename = row.Iso.Filename, Url = row.UrlTb.Text.Trim(), ImportedFromStick = true };
                ImportedEntries.Add((entry, row.Iso.FullPath));
            }
            if (errors.Count > 0) MessageBox.Show(string.Format(LocalizationService.T(Str.Db_ImportIncomplete_Body), string.Join("\n", errors)), LocalizationService.T(Str.Db_ImportIncomplete_Title), MessageBoxButton.OK, MessageBoxImage.Warning);
            DialogResult = ImportedEntries.Count > 0;
            Close();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // NewerVersionChoice
    // ═══════════════════════════════════════════════════════════════════
    public enum NewerVersionChoice
    {
        /// <summary>Bestehenden DB-Eintrag auf Stick-Version aktualisieren. Kein Duplikat.</summary>
        Replace,
        /// <summary>Neuen Eintrag anlegen. Bestehender bleibt erhalten.</summary>
        Add,
        /// <summary>Keine Änderung.</summary>
        Skip,
    }

    // ═══════════════════════════════════════════════════════════════════
    // NewerVersionOnStickDialog
    // ═══════════════════════════════════════════════════════════════════
    public sealed class NewerVersionOnStickDialog : Window
    {
        public List<(IsoEntry DbEntry, UsbService.StickIso StickIso, NewerVersionChoice Choice)>
            Results { get; } = new();

        private sealed class RowData
        {
            public required IsoEntry                 DbEntry;
            public required UsbService.StickIso      StickIso;
            public required RadioButton              RbReplace;
            public required RadioButton              RbAdd;
            public required RadioButton              RbSkip;
        }

        private readonly List<RowData> _rows = new();

        public NewerVersionOnStickDialog(
            IReadOnlyList<(IsoEntry DbEntry, UsbService.StickIso StickIso)> matches)
        {
            Title = LocalizationService.T(Str.Db_NewerVersionDialog_Title);
            Width = 700; Height = 560;
            ResizeMode = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = (Brush)Application.Current.Resources["BrushBg"];

            var root = new Grid { Margin = new Thickness(18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            root.Children.Add(new TextBlock
            {
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 14), FontSize = 12.5,
                Foreground = (Brush)Application.Current.Resources["BrushHeader"],
                Text = string.Format(LocalizationService.T(Str.Db_NewerVersionDialog_Info), matches.Count),
            });

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var panel  = new StackPanel();

            for (int i = 0; i < matches.Count; i++)
            {
                var (dbEntry, stickIso) = matches[i];
                string grp      = $"nvsg_{i}";
                string dbVer    = HttpService.ExtractVersion(dbEntry.Filename);
                string stickVer = HttpService.ExtractVersion(stickIso.Filename);

                var block = new Border
                {
                    BorderBrush = (Brush)Application.Current.Resources["BrushBorder"],
                    BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(14, 12, 14, 12), Margin = new Thickness(0, 0, 0, 12),
                    Background = (Brush)Application.Current.Resources["BrushCard"],
                };
                var inner = new StackPanel();

                inner.Children.Add(new TextBlock
                {
                    Text = string.Format(LocalizationService.T(Str.Db_CategoryNameHeader), dbEntry.Category, dbEntry.Name),
                    FontWeight = FontWeights.Bold, FontSize = 13, Margin = new Thickness(0, 0, 0, 10),
                    Foreground = (Brush)Application.Current.Resources["BrushHeader"],
                });

                // Versions-Gegenüberstellung
                var vg = new Grid { Margin = new Thickness(0, 0, 0, 12) };
                vg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(105) });
                vg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                vg.RowDefinitions.Add(new RowDefinition()); vg.RowDefinitions.Add(new RowDefinition());

                void AddVRow(int row, string lbl, string fn, string ver, bool newer)
                {
                    var l = new TextBlock { Text = lbl, FontSize = 11, Foreground = (Brush)Application.Current.Resources["BrushDim"], VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetRow(l, row); Grid.SetColumn(l, 0); vg.Children.Add(l);
                    var v = new TextBlock
                    {
                        Text = string.Format(LocalizationService.T(Str.Db_FilenameVersionLine), fn, ver), FontSize = 11,
                        FontWeight = newer ? FontWeights.SemiBold : FontWeights.Normal,
                        Foreground = newer ? (Brush)Application.Current.Resources["BrushBlue"] : (Brush)Application.Current.Resources["BrushMid"],
                        TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, ToolTip = fn,
                    };
                    Grid.SetRow(v, row); Grid.SetColumn(v, 1); vg.Children.Add(v);
                }
                AddVRow(0, LocalizationService.T(Str.Db_Label_Database), dbEntry.Filename,  dbVer,    false);
                AddVRow(1, LocalizationService.T(Str.Db_Label_OnStick),  stickIso.Filename, stickVer, true);
                inner.Children.Add(vg);

                var rbReplace = MakeRadio(LocalizationService.T(Str.Db_Radio_Replace), grp, true);
                var rbAdd     = MakeRadio(LocalizationService.T(Str.Db_Radio_Add), grp, false);
                var rbSkip    = MakeRadio(LocalizationService.T(Str.Db_Radio_Skip), grp, false);
                inner.Children.Add(rbReplace);
                inner.Children.Add(rbAdd);
                inner.Children.Add(rbSkip);
                block.Child = inner;
                panel.Children.Add(block);
                _rows.Add(new RowData { DbEntry = dbEntry, StickIso = stickIso, RbReplace = rbReplace, RbAdd = rbAdd, RbSkip = rbSkip });
            }

            scroll.Content = panel;
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);

            var br = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };

            var bAllR = new Button { Content = LocalizationService.T(Str.Db_Btn_ReplaceAll),    Style = (Style)Application.Current.Resources["BtnGhost"], Width = 120, Margin = new Thickness(0, 0, 6, 0), ToolTip = LocalizationService.T(Str.Db_Tooltip_ReplaceAll) };
            bAllR.Click += (_, _) => { foreach (var r in _rows) { r.RbReplace.IsChecked = true; r.RbAdd.IsChecked = false; r.RbSkip.IsChecked = false; } };
            var bAllS = new Button { Content = LocalizationService.T(Str.Db_Btn_SkipAll), Style = (Style)Application.Current.Resources["BtnGhost"], Width = 140, Margin = new Thickness(0, 0, 14, 0) };
            bAllS.Click += (_, _) => { foreach (var r in _rows) { r.RbSkip.IsChecked = true; r.RbReplace.IsChecked = false; r.RbAdd.IsChecked = false; } };
            var bCancel = new Button { Content = LocalizationService.T(Str.Db_Btn_Cancel), Style = (Style)Application.Current.Resources["BtnGhost"], Width = 100, Margin = new Thickness(0, 0, 8, 0) };
            bCancel.Click += (_, _) => { DialogResult = false; Close(); };
            var bOk = new Button { Content = LocalizationService.T(Str.Db_Btn_ApplySelection), Style = (Style)Application.Current.Resources["BtnPrimary"], Width = 160 };
            bOk.Click += BtnOk_Click;

            br.Children.Add(bAllR); br.Children.Add(bAllS); br.Children.Add(bCancel); br.Children.Add(bOk);
            Grid.SetRow(br, 2);
            root.Children.Add(br);
            Content = root;
        }

        /// <summary>
        /// Erzeugt einen RadioButton mit TextBlock als Content, da RadioButton
        /// kein eigenes TextWrapping-Property besitzt (CS0117).
        /// </summary>
        private static RadioButton MakeRadio(string text, string groupName, bool isChecked)
        {
            return new RadioButton
            {
                Content = new TextBlock
                {
                    Text         = text,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize     = 11.5,
                    Foreground   = (Brush)Application.Current.Resources["BrushHeader"],
                },
                GroupName = groupName,
                IsChecked = isChecked,
                Margin    = new Thickness(0, 3, 0, 3),
            };
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            foreach (var row in _rows)
            {
                NewerVersionChoice choice =
                    row.RbReplace.IsChecked == true ? NewerVersionChoice.Replace :
                    row.RbAdd.IsChecked     == true ? NewerVersionChoice.Add     :
                                                       NewerVersionChoice.Skip;
                Results.Add((row.DbEntry, row.StickIso, choice));
            }
            DialogResult = Results.Any(r => r.Choice != NewerVersionChoice.Skip);
            Close();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // DbHealthCheckDialog — Bericht: welche Distros sind online erreichbar
    // und erfolgreich ladbar? Macht Ausfälle sichtbar statt sie im
    // Protokoll untergehen zu lassen.
    // ═══════════════════════════════════════════════════════════════════
    public sealed class DbHealthCheckDialog : Window
    {
        public DbHealthCheckDialog(IReadOnlyList<VersionCheckEntryResult> results)
        {
            int failed = results.Count(r => !r.Resolved);
            Title = LocalizationService.T(Str.Db_HealthCheckDialog_Title);
            Width = 600; Height = 560;
            ResizeMode = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = (Brush)Application.Current.Resources["BrushBg"];

            var root = new Grid { Margin = new Thickness(18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            root.Children.Add(new TextBlock
            {
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 14), FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources[failed == 0 ? "BrushGreen" : "BrushRed"],
                Text = failed == 0
                    ? string.Format(LocalizationService.T(Str.Db_HealthCheck_AllReachable), results.Count)
                    : string.Format(LocalizationService.T(Str.Db_HealthCheck_SomeUnreachable), failed, results.Count),
            });

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var list   = new StackPanel();
            foreach (var r in results.OrderBy(r => r.Resolved).ThenBy(r => r.Name))
            {
                var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                row.Children.Add(new TextBlock { Text = r.Resolved ? "✅" : "❌", FontSize = 12 });
                var nameTb = new TextBlock
                {
                    Text = r.Name, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
                    Foreground = (Brush)Application.Current.Resources["BrushHeader"],
                };
                Grid.SetColumn(nameTb, 1); row.Children.Add(nameTb);
                var infoTb = new TextBlock
                {
                    Text = r.Resolved ? $"v{r.RemoteVersion}" : LocalizationService.T(Str.Log_Unreachable),
                    FontSize = 10.5, Margin = new Thickness(10, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center,
                    Foreground = (Brush)Application.Current.Resources[r.Resolved ? "BrushDim" : "BrushRed"],
                };
                Grid.SetColumn(infoTb, 2); row.Children.Add(infoTb);
                list.Children.Add(row);
            }
            scroll.Content = list;
            Grid.SetRow(scroll, 1); root.Children.Add(scroll);

            if (failed > 0)
            {
                var tip = new TextBlock
                {
                    FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0),
                    Foreground = (Brush)Application.Current.Resources["BrushDim"],
                    Text = LocalizationService.T(Str.Db_HealthCheck_Tip),
                };
                Grid.SetRow(tip, 2);
                root.Children.Add(tip);
            }

            var close = new Button { Content = LocalizationService.T(Str.Db_Btn_Close), Width = 130, Style = (Style)Application.Current.Resources["BtnPrimary"], HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            close.Click += (_, _) => Close();
            Grid.SetRow(close, 3);
            root.Children.Add(close);
            Content = root;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // GitHubTokenDialog — optionales Personal Access Token, hebt nur das
    // unauthentifizierte API-Limit (60/Std) auf 5000/Std an. Keinerlei
    // Berechtigungs-Scope nötig, ein "leeres"/Classic-Token ohne
    // angehakte Scopes reicht für öffentliche Repos.
    // ═══════════════════════════════════════════════════════════════════
    public sealed class GitHubTokenDialog : Window
    {
        public string Token { get; private set; } = string.Empty;

        public GitHubTokenDialog(string currentToken)
        {
            Title = LocalizationService.T(Str.Db_GitHubTokenDialog_Title);
            Width = 480; Height = 240;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = AppRes.Brush("BrushBg");

            var root = new StackPanel { Margin = new Thickness(20) };
            root.Children.Add(new TextBlock
            {
                Text = LocalizationService.T(Str.Db_GitHubTokenDialog_Description),
                TextWrapping = TextWrapping.Wrap, FontSize = 11.5, Margin = new Thickness(0, 0, 0, 14),
                Foreground = AppRes.Brush("BrushMid"),
            });
            var tb = new TextBox
            {
                Text = currentToken, Margin = new Thickness(0, 0, 0, 16),
                Padding = new Thickness(8, 6, 8, 6), FontFamily = new FontFamily("Consolas, Courier New"),
            };
            root.Children.Add(tb);

            var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var ok = new Button { Content = LocalizationService.T(Str.Db_Btn_Save), Style = (Style)Application.Current.Resources["BtnPrimary"], Width = 110 };
            ok.Click += (_, _) => { Token = tb.Text.Trim(); DialogResult = true; };
            var cancel = new Button { Content = LocalizationService.T(Str.Db_Btn_Cancel), Style = (Style)Application.Current.Resources["BtnGhost"], Width = 100, Margin = new Thickness(8, 0, 0, 0) };
            cancel.Click += (_, _) => DialogResult = false;
            btns.Children.Add(ok); btns.Children.Add(cancel);
            root.Children.Add(btns);
            Content = root;
        }
    }
}
