using ULM.Core.Models;
using ULM.ViewModels;
using Xunit;

namespace ULM.Tests;

public class IsoEntryViewModelShowManualSearchButtonTests
{
    [Fact]
    public void ShowManualSearchButton_False_BelowThreshold()
    {
        var entry = new IsoEntry { Name = "Shadowfetch Linux", FailedResolveStreak = 2 };
        var vm = new IsoEntryViewModel(entry, downloadDir: "");
        Assert.False(vm.ShowManualSearchButton);
    }

    [Fact]
    public void ShowManualSearchButton_True_AtThreshold()
    {
        var entry = new IsoEntry { Name = "Shadowfetch Linux", FailedResolveStreak = 3 };
        var vm = new IsoEntryViewModel(entry, downloadDir: "");
        Assert.True(vm.ShowManualSearchButton);
    }

    [Fact]
    public void ShowManualSearchButton_False_ForWellSupportedDistroWithoutFailures()
    {
        var entry = new IsoEntry { Name = "Ubuntu 26.04 Desktop", FailedResolveStreak = 0 };
        var vm = new IsoEntryViewModel(entry, downloadDir: "");
        Assert.False(vm.ShowManualSearchButton);
    }
}
