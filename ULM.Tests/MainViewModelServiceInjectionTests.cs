// ULM.Tests/MainViewModelServiceInjectionTests.cs
using System.Collections.Generic;
using System.Windows.Threading;
using ULM.Core.Models;
using ULM.ViewModels;
using Xunit;

namespace ULM.Tests;

// Hinweis: MainViewModel liest/schreibt im Konstruktor Einstellungen über AppPaths.Instance.SettingsIni
// (ulm_settings.ini neben der Test-Assembly, nicht die echte App-Konfiguration — AppPaths leitet den
// Pfad von AppContext.BaseDirectory ab, das im Testlauf auf ULM.Tests/bin/... zeigt). Das ist
// bestehendes, unverändertes Verhalten des Konstruktors; hier nur dokumentiert, nicht behoben.
public class MainViewModelServiceInjectionTests
{
    [Fact]
    public void GitHubToken_Set_ForwardsToInjectedHttpService()
    {
        var fakeHttp = new FakeHttpService();
        var vm = new MainViewModel(Dispatcher.CurrentDispatcher, fakeHttp, new FakeUsbService(), new FakeIsoDatabaseService());

        vm.GitHubToken = "abc123";

        Assert.Equal("abc123", fakeHttp.GitHubToken);
    }

    [Fact]
    public void RefreshDrives_UsesInjectedUsbService_NotRealHardware()
    {
        var fakeUsb = new FakeUsbService { DrivesToReturn = new List<UsbDrive> { new("Z:", "TestStick", 32_000_000_000, "NTFS") } };
        var vm = new MainViewModel(Dispatcher.CurrentDispatcher, new FakeHttpService(), fakeUsb, new FakeIsoDatabaseService());

        vm.RefreshDrives();

        Assert.Single(vm.Drives);
        Assert.Equal("Z:", vm.Drives[0].Letter);
    }
}
