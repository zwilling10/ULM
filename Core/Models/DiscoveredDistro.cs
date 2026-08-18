// Core/Models/DiscoveredDistro.cs
using System.Collections.Generic;
using ULM.Infrastructure;

namespace ULM.Core.Models
{
    /// <summary>Welche Vorlage <see cref="DiscoveredDistro.Info"/> zur Anzeige verwendet.</summary>
    public enum DiscoveryInfoKind { AddedOn, RankHits }

    /// <summary>
    /// Ein via DiscoveryService (DistroWatch) gefundener Distro-Kandidat für die
    /// "Aktuellste"/"Beliebteste"-Reiter von IsoSearchDialog. Enthält bereits nur
    /// Live-Medium-geprüfte (per USB-Stick bootfähige) Distros — siehe DiscoveryService.
    /// </summary>
    public sealed class DiscoveredDistro
    {
        public required string Name              { get; init; }
        public required string Slug               { get; init; }
        public string          SuggestedCategory   { get; init; } = "Einsteiger";
        public IReadOnlyList<string> Tags          { get; init; } = System.Array.Empty<string>();
        public bool            AlreadyInDb         { get; set; }

        // Rohdaten statt fertig formatiertem Text — Info wird bei jedem Zugriff aus der
        // AKTUELLEN Sprache berechnet (siehe LocalizationService.Current). Das verhindert,
        // dass ein 24h zwischengespeicherter Discovery-Cache-Eintrag (ulm_discovery_cache.ini)
        // nach einem Sprachwechsel bis zum nächsten manuellen Refresh in der alten Sprache
        // hängen bleibt — genau der Bug, der vorher hier stand.
        public required DiscoveryInfoKind InfoKind { get; init; }
        public required string InfoArg1            { get; init; }
        public string          InfoArg2            { get; init; } = string.Empty;

        public string Info => InfoKind == DiscoveryInfoKind.RankHits
            ? string.Format(LocalizationService.T(Str.Db_Discovery_RankHits), InfoArg1, InfoArg2)
            : string.Format(LocalizationService.T(Str.Db_Discovery_AddedOn), InfoArg1);
    }
}
