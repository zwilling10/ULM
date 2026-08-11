// ULM.Assistant/Services/FaqMatchingEngine.cs
using System;
using System.Collections.Generic;
using System.Linq;
using ULM.Assistant.Models;

namespace ULM.Assistant.Services
{
    // Ergebnis einer Matching-Anfrage. IsBestGuess=true bedeutet: kein Keyword hat exakt als
    // Teilstring getroffen, aber die Wörter im Text ähneln denen eines Themas genug (Tippfehler-
    // tolerant über Levenshtein-Distanz, plus Präfix-Vergleich für Wortformen wie "iso"/"isos") —
    // macht Uli "schlauer", ohne echte KI zu brauchen: es wird immer die naheliegendste Antwort
    // vorgeschlagen statt sofort aufzugeben, aber ehrlich als Vermutung gekennzeichnet.
    public readonly record struct FaqMatchResult(string? Id, bool IsBestGuess)
    {
        public static readonly FaqMatchResult None = new(null, false);
    }

    public static class FaqMatchingEngine
    {
        // Ein Wort zählt nur für den Tippfehler-/Präfix-Vergleich, wenn es lang genug ist — sonst
        // würden kurze Füllwörter ("ist", "der", "the") bei fast jedem Thema zufällig anschlagen.
        private const int MinFuzzyWordLength = 4;
        private const int MaxEditDistance = 1;

        public static FaqMatchResult Match(IReadOnlyList<FaqEntry> catalog, AssistantLanguage language, string userInput)
        {
            string[] inputWords = Tokenize(userInput);
            string text = string.Join(' ', inputWords);

            string? bestExactId = null;
            int bestExactScore = 0;
            string? bestFuzzyId = null;
            int bestFuzzyScore = 0;

            foreach (var entry in catalog)
            {
                var keywords = language == AssistantLanguage.German ? entry.KeywordsDe : entry.KeywordsEn;

                // Tier 1: exakter Teilstring-Treffer (wie bisher) — zählt als sicherer Beweis,
                // dass das Thema wirklich gemeint ist.
                int exactScore = keywords.Count(k => text.Contains(k.ToLowerInvariant()));
                if (exactScore > bestExactScore)
                {
                    bestExactScore = exactScore;
                    bestExactId = entry.Id;
                }

                // Tier 2: Wort-für-Wort-Ähnlichkeit (Präfix oder kleine Levenshtein-Distanz) —
                // liefert nur dann das Endergebnis, wenn Tier 1 über den GESAMTEN Katalog hinweg
                // nichts gefunden hat (siehe Rückgabe unten).
                int fuzzyScore = 0;
                foreach (var keyword in keywords)
                {
                    foreach (var keywordWord in keyword.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (keywordWord.Length < MinFuzzyWordLength) continue;
                        foreach (var inputWord in inputWords)
                        {
                            if (inputWord.Length < MinFuzzyWordLength) continue;
                            if (IsCloseMatch(inputWord, keywordWord)) fuzzyScore++;
                        }
                    }
                }
                if (fuzzyScore > bestFuzzyScore)
                {
                    bestFuzzyScore = fuzzyScore;
                    bestFuzzyId = entry.Id;
                }
            }

            if (bestExactScore > 0) return new FaqMatchResult(bestExactId, IsBestGuess: false);
            if (bestFuzzyScore > 0) return new FaqMatchResult(bestFuzzyId, IsBestGuess: true);
            return FaqMatchResult.None;
        }

        private static string[] Tokenize(string input) =>
            (input ?? "").ToLowerInvariant()
                .Split(new[] { ' ', '?', '!', '.', ',', ';', ':' }, StringSplitOptions.RemoveEmptyEntries);

        private static bool IsCloseMatch(string a, string b)
        {
            if (a == b) return true;
            if (a.StartsWith(b, StringComparison.Ordinal) || b.StartsWith(a, StringComparison.Ordinal))
                return true;
            return LevenshteinDistance(a, b) <= MaxEditDistance;
        }

        // Klassische Levenshtein-Distanz (Editierdistanz) — toleriert einzelne Tippfehler (ein
        // Zeichen eingefügt/gelöscht/ersetzt), z.B. "downlaod" vs. "download".
        private static int LevenshteinDistance(string a, string b)
        {
            int[,] d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            }
            return d[a.Length, b.Length];
        }
    }
}
