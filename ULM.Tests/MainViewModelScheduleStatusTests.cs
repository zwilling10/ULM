using System;
using ULM.ViewModels;
using Xunit;

namespace ULM.Tests;

/// <summary>
/// FormatNextAutoCheckText ist die reine Formatierungslogik hinter dem "Status"-Reiter
/// (Abschnitt "Geplante automatische Aktionen") — zeigt, wann der nächste automatische
/// Online-Versionscheck fällig ist, ohne dass der Nutzer dafür den Task-Manager oder das
/// Protokoll durchsuchen muss.
/// </summary>
public class MainViewModelScheduleStatusTests
{
    private static readonly DateTime Now = new(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NoPriorCheck_ReturnsUnknown()
    {
        string text = MainViewModel.FormatNextAutoCheckText(null, intervalDays: 3, Now);
        Assert.Equal("unbekannt (noch kein Check gelaufen)", text);
    }

    [Fact]
    public void CheckDueInFuture_ShowsRemainingDays()
    {
        DateTime last = Now.AddDays(-1); // vor 1 Tag, Intervall 3 Tage -> noch 2 Tage
        string text = MainViewModel.FormatNextAutoCheckText(last, intervalDays: 3, Now);
        Assert.Equal("in ca. 2 Tag(en)", text);
    }

    [Fact]
    public void CheckOverdue_ReportsDue()
    {
        DateTime last = Now.AddDays(-5); // vor 5 Tagen, Intervall 3 Tage -> überfällig
        string text = MainViewModel.FormatNextAutoCheckText(last, intervalDays: 3, Now);
        Assert.Equal("jetzt fällig", text);
    }
}
