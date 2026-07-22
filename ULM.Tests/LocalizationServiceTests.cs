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
