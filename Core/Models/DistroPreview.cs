// Core/Models/DistroPreview.cs
namespace ULM.Core.Models
{
    /// <summary>
    /// Ergebnis von DistroPreviewService.GetPreviewAsync — Kurzfakten + Beschreibung + Screenshot-
    /// URL für das Vorschau-Popup in IsoSearchDialog (DistroPreviewDialog). Felder werden
    /// strukturell aus der DistroWatch-Profilseite geparst (href-Query-Parameter, NICHT die
    /// sichtbaren deutschen Labels — siehe DistroPreviewService), damit das Popup unabhängig vom
    /// ULM-Sprachmodus korrekt bleibt.
    /// </summary>
    public sealed class DistroPreview
    {
        public required string Name         { get; init; }
        public string  Description          { get; init; } = string.Empty;
        public string  BasedOn              { get; init; } = string.Empty;
        public string  Origin               { get; init; } = string.Empty;
        public string  Architecture         { get; init; } = string.Empty;
        public string  Desktop              { get; init; } = string.Empty;
        public bool?   IsActive             { get; init; }
        public int     PopularityRank       { get; init; }
        public int     PopularityHitsPerDay { get; init; }
        public string? ScreenshotUrl        { get; init; }
    }
}
