using ULM.Infrastructure;
using Xunit;

namespace ULM.Tests;

public class AutostartServiceIsSameExecutableTests
{
    [Theory]
    [InlineData(@"C:\Tools\UniversalLinuxManager.exe", @"C:\Tools\UniversalLinuxManager.exe", true)]
    [InlineData(@"""C:\Tools\UniversalLinuxManager.exe""", @"C:\Tools\UniversalLinuxManager.exe", true)] // Registry-Wert in Anführungszeichen
    [InlineData(@"C:\Tools\UniversalLinuxManager.exe", @"C:\Tools\universallinuxmanager.exe", true)]     // Windows-Pfade: case-insensitive
    [InlineData(@"C:\Old\UniversalLinuxManager.exe", @"C:\Tools\UniversalLinuxManager.exe", false)]      // EXE wurde verschoben
    [InlineData(null, @"C:\Tools\UniversalLinuxManager.exe", false)]
    [InlineData("", @"C:\Tools\UniversalLinuxManager.exe", false)]
    [InlineData(@"C:\Tools\UniversalLinuxManager.exe", null, false)]
    public void IsSameExecutable_ComparesNormalizedPaths(string? registryValue, string? currentExePath, bool expected)
        => Assert.Equal(expected, AutostartService.IsSameExecutable(registryValue, currentExePath));
}
