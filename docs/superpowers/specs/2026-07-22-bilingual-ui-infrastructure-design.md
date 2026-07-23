# Zweisprachigkeit (Deutsch/Englisch) — Phase 1: Infrastruktur — Design

## Kontext

ULM ist bisher komplett auf Deutsch fest im Code verankert — nicht nur
Buttons/Labels/Dialog-Titel, sondern auch hunderte Log-/Aktivitäts-Meldungen
im Hauptfenster mit eingebauten Werten (z.B.
`"🆕 Neue ULM-Version verfügbar: v{info.LatestVersion}"`), über Dutzende
Dateien verteilt (`ViewModels/MainViewModel.cs`, alle `Views/Dialogs/*.cs`,
`Views/MainWindow.xaml(.cs)`, diverse `Core/Services/*.cs`).

**Wunsch:** ULM soll zusätzlich komplett auf Englisch nutzbar sein, mit einer
Sprachauswahl, die in den Programmstart eingebaut ist.

## Ziel (Gesamtvision, nicht alles Teil von Phase 1)

1. Jeder für den Nutzer sichtbare Text im Programm existiert in Deutsch UND
   Englisch.
2. Die aktuelle Sprache wird persistent gespeichert und beim nächsten Start
   automatisch wieder verwendet.
3. Die Sprache lässt sich jederzeit im laufenden Programm umschalten
   (Änderung wird nach einem Neustart wirksam, siehe Entscheidung unten).

## Umfang von Phase 1 (diese Spec/dieser Plan)

- Komplette technische Infrastruktur (Text-Tabelle, Lookup-Mechanismus,
  Ini-Persistenz, Spracherkennung aus der Windows-Systemsprache).
- Sprach-Umschalter-Button im Hauptfenster inkl. Neustart-Hinweis.
- Eine Beispiel-Migration zum Nachweis, dass das Muster trägt: die
  statischen Texte im Hauptfenster-Rahmen (Buttons, Menüpunkte,
  Spaltenüberschriften) — **nicht** der Log-/Aktivitätsverlauf.

**Ausdrücklich NICHT Teil von Phase 1** (spätere, eigene Pläne nach
demselben Muster):

- Alle Dialoge (`SetupDialogs.cs`, `DownloadDialogs.cs`,
  `DatabaseDialogs.cs`, `HelpDialog.cs`, `ChangelogDialog.cs`,
  `ManualSourceSearchDialog.cs`, `UpdateDownloadDialog.cs`,
  `VentoyInstallWindow.cs`, …)
- Der komplette Log-/Aktivitätsverlauf (`MainViewModel.Log(...)`-Aufrufe)
- Fehlermeldungen aus den `Core/Services/*.cs`-Klassen

Das ist mit der etablierten Infrastruktur aus Phase 1 reine, mechanische
Wiederholungsarbeit — jede weitere Phase bekommt einen eigenen
Implementierungsplan.

## Entscheidungen (im Brainstorming geklärt)

- **Umfang der Gesamtvision:** wirklich alles, inklusive Log-Verlauf (aber
  nicht alles auf einmal umgesetzt, siehe Phasenaufteilung oben).
- **Sprachwahl-UX:** wie der bestehende Dark/Light-Umschalter — einmalig
  aus der Windows-Systemsprache geraten, danach jederzeit per Button im
  Hauptfenster umschaltbar, Speicherung in `ulm_settings.ini`.
- **Live-Umschalten vs. Neustart:** Neustart-Hinweis reicht. Ein
  Sprachwechsel wirkt erst nach einem Neustart von ULM — kein Live-Retexten
  laufender Fenster/Log-Zeilen nötig. Deutlich kleinerer Aufwand als ein
  vollständig dynamisches Umschalt-System.
- **Technischer Ansatz:** eigene, schlanke Text-Tabelle statt .NET-RESX-
  Ressourcendateien. RESX bräuchte Satellite-Assemblies pro Sprache, was
  sich erfahrungsgemäß nicht reibungslos mit `PublishSingleFile` (dem
  self-contained Single-File-Build aus `build-release.sh`) verträgt —
  zusätzliche Build-Konfiguration und ein vermeidbares Risiko. Dieses
  Projekt vermeidet an mehreren Stellen bewusst neue Build-Schritte/
  NuGet-Pakete (siehe z.B. `docs/superpowers/plans/2026-07-18-boot-theme-fixes.md`
  Global Constraints). Eine eigene, typsichere Tabelle passt außerdem zum
  bestehenden Stil des Projekts (`IniService`, `AppPaths`, `ThemeService` —
  alles selbst gebaut statt schwere .NET-Subsysteme zu importieren).

## Architektur

### `Infrastructure/AppLanguage.cs`

```csharp
namespace ULM.Infrastructure
{
    public enum AppLanguage { German, English }
}
```

### `Infrastructure/Str.cs`

Ein `enum`-Eintrag pro eindeutigem, übersetzbarem Text. Tippfehler in
Schlüsseln fallen beim Kompilieren auf, nicht erst zur Laufzeit (anders als
bei string-basierten Lookup-Keys). Für Phase 1 nur die Einträge, die für
den Hauptfenster-Rahmen gebraucht werden (Buttons, Menüpunkte,
Spaltenüberschriften) — weitere Phasen erweitern dasselbe `enum`.

```csharp
namespace ULM.Infrastructure
{
    public enum Str
    {
        Btn_Download,
        Btn_Updates,
        Btn_CheckUrls,
        Btn_CopyUsb,
        Btn_EditDb,
        Btn_Ventoy,
        Btn_Cancel,
        Btn_HealthCheck,
        Btn_VerifyIntegrity,
        // … weitere Hauptfenster-Rahmen-Texte, vollständige Liste im Plan
        ColumnHeader_Name,
        ColumnHeader_Version,
        ColumnHeader_Category,
        ColumnHeader_Status,
        MenuItem_Settings,
        MenuItem_Help,
        // usw.
    }
}
```

### `Infrastructure/LocalizationService.cs`

Pendant zu `Infrastructure/ThemeService.cs` (gleiches Muster: statische
Klasse, `Initialize()`/`SetMode()`-artige API, kein DI-Container nötig, da
der Rest des Projekts Services ebenfalls als `Instance`-Singletons oder
statische Klassen hält).

```csharp
namespace ULM.Infrastructure
{
    public static class LocalizationService
    {
        public static AppLanguage Current { get; private set; } = AppLanguage.German;

        // Beim Programmstart aus ulm_settings.ini gelesen (App.xaml.cs, analog
        // zu ThemeService.Initialize()). Fehlt der Schlüssel (erster Start),
        // wird aus der Windows-Systemsprache geraten: Deutsch, falls
        // CultureInfo.CurrentUICulture mit "de" beginnt, sonst Englisch.
        public static void Initialize(string? savedValue) { /* … */ }

        // Speichert sofort in ulm_settings.ini. Wirkt NICHT live — Aufrufer
        // (MainWindow) zeigt danach den Neustart-Hinweis.
        public static void SetLanguage(AppLanguage lang, string settingsIniPath) { /* … */ }

        // Zentraler Lookup. args werden per string.Format eingesetzt
        // (Platzhalter {0}, {1}, … in den Text-Tabellen).
        public static string T(Str key, params object[] args) { /* … */ }
    }
}
```

Die eigentlichen Texte liegen als zwei `private static readonly
Dictionary<Str, string>` (`De` und `En`) in derselben Datei oder einer
begleitenden `Infrastructure/Strings.De.cs` / `Strings.En.cs` (Aufteilung
wird im Implementierungsplan final entschieden — abhängig davon, wie groß
die Tabelle in Phase 1 tatsächlich wird).

### Persistenz (`ulm_settings.ini`)

Neuer Schlüssel unter `[App]`, analog zu `ThemeMode`:

```ini
[App]
Language = de
```

Werte: `de` / `en`. Fehlt der Schlüssel komplett (Bestandsinstallationen,
die auf eine Version vor Phase 1 aktualisieren), greift die
Windows-Spracherkennung wie beim allerersten Start.

### UI: Sprach-Umschalter

Neuer Button im Hauptfenster, direkt neben dem bestehenden
Dark/Light-Umschalter (`BtnThemeToggle_Click` in `Views/MainWindow.xaml.cs`
als Vorbild). Zeigt die JEWEILS ANDERE Sprache als Ziel an (z.B. steht
„🌐 EN“, wenn Deutsch aktiv ist — Klick wechselt zu Englisch), analog zur
bestehenden Beschriftungs-Konvention des Theme-Buttons.

Klick-Ablauf:
1. `LocalizationService.SetLanguage(...)` speichert sofort.
2. `MessageBox`-artiger Hinweis (Vorbild: die bestehende
   Selbst-Update-Bestätigung in `MainWindow.xaml.cs`): „Sprache geändert —
   ULM jetzt neu starten, um die Änderung zu übernehmen?“ mit Ja/Nein.
3. Bei „Ja“: einfacher Relaunch (`Process.Start(GetCurrentExePath())` +
   `Application.Current.Shutdown()`) — kein Datei-Austausch wie beim
   Selbst-Update nötig, daher deutlich einfacher als
   `SelfUpdateService.ApplyUpdateAndRestart`.
4. Bei „Nein“: Hinweistext bleibt im Aktivitätsverlauf stehen, Umschalter
   zeigt ab sofort korrekt die neue Zielsprache für einen späteren Klick.

## Migration (Nachweis-Bereich für Phase 1)

Der Hauptfenster-Rahmen: Button-Beschriftungen, Menüpunkte,
Spaltenüberschriften der Distro-Liste. Konkret die in `Str.cs` oben
skizzierten Einträge. XAML-Texte, die aktuell hart im Markup stehen (z.B.
`Content="⬇ Herunterladen"`), werden entweder im Code-Behind nach
`InitializeComponent()` per `LocalizationService.T(...)` gesetzt (kein
zusätzliches WPF-Markup-Extension-System nötig, passt zum
"Neustart-statt-live"-Beschluss oben) oder — wo bereits ein
Code-Behind-Zugriffspunkt existiert (z.B. `UpdateBannerText` in
`MainViewModel.cs`) — dort direkt eingebaut.

## Testing

- **Vollständigkeits-Test:** ein Test, der über alle `Str`-Enum-Werte
  iteriert (`Enum.GetValues<Str>()`) und prüft, dass sowohl `De` als auch
  `En` für jeden Wert einen Eintrag haben. Verhindert, dass in künftigen
  Phasen ein neuer `Str`-Wert ohne beide Übersetzungen unbemerkt
  durchrutscht — wichtig, weil diese Tabelle auf hunderte Einträge wächst.
- Tests für `LocalizationService.Initialize`/`SetLanguage`
  (Ini-Persistenz, Rundtrip) und die Spracherkennung aus
  `CultureInfo.CurrentUICulture` (fehlender/vorhandener Ini-Wert).
- Kein UI-Automatisierungstest nötig (Projekt hat dafür bisher keinen
  Test-Harness, siehe bestehende Konvention bei
  `docs/superpowers/plans/2026-07-18-boot-theme-fixes.md` Task 1 Step 3 —
  manuelle Verifikation stattdessen).

## Manuelle Verifikation

Da es für WPF-UI-Text keinen automatisierten Rendering-Test gibt: nach der
Implementierung ULM einmal mit `Language = de` und einmal mit
`Language = en` in `ulm_settings.ini` starten, Hauptfenster-Rahmen
(Buttons, Menüpunkte, Spaltenüberschriften) visuell auf korrekte
Übersetzung und keine abgeschnittenen/überlappenden Labels prüfen (englische
Texte sind oft kürzer als deutsche — Layout-Bruch ist unwahrscheinlich,
aber zu prüfen).

## Offene Fragen für spätere Phasen (nicht jetzt entscheiden)

- Sollen Distro-Kategorien-Namen (z.B. „🖥 Einsteiger“) übersetzt werden,
  oder bleiben sie sprachneutral? (Betrifft Phase 2+, nicht Teil dieser
  Spec.)
- Wie wird die Text-Tabelle bei sehr großer Größe organisiert (eine Datei
  vs. mehrere nach Bereich getrennte Dateien)? Wird in der jeweiligen
  Phasen-Spec entschieden, sobald der tatsächliche Umfang sichtbar ist.
