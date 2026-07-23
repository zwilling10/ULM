namespace ULM.Infrastructure
{
    // Ein Eintrag pro übersetzbarem Text im Programm. Phase 1 deckt den
    // Hauptfenster-Rahmen ab, Phase 2 zusätzlich SetupDialog (siehe
    // docs/superpowers/specs/2026-07-22-bilingual-ui-infrastructure-design.md,
    // docs/superpowers/specs/2026-07-23-setupdialog-localization-design.md) —
    // weitere Phasen erweitern dieses enum um die restlichen Dialoge und den
    // Log-/Aktivitätsverlauf.
    public enum Str
    {
        Tab_IsoSelection,
        Tab_Log,
        Tab_Status,
        Btn_Download,
        Btn_CheckForUpdates,
        Btn_Cancel,
        Btn_Help,
        Btn_Settings,
        LanguageChangeConfirm_Title,
        LanguageChangeConfirm_Message,

        // SetupDialog: Kopfzeile
        Setup_Title_Welcome,
        Setup_Title_Settings,
        Setup_Header_Welcome,
        Setup_Header_Settings,
        Setup_Subtitle_Welcome,
        Setup_Subtitle_Settings,

        // SetupDialog: Arbeitsordner-Karte
        Setup_Directory_Header,
        Setup_Btn_Browse,
        Setup_FolderDialog_Title,
        Setup_Btn_UseDefaultPath,
        Setup_Directory_ItemsIntro,
        Setup_Directory_ItemDownloads,
        Setup_Directory_ItemDatabase,
        Setup_Directory_ItemLog,

        // SetupDialog: Über-ULM-Karte (nur Erststart)
        Setup_Card_AboutUlm,
        Setup_WelcomeBody,

        // SetupDialog: Modus-Karte
        Setup_Card_Mode,
        Setup_Chk_ExpertMode,
        Setup_Hint_Mode,

        // SetupDialog: Autostart-Karte
        Setup_Card_Autostart,
        Setup_Chk_Autostart,
        Setup_Hint_Autostart,

        // SetupDialog: Design-Karte
        Setup_Card_Design,
        Setup_Theme_System,
        Setup_Theme_Light,
        Setup_Theme_Dark,
        Setup_Hint_Theme,

        // SetupDialog: Sprache-Karte (Buttons selbst bleiben hartcodiert)
        Setup_Card_Language,
        Setup_Hint_Language,

        // SetupDialog: Fußzeile
        Setup_Chk_DontShowAgain,
        Setup_Btn_Apply,

        // SetupDialog: Fehler
        Setup_Error_Title,
        Setup_Error_FolderCreateFailed,
    }
}
