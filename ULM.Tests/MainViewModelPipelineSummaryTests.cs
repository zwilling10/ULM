using ULM.ViewModels;
using Xunit;

namespace ULM.Tests;

/// <summary>
/// Regression: die Abschluss-Meldung des kombinierten "Download → Stick-Kopie"-Modus baute sich
/// bisher AUSSCHLIESSLICH aus den Download-Erfolgszahlen zusammen — schlug die anschließende
/// Stick-Kopie fehl, meldete die App trotzdem "X ISO(s) heruntergeladen und kopiert.", obwohl keine
/// einzige davon tatsächlich auf dem Stick landete. BuildPipelineCompletionMessage() verwendet
/// stattdessen die ECHTEN Kopier-Erfolgszahlen (copyOk aus RunPipelineCopyConsumerAsync).
/// </summary>
public class MainViewModelPipelineSummaryTests
{
    [Fact]
    public void AllCopiesSucceeded_ReportsFullSuccessWithoutFailureNote()
    {
        string msg = MainViewModel.BuildPipelineCompletionMessage(copyOk: 8, totalQueued: 8, drive: "E:\\");
        Assert.Contains("8 ISO(s) heruntergeladen und auf E:\\ kopiert.", msg);
        Assert.DoesNotContain("fehlgeschlagen", msg);
    }

    [Fact]
    public void DownloadsOkButAllCopiesFailed_ReportsZeroSuccessNotFullSuccess()
    {
        // Der Bugreport: 8 erfolgreiche Downloads, 0 erfolgreiche Kopien.
        string msg = MainViewModel.BuildPipelineCompletionMessage(copyOk: 0, totalQueued: 8, drive: "E:\\");
        Assert.Equal(string.Empty, msg); // "kein Erfolg" -> Aufrufer zeigt Fehlschlag-Status statt einer Erfolgs-Box
    }

    [Fact]
    public void PartialCopyFailure_MentionsBothSuccessCountAndFailureCount()
    {
        string msg = MainViewModel.BuildPipelineCompletionMessage(copyOk: 5, totalQueued: 8, drive: "E:\\");
        Assert.Contains("5 ISO(s) heruntergeladen und auf E:\\ kopiert.", msg);
        Assert.Contains("3 ISO(s) fehlgeschlagen", msg);
    }
}
