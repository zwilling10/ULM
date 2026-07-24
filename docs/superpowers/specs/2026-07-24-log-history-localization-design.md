# Spec: Log-Tab + Status-Tab-Verlauf lokalisieren (Phase 4)

## Ziel

Alle zur Laufzeit erzeugten Log-/Verlaufsmeldungen der Hauptanwendung ins Zweisprachigkeits-System
überführen (Deutsch/Englisch), analog zu den bereits abgeschlossenen Phasen 1-3 und 5. Betrifft
den Log-Tab (Freitext-Protokoll + `ulm.log`-Datei) und den Status-Tab-Verlauf
(`ActivityHistory`-Liste) des Hauptfensters.

## Umfang

| Datei | Mechanismus | ca. Meldungen |
|---|---|---|
| `ViewModels/MainViewModel.cs` | `Log(string)` → `LogMessage`-Event; `RecordHistory(string)` → `ActivityHistory` | ~90 (`Log`) + 6 (`RecordHistory`, alle bis auf eine mit identischem `Log`-Gegenstück) |
| `ViewModels/MainViewModel.cs` | hartcodierte `StatusText = "..."`-Literale, die noch nicht über Phase 3 abgedeckt sind | 6 |
| `Views/MainWindow.xaml.cs` | `AppendLog(string)` — schreibt in `LogBox` + `ulm.log` | 54 |
| `Core/Workers/Workers.cs` | `DownloadWorker.LogMessage?.Invoke(...)` — Event, das über `worker.LogMessage += msg => Log(msg)` in denselben Log-Strom fließt | 16 |
| `Core/Models/IsoEntry.cs` | `TryDelete(...)`-Fehlermeldung, per `Action<string>? log`-Callback an `Log`/`AppendLog` durchgereicht | 1 |

**Explizit NICHT im Scope** (spätere Phasen):
- `Views/VentoyInstallWindow.cs` (eigenes Fenster mit eigenem `AppendLog`, 7 Strings) → Phase 6.
- Alle anderen Dialoge (`ChangelogDialog.cs`, `DatabaseDialogs.cs`, `DownloadDialogs.cs`,
  `ManualSourceSearchDialog.cs`, `UpdateDownloadDialog.cs`, `QuickConfirmationWindow.cs`) → Phase 6.
- Übrige `Core/Services/*`-Fehlermeldungen (`HttpService.cs`, `SelfUpdateService.cs`,
  `DiscoveryService.cs`) → Phase 7.
- Die deutschen Distro-Beschreibungstexte in `Core/Services/IsoDatabaseService.cs` (Datenbank-Inhalt,
  keine UI-Strings im eigentlichen Sinn) → eigene, noch nicht eingeplante Initiative.

## Namenskonvention

Neues Präfix `Str.Log_...` (analog `Str.Msg_` aus Phase 3, `Str.Help_` aus Phase 5). Namen
beschreiben das Ereignis, nicht den Aufrufort, z.B. `Str.Log_AppStarted`,
`Str.Log_DbEntriesLoaded`, `Str.Log_MirrorTest`, `Str.Log_UpdateFound`.

## Technisches Muster

- Statische Meldungen: `LocalizationService.T(Str.Log_X)`.
- Meldungen mit eingebetteten Laufzeitwerten: `string.Format(LocalizationService.T(Str.Log_X), arg1, arg2, ...)` — exakt das in Phase 3 etablierte Muster.
- Emoji-Präfixe (💾🌐⚠✅❌🔒📋🗑✏↔🆕✓🔗🔄⚡⛔▶🐢⚡🔎🐢) bleiben Teil des übersetzten Textes, werden nicht herausgelöst — konsistent mit der bereits lokalisierten Log-Symbol-Legende im HelpDialog.
- Bedingte Meldungen (Ternary/Verzweigung im selben Aufruf) werden pro Zweig in einen eigenen `Str`-Wert aufgeteilt; die C#-Verzweigungslogik selbst bleibt unverändert erhalten. Beispiel:
  ```csharp
  Log(result.HasUpdate
      ? string.Format(LocalizationService.T(Str.Log_UpdateFound), result.Name, result.LocalVersion, result.RemoteVersion)
      : string.Format(LocalizationService.T(Str.Log_VersionCurrent), result.Name, result.RemoteVersion));
  ```
- Werte, die selbst keine natürliche Sprache sind (Laufwerksbuchstaben, Dateinamen, `ex.Message` von Exceptions, das `✓`/`✗`-Symbol, durch `string.Join` zusammengesetzte technische Listen), werden unverändert als Format-Argument durchgereicht, nicht übersetzt.

## Dedup-Prinzip

Exakt identische Meldungstexte (Zeichen-für-Zeichen, inkl. Emoji) bekommen **einen** gemeinsamen
`Str`-Wert, unabhängig davon, an wie vielen Stellen oder in wie vielen Dateien sie vorkommen.
Wichtig: Ähnliche, aber nicht identische Texte (z.B. `"⚠ {0}: nicht erreichbar."` vs.
`"❌ {0}: nicht erreichbar."` — unterschiedliches Emoji) bleiben **getrennte** `Str`-Werte. Bereits
bekannte Dedup-Kandidaten aus der Bestandsaufnahme:
- `"   ✏ {0} → {1}"` (Namensaktualisierung nach Versionswechsel) — 2 Aufrufstellen in `MainViewModel.cs`.
- `"💾 Datenbank: neu gefundene Download-Quelle(n) gespeichert."` — 2 Aufrufstellen in `MainViewModel.cs`.
- Die Ternary-Paar-Meldungen `Str.Log_UpdateFound`/`Str.Log_VersionCurrent`/`Str.Log_EntryUnreachable`
  (mit `⚠`) — jeweils 2 Aufrufstellen (automatischer + manueller Versionscheck).
- Alle 6 `RecordHistory`+`Log`-Paare mit identischem Text — ein `Str`-Wert, an beiden Stellen verwendet.
- `"🗑 Gelöscht: {0}"` — kommt sowohl in `MainViewModel.cs` als auch (über den gemeinsamen
  `IsoEntry.TryDelete`-Callback-Aufrufpfad) in `MainWindow.xaml.cs` vor.

Weitere Dedup-Fälle werden bei der eigentlichen Umsetzung entdeckt und ebenso behandelt — die
exakte Gesamtliste wird nicht in dieser Spec vorab transkribiert (siehe Abschnitt „Abweichung vom
bisherigen Vorgehen" unten).

## Abweichung vom bisherigen Vorgehen (Kostenbewusstsein)

Anders als in Phase 5 (HelpDialog), wo die Spec alle 171 Enum-Namen samt exaktem Vorher/Nachher-Text
vorab auflistete, verzichtet diese Spec auf die vollständige Verbatim-Transkription aller ~150
Meldungen. Grund: die Ausführung läuft diesmal nicht über Implementer-Subagenten, die einen
in sich vollständigen Brief brauchen, sondern direkt durch den Controller (siehe Plan-Dokument) —
der ohnehin jede betroffene Zeile liest, bevor er sie ändert. Das spart einen großen, redundanten
Transkriptionsschritt und hält die Kosten dieser ohnehin größten Phase im Rahmen (Nutzerwunsch,
siehe Konversation). Reviews finden nach thematisch gruppierten Blöcken statt statt nach jeder
einzelnen Ersetzung.

## Testing-Ansatz

Wie bei Phase 5: kein dediziertes Unit-Test-Harness für einzelne Log-Strings — der generische
`AllStrValues_HaveGermanAndEnglishTranslation`-Test deckt neue `Str`-Werte automatisch ab. Manuelle
Verifikation am Ende: App auf Deutsch und Englisch starten, eine repräsentative Abfolge von
Aktionen auslösen (Programmstart, Stick-Scan, Download, manueller Update-Check, URL-Check,
DB-Gesundheitscheck, Abbruch), Log-Tab und Status-Tab-Verlauf auf verbliebenes Deutsch in der
englischen Ansicht prüfen sowie auf unveränderten deutschen Text in der deutschen Ansicht
(Regressionscheck).
