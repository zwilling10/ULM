# Manuelle Quellen-Suche Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ein neuer Zeilen-Button in der Hauptliste öffnet ein Fenster, in dem der Nutzer für einen einzelnen Distro-Eintrag manuell eine Download-URL eintragen ODER eine In-App-Suche starten kann, die entweder eigene Treffer zur Auswahl zeigt oder — bei null Treffern — den Standard-Browser mit vorausgefüllter Suche öffnet.

**Architecture:** MVVM (C#/WPF, .NET 8). Reine Erweiterung: eine neue `HttpService`-Methode für unbewertete Kandidaten-Sammlung, ein neues `Window` (`ManualSourceSearchDialog`), ein neuer Button in der bestehenden `EntryTemplate`-`DataTemplate` der Hauptliste. Wiederverwendet die bereits vorhandene Feld-Editier-UI aus `IsoEditDialog` (dafür in `AppRes` extrahiert) und die DuckDuckGo-Websuche-Logik aus `HttpService.ResolveViaWebSearchAsync`.

**Tech Stack:** C# 12 / .NET 8, WPF, xUnit 2.9.2.

## Global Constraints

- Build: `dotnet build`. Tests: `dotnet test ULM.Tests/ULM.Tests.csproj`. Baseline vor Task 1: **123 Tests, 0 Fehler** — muss nach jedem Task mindestens genauso hoch sein.
- Dieses Projekt hat **keine** automatisierten Tests für Dialog-Klassen (`Views/Dialogs/*.cs`) oder netzwerkabhängige `HttpService`-Methoden — durchgängige, bestehende Konvention (bestätigt: `IsoEditDialog`, `ResolveViaWebSearchAsync` & alle anderen Resolver haben keine Unit-Tests). Dieser Plan folgt derselben Konvention: Verifikation über Build-Erfolg + manuellen End-to-End-Test am Ende, nicht über neue Unit-Tests für UI/Netzwerk-Code.
- Deutsche, dem bestehenden Code entsprechende Kommentare und UI-Texte. Keine neuen Abhängigkeiten.
- Arbeitsverzeichnis: `C:\Users\zwill\Documents\C++ Projekt\Claude\ULM-features`, Branch `fix/stick-outdated-false-positive`.
- Bezug: `docs/superpowers/specs/2026-07-17-manual-source-search-design.md`.

---

### Task 1: Feld-Helfer `AddField`/`AddCategoryCombo` nach `AppRes` extrahieren

**Files:**
- Modify: `Views/Dialogs/DownloadDialogs.cs:18-22` (AppRes-Klasse erweitern)
- Modify: `Views/Dialogs/DatabaseDialogs.cs` (`IsoEditDialog`: private Helfer entfernen, 10 Aufrufstellen umstellen)

**Interfaces:**
- Produces: `AppRes.AddField(StackPanel root, string label, string value, bool multiLine = false) : TextBox`, `AppRes.AddCategoryCombo(StackPanel root, string selected) : ComboBox` — beide `internal static`, in `namespace ULM.Views.Dialogs`.

- [ ] **Step 1: `AppRes` in `Views/Dialogs/DownloadDialogs.cs` um die zwei Helfer erweitern**

Exakter aktueller Inhalt (Zeilen 18-22):
```csharp
    internal static class AppRes
    {
        public static Brush Brush(string key) => (Brush)Application.Current!.Resources[key];
        public static Style  Style(string key) => (Style)Application.Current!.Resources[key];
    }
```
Ersetzen durch:
```csharp
    internal static class AppRes
    {
        public static Brush Brush(string key) => (Brush)Application.Current!.Resources[key];
        public static Style  Style(string key) => (Style)Application.Current!.Resources[key];

        // Aus IsoEditDialog extrahiert (Views/Dialogs/DatabaseDialogs.cs) — zweiter Konsument ist
        // ManualSourceSearchDialog, das dieselben Bearbeiten-Felder braucht.
        public static TextBox AddField(StackPanel root, string label, string value, bool multiLine = false)
        {
            root.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 2), Foreground = Brush("BrushHeader") });
            var tb = new TextBox
            {
                Text = value, Margin = new Thickness(0, 0, 0, 10),
                MinHeight = multiLine ? 70 : 30, AcceptsReturn = multiLine,
                TextWrapping = multiLine ? TextWrapping.Wrap : TextWrapping.NoWrap,
                VerticalScrollBarVisibility = multiLine ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
            };
            root.Children.Add(tb);
            return tb;
        }

        public static ComboBox AddCategoryCombo(StackPanel root, string selected)
        {
            root.Children.Add(new TextBlock { Text = "Kategorie *", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 2), Foreground = Brush("BrushHeader") });
            var cb = new ComboBox { Margin = new Thickness(0, 0, 0, 10) };
            foreach (string cat in Constants.Categories) cb.Items.Add(cat);
            cb.SelectedItem = Constants.Categories.Contains(selected) ? selected : "Einsteiger";
            root.Children.Add(cb);
            return cb;
        }
    }
```

- [ ] **Step 2: `IsoEditDialog` in `Views/Dialogs/DatabaseDialogs.cs` auf `AppRes` umstellen**

Alter Text (die zwei privaten Methoden am Ende von `IsoEditDialog`, direkt nach `OkBtn_Click`):
```csharp
        private static TextBox AddField(StackPanel root, string label, string value, bool multiLine = false)
        {
            root.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 2), Foreground = (Brush)Application.Current.Resources["BrushHeader"] });
            var tb = new TextBox
            {
                Text = value, Margin = new Thickness(0, 0, 0, 10),
                MinHeight = multiLine ? 70 : 30, AcceptsReturn = multiLine,
                TextWrapping = multiLine ? TextWrapping.Wrap : TextWrapping.NoWrap,
                VerticalScrollBarVisibility = multiLine ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
            };
            root.Children.Add(tb);
            return tb;
        }

        private ComboBox AddCategoryCombo(StackPanel root, string selected)
        {
            root.Children.Add(new TextBlock { Text = "Kategorie *", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 2), Foreground = (Brush)Application.Current.Resources["BrushHeader"] });
            var cb = new ComboBox { Margin = new Thickness(0, 0, 0, 10) };
            foreach (string cat in Constants.Categories) cb.Items.Add(cat);
            cb.SelectedItem = Constants.Categories.Contains(selected) ? selected : "Einsteiger";
            root.Children.Add(cb);
            return cb;
        }
    }
```
Ersetzen durch (nur die schließende Klammer bleibt):
```csharp
    }
```

Dann alle 10 Aufrufe in `IsoEditDialog`'s Konstruktor umstellen — alter Text:
```csharp
            _tbName    = AddField(root, "Name *",          entry.Name);
            _cbCat     = AddCategoryCombo(root, entry.Category);
            _tbUrl     = AddField(root, "Primäre URL",     entry.Url);
            _tbFilename= AddField(root, "Dateiname *",     entry.Filename);
            _tbMirror1 = AddField(root, "Mirror 1",        entry.Mirror1);
            _tbMirror2 = AddField(root, "Mirror 2",        entry.Mirror2);
            _tbMirror3 = AddField(root, "Mirror 3",        entry.Mirror3);
            _tbGhRepo  = AddField(root, "GitHub Repo",     entry.GithubRepo);
            _tbGhAsset = AddField(root, "GitHub Asset",    entry.GithubAsset);
            _tbTip     = AddField(root, "Beschreibung",    entry.Tip, multiLine: true);
```
Neuer Text:
```csharp
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
```

- [ ] **Step 3: Build und Tests**

```bash
dotnet build
dotnet test ULM.Tests/ULM.Tests.csproj
```
Erwartet: Build ohne Fehler, **123 Tests, 0 Fehler** (reiner Refactor, keine neuen/entfernten Tests).

- [ ] **Step 4: Commit**

```bash
git add Views/Dialogs/DownloadDialogs.cs Views/Dialogs/DatabaseDialogs.cs
git commit -m "refactor: Feld-Helfer AddField/AddCategoryCombo nach AppRes extrahiert (zweiter Konsument folgt)"
```

---

### Task 2: `HttpService.SearchIsoLinksAsync` — unbewertete Kandidaten-Sammlung

**Files:**
- Modify: `Core/Services/HttpService.cs` (neue Methode + neuer Record, direkt nach `ResolveViaWebSearchAsync`)

**Interfaces:**
- Produces: `public sealed record IsoSearchHit(string Url, string Filename, string SourcePage)`, `public async Task<List<IsoSearchHit>> SearchIsoLinksAsync(string query)` auf `HttpService`.
- Consumes: bestehende private Helfer `GetStringAsync`, `ResolveDuckDuckGoRedirect`, `FindIsoLinksFollowingDownloadLinkAsync` (alle bereits in derselben Klasse vorhanden, keine Signaturänderung nötig).

- [ ] **Step 1: `IsoSearchHit` und `SearchIsoLinksAsync` einfügen**

Exaktes Ende von `ResolveViaWebSearchAsync` (alter Text, eindeutig in der Datei):
```csharp
                return await IsReachableAsync(url, 8).ConfigureAwait(false) ? (ExtractVersion(bestFname), url, bestFname) : Empty;
            }
            catch (Exception ex) { Debug.WriteLine($"[WebSearch] {entry.Name}: {ex.Message}"); }
            return Empty;
        }
```
Ersetzen durch (identischer Text plus die neue Methode direkt danach):
```csharp
                return await IsReachableAsync(url, 8).ConfigureAwait(false) ? (ExtractVersion(bestFname), url, bestFname) : Empty;
            }
            catch (Exception ex) { Debug.WriteLine($"[WebSearch] {entry.Name}: {ex.Message}"); }
            return Empty;
        }
        /// <summary>
        /// Sammelt ALLE über eine Websuche gefundenen .iso-Kandidaten für eine frei wählbare
        /// Suchanfrage und liefert sie unbewertet zurück — kein Bestmatch, keine Reachability-
        /// Prüfung, keine Mirror-Persistenz (im Unterschied zu ResolveViaWebSearchAsync). Grundlage
        /// für ManualSourceSearchDialog: der Nutzer entscheidet selbst, statt dass ULM automatisch
        /// den vermeintlich besten Treffer wählt. Nutzt dieselbe DuckDuckGo-Websuche wie
        /// ResolveViaWebSearchAsync — siehe dortige Doku zu Grenzen/Trefferqualität.
        /// </summary>
        public async Task<List<IsoSearchHit>> SearchIsoLinksAsync(string query)
        {
            var hits = new List<IsoSearchHit>();
            if (string.IsNullOrWhiteSpace(query)) return hits;
            try
            {
                string? html = await GetStringAsync($"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}", 15).ConfigureAwait(false);
                if (html is null) return hits;

                var resultPages = Regex.Matches(html, @"class=""result__a""[^>]*href=""([^""]+)""", RegexOptions.IgnoreCase)
                    .Cast<Match>().Select(m => ResolveDuckDuckGoRedirect(m.Groups[1].Value))
                    .Where(u => Uri.IsWellFormedUriString(u, UriKind.Absolute))
                    .Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList();

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string candidate in resultPages)
                {
                    if (hits.Count >= 10) break;
                    if (candidate.EndsWith(".iso", StringComparison.OrdinalIgnoreCase))
                    {
                        if (seen.Add(candidate)) hits.Add(new IsoSearchHit(candidate, Path.GetFileName(candidate.TrimEnd('/')), candidate));
                        continue;
                    }

                    string? pageHtml = await GetStringAsync(candidate, 12).ConfigureAwait(false);
                    if (pageHtml is null) continue;
                    var (foundLinks, foundOn, _) = await FindIsoLinksFollowingDownloadLinkAsync(candidate, pageHtml).ConfigureAwait(false);
                    foreach (string link in foundLinks)
                    {
                        string abs = link.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? link
                            : Uri.TryCreate(new Uri(foundOn), link, out var u) ? u.ToString() : foundOn.TrimEnd('/') + "/" + link.TrimStart('/');
                        if (seen.Add(abs)) hits.Add(new IsoSearchHit(abs, Path.GetFileName(abs.TrimEnd('/')), foundOn));
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[SearchIsoLinks] {query}: {ex.Message}"); }
            return hits;
        }

        public sealed record IsoSearchHit(string Url, string Filename, string SourcePage);
```

- [ ] **Step 2: Build**

```bash
dotnet build
```
Erwartet: 0 Fehler, 0 Warnungen. (Kein automatisierter Test — siehe Global Constraints; `SearchIsoLinksAsync` braucht echtes Netzwerk und wird in Task 4 manuell End-to-End getestet.)

- [ ] **Step 3: Commit**

```bash
git add Core/Services/HttpService.cs
git commit -m "feat: HttpService.SearchIsoLinksAsync fuer unbewertete manuelle ISO-Suche"
```

---

### Task 3: `ManualSourceSearchDialog` erstellen

**Files:**
- Create: `Views/Dialogs/ManualSourceSearchDialog.cs`

**Interfaces:**
- Consumes: `AppRes.AddField`/`AppRes.AddCategoryCombo` (Task 1), `HttpService.Instance.SearchIsoLinksAsync(string) : Task<List<IsoSearchHit>>` (Task 2), `IsoDatabaseService.Instance.Entries` (Namens-Duplikat-Check, gleiches Muster wie `IsoEditDialog`).
- Produces: `public sealed class ManualSourceSearchDialog : Window` mit Konstruktor `ManualSourceSearchDialog(IsoEntry entry)`, setzt bei „Speichern" `DialogResult = true` (Aufrufer speichert die DB, siehe Task 4).

- [ ] **Step 1: Datei anlegen**

```csharp
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
```

- [ ] **Step 2: Build**

```bash
dotnet build
```
Erwartet: 0 Fehler, 0 Warnungen.

- [ ] **Step 3: Commit**

```bash
git add Views/Dialogs/ManualSourceSearchDialog.cs
git commit -m "feat: ManualSourceSearchDialog fuer manuelle Quellen-Suche/-Eingabe"
```

---

### Task 4: Zeilen-Button in der Hauptliste verdrahten

**Files:**
- Modify: `Views/MainWindow.xaml` (Header-Grid-Spalten + `EntryTemplate`)
- Modify: `Views/MainWindow.xaml.cs` (neuer Click-Handler)

**Interfaces:**
- Consumes: `ManualSourceSearchDialog` (Task 3), `IsoEntryViewModel.Model : IsoEntry` (bereits vorhanden, siehe `ViewModels/IsoViewModels.cs:25`).

- [ ] **Step 1: Siebte Spalte im Spalten-Header ergänzen**

Alter Text (`Views/MainWindow.xaml`, Header-Grid der ISO-Auswahl-Tabelle):
```xml
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="36"/>
                                <ColumnDefinition Width="30"/>
                                <ColumnDefinition Width="388"/>
                                <ColumnDefinition Width="130"/>
                                <ColumnDefinition Width="150"/>
                                <ColumnDefinition Width="150"/>
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="1" Text="🔒" FontSize="10"
                                       HorizontalAlignment="Center"
                                       ToolTip="Hash-Status: grün = Prüfsumme vorhanden, rot = Integritätsprüfung fehlgeschlagen"
                                       Foreground="{DynamicResource BrushMid}"/>
```
Neuer Text (nur die `Grid.ColumnDefinitions` bekommen eine 7. Spalte, Rest unverändert):
```xml
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="36"/>
                                <ColumnDefinition Width="30"/>
                                <ColumnDefinition Width="388"/>
                                <ColumnDefinition Width="130"/>
                                <ColumnDefinition Width="150"/>
                                <ColumnDefinition Width="150"/>
                                <ColumnDefinition Width="30"/>
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="1" Text="🔒" FontSize="10"
                                       HorizontalAlignment="Center"
                                       ToolTip="Hash-Status: grün = Prüfsumme vorhanden, rot = Integritätsprüfung fehlgeschlagen"
                                       Foreground="{DynamicResource BrushMid}"/>
```

- [ ] **Step 2: Siebte Spalte + Button in `EntryTemplate` ergänzen**

Alter Text (`Views/MainWindow.xaml`, `EntryTemplate`, Zeilen 24-31 + 89-92):
```xml
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="36"/>
                        <ColumnDefinition Width="30"/>
                        <ColumnDefinition Width="388"/>
                        <ColumnDefinition Width="130"/>
                        <ColumnDefinition Width="150"/>
                        <ColumnDefinition Width="150"/>
                    </Grid.ColumnDefinitions>
```
Neuer Text:
```xml
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="36"/>
                        <ColumnDefinition Width="30"/>
                        <ColumnDefinition Width="388"/>
                        <ColumnDefinition Width="130"/>
                        <ColumnDefinition Width="150"/>
                        <ColumnDefinition Width="150"/>
                        <ColumnDefinition Width="30"/>
                    </Grid.ColumnDefinitions>
```

Und der alte Text am Ende des Grids (Zeilen 89-92):
```xml
                    <TextBlock Grid.Column="5" Text="{Binding VersionStatus}"
                               Foreground="{DynamicResource BrushMid}"
                               FontSize="11.5" VerticalAlignment="Center"/>
                </Grid>
```
Neuer Text (Button für Spalte 6 ergänzt):
```xml
                    <TextBlock Grid.Column="5" Text="{Binding VersionStatus}"
                               Foreground="{DynamicResource BrushMid}"
                               FontSize="11.5" VerticalAlignment="Center"/>

                    <Button Grid.Column="6" Content="🔧" FontSize="12"
                            Width="24" Height="24" Padding="0"
                            ToolTip="Quelle manuell suchen/eintragen"
                            Click="BtnManualSearch_Click"
                            Style="{DynamicResource BtnGhost}"
                            VerticalAlignment="Center" HorizontalAlignment="Center"/>
                </Grid>
```

- [ ] **Step 3: Click-Handler in `Views/MainWindow.xaml.cs` ergänzen**

Direkt nach `EntryRow_MouseLeftButtonDown` (siehe `Views/MainWindow.xaml.cs:535-540`) einfügen:
```csharp
        // Öffnet ManualSourceSearchDialog für genau die Zeile, in der der Button liegt — siehe
        // docs/superpowers/specs/2026-07-17-manual-source-search-design.md. Kein neues Auswahl-
        // Konzept nötig, da der Button direkt im DataContext (IsoEntryViewModel) der eigenen Zeile
        // sitzt (gleiches Muster wie EntryRow_MouseLeftButtonDown oben).
        private void BtnManualSearch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.DataContext is not IsoEntryViewModel vm) return;
            var dlg = new ManualSourceSearchDialog(vm.Model) { Owner = this };
            if (dlg.ShowDialog() != true) return;
            IsoDatabaseService.Instance.Save();
            _vm.RebuildTree();
        }
```

- [ ] **Step 4: Build und Tests**

```bash
dotnet build
dotnet test ULM.Tests/ULM.Tests.csproj
```
Erwartet: Build ohne Fehler, **123 Tests, 0 Fehler**.

- [ ] **Step 5: Manueller End-to-End-Test**

```bash
dotnet run --project UniversalLinuxManager.csproj
```
1. App starten, warten bis der Start-Versionscheck durch ist.
2. Bei einem Eintrag, der als „nicht erreichbar" markiert ist (z.B. dem Test-Fall „Shadowfetch Linux" aus der Testsitzung), auf den neuen 🔧-Button klicken.
3. Prüfen: Fenster öffnet sich, alle Felder sind vorausgefüllt, Suchfeld zeigt „{Name} iso".
4. Auf „🔍 Suchen" klicken — prüfen, dass entweder eine Trefferliste erscheint (Klick befüllt URL/Dateiname) oder bei null Treffern der Standard-Browser mit einer DuckDuckGo-Suche öffnet.
5. URL manuell eintragen (z.B. `https://www.shadowfetch.com/linux/download/shadowfetch-1.9.0-amd64.iso`), „Speichern" klicken.
6. Prüfen: Fenster schließt, `_vm.RebuildTree()` lief (kein Absturz), und ein anschließendes „🌐 URLs prüfen" zeigt den Eintrag jetzt als ✓ erreichbar.

- [ ] **Step 6: Commit**

```bash
git add Views/MainWindow.xaml Views/MainWindow.xaml.cs
git commit -m "feat: Zeilen-Button oeffnet manuelle Quellen-Suche pro Distro-Eintrag"
```

---

## Abschluss

Nach allen vier Tasks: `dotnet build` fehlerfrei, `dotnet test ULM.Tests/ULM.Tests.csproj` zeigt weiterhin **123 Tests, 0 Fehler** (dieser Plan fügt bewusst keine neuen Tests hinzu — siehe Global Constraints zur bestehenden Konvention bei Dialog-/Netzwerk-Code). Der manuelle End-to-End-Test in Task 4 Step 5 ist die eigentliche Abnahme dieses Features.
