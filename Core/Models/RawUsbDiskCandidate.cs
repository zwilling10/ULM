// Core/Models/RawUsbDiskCandidate.cs
namespace ULM.Core.Models
{
    /// <summary>
    /// Ein physischer USB-Datenträger ohne zugewiesenen Laufwerksbuchstaben (z.B. mit Rufus im
    /// ISO/DD-Modus beschrieben) — erkannt über Win32_DiskDrive statt Win32_LogicalDisk, siehe
    /// UsbService.ListUsbDisksNeedingPreparation().
    /// </summary>
    public sealed record RawUsbDiskCandidate(int DiskIndex, long SizeBytes);
}
