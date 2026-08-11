using System;
using System.IO;
using System.Linq;
using ULM.Assistant.Services;
using Xunit;

namespace ULM.Tests
{
    public class AssistantFaqCatalogServiceTests
    {
        [Fact]
        public void LoadOrDefault_MissingFile_ReturnsDefaultCatalog()
        {
            string missingPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.json");
            var catalog = FaqCatalogService.LoadOrDefault(missingPath);
            Assert.True(catalog.Count > 0);
        }

        [Fact]
        public void LoadOrDefault_CorruptJson_ReturnsDefaultCatalog()
        {
            string path = Path.Combine(Path.GetTempPath(), $"corrupt_{Guid.NewGuid()}.json");
            File.WriteAllText(path, "{ this is not valid json ][");
            try
            {
                var catalog = FaqCatalogService.LoadOrDefault(path);
                Assert.True(catalog.Count > 0);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void LoadOrDefault_ValidJson_LoadsExactEntries()
        {
            string path = Path.Combine(Path.GetTempPath(), $"valid_{Guid.NewGuid()}.json");
            File.WriteAllText(path, """
            [
              {
                "Id": "test-entry",
                "KeywordsDe": ["test"],
                "KeywordsEn": ["test"],
                "QuestionLabelDe": "Testfrage?",
                "QuestionLabelEn": "Test question?",
                "AnswerDe": "Testantwort.",
                "AnswerEn": "Test answer.",
                "RelatedIds": []
              }
            ]
            """);
            try
            {
                var catalog = FaqCatalogService.LoadOrDefault(path);
                Assert.Single(catalog);
                Assert.Equal("test-entry", catalog[0].Id);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void Constructor_WithJsonPath_ExposesLoadedCatalog()
        {
            string missingPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.json");
            var service = new FaqCatalogService(missingPath);
            Assert.True(service.Catalog.Count > 0);
        }

        [Fact]
        public void DefaultCatalog_AllEntries_HaveUniqueIdsAndCompleteBilingualText()
        {
            var catalog = FaqCatalogService.DefaultCatalog();
            var ids = catalog.Select(e => e.Id).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());

            foreach (var entry in catalog)
            {
                Assert.False(string.IsNullOrWhiteSpace(entry.QuestionLabelDe));
                Assert.False(string.IsNullOrWhiteSpace(entry.QuestionLabelEn));
                Assert.False(string.IsNullOrWhiteSpace(entry.ChipLabelDe));
                Assert.False(string.IsNullOrWhiteSpace(entry.ChipLabelEn));
                Assert.False(string.IsNullOrWhiteSpace(entry.AnswerDe));
                Assert.False(string.IsNullOrWhiteSpace(entry.AnswerEn));
                Assert.True(entry.KeywordsDe.Count > 0);
                Assert.True(entry.KeywordsEn.Count > 0);

                foreach (var relatedId in entry.RelatedIds)
                    Assert.Contains(relatedId, ids);
            }
        }
    }
}
