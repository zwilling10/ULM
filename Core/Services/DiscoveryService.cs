// Core/Services/DiscoveryService.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ULM.Core.Models;
using ULM.Infrastructure;

namespace ULM.Core.Services
{
    /// <summary>
    /// Holt die "Aktuellste"/"Beliebteste"-Distro-Listen für IsoSearchDialog von DistroWatch —
    /// dieselbe Quelle, die HttpService.ResolveViaDistroWatchAsync bereits für die
    /// Update-Auflösung nutzt. Beide Listen werden zusätzlich gegen das DistroWatch-Kategorie-Tag
    /// "Live Medium" geprüft (jede ULM-Distro MUSS live-bootfähig per USB-Stick sein — reine
    /// Installations-/Server-Images ohne Live-Modus werden aussortiert), Ergebnis wird 24h lokal
    /// zwischengespeichert (ulm_discovery_cache.ini), ein manueller Refresh überschreibt den Cache.
    /// </summary>
    public sealed class DiscoveryService
    {
        private static readonly Lazy<DiscoveryService> _lazy = new(() => new DiscoveryService());
        public static DiscoveryService Instance => _lazy.Value;

        private DiscoveryService() { }

        private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

        public sealed record DiscoveryResult(IReadOnlyList<DiscoveredDistro> Items, DateTimeOffset FetchedAtUtc, bool FromCache);

        public Task<DiscoveryResult> GetLatestAdditionsAsync(bool forceRefresh = false) =>
            GetAsync("Latest", forceRefresh, FetchLatestAdditionsAsync);

        public Task<DiscoveryResult> GetMostPopularAsync(bool forceRefresh = false) =>
            GetAsync("Popular", forceRefresh, FetchMostPopularAsync);

        // ── Gemeinsamer Cache-Ablauf für beide Listen ───────────────────────
        private async Task<DiscoveryResult> GetAsync(string section, bool forceRefresh, Func<Task<List<DiscoveredDistro>>> fetch)
        {
            if (!forceRefresh)
            {
                var cached = ReadCache(section);
                if (cached != null && DateTimeOffset.UtcNow - cached.Value.FetchedAtUtc < CacheTtl)
                    return new DiscoveryResult(MarkAlreadyInDb(cached.Value.Items), cached.Value.FetchedAtUtc, FromCache: true);
            }

            var items = await fetch().ConfigureAwait(false);
            var now   = DateTimeOffset.UtcNow;
            // Ein leeres Ergebnis heißt fast immer "DistroWatch gerade nicht erreichbar" (Timeout,
            // Netzwerkfehler) statt "wirklich keine Live-Medium-Distros" — das darf nicht 24h lang
            // eingefroren werden, sonst bleibt der Reiter bis zum manuellen Aktualisieren leer, obwohl
            // ein erneuter Versuch kurz danach funktionieren würde.
            if (items.Count > 0) WriteCache(section, items, now);
            return new DiscoveryResult(MarkAlreadyInDb(items), now, FromCache: false);
        }

        private static List<DiscoveredDistro> MarkAlreadyInDb(IReadOnlyList<DiscoveredDistro> items)
        {
            var db = IsoDatabaseService.Instance;
            foreach (var it in items) it.AlreadyInDb = db.Entries.Any(e => NameMatches(e.Name, it.Name));
            return items.ToList();
        }

        private static bool NameMatches(string a, string b)
        {
            static string Norm(string s) => new(s.Where(char.IsLetterOrDigit).ToArray());
            string na = Norm(a).ToLowerInvariant(), nb = Norm(b).ToLowerInvariant();
            return na.Length > 0 && nb.Length > 0 && (na.Contains(nb) || nb.Contains(na));
        }

        // ── "Aktuellste": DistroWatch-Startseite, Widget "Latest Additions" ─
        private async Task<List<DiscoveredDistro>> FetchLatestAdditionsAsync()
        {
            string? html = await HttpService.Instance.GetStringAsync("https://distrowatch.com/", 15).ConfigureAwait(false);
            if (html is null) return new List<DiscoveredDistro>();

            // Beobachtetes Muster: <tr><th class="News">07-11</th><td class="News"><a href="margine">Margine OS</a></td></tr>
            // ACHTUNG: class="News" wird auf der Startseite auch vom allgemeinen Nachrichten-/
            // Wochenrückblick-Widget verwendet (gleiche Zeilenstruktur, aber href ist dort eine volle
            // URL oder "weekly.php?..."). Nur echte Distro-Slugs (reines Wort, kein "/"/"?"/"http")
            // gehören zur "Latest Additions"-Box — sonst rutschen Nachrichtenartikel mit rein.
            var matches = Regex.Matches(html,
                @"<tr>\s*<th class=""News"">([\d-]+)</th>\s*<td class=""News""><a href=""([^""]+)"">([^<]+)</a></td>\s*</tr>",
                RegexOptions.IgnoreCase);

            var candidates = matches.Cast<Match>()
                .Select(m => (Date: m.Groups[1].Value, Slug: m.Groups[2].Value, Name: WebDecode(m.Groups[3].Value)))
                .Where(c => Regex.IsMatch(c.Slug, @"^[a-z0-9_-]+$", RegexOptions.IgnoreCase))
                .Take(20)
                .ToList();

            return await ResolveLiveMediumCandidatesAsync(candidates.Select(c => (c.Slug, c.Name, Info: $"Hinzugefügt: {c.Date}")).ToList())
                .ConfigureAwait(false);
        }

        // ── "Beliebteste": DistroWatch Page-Hit-Ranking, Tabelle "Last 12 months" ─
        private async Task<List<DiscoveredDistro>> FetchMostPopularAsync()
        {
            string? html = await HttpService.Instance.GetStringAsync("https://distrowatch.com/dwres.php?resource=popularity", 15).ConfigureAwait(false);
            if (html is null) return new List<DiscoveredDistro>();

            // Beobachtetes Muster (erste Tabelle im Dokument = "Last 12 months", Ranking-Reihenfolge
            // entspricht der Dokumentreihenfolge — ausreichend, um nur die ersten Treffer zu nehmen):
            // <th class="phr1">1</th><td class="phr2"><a ... href="cachyos">CachyOS</a></td>
            // <td class="phr3" ...>3955<img .../></td>
            var matches = Regex.Matches(html,
                @"<th class=""phr1"">(\d+)</th>\s*<td class=""phr2""><a[^>]*href=""([^""]+)"">([^<]+)</a></td>\s*<td class=""phr3""[^>]*>(\d+)",
                RegexOptions.IgnoreCase);

            var candidates = matches.Cast<Match>()
                .Select(m => (Rank: m.Groups[1].Value, Slug: m.Groups[2].Value, Name: WebDecode(m.Groups[3].Value), Hits: m.Groups[4].Value))
                .Take(20)
                .ToList();

            return await ResolveLiveMediumCandidatesAsync(candidates.Select(c => (c.Slug, c.Name, Info: $"#{c.Rank} · {c.Hits} Hits/Tag")).ToList())
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Prüft jeden Kandidaten gegen dessen DistroWatch-Profilseite: nur mit dem Kategorie-Tag
        /// "Live Medium" (per USB-Stick bootfähig, keine reine Installations-/Server-Distro) bleibt
        /// er in der Liste. Bricht ab, sobald 10 gültige Kandidaten gefunden wurden.
        /// </summary>
        private async Task<List<DiscoveredDistro>> ResolveLiveMediumCandidatesAsync(List<(string Slug, string Name, string Info)> candidates)
        {
            var result = new List<DiscoveredDistro>();
            foreach (var c in candidates)
            {
                if (result.Count >= 10) break;
                if (string.IsNullOrWhiteSpace(c.Slug) || string.IsNullOrWhiteSpace(c.Name)) continue;
                try
                {
                    string? profileHtml = await HttpService.Instance.GetStringAsync($"https://distrowatch.com/{c.Slug}", 12).ConfigureAwait(false);
                    if (profileHtml is null) continue;
                    if (!Regex.IsMatch(profileHtml, @"category=Live\+Medium#simple", RegexOptions.IgnoreCase)) continue;

                    var tags = Regex.Matches(profileHtml, @"category=[^""#]+#simple"">([^<]+)<", RegexOptions.IgnoreCase)
                        .Cast<Match>().Select(m => m.Groups[1].Value).ToList();

                    result.Add(new DiscoveredDistro { Name = c.Name, Slug = c.Slug, Info = c.Info, SuggestedCategory = GuessCategory(tags), Tags = tags });
                }
                catch (Exception ex) { Debug.WriteLine($"[Discovery] {c.Slug}: {ex.Message}"); }
            }
            return result;
        }

        private static string GuessCategory(IReadOnlyList<string> dwTags)
        {
            bool Has(string t) => dwTags.Any(x => x.Equals(t, StringComparison.OrdinalIgnoreCase));
            if (Has("Gaming"))                                                       return "Gaming";
            if (Has("Security") || Has("Privacy") || Has("Forensics"))                return "Sicherheit";
            if (Has("Rescue") || Has("Backup"))                                       return "Rettung";
            if (Has("Antivirus"))                                                     return "Antivirus";
            if (Has("Minimal") || Has("Old Computers") || Has("Lightweight"))          return "Leichtgewicht";
            if (Has("Server") || Has("Container") || Has("Kubernetes") ||
                Has("Immutable") || Has("Router/Firewall") || Has("Networking"))       return "Fortgeschrittene";
            return "Einsteiger";
        }

        private static string WebDecode(string s) => System.Net.WebUtility.HtmlDecode(s).Trim();

        // ── 24h-Dateicache (eigenes kleines INI-Format, mit IniService.ReadAll les- und
        //    IniService.Write manuell-erweiterbar — hier direkt geschrieben, da pro Refresh ~10
        //    Einträge x mehrere Felder anfallen und IniService.Write die Datei bei jedem Aufruf
        //    komplett neu einliest/schreibt) ─────────────────────────────────
        private static (DateTimeOffset FetchedAtUtc, List<DiscoveredDistro> Items)? ReadCache(string section)
        {
            string path = AppPaths.Instance.DiscoveryCacheIni;
            var data = IniService.ReadAll(path);
            if (!data.TryGetValue(section, out var meta) || !meta.TryGetValue("FetchedAtUtc", out var ts)) return null;
            if (!DateTimeOffset.TryParse(ts, out var fetchedAt)) return null;
            if (!int.TryParse(meta.GetValueOrDefault("Count", "0"), out int count) || count <= 0) return (fetchedAt, new List<DiscoveredDistro>());

            var items = new List<DiscoveredDistro>();
            for (int i = 0; i < count; i++)
            {
                if (!data.TryGetValue($"{section}_{i}", out var d)) continue;
                string name = d.GetValueOrDefault("Name", string.Empty);
                string slug = d.GetValueOrDefault("Slug", string.Empty);
                if (name.Length == 0 || slug.Length == 0) continue;
                string tagsRaw = d.GetValueOrDefault("Tags", string.Empty);
                items.Add(new DiscoveredDistro
                {
                    Name = name, Slug = slug,
                    Info = d.GetValueOrDefault("Info", string.Empty),
                    SuggestedCategory = d.GetValueOrDefault("Category", "Einsteiger"),
                    Tags = tagsRaw.Length == 0 ? Array.Empty<string>() : tagsRaw.Split('|', StringSplitOptions.RemoveEmptyEntries),
                });
            }
            return (fetchedAt, items);
        }

        private static void WriteCache(string section, List<DiscoveredDistro> items, DateTimeOffset fetchedAtUtc)
        {
            try
            {
                string path = AppPaths.Instance.DiscoveryCacheIni;
                var data = IniService.ReadAll(path);
                data[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["FetchedAtUtc"] = fetchedAtUtc.ToString("o"),
                    ["Count"]        = items.Count.ToString(),
                };
                for (int i = 0; i < items.Count; i++)
                {
                    data[$"{section}_{i}"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Name"]     = items[i].Name,
                        ["Slug"]     = items[i].Slug,
                        ["Info"]     = items[i].Info,
                        ["Category"] = items[i].SuggestedCategory,
                        ["Tags"]     = string.Join('|', items[i].Tags),
                    };
                }

                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                using var writer = new StreamWriter(path, append: false, Encoding.UTF8);
                foreach (var (sectionName, dict) in data)
                {
                    writer.WriteLine($"[{sectionName}]");
                    foreach (var (k, v) in dict) writer.WriteLine($"{k} = {v}");
                    writer.WriteLine();
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[DiscoveryCache] Schreiben fehlgeschlagen: {ex.Message}"); }
        }
    }
}
