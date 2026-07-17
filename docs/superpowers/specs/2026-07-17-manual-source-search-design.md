# Manuelle Quellen-Suche für hartnäckig fehlschlagende Distros — Design

## Kontext

Die automatische Selbstlern-Auflösung (`ResolveViaDistroWatchAsync` → `ResolveViaWebSearchAsync`,
beide über `https://html.duckduckgo.com/html/?q=...`) findet für die meisten Distros ohne
dedizierten Resolver zuverlässig eine Quelle (im Test bestätigt: „neon user desktop" wurde über
mehrere App-Starts hinweg konsequent aufgelöst). Für einzelne, insbesondere sehr neue/seltene
Distros scheitert sie aber hartnäckig — konkreter Fall: „Shadowfetch Linux" (über „ISO suchen"
zur DB hinzugefügt) blieb über mehrere App-Starts und ~30 Minuten hinweg bei jedem automatischen
Check unauffindbar, obwohl eine kurze manuelle Suche durch den Nutzer sofort eine echte, gültige
Download-URL fand (`https://www.shadowfetch.com/linux/download/shadowfetch-1.9.0-amd64.iso`).

Root Cause dieser Restfälle liegt außerhalb von ULMs Kontrolle (DuckDuckGo-Bot-Erkennung,
fehlender/unstrukturierter DistroWatch-Eintrag, Trefferqualität der automatisierten Suche) und
lässt sich nicht zuverlässig automatisiert beheben. Ziel dieses Features ist daher **kein**
besserer Automatismus, sondern ein schneller, gut sichtbarer manueller Ausweg direkt im Programm.

## Ziel

Der Nutzer kann direkt aus der Hauptliste heraus für jeden einzelnen Distro-Eintrag ein Fenster
öffnen, das (a) dieselben Felder wie „Bearbeiten" zum manuellen Eintragen einer URL bereitstellt
und (b) eine Suchfunktion anbietet, die entweder ULMs eigene Treffer zur Auswahl zeigt oder —
falls ULM nichts findet — nahtlos auf eine normale Browser-Suche ausweicht.

## Nicht-Ziele

- Keine Änderung an der bestehenden automatischen Auflösungskette (`ResolveLatestAsync` &
  Co.) — dieses Feature ist ein zusätzlicher, rein manueller Weg, kein Ersatz.
- Keine Mehrfachauswahl/Stapelverarbeitung — ein Fenster bearbeitet genau einen Eintrag.
- Keine Vorab-Erreichbarkeitsprüfung jedes einzelnen Suchtreffers in der Ergebnisliste (würde die
  Suche unnötig verlangsamen) — Erreichbarkeit zeigt sich wie gewohnt beim nächsten „URLs prüfen"
  oder Download-Versuch.

## Technischer Entwurf

### 1. Auslöser: Button direkt in jeder Zeile der Hauptliste

Die Hauptliste (`Views/MainWindow.xaml`, `ItemsControl` mit `Categories`-Binding, Zeilen-Template
vermutlich in `Themes/*.xaml` oder einer eigenen `DataTemplate`-Ressource) bekommt pro Eintrag
einen neuen kleinen Button (Vorschlag: 🔧, Tooltip „Quelle manuell suchen/eintragen"). Kein neues
Auswahl-Konzept nötig — der Button wirkt direkt auf seinen eigenen Eintrag, genau wie andere
bereits vorhandene Pro-Zeile-Bedienelemente. Klick öffnet `ManualSourceSearchDialog` für genau
diesen `IsoEntry`.

### 2. Neues Fenster: `Views/Dialogs/ManualSourceSearchDialog.cs`

Aufbau analog zu `IsoEditDialog` (`Views/Dialogs/DatabaseDialogs.cs`), oben dieselben Felder:
Name, Kategorie (ComboBox), Primäre URL, Dateiname, Mirror 1–3, GitHub Repo, GitHub Asset,
Beschreibung — vorausgefüllt aus dem übergebenen `IsoEntry`.

Darunter ein neuer Abschnitt „Manuelle Suche":
- Textfeld, vorausgefüllt mit `"{entry.Name} iso"`, editierbar.
- Button „🔍 Suchen".

**Klick auf Suchen** ruft eine neue Methode `HttpService.SearchIsoLinksAsync(string query)` auf,
die die Kandidaten-Sammel-Logik aus `ResolveViaWebSearchAsync` wiederverwendet (DuckDuckGo-Suche →
Trefferseiten laden → `.iso`-Links extrahieren, inkl. dem bestehenden „folge Download-Link eine
Ebene tiefer"-Mechanismus), aber **ohne** automatische Bestwahl, Erreichbarkeitsprüfung oder
Mirror-Persistenz — sie liefert die vollständige, deduplizierte Kandidatenliste zurück:
`Task<List<(string Url, string Filename, string SourcePage)>>`.

- **Treffer gefunden:** Liste erscheint dynamisch unter dem Suchfeld, im selben Fenster (kein
  separates Popup) — je Zeile Dateiname + Herkunftsseite. Klick auf einen Treffer füllt die
  URL- und Dateiname-Felder oben aus; die Ergebnisliste bleibt sichtbar, falls der Nutzer einen
  anderen Treffer probieren möchte.
- **Keine Treffer:** Der „Suchen"-Button verhält sich für diesen Klick wie ein Browser-Öffner —
  startet den Standard-Browser (`Process.Start` mit `UseShellExecute = true`) mit einer
  vorausgefüllten DuckDuckGo-Suche (`https://duckduckgo.com/?q=<query>+iso+download`) — echte
  Browser-Suche mit JavaScript/Cookies, nicht durch dieselbe Bot-Erkennung blockiert wie ULMs
  automatisierte Anfrage. Der Nutzer sucht dort selbst weiter und trägt eine gefundene URL danach
  manuell ins URL-Feld ein.

„✔ Speichern"/„Abbrechen" unten wie bei `IsoEditDialog` — der Nutzer sieht das (ggf. durch die
Suche befüllte) Ergebnis, kann es noch anpassen, und speichert bewusst. Kein automatisches
Sofort-Speichern beim Klick auf einen Suchtreffer.

### 3. Aufrufer-Seite (`MainWindow.xaml.cs`)

Neuer Click-Handler öffnet `new ManualSourceSearchDialog(entry) { Owner = this }.ShowDialog()`;
bei `DialogResult == true` wie bei „Bearbeiten" `IsoDatabaseService.Instance.Save()` +
`_vm.RebuildTree()`.

## Fehlerfälle

| Fall | Verhalten |
|---|---|
| DuckDuckGo-Suche selbst nicht erreichbar (Netzwerkfehler) | Wird wie „keine Treffer" behandelt — Fallback auf Browser-Öffnen. |
| Nutzer trägt manuell eine falsche/kaputte URL ein und speichert | Kein Unterschied zu manueller Bearbeitung über „Bearbeiten" heute — zeigt sich beim nächsten „URLs prüfen"/Download. |
| Standard-Browser lässt sich nicht öffnen (kein Default-Browser konfiguriert) | `Process.Start`-Exception abfangen, Fehlermeldung im Fenster anzeigen, Suchfeld bleibt nutzbar für manuelle Eingabe. |
| Suchtreffer-Liste sehr lang | Auf sinnvolle Anzahl begrenzen (Vorschlag: erste 10, wie andere Trefferlisten im Code z.B. `Take(8)` in `ResolveViaWebSearchAsync`). |

## Betroffene Dateien

- `Core/Services/HttpService.cs` — neue Methode `SearchIsoLinksAsync`
- `Views/Dialogs/ManualSourceSearchDialog.cs` — neu
- `Views/MainWindow.xaml` (Zeilen-Template) + `Views/MainWindow.xaml.cs` — neuer Button + Handler
- Ggf. `ViewModels/IsoViewModels.cs` — falls der Button ein Command statt reinem Click-Handler braucht (abhängig vom bestehenden Pro-Zeile-Bedienelement-Muster, wird bei der Umsetzung geprüft)
