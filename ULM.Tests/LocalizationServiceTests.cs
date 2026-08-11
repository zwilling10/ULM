using System;
using System.Globalization;
using System.IO;
using ULM.Infrastructure;
using Xunit;

namespace ULM.Tests;

public class LocalizationServiceTTests
{
    [Theory]
    [InlineData(AppLanguage.German, "❓ Hilfe")]
    [InlineData(AppLanguage.English, "❓ Help")]
    public void T_ReturnsCorrectTextForLanguage(AppLanguage language, string expected)
    {
        Assert.Equal(expected, LocalizationService.T(Str.Btn_Help, language));
    }

    [Theory]
    [InlineData(AppLanguage.German, "⬇  Herunterladen")]
    [InlineData(AppLanguage.English, "⬇  Download")]
    public void T_Btn_Download_ReturnsCorrectTextForLanguage(AppLanguage language, string expected)
    {
        Assert.Equal(expected, LocalizationService.T(Str.Btn_Download, language));
    }

    [Theory]
    [InlineData(AppLanguage.German, "✔ Übernehmen")]
    [InlineData(AppLanguage.English, "✔ Apply")]
    public void T_Setup_Btn_Apply_ReturnsCorrectTextForLanguage(AppLanguage language, string expected)
    {
        Assert.Equal(expected, LocalizationService.T(Str.Setup_Btn_Apply, language));
    }

    [Theory]
    [InlineData(AppLanguage.German, "Ordner konnte nicht erstellt werden:")]
    [InlineData(AppLanguage.English, "Could not create folder:")]
    public void T_Setup_Error_FolderCreateFailed_ReturnsCorrectTextForLanguage(AppLanguage language, string expected)
    {
        Assert.Equal(expected, LocalizationService.T(Str.Setup_Error_FolderCreateFailed, language));
    }

    [Theory]
    [InlineData(AppLanguage.German, "Auf {0} wurden {1} veraltete ISO(s) gefunden:")]
    [InlineData(AppLanguage.English, "{1} outdated ISO(s) found on {0}:")]
    public void T_Msg_StickOutdatedFound_ReturnsCorrectFormatStringForLanguage(AppLanguage language, string expected)
    {
        Assert.Equal(expected, LocalizationService.T(Str.Msg_StickOutdatedFound, language));
    }

    [Fact]
    public void Msg_StickOutdatedFound_FormatsCorrectlyInGerman()
    {
        string result = string.Format(LocalizationService.T(Str.Msg_StickOutdatedFound, AppLanguage.German), "E:", 3);
        Assert.Equal("Auf E: wurden 3 veraltete ISO(s) gefunden:", result);
    }

    [Fact]
    public void Msg_StickOutdatedFound_FormatsCorrectlyInEnglish()
    {
        string result = string.Format(LocalizationService.T(Str.Msg_StickOutdatedFound, AppLanguage.English), "E:", 3);
        Assert.Equal("3 outdated ISO(s) found on E::", result);
    }

    [Theory]
    [InlineData(AppLanguage.German, "Update")]
    [InlineData(AppLanguage.English, "Update")]
    public void T_Row_UpdatePrefix_ReturnsCorrectTextForLanguage(AppLanguage language, string expected)
    {
        Assert.Equal(expected, LocalizationService.T(Str.Row_UpdatePrefix, language));
    }

    [Theory]
    [InlineData(AppLanguage.German, "🎮 Gaming")]
    [InlineData(AppLanguage.English, "🎮 Gaming")]
    public void T_Category_Gaming_ReturnsCorrectTextForLanguage(AppLanguage language, string expected)
    {
        Assert.Equal(expected, LocalizationService.T(Str.Category_Gaming, language));
    }
}

public class LocalizationServiceDetectFromCultureTests
{
    [Fact]
    public void DetectFromCulture_German_ReturnsGerman()
    {
        Assert.Equal(AppLanguage.German, LocalizationService.DetectFromCulture(new CultureInfo("de-DE")));
    }

    [Fact]
    public void DetectFromCulture_NonGerman_ReturnsEnglish()
    {
        Assert.Equal(AppLanguage.English, LocalizationService.DetectFromCulture(new CultureInfo("fr-FR")));
    }
}

public class LocalizationServiceLoadFromIniTests
{
    [Fact]
    public void LoadFromIni_SavedDe_ReturnsGerman()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"ulm-loc-{Guid.NewGuid():N}.ini");
        try
        {
            IniService.Write(tempFile, "App", "Language", "de");
            Assert.Equal(AppLanguage.German, LocalizationService.LoadFromIni(tempFile));
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void LoadFromIni_SavedEn_ReturnsEnglish()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"ulm-loc-{Guid.NewGuid():N}.ini");
        try
        {
            IniService.Write(tempFile, "App", "Language", "en");
            Assert.Equal(AppLanguage.English, LocalizationService.LoadFromIni(tempFile));
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void LoadFromIni_MissingFile_FallsBackToCultureDetection()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"ulm-loc-{Guid.NewGuid():N}.ini");
        // Datei existiert bewusst nicht — IniService.Read liefert den uebergebenen Default "" zurueck,
        // LoadFromIni faellt dann auf DetectFromCulture(CurrentUICulture) zurueck.
        AppLanguage expected = LocalizationService.DetectFromCulture(CultureInfo.CurrentUICulture);
        Assert.Equal(expected, LocalizationService.LoadFromIni(tempFile));
    }
}

[Collection("LocalizationCurrent")]
public class LocalizationServiceInitializeWithPathTests
{
    [Fact]
    public void Initialize_WithCustomPath_SetsCurrentFromThatFile()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"ulm-loc-init-{Guid.NewGuid():N}.ini");
        try
        {
            IniService.Write(tempFile, "App", "Language", "en");
            LocalizationService.Initialize(tempFile);
            Assert.Equal(AppLanguage.English, LocalizationService.Current);
        }
        finally { File.Delete(tempFile); }
    }
}

// Serialisiert Tests, die den globalen, statischen LocalizationService.Current-Zustand
// lesen/schreiben (xUnit führt verschiedene Test-Klassen sonst parallel aus — ohne dieses
// Collection-Attribut könnte SetLanguage_WritesToIniAndUpdatesCurrent mitten in einem Test einer
// anderen Klasse, die ambient über LocalizationService.T(...) liest, die Sprache umschalten).
// Siehe MainViewModelPipelineSummaryTests, das dieselbe Collection nutzt.
[CollectionDefinition("LocalizationCurrent", DisableParallelization = true)]
public class LocalizationCurrentCollection { }

[Collection("LocalizationCurrent")]
public class LocalizationServiceSetLanguageTests
{
    [Fact]
    public void SetLanguage_WritesToIniAndUpdatesCurrent()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"ulm-loc-{Guid.NewGuid():N}.ini");
        try
        {
            LocalizationService.SetLanguage(AppLanguage.English, tempFile);

            Assert.Equal(AppLanguage.English, LocalizationService.Current);
            Assert.Equal("en", IniService.Read(tempFile, "App", "Language", ""));
        }
        finally { File.Delete(tempFile); }
    }
}

public class LocalizationServiceCompletenessTests
{
    [Fact]
    public void AllStrValues_HaveGermanAndEnglishTranslation()
    {
        foreach (Str key in Enum.GetValues<Str>())
        {
            string de = LocalizationService.T(key, AppLanguage.German);
            string en = LocalizationService.T(key, AppLanguage.English);
            Assert.False(string.IsNullOrWhiteSpace(de), $"Fehlende deutsche Übersetzung für {key}");
            Assert.False(string.IsNullOrWhiteSpace(en), $"Fehlende englische Übersetzung für {key}");
        }
    }
}

public class LocalizationServiceLinuxStringsTests
{
    [Theory]
    [InlineData(AppLanguage.German, "Alle")]
    [InlineData(AppLanguage.English, "All")]
    public void T_Linux_Category_All_ReturnsCorrectTextForLanguage(AppLanguage language, string expected)
    {
        Assert.Equal(expected, LocalizationService.T(Str.Linux_Category_All, language));
    }

    [Theory]
    [InlineData(AppLanguage.German, "Distro suchen…")]
    [InlineData(AppLanguage.English, "Search distro…")]
    public void T_Linux_Toolbar_SearchPlaceholder_ReturnsCorrectTextForLanguage(AppLanguage language, string expected)
    {
        Assert.Equal(expected, LocalizationService.T(Str.Linux_Toolbar_SearchPlaceholder, language));
    }

    [Theory]
    [InlineData(AppLanguage.German, "Aktualisieren")]
    [InlineData(AppLanguage.English, "Refresh")]
    public void T_Linux_Toolbar_Refresh_ReturnsCorrectTextForLanguage(AppLanguage language, string expected)
    {
        Assert.Equal(expected, LocalizationService.T(Str.Linux_Toolbar_Refresh, language));
    }

    [Theory]
    [InlineData(AppLanguage.German, "Keine Download-URL hinterlegt.")]
    [InlineData(AppLanguage.English, "No download URL configured.")]
    public void T_Linux_Download_NoUrl_ReturnsCorrectTextForLanguage(AppLanguage language, string expected)
    {
        Assert.Equal(expected, LocalizationService.T(Str.Linux_Download_NoUrl, language));
    }

    [Theory]
    [InlineData(AppLanguage.German, "Download fehlgeschlagen.")]
    [InlineData(AppLanguage.English, "Download failed.")]
    public void T_Linux_Download_Failed_ReturnsCorrectTextForLanguage(AppLanguage language, string expected)
    {
        Assert.Equal(expected, LocalizationService.T(Str.Linux_Download_Failed, language));
    }
}
