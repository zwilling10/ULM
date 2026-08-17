# Distro-Vorschau-Popup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** In "🔍 ISO suchen" (`IsoSearchDialog`, Reiter "Aktuellste"/"Beliebteste") bekommt jede
Zeile ein neues 🔍-Icon, das ein Popup mit Screenshot, Kurzfakten (Basiert auf, Desktop, Herkunft,
Architektur, Status, Popularität), Beschreibungstext und Tags öffnet — der Nutzer kann sich die
Distro ansehen, ohne die App zu verlassen oder sie herunterzuladen.

**Architecture:** Neue `DistroPreviewService` (Singleton, analog `DiscoveryService`) lädt on-demand
die DistroWatch-Profilseite und parst sie strukturell über die sprachneutralen `href`-Query-
Parameter (nicht über sichtbaren, immer-Deutsch gelieferten Label-Text). Neues Model
`DistroPreview`. Neuer `HttpService.GetBytesAsync` für den Screenshot-Download. Neuer
`DistroPreviewDialog` (WPF-Fenster, reiner C#-Aufbau wie alle bestehenden Dialoge in
`DatabaseDialogs.cs`) rendert das Ergebnis.

**Tech Stack:** .NET 8 / WPF (`net8.0-windows`), bestehendes `HttpService`/`LocalizationService`-
Muster, xUnit für Tests (`ULM.Tests`).

## Global Constraints

- Kein neues NuGet-Paket, keine WebView2-Abhängigkeit (Design-Entscheidung, siehe Spec).
- Keine Änderung an der Haupt-ISO-Datenbank (`IsoListDialog`) — nur `IsoSearchDialog`.
- Kein Download/Übernehmen-Button im Popup — rein informativ.
- Alle neuen sichtbaren Texte über `Str.*`/`LocalizationService.T(...)`, DE + EN, kein
  hartkodierter Text — insbesondere keine von DistroWatch selbst gelieferten deutschen
  Label-/Status-Fragmente (deshalb strukturelles Parsen über `href`-Parameter, nicht über Text).
- Bestehende Tests (aktuell 242) müssen nach jedem Task weiterhin grün sein.
- Vollständige Spec: `docs/superpowers/specs/2026-08-17-distro-preview-popup-design.md`.

---

### Task 1: Model + HttpService.GetBytesAsync

**Files:**
- Create: `Core/Models/DistroPreview.cs`
- Modify: `Core/Services/HttpService.cs:194-208` (direkt nach `GetStringAsync` einfügen)

**Interfaces:**
- Produces: `ULM.Core.Models.DistroPreview` (sealed class, `init`-Properties: `Name` (required
  string), `Description`/`BasedOn`/`Origin`/`Architecture`/`Desktop` (string, default
  `string.Empty`), `IsActive` (bool?), `PopularityRank`/`PopularityHitsPerDay` (int),
  `ScreenshotUrl` (string?)).
- Produces: `HttpService.GetBytesAsync(string url, int timeoutSeconds = 10) → Task<byte[]?>`.

Kein Test in diesem Task — reine Datenklasse (wie `DiscoveredDistro`, ebenfalls ungetestet) und
eine kleine HTTP-Hilfsmethode im exakt gleichen, bereits etablierten Fehlerbehandlungs-Muster wie
`GetStringAsync` direkt darüber (auch `GetStringAsync` selbst ist nicht separat unit-getestet —
echte Netzwerkabhängigkeit, siehe Spec-Abschnitt "Testing").

- [ ] **Step 1: `Core/Models/DistroPreview.cs` anlegen**

```csharp
// Core/Models/DistroPreview.cs
namespace ULM.Core.Models
{
    /// <summary>
    /// Ergebnis von DistroPreviewService.GetPreviewAsync — Kurzfakten + Beschreibung + Screenshot-
    /// URL für das Vorschau-Popup in IsoSearchDialog (DistroPreviewDialog). Felder werden
    /// strukturell aus der DistroWatch-Profilseite geparst (href-Query-Parameter, NICHT die
    /// sichtbaren deutschen Labels — siehe DistroPreviewService), damit das Popup unabhängig vom
    /// ULM-Sprachmodus korrekt bleibt.
    /// </summary>
    public sealed class DistroPreview
    {
        public required string Name         { get; init; }
        public string  Description          { get; init; } = string.Empty;
        public string  BasedOn              { get; init; } = string.Empty;
        public string  Origin               { get; init; } = string.Empty;
        public string  Architecture         { get; init; } = string.Empty;
        public string  Desktop              { get; init; } = string.Empty;
        public bool?   IsActive             { get; init; }
        public int     PopularityRank       { get; init; }
        public int     PopularityHitsPerDay { get; init; }
        public string? ScreenshotUrl        { get; init; }
    }
}
```

- [ ] **Step 2: `HttpService.GetBytesAsync` einfügen**

In `Core/Services/HttpService.cs` direkt nach der bestehenden `GetStringAsync`-Methode (endet mit
`catch (Exception ex) { Debug.WriteLine($"[GetString] {url}: {ex.Message}"); return null; } }`)
einfügen:

```csharp
        public async Task<byte[]?> GetBytesAsync(string url, int timeoutSeconds = 10)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                return await _client.GetByteArrayAsync(url, cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex) { Debug.WriteLine($"[GetBytes] {url}: {ex.Message}"); return null; }
        }
```

- [ ] **Step 3: Build prüfen**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: `0 Fehler, 0 Warnungen`

- [ ] **Step 4: Commit**

```bash
git add Core/Models/DistroPreview.cs Core/Services/HttpService.cs
git commit -m "feat: DistroPreview-Model + HttpService.GetBytesAsync"
```

---

### Task 2: DistroPreviewService (Parsing, TDD)

**Files:**
- Create: `Core/Services/DistroPreviewService.cs`
- Test: `ULM.Tests/DistroPreviewServiceTests.cs`

**Interfaces:**
- Consumes: `ULM.Core.Models.DistroPreview` (Task 1), `HttpService.Instance.GetStringAsync(string)`
  (bestehend).
- Produces: `DistroPreviewService.Instance` (Singleton), `internal static DistroPreview
  ParseProfileHtml(string name, string slug, string html)` (für Tests sichtbar via
  `InternalsVisibleTo("ULM.Tests")`, bereits im Projekt konfiguriert), `public async Task<
  DistroPreview?> GetPreviewAsync(string name, string slug)`.

- [ ] **Step 1: Test-Fixture + fehlschlagenden Test schreiben**

`ULM.Tests/DistroPreviewServiceTests.cs`:

```csharp
// ULM.Tests/DistroPreviewServiceTests.cs
using ULM.Core.Services;
using Xunit;

namespace ULM.Tests
{
    /// <summary>
    /// Testet DistroPreviewService.ParseProfileHtml gegen ein reales, live abgerufenes
    /// DistroWatch-Profilseiten-Fixture (ThorOS, 2026-08-17) — kein echter Netzwerkaufruf.
    /// ParseProfileHtml ist internal, für dieses Testprojekt sichtbar via InternalsVisibleTo
    /// (siehe UniversalLinuxManager.csproj).
    /// </summary>
    public class DistroPreviewServiceTests
    {
        // Trimmter, aber strukturell exakter Ausschnitt einer echten DistroWatch-Profilseite.
        // Enthält bewusst deutsche Sidebar-Labels ("Basiert auf:", "Status:" etc.) — die dürfen
        // vom Parser NICHT gelesen werden, nur die href-Query-Parameter (basedon=, origin=, ...).
        private const string SampleHtml = """
            <img src="images/icon-large/thoros.png" border="0" title="ThorOS" vspace="23" hspace="32" align="left">
            <a href="images/slinks/thoros.png"><img src="images/slinks/thoros-small.png" border="0" title="ThorOS" vspace="6" hspace="6" align="right" style="width: 100%; max-width: 480px;"></a>
            <ul><li><b>Betriebssystem-Typ:</b> <a href="search.php?ostype=Linux#simple">Linux</a><br></li><li><b>Basiert auf:</b> <a href="search.php?basedon=Debian (Stable)#simple">Debian (Stable)</a><br></li><li><b>Herkunft:</b> <a href="search.php?origin=USA#simple">USA</a>
            <br></li><li><b>Architektur:</b> <a href="search.php?architecture=x86_64#simple">x86_64</a><br></li><li><b>Desktop:</b> <a href="search.php?desktop=GNOME#simple">GNOME</a><br></li><li><b>Kategorie:</b> <a href="search.php?category=Desktop#simple">Desktop</a>, <a href="search.php?category=Large+Language+Model#simple">Large Language Model</a>, <a href="search.php?category=Live+Medium#simple">Live Medium</a><br></li><li><b>Status:</b> <font color="green">Aktiv</font><br></li><li><b>Popularität:</b> <a href="dwres.php?resource=popularity">488 (18 Treffer pro Tag)</a>
            </li></ul>
            ThorOS is a Debian-based desktop Linux distribution featuring the GNOME desktop. Its principal feature is "voice control".
                <br><br>
            <b><a href="dwres.php?resource=popularity">Popularität</a></b>
            """;

        [Fact]
        public void ParseProfileHtml_ExtractsAllFactsFromRealPageStructure()
        {
            var result = DistroPreviewService.ParseProfileHtml("ThorOS", "thoros", SampleHtml);

            Assert.Equal("ThorOS", result.Name);
            Assert.Equal("Debian (Stable)", result.BasedOn);
            Assert.Equal("USA", result.Origin);
            Assert.Equal("x86_64", result.Architecture);
            Assert.Equal("GNOME", result.Desktop);
            Assert.True(result.IsActive);
            Assert.Equal(488, result.PopularityRank);
            Assert.Equal(18, result.PopularityHitsPerDay);
            Assert.StartsWith("ThorOS is a Debian-based desktop Linux distribution", result.Description);
            Assert.DoesNotContain("<br", result.Description);
        }

        [Fact]
        public void ParseProfileHtml_InactiveStatus_ColorNotGreen()
        {
            string html = SampleHtml.Replace("""<font color="green">Aktiv</font>""", """<font color="red">Eingestellt</font>""");
            var result = DistroPreviewService.ParseProfileHtml("ThorOS", "thoros", html);
            Assert.False(result.IsActive);
        }

        [Fact]
        public void ParseProfileHtml_MissingStatusTag_IsActiveNull()
        {
            string html = SampleHtml.Replace("""<li><b>Status:</b> <font color="green">Aktiv</font><br></li>""", "");
            var result = DistroPreviewService.ParseProfileHtml("ThorOS", "thoros", html);
            Assert.Null(result.IsActive);
        }

        [Fact]
        public void ParseProfileHtml_MissingFact_ReturnsEmptyStringNotNull()
        {
            string html = SampleHtml.Replace("""<li><b>Herkunft:</b> <a href="search.php?origin=USA#simple">USA</a>""", "");
            var result = DistroPreviewService.ParseProfileHtml("ThorOS", "thoros", html);
            Assert.Equal(string.Empty, result.Origin);
        }
    }
}
```

- [ ] **Step 2: Test ausführen, Fehlschlag bestätigen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj --filter DistroPreviewServiceTests`
Expected: FAIL — `DistroPreviewService` existiert noch nicht (Compiler-Fehler CS0246).

- [ ] **Step 3: `DistroPreviewService` implementieren**

```csharp
// Core/Services/DistroPreviewService.cs
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ULM.Core.Models;

namespace ULM.Core.Services
{
    /// <summary>
    /// Lädt on-demand (nur beim Klick auf das 🔍-Icon in IsoSearchDialog, nicht beim Laden der
    /// ganzen Liste) die DistroWatch-Profilseite einer Distro und extrahiert Kurzfakten +
    /// Beschreibung für DistroPreviewDialog. WICHTIG: HttpService schickt immer
    /// Accept-Language: de-DE — die sichtbaren Sidebar-Labels UND der "Status"-Text auf der
    /// DistroWatch-Seite sind daher IMMER Deutsch, unabhängig vom ULM-Sprachmodus. Deshalb wird
    /// hier NICHT über den sichtbaren Label-Text geparst, sondern über die sprachneutralen
    /// href-Query-Parameter-Namen (?basedon=, ?origin=, ...) bzw. die Status-Farbe statt des
    /// Status-Texts — die im Dialog gezeigten Labels/Werte kommen komplett aus
    /// LocalizationService.T(...).
    /// </summary>
    public sealed class DistroPreviewService
    {
        private static readonly Lazy<DistroPreviewService> _lazy = new(() => new DistroPreviewService());
        public static DistroPreviewService Instance => _lazy.Value;

        private DistroPreviewService() { }

        public async Task<DistroPreview?> GetPreviewAsync(string name, string slug)
        {
            string? html = await HttpService.Instance.GetStringAsync($"https://distrowatch.com/{slug}").ConfigureAwait(false);
            if (html is null) return null;
            return ParseProfileHtml(name, slug, html);
        }

        internal static DistroPreview ParseProfileHtml(string name, string slug, string html)
        {
            string basedOn      = ExtractFirst(html, @"search\.php\?basedon=([^""#]+)#simple") ?? string.Empty;
            string origin        = ExtractFirst(html, @"search\.php\?origin=([^""#]+)#simple") ?? string.Empty;
            string architecture  = ExtractFirst(html, @"search\.php\?architecture=([^""#]+)#simple") ?? string.Empty;
            string desktop       = ExtractFirst(html, @"search\.php\?desktop=([^""#]+)#simple") ?? string.Empty;

            bool? isActive = null;
            var statusMatch = Regex.Match(html, @"<font color=""([^""]+)"">");
            if (statusMatch.Success)
                isActive = statusMatch.Groups[1].Value.Equals("green", StringComparison.OrdinalIgnoreCase);

            int rank = 0, hits = 0;
            var popMatch = Regex.Match(html, @"resource=popularity"">(\d+)\s*\((\d+)");
            if (popMatch.Success)
            {
                int.TryParse(popMatch.Groups[1].Value, out rank);
                int.TryParse(popMatch.Groups[2].Value, out hits);
            }

            string description = string.Empty;
            var descMatch = Regex.Match(html, @"</ul>\s*(.*?)\s*<br><br>", RegexOptions.Singleline);
            if (descMatch.Success)
                description = System.Net.WebUtility.HtmlDecode(descMatch.Groups[1].Value).Trim();

            return new DistroPreview
            {
                Name                 = name,
                Description          = description,
                BasedOn              = basedOn,
                Origin               = origin,
                Architecture         = architecture,
                Desktop              = desktop,
                IsActive             = isActive,
                PopularityRank       = rank,
                PopularityHitsPerDay = hits,
                ScreenshotUrl        = $"https://distrowatch.com/images/slinks/{slug}-small.png",
            };
        }

        private static string? ExtractFirst(string html, string pattern)
        {
            var m = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
            return m.Success ? System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim() : null;
        }
    }
}
```

- [ ] **Step 4: Tests ausführen, Erfolg bestätigen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj --filter DistroPreviewServiceTests`
Expected: PASS — 4 von 4 Tests grün.

- [ ] **Step 5: Volle Testsuite + Build prüfen**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug && dotnet test ULM.Tests/ULM.Tests.csproj`
Expected: `0 Fehler, 0 Warnungen`, alle Tests grün (242 bestehende + 4 neue = 246).

- [ ] **Step 6: Commit**

```bash
git add Core/Services/DistroPreviewService.cs ULM.Tests/DistroPreviewServiceTests.cs
git commit -m "feat: DistroPreviewService mit strukturellem HTML-Parsing (TDD)"
```

---

### Task 3: Lokalisierung (Str.cs + LocalizationService.cs)

**Files:**
- Modify: `Infrastructure/Str.cs:439-442` (neue Konstanten in der `Db_*`-Gruppe ergänzen)
- Modify: `Infrastructure/LocalizationService.cs:897-906` (Deutsch) und `:1910-1919` (Englisch)

**Interfaces:**
- Produces: neue `Str`-Enum-Werte `Preview_DialogTitle`, `Preview_OpenTooltip`,
  `Preview_LoadError`, `Preview_Label_BasedOn`, `Preview_Label_Origin`,
  `Preview_Label_Architecture`, `Preview_Label_Desktop`, `Preview_Label_Status`,
  `Preview_Status_Active`, `Preview_Status_Inactive`, `Preview_Label_Popularity`,
  `Preview_PopularityValue`, `Preview_OpenInBrowser`. (`Db_Loading`, `Db_Btn_CloseSimple`
  werden für Ladezustand/Schließen-Button wiederverwendet, keine neuen Werte nötig.)

Kein Test nötig (reine Ressourcen-Dictionaries, wie bei jeder vorherigen Lokalisierungs-Phase in
diesem Projekt — Verifikation über Build + spätere manuelle Sprachumschaltung).

- [ ] **Step 1: Neue `Str`-Konstanten ergänzen**

In `Infrastructure/Str.cs`, Zeile mit
`Db_FromCache, Db_FreshlyLoaded, Db_DiscoveryStatusSuffix, Db_NameAlreadyInDb,` bis
`Db_SuggestedCategory, Db_DistrowatchTags, Db_TakenOverStatus, Db_DiscoveryError,` — direkt danach
eine neue Zeile einfügen:

```csharp
        Db_SuggestedCategory, Db_DistrowatchTags, Db_TakenOverStatus, Db_DiscoveryError,

        Preview_DialogTitle, Preview_OpenTooltip, Preview_LoadError, Preview_Label_BasedOn,
        Preview_Label_Origin, Preview_Label_Architecture, Preview_Label_Desktop,
        Preview_Label_Status, Preview_Status_Active, Preview_Status_Inactive,
        Preview_Label_Popularity, Preview_PopularityValue, Preview_OpenInBrowser,
```

(Die bestehende `Db_ImportDialog_Title, ...`-Zeile direkt danach bleibt unverändert stehen.)

- [ ] **Step 2: Deutsche Übersetzungen ergänzen**

In `Infrastructure/LocalizationService.cs`, nach der Zeile
`[Str.Db_DiscoveryError]         = "⚠ Fehler: {0}",` einfügen:

```csharp
            [Str.Db_DiscoveryError]         = "⚠ Fehler: {0}",

            [Str.Preview_DialogTitle]       = "Vorschau: {0}",
            [Str.Preview_OpenTooltip]       = "Vorschau anzeigen",
            [Str.Preview_LoadError]         = "⚠ Vorschau konnte nicht geladen werden ({0}).",
            [Str.Preview_Label_BasedOn]     = "Basiert auf",
            [Str.Preview_Label_Origin]      = "Herkunft",
            [Str.Preview_Label_Architecture]= "Architektur",
            [Str.Preview_Label_Desktop]     = "Desktop",
            [Str.Preview_Label_Status]      = "Status",
            [Str.Preview_Status_Active]     = "Aktiv",
            [Str.Preview_Status_Inactive]   = "Eingestellt",
            [Str.Preview_Label_Popularity]  = "Popularität",
            [Str.Preview_PopularityValue]   = "Rang {0} · {1} Treffer/Tag",
            [Str.Preview_OpenInBrowser]     = "🔗 DistroWatch-Seite öffnen",
```

- [ ] **Step 3: Englische Übersetzungen ergänzen**

In `Infrastructure/LocalizationService.cs`, nach der Zeile
`[Str.Db_DiscoveryError]         = "⚠ Error: {0}",` einfügen:

```csharp
            [Str.Db_DiscoveryError]         = "⚠ Error: {0}",

            [Str.Preview_DialogTitle]       = "Preview: {0}",
            [Str.Preview_OpenTooltip]       = "Show preview",
            [Str.Preview_LoadError]         = "⚠ Could not load preview ({0}).",
            [Str.Preview_Label_BasedOn]     = "Based on",
            [Str.Preview_Label_Origin]      = "Origin",
            [Str.Preview_Label_Architecture]= "Architecture",
            [Str.Preview_Label_Desktop]     = "Desktop",
            [Str.Preview_Label_Status]      = "Status",
            [Str.Preview_Status_Active]     = "Active",
            [Str.Preview_Status_Inactive]   = "Discontinued",
            [Str.Preview_Label_Popularity]  = "Popularity",
            [Str.Preview_PopularityValue]   = "Rank {0} · {1} hits/day",
            [Str.Preview_OpenInBrowser]     = "🔗 Open DistroWatch page",
```

- [ ] **Step 4: Build prüfen**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: `0 Fehler, 0 Warnungen` (jeder `Str`-Wert ohne Eintrag in BEIDEN Dictionaries wirft zur
Laufzeit, nicht beim Build — deshalb in Task 6 zusätzlich manuell in beiden Sprachen prüfen).

- [ ] **Step 5: Commit**

```bash
git add Infrastructure/Str.cs Infrastructure/LocalizationService.cs
git commit -m "feat: Lokalisierungs-Strings für Distro-Vorschau-Popup (DE/EN)"
```

---

### Task 4: DistroPreviewDialog (UI)

**Files:**
- Modify: `Views/Dialogs/DatabaseDialogs.cs` — neue `using`-Zeilen + neue Klasse `DistroPreviewDialog`
  direkt nach dem Ende der bestehenden `IsoSearchDialog`-Klasse (nach der schließenden `}` von
  `BuildInfoTooltip`, vor dem Kommentarblock `// ImportStickIsosDialog`).

**Interfaces:**
- Consumes: `DistroPreviewService.Instance.GetPreviewAsync(string, string)` (Task 2),
  `HttpService.Instance.GetBytesAsync(string)` (Task 1), alle `Str.Preview_*`-Werte (Task 3).
- Produces: `public sealed class DistroPreviewDialog : Window`, Konstruktor
  `DistroPreviewDialog(string name, string slug, IReadOnlyList<string> tags)`.

Kein separater Unit-Test (WPF-Fenster, wie jeder andere Dialog in diesem Projekt ungetestet — siehe
Spec-Abschnitt "Testing"). Verifikation über Build + manuellen Test in Task 6.

- [ ] **Step 1: Neue `using`-Direktiven ergänzen**

In `Views/Dialogs/DatabaseDialogs.cs`, Zeile 3-14 (bestehender `using`-Block) erweitern:

```csharp
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
```

(Nur `System.Diagnostics` und `System.Windows.Media.Imaging` sind neu, Rest bleibt wie bisher.)

- [ ] **Step 2: `DistroPreviewDialog`-Klasse einfügen**

Direkt nach der schließenden `}` der `BuildInfoTooltip`-Methode und der schließenden `}` der
`IsoSearchDialog`-Klasse selbst (vor dem Kommentarblock `// ImportStickIsosDialog`) einfügen:

```csharp
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
```

- [ ] **Step 3: Build prüfen**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: `0 Fehler, 0 Warnungen`

- [ ] **Step 4: Commit**

```bash
git add Views/Dialogs/DatabaseDialogs.cs
git commit -m "feat: DistroPreviewDialog-UI (Screenshot, Kurzfakten, Beschreibung, Tags)"
```

---

### Task 5: 🔍-Icon in IsoSearchDialog-Zeilen einbinden

**Files:**
- Modify: `Views/Dialogs/DatabaseDialogs.cs:366-396` (Zeilen-Erstellung in `LoadDiscoveryTabAsync`)

**Interfaces:**
- Consumes: `DistroPreviewDialog` (Task 4), bestehende `DiscoveredDistro`-Felder
  (`Name`/`Slug`/`Tags`), `Str.Preview_OpenTooltip` (Task 3).

Kein separater Test — reine UI-Verdrahtung innerhalb einer bereits ungetesteten Methode (wie der
Rest von `IsoSearchDialog`). Verifikation über Build + manuellen Test in Task 6.

- [ ] **Step 1: Neue Grid-Spalte + Icon-Button einfügen**

In `Views/Dialogs/DatabaseDialogs.cs`, den bestehenden Block

```csharp
                    var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
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

                    var catCb = new ComboBox { Margin = new Thickness(6, 2, 6, 2), IsEnabled = !d.AlreadyInDb };
                    AppRes.FillCategoryCombo(catCb, d.SuggestedCategory);
                    Grid.SetColumn(catCb, 2); row.Children.Add(catCb);

                    var infoTb = new TextBlock { Text = d.Info, FontSize = 10.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 4, 4), Foreground = (Brush)Application.Current.Resources["BrushDim"] };
                    Grid.SetColumn(infoTb, 3); row.Children.Add(infoTb);
```

ersetzen durch (neue Spalte 2 für das Icon, Kategorie/Info-Spalten auf 3/4 verschoben):

```csharp
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
```

(`previewBtn` bleibt bewusst auch für `d.AlreadyInDb == true` aktiv — Vorschau ist auch für bereits
übernommene Distros sinnvoll, siehe Spec "Offene Punkte".)

- [ ] **Step 2: Build prüfen**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: `0 Fehler, 0 Warnungen`

- [ ] **Step 3: Commit**

```bash
git add Views/Dialogs/DatabaseDialogs.cs
git commit -m "feat: 🔍-Vorschau-Icon in IsoSearchDialog-Zeilen einbinden"
```

---

### Task 6: Volle Verifikation

**Files:** keine Änderungen, nur Prüfung.

- [ ] **Step 1: Volle Testsuite**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug && dotnet test ULM.Tests/ULM.Tests.csproj`
Expected: `0 Fehler, 0 Warnungen`, alle Tests grün (246 gesamt: 242 bestehende + 4 neue aus Task 2).

- [ ] **Step 2: Manuelle Verifikation durch den Nutzer (Hinweis, kein Automatismus)**

Folgendes kann nicht automatisiert geprüft werden (echte Netzwerkabhängigkeit + WPF-UI, siehe
Spec-Abschnitt "Testing") und sollte vom Nutzer nach der Umsetzung kurz gegengeprüft werden:

- "🔍 ISO suchen" öffnen, Reiter "Aktuellste": 🔍-Icon neben einer Zeile anklicken → Popup öffnet
  sich, zeigt Screenshot, Kurzfakten, Beschreibung, Tags.
- Eine Distro ohne Screenshot/fehlende Fakten testen (z.B. durch eine ungültige Test-URL simulieren)
  → Popup bleibt nutzbar, kein Absturz, betroffene Zeile wird einfach ausgeblendet.
- "🔗 DistroWatch-Seite öffnen" → öffnet Systembrowser mit der richtigen URL.
- Sprachumschalter auf Englisch stellen, Popup erneut öffnen → alle Labels/Status-Text auf
  Englisch, Beschreibungstext bleibt (korrekt) auf Englisch/Originalsprache.

- [ ] **Step 3: Abschluss-Commit-Historie prüfen**

Run: `git log --oneline -6`
Expected: 5 neue Commits seit Task 1 (Model+HttpService, Service+Tests, Lokalisierung, Dialog-UI,
Icon-Einbindung) sichtbar, alle mit `feat:`-Präfix.
