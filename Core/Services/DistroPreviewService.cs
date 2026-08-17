// Core/Services/DistroPreviewService.cs
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ULM.Core.Models;

namespace ULM.Core.Services
{
    /// <summary>
    /// Lädt on-demand (nur beim Klick auf das 🔍-Icon in IsoSearchDialog, nicht beim Laden der
    /// ganzen Liste) die DistroWatch-Profilseite einer Distro und extrahiert Kurzfakten +
    /// Beschreibung für DistroPreviewDialog. WICHTIG: HttpService schickt immer
    /// Accept-Language: de-DE — die sichtbaren Sidebar-Labels UND der "Status"-Text auf der
    /// DistroWatch-Seite sind daher IMMER Deutsch, unabhängig vom ULM-Sprachmodus. Deshalb wird
    /// hier NICHT über den sichtbaren Label-Text geparst, sondern über die sprachneutralen
    /// href-Query-Parameter-Namen (?basedon=, ?origin=, ...) bzw. die Status-Farbe statt des
    /// Status-Texts — die im Dialog gezeigten Labels/Werte kommen komplett aus
    /// LocalizationService.T(...).
    /// </summary>
    public sealed class DistroPreviewService
    {
        private static readonly Lazy<DistroPreviewService> _lazy = new(() => new DistroPreviewService());
        public static DistroPreviewService Instance => _lazy.Value;

        private DistroPreviewService() { }

        public async Task<DistroPreview?> GetPreviewAsync(string name, string slug)
        {
            string? html = await HttpService.Instance.GetStringAsync($"https://distrowatch.com/{slug}").ConfigureAwait(false);
            if (html is null) return null;
            return ParseProfileHtml(name, slug, html);
        }

        internal static DistroPreview ParseProfileHtml(string name, string slug, string html)
        {
            var basedOnMatch      = Regex.Match(html, @"search\.php\?basedon=([^""#]+)#simple", RegexOptions.IgnoreCase);
            var originMatch        = Regex.Match(html, @"search\.php\?origin=([^""#]+)#simple", RegexOptions.IgnoreCase);
            var architectureMatch  = Regex.Match(html, @"search\.php\?architecture=([^""#]+)#simple", RegexOptions.IgnoreCase);
            var desktopMatch       = Regex.Match(html, @"search\.php\?desktop=([^""#]+)#simple", RegexOptions.IgnoreCase);
            var statusMatch        = Regex.Match(html, @"<font color=""([^""]+)"">");
            var popMatch           = Regex.Match(html, @"resource=popularity"">(\d+)\s*\((\d+)");

            bool? isActive = statusMatch.Success
                ? statusMatch.Groups[1].Value.Equals("green", StringComparison.OrdinalIgnoreCase)
                : (bool?)null;

            int rank = 0, hits = 0;
            if (popMatch.Success)
            {
                int.TryParse(popMatch.Groups[1].Value, out rank);
                int.TryParse(popMatch.Groups[2].Value, out hits);
            }

            // Die Beschreibung steht direkt nach dem </ul>, das die Kurzfakten-Liste abschließt.
            // Ein naives "erstes </ul> im gesamten Dokument"-Muster trifft stattdessen fälschlich
            // das schließende </ul> des Seiten-Navigations-Dropdownmenüs (steht im HTML VOR den
            // Fakten) — live an SkillFishOS beobachtet: landete dann mitten im direkt
            // anschließenden <script>-Block mit dem Menü-JavaScript der Seite, JS-Quelltext
            // erschien als "Beschreibung" im Popup. Deshalb startet die Suche erst NACH dem Ende
            // des am weitesten hinten liegenden, tatsächlich gefundenen Fakten-Treffers
            // (Popularität steht als letztes Feld in der Liste, ist also normalerweise der
            // maßgebliche Anker — die Schleife ist trotzdem robust, falls einzelne Felder fehlen).
            int anchor = 0;
            foreach (var m in new[] { basedOnMatch, originMatch, architectureMatch, desktopMatch, statusMatch, popMatch })
                if (m.Success) anchor = Math.Max(anchor, m.Index + m.Length);

            string description = string.Empty;
            if (anchor > 0 && anchor < html.Length)
            {
                var descMatch = Regex.Match(html[anchor..], @"</ul>\s*(.*?)\s*<br><br>", RegexOptions.Singleline);
                if (descMatch.Success)
                    description = System.Net.WebUtility.HtmlDecode(descMatch.Groups[1].Value).Trim();
            }

            return new DistroPreview
            {
                Name                 = name,
                Description          = description,
                BasedOn              = DecodeGroup(basedOnMatch),
                Origin               = DecodeGroup(originMatch),
                Architecture         = DecodeGroup(architectureMatch),
                Desktop              = DecodeGroup(desktopMatch),
                IsActive             = isActive,
                PopularityRank       = rank,
                PopularityHitsPerDay = hits,
                ScreenshotUrl        = $"https://distrowatch.com/images/slinks/{slug}-small.png",
            };
        }

        private static string DecodeGroup(Match m) =>
            m.Success ? System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim() : string.Empty;
    }
}
