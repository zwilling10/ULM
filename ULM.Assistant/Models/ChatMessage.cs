// ULM.Assistant/Models/ChatMessage.cs
namespace ULM.Assistant.Models
{
    public enum ChatSender { User, Uli }

    public sealed class ChatMessage
    {
        public ChatSender Sender { get; init; }
        public string Text { get; init; } = "";
    }
}
