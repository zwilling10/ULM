// Views/Dialogs/ChangelogDialog.cs
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ULM.Views.Dialogs
{
    // ═══════════════════════════════════════════════════════════════════
    // ChangelogDialog — "Was ist neu?"
    //
    // Wird einmalig gezeigt, wenn sich die Version seit dem letzten Start
    // geändert hat (siehe MainWindow.OnLoaded: LastSeenVersion-Abgleich in
    // ulm_settings.ini). Bei jedem Release oben einen neuen Eintrag in
    // 'History' ergänzen — neueste Version zuerst.
    // ═══════════════════════════════════════════════════════════════════
    public sealed class ChangelogDialog : Window
    {
        private static readonly (string Version, string[] Notes)[] History =
        {
            ("2.37.0", new[]
            {
                "Neu: Schlägt ein Download mangels gefundener Quelle fehl, erscheint der Button „🔧 Quelle manuell suchen“ jetzt sofort direkt im Download-Fortschritt-Fenster — nicht erst nach mehreren aufeinanderfolgenden automatischen Fehlschlägen in der Hauptliste. Nach dem Eintragen einer Quelle startet der Download für diesen Eintrag automatisch neu.",
                "Neu: Erscheint der „🔧“-Button in der Hauptliste neu, weil die automatische Auflösung wiederholt scheitert, gibt es jetzt einen kurzen Hinweis dazu — bei genau einem betroffenen Eintrag ein sich selbst schließendes Popup, bei mehreren gleichzeitig ein dezentes Banner statt mehrerer Popups.",
            }),
            ("2.36.1", new[]
            {
                "Fehlerbehebung: Beim Start vom Ventoy-Stick erschien manchmal statt des Bootmenüs die Meldung „Failed to boot both default and fallback entries“ oder ein Absturz mit „alloc magic is broken“ — beides behoben.",
                "Fehlerbehebung: Im Bootmenü stand oben eine veraltete Versionsnummer und unten überlagerten sich mehrere Textzeilen; Titel, Versionsnummer und Stick-Auslastung (Speicherplatz, Anzahl ISOs) werden jetzt live und stets aktuell angezeigt.",
            }),
            ("2.36.0", new[]
            {
                "Neu: Button „🔧 Quelle manuell suchen/eintragen“ pro Distro-Zeile — erscheint nur noch als Sicherheitsnetz für echte Härtefälle, bei denen die automatische Quellensuche wiederholt erfolglos bleibt. Öffnet ein Fenster mit den bekannten Bearbeiten-Feldern plus Suchfunktion: findet ULM selbst nichts, öffnet ein Klick auf „Suchen“ stattdessen direkt eine vorausgefüllte Browser-Suche.",
                "Fehlerbehebung: „URLs prüfen“ fand für Einträge ohne bekannte Quelle (z.B. per „ISO suchen“ hinzugefügt) nie automatisch eine Quelle und meldete sie immer als nicht erreichbar — nutzt jetzt dieselbe Selbstlern-Auflösung wie Updates-Prüfung und Download.",
                "Fehlerbehebung: Hiren's BootCD PE wurde bei jedem Check fälschlich als „Update verfügbar“ gemeldet, obwohl sich nichts geändert hatte; ULM liest die tatsächliche Version jetzt von der Hiren's-Downloadseite, statt eine feste Versionsnummer anzunehmen.",
            }),
            ("2.35.1", new[]
            {
                "Fehlerbehebung: Findet ULM ein neueres Update (z.B. direkt nach dem Übernehmen einer vom Stick importierten ISO oder beim Gesundheitscheck), wird es jetzt sofort zum Aktualisieren angeboten — vorher erschien es zunächst nur als „Update verfügbar“ in der Liste, die eigentliche Frage kam erst beim nächsten Programmstart.",
                "Fehlerbehebung: Nach einer von ULM selbst durchgeführten Stick-Aktualisierung konnte zusätzlich zur Frage „Alte ISO löschen?“ fälschlich ein „ISO importieren?“-Dialog für genau diese alte Datei erscheinen.",
                "Fehlerbehebung: Der Gesundheitscheck-Dialog und ein anschließendes Update-Angebot konnten sich in seltenen Fällen überlagern — erscheinen jetzt nacheinander.",
                "Das Fenster \"ISO bearbeiten\" (Datenbank bearbeiten) passt seine Höhe jetzt automatisch an den Bildschirm an, damit alle Felder ohne Scrollen sichtbar sind.",
            }),
            ("2.35.0", new[]
            {
                "Neu: ULM prüft beim Start im Hintergrund, ob eine neuere Programmversion verfügbar ist, und zeigt in dem Fall ein Hinweis-Banner an. Per Klick lässt sich direkt die portable EXE oder der Setup-Installer herunterladen; ULM legt die Datei ab und öffnet den Ordner — gestartet wird sie wie gewohnt selbst.",
                "Neu: Schlägt ULM selbst ein Stick-Update vor und wird es durchgeführt, fragt das Programm anschließend, ob die alte, ersetzte ISO auf dem Stick gelöscht oder behalten werden soll.",
                "Fehlerbehebung: Auf den Stick kopierte ISOs, die ULM noch nicht kennt, werden jetzt bereits beim Programmstart erkannt und zum Übernehmen angeboten — vorher erst, nachdem der Stick ab- und wieder eingesteckt wurde.",
                "Fehlerbehebung: Zwei Datenbank-Einträge mit identischem Dateinamen (z.B. wenn mehrere importierte Einträge beim Versionscheck auf dieselbe aktuelle ISO zusammenfielen) blieben doppelt bestehen — solche exakten Duplikate werden jetzt automatisch entfernt.",
            }),
            ("2.34.0", new[]
            {
                "Neu: Sind mehrere USB-Sticks gleichzeitig angeschlossen, fragt ULM jetzt aktiv nach, mit welchem gearbeitet werden soll — sowohl beim Programmstart als auch beim Einstecken eines weiteren Sticks während der Laufzeit. Vorher wurde stillschweigend der erste erkannte Stick gewählt.",
                "Die Hilfe (❔) wurde um die neue Mehrfach-Stick-Auswahl und den Status-Reiter ergänzt.",
            }),
            ("2.33.0", new[]
            {
                "Fehlerbehebung: „Abbrechen“ während einer laufenden Stick-Integritätsprüfung zeigte zwar sofort „Abbruch.“ im Protokoll, die Prüfung lief im Hintergrund aber unbeeinflusst bis zum Ende weiter (bei mehreren ISOs über USB teils mehrere Minuten) — wirkt jetzt sofort.",
                "Neu: Reiter „Status“ (nur im Experten-Modus) — zeigt den aktuell laufenden Vorgang mit Datei/Fortschritt/Zähler, automatische Hintergrund-Scans, die nächste geplante automatische Aktion sowie einen Verlauf der letzten Hintergrund-Ereignisse. Ziel: volle Transparenz ohne einen Blick in den Task-Manager.",
                "Neu: optionaler Windows-Installer (Setup.exe) als Alternative zur portablen EXE — mit Startmenü-Eintrag und Deinstaller; fragt beim Deinstallieren nach, bevor heruntergeladene ISOs/Einstellungen mitgelöscht werden.",
                "Laufwerks-Überwachung von 4 auf 8 Sekunden verlangsamt — Erkennung von Stick-Wechseln bleibt aktiv, pollt aber seltener.",
            }),
            ("2.32.0", new[]
            {
                "Fehlerbehebung: ein durch Programmabsturz oder harten Kill mitten im Download unterbrochenes ISO konnte nach dem Neustart ungeprüft auf den Stick kopiert werden — die erwartete Zielgröße wird jetzt schon beim Download-Start gespeichert (nicht erst am Ende) und übersteht damit auch einen Absturz.",
                "Fehlerbehebung: der „(schneller)“-Mirror-Wechsel-Button erschien bisher bei jedem Download mit weiteren Mirror-Kandidaten, selbst bei bereits sehr guter Geschwindigkeit — erscheint jetzt erst nach Anlaufzeit und nur bei spürbar mittelmäßiger Übertragung.",
                "Fehlerbehebung: im kombinierten „Download → Stick-Kopie“-Modus zeigte die Gesamt-Fortschritts-Anzeige und die Abschluss-Meldung fälschlich vollen Erfolg, obwohl nur der Download geklappt hatte und die anschließende Stick-Kopie fehlgeschlagen war — beide zeigen jetzt das echte Kopier-Ergebnis.",
                "Neu: Hash-Status-Symbol in der Hauptliste — zeigt auf einen Blick, ob eine gespeicherte Prüfsumme vorhanden ist bzw. ob die letzte Integritätsprüfung eine Abweichung gefunden hat.",
                "Neu: Fortschrittsbalken färben sich abhängig vom Fortschritt (gedämpft am Anfang, grün kurz vor Fertigstellung).",
                "Neu: „🔁 Verpasste Kopien nachholen“ (vorher „Auf Stick kopieren“) — manuelles Sicherheitsnetz, falls die automatische Kopier-Nachfrage abgelehnt wurde oder eine Kopie fehlgeschlagen ist.",
                "Download-Fortschrittsfenster passt seine Höhe jetzt automatisch an die Bildschirmgröße an, damit bei mehreren parallelen Downloads mehr Zeilen ohne Bildlaufleiste sichtbar sind; die %-Anzeige wurde dabei bisher teils von der Bildlaufleiste verdeckt.",
                "Lange Tooltip-Texte wurden bisher als eine einzige, bildschirmbreite Zeile angezeigt — brechen jetzt lesbar um.",
            }),
            ("2.31.1", new[]
            {
                "Fehlerbehebung: ULM meldete eine bereits aktuelle Stick-ISO fälschlich als veraltet, wenn eine alte Version nie gelöscht wurde — bietet jetzt stattdessen das Löschen der alten Datei an.",
                "Neu: SHA-256-Integritätsprüfung — nach Download/Import wird ein Referenzhash gespeichert, bei Ubuntu/Debian/Fedora zusätzlich gegen die offizielle Prüfsumme verifiziert. Manuelle Prüfung über den neuen Button 'Integrität prüfen'.",
            }),
            ("2.31.0", new[]
            {
                "Neu: Autostart-Option — Checkbox im Einrichtungsfenster startet ULM ab sofort automatisch mit Windows, kein Admin-Recht nötig",
            }),
            ("2.30.0", new[]
            {
                "Neu: „🔍 ISO suchen“ zeigt jetzt zwei Online-Listen von DistroWatch — „🆕 Aktuellste“ (neu hinzugefügte Distros) und „🔥 Beliebteste“ (Popularitäts-Ranking), beide gefiltert auf garantiert per USB-Stick bootfähige Live-Medium-Distros, mit Kategorie-Vorschlag, Tooltip und optionalem Direkt-Download. Die frühere reine Textsuche entfällt (dafür: „🗃 Datenbank“)",
                "Neu: Mirror-Race — vor jedem Download werden alle konfigurierten Mirror-Quellen kurz parallel getestet und automatisch mit der schnellsten begonnen, statt einfach der ersten",
                "Neu: Geschwindigkeits-Wächter bricht dauerhaft extrem langsame Downloads automatisch ab und wechselt zur nächsten Quelle; bleibt nur eine langsame Quelle übrig, fragt ULM aktiv nach, ob trotzdem fortgefahren werden soll",
                "Neu: Freispeicher-Vorabprüfung — summiert vor Beginn eines Downloads die Größe ALLER markierten Distros und warnt, wenn der Speicherplatz im Arbeitsordner oder auf dem Stick nicht reicht, statt erst mittendrin zu scheitern",
                "DB-Gesundheitscheck-Fenster: Versions-/Status-Text saß ohne Abstand an der rechten Fensterkante — jetzt mit sichtbarem Rand",
                "Diverse Dialoge (DB-Gesundheitscheck, ISO-Editor, Stick-Import, Download-Fenster) im Dark Mode: mehrere Texte hatten keine explizite Vorder-/Hintergrundfarbe und blieben dadurch teils unlesbar hell",
            }),
            ("2.29.1", new[]
            {
                "Einrichtungsfenster passte sich bisher nicht an kleine Bildschirme an — auf 800x600 ragte es über den Bildschirm hinaus und der 'Übernehmen'-Button war unsichtbar. Größe richtet sich jetzt nach dem tatsächlichen Bildschirm-Arbeitsbereich, Kopf- und Fußzeile bleiben immer sichtbar.",
            }),
            ("2.29.0", new[]
            {
                "Neu: Dark Mode — Design-Wahl System/Hell/Dunkel im Setup-Dialog oder jederzeit über den Knopf oben rechts im Hauptfenster, schaltet sofort um (kein Neustart nötig)",
                "\"System\" übernimmt automatisch die Windows-Design-Einstellung und folgt ihr auch live, wenn sie sich während der Laufzeit ändert",
                "Alle Listen, Dialoge und Eingabefelder wurden für gute Lesbarkeit im Dark Mode durchgestylt und geprüft",
            }),
            ("2.28.1", new[]
            {
                "Fenstertitel zeigte fälschlich immer eine fest hinterlegte, veraltete Versionsnummer statt der tatsächlich installierten — jetzt dynamisch aus der Programmversion gelesen",
                "Neues Programm-Icon (passend zum Logo der Projektseite) für EXE, Taskleiste und Fenster-Titelleiste",
            }),
            ("2.28.0", new[]
            {
                "Neuer Automatismus löst für JEDE unbekannte/importierte Distro automatisch die Download-Quelle auf (DistroWatch- und SourceForge-Suche als zusätzliche Stufen) — nicht mehr nur für fest hinterlegte Distros",
                "Neu gefundene Download-Quellen für importierte ISOs werden jetzt zuverlässig dauerhaft gespeichert, statt bei jedem Neustart wieder zu verschwinden",
                "Erreichbarkeits-Checks werden kurz zwischengespeichert und der automatische Scan pausiert leicht zwischen Einträgen — verringert fälschliche 'nicht erreichbar'-Meldungen durch Bot-/Anti-Scraping-Schutz externer Server",
                "Download-Fortschritt zeigt jetzt die geschätzte Restzeit (ETA) an",
                "Freispeicher-Check vor jedem Download und Kopiervorgang — bricht rechtzeitig mit klarer Meldung ab statt mittendrin zu scheitern",
                "Log-Datei (ulm_log.txt) wird ab 5 MB automatisch rotiert, wächst also nicht mehr unbegrenzt",
                "Optionales GitHub-Token (Experten-Modus) hebt das API-Anfragelimit für GitHub-basierte Erkennung und den ULM-Update-Check deutlich an",
                "ULM prüft jetzt selbst im Hintergrund auf neue Versionen und meldet sie im Protokoll",
                "Neuer „Was ist neu?“-Dialog zeigt nach einem Update automatisch die Änderungen seit der zuletzt genutzten Version",
                "Ersteinrichtungs-Dialog: 'Nicht mehr anzeigen' übersprang bisher nur den Begrüßungstext, jetzt tatsächlich den kompletten Dialog beim nächsten Start; Farben an die App angeglichen",
            }),
            ("2.27.1", new[]
            {
                "Ventoy-Installation läuft jetzt tatsächlich still im Hintergrund (vorher fielen ungültige Kommandozeilenparameter lautlos auf die interaktive Ventoy-Oberfläche zurück)",
                "Doppelte Installationsfenster/Abfragen bei der Ventoy-Einrichtung behoben",
                "Ventoy-ZIP-Download schlug durch eine fälschlich angewendete 300-MB-Mindestgrößenprüfung immer fehl",
                "Automatischer Gesundheitscheck läuft jetzt gezielt nur noch bei neuen, unverifizierten Einträgen statt bei jedem Stick-Scan",
            }),
        };

        public ChangelogDialog(string previousVersion, string currentVersion)
        {
            Title = "Was ist neu?";
            Width = 560; MinHeight = 260; MaxHeight = 560;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = (Brush)Application.Current.Resources["BrushBg"];

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var root   = new StackPanel { Margin = new Thickness(22) };

            root.Children.Add(new TextBlock
            {
                Text = $"🆕 Aktualisiert von v{previousVersion} auf v{currentVersion}",
                FontSize = 15, FontWeight = FontWeights.Bold,
                Foreground = (Brush)Application.Current.Resources["BrushHeader"],
                Margin = new Thickness(0, 0, 0, 16),
            });

            // Nur Versionen NEUER als 'previousVersion' anzeigen, nicht die gesamte Historie —
            // wer von einer älteren Version kommt, sieht so alle übersprungenen Änderungen auf
            // einen Blick, statt nur die allerletzte.
            var relevant = History.Where(h => IsNewer(h.Version, previousVersion)).ToList();
            if (relevant.Count == 0) relevant = History.Take(1).ToList();

            foreach (var (version, notes) in relevant)
            {
                root.Children.Add(new TextBlock
                {
                    Text = $"Version {version}", FontWeight = FontWeights.SemiBold, FontSize = 12.5,
                    Foreground = (Brush)Application.Current.Resources["BrushBlue"],
                    Margin = new Thickness(0, 8, 0, 6),
                });
                foreach (string note in notes)
                    root.Children.Add(new TextBlock
                    {
                        Text = "•  " + note, TextWrapping = TextWrapping.Wrap, FontSize = 11.5,
                        Foreground = (Brush)Application.Current.Resources["BrushMid"],
                        Margin = new Thickness(4, 0, 0, 5), LineHeight = 17,
                    });
            }

            var btn = new Button
            {
                Content = "✔ Verstanden", Width = 130, HorizontalAlignment = HorizontalAlignment.Right,
                Style = (Style)Application.Current.Resources["BtnPrimary"], Margin = new Thickness(0, 18, 0, 0),
            };
            btn.Click += (_, _) => Close();
            root.Children.Add(btn);

            scroll.Content = root;
            Content = scroll;
            KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Escape) Close(); };
        }

        // Simpler numerischer Teil-für-Teil-Vergleich reicht hier — Changelog-Versionsnummern sind
        // immer reine "Major.Minor.Patch"-Strings ohne Suffixe.
        private static bool IsNewer(string a, string b)
        {
            int[] pa = a.Split('.').Select(p => int.TryParse(p, out int n) ? n : 0).ToArray();
            int[] pb = b.Split('.').Select(p => int.TryParse(p, out int n) ? n : 0).ToArray();
            for (int i = 0; i < System.Math.Max(pa.Length, pb.Length); i++)
            {
                int x = i < pa.Length ? pa[i] : 0, y = i < pb.Length ? pb[i] : 0;
                if (x != y) return x > y;
            }
            return false;
        }
    }
}
