// ULM.Assistant/Services/AssistantStrings.cs
using ULM.Assistant.Models;

namespace ULM.Assistant.Services
{
    // Eigenständiges, winziges Lokalisierungs-Set NUR für die Chrome-Texte des Chat-Fensters
    // (Begrüßung, Platzhalter, Fallback, …) — bewusst getrennt von der Haupt-App's
    // LocalizationService/Str (ULM.Assistant referenziert die Haupt-App nicht). Die eigentlichen
    // Katalog-Texte (Fragen/Antworten) liegen direkt zweisprachig in FaqEntry.
    public enum AssistantStr { WindowTitle, Greeting, InputPlaceholder, SendButton, Fallback, BackToOverview, BestGuessPrefix }

    public static class AssistantStrings
    {
        public static string T(AssistantStr key, AssistantLanguage language) =>
            language == AssistantLanguage.German ? De(key) : En(key);

        private static string De(AssistantStr key) => key switch
        {
            AssistantStr.WindowTitle      => "Uli — Hilfe",
            AssistantStr.Greeting         => "Hallo! Ich bin Uli 🐧. Wähle unten ein Thema oder tippe deine Frage:",
            AssistantStr.InputPlaceholder => "Frage eingeben …",
            AssistantStr.SendButton       => "Senden",
            AssistantStr.Fallback         => "Das habe ich leider nicht verstanden — hier sind die Themen, bei denen ich helfen kann:",
            AssistantStr.BackToOverview   => "⬅ Zur Übersicht",
            AssistantStr.BestGuessPrefix  => "? Beste Vermutung, nicht ganz sicher",
            _ => "",
        };

        private static string En(AssistantStr key) => key switch
        {
            AssistantStr.WindowTitle      => "Uli — Help",
            AssistantStr.Greeting         => "Hi! I'm Uli 🐧. Pick a topic below or type your question:",
            AssistantStr.InputPlaceholder => "Type your question …",
            AssistantStr.SendButton       => "Send",
            AssistantStr.Fallback         => "I'm sorry, I didn't understand that — here are the topics I can help with:",
            AssistantStr.BackToOverview   => "⬅ Back to overview",
            AssistantStr.BestGuessPrefix  => "? Best guess, not entirely sure",
            _ => "",
        };
    }
}
