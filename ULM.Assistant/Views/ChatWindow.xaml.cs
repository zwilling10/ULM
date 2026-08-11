// ULM.Assistant/Views/ChatWindow.xaml.cs
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ULM.Assistant.Models;
using ULM.Assistant.Services;

namespace ULM.Assistant.Views
{
    public partial class ChatWindow : Window
    {
        private readonly AssistantLanguage _language;
        private readonly IReadOnlyList<FaqEntry> _catalog;
        private readonly ObservableCollection<ChatMessageView> _messages = new();

        public ChatWindow(AssistantLanguage language)
        {
            InitializeComponent();
            _language = language;
            _catalog  = FaqCatalogService.Instance.Catalog;

            Title = AssistantStrings.T(AssistantStr.WindowTitle, _language);
            HeaderTitle.Text    = "Uli";
            HeaderSubtitle.Text = AssistantStrings.T(AssistantStr.WindowTitle, _language);
            InputPlaceholder.Text = AssistantStrings.T(AssistantStr.InputPlaceholder, _language);
            InputBox.Text = "";
            MessagesList.ItemsSource = _messages;

            AddUliMessage(AssistantStrings.T(AssistantStr.Greeting, _language));
            ShowMainTopics();
        }

        private void ShowMainTopics()
        {
            SuggestionsPanel.Children.Clear();
            foreach (var entry in _catalog)
                SuggestionsPanel.Children.Add(BuildSuggestionButton(entry));
        }

        private void ShowRelated(FaqEntry current)
        {
            SuggestionsPanel.Children.Clear();
            foreach (var relatedId in current.RelatedIds)
            {
                var related = _catalog.FirstOrDefault(e => e.Id == relatedId);
                if (related != null) SuggestionsPanel.Children.Add(BuildSuggestionButton(related));
            }

            var back = new Button
            {
                Content = AssistantStrings.T(AssistantStr.BackToOverview, _language),
                Style = (Style)FindResource("ChipButtonStyle"),
            };
            back.Click += (_, _) => ShowMainTopics();
            SuggestionsPanel.Children.Add(back);
        }

        private Button BuildSuggestionButton(FaqEntry entry)
        {
            string chip = _language == AssistantLanguage.German ? entry.ChipLabelDe : entry.ChipLabelEn;
            // Fallback für ältere assistant_faq.json-Dateien ohne ChipLabel (vor dieser Erweiterung
            // erzeugt) — sonst stünde ein leerer Button da.
            if (string.IsNullOrWhiteSpace(chip))
                chip = _language == AssistantLanguage.German ? entry.QuestionLabelDe : entry.QuestionLabelEn;

            var btn = new Button
            {
                Content = chip,
                Style = (Style)FindResource("ChipButtonStyle"),
                Tag = entry.Id,
            };
            btn.Click += (_, _) => AnswerTopic(entry);
            return btn;
        }

        private void AnswerTopic(FaqEntry entry)
        {
            string label  = _language == AssistantLanguage.German ? entry.QuestionLabelDe : entry.QuestionLabelEn;
            string answer = _language == AssistantLanguage.German ? entry.AnswerDe        : entry.AnswerEn;
            AddUserMessage(label);
            AddUliMessage(answer);
            ShowRelated(entry);
        }

        private void SendButton_Click(object sender, RoutedEventArgs e) => SubmitInput();

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) SubmitInput();
        }

        private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            InputPlaceholder.Visibility = InputBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SubmitInput()
        {
            string text = InputBox.Text.Trim();
            if (text.Length == 0) return;
            InputBox.Text = "";
            AddUserMessage(text);

            var result = FaqMatchingEngine.Match(_catalog, _language, text);
            var match = result.Id is null ? null : _catalog.FirstOrDefault(e => e.Id == result.Id);
            if (match is null)
            {
                AddUliMessage(AssistantStrings.T(AssistantStr.Fallback, _language));
                ShowMainTopics();
            }
            else
            {
                string answer = _language == AssistantLanguage.German ? match.AnswerDe : match.AnswerEn;
                string? hint  = result.IsBestGuess ? AssistantStrings.T(AssistantStr.BestGuessPrefix, _language) : null;
                AddUliMessage(answer, hint);
                ShowRelated(match);
            }
        }

        private void AddUserMessage(string text)
        {
            _messages.Add(new ChatMessageView(new ChatMessage { Sender = ChatSender.User, Text = text }));
            ScrollMessagesToBottom();
        }

        private void AddUliMessage(string text, string? hint = null)
        {
            _messages.Add(new ChatMessageView(new ChatMessage { Sender = ChatSender.Uli, Text = text }, hint));
            ScrollMessagesToBottom();
        }

        // Ohne das hier bleibt der ScrollViewer nach dem Hinzufügen einer neuen Nachricht an der
        // alten Position stehen — der Nutzer müsste manuell scrollen, um die neueste Antwort zu
        // sehen (genau der Bug aus dem Testfeedback). UpdateLayout() erzwingt VOR dem Scrollen
        // einen synchronen Layout-Durchlauf, damit ScrollToEnd() die durch die neue Nachricht
        // gewachsene Extent-Höhe bereits kennt — ohne das würde exakt eine Nachricht "hinterher"
        // gescrollt, weil die Bindung sonst erst beim nächsten Layout-Zyklus greift.
        private void ScrollMessagesToBottom()
        {
            MessagesScroll.UpdateLayout();
            MessagesScroll.ScrollToEnd();
        }
    }
}
