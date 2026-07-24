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
            Title  = LocalizationService.T(Str.Help_Title);
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
            content.Children.Add(MakeSub(LocalizationService.T(Str.Help_Subtitle)));
            content.Children.Add(Spacer(16));

            tocPanel.Children.Add(new TextBlock
            {
                Text = LocalizationService.T(Str.Help_NavHeading), FontSize = 9.5, FontWeight = FontWeights.Bold,
                Foreground = ClrSub, Margin = new Thickness(6, 0, 0, 8),
            });

            // ── Übersicht ──────────────────────────────────────────────────
            AddSection(LocalizationService.T(Str.Help_Sec_Overview_Title), LocalizationService.T(Str.Help_Sec_Overview_Nav));
            content.Children.Add(MakeText(LocalizationService.T(Str.Help_Overview_Body)));
            content.Children.Add(Spacer());

            // ── Programmstart ──────────────────────────────────────────────
            AddSection(LocalizationService.T(Str.Help_Sec_Startup_Title), LocalizationService.T(Str.Help_Sec_Startup_Nav));
            content.Children.Add(MakeText(LocalizationService.T(Str.Help_Startup_Intro)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_OnlineCheck_Label), LocalizationService.T(Str.Help_Item_OnlineCheck_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_UsbScan_Label), LocalizationService.T(Str.Help_Item_UsbScan_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_FileMaintenance_Label), LocalizationService.T(Str.Help_Item_FileMaintenance_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_UpdateCheck_Label), LocalizationService.T(Str.Help_Item_UpdateCheck_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_WhatsNew_Label), LocalizationService.T(Str.Help_Item_WhatsNew_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_Autostart_Label), LocalizationService.T(Str.Help_Item_Autostart_Body)));
            content.Children.Add(Spacer());

            // ── Hauptliste ─────────────────────────────────────────────────
            AddSection(LocalizationService.T(Str.Help_Sec_Usage_Title), LocalizationService.T(Str.Help_Sec_Usage_Nav));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_SelectDownload_Label), LocalizationService.T(Str.Help_Item_SelectDownload_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_CategoryCheckbox_Label), LocalizationService.T(Str.Help_Item_CategoryCheckbox_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_DoubleClick_Label), LocalizationService.T(Str.Help_Item_DoubleClick_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_MouseoverTooltip_Label), LocalizationService.T(Str.Help_Item_MouseoverTooltip_Body)));
            content.Children.Add(Spacer());

            // ── Farben & Symbole ───────────────────────────────────────────
            AddSection(LocalizationService.T(Str.Help_Sec_Colors_Title), LocalizationService.T(Str.Help_Sec_Colors_Nav));

            content.Children.Add(MakeSubhead(LocalizationService.T(Str.Help_Subhead_TextColors)));
            content.Children.Add(MakeColorItem(SwGreen,  LocalizationService.T(Str.Help_Color_Green_Label),  LocalizationService.T(Str.Help_Color_Green_Body)));
            content.Children.Add(MakeColorItem(SwOrange, LocalizationService.T(Str.Help_Color_Orange_Label), LocalizationService.T(Str.Help_Color_Orange_Body)));
            content.Children.Add(MakeColorItem(SwRed,    LocalizationService.T(Str.Help_Color_Red_Label),    LocalizationService.T(Str.Help_Color_Red_Body)));
            content.Children.Add(MakeColorItem(SwTeal,   LocalizationService.T(Str.Help_Color_Teal_Label),   LocalizationService.T(Str.Help_Color_Teal_Body)));
            content.Children.Add(MakeColorItem(SwBlue,   LocalizationService.T(Str.Help_Color_Blue_Label),   LocalizationService.T(Str.Help_Color_Blue_Body)));
            content.Children.Add(MakeColorItem(SwGray,   LocalizationService.T(Str.Help_Color_Gray_Label),   LocalizationService.T(Str.Help_Color_Gray_Body)));
            content.Children.Add(MakeColorItem(SwDark,   LocalizationService.T(Str.Help_Color_Dark_Label),   LocalizationService.T(Str.Help_Color_Dark_Body)));
            content.Children.Add(Spacer(6));

            content.Children.Add(MakeSubhead(LocalizationService.T(Str.Help_Subhead_Columns)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_ColLocal_Label), LocalizationService.T(Str.Help_Item_ColLocal_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_ColOnStick_Label), LocalizationService.T(Str.Help_Item_ColOnStick_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_ColCurrent_Label), LocalizationService.T(Str.Help_Item_ColCurrent_Body)));
            content.Children.Add(Spacer(6));

            content.Children.Add(MakeSubhead(LocalizationService.T(Str.Help_Subhead_HashSymbol)));
            content.Children.Add(MakeText(LocalizationService.T(Str.Help_HashSymbol_Body)));
            content.Children.Add(Spacer(6));

            content.Children.Add(MakeSubhead(LocalizationService.T(Str.Help_Subhead_NameSymbols)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_SymbolImported_Label), LocalizationService.T(Str.Help_Item_SymbolImported_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_SymbolUrlOk_Label), LocalizationService.T(Str.Help_Item_SymbolUrlOk_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_SymbolUrlFail_Label), LocalizationService.T(Str.Help_Item_SymbolUrlFail_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_SymbolNewVersion_Label), LocalizationService.T(Str.Help_Item_SymbolNewVersion_Body)));
            content.Children.Add(Spacer(6));

            content.Children.Add(MakeSubhead(LocalizationService.T(Str.Help_Subhead_CategorySymbols)));
            content.Children.Add(MakeText(LocalizationService.T(Str.Help_CategorySymbols_Body)));
            content.Children.Add(Spacer());

            // ── Design (Hell/Dunkel) ───────────────────────────────────────
            AddSection(LocalizationService.T(Str.Help_Sec_Theme_Title), LocalizationService.T(Str.Help_Sec_Theme_Nav));
            content.Children.Add(MakeText(LocalizationService.T(Str.Help_Theme_Intro)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_ThemeSetting_Label), LocalizationService.T(Str.Help_Item_ThemeSetting_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_ThemeSystem_Label), LocalizationService.T(Str.Help_Item_ThemeSystem_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_ThemeInstant_Label), LocalizationService.T(Str.Help_Item_ThemeInstant_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_ThemeRemembers_Label), LocalizationService.T(Str.Help_Item_ThemeRemembers_Body)));
            content.Children.Add(Spacer());

            // ── Protokoll-Symbole ─────────────────────────────────────────
            AddSection(LocalizationService.T(Str.Help_Sec_LogSymbols_Title), LocalizationService.T(Str.Help_Sec_LogSymbols_Nav));
            content.Children.Add(MakeText(LocalizationService.T(Str.Help_LogSymbols_Body)));
            content.Children.Add(Spacer());

            // ── ISO suchen (Online-Entdeckung) ────────────────────────────
            AddSection(LocalizationService.T(Str.Help_Sec_IsoSearch_Title), LocalizationService.T(Str.Help_Sec_IsoSearch_Nav));
            content.Children.Add(MakeText(LocalizationService.T(Str.Help_IsoSearch_Intro)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_Newest_Label), LocalizationService.T(Str.Help_Item_Newest_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_Popular_Label), LocalizationService.T(Str.Help_Item_Popular_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_LiveOnly_Label), LocalizationService.T(Str.Help_Item_LiveOnly_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_AlreadyInDb_Label), LocalizationService.T(Str.Help_Item_AlreadyInDb_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_AdoptAndDownload_Label), LocalizationService.T(Str.Help_Item_AdoptAndDownload_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_RefreshCache_Label), LocalizationService.T(Str.Help_Item_RefreshCache_Body)));
            content.Children.Add(Spacer());

            // ── Download ───────────────────────────────────────────────────
            AddSection(LocalizationService.T(Str.Help_Sec_Download_Title), LocalizationService.T(Str.Help_Sec_Download_Nav));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_StorageLocation_Label), LocalizationService.T(Str.Help_Item_StorageLocation_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_PipelineMode_Label), LocalizationService.T(Str.Help_Item_PipelineMode_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_MirrorRace_Label), LocalizationService.T(Str.Help_Item_MirrorRace_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_SpeedGuard_Label), LocalizationService.T(Str.Help_Item_SpeedGuard_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_FasterButton_Label), LocalizationService.T(Str.Help_Item_FasterButton_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_EtaRemaining_Label), LocalizationService.T(Str.Help_Item_EtaRemaining_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_VerifyIntegrity_Label), LocalizationService.T(Str.Help_Item_VerifyIntegrity_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_FreeSpaceCheck_Label), LocalizationService.T(Str.Help_Item_FreeSpaceCheck_Body)));
            content.Children.Add(Spacer());

            // ── USB-Stick ──────────────────────────────────────────────────
            AddSection(LocalizationService.T(Str.Help_Sec_UsbStick_Title), LocalizationService.T(Str.Help_Sec_UsbStick_Nav));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_WhatIsVentoy_Label), LocalizationService.T(Str.Help_Item_WhatIsVentoy_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_InstallUpdateVentoy_Label), LocalizationService.T(Str.Help_Item_InstallUpdateVentoy_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_MultipleSticks_Label), LocalizationService.T(Str.Help_Item_MultipleSticks_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_BootMenu_Label), LocalizationService.T(Str.Help_Item_BootMenu_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_CatchUpCopies_Label), LocalizationService.T(Str.Help_Item_CatchUpCopies_Body)));
            content.Children.Add(Spacer());

            // ── Datenmüll-Schutz ──────────────────────────────────────────
            AddSection(LocalizationService.T(Str.Help_Sec_JunkProtection_Title), LocalizationService.T(Str.Help_Sec_JunkProtection_Nav));
            content.Children.Add(MakeText(LocalizationService.T(Str.Help_JunkProtection_Intro)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_WhenChecked_Label), LocalizationService.T(Str.Help_Item_WhenChecked_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_HowChecked_Label), LocalizationService.T(Str.Help_Item_HowChecked_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_JunkInFolder_Label), LocalizationService.T(Str.Help_Item_JunkInFolder_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_JunkOnStick_Label), LocalizationService.T(Str.Help_Item_JunkOnStick_Body)));
            content.Children.Add(Spacer());

            // ── ISO-Import ────────────────────────────────────────────────
            AddSection(LocalizationService.T(Str.Help_Sec_IsoImport_Title), LocalizationService.T(Str.Help_Sec_IsoImport_Nav));
            content.Children.Add(MakeText(LocalizationService.T(Str.Help_IsoImport_Intro)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_NameCategoryUrl_Label), LocalizationService.T(Str.Help_Item_NameCategoryUrl_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_FolderStructure_Label), LocalizationService.T(Str.Help_Item_FolderStructure_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_DuplicateProtection_Label), LocalizationService.T(Str.Help_Item_DuplicateProtection_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_StayUpToDate_Label), LocalizationService.T(Str.Help_Item_StayUpToDate_Body)));
            content.Children.Add(Spacer());

            // ── Expert-Modus ───────────────────────────────────────────────
            AddSection(LocalizationService.T(Str.Help_Sec_ExpertMode_Title), LocalizationService.T(Str.Help_Sec_ExpertMode_Nav));
            content.Children.Add(MakeText(LocalizationService.T(Str.Help_ExpertMode_Intro)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_StatusTab_Label), LocalizationService.T(Str.Help_Item_StatusTab_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_UrlCheck_Label), LocalizationService.T(Str.Help_Item_UrlCheck_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_EditDatabase_Label), LocalizationService.T(Str.Help_Item_EditDatabase_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_DbHealthCheck_Label), LocalizationService.T(Str.Help_Item_DbHealthCheck_Body)));
            content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_GitHubToken_Label), LocalizationService.T(Str.Help_Item_GitHubToken_Body)));
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
