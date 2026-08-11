# Design: "Uli" — Lokaler Chat-Avatar-Assistent für die Windows-Hauptapp

**Datum:** 2026-08-10
**Status:** Entwurf, vom Nutzer abschnittsweise freigegeben
**Scope:** Nur Windows-WPF-Hauptapp (`UniversalLinuxManager.csproj`). Linux-GUI (Avalonia) ist explizit nicht Teil dieses Designs.

## Ziel

Ein schwebender Avatar-Button ("Uli", 🐧) im Hauptfenster öffnet ein nicht-modales
Chat-Fenster, in dem der Nutzer frei tippen oder Themen-Buttons klicken kann, um
Hilfe zu häufigen Fragen rund um ULM zu bekommen. Der Assistent berät nur (Q&A) —
er löst keine Aktionen in der App aus. Die Antworten kommen aus einem lokalen,
vordefinierten Fragen-Katalog (kein Cloud-LLM, kein API-Key, keine Kosten, kein
Netzwerkzugriff).

## Nicht-Ziele

- Keine echte KI/LLM-Anbindung (bewusste Entscheidung — Chat-*Optik*, aber lokale
  Keyword-Zuordnung dahinter).
- Kein agentisches Verhalten (keine Ausführung von Downloads, Kopiervorgängen etc.
  durch den Assistenten selbst).
- Keine Persistenz des Chat-Verlaufs über App-Neustarts hinweg.
- Kein Ersatz der bestehenden `HelpDialog` — die bleibt unverändert bestehen.
- Keine Linux-Umsetzung in dieser Phase.

## Architektur

Neues, eigenständiges Projekt **`ULM.Assistant`** (`net8.0-windows`, WPF-Class-
Library) in einem neuen Ordner `ULM.Assistant/` im Repo-Root, analog zum
bestehenden `Linux/`-Projekt als abgegrenzte Einheit.

- **Korrektur gegenüber der ursprünglichen Annahme:** `Core/` und `Infrastructure/`
  (inkl. `LocalizationService`) sind KEIN eigenes Projekt, sondern nur Ordner
  innerhalb von `UniversalLinuxManager.csproj` selbst (per implizitem Compile-Glob
  kompiliert). Ein `<ProjectReference>` von `ULM.Assistant` auf "Core" ist daher
  nicht möglich — und da die Haupt-App umgekehrt `ULM.Assistant` referenzieren muss
  (für den Avatar-Button), würde ein Reference-Zyklus entstehen.
- Stattdessen bleibt `ULM.Assistant` **komplett unabhängig** (keine
  `<ProjectReference>` in irgendeine Richtung außer der Haupt-App → `ULM.Assistant`).
  Die aktuelle Sprache wird per Dependency Injection hereingereicht: `AvatarButton`
  bekommt eine öffentliche Eigenschaft `Func<AssistantLanguage> GetLanguage`, die
  die Haupt-App nach `InitializeComponent()` in `MainWindow.xaml.cs` setzt (liest
  dort ganz normal `LocalizationService.Current`). `ULM.Assistant` kennt
  `LocalizationService`/`AppLanguage` also gar nicht — passt sogar noch besser zum
  Wunsch "eigenes, unabhängig laufendes Projekt" als ursprünglich angenommen.
- Die Haupt-App (`UniversalLinuxManager.csproj`) referenziert `ULM.Assistant` per
  `<ProjectReference>` und bindet im Hauptfenster nur den schwebenden Button ein.
  Der gesamte Chat-Code, die Matching-Logik und die Katalog-Datei bleiben
  vollständig innerhalb von `ULM.Assistant/`.
- Kein Eingriff in bestehende Dateien der Haupt-App außer: (a) Einbindung des
  Avatar-Buttons ins Hauptfenster-XAML, (b) `<ProjectReference>`-Eintrag +
  Glob-Ausschluss (analog zu `Linux\**`/`ULM.Tests\**`) in der `.csproj`.

## Komponenten (in `ULM.Assistant/`)

- **`AvatarButton`** (UserControl): schwebendes 🐧-Icon, in der Ecke des
  Hauptfensters positioniert (über allen Tabs sichtbar), öffnet beim Klick das
  Chat-Fenster.
- **`ChatWindow`** (WPF `Window`, `ShowDialog()` NICHT verwendet — `Show()` für
  nicht-modales Verhalten): Nachrichtenverlauf (Sprechblasen, Uli links / Nutzer
  rechts), Texteingabefeld + Senden-Button, dynamische Vorschlag-Buttons unter dem
  Verlauf.
- **`ChatViewModel`**: hält den aktuellen Nachrichtenverlauf (nur In-Memory, kein
  Speichern), verarbeitet Freitext-Eingaben über die Matching-Engine, verarbeitet
  Button-Klicks direkt über die Themen-`Id`, aktualisiert die Vorschlag-Buttons
  nach jeder Antwort.
- **`FaqCatalogService`**: lädt `assistant_faq.json` beim Start; bei fehlender
  oder fehlerhafter Datei greift ein eingebetteter Standard-Katalog im Code als
  Fallback (analog zum bestehenden `IsoDatabaseService.LoadDefaults()`-Muster).
  Die App/der Chat stürzt nie wegen einer kaputten Katalog-Datei ab.
- **`FaqMatchingEngine`**: reine Keyword-Zählung für Freitext-Eingaben (siehe
  unten). Button-Klicks umgehen diese Komponente komplett (liefern die `Id`
  direkt).

## Datenmodell (`assistant_faq.json`)

Datei liegt im Ausgabeordner neben der `.exe` (Content-Datei, `CopyToOutput`),
wird beim Start von `FaqCatalogService` geladen. Pro Eintrag:

```json
{
  "Id": "ventoy-setup",
  "KeywordsDe": ["ventoy", "stick einrichten", "bootf\u00e4hig"],
  "KeywordsEn": ["ventoy", "setup stick", "bootable"],
  "QuestionLabelDe": "Wie richte ich Ventoy auf einem Stick ein?",
  "QuestionLabelEn": "How do I set up Ventoy on a stick?",
  "AnswerDe": "…",
  "AnswerEn": "…",
  "RelatedIds": ["ventoy-update", "copy-to-usb"]
}
```

Initialer Katalog deckt ~10 Kernthemen ab: ISO suchen/filtern, Download starten,
Downloads parallel/Slots, Auf Stick kopieren, Ventoy einrichten/aktualisieren,
Sprache wechseln, Datenbank-Eintrag hinzufügen/bearbeiten, Update-/
Integritätsprüfung, häufige Fehler (Download fehlgeschlagen, Stick nicht
erkannt), Secure Boot. Texte werden inhaltlich an der bestehenden `HelpDialog`
und dem tatsächlichen App-Verhalten ausgerichtet — bei Unklarheiten wird
nachgefragt statt geraten.

## Interaktionsablauf

1. Nutzer klickt den 🐧-Button → `ChatWindow` öffnet sich (nicht-modal).
2. Uli begrüßt den Nutzer und zeigt die Haupt-Themen als Buttons.
3. Nutzer klickt einen Button **oder** tippt frei Text und sendet ihn.
   - Button-Klick → direkte `Id`-Auflösung, immer korrekt.
   - Freitext → `FaqMatchingEngine` zählt für jeden Katalog-Eintrag die Treffer
     seiner Keywords (case-insensitive, sprachabhängig je nach aktueller
     UI-Sprache) im eingegebenen Text. Höchster Treffer gewinnt; bei
     Punktgleichstand gewinnt der erste Eintrag in Katalog-Reihenfolge.
4. Bei 0 Treffern (Freitext): Fallback-Antwort — "Das habe ich leider nicht
   verstanden — hier sind die Themen, bei denen ich helfen kann:" + erneute
   Anzeige der Haupt-Themen-Buttons.
5. Nach jeder Antwort: 2-3 anklickbare Anschluss-Buttons aus `RelatedIds` plus
   ein "Zurück zur Übersicht"-Button.
6. Schließen des Fensters verwirft den Verlauf; erneutes Öffnen startet wieder
   bei der Begrüßung (kein Speichern).

## Lokalisierung

- Sprache wird per Dependency Injection von der Haupt-App hereingereicht (siehe
  Architektur-Korrektur oben) — folgt damit automatisch dem globalen DE/EN-
  Umschalter im Hauptfenster, kein eigener Sprachschalter im Chat-Fenster. Da ein
  Sprachwechsel im Bestandscode ohnehin erst nach einem Neustart wirkt (siehe
  Kommentar in `LocalizationService.cs`), muss der Assistent nicht auf einen
  Live-Sprachwechsel reagieren — die Sprache wird einmal beim Öffnen des
  Chat-Fensters abgefragt.
- Die wenigen Chrome-Texte des Chat-Fensters (Begrüßung, Eingabefeld-
  Platzhalter, Fallback-Satz, "Zurück zur Übersicht") liegen als kleines,
  eigenes DE/EN-Textpaar-Set **innerhalb von `ULM.Assistant`** — bewusst
  entkoppelt von der Haupt-`Str`-Resource-Klasse, kein Eingriff dort nötig.
- Katalog-Einträge selbst tragen beide Sprachen direkt im JSON
  (`QuestionLabelDe`/`En`, `AnswerDe`/`En`, `KeywordsDe`/`En`).

## Fehlerbehandlung

- Fehlende/kaputte `assistant_faq.json` → eingebetteter Standard-Katalog im
  Code, keine Ausnahme dringt zum Nutzer durch, App bleibt voll funktionsfähig.
- Kein Netzwerkzugriff, keine externen Abhängigkeiten außerhalb von .NET/WPF —
  der Assistent kann nichts an bestehender Download-/Ventoy-/DB-Logik kaputt
  machen, da er rein lesend auf den Katalog zugreift und keine App-Aktionen
  auslöst.

## Testing

- Neue Testklasse(n) in `ULM.Tests` (referenziert `ULM.Assistant`):
  - `FaqMatchingEngine`: eindeutiger Treffer, Punktgleichstand-Tie-Break, 0
    Treffer → Fallback.
  - `FaqCatalogService`: gültige Datei lädt korrekt, fehlende Datei → Fallback-
    Katalog, syntaktisch kaputtes JSON → Fallback-Katalog statt Absturz.
- Bestehende 198 Tests der Haupt-App bleiben unberührt (rein additive Änderung,
  kein Eingriff in bestehende Klassen außer dem Avatar-Button-Einbau im
  Hauptfenster-XAML).

## Offene Punkte / bewusst zurückgestellt

- **Git-Repo-Problem entdeckt (nicht Teil dieses Features):** Das lokale
  ULM-Repo ist derzeit nicht funktionsfähig — `.git` in diesem Ordner ist ein
  toter Worktree-Zeiger auf `C:/Users/zwill/Documents/C++ Projekt/Claude/ULM`,
  dieser Pfad existiert nicht mehr. Betrifft auch `Documents\C++ Projekt\Linux\`
  (identischer toter Zeiger). Nutzer hat entschieden: vorerst ignorieren, Spec
  wird ohne Commit lokal abgelegt. Muss vor dem eigentlichen Implementierungs-
  Branch geklärt werden, da ohne funktionierendes Repo kein Branch/Commit
  möglich ist.
- Kein Merge/Release ohne ausdrückliche Nutzer-Freigabe (Standard-Policy dieses
  Projekts) — gilt auch für dieses Feature.
