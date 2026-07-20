// ULM.Tests/MainViewModelUpdateBannerTests.cs
using ULM.Core.Services;
using ULM.ViewModels;
using System.Windows.Threading;
using Xunit;

namespace ULM.Tests;

public class MainViewModelUpdateBannerTests
{
    private static MainViewModel NewVm() =>
        new(Dispatcher.CurrentDispatcher, new FakeHttpService(), new FakeUsbService(), new FakeIsoDatabaseService());

    [Fact]
    public void SetAvailableUpdate_SetsAvailableStateAndEnabledButton()
    {
        var vm = NewVm();
        var info = new UlmUpdateInfo(true, "3.0.0", "https://example.test", "https://example.test/p.exe", "https://example.test/s.exe");

        vm.SetAvailableUpdate(info);

        Assert.Equal(UpdateBannerState.Available, vm.UpdateBannerState);
        Assert.True(vm.UpdateBannerVisible);
        Assert.True(vm.UpdateBannerButtonEnabled);
    }

    [Fact]
    public void SetUpdateDownloading_SetsDownloadingStateAndDisablesButton()
    {
        var vm = NewVm();
        vm.SetAvailableUpdate(new UlmUpdateInfo(true, "3.0.0", "https://example.test", "https://example.test/p.exe", "https://example.test/s.exe"));

        vm.SetUpdateDownloading();

        Assert.Equal(UpdateBannerState.Downloading, vm.UpdateBannerState);
        Assert.False(vm.UpdateBannerButtonEnabled);
    }

    [Fact]
    public void SetUpdateReadyToInstall_SetsReadyStateAndStoresPath()
    {
        var vm = NewVm();
        vm.SetAvailableUpdate(new UlmUpdateInfo(true, "3.0.0", "https://example.test", "https://example.test/p.exe", "https://example.test/s.exe"));
        vm.SetUpdateDownloading();

        vm.SetUpdateReadyToInstall(@"C:\Temp\ULM_Update\new.exe");

        Assert.Equal(UpdateBannerState.ReadyToInstall, vm.UpdateBannerState);
        Assert.True(vm.UpdateBannerButtonEnabled);
        Assert.Equal(@"C:\Temp\ULM_Update\new.exe", vm.DownloadedUpdatePath);
    }
}
