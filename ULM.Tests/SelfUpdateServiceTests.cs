using System;
using System.IO;
using ULM.Core.Services;
using Xunit;

namespace ULM.Tests;

public class SelfUpdateServiceDetectInstallKindTests
{
    [Fact]
    public void DetectInstallKind_UninstallerPresent_ReturnsInstalled()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"ulm-selfupdate-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            string exePath = Path.Combine(tempDir, "UniversalLinuxManager.exe");
            File.WriteAllText(Path.Combine(tempDir, "unins000.exe"), string.Empty);

            var kind = SelfUpdateService.Instance.DetectInstallKind(exePath);

            Assert.Equal(InstallKind.Installed, kind);
        }
        finally { Directory.Delete(tempDir, true); }
    }

    [Fact]
    public void DetectInstallKind_NoUninstaller_ReturnsPortable()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"ulm-selfupdate-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            string exePath = Path.Combine(tempDir, "UniversalLinuxManager.exe");

            var kind = SelfUpdateService.Instance.DetectInstallKind(exePath);

            Assert.Equal(InstallKind.Portable, kind);
        }
        finally { Directory.Delete(tempDir, true); }
    }
}

public class SelfUpdateServiceSelectDownloadUrlTests
{
    [Fact]
    public void SelectDownloadUrl_Installed_ReturnsSetupUrl()
    {
        var info = new UlmUpdateInfo(true, "3.0.0", "https://example.test/release",
            "https://example.test/portable.exe", "https://example.test/setup.exe");

        string url = SelfUpdateService.SelectDownloadUrl(info, InstallKind.Installed);

        Assert.Equal("https://example.test/setup.exe", url);
    }

    [Fact]
    public void SelectDownloadUrl_Portable_ReturnsPortableUrl()
    {
        var info = new UlmUpdateInfo(true, "3.0.0", "https://example.test/release",
            "https://example.test/portable.exe", "https://example.test/setup.exe");

        string url = SelfUpdateService.SelectDownloadUrl(info, InstallKind.Portable);

        Assert.Equal("https://example.test/portable.exe", url);
    }
}
