// ULM.Tests/MainViewModelVentoyCooldownTests.cs
using System;
using ULM.ViewModels;
using Xunit;

namespace ULM.Tests
{
    /// <summary>
    /// Testet MainViewModel.IsLikelySameStick — den reinen Vergleichskern hinter der
    /// Ventoy-Cooldown-Härtung (siehe MainViewModel.StartVentoyInstall /
    /// Views/MainWindow.xaml.cs OnNewDriveInserted).
    ///
    /// Live gefunden (2026-08-17): Ventoy2Disk partitioniert die gesamte physische Platte neu,
    /// wonach Windows oft einen ANDEREN Laufwerksbuchstaben vergibt (z.B. F: → E:). ULM erkennt den
    /// neu auftauchenden Buchstaben dadurch als "brandneuer Stick" und zeigt trotz gerade erst
    /// erfolgreich abgeschlossener eigener Installation erneut den destruktiven
    /// "ALLE DATEN WERDEN GELÖSCHT"-Dialog — sowohl automatisch beim Buchstaben-Wechsel als auch,
    /// wenn der Nutzer (durch eine kurzzeitig falsche "Kein Ventoy"-Statusanzeige verunsichert) den
    /// Stick manuell neu steckt. IsLikelySameStick erkennt diesen Fall heuristisch: ein neu
    /// auftauchender Stick kurz nach einer eigenen erfolgreichen Ventoy-Installation UND mit
    /// ähnlicher Größe gilt als derselbe physische Stick, kein destruktives Angebot nötig.
    /// </summary>
    public class MainViewModelVentoyCooldownTests
    {
        private static readonly DateTime Now = new(2026, 8, 17, 16, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(2);

        [Fact]
        public void IsLikelySameStick_SameSizeShortlyAfter_ReturnsTrue()
        {
            bool result = MainViewModel.IsLikelySameStick(
                previousSizeBytes: 114_600_000_000, previousCompletedAtUtc: Now.AddSeconds(-30),
                newSizeBytes: 114_600_000_000, nowUtc: Now, cooldown: Cooldown);
            Assert.True(result);
        }

        [Fact]
        public void IsLikelySameStick_SlightlyDifferentSize_WithinTolerance_ReturnsTrue()
        {
            // Ventoy legt zwei Partitionen an (exFAT + VTOYEFI) statt der einen NTFS-Partition
            // zuvor -- die von Windows gemeldete Gesamtgröße kann sich dadurch geringfügig
            // unterscheiden, ohne dass es ein anderer physischer Stick ist.
            bool result = MainViewModel.IsLikelySameStick(
                previousSizeBytes: 114_600_000_000, previousCompletedAtUtc: Now.AddSeconds(-30),
                newSizeBytes: 114_650_000_000, nowUtc: Now, cooldown: Cooldown);
            Assert.True(result);
        }

        [Fact]
        public void IsLikelySameStick_VeryDifferentSize_ReturnsFalse()
        {
            // Ein tatsächlich anderer, neu gesteckter Stick (z.B. 32 GB statt 115 GB) darf
            // weiterhin ganz normal den Einrichtungs-Dialog bekommen.
            bool result = MainViewModel.IsLikelySameStick(
                previousSizeBytes: 114_600_000_000, previousCompletedAtUtc: Now.AddSeconds(-30),
                newSizeBytes: 32_000_000_000, nowUtc: Now, cooldown: Cooldown);
            Assert.False(result);
        }

        [Fact]
        public void IsLikelySameStick_OutsideCooldownWindow_ReturnsFalse()
        {
            bool result = MainViewModel.IsLikelySameStick(
                previousSizeBytes: 114_600_000_000, previousCompletedAtUtc: Now.AddMinutes(-5),
                newSizeBytes: 114_600_000_000, nowUtc: Now, cooldown: Cooldown);
            Assert.False(result);
        }

        [Fact]
        public void IsLikelySameStick_ZeroOrNegativeSize_ReturnsFalse()
        {
            Assert.False(MainViewModel.IsLikelySameStick(0, Now.AddSeconds(-10), 114_600_000_000, Now, Cooldown));
            Assert.False(MainViewModel.IsLikelySameStick(114_600_000_000, Now.AddSeconds(-10), 0, Now, Cooldown));
        }

        [Fact]
        public void IsLikelySameStick_ExactlyAtCooldownBoundary_ReturnsFalse()
        {
            // Grenzfall bewusst exklusiv geprüft (>= statt >) -- knapp außerhalb des Fensters soll
            // eindeutig als "kein Zusammenhang mehr" gelten.
            bool result = MainViewModel.IsLikelySameStick(
                previousSizeBytes: 114_600_000_000, previousCompletedAtUtc: Now - Cooldown,
                newSizeBytes: 114_600_000_000, nowUtc: Now, cooldown: Cooldown);
            Assert.False(result);
        }
    }
}
