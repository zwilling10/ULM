## Release-Prozess

Bei "veröffentliche das"/"erstelle einen Release" o.ä.: zuerst fragen, ob
der Fahrplan in `docs/RELEASE.md` jetzt abgearbeitet werden soll (dessen
Schritt 0). Nach Bestätigung Schritt für Schritt befolgen, ohne die dort
bereits geklärten Punkte (Versionsnummer-Logik, SmartScreen-Hinweis,
Alte-Releases-Policy) erneut einzeln zu erfragen. Nur bei den dort
explizit gelisteten Sonderfällen nachfragen.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).

## Aktueller Arbeitsstand (Handoff)

- **Branch `feature/database-dialogs-localization`** (abgezweigt von `feature/log-history-localization`
  Tip `063880b`, NICHT von Phase 4 selbst — Phase 4 bleibt dadurch unangetastet/sauber). Entstanden,
  weil der Nutzer beim manuellen Testen von Phase 4 (Log/Status-Tab) nebenbei entdeckt hat, dass
  mehrere Datenbank-Dialoge (`Views/Dialogs/DatabaseDialogs.cs`, 857 Zeilen) UND
  `QuickConfirmationWindow.cs` komplett unlokalisiert waren (immer Deutsch, auch im Englisch-Modus)
  — regulär Phase-6-Scope, auf Nutzerwunsch ("Behebe erstmal diese") sofort direkt angegangen, ohne
  Spec/Plan-Zeremonie (analog zum direkten Vorgehen in Phase 4).
  - **Fertig und committet (`fe4fb71`):** Alle 7 Dialogklassen in `DatabaseDialogs.cs`
    (`IsoListDialog`, `IsoEditDialog`, `IsoSearchDialog`, `ImportStickIsosDialog`,
    `NewerVersionOnStickDialog`, `DbHealthCheckDialog`, `GitHubTokenDialog`) sowie
    `QuickConfirmationWindow.cs` (Title) vollständig lokalisiert. In `DownloadDialogs.cs` nur die
    eine geteilte `AppRes.AddCategoryCombo`-Zeile ("Kategorie *") — der Rest dieser Datei bewusst
    NICHT angefasst (eigener Phase-6-Scope). Dedup korrekt angewendet: `Str.Db_Btn_Cancel`
    (3x: IsoEditDialog/NewerVersionOnStickDialog/GitHubTokenDialog), `Str.Db_Btn_Save` (2x:
    IsoEditDialog/GitHubTokenDialog), `Str.Db_Btn_Close` (2x: IsoListDialog/DbHealthCheckDialog),
    `Str.Log_Unreachable` aus Task 9 (Workers.cs) für "nicht erreichbar" in DbHealthCheckDialog
    wiederverwendet statt neu angelegt. Build 0 Fehler, 198/198 Tests grün.
  - **Nachtrags-Fix, ebenfalls fertig und committet (`a8c46d5`):** Die anfangs bewusst offen
    gelassene Kategorie-Dropdown-Lücke (ComboBoxen zeigten rohe interne Schlüssel wie "Einsteiger"
    statt übersetztem Label) wurde auf erneuten Nutzerfund ("Search ISO"/"Database +New" Kategorien
    noch deutsch) doch noch in diesem Branch behoben — zwei neue Helper `AppRes.FillCategoryCombo`/
    `AppRes.SelectedCategory` (ComboBoxItem mit Content=Label/Tag=interner Schlüssel), angewendet in
    `IsoEditDialog`, `IsoSearchDialog`, `ImportStickIsosDialog` UND `ManualSourceSearchDialog.cs`
    (nutzt denselben geteilten `AppRes`-Helfer, daher notwendige Mitkorrektur trotz sonst
    unangetastetem Phase-6-Scope dieser Datei). Build 0 Fehler, 198/198 Tests grün.
  - **Nutzer hat erneut getestet (2026-07-28): "keine Fehler mehr gefunden" — Branch verifiziert.**
  - **Gebündelter Review (Sonnet) abgeschlossen: Approved, keine Änderungen nötig.** Alle
    Dedup-Entscheidungen und der Kategorie-Dropdown-Refactor im Detail gegengeprüft (alle 4
    Konsumenten lesen korrekt über `AppRes.SelectedCategory()` den `Tag`/internen Schlüssel
    zurück, nicht das übersetzte Label; keine verbliebene rohe `.SelectedItem`-Lesestelle).
  - **Branch fertig, getestet, reviewt — bleibt wie alle anderen Phasen-Branches eigenständig
    liegen, kein Merge ohne ausdrückliche Nutzer-Freigabe.**
  - **Nachtrag (2026-07-28, weiterer Nutzerfund beim Testen):** Beim Erkennen eines neuen
    USB-Sticks (nicht-Ventoy) und Bestätigen der "Als Ventoy-Stick einrichten?"-Abfrage öffnet
    sich `Views/VentoyInstallWindow.cs` — läuft in einer SEPARATEN, elevated Prozessinstanz
    (`Process.Start(... Verb="runas" ... --ventoy-install {letter} {updateMode} {secureBoot})`,
    siehe `App.xaml.cs` Zeile ~42). War komplett unlokalisiert (Titel, Log-Zeilen, Buttons), same
    für den zugehörigen `VentoyInstallWorker` in `Core/Workers/Workers.cs` (Zeilen 54-206, ca. 15
    `ProgressLog?.Invoke(...)`-Aufrufe, die direkt ins Fenster-Log fließen). Beides jetzt fertig
    lokalisiert, committet (`dc45f17`). Wichtige Erkenntnisse dabei:
    - `worker.Progress`-Event (pct, msg) — der `msg`-Parameter wird im Fenster NIE angezeigt
      (nur `pct` für Balken/Prozent-Text) — die dort übergebenen deutschen Texte ("Lade Ventoy …",
      "Entpacke …" etc.) sind toter Code für die Anzeige, bewusst NICHT lokalisiert (kein
      sichtbarer Effekt, unnötiger Aufwand).
    - Dedup: `Str.Log_VentoyUpdatedStatus`/`Str.Log_VentoyInstalledStatus` aus Task 5
      (MainViewModel.cs) für die Worker-Ergebniszeile "✅ Ventoy aktualisiert./installiert."
      wiederverwendet (exakt identischer Text). `Str.Row_Yes`/`Str.Row_No` ("Ja"/"Nein", aus
      früherer Phase) für die Secure-Boot-Anzeige wiederverwendet.
    - Fenster-`Title` ("Universal Linux Manager — Ventoy") bewusst NICHT angefasst — reiner
      Markenname, keine deutschen Wörter, sprachneutral.
    - Build 0 Fehler, 198/198 Tests grün. **Noch NICHT vom Nutzer im echten USB-Stick-Ablauf
      getestet** (nur Build+Tests grün, kein Stick-Insert-Test bisher).
  - **Nachtrag 2 (2026-07-28, Nutzerfund "Download Mode"-Ja/Nein/Abbrechen-Dialog):** Standard-
    `MessageBox`-Buttons (Ja/Nein/Abbrechen bei `MessageBoxButton.YesNo`/`YesNoCancel`/`OKCancel`,
    betrifft 14 von 34 `MessageBox.Show`-Aufrufen im Programm — die restlichen 19 nutzen nur "OK",
    das in beiden Sprachen gleich ist) werden von WPF/Windows selbst beschriftet, nicht über
    unseren `LocalizationService` — hängt an der Windows-System-UI-Sprache, nicht am
    ULM-Sprachschalter. Versuch, das per `Thread.CurrentThread.CurrentUICulture`-Fix in
    `App.xaml.cs` zu erzwingen, wurde vom Nutzer explizit **abgelehnt** ("Windows-Systemsprache
    soll entscheiden") und **zurückgenommen** — `App.xaml.cs` ist wieder identisch mit dem Stand
    vor diesem Versuch. **Bewusste, vom Nutzer bestätigte Entscheidung: native MessageBox-Buttons
    bleiben absichtlich an der Windows-Sprache hängen, nicht an ULM.** Für zukünftige Sessions:
    dieses Verhalten NICHT erneut als Bug behandeln.
  - **Nachtrag 3 (2026-07-28, Nutzerfund "Parallele Downloads"-Fenster):**
    `Views/Dialogs/DownloadDialogs.cs` (608 Zeilen, 4 Klassen: `DownloadSlotsDialog`,
    `DownloadProgressDialog`, `OrphanedDownloadsDialog`, `DriveSelectDialog`) war bis auf die
    eine geteilte `AppRes`-Kategorie-Zeile komplett unlokalisiert — jetzt vollständig lokalisiert,
    committet (`33d66b6`). Wichtige Details:
    - `OrphanedDownloadsDialog`s Konstruktor-Default-Parameter (`title`/`itemLabel`) konnten nicht
      direkt auf `LocalizationService.T(...)` gesetzt werden (keine Kompilierzeit-Konstante) —
      Signatur auf `string?` umgestellt, Auflösung auf `?? LocalizationService.T(...)` im Body.
      Betraf einen Aufrufer ohne explizite Parameter (`MainWindow.xaml.cs`
      `RunLocalFileMaintenanceAsync`), der bisher unbemerkt die deutschen Default-Werte zeigte.
    - Dedup: `Str.Db_Btn_CloseSimple`/`Str.Db_Btn_Skip` (aus Datenbank-Dialoge-Phase) sowie
      `Str.Log_OperationSucceededLogPrefix` ("✅ {0}", aus Phase 4 Task 8) wiederverwendet.
    - Build 0 Fehler, 198/198 Tests grün. **Noch NICHT vom Nutzer getestet.**
  - **Nachtrag 4 (2026-07-28, drei weitere Nutzerfunde beim Testen — alle behoben, committet
    `5b9f22b`):**
    1. **`TransferFormat.BuildDetail`** (`Core/Workers/Workers.cs`, geteilte Formatierungsfunktion
       für Download- UND Kopier-Fortschritts-Detailanzeige): "noch {eta}" hartcodiert deutsch, war
       der Screenshot-Fund "Zorin OS 18 Core — 62,6 MB/s · noch 32s · 1,5 GB / 3,5 GB". Da Englisch
       eine andere Wortstellung braucht ("32s left" statt "still 32s"), zwei komplette
       Satzschablonen statt Wort-Ersetzung: `Str.Xfer_DetailWithEta`. Der `else`-Zweig ohne ETA
       enthielt kein deutsches Wort, unverändert gelassen.
    2. **`DownloadWorker.sa.Status`** (`Core/Workers/Workers.cs`, 11 Fundstellen) — der laufend
       aktualisierte Status-Text pro Zeile im `DownloadProgressDialog` (war der ursprüngliche
       Screenshot-Fund "Ubuntu 26.04 LTS ✗ Fehlgeschlagen — alle Mirror versucht"). Bewusst
       getrennt von Task 9s bereits lokalisierten `LogMessage?.Invoke(...)`-Aufrufen (andere
       Aufrufstellen, `Str.Log_TryingUrl` NICHT mit den neuen `Str.DlStatus_*`-Werten verwechselt).
    3. **`OperationSucceeded`-Nachrichtentexte** (`ViewModels/MainViewModel.cs`, 3 Aufrufstellen:
       `BuildPipelineCompletionMessage`, reiner Download-Modus, reiner Kopier-Modus) — die
       mehrfach in dieser Session als bewusst zurückgestellte Lücke dokumentierte
       "Operation Completed"-MessageBox-Body-Stelle (Titel war schon lokalisiert). Jetzt fertig.
    - **Wichtige Erkenntnis (Testisolation):** `BuildPipelineCompletionMessage` ist `internal
      static` und wird von `ULM.Tests/MainViewModelPipelineSummaryTests.cs` per Text-Vergleich auf
      DEUTSCHEN Text getestet. Nach der Umstellung auf `LocalizationService.T(...)` hängt der
      Rückgabewert von `LocalizationService.Current` ab — einem globalen, statischen Zustand, den
      `LocalizationServiceSetLanguageTests.SetLanguage_WritesToIniAndUpdatesCurrent` in einer
      ANDEREN Testklasse mutiert. xUnit führt verschiedene Testklassen standardmäßig PARALLEL aus
      → Flaky Test (schlug zunächst 1-2x von 198 fehl, je nach Ausführungsreihenfolge/Timing).
      Gelöst mit einer neuen `[CollectionDefinition("LocalizationCurrent", DisableParallelization
      = true)]` in `LocalizationServiceTests.cs`, angewendet auf beide betroffenen Testklassen via
      `[Collection("LocalizationCurrent")]` — serialisiert nur diese zwei Klassen gegeneinander,
      der Rest der Suite bleibt parallel. `MainViewModelPipelineSummaryTests` setzt zusätzlich im
      Konstruktor explizit `LocalizationService.SetLanguage(AppLanguage.German, ...)`. Mit 3x
      wiederholtem vollem Testlauf verifiziert (198/198 jedes Mal, keine Flakiness mehr). **Für
      künftige Phasen wichtig:** jede weitere Umstellung von bisher testbaren `internal static`-
      Methoden auf `LocalizationService.T(...)` kann denselben Flaky-Test-Effekt auslösen — darauf
      achten, ob ein Test direkt auf den (bisher hartcodierten) Text prüft.
    - Build 0 Fehler, 198/198 Tests grün (3x wiederholt).
  - **Nachtrag 5 (2026-07-28, Nutzerfund "noch 2m 12s" trotz Nachtrag 4):** `TransferFormat.
    BuildDetail` war NICHT die einzige Implementierung dieser Formatierung — reine Code-Dopplung,
    beim ersten Fix übersehen. Zwei weitere, unabhängige Kopien: `MainViewModel.
    BuildTransferDetail` (private static, Zeile ~1164, für Kopiervorgang-Fortschritt — exakt
    dieselbe Satzschablone wie TransferFormat.BuildDetail, daher `Str.Xfer_DetailWithEta`
    wiederverwendet) und `HttpService.cs` DownloadAsync-Fortschritts-Callback (Zeile ~1233, für
    Download-Fortschritt — ANDERE Feldreihenfolge/Struktur als die anderen beiden, daher neuer,
    eigener Wert `Str.Xfer_EtaSuffix` = "  ·  noch {0}"/"  ·  {0} left"). Committet (`016bad4`).
    Build 0 Fehler, 198/198 Tests grün. Lehre: bei
    Formatierungs-Bugfixes IMMER nach mehreren unabhängigen Implementierungen derselben Logik
    suchen (`grep` nach dem charakteristischen Textfragment im GESAMTEN Repo, nicht nur in der
    Datei, wo der erste Treffer lag).
  - **Nachtrag 6 (2026-07-28, Nutzerfund "unten links im Hauptfenster: Lade herunter"):**
    `DownloadWorker.OverallProgress` (Zeile ~618) speist direkt `MainViewModel.StatusText`
    (Hauptfenster-Statuszeile unten links) — war nie Teil eines Plans, jetzt lokalisiert
    (`Str.Dl_OverallProgressDetail`). Beim Gegenprüfen dieselbe Lücke auch in `CopyToUsbWorker`
    gefunden (analoge Struktur, `worker.Progress += (pct, detail) => StatusText = detail;` OHNE
    Emoji-Wrapper diesmal) — alle sichtbaren `FileProgress`/`Progress`/`Completed`-Texte dort
    lokalisiert: "Keine Dateien.", Speicherplatz-Fehler, "Startet …"/"Abgebrochen"/"Fertig" (pro
    Datei UND Gesamt-Ergebnis, dedupliziert), "Kopiert {0}/{1}…" (Gesamt-Fortschritt, das
    Kopier-Pendant zum User-Fund), Exception-Fehlertext (`Str.DlStatus_GeneralError`
    wiederverwendet, exakt identisches "Fehler: {0}"-Muster). **Bewusst NICHT angefasst:**
    `UrlCheckWorker.Completed`s zweiter Parameter ("Abgebrochen"/"OK") — Aufrufer in
    `MainViewModel.OnCheckUrls` verwirft ihn explizit mit `_` (Discard), toter Code, kein
    sichtbarer Effekt. Committet (`9cbd5cc`). Build 0 Fehler, 198/198 Tests grün.
  - **Nachtrag 7 (2026-07-28, Nutzerfund "Update-Check abgeschlossen: All up to date." im
    "✔ Done"-Fenster):** Das war die schon in einem früheren Task-4+5-Review als Nitpick
    vermerkte, bewusst zurückgestellte Lücke: `QuickCheckSucceeded?.Invoke(...)` hatte an 2
    Stellen einen hartcodierten deutschen Präfix vor dem bereits lokalisierten `StatusText`
    (Zeile ~748 Integritätsprüfung, Zeile ~1253 Update-Check). Jetzt behoben
    (`Str.QuickConfirm_IntegrityCheckDone`/`Str.QuickConfirm_UpdateCheckDone`). Die dritte Stelle
    (Zeile ~1280, URL-Check) brauchte keine Änderung — übergibt `StatusText` bereits direkt ohne
    Präfix. Committet (`e0edf46`). Build 0 Fehler, 198/198 Tests grün.
  - **Nachtrag 8 (2026-07-28, "Show info Mouse-over" — echtes Feature statt Bugfix, auf
    Nutzerwunsch trotzdem umgesetzt):** Der Zeilen-Tooltip (`TipTooltip` in `IsoViewModels.cs`)
    war fast vollständig lokalisiert (Symbol-Erklärungen) — der einzige verbleibende deutsche
    Teil war `_entry.Tip`, die freie Distro-Beschreibung aus der Datenbank (Content, kein
    UI-String). Da es dafür keine englische Variante gab, wurde ein neues optionales Feld
    `IsoEntry.TipEn` ergänzt (Fallback auf `Tip`, falls leer) — inkl. Anpassung an
    `IsoDatabaseService` (Load/Save/`DefaultDatabase`, jetzt 13 statt 12 Spalten) und einem neuen
    Eingabefeld in `IsoEditDialog`/`ManualSourceSearchDialog`. Alle 27 Standard-Distros in
    `DefaultDatabase` (Core/Services/IsoDatabaseService.cs, Zeilen ~199-425) wurden mit englischer
    Übersetzung ergänzt. **Wichtig:** Das greift nur für NEU geladene Standard-Datenbanken
    (`LoadDefaults()`) oder Einträge, die manuell im Editor gepflegt werden — eine bereits
    bestehende `ulm_isos.ini` eines Nutzers bekommt die neuen TipEn-Werte nicht automatisch
    nachgetragen (kein Migrationsschritt, bewusst wie beim bestehenden Sha256-Präzedenzfall:
    `GetValueOrDefault` liefert für alte Dateien klaglos leeren String, TipTooltip fällt dann auf
    Tip/Deutsch zurück). Committet (`8e53ee0`). Build 0 Fehler, 198/198 Tests grün.
  - **Nachtrag 9 (2026-07-28, Nutzer hatte bestehende Datenbank ohne TipEn):** Die Vermutung aus
    Nachtrag 8 traf zu — Nutzer hat eine echte `ulm_isos.ini` unter seinem konfigurierten
    Arbeitsverzeichnis (einem selbst gewählten Ordner außerhalb des Repos, siehe
    `BaseDirectory` in `ulm_settings.ini` — NICHT im Build-Ordner, mein ursprünglicher Check dort
    war deshalb falsch-negativ). Statt "Datei löschen" (hätte auch alle sonstigen Anpassungen
    gelöscht) einen sicheren automatischen Nachtrag beim Laden ergänzt:
    `IsoDatabaseService.BackfillMissingTipEn()` — läuft nach jedem `LoadFromIni()`, ergänzt NUR
    `TipEn` (nur wenn aktuell leer) per Namensabgleich mit `DefaultDatabase`, alle anderen Felder
    bleiben unangetastet, speichert nur bei tatsächlicher Änderung. Behebt das Problem für JEDEN
    Nutzer, der von einer Version ohne TipEn aktualisiert, nicht nur für diesen Testlauf.
    Committet (`4b3c6fd`). Build 0 Fehler, 198/198 Tests grün. **Noch NICHT vom Nutzer bestätigt**
    — beim nächsten Start sollte die Nachtrags-Ergänzung automatisch greifen (kein manueller
    Eingriff/Löschen nötig).
  - **Nächster Schritt:** Nutzer testet die verbleibenden offenen Nachträge (Ventoy-
    Installationsablauf aus Nachtrag 1, Download-Dialoge aus Nachtrag 3, sowie Nachtrag 4-6),
    danach gebündelter Review für den gesamten Branch, dann zurück zu Phase 4 Task 10/11 auf
    `feature/log-history-localization` (Branch wechseln!).


- **Letzter Fokus:** Zweisprachigkeit (Deutsch/Englisch), phasenweise nach dem Muster
  Brainstorm → Spec → Plan → Subagent-Driven-Development → Verifikation. Roadmap:
  Phase 1 (Infrastruktur) → Settings-Konsolidierung → Phase 2 (SetupDialog) → Phase 3
  (MainWindow) → Phase 5 (HelpDialog, vor Phase 4 vorgezogen auf Nutzerwunsch) →
  Phase 4 (Log/Aktivitätsverlauf, noch offen) → Phase 6 (weitere Dialoge) → Phase 7
  (Core/Services-Fehlermeldungen).

- **Aktueller Status (2026-07-24):**
  - `v2.39.1` released (Selbst-Update-Neustart-Absturz behoben, Fix bereits live).
  - Branch `feature/bilingual-ui-phase1`: Phase-1-Infrastruktur fertig, PR
    [#6](https://github.com/zwilling10/ULM/pull/6) offen, noch nicht gemerged.
  - Branch `feature/settings-consolidation`: Design/Sprache/Modus-Konsolidierung
    (`⚙ Einstellungen`-Button) fertig implementiert, Build + Tests grün, End-to-End
    verifiziert. **Noch NICHT gepusht/gemerged** — Nutzer testet lokal.
  - Branch `feature/setupdialog-localization`: Phase 2 (SetupDialog, ~30 Strings)
    fertig implementiert und reviewt. **Noch NICHT gemerged.**
  - Branch `feature/mainwindow-localization`: Phase 3 (MainWindow — Header, Toolbar,
    Spalten, Status-Tab, Kategorien, alle MessageBoxen) fertig, 13 Tasks + finaler
    Review, inkl. 3 Nachtrags-Fixes aus echtem Nutzertest (ScanHintText,
    DriveInfoText, Run.Text-Bindings brauchten explizites `Mode=OneWay` — WPF-Falle:
    `Run.Text` defaultet auf TwoWay, anders als `TextBlock.Text`). **Noch NICHT
    gemerged.**
  - Branch `feature/helpdialog-localization` (aktuell ausgecheckt, HEAD `17b2322`):
    Phase 5 (HelpDialog, 171 Strings) komplett fertig — alle 10 Tasks + finaler
    Whole-Branch-Review (Opus) ohne Critical/Important-Befunde, "Ready to merge:
    Yes", 198/198 Tests grün. Ein während der Umsetzung aufgetretener Konflikt
    (eine separat ausgelagerte Content-Fix-Session hatte mitten im Plan einen Commit
    direkt auf denselben Branch gesetzt und dadurch zwei bereits übersetzte Strings
    veralten lassen) wurde vollständig aufgelöst (Commits `c1db795`, `48026e8`) und
    im finalen Review gegengeprüft. **Noch NICHT gemerged** — bleibt wie alle
    vorherigen Phasen-Branches eigenständig liegen, bis der Nutzer lokal getestet hat.

- **Phase 4 (Log-Tab/Status-Tab-Verlauf) gestartet, mitten in Task 3 pausiert
  (2026-07-24, Nutzer-Limit erschöpft — Wochenlimit 75%, Monatslimit 74%,
  Session-Burn-Rate-Prognose sagte Kostenlimit-Überschreitung vor Reset voraus):**
  - Branch `feature/log-history-localization` (abgezweigt von `feature/helpdialog-localization`
    Tip `17b2322`). Spec: `docs/superpowers/specs/2026-07-24-log-history-localization-design.md`.
    Plan: `docs/superpowers/plans/2026-07-24-log-history-localization.md` (11 Tasks).
  - Abweichung vom subagent-driven-development-Muster (bewusst, Nutzerwunsch):
    Controller macht die Ersetzungen direkt, kein Implementer-Subagent pro Task,
    Review erst nach mehreren Tasks gebündelt statt pro Einzeltask.
  - Task 1 (Startup+DB-Wartung), Task 2 (USB-Stick-Scan), Task 3 (Integrität+Ventoy-Bootmenü+
    Versionscheck), Task 4 (Download+Kopieren), Task 5 (Update-Check/URL-Check/DB-Health/
    Ventoy-Install/Abbruch) fertig, committet (`40c269c`, `529fb0a`, `526cfeb`, `776586b`,
    `5dae00e`). Tasks 1-3 gebündelt reviewt (Sonnet): Approved, keine Befunde. Task 4+5 noch
    NICHT reviewt (nächster gebündelter Review sollte beide zusammen abdecken).
  - **Bewusst NICHT lokalisiert in Task 4 (wichtige Scope-Entscheidungen, unbedingt
    beibehalten):** `CopyItemProgress?.Invoke(...)`-Textinhalte (Fortschrittsdialog-Anzeige,
    z.B. "⚠ Quelldatei nicht gefunden") gehören zu `DownloadDialogs.cs`, Phase 6. Die
    `msg`-Variablen für `OperationSucceeded?.Invoke(...)` (MessageBox-Body, inkl.
    `BuildPipelineCompletionMessage`-Methode, Zeilen ~962/982-984/1207-1209) — das ist eine
    von Phase 3 offenbar übersehene MessageBox-Body-Lücke (Titel war schon lokalisiert:
    `Str.Msg_OperationComplete_Title`), aber nicht Teil dieser Phase; separat vormerken, z.B.
    für Phase 6. `FormatNextAutoCheckText`/`FormatLastAutoCheckText` (Zeilen ~1014-1030,
    Status-Reiter "Nächste geplante Aktion") ebenfalls nicht angefasst — vermutlich
    Phase-3-Restbereich, nicht Log/History. `worker.OverallProgress`-Zeile ("⬇ {0}", detail
    kommt vom Worker) und die `StatusText = "❌ " + message`-Konkatenation (message kommt vom
    CopyToUsbWorker in Workers.cs) ebenfalls unangetastet gelassen — waren nie Teil des Plans.
  - **Task 5 — Abweichung vom Plandokument entdeckt und korrekt aufgelöst (wichtig für
    künftige Tasks):** Der Plan-Text behauptete, `OnCheckUpdates`s Ternary könne
    `Str.Log_EntryUnreachable`/`Str.Log_VersionCurrent` aus Task 3 wiederverwenden — beim
    Gegenlesen des tatsächlichen Codes stimmte das nicht mehr (abweichendes Emoji ❌ statt ⚠,
    fehlendes "(aktuell)"-Suffix). Stattdessen zeichengenau dedupliziert mit dem strukturell
    identischen Ternary in `RunHealthCheck` (Zeile ~1313, ebenfalls Task-5-Scope): neue Werte
    `Str.Log_ManualCheckUnreachable`/`Str.Log_ManualCheckCurrent` decken jetzt BEIDE Stellen ab.
    `Str.Log_UpdateFound` aus Task 3 wird weiterhin gültig wiederverwendet (Text war dort exakt
    gleich). Lehre: Bei Dedup-Entscheidungen immer den aktuellen Code gegenlesen, nicht dem
    Plan-Text blind vertrauen — der Plan kann durch zwischenzeitliche Bugfixes veralten. Außerdem
    fehlte im vorbereiteten 34-Werte-Enum-Block `Str.Log_AppClosing` (für
    `SaveAndClose()`/"▶ Anwendung wird beendet.") — als 35. Wert ergänzt.
  - **Task 4+5 gebündelt reviewt (Sonnet-Subagent): Approved, keine Änderungen nötig.** Alle
    Dedup-Entscheidungen gegengeprüft und bestätigt, Build + 198/198 Tests grün. Ein Nitpick für
    Phase 6 vermerkt: `QuickCheckSucceeded?.Invoke($"Update-Check abgeschlossen: {StatusText}")`
    (Zeile ~1256, Task 5) und die analoge Stelle aus Task 3 (Zeile ~748,
    `"Integritätsprüfung {0} abgeschlossen: ..."`) haben einen hartcodierten deutschen Präfix vor
    dem bereits lokalisierten `StatusText`. Bewusst nicht in Phase 4 angefasst — gehört zu
    `QuickConfirmationWindow`/Phase 6, siehe bestehende Scope-Entscheidung oben.
  - Task 6 (`MainWindow.xaml.cs` — Import/Datenmüll-Dialoge, Zeilen ~64-135) fertig, committet
    (`8637260`). `Str.Log_Deleted`/`Str.Log_FailedSuffix` aus Task 4 korrekt wiederverwendet
    (Text zeichengenau identisch geprüft). Noch NICHT reviewt.
  - Task 7 (`MainWindow.xaml.cs` — Programmstart/Update-Check/Datei-Wartung, Zeilen ~221-573)
    fertig, committet (`bc83a72`). `Str.Log_Deleted`/`Str.Log_FailedSuffix` aus Task 4 erneut
    korrekt wiederverwendet. Wichtige Abgrenzung dabei bestätigt: `Log_MaintenanceSkipped`
    ("ℹ Wartung übersprungen …") ist NICHT dasselbe wie Task 6s `Log_StickMaintenanceSkipped`
    ("ℹ Stick-Wartung übersprungen …") — unterschiedlicher Text, bewusst getrennt gehalten.
    Noch NICHT reviewt.
  - Task 8 (`MainWindow.xaml.cs` Rest — Ventoy/DB-Suche/Abbruch + `Core/Models/IsoEntry.cs`
    `TryDelete`) fertig, committet (`cd315fd`). `Str.Log_Deleted`/`Str.Log_FailedSuffix` (Task 4)
    und `Str.Log_VentoyActionWordUpdate`/`_Install` (Task 5) korrekt wiederverwendet. **Lücke im
    Plandokument entdeckt und mitbehoben:** Zeile ~167 (`AppendLog($"✅
    {message.Split('\n')[0]}")` im `OperationSucceeded`-Handler) lag zwischen Task 6s und Task 7s
    Zeilenbereichen und war in keiner Task-Beschreibung erfasst — beim datei-weiten
    Verifikations-Grep (Task 8 Step 5, "keine Treffer mehr in der GESAMTEN Datei") aufgefallen und
    ergänzt (`Str.Log_OperationSucceededLogPrefix` = "✅ {0}"; der eingebettete `message`-Body
    bleibt bewusst unübersetzt, siehe bestehende OperationSucceeded-Scope-Entscheidung).
    `MainWindow.xaml.cs` ist damit vollständig lokalisiert (letzter Task für diese Datei laut
    Plan). **Bewusst NICHT lokalisiert:** `StatusLbl.Text = $"✅ Ventoy-Stick: {nd.Letter}"`
    (Zeile ~704, `OnNewDriveInserted`) — stand nicht in Task 8s Produziert-Liste und ist kein
    `AppendLog`-Aufruf; vermutlich ein weiterer Phase-3-Restbereich analog zu
    `FormatNextAutoCheckText`/`OperationSucceeded`-Body — für eine spätere Phase vormerken.
    Noch NICHT reviewt.
  - Task 9 (`Core/Workers/Workers.cs` — DownloadWorker-Meldungen, 16 Fundstellen) fertig,
    committet (`063880b`). `Str.Log_UltimatelyFailed` korrekt an 2 Stellen dedupliziert (identischer
    Text, unterschiedliche Host-Variable als Argument). **Damit sind alle 9 Code-Tasks von
    Phase 4 abgeschlossen** — kein hartcodierter Log/StatusText/AppendLog/LogMessage-String mehr
    im gesamten definierten Scope (`MainViewModel.cs`, `MainWindow.xaml.cs`, `Workers.cs`,
    `IsoEntry.cs`). Noch NICHT reviewt (Task 6+7+8+9 warten auf gebündelten Review).
  - **Nächster Schritt:** Task 10 (volle Testsuite + manuelle Zweisprachigkeits-Verifikation
    Deutsch/Englisch in der laufenden App) und Task 11 (finaler Whole-Branch-Review, Opus) laut
    Plan. Vor Weitermachen `git status`/`git log --oneline -5` pruefen, um sicherzugehen, dass
    Task 9 (`063880b`) der aktuelle Stand ist.

- **Weitere offene Branches, warten auf Nutzer-Feedback:** `feature/settings-consolidation`,
  `feature/setupdialog-localization`, `feature/mainwindow-localization`,
  `feature/helpdialog-localization` — alle eigenständig liegen lassen, kein Merge ohne
  ausdrückliche Nutzer-Freigabe.
