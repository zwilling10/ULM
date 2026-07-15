using System;
using ULM.Core.Workers;
using Xunit;

namespace ULM.Tests;

/// <summary>
/// Regression: der "(schneller)"-Button erschien bisher bei JEDEM Mirror-Versuch mit noch
/// übrigen Kandidaten — unabhängig von der tatsächlich gemessenen Geschwindigkeit, sogar bei
/// bereits sehr schnellen Downloads direkt ab Sekunde 0. DownloadWorker.ShouldShowFasterMirrorButton
/// kapselt jetzt die Entscheidung (Anlaufzeit + gemessene Geschwindigkeit unter der Komfort-Schwelle)
/// als reine, testbare Funktion.
/// </summary>
public class DownloadWorkerFasterMirrorButtonTests
{
    private static readonly TimeSpan BeforeWarmup = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan AfterWarmup  = TimeSpan.FromSeconds(25); // WarmupGrace = 20s

    [Fact]
    public void NoMoreMirrors_NeverShowsButton()
        => Assert.False(DownloadWorker.ShouldShowFasterMirrorButton(
            hasMoreMirrors: false, elapsedSinceAttemptStart: AfterWarmup, currentBpsOrNegativeIfUnknown: 100));

    [Fact]
    public void StillWithinWarmupGrace_HidesButtonEvenIfSlow()
        => Assert.False(DownloadWorker.ShouldShowFasterMirrorButton(
            hasMoreMirrors: true, elapsedSinceAttemptStart: BeforeWarmup, currentBpsOrNegativeIfUnknown: 500));

    [Fact]
    public void SpeedNotYetMeasured_HidesButton()
        => Assert.False(DownloadWorker.ShouldShowFasterMirrorButton(
            hasMoreMirrors: true, elapsedSinceAttemptStart: AfterWarmup, currentBpsOrNegativeIfUnknown: -1));

    [Fact]
    public void AfterWarmup_VeryGoodSpeed_HidesButton()
    {
        // Regression-Fall aus dem Bugreport: ~10 MB/s ist klar über der Komfort-Schwelle (~3 MB/s) —
        // der Button darf hier NICHT erscheinen.
        double veryGoodBps = 10 * 1_048_576;
        Assert.False(DownloadWorker.ShouldShowFasterMirrorButton(
            hasMoreMirrors: true, elapsedSinceAttemptStart: AfterWarmup, currentBpsOrNegativeIfUnknown: veryGoodBps));
    }

    [Fact]
    public void AfterWarmup_MediocreSpeed_ShowsButton()
    {
        // Über der Auto-Abbruch-Schwelle (~1 MB/s, sonst würde der Wächter selbst schon abbrechen),
        // aber unter der Komfort-Schwelle (~3 MB/s) — genau der Fall, für den der Button gedacht ist.
        double mediocreBps = 2 * 1_048_576;
        Assert.True(DownloadWorker.ShouldShowFasterMirrorButton(
            hasMoreMirrors: true, elapsedSinceAttemptStart: AfterWarmup, currentBpsOrNegativeIfUnknown: mediocreBps));
    }

    [Fact]
    public void AfterWarmup_SpeedExactlyAtComfortThreshold_HidesButton()
        => Assert.False(DownloadWorker.ShouldShowFasterMirrorButton(
            hasMoreMirrors: true, elapsedSinceAttemptStart: AfterWarmup, currentBpsOrNegativeIfUnknown: 3 * 1_048_576));
}
