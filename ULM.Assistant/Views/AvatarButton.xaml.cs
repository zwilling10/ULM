// ULM.Assistant/Views/AvatarButton.xaml.cs
using System;
using System.Windows;
using System.Windows.Controls;
using ULM.Assistant.Models;

namespace ULM.Assistant.Views
{
    // Schwebender 🐧-Button, den die Haupt-App irgendwo im Hauptfenster platziert. GetLanguage
    // wird von der Haupt-App gesetzt (Dependency Injection statt Projekt-Referenz — siehe Spec,
    // Architektur-Korrektur). Ein zweiter Klick, während das Chat-Fenster schon offen ist, holt
    // es nur nach vorne, statt ein zweites zu öffnen.
    public partial class AvatarButton : UserControl
    {
        public Func<AssistantLanguage> GetLanguage { get; set; } = () => AssistantLanguage.English;

        private ChatWindow? _openWindow;

        public AvatarButton()
        {
            InitializeComponent();
        }

        private void RootButton_Click(object sender, RoutedEventArgs e)
        {
            if (_openWindow is { IsLoaded: true })
            {
                _openWindow.Activate();
                return;
            }

            _openWindow = new ChatWindow(GetLanguage());
            PositionBottomRightOfOwner(_openWindow);
            _openWindow.Closed += (_, _) => _openWindow = null;
            _openWindow.Show();
        }

        // Öffnet Ulis Chat-Fenster rechts unten im Hauptfenster statt an einer beliebigen
        // Windows-Standardposition — dort, wo auch der 🐧-Button selbst sitzt. Owner wird
        // zusätzlich gesetzt, damit das Chat-Fenster beim Minimieren/Schließen des Hauptfensters
        // mitgeht (WPF-Standardverhalten für Owner-Fenster).
        private void PositionBottomRightOfOwner(ChatWindow chat)
        {
            var owner = Window.GetWindow(this);
            if (owner is null) return;

            chat.Owner = owner;
            const double margin = 16;
            chat.Left = owner.Left + owner.ActualWidth  - chat.Width  - margin;
            chat.Top  = owner.Top  + owner.ActualHeight - chat.Height - margin;
        }
    }
}
