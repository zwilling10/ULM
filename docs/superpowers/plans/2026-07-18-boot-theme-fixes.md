# Boot-Theme-Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Beheben von zwei Bugs im Ventoy-Bootmenü von ULM: (1) ein GRUB-Boot-Fehler ("Failed to boot both default and fallback entries") direkt beim Einschalten, und (2) veraltete/überlappende Text-Anzeigen im Bootmenü (falsche Versionsnummer, doppelte Statuszeilen).

**Architektur:** Root Cause von Bug 1 ist ein falsch verstandener Ventoy-Control-Key (`VTOY_MENU_TIMEOUT`), der einfach entfernt wird. Root Cause von Bug 2 ist, dass Titel/Version/Stick-Stats fest als Pixel-Text in `background.png` eingebrannt sind, während `theme.txt` zusätzlich eigene Live-Labels darüberzeichnet — zwei Systeme zeichnen an derselben Stelle. Der Fix entfernt jeglichen Text aus `background.png` (nur noch Hintergrundfarbe, Trennlinien und das dezente VENTOY-Wasserzeichen bleiben) und lässt `theme.txt` künftig alle Text-Elemente live aus echten Werten (`Constants.AppVersion`, aktuelle Stick-Stats) rendern — inklusive einer Korrektur der vertikalen Positionen, damit sich das eigene Tasten-Hinweis-Label nicht mehr mit Ventoys nativer Disk-Info-Zeile überlappt.

**Tech Stack:** C# / .NET 8 (WPF), Ventoy `theme.txt` (GRUB-gfxmenu-Format) und `ventoy.json` (Ventoy Global Control Plugin), Python + Pillow (einmaliges Bild-Editing, nicht Teil des Builds).

## Global Constraints

- Bestehenden Codestil beibehalten: deutsche Kommentare, gleiche Einrückung/String-Verkettung wie im Rest von `UsbService.cs`.
- Keine neuen NuGet-Pakete oder Build-Schritte einführen (kein Python/Pillow im Build — das Bild wird einmalig offline bearbeitet und als fertige PNG committet).
- `Constants.AppVersion` ist die einzige Quelle der Wahrheit für die Versionsnummer (siehe `Core/Models/Constants.cs:15`) — nirgends eine Version erneut hart kodieren.
- Bestehende Abstands-Konvention der Bootmenü-Bereiche (siehe Kommentar über `WriteThemeTxt`) beibehalten und um die bisher fehlenden Bereiche (neues Subtitle-Label, Ventoys native Status-Zeile) ergänzen, statt sie zu ignorieren.

---

### Task 1: `VTOY_MENU_TIMEOUT`-Bug beheben (Boot-Fehler "Failed to boot both default and fallback entries")

**Files:**
- Modify: `Core/Services/UsbService.cs:247-255`

**Interfaces:**
- Konsumiert: nichts Neues.
- Produziert: `ventoy.json` enthält den Key `VTOY_MENU_TIMEOUT` nicht mehr.

- [ ] **Step 1: Root-Cause-Kommentar + Fix einbauen**

In `Core/Services/UsbService.cs` den bestehenden Block

```csharp
                w.WritePropertyName("control"); w.WriteStartArray();
                WCtrl(w, "VTOY_MENU_TIMEOUT", "0"); WCtrl(w, "VTOY_DEFAULT_MENU_MODE", "1"); WCtrl(w, "VTOY_TREE_VIEW_MENU_STYLE", "0");
```

ersetzen durch:

```csharp
                w.WritePropertyName("control"); w.WriteStartArray();
                // VTOY_MENU_TIMEOUT bewusst NICHT gesetzt: ein gesetzter Wert (auch "0") lässt
                // Ventoy den aktuell fokussierten Menüeintrag automatisch nach X Sekunden booten.
                // Im TreeView-Modus (VTOY_DEFAULT_MENU_MODE=1) ist der oberste Eintrag beim
                // Start aber ein Kategorie-Ordner (z.B. "[Antivirus]"), kein bootbares Image —
                // GRUB versuchte diesen automatisch zu booten und scheiterte mit "Failed to boot
                // both default and fallback entries. Press any key to continue.....". Ohne den
                // Key wartet Ventoy wie gewollt auf eine echte Nutzerauswahl.
                WCtrl(w, "VTOY_DEFAULT_MENU_MODE", "1"); WCtrl(w, "VTOY_TREE_VIEW_MENU_STYLE", "0");
```

Rest des Blocks (`VTOY_MAX_SEARCH_LEVEL`, `w.WriteEndArray();`) unverändert lassen.

- [ ] **Step 2: Build prüfen**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: `Build succeeded.`, keine neuen Warnungen zu `UsbService.cs`.

- [ ] **Step 3: Generierten Output manuell verifizieren**

Es existiert kein Unit-Test-Harness für `UsbService` (rein dateisystembasierte, statische Klasse ohne Interface-Seam für `DriveRoot`). Stattdessen den erzeugten Inhalt einmalig über ein Scratch-Skript prüfen — im Scratchpad-Verzeichnis, NICHT committen:

Run (PowerShell, Beispiel mit echtem Ventoy-Laufwerksbuchstaben `E:` anpassen):
```powershell
# Nur zur Verifikation — ersetzt E durch den tatsächlichen Ventoy-Stick-Buchstaben
Get-Content "E:\ventoy\ventoy.json" | Select-String "VTOY_MENU_TIMEOUT"
```
Expected: keine Ausgabe (Key ist nicht mehr vorhanden). Falls kein Stick verfügbar ist, reicht die Sichtprüfung des Diffs aus Step 1 plus der Build-Erfolg aus Step 2; die volle Verifikation erfolgt real auf dem Stick in Task 4.

- [ ] **Step 4: Commit**

```bash
git add Core/Services/UsbService.cs
git commit -m "fix: VTOY_MENU_TIMEOUT entfernt, da Auto-Boot in DIR-Eintrag zu GRUB-Fehler fuehrte"
```

---

### Task 2: Titel/Version/Stick-Stats live aus echten Daten rendern statt statisch

**Files:**
- Modify: `Core/Services/UsbService.cs:144-289` (`EnsureVentoyTheme`, `WriteThemeTxt`, `UpdateVentoyMenu`)

**Interfaces:**
- Konsumiert: `Constants.AppVersion` (`Core/Models/Constants.cs:15`), `DriveTotalMb(string letter)` / `DriveFreeMb(string letter)` (`Core/Services/UsbService.cs:93-97`, bereits vorhanden), `stickIsos.Count` (bereits in `UpdateVentoyMenu` berechnet).
- Produziert: `WriteThemeTxt(string dir, string letter, double totalMb, double freeMb, int isoCount)` — neue Signatur, wird nur noch von `UpdateVentoyMenu` aufgerufen.

- [ ] **Step 1: `WriteThemeTxt` auf dynamische Parameter umstellen**

Bestehende Methode (inkl. des Kommentarblocks direkt darüber) ersetzen:

```csharp
        // Vertikale Aufteilung (0-100% der Bildschirmhöhe), so gewählt, dass sich Titel,
        // Untertitel, Boot-Menü, Distro-Tipp (menu_tip, siehe UpdateVentoyMenu), Tasten-Hinweis
        // und Ventoys eigene native Disk-Info-Zeile (ventoy_left/ventoy_top, siehe UpdateVentoyMenu)
        // NICHT überlappen:
        //   Titel + Untertitel  2.0% – 9.0%  (ein zusammengehöriger Kopf-Block, kein Abstand
        //                                     zwischen den beiden Zeilen nötig)
        //   Boot-Menü          10.0% – 78.0%
        //   Distro-Tipp         81.0% (einzeilig)
        //   Tasten-Hinweis      88.0% (einzeilig) — BUGFIX: lag vorher bei 94%, nur 1% von
        //                                           Ventoys nativer Status-Zeile entfernt und
        //                                           überlappte sich sichtbar mit ihr
        //   Ventoy-Status       95.0% (Ventoy-eigene Zeile, siehe ventoy_top in UpdateVentoyMenu)
        private static void WriteThemeTxt(string dir, string letter, double totalMb, double freeMb, int isoCount)
        {
            string subtitle = $"Multiboot USB Stick Manager  v{Constants.AppVersion}   |   " +
                $"{letter}:  {totalMb / 1024.0:F1} GB gesamt  |  {freeMb / 1024.0:F1} GB frei  |  {isoCount} ISOs";
            string c =
                "# Universal Linux Manager Boot-Theme\n" +
                "desktop-image: \"background.png\"\n" +
                "desktop-color: \"#0D1B2A\"\n" +
                "\n+ label {\n  top=2%\n  left=0%\n  width=100%\n  height=48\n  align=\"center\"\n" +
                "  text=\"UNIVERSAL LINUX MANAGER\"\n  color=\"#FFFFFF\"\n}\n" +
                "\n+ label {\n  top=6.5%\n  left=0%\n  width=100%\n  height=26\n  align=\"center\"\n" +
                $"  text=\"{subtitle}\"\n  color=\"#4A6FA5\"\n}}\n" +
                "\n+ boot_menu {\n  left=10%\n  top=10%\n  width=80%\n  height=68%\n" +
                "  item_color=\"#C8D4E0\"\n  selected_item_color=\"#FFFFFF\"\n" +
                "  item_height=42\n  item_padding=16\n  item_spacing=6\n" +
                "  scrollbar=true\n  scrollbar_width=6\n" +
                "  scrollbar_thumb_color=\"#0075BE\"\n  scrollbar_frame_color=\"#1A3355\"\n}\n" +
                "\n+ label {\n  top=88%\n  left=0%\n  width=100%\n  height=22\n  align=\"center\"\n" +
                "  text=\"Auf/Ab: Auswahl  |  ENTER: Booten  |  Esc: Zurueck\"\n  color=\"#4A6FA5\"\n}\n";
            File.WriteAllText(Path.Combine(dir, "theme.txt"), c, Encoding.UTF8);
        }
```

- [ ] **Step 2: `EnsureVentoyTheme` vereinfachen — nicht mehr selbst `WriteThemeTxt`/`WriteBackgroundPng` aufrufen**

`UpdateVentoyMenu` übernimmt ab Step 3 beide Aufrufe selbst (mit den echten Werten). Aktuelle Methode:

```csharp
        public static void EnsureVentoyTheme(string letter)
        {
            try
            {
                string themeDir = Path.Combine(DriveRoot(letter), "ventoy", "themes", "ulm");
                Directory.CreateDirectory(themeDir);
                WriteThemeTxt(themeDir);
                WriteBackgroundPng(themeDir);
                UpdateVentoyMenu(letter, Array.Empty<IsoEntry>());
            }
            catch (Exception ex) { Debug.WriteLine($"[EnsureVentoyTheme] {ex.Message}"); }
        }
```

ersetzen durch:

```csharp
        public static void EnsureVentoyTheme(string letter)
        {
            try
            {
                string themeDir = Path.Combine(DriveRoot(letter), "ventoy", "themes", "ulm");
                Directory.CreateDirectory(themeDir);
                UpdateVentoyMenu(letter, Array.Empty<IsoEntry>());
            }
            catch (Exception ex) { Debug.WriteLine($"[EnsureVentoyTheme] {ex.Message}"); }
        }
```

- [ ] **Step 3: `UpdateVentoyMenu` schreibt `theme.txt` bei jedem Aufruf neu (mit aktuellen Stats)**

Anfang der Methode aktuell:

```csharp
                string root      = DriveRoot(letter);
                string ventoyDir = Path.Combine(root, "ventoy");
                Directory.CreateDirectory(ventoyDir);
                string themeDir = Path.Combine(ventoyDir, "themes", "ulm");
                if (Directory.Exists(themeDir)) WriteBackgroundPng(themeDir);

                var stickIsos = new List<(string VentoyPath, string Filename, string Category)>();
                if (Directory.Exists(root))
                {
```

ersetzen durch:

```csharp
                string root      = DriveRoot(letter);
                string ventoyDir = Path.Combine(root, "ventoy");
                Directory.CreateDirectory(ventoyDir);
                string themeDir = Path.Combine(ventoyDir, "themes", "ulm");

                var stickIsos = new List<(string VentoyPath, string Filename, string Category)>();
                if (Directory.Exists(root))
                {
```

Direkt NACH dem bestehenden Block, der `stickIsos` befüllt (also nach dieser bereits vorhandenen, unveränderten Stelle):

```csharp
                    foreach (string iso in Directory.GetFiles(root, "*.iso").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                    { string fn = Path.GetFileName(iso); stickIsos.Add(($"/{fn}", fn, string.Empty)); }
                }
```

folgenden neuen Block einfügen:

```csharp

                if (Directory.Exists(themeDir))
                {
                    WriteBackgroundPng(themeDir);
                    WriteThemeTxt(themeDir, letter, DriveTotalMb(letter), DriveFreeMb(letter), stickIsos.Count);
                }
```

- [ ] **Step 4: Build prüfen**

Run: `dotnet build UniversalLinuxManager.csproj -c Debug`
Expected: `Build succeeded.` — insbesondere keine Fehler zu der geänderten `WriteThemeTxt`-Signatur (alle Aufrufer wurden in Step 2/3 mit angepasst).

- [ ] **Step 5: Generierten `theme.txt`-Inhalt manuell verifizieren**

Da es keinen Unit-Test-Seam für diese dateisystembasierte Klasse gibt, den Inhalt real auf einem angeschlossenen Ventoy-Stick prüfen (Buchstaben anpassen):

```powershell
Get-Content "E:\ventoy\themes\ulm\theme.txt"
```

Expected: die zweite `label`-Zeile enthält `Multiboot USB Stick Manager  v2.36.0` (aktuelle `Constants.AppVersion`, nicht mehr `v2.27`), plus reale `GB gesamt`/`GB frei`/`ISOs`-Werte des angeschlossenen Sticks. Der Tasten-Hinweis-Block enthält `top=88%` statt `top=94%`.

- [ ] **Step 6: Commit**

```bash
git add Core/Services/UsbService.cs
git commit -m "fix: Titel/Version/Stick-Stats im Bootmenue live rendern statt statisch, Tasten-Hinweis von Ventoys nativer Statuszeile entkoppelt"
```

---

### Task 3: `background.png` von eingebranntem Text befreien

**Files:**
- Modify: `background.png` (Repo-Root, `EmbeddedResource` in `UniversalLinuxManager.csproj:76` — Dateiname/Pfad bleiben unverändert, nur der Bildinhalt ändert sich)
- Scratch (nicht committen): `<scratchpad>/strip_background_text.py`

**Interfaces:**
- Konsumiert: nichts aus Task 1/2.
- Produziert: `background.png` ohne eingebrannten Text (Titel, Untertitel, Top-Rechts-Stats, "USB MULTIBOOT MANAGER"-Tagline, untere Statuszeile), aber mit unverändertem VENTOY-Wasserzeichen und den vier cyanfarbenen Trennlinien.

- [ ] **Step 1: Scratch-Skript schreiben**

Datei `<scratchpad>/strip_background_text.py`:

```python
import shutil
from PIL import Image, ImageDraw

SRC = r"<Projektverzeichnis>\background.png"
BACKUP = SRC + ".orig"

shutil.copy2(SRC, BACKUP)

im = Image.open(SRC).convert("RGB")
w, h = im.size
navy = (14, 24, 46)  # entspricht theme.txt: desktop-color "#0D1B2A"
draw = ImageDraw.Draw(im)

# Erhalten bleiben die vier cyanfarbenen Trennlinien bei y=0-1, 117-119, 949-951,
# 1075-1076 sowie das VENTOY-Wasserzeichen bei y=391-546 (per Pixel-Scan ermittelt).
# Entfernt wird jeglicher eingebrannter Text, der jetzt live ueber theme.txt kommt
# oder schlicht eine redundante Dopplung war:
draw.rectangle([0, 2, w - 1, 116], fill=navy)     # Titel/Untertitel/Top-Rechts-Stats
draw.rectangle([0, 764, w - 1, 785], fill=navy)   # "USB MULTIBOOT MANAGER"-Tagline
draw.rectangle([0, 952, w - 1, 1074], fill=navy)  # untere Statuszeile (Duplikat)

im.save(SRC)
print("saved", SRC, im.size)
```

- [ ] **Step 2: Skript ausführen**

Run: `python "<scratchpad>/strip_background_text.py"`
Expected: `saved ...background.png (1920, 1080)`

- [ ] **Step 3: Ergebnis visuell prüfen**

Das neue `background.png` mit dem Read-Tool öffnen und prüfen:
- Kein Text mehr oben links/rechts (Titel, Version, Stats) und unten mittig (Statuszeile).
- VENTOY-Wasserzeichen in der Mitte weiterhin sichtbar, unverändert.
- Vier cyanfarbene Trennlinien oben/unten weiterhin vorhanden.
- Bildgröße weiterhin 1920×1080.

- [ ] **Step 4: Backup-Datei aufräumen**

Die in Step 1 angelegte `background.png.orig`-Sicherung liegt im Projektverzeichnis (nicht im Scratchpad) und darf nicht versehentlich mit committet werden:

Run: `rm "<Projektverzeichnis>\background.png.orig"`

(Falls das bearbeitete Bild doch nicht passt, vorher `background.png.orig` zurückkopieren statt zu löschen und Step 1–3 mit angepassten Koordinaten wiederholen.)

- [ ] **Step 5: Commit**

```bash
git add background.png
git commit -m "fix: eingebrannten Text aus background.png entfernt (Version/Stats kamen bereits doppelt und veraltet vor)"
```

---

### Task 4: End-to-End-Verifikation auf echtem Stick

**Files:** keine Code-Änderungen — reine Verifikation.

**Interfaces:** keine.

- [ ] **Step 1: App bauen und Ventoy-Theme auf einem Test-Stick neu einrichten**

Die App starten, zu einem Ventoy-Stick verbinden und einen Vorgang auslösen, der `UsbService.UpdateVentoyMenu` aufruft (z.B. Stick neu scannen lassen, siehe `ViewModels/MainViewModel.cs:669`), damit `theme.txt`/`background.png`/`ventoy.json` mit den Änderungen aus Task 1–3 neu geschrieben werden.

- [ ] **Step 2: Vom Stick booten**

Erwartet:
- Keine "Failed to boot both default and fallback entries"-Meldung mehr, das Bootmenü erscheint direkt.
- Oben zeigt die Versionszeile die aktuelle `Constants.AppVersion` (z.B. `v2.36.0`), keine veraltete `v2.27` mehr.
- Unten mittig keine überlappenden/doppelten Textzeilen mehr.

- [ ] **Step 3: Bei Erfolg — nichts weiter zu tun**

Falls eines der drei Punkte in Step 2 nicht stimmt, zurück zu Phase 1 der systematic-debugging-Skill (neue Evidenz sammeln, nicht direkt erneut fixen).

---

## Self-Review

**Spec-Abdeckung:**
- Bug 1 (GRUB-Bootfehler durch `VTOY_MENU_TIMEOUT`) → Task 1. ✅
- Bug 2a (veraltete Versionsnummer oben links) → Task 2 (Live-Rendering via `Constants.AppVersion`) + Task 3 (Pixel-Text entfernt). ✅
- Bug 2b (überlappender Text unten mittig) → Task 2 Step 1 (Tasten-Hinweis von 94% auf 88% verschoben, Ventoy-native Statuszeile bei 95% jetzt dokumentiert und nicht mehr kollidierend) + Task 3 (redundante eingebrannte Statuszeile entfernt). ✅

**Platzhalter-Scan:** Keine "TBD"/"implement later"/unvollständigen Code-Blöcke — jeder Step enthält vollständigen, copy-paste-fähigen Code oder ein konkretes Kommando mit erwartetem Ergebnis.

**Typkonsistenz:** `WriteThemeTxt(string dir, string letter, double totalMb, double freeMb, int isoCount)` wird in Task 2 einheitlich so definiert und in Task 2 Step 3 mit exakt dieser Signatur aufgerufen (`WriteThemeTxt(themeDir, letter, DriveTotalMb(letter), DriveFreeMb(letter), stickIsos.Count)`).
