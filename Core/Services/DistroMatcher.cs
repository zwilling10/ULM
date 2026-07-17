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
