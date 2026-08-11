using System.Collections.Generic;
using ULM.Assistant.Models;
using ULM.Assistant.Services;
using Xunit;

namespace ULM.Tests
{
    public class AssistantFaqMatchingEngineTests
    {
        private static List<FaqEntry> BuildCatalog() => new()
        {
            new FaqEntry
            {
                Id = "download-start",
                KeywordsDe = new() { "download", "herunterladen" },
                KeywordsEn = new() { "download" },
            },
            new FaqEntry
            {
                Id = "ventoy-setup",
                KeywordsDe = new() { "ventoy", "stick einrichten" },
                KeywordsEn = new() { "ventoy", "setup stick" },
            },
        };

        [Fact]
        public void Match_SingleKeywordHit_ReturnsCorrectIdNotAGuess()
        {
            var result = FaqMatchingEngine.Match(BuildCatalog(), AssistantLanguage.German, "Wie richte ich Ventoy ein?");
            Assert.Equal("ventoy-setup", result.Id);
            Assert.False(result.IsBestGuess);
        }

        [Fact]
        public void Match_NoKeywordOrFuzzyHit_ReturnsNone()
        {
            var result = FaqMatchingEngine.Match(BuildCatalog(), AssistantLanguage.German, "Wie ist das Wetter heute?");
            Assert.Equal(FaqMatchResult.None, result);
        }

        [Fact]
        public void Match_CaseInsensitive_StillMatches()
        {
            var result = FaqMatchingEngine.Match(BuildCatalog(), AssistantLanguage.German, "DOWNLOAD bitte");
            Assert.Equal("download-start", result.Id);
        }

        [Fact]
        public void Match_TieBreak_ReturnsFirstCatalogEntry()
        {
            var catalog = new List<FaqEntry>
            {
                new FaqEntry { Id = "first",  KeywordsDe = new() { "stick" } },
                new FaqEntry { Id = "second", KeywordsDe = new() { "stick" } },
            };
            var result = FaqMatchingEngine.Match(catalog, AssistantLanguage.German, "mein stick");
            Assert.Equal("first", result.Id);
        }

        [Fact]
        public void Match_EnglishKeywords_UsedWhenLanguageIsEnglish()
        {
            var result = FaqMatchingEngine.Match(BuildCatalog(), AssistantLanguage.English, "setup stick please");
            Assert.Equal("ventoy-setup", result.Id);
        }

        [Fact]
        public void Match_TypoInKeyword_ReturnsBestGuess()
        {
            // "downloaf" statt "download" — ein einzelner ersetzter Buchstabe (Editierdistanz 1),
            // keine exakte Teilstring-Übereinstimmung.
            var result = FaqMatchingEngine.Match(BuildCatalog(), AssistantLanguage.German, "wie starte ich einen downloaf");
            Assert.Equal("download-start", result.Id);
            Assert.True(result.IsBestGuess);
        }

        [Fact]
        public void Match_PluralWordForm_ReturnsBestGuessViaPrefix()
        {
            var catalog = new List<FaqEntry>
            {
                new FaqEntry { Id = "stick-topic", KeywordsDe = new() { "mein stick" } },
            };
            // "sticks" statt "stick" — kein exakter Teilstring-Treffer für "mein stick" als Ganzes,
            // aber "sticks" ist eine Präfixerweiterung von "stick".
            var result = FaqMatchingEngine.Match(catalog, AssistantLanguage.German, "wo liegen meine sticks");
            Assert.Equal("stick-topic", result.Id);
            Assert.True(result.IsBestGuess);
        }
    }
}
