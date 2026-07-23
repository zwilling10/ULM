# Zweisprachigkeit — Phase 2: SetupDialog lokalisieren — Design

## Kontext

Phase 1 (siehe
`docs/superpowers/specs/2026-07-22-bilingual-ui-infrastructure-design.md`)
hat die technische Infrastruktur (`Str`-Enum, `LocalizationService`,
Ini-Persistenz) geschaffen und exemplarisch nur den Hauptfenster-Rahmen
migriert. `Views/Dialogs/SetupDialogs.cs` (`SetupDialog`) wurde dabei
bewusst ausgeklammert (siehe „Ausdrücklich NICHT Teil von Phase 1" in der
Phase-1-Spec).

Nach der Einstellungen-Konsolidierung (siehe
`docs/superpowers/specs/2026-07-23-settings-consolidation-design.md`) ist
`SetupDialog` jetzt der zentrale Ort für Erststart UND spätere
Einstellungsänderung (`⚙ Einstellungen`-Button) — er wird also bei
`Language = en` in beiden Fällen sichtbar und ist aktuell komplett
hart-deutsch. Das ist der nächste sinnvolle Lokalisierungs-Schritt.

## Bestandsaufnahme

`Views/Dialogs/SetupDialogs.cs` (440 Zeilen) enthält 31 hart-deutsche
Textstellen (Fenstertitel, Karten-Überschriften, Checkbox-/Button-Labels,
Hinweistexte, eine Fehlermeldung). Ausgenommen bleiben bewusst:

- Die Sprach-Buttons selbst („🇩🇪 Deutsch" / „🇬🇧 English") — Eigennamen
  von Sprachen werden konventionsgemäß nie in die jeweils andere Sprache
  übersetzt (genau wie ein englisches Menü nie „Germany" statt
  „Deutschland" für die Sprachauswahl zeigen würde). Bleiben hartcodiert.
- Reine Emoji-Icons ohne begleitenden Text (z.B. das Kopfzeilen-Icon
  „🚀"/„⚙").

## Ziel

Alle 31 sichtbaren Textstellen in `SetupDialog` laufen über
`LocalizationService.T(Str...)` statt hartcodierter deutscher Strings,
nach exakt demselben Muster wie die Phase-1-Migration des
Hauptfenster-Rahmens.

## Entscheidungen (im Brainstorming geklärt)

- **Theme-Buttons ("System"/"Hell"/"Dunkel") werden übersetzt**, im
  Unterschied zu den Sprach-Buttons — es sind normale Wörter, keine
  Sprach-Eigennamen (Englisch: "System"/"Light"/"Dark").
- **Fehlermeldung per String-Verkettung statt neuem `T()`-Parameter:**
  `MessageBox.Show(LocalizationService.T(Str.Error_FolderCreateFailed) +
  "\n" + ex.Message, ...)` statt einen `args`-Platzhalter für
  `ex.Message` einzuführen. `T()` unterstützt zwar `params object[]
  args` für `string.Format`-Platzhalter (siehe Phase-1-Architektur),
  aber für einen einzelnen angehängten technischen Fehlertext ist
  einfache Verkettung minimal-invasiver und lesbarer als ein
  `{0}`-Platzhalter in beiden Sprachtabellen.
- **Kein Live-Retexten nötig:** `SetupDialog` wird bei jedem Öffnen neu
  konstruiert (kein langlebiges Fenster wie `MainWindow`), daher reicht
  ein einmaliges Auslesen von `LocalizationService.T(...)` beim Bau der
  Controls — kein Event-Mechanismus für Sprachwechsel-zur-Laufzeit nötig.
- **Umfang bleibt auf `SetupDialogs.cs` beschränkt** — `HelpDialog.cs`,
  `DownloadDialogs.cs`, `DatabaseDialogs.cs`, `ChangelogDialog.cs`,
  `ManualSourceSearchDialog.cs`, `UpdateDownloadDialog.cs`,
  `VentoyInstallWindow.cs` bleiben spätere, eigene Phasen (unverändert
  gegenüber der Abgrenzung aus Phase 1).

## Architektur

### `Infrastructure/Str.cs` — neue Einträge

31 neue Enum-Werte, thematisch gruppiert (Kommentar-Überschriften wie im
bestehenden Muster):

```csharp
public enum Str
{
    // … bestehende Phase-1-Einträge unverändert …

    // SetupDialog: Kopfzeile
    Setup_Title_Welcome,
    Setup_Title_Settings,
    Setup_Header_Welcome,
    Setup_Header_Settings,
    Setup_Subtitle_Welcome,
    Setup_Subtitle_Settings,

    // SetupDialog: Arbeitsordner-Karte
    Setup_Card_Directory,
    Setup_Directory_Header,
    Setup_Btn_Browse,
    Setup_FolderDialog_Title,
    Setup_Btn_UseDefaultPath,
    Setup_Directory_ItemsIntro,
    Setup_Directory_ItemDownloads,
    Setup_Directory_ItemDatabase,
    Setup_Directory_ItemLog,

    // SetupDialog: Über-ULM-Karte (nur Erststart)
    Setup_Card_AboutUlm,
    Setup_WelcomeBody,

    // SetupDialog: Modus-Karte
    Setup_Card_Mode,
    Setup_Chk_ExpertMode,
    Setup_Hint_Mode,

    // SetupDialog: Autostart-Karte
    Setup_Card_Autostart,
    Setup_Chk_Autostart,
    Setup_Hint_Autostart,

    // SetupDialog: Design-Karte
    Setup_Card_Design,
    Setup_Theme_System,
    Setup_Theme_Light,
    Setup_Theme_Dark,
    Setup_Hint_Theme,

    // SetupDialog: Sprache-Karte (Buttons selbst bleiben hartcodiert)
    Setup_Card_Language,
    Setup_Hint_Language,

    // SetupDialog: Fußzeile
    Setup_Chk_DontShowAgain,
    Setup_Btn_Apply,

    // SetupDialog: Fehler
    Setup_Error_Title,
    Setup_Error_FolderCreateFailed,
}
```

Anmerkung: `Setup_Title_*` (Fenster-`Title`) und `Setup_Header_*`
(Überschrift im Header-Banner) sind bewusst getrennte Einträge, obwohl
sie im Erststart-Fall textlich ähnlich sind ("Einrichtung" vs.
"Willkommen beim Universal Linux Manager") — sie waren es im
Originalcode auch schon (Zeile 37 vs. 88), keine Zusammenlegung nötig
oder gewünscht.

### `Infrastructure/Strings.De.cs` / `Strings.En.cs`

Für jeden neuen `Str`-Wert ein Eintrag in beiden Dictionaries. Der
Vollständigkeits-Test aus Phase 1 (`Enum.GetValues<Str>()` gegen `De`
und `En` geprüft) deckt das automatisch ab — kein neuer Test nötig,
nur mehr Einträge in der bestehenden Tabelle.

`Setup_WelcomeBody` behält die eingebauten Zeilenumbrüche/Bullet-Points
als ein zusammenhängender String (wie im Original), kein Aufsplitten in
mehrere `Str`-Werte.

### `Views/Dialogs/SetupDialogs.cs` — Umbau

Jede der 31 Stellen ersetzt den hartcodierten String durch
`LocalizationService.T(Str.Setup_...)`. Beispiel (Ausschnitt, analog für
alle anderen Stellen):

```csharp
Title = showWelcome
    ? LocalizationService.T(Str.Setup_Title_Welcome)
    : LocalizationService.T(Str.Setup_Title_Settings);
```

```csharp
catch (Exception ex)
{
    MessageBox.Show(
        LocalizationService.T(Str.Setup_Error_FolderCreateFailed) + "\n" + ex.Message,
        LocalizationService.T(Str.Setup_Error_Title),
        MessageBoxButton.OK, MessageBoxImage.Error);
    return;
}
```

Die drei `AddPreviewRow`-Aufrufe (Zeile 177–179) übergeben
`LocalizationService.T(Str.Setup_Directory_ItemDownloads)` usw. statt der
Literale `"ISO-Downloads"`/`"ISO-Datenbank"`/`"Protokolldatei"`.

Die Theme-Buttons (`AddThemeButton`-Aufrufe) und die Sprach-Buttons
(`AddLangButton`-Aufrufe) behalten ihre Emoji-Präfixe fest im Aufruf,
nur das Wort dahinter wird bei den Theme-Buttons durch `T(...)` ersetzt:

```csharp
AddThemeButton(AppThemeMode.System, "🌓 " + LocalizationService.T(Str.Setup_Theme_System));
AddThemeButton(AppThemeMode.Light,  "☀ "  + LocalizationService.T(Str.Setup_Theme_Light));
AddThemeButton(AppThemeMode.Dark,   "🌙 " + LocalizationService.T(Str.Setup_Theme_Dark));
// Sprach-Buttons bleiben unverändert hartcodiert:
AddLangButton(AppLanguage.German,  "🇩🇪 Deutsch");
AddLangButton(AppLanguage.English, "🇬🇧 English");
```

Keine Änderungen an Layout, Farben, Control-Struktur oder den
BUGFIX-Kommentaren im bestehenden Code — reine Text-Substitution.

## Testing

- Der bestehende Phase-1-Vollständigkeitstest deckt die neuen
  `Str`-Werte automatisch mit ab, sobald sie in `De`/`En` eingetragen
  sind.
- Kein neuer Unit-Test-Harness für `SetupDialog` selbst nötig
  (unverändert gegenüber der Konvention aus Phase 1 und der
  Einstellungen-Konsolidierung — WPF-Dialoge dieses Projekts werden
  manuell verifiziert, nicht automatisiert getestet).

## Manuelle Verifikation

1. `ulm_settings.ini`: `Language = de`, `SkipSetupDialog` entfernt →
   Erststart-Dialog erscheint komplett auf Deutsch, unverändert
   gegenüber dem aktuellen Stand (Regressionscheck).
2. `Language = en`, `SkipSetupDialog` entfernt → Erststart-Dialog
   komplett auf Englisch: Titel, Header, Über-ULM-Text, alle
   Karten-Überschriften, Checkbox-Labels, Hinweistexte, Fußzeile,
   Sprach-Buttons bleiben "🇩🇪 Deutsch"/"🇬🇧 English".
3. `Language = en`, laufendes Programm, Klick auf „⚙ Settings" → Dialog
   im Lite-Modus komplett auf Englisch (kein Über-ULM-Text, keine
   Ordner-Auswahl, wie in der Einstellungen-Konsolidierung festgelegt).
4. Fehlerfall provozieren (z.B. Pfad auf ein nicht beschreibbares
   Laufwerk setzen) bei `Language = en` → Fehlermeldung erscheint auf
   Englisch mit angehängter technischer `ex.Message` (bleibt Englisch,
   da .NET-Exception-Texte nicht lokalisiert werden — das ist so
   gewollt, kein Übersetzungsbedarf für Systemmeldungen).
5. Englische Texte auf abgeschnittene/überlappende Labels prüfen
   (gleiches Risiko wie in Phase 1 vermerkt, hier aber unwahrscheinlicher
   da englische Übersetzungen tendenziell kürzer sind als die deutschen
   Originale).

## Nachtrag (nach Implementierung)

Beim ursprünglichen String-Inventar wurde die Kartenüberschrift der Arbeitsordner-Karte selbst
(`MakeCard("📁 Arbeitsordner", section)`, separat von `Setup_Directory_Header` — dem Fließtext
INNERHALB der Karte) übersehen, obwohl alle 5 anderen Karten in diesem Dialog ihre Überschrift
bereits über `Str.Setup_Card_*` lokalisieren. Erst die manuelle Verifikation nach der
Implementierung deckte das auf (Karte blieb bei `Language = en` hartcodiert Deutsch). Ergänzt um
`Str.Setup_Card_Directory` (DE: "📁 Arbeitsordner", EN: "📁 Working Directory"), exakt nach dem
Muster der anderen Kartenüberschriften. Die tatsächliche Gesamtzahl neuer `Str`-Werte ist damit
34 (nicht 31/33 wie an früheren Stellen in diesem Dokument gezählt — reine Zähl-Ungenauigkeiten,
ohne Auswirkung auf den Code).

## Offene Fragen für spätere Phasen (nicht jetzt entscheiden)

- `HelpDialog.cs` hat mit ~165 deutschen String-Literalen den mit
  Abstand größten verbleibenden Umfang alle Dialoge — eigene, separate
  Phase/Spec nötig, nicht Teil dieser Änderung.
- Restliche Dialoge (`DownloadDialogs.cs`, `DatabaseDialogs.cs`,
  `ChangelogDialog.cs`, `ManualSourceSearchDialog.cs`,
  `UpdateDownloadDialog.cs`, `VentoyInstallWindow.cs`) und der komplette
  Log-/Aktivitätsverlauf bleiben wie in Phase 1 abgegrenzt spätere,
  eigene Pläne.
