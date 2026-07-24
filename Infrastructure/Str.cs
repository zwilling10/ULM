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
        Setup_Card_Directory,
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

        // Hauptfenster: Rahmen, Toolbar, Spalten, Status-Tab
        Main_HeaderSubtitle,
        Main_Chip_OnlineScan,
        Main_Chip_UsbScan,
        Main_Chip_HealthCheck,
        Main_Btn_Dismiss,
        Main_Label_TargetDrive,
        Main_Tooltip_RefreshDrives,
        Main_Btn_InstallVentoy,
        Main_Chk_SecureBoot,
        Main_Tooltip_HashStatusColumn,
        Main_ColumnHeader_Distribution,
        Main_ColumnHeader_Local,
        Main_ColumnHeader_OnStick,
        Main_ColumnHeader_Current,
        Main_Btn_ClearLog,
        Main_Status_CurrentOperation,
        Main_Status_NoOperation,
        Main_Status_OnlineScanRunning,
        Main_Status_UsbScanRunning,
        Main_Status_LabelOperation,
        Main_Status_LabelFile,
        Main_Status_LabelProgress,
        Main_Status_LabelDetail,
        Main_Status_LabelCounter,
        Main_Status_LabelTargetDrive,
        Main_Status_SectionBackgroundScans,
        Main_Status_LabelOnlineCheck,
        Main_Status_Running,
        Main_Status_Inactive,
        Main_Status_LabelLastChecked,
        Main_Status_LabelCompletedPrefix,
        Main_Status_LabelUsbCheck,
        Main_Status_LabelDrive,
        Main_Status_SectionScheduled,
        Main_Status_LabelNextCheck,
        Main_Status_DriveMonitoring,
        Main_Status_SectionHistory,
        Main_Btn_ClearHistory,
        Main_Btn_CheckUrls,
        Main_Btn_SearchIso,
        Main_Btn_EditDb,
        Main_Btn_HealthCheck,
        Main_Tooltip_HealthCheck,
        Main_Btn_CopyUsb,
        Main_Tooltip_CopyUsb,
        Main_Btn_VerifyIntegrity,
        Main_Tooltip_VerifyIntegrity,
        Main_Btn_GitHubToken,
        Main_Tooltip_GitHubToken,
        Main_Chk_ShowInfo,

        // Hauptfenster Code-Behind: MessageBox-/Dialog-Texte
        Msg_SlowDownload_Body,
        Msg_SlowDownload_Title,
        Msg_OrphanedIncomplete_Title,
        Msg_OrphanedIncomplete_Description,
        Msg_OperationComplete_Title,
        Main_Footer_IsoFolder,
        Msg_UpdateDownloadFailed,
        Msg_StickOutdatedFound,
        Msg_UpdateNow,
        Msg_StickUpdate_Title,
        Msg_OutdatedDuplicates_Title,
        Msg_OutdatedDuplicates_Description,
        Msg_LocalNotOnStick,
        Msg_CopyNow,
        Msg_LocalNotOnStick_Title,
        Msg_DeleteLocalAfterCopy_Immediate,
        Msg_DeleteLocalAfterCopy_AfterCopy,
        Msg_DeleteFiles_Title,
        Msg_NoUsbDetected,
        Msg_SelectAtLeastOne,
        Msg_DownloadMode_Body,
        Msg_DownloadMode_Title,
        Msg_NoVentoy_Body,
        Msg_NoVentoy_Title,
        Msg_NoStick_Body,
        Msg_NoStick_Title,
        Msg_FreeSpace_LabelWorkDir,
        Msg_FreeSpace_LabelStick,
        Msg_FreeSpace_Body1,
        Msg_FreeSpace_Body2,
        Msg_FreeSpace_Body3,
        Msg_FreeSpace_Title,
        Msg_PhaseCopyToStick,
        Msg_SelectDriveFirst,
        Msg_PleaseWait,
        Msg_NoLocalIsos,
        Msg_NewDriveDetected_Body,
        Msg_NewDriveDetected_Title,
        Msg_NoLabel,
        Msg_MultipleDrivesHeader,
        Msg_VentoyUpdate_Body,
        Msg_VentoyInstall_Body,
        Msg_VentoyUpdate_Title,
        Msg_VentoyInstall_Title,

        // Update-/Härtefall-Banner (MainViewModel.cs)
        Banner_UpdateAvailable,
        Banner_UpdateDownloading,
        Banner_UpdateReady,
        Banner_UpdateBtn_Available,
        Banner_UpdateBtn_Downloading,
        Banner_UpdateBtn_ReadyToInstall,
        Banner_HardCaseSingle,
        Banner_HardCasePlural,

        // Distro-Zeilen: Status-Texte + Tooltips (IsoViewModels.cs)
        Row_ManualSearchTooltip,
        Row_CategorySelectAllTooltip,
        Row_Local,
        Row_NotLocal,
        Row_Yes,
        Row_No,
        Row_Outdated,
        Row_Unverified,
        Row_UpdatePrefix,
        Row_CurrentPrefix,
        Row_LocallyAvailable,
        Row_HashMismatch,
        Row_HashVerifiedOfficial,
        Row_HashLocalOnly,
        Row_TipImported,
        Row_TipUrlOk,
        Row_TipUrlFail,
        Row_TipNewVersion,

        // Startphasen-Hinweis (MainViewModel.ScanHintText) — beim urspruenglichen Inventar
        // fuer Phase 3 uebersehen, beim manuellen Testen nachtraeglich gefunden
        Main_ScanHint_Online,
        Main_ScanHint_Usb,

        // Laufwerk-Info-Text (MainViewModel.DriveInfoText) — ebenfalls beim urspruenglichen
        // Inventar uebersehen, beim finalen Whole-Branch-Review nachtraeglich gefunden
        Main_DriveInfo_NoVentoy,
        Main_DriveInfo_FreeLabel,

        // Kategorie-Namen (Constants.cs)
        Category_Gaming,
        Category_Security,
        Category_Beginner,
        Category_Lightweight,
        Category_Advanced,
        Category_Rescue,
        Category_Antivirus,
        Category_WinPE,
    }
}
