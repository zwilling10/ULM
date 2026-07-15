# Autostart-Option — Design

## Kontext

ULM ist portabel (Single-File-EXE, kein Installer). Nutzer, die ULM regelmäßig
im Hintergrund für automatische Update-/Versionschecks offen halten wollen,
müssen es aktuell nach jedem Windows-Neustart manuell starten. Dies ist das
erste von mehreren geplanten Komfort-Features (siehe Reihenfolge in der
Session-Diskussion); Tray-Icon und Hintergrund-Minimierung sind bewusst
ausgeklammert und folgen als eigenes, späteres Feature.

## Ziel

Eine Checkbox „Mit Windows starten" im Einrichtungsfenster (`SetupDialog`),
die ULM per Registry-Autostart-Eintrag beim Windows-Login (normal sichtbar,
nicht minimiert) mitstartet.

## Nicht-Ziele

- Kein Tray-Icon, kein minimierter/Hintergrund-Start (separates Feature)
- Kein Autostart-Umschalter außerhalb des Einrichtungsfensters (konsistent
  mit Theme-/Experten-Modus-Wahl, die ebenfalls nur dort änderbar sind)
- Keine Verwaltung über `HKLM` (kein Admin-Recht nötig/verwendet)

## Technischer Entwurf

### `Infrastructure/AutostartService.cs` (neu)

Statische Klasse, analog zu `ThemeService`:

- `bool IsEnabled()` — liest `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`,
  Wert `UniversalLinuxManager`; `true` wenn vorhanden UND der Pfad auf die
  aktuell laufende EXE zeigt (Vergleich gegen `Environment.ProcessPath`)
- `void Enable()` — schreibt `Environment.ProcessPath` unter obigen
  Value-Namen in den Run-Key
- `void Disable()` — entfernt den Value, falls vorhanden

Registry ist alleinige Quelle der Wahrheit — kein Duplikat-Flag in
`ulm_settings.ini`, um Drift zu vermeiden (z. B. wenn die EXE manuell
verschoben oder der Registry-Eintrag extern gelöscht wird).

Registry-Zugriffsfehler (z. B. durch Gruppenrichtlinie) werden nicht als
harter Fehler behandelt: `Enable()`/`Disable()` fangen `SecurityException`/
`UnauthorizedAccessException` ab, loggen eine Zeile und lassen den restlichen
Setup-Ablauf unbeeinträchtigt weiterlaufen.

### `Views/Dialogs/SetupDialogs.cs`

Neue `CheckBox chkAutostart` neben der bestehenden Experten-Modus-Checkbox,
vorbelegt mit `AutostartService.IsEnabled()` beim Öffnen des Dialogs. Beim
Schließen mit `DialogResult = true` wird abhängig vom Checkbox-Zustand
`Enable()`/`Disable()` aufgerufen.

## Fehlerfälle

| Fall | Verhalten |
|---|---|
| Registry-Zugriff verweigert (Gruppenrichtlinie) | Checkbox-Aktion schlägt fehl, Logzeile, Setup läuft normal weiter |
| EXE wurde seit letztem Autostart-Setup verschoben | `IsEnabled()` liefert `false` (Pfad stimmt nicht mehr überein) — Checkbox erscheint unangehakt, Nutzer kann neu aktivieren |

## Betroffene Dateien

- `Infrastructure/AutostartService.cs` (neu)
- `Views/Dialogs/SetupDialogs.cs` (Checkbox + Verdrahtung)
