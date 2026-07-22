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

public class SelfUpdateServiceBuildApplyScriptTests
{
    [Fact]
    public void BuildApplyScript_ContainsProcessIdAndBothPaths()
    {
        string script = SelfUpdateService.BuildApplyScript(4242, @"C:\Temp\ULM_Update\new.exe", @"C:\Tools\ULM\UniversalLinuxManager.exe");

        Assert.Contains("-Id 4242", script);
        Assert.Contains(@"C:\Temp\ULM_Update\new.exe", script);
        Assert.Contains(@"C:\Tools\ULM\UniversalLinuxManager.exe", script);
        Assert.Contains("Start-Process", script);
    }

    [Fact]
    public void BuildApplyScript_PathWithApostrophe_EscapesForPowerShell()
    {
        string script = SelfUpdateService.BuildApplyScript(4242, @"C:\Users\O'Brien\new.exe", @"C:\Users\O'Brien\UniversalLinuxManager.exe");

        Assert.Contains("O''Brien", script);
        Assert.DoesNotContain("O'Brien\\new.exe'", script);
    }
}

public class SelfUpdateServiceBuildRestartAfterInstallScriptTests
{
    [Fact]
    public void BuildRestartAfterInstallScript_ContainsSetupProcessIdAndTargetPath()
    {
        string script = SelfUpdateService.BuildRestartAfterInstallScript(4242, @"C:\Tools\ULM\UniversalLinuxManager.exe");

        Assert.Contains("-Id 4242", script);
        Assert.Contains(@"C:\Tools\ULM\UniversalLinuxManager.exe", script);
        Assert.Contains("Start-Process", script);
        Assert.DoesNotContain("Copy-Item", script);
    }

    [Fact]
    public void BuildRestartAfterInstallScript_PathWithApostrophe_EscapesForPowerShell()
    {
        string script = SelfUpdateService.BuildRestartAfterInstallScript(4242, @"C:\Users\O'Brien\UniversalLinuxManager.exe");

        Assert.Contains("O''Brien", script);
    }
}
