// Core/Models/DiscoveredDistro.cs
using System.Collections.Generic;

namespace ULM.Core.Models
{
    /// <summary>
    /// Ein via DiscoveryService (DistroWatch) gefundener Distro-Kandidat für die
    /// "Aktuellste"/"Beliebteste"-Reiter von IsoSearchDialog. Enthält bereits nur
    /// Live-Medium-geprüfte (per USB-Stick bootfähige) Distros — siehe DiscoveryService.
    /// </summary>
    public sealed class DiscoveredDistro
    {
        public required string Name              { get; init; }
        public required string Slug               { get; init; }
        public required string Info               { get; init; }
        public string          SuggestedCategory   { get; init; } = "Einsteiger";
        public IReadOnlyList<string> Tags          { get; init; } = System.Array.Empty<string>();
        public bool            AlreadyInDb         { get; set; }
    }
}
