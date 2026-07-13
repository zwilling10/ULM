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
}
