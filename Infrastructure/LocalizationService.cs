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

            [Str.Main_ScanHint_Online]           = "Online-Scan, bitte warten",
            [Str.Main_ScanHint_Usb]              = "Stick-Scan, bitte warten",

            [Str.Main_DriveInfo_NoVentoy]        = "⚠ Kein Ventoy",
            [Str.Main_DriveInfo_FreeLabel]       = "Frei: ",

            [Str.Category_Gaming]                = "🎮 Gaming",
            [Str.Category_Security]              = "🔒 Sicherheit & Privatsphäre",
            [Str.Category_Beginner]              = "💻 Einsteiger (Komfort & Design)",
            [Str.Category_Lightweight]           = "🪶 Leichtgewicht (Geschwindigkeit & Effizienz)",
            [Str.Category_Advanced]              = "⚙ Fortgeschrittene (Unabhängigkeit & Stabilität)",
            [Str.Category_Rescue]                = "🛠 Rettung (Backup & Wiederherstellung)",
            [Str.Category_Antivirus]             = "🛡 Antivirus (Schutz & Bereinigung)",
            [Str.Category_WinPE]                 = "🪟 WinPE (Windows-Tools)",

            [Str.Help_Title]    = "❓ Universal Linux Manager — Hilfe & Dokumentation",
            [Str.Help_Subtitle] = "Bootfähige USB-Sticks mit Linux-ISOs einfach erstellen und verwalten.",
            [Str.Help_NavHeading] = "SPRUNGMARKEN",
            [Str.Help_Btn_Close]  = "✔ Schließen",

            [Str.Help_Sec_Overview_Title] = "🗺 Übersicht — Was macht ULM?",
            [Str.Help_Sec_Overview_Nav]   = "Übersicht",
            [Str.Help_Overview_Body] =
                "ULM ist ein Manager für Linux-Live-ISOs und Ventoy-USB-Sticks. Es erledigt vier Aufgaben:\n" +
                "  1. ISO-Downloads — lädt aktuelle Linux-Versionen direkt von den offiziellen Servern herunter\n" +
                "  2. USB-Verwaltung — installiert Ventoy auf dem Stick und kopiert ISOs dorthin\n" +
                "  3. Versionsüberwachung — prüft automatisch ob neuere ISO-Versionen verfügbar sind\n" +
                "  4. Datenmüll-Schutz — erkennt unvollständige/korrupte ISOs per Online-Größenprüfung, " +
                "sowohl im Arbeitsordner als auch auf dem Stick",

            [Str.Help_Sec_Startup_Title] = "🚀 Was passiert beim Programmstart?",
            [Str.Help_Sec_Startup_Nav]   = "Programmstart",
            [Str.Help_Startup_Intro]     = "Direkt nach dem Start laufen automatisch im Hintergrund, in dieser Reihenfolge:",
            [Str.Help_Item_OnlineCheck_Label] = "1. Online-Versionscheck",
            [Str.Help_Item_OnlineCheck_Body] =
                "Fragt zuerst für alle Distros in der Datenbank die aktuellste Version ab (ca. 5–30 Sek.) — " +
                "auch für vom Stick importierte Einträge. Findet neue Versionen automatisch — ohne manuelle " +
                "Eingabe von URLs. Aktualisiert die Datenbank-Einträge wenn eine neue Version verfügbar ist. " +
                "Ein pulsierender Hinweis oben in der Kopfzeile ('Online-Scan, bitte warten') zeigt an, dass " +
                "der Check noch läuft — am besten bis dahin noch nicht klicken, damit Datenbank und Stick-" +
                "Stand vollständig sind.",
            [Str.Help_Item_UsbScan_Label] = "2. USB-Stick-Scan",
            [Str.Help_Item_UsbScan_Body] =
                "Läuft erst NACH dem Versionscheck (nicht gleichzeitig), damit der Stick-Stand direkt mit den " +
                "aktuellsten Versionsdaten verglichen wird. Erkennt angeschlossene Ventoy-Sticks, zeigt welche " +
                "ISOs bereits drauf sind, welche veraltet sind und welche fehlen. Läuft erneut, wenn ein Stick " +
                "eingesteckt wird (derselbe pulsierende Hinweis, dann 'Stick-Scan, bitte warten'). Prüft dabei " +
                "jedes Mal zusätzlich die Online-Größe jeder gefundenen ISO (siehe 🧹 Datenmüll-Schutz).",
            [Str.Help_Item_FileMaintenance_Label] = "Datei-Wartung",
            [Str.Help_Item_FileMaintenance_Body] =
                "Läuft nach dem Versionscheck. Scannt den Arbeitsordner rekursiv und vergleicht jede ISO-Größe " +
                "mit der tatsächlichen Original-Größe beim Anbieter (Online-HEAD-Request). Erkennt so " +
                "unvollständige und abgebrochene Downloads zuverlässiger als eine feste Mindestgröße. " +
                "Bietet an, gefundenen Datenmüll zu löschen.",
            [Str.Help_Item_UpdateCheck_Label] = "ULM-Update-Check",
            [Str.Help_Item_UpdateCheck_Body] =
                "Prüft im Hintergrund, ob auf GitHub eine neuere ULM-Version verfügbar ist. Läuft rein " +
                "informativ mit — kein Dialog, keine Unterbrechung. Ist eine neue Version verfügbar, " +
                "erscheint nur eine Zeile im Protokoll:\n" +
                "  🆕 Neue ULM-Version verfügbar: vX.Y.Z (aktuell installiert: vA.B.C)\n" +
                "gefolgt vom Link zur Release-Seite.",
            [Str.Help_Item_WhatsNew_Label] = "„Was ist neu?“-Dialog",
            [Str.Help_Item_WhatsNew_Body] =
                "Erscheint automatisch beim ersten Start NACH einem Update auf eine neue ULM-Version " +
                "(nicht beim allerersten Programmstart) und listet alle Änderungen seit der zuletzt " +
                "gesehenen Version auf. Einmal quittiert, erscheint er erst beim nächsten Versionswechsel wieder.",
            [Str.Help_Item_Autostart_Label] = "🚀 Autostart (optional)",
            [Str.Help_Item_Autostart_Body] =
                "Checkbox 'Mit Windows starten' im Einrichtungsfenster — startet ULM dann automatisch " +
                "(sichtbares Fenster) bei jeder Windows-Anmeldung. Kein Admin-Recht nötig, funktioniert über " +
                "einen Registry-Eintrag nur für den aktuellen Benutzer. Lässt sich im Einrichtungsfenster " +
                "jederzeit wieder abwählen; ist das Fenster einmal per 'Nicht mehr anzeigen' übersprungen, " +
                "hilft ein Löschen des passenden Eintrags in 'ulm_settings.ini', um es erneut zu sehen.",

            [Str.Help_Sec_Usage_Title] = "📋 Die Verteilungs-Liste — Bedienung",
            [Str.Help_Sec_Usage_Nav]   = "Bedienung",
            [Str.Help_Item_SelectDownload_Label] = "ISO zum Download auswählen",
            [Str.Help_Item_SelectDownload_Body] = "Checkbox links aktivieren → ISO wird zum Download vorgemerkt (blauer Hintergrund). Mehrere ISOs gleichzeitig auswählen ist möglich.",
            [Str.Help_Item_CategoryCheckbox_Label] = "Kategorie-Checkbox",
            [Str.Help_Item_CategoryCheckbox_Body] = "Aktiviert oder deaktiviert alle Distros einer Kategorie auf einmal (z.B. alle 'Sicherheits'-Distros markieren).",
            [Str.Help_Item_DoubleClick_Label] = "Doppelklick auf Eintrag",
            [Str.Help_Item_DoubleClick_Body] = "Zeigt die Beschreibung der Distribution — Einsatzzweck, Besonderheiten, Zielgruppe.",
            [Str.Help_Item_MouseoverTooltip_Label] = "Mouseover (Tooltip)",
            [Str.Help_Item_MouseoverTooltip_Body] = "Hält man die Maus über den Distro-Namen, erscheint ein Tooltip. Er erklärt alle sichtbaren Symbole (📥, 🌐✓/✗, 🆕) UND zeigt die Distro-Beschreibung.",

            [Str.Help_Sec_Colors_Title] = "🎨 Farben & Symbole im Hauptfenster",
            [Str.Help_Sec_Colors_Nav]   = "Farben & Symbole",
            [Str.Help_Subhead_TextColors] = "Textfarben der Listeneinträge",
            [Str.Help_Color_Green_Label]  = "Grün",
            [Str.Help_Color_Green_Body]   = "ISO ist auf dem USB-Stick vorhanden (aktuellste Version, online größengeprüft) — oder lokal vollständig heruntergeladen und bereit zum Kopieren.",
            [Str.Help_Color_Orange_Label] = "Orange",
            [Str.Help_Color_Orange_Body]  = "Update verfügbar — online wurde eine neuere Version gefunden. Oder: veraltete Version auf dem Stick (neuere Version existiert).",
            [Str.Help_Color_Red_Label]    = "Rot",
            [Str.Help_Color_Red_Body]     = "URL nicht erreichbar — der Download-Server antwortet nicht. Erscheint nach einem URL-Check (Expert-Modus).",
            [Str.Help_Color_Teal_Label]   = "Türkis",
            [Str.Help_Color_Teal_Body]    = "Vom USB-Stick importiert — dieser Eintrag wurde beim Stick-Scan entdeckt und als neuer Eintrag hinzugefügt.",
            [Str.Help_Color_Blue_Label]   = "Gedämpftes Blau",
            [Str.Help_Color_Blue_Body]    = "Online-Check bestätigt: diese Version ist aktuell. Kein Update nötig, ISO ist auf dem neuesten Stand.",
            [Str.Help_Color_Gray_Label]   = "Hellgrau",
            [Str.Help_Color_Gray_Body]    = "Keine URL konfiguriert — für diesen Eintrag sind keine Download-URLs hinterlegt.",
            [Str.Help_Color_Dark_Label]   = "Dunkel (Standard)",
            [Str.Help_Color_Dark_Body]    = "Normaler Zustand — noch kein Online-Versionscheck durchgeführt, ISO nicht lokal und nicht auf dem Stick.",
            [Str.Help_Subhead_Columns] = "Spalten in der Liste",
            [Str.Help_Item_ColLocal_Label] = "Lokal",
            [Str.Help_Item_ColLocal_Body] =
                "Zeigt ob die ISO im lokalen Arbeitsordner vorhanden ist:\n" +
                "  'Lokal 3 565 MB' = heruntergeladen (mit Dateigröße)\n" +
                "  'nicht lokal'    = noch nicht heruntergeladen",
            [Str.Help_Item_ColOnStick_Label] = "Auf dem Stick",
            [Str.Help_Item_ColOnStick_Body] =
                "Zeigt den Status auf dem erkannten Ventoy-Stick:\n" +
                "  'Ja 3,56 GB'  = vorhanden, aktuelle Version, Online-Größe bestätigt\n" +
                "  'Veraltet …'  = auf dem Stick, aber veraltete Version\n" +
                "  'Nein'        = ISO fehlt auf dem Stick ODER wurde als unvollständig erkannt und entfernt\n" +
                "  'Ungeprüft'   = Stick wurde noch nicht gescannt",
            [Str.Help_Item_ColCurrent_Label] = "Aktuell",
            [Str.Help_Item_ColCurrent_Body] =
                "Zeigt das Ergebnis des Online-Versionschecks:\n" +
                "  'Update vX.Y.Z'     = neuere Version online verfügbar\n" +
                "  'Aktuell (vX.Y.Z)'  = Online-Check: bereits aktuellste Version\n" +
                "  'Lokal vorhanden'   = lokal vorhanden, kein Online-Check\n" +
                "  '?'                 = noch nicht geprüft",
            [Str.Help_Subhead_HashSymbol] = "Hash-Status-Symbol (schmale Spalte links vom Namen)",
            [Str.Help_HashSymbol_Body] =
                "Ein kleiner, selbst gezeichneter Smiley zeigt den Integritäts-Status der lokal " +
                "gespeicherten SHA-256-Prüfsumme (siehe 🔒 Integrität prüfen weiter unten):\n" +
                "  Grün  = Referenz-Hash vorhanden (lokal berechnet oder offiziell verifiziert)\n" +
                "  Rot   = bei der letzten Integritätsprüfung eine Abweichung gefunden — Datei " +
                "vermutlich beschädigt oder ersetzt\n" +
                "  Kein Symbol = noch kein Hash vorhanden (ISO noch nie heruntergeladen/importiert) — " +
                "absichtlich neutral, nicht rot, damit unberührte ISOs nicht wie ein Problem aussehen\n" +
                "Mouseover auf dem Symbol zeigt den genauen Grund an.",
            [Str.Help_Subhead_NameSymbols] = "Symbole im Distro-Namen (Mouseover zeigt Erklärung)",
            [Str.Help_Item_SymbolImported_Label] = "📥 (Präfix)",
            [Str.Help_Item_SymbolImported_Body] = "Vom USB-Stick importiert — diese ISO wurde beim Stick-Scan entdeckt und als neuer Eintrag hinzugefügt (nicht aus der Standard-Datenbank).",
            [Str.Help_Item_SymbolUrlOk_Label] = "🌐✓ (Suffix)",
            [Str.Help_Item_SymbolUrlOk_Body] = "URL-Check bestanden — die Download-URL ist erreichbar. Mouseover zeigt: 'URL erreichbar — Download-Server antwortet'.",
            [Str.Help_Item_SymbolUrlFail_Label] = "🌐✗ (Suffix)",
            [Str.Help_Item_SymbolUrlFail_Body] = "URL-Check fehlgeschlagen — die Download-URL ist nicht erreichbar. Mouseover zeigt: 'URL nicht erreichbar — Download-Server antwortet nicht'.",
            [Str.Help_Item_SymbolNewVersion_Label] = "🆕 vX.Y.Z (Suffix)",
            [Str.Help_Item_SymbolNewVersion_Body] = "Online wurde eine neuere Version (hier beispielhaft: vX.Y.Z) gefunden. Mouseover zeigt: 'Neue Version verfügbar: vX.Y.Z (jetzt herunterladen)'. Eintrag auswählen und Download starten.",
            [Str.Help_Subhead_CategorySymbols] = "Kategorie-Symbole (linke Spalte)",
            [Str.Help_CategorySymbols_Body] =
                "  🖥 Einsteiger        — Benutzerfreundliche Distributionen für den Desktop-Einstieg\n" +
                "  ⚙ Fortgeschrittene  — Mehr Konfigurationsfreiheit, Arch-basierte Systeme\n" +
                "  🪶 Leichtgewicht     — Ressourcensparend, für ältere und schwächere Hardware\n" +
                "  🎮 Gaming            — Für Spiele optimiert (ProtonGE, Steam, MangoHud)\n" +
                "  🔒 Sicherheit        — Datenschutz, Anonymität, Pen-Testing (Tails, Parrot, Kodachi)\n" +
                "  🛠 Rettung           — Rettungs- und Reparatur-Live-Systeme (GParted, Clonezilla)\n" +
                "  🛡 Antivirus         — Live-Systeme zur Virenprüfung und -entfernung\n" +
                "  🪟 WinPE             — Windows-basierte Rettungsumgebungen (Hiren's BootCD)",

            [Str.Help_Sec_Theme_Title] = "🌓 Design — Hell / Dunkel / System",
            [Str.Help_Sec_Theme_Nav]   = "Design",
            [Str.Help_Theme_Intro]     = "ULM hat ein helles und ein dunkles Erscheinungsbild. Beide sind vollständig durchgestylt (Listen, Dialoge, Eingabefelder) und für gute Lesbarkeit geprüft.",
            [Str.Help_Item_ThemeSetting_Label] = "Einstellen",
            [Str.Help_Item_ThemeSetting_Body] = "Beim Ersteinrichten im Setup-Dialog wählbar, oder jederzeit über den Knopf '🌓 Design: …' oben rechts im Hauptfenster (neben 'Modus: Anwender/Experte'). Ein Klick wechselt der Reihe nach zwischen System → Hell → Dunkel.",
            [Str.Help_Item_ThemeSystem_Label] = "System",
            [Str.Help_Item_ThemeSystem_Body] = "Übernimmt automatisch die aktuelle Windows-Design-Einstellung (Hell oder Dunkel). Ändert sich das Windows-Design während ULM läuft, zieht ULM automatisch nach — ohne Neustart.",
            [Str.Help_Item_ThemeInstant_Label] = "Sofortige Umschaltung",
            [Str.Help_Item_ThemeInstant_Body] = "Ein Wechsel wirkt sofort auf das gesamte offene Hauptfenster — inklusive der Zeilenfarben in der Distro-Liste. Kein Neustart nötig. Neu geöffnete Dialoge (Hilfe, Datenbank, Einrichtung, …) übernehmen die Wahl automatisch.",
            [Str.Help_Item_ThemeRemembers_Label] = "Merkt sich die Wahl",
            [Str.Help_Item_ThemeRemembers_Body] = "Die getroffene Wahl wird gespeichert und beim nächsten Programmstart automatisch wieder angewendet.",

            [Str.Help_Sec_LogSymbols_Title] = "📜 Protokoll-Symbole — Bedeutung",
            [Str.Help_Sec_LogSymbols_Nav]   = "Protokoll-Symbole",
            [Str.Help_LogSymbols_Body] =
                "  ▶   Programmstart / Abschnittsbeginn\n" +
                "  💾  Datenbank-Aktion oder Stick-Scan\n" +
                "  🔌  Laufwerk erkannt / Stick eingesteckt\n" +
                "  🌐  Online-Versionscheck läuft\n" +
                "  ⬇   Download gestartet oder in Bearbeitung\n" +
                "  🔗  Download-URL (zeigt welcher Server verwendet wird)\n" +
                "  ✅  Aktion erfolgreich abgeschlossen\n" +
                "  ❌  Fehler aufgetreten\n" +
                "  ⚠   Warnung (kein Fehler, aber Aufmerksamkeit nötig) — u.a. unvollständige Dateien\n" +
                "  🆕  Neue Version online gefunden\n" +
                "  ✓   Version ist aktuell (kein Update nötig)\n" +
                "  ✏   Anzeigename automatisch aktualisiert\n" +
                "  ↔   Dateiname in der Datenbank ersetzt\n" +
                "  🗑  Eintrag oder Datei gelöscht (auch: Datenmüll auf dem Stick entfernt)\n" +
                "  🔄  Duplikat zusammengeführt\n" +
                "  📋  Kopiervorgang auf den USB-Stick\n" +
                "  📂  Datei beim Import in den Kategorie-Ordner auf dem Stick verschoben\n" +
                "  ❓  Unbekannte ISO(s) auf dem Stick gefunden — Import möglich\n" +
                "  ⛔  Vorgang abgebrochen",

            [Str.Help_Sec_IsoSearch_Title] = "🔍 ISO suchen — neue Distros entdecken",
            [Str.Help_Sec_IsoSearch_Nav]   = "ISO suchen",
            [Str.Help_IsoSearch_Intro]     = "Der Knopf '🔍 ISO suchen' zeigt zwei Online-Listen von DistroWatch.com — eine Möglichkeit, gezielt neue Distros zu entdecken, statt nur die feste Standard-Datenbank durchzugehen. Für die bereits bekannte Datenbank gibt es weiterhin '🗃 Datenbank'.",
            [Str.Help_Item_Newest_Label] = "🆕 Aktuellste",
            [Str.Help_Item_Newest_Body] = "Die zuletzt neu zu DistroWatch hinzugefügten Distributionen (Top 10).",
            [Str.Help_Item_Popular_Label] = "🔥 Beliebteste",
            [Str.Help_Item_Popular_Body] = "DistroWatchs Page-Hit-Ranking (Top 10) — die aktuell meistbesuchten Distro-Profile.",
            [Str.Help_Item_LiveOnly_Label] = "Nur Live-Medium",
            [Str.Help_Item_LiveOnly_Body] = "Beide Listen zeigen AUSSCHLIESSLICH Distros mit dem DistroWatch-Kategorie-Tag 'Live Medium' — reine Installations- oder Server-Images ohne Live-Boot-Modus werden automatisch aussortiert. Jeder Vorschlag ist also garantiert per USB-Stick bootfähig.",
            [Str.Help_Item_AlreadyInDb_Label] = "Bereits vorhanden",
            [Str.Help_Item_AlreadyInDb_Body] = "Distros, die schon in der eigenen Datenbank stehen, werden blau hervorgehoben und können nicht erneut übernommen werden. Bei neuen Distros zeigt ein Mouseover-Tooltip Rang/Datum, vorgeschlagene Kategorie und den DistroWatch-Link.",
            [Str.Help_Item_AdoptAndDownload_Label] = "Übernehmen + Direkt herunterladen",
            [Str.Help_Item_AdoptAndDownload_Body] = "Ausgewählte Distros per '✔ Übernehmen' in die Datenbank aufnehmen (Kategorie vorher per Dropdown anpassbar). Ist zusätzlich 'Direkt herunterladen' angehakt, startet nach dem Schließen des Fensters sofort der reguläre Download-Ablauf für diese Einträge.",
            [Str.Help_Item_RefreshCache_Label] = "Aktualisieren / Cache",
            [Str.Help_Item_RefreshCache_Body] = "Beide Listen werden 24 Stunden lokal zwischengespeichert (kein Netzwerk-Roundtrip bei jedem Öffnen). Der Knopf '⟳ Aktualisieren' erzwingt eine frische Abfrage.",

            [Str.Help_Sec_Download_Title] = "⬇ Download — Wie und Wohin?",
            [Str.Help_Sec_Download_Nav]   = "Download",
            [Str.Help_Item_StorageLocation_Label] = "Speicherort",
            [Str.Help_Item_StorageLocation_Body] = "Alle ISOs werden im Arbeitsordner des Programms gespeichert (Unterordner 'ISOs'). Da ULM portabel ist, liegt der Arbeitsordner neben der Programmdatei — der genaue Pfad hängt davon ab, wohin ULM gespeichert wurde.",
            [Str.Help_Item_PipelineMode_Label] = "Pipeline-Modus",
            [Str.Help_Item_PipelineMode_Body] =
                "Wenn ein Ventoy-Stick erkannt wird, kann jede ISO direkt nach dem Download " +
                "auf den Stick kopiert werden. Die lokale Datei wird danach gelöscht. " +
                "Downloads und Kopieren laufen parallel. Im Fortschritts-Dialog wechselt die Zeile " +
                "einer ISO von 'Kopiere auf Stick' zu 'Fertig', sobald sie vollständig kopiert ist. " +
                "Der Gesamt-Fortschrittsbalken (Amber → Blau → Grün je nach Prozent) sowie die " +
                "Abschluss-Meldung spiegeln dabei den tatsächlichen Kopier-Erfolg wider — schlägt die " +
                "Stick-Kopie fehl, wird das jetzt klar als Fehlschlag gemeldet, statt fälschlich " +
                "'erfolgreich heruntergeladen und kopiert' anzuzeigen, nur weil der Download geklappt hat.",
            [Str.Help_Item_MirrorRace_Label] = "Mirror-Race (bis zu 8 Quellen)",
            [Str.Help_Item_MirrorRace_Body] =
                "Bevor der eigentliche Download beginnt, testet ULM alle konfigurierten Mirror-URLs " +
                "einer Distro parallel für ca. 3 Sekunden und startet dann mit der schnellsten Quelle " +
                "— nicht einfach mit der ersten. Gemessen wird in kurzen Zeitfenstern statt eines " +
                "einzigen Durchschnittswerts, damit CDNs, die erst nach ein bis zwei Sekunden auf volle " +
                "Geschwindigkeit hochfahren, nicht fälschlich als langsam eingestuft werden. Bei " +
                "SourceForge-Quellen fächert ULM zusätzlich automatisch mehrere geografisch verteilte " +
                "Mirror auf, statt sich auf SourceForges eigene (oft nicht optimale) Serverwahl zu " +
                "verlassen. Ergebnis erscheint im Protokoll:\n" +
                "  🔎 Distro: Mirror-Test — cdn1.beispiel.org 42,3 Mbit/s, …",
            [Str.Help_Item_SpeedGuard_Label] = "Geschwindigkeits-Wächter",
            [Str.Help_Item_SpeedGuard_Body] =
                "Bleibt eine laufende Übertragung (nach ca. 20 Sekunden Anlaufzeit) für weitere 20 " +
                "Sekunden ununterbrochen unter ca. 1 MB/s, bricht ULM automatisch ab und versucht die " +
                "nächste Mirror-Quelle — statt stundenlang auf einer extrem langsamen Verbindung zu " +
                "warten. Gibt es keine schnellere Quelle mehr und alle Versuche waren nur an der " +
                "Geschwindigkeit gescheitert (nicht an einem echten Fehler), fragt ULM aktiv nach:\n" +
                "  ⚠ Kein schnellerer Mirror gefunden — trotzdem mit dieser Quelle fortfahren?\n" +
                "Bestätigt man das, läuft der Download ohne weitere Geschwindigkeitsprüfung zu Ende.",
            [Str.Help_Item_FasterButton_Label] = "„(schneller)“-Button",
            [Str.Help_Item_FasterButton_Body] =
                "Erscheint im Download-Fortschrittsfenster neben einer laufenden Übertragung — aber " +
                "erst nach ca. 20 Sekunden Anlaufzeit UND nur, solange die tatsächlich gemessene " +
                "Geschwindigkeit spürbar mittelmäßig ist (unter ca. 3 MB/s), selbst wenn der Server " +
                "damit noch über der Geschwindigkeits-Wächter-Schwelle liegt (also gar nicht automatisch " +
                "abbrechen würde). Bei bereits guter Geschwindigkeit bleibt der Button versteckt — es " +
                "gibt dann nichts, wozu ein Wechsel sinnvoll wäre. Voraussetzung ist außerdem immer " +
                "mindestens ein weiterer, vom Mirror-Race bereits gemessener Kandidat. Klick bricht den " +
                "aktuellen Versuch ab und wechselt sofort zum nächsten Kandidaten. Findet sich dabei " +
                "kein schnellerer Server, kehrt ULM automatisch — ohne Nachfrage — zum ursprünglichen, " +
                "nachweislich erreichbaren Server zurück ('Kein schnellerer Server gefunden — Download " +
                "wird fortgesetzt').",
            [Str.Help_Item_EtaRemaining_Label] = "Verbleibende Zeit (ETA)",
            [Str.Help_Item_EtaRemaining_Body] =
                "Der Fortschritts-Dialog zeigt neben Geschwindigkeit und Größe auch die geschätzte " +
                "Restzeit an, z.B.:\n  12.4 MB/s  ·  noch 2m 14s  ·  1.2 GB / 3.5 GB\n" +
                "Die Schätzung passt sich laufend an die aktuelle Download-Geschwindigkeit an.",
            [Str.Help_Item_VerifyIntegrity_Label] = "🔒 Integrität prüfen",
            [Str.Help_Item_VerifyIntegrity_Body] = "Nach jedem Download oder beim Import vom Stick speichert ULM einen SHA-256-Referenzhash der ISO-Datei. Bei Ubuntu, Debian und Fedora prüft ULM automatisch die offizielle Prüfsumme vom Anbieter. Mit dem Button '🔒 Integrität prüfen' lässt sich jederzeit die Datei auf dem Stick gegen den Referenzhash verifizieren — warnt wenn die Kopie beschädigt oder unvollständig wurde.",
            [Str.Help_Item_FreeSpaceCheck_Label] = "Freispeicher-Check",
            [Str.Help_Item_FreeSpaceCheck_Body] =
                "Zweistufig: BEVOR der Download überhaupt startet, summiert ULM die online " +
                "ermittelbare Größe ALLER markierten Distros und vergleicht sie mit dem freien " +
                "Speicher im Arbeitsordner UND — falls direkt auf einen Stick kopiert werden soll — " +
                "zusätzlich mit dem freien Speicher dort. Reicht der Platz nicht, warnt ULM VOR " +
                "Beginn mit einer Ja/Nein-Rückfrage, statt erst mittendrin auf mehreren parallelen " +
                "Downloads zugleich zu scheitern. Zusätzlich prüft ein zweiter, feingranularer Check " +
                "unmittelbar vor jeder einzelnen Datei erneut den dann noch verfügbaren Platz:\n" +
                "  ❌ Nicht genug Speicherplatz auf X:\\ (benötigt 3.5 GB, frei 1.1 GB).",

            [Str.Help_Sec_UsbStick_Title] = "💾 USB-Stick-Verwaltung (Ventoy)",
            [Str.Help_Sec_UsbStick_Nav]   = "USB-Stick / Ventoy",
            [Str.Help_Item_WhatIsVentoy_Label] = "Was ist Ventoy?",
            [Str.Help_Item_WhatIsVentoy_Body] = "Ventoy richtet einen USB-Stick so ein, dass mehrere Linux-ISOs gleichzeitig gespeichert und beim Booten ausgewählt werden können. Einmal einrichten, dann einfach ISOs draufkopieren — kein Neu-Flashen nötig.",
            [Str.Help_Item_InstallUpdateVentoy_Label] = "Ventoy installieren / aktualisieren",
            [Str.Help_Item_InstallUpdateVentoy_Body] =
                "Nur im Expert-Modus sichtbar. " +
                "⚠ NEUINSTALLATION löscht ALLE Daten auf dem Stick! " +
                "Aktualisieren behält bestehende ISOs. Läuft als Administrator (UAC) in einem " +
                "eigenen ULM-Fenster mit Fortschrittsanzeige und Protokoll — Ventoy2Disk.exe selbst " +
                "läuft dabei komplett unsichtbar im Hintergrund (offizieller Silent-/CLI-Modus, " +
                "keine eigene Ventoy-Oberfläche, keine manuelle Bedienung nötig). Während die " +
                "Installation läuft, pausiert ULM die automatische Laufwerkserkennung — es können " +
                "keine weiteren Abfragen oder Dialoge parallel erscheinen. Nach Abschluss (Erfolg " +
                "oder Fehler) muss der 'Schließen'-Button aktiv geklickt werden, um fortzufahren.",
            [Str.Help_Item_MultipleSticks_Label] = "Mehrere USB-Sticks angeschlossen",
            [Str.Help_Item_MultipleSticks_Body] = "Sind zwei oder mehr USB-Sticks gleichzeitig angeschlossen — egal ob schon beim Programmstart oder erst später —, fragt ULM aktiv nach, mit welchem Stick gearbeitet werden soll (vorbelegt mit der zuletzt aktiven Auswahl). 'Abbrechen' behält einfach die bisherige Auswahl bei. Über das Laufwerks-Dropdown im Hauptfenster lässt sich jederzeit manuell zu einem anderen angeschlossenen Stick wechseln.",
            [Str.Help_Item_BootMenu_Label] = "Ventoy-Bootmenü",
            [Str.Help_Item_BootMenu_Body] = "Wird automatisch nach jedem Kopiervorgang UND nach jedem ISO-Import vom Stick aktualisiert. Enthält leserliche Namen, Beschreibungen und Kategorien aus der Datenbank.",
            [Str.Help_Item_CatchUpCopies_Label] = "🔁 Verpasste Kopien nachholen",
            [Str.Help_Item_CatchUpCopies_Body] = "Manuelles Sicherheitsnetz (Expert-Modus): kopiert bereits lokal vollständig heruntergeladene, ausgewählte ISOs (erneut) auf den Stick. ULM bietet das normalerweise automatisch an, sobald eine vollständige ISO auf dem Stick fehlt — dieses automatische 'Jetzt kopieren?'-Angebot erscheint aber pro Stick und Datei nur EINMAL je Sitzung, egal ob mit Ja oder Nein geantwortet wurde. Wurde es abgelehnt, oder ist eine vorherige Kopie fehlgeschlagen, ist dieser Button ohne Neustart der einzige Weg, es erneut zu versuchen.",

            [Str.Help_Sec_JunkProtection_Title] = "🧹 Datenmüll-Schutz — Online-Größenprüfung",
            [Str.Help_Sec_JunkProtection_Nav]   = "Datenmüll-Schutz",
            [Str.Help_JunkProtection_Intro]     = "Damit weder im Arbeitsordner noch auf dem Stick unbemerkt unvollständige oder beschädigte ISOs liegen bleiben, vergleicht ULM jede gefundene Datei mit der tatsächlichen Original-Größe beim Anbieter.",
            [Str.Help_Item_WhenChecked_Label] = "Wann wird geprüft?",
            [Str.Help_Item_WhenChecked_Body] = "Automatisch: im Arbeitsordner nach dem Start (Datei-Wartung) sowie auf dem Stick bei jedem Scan (Anstecken, Laufwerkswechsel, nach dem automatischen Versionscheck).",
            [Str.Help_Item_HowChecked_Label] = "Wie wird geprüft?",
            [Str.Help_Item_HowChecked_Body] = "ULM fragt per HEAD-Request die Original-Dateigröße ab (RemoteUrl → primäre URL → Mirror1-5 — die erste bekannte Antwort gewinnt) und vergleicht sie mit der gefundenen Dateigröße. Weicht sie um mehr als 2% ab, gilt die Datei als unvollständig. Ist online keine Größe ermittelbar, greift als Rückfallebene die 300-MB-Mindestgröße.",
            [Str.Help_Item_JunkInFolder_Label] = "Datenmüll im Arbeitsordner",
            [Str.Help_Item_JunkInFolder_Body] = "Wird als 'Unvollständig' bzw. 'Zu klein' protokolliert. Am Ende der Wartung erscheint ein Dialog mit allen betroffenen Dateien — gezielt auswählbar und bedenkenlos löschbar.",
            [Str.Help_Item_JunkOnStick_Label] = "Datenmüll auf dem Stick",
            [Str.Help_Item_JunkOnStick_Body] = "ISOs auf dem Stick, deren Größe nicht zur Online-Größe passt (z.B. durch einen abgebrochenen Kopiervorgang), zählen NICHT als vorhanden — kein fälschliches 'Ja' in der Spalte 'Auf dem Stick'. Ein Löschdialog wird automatisch angeboten.",

            [Str.Help_Sec_IsoImport_Title] = "📥 Unbekannte ISOs vom Stick importieren",
            [Str.Help_Sec_IsoImport_Nav]   = "ISO-Import",
            [Str.Help_IsoImport_Intro]     = "Findet ULM beim Stick-Scan ISO-Dateien, die noch nicht in der Datenbank stehen (z.B. manuell auf den Stick kopiert), erscheint ein Import-Dialog.",
            [Str.Help_Item_NameCategoryUrl_Label] = "Name, Kategorie, Quelle-URL",
            [Str.Help_Item_NameCategoryUrl_Body] = "Für jede unbekannte ISO Name und Kategorie vergeben. Optional: eine Quelle-URL hinterlegen. Sie ermöglicht später den Online-Update-Check auch für exotische Distros, deren Name keinem der bekannten Muster (Ubuntu, Debian, Mint, …) entspricht.",
            [Str.Help_Item_FolderStructure_Label] = "Ordnerstruktur bleibt sauber",
            [Str.Help_Item_FolderStructure_Body] = "Nach dem Import wird die Datei automatisch auf dem Stick in den passenden Kategorie-Ordner verschoben (z.B. '\\Sicherheit\\'), das Ventoy-Bootmenü wird aktualisiert und der Stick sofort neu gescannt.",
            [Str.Help_Item_DuplicateProtection_Label] = "Duplikat-Schutz",
            [Str.Help_Item_DuplicateProtection_Body] = "Erkennt ULM, dass eine 'unbekannte' ISO eigentlich einem bereits vorhandenen Datenbank-Eintrag entspricht (z.B. anderer Dateiname, andere Schreibweise derselben Distro), wird KEIN doppelter Eintrag angelegt. Stattdessen übernimmt der bestehende Eintrag einfach den neuen Dateinamen.",
            [Str.Help_Item_StayUpToDate_Label] = "Zukünftig aktuell halten",
            [Str.Help_Item_StayUpToDate_Body] =
                "Importierte Distros werden ab sofort wie reguläre Datenbank-Einträge behandelt: der " +
                "automatische Versionscheck beim Start prüft sie mit, und auch der manuelle " +
                "'Nach Updates suchen'-Button berücksichtigt sie jetzt — sobald sie lokal ODER auf dem " +
                "Stick vorhanden sind. Auch ohne hinterlegte URL versucht ULM automatisch, die richtige " +
                "Quelle zu finden — eine mehrstufige Kette, die für JEDE Distro gilt, nicht nur bekannte:\n" +
                "  1. Einer von >20 dedizierten Distro-Erkennern (unabhängig von Schreibweise/Sonderzeichen)\n" +
                "  2. Automatische Suche über DistroWatch.com — findet die offizielle Homepage der Distro " +
                "und darüber die Download-Seite, ganz ohne distro-spezifischen Code\n" +
                "  3. SourceForge-Projektsuche, falls die Distro dort gehostet wird\n" +
                "  4. Allgemeine Websuche als letzter Rückfall\n" +
                "Eine so gefundene Quelle wird dauerhaft in der Datenbank gespeichert — künftige " +
                "Prüfungen starten direkt darüber, statt jedes Mal neu zu suchen. Kurz aufeinanderfolgende " +
                "Erreichbarkeits-Checks werden zusätzlich einige Minuten zwischengespeichert, damit " +
                "wiederholte Anfragen an denselben Server nicht fälschlich als Bot-Verhalten eingestuft " +
                "und blockiert werden.\n" +
                "Hinweis: eine externe Bot-/Anti-Scraping-Erkennung (z.B. bei Suchanfragen oder auf " +
                "manchen Download-Servern) lässt sich nicht zu 100% ausschließen — in seltenen Fällen " +
                "kann ein Check trotz eigentlich erreichbarer Quelle vorübergehend fehlschlagen. Ein " +
                "erneuter Gesundheitscheck später behebt das in aller Regel.",

            [Str.Help_Sec_ExpertMode_Title] = "🛠 Expert-Modus — Zusatzfunktionen",
            [Str.Help_Sec_ExpertMode_Nav]   = "Expert-Modus",
            [Str.Help_ExpertMode_Intro]     = "Expert-Modus aktivieren: oben rechts 'Modus: Anwender' → klicken.",
            [Str.Help_Item_StatusTab_Label] = "📊 Status-Reiter",
            [Str.Help_Item_StatusTab_Body] =
                "Zeigt Transparenz über alles, was gerade oder demnächst automatisch im Hintergrund " +
                "läuft, ohne dass ein Blick in den Task-Manager nötig ist: den aktuell laufenden " +
                "manuellen Vorgang (Download, Kopieren, Integritätsprüfung, Ventoy, …) mit Datei, " +
                "Fortschritt und Zähler, die automatischen Hintergrund-Scans (Online-Versionscheck, " +
                "Stick-Prüfung), wann der nächste automatische Online-Versionscheck fällig ist, sowie " +
                "einen Verlauf der letzten Hintergrund-Ereignisse (mit 'Verlauf leeren'-Button).",
            [Str.Help_Item_UrlCheck_Label] = "URL-Check",
            [Str.Help_Item_UrlCheck_Body] = "Prüft ob alle konfigurierten URLs erreichbar sind (Primär-URL + Mirror1-5). Ergebnisse erscheinen als 🌐✓ / 🌐✗ im Distro-Namen.",
            [Str.Help_Item_EditDatabase_Label] = "Datenbank bearbeiten",
            [Str.Help_Item_EditDatabase_Body] = "Öffnet den DB-Editor zum Hinzufügen, Bearbeiten und Löschen von ISO-Einträgen. Felder: Name, Kategorie, URL, Mirror1-5, Filename, GitHub-Repo, Beschreibung.",
            [Str.Help_Item_DbHealthCheck_Label] = "🩺 DB-Gesundheitscheck",
            [Str.Help_Item_DbHealthCheck_Body] =
                "Löst für ALLE Datenbank-Einträge auf einmal die aktuelle Download-Quelle auf (auch " +
                "vom Stick importierte Distros, unabhängig davon ob lokal vorhanden) und zeigt einen " +
                "klaren Bericht: welche Distros gerade online erreichbar und ladbar sind — und welche " +
                "nicht. Kein Ersatz für den Versionscheck, sondern ein gezielter Diagnose-Werkzeug, um " +
                "defekte Einträge (abgelaufene URL, umgezogene Distro-Website) sofort zu erkennen, statt " +
                "sie erst beim nächsten Download-Versuch zu bemerken. Bei Ausfällen: im DB-Editor " +
                "zusätzliche Mirror-URLs oder ein GitHub-Repo hinterlegen.\n\n" +
                "Läuft automatisch — gezielt genau dann, wenn neue, noch unverifizierte Einträge in " +
                "die Datenbank kommen: nach Stick-Import, nach 'Hinzufügen' bei einer neueren Version " +
                "auf dem Stick, und nach manuellem 'Neu' im DB-Editor. NICHT bei jedem Stick-Scan, " +
                "Ventoy-Installation oder Kopiervorgang — das regelmäßige Prüfen bereits bekannter " +
                "Einträge übernimmt der Online-Versionscheck (Start + alle paar Tage). Eigene " +
                "Fortschrittsanzeige oben rechts, genauso wie beim Online-Scan (🩺 Gesundheitscheck). " +
                "Vor jedem Lauf werden doppelte Datenbank-Einträge automatisch erkannt und bereinigt.",
            [Str.Help_Item_GitHubToken_Label] = "🔑 GitHub-Token",
            [Str.Help_Item_GitHubToken_Body] = "Optional. GitHub-basierte Resolver (z.B. CachyOS, EndeavourOS) und der Ventoy-Update-Check nutzen ohne Token ein gemeinsames Limit von 60 Anfragen/Stunde für das ganze Netzwerk (nicht nur ULM) — bei intensiver Nutzung kann das knapp werden. Ein kostenloses GitHub Personal Access Token OHNE jeden Berechtigungs-Scope hebt das Limit auf 5000/Stunde an. Wird lokal in ulm_settings.ini gespeichert.",

            [Str.Help_Sec_Diagnostics_Title] = "🗒 Protokoll — Diagnose und Fehlersuche",
            [Str.Help_Sec_Diagnostics_Nav]   = "Diagnose",
            [Str.Help_Item_DownloadUrl_Label] = "Download-URL",
            [Str.Help_Item_DownloadUrl_Body] =
                "Beim Download wird die tatsächlich verwendete URL angezeigt:\n" +
                "  🔗 Distro-Name: https://…\n" +
                "Bei Fehlern kann so sofort die URL überprüft werden.",
            [Str.Help_Item_LogFile_Label] = "Protokoll-Datei",
            [Str.Help_Item_LogFile_Body] = "Alle Ereignisse werden dauerhaft im Arbeitsordner des Programms gespeichert (Datei 'ulm.log'). Nützlich für die Fehlersuche auf verschiedenen Systemen.",
            [Str.Help_Item_LogRotation_Label] = "Log-Rotation",
            [Str.Help_Item_LogRotation_Body] = "Überschreitet 'ulm_log.txt' 5 MB, wird sie automatisch einmal zu 'ulm_log.txt.old' verschoben und danach neu und leer begonnen — wächst also nicht mehr unbegrenzt bei Dauerbetrieb. Die vorherige Sicherung bleibt als '.old'-Datei erhalten.",
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

            [Str.Main_ScanHint_Online]           = "Online scan, please wait",
            [Str.Main_ScanHint_Usb]              = "Stick scan, please wait",

            [Str.Main_DriveInfo_NoVentoy]        = "⚠ No Ventoy",
            [Str.Main_DriveInfo_FreeLabel]       = "Free: ",

            [Str.Category_Gaming]                = "🎮 Gaming",
            [Str.Category_Security]              = "🔒 Security & Privacy",
            [Str.Category_Beginner]              = "💻 Beginner (Comfort & Design)",
            [Str.Category_Lightweight]           = "🪶 Lightweight (Speed & Efficiency)",
            [Str.Category_Advanced]              = "⚙ Advanced (Independence & Stability)",
            [Str.Category_Rescue]                = "🛠 Rescue (Backup & Recovery)",
            [Str.Category_Antivirus]             = "🛡 Antivirus (Protection & Cleanup)",
            [Str.Category_WinPE]                 = "🪟 WinPE (Windows Tools)",

            [Str.Help_Title]    = "❓ Universal Linux Manager — Help & Documentation",
            [Str.Help_Subtitle] = "Easily create and manage bootable USB sticks with Linux ISOs.",
            [Str.Help_NavHeading] = "QUICK LINKS",
            [Str.Help_Btn_Close]  = "✔ Close",

            [Str.Help_Sec_Overview_Title] = "🗺 Overview — What Does ULM Do?",
            [Str.Help_Sec_Overview_Nav]   = "Overview",
            [Str.Help_Overview_Body] =
                "ULM is a manager for Linux live ISOs and Ventoy USB sticks. It handles four tasks:\n" +
                "  1. ISO downloads — downloads current Linux versions directly from the official servers\n" +
                "  2. USB management — installs Ventoy on the stick and copies ISOs onto it\n" +
                "  3. Version monitoring — automatically checks whether newer ISO versions are available\n" +
                "  4. Junk file protection — detects incomplete/corrupted ISOs via an online size check, " +
                "both in the working folder and on the stick",

            [Str.Help_Sec_Startup_Title] = "🚀 What Happens at Program Start?",
            [Str.Help_Sec_Startup_Nav]   = "Startup",
            [Str.Help_Startup_Intro]     = "Right after startup, the following run automatically in the background, in this order:",
            [Str.Help_Item_OnlineCheck_Label] = "1. Online Version Check",
            [Str.Help_Item_OnlineCheck_Body] =
                "First queries the latest version for all distros in the database (approx. 5–30 sec.) — " +
                "including entries imported from the stick. Finds new versions automatically — no manual " +
                "URL entry needed. Updates the database entries when a new version is available. " +
                "A pulsing hint at the top of the header ('Online scan, please wait') shows that " +
                "the check is still running — it's best not to click anything until then, so the database and " +
                "stick state are complete.",
            [Str.Help_Item_UsbScan_Label] = "2. USB Stick Scan",
            [Str.Help_Item_UsbScan_Body] =
                "Runs only AFTER the version check (not at the same time), so the stick's state is compared " +
                "directly against the latest version data. Detects connected Ventoy sticks, shows which " +
                "ISOs are already on it, which are outdated, and which are missing. Runs again whenever a stick " +
                "is plugged in (the same pulsing hint, then 'Stick scan, please wait'). Each time, it also " +
                "checks the online size of every ISO found (see 🧹 Junk File Protection).",
            [Str.Help_Item_FileMaintenance_Label] = "File Maintenance",
            [Str.Help_Item_FileMaintenance_Body] =
                "Runs after the version check. Recursively scans the working folder and compares each ISO's size " +
                "with the actual original size at the provider (online HEAD request). This detects " +
                "incomplete and aborted downloads more reliably than a fixed minimum size. " +
                "Offers to delete any junk files found.",
            [Str.Help_Item_UpdateCheck_Label] = "ULM Update Check",
            [Str.Help_Item_UpdateCheck_Body] =
                "Checks in the background whether a newer ULM version is available on GitHub. Runs purely " +
                "for information — no dialog, no interruption. If a new version is available, " +
                "only one line appears in the log:\n" +
                "  🆕 New ULM version available: vX.Y.Z (currently installed: vA.B.C)\n" +
                "followed by a link to the release page.",
            [Str.Help_Item_WhatsNew_Label] = "\"What's New?\" Dialog",
            [Str.Help_Item_WhatsNew_Body] =
                "Appears automatically on the first start AFTER an update to a new ULM version " +
                "(not on the very first program start ever) and lists all changes since the last " +
                "version seen. Once dismissed, it only reappears at the next version change.",
            [Str.Help_Item_Autostart_Label] = "🚀 Autostart (Optional)",
            [Str.Help_Item_Autostart_Body] =
                "The 'Start with Windows' checkbox in the setup window — ULM then starts automatically " +
                "(visible window) at every Windows login. No admin rights needed, works via " +
                "a registry entry for the current user only. Can be deselected again at any time in the setup " +
                "window; if the window has been skipped via 'Don't show again', deleting the matching " +
                "entry in 'ulm_settings.ini' helps to see it again.",

            [Str.Help_Sec_Usage_Title] = "📋 The Distribution List — Usage",
            [Str.Help_Sec_Usage_Nav]   = "Usage",
            [Str.Help_Item_SelectDownload_Label] = "Select an ISO for Download",
            [Str.Help_Item_SelectDownload_Body] = "Enable the checkbox on the left → the ISO is queued for download (blue background). Selecting several ISOs at once is possible.",
            [Str.Help_Item_CategoryCheckbox_Label] = "Category Checkbox",
            [Str.Help_Item_CategoryCheckbox_Body] = "Enables or disables all distros in a category at once (e.g. selecting all 'Security' distros).",
            [Str.Help_Item_DoubleClick_Label] = "Double-Click an Entry",
            [Str.Help_Item_DoubleClick_Body] = "Shows the distribution's description — purpose, notable features, target audience.",
            [Str.Help_Item_MouseoverTooltip_Label] = "Mouseover (Tooltip)",
            [Str.Help_Item_MouseoverTooltip_Body] = "Hovering the mouse over the distro name shows a tooltip. It explains all visible symbols (📥, 🌐✓/✗, 🆕) AND shows the distro's description.",

            [Str.Help_Sec_Colors_Title] = "🎨 Colors & Symbols in the Main Window",
            [Str.Help_Sec_Colors_Nav]   = "Colors & Symbols",
            [Str.Help_Subhead_TextColors] = "Text Colors of the List Entries",
            [Str.Help_Color_Green_Label]  = "Green",
            [Str.Help_Color_Green_Body]   = "The ISO is present on the USB stick (latest version, size-verified online) — or fully downloaded locally and ready to copy.",
            [Str.Help_Color_Orange_Label] = "Orange",
            [Str.Help_Color_Orange_Body]  = "Update available — a newer version was found online. Or: an outdated version is on the stick (a newer version exists).",
            [Str.Help_Color_Red_Label]    = "Red",
            [Str.Help_Color_Red_Body]     = "URL not reachable — the download server is not responding. Appears after a URL check (expert mode).",
            [Str.Help_Color_Teal_Label]   = "Teal",
            [Str.Help_Color_Teal_Body]    = "Imported from the USB stick — this entry was discovered during the stick scan and added as a new entry.",
            [Str.Help_Color_Blue_Label]   = "Muted Blue",
            [Str.Help_Color_Blue_Body]    = "The online check confirms this version is current. No update needed, the ISO is up to date.",
            [Str.Help_Color_Gray_Label]   = "Light Gray",
            [Str.Help_Color_Gray_Body]    = "No URL configured — no download URLs are set for this entry.",
            [Str.Help_Color_Dark_Label]   = "Dark (Default)",
            [Str.Help_Color_Dark_Body]    = "Normal state — no online version check performed yet, ISO not local and not on the stick.",
            [Str.Help_Subhead_Columns] = "Columns in the List",
            [Str.Help_Item_ColLocal_Label] = "Local",
            [Str.Help_Item_ColLocal_Body] =
                "Shows whether the ISO is present in the local working folder:\n" +
                "  'Local 3,565 MB' = downloaded (with file size)\n" +
                "  'not local'      = not downloaded yet",
            [Str.Help_Item_ColOnStick_Label] = "On Stick",
            [Str.Help_Item_ColOnStick_Body] =
                "Shows the status on the detected Ventoy stick:\n" +
                "  'Yes 3.56 GB'   = present, current version, online size confirmed\n" +
                "  'Outdated …'    = on the stick, but an outdated version\n" +
                "  'No'            = ISO is missing from the stick OR was detected as incomplete and removed\n" +
                "  'Unverified'    = the stick has not been scanned yet",
            [Str.Help_Item_ColCurrent_Label] = "Current",
            [Str.Help_Item_ColCurrent_Body] =
                "Shows the result of the online version check:\n" +
                "  'Update vX.Y.Z'      = a newer version is available online\n" +
                "  'Current (vX.Y.Z)'   = online check: already the latest version\n" +
                "  'Available locally'  = present locally, no online check\n" +
                "  '?'                  = not checked yet",
            [Str.Help_Subhead_HashSymbol] = "Hash Status Symbol (Narrow Column Left of the Name)",
            [Str.Help_HashSymbol_Body] =
                "A small, custom-drawn smiley shows the integrity status of the locally " +
                "stored SHA-256 checksum (see 🔒 Verify Integrity further below):\n" +
                "  Green = a reference hash is present (calculated locally or officially verified)\n" +
                "  Red   = a mismatch was found during the last integrity check — the file is " +
                "probably corrupted or replaced\n" +
                "  No symbol = no hash exists yet (ISO never downloaded/imported) — " +
                "deliberately neutral, not red, so untouched ISOs don't look like a problem\n" +
                "Hovering over the symbol shows the exact reason.",
            [Str.Help_Subhead_NameSymbols] = "Symbols in the Distro Name (Mouseover Shows Explanation)",
            [Str.Help_Item_SymbolImported_Label] = "📥 (Prefix)",
            [Str.Help_Item_SymbolImported_Body] = "Imported from the USB stick — this ISO was discovered during the stick scan and added as a new entry (not from the standard database).",
            [Str.Help_Item_SymbolUrlOk_Label] = "🌐✓ (Suffix)",
            [Str.Help_Item_SymbolUrlOk_Body] = "URL check passed — the download URL is reachable. Mouseover shows: 'URL reachable — download server responding'.",
            [Str.Help_Item_SymbolUrlFail_Label] = "🌐✗ (Suffix)",
            [Str.Help_Item_SymbolUrlFail_Body] = "URL check failed — the download URL is not reachable. Mouseover shows: 'URL not reachable — download server not responding'.",
            [Str.Help_Item_SymbolNewVersion_Label] = "🆕 vX.Y.Z (Suffix)",
            [Str.Help_Item_SymbolNewVersion_Body] = "A newer version (shown here as an example: vX.Y.Z) was found online. Mouseover shows: 'New version available: vX.Y.Z (download now)'. Select the entry and start the download.",
            [Str.Help_Subhead_CategorySymbols] = "Category Symbols (Left Column)",
            [Str.Help_CategorySymbols_Body] =
                "  🖥 Beginner          — User-friendly distributions for getting started with desktop Linux\n" +
                "  ⚙ Advanced          — More configuration freedom, Arch-based systems\n" +
                "  🪶 Lightweight       — Resource-friendly, for older and weaker hardware\n" +
                "  🎮 Gaming            — Optimized for gaming (ProtonGE, Steam, MangoHud)\n" +
                "  🔒 Security          — Privacy, anonymity, pen-testing (Tails, Parrot, Kodachi)\n" +
                "  🛠 Rescue            — Rescue and repair live systems (GParted, Clonezilla)\n" +
                "  🛡 Antivirus         — Live systems for virus scanning and removal\n" +
                "  🪟 WinPE             — Windows-based rescue environments (Hiren's BootCD)",

            [Str.Help_Sec_Theme_Title] = "🌓 Theme — Light / Dark / System",
            [Str.Help_Sec_Theme_Nav]   = "Theme",
            [Str.Help_Theme_Intro]     = "ULM has a light and a dark appearance. Both are fully styled (lists, dialogs, input fields) and checked for good readability.",
            [Str.Help_Item_ThemeSetting_Label] = "Setting It",
            [Str.Help_Item_ThemeSetting_Body] = "Selectable during initial setup in the setup dialog, or at any time via the '🌓 Theme: …' button in the top right of the main window (next to 'Mode: User/Expert'). A click cycles through System → Light → Dark.",
            [Str.Help_Item_ThemeSystem_Label] = "System",
            [Str.Help_Item_ThemeSystem_Body] = "Automatically follows the current Windows theme setting (light or dark). If the Windows theme changes while ULM is running, ULM follows automatically — no restart needed.",
            [Str.Help_Item_ThemeInstant_Label] = "Instant Switching",
            [Str.Help_Item_ThemeInstant_Body] = "A switch takes effect immediately across the entire open main window — including the row colors in the distro list. No restart needed. Newly opened dialogs (Help, Database, Setup, …) automatically adopt the choice.",
            [Str.Help_Item_ThemeRemembers_Label] = "Remembers the Choice",
            [Str.Help_Item_ThemeRemembers_Body] = "The chosen setting is saved and automatically applied again the next time the program starts.",

            [Str.Help_Sec_LogSymbols_Title] = "📜 Log Symbols — Meaning",
            [Str.Help_Sec_LogSymbols_Nav]   = "Log Symbols",
            [Str.Help_LogSymbols_Body] =
                "  ▶   Program start / section start\n" +
                "  💾  Database action or stick scan\n" +
                "  🔌  Drive detected / stick plugged in\n" +
                "  🌐  Online version check running\n" +
                "  ⬇   Download started or in progress\n" +
                "  🔗  Download URL (shows which server is used)\n" +
                "  ✅  Action completed successfully\n" +
                "  ❌  Error occurred\n" +
                "  ⚠   Warning (not an error, but needs attention) — e.g. incomplete files\n" +
                "  🆕  New version found online\n" +
                "  ✓   Version is current (no update needed)\n" +
                "  ✏   Display name updated automatically\n" +
                "  ↔   Filename replaced in the database\n" +
                "  🗑  Entry or file deleted (also: junk file removed from the stick)\n" +
                "  🔄  Duplicate merged\n" +
                "  📋  Copy operation to the USB stick\n" +
                "  📂  File moved into the category folder on the stick during import\n" +
                "  ❓  Unknown ISO(s) found on the stick — import possible\n" +
                "  ⛔  Operation canceled",

            [Str.Help_Sec_IsoSearch_Title] = "🔍 Search ISO — Discover New Distros",
            [Str.Help_Sec_IsoSearch_Nav]   = "Search ISO",
            [Str.Help_IsoSearch_Intro]     = "The '🔍 Search ISO' button shows two online lists from DistroWatch.com — a way to specifically discover new distros instead of only browsing the fixed standard database. The already-known database is still available via '🗃 Database'.",
            [Str.Help_Item_Newest_Label] = "🆕 Newest",
            [Str.Help_Item_Newest_Body] = "The distributions most recently added to DistroWatch (top 10).",
            [Str.Help_Item_Popular_Label] = "🔥 Most Popular",
            [Str.Help_Item_Popular_Body] = "DistroWatch's page-hit ranking (top 10) — the currently most-visited distro profiles.",
            [Str.Help_Item_LiveOnly_Label] = "Live Medium Only",
            [Str.Help_Item_LiveOnly_Body] = "Both lists show EXCLUSIVELY distros with the DistroWatch category tag 'Live Medium' — pure installation or server images without a live-boot mode are automatically filtered out. Every suggestion is therefore guaranteed to be bootable from a USB stick.",
            [Str.Help_Item_AlreadyInDb_Label] = "Already Present",
            [Str.Help_Item_AlreadyInDb_Body] = "Distros already in your own database are highlighted in blue and cannot be adopted again. For new distros, a mouseover tooltip shows rank/date, a suggested category, and the DistroWatch link.",
            [Str.Help_Item_AdoptAndDownload_Label] = "Adopt + Download Directly",
            [Str.Help_Item_AdoptAndDownload_Body] = "Add selected distros to the database via '✔ Adopt' (category adjustable beforehand via dropdown). If 'Download directly' is also checked, the regular download process for these entries starts immediately after the window closes.",
            [Str.Help_Item_RefreshCache_Label] = "Refresh / Cache",
            [Str.Help_Item_RefreshCache_Body] = "Both lists are cached locally for 24 hours (no network round-trip every time you open them). The '⟳ Refresh' button forces a fresh query.",

            [Str.Help_Sec_Download_Title] = "⬇ Download — How and Where?",
            [Str.Help_Sec_Download_Nav]   = "Download",
            [Str.Help_Item_StorageLocation_Label] = "Storage Location",
            [Str.Help_Item_StorageLocation_Body] = "All ISOs are stored in the program's working folder (subfolder 'ISOs'). Since ULM is portable, the working folder sits next to the program file — the exact path depends on where ULM was saved.",
            [Str.Help_Item_PipelineMode_Label] = "Pipeline Mode",
            [Str.Help_Item_PipelineMode_Body] =
                "When a Ventoy stick is detected, each ISO can be copied to the stick " +
                "right after downloading. The local file is then deleted. " +
                "Downloads and copying run in parallel. In the progress dialog, an ISO's row " +
                "switches from 'Copying to stick' to 'Done' once it has been fully copied. " +
                "The overall progress bar (amber → blue → green depending on percentage) as well as the " +
                "completion message reflect the actual copy success — if the " +
                "stick copy fails, this is now clearly reported as a failure instead of incorrectly " +
                "showing 'successfully downloaded and copied' just because the download worked.",
            [Str.Help_Item_MirrorRace_Label] = "Mirror Race (Up to 8 Sources)",
            [Str.Help_Item_MirrorRace_Body] =
                "Before the actual download begins, ULM tests all configured mirror URLs " +
                "of a distro in parallel for about 3 seconds and then starts with the fastest source " +
                "— not simply the first one. Measurement is done in short time windows instead of a " +
                "single average, so CDNs that only ramp up to full " +
                "speed after one or two seconds aren't incorrectly classified as slow. For " +
                "SourceForge sources, ULM additionally automatically fans out across several geographically " +
                "distributed mirrors instead of relying on SourceForge's own (often suboptimal) server " +
                "selection. The result appears in the log:\n" +
                "  🔎 Distro: mirror test — cdn1.example.org 42.3 Mbit/s, …",
            [Str.Help_Item_SpeedGuard_Label] = "Speed Guard",
            [Str.Help_Item_SpeedGuard_Body] =
                "If a running transfer (after about 20 seconds of ramp-up time) stays " +
                "continuously below about 1 MB/s for another 20 seconds, ULM automatically aborts it and tries " +
                "the next mirror source — instead of waiting for hours on an extremely slow connection. " +
                "If there is no faster source left and all attempts only failed due to " +
                "speed (not a real error), ULM actively asks:\n" +
                "  ⚠ No faster mirror found — continue with this source anyway?\n" +
                "Confirming this lets the download finish without further speed checks.",
            [Str.Help_Item_FasterButton_Label] = "\"(faster)\" Button",
            [Str.Help_Item_FasterButton_Body] =
                "Appears in the download progress window next to a running transfer — but " +
                "only after about 20 seconds of ramp-up time AND only while the actually measured " +
                "speed is noticeably mediocre (below about 3 MB/s), even if the server " +
                "is still above the speed-guard threshold at that point (i.e. wouldn't automatically " +
                "abort anyway). At already good speeds the button stays hidden — there's " +
                "then nothing a switch would achieve. It also always requires " +
                "at least one other candidate already measured by the mirror race. Clicking aborts the " +
                "current attempt and immediately switches to the next candidate. If " +
                "no faster server is found, ULM automatically returns — without asking — to the original, " +
                "provenly reachable server ('No faster server found — download " +
                "continues').",
            [Str.Help_Item_EtaRemaining_Label] = "Time Remaining (ETA)",
            [Str.Help_Item_EtaRemaining_Body] =
                "Besides speed and size, the progress dialog also shows the estimated " +
                "remaining time, e.g.:\n  12.4 MB/s  ·  2m 14s left  ·  1.2 GB / 3.5 GB\n" +
                "The estimate continuously adjusts to the current download speed.",
            [Str.Help_Item_VerifyIntegrity_Label] = "🔒 Verify Integrity",
            [Str.Help_Item_VerifyIntegrity_Body] = "After every download or when importing from the stick, ULM stores a SHA-256 reference hash of the ISO file. For Ubuntu, Debian, and Fedora, ULM automatically checks the official checksum from the provider. The '🔒 Verify Integrity' button lets you verify the file on the stick against the reference hash at any time — it warns if the copy was corrupted or incomplete.",
            [Str.Help_Item_FreeSpaceCheck_Label] = "Free Space Check",
            [Str.Help_Item_FreeSpaceCheck_Body] =
                "Two-stage: BEFORE the download even starts, ULM sums up the online-" +
                "determinable size of ALL selected distros and compares it with the free " +
                "space in the working folder AND — if copying directly to a stick — " +
                "additionally with the free space there. If there isn't enough room, ULM warns BEFORE " +
                "starting with a yes/no prompt, instead of failing midway across several parallel " +
                "downloads at once. Additionally, a second, fine-grained check " +
                "re-checks the space still available immediately before each individual file:\n" +
                "  ❌ Not enough disk space on X:\\ (needs 3.5 GB, 1.1 GB free).",

            [Str.Help_Sec_UsbStick_Title] = "💾 USB Stick Management (Ventoy)",
            [Str.Help_Sec_UsbStick_Nav]   = "USB Stick / Ventoy",
            [Str.Help_Item_WhatIsVentoy_Label] = "What Is Ventoy?",
            [Str.Help_Item_WhatIsVentoy_Body] = "Ventoy sets up a USB stick so that multiple Linux ISOs can be stored on it at the same time and selected at boot. Set it up once, then simply copy ISOs onto it — no re-flashing needed.",
            [Str.Help_Item_InstallUpdateVentoy_Label] = "Install / Update Ventoy",
            [Str.Help_Item_InstallUpdateVentoy_Body] =
                "Only visible in expert mode. " +
                "⚠ A NEW INSTALLATION erases ALL data on the stick! " +
                "Updating keeps existing ISOs. Runs as administrator (UAC) in its " +
                "own ULM window with progress display and log — Ventoy2Disk.exe itself " +
                "runs completely invisibly in the background (official silent/CLI mode, " +
                "no separate Ventoy interface, no manual operation needed). While the " +
                "installation runs, ULM pauses automatic drive detection — no " +
                "other prompts or dialogs can appear in parallel. After completion (success " +
                "or failure), the 'Close' button must be actively clicked to continue.",
            [Str.Help_Item_MultipleSticks_Label] = "Multiple USB Sticks Connected",
            [Str.Help_Item_MultipleSticks_Body] = "If two or more USB sticks are connected at the same time — whether already at program start or only later —, ULM actively asks which stick to work with (pre-selected with the most recently active choice). 'Cancel' simply keeps the previous selection. The drive dropdown in the main window lets you manually switch to another connected stick at any time.",
            [Str.Help_Item_BootMenu_Label] = "Ventoy Boot Menu",
            [Str.Help_Item_BootMenu_Body] = "Is updated automatically after every copy operation AND after every ISO import from the stick. Contains readable names, descriptions, and categories from the database.",
            [Str.Help_Item_CatchUpCopies_Label] = "🔁 Catch Up Missed Copies",
            [Str.Help_Item_CatchUpCopies_Body] = "Manual safety net (expert mode): (re-)copies already fully locally downloaded, selected ISOs to the stick. ULM normally offers this automatically as soon as a complete ISO is missing from the stick — but this automatic 'Copy now?' offer only appears ONCE per stick and file per session, regardless of whether it was answered Yes or No. If it was declined, or a previous copy failed, this button is the only way to try again without restarting.",

            [Str.Help_Sec_JunkProtection_Title] = "🧹 Junk File Protection — Online Size Check",
            [Str.Help_Sec_JunkProtection_Nav]   = "Junk File Protection",
            [Str.Help_JunkProtection_Intro]     = "So that neither the working folder nor the stick ends up with unnoticed incomplete or corrupted ISOs, ULM compares every file found with the actual original size at the provider.",
            [Str.Help_Item_WhenChecked_Label] = "When Is It Checked?",
            [Str.Help_Item_WhenChecked_Body] = "Automatically: in the working folder after startup (file maintenance), and on the stick with every scan (plugging in, drive change, after the automatic version check).",
            [Str.Help_Item_HowChecked_Label] = "How Is It Checked?",
            [Str.Help_Item_HowChecked_Body] = "ULM queries the original file size via a HEAD request (RemoteUrl → primary URL → Mirror1-5 — the first known response wins) and compares it with the found file size. If it differs by more than 2%, the file is considered incomplete. If no size can be determined online, the 300 MB minimum size serves as a fallback.",
            [Str.Help_Item_JunkInFolder_Label] = "Junk in the Working Folder",
            [Str.Help_Item_JunkInFolder_Body] = "Logged as 'Incomplete' or 'Too small'. At the end of maintenance, a dialog appears with all affected files — individually selectable and safe to delete.",
            [Str.Help_Item_JunkOnStick_Label] = "Junk on the Stick",
            [Str.Help_Item_JunkOnStick_Body] = "ISOs on the stick whose size doesn't match the online size (e.g. due to an aborted copy operation) do NOT count as present — no incorrect 'Yes' in the 'On Stick' column. A deletion dialog is offered automatically.",

            [Str.Help_Sec_IsoImport_Title] = "📥 Import Unknown ISOs from the Stick",
            [Str.Help_Sec_IsoImport_Nav]   = "ISO Import",
            [Str.Help_IsoImport_Intro]     = "If ULM finds ISO files during the stick scan that aren't yet in the database (e.g. manually copied onto the stick), an import dialog appears.",
            [Str.Help_Item_NameCategoryUrl_Label] = "Name, Category, Source URL",
            [Str.Help_Item_NameCategoryUrl_Body] = "Assign a name and category for each unknown ISO. Optional: enter a source URL. It later enables the online update check even for exotic distros whose name doesn't match any of the known patterns (Ubuntu, Debian, Mint, …).",
            [Str.Help_Item_FolderStructure_Label] = "Folder Structure Stays Clean",
            [Str.Help_Item_FolderStructure_Body] = "After the import, the file is automatically moved on the stick into the matching category folder (e.g. '\\Security\\'), the Ventoy boot menu is updated, and the stick is rescanned immediately.",
            [Str.Help_Item_DuplicateProtection_Label] = "Duplicate Protection",
            [Str.Help_Item_DuplicateProtection_Body] = "If ULM detects that an 'unknown' ISO actually corresponds to an already-existing database entry (e.g. a different filename, a different spelling of the same distro), NO duplicate entry is created. Instead, the existing entry simply adopts the new filename.",
            [Str.Help_Item_StayUpToDate_Label] = "Staying Up to Date Going Forward",
            [Str.Help_Item_StayUpToDate_Body] =
                "From now on, imported distros are treated like regular database entries: the " +
                "automatic version check at startup checks them too, and the manual " +
                "'Check for Updates' button now considers them as well — as soon as they exist locally OR on the " +
                "stick. Even without a stored URL, ULM automatically tries to find the correct " +
                "source — a multi-stage chain that applies to EVERY distro, not just known ones:\n" +
                "  1. One of >20 dedicated distro resolvers (independent of spelling/special characters)\n" +
                "  2. Automatic search via DistroWatch.com — finds the distro's official homepage " +
                "and, from there, the download page, with no distro-specific code at all\n" +
                "  3. SourceForge project search, if the distro is hosted there\n" +
                "  4. General web search as a last resort\n" +
                "A source found this way is saved permanently in the database — future " +
                "checks start directly from it instead of searching again each time. Reachability checks " +
                "occurring shortly after one another are also cached for a few minutes, so " +
                "repeated requests to the same server aren't mistakenly classified as bot behavior " +
                "and blocked.\n" +
                "Note: external bot/anti-scraping detection (e.g. on search queries or on " +
                "some download servers) cannot be ruled out 100% — in rare cases " +
                "a check may temporarily fail despite the source actually being reachable. A " +
                "repeated health check later usually resolves this.",

            [Str.Help_Sec_ExpertMode_Title] = "🛠 Expert Mode — Additional Features",
            [Str.Help_Sec_ExpertMode_Nav]   = "Expert Mode",
            [Str.Help_ExpertMode_Intro]     = "Enable expert mode: click 'Mode: User' in the top right.",
            [Str.Help_Item_StatusTab_Label] = "📊 Status Tab",
            [Str.Help_Item_StatusTab_Body] =
                "Provides transparency about everything currently or soon running automatically in the " +
                "background, without needing to check Task Manager: the currently running " +
                "manual operation (download, copying, integrity check, Ventoy, …) with file, " +
                "progress, and counter, the automatic background scans (online version check, " +
                "stick check), when the next automatic online version check is due, as well as " +
                "a history of the latest background events (with a 'Clear History' button).",
            [Str.Help_Item_UrlCheck_Label] = "URL Check",
            [Str.Help_Item_UrlCheck_Body] = "Checks whether all configured URLs are reachable (primary URL + Mirror1-5). Results appear as 🌐✓ / 🌐✗ in the distro name.",
            [Str.Help_Item_EditDatabase_Label] = "Edit Database",
            [Str.Help_Item_EditDatabase_Body] = "Opens the DB editor for adding, editing, and deleting ISO entries. Fields: name, category, URL, Mirror1-5, filename, GitHub repo, description.",
            [Str.Help_Item_DbHealthCheck_Label] = "🩺 DB Health Check",
            [Str.Help_Item_DbHealthCheck_Body] =
                "Resolves the current download source for ALL database entries at once (including " +
                "distros imported from the stick, regardless of whether they exist locally) and shows a " +
                "clear report: which distros are currently reachable and downloadable online — and which " +
                "aren't. Not a replacement for the version check, but a targeted diagnostic tool to " +
                "immediately spot broken entries (expired URL, distro website moved), instead of only " +
                "noticing them on the next download attempt. In case of failures: add " +
                "additional mirror URLs or a GitHub repo in the DB editor.\n\n" +
                "Runs automatically — specifically whenever new, not-yet-verified entries enter " +
                "the database: after a stick import, after 'Add' for a newer version " +
                "on the stick, and after manually clicking 'New' in the DB editor. NOT on every stick scan, " +
                "Ventoy installation, or copy operation — regularly checking already-known " +
                "entries is handled by the online version check (start + every few days). Has its own " +
                "progress indicator in the top right, just like the online scan (🩺 Health Check). " +
                "Before every run, duplicate database entries are automatically detected and cleaned up.",
            [Str.Help_Item_GitHubToken_Label] = "🔑 GitHub Token",
            [Str.Help_Item_GitHubToken_Body] = "Optional. GitHub-based resolvers (e.g. CachyOS, EndeavourOS) and the Ventoy update check share a limit of 60 requests/hour for the whole network (not just ULM) without a token — this can get tight under heavy use. A free GitHub Personal Access Token WITHOUT any permission scope raises the limit to 5000/hour. Stored locally in ulm_settings.ini.",

            [Str.Help_Sec_Diagnostics_Title] = "🗒 Log — Diagnostics and Troubleshooting",
            [Str.Help_Sec_Diagnostics_Nav]   = "Diagnostics",
            [Str.Help_Item_DownloadUrl_Label] = "Download URL",
            [Str.Help_Item_DownloadUrl_Body] =
                "When downloading, the actually used URL is shown:\n" +
                "  🔗 Distro name: https://…\n" +
                "This way, the URL can be checked immediately in case of errors.",
            [Str.Help_Item_LogFile_Label] = "Log File",
            [Str.Help_Item_LogFile_Body] = "All events are permanently stored in the program's working folder (file 'ulm.log'). Useful for troubleshooting on different systems.",
            [Str.Help_Item_LogRotation_Label] = "Log Rotation",
            [Str.Help_Item_LogRotation_Body] = "If 'ulm_log.txt' exceeds 5 MB, it is automatically moved once to 'ulm_log.txt.old' and then started fresh and empty — so it no longer grows unbounded during continuous operation. The previous backup is kept as a '.old' file.",
        };
    }
}
