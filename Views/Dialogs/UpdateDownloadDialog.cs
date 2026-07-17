using System;
using System.Windows;
using System.Windows.Controls;
using ULM.Core.Services;

namespace ULM.Views.Dialogs
{
    // AppRes (Brush/Style-Helfer) liegt bereits im selben Namespace ULM.Views.Dialogs
    // (Views/Dialogs/DownloadDialogs.cs) — kein zusätzliches using nötig.
    // Kleiner Auswahldialog nach Klick auf "Herunterladen …" im Update-Banner: bietet die portable
    // EXE und/oder den Setup-Installer an — je nachdem, welche Assets im Release vorhanden sind.
    // Fehlt beides, bleibt nur "Zur Release-Seite öffnen" (OpenReleasePageInstead = true).
    public sealed class UpdateDownloadDialog : Window
    {
        public string ChosenUrl { get; private set; } = string.Empty;
        public bool   OpenReleasePageInstead { get; private set; }

        public UpdateDownloadDialog(UlmUpdateInfo info)
        {
            Title  = "Programm-Update herunterladen";
            Width  = 460; SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Background = AppRes.Brush("BrushBg");

            var root = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
            root.Children.Add(new TextBlock
            {
                Text = $"Version v{info.LatestVersion} steht bereit.\nWie möchtest du sie beziehen?",
                TextWrapping = TextWrapping.Wrap, FontSize = 12.5,
                Margin = new Thickness(0, 0, 0, 16), Foreground = AppRes.Brush("BrushHeader"),
            });

            bool any = false;
            if (!string.IsNullOrWhiteSpace(info.PortableExeUrl))
            {
                any = true;
                var b = new Button { Content = "⬇ Portable .exe (ohne Installation)", Style = AppRes.Style("BtnPrimary"), Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(10, 8, 10, 8) };
                b.Click += (_, _) => { ChosenUrl = info.PortableExeUrl; DialogResult = true; Close(); };
                root.Children.Add(b);
            }
            if (!string.IsNullOrWhiteSpace(info.SetupExeUrl))
            {
                any = true;
                var b = new Button { Content = "⬇ Setup-Installer (.exe)", Style = AppRes.Style("BtnSuccess"), Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(10, 8, 10, 8) };
                b.Click += (_, _) => { ChosenUrl = info.SetupExeUrl; DialogResult = true; Close(); };
                root.Children.Add(b);
            }
            if (!any)
            {
                var b = new Button { Content = "🌐 Zur Release-Seite öffnen", Style = AppRes.Style("BtnGhost"), Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(10, 8, 10, 8) };
                b.Click += (_, _) => { OpenReleasePageInstead = true; DialogResult = true; Close(); };
                root.Children.Add(b);
            }

            var bCancel = new Button { Content = "Abbrechen", Style = AppRes.Style("BtnGhost"), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
            bCancel.Click += (_, _) => { DialogResult = false; Close(); };
            root.Children.Add(bCancel);

            Content = root;
        }
    }
}
