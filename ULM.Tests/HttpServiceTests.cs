using ULM.Core.Models;
using ULM.Core.Services;
using Xunit;

namespace ULM.Tests;

public class HttpServiceExtractVersionTests
{
    [Theory]
    [InlineData("ubuntu-26.04-desktop-amd64.iso", "26.04")]
    [InlineData("tails-amd64-7.9.1.iso", "7.9.1")]
    [InlineData("linuxmint-22.3-cinnamon-64bit.iso", "22.3")]
    [InlineData("Zorin-OS-18-Core-64-bit-r1.iso", "18")]
    [InlineData("HBCD_PE_x64.iso", "")]
    public void ExtractVersion_ReturnsExpectedVersion(string filename, string expected)
        => Assert.Equal(expected, HttpService.ExtractVersion(filename));

    [Fact]
    public void ExtractVersion_FedoraStyle_PrefersReleaseNumberOverSubBuild()
    {
        // Regression: "1.7" (Sub-Build) wurde früher fälschlich vor "44" (echte Release-Nummer)
        // erkannt — siehe Kommentar in ExtractVersion zu genau diesem Fall.
        string v = HttpService.ExtractVersion("Fedora-Workstation-Live-44-1.7.x86_64.iso");
        Assert.Equal("44", v);
    }

    [Fact]
    public void ExtractVersion_ManjaroStyle_BuildSuffixIncluded()
    {
        // Manjaro-Dateinamen enthalten Version + Build-Nummer zusammen (26.0.4-260327) — beide
        // gehören laut VersionComparer zur vergleichbaren Versionskennung, werden also bewusst
        // gemeinsam erfasst statt nur "26.0.4".
        string v = HttpService.ExtractVersion("manjaro-kde-26.0.4-260327-linux618.iso");
        Assert.Equal("26.0.4-260327", v);
    }
}

public class HttpServiceIsVersionNewerTests
{
    [Theory]
    [InlineData("18", "17", true)]
    [InlineData("17", "18", false)]
    [InlineData("26.04", "26.04", false)]
    [InlineData("26.0.4", "26.0.4-260327", false)] // gleiche Version, fehlendes Build-Suffix ist NICHT neuer
    [InlineData("26.0.4-260327", "26.0.4", true)]  // Build-Suffix vorhanden vs. fehlend: das mit Suffix gewinnt
    [InlineData("7.9.1", "7.9", true)]
    public void IsVersionNewer_ComparesNumerically(string candidate, string current, bool expected)
        => Assert.Equal(expected, HttpService.IsVersionNewer(candidate, current));

    [Theory]
    [InlineData("", "1.0")]
    [InlineData("1.0", "")]
    [InlineData("", "")]
    public void IsVersionNewer_EmptyInput_ReturnsFalse(string candidate, string current)
        => Assert.False(HttpService.IsVersionNewer(candidate, current));
}

public class HttpServiceIsUpdateAvailableTests
{
    [Theory]
    // Bug-Regression: der Eintrag repräsentiert bereits die aktuellste Version (aus Dateiname ODER
    // Name) — es darf KEIN Update angezeigt werden, weder mit Dateiname noch nur mit Namensversion.
    [InlineData("tails-amd64-7.9.1.iso", "7.9.1", true, false)]
    [InlineData("Tails 7.9.1", "7.9.1", true, false)]              // nur Name trägt die Version
    [InlineData("Parrot-security-7.3_amd64.iso", "7.3", true, false)]
    // Echtes Update: online ist neuer als die repräsentierte Version.
    [InlineData("tails-amd64-7.7.1.iso", "7.9.1", true, true)]
    [InlineData("Tails 7.7.1", "7.9.1", true, true)]
    // Katalog ist NEUER als der Online-Fund → kein (Downgrade-)Update, kein blindes "true".
    [InlineData("Foo 5.0", "4.0", true, false)]
    // Völlig unbekannt (keine Version aus Name/Dateiname ableitbar): jede gefundene Datei = Erstbezug.
    [InlineData("HBCD_PE_x64.iso", "", true, true)]
    [InlineData("", "3.2", true, true)]
    // Nichts gefunden: kein Update.
    [InlineData("HBCD_PE_x64.iso", "", false, false)]
    [InlineData("", "", false, false)]
    public void IsUpdateAvailable_OnlyWhenRemoteIsNewerOrTrulyUnknown(
        string localFilenameOrName, string remoteVersion, bool remoteFileFound, bool expected)
        => Assert.Equal(expected, HttpService.IsUpdateAvailable(localFilenameOrName, remoteVersion, remoteFileFound));
}

public class HttpServiceBestLocalVersionSourceTests
{
    [Fact]
    public void BestLocalVersionSource_FilenameHasVersion_PrefersFilename()
        => Assert.Equal("ubuntu-26.04-desktop-amd64.iso",
            HttpService.BestLocalVersionSource("ubuntu-26.04-desktop-amd64.iso", "Ubuntu"));

    [Fact]
    public void BestLocalVersionSource_FilenameHasNoVersion_FallsBackToName()
    {
        // Regression: Hiren's BootCD PE liefert vom Resolver einen statischen, versionslosen
        // Dateinamen ("HBCD_PE_x64.iso") — ExtractVersion davon ist immer leer. Ohne Fallback auf
        // den Namen (der die Version "v1.0.8" bereits fest im Katalog trägt) meldete
        // IsUpdateAvailable bei JEDEM Check "Update", obwohl sich nichts geändert hat.
        string result = HttpService.BestLocalVersionSource("HBCD_PE_x64.iso", "Hiren's BootCD PE x64 v1.0.8");
        Assert.Equal("Hiren's BootCD PE x64 v1.0.8", result);
    }

    [Fact]
    public void BestLocalVersionSource_FilenameEmpty_UsesName()
        => Assert.Equal("Foo 7.9.1", HttpService.BestLocalVersionSource("", "Foo 7.9.1"));

    [Fact]
    public void BestLocalVersionSource_BothEmpty_ReturnsEmpty()
        => Assert.Equal(string.Empty, HttpService.BestLocalVersionSource("", ""));

    [Fact]
    public void IsUpdateAvailable_StaticVersionlessFilename_FallsBackToNameVersion_NoSpuriousUpdate()
    {
        // End-to-end-Regression fuer den Hiren's-BootCD-PE-Fall: kombiniert mit
        // BestLocalVersionSource darf IsUpdateAvailable KEIN Update mehr melden, wenn die aus dem
        // Namen abgeleitete Version mit der (hartkodiert) gefundenen Remote-Version uebereinstimmt.
        string localSource = HttpService.BestLocalVersionSource("HBCD_PE_x64.iso", "Hiren's BootCD PE x64 v1.0.8");
        Assert.False(HttpService.IsUpdateAvailable(localSource, "1.0.8", remoteFileFound: true));
    }
}

public class HttpServiceNormalizeForMatchTests
{
    [Theory]
    // Regression: "Pop!_OS 24.04 LTS NVIDIA" (Katalog) und "pop os 24.04 amd64 nvidia 12"
    // (vom Stick importiert, aus dem Dateinamen abgeleitet) müssen auf denselben normalisierten
    // Kern-Präfix abbilden, sonst greift der dedizierte Pop!_OS-Resolver für importierte
    // Einträge nicht (genau der Bug, der reale Health-Check-Fehlschläge verursacht hat).
    [InlineData("Pop!_OS 24.04 LTS NVIDIA", "popos2404ltsnvidia")]
    [InlineData("pop os 24.04 amd64 nvidia 12", "popos2404amd64nvidia12")]
    [InlineData("MX Linux 25.2 XFCE", "mxlinux252xfce")]
    [InlineData("Dr.Web LiveDisk 9.0.1", "drweblivedisk901")]
    public void NormalizeForMatch_StripsPunctuationAndLowercases(string input, string expected)
        => Assert.Equal(expected, HttpService.NormalizeForMatch(input));

    [Fact]
    public void NormalizeForMatch_PopOsVariants_AllContainSameKeyword()
    {
        string[] variants =
        {
            "Pop!_OS 24.04 LTS NVIDIA",
            "pop-os_24.04_amd64_nvidia_12.iso",
            "pop os 24.04 amd64 nvidia 12",
        };
        foreach (string v in variants)
            Assert.Contains("popos", HttpService.NormalizeForMatch(v));
    }
}

public class HttpServiceHasDedicatedResolverTests
{
    [Theory]
    [InlineData("Ubuntu 26.04 Desktop", "")]
    [InlineData("Linux Mint 22.3 Cinnamon", "")]
    [InlineData("Fedora Workstation", "")]
    [InlineData("Linux Kodachi", "")]
    [InlineData("Rescuezilla", "rescuezilla/rescuezilla")] // dediziert über GithubRepo, nicht Namen
    public void HasDedicatedResolver_ReturnsTrue_ForKnownDistros(string name, string githubRepo)
    {
        var entry = new IsoEntry { Name = name, GithubRepo = githubRepo };
        Assert.True(HttpService.HasDedicatedResolver(entry));
    }

    [Theory]
    // Regression: genau der Fall aus der Spec — eine Distro ohne jeden dedizierten Resolver.
    [InlineData("Shadowfetch Linux")]
    [InlineData("Irgendeine Ganz Neue Distro")]
    public void HasDedicatedResolver_ReturnsFalse_ForUnknownDistros(string name)
    {
        var entry = new IsoEntry { Name = name };
        Assert.False(HttpService.HasDedicatedResolver(entry));
    }
}

public class HttpServiceApplyResolveOutcomeTests
{
    [Fact]
    public void ApplyResolveOutcome_Success_ResetsStreakToZero()
    {
        var entry = new IsoEntry { Name = "Shadowfetch Linux", FailedResolveStreak = 2 };
        HttpService.ApplyResolveOutcome(entry, succeeded: true);
        Assert.Equal(0, entry.FailedResolveStreak);
    }

    [Fact]
    public void ApplyResolveOutcome_FailureWithoutDedicatedResolver_IncrementsStreak()
    {
        var entry = new IsoEntry { Name = "Shadowfetch Linux", FailedResolveStreak = 2 };
        HttpService.ApplyResolveOutcome(entry, succeeded: false);
        Assert.Equal(3, entry.FailedResolveStreak);
    }

    [Fact]
    public void ApplyResolveOutcome_FailureWithDedicatedResolver_DoesNotIncrementStreak()
    {
        // Regression: ein transienter Netzwerk-Hänger bei einer fest unterstützten Distro (Ubuntu)
        // darf NICHT als "Härtefall" gezählt werden — dafür existiert ein funktionierender Resolver.
        var entry = new IsoEntry { Name = "Ubuntu 26.04 Desktop", FailedResolveStreak = 0 };
        HttpService.ApplyResolveOutcome(entry, succeeded: false);
        Assert.Equal(0, entry.FailedResolveStreak);
    }
}

public class HttpServiceChecksumParserTests
{
    // Hinweis: die Fixture-Hashes im Task-Brief waren 63/62 Hex-Zeichen statt der für SHA-256
    // erforderlichen 64 (Tippfehler in der Vorlage) — hier auf gültige 64-Zeichen-Hex-Strings
    // aufgefüllt (Trailing-Nullen), sonst kann der {64}-Regex im Parser sie nie matchen.
    private const string Sha256SumsFixture =
        "d34e2b30b9a3a34532e51b1f3f4a1f6e2b6f7c8a1b2c3d4e5f60718293a4b5c0  ubuntu-24.04-desktop-amd64.iso\n" +
        "1a2b3c4d5e6f7089a0b1c2d3e4f506172839405162738495061728394a5b6c00  ubuntu-24.04-live-server-amd64.iso\n";

    [Fact]
    public void ParseSha256SumsLine_FindsMatchingFilename()
        => Assert.Equal("d34e2b30b9a3a34532e51b1f3f4a1f6e2b6f7c8a1b2c3d4e5f60718293a4b5c0",
            HttpService.ParseSha256SumsLine(Sha256SumsFixture, "ubuntu-24.04-desktop-amd64.iso"));

    [Fact]
    public void ParseSha256SumsLine_UnknownFilename_ReturnsNull()
        => Assert.Null(HttpService.ParseSha256SumsLine(Sha256SumsFixture, "does-not-exist.iso"));

    [Fact]
    public void ParseSha256SumsLine_EmptyContent_ReturnsNull()
        => Assert.Null(HttpService.ParseSha256SumsLine(string.Empty, "ubuntu-24.04-desktop-amd64.iso"));

    private const string BsdStyleFixture =
        "SHA256 (Fedora-Workstation-Live-42-1.7.x86_64.iso) = 9f8e7d6c5b4a392817263544536271809f8e7d6c5b4a39281726354453627100\n";

    [Fact]
    public void ParseBsdStyleChecksum_FindsMatchingFilename()
        => Assert.Equal("9f8e7d6c5b4a392817263544536271809f8e7d6c5b4a39281726354453627100",
            HttpService.ParseBsdStyleChecksum(BsdStyleFixture, "Fedora-Workstation-Live-42-1.7.x86_64.iso"));

    [Fact]
    public void ParseBsdStyleChecksum_UnknownFilename_ReturnsNull()
        => Assert.Null(HttpService.ParseBsdStyleChecksum(BsdStyleFixture, "does-not-exist.iso"));
}

public class HttpServiceMatchUlmReleaseAssetsTests
{
    [Fact]
    public void MatchUlmReleaseAssets_SeparatesPortableAndSetup()
    {
        var assets = new List<(string, string)>
        {
            ("UniversalLinuxManager-v2.34.0-win-x64.exe",       "https://x/portable.exe"),
            ("UniversalLinuxManager-Setup-v2.34.0-win-x64.exe", "https://x/setup.exe"),
            ("UniversalLinuxManager-v2.34.0-win-x64.zip",       "https://x/ignore.zip"),
        };
        var (portable, setup) = HttpService.MatchUlmReleaseAssets(assets);
        Assert.Equal("https://x/portable.exe", portable);
        Assert.Equal("https://x/setup.exe",    setup);
    }

    [Fact]
    public void MatchUlmReleaseAssets_MissingSetup_LeavesSetupEmpty()
    {
        var assets = new List<(string, string)>
        { ("UniversalLinuxManager-v2.34.0-win-x64.exe", "https://x/portable.exe") };
        var (portable, setup) = HttpService.MatchUlmReleaseAssets(assets);
        Assert.Equal("https://x/portable.exe", portable);
        Assert.Equal("", setup);
    }
}
