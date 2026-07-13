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
