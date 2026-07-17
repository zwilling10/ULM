# Programm-Selbst-Update-Banner — Design

## Kontext

Der Nutzer wünscht, dass jeder, der ULM benutzt, immer die aktuellste
Programmversion hat. Eine Hintergrund-Prüfung existiert bereits teilweise:

- `HttpService.CheckForUlmUpdateAsync(currentVersion, repo)`
  (`Core/Services/HttpService.cs:248`) fragt `zwilling10/ULM`
  `releases/latest` ab und liefert `(HasUpdate, LatestVersion, ReleaseUrl)`.
- `MainWindow.CheckUlmUpdateAsync()` (`Views/MainWindow.xaml.cs:204`) ruft
  das beim Start fire-and-forget auf.

**Problem:** Das Ergebnis erscheint nur als unauffällige Protokollzeile ganz
unten im Log — praktisch nie bemerkt. Der Nutzer bekommt neue Versionen
daher nicht mit.

## Root Cause

Der vorhandene Check meldet ein Update nur passiv via `AppendLog(...)`. Es
gibt kein sichtbares UI-Element und keine Möglichkeit, die neue Version
direkt aus der App heraus zu beziehen. Zudem liefert
`CheckForUlmUpdateAsync` bislang nur `tag_name`/`html_url`, nicht die
konkreten Download-Asset-URLs (portable .exe / Setup-.exe).

## Ziel

1. Bei verfügbarem Update erscheint ein **dauerhaft sichtbares Banner** im
   Hauptfenster (bleibt sichtbar bis zum Update oder Programmende), nicht
   nur eine Protokollzeile.
2. Klick auf das Banner öffnet einen kleinen Dialog, der dem Anwender die
   Wahl lässt zwischen **portabler .exe** und **Installer-.exe**. Die
   gewählte Datei wird ins Download-Verzeichnis geladen, danach wird der
   Ziel-Ordner im Explorer geöffnet — die Datei/Installation startet der
   Anwender selbst per Doppelklick.

## Nicht-Ziele

- **Kein** automatisches Starten/Ausführen der heruntergeladenen .exe durch
  ULM. Das Herunterladen ist auf ausdrücklichen Klick des Anwenders hin,
  die Ausführung macht ausschließlich der Anwender selbst.
- Kein Silent-Auto-Update (kein Ersetzen der laufenden .exe im Hintergrund).
- Keine Änderung an der bestehenden Distro-Update-Logik.

## Technischer Entwurf

### 1. Asset-URLs mitliefern (`HttpService.cs`)

`CheckForUlmUpdateAsync` wird um die beiden Asset-Download-URLs erweitert.
Neue Rückgabe als benanntes Record/Tuple:

```csharp
public sealed record UlmUpdateInfo(
    bool HasUpdate, string LatestVersion, string ReleaseUrl,
    string PortableExeUrl, string SetupExeUrl);

public async Task<UlmUpdateInfo> CheckForUlmUpdateAsync(
    string currentVersion, string repo = "zwilling10/ULM")
```

Nach dem bestehenden Parsen von `tag_name`/`html_url` zusätzlich das
`assets`-Array durchgehen (wie in `GitHubResolveUrlAsync`,
`HttpService.cs:227`) und per Namensmuster zuordnen:

- Portable: Name matcht `*-win-x64.exe`, aber **nicht** `*-Setup-*`.
- Installer: Name matcht `*-Setup-*-win-x64.exe`.

Fehlt ein Asset (nur eines veröffentlicht), bleibt die jeweilige URL leer;
der Dialog blendet die fehlende Option dann aus.

### 2. Banner im Hauptfenster (`MainWindow.xaml`)

Eine neue, standardmäßig eingeklappte Banner-Zeile direkt unter dem Header
(zwischen `Grid.Row="0"` Header und `Grid.Row="1"` Toolbar — neue RowDefinition
`Height="Auto"` einfügen, nachfolgende Rows verschieben sich um 1). Inhalt:

- Text „🆕 Neue Version verfügbar: v{latest} (installiert: v{current})".
- Button „Herunterladen …".
- Button „Ausblenden" (versteckt das Banner nur für die laufende Sitzung).

Sichtbarkeit über neue ViewModel-Eigenschaften gebunden (`BoolToVis`).

### 3. ViewModel-Zustand (`MainViewModel.cs`)

Neue Eigenschaften + Event:

```csharp
public bool   UpdateBannerVisible { get; private set; }   // OnPropertyChanged
public string UpdateBannerText    { get; private set; }
public void   DismissUpdateBanner();                        // setzt Visible=false
// Vom MainWindow nach erfolgreichem Check gesetzt:
public void   SetAvailableUpdate(UlmUpdateInfo info);
```

`SetAvailableUpdate` merkt sich die `UlmUpdateInfo` (für den Download-Dialog),
setzt Text und `UpdateBannerVisible = true`.

### 4. Download-Auswahl-Dialog (`Views/Dialogs/…`)

Neuer schlanker Dialog `UpdateDownloadDialog` (Muster wie
`DriveSelectDialog`/`OrphanedDownloadsDialog`): zeigt bis zu zwei Buttons
(„Portable .exe" / „Setup-Installer"), je nachdem welche URLs vorhanden
sind, plus „Abbrechen". Ergebnis: die gewählte Download-URL + Zieldateiname.

### 5. Download + Ordner öffnen (`MainWindow.xaml.cs`)

Klick auf „Herunterladen …" öffnet `UpdateDownloadDialog`. Nach Wahl:

- Zielpfad `AppPaths.Instance.DownloadDir` + Original-Asset-Dateiname.
- Download über `HttpService.Instance.DownloadAsync(url, dest, progress, ct)`
  (bestehende Methode, `HttpService.cs:1260`) — Fortschritt in die Statuszeile.
- Nach Erfolg: Ziel-Ordner im Explorer öffnen und die Datei markieren via
  `Process.Start("explorer.exe", $"/select,\"{dest}\"")` (Muster analog
  `Process.Start` in `MainViewModel.cs:1224`).
- Protokollzeile mit Ergebnis; bei Fehler sichtbare Fehlermeldung, Banner
  bleibt stehen.

## Fehlerfälle

| Fall | Verhalten |
|---|---|
| GitHub nicht erreichbar / kein Release | Kein Banner, kein Fehler (wie bisher) |
| Release ohne passendes Asset (nur Quellcode) | Banner erscheint (Update existiert), Download-Dialog zeigt nur „Zur Release-Seite öffnen" als Fallback über `ReleaseUrl` |
| Nur eines der beiden Assets vorhanden | Dialog zeigt nur die vorhandene Option |
| Download schlägt fehl / abgebrochen | Fehlermeldung im Log + Statuszeile, Banner bleibt sichtbar, `.part`-Datei wird von `DownloadAsync` selbst aufgeräumt |
| Explorer-Öffnen schlägt fehl | Log-Warnung, Download gilt trotzdem als erfolgreich (Datei liegt im Download-Ordner) |

## Betroffene Dateien

- `Core/Services/HttpService.cs` (`CheckForUlmUpdateAsync` → `UlmUpdateInfo`
  mit Asset-URLs; neuer `UlmUpdateInfo`-Record)
- `ViewModels/MainViewModel.cs` (`UpdateBannerVisible`/`UpdateBannerText`/
  `SetAvailableUpdate`/`DismissUpdateBanner`, Merken der `UlmUpdateInfo`)
- `Views/MainWindow.xaml` (neue Banner-Row + Row-Index-Verschiebung)
- `Views/MainWindow.xaml.cs` (`CheckUlmUpdateAsync` setzt Banner statt nur
  Log; Download-Handler; Dialog-Aufruf)
- `Views/Dialogs/…` (neuer `UpdateDownloadDialog`)
- Tests: Asset-Zuordnung (portable vs. Setup) als reine Parser-Logik gegen
  ein GitHub-`assets`-JSON-Fixture (analog bestehender HttpService-Tests)
