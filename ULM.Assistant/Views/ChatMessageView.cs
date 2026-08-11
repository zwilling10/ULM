// ULM.Assistant/Views/ChatMessageView.cs
using System.Windows;
using System.Windows.Media;
using ULM.Assistant.Models;

namespace ULM.Assistant.Views
{
    // Bindbare Hülle um ChatMessage für die WPF-Anzeige — bewusst getrennt von Models.ChatMessage,
    // damit das reine Datenmodell frei von WPF-Typen (Brush, HorizontalAlignment, CornerRadius)
    // bleibt. Liest Farben zur Laufzeit per String-Schlüssel aus Application.Current.Resources —
    // funktioniert automatisch im aktuell aktiven Hell/Dunkel-Theme der Haupt-App, ganz ohne
    // Projekt-Referenz dorthin (dieselbe Technik wie AppRes.Brush(...) in
    // Views/Dialogs/DownloadDialogs.cs der Haupt-App).
    public sealed class ChatMessageView
    {
        public string Text { get; }
        public string? HintText { get; }
        public Visibility HintVisibility { get; }
        public Brush BubbleBrush { get; }
        public HorizontalAlignment BubbleAlignment { get; }
        // Sprechblasen mit "Schwänzchen": die dem Absender zugewandte Ecke bleibt fast eckig
        // (3px), die anderen drei sind stark gerundet — macht auf einen Blick klar, wer zuerst
        // "spricht", ohne dass man erst die Farbe vergleichen muss.
        public CornerRadius BubbleCorner { get; }
        public Visibility AvatarVisibility { get; }

        public ChatMessageView(ChatMessage message, string? hintText = null)
        {
            Text = message.Text;
            HintText = hintText;
            HintVisibility = string.IsNullOrEmpty(hintText) ? Visibility.Collapsed : Visibility.Visible;

            bool fromUser = message.Sender == ChatSender.User;
            BubbleAlignment   = fromUser ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            BubbleCorner      = fromUser ? new CornerRadius(12, 3, 12, 12) : new CornerRadius(3, 12, 12, 12);
            AvatarVisibility  = fromUser ? Visibility.Collapsed : Visibility.Visible;
            BubbleBrush = Application.Current?.Resources[fromUser ? "BrushBlue" : "BrushCard"] as Brush
                ?? Brushes.LightGray;
        }
    }
}
