// ULM.Tests/MainViewModelServiceInjectionTests.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Threading;
using ULM.Core.Models;
using ULM.Core.Services;
using ULM.ViewModels;
using Xunit;

namespace ULM.Tests;

// Hinweis: MainViewModel liest/schreibt im Konstruktor Einstellungen über AppPaths.Instance.SettingsIni
// (ulm_settings.ini neben der Test-Assembly, nicht die echte App-Konfiguration — AppPaths leitet den
// Pfad von AppContext.BaseDirectory ab, das im Testlauf auf ULM.Tests/bin/... zeigt). Das ist
// bestehendes, unverändertes Verhalten des Konstruktors; hier nur dokumentiert, nicht behoben.
public class MainViewModelServiceInjectionTests
{
    private sealed class FakeHttpService : IHttpService
    {
        public string? GitHubToken { get; set; }
    }

    private sealed class FakeUsbService : IUsbService
    {
        public List<UsbDrive> DrivesToReturn { get; set; } = new();
        public List<UsbDrive> ListRemovableDrives() => DrivesToReturn;
        public Task<(List<UsbService.StickIso> Found, List<UsbService.StickIso> Incomplete)> ScanStickVerifiedAsync(string letter, IReadOnlyList<IsoEntry> entries)
            => Task.FromResult((new List<UsbService.StickIso>(), new List<UsbService.StickIso>()));
    }

    private sealed class FakeIsoDatabaseService : IIsoDatabaseService
    {
        private readonly List<IsoEntry> _entries = new();
        public IReadOnlyList<IsoEntry> Entries => _entries;
        public int Count => _entries.Count;
        public void Load() { }
        public void Save() { }
        public void SaveFilenames() { }
        public void Add(IsoEntry entry) => _entries.Add(entry);
        public void Remove(int index) => _entries.RemoveAt(index);
        public void SaveExpectedSize(IsoEntry entry, long bytes) { }
    }

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
