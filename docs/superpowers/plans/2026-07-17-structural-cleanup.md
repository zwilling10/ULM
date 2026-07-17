# Strukturelles Refactoring: Distro-Matching, View-Zustand, DI, HttpService-Split — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Vier unabhängige strukturelle Aufräumarbeiten am ULM-Code umsetzen, die bei einer Architektur-Review identifiziert wurden — ohne jede Verhaltensänderung für den Nutzer. Am Ende bleibt die App byte-für-byte funktional identisch, aber testbarer und besser navigierbar.

**Architecture:** MVVM (C#/WPF, .NET 8) bleibt unverändert. Die vier Aufgaben sind reine Verschiebungen/Extraktionen bestehenden, unveränderten Codes:
1. Reine Distro-/Versions-Vergleichslogik aus `MainViewModel` in eine neue Klasse `DistroMatcher` (Core-Schicht) verschieben.
2. Vier `HashSet`-Felder, die "bereits gemeldete Stick-Funde" verfolgen, aus der View (`MainWindow.xaml.cs`) ins `MainViewModel` verschieben.
3. `MainViewModel` bekommt Konstruktor-Parameter für `IHttpService`/`IUsbService`/`IIsoDatabaseService` statt die Singletons hart zu ziehen (mit Default auf `.Instance`, damit der bestehende Aufrufer unverändert bleibt).
4. `HttpService.cs` (1411 Zeilen) in zwei `partial class`-Dateien aufteilen: die ~20 dedizierten Distro-Resolver wandern in eine eigene Datei, generischer HTTP-Transport bleibt in der Hauptdatei. Öffentliche API bleibt identisch (partial class = ein Typ, egal auf wie viele Dateien verteilt).

**Tech Stack:** C# 12 / .NET 8, WPF, xUnit 2.9.2 (keine Mocking-Bibliothek — Fakes werden von Hand geschrieben, wie im Rest von `ULM.Tests` üblich).

## Global Constraints

- Arbeitsverzeichnis: `C:\Users\zwill\Documents\C++ Projekt\Claude\ULM\.claude\worktrees\refactor+structural-cleanup`, Branch `worktree-refactor+structural-cleanup` (Basis: `origin/master` @ `2e27cbf`).
- Build: `dotnet build`. Tests: `dotnet test ULM.Tests/ULM.Tests.csproj`. Baseline vor Task 1: **115 Tests, 0 Fehler** — muss nach jedem Task mindestens genauso hoch sein (plus neue Tests aus diesem Plan).
- Keine Verhaltensänderung, keine neue Abhängigkeit, keine Änderung der öffentlichen API von `HttpService`/`UsbService`/`IsoDatabaseService` nach außen (Workers.cs, MainWindow.xaml.cs dürfen unverändert bleiben, außer wo ein Task das explizit vorsieht).
- Dateikodierung: UTF-8 (Umlaute, Emoji in Strings). Bei PowerShell-Dateizugriffen immer `-Encoding UTF8` explizit angeben (Windows-PowerShell-Default ist UTF-16 LE).
- Commit-Stil des Repos: Imperativ, klein, deutsch, z.B. `refactor: Distro-Matching-Logik nach DistroMatcher verschoben`. Ein Commit pro Task.
- `InternalsVisibleTo("ULM.Tests")` ist bereits assembly-weit in `UniversalLinuxManager.csproj` gesetzt — `internal`-Typen/Methoden sind aus Tests heraus sichtbar, unabhängig vom Namespace.

---

### Task 1: Distro-Matching-Logik nach `DistroMatcher` extrahieren

**Files:**
- Create: `Core/Services/DistroMatcher.cs`
- Modify: `ViewModels/MainViewModel.cs` (Methoden/Felder entfernen, Aufrufstellen umstellen)
- Modify: `ULM.Tests/MainViewModelDistroMatchingTests.cs` (Aufrufe umstellen)

**Interfaces:**
- Produces: `ULM.Core.Services.DistroMatcher` (internal static class) mit den Methoden `FindExactDuplicateIndicesByFilename(IReadOnlyList<IsoEntry>) : List<int>`, `AreSameDistro(IsoEntry, IsoEntry) : bool`, `IsSameDistroDifferentVersion(string, string) : bool`, `IsVersionNewer(string, string) : bool`, `RepresentsGenuineFilenameChange(string?, string?) : bool`, `SplitOutdatedFromDuplicates(Dictionary<string,int>, IReadOnlyList<IsoEntry>, HashSet<string>) : (List<(IsoEntry Entry, string OldFilename)> TrulyOutdated, List<(IsoEntry Entry, string OldFilename)> StaleDuplicates)`, `HasVersionlessFilename(string) : bool`, `NormalizeForDistroComparison(string) : string`, `IsLikelySameDistroByName(string, string) : bool`.
- Consumes: `ULM.Core.Models.IsoEntry`, `ULM.Core.Services.HttpService.ExtractVersion(string)` (bleibt unverändert in `HttpService.cs`).

- [ ] **Step 1: Neue Datei `Core/Services/DistroMatcher.cs` anlegen**

```csharp
// Core/Services/DistroMatcher.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ULM.Core.Models;

namespace ULM.Core.Services
{
    /// <summary>
    /// Reine Vergleichslogik: erkennt, ob zwei Dateinamen/Einträge dieselbe Distro (evtl. andere
    /// Version) bezeichnen, und klassifiziert veraltete/duplizierte Stick-Funde. Keine DB-/Stick-
    /// Zugriffe — bewusst als statische, reine Funktionen gehalten, um sie ohne MainViewModel-
    /// Instanz testen zu können (siehe ULM.Tests/MainViewModelDistroMatchingTests.cs).
    /// </summary>
    internal static class DistroMatcher
    {
        private static readonly string[] _platformCodenames =
        {
            "noble", "jammy", "focal", "bionic", "oracular", "mantic", "lunar", "kinetic", "plucky",
            "trixie", "bookworm", "bullseye", "buster", "sid", "testing"
        };
        private static readonly HashSet<string> _genericDistroWords =
            new(StringComparer.OrdinalIgnoreCase)
            { "linux", "os", "live", "desktop", "server", "workstation", "official" };

        /// <summary>
        /// Findet Indizes EXAKTER Duplikate: Einträge, deren (nicht-leerer) Dateiname bereits bei
        /// einem FRÜHEREN Eintrag vorkam. Der erste Eintrag je Dateiname bleibt, spätere gelten als
        /// Duplikat. Rückgabe absteigend sortiert, damit der Aufrufer per Remove(index) sicher
        /// nacheinander entfernen kann, ohne noch ausstehende Indizes zu verschieben. Reine Funktion,
        /// ohne DB-/Stick-Zugriff testbar. BUGFIX: DeduplicateEntries behandelte bisher NUR "gleiche
        /// Distro, andere Version" (unterschiedliche Dateinamen) — zwei Einträge mit identischem
        /// Dateinamen (z.B. zwei importierte KDE-neon-Einträge, die beim Versionscheck auf dieselbe
        /// aktuelle ISO auflösen) blieben doppelt stehen, weil der Namensvergleich identische
        /// Dateinamen ausdrücklich ausschließt (IsSameDistroDifferentVersion liefert dafür false).
        /// </summary>
        internal static List<int> FindExactDuplicateIndicesByFilename(IReadOnlyList<IsoEntry> entries)
        {
            var seen  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dupes = new List<int>();
            for (int i = 0; i < entries.Count; i++)
            {
                string fn = entries[i].Filename;
                if (string.IsNullOrWhiteSpace(fn)) continue; // ohne Dateiname kein Duplikat-Urteil
                if (!seen.Add(fn)) dupes.Add(i);
            }
            dupes.Reverse(); // absteigend, damit Remove(index) sicher nacheinander möglich ist
            return dupes;
        }

        internal static bool AreSameDistro(IsoEntry a, IsoEntry b)
        {
            bool aHas = !string.IsNullOrWhiteSpace(a.Filename); bool bHas = !string.IsNullOrWhiteSpace(b.Filename);
            if (aHas && bHas) return IsSameDistroDifferentVersion(a.Filename, b.Filename);
            if (!aHas && bHas) return IsLikelySameDistroByName(a.Name, b.Filename);
            if (aHas && !bHas) return IsLikelySameDistroByName(b.Name, a.Filename);
            return false;
        }

        internal static bool IsSameDistroDifferentVersion(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
            if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return false;
            return NormalizeForDistroComparison(a) == NormalizeForDistroComparison(b);
        }

        internal static bool IsVersionNewer(string c, string d)
        {
            if (string.IsNullOrWhiteSpace(c) || string.IsNullOrWhiteSpace(d) || string.Equals(c, d, StringComparison.OrdinalIgnoreCase)) return false;
            int[] cP = ParseVersionParts(c), dP = ParseVersionParts(d);
            if (cP.Length > 0 && dP.Length > 0)
            {
                for (int i = 0; i < Math.Max(cP.Length, dP.Length); i++)
                { int a = i < cP.Length ? cP[i] : 0, b = i < dP.Length ? dP[i] : 0; if (a > b) return true; if (a < b) return false; }
                return false;
            }
            return string.Compare(c, d, StringComparison.OrdinalIgnoreCase) > 0;
        }

        private static int[] ParseVersionParts(string v)
        { var n = new List<int>(); foreach (string p in v.Split('.', '-', '_')) { if (int.TryParse(p, out int x)) n.Add(x); else break; } return n.ToArray(); }

        /// <summary>
        /// BUGFIX: Ob ein Eintrag durch einen Versionscheck-Durchlauf tatsächlich einen NEUEN
        /// Dateinamen bekommen hat — nicht bloß, ob "hasUpdate" für den Versionscheck selbst true
        /// war. Manche Resolver (z.B. ResolveHirensAsync für Hiren's BootCD PE) liefern IMMER
        /// denselben statischen Dateinamen ohne Versionsnummer; für solche Einträge ist
        /// HttpService.IsUpdateAvailable() bei jedem Check "true" (keine Version aus dem Dateinamen
        /// ableitbar → jeder Fund gilt als Erstbezug), obwohl sich am Dateinamen nichts ändert. Wird
        /// so ein Eintrag von einem Stick importiert und beim nächsten Versionscheck erneut "als
        /// Update" markiert, darf er NICHT als "auf dem Stick veraltet" gelten — der alte und der
        /// neue Dateiname sind identisch, die Stick-Kopie IST die aktuelle. Dieselbe Unterscheidung
        /// trifft ApplyStickResults (regulärer Stick-Scan) bereits korrekt über
        /// IsSameDistroDifferentVersion (liefert bei identischem Dateinamen explizit false) — diese
        /// Methode wendet dasselbe Prinzip auf den separaten Stick-Abgleich in
        /// TriggerAutoVersionCheck an.
        /// </summary>
        internal static bool RepresentsGenuineFilenameChange(string? oldFilename, string? newFilename)
            => !string.IsNullOrWhiteSpace(oldFilename) && !string.IsNullOrWhiteSpace(newFilename)
               && !string.Equals(oldFilename, newFilename, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Klassifiziert jeden "alter Dateiname liegt noch auf dem Stick"-Kandidaten in zwei Fälle:
        /// echt veraltet (der NEUE/aktuelle Dateiname fehlt auf dem Stick — Download nötig) oder
        /// Stale-Duplikat (der neue Dateiname liegt BEREITS auf dem Stick — die alte Datei ist reiner
        /// Datenmüll, kein Download nötig, sondern ein Löschangebot). BUGFIX: die vorherige Logik prüfte
        /// nur "ist der alte Name noch da" und ignorierte, ob der neue Name zusätzlich schon vorhanden
        /// ist — dadurch feuerte "veraltet", obwohl der Stick bereits aktuell war (alte Datei nie
        /// gelöscht). Reine Funktion, keine Seiteneffekte — testbar ohne DB/Stick-Zugriff.
        /// </summary>
        internal static (List<(IsoEntry Entry, string OldFilename)> TrulyOutdated,
                          List<(IsoEntry Entry, string OldFilename)> StaleDuplicates)
            SplitOutdatedFromDuplicates(Dictionary<string, int> oldFn, IReadOnlyList<IsoEntry> entries, HashSet<string> stickFilenames)
        {
            var outdated   = new List<(IsoEntry, string)>();
            var duplicates = new List<(IsoEntry, string)>();
            foreach (var kvp in oldFn)
            {
                if (!stickFilenames.Contains(kvp.Key)) continue; // alter Name nicht (mehr) auf dem Stick
                var e = entries[kvp.Value];
                if (stickFilenames.Contains(e.Filename)) duplicates.Add((e, kvp.Key)); // neuer Name AUCH da
                else                                     outdated.Add((e, kvp.Key));   // neuer Name fehlt
            }
            return (outdated, duplicates);
        }

        internal static bool HasVersionlessFilename(string filename)
            => !string.IsNullOrWhiteSpace(filename) && string.IsNullOrEmpty(HttpService.ExtractVersion(filename));

        internal static string NormalizeForDistroComparison(string filename)
        {
            string s = Regex.Replace(filename.ToLowerInvariant(), @"[\d.]+", string.Empty);
            foreach (string cn in _platformCodenames)
            { s = s.Replace("." + cn, string.Empty); s = s.Replace("-" + cn, string.Empty); s = s.Replace("_" + cn, string.Empty); }
            return s;
        }

        internal static bool IsLikelySameDistroByName(string dbEntryName, string stickFilename)
        {
            if (string.IsNullOrWhiteSpace(dbEntryName) || string.IsNullOrWhiteSpace(stickFilename)) return false;
            string nameLower = dbEntryName.ToLowerInvariant();
            string fileLower = Path.GetFileNameWithoutExtension(stickFilename).ToLowerInvariant();
            string? dw = nameLower
                .Split(new[] { ' ', '-', '_', '.', '(', ')' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(w => w.Length > 4 && !char.IsDigit(w[0]) && !_genericDistroWords.Contains(w));
            return dw != null && fileLower.Contains(dw);
        }
    }
}
```

- [ ] **Step 2: Die verschobenen Methoden/Felder aus `ViewModels/MainViewModel.cs` entfernen**

10 gezielte Löschungen (mit dem Edit-Tool, alter Text → leer). Exakter zu löschender Text je Block:

Block A — die zwei statischen Felder:
```csharp
        private static readonly string[] _platformCodenames =
        {
            "noble", "jammy", "focal", "bionic", "oracular", "mantic", "lunar", "kinetic", "plucky",
            "trixie", "bookworm", "bullseye", "buster", "sid", "testing"
        };
        private static readonly HashSet<string> _genericDistroWords =
            new(StringComparer.OrdinalIgnoreCase)
            { "linux", "os", "live", "desktop", "server", "workstation", "official" };
```

Block B — `FindExactDuplicateIndicesByFilename` mit Doc-Kommentar:
```csharp
        /// <summary>
        /// Findet Indizes EXAKTER Duplikate: Einträge, deren (nicht-leerer) Dateiname bereits bei
        /// einem FRÜHEREN Eintrag vorkam. Der erste Eintrag je Dateiname bleibt, spätere gelten als
        /// Duplikat. Rückgabe absteigend sortiert, damit der Aufrufer per Remove(index) sicher
        /// nacheinander entfernen kann, ohne noch ausstehende Indizes zu verschieben. Reine Funktion,
        /// ohne DB-/Stick-Zugriff testbar. BUGFIX: DeduplicateEntries behandelte bisher NUR "gleiche
        /// Distro, andere Version" (unterschiedliche Dateinamen) — zwei Einträge mit identischem
        /// Dateinamen (z.B. zwei importierte KDE-neon-Einträge, die beim Versionscheck auf dieselbe
        /// aktuelle ISO auflösen) blieben doppelt stehen, weil der Namensvergleich identische
        /// Dateinamen ausdrücklich ausschließt (IsSameDistroDifferentVersion liefert dafür false).
        /// </summary>
        internal static List<int> FindExactDuplicateIndicesByFilename(IReadOnlyList<IsoEntry> entries)
        {
            var seen  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dupes = new List<int>();
            for (int i = 0; i < entries.Count; i++)
            {
                string fn = entries[i].Filename;
                if (string.IsNullOrWhiteSpace(fn)) continue; // ohne Dateiname kein Duplikat-Urteil
                if (!seen.Add(fn)) dupes.Add(i);
            }
            dupes.Reverse(); // absteigend, damit Remove(index) sicher nacheinander möglich ist
            return dupes;
        }

```

Block C — `AreSameDistro`:
```csharp
        internal static bool AreSameDistro(IsoEntry a, IsoEntry b)
        {
            bool aHas = !string.IsNullOrWhiteSpace(a.Filename); bool bHas = !string.IsNullOrWhiteSpace(b.Filename);
            if (aHas && bHas) return IsSameDistroDifferentVersion(a.Filename, b.Filename);
            if (!aHas && bHas) return IsLikelySameDistroByName(a.Name, b.Filename);
            if (aHas && !bHas) return IsLikelySameDistroByName(b.Name, a.Filename);
            return false;
        }

```

Block D — `IsVersionNewer` + `ParseVersionParts`:
```csharp
        internal static bool IsVersionNewer(string c, string d)
        {
            if (string.IsNullOrWhiteSpace(c) || string.IsNullOrWhiteSpace(d) || string.Equals(c, d, StringComparison.OrdinalIgnoreCase)) return false;
            int[] cP = ParseVersionParts(c), dP = ParseVersionParts(d);
            if (cP.Length > 0 && dP.Length > 0)
            {
                for (int i = 0; i < Math.Max(cP.Length, dP.Length); i++)
                { int a = i < cP.Length ? cP[i] : 0, b = i < dP.Length ? dP[i] : 0; if (a > b) return true; if (a < b) return false; }
                return false;
            }
            return string.Compare(c, d, StringComparison.OrdinalIgnoreCase) > 0;
        }

        private static int[] ParseVersionParts(string v)
        { var n = new List<int>(); foreach (string p in v.Split('.', '-', '_')) { if (int.TryParse(p, out int x)) n.Add(x); else break; } return n.ToArray(); }

```

Block E — `IsSameDistroDifferentVersion`:
```csharp
        internal static bool IsSameDistroDifferentVersion(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
            if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return false;
            return NormalizeForDistroComparison(a) == NormalizeForDistroComparison(b);
        }

```

Block F — `RepresentsGenuineFilenameChange` mit Doc-Kommentar:
```csharp
        /// <summary>
        /// BUGFIX: Ob ein Eintrag durch einen Versionscheck-Durchlauf tatsächlich einen NEUEN
        /// Dateinamen bekommen hat — nicht bloß, ob "hasUpdate" für den Versionscheck selbst true
        /// war. Manche Resolver (z.B. ResolveHirensAsync für Hiren's BootCD PE) liefern IMMER
        /// denselben statischen Dateinamen ohne Versionsnummer; für solche Einträge ist
        /// HttpService.IsUpdateAvailable() bei jedem Check "true" (keine Version aus dem Dateinamen
        /// ableitbar → jeder Fund gilt als Erstbezug), obwohl sich am Dateinamen nichts ändert. Wird
        /// so ein Eintrag von einem Stick importiert und beim nächsten Versionscheck erneut "als
        /// Update" markiert, darf er NICHT als "auf dem Stick veraltet" gelten — der alte und der
        /// neue Dateiname sind identisch, die Stick-Kopie IST die aktuelle. Dieselbe Unterscheidung
        /// trifft ApplyStickResults (regulärer Stick-Scan) bereits korrekt über
        /// IsSameDistroDifferentVersion (liefert bei identischem Dateinamen explizit false) — diese
        /// Methode wendet dasselbe Prinzip auf den separaten Stick-Abgleich in
        /// TriggerAutoVersionCheck an.
        /// </summary>
        internal static bool RepresentsGenuineFilenameChange(string? oldFilename, string? newFilename)
            => !string.IsNullOrWhiteSpace(oldFilename) && !string.IsNullOrWhiteSpace(newFilename)
               && !string.Equals(oldFilename, newFilename, StringComparison.OrdinalIgnoreCase);

```

Block G — `SplitOutdatedFromDuplicates` mit Doc-Kommentar:
```csharp
        /// <summary>
        /// Klassifiziert jeden "alter Dateiname liegt noch auf dem Stick"-Kandidaten in zwei Fälle:
        /// echt veraltet (der NEUE/aktuelle Dateiname fehlt auf dem Stick — Download nötig) oder
        /// Stale-Duplikat (der neue Dateiname liegt BEREITS auf dem Stick — die alte Datei ist reiner
        /// Datenmüll, kein Download nötig, sondern ein Löschangebot). BUGFIX: die vorherige Logik prüfte
        /// nur "ist der alte Name noch da" und ignorierte, ob der neue Name zusätzlich schon vorhanden
        /// ist — dadurch feuerte "veraltet", obwohl der Stick bereits aktuell war (alte Datei nie
        /// gelöscht). Reine Funktion, keine Seiteneffekte — testbar ohne DB/Stick-Zugriff.
        /// </summary>
        internal static (List<(IsoEntry Entry, string OldFilename)> TrulyOutdated,
                          List<(IsoEntry Entry, string OldFilename)> StaleDuplicates)
            SplitOutdatedFromDuplicates(Dictionary<string, int> oldFn, IReadOnlyList<IsoEntry> entries, HashSet<string> stickFilenames)
        {
            var outdated   = new List<(IsoEntry, string)>();
            var duplicates = new List<(IsoEntry, string)>();
            foreach (var kvp in oldFn)
            {
                if (!stickFilenames.Contains(kvp.Key)) continue; // alter Name nicht (mehr) auf dem Stick
                var e = entries[kvp.Value];
                if (stickFilenames.Contains(e.Filename)) duplicates.Add((e, kvp.Key)); // neuer Name AUCH da
                else                                     outdated.Add((e, kvp.Key));   // neuer Name fehlt
            }
            return (outdated, duplicates);
        }

```

Block H — `HasVersionlessFilename`, `NormalizeForDistroComparison`, `IsLikelySameDistroByName` (drei aufeinanderfolgende Methoden, in einem Edit):
```csharp
        internal static bool HasVersionlessFilename(string filename)
            => !string.IsNullOrWhiteSpace(filename) && string.IsNullOrEmpty(HttpService.ExtractVersion(filename));

        internal static string NormalizeForDistroComparison(string filename)
        {
            string s = Regex.Replace(filename.ToLowerInvariant(), @"[\d.]+", string.Empty);
            foreach (string cn in _platformCodenames)
            { s = s.Replace("." + cn, string.Empty); s = s.Replace("-" + cn, string.Empty); s = s.Replace("_" + cn, string.Empty); }
            return s;
        }

        internal static bool IsLikelySameDistroByName(string dbEntryName, string stickFilename)
        {
            if (string.IsNullOrWhiteSpace(dbEntryName) || string.IsNullOrWhiteSpace(stickFilename)) return false;
            string nameLower = dbEntryName.ToLowerInvariant();
            string fileLower = Path.GetFileNameWithoutExtension(stickFilename).ToLowerInvariant();
            string? dw = nameLower
                .Split(new[] { ' ', '-', '_', '.', '(', ')' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(w => w.Length > 4 && !char.IsDigit(w[0]) && !_genericDistroWords.Contains(w));
            return dw != null && fileLower.Contains(dw);
        }

```
(Jeden Block durch eine leere Zeichenkette ersetzen — d.h. die Zeilen komplett entfernen.)

- [ ] **Step 3: Aufrufstellen in `ViewModels/MainViewModel.cs` auf `DistroMatcher.` umstellen**

12 Ersetzungen (alt → neu, jeweils eindeutig im Kontext ihrer Zeile):

| Alt | Neu |
|---|---|
| `foreach (int i in FindExactDuplicateIndicesByFilename(_db.Entries))` | `foreach (int i in DistroMatcher.FindExactDuplicateIndicesByFilename(_db.Entries))` |
| `                                AreSameDistro(a, b))` | `                                DistroMatcher.AreSameDistro(a, b))` |
| `                                IsVersionNewer(` (in `DeduplicateEntries`, gefolgt von `HttpService.ExtractVersion(e.Filename),`) | `                                DistroMatcher.IsVersionNewer(` |
| `var existing = _db.Entries.FirstOrDefault(d => AreSameDistro(d, e));` | `var existing = _db.Entries.FirstOrDefault(d => DistroMatcher.AreSameDistro(d, e));` |
| `var (od, duplicates) = SplitOutdatedFromDuplicates(oldFn, _db.Entries, stickFn);` | `var (od, duplicates) = DistroMatcher.SplitOutdatedFromDuplicates(oldFn, _db.Entries, stickFn);` |
| `                    .FirstOrDefault(e => IsLikelySameDistroByName(e.Name, stickIso.Filename) &&` | `                    .FirstOrDefault(e => DistroMatcher.IsLikelySameDistroByName(e.Name, stickIso.Filename) &&` |
| `                         IsVersionNewer(HttpService.ExtractVersion(stickIso.Filename), HttpService.ExtractVersion(e.Filename))));` | `                         DistroMatcher.IsVersionNewer(HttpService.ExtractVersion(stickIso.Filename), HttpService.ExtractVersion(e.Filename))));` |
| `                var si = found.FirstOrDefault(f => IsSameDistroDifferentVersion(e.Filename, f.Filename));` | `                var si = found.FirstOrDefault(f => DistroMatcher.IsSameDistroDifferentVersion(e.Filename, f.Filename));` |
| `                if (IsVersionNewer(HttpService.ExtractVersion(si.Filename), HttpService.ExtractVersion(e.Filename)))` | `                if (DistroMatcher.IsVersionNewer(HttpService.ExtractVersion(si.Filename), HttpService.ExtractVersion(e.Filename)))` |
| `                var other = found.FirstOrDefault(f => IsSameDistroDifferentVersion(e.Filename, f.Filename));` | `                var other = found.FirstOrDefault(f => DistroMatcher.IsSameDistroDifferentVersion(e.Filename, f.Filename));` |
| `                if (string.IsNullOrEmpty(e.Sha256) || !HasVersionlessFilename(e.Filename)) continue;` | `                if (string.IsNullOrEmpty(e.Sha256) || !DistroMatcher.HasVersionlessFilename(e.Filename)) continue;` |
| `                             && RepresentsGenuineFilenameChange(_db.Entries[i].Filename, _db.Entries[i].RemoteFilename))` | `                             && DistroMatcher.RepresentsGenuineFilenameChange(_db.Entries[i].Filename, _db.Entries[i].RemoteFilename))` |

- [ ] **Step 4: `ULM.Tests/MainViewModelDistroMatchingTests.cs` auf `DistroMatcher` umstellen**

`using ULM.Core.Services;` zur Datei hinzufügen (nach `using ULM.Core.Models;`). Dann alle Aufrufe `MainViewModel.<Methode>(` auf `DistroMatcher.<Methode>(` umstellen, für genau diese sieben Methodennamen: `IsSameDistroDifferentVersion`, `FindExactDuplicateIndicesByFilename`, `IsVersionNewer`, `RepresentsGenuineFilenameChange`, `SplitOutdatedFromDuplicates`, `HasVersionlessFilename`, `IsLikelySameDistroByName`. Per PowerShell (präzise, keine Fehlübertragung durch manuelles Retippen):

```powershell
$path = "ULM.Tests/MainViewModelDistroMatchingTests.cs"
$content = Get-Content -Path $path -Raw -Encoding UTF8
$methods = @('IsSameDistroDifferentVersion','FindExactDuplicateIndicesByFilename','IsVersionNewer',
             'RepresentsGenuineFilenameChange','SplitOutdatedFromDuplicates','HasVersionlessFilename',
             'IsLikelySameDistroByName')
foreach ($m in $methods) { $content = $content -replace "MainViewModel\.$m\(", "DistroMatcher.$m(" }
Set-Content -Path $path -Value $content -Encoding UTF8 -NoNewline
```

Danach mit dem Edit-Tool `using ULM.Core.Models;` → `using ULM.Core.Models;
using ULM.Core.Services;` ergänzen (einmalig, am Kopf der Datei).

- [ ] **Step 5: Build und Tests**

```bash
dotnet build
dotnet test ULM.Tests/ULM.Tests.csproj
```
Erwartet: Build ohne Fehler, **115 Tests, 0 Fehler** (identische Anzahl — reine Verschiebung, keine neuen/entfernten Testfälle).

- [ ] **Step 6: Commit**

```bash
git add Core/Services/DistroMatcher.cs ViewModels/MainViewModel.cs ULM.Tests/MainViewModelDistroMatchingTests.cs
git commit -m "refactor: Distro-Matching-Logik von MainViewModel nach DistroMatcher verschoben"
```

---

### Task 2: Stick-Meldungs-Zustand von `MainWindow` ins `MainViewModel` verschieben

**Files:**
- Modify: `ViewModels/MainViewModel.cs:27-34` (vier Felder + vier Methoden ergänzen)
- Modify: `Views/MainWindow.xaml.cs:31-34,63,86,117,510` (Felder entfernen, Aufrufe umstellen)
- Test: `ULM.Tests/MainViewModelStickNotificationTests.cs` (neu)

**Interfaces:**
- Produces: `MainViewModel.MarkCopyOffered(string drive, string filename) : bool`, `MarkUnknownStickIsoOffered(string drive, string filename) : bool`, `MarkNewerVersionOffered(string drive, string filename) : bool`, `MarkIncompleteStickIsoOffered(string drive, string filename) : bool` — jede liefert `true` beim ersten Aufruf für eine Drive+Filename-Kombination, `false` bei jedem weiteren (identische Semantik wie das bisherige `HashSet.Add(...)` in der View).

- [ ] **Step 1: Vier Felder und vier Methoden in `ViewModels/MainViewModel.cs` ergänzen**

Mit dem Edit-Tool nach der bestehenden Zeile `        private bool     _expertMode;` einfügen:

```csharp
        private bool     _expertMode;

        // Session-Dedup: verhindert, dass derselbe Stick-Fund (Laufwerk+Dateiname) dem Nutzer
        // mehrfach als Dialog/Meldung angeboten wird, wenn TriggerUsbScan mehrfach über denselben
        // Fund läuft. Anwendungszustand ("wurde dieser Fund schon behandelt?"), daher im ViewModel
        // statt in MainWindow.xaml.cs (dort lag es vorher, was gegen MVVM verstieß).
        private readonly HashSet<string> _offeredCopyKeys     = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _importedStickKeys   = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _newerVersionKeys    = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _incompleteStickKeys = new(StringComparer.OrdinalIgnoreCase);

        public bool MarkCopyOffered(string drive, string filename)               => _offeredCopyKeys.Add($"{drive}|{filename}");
        public bool MarkUnknownStickIsoOffered(string drive, string filename)    => _importedStickKeys.Add($"{drive}|{filename}");
        public bool MarkNewerVersionOffered(string drive, string filename)       => _newerVersionKeys.Add($"{drive}|{filename}");
        public bool MarkIncompleteStickIsoOffered(string drive, string filename) => _incompleteStickKeys.Add($"{drive}|{filename}");
```

(D.h. old_string ist nur die eine Zeile `        private bool     _expertMode;`, new_string ist der komplette obige Block.)

- [ ] **Step 2: Die vier Felder aus `Views/MainWindow.xaml.cs` entfernen**

Alter Text (Zeilen 31-34):
```csharp
        private readonly HashSet<string> _offeredCopyKeys     = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _importedStickKeys   = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _newerVersionKeys    = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _incompleteStickKeys = new(StringComparer.OrdinalIgnoreCase);
```
→ komplett entfernen (leerer new_string).

- [ ] **Step 3: Die vier Nutzungsstellen in `Views/MainWindow.xaml.cs` umstellen**

| Alt | Neu |
|---|---|
| `var fresh = matches.Where(m => _newerVersionKeys.Add($"{drive}\|{m.StickIso.Filename}")).ToList();` | `var fresh = matches.Where(m => _vm.MarkNewerVersionOffered(drive, m.StickIso.Filename)).ToList();` |
| `var fresh = unknowns.Where(u => _importedStickKeys.Add($"{drive}\|{u.Filename}")).ToList();` | `var fresh = unknowns.Where(u => _vm.MarkUnknownStickIsoOffered(drive, u.Filename)).ToList();` |
| `var fresh = incomplete.Where(i => _incompleteStickKeys.Add($"{drive}\|{i.Filename}")).ToList();` | `var fresh = incomplete.Where(i => _vm.MarkIncompleteStickIsoOffered(drive, i.Filename)).ToList();` |
| `var fresh = entries.Where(e => _offeredCopyKeys.Add($"{drive}\|{e.Filename}")).ToList();` | `var fresh = entries.Where(e => _vm.MarkCopyOffered(drive, e.Filename)).ToList();` |

(Die `\|` oben ist nur Markdown-Escaping für das Tabellenzeichen `|` — im tatsächlichen C#-Code steht ein normales `|`.)

- [ ] **Step 4: Test schreiben — `ULM.Tests/MainViewModelStickNotificationTests.cs`**

```csharp
// ULM.Tests/MainViewModelStickNotificationTests.cs
using System.Windows.Threading;
using ULM.ViewModels;
using Xunit;

namespace ULM.Tests;

public class MainViewModelStickNotificationTests
{
    private static MainViewModel CreateVm() => new(Dispatcher.CurrentDispatcher);

    [Fact]
    public void MarkNewerVersionOffered_FirstCall_ReturnsTrue()
    {
        var vm = CreateVm();
        Assert.True(vm.MarkNewerVersionOffered("E:", "ubuntu-26.04.iso"));
    }

    [Fact]
    public void MarkNewerVersionOffered_SecondCallSameDriveAndFile_ReturnsFalse()
    {
        var vm = CreateVm();
        vm.MarkNewerVersionOffered("E:", "ubuntu-26.04.iso");
        Assert.False(vm.MarkNewerVersionOffered("E:", "ubuntu-26.04.iso"));
    }

    [Fact]
    public void MarkNewerVersionOffered_SameFileDifferentDrive_ReturnsTrue()
    {
        var vm = CreateVm();
        vm.MarkNewerVersionOffered("E:", "ubuntu-26.04.iso");
        Assert.True(vm.MarkNewerVersionOffered("F:", "ubuntu-26.04.iso"));
    }

    [Fact]
    public void MarkCopyOffered_SecondCallSameKey_ReturnsFalse()
    {
        var vm = CreateVm();
        vm.MarkCopyOffered("E:", "debian-13.iso");
        Assert.False(vm.MarkCopyOffered("E:", "debian-13.iso"));
    }

    [Fact]
    public void MarkUnknownStickIsoOffered_SecondCallSameKey_ReturnsFalse()
    {
        var vm = CreateVm();
        vm.MarkUnknownStickIsoOffered("E:", "mystery.iso");
        Assert.False(vm.MarkUnknownStickIsoOffered("E:", "mystery.iso"));
    }

    [Fact]
    public void MarkIncompleteStickIsoOffered_SecondCallSameKey_ReturnsFalse()
    {
        var vm = CreateVm();
        vm.MarkIncompleteStickIsoOffered("E:", "broken.iso");
        Assert.False(vm.MarkIncompleteStickIsoOffered("E:", "broken.iso"));
    }
}
```

- [ ] **Step 5: Test zunächst NICHT ausführen können (Kompilierfehler erwartet, falls Step 1 übersprungen wurde)**

Falls Step 1 bereits erledigt ist (wie oben in Reihenfolge vorgesehen), kompiliert der Test direkt. Zur Kontrolle trotzdem einmal isoliert laufen lassen:

```bash
dotnet test ULM.Tests/ULM.Tests.csproj --filter "FullyQualifiedName~MainViewModelStickNotificationTests"
```
Erwartet: 6 neue Tests, alle bestehen.

- [ ] **Step 6: Vollständiger Build und Testlauf**

```bash
dotnet build
dotnet test ULM.Tests/ULM.Tests.csproj
```
Erwartet: **121 Tests, 0 Fehler** (115 Baseline + 6 neue).

- [ ] **Step 7: Commit**

```bash
git add ViewModels/MainViewModel.cs Views/MainWindow.xaml.cs ULM.Tests/MainViewModelStickNotificationTests.cs
git commit -m "refactor: Stick-Meldungs-Dedup-Zustand von MainWindow ins MainViewModel verschoben"
```

---

### Task 3: Dependency Injection für `HttpService`/`UsbService`/`IsoDatabaseService`

**Files:**
- Modify: `Core/Services/HttpService.cs` (Interface `IHttpService` ergänzen)
- Modify: `Core/Services/UsbService.cs` (Interface `IUsbService` ergänzen)
- Modify: `Core/Services/IsoDatabaseService.cs` (Interface `IIsoDatabaseService` ergänzen)
- Modify: `ViewModels/MainViewModel.cs` (Felder + Konstruktor + zwei `UsbService.Instance`-Aufrufe)
- Test: `ULM.Tests/MainViewModelServiceInjectionTests.cs` (neu)

**Interfaces:**
- Produces: `IHttpService { string? GitHubToken { get; set; } }`, `IUsbService { List<UsbDrive> ListRemovableDrives(); Task<(List<UsbService.StickIso> Found, List<UsbService.StickIso> Incomplete)> ScanStickVerifiedAsync(string letter, IReadOnlyList<IsoEntry> entries); }`, `IIsoDatabaseService { IReadOnlyList<IsoEntry> Entries { get; } int Count { get; } void Load(); void Save(); void SaveFilenames(); void Add(IsoEntry entry); void Remove(int index); }`.
- Consumes (aus Task 1/2, falls vorher ausgeführt): keine Überschneidung — andere Dateien.

**Wichtige Einschränkung (im PR-Text/Commit erwähnen, nicht stillschweigend übergehen):** Diese Injection deckt nur die drei Service-Zugriffe ab, die `MainViewModel` selbst direkt hält. Die eigentliche Netzwerk-/Stick-Arbeit läuft in den `*Worker`-Klassen in `Core/Workers/Workers.cs`, die `HttpService.Instance`/`UsbService.Instance` weiterhin direkt und unverändert aufrufen (11 Stellen, siehe `Workers.cs`). Diese Worker mit Fakes testbar zu machen wäre eine eigene, deutlich größere Aufgabe (Worker-Konstruktoren müssten ebenfalls Parameter bekommen) — bewusst **nicht** Teil dieses Plans (YAGNI, kein aktueller Bedarf).

- [ ] **Step 1: `IHttpService` in `Core/Services/HttpService.cs` ergänzen**

Mit dem Edit-Tool die Zeile `    public sealed class HttpService` ersetzen durch:
```csharp
    public interface IHttpService
    {
        string? GitHubToken { get; set; }
    }

    public sealed class HttpService : IHttpService
```

- [ ] **Step 2: `IUsbService` in `Core/Services/UsbService.cs` ergänzen**

Mit dem Edit-Tool die Zeile `    public sealed class UsbService` ersetzen durch:
```csharp
    public interface IUsbService
    {
        List<UsbDrive> ListRemovableDrives();
        Task<(List<UsbService.StickIso> Found, List<UsbService.StickIso> Incomplete)> ScanStickVerifiedAsync(string letter, IReadOnlyList<IsoEntry> entries);
    }

    public sealed class UsbService : IUsbService
```

- [ ] **Step 3: `IIsoDatabaseService` in `Core/Services/IsoDatabaseService.cs` ergänzen**

Mit dem Edit-Tool die Zeile `    public sealed class IsoDatabaseService` ersetzen durch:
```csharp
    public interface IIsoDatabaseService
    {
        IReadOnlyList<IsoEntry> Entries { get; }
        int Count { get; }
        void Load();
        void Save();
        void SaveFilenames();
        void Add(IsoEntry entry);
        void Remove(int index);
    }

    public sealed class IsoDatabaseService : IIsoDatabaseService
```

- [ ] **Step 4: Felder in `ViewModels/MainViewModel.cs` auf die Interfaces umstellen**

Alt:
```csharp
        private readonly IsoDatabaseService _db    = IsoDatabaseService.Instance;
        private readonly HttpService        _http  = HttpService.Instance;
        private readonly UsbService         _usb   = UsbService.Instance;
```
Neu:
```csharp
        private readonly IIsoDatabaseService _db;
        private readonly IHttpService        _http;
        private readonly IUsbService         _usb;
```

- [ ] **Step 5: Konstruktor um optionale Parameter erweitern**

Alt:
```csharp
        public MainViewModel(Dispatcher ui)
        {
            _ui = ui;
```
Neu:
```csharp
        public MainViewModel(Dispatcher ui, IHttpService? http = null, IUsbService? usb = null, IIsoDatabaseService? db = null)
        {
            _ui   = ui;
            _http = http ?? HttpService.Instance;
            _usb  = usb  ?? UsbService.Instance;
            _db   = db   ?? IsoDatabaseService.Instance;
```
Der restliche Konstruktor-Body bleibt unverändert (er griff bisher schon implizit über die Feld-Initializer auf `.Instance` zu — jetzt geschieht das explizit hier, Verhalten identisch für den bestehenden Aufrufer `new MainViewModel(Dispatcher)` in `Views/MainWindow.xaml.cs`, der unverändert bleibt).

- [ ] **Step 6: Die zwei direkten `UsbService.Instance`-Aufrufe im ViewModel auf das injizierte Feld umstellen**

Alt (in `VerifyStickIntegrityAsync`):
```csharp
                var (found, _) = await UsbService.Instance.ScanStickVerifiedAsync(SelectedDriveLetter, _db.Entries).ConfigureAwait(false);
```
Neu:
```csharp
                var (found, _) = await _usb.ScanStickVerifiedAsync(SelectedDriveLetter, _db.Entries).ConfigureAwait(false);
```

Alt (im Stick-Scan-Callback):
```csharp
                        var (si, incomplete) = await UsbService.Instance.ScanStickVerifiedAsync(driveToScan, _db.Entries).ConfigureAwait(false);
```
Neu:
```csharp
                        var (si, incomplete) = await _usb.ScanStickVerifiedAsync(driveToScan, _db.Entries).ConfigureAwait(false);
```

- [ ] **Step 7: Test schreiben — `ULM.Tests/MainViewModelServiceInjectionTests.cs`**

```csharp
// ULM.Tests/MainViewModelServiceInjectionTests.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Threading;
using ULM.Core.Models;
using ULM.Core.Services;
using ULM.ViewModels;
using Xunit;

namespace ULM.Tests;

// Hinweis: MainViewModel liest/schreibt im Konstruktor Einstellungen über AppPaths.Instance.SettingsIni
// (ulm_settings.ini neben der Test-Assembly, nicht die echte App-Konfiguration — AppPaths leitet den
// Pfad von AppContext.BaseDirectory ab, das im Testlauf auf ULM.Tests/bin/... zeigt). Das ist ein
// bestehendes, unveränderten Verhalten des Konstruktors; hier nur dokumentiert, nicht behoben.
public class MainViewModelServiceInjectionTests
{
    private sealed class FakeHttpService : IHttpService
    {
        public string? GitHubToken { get; set; }
    }

    private sealed class FakeUsbService : IUsbService
    {
        public List<UsbDrive> DrivesToReturn { get; set; } = new();
        public List<UsbDrive> ListRemovableDrives() => DrivesToReturn;
        public Task<(List<UsbService.StickIso> Found, List<UsbService.StickIso> Incomplete)> ScanStickVerifiedAsync(string letter, IReadOnlyList<IsoEntry> entries)
            => Task.FromResult((new List<UsbService.StickIso>(), new List<UsbService.StickIso>()));
    }

    private sealed class FakeIsoDatabaseService : IIsoDatabaseService
    {
        private readonly List<IsoEntry> _entries = new();
        public IReadOnlyList<IsoEntry> Entries => _entries;
        public int Count => _entries.Count;
        public void Load() { }
        public void Save() { }
        public void SaveFilenames() { }
        public void Add(IsoEntry entry) => _entries.Add(entry);
        public void Remove(int index) => _entries.RemoveAt(index);
    }

    [Fact]
    public void GitHubToken_Set_ForwardsToInjectedHttpService()
    {
        var fakeHttp = new FakeHttpService();
        var vm = new MainViewModel(Dispatcher.CurrentDispatcher, fakeHttp, new FakeUsbService(), new FakeIsoDatabaseService());

        vm.GitHubToken = "abc123";

        Assert.Equal("abc123", fakeHttp.GitHubToken);
    }

    [Fact]
    public void RefreshDrives_UsesInjectedUsbService_NotRealHardware()
    {
        var fakeUsb = new FakeUsbService { DrivesToReturn = new List<UsbDrive> { new("Z:", "TestStick", 32_000_000_000, "NTFS") } };
        var vm = new MainViewModel(Dispatcher.CurrentDispatcher, new FakeHttpService(), fakeUsb, new FakeIsoDatabaseService());

        vm.RefreshDrives();

        Assert.Single(vm.Drives);
        Assert.Equal("Z:", vm.Drives[0].Letter);
    }
}
```

- [ ] **Step 8: Build und Tests**

```bash
dotnet build
dotnet test ULM.Tests/ULM.Tests.csproj
```
Erwartet: **123 Tests, 0 Fehler** (121 aus Task 2 + 2 neue). Falls Task 1/2 in anderer Reihenfolge/nicht ausgeführt wurden, entsprechend weniger als Baseline — Hauptsache 0 Fehler und genau 2 mehr als der Stand vor diesem Task.

- [ ] **Step 9: Commit**

```bash
git add Core/Services/HttpService.cs Core/Services/UsbService.cs Core/Services/IsoDatabaseService.cs ViewModels/MainViewModel.cs ULM.Tests/MainViewModelServiceInjectionTests.cs
git commit -m "refactor: HttpService/UsbService/IsoDatabaseService ueber Interfaces in MainViewModel injizierbar"
```

---

### Task 4: `HttpService` in Transport- und Distro-Resolver-Datei aufteilen

**Files:**
- Modify: `Core/Services/HttpService.cs` (Distro-Resolver-Block entfernen, Klasse als `partial` markieren)
- Create: `Core/Services/HttpService.DistroResolvers.cs` (derselbe Block, unverändert)

**Interfaces:** Keine — `partial class HttpService` ist zur Laufzeit und für jeden Aufrufer (`Workers.cs`, `MainViewModel.cs`, Tests) exakt derselbe Typ wie vorher. Kein Call-Site ändert sich irgendwo im Projekt.

**Hintergrund:** Der Block ist im Original bereits mit einem Abschnittskommentar `// ── Distro-Resolver ──` markiert (Zeile 928) und endet unmittelbar vor der Dokumentation von `RaceMirrorsAsync` (Zeile 1235) — ein klar abgegrenzter, zusammenhängender Bereich von 22 privaten Resolver-Methoden (`ResolveUbuntuDesktopAsync` … `ResolveUbuntuGamepackAsync`). Die Verschiebung erfolgt **skriptgestützt statt manuell abgetippt**, um bei ~300 Zeilen Code mit Umlauten/Emoji jedes Transkriptionsrisiko auszuschließen — das Skript kopiert die Zeilen exakt byte-für-byte aus der bestehenden Datei.

- [ ] **Step 1: Grenzen verifizieren (Schutz gegen zwischenzeitliche Änderungen an der Datei)**

```powershell
$src = "Core/Services/HttpService.cs"
$all = Get-Content -Path $src -Encoding UTF8
if ($all[927] -notmatch '── Distro-Resolver ──') { throw "Startmarker nicht an Zeile 928 gefunden - Datei hat sich veraendert, Block neu lokalisieren." }
if ($all[1232] -notmatch 'ResolveUbuntuGamepackAsync') { throw "Endmarker nicht an Zeile 1233 gefunden - Datei hat sich veraendert, Block neu lokalisieren." }
if ($all[1233].Trim() -ne '') { throw "Zeile 1234 ist nicht leer wie erwartet - Blockgrenze pruefen." }
Write-Host "Grenzen bestaetigt: Zeilen 928-1234."
```
Erwartet: `Grenzen bestaetigt: Zeilen 928-1234.` Falls eine Exception geworfen wird: Blockgrenzen im aktuellen Stand von `Core/Services/HttpService.cs` per Lesen der Datei neu bestimmen (Suche nach `// ── Distro-Resolver ──` und der letzten `Resolve*Async`-Methode direkt vor der XML-Doku von `RaceMirrorsAsync`), dann Step-2-Skript mit den neuen Zeilennummern anpassen.

- [ ] **Step 2: Block ausschneiden und neue Datei schreiben**

```powershell
$src = "Core/Services/HttpService.cs"
$all = Get-Content -Path $src -Encoding UTF8

$header = $all[0..926]        # Zeilen 1-927 (unveraendert)
$block  = $all[927..1233]     # Zeilen 928-1234 (der Distro-Resolver-Block, unveraendert)
$footer = $all[1234..($all.Count-1)]  # Zeile 1235 bis Dateiende

# Neue Hauptdatei ohne den Block schreiben
Set-Content -Path $src -Value ($header + $footer) -Encoding UTF8

# Neue Partial-Datei mit dem Block schreiben
$newFileHeader = @'
// Core/Services/HttpService.DistroResolvers.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ULM.Core.Models;
using ULM.Infrastructure;

namespace ULM.Core.Services
{
    public sealed partial class HttpService
    {
'@
$newFileFooter = @'

    }
}
'@
$resolverBody = $block -join "`r`n"
Set-Content -Path "Core/Services/HttpService.DistroResolvers.cs" -Value ($newFileHeader + "`r`n" + $resolverBody + $newFileFooter) -Encoding UTF8

Write-Host "Fertig. Hauptdatei jetzt $((Get-Content $src).Count) Zeilen, neue Datei $((Get-Content 'Core/Services/HttpService.DistroResolvers.cs').Count) Zeilen."
```

- [ ] **Step 3: Hauptklasse als `partial` markieren**

Mit dem Edit-Tool in `Core/Services/HttpService.cs`:
Alt: `    public sealed class HttpService`
Neu: `    public sealed partial class HttpService`

(Dieser String ist in der Datei eindeutig — nur eine Klassendeklaration.)

- [ ] **Step 4: Stichprobe — Anfang/Ende beider Dateien lesen**

Mit dem Read-Tool `Core/Services/HttpService.cs` (erste 30 Zeilen und letzte 10 Zeilen) und `Core/Services/HttpService.DistroResolvers.cs` (komplett) ansehen. Prüfen:
- `HttpService.cs` beginnt weiterhin mit den ursprünglichen `using`-Zeilen und endet weiterhin mit den schließenden Klammern für Klasse und Namespace (kein abgeschnittener Rest).
- `HttpService.DistroResolvers.cs` enthält alle 22 `Resolve*Async`-Methoden vollständig, inklusive der Emoji/Umlaute in Kommentaren unbeschädigt (stichprobenartig `ResolveHirensAsync` und `ResolveManjaroAsync` ansehen, da diese Sonderfälle mit Kommentaren enthalten).

- [ ] **Step 5: Build und Tests**

```bash
dotnet build
dotnet test ULM.Tests/ULM.Tests.csproj
```
Erwartet: Build ohne Fehler (der Compiler schlägt sofort fehl, falls beim Skript-Schnitt Klammern unausgeglichen wurden oder Zeilen verloren gingen — das ist das eigentliche Sicherheitsnetz dieses Tasks). **123 Tests, 0 Fehler** (unverändert gegenüber Task 3 — dieser Task fügt keine neuen Tests hinzu, reine Datei-Aufteilung).

- [ ] **Step 6: `git diff --stat` gegenzuprüfen, dass nur Zeilen verschoben (nicht verändert) wurden**

```bash
git diff --stat Core/Services/HttpService.cs
git status --short
```
Erwartet: `HttpService.cs` zeigt nur Löschungen (~307 Zeilen), keine Änderungen an verbleibenden Zeilen; `HttpService.DistroResolvers.cs` erscheint als neue Datei (`??` bzw. `A`).

- [ ] **Step 7: Commit**

```bash
git add Core/Services/HttpService.cs Core/Services/HttpService.DistroResolvers.cs
git commit -m "refactor: HttpService in Transport-Datei und HttpService.DistroResolvers.cs aufgeteilt (partial class)"
```

---

## Abschluss

Nach allen vier Tasks: `dotnet build` fehlerfrei, `dotnet test ULM.Tests/ULM.Tests.csproj` zeigt **123 Tests, 0 Fehler** (115 Baseline + 6 aus Task 2 + 2 aus Task 3). Kein Aufrufer außerhalb der geänderten Dateien musste angefasst werden außer den in Task 2 explizit gelisteten vier Stellen in `Views/MainWindow.xaml.cs`. Die App-Funktionalität (Download, Stick-Scan, Ventoy, Duplikat-Erkennung) ist unverändert — dieser Plan verändert ausschließlich, WO Code liegt und WIE er instanziiert wird, nicht WAS er tut.

Manuelle Schlussprüfung (empfohlen, da dies UI-nahe Codepfade betrifft): App einmal starten (`dotnet run` oder die gebaute EXE), einen Stick mit ein paar ISOs einstecken, Duplikat-/Update-Erkennung einmal durchlaufen lassen und im Log auf normales Verhalten prüfen — insbesondere, dass Meldungen zu neueren/unbekannten/unvollständigen Stick-Funden weiterhin genau einmal pro Fund erscheinen (Task 2 betrifft direkt diese Dedup-Logik).
