using ULM.Assistant.Models;
using ULM.Assistant.Services;
using Xunit;

namespace ULM.Tests
{
    public class AssistantStringsTests
    {
        [Theory]
        [InlineData(AssistantStr.WindowTitle)]
        [InlineData(AssistantStr.Greeting)]
        [InlineData(AssistantStr.InputPlaceholder)]
        [InlineData(AssistantStr.SendButton)]
        [InlineData(AssistantStr.Fallback)]
        [InlineData(AssistantStr.BackToOverview)]
        [InlineData(AssistantStr.BestGuessPrefix)]
        public void T_BothLanguages_ReturnNonEmptyDistinctText(AssistantStr key)
        {
            string de = AssistantStrings.T(key, AssistantLanguage.German);
            string en = AssistantStrings.T(key, AssistantLanguage.English);
            Assert.False(string.IsNullOrWhiteSpace(de));
            Assert.False(string.IsNullOrWhiteSpace(en));
            Assert.NotEqual(de, en);
        }
    }
}
