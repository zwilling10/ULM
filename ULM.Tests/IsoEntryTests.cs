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
    public void AllDownloadUrls_NormalizesPinnedSourceForgeMirror()
    {
        var entry = new IsoEntry
        {
            Url = "https://altushost-bul.dl.sourceforge.net/project/equestria-os/equestria-os-2026.07.08-x86_64.iso?viasf=1&fid=300117e26ccce9ea&e=1784121291&st=SN49t1Ny8cGNVkTxslzIgw",
        };
        string result = Assert.Single(entry.AllDownloadUrls());
        Assert.Equal("https://master.dl.sourceforge.net/project/equestria-os/equestria-os-2026.07.08-x86_64.iso?viasf=1", result);
    }
}
