// ULM.Assistant/Models/AssistantLanguage.cs
namespace ULM.Assistant.Models
{
    // Eigenständiges Sprach-Enum, bewusst NICHT identisch mit ULM.Infrastructure.AppLanguage
    // der Haupt-App — ULM.Assistant referenziert die Haupt-App nicht. Die Haupt-App bildet
    // AppLanguage beim Setzen von AvatarButton.GetLanguage auf dieses Enum ab (siehe Task 7).
    public enum AssistantLanguage { German, English }
}
