// ULM.Tests/MainViewModelRawUsbDiskTests.cs
using System.Collections.Generic;
using System.Windows.Threading;
using ULM.Core.Models;
using ULM.ViewModels;
using Xunit;

namespace ULM.Tests
{
    public class MainViewModelRawUsbDiskTests
    {
        private static (MainViewModel vm, FakeUsbService usb) Build()
        {
            var usb = new FakeUsbService();
            var vm  = new MainViewModel(Dispatcher.CurrentDispatcher, usb: usb);
            return (vm, usb);
        }

        [Fact]
        public void CheckRawUsbDisks_NewCandidate_FiresRawUsbDiskDetectedEvent()
        {
            var (vm, usb) = Build();
            usb.RawDisksToReturn = new() { new RawUsbDiskCandidate(DiskIndex: 3, SizeBytes: 32_000_000_000) };
            var detected = new List<RawUsbDiskCandidate>();
            vm.RawUsbDiskDetected += c => detected.Add(c);

            vm.CheckRawUsbDisks();

            Assert.Single(detected);
            Assert.Equal(3, detected[0].DiskIndex);
        }

        [Fact]
        public void CheckRawUsbDisks_NewCandidate_DoesNotAutomaticallyPrepare()
        {
            // BUGFIX-Regressionstest: früher rief CheckRawUsbDisks() PrepareRawUsbDisk() sofort
            // und ungefragt auf (löscht die Partitionstabelle) — bevor überhaupt eine Bestätigung
            // erschien. Erkennung und Vorbereitung müssen getrennt sein.
            var (vm, usb) = Build();
            usb.RawDisksToReturn = new() { new RawUsbDiskCandidate(3, 32_000_000_000) };

            vm.CheckRawUsbDisks();

            Assert.Empty(usb.PrepareCalls);
        }

        [Fact]
        public void CheckRawUsbDisks_SameCandidateTwice_EventFiresOnlyOnce()
        {
            var (vm, usb) = Build();
            usb.RawDisksToReturn = new() { new RawUsbDiskCandidate(3, 32_000_000_000) };
            int fireCount = 0;
            vm.RawUsbDiskDetected += _ => fireCount++;

            vm.CheckRawUsbDisks();
            vm.CheckRawUsbDisks();

            Assert.Equal(1, fireCount);
        }

        [Fact]
        public void CheckRawUsbDisks_NoCandidates_EventDoesNotFire()
        {
            var (vm, usb) = Build();
            usb.RawDisksToReturn = new();
            bool fired = false;
            vm.RawUsbDiskDetected += _ => fired = true;

            vm.CheckRawUsbDisks();

            Assert.False(fired);
        }

        [Fact]
        public void PrepareRawUsbDisk_Success_CallsUnderlyingServiceAndReturnsTrue()
        {
            var (vm, usb) = Build();
            usb.PrepareShouldSucceed = true;
            var candidate = new RawUsbDiskCandidate(3, 32_000_000_000);

            bool result = vm.PrepareRawUsbDisk(candidate, 'F');

            Assert.True(result);
            Assert.Single(usb.PrepareCalls);
            Assert.Equal((3, 'F'), usb.PrepareCalls[0]);
        }

        [Fact]
        public void PrepareRawUsbDisk_Failure_ReturnsFalseWithoutThrowing()
        {
            var (vm, usb) = Build();
            usb.PrepareShouldSucceed = false;
            var candidate = new RawUsbDiskCandidate(3, 32_000_000_000);

            var ex = Record.Exception(() => vm.PrepareRawUsbDisk(candidate, 'F'));

            Assert.Null(ex);
        }
    }
}
