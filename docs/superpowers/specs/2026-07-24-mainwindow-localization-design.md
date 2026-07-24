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
- **Neu entdeckt beim genauen Lesen aller 5 Dateien: `UpdateBannerButtonText`**
  (3 feste Texte am Update-Banner-Button: „⬇ Herunterladen …", „⬇ Wird
  heruntergeladen …", „✅ Jetzt installieren & neu starten") war in der
  ursprünglichen Bestandsaufnahme nicht erfasst — kommt dazu (128 statt 125
  `Str`-Werte insgesamt).
- **Architektur-Korrektur für `MainWindow.xaml` (XAML, nicht C#):** Anders
  als `SetupDialogs.cs` (reiner C#-Code, `LocalizationService.T(...)` direkt
  im Ausdruck aufrufbar) ist `MainWindow.xaml` deklaratives Markup — ein
  XAML-Attribut wie `Text="..."` kann keine C#-Methode aufrufen. Drei
  Fallgruppen, alle bereits etablierte Muster aus Phase 1/2:
  1. **Statische Texte ohne `x:Name`:** Element bekommt ein neues `x:Name`
     (≈30 Elemente betroffen — Spaltenüberschriften, Status-Tab-Labels,
     Sektions-Überschriften etc.), `ApplyLocalizedText()` (bereits
     bestehende Methode in `MainWindow.xaml.cs`, aktuell für `BtnDownload`
     & Co.) setzt `Text`/`Content`/`ToolTip` nach `InitializeComponent()`.
     Bereits benannte Elemente (Buttons/Checkboxes mit Click-Handler)
     brauchen kein neues `x:Name`, nur eine neue Zeile in
     `ApplyLocalizedText()`.
  2. **Zustandsabhängige Texte in `DataTrigger`/`Setter`** (3 Stellen: „Kein
     Vorgang aktiv." + 2 Alternativtexte; „läuft …"/„inaktiv" ×2 Paare) —
     ein `Setter Property="Text" Value="..."` kann `T(...)` nicht aufrufen.
     Werden durch neue berechnete Properties auf `MainViewModel` ersetzt
     (`{Binding NeuePropertyName}` statt `DataTrigger`), exakt das Muster
     des bereits bestehenden `ScanHintText` (ebenfalls ein aus zwei Bools
     berechneter String).
  3. **Tooltips in Zeilen-/Kategorie-Vorlagen** (`Quelle manuell
     suchen/eintragen`, `Alle Distros dieser Kategorie an-/abwählen`) —
     diese `DataTemplate`s werden pro Zeile/Kategorie mehrfach instanziiert,
     `ApplyLocalizedText()` liefe nur einmal fürs Fenster. Werden zu
     berechneten Properties auf `IsoEntryViewModel`/`IsoCategoryViewModel`
     (exakt das Muster von `TipTooltip`/`HashStatusTooltip`, die dort schon
     existieren) und per `{Binding}` statt festem `ToolTip="..."` gebunden.
     Deshalb umbenannt zu `Row_ManualSearchTooltip`/
     `Row_CategorySelectAllTooltip` und der `IsoViewModels.cs`-Gruppe
     zugeordnet statt der `MainWindow.xaml`-Gruppe.
- **`string.Format(...)` für mehrere eingebettete Laufzeitwerte.**
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

### `Infrastructure/Str.cs` — neue Einträge (128 neue Werte, 5 Gruppen)

```csharp
// ── Hauptfenster: Rahmen, Toolbar, Spalten, Status-Tab ──────────────
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
Banner_UpdateBtn_Available,
Banner_UpdateBtn_Downloading,
Banner_UpdateBtn_ReadyToInstall,
Banner_HardCaseSingle,
Banner_HardCasePlural,

// ── Distro-Zeilen: Status-Texte + Tooltips (IsoViewModels.cs) ───────
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

Die vollständige Deutsch/Englisch-Textliste aller 128 Werte wird im
Implementierungsplan Task für Task mit exaktem Vorher-/Nachher-Code
mitgeliefert (wie in Phase 2) — hier nur das Architekturmuster.

### Betroffene Dateien

- `Infrastructure/Str.cs` — 128 neue Enum-Werte.
- `Infrastructure/LocalizationService.cs` — je ein DE- und EN-Eintrag pro
  neuem Wert.
- `Views/MainWindow.xaml` — 50 Ersetzungen (Buttons, Labels,
  Spaltenüberschriften; ≈30 Elemente bekommen dabei ein neues `x:Name`,
  siehe Architektur-Korrektur oben). Die 3 zustandsabhängigen Texte werden
  von `DataTrigger`/`Setter` auf `{Binding}` gegen neue
  `MainViewModel`-Properties umgestellt (kein XAML-Literal mehr, siehe
  unten). Die 2 Zeilen-/Kategorie-Tooltips wandern zu
  `ViewModels/IsoViewModels.cs`.
- `Views/MainWindow.xaml.cs` — 44 Ersetzungen (MessageBox-Dialoge,
  Footer-Text; teilt `Banner_HardCaseSingle` mit `MainViewModel.cs`, da
  derselbe Text an beiden Stellen verwendet wird).
- `ViewModels/MainViewModel.cs` — 6 direkte `Str`-Ersetzungen (3×
  `UpdateBannerText`, 3× `UpdateBannerButtonText`, 2× `HardCaseBannerText`
  — macht zusammen 8 Zuweisungen für die 8 `Banner_*`-Werte) PLUS 3 neue
  berechnete Properties, die die `MainWindow.xaml`-`DataTrigger`-Texte
  ersetzen (verwenden `Main_Status_NoOperation`/`OnlineScanRunning`/
  `UsbScanRunning`/`Running`/`Inactive` intern). Keine Berührung von
  `Log(...)`/`StatusText` (Phase 4).
- `ViewModels/IsoViewModels.cs` — 16 Ersetzungen in `LocalStatus`,
  `UsbStatus`, `VersionStatus`, `HashStatusTooltip`, `TipTooltip`, PLUS 2
  neue berechnete Properties (`ManualSearchTooltip` auf
  `IsoEntryViewModel`, `SelectAllTooltip` auf `IsoCategoryViewModel`) für
  die aus `MainWindow.xaml` verschobenen Zeilen-/Kategorie-Tooltips.
  `StatusBracket` bleibt unangetastet.
- `Core/Models/Constants.cs` — `CategoryLabel(string)` komplett auf
  `LocalizationService.T(Str.Category_...)` umgestellt.

## Testing

- Der bestehende Vollständigkeitstest
  (`LocalizationServiceCompletenessTests.AllStrValues_HaveGermanAndEnglishTranslation`)
  deckt alle 128 neuen Werte automatisch ab.
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

## Nachtrag (nach Implementierung)

Zwei Strings wurden im ursprünglichen Inventar übersehen und erst nach den 12 Plan-Tasks
gefunden — jeweils dasselbe technische Muster wie die bereits erfassten Banner-Texte
(`MainViewModel`-Property, kein `x:Name`/`ApplyLocalizedText()` nötig):

- **`ScanHintText`** ("Online-Scan, bitte warten"/"Stick-Scan, bitte warten", der rotierende
  Hinweistext neben dem Start-Spinner) — vom Nutzer beim manuellen Testen gefunden. Ergänzt um
  `Str.Main_ScanHint_Online`/`Main_ScanHint_Usb` (Commit `85ce38e`).
- **`DriveInfoText`** ("⚠ Kein Ventoy"/"Frei: " im Laufwerk-Info-Text neben der Stick-Auswahl) —
  beim finalen Whole-Branch-Review per Keyword-Sweep gefunden. Ergänzt um
  `Str.Main_DriveInfo_NoVentoy`/`Main_DriveInfo_FreeLabel` (Commit `ef6a9d6`). "✅ Ventoy" bleibt
  hartcodiert (Markenname, identisch in beiden Sprachen).

Zusätzlich wurde ein echter Laufzeit-Bug bei der Umsetzung von Task 6 gefunden: `Run.Text`
bindet in WPF standardmäßig `TwoWay` (anders als `TextBlock.Text`), was bei den zwei neuen
`{Binding OnlineCheckStatusText}`/`{Binding UsbCheckStatusText}`-Bindings zu einem Absturz beim
Programmstart führte, da die Properties schreibgeschützt sind. Behoben durch explizites
`Mode=OneWay` (Commit `dce2a03`) — alle anderen `Run`-Bindings in der Datei hatten dieses Mode
bereits, die Konvention wurde beim Schreiben des Plans übersehen.

Die tatsächliche Gesamtzahl neuer `Str`-Werte für Phase 3 ist damit 132 (128 aus dem
ursprünglichen Plan + 4 Nachträge), Gesamtstand nach Phase 3: 176.

## Offene Fragen für spätere Phasen (nicht jetzt entscheiden)

- Phase 4 (Log-/Aktivitätsverlauf + `StatusText`) ist die technisch
  aufwendigste verbleibende Phase — viele Aufrufstellen mit Laufzeitwerten
  mitten in der Geschäftslogik, vermutlich ebenfalls `string.Format`-lastig
  wie hier in Phase 3 gelernt.
- Phase 5 (`HelpDialog.cs`) und Phase 6 (übrige Dialoge) folgen demselben
  Muster wie Phase 2/3, keine neuen architektonischen Fragen erwartet.
