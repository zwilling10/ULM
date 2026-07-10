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

            // ── Schließen-Button (nur bei Fehler) ──────────────────────────
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
            _btnClose.Click += (_, _) => Application.Current.Shutdown(1); // ExitCode 1 = Fehler
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

            worker.Completed += success => Dispatcher.Invoke(async () =>
            {
                _bar.Value    = success ? 100 : _bar.Value;
                _pctText.Text = success ? "100%" : _pctText.Text;

                AppendLog(new string('─', 52));

                if (success)
                {
                    // ── Erfolg: Fenster zeigt kurz die Bestätigung, dann Shutdown(0) ──
                    _titleText.Text       = $"✅ Ventoy wurde erfolgreich {(_updateMode ? "aktualisiert" : "installiert")}.";
                    _titleText.Foreground = BrushSuccess;
                    AppendLog("✅ Vorgang abgeschlossen.");
                    AppendLog("   Dieses Fenster schließt sich in 3 Sekunden …");

                    await Task.Delay(3000);

                    // ExitCode 0 signalisiert der normalen ULM-Instanz: Erfolg
                    Application.Current.Shutdown(0);
                }
                else
                {
                    // ── Fehler: Schließen-Button einblenden ───────────────────────────
                    _titleText.Text       = "❌ Ventoy-Installation fehlgeschlagen.";
                    _titleText.Foreground = BrushError;
                    AppendLog("❌ Bitte das Protokoll prüfen.");
                    AppendLog("   Schließen-Button: ExitCode 1 → normale ULM-Instanz zeigt Fehlermeldung.");
                    _btnClose.Visibility  = Visibility.Visible;
                    // Shutdown(1) wird durch den Schließen-Button ausgelöst
                }
            });

            await worker.RunAsync();
        }

        private void AppendLog(string msg)
        {
            _logText.Text += msg + "\n";
            _scroll.ScrollToBottom();
        }

        // Fenster-Schließen durch X verhindert stillen Exit ohne Code:
        // ExitCode 1 sicherstellen.
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Wenn der Worker noch läuft (kein btnClose sichtbar) und der
            // Benutzer das X klickt, als Fehler werten.
            Application.Current.Shutdown(1);
            base.OnClosing(e);
        }
    }
}
