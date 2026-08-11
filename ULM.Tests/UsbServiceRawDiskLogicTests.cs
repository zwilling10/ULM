// ULM.Tests/UsbServiceRawDiskLogicTests.cs
using System.Collections.Generic;
using ULM.Core.Services;
using Xunit;

namespace ULM.Tests
{
    public class UsbServiceRawDiskLogicTests
    {
        [Fact]
        public void IsSafeToPrepare_DifferentFromSystemDisk_ReturnsTrue()
        {
            Assert.True(UsbService.IsSafeToPrepare(diskIndex: 1, systemDiskIndex: 0));
        }

        [Fact]
        public void IsSafeToPrepare_SameAsSystemDisk_ReturnsFalse()
        {
            Assert.False(UsbService.IsSafeToPrepare(diskIndex: 0, systemDiskIndex: 0));
        }

        [Fact]
        public void IsSafeToPrepare_UnknownSystemDiskIndex_FailsClosed()
        {
            // null = Systemdatenträger-Index konnte nicht ermittelt werden — MUSS als unsicher
            // gelten (fail-closed), nicht als "kein Konflikt gefunden also sicher" (fail-open).
            Assert.False(UsbService.IsSafeToPrepare(diskIndex: 1, systemDiskIndex: null));
        }

        [Fact]
        public void FindNewRawDiskIndices_NewIndexAppears_ReturnsOnlyTheNewOne()
        {
            var previous = new List<int> { 1, 2 };
            var current  = new List<int> { 1, 2, 3 };
            var result = UsbService.FindNewRawDiskIndices(previous, current);
            Assert.Equal(new[] { 3 }, result);
        }

        [Fact]
        public void FindNewRawDiskIndices_NothingChanged_ReturnsEmpty()
        {
            var previous = new List<int> { 1, 2 };
            var current  = new List<int> { 1, 2 };
            var result = UsbService.FindNewRawDiskIndices(previous, current);
            Assert.Empty(result);
        }

        [Fact]
        public void FindNewRawDiskIndices_DiskRemoved_ReturnsEmptyNotNegative()
        {
            var previous = new List<int> { 1, 2, 3 };
            var current  = new List<int> { 1 };
            var result = UsbService.FindNewRawDiskIndices(previous, current);
            Assert.Empty(result);
        }

        [Fact]
        public void FindFreeDriveLetter_SkipsUsedLetters_ReturnsFirstFreeFromD()
        {
            var used = new[] { 'C', 'D', 'E' };
            char? result = UsbService.FindFreeDriveLetter(used);
            Assert.Equal('F', result);
        }

        [Fact]
        public void FindFreeDriveLetter_AllLettersUsed_ReturnsNull()
        {
            var used = new List<char>();
            for (char c = 'A'; c <= 'Z'; c++) used.Add(c);
            char? result = UsbService.FindFreeDriveLetter(used);
            Assert.Null(result);
        }
    }
}
