# Zweisprachigkeit — Phase 3: Hauptfenster komplett lokalisieren — Design

## Kontext

Phase 1 (`docs/superpowers/specs/2026-07-22-bilingual-ui-infrastructure-design.md`)
hat nur eine Beispiel-Migration eines kleinen Teils des Hauptfenster-Rahmens
gemacht (9 Buttons/Tabs). Phase 2
(`docs/superpowers/specs/2026-07-23-setupdialog-localization-design.md`) hat
`SetupDialog` vollständig lokalisiert. Eine reale Verifikation mit
`Language = en` zeigte danach: der überwiegende Teil des Hauptfensters selbst
ist noch komplett Deutsch — Untertitel, Ziel-USB-Feld, Spaltenüberschriften,
Status-Tab, die 7 Experten-Aktions-Buttons samt Tooltips, Update-/Härtefall-
Banner, die Zeilen-Status-Texte der Distro-Liste und die Kategorie-Namen.

**Wunsch:** die komplette App soll am Ende in Deutsch UND Englisch nutzbar
sein. Das ist zu groß für einen Plan — diese Spec deckt **Phase 3: das
Hauptfenster** ab. Weitere, bereits grob abgestimmte Phasen:

- **Phase 4:** Log-/Aktivitätsverlauf (`MainViewModel.Log(...)`, ~85
  Aufrufstellen) + das rotierende `StatusText` (29 Aufrufstellen) — bewusst
  NICHT Teil von Phase 3, da es ein grundsätzlich anderes technisches Muster
  ist (viele verstreute Aufrufstellen mit Laufzeitwerten mitten in der
  Geschäftslogik, nicht wenige feste Vorlagen).
- **Phase 5:** `HelpDialog.cs` (~165 deutsche String-Literale, für sich schon
  groß genug für eine eigene Phase).
- **Phase 6:** übrige Dialoge (`DownloadDialogs.cs`, `DatabaseDialogs.cs`,
  `ChangelogDialog.cs`, `ManualSourceSearchDialog.cs`,
  `UpdateDownloadDialog.cs`, `VentoyInstallWindow.cs`).
- **Phase 7:** Fehlermeldungen aus `Core/Services/*.cs`.

## Bestandsaufnahme

Vollständige Inventur (jede der 4 betroffenen Dateien komplett gelesen, nicht
nur stichprobenartig — nach der übersehenen Arbeitsordner-Kartenüberschrift
in Phase 2 bewusst gründlicher):

| Datei | Live-Textstellen |
|---|---|
| `Views/MainWindow.xaml` | 64 |
| `Views/MainWindow.xaml.cs` | 47 Vorkommen (~44 unterschiedliche Texte nach Zusammenfassen von Duplikaten) |
| `ViewModels/MainViewModel.cs` (nur Update-/Härtefall-Banner, NICHT Log/StatusText) | 5 |
| `ViewModels/IsoViewModels.cs` | 25 Vorkommen (16 `Str`-Werte nach Wiederverwendung gemeinsamer Wörter) |
| `Core/Models/Constants.cs` | 8 |

Explizit **ausgeschlossen** (nicht Teil dieser Phase):

- `MainViewModel.Log(...)`-Aufrufe (~85 Stellen) und `StatusText = "..."`
  (29 Stellen) → Phase 4.
- `MainWindow.xaml.cs:644` (`StatusLbl.Text = "✅ Ventoy-Stick: {letter}"`) —
  setzt zwar in dieser Datei, aber dasselbe rotierende Status-Label wie
  `StatusText` → ebenfalls Phase 4.
- `IsoViewModels.cs`-Property `StatusBracket` (Zeilen 126–132) — wird
  nirgends gebunden/verwendet (nur eigene Deklaration + toter
  `OnPropertyChanged`-Aufruf). Bleibt unangetastet; totes-Code-Aufräumen ist
  ein separates Thema, nicht Teil einer Übersetzungs-Phase.
- Reine Interpunktion/Symbole ohne Wortbedeutung (`%`, `(`, `)`, das
  Logo-Kürzel `"UL"`, der bereits englische Header-Text `"Universal Linux
  Manager"` an `MainWindow.xaml:155` — deckt sich mit `Constants.AppTitle`
  und braucht keine Übersetzung).

## Entscheidungen (im Brainstorming geklärt)

- **Umfang:** alles auf einmal — Hauptfenster-Rahmen + Kategorie-Namen +
  Zeilen-Status-Texte + die kurzen Update-/Härtefall-Banner-Vorlagen aus
  `MainViewModel.cs` (letztere technisch genauso einfach wie die
  MessageBox-Texte: 1–3 feste Aufrufstellen pro Property, kein Teil des
  sprawlenden Log-/StatusText-Systems).
- **Kategorie-Namen werden übersetzt** (z.B. „🔒 Security & Privacy" statt
  sprachneutral Deutsch belassen).
- **Duplikate zusammenfassen:** wiederkehrende Texte (z.B. „Dateien
  löschen?" an 3 Stellen, „Bitte zuerst ein USB-Laufwerk auswählen!" an 2
  Stellen, „Ja"/„Nein"/„Ungeprüft" in mehreren `IsoViewModels`-Properties)
  bekommen JE EINEN `Str`-Wert, an mehreren Call-Sites wiederverwendet —
  keine Kopien.
- **Neu: `string.Format(...)` für mehrere eingebettete Laufzeitwerte.**
  Phase 2 hat für einen einzelnen angehängten technischen Text (`ex.Message`)
  bewusst einfache Verkettung statt eines neuen `T()`-Parameters gewählt. Für
  Texte mit **mehreren, grammatikalisch mitten im Satz eingebetteten**
  Werten (z.B. „Auf {Laufwerk} wurden {Anzahl} veraltete ISO(s) gefunden:")
  ist reine Verkettung nicht praktikabel — die Wortstellung unterscheidet
  sich zwischen Deutsch und Englisch. Lösung: `Str`-Werte enthalten
  `{0}`/`{1}`-Platzhalter, Aufrufer verwenden den **eingebauten
  .NET-Standardmechanismus** `string.Format(LocalizationService.T(Str.X),
  arg1, arg2)`. Das ist KEINE Änderung an `LocalizationService.T()` selbst
  (bleibt exakt wie in Phase 1 gebaut) — nur eine übliche Anwendung von
  `string.Format` auf das Ergebnis, dasselbe Muster, das der Rest des
  Projekts an vielen Stellen bereits für andere Zwecke nutzt.
- **`StatusBracket` bleibt unangetastet** (totes Code, siehe oben).
- **`MainWindow.xaml.cs:644` und die generelle Status-/Log-Maschinerie**
  bleiben Phase 4.

## Architektur

### `Infrastructure/Str.cs` — neue Einträge (125 neue Werte, 5 Gruppen)

```csharp
// ── Hauptfenster: Rahmen, Toolbar, Spalten, Status-Tab ──────────────
Main_Tooltip_ManualSearch,
Main_Tooltip_CategorySelectAll,
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

// ── Hauptfenster Code-Behind: MessageBox-/Dialog-Texte ──────────────
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

// ── Update-/Härtefall-Banner (MainViewModel.cs) ─────────────────────
Banner_UpdateAvailable,
Banner_UpdateDownloading,
Banner_UpdateReady,
Banner_HardCaseSingle,
Banner_HardCasePlural,

// ── Distro-Zeilen: Status-Texte + Tooltips (IsoViewModels.cs) ───────
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

// ── Kategorie-Namen (Constants.cs) ──────────────────────────────────
Category_Gaming,
Category_Security,
Category_Beginner,
Category_Lightweight,
Category_Advanced,
Category_Rescue,
Category_Antivirus,
Category_WinPE,
```

### Übersetzungen (Auszug der Muster, vollständige Liste im Implementierungsplan)

Statische 1:1-Texte laufen wie bisher über `LocalizationService.T(Str.X)`.
Texte mit einem einzelnen angehängten technischen Wert bleiben bei
Verkettung (Phase-2-Muster). Texte mit mehreren eingebetteten Werten nutzen
`{0}`/`{1}`-Platzhalter + `string.Format(...)`:

```csharp
// Einfach (kein eingebetteter Wert):
[Str.Main_Label_TargetDrive] = "ZIEL-USB-LAUFWERK",              // DE
[Str.Main_Label_TargetDrive] = "TARGET USB DRIVE",                // EN

// Ein angehängter technischer Wert → Verkettung (wie Phase 2):
LocalizationService.T(Str.Main_Footer_IsoFolder) → "ISO-Ordner: {0}" / "ISO folder: {0}"
FooterLbl.Text = string.Format(LocalizationService.T(Str.Main_Footer_IsoFolder), _paths.DownloadDir);

// Mehrere eingebettete, grammatikalisch verschränkte Werte → string.Format:
[Str.Msg_StickOutdatedFound] = "Auf {0} wurden {1} veraltete ISO(s) gefunden:",          // DE
[Str.Msg_StickOutdatedFound] = "{1} outdated ISO(s) found on {0}:",                       // EN (Wortstellung angepasst!)
sb.AppendLine(string.Format(LocalizationService.T(Str.Msg_StickOutdatedFound), drive, outdated.Count));

// Wiederverwendung gemeinsamer Wörter (Row_Yes an 2 Stellen in IsoViewModels.cs):
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
        if (_entry.HasResolvedUpdate)     return $"{LocalizationService.T(Str.Row_UpdatePrefix)} v{_entry.RemoteVersion}";
        if (_entry.HasOnlineVersionInfo)  return $"{LocalizationService.T(Str.Row_CurrentPrefix)} (v{_entry.RemoteVersion})";
        if (_entry.UsbStatus == Core.Models.UsbStatus.Ok) return LocalizationService.T(Str.Row_Yes);
        if (_entry.IsLocallyAvailable(_downloadDir))       return LocalizationService.T(Str.Row_LocallyAvailable);
        return "?"; // sprachneutrales Symbol, kein Str-Wert nötig
    }
}
```

Die vollständige Deutsch/Englisch-Textliste aller 125 Werte wird im
Implementierungsplan Task für Task mit exaktem Vorher-/Nachher-Code
mitgeliefert (wie in Phase 2) — hier nur das Architekturmuster.

### Betroffene Dateien

- `Infrastructure/Str.cs` — 125 neue Enum-Werte.
- `Infrastructure/LocalizationService.cs` — je ein DE- und EN-Eintrag pro
  neuem Wert.
- `Views/MainWindow.xaml` — 52 Ersetzungen (Buttons, Labels, Tooltips,
  Spaltenüberschriften, Status-Tab-Texte).
- `Views/MainWindow.xaml.cs` — 44 Ersetzungen (MessageBox-Dialoge,
  Footer-Text; teilt `Banner_HardCaseSingle` mit `MainViewModel.cs`, da
  derselbe Text an beiden Stellen verwendet wird).
- `ViewModels/MainViewModel.cs` — 5 Ersetzungen, NUR die drei
  `UpdateBannerText`- und zwei `HardCaseBannerText`-Zuweisungen. Keine
  Berührung von `Log(...)`/`StatusText` (Phase 4).
- `ViewModels/IsoViewModels.cs` — 16 Ersetzungen in `LocalStatus`,
  `UsbStatus`, `VersionStatus`, `HashStatusTooltip`, `TipTooltip`.
  `StatusBracket` bleibt unangetastet.
- `Core/Models/Constants.cs` — `CategoryLabel(string)` komplett auf
  `LocalizationService.T(Str.Category_...)` umgestellt.

## Testing

- Der bestehende Vollständigkeitstest
  (`LocalizationServiceCompletenessTests.AllStrValues_HaveGermanAndEnglishTranslation`)
  deckt alle 125 neuen Werte automatisch ab.
- Ein paar neue Spot-Tests für `string.Format(...)`-Fälle (z.B.
  `Msg_StickOutdatedFound`, `Row_UpdatePrefix`) — bestätigen, dass
  Platzhalter UND Wortstellung in beiden Sprachen korrekt sind (das ist die
  eine Stelle, wo ein Test tatsächlich einen Logikfehler abfangen könnte,
  z.B. vertauschte `{0}`/`{1}`).
- Kein UI-Automatisierungstest (unveränderte Projekt-Konvention).

## Manuelle Verifikation

Wie in Phase 2: `ulm_settings.ini` einmal mit `Language = de` (Regressions-
check) und einmal mit `Language = en` starten. Diesmal zusätzlich gezielt:

1. Update-Banner erzwingen (z.B. `LastSeenVersion` in der ini künstlich
   niedriger setzen als die tatsächliche Version) → Banner-Text auf
   Englisch prüfen, alle drei Zustände (verfügbar/lädt/bereit) falls
   praktikabel durchspielen.
2. Härtefall-Banner erzwingen (schwieriger, ggf. nur Code-Review statt
   Live-Trigger, falls kein einfacher manueller Auslöser existiert).
3. Distro-Zeilen-Status in verschiedenen Zuständen prüfen (lokal
   vorhanden/nicht lokal, auf Stick/nicht auf Stick/veraltet, aktuell/
   Update verfügbar) — Reihenfolge der `string.Format`-Platzhalter im
   Englischen korrekt?
4. Mindestens 2–3 der MessageBox-Dialoge mit mehreren eingebetteten Werten
   gezielt auslösen (z.B. Speicherplatz-Warnung, Stick-Aktualisierung) und
   auf natürliche englische Wortstellung prüfen.
5. Kategorie-Überschriften in der Liste — alle 8 auf Englisch.

## Offene Fragen für spätere Phasen (nicht jetzt entscheiden)

- Phase 4 (Log-/Aktivitätsverlauf + `StatusText`) ist die technisch
  aufwendigste verbleibende Phase — viele Aufrufstellen mit Laufzeitwerten
  mitten in der Geschäftslogik, vermutlich ebenfalls `string.Format`-lastig
  wie hier in Phase 3 gelernt.
- Phase 5 (`HelpDialog.cs`) und Phase 6 (übrige Dialoge) folgen demselben
  Muster wie Phase 2/3, keine neuen architektonischen Fragen erwartet.
