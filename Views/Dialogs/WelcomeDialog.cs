// Views/Dialogs/WelcomeDialog.cs
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using ULM.Infrastructure;

namespace ULM.Views.Dialogs
{
    // Eigenständiges Begrüßungsfenster, das VOR dem SetupDialog erscheint — ausschließlich beim
    // allerersten Programmstart (siehe App.xaml.cs, STEP 2). Zeigt nur den "Über ULM"-Text, der
    // bisher als Karte INNERHALB des SetupDialogs stand — dadurch ist der SetupDialog selbst
    // jetzt kürzer. Kein Zustand zu sammeln, ein einziger "Weiter"-Button; Schließen über das
    // Fenster-X zählt wie beim SetupDialog als Abbruch (DialogResult bleibt null/false).
    public sealed class WelcomeDialog : Window
    {
        public WelcomeDialog()
        {
            Title = LocalizationService.T(Str.Welcome_Title);
            // Gleiche Breiten-Logik wie SetupDialog (siehe dort) — bewusst gleich breit gehalten,
            // damit die beiden aufeinanderfolgenden Erststart-Fenster einheitlich wirken.
            double maxW = SystemParameters.WorkArea.Width  - 40;
            double maxH = SystemParameters.WorkArea.Height - 40;
            Width     = Math.Max(680, Math.Min(880, maxW));
            MinWidth  = Math.Min(800, Width);
            MaxHeight = Math.Max(360, maxH);
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = ThemeColors.Bg;

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // ── HEADER (identischer Aufbau wie SetupDialog, für einheitlichen Auftritt) ──
            var header = new Border
            {
                Background = ThemeColors.HeaderBar,
                Padding = new Thickness(28, 22, 28, 22),
            };
            var headerContent = new StackPanel { Orientation = Orientation.Horizontal };
            var icon = new Border
            {
                Width = 52, Height = 52, CornerRadius = new CornerRadius(12), Background = ThemeColors.Blue,
                Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center,
                Effect = new DropShadowEffect { Color = ((SolidColorBrush)ThemeColors.Blue).Color, Opacity = 0.45, BlurRadius = 16, ShadowDepth = 0 },
            };
            icon.Child = new TextBlock { Text = "🚀", FontSize = 26, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            headerContent.Children.Add(icon);
            var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titleStack.Children.Add(new TextBlock
            {
                Text = LocalizationService.T(Str.Setup_Header_Welcome),
                FontSize = 19, FontWeight = FontWeights.Bold, Foreground = Brushes.White,
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = LocalizationService.T(Str.Setup_Subtitle_Welcome),
                FontSize = 12, Foreground = ThemeColors.Dim, Margin = new Thickness(0, 3, 0, 0),
            });
            headerContent.Children.Add(titleStack);
            header.Child = headerContent;
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // ── BODY ─────────────────────────────────────────────────
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var body   = new StackPanel { Margin = new Thickness(28, 22, 28, 18) };

            var section = new StackPanel();
            section.Children.Add(new TextBlock
            {
                Text = LocalizationService.T(Str.Setup_WelcomeBody),
                TextWrapping = TextWrapping.Wrap, FontSize = 12, LineHeight = 17,
                Foreground = ThemeColors.Mid,
            });
            body.Children.Add(SetupDialog.MakeCard(LocalizationService.T(Str.Setup_Card_AboutUlm), section));

            scroll.Content = body;
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);

            // ── FOOTER ───────────────────────────────────────────────
            var footerGrid = new Grid { Margin = new Thickness(0) };
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var btnContinue = SetupDialog.MakeButton(LocalizationService.T(Str.Welcome_Btn_Continue), ThemeColors.Blue, Brushes.White, 160, 40);
            btnContinue.FontWeight = FontWeights.SemiBold;
            btnContinue.HorizontalAlignment = HorizontalAlignment.Right;
            btnContinue.Click += (_, _) => { DialogResult = true; Close(); };
            Grid.SetColumn(btnContinue, 1);
            footerGrid.Children.Add(btnContinue);

            var btnBorder = new Border
            {
                BorderBrush = ThemeColors.Border, BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(24, 14, 24, 14), Background = ThemeColors.White,
                Child = footerGrid,
            };
            Grid.SetRow(btnBorder, 2);
            root.Children.Add(btnBorder);

            Content = root;
        }
    }
}
