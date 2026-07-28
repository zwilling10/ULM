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

        // ── HelpDialog: Chrome ───────────────────────────────────────────────
        Help_Title,
        Help_Subtitle,
        Help_NavHeading,
        Help_Btn_Close,

        // ── HelpDialog: Abschnitt 1 — Übersicht ────────────────────────────
        Help_Sec_Overview_Title, Help_Sec_Overview_Nav,
        Help_Overview_Body,

        // ── HelpDialog: Abschnitt 2 — Programmstart ────────────────────────
        Help_Sec_Startup_Title, Help_Sec_Startup_Nav,
        Help_Startup_Intro,
        Help_Item_OnlineCheck_Label, Help_Item_OnlineCheck_Body,
        Help_Item_UsbScan_Label, Help_Item_UsbScan_Body,
        Help_Item_FileMaintenance_Label, Help_Item_FileMaintenance_Body,
        Help_Item_UpdateCheck_Label, Help_Item_UpdateCheck_Body,
        Help_Item_WhatsNew_Label, Help_Item_WhatsNew_Body,
        Help_Item_Autostart_Label, Help_Item_Autostart_Body,

        // ── HelpDialog: Abschnitt 3 — Bedienung ────────────────────────────
        Help_Sec_Usage_Title, Help_Sec_Usage_Nav,
        Help_Item_SelectDownload_Label, Help_Item_SelectDownload_Body,
        Help_Item_CategoryCheckbox_Label, Help_Item_CategoryCheckbox_Body,
        Help_Item_DoubleClick_Label, Help_Item_DoubleClick_Body,
        Help_Item_MouseoverTooltip_Label, Help_Item_MouseoverTooltip_Body,

        // ── HelpDialog: Abschnitt 4 — Farben & Symbole ─────────────────────
        Help_Sec_Colors_Title, Help_Sec_Colors_Nav,
        Help_Subhead_TextColors,
        Help_Color_Green_Label, Help_Color_Green_Body,
        Help_Color_Orange_Label, Help_Color_Orange_Body,
        Help_Color_Red_Label, Help_Color_Red_Body,
        Help_Color_Teal_Label, Help_Color_Teal_Body,
        Help_Color_Blue_Label, Help_Color_Blue_Body,
        Help_Color_Gray_Label, Help_Color_Gray_Body,
        Help_Color_Dark_Label, Help_Color_Dark_Body,
        Help_Subhead_Columns,
        Help_Item_ColLocal_Label, Help_Item_ColLocal_Body,
        Help_Item_ColOnStick_Label, Help_Item_ColOnStick_Body,
        Help_Item_ColCurrent_Label, Help_Item_ColCurrent_Body,
        Help_Subhead_HashSymbol,
        Help_HashSymbol_Body,
        Help_Subhead_NameSymbols,
        Help_Item_SymbolImported_Label, Help_Item_SymbolImported_Body,
        Help_Item_SymbolUrlOk_Label, Help_Item_SymbolUrlOk_Body,
        Help_Item_SymbolUrlFail_Label, Help_Item_SymbolUrlFail_Body,
        Help_Item_SymbolNewVersion_Label, Help_Item_SymbolNewVersion_Body,
        Help_Subhead_CategorySymbols,
        Help_CategorySymbols_Body,

        // ── HelpDialog: Abschnitt 5 — Design ───────────────────────────────
        Help_Sec_Theme_Title, Help_Sec_Theme_Nav,
        Help_Theme_Intro,
        Help_Item_ThemeSetting_Label, Help_Item_ThemeSetting_Body,
        Help_Item_ThemeSystem_Label, Help_Item_ThemeSystem_Body,
        Help_Item_ThemeInstant_Label, Help_Item_ThemeInstant_Body,
        Help_Item_ThemeRemembers_Label, Help_Item_ThemeRemembers_Body,

        // ── HelpDialog: Abschnitt 6 — Protokoll-Symbole ────────────────────
        Help_Sec_LogSymbols_Title, Help_Sec_LogSymbols_Nav,
        Help_LogSymbols_Body,

        // ── HelpDialog: Abschnitt 7 — ISO suchen ───────────────────────────
        Help_Sec_IsoSearch_Title, Help_Sec_IsoSearch_Nav,
        Help_IsoSearch_Intro,
        Help_Item_Newest_Label, Help_Item_Newest_Body,
        Help_Item_Popular_Label, Help_Item_Popular_Body,
        Help_Item_LiveOnly_Label, Help_Item_LiveOnly_Body,
        Help_Item_AlreadyInDb_Label, Help_Item_AlreadyInDb_Body,
        Help_Item_AdoptAndDownload_Label, Help_Item_AdoptAndDownload_Body,
        Help_Item_RefreshCache_Label, Help_Item_RefreshCache_Body,

        // ── HelpDialog: Abschnitt 8 — Download ─────────────────────────────
        Help_Sec_Download_Title, Help_Sec_Download_Nav,
        Help_Item_StorageLocation_Label, Help_Item_StorageLocation_Body,
        Help_Item_PipelineMode_Label, Help_Item_PipelineMode_Body,
        Help_Item_MirrorRace_Label, Help_Item_MirrorRace_Body,
        Help_Item_SpeedGuard_Label, Help_Item_SpeedGuard_Body,
        Help_Item_FasterButton_Label, Help_Item_FasterButton_Body,
        Help_Item_EtaRemaining_Label, Help_Item_EtaRemaining_Body,
        Help_Item_VerifyIntegrity_Label, Help_Item_VerifyIntegrity_Body,
        Help_Item_FreeSpaceCheck_Label, Help_Item_FreeSpaceCheck_Body,

        // ── HelpDialog: Abschnitt 9 — USB-Stick / Ventoy ───────────────────
        Help_Sec_UsbStick_Title, Help_Sec_UsbStick_Nav,
        Help_Item_WhatIsVentoy_Label, Help_Item_WhatIsVentoy_Body,
        Help_Item_InstallUpdateVentoy_Label, Help_Item_InstallUpdateVentoy_Body,
        Help_Item_MultipleSticks_Label, Help_Item_MultipleSticks_Body,
        Help_Item_BootMenu_Label, Help_Item_BootMenu_Body,
        Help_Item_CatchUpCopies_Label, Help_Item_CatchUpCopies_Body,

        // ── HelpDialog: Abschnitt 10 — Datenmüll-Schutz ────────────────────
        Help_Sec_JunkProtection_Title, Help_Sec_JunkProtection_Nav,
        Help_JunkProtection_Intro,
        Help_Item_WhenChecked_Label, Help_Item_WhenChecked_Body,
        Help_Item_HowChecked_Label, Help_Item_HowChecked_Body,
        Help_Item_JunkInFolder_Label, Help_Item_JunkInFolder_Body,
        Help_Item_JunkOnStick_Label, Help_Item_JunkOnStick_Body,

        // ── HelpDialog: Abschnitt 11 — ISO-Import ──────────────────────────
        Help_Sec_IsoImport_Title, Help_Sec_IsoImport_Nav,
        Help_IsoImport_Intro,
        Help_Item_NameCategoryUrl_Label, Help_Item_NameCategoryUrl_Body,
        Help_Item_FolderStructure_Label, Help_Item_FolderStructure_Body,
        Help_Item_DuplicateProtection_Label, Help_Item_DuplicateProtection_Body,
        Help_Item_StayUpToDate_Label, Help_Item_StayUpToDate_Body,

        // ── HelpDialog: Abschnitt 12 — Expert-Modus ────────────────────────
        Help_Sec_ExpertMode_Title, Help_Sec_ExpertMode_Nav,
        Help_ExpertMode_Intro,
        Help_Item_StatusTab_Label, Help_Item_StatusTab_Body,
        Help_Item_UrlCheck_Label, Help_Item_UrlCheck_Body,
        Help_Item_EditDatabase_Label, Help_Item_EditDatabase_Body,
        Help_Item_DbHealthCheck_Label, Help_Item_DbHealthCheck_Body,
        Help_Item_GitHubToken_Label, Help_Item_GitHubToken_Body,

        // ── HelpDialog: Abschnitt 13 — Diagnose ────────────────────────────
        Help_Sec_Diagnostics_Title, Help_Sec_Diagnostics_Nav,
        Help_Item_DownloadUrl_Label, Help_Item_DownloadUrl_Body,
        Help_Item_LogFile_Label, Help_Item_LogFile_Body,
        Help_Item_LogRotation_Label, Help_Item_LogRotation_Body,

        // ── Log-Meldungen: Startup + DB-Wartung ─────────────────────────────
        Log_AppStarted, Log_IsoFolderPath, Log_DatabasePath, Log_DbEntriesLoaded,
        Log_DbEntryRemoved, Log_ExactDuplicateRemoved, Log_Merged, Log_DuplicateRemoved,
        Log_FilenameAdopted, Log_FilenameNotAdopted, Log_EntryAdded, Log_NameUpdated,
        Log_FilenameReplaced, Log_EntryAddedSimple,

        // ── Log-Meldungen: USB-Stick-Scan ───────────────────────────────────
        Log_DrivesDetected, Log_ScanningStick, Log_StickScanStarted, Log_StickScanSummary,
        Log_StickScanFound, Log_StickIsoListItem, Log_StickIncompleteFound, Log_StickJunkSuspected,
        Log_StickHashMismatchFound, Log_StickHashMismatchItem, Log_StickOutdatedCount,
        Log_StickOutdatedItem, Log_StickDuplicatesFound, Log_StickDuplicateItem, Log_StickAllCurrent,

        // ── Log-Meldungen: Integritaetspruefung + Ventoy-Bootmenue + Versionscheck ──
        Log_CheckingIntegrity, Log_IntegrityCheckStarted, Log_CheckedOfTotal, Log_CancellingStatus,
        Log_IntegrityCheckCancelled, Log_HashMismatchesStatus, Log_IsosVerifiedStatus,
        Log_IntegrityCheckDone, Log_IntegrityCheckFailed, Log_ErrorStatus, Log_VentoyMenuUpdating,
        Log_VentoyMenuUpdated, Log_VersionCheckStarted, Log_VersionCheckRunningStatus,
        Log_EntryUnreachable, Log_UpdateFound, Log_VersionCurrent, Log_UpdatesAppliedStatus,
        Log_AllCurrentStatus, Log_UnreachableStatus, Log_VersionCheckSummary,
        Log_DbNewVersionsSaved, Log_DbNewSourcesSaved, Log_CheckingStick, Log_StickCheckDone,

        // ── Log-Meldungen: Download + Kopieren ──────────────────────────────
        Log_DownloadStarted, Log_ToDriveSuffix, Log_QueueItem, Log_MovedToCopyQueue, Log_DownloadsDone,
        Log_PipelineCopyRunningStatus, Log_ZeroDownloadsStatus, Log_DownloadsDonePipelineContinues,
        Log_StickCopyCancelled, Log_DownloadedAndCopiedStatus, Log_SomeFailedStatus,
        Log_NoDownloadsStatus, Log_DownloadedCountStatus, Log_FailedSuffix, Log_SourceFileNotFound,
        Log_FileTooSmall, Log_NotEnoughSpace, Log_CopyingToStick, Log_SizeCheckFailedRemoved,
        Log_CopyError, Log_CopyDoneItem, Log_LocallyDeletedSuffix, Log_CopyStarted,
        Log_DeleteAfterSuffix, Log_CopyQueueItem, Log_CopyCancelled, Log_CopyDone,
        Log_CopiedToStickStatus, Log_NothingToCopyStatus, Log_Deleted, Log_LocalFilesDeleted,

        // ── Log-Meldungen: Update-Check/URL-Check/DB-Health/Ventoy-Install/Abbruch ──
        Log_CheckingForUpdates, Log_ManualUpdateCheckStarted, Log_ManualCheckUnreachable,
        Log_ManualCheckCurrent, Log_UpdatesFoundStatus, Log_AllCurrentSimpleStatus,
        Log_NoLocalIsosStatus, Log_ManualUpdateCheckSummary, Log_CheckingUrls, Log_UrlCheckStarted,
        Log_UrlCheckItem, Log_UrlCheckSummaryStatus, Log_UrlCheckLogSummary, Log_DbHealthCheckStarted,
        Log_DbHealthAllReachableStatus, Log_DbHealthSomeUnreachableStatus, Log_DbHealthLogSummary,
        Log_VentoyActionStarted, Log_VentoyActionWordUpdate, Log_VentoyActionWordInstall,
        Log_StartingAsAdmin, Log_WaitingForUac, Log_ExePathNotFound, Log_AdminProcessFailed,
        Log_AdminProcessRunning, Log_VentoyInstallRunning, Log_VentoyUpdatedStatus,
        Log_VentoyInstalledStatus, Log_VentoyFailedStatus, Log_VentoyExitCode, Log_UacDenied,
        Log_UacAbortedStatus, Log_GenericError, Log_CancelRequested, Log_AppClosing,

        // ── Log-Meldungen: Import + Datenmuell-Erkennung (Stick) ──
        Log_NewerVersionsFound, Log_NoChangeSimple, Log_SkippedItem, Log_ReplacedCount,
        Log_AddedCount, Log_DbUpdateSummary, Log_UnknownIsosFound, Log_ImportSkipped,
        Log_FileMovedToCategory, Log_IsosAddedToDb, Log_MoveFailedSuffix, Log_IncompleteIsosFound,
        Log_FilesDeletedStatus, Log_StickMaintenanceSkipped,

        // ── Log-Meldungen: Programmstart/Update-Check/Datei-Wartung ──
        Log_AutoVersionCheckStarting, Log_NewUlmVersionAvailable, Log_ReleaseUrlLine,
        Log_AutoUpdateDownloadFailed, Log_AutoUpdateDownloadFailedFallback, Log_UpdateDownloaded,
        Log_ReleasePageOpenFailed, Log_DownloadingProgramUpdate, Log_UpdateDownloadFailed,
        Log_UpdateSaved, Log_FolderOpenFailed, Log_BackgroundCheckDue, Log_WorkFolderNotFound,
        Log_ScanningIsoFolder, Log_ScanError, Log_IsoFilesFoundCount, Log_FileEmpty,
        Log_FileOrphaned, Log_FileIncomplete, Log_FileComplete, Log_FileTooSmallUnverified,
        Log_FileOkUnverified, Log_FileCancelledPartial, Log_PartSearchError, Log_NoJunkFound,
        Log_JunkFilesClassified, Log_FilesDeletedSimpleStatus, Log_MaintenanceSkipped,
        Log_FileMaintenanceError,

        // ── Log-Meldungen: Ventoy/DB-Suche/Abbruch (Rest MainWindow.xaml.cs) + IsoEntry ──
        Log_StaleDuplicatesDeletedStatus, Log_DuplicateCleanupSkipped, Log_UsbDrivesDetected,
        Log_VentoyInstallStartingOnDrive, Log_VentoyActionStartedEllipsis, Log_CheckingRequiredSpace,
        Log_SourceManuallyAddedDeferred, Log_SourceManuallyAddedRetrying, Log_IsosAddedFromOnlineSearch,
        Log_GitHubTokenRemoved, Log_GitHubTokenSaved, Log_CancellingOperation, Log_DeleteFailed,
        Log_OperationSucceededLogPrefix,

        // ── Log-Meldungen: Download-Worker (Core/Workers/Workers.cs) ──
        Log_NoDownloadUrlFound, Log_Unreachable, Log_MirrorTest, Log_TryingUrl,
        Log_AndTryNextSource, Log_PermanentlySlow, Log_UserRequestsFasterMirror, Log_MirrorFailed,
        Log_TryNextMirrorSuffix, Log_NoFasterMirrorFound, Log_UltimatelyFailed,
        Log_ProceedingDespiteSlowness, Log_UserDeclinedSlowDownload, Log_ChecksumVerified,
        Log_ChecksumMismatchWarning, Log_DownloadCompletedItem, Log_DownloadFailedItem,
        Log_GeneralDownloadError,

        // ── Views/Dialogs/DatabaseDialogs.cs ──────────────────────────────
        Db_ListDialog_Title, Db_Btn_New, Db_Btn_Edit, Db_Btn_Delete, Db_Btn_MoveUp, Db_Btn_MoveDown,
        Db_Btn_Close, Db_NoFilenameTooltip, Db_DeleteEntryConfirm_Body, Db_DeleteEntryConfirm_Title,

        Db_EditDialog_Title_New, Db_EditDialog_Title_Edit, Db_Field_Name, Db_Field_Category,
        Db_Field_PrimaryUrl, Db_Field_Filename, Db_Field_Mirror1, Db_Field_Mirror2, Db_Field_Mirror3,
        Db_Field_GithubRepo, Db_Field_GithubAsset, Db_Field_Description, Db_Btn_Save, Db_Btn_Cancel,
        Db_RequiredFields_Body, Db_RequiredFields_Title, Db_NameTaken_Body, Db_NameTaken_Title,

        Db_SearchDialog_Title, Db_Tab_Latest, Db_Tab_Popular, Db_Chk_DownloadImmediately,
        Db_Btn_CloseSimple, Db_Btn_Refresh, Db_Btn_TakeOver, Db_Loading, Db_NoDiscoveryResults,
        Db_FromCache, Db_FreshlyLoaded, Db_DiscoveryStatusSuffix, Db_NameAlreadyInDb,
        Db_SuggestedCategory, Db_DistrowatchTags, Db_TakenOverStatus, Db_DiscoveryError,

        Db_ImportDialog_Title, Db_ImportDialog_Info, Db_ColHeader_NameEdit, Db_ColHeader_Category,
        Db_FileSizeLine, Db_SourceUrlLabel, Db_Btn_SelectAll, Db_Btn_DeselectAll, Db_Btn_Skip,
        Db_Btn_Import, Db_ImportError_NoName, Db_ImportIncomplete_Body, Db_ImportIncomplete_Title,

        Db_NewerVersionDialog_Title, Db_NewerVersionDialog_Info, Db_CategoryNameHeader,
        Db_Label_Database, Db_Label_OnStick, Db_FilenameVersionLine, Db_Radio_Replace, Db_Radio_Add,
        Db_Radio_Skip, Db_Btn_ReplaceAll, Db_Tooltip_ReplaceAll, Db_Btn_SkipAll, Db_Btn_ApplySelection,

        Db_HealthCheckDialog_Title, Db_HealthCheck_AllReachable, Db_HealthCheck_SomeUnreachable,
        Db_HealthCheck_Tip,

        Db_GitHubTokenDialog_Title, Db_GitHubTokenDialog_Description,

        QuickConfirm_Title,

        // ── Views/VentoyInstallWindow.cs + VentoyInstallWorker (Core/Workers/Workers.cs) ──
        VentoyWin_TitleUpdating, VentoyWin_TitleInstalling, VentoyWin_Btn_CloseError,
        VentoyWin_Btn_CloseSuccess, VentoyWin_Log_Drive, VentoyWin_Log_Mode, VentoyWin_ModeUpdate,
        VentoyWin_ModeFresh, VentoyWin_Log_SecureBoot, VentoyWin_SuccessUpdated,
        VentoyWin_SuccessInstalled, VentoyWin_Log_Done, VentoyWin_Log_PleaseCloseToContinue,
        VentoyWin_Failed, VentoyWin_Log_CheckProtocol, VentoyWin_Log_CloseButtonExitCodeInfo,
        VentoyWin_Log_FetchingLatest, VentoyWin_Log_UrlFetchFailed, VentoyWin_Log_Downloading,
        VentoyWin_Log_DownloadProgress, VentoyWin_Log_DownloadFailed, VentoyWin_Log_Extracting,
        VentoyWin_Log_ExeNotFound, VentoyWin_Log_StartingExe, VentoyWin_Log_ExeStartFailed,
        VentoyWin_Log_Timeout, VentoyWin_Log_ResultFailed, VentoyWin_Log_CliDoneSuffix,
        VentoyWin_Log_GeneralError,

        // ── Views/Dialogs/DownloadDialogs.cs ──────────────────────────────
        Dl_SlotsDialog_Title, Dl_SlotsSelectedCount, Dl_TestingSpeed, Dl_Label_ParallelDownloads,
        Dl_Chk_UseRecommendation, Dl_Btn_Cancel, Dl_Btn_StartDownloads, Dl_SpeedTestResult,
        Dl_SpeedTestFailed,

        Dl_ProgressDialog_Title, Dl_DownloadRunningSingle, Dl_DownloadRunningMultiple,
        Dl_OverallStatus, Dl_Btn_CancelX, Dl_Btn_ManualSearch, Dl_Waiting, Dl_Btn_Faster,
        Dl_Tooltip_Faster, Dl_CopyingToStickSuffix, Dl_SummaryPartial,

        Dl_OrphanedDefaultTitle, Dl_OrphanedDefaultItemLabel, Dl_OrphanedFoundText,
        Dl_Btn_DeleteSelected,

        Dl_DriveSelectDialog_Title, Dl_DefaultHeaderText, Dl_NoName, Dl_DriveComboItem,
        Dl_VentoyPresentTag, Dl_VentoyDriveHint, Dl_Btn_Select,

        // ── Core/Workers/Workers.cs: TransferFormat + DownloadWorker.sa.Status ──
        Xfer_DetailWithEta,
        DlStatus_NoUrlFound, DlStatus_TestingMirrors, DlStatus_LabelPrimary, DlStatus_LabelMirrorN,
        DlStatus_LabelEllipsis, DlStatus_LabelDetail, DlStatus_NoFasterServerContinuing,
        DlStatus_LabelResumed, DlStatus_LabelSlow, DlStatus_Done, DlStatus_FailedAllMirrorsTried,
        DlStatus_GeneralError,

        // ── ViewModels/MainViewModel.cs: OperationSucceeded-Nachrichtentexte (MessageBox-Body) ──
        OpSucceeded_PipelineBody, OpSucceeded_FailedSuffix, OpSucceeded_DownloadOnlyBody,
        OpSucceeded_FailedSuffixSimple, OpSucceeded_CopyOnlyBody, OpSucceeded_LocalFilesDeletedSuffix,

        // ── Core/Services/HttpService.cs: Download-Fortschritt-ETA-Suffix ──
        Xfer_EtaSuffix,

        // ── Core/Workers/Workers.cs: DownloadWorker.OverallProgress (Hauptfenster Statuszeile) ──
        Dl_OverallProgressDetail,

        // ── Core/Workers/Workers.cs: CopyToUsbWorker (Hauptfenster Statuszeile + Fortschrittsdialog) ──
        CpStatus_NoFiles, CpStatus_NotEnoughSpace, CpStatus_Starting, CpStatus_OverallProgress,
        CpStatus_Cancelled, CpStatus_Done,

        // ── ViewModels/MainViewModel.cs: QuickCheckSucceeded-Nachrichtentexte (QuickConfirmationWindow) ──
        QuickConfirm_IntegrityCheckDone, QuickConfirm_UpdateCheckDone,
    }
}
