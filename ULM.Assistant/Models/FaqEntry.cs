// ULM.Assistant/Models/FaqEntry.cs
using System.Collections.Generic;

namespace ULM.Assistant.Models
{
    // Ein Thema in Ulis Fragen-Katalog. Wird per System.Text.Json direkt aus/in
    // assistant_faq.json (de)serialisiert — Property-Namen müssen daher exakt zu den
    // JSON-Schlüsseln passen (siehe FaqCatalogService.DefaultCatalog).
    public sealed class FaqEntry
    {
        public string Id { get; set; } = "";
        public List<string> KeywordsDe { get; set; } = new();
        public List<string> KeywordsEn { get; set; } = new();
        public string QuestionLabelDe { get; set; } = "";
        public string QuestionLabelEn { get; set; } = "";
        // Kurzes Chip-Label (1-3 Wörter, z.B. "ISO suchen") für die Vorschlag-Buttons im Chat —
        // getrennt von QuestionLabel (voller Fragesatz, wird als Chat-Bubble angezeigt, wenn der
        // Chip geklickt wird). Leer bei älteren, vor dieser Erweiterung erzeugten
        // assistant_faq.json-Dateien — ChatWindow fällt dann auf QuestionLabel zurück.
        public string ChipLabelDe { get; set; } = "";
        public string ChipLabelEn { get; set; } = "";
        public string AnswerDe { get; set; } = "";
        public string AnswerEn { get; set; } = "";
        public List<string> RelatedIds { get; set; } = new();
    }
}
