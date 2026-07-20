using ULM.Core.Models;
using ULM.Core.Services;
using ULM.ViewModels;
using Xunit;

namespace ULM.Tests;

/// <summary>
/// Testet die Distro-Namens-/Versions-Abgleichlogik in MainViewModel — genau der Code, der beim
/// Import unbekannter Stick-ISOs und beim Duplikat-Erkennen entscheidet, ob zwei Einträge dieselbe
/// Distro sind. Diese Methoden sind bewusst 'internal' (nicht public) und werden nur via
/// InternalsVisibleTo für dieses Testprojekt sichtbar (siehe UniversalLinuxManager.csproj).
/// </summary>
public class MainViewModelDistroMatchingTests
{
    [Fact]
    public void IsSameDistroDifferentVersion_SameDistroDifferentVersion_ReturnsTrue()
        => Assert.True(DistroMatcher.IsSameDistroDifferentVersion(
            "ubuntu-24.04-desktop-amd64.iso", "ubuntu-26.04-desktop-amd64.iso"));

    [Fact]
    public void IsSameDistroDifferentVersion_DifferentDistro_ReturnsFalse()
        => Assert.False(DistroMatcher.IsSameDistroDifferentVersion(
            "ubuntu-24.04-desktop-amd64.iso", "fedora-Workstation-Live-44-1.7.x86_64.iso"));

    [Fact]
    public void IsSameDistroDifferentVersion_IdenticalFilename_ReturnsFalse()
        // Exakt gleicher Dateiname bedeutet "gleiche Version", nicht "andere Version" —
        // die Funktion beantwortet explizit "ist es eine ANDERE Version derselben Distro?".
        => Assert.False(DistroMatcher.IsSameDistroDifferentVersion(
            "ubuntu-24.04-desktop-amd64.iso", "ubuntu-24.04-desktop-amd64.iso"));

    [Fact]
    public void IsSameDistroDifferentVersion_DebianCodenameDiffers_StillSameDistro()
        // Debian-Codenamen (trixie/bookworm) werden vor dem Vergleich entfernt, damit ein reiner
        // Codename-Wechsel zwischen zwei Releases nicht als "andere Distro" fehlinterpretiert wird.
        => Assert.True(DistroMatcher.IsSameDistroDifferentVersion(
            "debian-live-13.5.0-amd64-trixie.iso", "debian-live-12.0.0-amd64-bookworm.iso"));

    [Theory]
    [InlineData("", "ubuntu-24.04.iso")]
    [InlineData("ubuntu-24.04.iso", "")]
    [InlineData("", "")]
    public void IsSameDistroDifferentVersion_EmptyInput_ReturnsFalse(string a, string b)
        => Assert.False(DistroMatcher.IsSameDistroDifferentVersion(a, b));

    [Fact]
    public void IsLikelySameDistroByName_MatchingKeyword_ReturnsTrue()
        => Assert.True(DistroMatcher.IsLikelySameDistroByName(
            "Zorin OS 18 Core", "Zorin-OS-18-Core-64-bit-r1.iso"));

    [Fact]
    public void IsLikelySameDistroByName_PopOs_MatchesViaNvidiaKeyword()
    {
        // Bekannte Einschränkung: '!' ist kein anerkanntes Trennzeichen, daher wird "Pop!_OS" nicht
        // als eigenständiges "pop"-Schlüsselwort erkannt (der Teil vor dem '_' bleibt "pop!", das
        // durch die Länge <=4 nicht qualifiziert) — der Abgleich funktioniert hier trotzdem über
        // das nächste qualifizierende Wort "nvidia". Test dokumentiert das bewusst, damit eine
        // künftige Änderung an der Trennzeichen-Liste nicht versehentlich diesen Fallback verliert.
        Assert.True(DistroMatcher.IsLikelySameDistroByName(
            "Pop!_OS 24.04 LTS NVIDIA", "pop-os_24.04_amd64_nvidia_12.iso"));
    }

    [Fact]
    public void IsLikelySameDistroByName_UnrelatedDistro_ReturnsFalse()
        => Assert.False(DistroMatcher.IsLikelySameDistroByName(
            "Ubuntu 26.04 LTS", "fedora-Workstation-Live-44-1.7.x86_64.iso"));

    [Fact]
    public void IsLikelySameDistroByName_OnlyGenericWords_ReturnsFalse()
        // "OS", "Live" sind als generische Wörter ausgeschlossen — bleibt kein Schlüsselwort übrig,
        // kann kein sinnvoller Abgleich stattfinden.
        => Assert.False(DistroMatcher.IsLikelySameDistroByName("OS Live Server", "irgendwas.iso"));

    [Theory]
    [InlineData("", "irgendwas.iso")]
    [InlineData("Ubuntu", "")]
    public void IsLikelySameDistroByName_EmptyInput_ReturnsFalse(string name, string filename)
        => Assert.False(DistroMatcher.IsLikelySameDistroByName(name, filename));

    [Theory]
    [InlineData("18", "17", true)]
    [InlineData("17", "18", false)]
    [InlineData("1.2.0", "1.10.0", false)] // numerischer, nicht lexikalischer Vergleich: 1.2 < 1.10
    [InlineData("1.0", "1.0", false)]
    public void IsVersionNewer_ComparesNumerically(string candidate, string current, bool expected)
        => Assert.Equal(expected, DistroMatcher.IsVersionNewer(candidate, current));

    // Regression: Distros mit einem Resolver, der IMMER denselben statischen Dateinamen liefert
    // (z.B. Hiren's BootCD PE — ResolveHirensAsync gibt konstant "HBCD_PE_x64.iso" zurück, ohne
    // Versionsnummer im Namen), wurden nach dem Import von einem Stick fälschlich als "veraltet auf
    // dem Stick" gemeldet — bei JEDEM Versionscheck erneut, obwohl sich der Dateiname nie ändert und
    // die Stick-Kopie exakt der aktuellen entspricht. Ursache: TriggerAutoVersionCheck prüfte nur
    // "ist der alte Dateiname auf dem Stick vorhanden", ohne zu prüfen, ob sich der Dateiname beim
    // Update überhaupt geändert hat. Dieselbe Situation behandelt ApplyStickResults (der reguläre
    // Stick-Scan-Pfad) bereits korrekt: IsSameDistroDifferentVersion liefert für IDENTISCHE Namen
    // explizit false — nur ein tatsächlicher Namenswechsel gilt als "andere Version".
    [Theory]
    [InlineData("HBCD_PE_x64.iso", "HBCD_PE_x64.iso", false)]        // unverändert -> keine echte Änderung
    [InlineData("HBCD_PE_X64.ISO", "hbcd_pe_x64.iso", false)]        // Groß-/Kleinschreibung ignorieren
    [InlineData("equestria-os-2026.07.08-x86_64.iso", "equestria-os-2026.07.14-x86_64.iso", true)] // echte Umbenennung
    [InlineData("", "HBCD_PE_x64.iso", false)]
    [InlineData("HBCD_PE_x64.iso", "", false)]
    public void RepresentsGenuineFilenameChange_OnlyTrueWhenFilenameActuallyDiffers(string oldFilename, string newFilename, bool expected)
        => Assert.Equal(expected, DistroMatcher.RepresentsGenuineFilenameChange(oldFilename, newFilename));

    // Entscheidet, ob für einen Eintrag ein Hash-Rehash sinnvoll ist: nur wenn sich aus dem
    // Dateinamen KEINE Version ablesen lässt (z. B. "HBCD_PE_x64.iso") — bei versionierten
    // Dateinamen ("ubuntu-24.04...") reicht der bestehende Namensvergleich, ein Hash-Rehash über
    // USB wäre unnötig teuer.
    [Theory]
    [InlineData("HBCD_PE_x64.iso", true)]
    [InlineData("ubuntu-24.04-desktop-amd64.iso", false)]
    [InlineData("equestria-os-2026.07.15-x86_64.iso", false)]
    [InlineData("", false)]
    public void HasVersionlessFilename_TrueOnlyWithoutExtractableVersion(string filename, bool expected)
        => Assert.Equal(expected, DistroMatcher.HasVersionlessFilename(filename));

    public class MainViewModelShouldAdoptImportedFilenameTests
    {
        [Fact]
        public void ExistingHasNoFilenameYet_Adopts()
            // Katalogeintrag wurde noch nie aufgelöst (z.B. per "ISO suchen" ohne Dateiname
            // hinzugefügt) — jeder Fund beim Import ist ein echter Erstbezug.
            => Assert.True(DistroMatcher.ShouldAdoptImportedFilename("", "debian-live-13.5.0-amd64-xfce.iso"));

        [Fact]
        public void ImportedVersionNewer_Adopts()
            => Assert.True(DistroMatcher.ShouldAdoptImportedFilename(
                "debian-live-13.6.0-amd64-xfce.iso", "debian-live-13.7.0-amd64-xfce.iso"));

        [Fact]
        public void ImportedVersionOlder_DoesNotAdopt()
        {
            // Regression: genau der real aufgetretene Fall. AddImportedEntry übernahm bisher den
            // Dateinamen einer fälschlich als "unbekannt" einsortierten ISO UNGEPRÜFT — auch wenn sie
            // eine ÄLTERE Version war als die bereits im Katalog hinterlegte. Das degradierte den
            // Katalog aktiv rückwärts (Name blieb "13.6.0", Filename fiel zurück auf "13.5.0").
            Assert.False(DistroMatcher.ShouldAdoptImportedFilename(
                "debian-live-13.6.0-amd64-xfce.iso", "debian-live-13.5.0-amd64-xfce.iso"));
        }

        [Fact]
        public void ImportedVersionEqual_DoesNotAdopt()
            // Gleiche Version, nur anderer Dateiname (z.B. Mirror-Eigenheit) — kein echter Fortschritt,
            // der bestehende Eintrag bleibt unangetastet.
            => Assert.False(DistroMatcher.ShouldAdoptImportedFilename(
                "debian-live-13.6.0-amd64-xfce.iso", "debian-live-13.6.0-amd64-xfce.iso"));
    }

    public class MainViewModelSplitOutdatedFromDuplicatesTests
    {
        private static IsoEntry Entry(string filename) => new() { Name = filename, Filename = filename };

        [Fact]
        public void SplitOutdatedFromDuplicates_OldNamePresentNewNameAbsent_IsTrulyOutdated()
        {
            var entries = new List<IsoEntry> { Entry("equestria-os-2026.07.15-x86_64.iso") };
            var oldFn = new Dictionary<string, int> { ["equestria-os-2026.07.08-x86_64.iso"] = 0 };
            var stick = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "equestria-os-2026.07.08-x86_64.iso" };

            var (outdated, duplicates) = DistroMatcher.SplitOutdatedFromDuplicates(oldFn, entries, stick);

            Assert.Single(outdated);
            Assert.Equal("equestria-os-2026.07.08-x86_64.iso", outdated[0].OldFilename);
            Assert.Empty(duplicates);
        }

        [Fact]
        public void SplitOutdatedFromDuplicates_OldNameAndNewNameBothPresent_IsStaleDuplicate()
        {
            // Regression: genau der vom Nutzer gemeldete Fall — equestria-os-...07.08... UND
            // ...07.15... liegen gleichzeitig auf dem Stick. Die aktuelle Version ist bereits da,
            // die alte Datei ist reiner Datenmüll — KEINE "veraltet"-Meldung, sondern ein Löschangebot.
            var entries = new List<IsoEntry> { Entry("equestria-os-2026.07.15-x86_64.iso") };
            var oldFn = new Dictionary<string, int> { ["equestria-os-2026.07.08-x86_64.iso"] = 0 };
            var stick = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "equestria-os-2026.07.08-x86_64.iso", "equestria-os-2026.07.15-x86_64.iso" };

            var (outdated, duplicates) = DistroMatcher.SplitOutdatedFromDuplicates(oldFn, entries, stick);

            Assert.Empty(outdated);
            Assert.Single(duplicates);
            Assert.Equal("equestria-os-2026.07.08-x86_64.iso", duplicates[0].OldFilename);
        }

        [Fact]
        public void SplitOutdatedFromDuplicates_OldNameNotOnStick_ProducesNothing()
        {
            var entries = new List<IsoEntry> { Entry("equestria-os-2026.07.15-x86_64.iso") };
            var oldFn = new Dictionary<string, int> { ["equestria-os-2026.07.08-x86_64.iso"] = 0 };
            var stick = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // Stick leer / bereits bereinigt

            var (outdated, duplicates) = DistroMatcher.SplitOutdatedFromDuplicates(oldFn, entries, stick);

            Assert.Empty(outdated);
            Assert.Empty(duplicates);
        }

        [Fact]
        public void SplitOutdatedFromDuplicates_MultipleEntries_ClassifiesEachIndependently()
        {
            var entries = new List<IsoEntry>
            {
                Entry("distro-a-2.0.iso"), // Index 0 — echt veraltet
                Entry("distro-b-2.0.iso"), // Index 1 — Duplikat
            };
            var oldFn = new Dictionary<string, int>
            {
                ["distro-a-1.0.iso"] = 0,
                ["distro-b-1.0.iso"] = 1,
            };
            var stick = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "distro-a-1.0.iso", "distro-b-1.0.iso", "distro-b-2.0.iso" };

            var (outdated, duplicates) = DistroMatcher.SplitOutdatedFromDuplicates(oldFn, entries, stick);

            Assert.Single(outdated); Assert.Equal("distro-a-2.0.iso", outdated[0].Entry.Filename);
            Assert.Equal("distro-a-1.0.iso", outdated[0].OldFilename);
            Assert.Single(duplicates); Assert.Equal("distro-b-1.0.iso", duplicates[0].OldFilename);
        }
    }

    public class MainViewModelFindKnownDistroForStickFileTests
    {
        private static IsoEntry E(string name, string filename) => new() { Name = name, Filename = filename };

        [Fact]
        public void StickVersionNewer_ReturnsEntryWithStickIsNewerTrue()
        {
            var entries = new List<IsoEntry> { E("Ubuntu 24.04 LTS", "ubuntu-24.04-desktop-amd64.iso") };

            var result = DistroMatcher.FindKnownDistroForStickFile(entries, "ubuntu-26.04-desktop-amd64.iso");

            Assert.NotNull(result);
            Assert.Same(entries[0], result!.Value.Entry);
            Assert.True(result.Value.StickIsNewer);
        }

        [Fact]
        public void StickVersionOlder_ReturnsEntryWithStickIsNewerFalse()
        {
            // Regression: genau der real gemeldete Fall. Der Katalog wurde bereits per Versionscheck
            // auf 13.6.0 umbenannt, die alte 13.5.0-Datei liegt nach einem erfolgreichen Update noch
            // physisch auf dem Stick — OHNE oldFn-Kontext (z.B. weil der Scan aus einem anderen
            // Aufrufpfad als dem Versionscheck-Abschluss kommt). Muss trotzdem als "bekannt, nur
            // überholte Version" erkannt werden statt als "unbekannte Distro".
            var entries = new List<IsoEntry> { E("Debian 13.6.0 Trixie Live XFCE", "debian-live-13.6.0-amd64-xfce.iso") };

            var result = DistroMatcher.FindKnownDistroForStickFile(entries, "debian-live-13.5.0-amd64-xfce.iso");

            Assert.NotNull(result);
            Assert.Same(entries[0], result!.Value.Entry);
            Assert.False(result.Value.StickIsNewer);
        }

        [Fact]
        public void NoMatchingDistro_ReturnsNull()
        {
            var entries = new List<IsoEntry> { E("Ubuntu 26.04 LTS", "ubuntu-26.04-desktop-amd64.iso") };

            var result = DistroMatcher.FindKnownDistroForStickFile(entries, "fedora-Workstation-Live-44-1.7.x86_64.iso");

            Assert.Null(result);
        }

        [Fact]
        public void EntryFilenameEmpty_TreatsFirstFindAsNewer()
        {
            // Noch nie aufgelöster Katalogeintrag (z.B. per "ISO suchen" hinzugefügt) — jeder Fund
            // auf dem Stick zählt als Erstbezug, nicht als "überholt".
            var entries = new List<IsoEntry> { E("Zorin OS 18 Core", "") };

            var result = DistroMatcher.FindKnownDistroForStickFile(entries, "Zorin-OS-18-Core-64-bit-r1.iso");

            Assert.NotNull(result);
            Assert.True(result!.Value.StickIsNewer);
        }

        [Fact]
        public void ExactFilenameMatch_ReturnsNull()
        {
            // Wird vom Aufrufer normalerweise schon vorher rausgefiltert (dbFn-Check) — die Funktion
            // muss trotzdem für sich allein korrekt sein und keinen falschen Kandidaten liefern.
            var entries = new List<IsoEntry> { E("Ubuntu 24.04 LTS", "ubuntu-24.04-desktop-amd64.iso") };

            var result = DistroMatcher.FindKnownDistroForStickFile(entries, "ubuntu-24.04-desktop-amd64.iso");

            Assert.Null(result);
        }

        [Fact]
        public void MultipleEntries_MatchesOnlyTheRightOne()
        {
            var entries = new List<IsoEntry>
            {
                E("Fedora Workstation 44", "Fedora-Workstation-Live-44-1.7.x86_64.iso"),
                E("Debian 13.6.0 Trixie Live XFCE", "debian-live-13.6.0-amd64-xfce.iso"),
            };

            var result = DistroMatcher.FindKnownDistroForStickFile(entries, "debian-live-13.5.0-amd64-xfce.iso");

            Assert.NotNull(result);
            Assert.Same(entries[1], result!.Value.Entry);
            Assert.False(result.Value.StickIsNewer);
        }
    }

    public class MainViewModelFindExactDuplicateIndicesTests
    {
        private static IsoEntry E(string filename) => new() { Name = filename, Filename = filename };

        [Fact]
        public void FindExactDuplicateIndicesByFilename_IdenticalFilenames_ReturnsLaterIndex()
        {
            // Regression: genau der vom Nutzer gemeldete Fall — zwei Einträge mit IDENTISCHEM
            // Dateinamen (zwei importierte KDE-neon-Einträge, die beim Versionscheck auf dieselbe
            // aktuelle ISO kollabiert sind). Der erste bleibt, der zweite ist ein exaktes Duplikat.
            var entries = new List<IsoEntry>
            {
                E("neon-user-desktop-20260709-1906.iso"), // 0 — bleibt
                E("ubuntu-26.04-desktop-amd64.iso"),       // 1 — bleibt
                E("neon-user-desktop-20260709-1906.iso"), // 2 — Duplikat von 0
            };
            var dupes = DistroMatcher.FindExactDuplicateIndicesByFilename(entries);
            Assert.Equal(new[] { 2 }, dupes);
        }

        [Fact]
        public void FindExactDuplicateIndicesByFilename_MultipleDuplicates_ReturnedDescending()
        {
            // Absteigend, damit der Aufrufer per _db.Remove(index) sicher nacheinander entfernen kann,
            // ohne dass sich die Indizes noch nicht entfernter Duplikate verschieben.
            var entries = new List<IsoEntry> { E("a.iso"), E("a.iso"), E("a.iso") };
            var dupes = DistroMatcher.FindExactDuplicateIndicesByFilename(entries);
            Assert.Equal(new[] { 2, 1 }, dupes);
        }

        [Fact]
        public void FindExactDuplicateIndicesByFilename_EmptyFilenames_NeverDuplicates()
        {
            // Ohne Dateiname (noch nie aufgelöster Eintrag) lässt sich kein Duplikat-Urteil fällen.
            var entries = new List<IsoEntry> { E(""), E("") };
            Assert.Empty(DistroMatcher.FindExactDuplicateIndicesByFilename(entries));
        }
    }
}
