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
            Title  = "❓ Universal Linux Manager — Hilfe & Dokumentation";
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
            content.Children.Add(MakeSub("Bootfähige USB-Sticks mit Linux-ISOs einfach erstellen und verwalten."));
            content.Children.Add(Spacer(16));

            tocPanel.Children.Add(new TextBlock
            {
                Text = "SPRUNGMARKEN", FontSize = 9.5, FontWeight = FontWeights.Bold,
                Foreground = ClrSub, Margin = new Thickness(6, 0, 0, 8),
            });

            // ── Übersicht ──────────────────────────────────────────────────
            AddSection("🗺 Übersicht — Was macht ULM?", "Übersicht");
            content.Children.Add(MakeText(
                "ULM ist ein Manager für Linux-Live-ISOs und Ventoy-USB-Sticks. Es erledigt vier Aufgaben:\n" +
                "  1. ISO-Downloads — lädt aktuelle Linux-Versionen direkt von den offiziellen Servern herunter\n" +
                "  2. USB-Verwaltung — installiert Ventoy auf dem Stick und kopiert ISOs dorthin\n" +
                "  3. Versionsüberwachung — prüft automatisch ob neuere ISO-Versionen verfügbar sind\n" +
                "  4. Datenmüll-Schutz — erkennt unvollständige/korrupte ISOs per Online-Größenprüfung, " +
                "sowohl im Arbeitsordner als auch auf dem Stick"));
            content.Children.Add(Spacer());

            // ── Programmstart ──────────────────────────────────────────────
            AddSection("🚀 Was passiert beim Programmstart?", "Programmstart");
            content.Children.Add(MakeText("Direkt nach dem Start laufen automatisch im Hintergrund, in dieser Reihenfolge:"));
            content.Children.Add(MakeItem("1. Online-Versionscheck",
                "Fragt zuerst für alle Distros in der Datenbank die aktuellste Version ab (ca. 5–30 Sek.) — " +
                "auch für vom Stick importierte Einträge. Findet neue Versionen automatisch — ohne manuelle " +
                "Eingabe von URLs. Aktualisiert die Datenbank-Einträge wenn eine neue Version verfügbar ist. " +
                "Ein pulsierender Hinweis oben in der Kopfzeile ('Online-Scan, bitte warten') zeigt an, dass " +
                "der Check noch läuft — am besten bis dahin noch nicht klicken, damit Datenbank und Stick-" +
                "Stand vollständig sind."));
            content.Children.Add(MakeItem("2. USB-Stick-Scan",
                "Läuft erst NACH dem Versionscheck (nicht gleichzeitig), damit der Stick-Stand direkt mit den " +
                "aktuellsten Versionsdaten verglichen wird. Erkennt angeschlossene Ventoy-Sticks, zeigt welche " +
                "ISOs bereits drauf sind, welche veraltet sind und welche fehlen. Läuft erneut, wenn ein Stick " +
                "eingesteckt wird (derselbe pulsierende Hinweis, dann 'Stick-Scan, bitte warten'). Prüft dabei " +
                "jedes Mal zusätzlich die Online-Größe jeder gefundenen ISO (siehe 🧹 Datenmüll-Schutz)."));
            content.Children.Add(MakeItem("Datei-Wartung",
                "Läuft nach dem Versionscheck. Scannt den Arbeitsordner rekursiv und vergleicht jede ISO-Größe " +
                "mit der tatsächlichen Original-Größe beim Anbieter (Online-HEAD-Request). Erkennt so " +
                "unvollständige und abgebrochene Downloads zuverlässiger als eine feste Mindestgröße. " +
                "Bietet an, gefundenen Datenmüll zu löschen."));
            content.Children.Add(MakeItem("ULM-Update-Check",
                "Prüft im Hintergrund, ob auf GitHub eine neuere ULM-Version verfügbar ist. Läuft rein " +
                "informativ mit — kein Dialog, keine Unterbrechung. Ist eine neue Version verfügbar, " +
                "erscheint nur eine Zeile im Protokoll:\n" +
                "  🆕 Neue ULM-Version verfügbar: vX.Y.Z (aktuell installiert: vA.B.C)\n" +
                "gefolgt vom Link zur Release-Seite."));
            content.Children.Add(MakeItem("„Was ist neu?“-Dialog",
                "Erscheint automatisch beim ersten Start NACH einem Update auf eine neue ULM-Version " +
                "(nicht beim allerersten Programmstart) und listet alle Änderungen seit der zuletzt " +
                "gesehenen Version auf. Einmal quittiert, erscheint er erst beim nächsten Versionswechsel wieder."));
            content.Children.Add(MakeItem("🚀 Autostart (optional)",
                "Checkbox 'Mit Windows starten' im Einrichtungsfenster — startet ULM dann automatisch " +
                "(sichtbares Fenster) bei jeder Windows-Anmeldung. Kein Admin-Recht nötig, funktioniert über " +
                "einen Registry-Eintrag nur für den aktuellen Benutzer. Lässt sich im Einrichtungsfenster " +
                "jederzeit wieder abwählen; ist das Fenster einmal per 'Nicht mehr anzeigen' übersprungen, " +
                "hilft ein Löschen des passenden Eintrags in 'ulm_settings.ini', um es erneut zu sehen."));
            content.Children.Add(Spacer());

            // ── Hauptliste ─────────────────────────────────────────────────
            AddSection("📋 Die Verteilungs-Liste — Bedienung", "Bedienung");
            content.Children.Add(MakeItem("ISO zum Download auswählen",
                "Checkbox links aktivieren → ISO wird zum Download vorgemerkt (blauer Hintergrund). " +
                "Mehrere ISOs gleichzeitig auswählen ist möglich."));
            content.Children.Add(MakeItem("Kategorie-Checkbox",
                "Aktiviert oder deaktiviert alle Distros einer Kategorie auf einmal " +
                "(z.B. alle 'Sicherheits'-Distros markieren)."));
            content.Children.Add(MakeItem("Doppelklick auf Eintrag",
                "Zeigt die Beschreibung der Distribution — Einsatzzweck, Besonderheiten, Zielgruppe."));
            content.Children.Add(MakeItem("Mouseover (Tooltip)",
                "Hält man die Maus über den Distro-Namen, erscheint ein Tooltip. " +
                "Er erklärt alle sichtbaren Symbole (📥, 🌐✓/✗, 🆕) UND zeigt die Distro-Beschreibung."));
            content.Children.Add(Spacer());

            // ── Farben & Symbole ───────────────────────────────────────────
            AddSection("🎨 Farben & Symbole im Hauptfenster", "Farben & Symbole");

            content.Children.Add(MakeSubhead("Textfarben der Listeneinträge"));
            content.Children.Add(MakeColorItem(SwGreen,  "Grün",
                "ISO ist auf dem USB-Stick vorhanden (aktuellste Version, online größengeprüft) — " +
                "oder lokal vollständig heruntergeladen und bereit zum Kopieren."));
            content.Children.Add(MakeColorItem(SwOrange, "Orange",
                "Update verfügbar — online wurde eine neuere Version gefunden. " +
                "Oder: veraltete Version auf dem Stick (neuere Version existiert)."));
            content.Children.Add(MakeColorItem(SwRed, "Rot",
                "URL nicht erreichbar — der Download-Server antwortet nicht. " +
                "Erscheint nach einem URL-Check (Expert-Modus)."));
            content.Children.Add(MakeColorItem(SwTeal, "Türkis",
                "Vom USB-Stick importiert — dieser Eintrag wurde beim Stick-Scan " +
                "entdeckt und als neuer Eintrag hinzugefügt."));
            content.Children.Add(MakeColorItem(SwBlue, "Gedämpftes Blau",
                "Online-Check bestätigt: diese Version ist aktuell. " +
                "Kein Update nötig, ISO ist auf dem neuesten Stand."));
            content.Children.Add(MakeColorItem(SwGray, "Hellgrau",
                "Keine URL konfiguriert — für diesen Eintrag sind keine " +
                "Download-URLs hinterlegt."));
            content.Children.Add(MakeColorItem(SwDark, "Dunkel (Standard)",
                "Normaler Zustand — noch kein Online-Versionscheck durchgeführt, " +
                "ISO nicht lokal und nicht auf dem Stick."));
            content.Children.Add(Spacer(6));

            content.Children.Add(MakeSubhead("Spalten in der Liste"));
            content.Children.Add(MakeItem("Lokal",
                "Zeigt ob die ISO im lokalen Arbeitsordner vorhanden ist:\n" +
                "  'Lokal 3 565 MB' = heruntergeladen (mit Dateigröße)\n" +
                "  'nicht lokal'    = noch nicht heruntergeladen"));
            content.Children.Add(MakeItem("Auf dem Stick",
                "Zeigt den Status auf dem erkannten Ventoy-Stick:\n" +
                "  'Ja 3,56 GB'  = vorhanden, aktuelle Version, Online-Größe bestätigt\n" +
                "  'Veraltet …'  = auf dem Stick, aber veraltete Version\n" +
                "  'Nein'        = ISO fehlt auf dem Stick ODER wurde als unvollständig erkannt und entfernt\n" +
                "  'Ungeprüft'   = Stick wurde noch nicht gescannt"));
            content.Children.Add(MakeItem("Aktuell",
                "Zeigt das Ergebnis des Online-Versionschecks:\n" +
                "  'Update vX.Y.Z'     = neuere Version online verfügbar\n" +
                "  'Aktuell (vX.Y.Z)'  = Online-Check: bereits aktuellste Version\n" +
                "  'Lokal vorhanden'   = lokal vorhanden, kein Online-Check\n" +
                "  '?'                 = noch nicht geprüft"));
            content.Children.Add(Spacer(6));

            content.Children.Add(MakeSubhead("Symbole im Distro-Namen (Mouseover zeigt Erklärung)"));
            content.Children.Add(MakeItem("📥 (Präfix)",
                "Vom USB-Stick importiert — diese ISO wurde beim Stick-Scan entdeckt " +
                "und als neuer Eintrag hinzugefügt (nicht aus der Standard-Datenbank)."));
            content.Children.Add(MakeItem("🌐✓ (Suffix)",
                "URL-Check bestanden — die Download-URL ist erreichbar. " +
                "Mouseover zeigt: 'URL erreichbar — Download-Server antwortet'."));
            content.Children.Add(MakeItem("🌐✗ (Suffix)",
                "URL-Check fehlgeschlagen — die Download-URL ist nicht erreichbar. " +
                "Mouseover zeigt: 'URL nicht erreichbar — Download-Server antwortet nicht'."));
            content.Children.Add(MakeItem("🆕 vX.Y.Z (Suffix)",
                "Online wurde eine neuere Version (hier beispielhaft: vX.Y.Z) gefunden. " +
                "Mouseover zeigt: 'Neue Version verfügbar: vX.Y.Z (jetzt herunterladen)'. " +
                "Eintrag auswählen und Download starten."));
            content.Children.Add(Spacer(6));

            content.Children.Add(MakeSubhead("Kategorie-Symbole (linke Spalte)"));
            content.Children.Add(MakeText(
                "  🖥 Einsteiger        — Benutzerfreundliche Distributionen für den Desktop-Einstieg\n" +
                "  ⚙ Fortgeschrittene  — Mehr Konfigurationsfreiheit, Arch-basierte Systeme\n" +
                "  🪶 Leichtgewicht     — Ressourcensparend, für ältere und schwächere Hardware\n" +
                "  🎮 Gaming            — Für Spiele optimiert (ProtonGE, Steam, MangoHud)\n" +
                "  🔒 Sicherheit        — Datenschutz, Anonymität, Pen-Testing (Tails, Parrot, Kodachi)\n" +
                "  🛠 Rettung           — Rettungs- und Reparatur-Live-Systeme (GParted, Clonezilla)\n" +
                "  🛡 Antivirus         — Live-Systeme zur Virenprüfung und -entfernung\n" +
                "  🪟 WinPE             — Windows-basierte Rettungsumgebungen (Hiren's BootCD)"));
            content.Children.Add(Spacer());

            // ── Design (Hell/Dunkel) ───────────────────────────────────────
            AddSection("🌓 Design — Hell / Dunkel / System", "Design");
            content.Children.Add(MakeText(
                "ULM hat ein helles und ein dunkles Erscheinungsbild. Beide sind vollständig " +
                "durchgestylt (Listen, Dialoge, Eingabefelder) und für gute Lesbarkeit geprüft."));
            content.Children.Add(MakeItem("Einstellen",
                "Beim Ersteinrichten im Setup-Dialog wählbar, oder jederzeit über den Knopf " +
                "'🌓 Design: …' oben rechts im Hauptfenster (neben 'Modus: Anwender/Experte'). " +
                "Ein Klick wechselt der Reihe nach zwischen System → Hell → Dunkel."));
            content.Children.Add(MakeItem("System",
                "Übernimmt automatisch die aktuelle Windows-Design-Einstellung (Hell oder Dunkel). " +
                "Ändert sich das Windows-Design während ULM läuft, zieht ULM automatisch nach — " +
                "ohne Neustart."));
            content.Children.Add(MakeItem("Sofortige Umschaltung",
                "Ein Wechsel wirkt sofort auf das gesamte offene Hauptfenster — inklusive der " +
                "Zeilenfarben in der Distro-Liste. Kein Neustart nötig. Neu geöffnete Dialoge " +
                "(Hilfe, Datenbank, Einrichtung, …) übernehmen die Wahl automatisch."));
            content.Children.Add(MakeItem("Merkt sich die Wahl",
                "Die getroffene Wahl wird gespeichert und beim nächsten Programmstart automatisch " +
                "wieder angewendet."));
            content.Children.Add(Spacer());

            // ── Protokoll-Symbole ─────────────────────────────────────────
            AddSection("📜 Protokoll-Symbole — Bedeutung", "Protokoll-Symbole");
            content.Children.Add(MakeText(
                "  ▶   Programmstart / Abschnittsbeginn\n" +
                "  💾  Datenbank-Aktion oder Stick-Scan\n" +
                "  🔌  Laufwerk erkannt / Stick eingesteckt\n" +
                "  🌐  Online-Versionscheck läuft\n" +
                "  ⬇   Download gestartet oder in Bearbeitung\n" +
                "  🔗  Download-URL (zeigt welcher Server verwendet wird)\n" +
                "  ✅  Aktion erfolgreich abgeschlossen\n" +
                "  ❌  Fehler aufgetreten\n" +
                "  ⚠   Warnung (kein Fehler, aber Aufmerksamkeit nötig) — u.a. unvollständige Dateien\n" +
                "  🆕  Neue Version online gefunden\n" +
                "  ✓   Version ist aktuell (kein Update nötig)\n" +
                "  ✏   Anzeigename automatisch aktualisiert\n" +
                "  ↔   Dateiname in der Datenbank ersetzt\n" +
                "  🗑  Eintrag oder Datei gelöscht (auch: Datenmüll auf dem Stick entfernt)\n" +
                "  🔄  Duplikat zusammengeführt\n" +
                "  📋  Kopiervorgang auf den USB-Stick\n" +
                "  📂  Datei beim Import in den Kategorie-Ordner auf dem Stick verschoben\n" +
                "  ❓  Unbekannte ISO(s) auf dem Stick gefunden — Import möglich\n" +
                "  ⛔  Vorgang abgebrochen"));
            content.Children.Add(Spacer());

            // ── ISO suchen (Online-Entdeckung) ────────────────────────────
            AddSection("🔍 ISO suchen — neue Distros entdecken", "ISO suchen");
            content.Children.Add(MakeText(
                "Der Knopf '🔍 ISO suchen' zeigt zwei Online-Listen von DistroWatch.com — eine " +
                "Möglichkeit, gezielt neue Distros zu entdecken, statt nur die feste Standard-Datenbank " +
                "durchzugehen. Für die bereits bekannte Datenbank gibt es weiterhin '🗃 Datenbank'."));
            content.Children.Add(MakeItem("🆕 Aktuellste",
                "Die zuletzt neu zu DistroWatch hinzugefügten Distributionen (Top 10)."));
            content.Children.Add(MakeItem("🔥 Beliebteste",
                "DistroWatchs Page-Hit-Ranking (Top 10) — die aktuell meistbesuchten Distro-Profile."));
            content.Children.Add(MakeItem("Nur Live-Medium",
                "Beide Listen zeigen AUSSCHLIESSLICH Distros mit dem DistroWatch-Kategorie-Tag " +
                "'Live Medium' — reine Installations- oder Server-Images ohne Live-Boot-Modus werden " +
                "automatisch aussortiert. Jeder Vorschlag ist also garantiert per USB-Stick bootfähig."));
            content.Children.Add(MakeItem("Bereits vorhanden",
                "Distros, die schon in der eigenen Datenbank stehen, werden blau hervorgehoben und " +
                "können nicht erneut übernommen werden. Bei neuen Distros zeigt ein Mouseover-Tooltip " +
                "Rang/Datum, vorgeschlagene Kategorie und den DistroWatch-Link."));
            content.Children.Add(MakeItem("Übernehmen + Direkt herunterladen",
                "Ausgewählte Distros per '✔ Übernehmen' in die Datenbank aufnehmen (Kategorie vorher " +
                "per Dropdown anpassbar). Ist zusätzlich 'Direkt herunterladen' angehakt, startet nach " +
                "dem Schließen des Fensters sofort der reguläre Download-Ablauf für diese Einträge."));
            content.Children.Add(MakeItem("Aktualisieren / Cache",
                "Beide Listen werden 24 Stunden lokal zwischengespeichert (kein Netzwerk-Roundtrip bei " +
                "jedem Öffnen). Der Knopf '⟳ Aktualisieren' erzwingt eine frische Abfrage."));
            content.Children.Add(Spacer());

            // ── Download ───────────────────────────────────────────────────
            AddSection("⬇ Download — Wie und Wohin?", "Download");
            content.Children.Add(MakeItem("Speicherort",
                "Alle ISOs werden im Arbeitsordner des Programms gespeichert (Unterordner 'ISOs'). " +
                "Da ULM portabel ist, liegt der Arbeitsordner neben der Programmdatei — " +
                "der genaue Pfad hängt davon ab, wohin ULM gespeichert wurde."));
            content.Children.Add(MakeItem("Pipeline-Modus",
                "Wenn ein Ventoy-Stick erkannt wird, kann jede ISO direkt nach dem Download " +
                "auf den Stick kopiert werden. Die lokale Datei wird danach gelöscht. " +
                "Downloads und Kopieren laufen parallel. Im Fortschritts-Dialog wechselt die Zeile " +
                "einer ISO von 'Kopiere auf Stick' zu 'Fertig', sobald sie vollständig kopiert ist."));
            content.Children.Add(MakeItem("Mirror-Race (bis zu 8 Quellen)",
                "Bevor der eigentliche Download beginnt, testet ULM alle konfigurierten Mirror-URLs " +
                "einer Distro parallel für ca. 3 Sekunden und startet dann mit der schnellsten Quelle " +
                "— nicht einfach mit der ersten. Gemessen wird in kurzen Zeitfenstern statt eines " +
                "einzigen Durchschnittswerts, damit CDNs, die erst nach ein bis zwei Sekunden auf volle " +
                "Geschwindigkeit hochfahren, nicht fälschlich als langsam eingestuft werden. Bei " +
                "SourceForge-Quellen fächert ULM zusätzlich automatisch mehrere geografisch verteilte " +
                "Mirror auf, statt sich auf SourceForges eigene (oft nicht optimale) Serverwahl zu " +
                "verlassen. Ergebnis erscheint im Protokoll:\n" +
                "  🔎 Distro: Mirror-Test — cdn1.beispiel.org 42,3 Mbit/s, …"));
            content.Children.Add(MakeItem("Geschwindigkeits-Wächter",
                "Bleibt eine laufende Übertragung (nach ca. 20 Sekunden Anlaufzeit) für weitere 20 " +
                "Sekunden ununterbrochen unter ca. 1 MB/s, bricht ULM automatisch ab und versucht die " +
                "nächste Mirror-Quelle — statt stundenlang auf einer extrem langsamen Verbindung zu " +
                "warten. Gibt es keine schnellere Quelle mehr und alle Versuche waren nur an der " +
                "Geschwindigkeit gescheitert (nicht an einem echten Fehler), fragt ULM aktiv nach:\n" +
                "  ⚠ Kein schnellerer Mirror gefunden — trotzdem mit dieser Quelle fortfahren?\n" +
                "Bestätigt man das, läuft der Download ohne weitere Geschwindigkeitsprüfung zu Ende."));
            content.Children.Add(MakeItem("„(schneller)“-Button",
                "Erscheint im Download-Fortschrittsfenster neben einer laufenden Übertragung, sobald " +
                "noch mindestens ein weiterer, vom Mirror-Race bereits gemessener Kandidat übrig ist — " +
                "auch dann, wenn der aktuelle Server über der Geschwindigkeits-Wächter-Schwelle liegt " +
                "(also selbst gar nicht automatisch abbrechen würde), einem aber trotzdem zu langsam " +
                "vorkommt. Klick bricht den aktuellen Versuch ab und wechselt sofort zum nächsten " +
                "Kandidaten. Findet sich dabei kein schnellerer Server, kehrt ULM automatisch — ohne " +
                "Nachfrage — zum ursprünglichen, nachweislich erreichbaren Server zurück " +
                "('Kein schnellerer Server gefunden — Download wird fortgesetzt')."));
            content.Children.Add(MakeItem("Verbleibende Zeit (ETA)",
                "Der Fortschritts-Dialog zeigt neben Geschwindigkeit und Größe auch die geschätzte " +
                "Restzeit an, z.B.:\n  12.4 MB/s  ·  noch 2m 14s  ·  1.2 GB / 3.5 GB\n" +
                "Die Schätzung passt sich laufend an die aktuelle Download-Geschwindigkeit an."));
            content.Children.Add(MakeItem("Freispeicher-Check",
                "Zweistufig: BEVOR der Download überhaupt startet, summiert ULM die online " +
                "ermittelbare Größe ALLER markierten Distros und vergleicht sie mit dem freien " +
                "Speicher im Arbeitsordner UND — falls direkt auf einen Stick kopiert werden soll — " +
                "zusätzlich mit dem freien Speicher dort. Reicht der Platz nicht, warnt ULM VOR " +
                "Beginn mit einer Ja/Nein-Rückfrage, statt erst mittendrin auf mehreren parallelen " +
                "Downloads zugleich zu scheitern. Zusätzlich prüft ein zweiter, feingranularer Check " +
                "unmittelbar vor jeder einzelnen Datei erneut den dann noch verfügbaren Platz:\n" +
                "  ❌ Nicht genug Speicherplatz auf X:\\ (benötigt 3.5 GB, frei 1.1 GB)."));
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
            content.Children.Add(MakeItem("Ventoy-Bootmenü",
                "Wird automatisch nach jedem Kopiervorgang UND nach jedem ISO-Import vom Stick aktualisiert. " +
                "Enthält leserliche Namen, Beschreibungen und Kategorien aus der Datenbank."));
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
            content.Children.Add(MakeText("Expert-Modus aktivieren: oben rechts 'Modus: Anwender' → klicken."));
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
