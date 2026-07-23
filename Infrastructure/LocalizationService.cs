using System;
using System.Collections.Generic;
using System.Globalization;

namespace ULM.Infrastructure
{
    // Pendant zu ThemeService: statische Klasse, Initialize() beim Programmstart,
    // Persistenz über IniService/ulm_settings.ini. Ein Sprachwechsel wirkt bewusst
    // NICHT live (anders als ThemeService) — siehe Design-Entscheidung in
    // docs/superpowers/specs/2026-07-22-bilingual-ui-infrastructure-design.md:
    // "Neustart-Hinweis reicht" statt vollständigem Live-Retexten.
    public static class LocalizationService
    {
        public static AppLanguage Current { get; private set; } = AppLanguage.German;

        public static void Initialize() => Current = LoadFromIni(AppPaths.Instance.SettingsIni);

        // Testbar ohne Application.Current/UI-Zugriff — nur Datei-IO über IniService.
        internal static AppLanguage LoadFromIni(string settingsIniPath)
        {
            string saved = IniService.Read(settingsIniPath, "App", "Language", "");
            return saved switch
            {
                "de" => AppLanguage.German,
                "en" => AppLanguage.English,
                _    => DetectFromCulture(CultureInfo.CurrentUICulture),
            };
        }

        // Reine Logik, testbar ohne die echte Systemsprache umstellen zu müssen.
        internal static AppLanguage DetectFromCulture(CultureInfo culture) =>
            string.Equals(culture.TwoLetterISOLanguageName, "de", StringComparison.OrdinalIgnoreCase)
                ? AppLanguage.German
                : AppLanguage.English;

        public static void SetLanguage(AppLanguage lang) => SetLanguage(lang, AppPaths.Instance.SettingsIni);

        internal static void SetLanguage(AppLanguage lang, string settingsIniPath)
        {
            Current = lang;
            IniService.Write(settingsIniPath, "App", "Language", lang == AppLanguage.German ? "de" : "en");
        }

        public static string T(Str key) => T(key, Current);

        public static string T(Str key, AppLanguage language) =>
            language == AppLanguage.German ? De[key] : En[key];

        private static readonly Dictionary<Str, string> De = new()
        {
            [Str.Tab_IsoSelection]             = "ISO-Auswahl",
            [Str.Tab_Log]                      = "Protokoll",
            [Str.Tab_Status]                   = "Status",
            [Str.Btn_Download]                 = "⬇  Herunterladen",
            [Str.Btn_CheckForUpdates]          = "↻  Updates prüfen",
            [Str.Btn_Cancel]                   = "✕  Stopp",
            [Str.Btn_Help]                     = "❓ Hilfe",
            [Str.Btn_Settings]                 = "⚙ Einstellungen",
            [Str.LanguageChangeConfirm_Title]  = "Sprache geändert",
            [Str.LanguageChangeConfirm_Message] = "ULM jetzt neu starten, um die neue Sprache zu übernehmen?",

            [Str.Setup_Title_Welcome]            = "Universal Linux Manager — Einrichtung",
            [Str.Setup_Title_Settings]           = "Universal Linux Manager — Einstellungen",
            [Str.Setup_Header_Welcome]           = "Willkommen beim Universal Linux Manager",
            [Str.Setup_Header_Settings]          = "Einstellungen",
            [Str.Setup_Subtitle_Welcome]         = "Kurze Einrichtung, dann kann's losgehen.",
            [Str.Setup_Subtitle_Settings]        = "Änderungen wirken nach Klick auf \"✔ Übernehmen\".",

            [Str.Setup_Directory_Header]         = "Speicherort für ISO-Downloads und Einstellungsdateien:",
            [Str.Setup_Btn_Browse]               = "📂 Durchsuchen",
            [Str.Setup_FolderDialog_Title]       = "Arbeitsverzeichnis für den Universal Linux Manager wählen",
            [Str.Setup_Btn_UseDefaultPath]       = "Standard-Pfad übernehmen",
            [Str.Setup_Directory_ItemsIntro]     = "Folgende Elemente werden angelegt:",
            [Str.Setup_Directory_ItemDownloads]  = "ISO-Downloads",
            [Str.Setup_Directory_ItemDatabase]   = "ISO-Datenbank",
            [Str.Setup_Directory_ItemLog]        = "Protokolldatei",

            [Str.Setup_Card_AboutUlm]            = "ℹ Über ULM",
            [Str.Setup_WelcomeBody]              =
                "Mit diesem Tool kannst du mühelos 20–30 verschiedene Linux-Distributionen verwalten, " +
                "automatisch die neuesten ISOs herunterladen und diese bootfähig auf deinen Ventoy-USB-Stick übertragen.\n\n" +
                "Features im Überblick:\n" +
                "• Automatisierte URL-Prüfung & Versions-Check\n" +
                "• Integrierte Ventoy-Installation & Secure-Boot-Support\n" +
                "• Parallele Downloads für maximale Performance",

            [Str.Setup_Card_Mode]                = "👤 Modus",
            [Str.Setup_Chk_ExpertMode]           = "Experten-Modus aktivieren (alle Funktionen sichtbar)",
            [Str.Setup_Hint_Mode]                =
                "Bestimmt, wie viele Funktionen und erweiterte Einstellungen im Hauptprogramm angezeigt werden. " +
                "Unmarkiert = Anwender-Modus (empfohlen). Der Modus kann später jederzeit über ⚙ Einstellungen oben rechts geändert werden.",

            [Str.Setup_Card_Autostart]           = "🚀 Autostart",
            [Str.Setup_Chk_Autostart]            = "Mit Windows starten",
            [Str.Setup_Hint_Autostart]           =
                "ULM startet dann automatisch (sichtbares Fenster) bei jeder Windows-Anmeldung. " +
                "Kein Admin-Recht nötig. Kann später hier jederzeit wieder deaktiviert werden.",

            [Str.Setup_Card_Design]              = "🌓 Design",
            [Str.Setup_Theme_System]             = "System",
            [Str.Setup_Theme_Light]              = "Hell",
            [Str.Setup_Theme_Dark]               = "Dunkel",
            [Str.Setup_Hint_Theme]               =
                "\"System\" übernimmt automatisch die aktuelle Windows-Einstellung. Kann später jederzeit " +
                "über ⚙ Einstellungen oben rechts geändert werden — auch live, ohne Neustart.",

            [Str.Setup_Card_Language]            = "🌐 Sprache",
            [Str.Setup_Hint_Language]            = "Wirkt nach einem Neustart von ULM. Kann später jederzeit über ⚙ Einstellungen oben rechts geändert werden.",

            [Str.Setup_Chk_DontShowAgain]        = "Diese Einrichtung beim nächsten Start überspringen (Modus wird gespeichert)",
            [Str.Setup_Btn_Apply]                = "✔ Übernehmen",

            [Str.Setup_Error_Title]              = "Fehler",
            [Str.Setup_Error_FolderCreateFailed] = "Ordner konnte nicht erstellt werden:",
        };

        private static readonly Dictionary<Str, string> En = new()
        {
            [Str.Tab_IsoSelection]             = "ISO Selection",
            [Str.Tab_Log]                      = "Log",
            [Str.Tab_Status]                   = "Status",
            [Str.Btn_Download]                 = "⬇  Download",
            [Str.Btn_CheckForUpdates]          = "↻  Check for Updates",
            [Str.Btn_Cancel]                   = "✕  Stop",
            [Str.Btn_Help]                     = "❓ Help",
            [Str.Btn_Settings]                 = "⚙ Settings",
            [Str.LanguageChangeConfirm_Title]  = "Language changed",
            [Str.LanguageChangeConfirm_Message] = "Restart ULM now to apply the new language?",

            [Str.Setup_Title_Welcome]            = "Universal Linux Manager — Setup",
            [Str.Setup_Title_Settings]           = "Universal Linux Manager — Settings",
            [Str.Setup_Header_Welcome]           = "Welcome to Universal Linux Manager",
            [Str.Setup_Header_Settings]          = "Settings",
            [Str.Setup_Subtitle_Welcome]         = "Quick setup, then you're ready to go.",
            [Str.Setup_Subtitle_Settings]        = "Changes take effect after clicking \"✔ Apply\".",

            [Str.Setup_Directory_Header]         = "Storage location for ISO downloads and settings files:",
            [Str.Setup_Btn_Browse]               = "📂 Browse",
            [Str.Setup_FolderDialog_Title]       = "Choose a working directory for Universal Linux Manager",
            [Str.Setup_Btn_UseDefaultPath]       = "Use default path",
            [Str.Setup_Directory_ItemsIntro]     = "The following items will be created:",
            [Str.Setup_Directory_ItemDownloads]  = "ISO downloads",
            [Str.Setup_Directory_ItemDatabase]   = "ISO database",
            [Str.Setup_Directory_ItemLog]        = "Log file",

            [Str.Setup_Card_AboutUlm]            = "ℹ About ULM",
            [Str.Setup_WelcomeBody]              =
                "With this tool you can effortlessly manage 20–30 different Linux distributions, " +
                "automatically download the latest ISOs, and transfer them to your bootable Ventoy USB stick.\n\n" +
                "Features at a glance:\n" +
                "• Automated URL checking & version detection\n" +
                "• Integrated Ventoy installation & Secure Boot support\n" +
                "• Parallel downloads for maximum performance",

            [Str.Setup_Card_Mode]                = "👤 Mode",
            [Str.Setup_Chk_ExpertMode]           = "Enable expert mode (all features visible)",
            [Str.Setup_Hint_Mode]                =
                "Determines how many features and advanced settings are shown in the main program. " +
                "Unchecked = user mode (recommended). The mode can be changed later at any time via ⚙ Settings in the top right.",

            [Str.Setup_Card_Autostart]           = "🚀 Autostart",
            [Str.Setup_Chk_Autostart]            = "Start with Windows",
            [Str.Setup_Hint_Autostart]           =
                "ULM will then start automatically (visible window) at every Windows login. " +
                "No admin rights required. Can be disabled again here at any time.",

            [Str.Setup_Card_Design]              = "🌓 Theme",
            [Str.Setup_Theme_System]             = "System",
            [Str.Setup_Theme_Light]              = "Light",
            [Str.Setup_Theme_Dark]               = "Dark",
            [Str.Setup_Hint_Theme]               =
                "\"System\" automatically follows the current Windows setting. Can be changed later at any time " +
                "via ⚙ Settings in the top right — even live, without a restart.",

            [Str.Setup_Card_Language]            = "🌐 Language",
            [Str.Setup_Hint_Language]            = "Takes effect after restarting ULM. Can be changed later at any time via ⚙ Settings in the top right.",

            [Str.Setup_Chk_DontShowAgain]        = "Skip this setup on next start (mode will be saved)",
            [Str.Setup_Btn_Apply]                = "✔ Apply",

            [Str.Setup_Error_Title]              = "Error",
            [Str.Setup_Error_FolderCreateFailed] = "Could not create folder:",
        };
    }
}
