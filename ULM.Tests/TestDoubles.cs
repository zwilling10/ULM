// ULM.Tests/TestDoubles.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using ULM.Core.Models;
using ULM.Core.Services;

namespace ULM.Tests;

internal sealed class FakeHttpService : IHttpService
{
    public string? GitHubToken { get; set; }
}

internal sealed class FakeUsbService : IUsbService
{
    public List<UsbDrive> DrivesToReturn { get; set; } = new();
    public List<UsbDrive> ListRemovableDrives() => DrivesToReturn;
    public Task<(List<UsbService.StickIso> Found, List<UsbService.StickIso> Incomplete)> ScanStickVerifiedAsync(string letter, IReadOnlyList<IsoEntry> entries)
        => Task.FromResult((new List<UsbService.StickIso>(), new List<UsbService.StickIso>()));
}

internal sealed class FakeIsoDatabaseService : IIsoDatabaseService
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
