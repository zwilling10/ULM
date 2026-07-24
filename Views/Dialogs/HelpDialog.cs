// Views/Dialogs/HelpDialog.cs
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;   // Ellipse
using ULM.Core.Models;
using ULM.Infrastructure;

namespace ULM.Views.Dialogs
{
    public sealed class HelpDialog : Window
    {
        // Statt fest hinterlegter Hex-Farben: Zugriff auf die aktuell aktive Palette (Hell/Dunkel),
        // damit dieser Dialog automatisch zum gewählten Design passt. Als Properties (nicht mehr
        // "static readonly" mit einmalig eingefrorenem Wert), da HelpDialog bei jedem Öffnen neu
        // konstruiert wird und so immer die zum Zeitpunkt des Öffnens aktuelle Farbe liest.
        private static Brush BgDialog  => ThemeColors.Bg;
        private static Brush BgToc     => ThemeColors.Card;
        private static Brush ClrTitle  => ThemeColors.Header;
        private static Brush ClrSection=> ThemeColors.Blue;
        private static Brush ClrLabel  => ThemeColors.Header;
        private static Brush ClrBody   => ThemeColors.Mid;
        private static Brush ClrSub    => ThemeColors.Dim;
        private static Brush ClrBorder => ThemeColors.Border;

        private static Brush SwGreen  => ThemeColors.Green;
        private static Brush SwOrange => ThemeColors.Amber;
        private static Brush SwRed    => ThemeColors.Red;
        private static Brush SwTeal   => ThemeColors.Teal;
        private static Brush SwBlue   => ThemeColors.Mid;
        private static Brush SwGray   => ThemeColors.Dim;
        private static Brush SwDark   => ThemeColors.Header;

        public HelpDialog()
        {
            Title  = LocalizationService.T(Str.Help_Title);
            Width  = 880; Height = 660;
            MinWidth = 680; MinHeight = 420;
            ResizeMode = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = BgDialog;

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // ── Sprungmarken-Leiste (links) + Inhalt (rechts) ────────────────
            var body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(178) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var tocPanel = new StackPanel { Margin = new Thickness(14, 20, 10, 10) };
            var tocHost  = new Border
            {
                Background      = BgToc,
                BorderBrush     = ClrBorder,
                BorderThickness = new Thickness(0, 0, 1, 0),
                Child           = new ScrollViewer
                {
                    VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Content                       = tocPanel,
                },
            };
            Grid.SetColumn(tocHost, 0);
            body.Children.Add(tocHost);

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(24, 20, 24, 10),
            };
            Grid.SetColumn(scroll, 1);
            body.Children.Add(scroll);

            Grid.SetRow(body, 0);
            root.Children.Add(body);

            var content = new StackPanel();

            // Registriert eine Sektion im Inhalt UND als klickbare Sprungmarke in der linken
            // Leiste. Scrollt die Sektion explizit an den OBEREN Rand des sichtbaren Bereichs —
            // FrameworkElement.BringIntoView() würde nur minimal scrollen und die Sektion dabei
            // oft ganz unten im Fenster (am unteren Viewport-Rand) landen lassen.
            void AddSection(string title, string navLabel)
            {
                var section = MakeSection(title);
                content.Children.Add(section);
                tocPanel.Children.Add(MakeNavLink(navLabel, scroll, section));
            }

            content.Children.Add(MakeTitle(Constants.AppFullTitle));
            content.Children.Add(MakeSub(LocalizationService.T(Str.Help_Subtitle)));
            content.Children.Add(Spacer(16));

            tocPanel.Children.Add(new TextBlock
            {
                Text = LocalizationService.T(Str.Help_NavHeading), FontSize = 9.5, FontWeight = FontWeights.Bold,
                Foreground = ClrSub, Margin = new Thickness(6, 0, 0, 8),
            });

            // ── Übersicht ──────────────────────────────────────────────────
            AddSection(LocalizationService.T(Str.Help_Sec_Overview_Title), LocalizationService.T(Str.Help_Sec_Overview_Nav));
            content.Children.Add(MakeText(LocalizationService.T(Str.Help_Overview_Body)));
            content.Children.Add(Spacer());

            // ── Programmstart ──────────────────────────────────────────────
            AddSection(LocalizationService.T(Str.Help_Sec_Startup_Title), LocalizationService.T(Str.Help_Sec_Startup_Nav));
            content.Children.Add(MakeText(LocalizationService.T(Str.Help_Startup_Intro)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_OnlineCheck_Label), LocalizationService.T(Str.Help_Item_OnlineCheck_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_UsbScan_Label), LocalizationService.T(Str.Help_Item_UsbScan_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_FileMaintenance_Label), LocalizationService.T(Str.Help_Item_FileMaintenance_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_UpdateCheck_Label), LocalizationService.T(Str.Help_Item_UpdateCheck_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_WhatsNew_Label), LocalizationService.T(Str.Help_Item_WhatsNew_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_Autostart_Label), LocalizationService.T(Str.Help_Item_Autostart_Body)));
            content.Children.Add(Spacer());

            // ── Hauptliste ─────────────────────────────────────────────────
            AddSection(LocalizationService.T(Str.Help_Sec_Usage_Title), LocalizationService.T(Str.Help_Sec_Usage_Nav));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_SelectDownload_Label), LocalizationService.T(Str.Help_Item_SelectDownload_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_CategoryCheckbox_Label), LocalizationService.T(Str.Help_Item_CategoryCheckbox_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_DoubleClick_Label), LocalizationService.T(Str.Help_Item_DoubleClick_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_MouseoverTooltip_Label), LocalizationService.T(Str.Help_Item_MouseoverTooltip_Body)));
            content.Children.Add(Spacer());

            // ── Farben & Symbole ───────────────────────────────────────────
            AddSection(LocalizationService.T(Str.Help_Sec_Colors_Title), LocalizationService.T(Str.Help_Sec_Colors_Nav));

            content.Children.Add(MakeSubhead(LocalizationService.T(Str.Help_Subhead_TextColors)));
            content.Children.Add(MakeColorItem(SwGreen,  LocalizationService.T(Str.Help_Color_Green_Label),  LocalizationService.T(Str.Help_Color_Green_Body)));
            content.Children.Add(MakeColorItem(SwOrange, LocalizationService.T(Str.Help_Color_Orange_Label), LocalizationService.T(Str.Help_Color_Orange_Body)));
            content.Children.Add(MakeColorItem(SwRed,    LocalizationService.T(Str.Help_Color_Red_Label),    LocalizationService.T(Str.Help_Color_Red_Body)));
            content.Children.Add(MakeColorItem(SwTeal,   LocalizationService.T(Str.Help_Color_Teal_Label),   LocalizationService.T(Str.Help_Color_Teal_Body)));
            content.Children.Add(MakeColorItem(SwBlue,   LocalizationService.T(Str.Help_Color_Blue_Label),   LocalizationService.T(Str.Help_Color_Blue_Body)));
            content.Children.Add(MakeColorItem(SwGray,   LocalizationService.T(Str.Help_Color_Gray_Label),   LocalizationService.T(Str.Help_Color_Gray_Body)));
            content.Children.Add(MakeColorItem(SwDark,   LocalizationService.T(Str.Help_Color_Dark_Label),   LocalizationService.T(Str.Help_Color_Dark_Body)));
            content.Children.Add(Spacer(6));

            content.Children.Add(MakeSubhead(LocalizationService.T(Str.Help_Subhead_Columns)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_ColLocal_Label), LocalizationService.T(Str.Help_Item_ColLocal_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_ColOnStick_Label), LocalizationService.T(Str.Help_Item_ColOnStick_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_ColCurrent_Label), LocalizationService.T(Str.Help_Item_ColCurrent_Body)));
            content.Children.Add(Spacer(6));

            content.Children.Add(MakeSubhead(LocalizationService.T(Str.Help_Subhead_HashSymbol)));
            content.Children.Add(MakeText(LocalizationService.T(Str.Help_HashSymbol_Body)));
            content.Children.Add(Spacer(6));

            content.Children.Add(MakeSubhead(LocalizationService.T(Str.Help_Subhead_NameSymbols)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_SymbolImported_Label), LocalizationService.T(Str.Help_Item_SymbolImported_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_SymbolUrlOk_Label), LocalizationService.T(Str.Help_Item_SymbolUrlOk_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_SymbolUrlFail_Label), LocalizationService.T(Str.Help_Item_SymbolUrlFail_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_SymbolNewVersion_Label), LocalizationService.T(Str.Help_Item_SymbolNewVersion_Body)));
            content.Children.Add(Spacer(6));

            content.Children.Add(MakeSubhead(LocalizationService.T(Str.Help_Subhead_CategorySymbols)));
            content.Children.Add(MakeText(LocalizationService.T(Str.Help_CategorySymbols_Body)));
            content.Children.Add(Spacer());

            // ── Design (Hell/Dunkel) ───────────────────────────────────────
            AddSection(LocalizationService.T(Str.Help_Sec_Theme_Title), LocalizationService.T(Str.Help_Sec_Theme_Nav));
            content.Children.Add(MakeText(LocalizationService.T(Str.Help_Theme_Intro)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_ThemeSetting_Label), LocalizationService.T(Str.Help_Item_ThemeSetting_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_ThemeSystem_Label), LocalizationService.T(Str.Help_Item_ThemeSystem_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_ThemeInstant_Label), LocalizationService.T(Str.Help_Item_ThemeInstant_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_ThemeRemembers_Label), LocalizationService.T(Str.Help_Item_ThemeRemembers_Body)));
            content.Children.Add(Spacer());

            // ── Protokoll-Symbole ─────────────────────────────────────────
            AddSection(LocalizationService.T(Str.Help_Sec_LogSymbols_Title), LocalizationService.T(Str.Help_Sec_LogSymbols_Nav));
            content.Children.Add(MakeText(LocalizationService.T(Str.Help_LogSymbols_Body)));
            content.Children.Add(Spacer());

            // ── ISO suchen (Online-Entdeckung) ────────────────────────────
            AddSection(LocalizationService.T(Str.Help_Sec_IsoSearch_Title), LocalizationService.T(Str.Help_Sec_IsoSearch_Nav));
            content.Children.Add(MakeText(LocalizationService.T(Str.Help_IsoSearch_Intro)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_Newest_Label), LocalizationService.T(Str.Help_Item_Newest_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_Popular_Label), LocalizationService.T(Str.Help_Item_Popular_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_LiveOnly_Label), LocalizationService.T(Str.Help_Item_LiveOnly_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_AlreadyInDb_Label), LocalizationService.T(Str.Help_Item_AlreadyInDb_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_AdoptAndDownload_Label), LocalizationService.T(Str.Help_Item_AdoptAndDownload_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_RefreshCache_Label), LocalizationService.T(Str.Help_Item_RefreshCache_Body)));
            content.Children.Add(Spacer());

            // ── Download ───────────────────────────────────────────────────
            AddSection(LocalizationService.T(Str.Help_Sec_Download_Title), LocalizationService.T(Str.Help_Sec_Download_Nav));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_StorageLocation_Label), LocalizationService.T(Str.Help_Item_StorageLocation_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_PipelineMode_Label), LocalizationService.T(Str.Help_Item_PipelineMode_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_MirrorRace_Label), LocalizationService.T(Str.Help_Item_MirrorRace_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_SpeedGuard_Label), LocalizationService.T(Str.Help_Item_SpeedGuard_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_FasterButton_Label), LocalizationService.T(Str.Help_Item_FasterButton_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_EtaRemaining_Label), LocalizationService.T(Str.Help_Item_EtaRemaining_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_VerifyIntegrity_Label), LocalizationService.T(Str.Help_Item_VerifyIntegrity_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_FreeSpaceCheck_Label), LocalizationService.T(Str.Help_Item_FreeSpaceCheck_Body)));
            content.Children.Add(Spacer());

            // ── USB-Stick ──────────────────────────────────────────────────
            AddSection("💾 USB-Stick-Verwaltung (Ventoy)", "USB-Stick / Ventoy");
            content.Children.Add(MakeItem("Was ist Ventoy?",
                "Ventoy richtet einen USB-Stick so ein, dass mehrere Linux-ISOs " +
                "gleichzeitig gespeichert und beim Booten ausgewählt werden können. " +
                "Einmal einrichten, dann einfach ISOs draufkopieren — kein Neu-Flashen nötig."));
            content.Children.Add(MakeItem("Ventoy installieren / aktualisieren",
                "Nur im Expert-Modus sichtbar. " +
                "⚠ NEUINSTALLATION löscht ALLE Daten auf dem Stick! " +
                "Aktualisieren behält bestehende ISOs. Läuft als Administrator (UAC) in einem " +
                "eigenen ULM-Fenster mit Fortschrittsanzeige und Protokoll — Ventoy2Disk.exe selbst " +
                "läuft dabei komplett unsichtbar im Hintergrund (offizieller Silent-/CLI-Modus, " +
                "keine eigene Ventoy-Oberfläche, keine manuelle Bedienung nötig). Während die " +
                "Installation läuft, pausiert ULM die automatische Laufwerkserkennung — es können " +
                "keine weiteren Abfragen oder Dialoge parallel erscheinen. Nach Abschluss (Erfolg " +
                "oder Fehler) muss der 'Schließen'-Button aktiv geklickt werden, um fortzufahren."));
            content.Children.Add(MakeItem("Mehrere USB-Sticks angeschlossen",
                "Sind zwei oder mehr USB-Sticks gleichzeitig angeschlossen — egal ob schon beim " +
                "Programmstart oder erst später —, fragt ULM aktiv nach, mit welchem Stick gearbeitet " +
                "werden soll (vorbelegt mit der zuletzt aktiven Auswahl). 'Abbrechen' behält einfach die " +
                "bisherige Auswahl bei. Über das Laufwerks-Dropdown im Hauptfenster lässt sich jederzeit " +
                "manuell zu einem anderen angeschlossenen Stick wechseln."));
            content.Children.Add(MakeItem("Ventoy-Bootmenü",
                "Wird automatisch nach jedem Kopiervorgang UND nach jedem ISO-Import vom Stick aktualisiert. " +
                "Enthält leserliche Namen, Beschreibungen und Kategorien aus der Datenbank."));
            content.Children.Add(MakeItem("🔁 Verpasste Kopien nachholen",
                "Manuelles Sicherheitsnetz (Expert-Modus): kopiert bereits lokal vollständig " +
                "heruntergeladene, ausgewählte ISOs (erneut) auf den Stick. ULM bietet das normalerweise " +
                "automatisch an, sobald eine vollständige ISO auf dem Stick fehlt — dieses automatische " +
                "'Jetzt kopieren?'-Angebot erscheint aber pro Stick und Datei nur EINMAL je Sitzung, egal " +
                "ob mit Ja oder Nein geantwortet wurde. Wurde es abgelehnt, oder ist eine vorherige Kopie " +
                "fehlgeschlagen, ist dieser Button ohne Neustart der einzige Weg, es erneut zu versuchen."));
            content.Children.Add(Spacer());

            // ── Datenmüll-Schutz ──────────────────────────────────────────
            AddSection("🧹 Datenmüll-Schutz — Online-Größenprüfung", "Datenmüll-Schutz");
            content.Children.Add(MakeText(
                "Damit weder im Arbeitsordner noch auf dem Stick unbemerkt unvollständige oder " +
                "beschädigte ISOs liegen bleiben, vergleicht ULM jede gefundene Datei mit der " +
                "tatsächlichen Original-Größe beim Anbieter."));
            content.Children.Add(MakeItem("Wann wird geprüft?",
                "Automatisch: im Arbeitsordner nach dem Start (Datei-Wartung) sowie auf dem Stick bei " +
                "jedem Scan (Anstecken, Laufwerkswechsel, nach dem automatischen Versionscheck)."));
            content.Children.Add(MakeItem("Wie wird geprüft?",
                "ULM fragt per HEAD-Request die Original-Dateigröße ab (RemoteUrl → primäre URL → " +
                "Mirror1-5 — die erste bekannte Antwort gewinnt) und vergleicht sie mit der gefundenen " +
                "Dateigröße. Weicht sie um mehr als 2% ab, gilt die Datei als unvollständig. " +
                "Ist online keine Größe ermittelbar, greift als Rückfallebene die 300-MB-Mindestgröße."));
            content.Children.Add(MakeItem("Datenmüll im Arbeitsordner",
                "Wird als 'Unvollständig' bzw. 'Zu klein' protokolliert. Am Ende der Wartung erscheint " +
                "ein Dialog mit allen betroffenen Dateien — gezielt auswählbar und bedenkenlos löschbar."));
            content.Children.Add(MakeItem("Datenmüll auf dem Stick",
                "ISOs auf dem Stick, deren Größe nicht zur Online-Größe passt (z.B. durch einen " +
                "abgebrochenen Kopiervorgang), zählen NICHT als vorhanden — kein fälschliches 'Ja' in " +
                "der Spalte 'Auf dem Stick'. Ein Löschdialog wird automatisch angeboten."));
            content.Children.Add(Spacer());

            // ── ISO-Import ────────────────────────────────────────────────
            AddSection("📥 Unbekannte ISOs vom Stick importieren", "ISO-Import");
            content.Children.Add(MakeText(
                "Findet ULM beim Stick-Scan ISO-Dateien, die noch nicht in der Datenbank stehen " +
                "(z.B. manuell auf den Stick kopiert), erscheint ein Import-Dialog."));
            content.Children.Add(MakeItem("Name, Kategorie, Quelle-URL",
                "Für jede unbekannte ISO Name und Kategorie vergeben. Optional: eine Quelle-URL " +
                "hinterlegen. Sie ermöglicht später den Online-Update-Check auch für exotische Distros, " +
                "deren Name keinem der bekannten Muster (Ubuntu, Debian, Mint, …) entspricht."));
            content.Children.Add(MakeItem("Ordnerstruktur bleibt sauber",
                "Nach dem Import wird die Datei automatisch auf dem Stick in den passenden " +
                "Kategorie-Ordner verschoben (z.B. '\\Sicherheit\\'), das Ventoy-Bootmenü wird aktualisiert " +
                "und der Stick sofort neu gescannt."));
            content.Children.Add(MakeItem("Duplikat-Schutz",
                "Erkennt ULM, dass eine 'unbekannte' ISO eigentlich einem bereits vorhandenen " +
                "Datenbank-Eintrag entspricht (z.B. anderer Dateiname, andere Schreibweise derselben " +
                "Distro), wird KEIN doppelter Eintrag angelegt. Stattdessen übernimmt der bestehende " +
                "Eintrag einfach den neuen Dateinamen."));
            content.Children.Add(MakeItem("Zukünftig aktuell halten",
                "Importierte Distros werden ab sofort wie reguläre Datenbank-Einträge behandelt: der " +
                "automatische Versionscheck beim Start prüft sie mit, und auch der manuelle " +
                "'Nach Updates suchen'-Button berücksichtigt sie jetzt — sobald sie lokal ODER auf dem " +
                "Stick vorhanden sind. Auch ohne hinterlegte URL versucht ULM automatisch, die richtige " +
                "Quelle zu finden — eine mehrstufige Kette, die für JEDE Distro gilt, nicht nur bekannte:\n" +
                "  1. Einer von >20 dedizierten Distro-Erkennern (unabhängig von Schreibweise/Sonderzeichen)\n" +
                "  2. Automatische Suche über DistroWatch.com — findet die offizielle Homepage der Distro " +
                "und darüber die Download-Seite, ganz ohne distro-spezifischen Code\n" +
                "  3. SourceForge-Projektsuche, falls die Distro dort gehostet wird\n" +
                "  4. Allgemeine Websuche als letzter Rückfall\n" +
                "Eine so gefundene Quelle wird dauerhaft in der Datenbank gespeichert — künftige " +
                "Prüfungen starten direkt darüber, statt jedes Mal neu zu suchen. Kurz aufeinanderfolgende " +
                "Erreichbarkeits-Checks werden zusätzlich einige Minuten zwischengespeichert, damit " +
                "wiederholte Anfragen an denselben Server nicht fälschlich als Bot-Verhalten eingestuft " +
                "und blockiert werden.\n" +
                "Hinweis: eine externe Bot-/Anti-Scraping-Erkennung (z.B. bei Suchanfragen oder auf " +
                "manchen Download-Servern) lässt sich nicht zu 100% ausschließen — in seltenen Fällen " +
                "kann ein Check trotz eigentlich erreichbarer Quelle vorübergehend fehlschlagen. Ein " +
                "erneuter Gesundheitscheck später behebt das in aller Regel."));
            content.Children.Add(Spacer());

            // ── Expert-Modus ───────────────────────────────────────────────
            AddSection("🛠 Expert-Modus — Zusatzfunktionen", "Expert-Modus");
            content.Children.Add(MakeText("Expert-Modus aktivieren: oben rechts auf '⚙ Einstellungen' klicken, " +
                "in der Karte 'Modus' die Checkbox 'Experten-Modus aktivieren' setzen und mit " +
                "'✔ Übernehmen' bestätigen."));
            content.Children.Add(MakeItem("📊 Status-Reiter",
                "Zeigt Transparenz über alles, was gerade oder demnächst automatisch im Hintergrund " +
                "läuft, ohne dass ein Blick in den Task-Manager nötig ist: den aktuell laufenden " +
                "manuellen Vorgang (Download, Kopieren, Integritätsprüfung, Ventoy, …) mit Datei, " +
                "Fortschritt und Zähler, die automatischen Hintergrund-Scans (Online-Versionscheck, " +
                "Stick-Prüfung), wann der nächste automatische Online-Versionscheck fällig ist, sowie " +
                "einen Verlauf der letzten Hintergrund-Ereignisse (mit 'Verlauf leeren'-Button)."));
            content.Children.Add(MakeItem("URL-Check",
                "Prüft ob alle konfigurierten URLs erreichbar sind (Primär-URL + Mirror1-5). " +
                "Ergebnisse erscheinen als 🌐✓ / 🌐✗ im Distro-Namen."));
            content.Children.Add(MakeItem("Datenbank bearbeiten",
                "Öffnet den DB-Editor zum Hinzufügen, Bearbeiten und Löschen von ISO-Einträgen. " +
                "Felder: Name, Kategorie, URL, Mirror1-5, Filename, GitHub-Repo, Beschreibung."));
            content.Children.Add(MakeItem("🩺 DB-Gesundheitscheck",
                "Löst für ALLE Datenbank-Einträge auf einmal die aktuelle Download-Quelle auf (auch " +
                "vom Stick importierte Distros, unabhängig davon ob lokal vorhanden) und zeigt einen " +
                "klaren Bericht: welche Distros gerade online erreichbar und ladbar sind — und welche " +
                "nicht. Kein Ersatz für den Versionscheck, sondern ein gezielter Diagnose-Werkzeug, um " +
                "defekte Einträge (abgelaufene URL, umgezogene Distro-Website) sofort zu erkennen, statt " +
                "sie erst beim nächsten Download-Versuch zu bemerken. Bei Ausfällen: im DB-Editor " +
                "zusätzliche Mirror-URLs oder ein GitHub-Repo hinterlegen.\n\n" +
                "Läuft automatisch — gezielt genau dann, wenn neue, noch unverifizierte Einträge in " +
                "die Datenbank kommen: nach Stick-Import, nach 'Hinzufügen' bei einer neueren Version " +
                "auf dem Stick, und nach manuellem 'Neu' im DB-Editor. NICHT bei jedem Stick-Scan, " +
                "Ventoy-Installation oder Kopiervorgang — das regelmäßige Prüfen bereits bekannter " +
                "Einträge übernimmt der Online-Versionscheck (Start + alle paar Tage). Eigene " +
                "Fortschrittsanzeige oben rechts, genauso wie beim Online-Scan (🩺 Gesundheitscheck). " +
                "Vor jedem Lauf werden doppelte Datenbank-Einträge automatisch erkannt und bereinigt."));
            content.Children.Add(MakeItem("🔑 GitHub-Token",
                "Optional. GitHub-basierte Resolver (z.B. CachyOS, EndeavourOS) und der Ventoy-" +
                "Update-Check nutzen ohne Token ein gemeinsames Limit von 60 Anfragen/Stunde für " +
                "das ganze Netzwerk (nicht nur ULM) — bei intensiver Nutzung kann das knapp werden. " +
                "Ein kostenloses GitHub Personal Access Token OHNE jeden Berechtigungs-Scope hebt " +
                "das Limit auf 5000/Stunde an. Wird lokal in ulm_settings.ini gespeichert."));
            content.Children.Add(Spacer());

            // ── Protokoll ──────────────────────────────────────────────────
            AddSection("🗒 Protokoll — Diagnose und Fehlersuche", "Diagnose");
            content.Children.Add(MakeItem("Download-URL",
                "Beim Download wird die tatsächlich verwendete URL angezeigt:\n" +
                "  🔗 Distro-Name: https://…\n" +
                "Bei Fehlern kann so sofort die URL überprüft werden."));
            content.Children.Add(MakeItem("Protokoll-Datei",
                "Alle Ereignisse werden dauerhaft im Arbeitsordner des Programms gespeichert " +
                "(Datei 'ulm.log'). Nützlich für die Fehlersuche auf verschiedenen Systemen."));
            content.Children.Add(MakeItem("Log-Rotation",
                "Überschreitet 'ulm_log.txt' 5 MB, wird sie automatisch einmal zu 'ulm_log.txt.old' " +
                "verschoben und danach neu und leer begonnen — wächst also nicht mehr unbegrenzt bei " +
                "Dauerbetrieb. Die vorherige Sicherung bleibt als '.old'-Datei erhalten."));

            scroll.Content = content;

            var btnRow = new StackPanel
            {
                Orientation         = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin              = new Thickness(24, 8, 24, 16),
            };
            var btnOk = new Button
            {
                Content = "✔ Schließen",
                Width   = 130,
                Style   = (Style)Application.Current.Resources["BtnPrimary"],
            };
            btnOk.Click += (_, _) => Close();
            btnRow.Children.Add(btnOk);
            Grid.SetRow(btnRow, 1);
            root.Children.Add(btnRow);

            Content = root;
            KeyDown += (_, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter ||
                    e.Key == System.Windows.Input.Key.Escape)
                    Close();
            };
        }

        // ── UI-Hilfsmethoden ──────────────────────────────────────────────

        private TextBlock MakeTitle(string text) => new()
        {
            Text       = text,
            FontSize   = 18,
            FontWeight = FontWeights.Bold,
            Foreground = ClrTitle,
            Margin     = new Thickness(0, 0, 0, 4),
        };

        private TextBlock MakeSub(string text) => new()
        {
            Text         = text,
            FontSize     = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground   = ClrSub,
        };

        private Border MakeSection(string title)
        {
            var lbl = new TextBlock
            {
                Text              = title,
                FontSize          = 13.5,
                FontWeight        = FontWeights.SemiBold,
                Foreground        = ClrSection,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 6) };
            panel.Children.Add(lbl);
            return new Border
            {
                Child           = panel,
                BorderBrush     = ClrBorder,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Margin          = new Thickness(0, 0, 0, 8),
                Padding         = new Thickness(0, 0, 0, 4),
            };
        }

        // Klickbare Sprungmarke in der linken Leiste — scrollt die Ziel-Sektion an den
        // OBEREN Rand des Inhaltsbereichs (nicht nur "irgendwie sichtbar").
        private Button MakeNavLink(string text, ScrollViewer scroll, FrameworkElement target)
        {
            var btn = new Button
            {
                Content                    = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background                 = Brushes.Transparent,
                BorderThickness            = new Thickness(0),
                Foreground                 = ClrLabel,
                FontSize                   = 11,
                Padding                    = new Thickness(6, 5, 6, 5),
                Margin                     = new Thickness(0, 0, 0, 1),
                Cursor                     = System.Windows.Input.Cursors.Hand,
            };
            btn.Click += (_, _) =>
            {
                if (scroll.Content is not UIElement scrollContent) return;
                double offsetY = target.TranslatePoint(new Point(0, 0), scrollContent).Y;
                scroll.ScrollToVerticalOffset(Math.Max(0, offsetY - 4));
            };
            return btn;
        }

        private TextBlock MakeSubhead(string text) => new()
        {
            Text       = text,
            FontSize   = 11.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = ClrLabel,
            Margin     = new Thickness(0, 4, 0, 6),
        };

        private UIElement MakeItem(string label, string text)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(155) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lbl = new TextBlock
            {
                Text              = label,
                FontWeight        = FontWeights.SemiBold,
                FontSize          = 11.5,
                Foreground        = ClrLabel,
                TextWrapping      = TextWrapping.Wrap,
                Margin            = new Thickness(12, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Top,
            };
            var txt = new TextBlock
            {
                Text         = text,
                FontSize     = 11.5,
                TextWrapping = TextWrapping.Wrap,
                Foreground   = ClrBody,
                LineHeight   = 18,
            };
            Grid.SetColumn(lbl, 0);
            Grid.SetColumn(txt, 1);
            grid.Children.Add(lbl);
            grid.Children.Add(txt);
            return grid;
        }

        private UIElement MakeColorItem(Brush swatchColor, string label, string description)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 7) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(115) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var dot = new Ellipse       // System.Windows.Shapes.Ellipse
            {
                Width  = 12, Height = 12,
                Fill   = swatchColor,
                Margin = new Thickness(12, 2, 4, 0),
                VerticalAlignment = VerticalAlignment.Top,
            };
            var lbl = new TextBlock
            {
                Text              = label,
                FontSize          = 11.5,
                FontWeight        = FontWeights.SemiBold,
                Foreground        = swatchColor,
                VerticalAlignment = VerticalAlignment.Top,
                Margin            = new Thickness(0, 0, 8, 0),
            };
            var desc = new TextBlock
            {
                Text         = description,
                FontSize     = 11.5,
                TextWrapping = TextWrapping.Wrap,
                Foreground   = ClrBody,
                LineHeight   = 18,
            };
            Grid.SetColumn(dot,  0);
            Grid.SetColumn(lbl,  1);
            Grid.SetColumn(desc, 2);
            grid.Children.Add(dot);
            grid.Children.Add(lbl);
            grid.Children.Add(desc);
            return grid;
        }

        private TextBlock MakeText(string text) => new()
        {
            Text         = text,
            FontSize     = 11.5,
            TextWrapping = TextWrapping.Wrap,
            Foreground   = ClrBody,
            Margin       = new Thickness(12, 0, 0, 8),
            LineHeight   = 18,
        };

        private static UIElement Spacer(double h = 8) => new Border { Height = h };
    }
}
