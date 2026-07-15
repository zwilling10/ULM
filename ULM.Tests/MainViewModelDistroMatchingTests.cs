using ULM.Core.Models;
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
        => Assert.True(MainViewModel.IsSameDistroDifferentVersion(
            "ubuntu-24.04-desktop-amd64.iso", "ubuntu-26.04-desktop-amd64.iso"));

    [Fact]
    public void IsSameDistroDifferentVersion_DifferentDistro_ReturnsFalse()
        => Assert.False(MainViewModel.IsSameDistroDifferentVersion(
            "ubuntu-24.04-desktop-amd64.iso", "fedora-Workstation-Live-44-1.7.x86_64.iso"));

    [Fact]
    public void IsSameDistroDifferentVersion_IdenticalFilename_ReturnsFalse()
        // Exakt gleicher Dateiname bedeutet "gleiche Version", nicht "andere Version" —
        // die Funktion beantwortet explizit "ist es eine ANDERE Version derselben Distro?".
        => Assert.False(MainViewModel.IsSameDistroDifferentVersion(
            "ubuntu-24.04-desktop-amd64.iso", "ubuntu-24.04-desktop-amd64.iso"));

    [Fact]
    public void IsSameDistroDifferentVersion_DebianCodenameDiffers_StillSameDistro()
        // Debian-Codenamen (trixie/bookworm) werden vor dem Vergleich entfernt, damit ein reiner
        // Codename-Wechsel zwischen zwei Releases nicht als "andere Distro" fehlinterpretiert wird.
        => Assert.True(MainViewModel.IsSameDistroDifferentVersion(
            "debian-live-13.5.0-amd64-trixie.iso", "debian-live-12.0.0-amd64-bookworm.iso"));

    [Theory]
    [InlineData("", "ubuntu-24.04.iso")]
    [InlineData("ubuntu-24.04.iso", "")]
    [InlineData("", "")]
    public void IsSameDistroDifferentVersion_EmptyInput_ReturnsFalse(string a, string b)
        => Assert.False(MainViewModel.IsSameDistroDifferentVersion(a, b));

    [Fact]
    public void IsLikelySameDistroByName_MatchingKeyword_ReturnsTrue()
        => Assert.True(MainViewModel.IsLikelySameDistroByName(
            "Zorin OS 18 Core", "Zorin-OS-18-Core-64-bit-r1.iso"));

    [Fact]
    public void IsLikelySameDistroByName_PopOs_MatchesViaNvidiaKeyword()
    {
        // Bekannte Einschränkung: '!' ist kein anerkanntes Trennzeichen, daher wird "Pop!_OS" nicht
        // als eigenständiges "pop"-Schlüsselwort erkannt (der Teil vor dem '_' bleibt "pop!", das
        // durch die Länge <=4 nicht qualifiziert) — der Abgleich funktioniert hier trotzdem über
        // das nächste qualifizierende Wort "nvidia". Test dokumentiert das bewusst, damit eine
        // künftige Änderung an der Trennzeichen-Liste nicht versehentlich diesen Fallback verliert.
        Assert.True(MainViewModel.IsLikelySameDistroByName(
            "Pop!_OS 24.04 LTS NVIDIA", "pop-os_24.04_amd64_nvidia_12.iso"));
    }

    [Fact]
    public void IsLikelySameDistroByName_UnrelatedDistro_ReturnsFalse()
        => Assert.False(MainViewModel.IsLikelySameDistroByName(
            "Ubuntu 26.04 LTS", "fedora-Workstation-Live-44-1.7.x86_64.iso"));

    [Fact]
    public void IsLikelySameDistroByName_OnlyGenericWords_ReturnsFalse()
        // "OS", "Live" sind als generische Wörter ausgeschlossen — bleibt kein Schlüsselwort übrig,
        // kann kein sinnvoller Abgleich stattfinden.
        => Assert.False(MainViewModel.IsLikelySameDistroByName("OS Live Server", "irgendwas.iso"));

    [Theory]
    [InlineData("", "irgendwas.iso")]
    [InlineData("Ubuntu", "")]
    public void IsLikelySameDistroByName_EmptyInput_ReturnsFalse(string name, string filename)
        => Assert.False(MainViewModel.IsLikelySameDistroByName(name, filename));

    [Theory]
    [InlineData("18", "17", true)]
    [InlineData("17", "18", false)]
    [InlineData("1.2.0", "1.10.0", false)] // numerischer, nicht lexikalischer Vergleich: 1.2 < 1.10
    [InlineData("1.0", "1.0", false)]
    public void IsVersionNewer_ComparesNumerically(string candidate, string current, bool expected)
        => Assert.Equal(expected, MainViewModel.IsVersionNewer(candidate, current));

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
        => Assert.Equal(expected, MainViewModel.RepresentsGenuineFilenameChange(oldFilename, newFilename));

    public class MainViewModelSplitOutdatedFromDuplicatesTests
    {
        private static IsoEntry Entry(string filename) => new() { Name = filename, Filename = filename };

        [Fact]
        public void SplitOutdatedFromDuplicates_OldNamePresentNewNameAbsent_IsTrulyOutdated()
        {
            var entries = new List<IsoEntry> { Entry("equestria-os-2026.07.15-x86_64.iso") };
            var oldFn = new Dictionary<string, int> { ["equestria-os-2026.07.08-x86_64.iso"] = 0 };
            var stick = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "equestria-os-2026.07.08-x86_64.iso" };

            var (outdated, duplicates) = MainViewModel.SplitOutdatedFromDuplicates(oldFn, entries, stick);

            Assert.Single(outdated);
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

            var (outdated, duplicates) = MainViewModel.SplitOutdatedFromDuplicates(oldFn, entries, stick);

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

            var (outdated, duplicates) = MainViewModel.SplitOutdatedFromDuplicates(oldFn, entries, stick);

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

            var (outdated, duplicates) = MainViewModel.SplitOutdatedFromDuplicates(oldFn, entries, stick);

            Assert.Single(outdated); Assert.Equal("distro-a-2.0.iso", outdated[0].Filename);
            Assert.Single(duplicates); Assert.Equal("distro-b-1.0.iso", duplicates[0].OldFilename);
        }
    }
}
