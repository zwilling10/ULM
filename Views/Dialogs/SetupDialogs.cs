// Views/Dialogs/SetupDialogs.cs
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using ULM.Core.Models;

namespace ULM.Views.Dialogs
{
    // ═════════════════════════════════════════════════════════════════
    // SETUP DIALOG — fasst Arbeitsordner-Auswahl (nur beim allerersten
    // Start), Willkommenstext (überspringbar) und Modus-Wahl (immer) in
    // EINEM Fenster mit Checkboxen und einem einzigen "Übernehmen"-Button
    // zusammen, statt bis zu drei getrennte Dialoge nacheinander zu zeigen.
    // ═════════════════════════════════════════════════════════════════
    public sealed class SetupDialog : Window
    {
        public string ChosenDirectory  { get; private set; } = string.Empty;
        public bool   DontShowAgain    { get; private set; }
        public bool   ExpertModeChosen { get; private set; }

        private static string DefaultBase =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "UniversalLinuxManager");

        public SetupDialog(bool showDirectory, bool showWelcome, bool currentExpertMode = false)
        {
            Title  = "Universal Linux Manager — Einrichtung";
            Width  = 760;
            MinWidth  = 700;
            // Höhe wird von WPF an den tatsächlichen Inhalt angepasst (siehe MaxHeight/ScrollViewer
            // unten als Sicherheitsnetz) statt einer manuell geschätzten Pixelzahl — eine
            // handgerechnete Höhe war je nach gezeigten Abschnitten (Erststart/Willkommen/Modus)
            // zu knapp bemessen und schnitt den unteren Inhalt sichtbar ab.
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Brush(Constants.ColorBg);

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Body
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer

            // ── HEADER ───────────────────────────────────────────────
            // Farben aus Constants (Glary Design System) statt eigenständiger Ad-hoc-Palette —
            // vorher nutzte dieses Fenster ein anderes Blau (#2563EB) als der Rest der App
            // (Constants.ColorBlue #0075BE), wirkte dadurch wie ein Fremdkörper vor dem
            // eigentlichen Hauptfenster.
            var header = new Border
            {
                Background = new LinearGradientBrush(
                    (Color)ColorConverter.ConvertFromString(Constants.ColorMid),
                    (Color)ColorConverter.ConvertFromString(Constants.ColorHeader), 0),
                Padding = new Thickness(28, 22, 28, 22),
            };
            var headerContent = new StackPanel { Orientation = Orientation.Horizontal };
            var icon = new Border
            {
                Width = 52, Height = 52, CornerRadius = new CornerRadius(12), Background = Brush(Constants.ColorBlue),
                Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center,
                Effect = new DropShadowEffect { Color = (Color)ColorConverter.ConvertFromString(Constants.ColorBlue), Opacity = 0.45, BlurRadius = 16, ShadowDepth = 0 },
            };
            icon.Child = new TextBlock { Text = "🚀", FontSize = 26, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            headerContent.Children.Add(icon);
            var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titleStack.Children.Add(new TextBlock { Text = "Willkommen beim Universal Linux Manager", FontSize = 19, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
            titleStack.Children.Add(new TextBlock { Text = "Kurze Einrichtung, dann kann's losgehen.", FontSize = 12, Foreground = Brush(Constants.ColorDim), Margin = new Thickness(0, 3, 0, 0) });
            headerContent.Children.Add(titleStack);
            header.Child = headerContent;
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // ── BODY ─────────────────────────────────────────────────
            // MaxHeight nur als Sicherheitsnetz für sehr kleine Bildschirme — im Normalfall
            // sizet SizeToContent das Fenster exakt auf die Höhe aller sichtbaren Abschnitte,
            // ohne dass hier gescrollt werden muss.
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 780 };
            var body   = new StackPanel { Margin = new Thickness(28, 22, 28, 18) };

            TextBox? txtPath = null;
            if (showDirectory)
            {
                var tbDownloads = new TextBlock { FontFamily = new FontFamily("Consolas, Courier New"), FontSize = 11 };
                var tbDatabase  = new TextBlock { FontFamily = new FontFamily("Consolas, Courier New"), FontSize = 11 };
                var tbLog       = new TextBlock { FontFamily = new FontFamily("Consolas, Courier New"), FontSize = 11 };
                void UpdatePreview(string basePath)
                {
                    basePath = basePath.Trim();
                    tbDownloads.Text = Path.Combine(basePath, "ISOs");
                    tbDatabase.Text  = Path.Combine(basePath, "ulm_isos.ini");
                    tbLog.Text       = Path.Combine(basePath, "ulm_log.txt");
                }

                var section = new StackPanel();
                section.Children.Add(new TextBlock
                {
                    Text = "Speicherort für ISO-Downloads und Einstellungsdateien:", FontSize = 12,
                    FontWeight = FontWeights.SemiBold, Foreground = Brush(Constants.ColorHeader), Margin = new Thickness(0, 0, 0, 8),
                });

                var pathRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
                pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                txtPath = new TextBox
                {
                    Text = DefaultBase, Height = 34, VerticalContentAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(8, 0, 8, 0), FontSize = 12, Background = Brushes.White,
                    Foreground = Brush(Constants.ColorHeader), BorderBrush = Brush(Constants.ColorBorder), BorderThickness = new Thickness(1),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                var txtPathRef = txtPath;
                txtPathRef.TextChanged += (_, _) => UpdatePreview(txtPathRef.Text);
                Grid.SetColumn(txtPathRef, 0);
                pathRow.Children.Add(txtPathRef);

                var btnBrowse = MakeButton("📂 Durchsuchen", Constants.ColorCard, Constants.ColorMid, 110, 34);
                btnBrowse.Margin = new Thickness(8, 0, 0, 0);
                btnBrowse.Click += (_, _) =>
                {
                    var dlg = new Microsoft.Win32.OpenFolderDialog
                    {
                        Title = "Arbeitsverzeichnis für den Universal Linux Manager wählen",
                        InitialDirectory = Directory.Exists(txtPathRef.Text) ? txtPathRef.Text : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    };
                    if (dlg.ShowDialog() == true) txtPathRef.Text = dlg.FolderName;
                };
                Grid.SetColumn(btnBrowse, 1);
                pathRow.Children.Add(btnBrowse);
                section.Children.Add(pathRow);

                var btnDefault = MakeButton("Standard-Pfad übernehmen", Constants.ColorBg, Constants.ColorMid, 190, 30);
                btnDefault.BorderBrush = Brush(Constants.ColorBorder); btnDefault.BorderThickness = new Thickness(1);
                btnDefault.HorizontalAlignment = HorizontalAlignment.Left;
                btnDefault.Margin = new Thickness(0, 0, 0, 14);
                btnDefault.Click += (_, _) => txtPathRef.Text = DefaultBase;
                section.Children.Add(btnDefault);

                section.Children.Add(new TextBlock { Text = "Folgende Elemente werden angelegt:", FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = Brush(Constants.ColorHeader), Margin = new Thickness(0, 0, 0, 6) });
                var previewBorder = new Border
                {
                    Background = Brush(Constants.ColorLBlue), BorderBrush = Brush(Constants.ColorBorder), BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 10, 12, 10),
                };
                var previewGrid = new Grid();
                previewGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                previewGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                for (int i = 0; i < 3; i++) previewGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                AddPreviewRow(previewGrid, 0, "ISO-Downloads",  tbDownloads);
                AddPreviewRow(previewGrid, 1, "ISO-Datenbank",  tbDatabase);
                AddPreviewRow(previewGrid, 2, "Protokolldatei", tbLog);
                previewBorder.Child = previewGrid;
                section.Children.Add(previewBorder);

                body.Children.Add(MakeCard("📁 Arbeitsordner", section));
                UpdatePreview(DefaultBase);
            }

            if (showWelcome)
            {
                var section = new StackPanel();
                section.Children.Add(new TextBlock
                {
                    Text = "Mit diesem Tool kannst du mühelos 20–30 verschiedene Linux-Distributionen verwalten, " +
                           "automatisch die neuesten ISOs herunterladen und diese bootfähig auf deinen Ventoy-USB-Stick übertragen.\n\n" +
                           "Features im Überblick:\n" +
                           "• Automatisierte URL-Prüfung & Versions-Check\n" +
                           "• Integrierte Ventoy-Installation & Secure-Boot-Support\n" +
                           "• Parallele Downloads für maximale Performance",
                    TextWrapping = TextWrapping.Wrap, FontSize = 12, LineHeight = 17,
                    Foreground = Brush(Constants.ColorMid),
                });
                body.Children.Add(MakeCard("ℹ Über ULM", section));
            }

            var modeSection = new StackPanel();
            var chkExpert = new CheckBox
            {
                Content = "Experten-Modus aktivieren (alle Funktionen sichtbar)",
                FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = Brush(Constants.ColorHeader),
                VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 8),
                IsChecked = currentExpertMode, // merkt sich die zuletzt gewählte Einstellung
            };
            modeSection.Children.Add(chkExpert);
            modeSection.Children.Add(new TextBlock
            {
                Text = "Bestimmt, wie viele Funktionen und erweiterte Einstellungen im Hauptprogramm angezeigt werden. " +
                       "Unmarkiert = Anwender-Modus (empfohlen). Der Modus kann später jederzeit oben rechts gewechselt werden.",
                TextWrapping = TextWrapping.Wrap, Foreground = Brush(Constants.ColorDim), FontSize = 11, LineHeight = 16,
            });
            body.Children.Add(MakeCard("👤 Modus", modeSection));

            scroll.Content = body;
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);

            // ── FOOTER ───────────────────────────────────────────────
            // "Beim nächsten Start überspringen" ist jetzt IMMER sichtbar (nicht mehr nur, wenn
            // der Willkommenstext gezeigt wird) — sie steuert das gesamte Einrichtungsfenster,
            // nicht nur den Begrüßungstext. Siehe BUGFIX-Kommentar in App.xaml.cs: vorher blieb
            // das Fenster trotz gesetzter Checkbox bei jedem Start sichtbar, weil sie nur den
            // Willkommens-Abschnitt, nie den ganzen Dialog abschaltete.
            var footerGrid = new Grid { Margin = new Thickness(0) };
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var chkDontShowAgain = new CheckBox
            {
                Content = "Diese Einrichtung beim nächsten Start überspringen (Modus wird gespeichert)",
                FontSize = 11, Foreground = Brush(Constants.ColorMid),
                VerticalAlignment = VerticalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(chkDontShowAgain, 0);
            footerGrid.Children.Add(chkDontShowAgain);

            var btnApply = MakeButton("✔ Übernehmen", Constants.ColorBlue, "White", 160, 40);
            btnApply.FontWeight = FontWeights.SemiBold;
            btnApply.HorizontalAlignment = HorizontalAlignment.Right;
            btnApply.Click += (_, _) =>
            {
                string chosen = string.Empty;
                if (showDirectory)
                {
                    chosen = txtPath!.Text.Trim();
                    if (string.IsNullOrWhiteSpace(chosen)) return;
                    try { Directory.CreateDirectory(chosen); }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ordner konnte nicht erstellt werden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                ChosenDirectory  = chosen;
                DontShowAgain    = chkDontShowAgain.IsChecked == true;
                ExpertModeChosen = chkExpert.IsChecked == true;
                DialogResult = true;
                Close();
            };
            Grid.SetColumn(btnApply, 1);
            footerGrid.Children.Add(btnApply);

            var btnBorder = new Border
            {
                BorderBrush = Brush(Constants.ColorBorder), BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(24, 14, 24, 14), Background = Brushes.White,
                Child = footerGrid,
            };
            Grid.SetRow(btnBorder, 2);
            root.Children.Add(btnBorder);

            Content = root;
        }

        // ── UI-Hilfsmethoden ────────────────────────────────────────────
        private static UIElement MakeCard(string title, UIElement content)
        {
            var card = new Border
            {
                Background = Brushes.White, BorderBrush = Brush(Constants.ColorBorder), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12), Padding = new Thickness(20, 18, 20, 18), Margin = new Thickness(0, 0, 0, 16),
                Effect = new DropShadowEffect { Color = Colors.Black, Opacity = 0.06, BlurRadius = 14, ShadowDepth = 3, Direction = 270 },
            };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.Bold, Foreground = Brush(Constants.ColorHeader), Margin = new Thickness(0, 0, 0, 12) });
            stack.Children.Add(content);
            card.Child = stack;
            return card;
        }

        private static void AddPreviewRow(Grid grid, int row, string label, TextBlock valueBlock)
        {
            var lbl = new TextBlock
            {
                Text = label + ":", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Brush(Constants.ColorBlue),
                Margin = new Thickness(0, 0, 12, row < 2 ? 6 : 0), VerticalAlignment = VerticalAlignment.Center, Width = 110,
            };
            Grid.SetRow(lbl, row); Grid.SetColumn(lbl, 0);
            grid.Children.Add(lbl);

            valueBlock.Foreground = Brush(Constants.ColorMid);
            valueBlock.TextWrapping = TextWrapping.Wrap;
            valueBlock.VerticalAlignment = VerticalAlignment.Center;
            valueBlock.Margin = new Thickness(0, 0, 0, row < 2 ? 6 : 0);
            Grid.SetRow(valueBlock, row); Grid.SetColumn(valueBlock, 1);
            grid.Children.Add(valueBlock);
        }

        private static Button MakeButton(string label, string bg, string fg, double width, double height)
        {
            var foreground = fg == "White" ? (Brush)Brushes.White : new SolidColorBrush((Color)ColorConverter.ConvertFromString(fg));
            return new Button
            {
                Content = label, Width = width, Height = height,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg)),
                Foreground = foreground, BorderThickness = new Thickness(0), FontSize = 12, Cursor = Cursors.Hand,
                Template = RoundedButtonTemplate,
            };
        }

        // Abgerundete Buttons statt der eckigen Standard-Windows-Chrome — für einen etwas
        // moderneren Eindruck, mit dezentem Hover-/Press-Feedback über Opacity-Trigger.
        private static readonly ControlTemplate RoundedButtonTemplate = BuildRoundedButtonTemplate();

        private static ControlTemplate BuildRoundedButtonTemplate()
        {
            var border = new FrameworkElementFactory(typeof(Border), "Bd");
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
            border.SetValue(Border.SnapsToDevicePixelsProperty, true);

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);

            var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
            var hover = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Border.OpacityProperty, 0.88, "Bd"));
            template.Triggers.Add(hover);
            var pressed = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressed.Setters.Add(new Setter(Border.OpacityProperty, 0.75, "Bd"));
            template.Triggers.Add(pressed);
            return template;
        }

        private static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    }
}
