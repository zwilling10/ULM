// ULM.Tests/UsbServiceVentoyDetectionTests.cs
using System.Collections.Generic;
using ULM.Core.Services;
using Xunit;

namespace ULM.Tests
{
    /// <summary>
    /// Testet UsbService.RetryUntilTrue — den reinen Wiederholungs-Kern hinter
    /// IsVentoyInstalledWithRetry (siehe UsbService.cs). Live gefunden (2026-08-17): ein bereits
    /// vollständig eingerichteter Ventoy-Stick (Ventoy-Partition + VTOYEFI, in der
    /// Datenträgerverwaltung bestätigt) löste trotzdem den destruktiven "ALLE DATEN WERDEN
    /// GELÖSCHT"-Dialog aus — vermuteter Grund: Directory.Exists() auf den frisch gemounteten
    /// Stick unmittelbar nach dem Einstecken kann kurz VOR dem tatsächlichen Mount-Abschluss
    /// laufen und fälschlich "false" liefern. Da ein falsches "false" hier zu einem destruktiven
    /// Angebot führt, wird bis zu dreimal mit kurzer Pause geprüft, bevor "nicht installiert" als
    /// endgültig gilt — RetryUntilTrue ist der injizierbare, testbare Kern davon (echtes
    /// Directory.Exists + Thread.Sleep sind ohne echte USB-Hardware nicht sinnvoll automatisiert
    /// testbar, siehe restliche UsbService-Tests).
    /// </summary>
    public class UsbServiceVentoyDetectionTests
    {
        [Fact]
        public void RetryUntilTrue_SucceedsFirstAttempt_ReturnsTrueWithoutDelay()
        {
            int delayCalls = 0;
            bool result = UsbService.RetryUntilTrue(() => true, attempts: 3, delay: _ => delayCalls++);
            Assert.True(result);
            Assert.Equal(0, delayCalls);
        }

        [Fact]
        public void RetryUntilTrue_SucceedsOnThirdAttempt_ReturnsTrueAfterTwoDelays()
        {
            var results = new Queue<bool>(new[] { false, false, true });
            int delayCalls = 0;
            bool result = UsbService.RetryUntilTrue(() => results.Dequeue(), attempts: 3, delay: _ => delayCalls++);
            Assert.True(result);
            Assert.Equal(2, delayCalls);
        }

        [Fact]
        public void RetryUntilTrue_AllAttemptsFail_ReturnsFalseWithoutTrailingDelay()
        {
            int delayCalls = 0;
            bool result = UsbService.RetryUntilTrue(() => false, attempts: 3, delay: _ => delayCalls++);
            Assert.False(result);
            // Nach dem LETZTEN fehlgeschlagenen Versuch wird nicht mehr gewartet — sinnlose
            // Verzögerung kurz bevor das endgültige "false" ohnehin zurückgegeben wird.
            Assert.Equal(2, delayCalls);
        }

        [Fact]
        public void RetryUntilTrue_SingleAttempt_NeverDelays()
        {
            int delayCalls = 0;
            bool result = UsbService.RetryUntilTrue(() => false, attempts: 1, delay: _ => delayCalls++);
            Assert.False(result);
            Assert.Equal(0, delayCalls);
        }
    }
}
