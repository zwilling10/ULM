# Hauptfenster lokalisieren (Zweisprachigkeit Phase 3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Jeder sichtbare Text im Hauptfenster (`Views/MainWindow.xaml` + Code-Behind, `ViewModels/MainViewModel.cs` Banner, `ViewModels/IsoViewModels.cs` Zeilen-Status, `Core/Models/Constants.cs` Kategorien) läuft über `LocalizationService.T(Str...)`, außer dem explizit ausgeschlossenen Log-/Aktivitätsverlauf und `StatusText` (Phase 4).

**Architektur:** 128 neue `Str`-Enum-Werte. Reiner C#-Code (`MainWindow.xaml.cs`, `MainViewModel.cs`, `IsoViewModels.cs`, `Constants.cs`) ersetzt Literale direkt durch `LocalizationService.T(Str.X)` bzw. `string.Format(LocalizationService.T(Str.X), ...)` bei mehreren eingebetteten Werten. `MainWindow.xaml` (deklaratives Markup) braucht drei verschiedene Muster: (a) neues `x:Name` + Zuweisung in der bestehenden `ApplyLocalizedText()`-Methode für statische Texte, (b) neue berechnete Properties auf `MainViewModel` für die 3 zustandsabhängigen `DataTrigger`/`Setter`-Texte (ersetzt durch `{Binding}`), (c) neue berechnete Properties auf `IsoEntryViewModel`/`IsoCategoryViewModel` für die 2 Zeilen-/Kategorie-Vorlagen-Tooltips (ersetzt durch `{Binding}`).

**Tech Stack:** C# / .NET 8 (WPF), xUnit, keine neuen NuGet-Pakete.

## Global Constraints

- Log-/Aktivitätsverlauf (`MainViewModel.Log(...)`, `AppendLog(...)` in `MainWindow.xaml.cs`) und `StatusText` bleiben komplett unangetastet (Phase 4) — inklusive `MainWindow.xaml.cs:644` (`StatusLbl.Text = "✅ Ventoy-Stick: {letter}"`), das denselben Status-Bereich setzt.
- `IsoViewModels.cs`-Property `StatusBracket` (tot, nirgends gebunden) bleibt unangetastet — kein Aufräumen toten Codes im Rahmen dieser Phase.
- Kategorie-Namen werden übersetzt (bereits abgestimmt).
- Duplikate bekommen JE EINEN `Str`-Wert, an mehreren Call-Sites wiederverwendet: `Main_Btn_Dismiss` (2×), `Msg_DeleteFiles_Title` (3×), `Msg_SelectDriveFirst` (2×), `Msg_DeleteLocalAfterCopy_AfterCopy` (2×), `Main_Status_Running`/`Main_Status_Inactive` (je 2×), `Row_Yes` (2×).
- Texte mit mehreren, grammatikalisch eingebetteten Laufzeitwerten nutzen `{0}`/`{1}`-Platzhalter + `string.Format(LocalizationService.T(Str.X), ...)` (Standard-.NET-Mechanismus, keine Änderung an `LocalizationService.T()` selbst). Texte mit einem einzelnen angehängten Wert nutzen weiterhin einfache Verkettung/Interpolation.
- `x:Name`-Namenskonvention für neu benannte XAML-Elemente: `Txt`-Präfix für `TextBlock`, `Run`-Präfix für `Run`, `Btn`-Präfix für `Button` (passend zum bestehenden Stil `BtnDownload`, `DriveInfoTxt`).
- Kein Unit-Test-Harness für WPF-Fenster in diesem Projekt — Verifikation über Build-Erfolg + volle Testsuite + manuelle Verifikation in Task 12.

---

### Task 1: `Str.cs` — 128 neue Enum-Werte

**Files:**
- Modify: `Infrastructure/Str.cs`

**Interfaces:**
- Produziert: 128 neue `Str`-Enum-Werte, exakte Liste unten — werden von Task 2 (Dictionaries) und Task 3–11 (Verwendung) konsumiert.

- [ ] **Step 1: Enum-Werte ergänzen**

In `Infrastructure/Str.cs` den bestehenden Block

```csharp
        // SetupDialog: Fehler
        Setup_Error_Title,
        Setup_Error_FolderCreateFailed,
    }
}
```

ersetzen durch:

```csharp
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
```

- [ ] **Step 2: Build prüfen**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: `Build succeeded.` (Enum-Werte noch nirgends verwendet, unschädlich).

- [ ] **Step 3: Commit**

```bash
git add Infrastructure/Str.cs
git commit -m "feat: Str-Enum um 128 Hauptfenster-Eintraege erweitert"
```

---

### Task 2: `LocalizationService.cs` — Übersetzungen für die 128 neuen Einträge

**Files:**
- Modify: `Infrastructure/LocalizationService.cs`
- Test: `ULM.Tests/LocalizationServiceTests.cs`

**Interfaces:**
- Konsumiert: die 128 `Str`-Werte aus Task 1.
- Produziert: `LocalizationService.T(Str.X)` liefert für alle 128 neuen Werte in beiden Sprachen einen nicht-leeren String — wird von Task 3–11 konsumiert. Werte mit `{0}`/`{1}`-Platzhaltern werden per `string.Format(LocalizationService.T(Str.X), ...)` von den Aufrufern verwendet (nicht Teil dieses Tasks, siehe spätere Tasks).

- [ ] **Step 1: Neue Einträge im `De`-Dictionary ergänzen**

Die letzte Zeile vor der schließenden `};` des `De`-Dictionary

```csharp
            [Str.Setup_Error_Title]              = "Fehler",
            [Str.Setup_Error_FolderCreateFailed] = "Ordner konnte nicht erstellt werden:",
        };
```

ersetzen durch:

```csharp
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
```

- [ ] **Step 2: Neue Einträge im `En`-Dictionary ergänzen**

Die letzte Zeile vor der schließenden `};` des `En`-Dictionary

```csharp
            [Str.Setup_Error_Title]              = "Error",
            [Str.Setup_Error_FolderCreateFailed] = "Could not create folder:",
        };
```

ersetzen durch:

```csharp
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
```

- [ ] **Step 3: Spot-Tests für `string.Format`-Fälle ergänzen**

In `ULM.Tests/LocalizationServiceTests.cs` in der Klasse `LocalizationServiceTTests` nach der letzten bestehenden Theory-Methode (vor der schließenden `}` der Klasse) einfügen:

```csharp

    [Theory]
    [InlineData(AppLanguage.German, "Auf {0} wurden {1} veraltete ISO(s) gefunden:")]
    [InlineData(AppLanguage.English, "{1} outdated ISO(s) found on {0}:")]
    public void T_Msg_StickOutdatedFound_ReturnsCorrectFormatStringForLanguage(AppLanguage language, string expected)
    {
        Assert.Equal(expected, LocalizationService.T(Str.Msg_StickOutdatedFound, language));
    }

    [Fact]
    public void Msg_StickOutdatedFound_FormatsCorrectlyInGerman()
    {
        string result = string.Format(LocalizationService.T(Str.Msg_StickOutdatedFound, AppLanguage.German), "E:", 3);
        Assert.Equal("Auf E: wurden 3 veraltete ISO(s) gefunden:", result);
    }

    [Fact]
    public void Msg_StickOutdatedFound_FormatsCorrectlyInEnglish()
    {
        string result = string.Format(LocalizationService.T(Str.Msg_StickOutdatedFound, AppLanguage.English), "E:", 3);
        Assert.Equal("3 outdated ISO(s) found on E::", result);
    }

    [Theory]
    [InlineData(AppLanguage.German, "Update")]
    [InlineData(AppLanguage.English, "Update")]
    public void T_Row_UpdatePrefix_ReturnsCorrectTextForLanguage(AppLanguage language, string expected)
    {
        Assert.Equal(expected, LocalizationService.T(Str.Row_UpdatePrefix, language));
    }

    [Theory]
    [InlineData(AppLanguage.German, "🎮 Gaming")]
    [InlineData(AppLanguage.English, "🎮 Gaming")]
    public void T_Category_Gaming_ReturnsCorrectTextForLanguage(AppLanguage language, string expected)
    {
        Assert.Equal(expected, LocalizationService.T(Str.Category_Gaming, language));
    }
```

- [ ] **Step 4: Tests laufen lassen**

Run: `dotnet test ULM.Tests`
Expected: alle Tests grün, inklusive der 5 neuen Tests und des unveränderten `LocalizationServiceCompletenessTests.AllStrValues_HaveGermanAndEnglishTranslation` (deckt jetzt 172 Werte statt 44 ab — 128 neue + die 44 aus Phase 1/2).

- [ ] **Step 5: Commit**

```bash
git add Infrastructure/LocalizationService.cs ULM.Tests/LocalizationServiceTests.cs
git commit -m "feat: Uebersetzungen fuer 128 Hauptfenster-Str-Eintraege ergaenzt"
```

---

### Task 3: `MainWindow.xaml` — Header, Toolbar, Spaltenüberschriften

**Files:**
- Modify: `Views/MainWindow.xaml`
- Modify: `Views/MainWindow.xaml.cs` (nur `ApplyLocalizedText()`)

**Interfaces:**
- Konsumiert: `LocalizationService.T(Str.Main_...)` aus Task 1/2.
- Produziert: 10 neue `x:Name`-Attribute (`TxtHeaderSubtitle`, `TxtChipOnlineScan`, `TxtChipUsbScan`, `TxtChipHealthCheck`, `TxtTargetDrive`, `TxtHashStatusIcon`, `TxtColHeaderDistribution`, `TxtColHeaderLocal`, `TxtColHeaderOnStick`, `TxtColHeaderCurrent`, `BtnClearLog`) — werden nur von diesem Task konsumiert (jeweils direkt im selben Schritt in `ApplyLocalizedText()` verdrahtet).

- [ ] **Step 1: Header-Untertitel**

```xml
                        <TextBlock Text="Universal Linux Manager"
                                   Foreground="White" FontSize="16" FontWeight="Bold"
                                   FontFamily="{DynamicResource FontMain}"/>
                        <TextBlock Text="USB-Stick einrichten · Linux ISOs verwalten · Downloads überwachen"
                                   Foreground="{DynamicResource BrushDim}" FontSize="10"
                                   FontFamily="{DynamicResource FontMain}"/>
```

ersetzen durch:

```xml
                        <TextBlock Text="Universal Linux Manager"
                                   Foreground="White" FontSize="16" FontWeight="Bold"
                                   FontFamily="{DynamicResource FontMain}"/>
                        <TextBlock x:Name="TxtHeaderSubtitle"
                                   Foreground="{DynamicResource BrushDim}" FontSize="10"
                                   FontFamily="{DynamicResource FontMain}"/>
```

- [ ] **Step 2: Scan-Chip-Labels (Online-Scan / Stick-Scan / Gesundheitscheck)**

```xml
                                <TextBlock DockPanel.Dock="Left" Text="🌐 Online-Scan"
                                           Foreground="{DynamicResource BrushDim}"
                                           FontSize="9" FontWeight="SemiBold"/>
```

ersetzen durch:

```xml
                                <TextBlock x:Name="TxtChipOnlineScan" DockPanel.Dock="Left"
                                           Foreground="{DynamicResource BrushDim}"
                                           FontSize="9" FontWeight="SemiBold"/>
```

```xml
                            <TextBlock Text="💾 Stick-Scan"
                                       Foreground="{DynamicResource BrushDim}"
                                       FontSize="9" FontWeight="SemiBold"/>
```

ersetzen durch:

```xml
                            <TextBlock x:Name="TxtChipUsbScan"
                                       Foreground="{DynamicResource BrushDim}"
                                       FontSize="9" FontWeight="SemiBold"/>
```

```xml
                                <TextBlock DockPanel.Dock="Left" Text="🩺 Gesundheitscheck"
                                           Foreground="{DynamicResource BrushDim}"
                                           FontSize="9" FontWeight="SemiBold"/>
```

ersetzen durch:

```xml
                                <TextBlock x:Name="TxtChipHealthCheck" DockPanel.Dock="Left"
                                           Foreground="{DynamicResource BrushDim}"
                                           FontSize="9" FontWeight="SemiBold"/>
```

- [ ] **Step 3: Toolbar — Ziel-Laufwerk-Label, Laufwerke-neu-einlesen-Tooltip, Ventoy-Button, Secure-Boot-Checkbox**

```xml
                <TextBlock Grid.Column="0" Text="ZIEL-USB-LAUFWERK"
                           FontSize="9" FontWeight="Bold" Foreground="{DynamicResource BrushDim}"
                           VerticalAlignment="Center" Margin="0,0,10,0"/>

                <ComboBox Grid.Column="1" x:Name="DriveCombo"
                          ItemsSource="{Binding Drives}"
                          SelectedItem="{Binding SelectedDrive, Mode=TwoWay}"
                          DisplayMemberPath="DisplayName"/>

                <Button Grid.Column="2" x:Name="BtnRefreshDrives"
                        Content="↻" Style="{DynamicResource BtnGhost}"
                        Width="32" Height="32" FontSize="16"
                        ToolTip="Laufwerke neu einlesen"
                        Click="BtnRefreshDrives_Click"/>

                <Button Grid.Column="3" x:Name="BtnVentoy"
                        Content="⚡ Ventoy installieren"
                        Style="{DynamicResource BtnSuccess}"
                        Margin="10,0,0,0" Click="BtnVentoy_Click"/>

                <CheckBox Grid.Column="4" x:Name="ChkSecureBoot"
                          Content="🔒 Secure Boot" Margin="14,0,0,0"
                          VerticalAlignment="Center"
                          Checked="ChkSecureBoot_Changed"
                          Unchecked="ChkSecureBoot_Changed"/>
```

ersetzen durch:

```xml
                <TextBlock x:Name="TxtTargetDrive" Grid.Column="0"
                           FontSize="9" FontWeight="Bold" Foreground="{DynamicResource BrushDim}"
                           VerticalAlignment="Center" Margin="0,0,10,0"/>

                <ComboBox Grid.Column="1" x:Name="DriveCombo"
                          ItemsSource="{Binding Drives}"
                          SelectedItem="{Binding SelectedDrive, Mode=TwoWay}"
                          DisplayMemberPath="DisplayName"/>

                <Button Grid.Column="2" x:Name="BtnRefreshDrives"
                        Content="↻" Style="{DynamicResource BtnGhost}"
                        Width="32" Height="32" FontSize="16"
                        Click="BtnRefreshDrives_Click"/>

                <Button Grid.Column="3" x:Name="BtnVentoy"
                        Style="{DynamicResource BtnSuccess}"
                        Margin="10,0,0,0" Click="BtnVentoy_Click"/>

                <CheckBox Grid.Column="4" x:Name="ChkSecureBoot"
                          Margin="14,0,0,0"
                          VerticalAlignment="Center"
                          Checked="ChkSecureBoot_Changed"
                          Unchecked="ChkSecureBoot_Changed"/>
```

- [ ] **Step 4: Hash-Status-Tooltip + 4 Spaltenüberschriften**

```xml
                            <TextBlock Grid.Column="1" Text="🔒" FontSize="10"
                                       HorizontalAlignment="Center"
                                       ToolTip="Hash-Status: grün = Prüfsumme vorhanden, rot = Integritätsprüfung fehlgeschlagen"
                                       Foreground="{DynamicResource BrushMid}"/>
                            <TextBlock Grid.Column="2" Text="Linux-Distribution  (Haken = Download)"
                                       FontWeight="Bold" FontSize="11" Foreground="{DynamicResource BrushMid}"/>
                            <TextBlock Grid.Column="3" Text="Lokal"
                                       FontWeight="Bold" FontSize="11" Foreground="{DynamicResource BrushMid}"/>
                            <TextBlock Grid.Column="4" Text="Auf dem Stick"
                                       FontWeight="Bold" FontSize="11" Foreground="{DynamicResource BrushMid}"/>
                            <TextBlock Grid.Column="5" Text="Aktuell"
                                       FontWeight="Bold" FontSize="11" Foreground="{DynamicResource BrushMid}"/>
```

ersetzen durch:

```xml
                            <TextBlock x:Name="TxtHashStatusIcon" Grid.Column="1" Text="🔒" FontSize="10"
                                       HorizontalAlignment="Center"
                                       Foreground="{DynamicResource BrushMid}"/>
                            <TextBlock x:Name="TxtColHeaderDistribution" Grid.Column="2"
                                       FontWeight="Bold" FontSize="11" Foreground="{DynamicResource BrushMid}"/>
                            <TextBlock x:Name="TxtColHeaderLocal" Grid.Column="3"
                                       FontWeight="Bold" FontSize="11" Foreground="{DynamicResource BrushMid}"/>
                            <TextBlock x:Name="TxtColHeaderOnStick" Grid.Column="4"
                                       FontWeight="Bold" FontSize="11" Foreground="{DynamicResource BrushMid}"/>
                            <TextBlock x:Name="TxtColHeaderCurrent" Grid.Column="5"
                                       FontWeight="Bold" FontSize="11" Foreground="{DynamicResource BrushMid}"/>
```

- [ ] **Step 5: „Protokoll leeren"-Button**

```xml
                    <Button Grid.Row="1" Content="Protokoll leeren"
                            Style="{DynamicResource BtnGhost}"
                            HorizontalAlignment="Right" Margin="8"
                            Click="BtnClearLog_Click"/>
```

ersetzen durch:

```xml
                    <Button x:Name="BtnClearLog" Grid.Row="1"
                            Style="{DynamicResource BtnGhost}"
                            HorizontalAlignment="Right" Margin="8"
                            Click="BtnClearLog_Click"/>
```

- [ ] **Step 6: `ApplyLocalizedText()` um die neuen Zuweisungen erweitern**

```csharp
        private void ApplyLocalizedText()
        {
            IsoTab.Header    = LocalizationService.T(Str.Tab_IsoSelection);
            LogTab.Header    = LocalizationService.T(Str.Tab_Log);
            StatusTab.Header = LocalizationService.T(Str.Tab_Status);
            BtnDownload.Content = LocalizationService.T(Str.Btn_Download);
            BtnUpdates.Content  = LocalizationService.T(Str.Btn_CheckForUpdates);
            BtnCancel.Content   = LocalizationService.T(Str.Btn_Cancel);
            BtnHelp.Content     = LocalizationService.T(Str.Btn_Help);
            BtnSettings.Content = LocalizationService.T(Str.Btn_Settings);
        }
```

ersetzen durch:

```csharp
        private void ApplyLocalizedText()
        {
            IsoTab.Header    = LocalizationService.T(Str.Tab_IsoSelection);
            LogTab.Header    = LocalizationService.T(Str.Tab_Log);
            StatusTab.Header = LocalizationService.T(Str.Tab_Status);
            BtnDownload.Content = LocalizationService.T(Str.Btn_Download);
            BtnUpdates.Content  = LocalizationService.T(Str.Btn_CheckForUpdates);
            BtnCancel.Content   = LocalizationService.T(Str.Btn_Cancel);
            BtnHelp.Content     = LocalizationService.T(Str.Btn_Help);
            BtnSettings.Content = LocalizationService.T(Str.Btn_Settings);

            TxtHeaderSubtitle.Text  = LocalizationService.T(Str.Main_HeaderSubtitle);
            TxtChipOnlineScan.Text  = LocalizationService.T(Str.Main_Chip_OnlineScan);
            TxtChipUsbScan.Text     = LocalizationService.T(Str.Main_Chip_UsbScan);
            TxtChipHealthCheck.Text = LocalizationService.T(Str.Main_Chip_HealthCheck);
            BtnUpdateDismiss.Content   = LocalizationService.T(Str.Main_Btn_Dismiss);
            BtnHardCaseDismiss.Content = LocalizationService.T(Str.Main_Btn_Dismiss);
            TxtTargetDrive.Text     = LocalizationService.T(Str.Main_Label_TargetDrive);
            BtnRefreshDrives.ToolTip = LocalizationService.T(Str.Main_Tooltip_RefreshDrives);
            BtnVentoy.Content       = LocalizationService.T(Str.Main_Btn_InstallVentoy);
            ChkSecureBoot.Content   = LocalizationService.T(Str.Main_Chk_SecureBoot);
            TxtHashStatusIcon.ToolTip = LocalizationService.T(Str.Main_Tooltip_HashStatusColumn);
            TxtColHeaderDistribution.Text = LocalizationService.T(Str.Main_ColumnHeader_Distribution);
            TxtColHeaderLocal.Text    = LocalizationService.T(Str.Main_ColumnHeader_Local);
            TxtColHeaderOnStick.Text  = LocalizationService.T(Str.Main_ColumnHeader_OnStick);
            TxtColHeaderCurrent.Text  = LocalizationService.T(Str.Main_ColumnHeader_Current);
            BtnClearLog.Content     = LocalizationService.T(Str.Main_Btn_ClearLog);
        }
```

- [ ] **Step 7: Build prüfen**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: `Build succeeded.`, keine neuen Warnungen. Insbesondere keine `CS0103`-Fehler zu den neuen `x:Name`-Bezeichnern (bestätigt, dass jedes neue `x:Name` korrekt im generierten `.g.cs` landet).

- [ ] **Step 8: Commit**

```bash
git add Views/MainWindow.xaml Views/MainWindow.xaml.cs
git commit -m "feat: Hauptfenster Header, Toolbar und Spaltenueberschriften lokalisiert"
```

---

### Task 4: `MainWindow.xaml` — Status-Tab (statische Texte)

**Files:**
- Modify: `Views/MainWindow.xaml`
- Modify: `Views/MainWindow.xaml.cs` (nur `ApplyLocalizedText()`)

**Interfaces:**
- Konsumiert: `LocalizationService.T(Str.Main_Status_...)`, `T(Str.Main_Btn_ClearHistory)` aus Task 1/2.
- Produziert: 12 neue `x:Name`-Attribute auf `Run`-Elementen (`RunLabelOperation`, `RunLabelFile`, `RunLabelProgress`, `RunLabelDetail`, `RunLabelCounter`, `RunLabelTargetDrive`, `RunLabelOnlineCheck`, `RunLabelLastChecked`, `RunLabelCompletedPrefix`, `RunLabelUsbCheck`, `RunLabelDrive`, `RunLabelNextCheck`) und 6 auf `TextBlock`/`Button` (`TxtStatusCurrentOperation`, `TxtStatusSectionBackgroundScans`, `TxtStatusSectionScheduled`, `TxtStatusDriveMonitoring`, `TxtStatusSectionHistory`, `BtnClearHistory`). Diese Namen werden von KEINEM anderen Task konsumiert. WICHTIG: dieser Task lässt die `DataTrigger`/`Style`-Blöcke für „Kein Vorgang aktiv." (Zeilen 454-470 im Ausgangszustand) und die beiden „läuft …"/„inaktiv"-`Run`-Paare komplett unangetastet — die gehören Task 6.

- [ ] **Step 1: „Aktueller Vorgang"-Sektionsüberschrift**

```xml
                        <TextBlock Text="Aktueller Vorgang" FontWeight="Bold" FontSize="13"
                                   Foreground="{DynamicResource BrushMid}" Margin="0,0,0,4"/>
```

ersetzen durch:

```xml
                        <TextBlock x:Name="TxtStatusCurrentOperation" FontWeight="Bold" FontSize="13"
                                   Foreground="{DynamicResource BrushMid}" Margin="0,0,0,4"/>
```

- [ ] **Step 2: Die 6 „Vorgang:/Datei:/Fortschritt:/Detail:/Zähler:/Ziel-Laufwerk:"-Labels**

```xml
                            <TextBlock FontSize="12"><Run Text="Vorgang: "/><Run Text="{Binding StatusText, Mode=OneWay}" FontWeight="SemiBold"/></TextBlock>
                            <TextBlock FontSize="12"><Run Text="Datei: "/><Run Text="{Binding CurrentOperationItem, Mode=OneWay}"/></TextBlock>
                            <TextBlock FontSize="12"><Run Text="Fortschritt: "/><Run Text="{Binding ProgressPercent, Mode=OneWay}"/><Run Text="%"/></TextBlock>
                            <TextBlock FontSize="12"><Run Text="Detail: "/><Run Text="{Binding CurrentOperationDetail, Mode=OneWay}"/></TextBlock>
                            <TextBlock FontSize="12"><Run Text="Zähler: "/><Run Text="{Binding CurrentOperationCounter, Mode=OneWay}"/></TextBlock>
                            <TextBlock FontSize="12"><Run Text="Ziel-Laufwerk: "/><Run Text="{Binding SelectedDriveLetter, Mode=OneWay}"/></TextBlock>
```

ersetzen durch:

```xml
                            <TextBlock FontSize="12"><Run x:Name="RunLabelOperation"/><Run Text="{Binding StatusText, Mode=OneWay}" FontWeight="SemiBold"/></TextBlock>
                            <TextBlock FontSize="12"><Run x:Name="RunLabelFile"/><Run Text="{Binding CurrentOperationItem, Mode=OneWay}"/></TextBlock>
                            <TextBlock FontSize="12"><Run x:Name="RunLabelProgress"/><Run Text="{Binding ProgressPercent, Mode=OneWay}"/><Run Text="%"/></TextBlock>
                            <TextBlock FontSize="12"><Run x:Name="RunLabelDetail"/><Run Text="{Binding CurrentOperationDetail, Mode=OneWay}"/></TextBlock>
                            <TextBlock FontSize="12"><Run x:Name="RunLabelCounter"/><Run Text="{Binding CurrentOperationCounter, Mode=OneWay}"/></TextBlock>
                            <TextBlock FontSize="12"><Run x:Name="RunLabelTargetDrive"/><Run Text="{Binding SelectedDriveLetter, Mode=OneWay}"/></TextBlock>
```

- [ ] **Step 3: „Automatische Hintergrund-Scans"-Block (Sektionsüberschrift + Online-Versionscheck-Zeile, DataTrigger-Teil bleibt unangetastet)**

```xml
                        <TextBlock Text="Automatische Hintergrund-Scans" FontWeight="Bold" FontSize="13"
                                   Foreground="{DynamicResource BrushMid}" Margin="0,0,0,4"/>
                        <StackPanel Margin="0,0,0,12">
                            <TextBlock FontSize="12">
                                <Run Text="🌐 Online-Versionscheck: "/>
                                <Run Text="läuft …" Foreground="{DynamicResource BrushBlue}">
                                    <Run.Style>
                                        <Style TargetType="Run">
                                            <Style.Triggers>
                                                <DataTrigger Binding="{Binding OnlineScanActive}" Value="False">
                                                    <Setter Property="Text" Value="inaktiv"/>
                                                </DataTrigger>
                                            </Style.Triggers>
                                        </Style>
                                    </Run.Style>
                                </Run>
                                <Run Text=" ("/><Run Text="{Binding OnlineScanPercent, Mode=OneWay}"/><Run Text="%)"/>
                            </TextBlock>
                            <TextBlock FontSize="12" Margin="16,0,0,6">
                                <Run Text="↳ zuletzt geprüft: "/><Run Text="{Binding OnlineScanCurrentItem, Mode=OneWay}"/>
                                <Run Text="  (abgeschlossen: "/><Run Text="{Binding LastAutoCheckText, Mode=OneWay}"/><Run Text=")"/>
                            </TextBlock>
                            <TextBlock FontSize="12">
                                <Run Text="💾 Stick-Prüfung: "/>
                                <Run Text="läuft …" Foreground="{DynamicResource BrushBlue}">
                                    <Run.Style>
                                        <Style TargetType="Run">
                                            <Style.Triggers>
                                                <DataTrigger Binding="{Binding UsbScanActive}" Value="False">
                                                    <Setter Property="Text" Value="inaktiv"/>
                                                </DataTrigger>
                                            </Style.Triggers>
                                        </Style>
                                    </Run.Style>
                                </Run>
                                <Run Text=" ("/><Run Text="{Binding UsbScanPercent, Mode=OneWay}"/><Run Text="%)"/>
                            </TextBlock>
                            <TextBlock FontSize="12" Margin="16,0,0,0">
                                <Run Text="↳ Laufwerk: "/><Run Text="{Binding SelectedDriveLetter, Mode=OneWay}"/>
                            </TextBlock>
                        </StackPanel>
```

ersetzen durch:

```xml
                        <TextBlock x:Name="TxtStatusSectionBackgroundScans" FontWeight="Bold" FontSize="13"
                                   Foreground="{DynamicResource BrushMid}" Margin="0,0,0,4"/>
                        <StackPanel Margin="0,0,0,12">
                            <TextBlock FontSize="12">
                                <Run x:Name="RunLabelOnlineCheck"/>
                                <Run Text="läuft …" Foreground="{DynamicResource BrushBlue}">
                                    <Run.Style>
                                        <Style TargetType="Run">
                                            <Style.Triggers>
                                                <DataTrigger Binding="{Binding OnlineScanActive}" Value="False">
                                                    <Setter Property="Text" Value="inaktiv"/>
                                                </DataTrigger>
                                            </Style.Triggers>
                                        </Style>
                                    </Run.Style>
                                </Run>
                                <Run Text=" ("/><Run Text="{Binding OnlineScanPercent, Mode=OneWay}"/><Run Text="%)"/>
                            </TextBlock>
                            <TextBlock FontSize="12" Margin="16,0,0,6">
                                <Run x:Name="RunLabelLastChecked"/><Run Text="{Binding OnlineScanCurrentItem, Mode=OneWay}"/>
                                <Run x:Name="RunLabelCompletedPrefix"/><Run Text="{Binding LastAutoCheckText, Mode=OneWay}"/><Run Text=")"/>
                            </TextBlock>
                            <TextBlock FontSize="12">
                                <Run x:Name="RunLabelUsbCheck"/>
                                <Run Text="läuft …" Foreground="{DynamicResource BrushBlue}">
                                    <Run.Style>
                                        <Style TargetType="Run">
                                            <Style.Triggers>
                                                <DataTrigger Binding="{Binding UsbScanActive}" Value="False">
                                                    <Setter Property="Text" Value="inaktiv"/>
                                                </DataTrigger>
                                            </Style.Triggers>
                                        </Style>
                                    </Run.Style>
                                </Run>
                                <Run Text=" ("/><Run Text="{Binding UsbScanPercent, Mode=OneWay}"/><Run Text="%)"/>
                            </TextBlock>
                            <TextBlock FontSize="12" Margin="16,0,0,0">
                                <Run x:Name="RunLabelDrive"/><Run Text="{Binding SelectedDriveLetter, Mode=OneWay}"/>
                            </TextBlock>
                        </StackPanel>
```

- [ ] **Step 4: „Geplante automatische Aktionen"-Sektion + Laufwerks-Überwachung-Satz**

```xml
                        <TextBlock Text="Geplante automatische Aktionen" FontWeight="Bold" FontSize="13"
                                   Foreground="{DynamicResource BrushMid}" Margin="0,0,0,4"/>
                        <TextBlock Margin="0,0,0,2" FontSize="12">
                            <Run Text="🌐 Nächster automatischer Online-Versionscheck: "/>
                            <Run Text="{Binding NextAutoCheckText, Mode=OneWay}"/>
                        </TextBlock>
                        <TextBlock Margin="0,0,0,12" FontSize="12" Foreground="{DynamicResource BrushMid}"
                                   Text="🔌 Laufwerks-Überwachung: läuft laufend im Hintergrund (Prüfung alle 8 Sekunden)."/>
```

ersetzen durch:

```xml
                        <TextBlock x:Name="TxtStatusSectionScheduled" FontWeight="Bold" FontSize="13"
                                   Foreground="{DynamicResource BrushMid}" Margin="0,0,0,4"/>
                        <TextBlock Margin="0,0,0,2" FontSize="12">
                            <Run x:Name="RunLabelNextCheck"/>
                            <Run Text="{Binding NextAutoCheckText, Mode=OneWay}"/>
                        </TextBlock>
                        <TextBlock x:Name="TxtStatusDriveMonitoring" Margin="0,0,0,12" FontSize="12" Foreground="{DynamicResource BrushMid}"/>
```

- [ ] **Step 5: „Verlauf"-Sektionsüberschrift + „Verlauf leeren"-Button**

```xml
                            <TextBlock Grid.Column="0" Text="Verlauf" FontWeight="Bold" FontSize="13"
                                       Foreground="{DynamicResource BrushMid}" VerticalAlignment="Center"/>
                            <Button Grid.Column="1" Content="Verlauf leeren" Style="{DynamicResource BtnGhost}"
                                    Click="BtnClearHistory_Click"/>
```

ersetzen durch:

```xml
                            <TextBlock x:Name="TxtStatusSectionHistory" Grid.Column="0" FontWeight="Bold" FontSize="13"
                                       Foreground="{DynamicResource BrushMid}" VerticalAlignment="Center"/>
                            <Button x:Name="BtnClearHistory" Grid.Column="1" Style="{DynamicResource BtnGhost}"
                                    Click="BtnClearHistory_Click"/>
```

- [ ] **Step 6: `ApplyLocalizedText()` erweitern**

```csharp
            BtnClearLog.Content     = LocalizationService.T(Str.Main_Btn_ClearLog);
        }
```

ersetzen durch:

```csharp
            BtnClearLog.Content     = LocalizationService.T(Str.Main_Btn_ClearLog);

            TxtStatusCurrentOperation.Text       = LocalizationService.T(Str.Main_Status_CurrentOperation);
            RunLabelOperation.Text               = LocalizationService.T(Str.Main_Status_LabelOperation);
            RunLabelFile.Text                    = LocalizationService.T(Str.Main_Status_LabelFile);
            RunLabelProgress.Text                = LocalizationService.T(Str.Main_Status_LabelProgress);
            RunLabelDetail.Text                  = LocalizationService.T(Str.Main_Status_LabelDetail);
            RunLabelCounter.Text                 = LocalizationService.T(Str.Main_Status_LabelCounter);
            RunLabelTargetDrive.Text             = LocalizationService.T(Str.Main_Status_LabelTargetDrive);
            TxtStatusSectionBackgroundScans.Text = LocalizationService.T(Str.Main_Status_SectionBackgroundScans);
            RunLabelOnlineCheck.Text             = LocalizationService.T(Str.Main_Status_LabelOnlineCheck);
            RunLabelLastChecked.Text             = LocalizationService.T(Str.Main_Status_LabelLastChecked);
            RunLabelCompletedPrefix.Text         = LocalizationService.T(Str.Main_Status_LabelCompletedPrefix);
            RunLabelUsbCheck.Text                = LocalizationService.T(Str.Main_Status_LabelUsbCheck);
            RunLabelDrive.Text                   = LocalizationService.T(Str.Main_Status_LabelDrive);
            TxtStatusSectionScheduled.Text       = LocalizationService.T(Str.Main_Status_SectionScheduled);
            RunLabelNextCheck.Text               = LocalizationService.T(Str.Main_Status_LabelNextCheck);
            TxtStatusDriveMonitoring.Text        = LocalizationService.T(Str.Main_Status_DriveMonitoring);
            TxtStatusSectionHistory.Text         = LocalizationService.T(Str.Main_Status_SectionHistory);
            BtnClearHistory.Content              = LocalizationService.T(Str.Main_Btn_ClearHistory);
        }
```

- [ ] **Step 7: Build prüfen**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: `Build succeeded.`, keine neuen Warnungen.

- [ ] **Step 8: Commit**

```bash
git add Views/MainWindow.xaml Views/MainWindow.xaml.cs
git commit -m "feat: Hauptfenster Status-Tab statische Texte lokalisiert"
```

---

### Task 5: `MainWindow.xaml` — Experten-Aktionsleiste + Info-Checkbox

**Files:**
- Modify: `Views/MainWindow.xaml`
- Modify: `Views/MainWindow.xaml.cs` (nur `ApplyLocalizedText()`)

**Interfaces:**
- Konsumiert: `LocalizationService.T(Str.Main_Btn_...)`, `T(Str.Main_Tooltip_...)`, `T(Str.Main_Chk_ShowInfo)` aus Task 1/2.
- Produziert: nichts, das andere Tasks konsumieren. Alle betroffenen Elemente (`BtnCheckUrls`, `BtnSearch`, `BtnEditDb`, `BtnHealthCheck`, `BtnCopyUsb`, `BtnVerifyIntegrity`, `BtnGitHubToken`, `ChkShowInfo`) haben bereits ein `x:Name` (wegen ihrer Click-Handler) — kein neues `x:Name` nötig.

- [ ] **Step 1: Die 7 Experten-Aktions-Buttons (Content + Tooltips)**

```xml
                <StackPanel Grid.Row="1" x:Name="ExpertBar"
                            Orientation="Horizontal" Margin="0,0,0,6">
                    <Button x:Name="BtnCheckUrls" Content="🌐  URLs prüfen"
                            Style="{DynamicResource BtnGhost}" Width="130"
                            Click="BtnCheckUrls_Click" Margin="0,0,8,0"/>
                    <Button x:Name="BtnSearch" Content="🔍  ISO suchen"
                            Style="{DynamicResource BtnGhost}" Width="130"
                            Click="BtnSearch_Click" Margin="0,0,8,0"/>
                    <Button x:Name="BtnEditDb" Content="🗃  Datenbank"
                            Style="{DynamicResource BtnGhost}" Width="130"
                            Click="BtnEditDb_Click" Margin="0,0,8,0"/>
                    <Button x:Name="BtnHealthCheck" Content="🩺  DB-Gesundheitscheck"
                            Style="{DynamicResource BtnGhost}" Width="170"
                            ToolTip="Prüft für alle Distros in der Datenbank, ob sie aktuell online erreichbar und ladbar sind."
                            Click="BtnHealthCheck_Click" Margin="0,0,8,0"/>
                    <Button x:Name="BtnCopyUsb" Content="🔁  Verpasste Kopien nachholen"
                            Style="{DynamicResource BtnGhost}" Width="210"
                            ToolTip="Manuelles Sicherheitsnetz: kopiert bereits lokal vollständig heruntergeladene, ausgewählte ISOs (erneut) auf den Stick — z.B. wenn die automatische 'Jetzt kopieren?'-Nachfrage abgelehnt wurde oder eine vorherige Kopie fehlgeschlagen ist. Der automatische Scan bietet dieselbe ISO pro Stick nur einmal je Sitzung an."
                            Click="BtnCopyUsb_Click" Margin="0,0,8,0"/>
                    <Button x:Name="BtnVerifyIntegrity" Content="🔒  Integrität prüfen"
                            Style="{DynamicResource BtnGhost}" Width="160"
                            ToolTip="Prüft die ISOs auf dem gewählten Stick gegen den beim Download/Import gespeicherten SHA-256-Referenzhash."
                            Click="BtnVerifyIntegrity_Click" Margin="0,0,8,0"/>
                    <Button x:Name="BtnGitHubToken" Content="🔑  GitHub-Token"
                            Style="{DynamicResource BtnGhost}" Width="130"
                            ToolTip="Optional: hebt das API-Limit für GitHub-basierte Distros von 60 auf 5000 Anfragen/Std an."
                            Click="BtnGitHubToken_Click"/>
                </StackPanel>
```

ersetzen durch:

```xml
                <StackPanel Grid.Row="1" x:Name="ExpertBar"
                            Orientation="Horizontal" Margin="0,0,0,6">
                    <Button x:Name="BtnCheckUrls"
                            Style="{DynamicResource BtnGhost}" Width="130"
                            Click="BtnCheckUrls_Click" Margin="0,0,8,0"/>
                    <Button x:Name="BtnSearch"
                            Style="{DynamicResource BtnGhost}" Width="130"
                            Click="BtnSearch_Click" Margin="0,0,8,0"/>
                    <Button x:Name="BtnEditDb"
                            Style="{DynamicResource BtnGhost}" Width="130"
                            Click="BtnEditDb_Click" Margin="0,0,8,0"/>
                    <Button x:Name="BtnHealthCheck"
                            Style="{DynamicResource BtnGhost}" Width="170"
                            Click="BtnHealthCheck_Click" Margin="0,0,8,0"/>
                    <Button x:Name="BtnCopyUsb"
                            Style="{DynamicResource BtnGhost}" Width="210"
                            Click="BtnCopyUsb_Click" Margin="0,0,8,0"/>
                    <Button x:Name="BtnVerifyIntegrity"
                            Style="{DynamicResource BtnGhost}" Width="160"
                            Click="BtnVerifyIntegrity_Click" Margin="0,0,8,0"/>
                    <Button x:Name="BtnGitHubToken"
                            Style="{DynamicResource BtnGhost}" Width="130"
                            Click="BtnGitHubToken_Click"/>
                </StackPanel>
```

- [ ] **Step 2: Info-Fenster-Checkbox**

```xml
                <CheckBox Grid.Column="1" x:Name="ChkShowInfo"
                          Content="Info-Fenster (Mouseover) anzeigen"
                          IsChecked="True" FontSize="10.5"
                          VerticalAlignment="Center"/>
```

ersetzen durch:

```xml
                <CheckBox Grid.Column="1" x:Name="ChkShowInfo"
                          IsChecked="True" FontSize="10.5"
                          VerticalAlignment="Center"/>
```

- [ ] **Step 3: `ApplyLocalizedText()` erweitern**

```csharp
            BtnClearHistory.Content              = LocalizationService.T(Str.Main_Btn_ClearHistory);
        }
```

ersetzen durch:

```csharp
            BtnClearHistory.Content              = LocalizationService.T(Str.Main_Btn_ClearHistory);

            BtnCheckUrls.Content       = LocalizationService.T(Str.Main_Btn_CheckUrls);
            BtnSearch.Content          = LocalizationService.T(Str.Main_Btn_SearchIso);
            BtnEditDb.Content          = LocalizationService.T(Str.Main_Btn_EditDb);
            BtnHealthCheck.Content     = LocalizationService.T(Str.Main_Btn_HealthCheck);
            BtnHealthCheck.ToolTip     = LocalizationService.T(Str.Main_Tooltip_HealthCheck);
            BtnCopyUsb.Content         = LocalizationService.T(Str.Main_Btn_CopyUsb);
            BtnCopyUsb.ToolTip         = LocalizationService.T(Str.Main_Tooltip_CopyUsb);
            BtnVerifyIntegrity.Content = LocalizationService.T(Str.Main_Btn_VerifyIntegrity);
            BtnVerifyIntegrity.ToolTip = LocalizationService.T(Str.Main_Tooltip_VerifyIntegrity);
            BtnGitHubToken.Content     = LocalizationService.T(Str.Main_Btn_GitHubToken);
            BtnGitHubToken.ToolTip     = LocalizationService.T(Str.Main_Tooltip_GitHubToken);
            ChkShowInfo.Content        = LocalizationService.T(Str.Main_Chk_ShowInfo);
        }
```

- [ ] **Step 4: Build prüfen**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: `Build succeeded.`, keine neuen Warnungen.

- [ ] **Step 5: Commit**

```bash
git add Views/MainWindow.xaml Views/MainWindow.xaml.cs
git commit -m "feat: Hauptfenster Experten-Aktionsleiste und Info-Checkbox lokalisiert"
```

---

### Task 6: `MainViewModel.cs` + `MainWindow.xaml` — Banner-Texte + zustandsabhängige Status-Texte

**Files:**
- Modify: `ViewModels/MainViewModel.cs`
- Modify: `Views/MainWindow.xaml`

**Interfaces:**
- Konsumiert: `LocalizationService.T(Str.Banner_...)`, `T(Str.Main_Status_NoOperation/OnlineScanRunning/UsbScanRunning/Running/Inactive)` aus Task 1/2. `string.Format` für `Banner_UpdateAvailable`, `Banner_UpdateReady`, `Banner_HardCaseSingle`, `Banner_HardCasePlural` (mehrere/eingebettete Werte).
- Produziert: 3 neue `MainViewModel`-Properties (`CurrentOperationStatusText`, `OnlineCheckStatusText`, `UsbCheckStatusText`) — werden nur von den XAML-Bindings in diesem Task konsumiert.

- [ ] **Step 1: `NotifyScanHint()` um die 3 neuen Properties erweitern + neue Properties ergänzen**

```csharp
        public bool ScanInProgress => OnlineScanActive || UsbScanActive;
        public string ScanHintText => OnlineScanActive ? "Online-Scan, bitte warten"
                                    : UsbScanActive     ? "Stick-Scan, bitte warten"
                                    : string.Empty;
        private void NotifyScanHint() { OnPropertyChanged(nameof(ScanInProgress)); OnPropertyChanged(nameof(ScanHintText)); }
```

ersetzen durch:

```csharp
        public bool ScanInProgress => OnlineScanActive || UsbScanActive;
        public string ScanHintText => OnlineScanActive ? "Online-Scan, bitte warten"
                                    : UsbScanActive     ? "Stick-Scan, bitte warten"
                                    : string.Empty;

        // Ersetzt die frueheren MainWindow.xaml-DataTrigger/Setter-Bloecke fuer den Status-Tab —
        // ein Setter Property="Text" Value="..." kann LocalizationService.T(...) nicht aufrufen,
        // siehe docs/superpowers/specs/2026-07-24-mainwindow-localization-design.md Architektur-
        // Korrektur. Gleiches Berechnungsmuster wie ScanHintText oben.
        public string CurrentOperationStatusText =>
            OnlineScanActive ? LocalizationService.T(Str.Main_Status_OnlineScanRunning)
            : UsbScanActive   ? LocalizationService.T(Str.Main_Status_UsbScanRunning)
            : LocalizationService.T(Str.Main_Status_NoOperation);
        public string OnlineCheckStatusText => OnlineScanActive
            ? LocalizationService.T(Str.Main_Status_Running)
            : LocalizationService.T(Str.Main_Status_Inactive);
        public string UsbCheckStatusText => UsbScanActive
            ? LocalizationService.T(Str.Main_Status_Running)
            : LocalizationService.T(Str.Main_Status_Inactive);

        private void NotifyScanHint()
        {
            OnPropertyChanged(nameof(ScanInProgress));
            OnPropertyChanged(nameof(ScanHintText));
            OnPropertyChanged(nameof(CurrentOperationStatusText));
            OnPropertyChanged(nameof(OnlineCheckStatusText));
            OnPropertyChanged(nameof(UsbCheckStatusText));
        }
```

- [ ] **Step 2: Update-Banner-Texte + Button-Texte**

```csharp
        public void SetAvailableUpdate(UlmUpdateInfo info)
        {
            _availableUpdate = info;
            UpdateBannerState = UpdateBannerState.Available;
            UpdateBannerText = $"🆕 Neue Version verfügbar: v{info.LatestVersion} (installiert: v{Constants.AppVersion})";
            UpdateBannerButtonText = "⬇ Herunterladen …";
            UpdateBannerButtonEnabled = true;
            UpdateBannerVisible = true;
        }
        // Vom MainWindow aufgerufen, sobald der automatische Hintergrund-Download startet.
        public void SetUpdateDownloading()
        {
            UpdateBannerState = UpdateBannerState.Downloading;
            UpdateBannerText = "⬇ Update wird heruntergeladen …";
            UpdateBannerButtonText = "⬇ Wird heruntergeladen …";
            UpdateBannerButtonEnabled = false;
        }
        // Vom MainWindow aufgerufen, sobald der Download fertig und die Datei bereit zur Installation ist.
        public void SetUpdateReadyToInstall(string downloadedFilePath)
        {
            _downloadedUpdatePath = downloadedFilePath;
            UpdateBannerState = UpdateBannerState.ReadyToInstall;
            UpdateBannerText = $"✅ Update bereit — v{_availableUpdate?.LatestVersion}";
            UpdateBannerButtonText = "✅ Jetzt installieren & neu starten";
            UpdateBannerButtonEnabled = true;
        }
```

ersetzen durch:

```csharp
        public void SetAvailableUpdate(UlmUpdateInfo info)
        {
            _availableUpdate = info;
            UpdateBannerState = UpdateBannerState.Available;
            UpdateBannerText = string.Format(LocalizationService.T(Str.Banner_UpdateAvailable), info.LatestVersion, Constants.AppVersion);
            UpdateBannerButtonText = LocalizationService.T(Str.Banner_UpdateBtn_Available);
            UpdateBannerButtonEnabled = true;
            UpdateBannerVisible = true;
        }
        // Vom MainWindow aufgerufen, sobald der automatische Hintergrund-Download startet.
        public void SetUpdateDownloading()
        {
            UpdateBannerState = UpdateBannerState.Downloading;
            UpdateBannerText = LocalizationService.T(Str.Banner_UpdateDownloading);
            UpdateBannerButtonText = LocalizationService.T(Str.Banner_UpdateBtn_Downloading);
            UpdateBannerButtonEnabled = false;
        }
        // Vom MainWindow aufgerufen, sobald der Download fertig und die Datei bereit zur Installation ist.
        public void SetUpdateReadyToInstall(string downloadedFilePath)
        {
            _downloadedUpdatePath = downloadedFilePath;
            UpdateBannerState = UpdateBannerState.ReadyToInstall;
            UpdateBannerText = string.Format(LocalizationService.T(Str.Banner_UpdateReady), _availableUpdate?.LatestVersion);
            UpdateBannerButtonText = LocalizationService.T(Str.Banner_UpdateBtn_ReadyToInstall);
            UpdateBannerButtonEnabled = true;
        }
```

Zusätzlich das Feld weiter oben in derselben Klasse:

```csharp
        private string _updateBannerButtonText = "⬇ Herunterladen …";
```

ersetzen durch:

```csharp
        private string _updateBannerButtonText = string.Empty;
```

(Der Default-Wert wird ohnehin von `SetAvailableUpdate` überschrieben, bevor das Banner sichtbar wird — ein hartcodierter deutscher Default-String ist hier überflüssig und potenziell irreführend, siehe auch das analoge `_hardCaseBannerText = string.Empty` direkt darunter.)

- [ ] **Step 3: Härtefall-Banner-Text**

```csharp
            HardCaseBannerText = _pendingHardCaseNames.Count == 1
                ? $"🔧 Manuelle Quellen-Suche jetzt möglich für: {_pendingHardCaseNames[0]}"
                : $"🔧 Manuelle Quellen-Suche jetzt möglich für {_pendingHardCaseNames.Count} Distros: {string.Join(", ", _pendingHardCaseNames)}";
```

ersetzen durch:

```csharp
            HardCaseBannerText = _pendingHardCaseNames.Count == 1
                ? string.Format(LocalizationService.T(Str.Banner_HardCaseSingle), _pendingHardCaseNames[0])
                : string.Format(LocalizationService.T(Str.Banner_HardCasePlural), _pendingHardCaseNames.Count, string.Join(", ", _pendingHardCaseNames));
```

- [ ] **Step 4: XAML — „Kein Vorgang aktiv."-Block durch Binding ersetzen**

```xml
                        <TextBlock Margin="0,0,0,12" FontSize="12" Text="Kein Vorgang aktiv.">
                            <TextBlock.Style>
                                <Style TargetType="TextBlock">
                                    <Style.Triggers>
                                        <DataTrigger Binding="{Binding OnlineScanActive}" Value="True">
                                            <Setter Property="Text" Value="🌐 Automatischer Online-Versionscheck läuft — Details siehe „Automatische Hintergrund-Scans” unten."/>
                                        </DataTrigger>
                                        <DataTrigger Binding="{Binding UsbScanActive}" Value="True">
                                            <Setter Property="Text" Value="💾 Automatische Stick-Prüfung läuft — Details siehe „Automatische Hintergrund-Scans” unten."/>
                                        </DataTrigger>
                                        <DataTrigger Binding="{Binding IsBusy}" Value="True">
                                            <Setter Property="Visibility" Value="Collapsed"/>
                                        </DataTrigger>
                                    </Style.Triggers>
                                </Style>
                            </TextBlock.Style>
                        </TextBlock>
```

ersetzen durch:

```xml
                        <TextBlock Margin="0,0,0,12" FontSize="12" Text="{Binding CurrentOperationStatusText}">
                            <TextBlock.Style>
                                <Style TargetType="TextBlock">
                                    <Style.Triggers>
                                        <DataTrigger Binding="{Binding IsBusy}" Value="True">
                                            <Setter Property="Visibility" Value="Collapsed"/>
                                        </DataTrigger>
                                    </Style.Triggers>
                                </Style>
                            </TextBlock.Style>
                        </TextBlock>
```

- [ ] **Step 5: XAML — die beiden „läuft …"/„inaktiv"-`Run`-Blöcke durch Bindings ersetzen**

```xml
                                <Run x:Name="RunLabelOnlineCheck"/>
                                <Run Text="läuft …" Foreground="{DynamicResource BrushBlue}">
                                    <Run.Style>
                                        <Style TargetType="Run">
                                            <Style.Triggers>
                                                <DataTrigger Binding="{Binding OnlineScanActive}" Value="False">
                                                    <Setter Property="Text" Value="inaktiv"/>
                                                </DataTrigger>
                                            </Style.Triggers>
                                        </Style>
                                    </Run.Style>
                                </Run>
```

ersetzen durch:

```xml
                                <Run x:Name="RunLabelOnlineCheck"/>
                                <Run Text="{Binding OnlineCheckStatusText}" Foreground="{DynamicResource BrushBlue}"/>
```

```xml
                                <Run x:Name="RunLabelUsbCheck"/>
                                <Run Text="läuft …" Foreground="{DynamicResource BrushBlue}">
                                    <Run.Style>
                                        <Style TargetType="Run">
                                            <Style.Triggers>
                                                <DataTrigger Binding="{Binding UsbScanActive}" Value="False">
                                                    <Setter Property="Text" Value="inaktiv"/>
                                                </DataTrigger>
                                            </Style.Triggers>
                                        </Style>
                                    </Run.Style>
                                </Run>
```

ersetzen durch:

```xml
                                <Run x:Name="RunLabelUsbCheck"/>
                                <Run Text="{Binding UsbCheckStatusText}" Foreground="{DynamicResource BrushBlue}"/>
```

- [ ] **Step 6: Build prüfen**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: `Build succeeded.`, keine neuen Warnungen.

- [ ] **Step 7: Volle Testsuite laufen lassen**

Run: `dotnet test ULM.Tests`
Expected: alle Tests grün (dieser Task ändert keine Test-relevante Logik in isolierten Units, nur ViewModel-Properties/UI-Verdrahtung).

- [ ] **Step 8: Commit**

```bash
git add ViewModels/MainViewModel.cs Views/MainWindow.xaml
git commit -m "feat: Update-/Haertefall-Banner und zustandsabhaengige Status-Texte lokalisiert"
```

---

### Task 7: `IsoViewModels.cs` + `MainWindow.xaml` — Zeilen-Status, Tooltips, Kategorie-Tooltip

**Files:**
- Modify: `ViewModels/IsoViewModels.cs`
- Modify: `Views/MainWindow.xaml`

**Interfaces:**
- Konsumiert: `LocalizationService.T(Str.Row_...)` aus Task 1/2.
- Produziert: 2 neue Properties (`IsoEntryViewModel.ManualSearchTooltip`, `IsoCategoryViewModel.SelectAllTooltip`) — werden nur von den 2 XAML-Bindings in diesem Task konsumiert.

- [ ] **Step 1: `LocalStatus`, `UsbStatus`, `VersionStatus`**

```csharp
        public string LocalStatus
        {
            get
            {
                if (_entry.IsLocallyAvailable(_downloadDir))
                {
                    long size = _entry.LocalFileSize(_downloadDir);
                    return $"Lokal {size / 1_048_576} MB";
                }
                return "nicht lokal";
            }
        }

        public string UsbStatus => _entry.UsbStatus switch
        {
            Core.Models.UsbStatus.Ok       => $"Ja  {_entry.UsbSize}".Trim(),
            Core.Models.UsbStatus.Outdated => $"Veraltet  {_entry.UsbSize}".Trim(),
            Core.Models.UsbStatus.Missing  => "Nein",
            _                              => "Ungeprüft",
        };

        public string VersionStatus
        {
            get
            {
                if (_entry.HasResolvedUpdate)
                    return $"Update v{_entry.RemoteVersion}";
                if (_entry.HasOnlineVersionInfo)
                    return $"Aktuell (v{_entry.RemoteVersion})";
                if (_entry.UsbStatus == Core.Models.UsbStatus.Ok)
                    return "Ja";
                if (_entry.IsLocallyAvailable(_downloadDir))
                    return "Lokal vorhanden";
                return "?";
            }
        }
```

ersetzen durch:

```csharp
        public string LocalStatus
        {
            get
            {
                if (_entry.IsLocallyAvailable(_downloadDir))
                {
                    long size = _entry.LocalFileSize(_downloadDir);
                    return $"{LocalizationService.T(Str.Row_Local)} {size / 1_048_576} MB";
                }
                return LocalizationService.T(Str.Row_NotLocal);
            }
        }

        public string UsbStatus => _entry.UsbStatus switch
        {
            Core.Models.UsbStatus.Ok       => $"{LocalizationService.T(Str.Row_Yes)}  {_entry.UsbSize}".Trim(),
            Core.Models.UsbStatus.Outdated => $"{LocalizationService.T(Str.Row_Outdated)}  {_entry.UsbSize}".Trim(),
            Core.Models.UsbStatus.Missing  => LocalizationService.T(Str.Row_No),
            _                              => LocalizationService.T(Str.Row_Unverified),
        };

        public string VersionStatus
        {
            get
            {
                if (_entry.HasResolvedUpdate)
                    return $"{LocalizationService.T(Str.Row_UpdatePrefix)} v{_entry.RemoteVersion}";
                if (_entry.HasOnlineVersionInfo)
                    return $"{LocalizationService.T(Str.Row_CurrentPrefix)} (v{_entry.RemoteVersion})";
                if (_entry.UsbStatus == Core.Models.UsbStatus.Ok)
                    return LocalizationService.T(Str.Row_Yes);
                if (_entry.IsLocallyAvailable(_downloadDir))
                    return LocalizationService.T(Str.Row_LocallyAvailable);
                return "?";
            }
        }

        // Neu in Phase 3: ersetzt den frueher direkt in MainWindow.xaml hartcodierten
        // ToolTip="Quelle manuell suchen/eintragen" auf dem 🔧-Button in EntryTemplate — die
        // DataTemplate wird pro Zeile instanziiert, ApplyLocalizedText() (einmalig fuers ganze
        // Fenster) kann sie nicht erreichen. Siehe
        // docs/superpowers/specs/2026-07-24-mainwindow-localization-design.md Architektur-Korrektur.
        public string ManualSearchTooltip => LocalizationService.T(Str.Row_ManualSearchTooltip);
```

- [ ] **Step 2: `HashStatusTooltip` und `TipTooltip`**

Etwas weiter oben in derselben Klasse (vor `LocalStatus`):

```csharp
                if (_entry.HasResolvedUpdate)
                    sb.AppendLine($"🆕  Neue Version verfügbar: v{_entry.RemoteVersion}  (jetzt herunterladen)");
```

ersetzen durch:

```csharp
                if (_entry.HasResolvedUpdate)
                    sb.AppendLine(string.Format(LocalizationService.T(Str.Row_TipNewVersion), _entry.RemoteVersion));
```

```csharp
                if (_entry.ImportedFromStick)
                    sb.AppendLine("📥  Vom USB-Stick importiert");

                if (_entry.UrlChecked)
                    sb.AppendLine(_entry.UrlOk
                        ? "🌐✓  URL erreichbar — Download-Server antwortet"
                        : "🌐✗  URL nicht erreichbar — Download-Server antwortet nicht");
```

ersetzen durch:

```csharp
                if (_entry.ImportedFromStick)
                    sb.AppendLine(LocalizationService.T(Str.Row_TipImported));

                if (_entry.UrlChecked)
                    sb.AppendLine(_entry.UrlOk
                        ? LocalizationService.T(Str.Row_TipUrlOk)
                        : LocalizationService.T(Str.Row_TipUrlFail));
```

Suche im Bereich der `HashStatusTooltip`-Property nach:

```csharp
            "⚠ Hash-Abweichung — Datei weicht von der zuletzt gespeicherten Prüfsumme ab (evtl. beschädigt oder ersetzt)."
```

und ersetze den gesamten String-Literal-Ausdruck an dieser Stelle durch `LocalizationService.T(Str.Row_HashMismatch)`. Ebenso für

```csharp
            "✅ Prüfsumme gegen die offiziell vom Anbieter veröffentlichte Prüfsumme verifiziert."
```

durch `LocalizationService.T(Str.Row_HashVerifiedOfficial)`, und für

```csharp
            "✅ Referenz-Prüfsumme lokal beim Download/Import berechnet (keine offizielle Gegenprüfung)."
```

durch `LocalizationService.T(Str.Row_HashLocalOnly)`.

(Die genaue umgebende `if`/`else`-Struktur von `HashStatusTooltip` wurde beim Schreiben dieses Plans nicht Zeile für Zeile zitiert — falls die drei Literale nicht 1:1 so im Code stehen wie oben gezeigt, den umgebenden `if`/`else`-Ausdruck beibehalten und NUR die drei String-Literale durch die `T(...)`-Aufrufe ersetzen.)

- [ ] **Step 3: Neue `SelectAllTooltip`-Property auf `IsoCategoryViewModel`**

```csharp
        public string Category       { get; }
        public string CategoryLabel  { get; }
        public bool   IsExpanded     { get; set; } = true;
```

ersetzen durch:

```csharp
        public string Category       { get; }
        public string CategoryLabel  { get; }
        public bool   IsExpanded     { get; set; } = true;

        // Neu in Phase 3: ersetzt den frueher direkt in MainWindow.xaml hartcodierten
        // ToolTip="Alle Distros dieser Kategorie an-/abwählen" auf der Sammel-Checkbox in
        // CategoryTemplate — dieselbe Begruendung wie IsoEntryViewModel.ManualSearchTooltip oben.
        public string SelectAllTooltip => LocalizationService.T(Str.Row_CategorySelectAllTooltip);
```

- [ ] **Step 4: `using ULM.Infrastructure;` prüfen**

`ViewModels/IsoViewModels.cs` importiert bereits `using ULM.Infrastructure;` (Zeile 9, für `IsoEntry`/`AppLanguage`-Nachbarn) — kein neuer `using`-Eintrag nötig. Falls der Build in Step 6 einen `CS0103`-Fehler zu `LocalizationService`/`Str` meldet, `using ULM.Infrastructure;` am Dateianfang ergänzen.

- [ ] **Step 5: XAML — die beiden Tooltips auf Bindings umstellen**

In `Views/MainWindow.xaml`:

```xml
                    <Button Grid.Column="6" Content="🔧" FontSize="12"
                            Width="24" Height="24" Padding="0"
                            ToolTip="Quelle manuell suchen/eintragen"
                            Click="BtnManualSearch_Click"
```

ersetzen durch:

```xml
                    <Button Grid.Column="6" Content="🔧" FontSize="12"
                            Width="24" Height="24" Padding="0"
                            ToolTip="{Binding ManualSearchTooltip}"
                            Click="BtnManualSearch_Click"
```

```xml
                        <CheckBox IsChecked="{Binding AllSelected, Mode=TwoWay}"
                                  IsThreeState="True"
                                  VerticalAlignment="Center" Margin="0,0,10,0"
                                  ToolTip="Alle Distros dieser Kategorie an-/abwählen"/>
```

ersetzen durch:

```xml
                        <CheckBox IsChecked="{Binding AllSelected, Mode=TwoWay}"
                                  IsThreeState="True"
                                  VerticalAlignment="Center" Margin="0,0,10,0"
                                  ToolTip="{Binding SelectAllTooltip}"/>
```

- [ ] **Step 6: Build prüfen**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: `Build succeeded.`, keine neuen Warnungen.

- [ ] **Step 7: Volle Testsuite laufen lassen**

Run: `dotnet test ULM.Tests`
Expected: alle Tests grün.

- [ ] **Step 8: Commit**

```bash
git add ViewModels/IsoViewModels.cs Views/MainWindow.xaml
git commit -m "feat: Distro-Zeilen-Status, Tooltips und Kategorie-Tooltip lokalisiert"
```

---

### Task 8: `MainWindow.xaml.cs` — MessageBox-Batch 1 (Konstruktor bis `ManualUpdateDownloadFallbackAsync`)

**Files:**
- Modify: `Views/MainWindow.xaml.cs`

**Interfaces:**
- Konsumiert: `LocalizationService.T(Str.Msg_...)`, `T(Str.Banner_HardCaseSingle)`, `T(Str.Main_Footer_IsoFolder)` aus Task 1/2. `string.Format` für Werte mit Platzhaltern.

- [ ] **Step 1: Langsamer-Download-Bestätigung**

```csharp
            _vm.ConfirmSlowDownload = (name, host) => MessageBox.Show(
                $"{name}: Es wurde kein schnellerer Mirror gefunden — {host} überträgt weiterhin nur sehr langsam.\n\n" +
                "Trotzdem mit dieser Quelle fortfahren? (Das kann sehr lange dauern.)",
                "⚠ Langsamer Download", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
```

ersetzen durch:

```csharp
            _vm.ConfirmSlowDownload = (name, host) => MessageBox.Show(
                string.Format(LocalizationService.T(Str.Msg_SlowDownload_Body), name, host),
                LocalizationService.T(Str.Msg_SlowDownload_Title), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
```

- [ ] **Step 2: Unvollständige-ISOs-auf-Stick-Dialog**

```csharp
                var dlg = new OrphanedDownloadsDialog(files, "Unvollständige ISOs auf dem Stick gefunden", "unvollständige ISO-Datei(en) auf dem Stick") { Owner = this };
```

ersetzen durch:

```csharp
                var dlg = new OrphanedDownloadsDialog(files,
                    LocalizationService.T(Str.Msg_OrphanedIncomplete_Title),
                    LocalizationService.T(Str.Msg_OrphanedIncomplete_Description)) { Owner = this };
```

- [ ] **Step 3: Vorgang-abgeschlossen-MessageBox**

```csharp
                MessageBox.Show(message, "✅ Vorgang abgeschlossen", MessageBoxButton.OK, MessageBoxImage.Information);
```

ersetzen durch:

```csharp
                MessageBox.Show(message, LocalizationService.T(Str.Msg_OperationComplete_Title), MessageBoxButton.OK, MessageBoxImage.Information);
```

- [ ] **Step 4: Härtefall-Einzel-Hinweis**

```csharp
            _vm.HardCaseNoticeRequested += name => new QuickConfirmationWindow(
                $"🔧 Manuelle Quellen-Suche jetzt möglich für: {name}") { Owner = this }.Show();
```

ersetzen durch:

```csharp
            _vm.HardCaseNoticeRequested += name => new QuickConfirmationWindow(
                string.Format(LocalizationService.T(Str.Banner_HardCaseSingle), name)) { Owner = this }.Show();
```

- [ ] **Step 5: Footer-Text (ISO-Ordner)**

```csharp
            FooterLbl.Text = $"ISO-Ordner: {AppPaths.Instance.DownloadDir}";
```

ersetzen durch:

```csharp
            FooterLbl.Text = string.Format(LocalizationService.T(Str.Main_Footer_IsoFolder), AppPaths.Instance.DownloadDir);
```

- [ ] **Step 6: Update-Download-Fehlgeschlagen-MessageBox**

```csharp
                MessageBox.Show("Der Download des Programm-Updates ist fehlgeschlagen.", Constants.AppTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
```

ersetzen durch:

```csharp
                MessageBox.Show(LocalizationService.T(Str.Msg_UpdateDownloadFailed), Constants.AppTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
```

- [ ] **Step 7: Build prüfen**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: `Build succeeded.`, keine neuen Warnungen.

- [ ] **Step 8: Commit**

```bash
git add Views/MainWindow.xaml.cs
git commit -m "feat: MainWindow MessageBox-Texte Batch 1 lokalisiert (Downloads, Wartung, Footer)"
```

---

### Task 9: `MainWindow.xaml.cs` — MessageBox-Batch 2 (`OnStickUpdateAvailable` bis `OnMissingOnStickDetected`)

**Files:**
- Modify: `Views/MainWindow.xaml.cs`

**Interfaces:**
- Konsumiert: `LocalizationService.T(Str.Msg_...)` aus Task 1/2. `string.Format` für Werte mit Platzhaltern.

- [ ] **Step 1: Stick-Aktualisierung-Dialog**

```csharp
            var sb = new StringBuilder(); sb.AppendLine($"Auf {drive} wurden {outdated.Count} veraltete ISO(s) gefunden:"); sb.AppendLine();
            foreach (var (entry, _) in outdated) sb.AppendLine($"  • {entry.Name}"); sb.AppendLine(); sb.AppendLine("Jetzt aktualisieren?");
            if (MessageBox.Show(sb.ToString(), "💾 Stick-Aktualisierung", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
```

ersetzen durch:

```csharp
            var sb = new StringBuilder(); sb.AppendLine(string.Format(LocalizationService.T(Str.Msg_StickOutdatedFound), drive, outdated.Count)); sb.AppendLine();
            foreach (var (entry, _) in outdated) sb.AppendLine($"  • {entry.Name}"); sb.AppendLine(); sb.AppendLine(LocalizationService.T(Str.Msg_UpdateNow));
            if (MessageBox.Show(sb.ToString(), LocalizationService.T(Str.Msg_StickUpdate_Title), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
```

- [ ] **Step 2: Veraltete-Duplikate-Dialog**

```csharp
            var dlg = new OrphanedDownloadsDialog(files,
                "Veraltete Duplikate auf dem Stick gefunden",
                "veraltete Duplikat-ISO(s) — aktuelle Version bereits vorhanden") { Owner = this };
```

ersetzen durch:

```csharp
            var dlg = new OrphanedDownloadsDialog(files,
                LocalizationService.T(Str.Msg_OutdatedDuplicates_Title),
                LocalizationService.T(Str.Msg_OutdatedDuplicates_Description)) { Owner = this };
```

- [ ] **Step 3: Vollständige-ISOs-nicht-auf-Stick-Dialog + Lösch-Nachfrage**

```csharp
            var sb = new StringBuilder(); sb.AppendLine($"{fresh.Count} ISO(s) vollständig lokal, NICHT auf {drive}:"); sb.AppendLine();
            foreach (var e in fresh) sb.AppendLine($"  • {e.Name}"); sb.AppendLine(); sb.AppendLine("Jetzt kopieren?");
            if (MessageBox.Show(sb.ToString(), "💾 Vollständige ISOs nicht auf dem Stick", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            bool del = MessageBox.Show("Lokale Dateien danach löschen?", "Dateien löschen?", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
```

ersetzen durch:

```csharp
            var sb = new StringBuilder(); sb.AppendLine(string.Format(LocalizationService.T(Str.Msg_LocalNotOnStick), fresh.Count, drive)); sb.AppendLine();
            foreach (var e in fresh) sb.AppendLine($"  • {e.Name}"); sb.AppendLine(); sb.AppendLine(LocalizationService.T(Str.Msg_CopyNow));
            if (MessageBox.Show(sb.ToString(), LocalizationService.T(Str.Msg_LocalNotOnStick_Title), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            bool del = MessageBox.Show(LocalizationService.T(Str.Msg_DeleteLocalAfterCopy_Immediate), LocalizationService.T(Str.Msg_DeleteFiles_Title), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
```

- [ ] **Step 4: Build prüfen**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: `Build succeeded.`, keine neuen Warnungen.

- [ ] **Step 5: Commit**

```bash
git add Views/MainWindow.xaml.cs
git commit -m "feat: MainWindow MessageBox-Texte Batch 2 lokalisiert (Stick-Aktualisierung, Duplikate)"
```

---

### Task 10: `MainWindow.xaml.cs` — MessageBox-Batch 3 (`OnNewDriveInserted` bis `ConfirmEnoughFreeSpaceAsync`)

**Files:**
- Modify: `Views/MainWindow.xaml.cs`

**Interfaces:**
- Konsumiert: `LocalizationService.T(Str.Msg_...)` aus Task 1/2. `string.Format` für Werte mit Platzhaltern.

- [ ] **Step 1: Neuer-USB-Stick-erkannt-Dialog**

```csharp
            if (MessageBox.Show($"Neuer USB-Stick: {nd.Letter}\nLabel: {(string.IsNullOrWhiteSpace(nd.Label) ? "—" : nd.Label)}   Größe: {nd.SizeBytes / 1_073_741_824.0:F0} GB\n\nAutomatisch als Ventoy-Stick einrichten?\n\n⚠ ALLE DATEN AUF DIESEM STICK WERDEN GELÖSCHT!", "USB-Stick erkannt — Datenverlust!", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
```

ersetzen durch:

```csharp
            if (MessageBox.Show(
                string.Format(LocalizationService.T(Str.Msg_NewDriveDetected_Body),
                    nd.Letter, string.IsNullOrWhiteSpace(nd.Label) ? "—" : nd.Label, nd.SizeBytes / 1_073_741_824.0),
                LocalizationService.T(Str.Msg_NewDriveDetected_Title), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
```

`Msg_NewDriveDetected_Body` enthält bereits `{2:F0}` (siehe Task 2) — `nd.SizeBytes / 1_073_741_824.0` wird deshalb unverändert (als `double`, ohne eigene Formatierung) übergeben und von `string.Format` selbst gerundet, exakt wie zuvor mit dem inline `:F0`.

- [ ] **Step 2: Mehrere-USB-Sticks-Auswahldialog**

```csharp
            var dlg = new DriveSelectDialog(_vm.Drives,
                headerText: $"Es sind {_vm.Drives.Count} USB-Sticks angeschlossen. Mit welchem möchtest du arbeiten?",
                preselect: _vm.SelectedDrive)
            { Owner = this };
```

ersetzen durch:

```csharp
            var dlg = new DriveSelectDialog(_vm.Drives,
                headerText: string.Format(LocalizationService.T(Str.Msg_MultipleDrivesHeader), _vm.Drives.Count),
                preselect: _vm.SelectedDrive)
            { Owner = this };
```

- [ ] **Step 3: Ventoy-Installieren/Aktualisieren-Dialog**

```csharp
            string letter = target.Letter; string label = string.IsNullOrWhiteSpace(target.Label) ? "Kein Name" : target.Label; double gb = target.SizeBytes / 1_073_741_824.0;
            bool installed = UsbService.IsVentoyInstalled(letter);
            string warn = installed
                ? $"Ventoy auf\n\n   {letter}  {label}  ({gb:F0} GB)\n\naktualisieren?\n\n✅ Bestehende ISO-Dateien bleiben erhalten."
                : $"⚠ ACHTUNG — DATENVERLUST!\n\nAlle Daten auf\n\n   {letter}  {label}  ({gb:F0} GB)\n\nwerden unwiderruflich gelöscht!";
            if (MessageBox.Show(warn, installed ? "Ventoy aktualisieren" : "⚠ Ventoy installieren — Datenverlust!", MessageBoxButton.OKCancel, installed ? MessageBoxImage.Question : MessageBoxImage.Warning) != MessageBoxResult.OK) return;
```

ersetzen durch:

```csharp
            string letter = target.Letter; string label = string.IsNullOrWhiteSpace(target.Label) ? LocalizationService.T(Str.Msg_NoLabel) : target.Label; double gb = target.SizeBytes / 1_073_741_824.0;
            bool installed = UsbService.IsVentoyInstalled(letter);
            string warn = installed
                ? string.Format(LocalizationService.T(Str.Msg_VentoyUpdate_Body), letter, label, gb.ToString("F0"))
                : string.Format(LocalizationService.T(Str.Msg_VentoyInstall_Body), letter, label, gb.ToString("F0"));
            if (MessageBox.Show(warn, installed ? LocalizationService.T(Str.Msg_VentoyUpdate_Title) : LocalizationService.T(Str.Msg_VentoyInstall_Title), MessageBoxButton.OKCancel, installed ? MessageBoxImage.Question : MessageBoxImage.Warning) != MessageBoxResult.OK) return;
```

- [ ] **Step 4: Kein-USB-Laufwerk-erkannt-Dialog**

```csharp
            if (_vm.Drives.Count == 0) { MessageBox.Show("Kein USB-Laufwerk erkannt.", Constants.AppTitle, MessageBoxButton.OK, MessageBoxImage.Information); return null; }
```

ersetzen durch:

```csharp
            if (_vm.Drives.Count == 0) { MessageBox.Show(LocalizationService.T(Str.Msg_NoUsbDetected), Constants.AppTitle, MessageBoxButton.OK, MessageBoxImage.Information); return null; }
```

- [ ] **Step 5: Download-Klick — Auswahl-/Modus-/Ventoy-/Lösch-Dialoge**

```csharp
            if (queue.Count == 0) { MessageBox.Show("Bitte mindestens eine Distribution markieren!", Constants.AppTitle, MessageBoxButton.OK, MessageBoxImage.Information); return; }
            string drive = _vm.SelectedDriveLetter; bool copy = false, del = false;
            if (!string.IsNullOrEmpty(drive))
            {
                var r = MessageBox.Show($"USB-Stick erkannt: {drive}\n\nHerunterladen UND direkt auf Stick kopieren?", "Download-Modus", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (r == MessageBoxResult.Cancel) return; copy = r == MessageBoxResult.Yes;
                if (copy && !UsbService.IsVentoyInstalled(drive)) if (MessageBox.Show($"Kein Ventoy auf {drive}. Trotzdem kopieren?", "Ventoy nicht gefunden", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                if (copy) del = MessageBox.Show("Lokale Dateien nach dem Kopieren löschen?", "Dateien löschen?", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
            }
            else if (MessageBox.Show($"Kein USB-Stick.\n\nISOs gespeichert in:\n{AppPaths.Instance.DownloadDir}\n\nFortfahren?", "Kein Stick erkannt", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) return;
```

ersetzen durch:

```csharp
            if (queue.Count == 0) { MessageBox.Show(LocalizationService.T(Str.Msg_SelectAtLeastOne), Constants.AppTitle, MessageBoxButton.OK, MessageBoxImage.Information); return; }
            string drive = _vm.SelectedDriveLetter; bool copy = false, del = false;
            if (!string.IsNullOrEmpty(drive))
            {
                var r = MessageBox.Show(string.Format(LocalizationService.T(Str.Msg_DownloadMode_Body), drive), LocalizationService.T(Str.Msg_DownloadMode_Title), MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (r == MessageBoxResult.Cancel) return; copy = r == MessageBoxResult.Yes;
                if (copy && !UsbService.IsVentoyInstalled(drive)) if (MessageBox.Show(string.Format(LocalizationService.T(Str.Msg_NoVentoy_Body), drive), LocalizationService.T(Str.Msg_NoVentoy_Title), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                if (copy) del = MessageBox.Show(LocalizationService.T(Str.Msg_DeleteLocalAfterCopy_AfterCopy), LocalizationService.T(Str.Msg_DeleteFiles_Title), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
            }
            else if (MessageBox.Show(string.Format(LocalizationService.T(Str.Msg_NoStick_Body), AppPaths.Instance.DownloadDir), LocalizationService.T(Str.Msg_NoStick_Title), MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) return;
```

- [ ] **Step 6: Freispeicher-Warnung**

```csharp
                string msg = $"Die {queue.Count} ausgewählten Distros benötigen zusammen ca. {Gb(totalBytes)}" +
                             (unknownCount > 0 ? $" (bei {unknownCount} Distro(s) war die Größe online nicht ermittelbar — evtl. mehr)" : "") +
                             $",\naber auf {label} sind nur {GbMb(freeMb)} frei.\n\nTrotzdem fortfahren?";
                return MessageBox.Show(msg, "⚠ Nicht genug Speicherplatz", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
            }

            if (!WarnIfTooSmall($"dem Arbeitsordner ({AppPaths.Instance.DownloadDir})", Path.GetPathRoot(AppPaths.Instance.DownloadDir) ?? AppPaths.Instance.DownloadDir))
                return false;
            if (!string.IsNullOrEmpty(stickDrive) && !WarnIfTooSmall($"dem Stick {stickDrive}", UsbService.DriveRoot(stickDrive)))
                return false;
```

ersetzen durch:

```csharp
                string msg = string.Format(LocalizationService.T(Str.Msg_FreeSpace_Body1), queue.Count, Gb(totalBytes)) +
                             (unknownCount > 0 ? string.Format(LocalizationService.T(Str.Msg_FreeSpace_Body2), unknownCount) : "") +
                             string.Format(LocalizationService.T(Str.Msg_FreeSpace_Body3), label, GbMb(freeMb));
                return MessageBox.Show(msg, LocalizationService.T(Str.Msg_FreeSpace_Title), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
            }

            if (!WarnIfTooSmall(string.Format(LocalizationService.T(Str.Msg_FreeSpace_LabelWorkDir), AppPaths.Instance.DownloadDir), Path.GetPathRoot(AppPaths.Instance.DownloadDir) ?? AppPaths.Instance.DownloadDir))
                return false;
            if (!string.IsNullOrEmpty(stickDrive) && !WarnIfTooSmall(string.Format(LocalizationService.T(Str.Msg_FreeSpace_LabelStick), stickDrive), UsbService.DriveRoot(stickDrive)))
                return false;
```

- [ ] **Step 7: Build prüfen**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: `Build succeeded.`, keine neuen Warnungen.

- [ ] **Step 8: Commit**

```bash
git add Views/MainWindow.xaml.cs
git commit -m "feat: MainWindow MessageBox-Texte Batch 3 lokalisiert (Laufwerke, Ventoy, Download, Speicherplatz)"
```

---

### Task 11: `MainWindow.xaml.cs` — MessageBox-Batch 4 (Rest: Phase-Label, Laufwerk-Wählen-Guards, Datenbank/Kopieren)

**Files:**
- Modify: `Views/MainWindow.xaml.cs`

**Interfaces:**
- Konsumiert: `LocalizationService.T(Str.Msg_...)` aus Task 1/2.

- [ ] **Step 1: Phase-Label „Kopiere auf Stick"**

```csharp
            if (hasCopy && !hasDownload) foreach (string n in nameList) _downloadProgressDialog.SetPhaseLabel(n, "Kopiere auf Stick");
```

ersetzen durch:

```csharp
            if (hasCopy && !hasDownload) foreach (string n in nameList) _downloadProgressDialog.SetPhaseLabel(n, LocalizationService.T(Str.Msg_PhaseCopyToStick));
```

- [ ] **Step 2: „Bitte zuerst ein USB-Laufwerk auswählen!" (Integritätsprüfung)**

```csharp
            if (string.IsNullOrEmpty(_vm.SelectedDriveLetter)) { MessageBox.Show("Bitte zuerst ein USB-Laufwerk auswählen!", Constants.AppTitle, MessageBoxButton.OK, MessageBoxImage.Information); return; }
            SetBusyUi(true); await _vm.VerifyStickIntegrityAsync(); SetBusyUi(false);
```

ersetzen durch:

```csharp
            if (string.IsNullOrEmpty(_vm.SelectedDriveLetter)) { MessageBox.Show(LocalizationService.T(Str.Msg_SelectDriveFirst), Constants.AppTitle, MessageBoxButton.OK, MessageBoxImage.Information); return; }
            SetBusyUi(true); await _vm.VerifyStickIntegrityAsync(); SetBusyUi(false);
```

- [ ] **Step 3: „Bitte warten …" (Datenbank bearbeiten während IsBusy)**

```csharp
            if (_vm.IsBusy) { MessageBox.Show("Bitte warten …"); return; }
```

ersetzen durch:

```csharp
            if (_vm.IsBusy) { MessageBox.Show(LocalizationService.T(Str.Msg_PleaseWait)); return; }
```

- [ ] **Step 4: „Bitte zuerst ein USB-Laufwerk auswählen!" + „Keine lokal heruntergeladenen ISOs vorhanden." + Lösch-Nachfrage (Verpasste Kopien nachholen)**

```csharp
            if (string.IsNullOrEmpty(_vm.SelectedDriveLetter)) { MessageBox.Show("Bitte zuerst ein USB-Laufwerk auswählen!", Constants.AppTitle, MessageBoxButton.OK, MessageBoxImage.Information); return; }
            List<IsoEntry> queue = _vm.GetLocallyAvailableEntries();
            if (queue.Count == 0) { MessageBox.Show("Keine lokal heruntergeladenen ISOs vorhanden.", Constants.AppTitle, MessageBoxButton.OK, MessageBoxImage.Information); return; }
            bool del = MessageBox.Show("Lokale Dateien nach dem Kopieren löschen?", "Dateien löschen?", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
```

ersetzen durch:

```csharp
            if (string.IsNullOrEmpty(_vm.SelectedDriveLetter)) { MessageBox.Show(LocalizationService.T(Str.Msg_SelectDriveFirst), Constants.AppTitle, MessageBoxButton.OK, MessageBoxImage.Information); return; }
            List<IsoEntry> queue = _vm.GetLocallyAvailableEntries();
            if (queue.Count == 0) { MessageBox.Show(LocalizationService.T(Str.Msg_NoLocalIsos), Constants.AppTitle, MessageBoxButton.OK, MessageBoxImage.Information); return; }
            bool del = MessageBox.Show(LocalizationService.T(Str.Msg_DeleteLocalAfterCopy_AfterCopy), LocalizationService.T(Str.Msg_DeleteFiles_Title), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
```

- [ ] **Step 5: Build prüfen**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: `Build succeeded.`, keine neuen Warnungen.

- [ ] **Step 6: Verifikations-Grep über die ganze Datei**

Run: `grep -n "MessageBox.Show(\"" Views/MainWindow.xaml.cs`
Expected: keine Treffer mehr — jede `MessageBox.Show(...)`-Aufrufstelle mit hartcodiertem deutschem String-Literal als erstem Argument sollte jetzt durch `LocalizationService.T(...)` bzw. `string.Format(...)` ersetzt sein. (Aufrufe wie `MessageBox.Show(msg, ...)` oder `MessageBox.Show(sb.ToString(), ...)` mit bereits lokalisierter Variable als erstem Argument sind erwartungsgemäß weiterhin vorhanden und kein Fund.)

- [ ] **Step 7: Commit**

```bash
git add Views/MainWindow.xaml.cs
git commit -m "feat: MainWindow MessageBox-Texte Batch 4 lokalisiert (Phase-Label, Laufwerk-Guards)"
```

---

### Task 12: `Constants.cs` — Kategorie-Namen

**Files:**
- Modify: `Core/Models/Constants.cs`

**Interfaces:**
- Konsumiert: `LocalizationService.T(Str.Category_...)` aus Task 1/2.
- Produziert: nichts, das andere Tasks konsumieren — `CategoryLabel(string)` wird bereits von `IsoCategoryViewModel` (Task 7) aufgerufen, ohne dass sich dessen Aufruf-Signatur ändert.

- [ ] **Step 1: `CategoryLabel(string)` umstellen**

```csharp
        public static string CategoryLabel(string category) => category switch
        {
            "Gaming"           => "🎮 Gaming",
            "Sicherheit"       => "🔒 Sicherheit & Privatsphäre",
            "Einsteiger"       => "💻 Einsteiger (Komfort & Design)",
            "Leichtgewicht"    => "🪶 Leichtgewicht (Geschwindigkeit & Effizienz)",
            "Fortgeschrittene" => "⚙ Fortgeschrittene (Unabhängigkeit & Stabilität)",
            "Rettung"          => "🛠 Rettung (Backup & Wiederherstellung)",
            "Antivirus"        => "🛡 Antivirus (Schutz & Bereinigung)",
            "WinPE"            => "🪟 WinPE (Windows-Tools)",
            _                  => category
        };
```

ersetzen durch:

```csharp
        public static string CategoryLabel(string category) => category switch
        {
            "Gaming"           => LocalizationService.T(Str.Category_Gaming),
            "Sicherheit"       => LocalizationService.T(Str.Category_Security),
            "Einsteiger"       => LocalizationService.T(Str.Category_Beginner),
            "Leichtgewicht"    => LocalizationService.T(Str.Category_Lightweight),
            "Fortgeschrittene" => LocalizationService.T(Str.Category_Advanced),
            "Rettung"          => LocalizationService.T(Str.Category_Rescue),
            "Antivirus"        => LocalizationService.T(Str.Category_Antivirus),
            "WinPE"            => LocalizationService.T(Str.Category_WinPE),
            _                  => category
        };
```

Die `switch`-Schlüssel selbst (`"Gaming"`, `"Sicherheit"`, …) bleiben unverändert — das sind interne, sprachneutrale Kategorie-IDs (siehe `Categories`-Array direkt darüber), keine sichtbaren Texte.

- [ ] **Step 2: `using`-Eintrag prüfen**

`Core/Models/Constants.cs` hat aktuell nur `using System.Reflection;`. `LocalizationService`/`Str` liegen in `ULM.Infrastructure` — da `Constants.cs` selbst im Namespace `ULM.Core.Models` liegt (nicht `ULM.Infrastructure`), am Dateianfang ergänzen:

```csharp
using System.Reflection;
```

ersetzen durch:

```csharp
using System.Reflection;
using ULM.Infrastructure;
```

- [ ] **Step 3: Build prüfen**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: `Build succeeded.`, keine neuen Warnungen.

- [ ] **Step 4: Volle Testsuite laufen lassen**

Run: `dotnet test ULM.Tests`
Expected: alle Tests grün, inklusive `LocalizationServiceCompletenessTests` (deckt jetzt alle 172 `Str`-Werte ab).

- [ ] **Step 5: Commit**

```bash
git add Core/Models/Constants.cs
git commit -m "feat: Kategorie-Namen lokalisiert"
```

---

### Task 13: Volle Testsuite + manuelle Zweisprachigkeits-Verifikation

**Files:** keine Code-Änderungen — reine Verifikation.

**Interfaces:** keine.

- [ ] **Step 1: Volle Testsuite laufen lassen**

Run: `dotnet test ULM.Tests`
Expected: alle Tests grün.

- [ ] **Step 2: Deutsch — Regressionscheck**

`ulm_settings.ini`: `Language = de`. Hauptfenster starten. Erwartet: keine sichtbare Abweichung vom Stand vor diesem Plan — Header-Untertitel, Chip-Labels, Toolbar, Spaltenüberschriften, Status-Tab, Experten-Buttons, Kategorie-Namen, Zeilen-Status alle unverändert Deutsch.

- [ ] **Step 3: Englisch — Rahmen, Toolbar, Spalten**

`Language = en`. Prüfen: Untertitel „Set up USB stick · Manage Linux ISOs · Monitor downloads", „TARGET USB DRIVE"-Label, „⚡ Install Ventoy"-Button, 4 Spaltenüberschriften „Linux Distribution (check = download)"/„Local"/„On Stick"/„Current", „Clear Log"-Button. Keine abgeschnittenen/überlappenden Labels (englische Texte i.d.R. kürzer).

- [ ] **Step 4: Englisch — Status-Tab inkl. zustandsabhängiger Texte**

Im Status-Tab: „Current Operation", die 6 „Operation:/File:/Progress:/Detail:/Counter:/Target drive:"-Labels, „Automatic Background Scans", „Scheduled Automatic Actions", „History"/„Clear History". Einen Online-Versionscheck auslösen (z.B. „🔄 Updates prüfen" klicken oder automatischen Start-Check abwarten) und dabei live beobachten: Text wechselt zwischen „running …" und „inactive" je nach `OnlineScanActive`/`UsbScanActive`, UND der „Aktueller Vorgang"-Bereich zeigt währenddessen den passenden „🌐 Automatic online version check running — …"-Satz statt „No operation active.".

- [ ] **Step 5: Englisch — Experten-Aktionsleiste, Kategorien, Zeilen-Status**

Experten-Modus aktivieren (falls nicht schon aktiv). Die 7 Aktions-Buttons + Tooltips (Hover prüfen) auf Englisch. Kategorie-Überschriften in der Distro-Liste — alle 8 auf Englisch (z.B. „🔒 Security & Privacy"). Bei mindestens 2-3 Distro-Zeilen mit unterschiedlichem Status (lokal vorhanden, auf Stick, veraltet, Update verfügbar) die Spalten „Local"/„On Stick"/„Current" auf korrekte englische Übersetzung UND korrekt eingebettete Werte prüfen (z.B. „Local 1234 MB", „Outdated  1.2 GB", „Update v6.2").

- [ ] **Step 6: Englisch — Update-Banner (falls praktikabel auslösbar)**

Falls ein Update verfügbar gemacht werden kann (z.B. `LastSeenVersion`/Versionscheck-Mechanismus): Banner-Text und Button-Text in allen 3 Zuständen (verfügbar/lädt/bereit) auf Englisch prüfen. Falls nicht praktikabel ohne echten Netzwerk-Trigger: stattdessen Code-Review von `SetAvailableUpdate`/`SetUpdateDownloading`/`SetUpdateReadyToInstall` als Ersatzverifikation dokumentieren.

- [ ] **Step 7: Englisch — mindestens 2 MessageBox-Dialoge mit mehreren eingebetteten Werten**

Mindestens 2 der `string.Format`-basierten Dialoge gezielt auslösen (z.B. „Bitte mindestens eine Distribution markieren!" durch Download ohne Auswahl, oder die Speicherplatz-Warnung durch Download vieler großer Distros auf ein kleines Ziellaufwerk) und auf natürliche englische Wortstellung UND korrekt eingesetzte Werte prüfen (keine vertauschten `{0}`/`{1}`).

- [ ] **Step 8: Bei Erfolg — nichts weiter zu tun**

Falls einer der Punkte in Step 2–7 nicht stimmt, zurück zu Phase 1 der systematic-debugging-Skill (neue Evidenz sammeln, nicht direkt erneut fixen).

---

## Self-Review

**Spec-Abdeckung:**
- Alle 128 Textstellen aus der Spec-Bestandsaufnahme (5 Gruppen: `MainWindow.xaml` Rahmen+Status, `MainWindow.xaml.cs` MessageBoxes, `MainViewModel.cs` Banner, `IsoViewModels.cs` Zeilen-Status, `Constants.cs` Kategorien) → Task 1 (Enum) + Task 2 (Übersetzungen) + Task 3–12 (Verwendung). ✅
- Architektur-Korrektur (x:Name + `ApplyLocalizedText()` für statische XAML-Texte, `MainViewModel`-Properties statt `DataTrigger` für zustandsabhängige Texte, `IsoEntryViewModel`/`IsoCategoryViewModel`-Properties statt statischem `ToolTip` für Vorlagen-Tooltips) → Task 3–7, jeweils mit expliziter Begründung. ✅
- `string.Format`-Entscheidung für mehrere eingebettete Werte → durchgängig in Task 6, 8, 9, 10 angewendet, mit Wortstellungs-Anpassung in den englischen Übersetzungen (z.B. `Msg_StickOutdatedFound`). ✅
- Duplikate zusammengeführt (`Main_Btn_Dismiss`, `Msg_DeleteFiles_Title`, `Msg_SelectDriveFirst`, `Msg_DeleteLocalAfterCopy_AfterCopy`, `Main_Status_Running`/`Inactive`, `Row_Yes`) → in den jeweiligen Tasks als Wiederverwendung derselben `Str`-Werte umgesetzt, kein Duplikat neu angelegt. ✅
- `StatusBracket` unangetastet, Log-/Aktivitätsverlauf + `StatusText` (inkl. `MainWindow.xaml.cs:644`) komplett außerhalb dieses Plans → in keinem Task berührt, explizit in den Global Constraints benannt. ✅
- Manuelle Verifikation deckt alle 3 Architekturmuster ab (statisch, zustandsabhängig, Vorlagen-Tooltip) sowie mehrere `string.Format`-Fälle → Task 13. ✅

**Platzhalter-Scan:** Keine „TBD"/„implement later"/unvollständigen Code-Blöcke. Zwei Stellen enthalten bewusste Hinweise statt exaktem Zeilen-Zitat, wo das Ausgangs-Snippet beim Schreiben dieses Plans nicht wortwörtlich re-verifiziert wurde (Task 7 Step 2, `HashStatusTooltip`-Umgebungsstruktur) — dort wird die Ersetzungsregel (nur die 3 String-Literale, umgebende Struktur beibehalten) explizit statt eines geratenen Zeilen-Zitats angegeben, um keinen falschen Diff vorzutäuschen.

**Typkonsistenz:** Alle 128 `Str`-Werte werden in Task 1 exakt so benannt, wie sie in Task 2 (Dictionary-Keys) und Task 3–12 (`LocalizationService.T(Str.X)`-Aufrufe) verwendet werden — Namen wurden beim Schreiben dieses Plans direkt gegen den vollständig gelesenen Quellcode (`MainWindow.xaml`, `MainWindow.xaml.cs`, `MainViewModel.cs`, `IsoViewModels.cs`, `Constants.cs`, Stand Commit `0fb3859`) geprüft. Neue Properties (`CurrentOperationStatusText`, `OnlineCheckStatusText`, `UsbCheckStatusText`, `ManualSearchTooltip`, `SelectAllTooltip`) werden in Task 6/7 definiert und in denselben Tasks unmittelbar per `{Binding}` verwendet — keine Cross-Task-Abhängigkeit auf diese neuen Namen.

