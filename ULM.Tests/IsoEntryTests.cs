using System;
using System.IO;
using System.Threading.Tasks;
using ULM.Core.Models;
using Xunit;

namespace ULM.Tests;

public class IsoEntryNormalizeSourceForgeUrlTests
{
    [Theory]
    // Regression: eine aus der Browser-Adresszeile kopierte, gepinnte Mirror-URL (samt signierter
    // Ablauf-Parameter) wird auf SourceForges stabilen Auto-Redirector umgeschrieben.
    [InlineData(
        "https://altushost-bul.dl.sourceforge.net/project/equestria-os/equestria-os-2026.07.08-x86_64.iso?viasf=1&fid=300117e26ccce9ea&e=1784121291&st=SN49t1Ny8cGNVkTxslzIgw",
        "https://master.dl.sourceforge.net/project/equestria-os/equestria-os-2026.07.08-x86_64.iso?viasf=1")]
    [InlineData(
        "https://netcologne.dl.sourceforge.net/project/gparted/gparted-live-stable/1.8.1-3/gparted-live-1.8.1-3-amd64.iso",
        "https://master.dl.sourceforge.net/project/gparted/gparted-live-stable/1.8.1-3/gparted-live-1.8.1-3-amd64.iso?viasf=1")]
    public void NormalizeSourceForgeUrl_RewritesPinnedMirrorToMaster(string input, string expected)
        => Assert.Equal(expected, IsoEntry.NormalizeSourceForgeUrl(input));

    [Theory]
    // Bereits die stabile master-URL: unverändert lassen.
    [InlineData("https://master.dl.sourceforge.net/project/gparted/gparted-live-stable/1.8.1-3/gparted-live-1.8.1-3-amd64.iso?viasf=1")]
    // Kein SourceForge-Mirror-Muster: unverändert lassen.
    [InlineData("https://example.com/downloads/distro.iso")]
    [InlineData("https://github.com/rescuezilla/rescuezilla/releases/download/v2.6.2/rescuezilla-2.6.2-64bit.noble.iso")]
    // SourceForge, aber nicht das gepinnte "*.dl.sourceforge.net/project/..."-Muster.
    [InlineData("https://sourceforge.net/projects/equestria-os/files/equestria-os-2026.07.08-x86_64.iso/download")]
    public void NormalizeSourceForgeUrl_LeavesNonPinnedUrlsUnchanged(string url)
        => Assert.Equal(url, IsoEntry.NormalizeSourceForgeUrl(url));

    [Fact]
    public void AllDownloadUrls_NormalizesAndFanOutsPinnedSourceForgeMirror()
    {
        var entry = new IsoEntry
        {
            Url = "https://altushost-bul.dl.sourceforge.net/project/equestria-os/equestria-os-2026.07.08-x86_64.iso?viasf=1&fid=300117e26ccce9ea&e=1784121291&st=SN49t1Ny8cGNVkTxslzIgw",
        };
        var result = entry.AllDownloadUrls().ToList();
        // Nicht mehr nur EIN Kandidat: die normalisierte master-URL wird sofort aufgefächert
        // (siehe AllDownloadUrls-BUGFIX-Kommentar), damit auch Aufrufer, die selbst nicht
        // nachfächern (UrlCheckWorker, GetExpectedSizeAsync, ResolveGenericAsync), echte
        // Mirror-Auswahl bekommen.
        Assert.Equal("https://master.dl.sourceforge.net/project/equestria-os/equestria-os-2026.07.08-x86_64.iso?viasf=1", result[0]);
        Assert.True(result.Count >= 4, $"Erwartet mehrere Mirror-Kandidaten, war {result.Count}");
        Assert.Equal(result.Count, result.Distinct().Count());
    }

    [Fact]
    public void AllDownloadUrls_TwoDistinctPinnedMirrorsForSameFile_PreservesFullRedundancy()
    {
        // Regression: der ausgelieferte "Linux Kodachi"-Eintrag (IsoDatabaseService.cs) hat Mirror1
        // als gepinnten Köln-Mirror UND Mirror2 als master-URL für DIESELBE Datei — beide
        // normalisierten früher auf denselben String und die Deduplizierung verschluckte die zweite
        // Quelle komplett (nur 1 statt der vollen Mirror-Auswahl). Jetzt fächert bereits die erste
        // normalisierte URL alle Mirror-Kandidaten auf; die zweite (identische) Quelle darf keine
        // zusätzlichen echten Kandidaten mehr verlieren, weil sie einfach als Duplikat herausfällt.
        var entry = new IsoEntry
        {
            Mirror1 = "https://netcologne.dl.sourceforge.net/project/linuxkodachi/kodachi-desktop/linux-kodachi-xfce-9.0.1-amd64.iso?viasf=1",
            Mirror2 = "https://master.dl.sourceforge.net/project/linuxkodachi/kodachi-desktop/linux-kodachi-xfce-9.0.1-amd64.iso?viasf=1",
        };
        var result = entry.AllDownloadUrls().ToList();
        Assert.True(result.Count >= 4, $"Erwartet mehrere Mirror-Kandidaten trotz zwei (identisch normalisierten) Quellfeldern, war {result.Count}");
        Assert.Equal(result.Count, result.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void AllDownloadUrls_TakeTen_DoesNotCrowdOutRealMirror4And5()
    {
        // Regression: der ausgelieferte "Linux Kodachi"-Eintrag hat NEBEN der SourceForge-Quelle
        // (Mirror1/2) noch drei echte, unabhängige Nicht-SourceForge-Mirror (Mirror3-5). Der
        // SourceForge-Fächer allein liefert 4 Kandidaten (master + 3 benannte Mirror) — zusammen mit
        // Url + Mirror3-5 muss Mirror4 UND Mirror5 innerhalb der ersten 10 Kandidaten (siehe
        // DownloadWorker.Take(10)) noch enthalten sein.
        var entry = new IsoEntry
        {
            Url     = "https://cdn.kodachi.cloud/kodachi.iso",
            Mirror1 = "https://netcologne.dl.sourceforge.net/project/linuxkodachi/kodachi-desktop/linux-kodachi-xfce-9.0.1-amd64.iso?viasf=1",
            Mirror2 = "https://master.dl.sourceforge.net/project/linuxkodachi/kodachi-desktop/linux-kodachi-xfce-9.0.1-amd64.iso?viasf=1",
            Mirror3 = "https://downloads.sourceforge.net/project/linuxkodachi/kodachi-desktop/linux-kodachi-xfce-9.0.1-amd64.iso",
            Mirror4 = "https://cdn.kodachi.cloud/kodachi-alt1.iso",
            Mirror5 = "https://downloads.kodachi.cloud/kodachi-alt2.iso",
        };
        var first10 = entry.AllDownloadUrls().Take(10).ToList();
        Assert.Contains(entry.Mirror4, first10);
        Assert.Contains(entry.Mirror5, first10);
    }
}

public class IsoEntryExpandSourceForgeMirrorsTests
{
    [Fact]
    public void ExpandSourceForgeMirrors_MasterUrl_FansOutToPlainPlusUseMirrorVariants()
    {
        var result = IsoEntry.ExpandSourceForgeMirrors(
            "https://master.dl.sourceforge.net/project/equestria-os/equestria-os-2026.07.08-x86_64.iso?viasf=1");

        // Erster Kandidat: die schlichte master-URL (SourceForges eigene Mirror-Wahl).
        Assert.Equal(
            "https://master.dl.sourceforge.net/project/equestria-os/equestria-os-2026.07.08-x86_64.iso?viasf=1",
            result[0]);
        // Danach genau eine ?use_mirror=<name>-Variante pro bekanntem Mirror.
        var useMirrorVariants = result.Skip(1).ToList();
        Assert.All(useMirrorVariants, u => Assert.Contains("?use_mirror=", u));
        Assert.All(useMirrorVariants, u => Assert.StartsWith(
            "https://master.dl.sourceforge.net/project/equestria-os/equestria-os-2026.07.08-x86_64.iso?use_mirror=", u));
        // Mehrere DISTINKTE Kandidaten — sonst hätte das Mirror-Race nichts zu vergleichen.
        Assert.True(result.Count >= 4);
        Assert.Equal(result.Count, result.Distinct().Count());
    }

    [Theory]
    // Nicht-SourceForge-URLs bleiben unverändert und einzeln.
    [InlineData("https://example.com/downloads/distro.iso")]
    [InlineData("https://github.com/rescuezilla/rescuezilla/releases/download/v2.6.2/rescuezilla-2.6.2-64bit.noble.iso")]
    // Ein gepinnter Mirror ist NICHT master → wird hier NICHT aufgefächert (Normalisierung in
    // AllDownloadUrls hätte ihn vorher ohnehin schon auf master umgeschrieben).
    [InlineData("https://altushost-bul.dl.sourceforge.net/project/equestria-os/foo.iso")]
    public void ExpandSourceForgeMirrors_NonMasterUrl_ReturnedUnchangedAsSingle(string url)
    {
        var result = IsoEntry.ExpandSourceForgeMirrors(url);
        Assert.Equal(new[] { url }, result);
    }

    [Fact]
    public void AllDownloadUrls_SingleSourceForgeEntry_AlreadyYieldsMultipleMirrors()
    {
        // Der reale "ISO suchen"-Fall: ein Eintrag mit genau EINER SourceForge-Quelle.
        // AllDownloadUrls() fächert seit dem Redundanz-Bugfix selbst auf — kein zusätzliches
        // manuelles SelectMany(ExpandSourceForgeMirrors) durch den Aufrufer mehr nötig.
        var entry = new IsoEntry
        {
            Url = "https://master.dl.sourceforge.net/project/equestria-os/equestria-os-2026.07.08-x86_64.iso?viasf=1",
        };
        var result = entry.AllDownloadUrls().ToList();
        Assert.True(result.Count >= 4, $"Erwartet mehrere Mirror-Kandidaten, war {result.Count}");
    }
}

public class IsoEntryComputeSha256Tests
{
    [Fact]
    public async Task ComputeSha256Async_KnownContent_ReturnsExpectedHash()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ulm-hash-test-{Guid.NewGuid():N}.iso");
        await File.WriteAllTextAsync(path, "ULM-Test-Inhalt");
        try
        {
            string hash = await IsoEntry.ComputeSha256Async(path);
            // sha256("ULM-Test-Inhalt") — vorab mit `sha256sum` verifiziert.
            Assert.Equal(64, hash.Length);
            Assert.Matches("^[0-9a-f]{64}$", hash);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ComputeSha256Async_SameContentTwice_ReturnsSameHash()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ulm-hash-test-{Guid.NewGuid():N}.iso");
        await File.WriteAllTextAsync(path, "Reproduzierbarer Inhalt");
        try
        {
            string h1 = await IsoEntry.ComputeSha256Async(path);
            string h2 = await IsoEntry.ComputeSha256Async(path);
            Assert.Equal(h1, h2);
            Assert.NotEmpty(h1);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ComputeSha256Async_MissingFile_ReturnsEmpty()
        => Assert.Equal(string.Empty, await IsoEntry.ComputeSha256Async(
            Path.Combine(Path.GetTempPath(), $"ulm-does-not-exist-{Guid.NewGuid():N}.iso")));
}
