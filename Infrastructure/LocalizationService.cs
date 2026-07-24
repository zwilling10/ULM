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
            [Str.Setup_Subtitle_Settings]        = "Änderungen wirken nach Klick auf „✔ Übernehmen“.",

            [Str.Setup_Card_Directory]           = "📁 Arbeitsordner",
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

            [Str.Main_HeaderSubtitle]            = "USB-Stick einrichten · Linux ISOs verwalten · Downloads überwachen",
            [Str.Main_Chip_OnlineScan]           = "🌐 Online-Scan",
            [Str.Main_Chip_UsbScan]              = "💾 Stick-Scan",
            [Str.Main_Chip_HealthCheck]          = "🩺 Gesundheitscheck",
            [Str.Main_Btn_Dismiss]               = "Ausblenden",
            [Str.Main_Label_TargetDrive]         = "ZIEL-USB-LAUFWERK",
            [Str.Main_Tooltip_RefreshDrives]     = "Laufwerke neu einlesen",
            [Str.Main_Btn_InstallVentoy]         = "⚡ Ventoy installieren",
            [Str.Main_Chk_SecureBoot]            = "🔒 Secure Boot",
            [Str.Main_Tooltip_HashStatusColumn]  = "Hash-Status: grün = Prüfsumme vorhanden, rot = Integritätsprüfung fehlgeschlagen",
            [Str.Main_ColumnHeader_Distribution] = "Linux-Distribution  (Haken = Download)",
            [Str.Main_ColumnHeader_Local]        = "Lokal",
            [Str.Main_ColumnHeader_OnStick]      = "Auf dem Stick",
            [Str.Main_ColumnHeader_Current]      = "Aktuell",
            [Str.Main_Btn_ClearLog]              = "Protokoll leeren",
            [Str.Main_Status_CurrentOperation]   = "Aktueller Vorgang",
            [Str.Main_Status_NoOperation]        = "Kein Vorgang aktiv.",
            [Str.Main_Status_OnlineScanRunning]  = "🌐 Automatischer Online-Versionscheck läuft — Details siehe „Automatische Hintergrund-Scans” unten.",
            [Str.Main_Status_UsbScanRunning]     = "💾 Automatische Stick-Prüfung läuft — Details siehe „Automatische Hintergrund-Scans” unten.",
            [Str.Main_Status_LabelOperation]     = "Vorgang: ",
            [Str.Main_Status_LabelFile]          = "Datei: ",
            [Str.Main_Status_LabelProgress]      = "Fortschritt: ",
            [Str.Main_Status_LabelDetail]        = "Detail: ",
            [Str.Main_Status_LabelCounter]       = "Zähler: ",
            [Str.Main_Status_LabelTargetDrive]   = "Ziel-Laufwerk: ",
            [Str.Main_Status_SectionBackgroundScans] = "Automatische Hintergrund-Scans",
            [Str.Main_Status_LabelOnlineCheck]   = "🌐 Online-Versionscheck: ",
            [Str.Main_Status_Running]            = "läuft …",
            [Str.Main_Status_Inactive]           = "inaktiv",
            [Str.Main_Status_LabelLastChecked]   = "↳ zuletzt geprüft: ",
            [Str.Main_Status_LabelCompletedPrefix] = "  (abgeschlossen: ",
            [Str.Main_Status_LabelUsbCheck]      = "💾 Stick-Prüfung: ",
            [Str.Main_Status_LabelDrive]         = "↳ Laufwerk: ",
            [Str.Main_Status_SectionScheduled]   = "Geplante automatische Aktionen",
            [Str.Main_Status_LabelNextCheck]     = "🌐 Nächster automatischer Online-Versionscheck: ",
            [Str.Main_Status_DriveMonitoring]    = "🔌 Laufwerks-Überwachung: läuft laufend im Hintergrund (Prüfung alle 8 Sekunden).",
            [Str.Main_Status_SectionHistory]     = "Verlauf",
            [Str.Main_Btn_ClearHistory]          = "Verlauf leeren",
            [Str.Main_Btn_CheckUrls]             = "🌐  URLs prüfen",
            [Str.Main_Btn_SearchIso]             = "🔍  ISO suchen",
            [Str.Main_Btn_EditDb]                = "🗃  Datenbank",
            [Str.Main_Btn_HealthCheck]           = "🩺  DB-Gesundheitscheck",
            [Str.Main_Tooltip_HealthCheck]       = "Prüft für alle Distros in der Datenbank, ob sie aktuell online erreichbar und ladbar sind.",
            [Str.Main_Btn_CopyUsb]               = "🔁  Verpasste Kopien nachholen",
            [Str.Main_Tooltip_CopyUsb]           =
                "Manuelles Sicherheitsnetz: kopiert bereits lokal vollständig heruntergeladene, ausgewählte ISOs (erneut) auf den Stick — " +
                "z.B. wenn die automatische 'Jetzt kopieren?'-Nachfrage abgelehnt wurde oder eine vorherige Kopie fehlgeschlagen ist. " +
                "Der automatische Scan bietet dieselbe ISO pro Stick nur einmal je Sitzung an.",
            [Str.Main_Btn_VerifyIntegrity]       = "🔒  Integrität prüfen",
            [Str.Main_Tooltip_VerifyIntegrity]   = "Prüft die ISOs auf dem gewählten Stick gegen den beim Download/Import gespeicherten SHA-256-Referenzhash.",
            [Str.Main_Btn_GitHubToken]           = "🔑  GitHub-Token",
            [Str.Main_Tooltip_GitHubToken]       = "Optional: hebt das API-Limit für GitHub-basierte Distros von 60 auf 5000 Anfragen/Std an.",
            [Str.Main_Chk_ShowInfo]              = "Info-Fenster (Mouseover) anzeigen",

            [Str.Msg_SlowDownload_Body]          =
                "{0}: Es wurde kein schnellerer Mirror gefunden — {1} überträgt weiterhin nur sehr langsam.\n\n" +
                "Trotzdem mit dieser Quelle fortfahren? (Das kann sehr lange dauern.)",
            [Str.Msg_SlowDownload_Title]         = "⚠ Langsamer Download",
            [Str.Msg_OrphanedIncomplete_Title]   = "Unvollständige ISOs auf dem Stick gefunden",
            [Str.Msg_OrphanedIncomplete_Description] = "unvollständige ISO-Datei(en) auf dem Stick",
            [Str.Msg_OperationComplete_Title]    = "✅ Vorgang abgeschlossen",
            [Str.Main_Footer_IsoFolder]          = "ISO-Ordner: {0}",
            [Str.Msg_UpdateDownloadFailed]       = "Der Download des Programm-Updates ist fehlgeschlagen.",
            [Str.Msg_StickOutdatedFound]         = "Auf {0} wurden {1} veraltete ISO(s) gefunden:",
            [Str.Msg_UpdateNow]                  = "Jetzt aktualisieren?",
            [Str.Msg_StickUpdate_Title]          = "💾 Stick-Aktualisierung",
            [Str.Msg_OutdatedDuplicates_Title]   = "Veraltete Duplikate auf dem Stick gefunden",
            [Str.Msg_OutdatedDuplicates_Description] = "veraltete Duplikat-ISO(s) — aktuelle Version bereits vorhanden",
            [Str.Msg_LocalNotOnStick]            = "{0} ISO(s) vollständig lokal, NICHT auf {1}:",
            [Str.Msg_CopyNow]                    = "Jetzt kopieren?",
            [Str.Msg_LocalNotOnStick_Title]      = "💾 Vollständige ISOs nicht auf dem Stick",
            [Str.Msg_DeleteLocalAfterCopy_Immediate] = "Lokale Dateien danach löschen?",
            [Str.Msg_DeleteLocalAfterCopy_AfterCopy] = "Lokale Dateien nach dem Kopieren löschen?",
            [Str.Msg_DeleteFiles_Title]          = "Dateien löschen?",
            [Str.Msg_NoUsbDetected]              = "Kein USB-Laufwerk erkannt.",
            [Str.Msg_SelectAtLeastOne]           = "Bitte mindestens eine Distribution markieren!",
            [Str.Msg_DownloadMode_Body]          = "USB-Stick erkannt: {0}\n\nHerunterladen UND direkt auf Stick kopieren?",
            [Str.Msg_DownloadMode_Title]         = "Download-Modus",
            [Str.Msg_NoVentoy_Body]              = "Kein Ventoy auf {0}. Trotzdem kopieren?",
            [Str.Msg_NoVentoy_Title]             = "Ventoy nicht gefunden",
            [Str.Msg_NoStick_Body]               = "Kein USB-Stick.\n\nISOs gespeichert in:\n{0}\n\nFortfahren?",
            [Str.Msg_NoStick_Title]              = "Kein Stick erkannt",
            [Str.Msg_FreeSpace_LabelWorkDir]     = "dem Arbeitsordner ({0})",
            [Str.Msg_FreeSpace_LabelStick]       = "dem Stick {0}",
            [Str.Msg_FreeSpace_Body1]            = "Die {0} ausgewählten Distros benötigen zusammen ca. {1}",
            [Str.Msg_FreeSpace_Body2]            = " (bei {0} Distro(s) war die Größe online nicht ermittelbar — evtl. mehr)",
            [Str.Msg_FreeSpace_Body3]            = ",\naber auf {0} sind nur {1} frei.\n\nTrotzdem fortfahren?",
            [Str.Msg_FreeSpace_Title]            = "⚠ Nicht genug Speicherplatz",
            [Str.Msg_PhaseCopyToStick]           = "Kopiere auf Stick",
            [Str.Msg_SelectDriveFirst]           = "Bitte zuerst ein USB-Laufwerk auswählen!",
            [Str.Msg_PleaseWait]                 = "Bitte warten …",
            [Str.Msg_NoLocalIsos]                = "Keine lokal heruntergeladenen ISOs vorhanden.",
            [Str.Msg_NewDriveDetected_Body]      =
                "Neuer USB-Stick: {0}\nLabel: {1}   Größe: {2:F0} GB\n\n" +
                "Automatisch als Ventoy-Stick einrichten?\n\n⚠ ALLE DATEN AUF DIESEM STICK WERDEN GELÖSCHT!",
            [Str.Msg_NewDriveDetected_Title]     = "USB-Stick erkannt — Datenverlust!",
            [Str.Msg_NoLabel]                    = "Kein Name",
            [Str.Msg_MultipleDrivesHeader]       = "Es sind {0} USB-Sticks angeschlossen. Mit welchem möchtest du arbeiten?",
            [Str.Msg_VentoyUpdate_Body]          = "Ventoy auf\n\n   {0}  {1}  ({2} GB)\n\naktualisieren?\n\n✅ Bestehende ISO-Dateien bleiben erhalten.",
            [Str.Msg_VentoyInstall_Body]         = "⚠ ACHTUNG — DATENVERLUST!\n\nAlle Daten auf\n\n   {0}  {1}  ({2} GB)\n\nwerden unwiderruflich gelöscht!",
            [Str.Msg_VentoyUpdate_Title]         = "Ventoy aktualisieren",
            [Str.Msg_VentoyInstall_Title]        = "⚠ Ventoy installieren — Datenverlust!",

            [Str.Banner_UpdateAvailable]         = "🆕 Neue Version verfügbar: v{0} (installiert: v{1})",
            [Str.Banner_UpdateDownloading]       = "⬇ Update wird heruntergeladen …",
            [Str.Banner_UpdateReady]             = "✅ Update bereit — v{0}",
            [Str.Banner_UpdateBtn_Available]     = "⬇ Herunterladen …",
            [Str.Banner_UpdateBtn_Downloading]   = "⬇ Wird heruntergeladen …",
            [Str.Banner_UpdateBtn_ReadyToInstall] = "✅ Jetzt installieren & neu starten",
            [Str.Banner_HardCaseSingle]          = "🔧 Manuelle Quellen-Suche jetzt möglich für: {0}",
            [Str.Banner_HardCasePlural]          = "🔧 Manuelle Quellen-Suche jetzt möglich für {0} Distros: {1}",

            [Str.Row_ManualSearchTooltip]        = "Quelle manuell suchen/eintragen",
            [Str.Row_CategorySelectAllTooltip]   = "Alle Distros dieser Kategorie an-/abwählen",
            [Str.Row_Local]                      = "Lokal",
            [Str.Row_NotLocal]                   = "nicht lokal",
            [Str.Row_Yes]                        = "Ja",
            [Str.Row_No]                         = "Nein",
            [Str.Row_Outdated]                   = "Veraltet",
            [Str.Row_Unverified]                 = "Ungeprüft",
            [Str.Row_UpdatePrefix]               = "Update",
            [Str.Row_CurrentPrefix]              = "Aktuell",
            [Str.Row_LocallyAvailable]           = "Lokal vorhanden",
            [Str.Row_HashMismatch]               = "⚠ Hash-Abweichung — Datei weicht von der zuletzt gespeicherten Prüfsumme ab (evtl. beschädigt oder ersetzt).",
            [Str.Row_HashVerifiedOfficial]       = "✅ Prüfsumme gegen die offiziell vom Anbieter veröffentlichte Prüfsumme verifiziert.",
            [Str.Row_HashLocalOnly]              = "✅ Referenz-Prüfsumme lokal beim Download/Import berechnet (keine offizielle Gegenprüfung).",
            [Str.Row_TipImported]                = "📥  Vom USB-Stick importiert",
            [Str.Row_TipUrlOk]                   = "🌐✓  URL erreichbar — Download-Server antwortet",
            [Str.Row_TipUrlFail]                 = "🌐✗  URL nicht erreichbar — Download-Server antwortet nicht",
            [Str.Row_TipNewVersion]              = "🆕  Neue Version verfügbar: v{0}  (jetzt herunterladen)",

            [Str.Category_Gaming]                = "🎮 Gaming",
            [Str.Category_Security]              = "🔒 Sicherheit & Privatsphäre",
            [Str.Category_Beginner]              = "💻 Einsteiger (Komfort & Design)",
            [Str.Category_Lightweight]           = "🪶 Leichtgewicht (Geschwindigkeit & Effizienz)",
            [Str.Category_Advanced]              = "⚙ Fortgeschrittene (Unabhängigkeit & Stabilität)",
            [Str.Category_Rescue]                = "🛠 Rettung (Backup & Wiederherstellung)",
            [Str.Category_Antivirus]             = "🛡 Antivirus (Schutz & Bereinigung)",
            [Str.Category_WinPE]                 = "🪟 WinPE (Windows-Tools)",
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
            [Str.Setup_Subtitle_Settings]        = "Changes take effect after clicking “✔ Apply”.",

            [Str.Setup_Card_Directory]           = "📁 Working Directory",
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

            [Str.Main_HeaderSubtitle]            = "Set up USB stick · Manage Linux ISOs · Monitor downloads",
            [Str.Main_Chip_OnlineScan]           = "🌐 Online Scan",
            [Str.Main_Chip_UsbScan]              = "💾 Stick Scan",
            [Str.Main_Chip_HealthCheck]          = "🩺 Health Check",
            [Str.Main_Btn_Dismiss]               = "Dismiss",
            [Str.Main_Label_TargetDrive]         = "TARGET USB DRIVE",
            [Str.Main_Tooltip_RefreshDrives]     = "Rescan drives",
            [Str.Main_Btn_InstallVentoy]         = "⚡ Install Ventoy",
            [Str.Main_Chk_SecureBoot]            = "🔒 Secure Boot",
            [Str.Main_Tooltip_HashStatusColumn]  = "Hash status: green = checksum available, red = integrity check failed",
            [Str.Main_ColumnHeader_Distribution] = "Linux Distribution  (check = download)",
            [Str.Main_ColumnHeader_Local]        = "Local",
            [Str.Main_ColumnHeader_OnStick]      = "On Stick",
            [Str.Main_ColumnHeader_Current]      = "Current",
            [Str.Main_Btn_ClearLog]              = "Clear Log",
            [Str.Main_Status_CurrentOperation]   = "Current Operation",
            [Str.Main_Status_NoOperation]        = "No operation active.",
            [Str.Main_Status_OnlineScanRunning]  = "🌐 Automatic online version check running — see “Automatic Background Scans” below for details.",
            [Str.Main_Status_UsbScanRunning]     = "💾 Automatic stick check running — see “Automatic Background Scans” below for details.",
            [Str.Main_Status_LabelOperation]     = "Operation: ",
            [Str.Main_Status_LabelFile]          = "File: ",
            [Str.Main_Status_LabelProgress]      = "Progress: ",
            [Str.Main_Status_LabelDetail]        = "Detail: ",
            [Str.Main_Status_LabelCounter]       = "Counter: ",
            [Str.Main_Status_LabelTargetDrive]   = "Target drive: ",
            [Str.Main_Status_SectionBackgroundScans] = "Automatic Background Scans",
            [Str.Main_Status_LabelOnlineCheck]   = "🌐 Online version check: ",
            [Str.Main_Status_Running]            = "running …",
            [Str.Main_Status_Inactive]           = "inactive",
            [Str.Main_Status_LabelLastChecked]   = "↳ last checked: ",
            [Str.Main_Status_LabelCompletedPrefix] = "  (completed: ",
            [Str.Main_Status_LabelUsbCheck]      = "💾 Stick check: ",
            [Str.Main_Status_LabelDrive]         = "↳ Drive: ",
            [Str.Main_Status_SectionScheduled]   = "Scheduled Automatic Actions",
            [Str.Main_Status_LabelNextCheck]     = "🌐 Next automatic online version check: ",
            [Str.Main_Status_DriveMonitoring]    = "🔌 Drive monitoring: runs continuously in the background (checked every 8 seconds).",
            [Str.Main_Status_SectionHistory]     = "History",
            [Str.Main_Btn_ClearHistory]          = "Clear History",
            [Str.Main_Btn_CheckUrls]             = "🌐  Check URLs",
            [Str.Main_Btn_SearchIso]             = "🔍  Search ISO",
            [Str.Main_Btn_EditDb]                = "🗃  Database",
            [Str.Main_Btn_HealthCheck]           = "🩺  DB Health Check",
            [Str.Main_Tooltip_HealthCheck]       = "Checks whether all distros in the database are currently reachable and downloadable online.",
            [Str.Main_Btn_CopyUsb]               = "🔁  Catch Up Missed Copies",
            [Str.Main_Tooltip_CopyUsb]           =
                "Manual safety net: (re-)copies already fully locally downloaded, selected ISOs to the stick — " +
                "e.g. if the automatic 'Copy now?' prompt was declined or a previous copy failed. " +
                "The automatic scan only offers the same ISO for a stick once per session.",
            [Str.Main_Btn_VerifyIntegrity]       = "🔒  Verify Integrity",
            [Str.Main_Tooltip_VerifyIntegrity]   = "Checks the ISOs on the selected stick against the SHA-256 reference hash saved at download/import time.",
            [Str.Main_Btn_GitHubToken]           = "🔑  GitHub Token",
            [Str.Main_Tooltip_GitHubToken]       = "Optional: raises the API limit for GitHub-based distros from 60 to 5000 requests/hour.",
            [Str.Main_Chk_ShowInfo]              = "Show info window (mouseover)",

            [Str.Msg_SlowDownload_Body]          =
                "{0}: No faster mirror was found — {1} is still transferring very slowly.\n\n" +
                "Continue with this source anyway? (This can take a very long time.)",
            [Str.Msg_SlowDownload_Title]         = "⚠ Slow Download",
            [Str.Msg_OrphanedIncomplete_Title]   = "Incomplete ISOs Found on the Stick",
            [Str.Msg_OrphanedIncomplete_Description] = "incomplete ISO file(s) on the stick",
            [Str.Msg_OperationComplete_Title]    = "✅ Operation Completed",
            [Str.Main_Footer_IsoFolder]          = "ISO folder: {0}",
            [Str.Msg_UpdateDownloadFailed]       = "The download of the program update failed.",
            [Str.Msg_StickOutdatedFound]         = "{1} outdated ISO(s) found on {0}:",
            [Str.Msg_UpdateNow]                  = "Update now?",
            [Str.Msg_StickUpdate_Title]          = "💾 Stick Update",
            [Str.Msg_OutdatedDuplicates_Title]   = "Outdated Duplicates Found on the Stick",
            [Str.Msg_OutdatedDuplicates_Description] = "outdated duplicate ISO(s) — current version already present",
            [Str.Msg_LocalNotOnStick]            = "{0} ISO(s) fully local, NOT on {1}:",
            [Str.Msg_CopyNow]                    = "Copy now?",
            [Str.Msg_LocalNotOnStick_Title]      = "💾 Complete ISOs Not on the Stick",
            [Str.Msg_DeleteLocalAfterCopy_Immediate] = "Delete local files afterwards?",
            [Str.Msg_DeleteLocalAfterCopy_AfterCopy] = "Delete local files after copying?",
            [Str.Msg_DeleteFiles_Title]          = "Delete Files?",
            [Str.Msg_NoUsbDetected]              = "No USB drive detected.",
            [Str.Msg_SelectAtLeastOne]           = "Please select at least one distribution!",
            [Str.Msg_DownloadMode_Body]          = "USB stick detected: {0}\n\nDownload AND copy directly to the stick?",
            [Str.Msg_DownloadMode_Title]         = "Download Mode",
            [Str.Msg_NoVentoy_Body]              = "No Ventoy on {0}. Copy anyway?",
            [Str.Msg_NoVentoy_Title]             = "Ventoy Not Found",
            [Str.Msg_NoStick_Body]               = "No USB stick.\n\nISOs saved to:\n{0}\n\nContinue?",
            [Str.Msg_NoStick_Title]              = "No Stick Detected",
            [Str.Msg_FreeSpace_LabelWorkDir]     = "the working folder ({0})",
            [Str.Msg_FreeSpace_LabelStick]       = "the stick {0}",
            [Str.Msg_FreeSpace_Body1]            = "The {0} selected distros together need approx. {1}",
            [Str.Msg_FreeSpace_Body2]            = " (for {0} distro(s) the size could not be determined online — possibly more)",
            [Str.Msg_FreeSpace_Body3]            = ",\nbut only {1} is free on {0}.\n\nContinue anyway?",
            [Str.Msg_FreeSpace_Title]            = "⚠ Not Enough Disk Space",
            [Str.Msg_PhaseCopyToStick]           = "Copying to Stick",
            [Str.Msg_SelectDriveFirst]           = "Please select a USB drive first!",
            [Str.Msg_PleaseWait]                 = "Please wait …",
            [Str.Msg_NoLocalIsos]                = "No locally downloaded ISOs available.",
            [Str.Msg_NewDriveDetected_Body]      =
                "New USB stick: {0}\nLabel: {1}   Size: {2:F0} GB\n\n" +
                "Set up automatically as a Ventoy stick?\n\n⚠ ALL DATA ON THIS STICK WILL BE ERASED!",
            [Str.Msg_NewDriveDetected_Title]     = "USB Stick Detected — Data Loss!",
            [Str.Msg_NoLabel]                    = "No Name",
            [Str.Msg_MultipleDrivesHeader]       = "{0} USB sticks are connected. Which one would you like to work with?",
            [Str.Msg_VentoyUpdate_Body]          = "Update Ventoy on\n\n   {0}  {1}  ({2} GB)?\n\n✅ Existing ISO files will be kept.",
            [Str.Msg_VentoyInstall_Body]         = "⚠ WARNING — DATA LOSS!\n\nAll data on\n\n   {0}  {1}  ({2} GB)\n\nwill be irrevocably erased!",
            [Str.Msg_VentoyUpdate_Title]         = "Update Ventoy",
            [Str.Msg_VentoyInstall_Title]        = "⚠ Install Ventoy — Data Loss!",

            [Str.Banner_UpdateAvailable]         = "🆕 New version available: v{0} (installed: v{1})",
            [Str.Banner_UpdateDownloading]       = "⬇ Downloading update …",
            [Str.Banner_UpdateReady]             = "✅ Update ready — v{0}",
            [Str.Banner_UpdateBtn_Available]     = "⬇ Download …",
            [Str.Banner_UpdateBtn_Downloading]   = "⬇ Downloading …",
            [Str.Banner_UpdateBtn_ReadyToInstall] = "✅ Install Now & Restart",
            [Str.Banner_HardCaseSingle]          = "🔧 Manual source search now available for: {0}",
            [Str.Banner_HardCasePlural]          = "🔧 Manual source search now available for {0} distros: {1}",

            [Str.Row_ManualSearchTooltip]        = "Search/enter source manually",
            [Str.Row_CategorySelectAllTooltip]   = "Select/deselect all distros in this category",
            [Str.Row_Local]                      = "Local",
            [Str.Row_NotLocal]                   = "not local",
            [Str.Row_Yes]                        = "Yes",
            [Str.Row_No]                         = "No",
            [Str.Row_Outdated]                   = "Outdated",
            [Str.Row_Unverified]                 = "Unverified",
            [Str.Row_UpdatePrefix]               = "Update",
            [Str.Row_CurrentPrefix]              = "Current",
            [Str.Row_LocallyAvailable]           = "Available locally",
            [Str.Row_HashMismatch]               = "⚠ Hash mismatch — file differs from the last saved checksum (possibly corrupted or replaced).",
            [Str.Row_HashVerifiedOfficial]       = "✅ Checksum verified against the checksum officially published by the provider.",
            [Str.Row_HashLocalOnly]              = "✅ Reference checksum calculated locally at download/import time (no official cross-check).",
            [Str.Row_TipImported]                = "📥  Imported from USB stick",
            [Str.Row_TipUrlOk]                   = "🌐✓  URL reachable — download server responding",
            [Str.Row_TipUrlFail]                 = "🌐✗  URL not reachable — download server not responding",
            [Str.Row_TipNewVersion]              = "🆕  New version available: v{0}  (download now)",

            [Str.Category_Gaming]                = "🎮 Gaming",
            [Str.Category_Security]              = "🔒 Security & Privacy",
            [Str.Category_Beginner]              = "💻 Beginner (Comfort & Design)",
            [Str.Category_Lightweight]           = "🪶 Lightweight (Speed & Efficiency)",
            [Str.Category_Advanced]              = "⚙ Advanced (Independence & Stability)",
            [Str.Category_Rescue]                = "🛠 Rescue (Backup & Recovery)",
            [Str.Category_Antivirus]             = "🛡 Antivirus (Protection & Cleanup)",
            [Str.Category_WinPE]                 = "🪟 WinPE (Windows Tools)",
        };
    }
}
