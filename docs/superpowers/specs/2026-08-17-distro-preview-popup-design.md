# Design: Distro-Vorschau-Popup in "🔍 ISO suchen"

**Datum:** 2026-08-17
**Status:** Entwurf, vom Nutzer freigegeben (Architektur + Mockup bestätigt)
**Scope:** Nur `Views/Dialogs/DatabaseDialogs.cs` (`IsoSearchDialog`, Reiter "Aktuellste"/
"Beliebteste"), plus neue Dateien `Core/Services/DistroPreviewService.cs`,
`Core/Models/DistroPreview.cs`, `Views/Dialogs/DistroPreviewDialog.cs`. Die Haupt-ISO-Datenbank
(`IsoListDialog`, die 27+ manuell kuratierten Standard-Distros) ist **nicht** betroffen — dort gibt
es aktuell keinen DistroWatch-Slug pro Eintrag, das wäre ein eigenes, größeres Vorhaben.

## Ausgangslage

`IsoSearchDialog` zeigt pro gefundener Distro nur eine Zeile (Name, Kategorie-Dropdown, Datum) plus
ein Hover-Tooltip mit Name/Datum/Kategorie/Tags/nacktem `distrowatch.com/{slug}`-Link als Text
(siehe `BuildInfoTooltip`, [DatabaseDialogs.cs:433](../../../Views/Dialogs/DatabaseDialogs.cs)).
Der Nutzer kann sich vor dem Download keinen echten Eindruck von der Distro verschaffen, ohne die
App zu verlassen und die DistroWatch-Seite manuell im Browser zu öffnen.

## Ziel

Ein neues 🔍-Icon pro Zeile öffnet ein eigenes, in ULMs Look gehaltenes Popup-Fenster mit:
Screenshot-Vorschau, Kurzfakten (Basiert auf, Desktop, Herkunft, Architektur, Status, Popularität),
Beschreibungstext, DistroWatch-Tags, Link zur echten Profilseite (öffnet Systembrowser, optional).
Mockup vom Nutzer bestätigt (siehe `.superpowers/brainstorm/`-Session vom 2026-08-17).

## Nicht-Ziele

- **Kein eingebetteter Live-Browser (WebView2).** Bewusst gegen die echte Chromium-Einbettung
  entschieden — keine neue Laufzeit-Abhängigkeit, passt zur portablen, installer-freien
  Positionierung der App. Die kuratierte Karte lädt stattdessen gezielt Text + ein Bild per
  `HttpService`, genau wie `DiscoveryService` das für die Tags schon tut.
- **Keine Vorschau in der Haupt-ISO-Datenbank** (`IsoListDialog`). Dort fehlt der DistroWatch-Slug
  pro Eintrag komplett — eigenes, separates Vorhaben, falls gewünscht.
- **Kein Download/Übernehmen-Button im Popup selbst.** Die Aktion bleibt ausschließlich bei der
  bestehenden Checkbox + Kategorie-Dropdown + "Übernehmen"-Button in der Zeile dahinter, um keinen
  zweiten, parallelen Aktionspfad zu schaffen. Das Popup ist rein informativ, schließt sich über
  einen "Schließen"-Button oder den externen Link.

## Architektur

### Datenmodell

```csharp
// Core/Models/DistroPreview.cs
public sealed class DistroPreview
{
    public required string Name        { get; init; }
    public string  Description         { get; init; } = string.Empty; // Originaltext, meist Englisch
    public string  BasedOn             { get; init; } = string.Empty;
    public string  Origin              { get; init; } = string.Empty;
    public string  Architecture        { get; init; } = string.Empty;
    public string  Desktop             { get; init; } = string.Empty;
    public bool?   IsActive            { get; init; }   // null = unbekannt/nicht geparst
    public int     PopularityRank      { get; init; }   // 0 = unbekannt
    public int     PopularityHitsPerDay{ get; init; }
    public string? ScreenshotUrl       { get; init; }   // null = kein Bild verfügbar
}
```

### DistroPreviewService (analog zu DiscoveryService)

Neue Klasse `Core/Services/DistroPreviewService.cs`, Singleton-Pattern wie `DiscoveryService`.
`Task<DistroPreview?> GetPreviewAsync(string slug)`:

1. `HttpService.Instance.GetStringAsync($"https://distrowatch.com/{slug}")` — nutzt automatisch den
   bestehenden 5-Minuten-In-Memory-Cache, kein neuer Cache-Layer nötig (Popup wird ohnehin nur
   on-demand pro Klick geladen, nicht beim Öffnen der ganzen Liste).
2. **Wichtige Regel, aus den zwei heutigen Scraper-Bugfixes gelernt:** Felder werden **nicht** über
   den sichtbaren, lokalisierten Label-Text geparst (`HttpService` schickt immer
   `Accept-Language: de-DE`, DistroWatch liefert die Sidebar-Labels + den "Status"-Text dadurch
   IMMER auf Deutsch, unabhängig vom ULM-Sprachmodus — das würde bei Englisch-Modus falsche Sprache
   reinziehen). Stattdessen wird über die stabilen `href`-Query-Parameter-Namen der Links geparst,
   die sprachunabhängig sind:
   - `search.php?basedon=([^"#]+)#simple` → BasedOn
   - `search.php?origin=([^"#]+)#simple` → Origin
   - `search.php?architecture=([^"#]+)#simple` → Architecture
   - `search.php?desktop=([^"#]+)#simple` → Desktop
   - Status: `<font color="([^"]+)">` — nur die Farbe auswerten, NICHT den eingeschlossenen Text
     (der ist "Aktiv"/deutsch). `color="green"` → `IsActive = true`, jede andere gefundene Farbe
     (DistroWatch nutzt z.B. Orange/Rot für Dormant/Discontinued) → `IsActive = false`. Kein
     `<font color=...>`-Tag im erwarteten Bereich gefunden → `IsActive = null` (unbekannt, Zeile
     wird im Dialog dann ausgeblendet statt einen falschen Wert zu zeigen). Die drei Zustände
     werden im Dialog über `LocalizationService.T(...)` ausgegeben.
   - Popularität: `dwres.php?resource=popularity">(\d+) \(([\d.]+) [^)]+\)` — nur die zwei Zahlen
     (Rang, Treffer/Tag) extrahieren, nicht den umgebenden deutschen Text ("Treffer pro Tag").
     Rendering im Dialog über einen eigenen `Str.Preview_Popularity`-Format-String (DE/EN), analog
     zum `Xfer_DetailWithEta`-Präzedenzfall aus der Lokalisierungs-Arbeit.
   - Beschreibungstext: der Absatz direkt nach der `</ul>` der Fakten-Liste bis zum nächsten
     `<br><br>` — das ist Freitext des Distro-Autors (i.d.R. Englisch), wird unverändert
     übernommen, genau wie der bestehende `Info`/Tooltip-Text es heute schon tut.
   - Tags: bereits vorhanden über `DiscoveredDistro.Tags`, muss nicht erneut geparst werden.
3. Screenshot: `https://distrowatch.com/images/slinks/{slug}-small.png` — feste Konvention (live an
   6 aktuellen Kandidaten geprüft, überall HTTP 200). Kein HTML-Parsing nötig, direkt die URL
   zusammenbauen. Existenz wird beim Laden des Bildes selbst geprüft (siehe Fehlerbehandlung).
4. Rückgabe `null` bei Netzwerkfehler oder falls die Seite kein erwartetes Muster enthält (z.B.
   Slug ungültig geworden) — der Dialog zeigt dann einen Fehlertext statt eines leeren Popups.

### HttpService-Erweiterung

Neue Methode, gleiche Fehlerbehandlung/Timeout-Muster wie `GetStringAsync`:

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

Kein String-Cache (wie bei `GetStringAsync`) — Bilder sind größer, ein einzelner Popup-Aufruf lädt
sie ohnehin nur einmal pro Slug innerhalb der Session-Laufzeit relevant, kein wiederholter Bedarf,
der einen Cache rechtfertigen würde.

### UI: neues Icon in `IsoSearchDialog`

In der bestehenden Zeilen-Erstellung ([DatabaseDialogs.cs:366-396](../../../Views/Dialogs/DatabaseDialogs.cs))
kommt eine fünfte Grid-Spalte (`GridLength.Auto`) zwischen Name und Kategorie-Dropdown dazu: ein
kleiner `Button` mit 🔍-Symbol, `ToolTip = LocalizationService.T(Str.Preview_OpenTooltip)`. Klick
öffnet `new DistroPreviewDialog(distro.Name, distro.Slug, distro.Tags).ShowDialog()` (Owner =
`IsoSearchDialog`, `WindowStartupLocation.CenterOwner`, analog zu allen bestehenden Dialogen im
Projekt). Für bereits in der DB vorhandene Distros (`AlreadyInDb == true`) bleibt das Icon aktiv —
Vorschau ist auch dann noch sinnvoll, nur Checkbox/Dropdown sind wie bisher deaktiviert.

### UI: `DistroPreviewDialog`

Neues `sealed class DistroPreviewDialog : Window` in `Views/Dialogs/DatabaseDialogs.cs` (gleiche
Datei wie `IsoSearchDialog`, passt zum bestehenden Muster "ein Dialog pro Feature-Bereich in einer
Datei"). Breite fix ~360px (siehe Mockup), Höhe `SizeToContent.Height` mit `MaxHeight` +
`ScrollViewer` für sehr lange Beschreibungstexte. Aufbau von oben nach unten:

1. Titelzeile mit Distro-Name.
2. Screenshot-Bereich (`Image`-Control, `Stretch=UniformToFill`, feste Höhe ~130px) — erst
   sichtbar, wenn das Bild erfolgreich geladen ist (siehe Fehlerbehandlung), sonst kompletter
   Wegfall dieses Bereichs statt eines kaputten Bild-Icons.
3. Kurzfakten-Grid (Label/Wert-Paare, Labels über `Str.Preview_*` lokalisiert).
4. Beschreibungstext in eigenem, leicht abgesetztem Kasten (analog `BrushLBlue`-Hervorhebung, wie
   an anderen Stellen im Projekt für "informative" Blöcke verwendet).
5. Tag-Chips (Wiederverwendung der bereits vorhandenen `Tags`-Liste, keine neue Netzwerkanfrage).
6. Footer: Link "🔗 DistroWatch-Seite im Browser öffnen" (`Process.Start` mit
   `UseShellExecute = true`, öffnet Systembrowser — bewusst NICHT WebView2, nur ein Fallback für
   Nutzer, die doch tiefer einsteigen wollen) + "Schließen"-Button rechts.

Ladezustand: Dialog öffnet sofort mit "Lade …"-Text (Str.Db_Loading wiederverwendbar), Inhalt
ersetzt sich nach Abschluss des asynchronen Ladens (`Loaded`-Event, analog zum bestehenden Muster
in `IsoSearchDialog.Loaded`).

## Fehlerbehandlung

- `DistroPreviewService.GetPreviewAsync` liefert `null` → Dialog zeigt einen Fehlertext (neuer
  `Str.Preview_LoadError`, Format-String mit Slug) statt leerer Felder oder Absturz.
- Screenshot-Bild 404/Netzwerkfehler → `GetBytesAsync` liefert `null` → Screenshot-Bereich wird im
  Dialog einfach nicht angezeigt (Grid-Row `Height=0`/`Visibility=Collapsed`), Rest der Karte bleibt
  normal nutzbar. Kein Platzhalter-"kaputtes Bild"-Icon.
- Einzelne Fakten fehlen (z.B. kein "Basiert auf" bei manchen Nischen-Distros) → jeweilige Zeile im
  Fakten-Grid wird übersprungen statt einen leeren Wert anzuzeigen.

## Lokalisierung

Alle neuen sichtbaren Texte (Feld-Labels, Fehlertext, Tooltip, Button-Beschriftungen) über neue
`Str.Preview_*`-Konstanten in `Infrastructure/Str.cs` + `LocalizationService.cs` (DE/EN), analog zum
etablierten Muster der gesamten laufenden Zweisprachigkeits-Arbeit in diesem Projekt. Kein
hartkodierter deutscher Text, insbesondere nicht die von DistroWatch selbst gelieferten deutschen
Label/Status-Fragmente (siehe Architektur-Abschnitt oben — deshalb strukturelles statt textbasiertes
Parsen).

## Testing

- **Parsing-Logik** (`DistroPreviewService`-interne Extraktion aus einem HTML-String) wird als
  reine, `internal`/`InternalsVisibleTo`-testbare Methode mit einem eingebetteten HTML-Fixture
  (Ausschnitt einer echten, heute live abgerufenen Profilseite, z.B. ThorOS) unit-getestet — deckt
  ab: alle Fakten korrekt extrahiert, Status-Farbe korrekt erkannt, Popularitäts-Zahlen korrekt
  extrahiert, Beschreibungstext korrekt abgegrenzt, fehlendes Feld führt zu leerem statt
  fehlerhaftem Wert.
- **Netzwerk-Aufrufe selbst** (`HttpService.GetBytesAsync`, `GetPreviewAsync`s eigentlicher
  `GetStringAsync`-Call) bleiben ungetestet — gleiches, bereits etabliertes Muster wie
  `DiscoveryService` (kein Mock-HTTP-Server im Projekt, echte Netzwerkabhängigkeit).
- **UI** (`DistroPreviewDialog`) bleibt wie alle anderen WPF-Dialoge im Projekt ungetestet, manuelle
  Verifikation durch den Nutzer.

## Offene Punkte

- Exaktes Aussehen/Feinschliff der Fakten-Grid-Zeilen (Icons vor den Labels? wie bei den
  Kategorie-Pills im Mockup) — kann während der Umsetzung noch angepasst werden, kein
  Architektur-Entscheid.
- Ob `AlreadyInDb`-Distros das Icon ebenfalls bekommen, wurde oben als "ja" festgelegt — falls das
  nicht gewünscht ist, einfach `IsEnabled = !d.AlreadyInDb` wie bei Checkbox/Dropdown.
