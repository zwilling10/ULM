# Log-Tab + Status-Tab-Verlauf Lokalisierung Implementation Plan

> **Abweichung vom Standardablauf:** Dieser Plan wird NICHT per subagent-driven-development mit
> frischen Implementer-Subagenten pro Task ausgeführt, sondern direkt vom Controller (Claude in
> dieser Session) umgesetzt — auf ausdrücklichen Nutzerwunsch, um das monatliche Ausgabenlimit zu
> schonen (siehe Konversation und `docs/superpowers/specs/2026-07-24-log-history-localization-design.md`,
> Abschnitt „Abweichung vom bisherigen Vorgehen"). Deshalb transkribiert dieser Plan NICHT jede
> einzelne der ~150 betroffenen Meldungen vorab wörtlich (anders als die Pläne der Phasen 3 und 5) —
> der Controller liest jeden betroffenen Codeabschnitt unmittelbar vor der Bearbeitung selbst,
> wodurch eine vorab eingefrorene Transkription sowohl redundant als auch (wie der Task-5-Zwischenfall
> in Phase 5 gezeigt hat) ein Risiko für stille Textabweichungen wäre. Jeder Task listet stattdessen:
> exakten Dateibereich, Namenskonvention, 2-3 vollständig ausgearbeitete Musterbeispiele (inkl.
> Sonderfälle wie Ternary-Verzweigungen und Dedup) sowie präzise, prüfbare Abnahmekriterien.
> Reviews erfolgen nach thematisch gruppierten Blöcken (nicht nach jeder Einzeländerung) durch einen
> Reviewer-Subagenten; der abschließende Whole-Branch-Review läuft wie gewohnt auf dem leistungsfähigsten Modell.

**Goal:** Alle zur Laufzeit erzeugten Log-/Verlaufsmeldungen des Hauptfensters (Log-Tab,
Status-Tab-Verlauf, Download-Worker) ins bestehende Str/LocalizationService-Zweisprachigkeitssystem
überführen.

**Architecture:** Neues Enum-Präfix `Str.Log_...` in `Infrastructure/Str.cs`, Übersetzungspaare in
`Infrastructure/LocalizationService.cs`, Aufrufstellen in `ViewModels/MainViewModel.cs`,
`Views/MainWindow.xaml.cs`, `Core/Workers/Workers.cs` und `Core/Models/IsoEntry.cs` auf
`LocalizationService.T(Str.Log_X)` bzw. `string.Format(LocalizationService.T(Str.Log_X), ...)`
umgestellt — exakt das in Phase 3 etablierte Muster für dynamische Meldungen.

**Tech Stack:** C# / WPF / .NET 8, bestehendes `LocalizationService`/`Str`-Enum-System, xUnit
(`ULM.Tests`).

## Global Constraints

- Neues Präfix `Str.Log_...` für alle in dieser Phase hinzugefügten Werte.
- Statische Meldungen: `LocalizationService.T(Str.Log_X)`. Meldungen mit Laufzeitwerten:
  `string.Format(LocalizationService.T(Str.Log_X), arg1, ...)`.
- Emoji-Präfixe (💾🌐⚠✅❌🔒📋🗑✏↔🆕✓🔗🔄⚡⛔▶🐢🔎 usw.) bleiben Teil des übersetzten Textes.
- Ternäre/bedingte Meldungen: pro Zweig ein eigener `Str`-Wert, die C#-Verzweigung bleibt
  strukturell erhalten (siehe Beispiel in Task 3).
- Werte ohne natürliche Sprache (Laufwerksbuchstaben, Dateinamen, `ex.Message`, `✓`/`✗`-Symbole,
  durch `string.Join` erzeugte technische Listen) werden unverändert als Format-Argument
  durchgereicht, nicht übersetzt.
- **Dedup-Pflicht:** exakt identische Meldungstexte (zeichengenau inkl. Emoji) bekommen EINEN
  gemeinsamen `Str`-Wert, auch über Dateigrenzen hinweg. Ähnliche, aber nicht exakt identische Texte
  (unterschiedliches Emoji, unterschiedliche Wortwahl) bleiben getrennt — im Zweifel: nicht
  deduplizieren.
- Bekannte Dedup-Fälle (siehe Spec): `"   ✏ {0} → {1}"` (2x in MainViewModel.cs), `"💾 Datenbank:
  neu gefundene Download-Quelle(n) gespeichert."` (2x), die drei Ternary-Meldungen des
  Versionschecks (automatischer + manueller Check verwenden dieselben drei Texte), alle 6
  `RecordHistory`+`Log`-Paare mit identischem Text, `"🗑 Gelöscht: {0}"` (MainViewModel.cs UND
  MainWindow.xaml.cs, über den gemeinsamen `IsoEntry.TryDelete`-Callback-Pfad).
- Kein dediziertes Unit-Test-Harness für einzelne Log-Strings — `AllStrValues_HaveGermanAndEnglishTranslation`
  (`ULM.Tests/LocalizationServiceTests.cs:154`) deckt neue Werte automatisch ab, da es über
  `Enum.GetValues<Str>()` iteriert.
- Vor jeder Bearbeitung: den betroffenen Codeabschnitt frisch lesen (nicht auf eine evtl. veraltete
  Zeilennummer verlassen) — Lehre aus dem Phase-5-Zwischenfall (siehe oben).
- Nach jedem Task: `dotnet build UniversalLinuxManager.csproj -c Debug` muss 0 Fehler/0 neue
  Warnungen liefern, bevor committet wird.

---

### Task 1: `MainViewModel.cs` — Startup + DB-Wartung/Dedup-Logik

**Files:**
- Modify: `Infrastructure/Str.cs` (neue `Str.Log_...`-Werte anhängen)
- Modify: `Infrastructure/LocalizationService.cs` (De+En-Einträge für dieselben Werte)
- Modify: `ViewModels/MainViewModel.cs:318-475` (aktueller Stand zum Zeitpunkt der Spec-Erstellung —
  vor Bearbeitung erneut lesen)

**Interfaces:**
- Produziert: neue `Str.Log_*`-Enum-Werte für Startup- und DB-Wartungsmeldungen (Programmstart,
  ISO-Ordner/Datenbank-Pfad-Anzeige, Anzahl geladener Distros, DB-Eintrag/Duplikat entfernt,
  Duplikate zusammengeführt, Dateiname übernommen/nicht übernommen, Eintrag hinzugefügt, Name/
  Dateiname aktualisiert).
- Konsumiert: nichts aus anderen Tasks dieser Phase.

- [ ] **Step 1: Betroffenen Bereich lesen**

Lies `ViewModels/MainViewModel.cs` im Bereich der `LoadDatabaseAndDedupe`-artigen Methode(n)
zwischen der ersten `Log(` in der Konstruktor-/Initialisierungslogik (Suchtext:
`"▶ Universal Linux Manager gestartet."`) und dem Ende der Dedup-/Merge-Hilfsmethoden (Suchtext:
`Log($"   + {e.Name}  ({e.Filename})"); _db.Save(); return e;`). Notiere jede `Log(...)`-Aufrufstelle
mit hartcodiertem deutschem Text in diesem Bereich.

- [ ] **Step 2: Str-Werte ergänzen**

Beispiele für den erwarteten Namens-/Musterstil (vollständig ausgearbeitet, als Vorlage für die
restlichen Aufrufstellen in diesem Bereich):

```csharp
// Infrastructure/Str.cs — an das Ende des Enums anhängen, vor der schliessenden Klammer
// Log-Meldungen: Startup + DB-Wartung
Log_AppStarted, Log_IsoFolderPath, Log_DatabasePath, Log_DbEntriesLoaded,
Log_DbEntryRemoved, Log_ExactDuplicateRemoved, Log_Merged, Log_DuplicateRemoved,
Log_FilenameAdopted, Log_FilenameNotAdopted, Log_EntryAdded, Log_NameUpdated,
Log_FilenameReplaced, Log_EntryAddedSimple,
```

Für jede tatsächlich im Bereich gefundene Aufrufstelle einen `Str.Log_...`-Namen nach demselben
Muster vergeben (Verb/Ereignis-Name, keine Aufrufort-Referenz). Bei zwei Aufrufstellen mit exakt
identischem Text (z.B. `"   ✏ {0} → {1}"` — kommt in diesem Bereich UND später in Task 3 vor):
EINEN Wert anlegen, in Task 3 wiederverwenden statt neu anzulegen.

- [ ] **Step 3: Übersetzungen ergänzen**

```csharp
// Infrastructure/LocalizationService.cs — De-Dictionary
[Str.Log_AppStarted]      = "▶ Universal Linux Manager gestartet.",
[Str.Log_IsoFolderPath]   = "   ISO-Ordner: {0}",
[Str.Log_DatabasePath]    = "   Datenbank:  {0}",
[Str.Log_DbEntriesLoaded] = "   {0} Distros in der Datenbank geladen.",

// En-Dictionary
[Str.Log_AppStarted]      = "▶ Universal Linux Manager started.",
[Str.Log_IsoFolderPath]   = "   ISO folder: {0}",
[Str.Log_DatabasePath]    = "   Database:  {0}",
[Str.Log_DbEntriesLoaded] = "   {0} distros loaded from database.",
```

Alle weiteren im Bereich gefundenen Meldungen nach demselben Schema übersetzen (De = exakter
bisheriger Text, En = fachlich korrekte, mit dem HelpDialog-Glossar konsistente Übersetzung —
"Datenbank" → "database", "Duplikat" → "duplicate", "Eintrag" → "entry").

- [ ] **Step 4: Aufrufstellen umstellen**

Statische Meldung:
```csharp
// vorher
Log("▶ Universal Linux Manager gestartet.");
// nachher
Log(LocalizationService.T(Str.Log_AppStarted));
```

Meldung mit einem Argument:
```csharp
// vorher
Log($"   {_db.Count} Distros in der Datenbank geladen.");
// nachher
Log(string.Format(LocalizationService.T(Str.Log_DbEntriesLoaded), _db.Count));
```

Meldung mit mehreren Argumenten:
```csharp
// vorher
Log($"   🗑 DB-Eintrag entfernt: {e.Name}  ({e.Filename})");
// nachher
Log(string.Format(LocalizationService.T(Str.Log_DbEntryRemoved), e.Name, e.Filename));
```

Alle übrigen Aufrufstellen im Bereich nach demselben Muster umstellen.

- [ ] **Step 5: Build prüfen**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: `Build succeeded.`, 0 Fehler, keine neuen Warnungen.

- [ ] **Step 6: Verifikations-Grep**

Run: `grep -n 'Log("\|Log(\$"' ViewModels/MainViewModel.cs | sed -n '1,40p'`
Erwartet: keine der im Bereich Zeilen ~318-475 behandelten Aufrufstellen taucht noch mit
hartcodiertem String auf (nur noch `Log(LocalizationService.T(...))` bzw.
`Log(string.Format(...))`).

- [ ] **Step 7: Commit**

```bash
git add Infrastructure/Str.cs Infrastructure/LocalizationService.cs ViewModels/MainViewModel.cs
git commit -m "feat: Log-Meldungen Startup und DB-Wartung lokalisiert"
```

---

### Task 2: `MainViewModel.cs` — USB-Stick-Scan

**Files:**
- Modify: `Infrastructure/Str.cs`, `Infrastructure/LocalizationService.cs`
- Modify: `ViewModels/MainViewModel.cs` (Stick-Scan-Methode(n), Suchtext ab
  `Log($"🔌 Laufwerke: ` bis `Log($"✅ Alle ISOs auf {drive} aktuell.")`)

**Interfaces:**
- Produziert: `Str.Log_*`-Werte für Laufwerkserkennung, Stick-Scan-Start/-Ergebnis,
  unvollständige/Hash-Abweichungs-Funde, veraltete Duplikate, "alles aktuell".
- Konsumiert: nichts.

- [ ] **Step 1: Betroffenen Bereich lesen und Aufrufstellen notieren**

Bereich zwischen `Log($"🔌 Laufwerke: {string.Join(...)}");` und
`Log($"✅ Alle ISOs auf {drive} aktuell.");` in `ViewModels/MainViewModel.cs`.

- [ ] **Step 2-4: Str-Werte, Übersetzungen, Aufrufstellen (Muster wie Task 1)**

Ein vollständig ausgearbeitetes Beispiel für den in diesem Bereich vorkommenden Fall "Text +
technische, nicht übersetzte Liste":

```csharp
// vorher
Log($"🔌 Laufwerke: {string.Join(", ", Drives.Select(d => $"{d.Letter} ({d.Label})"))}");
// nachher — der string.Join-Teil ist technische Aufzählung (Laufwerksbuchstabe + Label), bleibt
// unuebersetzt und wird komplett als EIN Format-Argument durchgereicht
Log(string.Format(LocalizationService.T(Str.Log_DrivesDetected),
    string.Join(", ", Drives.Select(d => $"{d.Letter} ({d.Label})"))));
```

```csharp
// Infrastructure/LocalizationService.cs
[Str.Log_DrivesDetected] = "🔌 Laufwerke: {0}",   // De
[Str.Log_DrivesDetected] = "🔌 Drives: {0}",       // En
```

Alle übrigen Meldungen im Bereich (Stick-Scan gestartet, N ISO(s) gefunden, Datei-Listeneintrag,
unvollständige ISOs erkannt, Datenmüll-Verdacht, Hash-Abweichung erkannt/Listeneintrag, veraltete
ISO(s)/Listeneintrag, veraltete Duplikate/Listeneintrag, "Alle ISOs aktuell") nach demselben Schema
wie Task 1 behandeln.

- [ ] **Step 5: Build prüfen** (wie Task 1, Step 5)

- [ ] **Step 6: Verifikations-Grep** (wie Task 1, Step 6, angepasst auf den Stick-Scan-Bereich)

- [ ] **Step 7: Commit**

```bash
git add Infrastructure/Str.cs Infrastructure/LocalizationService.cs ViewModels/MainViewModel.cs
git commit -m "feat: Log-Meldungen USB-Stick-Scan lokalisiert"
```

---

### Task 3: `MainViewModel.cs` — Integritätsprüfung + Ventoy-Bootmenü + Online-Versionscheck

**Files:**
- Modify: `Infrastructure/Str.cs`, `Infrastructure/LocalizationService.cs`
- Modify: `ViewModels/MainViewModel.cs` (Bereich Zeilen ~689-870: Integritätsprüfung,
  Ventoy-Bootmenü-Update, automatischer Online-Versionscheck)

**Interfaces:**
- Produziert: `Str.Log_IntegrityCheckStarted/Cancelled/Done/Failed`, `Str.Log_VentoyMenuUpdating/Updated`,
  `Str.Log_VersionCheckStarted/Summary`, `Str.Log_EntryUnreachable`, `Str.Log_UpdateFound`,
  `Str.Log_VersionCurrent`, `Str.Log_NameUpdated` (falls nicht schon aus Task 1 vorhanden — siehe
  Dedup-Hinweis in Task 1 Step 2), `Str.Log_DbNewVersionsSaved`, `Str.Log_DbNewSourcesSaved`,
  `Str.Log_CheckingStick`, `Str.Log_StickCheckDone`.
- Konsumiert: `Str.Log_NameUpdated` — falls in Task 1 bereits angelegt (Dedup), hier NICHT neu
  anlegen, sondern denselben Wert referenzieren. `Str.Log_UpdateFound`/`Str.Log_VersionCurrent`/
  `Str.Log_EntryUnreachable` werden in Task 5 (manueller Update-Check) erneut verwendet — dort nicht
  neu anlegen.

- [ ] **Step 1: Betroffenen Bereich lesen**

Von `RecordHistory($"🔒 Integritätsprüfung ...` (Beginn Integritätsprüfungs-Methode) bis
`RecordHistory($"💾 Stick-Prüfung {driveToScan} abgeschlossen ...` (Ende automatischer
Versionscheck-Block) in `ViewModels/MainViewModel.cs`.

- [ ] **Step 2: `RecordHistory`+`Log`-Dedup anwenden**

Vollständig ausgearbeitetes Beispiel für den Dedup-Fall (identischer Text an beiden Stellen):

```csharp
// vorher
RecordHistory($"🔒 Integritätsprüfung {SelectedDriveLetter} gestartet …");
Log($"🔒 Integritätsprüfung {SelectedDriveLetter} gestartet …");

// nachher — EIN Str-Wert, EINMAL formatiert, an beide Senken geschickt
string msg = string.Format(LocalizationService.T(Str.Log_IntegrityCheckStarted), SelectedDriveLetter);
RecordHistory(msg);
Log(msg);
```

Dieses Muster auf alle 6 `RecordHistory`+`Log`-Paare in diesem Bereich anwenden (5 davon sind
exakt gepaart; die Stick-Prüfungs-Abschluss-Meldung am Ende des Bereichs hat KEIN `Log`-Gegenstück
— dort bleibt es bei `RecordHistory(string.Format(...))` allein).

- [ ] **Step 3: Ternäre Versionscheck-Meldung**

Vollständig ausgearbeitetes Beispiel für die bedingte Meldung (Versionscheck-Ergebnis pro Distro):

```csharp
// vorher
if (!result.Resolved) { Log($"   ⚠ {result.Name}: nicht erreichbar."); return; }
Log(result.HasUpdate
    ? $"   🆕 {result.Name}: v{result.LocalVersion} → v{result.RemoteVersion}"
    : $"   ✓ {result.Name}: v{result.RemoteVersion} (aktuell)");

// nachher
if (!result.Resolved)
{
    Log(string.Format(LocalizationService.T(Str.Log_EntryUnreachable), result.Name));
    return;
}
Log(result.HasUpdate
    ? string.Format(LocalizationService.T(Str.Log_UpdateFound), result.Name, result.LocalVersion, result.RemoteVersion)
    : string.Format(LocalizationService.T(Str.Log_VersionCurrent), result.Name, result.RemoteVersion));
```

- [ ] **Step 4: Übrige Meldungen im Bereich**

Nach demselben Schema: Integritätsprüfung abgebrochen/fertig/fehlgeschlagen, Ventoy-Bootmenü wird
aktualisiert/aktualisiert, Versionscheck gestartet/Zusammenfassung, Datenbank neue Version(en)/
neue Quelle(n) gespeichert, "Prüfe Stick …".

- [ ] **Step 5: Build prüfen** (wie Task 1)

- [ ] **Step 6: Verifikations-Grep** (wie Task 1, angepasst)

- [ ] **Step 7: Commit**

```bash
git add Infrastructure/Str.cs Infrastructure/LocalizationService.cs ViewModels/MainViewModel.cs
git commit -m "feat: Log-Meldungen Integritaetspruefung, Ventoy-Bootmenue und Versionscheck lokalisiert"
```

---

### Task 4: `MainViewModel.cs` — Download + Kopieren

**Files:**
- Modify: `Infrastructure/Str.cs`, `Infrastructure/LocalizationService.cs`
- Modify: `ViewModels/MainViewModel.cs` (Bereich Zeilen ~883-1196: Download-Start bis
  Kopiervorgang-Abschluss)

**Interfaces:**
- Produziert: `Str.Log_DownloadStarted`, `Str.Log_ToDriveSuffix`, `Str.Log_QueueItem`,
  `Str.Log_MovedToCopyQueue`, `Str.Log_DownloadsDone`, `Str.Log_DownloadsDonePipelineContinues`,
  `Str.Log_SomeFailed`, `Str.Log_NoDownloads`, `Str.Log_StickCopyCancelled`,
  `Str.Log_SourceFileNotFound`, `Str.Log_FileTooSmall`, `Str.Log_NotEnoughSpace`,
  `Str.Log_CopyingToStick`, `Str.Log_IncompleteRemoved`, `Str.Log_CopyError`,
  `Str.Log_CopyStarted`, `Str.Log_DeleteAfterSuffix`, `Str.Log_CopyQueueItem`,
  `Str.Log_CopyCancelled`, `Str.Log_CopyDone`, `Str.Log_Deleted`, `Str.Log_LocalFilesDeleted`.
- Konsumiert: `Str.Log_Deleted` wird in Task 6 (MainWindow.xaml.cs) wiederverwendet — dort nicht
  neu anlegen, denselben Wert referenzieren.

- [ ] **Step 1: Betroffenen Bereich lesen**

Von `Log($"⬇ Download gestartet: ...` bis `Log($"   🗑 {del} ISO(s) lokal gelöscht.")` in
`ViewModels/MainViewModel.cs`.

- [ ] **Step 2: Verkettete bedingte Suffixe**

Vollständig ausgearbeitetes Beispiel (String-Verkettung mit bedingtem Zusatz, Muster aus Phase 3
übernommen):

```csharp
// vorher
Log($"⬇ Download gestartet: {queue.Count} ISO(s), {slots} parallel" +
    (string.IsNullOrEmpty(drive) ? "" : $" → {drive}"));

// nachher
string startMsg = string.Format(LocalizationService.T(Str.Log_DownloadStarted), queue.Count, slots) +
    (string.IsNullOrEmpty(drive) ? "" : string.Format(LocalizationService.T(Str.Log_ToDriveSuffix), drive));
Log(startMsg);
```

```csharp
// Infrastructure/LocalizationService.cs
[Str.Log_DownloadStarted] = "⬇ Download gestartet: {0} ISO(s), {1} parallel",  // De
[Str.Log_ToDriveSuffix]   = " → {0}",                                          // De
[Str.Log_DownloadStarted] = "⬇ Download started: {0} ISO(s), {1} parallel",    // En
[Str.Log_ToDriveSuffix]   = " → {0}",                                          // En (identisch, da nur ein Pfeil+Wert)
```

Dasselbe Verkettungsmuster gilt für `Str.Log_CopyStarted`+`Str.Log_DeleteAfterSuffix` (Zeile mit
`"📋 Kopiervorgang auf {drive}: ..." + (deleteAfter ? " (danach lokal löschen)" : "")`) sowie für
die Fehlgeschlagen-Suffixe bei "gelöscht"/"Duplikate gelöscht" (`", {failed} fehlgeschlagen"`).

- [ ] **Step 3: `IsoEntry.TryDelete`-Callback-Aufrufe NICHT anfassen**

An den Stellen `IsoEntry.TryDelete(path, AppendLog)` bzw. `IsoEntry.TryDelete(p, msg => Log(msg))`
wird kein Text übersetzt (das passiert in Task 8/`IsoEntry.cs` selbst) — hier nur die Meldungen
DANACH (`Log($"   🗑 Gelöscht: {e.Filename}")` etc.) umstellen.

- [ ] **Step 4: Übrige Meldungen im Bereich**

Nach demselben Schema wie Task 1: Warteschlangen-Einträge, Downloads abgeschlossen, Pipeline
läuft weiter, "X ISO(s) fehlgeschlagen"/"Keine Downloads", Stick-Kopie abgebrochen, Quelldatei
nicht gefunden, Datei zu klein, nicht genug Speicherplatz, Kopiere auf Stick, unvollständig
entfernt, Kopierfehler, Kopiervorgang fertig, lokal gelöscht.

- [ ] **Step 5: Build prüfen** (wie Task 1)

- [ ] **Step 6: Verifikations-Grep** (wie Task 1, angepasst)

- [ ] **Step 7: Commit**

```bash
git add Infrastructure/Str.cs Infrastructure/LocalizationService.cs ViewModels/MainViewModel.cs
git commit -m "feat: Log-Meldungen Download und Kopieren lokalisiert"
```

---

### Task 5: `MainViewModel.cs` — Update-Check/URL-Check/DB-Health/Ventoy-Install/Abbruch + verbleibende StatusText-Literale

**Files:**
- Modify: `Infrastructure/Str.cs`, `Infrastructure/LocalizationService.cs`
- Modify: `ViewModels/MainViewModel.cs` (Bereich Zeilen ~1215-1418: manueller Update-Check bis
  Programmende, inkl. der 6 verbliebenen hartcodierten `StatusText = "..."`-Zuweisungen aus der
  Spec-Bestandsaufnahme)

**Interfaces:**
- Produziert: `Str.Log_ManualUpdateCheckStarted`, `Str.Log_CheckingForUpdates`,
  `Str.Log_UpdateCheckSummary`, `Str.Log_UrlCheckStarted`, `Str.Log_CheckingUrls`,
  `Str.Log_UrlCheckItem`, `Str.Log_UrlCheckSummary`, `Str.Log_DbHealthCheckStarted`,
  `Str.Log_DbHealthResolved`, `Str.Log_DbHealthUnreachable` (eigener Wert, NICHT
  `Str.Log_EntryUnreachable` — unterschiedliches Emoji `❌` statt `⚠`, siehe Global Constraints),
  `Str.Log_DbHealthSummary`, `Str.Log_VentoyActionStarted`, `Str.Log_VentoyActionWordUpdate`,
  `Str.Log_VentoyActionWordInstall`, `Str.Log_StartingAsAdmin`, `Str.Log_WaitingForUac`,
  `Str.Log_ExePathNotFound`, `Str.Log_AdminProcessFailed`, `Str.Log_AdminProcessRunning`,
  `Str.Log_VentoyInstallRunning`, `Str.Log_VentoyExitCode`, `Str.Log_UacDenied`,
  `Str.Log_CancelRequested`, `Str.Log_GenericError`, `Str.Log_AppClosing`.
- Konsumiert: `Str.Log_UpdateFound`/`Str.Log_VersionCurrent`/`Str.Log_EntryUnreachable` aus Task 3
  (Dedup — hier wiederverwenden, NICHT neu anlegen, da derselbe Ternary-Block hier erneut auftritt).

- [ ] **Step 1: Betroffenen Bereich lesen**

Von `SetBusy(true); StatusText = "Prüfe auf Updates …"; ...` bis `Log("▶ Anwendung wird beendet.");`
in `ViewModels/MainViewModel.cs`.

- [ ] **Step 2: Zusammengesetzte StatusText+Log-Zeilen**

Vollständig ausgearbeitetes Beispiel für eine Zeile mit sowohl `StatusText`- als auch `Log`-Literal:

```csharp
// vorher
SetBusy(true); StatusText = "Prüfe auf Updates …"; ProgressPercent = 0; Log("🔄 Manueller Update-Check …");

// nachher
SetBusy(true);
StatusText = LocalizationService.T(Str.Log_CheckingForUpdates);
ProgressPercent = 0;
Log(LocalizationService.T(Str.Log_ManualUpdateCheckStarted));
```

Dasselbe Muster für die Zeilen mit `"Prüfe URLs …"`, `"Warte auf UAC-Bestätigung …"`,
`"Ventoy-Installation läuft …"`, `"Abbruch …"` (jeweils eigener `StatusText`-`Str`-Wert, getrennt
vom begleitenden `Log`-Wert in derselben Zeile).

- [ ] **Step 3: Zusammengesetztes Wort in Interpolation**

Vollständig ausgearbeitetes Beispiel für eingebettetes, selbst zu übersetzendes Wort:

```csharp
// vorher
Log($"⚡ Ventoy-{(updateMode ? "Aktualisierung" : "Installation")} auf {letter}");

// nachher
string action = updateMode
    ? LocalizationService.T(Str.Log_VentoyActionWordUpdate)
    : LocalizationService.T(Str.Log_VentoyActionWordInstall);
Log(string.Format(LocalizationService.T(Str.Log_VentoyActionStarted), action, letter));
```

```csharp
// Infrastructure/LocalizationService.cs
[Str.Log_VentoyActionWordUpdate]  = "Aktualisierung",   // De
[Str.Log_VentoyActionWordInstall] = "Installation",     // De
[Str.Log_VentoyActionStarted]     = "⚡ Ventoy-{0} auf {1}",  // De
[Str.Log_VentoyActionWordUpdate]  = "update",           // En
[Str.Log_VentoyActionWordInstall] = "installation",     // En
[Str.Log_VentoyActionStarted]     = "⚡ Ventoy {0} on {1}",   // En
```

- [ ] **Step 4: DB-Health-Check-Ternary (eigenes Emoji, kein Dedup mit Task 3)**

```csharp
// vorher
Log(result.Resolved ? $"   ✓ {result.Name}: v{result.RemoteVersion}" : $"   ❌ {result.Name}: nicht erreichbar.");

// nachher
Log(result.Resolved
    ? string.Format(LocalizationService.T(Str.Log_DbHealthResolved), result.Name, result.RemoteVersion)
    : string.Format(LocalizationService.T(Str.Log_DbHealthUnreachable), result.Name));
```

- [ ] **Step 5: Übrige Meldungen im Bereich**

Nach demselben Schema: Update-Check-Zusammenfassung, URL-Check-Item (Symbol bleibt
unübersetztes Argument), URL-Check-Zusammenfassung, DB-Gesundheitscheck gestartet/Zusammenfassung,
"Startet als Administrator", EXE-Pfad nicht ermittelbar, Admin-Prozess konnte nicht gestartet
werden/läuft, Ventoy-ExitCode, UAC abgelehnt, "❌ {ex.Message}" (nur Rahmen übersetzen, `ex.Message`
bleibt Argument), Abbruch angefordert, Anwendung wird beendet.

- [ ] **Step 6: Build prüfen** (wie Task 1)

- [ ] **Step 7: Verifikations-Grep**

Run: `grep -n 'Log("\|Log(\$"\|StatusText = "' ViewModels/MainViewModel.cs`
Erwartet: keine Treffer mehr im gesamten File (dieser Task ist der letzte für `MainViewModel.cs`)
— jeder verbleibende Treffer muss einzeln geprüft werden, ob er wirklich noch unbehandelt ist oder
der Grep nur strukturell falsch anschlägt.

- [ ] **Step 8: Commit**

```bash
git add Infrastructure/Str.cs Infrastructure/LocalizationService.cs ViewModels/MainViewModel.cs
git commit -m "feat: Log-Meldungen Update-Check, URL-Check, DB-Health, Ventoy-Install und Abbruch lokalisiert"
```

---

### Task 6: `MainWindow.xaml.cs` — Import/Datenmüll-Dialoge (nach Stick-Scan)

**Files:**
- Modify: `Infrastructure/Str.cs`, `Infrastructure/LocalizationService.cs`
- Modify: `Views/MainWindow.xaml.cs` (Bereich Zeilen ~68-134: Reaktion auf neue Versionen,
  unbekannte ISOs, Datenmüll-Erkennung nach Stick-Scan)

**Interfaces:**
- Produziert: `Str.Log_*`-Werte für die in diesem Bereich gefundenen `AppendLog(...)`-Aufrufe
  (neuere Versionen erkannt, keine Änderung, ersetzt/hinzugefügt-Zusammenfassung, unbekannte ISOs
  erkannt, Import übersprungen, Datei verschoben, ISOs hinzugefügt, unvollständige ISOs erkannt,
  Stick-Wartung übersprungen).
- Konsumiert: `Str.Log_Deleted` aus Task 4 (Dedup — die Zeile `AppendLog($"   🗑 Gelöscht:
  {Path.GetFileName(path)}"));` in diesem Bereich hat denselben Text wie die bereits in Task 4
  behandelte Meldung; hier NICHT neu anlegen).

- [ ] **Step 1: Betroffenen Bereich lesen**

Von `AppendLog($"📥 {fresh.Count} ISO(s) auf {drive} neuer als DB-Eintrag.");` bis
`else AppendLog($"ℹ Stick-Wartung übersprungen ({fresh.Count} Datei(en) behalten).");` in
`Views/MainWindow.xaml.cs`.

- [ ] **Step 2: Verschachtelte bedingte Verkettung**

Vollständig ausgearbeitetes Beispiel für die komplexeste Zeile in diesem Bereich:

```csharp
// vorher
if (replaced > 0 || added > 0)
{
    _vm.RebuildTree();
    AppendLog($"✅ DB: {(replaced > 0 ? $"{replaced} ersetzt" : "")}{(replaced > 0 && added > 0 ? ", " : "")}{(added > 0 ? $"{added} hinzugefügt" : "")}.");
}

// nachher
if (replaced > 0 || added > 0)
{
    _vm.RebuildTree();
    string replacedPart = replaced > 0 ? string.Format(LocalizationService.T(Str.Log_ReplacedCount), replaced) : "";
    string addedPart    = added > 0    ? string.Format(LocalizationService.T(Str.Log_AddedCount), added) : "";
    string separator    = (replaced > 0 && added > 0) ? ", " : "";
    AppendLog(string.Format(LocalizationService.T(Str.Log_DbUpdateSummary), replacedPart, separator, addedPart));
}
```

```csharp
// Infrastructure/LocalizationService.cs
[Str.Log_ReplacedCount]  = "{0} ersetzt",     // De
[Str.Log_AddedCount]     = "{0} hinzugefügt", // De
[Str.Log_DbUpdateSummary] = "✅ DB: {0}{1}{2}.",  // De
[Str.Log_ReplacedCount]  = "{0} replaced",    // En
[Str.Log_AddedCount]     = "{0} added",       // En
[Str.Log_DbUpdateSummary] = "✅ DB: {0}{1}{2}.",  // En
```

- [ ] **Step 3: Übrige Meldungen im Bereich**

Nach demselben Schema: neuere ISOs erkannt, keine Änderung, Skip-Item, unbekannte ISOs erkannt,
Import übersprungen, Datei-verschoben-Item, ISOs hinzugefügt (+ optionaler
"konnte(n) nicht verschoben werden"-Zusatz analog Step 2), unvollständige ISOs erkannt, gelöscht,
"X Datei(en) gelöscht" (+ optionaler "fehlgeschlagen"-Zusatz), "Stick-Wartung übersprungen".

- [ ] **Step 4: Build prüfen** (wie Task 1)

- [ ] **Step 5: Verifikations-Grep** (wie Task 1, angepasst auf den bearbeiteten Bereich)

- [ ] **Step 6: Commit**

```bash
git add Infrastructure/Str.cs Infrastructure/LocalizationService.cs Views/MainWindow.xaml.cs
git commit -m "feat: Log-Meldungen Import und Datenmuell-Erkennung (Stick) lokalisiert"
```

---

### Task 7: `MainWindow.xaml.cs` — Programmstart/Update-Check/Datei-Wartung

**Files:**
- Modify: `Infrastructure/Str.cs`, `Infrastructure/LocalizationService.cs`
- Modify: `Views/MainWindow.xaml.cs` (Bereich Zeilen ~221-564: automatischer Online-Check-Start,
  Self-Update-Check, Arbeitsordner-Datei-Wartung/Datenmüll-Scan)

**Interfaces:**
- Produziert: `Str.Log_*`-Werte für automatischen Versionscheck-Start, neue ULM-Version verfügbar,
  Update-Download-Erfolg/Fehlschlag, Release-Seite, Hintergrund-Check fällig, Arbeitsordner nicht
  gefunden, ISO-Ordner-Scan, ISO-Datei(en) gefunden, leer/verwaist/unvollständig/vollständig/zu
  klein/OK-ungeprüft/abgebrochen (Datei-Wartungs-Status), .part-Suche-Fehler, kein Datenmüll,
  Datenmüll eingestuft, gelöscht (+fehlgeschlagen-Zusatz), Wartung übersprungen, Datei-Wartung-
  Fehler.
- Konsumiert: `Str.Log_Deleted` aus Task 4 — hier wiederverwenden (siehe Global Constraints Dedup).

- [ ] **Step 1: Betroffenen Bereich lesen**

Von `AppendLog("🌐 Automatischer Online-Versionscheck wird gestartet …");` bis
`catch (Exception ex) { AppendLog($"⚠ Datei-Wartung: {ex.Message}"); }` in
`Views/MainWindow.xaml.cs`.

- [ ] **Step 2: Beispiel für Datei-Wartungs-Statuszeile (relativer Pfad + Dateigröße als Argumente)**

```csharp
// vorher
{ AppendLog($"   ⚠ Unvollständig: {RelativePath(dir, f)}  ({FmtSize(size)} / {FmtSize(expected)} erwartet)"); candidates.Add((f, size)); }

// nachher
{
    AppendLog(string.Format(LocalizationService.T(Str.Log_FileIncomplete), RelativePath(dir, f), FmtSize(size), FmtSize(expected)));
    candidates.Add((f, size));
}
```

`RelativePath(dir, f)` und `FmtSize(...)` liefern keine natürliche Sprache (Pfad bzw. formatierte
Byte-Größe) — beide bleiben unübersetzte Format-Argumente.

- [ ] **Step 3: Übrige Meldungen im Bereich**

Nach demselben Schema: alle Datei-Wartungs-Statuszeilen (leer/verwaist/vollständig/zu klein/OK-
ungeprüft/abgebrochen — jede ein eigener `Str`-Wert, da unterschiedlicher Text/Emoji), Scan-Start,
ISO-Datei(en)-gefunden-Zähler, .part-Suche-Fehler, "kein Datenmüll", "X Datei(en) als Datenmüll
eingestuft", gelöscht (via `Str.Log_Deleted` aus Task 4), "X gelöscht"+optionaler
Fehlgeschlagen-Zusatz, Wartung übersprungen, sowie die separate automatischer-Check-/Self-Update-
Gruppe (Versionscheck gestartet, neue Version verfügbar inkl. Versions-Platzhalter `vX.Y.Z`/
`vA.B.C` als **wörtlicher Text, nicht als echte Interpolation** — siehe HelpDialog-Präzedenzfall
für dasselbe Muster —, Release-URL, Update-Download-Erfolg/-Fehlschlag, Hintergrund-Check fällig).

**Wichtig:** Die Zeile `AppendLog($"🆕 Neue ULM-Version verfügbar: v{info.LatestVersion} (aktuell
installiert: v{Constants.AppVersion})");` hat ECHTE Interpolationswerte (`info.LatestVersion`,
`Constants.AppVersion`) — nicht mit dem HelpDialog-Text verwechseln, der denselben Satz als
wörtliches Beispiel (`vX.Y.Z`) dokumentiert. Hier zwei Format-Argumente verwenden.

- [ ] **Step 4: Build prüfen** (wie Task 1)

- [ ] **Step 5: Verifikations-Grep** (wie Task 1, angepasst)

- [ ] **Step 6: Commit**

```bash
git add Infrastructure/Str.cs Infrastructure/LocalizationService.cs Views/MainWindow.xaml.cs
git commit -m "feat: Log-Meldungen Programmstart, Update-Check und Datei-Wartung lokalisiert"
```

---

### Task 8: `MainWindow.xaml.cs` — Rest (Ventoy/DB-Suche/Abbruch) + `Core/Models/IsoEntry.cs`

**Files:**
- Modify: `Infrastructure/Str.cs`, `Infrastructure/LocalizationService.cs`
- Modify: `Views/MainWindow.xaml.cs` (Bereich Zeilen ~609-932: verbleibende `AppendLog`-Aufrufe)
- Modify: `Core/Models/IsoEntry.cs:288` (die eine Meldung in `TryDelete`)

**Interfaces:**
- Produziert: `Str.Log_*`-Werte für veraltete Duplikate gelöscht (+fehlgeschlagen), Duplikat-
  Bereinigung übersprungen, USB-Laufwerke erkannt, Ventoy-Installation/-Aktualisierung wird
  gestartet, Speicherplatz-Prüfung, Quelle manuell hinterlegt (2 Varianten), ISOs aus Online-Suche
  hinzugefügt, Vorgang wird abgebrochen, sowie `Str.Log_DeleteFailed` für `IsoEntry.cs`.
- Konsumiert: `Str.Log_Deleted` aus Task 4 (Dedup — die Zeile `AppendLog($"   🗑 Gelöscht:
  {Path.GetFileName(path)}"));` am Anfang dieses Bereichs hat denselben Text wie die bereits in
  Task 4 behandelte Meldung; hier NICHT neu anlegen).

- [ ] **Step 1: Betroffenen Bereich lesen**

Von `{ if (IsoEntry.TryDelete(path, AppendLog)) { deleted++; AppendLog($"   🗑 Gelöscht:
{Path.GetFileName(path)}"); } else failed++; }` (Duplikat-Löschung) bis
`private void BtnCancel_Click(...) { AppendLog("⛔ Vorgang wird abgebrochen …"); ... }` in
`Views/MainWindow.xaml.cs`. Zusätzlich `Core/Models/IsoEntry.cs:283-289` (`TryDelete`-Methode).

- [ ] **Step 2: `IsoEntry.cs`-Meldung (eigene, kleine Datei)**

```csharp
// Core/Models/IsoEntry.cs — vorher
catch (Exception ex) { log?.Invoke($"⚠ Löschen fehlgeschlagen ({Path.GetFileName(path)}): {ex.Message}"); return false; }

// nachher
catch (Exception ex)
{
    log?.Invoke(string.Format(LocalizationService.T(Str.Log_DeleteFailed), Path.GetFileName(path), ex.Message));
    return false;
}
```

`IsoEntry.cs` benötigt dafür `using ULM.Infrastructure;` (prüfen, ob der Using-Block das bereits
enthält — falls nicht, ergänzen).

- [ ] **Step 3: Übrige Meldungen im Bereich (`MainWindow.xaml.cs`)**

Nach demselben Schema wie vorherige Tasks: veraltete Duplikate gelöscht (+fehlgeschlagen-Zusatz),
Duplikat-Bereinigung übersprungen, "{0} USB-Laufwerke erkannt: {1}" (zweiter Teil = technische,
unübersetzte Liste wie in Task 2), Ventoy-Installation/-Aktualisierung wird gestartet (Text-Wort
"Aktualisierung"/"Installation" — hier ggf. `Str.Log_VentoyActionWordUpdate`/`_Install` aus Task 5
wiederverwenden, falls exakt derselbe Wortlaut, sonst eigenen Wert anlegen und im Kommentar
vermerken warum), Speicherplatz-Prüfung, Quelle manuell hinterlegt (2 Varianten), ISOs aus
Online-Suche hinzugefügt, Vorgang wird abgebrochen.

- [ ] **Step 4: Build prüfen** (wie Task 1)

- [ ] **Step 5: Datei-weiter Verifikations-Grep über die GESAMTE `MainWindow.xaml.cs`**

Run: `grep -n 'AppendLog("\|AppendLog(\$"' Views/MainWindow.xaml.cs`
Erwartet: keine Treffer mehr — dieser Task ist der letzte für diese Datei. Jeder verbleibende
Treffer muss einzeln geprüft werden (echter Rest oder nur strukturell falscher Grep-Treffer bei
mehrzeiligen Aufrufen).

- [ ] **Step 6: Commit**

```bash
git add Infrastructure/Str.cs Infrastructure/LocalizationService.cs Views/MainWindow.xaml.cs Core/Models/IsoEntry.cs
git commit -m "feat: Log-Meldungen Ventoy/DB-Suche/Abbruch und IsoEntry-Loeschfehler lokalisiert"
```

---

### Task 9: `Core/Workers/Workers.cs` — DownloadWorker-Meldungen

**Files:**
- Modify: `Infrastructure/Str.cs`, `Infrastructure/LocalizationService.cs`
- Modify: `Core/Workers/Workers.cs` (alle `LogMessage?.Invoke(...)`-Aufrufe mit deutschem Text,
  ca. 16 Stück)

**Interfaces:**
- Produziert: `Str.Log_*`-Werte für: keine Download-URL, Mirror-Test-Ergebnis, Download-URL
  verwendet, dauerhaft langsam (+optionaler "nächste Quelle"-Zusatz), Nutzer fordert schnelleren
  Mirror an, Mirror fehlgeschlagen (+optionaler "nächsten Mirror"-Zusatz), kein schnellerer Mirror
  gefunden, letztlich doch fehlgeschlagen (2 Vorkommen — Dedup!), fährt trotz Langsamkeit fort,
  Nutzer hat abgelehnt, Prüfsumme verifiziert, Prüfsumme-Abweichung-Warnung, Download abgeschlossen,
  Download fehlgeschlagen, genereller Download-Fehler.
- Konsumiert: nichts (dieser Worker läuft unabhängig von `MainViewModel.cs`, teilt sich aber ggf.
  das Wort "nicht erreichbar" aus Zeile 407/408 — dort separat prüfen, ob exakt identisch mit einem
  bereits angelegten Wert aus Task 3/5, sonst eigenen Wert anlegen).

- [ ] **Step 1: Betroffenen Bereich lesen**

Alle `LogMessage?.Invoke($"...` -Aufrufe in `Core/Workers/Workers.cs` (Suchtext:
`LogMessage?.Invoke(`) — ca. 16 Fundstellen im Download-Worker.

- [ ] **Step 2: Verschachtelte `string.Join`-Meldung mit bedingtem inneren Text**

Vollständig ausgearbeitetes Beispiel für den komplexesten Fall in dieser Datei:

```csharp
// vorher
LogMessage?.Invoke($"   🔎 {entry.Name}: Mirror-Test — " +
    string.Join(", ", raced.Select(r => $"{TryGetSourceLabel(r.Url)} {(r.Bps > 0 ? $"{r.Bps * 8 / 1_000_000:F1} Mbit/s" : "nicht erreichbar")}")));

// nachher
string mirrorList = string.Join(", ", raced.Select(r =>
    $"{TryGetSourceLabel(r.Url)} {(r.Bps > 0 ? $"{r.Bps * 8 / 1_000_000:F1} Mbit/s" : LocalizationService.T(Str.Log_Unreachable))}"));
LogMessage?.Invoke(string.Format(LocalizationService.T(Str.Log_MirrorTest), entry.Name, mirrorList));
```

`Str.Log_Unreachable` ("nicht erreichbar" / "unreachable", statisch, ohne Argument) ist ein neuer,
eigenständiger Wert für dieses Wort als alleinstehende Phrase innerhalb der Liste — NICHT
identisch mit `Str.Log_EntryUnreachable` aus Task 3 (das ist der ganze Satz `"   ⚠ {0}: nicht
erreichbar."`, nicht nur das Wort). Beide Werte bleiben getrennt.

- [ ] **Step 3: Bedingter Zusatz ("und versuche nächste Quelle")**

```csharp
// vorher
LogMessage?.Invoke($"   🐢 {entry.Name}: {host} dauerhaft langsam (< {TransferFormat.FormatBytes(SlowSpeedThresholdBytesPerSec)}/s) — breche ab" +
    (hasMoreMirrors ? " und versuche nächste Quelle …" : "."));

// nachher
string suffix = hasMoreMirrors
    ? LocalizationService.T(Str.Log_AndTryNextSource)
    : ".";
LogMessage?.Invoke(string.Format(LocalizationService.T(Str.Log_PermanentlySlow), entry.Name, host,
    TransferFormat.FormatBytes(SlowSpeedThresholdBytesPerSec)) + suffix);
```

- [ ] **Step 4: Übrige Meldungen (Dedup beachten)**

Nach demselben Schema die restlichen ~13 Meldungen. Dabei WICHTIG: `"letztlich doch
fehlgeschlagen."` kommt an zwei Stellen mit identischem Text vor (`{host}`/`{manualSkipFallbackHost}`
bzw. `{slowAbortedHost}` als jeweiliges Argument) — EIN `Str`-Wert, an beiden Stellen verwendet.

- [ ] **Step 5: Build prüfen** (wie Task 1)

- [ ] **Step 6: Verifikations-Grep**

Run: `grep -n 'LogMessage?.Invoke($"\|LogMessage?.Invoke("' Core/Workers/Workers.cs`
Erwartet: keine Treffer mehr.

- [ ] **Step 7: Commit**

```bash
git add Infrastructure/Str.cs Infrastructure/LocalizationService.cs Core/Workers/Workers.cs
git commit -m "feat: Download-Worker-Log-Meldungen lokalisiert"
```

---

### Task 10: Volle Testsuite + manuelle Zweisprachigkeits-Verifikation

**Files:** keine Code-Änderungen — reine Verifikation.

- [ ] **Step 1: Volle Testsuite laufen lassen**

Run: `dotnet test ULM.Tests`
Expected: alle Tests grün. `AllStrValues_HaveGermanAndEnglishTranslation` deckt die neuen
`Str.Log_*`-Werte automatisch mit ab (iteriert über `Enum.GetValues<Str>()`).

- [ ] **Step 2: Deutsch — Regressionscheck**

`ulm_settings.ini`: `Language = de`. App starten, Programmstart abwarten, Stick anschließen
(oder Stick-Scan über UI auslösen), einen manuellen Download anstoßen, "Nach Updates suchen"
klicken. Log-Tab und Status-Tab-Verlauf müssen unverändert deutsch bleiben (Wortlaut identisch
zum Stand vor diesem Plan).

- [ ] **Step 3: Englisch — Durchsicht der wichtigsten Aktionspfade**

`Language = en`. Dieselben Aktionen wie Step 2 auslösen (Start, Stick-Scan, Download, manueller
Update-Check, URL-Check falls im Expert-Modus verfügbar, DB-Gesundheitscheck). Log-Tab und
Status-Tab-Verlauf durchsehen: kein deutscher Text mehr sichtbar, eingebettete Werte (Dateinamen,
Prozentzahlen, Versionsnummern) korrekt an der richtigen Stelle im übersetzten Satz.

- [ ] **Step 4: Bei Erfolg — nichts weiter zu tun**

Falls einer der Punkte in Step 2-3 nicht stimmt: zurück zu Phase 1 der systematic-debugging-Skill
(neue Evidenz sammeln, nicht direkt erneut fixen).

---

### Task 11: Finaler Whole-Branch-Review

**Files:** keine Code-Änderungen.

- [ ] **Step 1: Review-Paket erzeugen**

```bash
git merge-base feature/helpdialog-localization feature/log-history-localization
```

Ergebnis als `MERGE_BASE` notieren, dann:

```bash
"$HOME/.claude/plugins/cache/superpowers-marketplace/superpowers/6.1.1/skills/subagent-driven-development/scripts/review-package" MERGE_BASE HEAD
```

- [ ] **Step 2: Reviewer dispatchen**

Dispatch auf dem leistungsfähigsten verfügbaren Modell (Opus), mit demselben Prompt-Aufbau wie
beim HelpDialog-Whole-Branch-Review: Kontext (was wurde umgesetzt, welcher Zwischenfall/welche
Abweichung ist bekannt — hier: die bewusst weniger granularen Zwischenreviews statt Review pro
Einzel-Task), Pfad zu Spec/Plan, Pfad zum Review-Paket, explizite Prüfpunkte: Dedup korrekt
angewendet (insbesondere die in den Global Constraints gelisteten bekannten Fälle), kein
`string.Format` vergessen, keine Unicode-Verstümmelung, dateiweiter Sweep auf verbliebene
hartcodierte Strings in allen 5 betroffenen Dateien, alle referenzierten `Str.Log_*`-Namen
existieren in `Str.cs` mit De+En-Eintrag.

- [ ] **Step 3: Befunde auflösen**

Critical/Important-Befunde in einem gesammelten Fix-Commit beheben (nicht pro Befund einzeln
dispatchen), dann erneut reviewen lassen bis "Ready to merge: Yes".
