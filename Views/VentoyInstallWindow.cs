// Views/VentoyInstallWindow.cs
//
// Kleines Fortschrittsfenster, das AUSSCHLIESSLICH in der Admin-Instanz
// angezeigt wird wenn ULM mit --ventoy-install gestartet wurde.
//
// ExitCode beim Beenden:
//   0 = Ventoy erfolgreich installiert / aktualisiert
//   1 = Installation fehlgeschlagen oder vom Benutzer abgebrochen
//
// Die normale (nicht-Admin) ULM-Instanz wartet auf diesen ExitCode
// über Process.WaitForExit() und aktualisiert danach die Stick-Anzeige.

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ULM.Core.Workers;

namespace ULM.Views
{
    public sealed class VentoyInstallWindow : Window
    {
        private readonly string      _letter;
        private readonly bool        _updateMode;
        private readonly bool        _secureBoot;

        private readonly TextBlock   _titleText;
        private readonly ProgressBar _bar;
        private readonly TextBlock   _pctText;
        private readonly TextBlock   _logText;
        private readonly ScrollViewer _scroll;
        private readonly Button      _btnClose;
        private int _exitCode = 1; // Standard: Fehler, bis ein Erfolg das Gegenteil belegt

        // Farben (keine Theme-Ressourcen nötig — dies ist ein Standalone-Fenster)
        private static readonly SolidColorBrush BrushBg      = new(Color.FromRgb(0x0D, 0x1B, 0x2A));
        private static readonly SolidColorBrush BrushCard    = new(Color.FromRgb(0x0A, 0x14, 0x1E));
        private static readonly SolidColorBrush BrushBlue    = new(Color.FromRgb(0x00, 0x75, 0xBE));
        private static readonly SolidColorBrush BrushText    = new(Color.FromRgb(0xC8, 0xD4, 0xE0));
        private static readonly SolidColorBrush BrushDim     = new(Color.FromRgb(0x4A, 0x6F, 0xA5));
        private static readonly SolidColorBrush BrushSuccess = new(Color.FromRgb(0x27, 0xAE, 0x60));
        private static readonly SolidColorBrush BrushError   = new(Color.FromRgb(0xE7, 0x4C, 0x3C));
        private static readonly SolidColorBrush BrushBorder  = new(Color.FromRgb(0x1A, 0x33, 0x55));

        public VentoyInstallWindow(string letter, bool updateMode, bool secureBoot)
        {
            _letter     = letter;
            _updateMode = updateMode;
            _secureBoot = secureBoot;

            Title             = "Universal Linux Manager — Ventoy";
            Width             = 520;
            Height            = 380;
            ResizeMode        = ResizeMode.CanMinimize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background        = BrushBg;

            // ── Layout ────────────────────────────────────────────────────
            var root = new Grid { Margin = new Thickness(22, 18, 22, 18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // Titel
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // ProgressBar
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) }); // Abstand
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Log
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // Schließen-Button

            // ── Titelzeile ─────────────────────────────────────────────────
            _titleText = new TextBlock
            {
                Text         = $"⚡ Ventoy wird {(updateMode ? "aktualisiert" : "installiert")} auf {letter} …",
                Foreground   = Brushes.White,
                FontSize     = 14,
                FontWeight   = FontWeights.SemiBold,
                Margin       = new Thickness(0, 0, 0, 16),
                TextWrapping = TextWrapping.Wrap,
            };
            Grid.SetRow(_titleText, 0);
            root.Children.Add(_titleText);

            // ── Fortschrittsbalken ─────────────────────────────────────────
            var barGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _bar = new ProgressBar
            {
                Minimum         = 0,
                Maximum         = 100,
                Value           = 0,
                Height          = 16,
                Foreground      = BrushBlue,
                Background      = BrushBorder,
                BorderThickness = new Thickness(0),
            };
            Grid.SetColumn(_bar, 0);
            barGrid.Children.Add(_bar);

            _pctText = new TextBlock
            {
                Text              = "0%",
                Foreground        = BrushText,
                FontSize          = 11,
                FontWeight        = FontWeights.SemiBold,
                Width             = 42,
                TextAlignment     = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(10, 0, 0, 0),
            };
            Grid.SetColumn(_pctText, 1);
            barGrid.Children.Add(_pctText);

            Grid.SetRow(barGrid, 1);
            root.Children.Add(barGrid);

            // ── Log-Ausgabe ────────────────────────────────────────────────
            _logText = new TextBlock
            {
                Foreground   = BrushDim,
                FontFamily   = new FontFamily("Consolas, Courier New"),
                FontSize     = 10.5,
                TextWrapping = TextWrapping.Wrap,
            };

            var logBorder = new Border
            {
                Background      = BrushCard,
                BorderBrush     = BrushBorder,
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(3),
                Padding         = new Thickness(10, 8, 10, 8),
            };
            _scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content                     = _logText,
            };
            logBorder.Child = _scroll;
            Grid.SetRow(logBorder, 3);
            root.Children.Add(logBorder);

            // ── Schließen-Button (bei Erfolg UND Fehler — siehe RunInstallationAsync) ──────
            // Schließt NIE automatisch: der Nutzer muss den Abschluss aktiv bestätigen, damit
            // eindeutig ist, dass die Installation fertig ist, bevor er den Stick entfernt.
            _btnClose = new Button
            {
                Content             = "✕ Schließen",
                Width               = 130,
                Height              = 34,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin              = new Thickness(0, 12, 0, 0),
                Visibility          = Visibility.Collapsed,
                Background          = BrushBorder,
                Foreground          = Brushes.White,
                BorderBrush         = BrushError,
                BorderThickness     = new Thickness(1),
                FontSize            = 12,
            };
            _btnClose.Click += (_, _) => Application.Current.Shutdown(_exitCode);
            Grid.SetRow(_btnClose, 4);
            root.Children.Add(_btnClose);

            Content = root;
            Loaded += async (_, _) => await RunInstallationAsync();
        }

        private async Task RunInstallationAsync()
        {
            AppendLog($"Laufwerk : {_letter}");
            AppendLog($"Modus    : {(_updateMode ? "Aktualisieren (ISOs bleiben erhalten)" : "Neuinstallation (Datenverlust!)")}");
            AppendLog($"Secure Boot: {(_secureBoot ? "Ja" : "Nein")}");
            AppendLog(new string('─', 52));

            var worker = new VentoyInstallWorker(_letter, _updateMode, _secureBoot);

            worker.ProgressLog += msg => Dispatcher.Invoke(() => AppendLog(msg));

            worker.Progress += (pct, msg) => Dispatcher.Invoke(() =>
            {
                _bar.Value    = pct;
                _pctText.Text = $"{pct}%";
            });

            worker.Completed += success => Dispatcher.Invoke(() =>
            {
                _bar.Value    = success ? 100 : _bar.Value;
                _pctText.Text = success ? "100%" : _pctText.Text;

                AppendLog(new string('─', 52));

                if (success)
                {
                    // ── Erfolg: NICHT automatisch schließen — der Nutzer bestätigt aktiv per
                    // Button. Ein automatischer Timer-Schluss könnte sich mit einer zufällig
                    // gleichzeitig auftretenden Laufwerks-Neuerkennung der normalen ULM-Instanz
                    // überschneiden; ein expliziter Klick macht den Abschluss eindeutig.
                    _exitCode              = 0;
                    _titleText.Text        = $"✅ Ventoy wurde erfolgreich {(_updateMode ? "aktualisiert" : "installiert")}.";
                    _titleText.Foreground  = BrushSuccess;
                    AppendLog("✅ Vorgang abgeschlossen.");
                    AppendLog("   Bitte 'Schließen' klicken, um fortzufahren.");
                    _btnClose.Content       = "✔ Schließen";
                    _btnClose.BorderBrush   = BrushSuccess;
                    _btnClose.Visibility    = Visibility.Visible;
                }
                else
                {
                    // ── Fehler: Schließen-Button einblenden ───────────────────────────
                    _exitCode              = 1;
                    _titleText.Text        = "❌ Ventoy-Installation fehlgeschlagen.";
                    _titleText.Foreground  = BrushError;
                    AppendLog("❌ Bitte das Protokoll prüfen.");
                    AppendLog("   Schließen-Button: ExitCode 1 → normale ULM-Instanz zeigt Fehlermeldung.");
                    _btnClose.Content       = "✕ Schließen";
                    _btnClose.BorderBrush   = BrushError;
                    _btnClose.Visibility    = Visibility.Visible;
                }
                // Shutdown wird in beiden Fällen erst durch den Schließen-Button ausgelöst
            });

            await worker.RunAsync();
        }

        private void AppendLog(string msg)
        {
            _logText.Text += msg + "\n";
            _scroll.ScrollToBottom();
        }

        // BUGFIX: Schließen (X, Alt+F4, ODER der Schließen-Button selbst — dessen Klick ruft
        // bereits Application.Current.Shutdown(_exitCode) auf, was Close() UND damit OnClosing
        // erneut auslöst) rief hier bisher IMMER Shutdown(1) auf — auch nach einer erfolgreichen
        // Installation. Da Environment.ExitCode zuletzt-gewinnt, wurde dadurch JEDER Erfolg beim
        // Beenden nachträglich zu ExitCode 1 überschrieben, sodass die normale ULM-Instanz eine
        // erfolgreiche Installation fälschlich als fehlgeschlagen meldete. ExitCode 1 wird jetzt nur
        // noch erzwungen, wenn der Worker beim Schließen NOCH LÄUFT (Schließen-Button noch nicht
        // sichtbar, siehe worker.Completed) — dann ist Abbruch tatsächlich gleichbedeutend mit
        // Fehler. Nach Abschluss ist _exitCode bereits korrekt gesetzt und bleibt unangetastet.
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_btnClose.Visibility != Visibility.Visible) _exitCode = 1;
            Application.Current.Shutdown(_exitCode);
            base.OnClosing(e);
        }
    }
}
