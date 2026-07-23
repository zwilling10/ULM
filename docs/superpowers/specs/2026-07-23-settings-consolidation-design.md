# Einstellungen konsolidieren (Design/Sprache/Modus) — Design

## Kontext

Nach Einführung der Zweisprachigkeit (Phase 1, siehe
`docs/superpowers/specs/2026-07-22-bilingual-ui-infrastructure-design.md`)
hat die Kopfzeile des Hauptfensters vier Pillen-Buttons nebeneinander:
„Modus: Experte 🛠", „🌓 Design: Dunkel", „🌐 English" und „❓ Hilfe". Das
wird mit jeder künftigen Einstellung enger und beeinträchtigt die
Lesbarkeit/Auffindbarkeit der übrigen Funktionen in der Kopfzeile.

**Wunsch:** Design- und Sprach-Umschalter (sowie der Experten-Modus-
Umschalter) sollen zu einer einzigen, kompakten Einstellungen-Option
zusammengefasst werden, mit Checkboxen/Auswahl statt einzelner Buttons,
plus einem Weg, diese Auswahl jederzeit nach dem Start erneut zu ändern.

## Bestandsaufnahme

`Views/Dialogs/SetupDialogs.cs` enthält bereits ein vollständiges
Einrichtungsfenster (`SetupDialog`), aktuell ausschließlich beim
Programmstart aufgerufen (`App.xaml.cs`, gesteuert über
`SkipSetupDialog` in `ulm_settings.ini`). Es hat bereits:

- Eine „👤 Modus"-Karte mit `CheckBox chkExpert`.
- Eine „🚀 Autostart"-Karte mit `CheckBox chkAutostart` (direkt beim
  Klick auf „✔ Übernehmen" angewendet über `AutostartService`).
- Eine „🌓 Design"-Karte mit einer Button-Gruppe (System/Hell/Dunkel,
  aktiver Button hervorgehoben) — Auswahl wird erst NACH Dialog-Schluss
  in `App.xaml.cs` per `ThemeService.SetMode(...)` angewendet, nicht live
  während der Auswahl im Dialog selbst.
- Einen Konstruktor-Parameter `showDirectory`/`showWelcome`, der die
  Ordner-Auswahl und den Willkommenstext ausblendet — bisher nie mit
  `false`/`false` aufgerufen, aber genau dafür vorgesehen: ein
  „Lite"-Modus für ein *späteres* Wiederöffnen ohne Erststart-Inhalte.
- Eine „Diese Einrichtung beim nächsten Start überspringen"-Checkbox plus
  einen einzigen „✔ Übernehmen"-Button, der ALLE Auswahlen gesammelt
  anwendet (`DialogResult = true`, `Close()`).

`SetupDialog` wird aktuell an genau einer Stelle konstruiert
(`App.xaml.cs:121`). Nach `SkipSetupDialog = 1` gibt es aktuell **keinen**
Weg mehr, z.B. „Mit Windows starten" jemals wieder zu ändern — dieser
fehlende Wiederöffnen-Weg wird mit dieser Änderung automatisch mit
behoben.

## Ziel

1. Kopfzeile bekommt einen einzigen `⚙ Einstellungen`-Button anstelle der
   drei Buttons „Modus: Experte", „🌓 Design: …", „🌐 …" (Sprache).
   „❓ Hilfe" bleibt unverändert ein eigener Button.
2. Klick öffnet `SetupDialog` im Lite-Modus (`showDirectory:false,
   showWelcome:false`) — zeigt „👤 Modus", „🚀 Autostart", „🌓 Design" und
   eine **neue** Karte „🌐 Sprache" (Button-Gruppe, exakt nach dem Muster
   der bestehenden Design-Buttons: „🇩🇪 Deutsch" / „🇬🇧 English").
3. „✔ Übernehmen" wendet Design, Modus und Autostart wie bisher sofort/
   live an. Sprache wird nur dann geändert (inkl. Neustart-Bestätigung),
   wenn sie sich tatsächlich vom vorherigen Wert unterscheidet — blieb sie
   unverändert, erscheint kein Neustart-Hinweis.
4. Der Erststart-Dialog (`App.xaml.cs`) bekommt dieselbe neue
   Sprach-Karte automatisch mit (kein separater Weg nötig) — dort wird
   die gewählte Sprache direkt angewendet, bevor `MainWindow` konstruiert
   wird (kein Neustart-Dialog beim allerersten Start nötig, das Fenster
   existiert ja noch gar nicht).

## Entscheidungen (im Brainstorming geklärt)

- **Umfang:** Design + Sprache + Modus wandern in die neue Einstellungen-
  Option. „❓ Hilfe" bleibt ein eigener Button.
- **Anwenden-Verhalten:** `SetupDialog`s bestehendes „Sammeln + ein
  Übernehmen-Button"-Muster wird beibehalten und wiederverwendet — kein
  neues, abweichendes „jede Checkbox wirkt sofort"-Panel. Kleine bewusste
  Verhaltensänderung gegenüber den bisherigen Einzel-Buttons: Design/
  Modus wirken im neuen Dialog erst nach Klick auf „✔ Übernehmen", nicht
  mehr sofort beim Anklicken einer Option — dafür ein einheitliches
  Muster für Erststart UND späteres Ändern, kein neuer Code für ein
  zweites Einstellungs-UI-Konzept.
- **Kein separates Kontextmenü:** Der sichtbare `⚙`-Button ist der einzige
  Zugang (kein zusätzliches Rechtsklick-Menü) — bessere Auffindbarkeit,
  weniger neue Fläche.

## Architektur

### `Views/Dialogs/SetupDialogs.cs` — neue Sprach-Karte

Neue `public AppLanguage ChosenLanguage { get; private set; }`-Property
und ein neuer Konstruktor-Parameter `AppLanguage currentLanguage =
AppLanguage.German`, analog zu `currentThemeMode`. Eine neue Karte „🌐
Sprache" wird nach der bestehenden „🌓 Design"-Karte eingefügt, exakt
nach dem Muster von `AddThemeButton`/`UpdateThemeButtons`:

```csharp
// ── Sprache (Deutsch / English) ─────────────────────────────
var langSection = new StackPanel();
var langRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
var langButtons = new System.Collections.Generic.Dictionary<AppLanguage, Button>();
void UpdateLangButtons()
{
    foreach (var (lang, btn) in langButtons)
    {
        bool active = lang == ChosenLanguage;
        btn.Background = active ? ThemeColors.Blue : ThemeColors.Card;
        btn.Foreground = active ? Brushes.White : ThemeColors.Mid;
    }
}
void AddLangButton(AppLanguage lang, string label)
{
    var btn = MakeButton(label, ThemeColors.Card, ThemeColors.Mid, 130, 32);
    btn.Margin = new Thickness(0, 0, 8, 0);
    btn.Click += (_, _) => { ChosenLanguage = lang; UpdateLangButtons(); };
    langButtons[lang] = btn;
    langRow.Children.Add(btn);
}
AddLangButton(AppLanguage.German,  "🇩🇪 Deutsch");
AddLangButton(AppLanguage.English, "🇬🇧 English");
UpdateLangButtons();
langSection.Children.Add(langRow);
langSection.Children.Add(new TextBlock
{
    Text = "Wirkt nach einem Neustart von ULM. Kann später jederzeit über ⚙ Einstellungen oben rechts geändert werden.",
    TextWrapping = TextWrapping.Wrap, Foreground = ThemeColors.Dim, FontSize = 11, LineHeight = 16,
});
body.Children.Add(MakeCard("🌐 Sprache", langSection));
```

Bestehende Hinweistexte bei „Design" und „Modus" werden von „oben rechts
gewechselt" auf „über ⚙ Einstellungen oben rechts geändert" angepasst
(die konkreten Einzel-Buttons, auf die sie sich beziehen, verschwinden ja).

### `App.xaml.cs` — Erststart-Dialog

`currentLanguage: LocalizationService.Current` wird an den bestehenden
Konstruktor-Aufruf ergänzt. Nach `ShowDialog() == true`:

```csharp
LocalizationService.SetLanguage(setupDlg.ChosenLanguage);
```

Direkt nach der bestehenden `ThemeService.SetMode(...)`-Zeile — kein
Neustart-Dialog nötig, `MainWindow` wird ja erst danach konstruiert.

### `Views/MainWindow.xaml` — Ein Button statt drei

`BtnThemeToggle`, `BtnLanguageToggle`, `BtnModeToggle` werden entfernt,
ersetzt durch:

```xml
<Button x:Name="BtnSettings" Content="⚙ Einstellungen" Style="{DynamicResource BtnGhost}"
        Foreground="White" BorderBrush="#4A6785"
        Click="BtnSettings_Click" Width="140" Margin="0,0,8,0"/>
```

### `Views/MainWindow.xaml.cs` — Öffnen + Anwenden

`BtnSettings_Click` öffnet `SetupDialog` im Lite-Modus, merkt sich die
Sprache VOR dem Dialog, wendet nach „✔ Übernehmen" Design/Modus sofort an
und zeigt den Neustart-Dialog nur bei tatsächlicher Sprachänderung. Die
Neustart-Bestätigung + der race-freie Neustart-Mechanismus (per
`SelfUpdateService.BuildRestartAfterInstallScript`, gerade erst repariert)
werden EINS ZU EINS aus dem bisherigen `BtnLanguageToggle_Click`
übernommen — dieser Handler entfällt komplett (kein Button ruft ihn mehr
auf), sein Code wandert in `BtnSettings_Click`. `UpdateThemeButtonLabel()`
und `UpdateLanguageButtonLabel()` entfallen ebenfalls ersatzlos, da keine
Buttons mehr existieren, die sie brauchen.

## Testing

- Kein Unit-Test-Harness für WPF-Dialoge in diesem Projekt (bestehende
  Konvention, siehe `SetupDialog` selbst — bereits heute ungetestet).
  Manuelle Verifikation wie bei der Zweisprachigkeit selbst: Screenshot-
  gestützt, echter Programmstart.

## Manuelle Verifikation

1. Erststart (bzw. `SkipSetupDialog` entfernt aus `ulm_settings.ini`):
   Sprach-Karte erscheint im Einrichtungsfenster, Auswahl wird ohne
   Neustart-Dialog direkt im neu geöffneten Hauptfenster wirksam.
2. Laufendes Programm: Klick auf „⚙ Einstellungen" öffnet denselben
   Dialog ohne Willkommenstext/Ordner-Auswahl. Design/Modus ändern +
   Übernehmen → sofort sichtbar, kein Neustart-Hinweis (Sprache
   unverändert gelassen).
3. Zusätzlich Sprache ändern + Übernehmen → Neustart-Bestätigung
   erscheint, bei „Ja" derselbe race-freie Neustart wie zuvor verifiziert.
4. Autostart-Checkbox im wiedereröffneten Dialog ändern + Übernehmen →
   tatsächlich wirksam (bisher nach Erststart nie erneut erreichbar).
