# Release-Fahrplan

Checkliste für "veröffentliche das" / "erstelle einen Release".

**Schritt 0 — immer zuerst:** Bevor der Fahrplan tatsächlich ausgeführt
wird, aktiv nachfragen, ob er jetzt abgearbeitet werden soll (z.B. "Soll
ich den Release-Fahrplan jetzt ausführen?"). Erst nach Bestätigung mit
Schritt 1 beginnen. Innerhalb des Fahrplans selbst dann OHNE weitere
Rückfragen durchlaufen, solange keiner der Sonderfälle unten zutrifft
(dann anhalten und gezielt nachfragen statt zu raten) — die eine
Bestätigung am Anfang ersetzt die vielen Einzelfragen (Versionsnummer,
SmartScreen-Hinweis, Alte-Releases-Policy etc.), die in früheren Sitzungen
einzeln gestellt wurden.

## 1. Vorbereitung

- [ ] `dotnet build UniversalLinuxManager.csproj -c Debug` → 0 Fehler/Warnungen
- [ ] `dotnet test ULM.Tests` → alle grün
- [ ] Bei Fehlschlag: **abbrechen**, nicht weitermachen, Nutzer informieren.
- [ ] Alle Änderungen sind bereits committet (kein `git add -A`/Freitext-Commit
      an dieser Stelle — falls uncommittete Änderungen vorhanden sind, den
      Nutzer fragen, was davon in den Release soll).

## 2. Versionsnummer bestimmen

`git log <letzter-tag>..HEAD --oneline` ansehen:

- Nur `fix:`-Commits seit dem letzten Tag → **Patch-Bump** (z.B. 2.33.0 → 2.33.1)
- Mindestens ein `feat:`-Commit dabei → **Minor-Bump**, Patch auf 0 (z.B. 2.32.0 → 2.33.0)
- Ein echter Breaking-Change (selten bei diesem Projekt) → Major-Bump, nur
  nach Rückfrage.

Aktualisieren in `UniversalLinuxManager.csproj`: `<Version>`,
`<AssemblyVersion>`, `<FileVersion>` (alle drei, identischer Wert plus `.0`
am Ende für die beiden Assembly-Felder). `Constants.AppVersion` liest das
automatisch aus der Assembly — nirgendwo sonst im C#-Code hartkodiert.

## 3. Changelog-Eintrag

Neuer Eintrag ganz oben im `History`-Array in
`Views/Dialogs/ChangelogDialog.cs` (neueste Version zuerst). Deutsche,
nutzerverständliche Stichpunkte — keine rohen Commit-Messages
hineinkopieren. Ton: "Fehlerbehebung: …" / "Neu: …", siehe bestehende
Einträge als Vorlage. Wird der App-eigenen "Was ist neu?"-Dialog beim
nächsten Start nach einem Versions-Wechsel automatisch angezeigt.

## 4. Projektseite (`docs/index.html`)

- [ ] Versions-Badge in der Hero-Eyebrow-Zeile (`<div class="eyebrow">`) aktualisieren.
- [ ] Funktionen-Karten / Download-Bereich abgleichen, falls sich der
      Funktionsumfang oder die Download-Varianten geändert haben.
- [ ] Anführungszeichen-Konvention beachten: „…" (deutsche Typografie,
      U+201E/U+201C) statt gerader `"…"` — siehe Commit 82aeb55 für den
      Präzedenzfall, warum das eine bewusste Konvention ist.
- [ ] **Feste Regel, nicht entfernen:** Solange `installer/ULM.iss` ein
      unsigniertes Setup.exe baut, MUSS der SmartScreen-Warnhinweis
      ("Unbekannter Herausgeber" → "Weitere Informationen" → "Trotzdem
      ausführen") im Download-Bereich stehen bleiben.
- [ ] Ich kann `file://`-Seiten nicht im Browser rendern (Sandbox blockiert
      das) — nach dem Push ist die Seite aber über die echte
      `https://zwilling10.github.io/ULM/`-URL per Browser-Tool prüfbar
      (GitHub Pages deployt aus `docs/` auf `master` üblicherweise
      innerhalb von Sekunden bis wenigen Minuten).

## 5. README.md

Nur anfassen, falls sich Build-Befehle, Download-Varianten oder
Voraussetzungen geändert haben — sonst überspringen.

## 6. Release-Vorbereitungs-Commit

Ein Commit für Versions-Bump + Changelog + HTML, Titel-Konvention:
`vX.Y.Z: <kurze Zusammenfassung der wichtigsten Punkte>`

## 7. Merge nach `master`

```bash
git merge-base --is-ancestor origin/master HEAD
```

- **Ist Vorfahre (Normalfall bei diesem Ein-Personen-Projekt):** reiner
  Fast-Forward, sicher automatisch machbar:
  ```bash
  git push origin HEAD:master
  ```
  Kein lokales `git checkout master` nötig (kollidiert, falls `master`
  gerade in einem anderen Worktree ausgecheckt ist — z.B. dem
  Haupt-Worktree `ULM` neben diesem `ULM-features`-Worktree).
- **Ist KEIN Vorfahre (echte Divergenz/Konflikt):** anhalten, Nutzer
  fragen. Keinen Merge-Commit ohne Rückfrage erzeugen.

## 8. Tag setzen + pushen

```bash
git tag -a vX.Y.Z -m "Universal Linux Manager vX.Y.Z" <commit-sha>
git push origin vX.Y.Z
```

Der Tag-Push triggert automatisch `.github/workflows/release.yml` — baut
portable EXE + Setup.exe + ZIP über `build-release.sh --zip --installer`
und legt sie als GitHub-Release-Assets an. Kein manueller Build/Upload
nötig, Inno Setup ist auf `windows-latest`-Runnern vorinstalliert.

## 9. Workflow überwachen

```bash
gh run watch <run-id> --exit-status
```

Bei Fehlschlag: **keine alten Releases löschen**, Fehler analysieren,
Nutzer informieren statt selbst zu improvisieren.

## 10. Assets verifizieren

```bash
gh release view vX.Y.Z
```

Erwartet: `UniversalLinuxManager-Setup-vX.Y.Z-win-x64.exe`,
`UniversalLinuxManager-vX.Y.Z-win-x64.exe`,
`UniversalLinuxManager-vX.Y.Z-win-x64.zip`.

## 11. Alte Releases aufräumen

Standing Policy dieses Projekts (bestätigt 2026-07-16): **nur der
neueste Release bleibt öffentlich sichtbar.**

```bash
gh release delete <alter-tag> -y
```

**Ohne** `--cleanup-tag` — die Git-Tags selbst bleiben als Historie
erhalten, nur die Release-Seite samt alten Download-Assets verschwindet.

## 12. Live-Seite verifizieren

`https://zwilling10.github.io/ULM/` per Browser-Tool aufrufen (`get_page_text`
genügt, Screenshot ist bei dieser Domain bisher unzuverlässig/timeout-anfällig)
— prüfen: Versions-Badge, Download-Bereich, SmartScreen-Hinweis vorhanden.

## 13. Abschluss-Meldung

Kurze Zusammenfassung an den Nutzer: Release-URL, was enthalten war, was
gelöscht wurde, plus Hinweis auf alles, was ich nicht selbst verifizieren
konnte (z.B. optischer HTML-Eindruck) und der Nutzer ggf. noch selbst
gegenchecken sollte.

## Wann trotzdem nachfragen (nicht blind durchziehen)

- Tests oder Build schlagen fehl.
- `origin/master` ist kein Vorfahre des aktuellen Branches (echter Konflikt).
- Es gibt uncommittete Änderungen, deren Zugehörigkeit zum Release unklar ist.
- Ein Breaking Change liegt vor (Major-Version-Frage).
- Der Release-Workflow schlägt fehl und die Ursache ist nicht offensichtlich
  (z.B. Inno-Setup/ISCC auf dem Runner plötzlich nicht mehr vorhanden).
