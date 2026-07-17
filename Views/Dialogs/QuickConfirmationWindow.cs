// Views/Dialogs/QuickConfirmationWindow.cs
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ULM.Views.Dialogs
{
    // AppRes (Brush/Style-Helfer) liegt bereits im selben Namespace ULM.Views.Dialogs
    // (Views/Dialogs/DownloadDialogs.cs) — kein zusätzliches using nötig.
    //
    // Unauffällige Erfolgsmeldung für schnelle Hintergrund-Checks (URL-/Update-/Integritätsprüfung,
    // siehe MainViewModel.QuickCheckSucceeded) — schließt sich nach 2s von selbst, oder der Nutzer
    // klickt vorher auf OK. Bewusst NICHT modal (Show statt ShowDialog), damit der Arbeitsfluss nicht
    // unterbrochen wird, anders als die blockierende MessageBox bei OperationSucceeded (Download/Kopie).
    public sealed class QuickConfirmationWindow : Window
    {
        public QuickConfirmationWindow(string message)
        {
            Title = "✔ Erledigt";
            Width = 360; SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Background = AppRes.Brush("BrushBg");

            var root = new StackPanel { Margin = new Thickness(20) };
            root.Children.Add(new TextBlock
            {
                Text = message, TextWrapping = TextWrapping.Wrap, FontSize = 12.5,
                Foreground = AppRes.Brush("BrushHeader"), Margin = new Thickness(0, 0, 0, 16),
            });
            var ok = new Button { Content = "OK", Style = AppRes.Style("BtnPrimary"), Width = 90, HorizontalAlignment = HorizontalAlignment.Right };
            ok.Click += (_, _) => Close();
            root.Children.Add(ok);
            Content = root;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (_, _) => { timer.Stop(); Close(); };
            Loaded += (_, _) => timer.Start();
            Closed += (_, _) => timer.Stop();
        }
    }
}
