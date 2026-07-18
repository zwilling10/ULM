# Härtefall-Erkennung für den Quelle-manuell-suchen-Button — Design

## Kontext

[2026-07-17-manual-source-search-design.md](2026-07-17-manual-source-search-design.md) hat
`ManualSourceSearchDialog` und den 🔧-Button pro Zeile eingeführt (`d1895f2`). Der Button erscheint
seitdem unbedingt in **jeder** Zeile der Hauptliste — auch bei Distros, deren automatische
Selbstlern-Auflösung nie ein Problem hatte. Das widerspricht dem ursprünglichen Ziel: die manuelle
Suche soll ein Sicherheitsnetz für hartnäckige Einzelfälle sein (Shadowfetch-Beispiel), kein
Dauerelement in jeder Zeile.

## Ziel

Der Button erscheint nur noch bei Einträgen, für die die automatische Auflösung nachweislich
wiederholt keine Quelle findet — unabhängig davon, ob der Eintrag über „ISO suchen" oder Import zur
DB kam. Bei allen anderen Einträgen (insbesondere den ~20 fest verdrahteten Distros mit dediziertem
Resolver) bleibt der Button verborgen.

## Nicht-Ziele

- Keine Änderung an der automatischen Auflösungskette selbst (`ResolveLatestAsync` & Co.) — nur ein
  zusätzliches, rein beobachtendes Signal, wann diese Kette für einen Eintrag wiederholt leer
  ausgeht.
- Kein neues Flag, das den Ursprung eines Eintrags (Import vs. ISO-Suche vs. manuell angelegt)
  unterscheidet — das Verhalten der Auflösungskette selbst (dedizierter Resolver vorhanden oder
  nicht) grenzt die betroffenen Einträge bereits korrekt ein, siehe unten.

## Technischer Entwurf

### 1. Neues persistentes Feld `IsoEntry.FailedResolveStreak`

`int`, Default `0`. Zählt aufeinanderfolgende Fehlschläge der automatischen Auflösung für Einträge
**ohne dedizierten Resolver**. Wird wie `Sha256`/`ExpectedSizeBytes` in `ulm_isos.ini` geschrieben
(`IsoDatabaseService.Save()`) und gelesen (`IsoDatabaseService.LoadFromIni()`), migrationsfrei über
`GetValueOrDefault` (fehlender Key in einer alten ini → `0`).

### 2. Zähl-/Reset-Logik in `HttpService.ResolveLatestAsync`

`ResolveLatestAsync` ([HttpService.cs:406](../../../Core/Services/HttpService.cs)) ist der
gemeinsame Funnel für alle drei Aufrufer (`UrlCheckWorker`, `AutoVersionCheckWorker`/
`UpdateScanWorker`, `DownloadWorker`). Die bestehende dedizierte-Resolver-Kette (Zeilen 413–457)
bekommt ein lokales `bool hadDedicatedAttempt`, gesetzt auf `true`, sobald `GithubRepo` gefüllt ist
oder einer der `nl.Contains(...)`-Zweige tatsächlich ausgeführt wurde (unabhängig vom Ergebnis).

Am Ende von `ResolveLatestAsync`:
- **Erfolg** (`result != Empty`, gleich über welchen Pfad) → `entry.FailedResolveStreak = 0`.
- **Kein dedizierter Resolver zutreffend** (`hadDedicatedAttempt == false`) **und** generische
  Auflösung liefert ebenfalls nichts → `entry.FailedResolveStreak++`.
- **Dedizierter Resolver vorhanden, schlägt aber (transient) fehl** → Zähler bleibt unverändert.
  Ohne diese Unterscheidung würde z.B. ein einzelner Netzwerk-Hänger bei einer fest unterstützten
  Distro (Ubuntu, Fedora, …) fälschlich Richtung „Härtefall" zählen, obwohl dafür ein funktionierender
  Resolver existiert und der nächste Check normalerweise wieder erfolgreich ist.

### 3. Button-Sichtbarkeit

Neue Konstante `Constants.ManualSearchFailureThreshold = 3` (Stil wie
`AutoCheckIntervalDays` in [Constants.cs:31](../../../Core/Models/Constants.cs)).

Neue Property in `IsoEntryViewModel`:
```csharp
public bool ShowManualSearchButton => Model.FailedResolveStreak >= Constants.ManualSearchFailureThreshold;
```

`Views/MainWindow.xaml` bindet den bereits vorhandenen `BoolToVis`-Converter
(schon registriert, siehe `Border`-Elemente für `ScanInProgress`/`OnlineScanActive` etc.):
```xml
<Button Grid.Column="6" Content="🔧" ...
        Visibility="{Binding ShowManualSearchButton, Converter={StaticResource BoolToVis}}" .../>
```
statt der aktuell unbedingten Sichtbarkeit.

### 4. Sofortiger Reset nach manueller Reparatur

`MainWindow.xaml.cs`, im bestehenden `BtnManualSearch_Click`-Speicherpfad
(`DialogResult == true` → `IsoDatabaseService.Instance.Save()` + `_vm.RebuildTree()`): zusätzlich
`entry.FailedResolveStreak = 0`, falls nach dem Dialog eine URL vorhanden ist. Ohne diesen Reset
bliebe der Button nach einer erfolgreichen manuellen Reparatur bis zum nächsten automatischen Check
weiterhin sichtbar — der Nutzer hat das Problem aber bereits selbst gelöst.

## Fehlerfälle

| Fall | Verhalten |
|---|---|
| Alte `ulm_isos.ini` ohne `FailedResolveStreak`-Key | `GetValueOrDefault` liefert `0` — Button bleibt für alle bestehenden Einträge zunächst verborgen, bis die Automatik tatsächlich 3x hintereinander scheitert. Kein Migrationsschritt nötig. |
| Entry hat dedizierten Resolver, der dauerhaft (nicht nur transient) kaputt ist (z.B. Anbieter-Website umstrukturiert) | Zähler bleibt bewusst bei 0, Button erscheint nie — Nicht-Ziel dieses Features, das automatische Auflösungsverhalten selbst zu verbessern. Sichtbar wird das stattdessen weiterhin über `VersionStatus` ("?") und ggf. `UrlOk == false`. |
| Nutzer legt über `IsoEditDialog` einen komplett neuen, resolverlosen Eintrag manuell an | Zählt genau wie ein importierter/gesuchter Eintrag — bewusste Vereinfachung (siehe Nicht-Ziele), da das Verhalten unabhängig vom Ursprung korrekt ist: kein Resolver + wiederholt kein Fund = Härtefall. |
| Eintrag pendelt (mal erfolgreich, mal nicht) knapp unter der Schwelle | Jeder Erfolg setzt den Zähler auf 0 zurück — der Button erscheint nur bei einer **zusammenhängenden** Fehlschlagsserie, nicht kumulativ über die gesamte Lebenszeit des Eintrags. |

## Betroffene Dateien

- `Core/Models/IsoEntry.cs` — neues Feld `FailedResolveStreak`
- `Core/Models/Constants.cs` — neue Konstante `ManualSearchFailureThreshold`
- `Core/Services/HttpService.cs` — `ResolveLatestAsync`: `hadDedicatedAttempt`-Tracking, Zähl-/Reset-Logik
- `Core/Services/IsoDatabaseService.cs` — `Save()`/`LoadFromIni()`: neues ini-Feld
- `ViewModels/IsoViewModels.cs` — neue Property `ShowManualSearchButton`
- `Views/MainWindow.xaml` — Button-`Visibility`-Binding statt unbedingter Sichtbarkeit
- `Views/MainWindow.xaml.cs` — `BtnManualSearch_Click`-Speicherpfad: Reset bei erfolgreichem manuellem Fix
