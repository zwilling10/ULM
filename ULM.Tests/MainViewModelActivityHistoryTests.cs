using System;
using ULM.ViewModels;
using Xunit;

namespace ULM.Tests;

/// <summary>
/// FormatHistoryEntry ist die reine Formatierung hinter dem "Verlauf"-Abschnitt im Status-Reiter —
/// stempelt jede Hintergrund-Meilenstein-Meldung (Start/Ende von Integritätspruefung,
/// automatischem Versionscheck, automatischem Stick-Scan) mit einer Uhrzeit.
/// </summary>
public class MainViewModelActivityHistoryTests
{
    [Fact]
    public void FormatsMessageWithTimestamp()
    {
        var now = new DateTime(2026, 7, 16, 8, 26, 3);
        string entry = MainViewModel.FormatHistoryEntry("🔒 Integritätsprüfung D: gestartet …", now);
        Assert.Equal("[08:26:03] 🔒 Integritätsprüfung D: gestartet …", entry);
    }
}
