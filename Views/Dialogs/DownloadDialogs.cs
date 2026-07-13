// Views/Dialogs/DownloadDialogs.cs
// DownloadSlotsDialog, DownloadProgressDialog, OrphanedDownloadsDialog, DriveSelectDialog
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ULM.Core.Models;
using ULM.Core.Services;

namespace ULM.Views.Dialogs
{
    internal static class AppRes
    {
        public static Brush Brush(string key) => (Brush)Application.Current!.Resources[key];
        public static Style  Style(string key) => (Style)Application.Current!.Resources[key];
    }

    // ═══════════════════════════════════════════════════════════════════
    // DownloadSlotsDialog
    // ═══════════════════════════════════════════════════════════════════
    public sealed class DownloadSlotsDialog : Window
    {
        public int ChosenSlots { get; private set; } = 1;

        private readonly int       _maxSlots;
        private int                _recommended = 1;
        private bool               _testDone;
        private readonly TextBlock _statusText;
        private readonly TextBlock _resultText;
        private readonly Slider    _slider;
        private readonly TextBlock _sliderValueText;
        private readonly CheckBox  _chkAuto;
        private readonly Button    _btnOk;

        public DownloadSlotsDialog(int queueCount, int maxSlots)
        {
            _maxSlots = Math.Max(1, Math.Min(maxSlots, queueCount));
            Title = "Parallele Downloads";
            Width = 460; Height = 380;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Background = AppRes.Brush("BrushBg");

            var root = new StackPanel { Margin = new Thickness(22) };

            root.Children.Add(new TextBlock
            {
                Text = $"Du hast {queueCount} ISO(s) zum Download ausgewählt.",
                FontWeight = FontWeights.Bold, FontSize = 14,
                Margin = new Thickness(0, 0, 0, 14), TextWrapping = TextWrapping.Wrap,
                Foreground = AppRes.Brush("BrushHeader"),
            });

            _statusText = new TextBlock { Text = "🔄 Teste Verbindungsgeschwindigkeit ...", FontSize = 12.5, Foreground = AppRes.Brush("BrushMid"), Margin = new Thickness(0, 0, 0, 8) };
            root.Children.Add(_statusText);

            _resultText = new TextBlock { Text = string.Empty, FontSize = 12, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 18), TextWrapping = TextWrapping.Wrap, Foreground = AppRes.Brush("BrushBlue") };
            root.Children.Add(_resultText);

            root.Children.Add(new TextBlock { Text = "Parallele Downloads:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6), Foreground = AppRes.Brush("BrushHeader") });

            var slRow = new Grid();
            slRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            slRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _slider = new Slider { Minimum = 1, Maximum = _maxSlots, Value = 1, TickFrequency = 1, IsSnapToTickEnabled = true, IsEnabled = false, VerticalAlignment = VerticalAlignment.Center };
            _sliderValueText = new TextBlock { Text = "1", FontWeight = FontWeights.Bold, Width = 28, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Foreground = AppRes.Brush("BrushHeader") };
            _slider.ValueChanged += (_, _) => _sliderValueText!.Text = ((int)_slider.Value).ToString();

            Grid.SetColumn(_slider, 0); slRow.Children.Add(_slider);
            Grid.SetColumn(_sliderValueText, 1); slRow.Children.Add(_sliderValueText);
            root.Children.Add(slRow);

            _chkAuto = new CheckBox { Content = "Empfehlung automatisch verwenden", IsChecked = true, Margin = new Thickness(0, 14, 0, 0), FontSize = 12 };
            _chkAuto.Checked   += (_, _) => ApplyAutoState(true);
            _chkAuto.Unchecked += (_, _) => ApplyAutoState(false);
            root.Children.Add(_chkAuto);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 28, 0, 0) };
            var bCancel = new Button { Content = "Abbrechen", Width = 100, Style = AppRes.Style("BtnGhost"), Margin = new Thickness(0, 0, 8, 0) };
            bCancel.Click += (_, _) => { DialogResult = false; Close(); };
            _btnOk = new Button { Content = "✔ Downloads starten", Width = 175, Style = AppRes.Style("BtnPrimary"), IsEnabled = false };
            _btnOk.Click += (_, _) => { ChosenSlots = (int)_slider.Value; DialogResult = true; Close(); };
            btnRow.Children.Add(bCancel); btnRow.Children.Add(_btnOk);
            root.Children.Add(btnRow);
            Content = root;
            Loaded += async (_, _) => await RunSpeedTestAsync();
        }

        private void ApplyAutoState(bool auto) { _slider.IsEnabled = !auto && _testDone; if (auto) _slider.Value = _recommended; }

        private async Task RunSpeedTestAsync()
        {
            double mbps = await HttpService.Instance.MeasureDownloadSpeedMbpsAsync(CancellationToken.None).ConfigureAwait(true);
            _recommended = RecommendSlots(mbps, _maxSlots); _testDone = true;
            _statusText.Visibility = Visibility.Collapsed; _resultText.Visibility = Visibility.Visible;
            _resultText.Text = mbps > 0
                ? $"📶 {mbps:F1} Mbit/s  →  Empfohlen: {_recommended} parallele(r) Download(s)."
                : $"⚠ Test fehlgeschlagen — Standard-Empfehlung: {_recommended} parallele(r) Download(s).";
            _slider.Value = _recommended; _slider.IsEnabled = _chkAuto.IsChecked != true; _btnOk.IsEnabled = true;
        }

        private static int RecommendSlots(double mbps, int max) => Math.Max(1, Math.Min(max, mbps switch
        { <= 0 => 2, < 8 => 1, < 25 => 2, < 60 => 3, < 120 => 4, < 250 => 5, _ => 6 }));
    }

    // ═══════════════════════════════════════════════════════════════════
    // DownloadProgressDialog
    // ═══════════════════════════════════════════════════════════════════
    public sealed class DownloadProgressDialog : Window
    {
        public event Action? CancelRequested;

        private readonly StackPanel _itemsPanel;
        private readonly TextBlock  _summaryText;

        private sealed class Row
        {
            public required TextBlock   NameText;
            public required ProgressBar Bar;
            public required TextBlock   PercentText;
            public required TextBlock   StatusText;
            public required string      OriginalName;
        }

        private readonly Dictionary<string, Row> _rows = new(StringComparer.OrdinalIgnoreCase);

        public DownloadProgressDialog(IEnumerable<string> isoNames)
        {
            Title = "Download-Fortschritt";
            Width = 540; Height = 480;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = AppRes.Brush("BrushBg");

            var root = new Grid { Margin = new Thickness(18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _summaryText = new TextBlock { Text = "⬇ Downloads laufen ...", FontWeight = FontWeights.Bold, FontSize = 14, Margin = new Thickness(0, 0, 0, 14), Foreground = AppRes.Brush("BrushHeader") };
            Grid.SetRow(_summaryText, 0); root.Children.Add(_summaryText);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _itemsPanel = new StackPanel();
            scroll.Content = _itemsPanel;
            Grid.SetRow(scroll, 1); root.Children.Add(scroll);

            foreach (string name in isoNames) AddRow(name);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            var bCan = new Button { Content = "✕ Abbrechen", Width = 120, Style = AppRes.Style("BtnDanger"), Margin = new Thickness(0, 0, 8, 0) };
            bCan.Click += (_, _) => CancelRequested?.Invoke();
            var bClose = new Button { Content = "Schließen", Width = 110, Style = AppRes.Style("BtnGhost") };
            bClose.Click += (_, _) => Close();
            btnRow.Children.Add(bCan); btnRow.Children.Add(bClose);
            Grid.SetRow(btnRow, 2); root.Children.Add(btnRow);
            Content = root;
        }

        private Row AddRow(string name)
        {
            var border = new Border { BorderBrush = AppRes.Brush("BrushBorder"), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 8, 0, 8) };
            var stack = new StackPanel();

            var hRow = new Grid();
            hRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameText = new TextBlock { Text = name, FontWeight = FontWeights.SemiBold, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = AppRes.Brush("BrushHeader") };
            Grid.SetColumn(nameText, 0); hRow.Children.Add(nameText);
            var pctText  = new TextBlock { Text = "0%", FontSize = 11, Margin = new Thickness(8, 0, 0, 0), Foreground = AppRes.Brush("BrushBlue") };
            Grid.SetColumn(pctText, 1); hRow.Children.Add(pctText);
            stack.Children.Add(hRow);

            var bar = new ProgressBar { Minimum = 0, Maximum = 100, Value = 0, Height = 8, Margin = new Thickness(0, 5, 0, 3) };
            stack.Children.Add(bar);
            var stat = new TextBlock { Text = "Wartet ...", FontSize = 10.5, Foreground = AppRes.Brush("BrushDim"), TextTrimming = TextTrimming.CharacterEllipsis };
            stack.Children.Add(stat);

            border.Child = stack;
            _itemsPanel.Children.Add(border);
            var row = new Row { NameText = nameText, Bar = bar, PercentText = pctText, StatusText = stat, OriginalName = name };
            _rows[name] = row;
            return row;
        }

        public void UpdateItem(string name, int percent, string status)
        {
            if (!_rows.TryGetValue(name, out var row)) row = AddRow(name);
            int c = Math.Max(0, Math.Min(100, percent));
            row.Bar.Value = c; row.PercentText.Text = $"{c}%"; row.StatusText.Text = status;
        }

        public void SetPhaseLabel(string name, string? phaseSuffix)
        {
            if (!_rows.TryGetValue(name, out var row)) row = AddRow(name);
            row.NameText.Text = string.IsNullOrWhiteSpace(phaseSuffix) ? row.OriginalName : $"{row.OriginalName}  —  {phaseSuffix}";
        }

        public void SetOverallComplete(string summary) => _summaryText.Text = $"✅ {summary}";
    }

    // ═══════════════════════════════════════════════════════════════════
    // OrphanedDownloadsDialog
    // ═══════════════════════════════════════════════════════════════════
    public sealed class OrphanedDownloadsDialog : Window
    {
        public List<string> ToDelete { get; } = new();
        private readonly Dictionary<string, CheckBox> _checks = new();

        public OrphanedDownloadsDialog(List<(string Path, long Size)> files,
            string title = "Unvollständige Downloads gefunden",
            string itemLabel = "unvollständige Download-Datei(en)")
        {
            Title = title;
            Width = 560; Height = 460;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = AppRes.Brush("BrushBg");

            var root = new Grid { Margin = new Thickness(20) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            root.Children.Add(new TextBlock { Text = $"Beim letzten Mal wurden {files.Count} {itemLabel} gefunden.\nDiese können bedenkenlos gelöscht werden:", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 14), FontSize = 12.5, Foreground = AppRes.Brush("BrushHeader") });

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var list   = new StackPanel();
            foreach (var (path, size) in files)
            {
                var chk = new CheckBox { Content = $"{Path.GetFileName(path)}   ({FormatBytes(size)})", IsChecked = true, Margin = new Thickness(0, 4, 0, 4), FontSize = 11.5 };
                _checks[path] = chk; list.Children.Add(chk);
            }
            scroll.Content = list;
            Grid.SetRow(scroll, 1); root.Children.Add(scroll);

            var br = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            var bSkip = new Button { Content = "Überspringen", Width = 120, Style = AppRes.Style("BtnGhost"), Margin = new Thickness(0, 0, 8, 0) };
            bSkip.Click += (_, _) => { DialogResult = false; Close(); };
            var bDel = new Button { Content = "🗑 Ausgewählte löschen", Width = 190, Style = AppRes.Style("BtnDanger") };
            bDel.Click += (_, _) =>
            {
                ToDelete.Clear();
                foreach (var kvp in _checks) if (kvp.Value.IsChecked == true) ToDelete.Add(kvp.Key);
                DialogResult = true; Close();
            };
            br.Children.Add(bSkip); br.Children.Add(bDel);
            Grid.SetRow(br, 2); root.Children.Add(br);
            Content = root;
        }

        private static string FormatBytes(long bytes)
        {
            string[] u = { "B", "KB", "MB", "GB" }; double v = bytes; int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return i == 0 ? $"{(long)v} {u[i]}" : $"{v:F1} {u[i]}";
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // DriveSelectDialog — Auswahl des richtigen USB-Sticks bei mehreren
    //
    // Wird angezeigt wenn mehr als ein Wechseldatenträger erkannt wird
    // und der Benutzer Ventoy installieren/aktualisieren möchte.
    // Zeigt alle erkannten Laufwerke mit Buchstabe, Label und Größe an,
    // damit der Anwender sicher das richtige Ziel auswählt.
    // ═══════════════════════════════════════════════════════════════════
    public sealed class DriveSelectDialog : Window
    {
        public UsbDrive? SelectedDrive { get; private set; }

        private readonly ComboBox _combo;

        public DriveSelectDialog(IReadOnlyList<UsbDrive> drives)
        {
            Title  = "Ziel-USB-Stick auswählen";
            Width  = 440; Height = 240;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Background = AppRes.Brush("BrushBg");

            var root = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };

            root.Children.Add(new TextBlock
            {
                Text         = $"Es wurden {drives.Count} USB-Laufwerke erkannt.\n" +
                               "Bitte das Ziel-Laufwerk für die Ventoy-Installation auswählen:",
                TextWrapping = TextWrapping.Wrap,
                FontSize     = 12.5,
                Margin       = new Thickness(0, 0, 0, 18),
                Foreground   = AppRes.Brush("BrushHeader"),
            });

            _combo = new ComboBox
            {
                Margin  = new Thickness(0, 0, 0, 8),
                FontSize = 13,
                Padding  = new Thickness(8, 7, 8, 7),
            };

            foreach (var drive in drives)
            {
                string label = string.IsNullOrWhiteSpace(drive.Label) ? "Kein Name" : drive.Label;
                double gb    = drive.SizeBytes / 1_073_741_824.0;
                bool   isV   = UsbService.IsVentoyInstalled(drive.Letter);
                string tag   = isV ? "  [Ventoy vorhanden]" : "";

                _combo.Items.Add(new ComboBoxItem
                {
                    Content = $"{drive.Letter}   {label}   ({gb:F0} GB){tag}",
                    Tag     = drive,
                    FontWeight = isV ? FontWeights.SemiBold : FontWeights.Normal,
                });
            }
            _combo.SelectedIndex = 0;
            root.Children.Add(_combo);

            // Hinweis-Text unter dem ComboBox
            root.Children.Add(new TextBlock
            {
                Text       = "ℹ Laufwerke mit 'Ventoy vorhanden' können aktualisiert werden,\n" +
                             "   alle anderen werden neu formatiert (Datenverlust!).",
                FontSize   = 10.5,
                Foreground = AppRes.Brush("BrushDim"),
                Margin     = new Thickness(0, 4, 0, 20),
                TextWrapping = TextWrapping.Wrap,
            });

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var bCancel = new Button { Content = "Abbrechen", Width = 100, Style = AppRes.Style("BtnGhost"), Margin = new Thickness(0, 0, 8, 0) };
            bCancel.Click += (_, _) => { DialogResult = false; Close(); };
            var bOk = new Button { Content = "✔ Auswählen", Width = 130, Style = AppRes.Style("BtnPrimary") };
            bOk.Click += (_, _) =>
            {
                if (_combo.SelectedItem is ComboBoxItem ci && ci.Tag is UsbDrive d)
                    SelectedDrive = d;
                DialogResult = SelectedDrive is not null;
                Close();
            };
            btnRow.Children.Add(bCancel); btnRow.Children.Add(bOk);
            root.Children.Add(btnRow);
            Content = root;
        }
    }
}
