// ULM.Tests/MainViewModelStickNotificationTests.cs
using System.Windows.Threading;
using ULM.ViewModels;
using Xunit;

namespace ULM.Tests;

public class MainViewModelStickNotificationTests
{
    private static MainViewModel CreateVm() => new(Dispatcher.CurrentDispatcher);

    [Fact]
    public void MarkNewerVersionOffered_FirstCall_ReturnsTrue()
    {
        var vm = CreateVm();
        Assert.True(vm.MarkNewerVersionOffered("E:", "ubuntu-26.04.iso"));
    }

    [Fact]
    public void MarkNewerVersionOffered_SecondCallSameDriveAndFile_ReturnsFalse()
    {
        var vm = CreateVm();
        vm.MarkNewerVersionOffered("E:", "ubuntu-26.04.iso");
        Assert.False(vm.MarkNewerVersionOffered("E:", "ubuntu-26.04.iso"));
    }

    [Fact]
    public void MarkNewerVersionOffered_SameFileDifferentDrive_ReturnsTrue()
    {
        var vm = CreateVm();
        vm.MarkNewerVersionOffered("E:", "ubuntu-26.04.iso");
        Assert.True(vm.MarkNewerVersionOffered("F:", "ubuntu-26.04.iso"));
    }

    [Fact]
    public void MarkCopyOffered_SecondCallSameKey_ReturnsFalse()
    {
        var vm = CreateVm();
        vm.MarkCopyOffered("E:", "debian-13.iso");
        Assert.False(vm.MarkCopyOffered("E:", "debian-13.iso"));
    }

    [Fact]
    public void MarkUnknownStickIsoOffered_SecondCallSameKey_ReturnsFalse()
    {
        var vm = CreateVm();
        vm.MarkUnknownStickIsoOffered("E:", "mystery.iso");
        Assert.False(vm.MarkUnknownStickIsoOffered("E:", "mystery.iso"));
    }

    [Fact]
    public void MarkIncompleteStickIsoOffered_SecondCallSameKey_ReturnsFalse()
    {
        var vm = CreateVm();
        vm.MarkIncompleteStickIsoOffered("E:", "broken.iso");
        Assert.False(vm.MarkIncompleteStickIsoOffered("E:", "broken.iso"));
    }
}
