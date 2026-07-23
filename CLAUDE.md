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

- **Letzter Fokus:** Zweisprachigkeit (Deutsch/Englisch) — Phase 1 (Infrastruktur +
  Beispiel-Migration) und eine direkt daran anschließende Einstellungen-Konsolidierung
  (Design/Sprache/Modus zu einem `⚙ Einstellungen`-Button zusammengefasst).

- **Aktueller Status:**
  - `v2.39.1` released (Selbst-Update-Neustart-Absturz behoben, Fix bereits live).
  - Branch `feature/bilingual-ui-phase1`: Phase-1-Infrastruktur fertig, PR
    [#6](https://github.com/zwilling10/ULM/pull/6) offen, noch nicht gemerged.
  - Branch `feature/settings-consolidation` (aktuell ausgecheckt, HEAD `5c7e27a`):
    Design/Sprache/Modus-Konsolidierung fertig implementiert, Build + alle 186 Tests
    grün, real End-to-End verifiziert (inkl. zwei dabei gefundener und behobener Bugs:
    neuer Einstellungen-Button war nicht übersetzt; SetupDialog-Header ignorierte
    `showWelcome`). **Noch NICHT gepusht/gemerged** — Nutzer testet lokal und meldet
    sich, bevor PR/Merge passiert.
  - Design für Phase 2 (SetupDialog vollständig lokalisieren, ca. 30 neue `Str`-Werte,
    gleiches Muster wie Phase 1) wurde im Gespräch bereits abgestimmt und vom Nutzer
    bestätigt — **aber noch nicht als Spec-Dokument geschrieben/committet.**

- **Nächster Schritt:** Spec für „SetupDialog lokalisieren" nach
  `docs/superpowers/specs/YYYY-MM-DD-setupdialog-localization-design.md` schreiben
  (Inhalt siehe Konversation: alle ~30 Strings inkl. Erststart-Bereich, Fehlermeldung
  via String-Verkettung statt neuer `T()`-Parameter, Sprach-Buttons bleiben
  hartcodiert), dann wie gewohnt Plan schreiben und per Subagent-Driven-Development
  umsetzen. Vor dem Start kurz prüfen, ob der Nutzer inzwischen zu
  `feature/settings-consolidation` Feedback gegeben hat (ggf. zuerst das behandeln).
