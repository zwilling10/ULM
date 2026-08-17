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
            string basedOn      = ExtractFirst(html, @"search\.php\?basedon=([^""#]+)#simple") ?? string.Empty;
            string origin        = ExtractFirst(html, @"search\.php\?origin=([^""#]+)#simple") ?? string.Empty;
            string architecture  = ExtractFirst(html, @"search\.php\?architecture=([^""#]+)#simple") ?? string.Empty;
            string desktop       = ExtractFirst(html, @"search\.php\?desktop=([^""#]+)#simple") ?? string.Empty;

            bool? isActive = null;
            var statusMatch = Regex.Match(html, @"<font color=""([^""]+)"">");
            if (statusMatch.Success)
                isActive = statusMatch.Groups[1].Value.Equals("green", StringComparison.OrdinalIgnoreCase);

            int rank = 0, hits = 0;
            var popMatch = Regex.Match(html, @"resource=popularity"">(\d+)\s*\((\d+)");
            if (popMatch.Success)
            {
                int.TryParse(popMatch.Groups[1].Value, out rank);
                int.TryParse(popMatch.Groups[2].Value, out hits);
            }

            string description = string.Empty;
            var descMatch = Regex.Match(html, @"</ul>\s*(.*?)\s*<br><br>", RegexOptions.Singleline);
            if (descMatch.Success)
                description = System.Net.WebUtility.HtmlDecode(descMatch.Groups[1].Value).Trim();

            return new DistroPreview
            {
                Name                 = name,
                Description          = description,
                BasedOn              = basedOn,
                Origin               = origin,
                Architecture         = architecture,
                Desktop              = desktop,
                IsActive             = isActive,
                PopularityRank       = rank,
                PopularityHitsPerDay = hits,
                ScreenshotUrl        = $"https://distrowatch.com/images/slinks/{slug}-small.png",
            };
        }

        private static string? ExtractFirst(string html, string pattern)
        {
            var m = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
            return m.Success ? System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim() : null;
        }
    }
}
