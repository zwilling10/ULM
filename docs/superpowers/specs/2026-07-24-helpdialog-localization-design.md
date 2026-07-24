# Zweisprachigkeit — Phase 5: HelpDialog lokalisieren — Design

## Kontext

Phase 1–3 (siehe `docs/superpowers/specs/2026-07-22-bilingual-ui-infrastructure-design.md`,
`docs/superpowers/specs/2026-07-23-setupdialog-localization-design.md`,
`docs/superpowers/specs/2026-07-24-mainwindow-localization-design.md`) haben Hauptfenster-Rahmen,
`SetupDialog` und den kompletten Rest des Hauptfensters lokalisiert. `Views/Dialogs/HelpDialog.cs`
war in allen drei Phasen explizit als „später, eigene Phase" ausgeklammert — der Nutzer hat jetzt
priorisiert, mit `HelpDialog` weiterzumachen, VOR Phase 4 (Log-/Aktivitätsverlauf).

`HelpDialog` ist der „❓ Hilfe"-Button-Dialog — eine reine Dokumentationsseite (13 Abschnitte mit
Sprungmarken-Leiste), komplett in C# gebaut (`Views/Dialogs/HelpDialog.cs`, 729 Zeilen), nach
demselben Baumuster wie `SetupDialogs.cs` (Hilfsmethoden wie `MakeText`/`MakeItem`, die
String-Argumente entgegennehmen und daraus `TextBlock`/`Grid`-Elemente bauen).

## Bestandsaufnahme

**171 zu lokalisierende Textstellen** — deutlich mehr als jede bisherige Phase, aber technisch
einfacher als Phase 3 (reiner C#-Code, keine XAML-Bindungs-Komplikationen, keine
`string.Format`-Fälle — nirgends werden Laufzeitwerte in die Hilfetexte eingebettet; Beispiele
wie „vX.Y.Z" sind wörtlicher Platzhaltertext IN der Dokumentation, keine echten
Interpolationen).

Aufschlüsselung:

| Kategorie | Anzahl |
|---|---|
| Fenster-Chrome (Titel, Untertitel, „SPRUNGMARKEN"-Überschrift, „✔ Schließen"-Button) | 4 |
| 13 Abschnitte: je Inhalts-Titel + Sprungmarken-Text | 26 |
| 5 Zwischenüberschriften (`MakeSubhead`) | 5 |
| 10 einleitende Absätze (`MakeText`) | 10 |
| 56 Label+Text-Paare (`MakeItem`) | 112 |
| 7 Farb-Erklärungen (`MakeColorItem`, Label+Beschreibung) | 14 |
| **Gesamt** | **171** |

Viele Texte sind mehrzeilige Absätze (einige über 10 Zeilen), nicht nur kurze UI-Labels — das
ist inhaltlich näher an Dokumentations-Übersetzung als an UI-Label-Übersetzung.

**Separater Fund (nicht Teil dieser Phase):** Der Hilfetext beschreibt an zwei Stellen (Zeile 262,
492) noch die alten, separaten „Modus: Anwender/Experte"- und „Design: …"-Buttons, die in der
Einstellungen-Konsolidierung durch einen einzigen „⚙ Settings"-Button ersetzt wurden — ein
inhaltlicher Fehler, unabhängig von der Übersetzung. Als eigene Aufgabe ausgelagert (siehe
Session-Task „HelpDialog beschreibt entfernte UI"), NICHT Teil dieses Plans. Die Übersetzung
überträgt den bestehenden (fehlerhaften) Text 1:1 in beide Sprachen — Korrektur folgt separat.

## Ziel

Jeder String-Parameter, der aktuell an `MakeTitle`/`MakeSub`/`MakeText`/`MakeItem`/
`MakeColorItem`/`MakeSubhead`/`AddSection` sowie der `Title`-Property und dem
„✔ Schließen"-Button übergeben wird, läuft über `LocalizationService.T(Str.Help_...)`.

## Entscheidungen (im Brainstorming geklärt)

- **Umfang:** alles auf einmal (171 Strings in einem Plan) — der Dialog ist technisch homogen,
  keine Architektur-Verzweigungen wie im Hauptfenster, eine Aufteilung in Unterphasen hätte
  keinen technischen Vorteil.
- **Kein `string.Format` nötig:** anders als in Phase 3 gibt es hier keine echten
  Laufzeitwert-Einbettungen — alle Texte sind vollständig statisch.
- **Terminologie-Glossar** (für konsistente Übersetzung über alle 171 Strings):

  | Deutsch | Englisch |
  |---|---|
  | Arbeitsordner | working folder |
  | Stick / USB-Stick | stick / USB stick |
  | Datenmüll(-Schutz) | junk file (protection) |
  | Prüfsumme | checksum |
  | Referenzhash | reference hash |
  | Sprungmarken | Quick Links |
  | Übersicht | Overview |
  | Bedienung | Usage |
  | Gesundheitscheck | Health Check |
  | Datenbank | database |
  | Versionscheck | version check |
  | Hintergrund-Scan | background scan |
  | Fortschritt(sanzeige) | progress (indicator) |
  | Protokoll | log |

- **Content-Bug wird NICHT im Rahmen dieser Übersetzung korrigiert** (siehe Bestandsaufnahme) —
  beide Sprachversionen übernehmen den bestehenden Text unverändert inhaltlich identisch zum
  Original, nur übersetzt.

## Architektur

### `Infrastructure/Str.cs` — 171 neue Enum-Werte, `Help_`-Präfix

Benennungsschema (Reihenfolge = Reihenfolge im Dialog):

```csharp
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
```

(171 Werte gezählt, exakte Liste s.o. — 4 Chrome + 26 Sektionen + 5 Subheads + 10 Intro-Texte +
112 Item-Paare + 14 Farb-Paare.)

### `Views/Dialogs/HelpDialog.cs` — Verwendung

Reine Argument-Ersetzung, identisches Muster wie `SetupDialogs.cs`:

```csharp
content.Children.Add(MakeItem("1. Online-Versionscheck", "Fragt zuerst..."));
```

wird zu

```csharp
content.Children.Add(MakeItem(LocalizationService.T(Str.Help_Item_OnlineCheck_Label),
                                LocalizationService.T(Str.Help_Item_OnlineCheck_Body)));
```

`AddSection(title, navLabel)` analog mit zwei `T(...)`-Aufrufen. Keine Strukturänderung an den
Hilfsmethoden selbst (`MakeText`/`MakeItem`/`MakeColorItem`/`MakeSubhead`/`MakeSection`/
`MakeNavLink` bleiben unverändert) — nur die Aufrufstellen im Konstruktor ändern sich.

## Testing

- Der bestehende Vollständigkeitstest deckt alle 171 neuen Werte automatisch ab.
- Keine neuen Spot-Tests nötig (keine `string.Format`-Fälle, reine 1:1-Textzuordnung).
- Kein UI-Automatisierungstest (unveränderte Projekt-Konvention).

## Manuelle Verifikation

1. `Language = de`: HelpDialog öffnen, stichprobenartig 3–4 Abschnitte gegen den Stand vor
   diesem Plan vergleichen (sollte identisch aussehen).
2. `Language = en`: HelpDialog öffnen, alle 13 Sprungmarken durchklicken, prüfen dass jeder
   Abschnitt vollständig englisch ist (Titel, Sprungmarken-Text, Zwischenüberschriften,
   Item-Labels, Fließtext) — kein Text-Umbruch/Layout-Problem durch längere/kürzere englische
   Texte.
3. Fenstertitel „❓ Universal Linux Manager — Help & Documentation" (o.ä.) und
   „✔ Close"-Button prüfen.

## Offene Fragen für spätere Phasen (nicht jetzt entscheiden)

- Phase 4 (Log-/Aktivitätsverlauf) bleibt die nächste offene, größte verbleibende Phase nach
  HelpDialog.
- Phase 6 (übrige Dialoge: `DownloadDialogs.cs`, `DatabaseDialogs.cs`, `ChangelogDialog.cs`,
  `ManualSourceSearchDialog.cs`, `UpdateDownloadDialog.cs`, `VentoyInstallWindow.cs`) und
  Phase 7 (Fehlermeldungen aus `Core/Services/*.cs`) unverändert.
- Der Content-Bug (veraltete Modus-/Design-Button-Beschreibung) ist als separate Aufgabe
  ausgelagert, nicht Teil dieses Plans.
