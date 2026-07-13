// Core/Services/HttpService.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ULM.Core.Models;
using ULM.Infrastructure;

namespace ULM.Core.Services
{
    public sealed class HttpService
    {
        private static readonly Lazy<HttpService> _lazy = new(() => new HttpService());
        public static HttpService Instance => _lazy.Value;

        private readonly HttpClient _client;
        private readonly Dictionary<string, (string Value, DateTimeOffset Expiry)> _cache = new();
        private readonly SemaphoreSlim _cacheLock = new(1, 1);

        /// <summary>
        /// Optionales GitHub-Personal-Access-Token (nur "public_repo"/keine Rechte nötig — dient
        /// ausschließlich dazu, das unauthentifizierte API-Limit von 60 auf 5000 Anfragen/Stunde
        /// anzuheben). Wird von MainViewModel aus ulm_settings.ini geladen. Ohne Token funktioniert
        /// alles wie bisher — nur eben mit dem niedrigeren Limit.
        /// </summary>
        public string? GitHubToken { get; set; }

        private void AddGitHubAuthHeader(HttpRequestMessage req)
        {
            if (!string.IsNullOrWhiteSpace(GitHubToken))
                req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {GitHubToken}");
        }

        private HttpService()
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                ConnectTimeout           = TimeSpan.FromSeconds(12),
                AllowAutoRedirect        = true,
                MaxAutomaticRedirections = 10,
            };
            _client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
            _client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            _client.DefaultRequestHeaders.TryAddWithoutValidation("Accept",
                "text/html,application/xhtml+xml,*/*;q=0.9");
            _client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language",
                "de-DE,de;q=0.9,en-US;q=0.8");
        }

        private async Task<string?> GetCachedAsync(string key)
        {
            await _cacheLock.WaitAsync().ConfigureAwait(false);
            try { if (_cache.TryGetValue(key, out var e) && DateTimeOffset.UtcNow < e.Expiry) return e.Value; _cache.Remove(key); return null; }
            finally { _cacheLock.Release(); }
        }
        private async Task SetCachedAsync(string key, string value)
        {
            await _cacheLock.WaitAsync().ConfigureAwait(false);
            try { _cache[key] = (value, DateTimeOffset.UtcNow.AddMinutes(5)); }
            finally { _cacheLock.Release(); }
        }

        // BUGFIX: Erreichbarkeits-Checks wurden bisher NIE zwischengespeichert — jeder Gesundheits-/
        // Versionscheck prüfte JEDE URL frisch, egal wie kurz der vorherige Check zurücklag. Bei
        // mehreren Checks kurz hintereinander (z.B. zwei Gesundheitschecks nur 20 Sekunden
        // auseinander, wie real beobachtet) verdoppelt das die Anfragen an denselben Origin-Server
        // in kurzer Zeit — und genau solche schnellen Wiederholungsanfragen lösen bei
        // Bot-/Anti-Scraping-Schutz (Cloudflare u.ä.) Tarpitting/Drosselung aus, die dann als
        // "nicht erreichbar" ankommt, obwohl die Datei tatsächlich verfügbar ist (mit curl isoliert
        // geprüft: funktioniert; 5x schnell hintereinander: hängt/blockiert). Ein kurzes Caching
        // (dieselbe 5-Minuten-TTL wie GetStringAsync) entschärft genau dieses Muster, ohne echte
        // Nichterreichbarkeit zu verschleiern.
        public async Task<bool> IsReachableAsync(string url, int timeoutSeconds = 6)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            string cacheKey = "reach:" + url;
            string? cached = await GetCachedAsync(cacheKey).ConfigureAwait(false);
            if (cached is not null) return cached == "1";
            bool ok;
            try
            {
                using var cts  = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                using var head = new HttpRequestMessage(HttpMethod.Head, url);
                using var hr   = await _client.SendAsync(head, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
                if ((int)hr.StatusCode < 400) ok = true;
                else
                {
                    using var get = new HttpRequestMessage(HttpMethod.Get, url);
                    get.Headers.TryAddWithoutValidation("Range", "bytes=0-0");
                    using var gr = await _client.SendAsync(get, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
                    ok = (int)gr.StatusCode < 400;
                }
            }
            catch { ok = false; }
            await SetCachedAsync(cacheKey, ok ? "1" : "0").ConfigureAwait(false);
            return ok;
        }

        public async Task<long> GetRemoteContentLengthAsync(string url, int timeoutSeconds = 8)
        {
            if (string.IsNullOrWhiteSpace(url)) return -1;
            try
            {
                using var cts  = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                using var head = new HttpRequestMessage(HttpMethod.Head, url);
                using var resp = await _client.SendAsync(head, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode && resp.Content.Headers.ContentLength.HasValue)
                    return resp.Content.Headers.ContentLength.Value;
            }
            catch { /* Fällt unten auf Range-GET zurück */ }

            // Manche Mirror/CDNs lehnen HEAD ab (405) oder liefern dabei keine Content-Length.
            // Ein Range-GET (nur 1 Byte) liefert die Gesamtgröße über Content-Range, ohne die
            // Datei tatsächlich herunterzuladen — funktioniert bei praktisch jedem Static-File-Server.
            try
            {
                using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                using var get  = new HttpRequestMessage(HttpMethod.Get, url);
                get.Headers.TryAddWithoutValidation("Range", "bytes=0-0");
                using var gr   = await _client.SendAsync(get, HttpCompletionOption.ResponseHeadersRead, cts2.Token).ConfigureAwait(false);
                if ((int)gr.StatusCode < 400 && gr.Content.Headers.ContentRange?.Length is long total && total > 0)
                    return total;
                if (gr.IsSuccessStatusCode && gr.Content.Headers.ContentLength is long full && full > 0)
                    return full; // Server ignoriert Range und liefert die volle Datei mit Content-Length
            }
            catch { /* endgültig nicht ermittelbar */ }
            return -1;
        }

        /// <summary>
        /// Ermittelt die Original-Dateigröße der Distro beim Anbieter — geprüft werden
        /// RemoteUrl → Url → Mirror1-5 (siehe IsoEntry.AllDownloadUrls), erste bekannte
        /// Content-Length gewinnt. Grundlage für die Vollständigkeits-Prüfung von lokalen
        /// und auf den Stick kopierten ISOs (kein Datenmüll durch abgebrochene Transfers).
        /// </summary>
        public async Task<long> GetExpectedSizeAsync(IsoEntry entry, int timeoutSeconds = 8)
        {
            foreach (string url in entry.AllDownloadUrls())
            {
                long len = await GetRemoteContentLengthAsync(url, timeoutSeconds).ConfigureAwait(false);
                if (len > 0) return len;
            }
            return -1;
        }

        public async Task<string?> GetStringAsync(string url, int timeoutSeconds = 15)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            string cacheKey = "get:" + url;
            string? cached  = await GetCachedAsync(cacheKey).ConfigureAwait(false);
            if (cached is not null) return cached;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                string text   = await _client.GetStringAsync(url, cts.Token).ConfigureAwait(false);
                await SetCachedAsync(cacheKey, text).ConfigureAwait(false);
                return text;
            }
            catch (Exception ex) { Debug.WriteLine($"[GetString] {url}: {ex.Message}"); return null; }
        }

        public async Task<double> MeasureDownloadSpeedMbpsAsync(CancellationToken ct, long testBytes = 4_000_000)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(10));
                var sw = Stopwatch.StartNew();
                using var resp = await _client.GetAsync($"https://speed.cloudflare.com/__down?bytes={testBytes}", HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return 0;
                using var stream = await resp.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
                byte[] buf = new byte[65_536]; long total = 0; int read;
                while ((read = await stream.ReadAsync(buf, cts.Token).ConfigureAwait(false)) > 0) total += read;
                sw.Stop();
                return (total * 8.0 / 1_000_000.0) / Math.Max(0.05, sw.Elapsed.TotalSeconds);
            }
            catch (Exception ex) { Debug.WriteLine($"[SpeedTest] {ex.Message}"); return 0; }
        }

        public async Task<string> GitHubResolveUrlAsync(string repo, string assetPattern)
        {
            if (string.IsNullOrWhiteSpace(repo) || string.IsNullOrWhiteSpace(assetPattern)) return string.Empty;
            string cacheKey = $"gh:{repo}:{assetPattern}";
            string? cached  = await GetCachedAsync(cacheKey).ConfigureAwait(false);
            if (cached is not null) return cached;
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repo}/releases/latest");
                req.Headers.TryAddWithoutValidation("Accept",     "application/vnd.github.v3+json");
                req.Headers.TryAddWithoutValidation("User-Agent", "ULM/2.27");
                AddGitHubAuthHeader(req);
                using var cts  = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                using var resp = await _client.SendAsync(req, cts.Token).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return string.Empty;
                string json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("assets", out var assets)) return string.Empty;
                string regex = "^" + Regex.Escape(assetPattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
                foreach (var asset in assets.EnumerateArray())
                {
                    string name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                    if (!Regex.IsMatch(name, regex, RegexOptions.IgnoreCase)) continue;
                    string url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? string.Empty : string.Empty;
                    if (string.IsNullOrWhiteSpace(url)) continue;
                    await SetCachedAsync(cacheKey, url).ConfigureAwait(false);
                    return url;
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[GitHub] {repo}: {ex.Message}"); }
            return string.Empty;
        }

        /// <summary>
        /// Prüft, ob eine neuere ULM-Version als GitHub-Release verfügbar ist — analog zu den
        /// Distro-Versionschecks, nur für ULM selbst. Rein informativ: löst nichts automatisch aus,
        /// der Aufrufer entscheidet, wie/ob der Hinweis angezeigt wird.
        /// </summary>
        public async Task<(bool HasUpdate, string LatestVersion, string ReleaseUrl)> CheckForUlmUpdateAsync(string currentVersion, string repo = "zwilling10/ULM")
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repo}/releases/latest");
                req.Headers.TryAddWithoutValidation("Accept",     "application/vnd.github.v3+json");
                req.Headers.TryAddWithoutValidation("User-Agent", $"ULM/{currentVersion}");
                AddGitHubAuthHeader(req);
                using var cts  = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                using var resp = await _client.SendAsync(req, cts.Token).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return (false, string.Empty, string.Empty);
                string json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                string tag = doc.RootElement.TryGetProperty("tag_name", out var t) ? t.GetString() ?? string.Empty : string.Empty;
                string url = doc.RootElement.TryGetProperty("html_url", out var u) ? u.GetString() ?? string.Empty : string.Empty;
                string latest = tag.TrimStart('v', 'V');
                if (string.IsNullOrWhiteSpace(latest)) return (false, string.Empty, string.Empty);
                return (IsVersionNewer(latest, currentVersion), latest, url);
            }
            catch (Exception ex) { Debug.WriteLine($"[UlmUpdateCheck] {ex.Message}"); return (false, string.Empty, string.Empty); }
        }

        public static string ExtractVersion(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            // "Release-Num-Sub.Build" (z.B. Fedora "Fedora-Workstation-Live-44-1.7.iso" oder
            // Rocky/Alma "-9-2.5.iso") — die blanke Release-Nummer VOR dem Punkt-Sub-Build ist die
            // eigentliche Version, nicht der nachfolgende Sub-Build. Muss vor der allgemeinen
            // Punkt-Erkennung geprüft werden, sonst gewinnt fälschlich der Sub-Build (z.B. "1.7"
            // statt "44") — das hat reale Versionsvergleiche verfälscht.
            var releaseBuild = Regex.Match(text, @"(?:^|[-_])(\d{1,3})-\d+\.\d+(?:[-_.]|$)");
            if (releaseBuild.Success && int.Parse(releaseBuild.Groups[1].Value) >= 10)
                return releaseBuild.Groups[1].Value;

            var m = Regex.Match(text, @"(\d+(?:\.\d+)+(?:[-_]\d+)?)");
            if (m.Success) return m.Groups[1].Value;
            string t2 = text.EndsWith(".iso", StringComparison.OrdinalIgnoreCase) ? text[..^4] : text;
            var m2 = Regex.Match(t2, @"(?:^|[-_\s])(\d{2,6})(?:[-_\s]|$)");
            if (m2.Success && long.TryParse(m2.Groups[1].Value, out long v) && v >= 10 && !(v >= 2000 && v <= 2099))
                return m2.Groups[1].Value;
            return string.Empty;
        }

        /// <summary>
        /// Vergleicht zwei Versions-STRINGS (z.B. "24.04", "7.9.1") numerisch, teilweise-für-teil.
        /// Public, damit Worker-Klassen (z.B. UpdateScanWorker) echte Versionsvergleiche nutzen
        /// können statt reiner Dateinamens-String-Gleichheit — zwei unterschiedliche Dateinamen
        /// bedeuten nicht automatisch, dass die neue Version tatsächlich NEUER ist.
        /// </summary>
        public static bool IsVersionNewer(string candidate, string current)
        {
            if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(current) ||
                string.Equals(candidate, current, StringComparison.OrdinalIgnoreCase)) return false;
            return VersionComparer.Instance.Compare(candidate, current) > 0;
        }

        private static readonly (string, string, string) Empty = (string.Empty, string.Empty, string.Empty);

        /// <summary>
        /// Entfernt alle Nicht-Alphanumerischen Zeichen und normalisiert Groß-/Kleinschreibung.
        /// Manuell hinzugefügte oder vom Stick importierte Einträge haben oft einen aus dem
        /// Dateinamen abgeleiteten Namen mit abweichender Zeichensetzung (Leerzeichen statt "-"/"_"/"!",
        /// z.B. "pop os 24.04 amd64 nvidia 12" statt "Pop!_OS 24.04 LTS NVIDIA"). Ohne Normalisierung
        /// griffen einige der Distro-Erkennungsmuster unten nur bei exakter Original-Zeichensetzung —
        /// solche Einträge fielen dann auf den langsameren/unzuverlässigeren Websuche-Fallback zurück,
        /// obwohl ein dedizierter Resolver für sie existiert.
        /// </summary>
        internal static string NormalizeForMatch(string s) =>
            Regex.Replace((s ?? string.Empty).ToLowerInvariant(), "[^a-z0-9]+", string.Empty);

        public async Task<(string Version, string Url, string Filename)> ResolveLatestAsync(IsoEntry entry)
        {
            if (entry is null) return Empty;
            string rawFl = entry.Filename.ToLowerInvariant();
            string nl = NormalizeForMatch(entry.Name);
            string fl = NormalizeForMatch(entry.Filename);
            (string, string, string) result = Empty;
            if (!string.IsNullOrWhiteSpace(entry.GithubRepo))
            {
                string rl = entry.GithubRepo.ToLowerInvariant();
                if      (rl.Contains("cachyos"))   result = await ResolveCachyOsAsync().ConfigureAwait(false);
                else if (rl.Contains("endeavour")) result = await ResolveEndeavourOsAsync().ConfigureAwait(false);
                else
                {
                    string asset = string.IsNullOrWhiteSpace(entry.GithubAsset) ? "*.iso" : entry.GithubAsset;
                    string ghUrl = await GitHubResolveUrlAsync(entry.GithubRepo, asset).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(ghUrl))
                    { string fname = Path.GetFileName(new Uri(ghUrl).AbsolutePath); result = (ExtractVersion(fname), ghUrl, fname); }
                }
            }
            if (result == Empty)
            {
                if      (nl.Contains("gamepack"))       result = await ResolveUbuntuGamepackAsync().ConfigureAwait(false);
                else if (nl.Contains("lubuntu"))        result = await ResolveLubuntuAsync().ConfigureAwait(false);
                else if (nl.Contains("ubuntu"))         result = await ResolveUbuntuDesktopAsync().ConfigureAwait(false);
                else if (nl.Contains("linuxmint") || fl.Contains("linuxmint"))       result = await ResolveMintAsync(DetectEdition(nl, fl)).ConfigureAwait(false);
                else if (nl.Contains("debian"))         result = await ResolveDebianLiveAsync(DetectEdition(nl, fl, "xfce")).ConfigureAwait(false);
                else if (nl.Contains("tails"))          result = await ResolveTailsAsync().ConfigureAwait(false);
                else if (nl.Contains("fedora"))         result = await ResolveFedoraAsync().ConfigureAwait(false);
                else if (nl.Contains("ultramarine"))     result = await ResolveUltramarineAsync(DetectUltramarineEdition(nl, fl)).ConfigureAwait(false);
                else if (nl.Contains("parrot"))         result = await ResolveParrotAsync().ConfigureAwait(false);
                else if (nl.Contains("zorin"))          result = await ResolveZorinAsync().ConfigureAwait(false);
                else if (nl.Contains("popos"))          result = await ResolvePopOsAsync().ConfigureAwait(false);
                else if (nl.Contains("manjaro"))        result = await ResolveManjaroAsync(DetectEdition(nl, fl, "kde")).ConfigureAwait(false);
                else if (nl.Contains("mxlinux") || rawFl.StartsWith("mx-"))          result = await ResolveMxLinuxAsync().ConfigureAwait(false);
                else if (nl.Contains("nobara"))         result = await ResolveNobaraAsync().ConfigureAwait(false);
                else if (nl.Contains("hiren"))          result = await ResolveHirensAsync().ConfigureAwait(false);
                else if (nl.Contains("drweb"))          result = await ResolveDrWebAsync().ConfigureAwait(false);
                else if (nl.Contains("finnix"))         result = await ResolveFinnixAsync().ConfigureAwait(false);
                else if (nl.Contains("cachyos"))        result = await ResolveCachyOsAsync().ConfigureAwait(false);
                else if (nl.Contains("endeavour"))      result = await ResolveEndeavourOsAsync().ConfigureAwait(false);
                else if (nl.Contains("systemrescue"))   result = await ResolveSourceForgeAsync("systemrescuecd", "/sysresccd-x86", @"systemrescue-[\d.]+-amd64\.iso").ConfigureAwait(false);
                else if (nl.Contains("gparted"))        result = await ResolveSourceForgeAsync("gparted", "/gparted-live-stable", @"gparted-live-[\.\d]+-\d+-amd64\.iso").ConfigureAwait(false);
                else if (nl.Contains("clonezilla"))     result = await ResolveSourceForgeAsync("clonezilla", "/clonezilla_live_stable", @"clonezilla-live-[\.\d]+-\d+-amd64\.iso").ConfigureAwait(false);
                else if (nl.Contains("rescuezilla"))    result = await ResolveSourceForgeAsync("rescuezilla", "/rescuezilla", @"rescuezilla-[\d.]+-64bit.*\.iso").ConfigureAwait(false);
                else if (nl.Contains("kodachi"))        result = await ResolveKodachiAsync().ConfigureAwait(false);
            }
            if (result != Empty) return result;

            var generic = await ResolveGenericAsync(entry).ConfigureAwait(false);
            // Kein dedizierter Resolver hat gegriffen — typischerweise ein manuell hinzugefügter
            // oder vom Stick importierter Eintrag ohne konfigurierte Url/GithubRepo. Eine hier
            // erfolgreich entdeckte Quelle dauerhaft im (persistenten) Url-Feld merken: künftige
            // Prüfungen starten dann direkt darüber statt erneut die langsame generische
            // Auflösung/Websuche zu durchlaufen — TryDiscoverNewerVersionAsync findet über diese
            // gemerkte URL automatisch auch künftige Versions-Updates, sobald der Anbieter eine
            // Verzeichnis-Auflistung nutzt (wie jeder dedizierte Resolver es täte, nur generisch).
            if (generic != Empty && string.IsNullOrWhiteSpace(entry.Url) && string.IsNullOrWhiteSpace(entry.GithubRepo))
                entry.Url = generic.Item2;
            return generic;
        }

        private async Task<(string, string, string)> ResolveGenericAsync(IsoEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(entry.GithubRepo))
            {
                string asset = string.IsNullOrWhiteSpace(entry.GithubAsset) ? "*.iso" : entry.GithubAsset;
                string ghUrl = await GitHubResolveUrlAsync(entry.GithubRepo, asset).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(ghUrl))
                { string fname = Path.GetFileName(new Uri(ghUrl).AbsolutePath); return (ExtractVersion(fname), ghUrl, fname); }
            }
            // Alle 5 Mirror einbeziehen (nicht nur 1-3) — jede zusätzliche konfigurierte
            // Quelle erhöht die Chance, dass die Distro auch bei defekter Primär-URL
            // oder abgeschaltetem Erst-Mirror noch automatisch gefunden wird.
            string[] allUrls = entry.AllDownloadUrls().ToArray();
            if (allUrls.Length > 0)
            {
                foreach (string baseUrl in allUrls)
                {
                    var sfm = Regex.Match(baseUrl, @"(?:sourceforge\.net|dl\.sourceforge\.net)/(?:project|projects)/([^/?#]+)", RegexOptions.IgnoreCase);
                    if (!sfm.Success) continue;
                    var sfResult = await TryResolveSourceForgeProjectAsync(sfm.Groups[1].Value, entry.Filename).ConfigureAwait(false);
                    if (sfResult != Empty) return sfResult;
                }
                if (!string.IsNullOrWhiteSpace(entry.Filename))
                    foreach (string baseUrl in allUrls)
                    { var disc = await TryDiscoverNewerVersionAsync(baseUrl, entry.Filename).ConfigureAwait(false); if (disc != Empty) return disc; }
                foreach (string url in allUrls)
                    if (await IsReachableAsync(url, 8).ConfigureAwait(false))
                        return (ExtractVersion(entry.Filename), url, entry.Filename);
            }

            // Generischer Automatismus für UNBEKANNTE Distros (importiert/manuell hinzugefügt, kein
            // dedizierter Resolver, keine konfigurierte Url): über DistroWatch die offizielle
            // Projekt-Homepage finden statt einen Einzelfall-Resolver pro Distro zu schreiben —
            // DistroWatch listet praktisch jede existierende Linux-Distribution mit einem
            // verlässlichen, strukturierten Homepage-Link. Deutlich präziser als die reine
            // Websuche unten, da sie zuerst die tatsächliche Projektseite findet statt zu hoffen,
            // dass irgendein Suchtreffer zufällig einen .iso-Link enthält.
            var dw = await ResolveViaDistroWatchAsync(entry).ConfigureAwait(false);
            if (dw != Empty) return dw;

            // Allerletzter Fallback — nur wenn ALLE oben genannten, schnelleren und präziseren
            // Strategien nichts gefunden haben: eine echte Websuche, wie sie ein Mensch machen
            // würde. Läuft bewusst auch dann, wenn gar keine URL konfiguriert ist (allUrls leer)
            // — genau dort hatte die automatische Auflösung bisher NULL Chancen.
            return await ResolveViaWebSearchAsync(entry).ConfigureAwait(false);
        }

        /// <summary>
        /// Generischer Automatismus für JEDE unbekannte Distro (kein Einzelfall-Code pro Distro):
        /// 1. Über eine gezielte Suche ("site:distrowatch.com") die DistroWatch-Seite dieser Distro
        ///    finden — funktioniert unabhängig davon, wie unsauber/importiert der Name ist, da
        ///    DuckDuckGo (nicht DistroWatchs eigene, kriterienbasierte Suche) die Fuzzy-Zuordnung
        ///    übernimmt.
        /// 2. Von dort das strukturierte "Home Page"-Feld extrahieren (bei DistroWatch für nahezu
        ///    jede gelistete Distro vorhanden).
        /// 3. Die echte Projekt-Homepage nach .iso-Links durchsuchen; findet sich dort keiner,
        ///    einen Link mit "download" im Text/href eine Ebene tiefer verfolgen — der Klickpfad,
        ///    den auch ein Mensch nehmen würde.
        /// </summary>
        private async Task<(string, string, string)> ResolveViaDistroWatchAsync(IsoEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.Name)) return Empty;
            try
            {
                string query = Uri.EscapeDataString($"{FirstKeyword(entry.Name)} site:distrowatch.com");
                string? searchHtml = await GetStringAsync($"https://html.duckduckgo.com/html/?q={query}", 15).ConfigureAwait(false);
                if (searchHtml is null) return Empty;

                var dwCandidates = Regex.Matches(searchHtml, @"class=""result__a""[^>]*href=""([^""]+)""", RegexOptions.IgnoreCase)
                    .Cast<Match>().Select(m => ResolveDuckDuckGoRedirect(m.Groups[1].Value))
                    .Where(u => u.Contains("distrowatch.com", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase).Take(3).ToList();
                if (dwCandidates.Count == 0) return Empty;

                string? homepage = null;
                foreach (string dwUrl in dwCandidates)
                {
                    string? dwHtml = await GetStringAsync(dwUrl, 12).ConfigureAwait(false);
                    if (dwHtml is null) continue;
                    // Verifiziertes Muster (DistroWatchs Distro-Detailtabelle nutzt <th>/<td>-Paare,
                    // NICHT zwei <td>-Zellen): <th class="Info">Home Page</th><td class="Info">
                    // <a href="...">...</a></td> — gegen eine echte Seite (garuda) geprüft.
                    var m = Regex.Match(dwHtml, @"Home\s*Page</th>\s*<td[^>]*>\s*<a\s+href=""([^""]+)""", RegexOptions.IgnoreCase);
                    if (m.Success) { homepage = WebUtility.HtmlDecode(m.Groups[1].Value); break; }
                }
                if (string.IsNullOrWhiteSpace(homepage)) return Empty;

                string? homeHtml = await GetStringAsync(homepage, 12).ConfigureAwait(false);
                if (homeHtml is null) return Empty;

                // SourceForge zuerst versuchen (direkt auf der Homepage) — präziser als rohes Link-
                // Scraping, siehe TryResolveSourceForgeProjectAsync.
                string? sfSlug = TryFindSourceForgeProjectSlug(homeHtml);
                if (sfSlug != null)
                {
                    var sfResult = await TryResolveSourceForgeProjectAsync(sfSlug, entry.Filename).ConfigureAwait(false);
                    if (sfResult != Empty) return sfResult;
                }

                var (links, pageForLinks, pageHtml) = await FindIsoLinksFollowingDownloadLinkAsync(homepage, homeHtml).ConfigureAwait(false);
                if (links.Count == 0)
                {
                    // Der Download-Link-Sprung landete auf einer neuen Seite (z.B. Q4OS: Homepage →
                    // "Download" → Unterseite, die erst DORT auf SourceForge verlinkt) — auch diese
                    // noch auf SourceForge prüfen, bevor endgültig aufgegeben wird.
                    if (pageForLinks != homepage)
                    {
                        string? sfSlug2 = TryFindSourceForgeProjectSlug(pageHtml);
                        if (sfSlug2 != null)
                        {
                            var sfResult2 = await TryResolveSourceForgeProjectAsync(sfSlug2, entry.Filename).ConfigureAwait(false);
                            if (sfResult2 != Empty) return sfResult2;
                        }
                    }
                    return Empty;
                }

                string best = !string.IsNullOrWhiteSpace(entry.Filename)
                    ? FindBestIsoMatch(links, entry.Filename)
                    : links.FirstOrDefault(l => l.Contains(FirstKeyword(entry.Name), StringComparison.OrdinalIgnoreCase)) ?? links[0];
                if (string.IsNullOrEmpty(best)) return Empty;

                string bestFname = Path.GetFileName(best.TrimEnd('/'));
                string url = best.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? best
                    : Uri.TryCreate(new Uri(pageForLinks), best, out var abs) ? abs.ToString() : pageForLinks.TrimEnd('/') + "/" + best.TrimStart('/');
                if (!await IsReachableAsync(url, 8).ConfigureAwait(false)) return Empty;

                // BUGFIX: Distros ohne konfigurierte Mirror1-5 (z.B. über "ISO suchen" neu
                // hinzugefügte, noch komplett unbekannte Einträge) hatten bisher NIE eine zweite
                // Quelle zum Ausweichen — landet die Homepage zufällig auf einem lahmen CDN
                // (beobachtet: <1 MB/s, mehrere Stunden ETA), gibt es keinen Fallback UND kein
                // Mirror-Race (DownloadWorker vergleicht nur, wenn es >1 URL gibt). Findet die
                // Homepage mehrere .iso-Links (z.B. mehrere CDN-Spiegel oder Architekturen), werden
                // bis zu 2 weitere hier dauerhaft als Mirror1/2 gemerkt (nur wenn dort noch nichts
                // Nutzerdefiniertes steht) — dieselbe Persistenz-Logik wie schon für entry.Url oben
                // im Aufrufer (ResolveLatestAsync). Reachability wird hier NICHT geprüft (teuer,
                // mehrere zusätzliche Requests) — das übernimmt ohnehin das bestehende Mirror-Race
                // vor dem eigentlichen Download.
                if (links.Count > 1)
                {
                    var extras = links.Where(l => l != best)
                        .Select(l => l.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? l
                            : Uri.TryCreate(new Uri(pageForLinks), l, out var eabs) ? eabs.ToString() : pageForLinks.TrimEnd('/') + "/" + l.TrimStart('/'))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(2).ToList();
                    if (extras.Count > 0 && string.IsNullOrWhiteSpace(entry.Mirror1)) entry.Mirror1 = extras[0];
                    if (extras.Count > 1 && string.IsNullOrWhiteSpace(entry.Mirror2)) entry.Mirror2 = extras[1];
                }
                return (ExtractVersion(bestFname), url, bestFname);
            }
            catch (Exception ex) { Debug.WriteLine($"[DistroWatch] {entry.Name}: {ex.Message}"); return Empty; }
        }

        /// <summary>
        /// Sucht auf einer Seite nach direkten .iso-Links; findet sich keiner, wird ein Link mit
        /// "download" im sichtbaren Text eine Ebene tiefer verfolgt und dort erneut gesucht — viele
        /// Projekt-Homepages zeigen die ISO nicht direkt an, sondern erst auf einer eigenen
        /// Download-Unterseite. Gemeinsam genutzt von ResolveViaDistroWatchAsync und
        /// ResolveViaWebSearchAsync, damit beide Automatismen gleich robust sind.
        /// </summary>
        private async Task<(List<string> Links, string PageUrl, string PageHtml)> FindIsoLinksFollowingDownloadLinkAsync(string pageUrl, string pageHtml)
        {
            var links = ExtractIsoLinks(pageHtml);
            if (links.Count > 0) return (links, pageUrl, pageHtml);

            var dlMatch = Regex.Match(pageHtml, @"<a\s+[^>]*href=""([^""]+)""[^>]*>\s*(?:[^<]{0,40}?(?:download|herunterladen))", RegexOptions.IgnoreCase);
            if (!dlMatch.Success) return (links, pageUrl, pageHtml);
            string dlHref = WebUtility.HtmlDecode(dlMatch.Groups[1].Value);
            string dlUrl = Uri.TryCreate(new Uri(pageUrl), dlHref, out var abs) ? abs.ToString() : dlHref;
            if (string.Equals(dlUrl, pageUrl, StringComparison.OrdinalIgnoreCase)) return (links, pageUrl, pageHtml);

            string? dlHtml = await GetStringAsync(dlUrl, 12).ConfigureAwait(false);
            return dlHtml is null ? (links, pageUrl, pageHtml) : (ExtractIsoLinks(dlHtml), dlUrl, dlHtml);
        }

        /// <summary>
        /// SourceForge ist ein sehr verbreitetes Hosting für kleinere Community-Distros (siehe die
        /// dedizierten Resolver für Rescuezilla/GParted/Clonezilla/SystemRescue/Ubuntu-GamePack).
        /// Wird auf einer gecrawlten Seite ein Link zu einem SourceForge-Projekt gefunden, liefert
        /// diese bereits bewährte RSS-Feed-Auflösung präzisere und zuverlässigere Ergebnisse als
        /// reines .iso-Link-Scraping (SourceForges eigene Download-Seiten sind oft JS-lastig).
        /// </summary>
        private async Task<(string, string, string)> TryResolveSourceForgeProjectAsync(string project, string? currentFilename)
        {
            string? rss = await GetStringAsync($"https://sourceforge.net/projects/{project}/rss?path=/").ConfigureAwait(false);
            if (rss is null) return Empty;
            var paths = Regex.Matches(rss, @"<title><!\[CDATA\[(/[^\]]*\.iso)\]\]></title>").Cast<Match>().Select(m => m.Groups[1].Value.Trim()).ToList();
            if (paths.Count == 0) return Empty;
            string best = string.IsNullOrWhiteSpace(currentFilename) ? paths[0] : FindBestIsoMatch(paths, currentFilename);
            if (string.IsNullOrEmpty(best)) best = paths[0];
            string fname = best.Split('/').Last();
            string dlUrl = $"https://master.dl.sourceforge.net/project/{project}{best}?viasf=1";
            return await IsReachableAsync(dlUrl, 12).ConfigureAwait(false) ? (ExtractVersion(fname), dlUrl, fname) : Empty;
        }

        private static string? TryFindSourceForgeProjectSlug(string html)
        {
            var m = Regex.Match(html, @"sourceforge\.net/(?:project|projects)/([^/""'?#]+)", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : null;
        }

        private async Task<(string, string, string)> TryDiscoverNewerVersionAsync(string isoUrl, string currentFilename)
        {
            string currentVer = ExtractVersion(currentFilename); if (string.IsNullOrEmpty(currentVer)) return Empty;
            try
            {
                var uri = new Uri(isoUrl);
                string parentUrl = isoUrl[..isoUrl.LastIndexOf('/')] + "/";
                string? parentHtml = await GetStringAsync(parentUrl).ConfigureAwait(false);
                if (parentHtml is not null)
                {
                    string filePattern = Regex.Escape(currentFilename).Replace(Regex.Escape(currentVer), @"([\d]+(?:\.[\d]+)+(?:[-_][\d]+)?)");
                    var newer = Regex.Matches(parentHtml, filePattern, RegexOptions.IgnoreCase).Cast<Match>().Select(m => m.Value).Where(f => IsVersionNewer(ExtractVersion(f), currentVer)).OrderByDescending(f => ExtractVersion(f), VersionComparer.Instance).ToList();
                    foreach (string nf in newer) { string url = parentUrl + nf; if (await IsReachableAsync(url, 8).ConfigureAwait(false)) return (ExtractVersion(nf), url, nf); }

                    // Fallback: die strikte Namens-Substitution fand nichts — meist weil sich das
                    // Dateinamensschema geändert hat (neuer Codename, andere Edition, Build-Suffix).
                    // Statt aufzugeben: alle .iso-Links der Seite generisch nach dem besten Treffer
                    // durchsuchen (Stem-Ähnlichkeit + Versionsvergleich, siehe FindBestIsoMatch —
                    // dieselbe Logik, die bereits für SourceForge-Feeds verwendet wird).
                    var byListing = await TryBestMatchFromListingAsync(parentUrl, parentHtml, currentFilename, currentVer).ConfigureAwait(false);
                    if (byListing != Empty) return byListing;
                }
                if (uri.Segments.Length >= 3)
                {
                    string gpPath = string.Join("", uri.Segments.Take(uri.Segments.Length - 2));
                    string gpUrl  = $"{uri.Scheme}://{uri.Host}{gpPath}";
                    string? gpHtml = await GetStringAsync(gpUrl).ConfigureAwait(false);
                    if (gpHtml is not null)
                    {
                        var versions = Regex.Matches(gpHtml, @"href=""(?:[^""]*/)?([\d]+(?:\.[\d]+)+(?:[-_][\d]+)?)/?""").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().Where(v => IsVersionNewer(v, currentVer)).OrderByDescending(v => v, VersionComparer.Instance).ToList();
                        foreach (string newVer in versions.Take(5))
                        {
                            string newFn = ReplaceFirstOccurrence(currentFilename, currentVer, newVer);
                            string candidateUrl = gpUrl + $"{newVer}/{newFn}";
                            if (await IsReachableAsync(candidateUrl, 8).ConfigureAwait(false)) return (newVer, candidateUrl, newFn);

                            // Substituierter Dateiname existiert in dieser Versions-Unterordner nicht
                            // (mehr) — die Verzeichnis-Auflistung selbst nach dem besten .iso-Treffer
                            // durchsuchen, statt den Kandidaten zu verwerfen.
                            string verDirUrl = $"{gpUrl}{newVer}/";
                            string? verHtml = await GetStringAsync(verDirUrl).ConfigureAwait(false);
                            if (verHtml is null) continue;
                            var byVerListing = await TryBestMatchFromListingAsync(verDirUrl, verHtml, currentFilename, currentVer).ConfigureAwait(false);
                            if (byVerListing != Empty) return byVerListing;
                        }
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[TryDiscoverNewerVersion] {isoUrl}: {ex.Message}"); }
            return Empty;
        }

        /// <summary>
        /// Generischer Fallback für TryDiscoverNewerVersionAsync: durchsucht eine beliebige
        /// Verzeichnis-Auflistung (Apache/Nginx-Autoindex-Stil oder jede andere HTML-Seite mit
        /// &lt;a href="...iso"&gt;-Links) nach dem zur aktuellen Datei passendsten .iso — auch
        /// wenn sich das exakte Namensschema geändert hat. Verifiziert den Kandidaten immer per
        /// HEAD/Range-Request, bevor er als Treffer zurückgegeben wird (keine Blindschüsse).
        /// </summary>
        private async Task<(string, string, string)> TryBestMatchFromListingAsync(string pageUrl, string pageHtml, string currentFilename, string currentVer)
        {
            var links = ExtractIsoLinks(pageHtml);
            if (links.Count == 0) return Empty;
            string best = FindBestIsoMatch(links, currentFilename);
            if (string.IsNullOrEmpty(best)) return Empty;
            string fname = Path.GetFileName(best.TrimEnd('/'));
            bool isNewer = IsVersionNewer(ExtractVersion(fname), currentVer);
            if (!isNewer && !string.Equals(fname, currentFilename, StringComparison.OrdinalIgnoreCase)) return Empty;
            string url = Uri.TryCreate(new Uri(pageUrl), best, out var abs) ? abs.ToString() : pageUrl.TrimEnd('/') + "/" + best.TrimStart('/');
            return await IsReachableAsync(url, 8).ConfigureAwait(false) ? (ExtractVersion(fname), url, fname) : Empty;
        }

        // Viele Anbieter zeigen den Download nicht als klickbaren <a href>-Link, sondern nur als
        // reinen Text (z.B. "wget https://.../datei.iso" in einem Code-Block) — genau dieses Muster
        // hat bei Ultramarine die reine href-Suche ins Leere laufen lassen, obwohl die echte URL
        // direkt auf der offiziellen Download-Seite stand. Absolute .iso-URLs im Fließtext ergänzend
        // erfassen deckt dieses Muster generisch für JEDEN Anbieter ab, ohne Sonderfälle pro Distro.
        //
        // SICHERHEIT: Diese Funktion wird sowohl auf bekannten Verzeichnis-Auflistungen (bereits
        // vertrauenswürdige, in der DB konfigurierte Mirror-URLs) als auch — über
        // ResolveViaWebSearchAsync — auf beliebigen von DuckDuckGo zurückgegebenen Trefferseiten
        // aufgerufen. Im zweiten Fall ist die Quelle NICHT vertrauenswürdig: eine kompromittierte
        // oder SEO-Spam-Seite könnte theoretisch eine passend aussehende, aber bösartige .iso-URL
        // im Text platzieren. Die einzige Absicherung ist die nachgelagerte Versions-/Namens-
        // Plausibilitätsprüfung (FindBestIsoMatch/IsVersionNewer) plus Erreichbarkeitsprüfung —
        // KEINE Inhaltsverifikation (siehe Kommentar bei DownloadAsync). Bei sehr exotischen/neuen
        // Distros ohne dedizierten Resolver ist der Websuche-Pfad daher der am wenigsten vertrauens-
        // würdige Teil der gesamten Auflösungskette.
        private static List<string> ExtractIsoLinks(string html)
        {
            var hrefLinks = Regex.Matches(html, @"href\s*=\s*[""']([^""'>]+\.iso)[""']", RegexOptions.IgnoreCase)
                .Cast<Match>().Select(m => WebUtility.HtmlDecode(m.Groups[1].Value));
            var textLinks = Regex.Matches(html, @"https?://[^\s""'<>]+\.iso", RegexOptions.IgnoreCase)
                .Cast<Match>().Select(m => m.Value);
            return hrefLinks.Concat(textLinks).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Letzte Rückfallebene, wenn dedizierte Resolver, GitHub-API, SourceForge und generische
        /// Verzeichnis-Erkennung alle nichts gefunden haben: eine echte Websuche über DuckDuckGos
        /// HTML-only-Endpunkt (kein API-Key nötig, kein JavaScript erforderlich — funktioniert wie
        /// eine normale Browser-Suchanfrage). Aus den Trefferlinks wird entweder direkt eine .iso
        /// übernommen oder die verlinkte Seite nach dem besten .iso-Treffer durchsucht (dieselbe
        /// Scoring-Logik wie bei der generischen Verzeichnis-Erkennung). JEDER Kandidat wird vor
        /// der Annahme per Erreichbarkeitsprüfung verifiziert — kein ungeprüfter Download-Start.
        ///
        /// Kein Ersatz für eine Garantie: Trefferqualität hängt von den Suchergebnissen ab, und
        /// die Struktur des Such-Endpunkts kann sich ändern. Es ist bewusst der letzte, nicht der
        /// einzige Versuch — alle präziseren Strategien laufen vorher.
        /// </summary>
        private async Task<(string, string, string)> ResolveViaWebSearchAsync(IsoEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.Name)) return Empty;
            try
            {
                string query = Uri.EscapeDataString($"{entry.Name} iso download");
                string? html = await GetStringAsync($"https://html.duckduckgo.com/html/?q={query}", 15).ConfigureAwait(false);
                if (html is null) return Empty;

                var resultPages = Regex.Matches(html, @"class=""result__a""[^>]*href=""([^""]+)""", RegexOptions.IgnoreCase)
                    .Cast<Match>().Select(m => ResolveDuckDuckGoRedirect(m.Groups[1].Value))
                    .Where(u => Uri.IsWellFormedUriString(u, UriKind.Absolute))
                    .Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList();

                // Kandidaten sammeln: direkte .iso-Treffer aus der Ergebnisliste selbst, plus
                // .iso-Links von den (nicht-.iso) Ergebnisseiten. Die gesamte Sammlung wird EINMAL
                // gemeinsam bewertet, statt den ersten erreichbaren Treffer blind zu übernehmen —
                // sonst könnte ein uralter, zufällig noch online stehender Build vor der
                // tatsächlich aktuellen Version gewinnen.
                var pool = new List<(string Link, string PageUrl)>();
                foreach (string candidate in resultPages)
                {
                    if (candidate.EndsWith(".iso", StringComparison.OrdinalIgnoreCase))
                    { pool.Add((candidate, candidate)); continue; }

                    string? pageHtml = await GetStringAsync(candidate, 12).ConfigureAwait(false);
                    if (pageHtml is null) continue;
                    // Folgt bei Bedarf einem "Download"-Link eine Ebene tiefer (siehe
                    // FindIsoLinksFollowingDownloadLinkAsync) — viele Trefferseiten sind die
                    // Projekt-Homepage, nicht die eigentliche Download-Unterseite.
                    var (foundLinks, foundOn, _) = await FindIsoLinksFollowingDownloadLinkAsync(candidate, pageHtml).ConfigureAwait(false);
                    foreach (string link in foundLinks) pool.Add((link, foundOn));
                }
                if (pool.Count == 0) return Empty;

                string keyword = FirstKeyword(entry.Name);
                string best = !string.IsNullOrWhiteSpace(entry.Filename)
                    ? FindBestIsoMatch(pool.Select(p => p.Link).ToList(), entry.Filename)
                    : pool.Select(p => p.Link).FirstOrDefault(l => l.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
                if (string.IsNullOrEmpty(best)) return Empty;

                // Gleiche Sicherheitsschranke wie überall sonst in der Resolver-Kette: ein anderer
                // Dateiname ist nur dann ein Treffer, wenn er nachweislich NEUER ist (oder exakt der
                // bisherige) — verhindert, dass eine über die Suche gefundene ältere Version die
                // aktuell bekannte verdrängt.
                string bestFname = Path.GetFileName(best.TrimEnd('/'));
                if (!string.IsNullOrWhiteSpace(entry.Filename))
                {
                    bool isNewer = IsVersionNewer(ExtractVersion(bestFname), ExtractVersion(entry.Filename));
                    if (!isNewer && !string.Equals(bestFname, entry.Filename, StringComparison.OrdinalIgnoreCase)) return Empty;
                }

                string basePage = pool.First(p => p.Link == best).PageUrl;
                string url = best.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? best
                    : Uri.TryCreate(new Uri(basePage), best, out var abs) ? abs.ToString() : basePage.TrimEnd('/') + "/" + best.TrimStart('/');
                return await IsReachableAsync(url, 8).ConfigureAwait(false) ? (ExtractVersion(bestFname), url, bestFname) : Empty;
            }
            catch (Exception ex) { Debug.WriteLine($"[WebSearch] {entry.Name}: {ex.Message}"); }
            return Empty;
        }

        // DuckDuckGos HTML-Ergebnisse verlinken über einen Redirect (/l/?uddg=<urlencoded-ziel>&...)
        // statt direkt auf die Zielseite — die echte URL steckt im "uddg"-Parameter.
        private static string ResolveDuckDuckGoRedirect(string href)
        {
            var m = Regex.Match(href, @"uddg=([^&]+)");
            string real = m.Success ? Uri.UnescapeDataString(m.Groups[1].Value) : href;
            if (real.StartsWith("//", StringComparison.Ordinal)) real = "https:" + real;
            return real;
        }

        // Erstes markante Wort im Distro-Namen (z.B. "Ubuntu" aus "Ubuntu 24.04 Desktop") als
        // grober Filter, wenn kein bekannter Dateiname für einen präziseren Score vorliegt.
        private static string FirstKeyword(string name)
        {
            foreach (string w in name.Split(' ', '-', '_', '(', ')'))
                if (w.Length > 3 && !char.IsDigit(w[0])) return w;
            return name;
        }

        private static string FindBestIsoMatch(IList<string> paths, string currentFilename)
        {
            if (string.IsNullOrEmpty(currentFilename)||paths.Count==0) return string.Empty;
            string cb=Path.GetFileNameWithoutExtension(currentFilename); string cv=ExtractVersion(currentFilename);
            string stem=string.IsNullOrEmpty(cv)?cb:cb.Replace(cv,string.Empty).Trim('-','_','.');
            return paths.Where(p=>p.EndsWith(".iso",StringComparison.OrdinalIgnoreCase))
                .Select(p=>new{Path=p,File=Path.GetFileName(p),Score=(Path.GetFileName(p).Contains(stem,StringComparison.OrdinalIgnoreCase)?10:0)+(IsVersionNewer(ExtractVersion(Path.GetFileName(p)),cv)?5:0)+(string.Equals(Path.GetFileName(p),currentFilename,StringComparison.OrdinalIgnoreCase)?3:0)})
                .Where(x=>x.Score>0).OrderByDescending(x=>x.Score).ThenByDescending(x=>ExtractVersion(x.File),VersionComparer.Instance)
                .Select(x=>x.Path).FirstOrDefault()??string.Empty;
        }

        private static string ReplaceFirstOccurrence(string source, string find, string replace)
        { int pos=source.IndexOf(find,StringComparison.Ordinal); return pos<0?source:source[..pos]+replace+source[(pos+find.Length)..]; }

        private static string DetectEdition(string nl, string fl, string def="cinnamon")
        { foreach(string ed in new[]{"cinnamon","mate","xfce","gnome","kde"})if(nl.Contains(ed)||fl.Contains(ed))return ed; return def; }

        private static string DetectUltramarineEdition(string nl, string fl)
        { foreach(string ed in new[]{"budgie","gnome","plasma","xfce"})if(nl.Contains(ed)||fl.Contains(ed))return ed; return "plasma"; }

        private static string? FindLatestVersion(string html, string pattern=@"href=""(\d+(?:\.\d+)*)/?""")
        { var hits=Regex.Matches(html,pattern).Cast<Match>().Select(m=>m.Groups[1].Value).Distinct().ToList(); return hits.Count==0?null:hits.OrderByDescending(v=>v,VersionComparer.Instance).First(); }

        private sealed class VersionComparer : IComparer<string>
        {
            public static readonly VersionComparer Instance = new();
            public int Compare(string? x, string? y)
            { int[] px=Parts(x),py=Parts(y); int len=Math.Max(px.Length,py.Length); for(int i=0;i<len;i++){int a=i<px.Length?px[i]:0,b=i<py.Length?py[i]:0;if(a!=b)return a.CompareTo(b);}return 0; }
            // Auch auf '-'/'_' splitten, nicht nur '.': ein Versions-String wie "26.0.4-260327"
            // (Punkt-Version + Bindestrich-Build, z.B. Manjaro) ließ beim reinen Punkt-Split das
            // letzte Segment "4-260327" als nicht-parsbare Ganzzahl auf 0 kollabieren — dadurch
            // wurde "26.0.4" (ohne Build-Suffix) fälschlich als NEUER als "26.0.4-260327" bewertet,
            // obwohl beide dieselbe Version sind. Mit vollständiger Segmentierung vergleicht dieselbe
            // Version jetzt korrekt als gleich (bzw. der fehlende Build-Teil als älter, nie als neuer).
            private static int[] Parts(string? s)=>(s??string.Empty).Split('.','-','_').Select(p=>int.TryParse(p,out int n)?n:0).ToArray();
        }

        // ── Distro-Resolver ───────────────────────────────────────────────

        private async Task<(string, string, string)> ResolveUbuntuDesktopAsync()
        {
            string? html = await GetStringAsync("https://releases.ubuntu.com/").ConfigureAwait(false); if (html is null) return Empty;
            var vers = Regex.Matches(html, @"href=""(\d+\.\d+(?:\.\d+)?)/""").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().OrderByDescending(v => v, VersionComparer.Instance).ToList();
            foreach (string ver in vers) { string fname=$"ubuntu-{ver}-desktop-amd64.iso"; string url=$"https://releases.ubuntu.com/{ver}/{fname}"; if(await IsReachableAsync(url,8).ConfigureAwait(false))return(ver,url,fname); }
            return Empty;
        }

        private async Task<(string, string, string)> ResolveLubuntuAsync()
        {
            string? html = await GetStringAsync("https://cdimage.ubuntu.com/lubuntu/releases/").ConfigureAwait(false); if (html is null) return Empty;
            var vers = Regex.Matches(html, @"href=""(\d+\.\d+(?:\.\d+)?)/""").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().OrderByDescending(v => v, VersionComparer.Instance).ToList();
            foreach (string ver in vers) { string relUrl=$"https://cdimage.ubuntu.com/lubuntu/releases/{ver}/release/"; string? h2=await GetStringAsync(relUrl).ConfigureAwait(false); if(h2 is null)continue; var mm=Regex.Match(h2,@"(lubuntu-[\d.]+(?:\.\d+)?-desktop-amd64\.iso)"); if(!mm.Success)continue; string fname=mm.Groups[1].Value; return(ExtractVersion(fname),relUrl+fname,fname); }
            return Empty;
        }

        private async Task<(string, string, string)> ResolveMintAsync(string edition)
        {
            foreach (string src in new[]{"https://www.linuxmint.com/download.php","https://mirrors.edge.kernel.org/linuxmint/stable/"})
            {
                string? html=await GetStringAsync(src).ConfigureAwait(false); if(html is null)continue;
                var vers=Regex.Matches(html,@"\blinuxmint-?(\d+\.\d+)\b",RegexOptions.IgnoreCase).Cast<Match>().Select(m=>m.Groups[1].Value).Concat(Regex.Matches(html,@"href=""(\d+\.\d+)/""").Cast<Match>().Select(m=>m.Groups[1].Value)).Distinct().OrderByDescending(v=>v,VersionComparer.Instance).ToList();
                if(vers.Count==0)continue; string ver=vers[0]; string fname=$"linuxmint-{ver}-{edition}-64bit.iso";
                return(ver,$"https://pub.linuxmint.io/stable/{ver}/{fname}",fname);
            }
            return Empty;
        }

        private async Task<(string, string, string)> ResolveDebianLiveAsync(string edition)
        {
            foreach (string m in new[]{"https://ftp.halifax.rwth-aachen.de/debian-cd/current-live/amd64/iso-hybrid/","https://ftp.fau.de/debian-cd/current-live/amd64/iso-hybrid/","https://cdimage.debian.org/debian-cd/current-live/amd64/iso-hybrid/"})
            {
                string? html=await GetStringAsync(m).ConfigureAwait(false); if(html is null)continue;
                var hits=Regex.Matches(html,$@"(debian-live-[\d.]+-amd64-{Regex.Escape(edition)}\.iso)").Cast<Match>().Select(x=>x.Groups[1].Value).ToList();
                if(hits.Count==0)continue; string fname=hits.OrderByDescending(f=>f).First();
                return(ExtractVersion(fname),m+fname,fname);
            }
            return Empty;
        }

        private async Task<(string, string, string)> ResolveTailsAsync()
        {
            foreach (string m in new[]{"https://ftp.halifax.rwth-aachen.de/tails/stable/","https://mirrors.dotsrc.org/tails/stable/","https://ftp.fau.de/tails/stable/"})
            {
                string? html=await GetStringAsync(m).ConfigureAwait(false); if(html is null)continue;
                var vers=Regex.Matches(html,@"href=""tails-amd64-([\d.]+)/""").Cast<Match>().Select(x=>x.Groups[1].Value).Distinct().OrderByDescending(v=>v,VersionComparer.Instance).ToList();
                if(vers.Count==0)continue; string ver=vers[0]; string fname=$"tails-amd64-{ver}.iso";
                return(ver,$"{m}tails-amd64-{ver}/{fname}",fname);
            }
            return Empty;
        }

        private async Task<(string, string, string)> ResolveFedoraAsync()
        {
            foreach (string mirror in new[]{"https://ftp.fau.de/fedora/linux/releases/","https://ftp.halifax.rwth-aachen.de/fedora/linux/releases/"})
            {
                string? html=await GetStringAsync(mirror).ConfigureAwait(false); if(html is null)continue;
                var intVers=Regex.Matches(html,@"href=""(\d+)/""").Cast<Match>().Select(x=>x.Groups[1].Value).Where(v=>int.TryParse(v,out _)).Select(int.Parse).OrderByDescending(v=>v).ToList();
                if(intVers.Count==0)continue; string latest=intVers[0].ToString(); string isoDir=$"{mirror}{latest}/Workstation/x86_64/iso/";
                string? html2=await GetStringAsync(isoDir).ConfigureAwait(false); if(html2 is null)continue;
                var mm=Regex.Match(html2,@"(Fedora-Workstation-Live-(\d+)-([\d.]+)\.x86_64\.iso)"); if(!mm.Success)continue;
                return(mm.Groups[2].Value,isoDir+mm.Groups[1].Value,mm.Groups[1].Value);
            }
            return Empty;
        }

        private async Task<(string, string, string)> ResolveUltramarineAsync(string edition)
        {
            const string baseUrl = "https://images.fyralabs.com/isos/ultramarine/";
            string? html = await GetStringAsync(baseUrl).ConfigureAwait(false); if (html is null) return Empty;
            var intVers = Regex.Matches(html, @"href=""(\d+)/""").Cast<Match>().Select(x => x.Groups[1].Value).Where(v => int.TryParse(v, out _)).Select(int.Parse).OrderByDescending(v => v).ToList();
            foreach (int ver in intVers)
            {
                string dirUrl = $"{baseUrl}{ver}/";
                string fname  = $"ultramarine-{edition}-{ver}-live-anaconda-x86_64.iso";
                string url    = dirUrl + fname;
                if (await IsReachableAsync(url, 8).ConfigureAwait(false)) return (ver.ToString(), url, fname);
            }
            return Empty;
        }

        private async Task<(string, string, string)> ResolveParrotAsync()
        {
            string? html=await GetStringAsync("https://deb.parrot.sh/parrot/iso/").ConfigureAwait(false); if(html is null)return Empty;
            string? latest=FindLatestVersion(html,@"href=""(\d+\.\d+(?:\.\d+)?)/?"""); if(latest is null)return Empty;
            return(latest,$"https://deb.parrot.sh/parrot/iso/{latest}/Parrot-security-{latest}_amd64.iso",$"Parrot-security-{latest}_amd64.iso");
        }

        private async Task<(string, string, string)> ResolveZorinAsync()
        {
            string? html=await GetStringAsync("https://ftp.halifax.rwth-aachen.de/zorinos/").ConfigureAwait(false); if(html is null)return Empty;
            var intVers=Regex.Matches(html,@"href=""(\d+)/""").Cast<Match>().Select(x=>x.Groups[1].Value).Where(v=>int.TryParse(v,out _)).Select(int.Parse).OrderByDescending(v=>v).ToList();
            if(intVers.Count==0)return Empty; string latest=intVers[0].ToString(); string dirUrl=$"https://ftp.halifax.rwth-aachen.de/zorinos/{latest}/";
            string? html2=await GetStringAsync(dirUrl).ConfigureAwait(false); if(html2 is null)return Empty;
            var m=Regex.Match(html2,@"(Zorin-OS-\d+-Core-64-bit(?:-r\d+)?\.iso)"); if(!m.Success)return Empty;
            return(latest,dirUrl+m.Groups[1].Value,m.Groups[1].Value);
        }

        private async Task<(string, string, string)> ResolvePopOsAsync()
        {
            string? html=await GetStringAsync("https://github.com/pop-os/iso/releases").ConfigureAwait(false);
            if(html is not null)
            {
                var isos=Regex.Matches(html,@"(pop-os_([\d.]+)_amd64_nvidia_(\d+)\.iso)").Cast<Match>().Select(x=>(Filename:x.Groups[1].Value,Version:x.Groups[2].Value,Build:int.Parse(x.Groups[3].Value))).OrderByDescending(x=>x.Version,VersionComparer.Instance).ThenByDescending(x=>x.Build).ToList();
                if(isos.Count>0){var(fname,ver,build)=isos[0];return(ver,$"https://iso.pop-os.org/{ver}/amd64/nvidia/{build}/{fname}",fname);}
            }
            foreach(string ver in new[]{"24.04","22.04"})for(int b=12;b>=6;b--){string fname=$"pop-os_{ver}_amd64_nvidia_{b}.iso";string url=$"https://iso.pop-os.org/{ver}/amd64/nvidia/{b}/{fname}";if(await IsReachableAsync(url,6).ConfigureAwait(false))return(ver,url,fname);}
            return Empty;
        }

        private async Task<(string, string, string)> ResolveManjaroAsync(string edition)
        {
            foreach(string page in new[]{$"https://manjaro.org/products/download/x86",$"https://manjaro.org/downloads/official/{edition}/"})
            {
                string? html=await GetStringAsync(page).ConfigureAwait(false); if(html is null)continue;
                string edAlt=edition=="kde"?"plasma":edition;
                var links=Regex.Matches(html,$@"(https://[^\s""'<>]*manjaro-(?:{Regex.Escape(edition)}|{Regex.Escape(edAlt)})-[\d.]+-[\d]+-linux\d+\.iso)",RegexOptions.IgnoreCase).Cast<Match>().Select(m=>m.Groups[1].Value).ToList();
                if(links.Count==0)continue; string url=links[0]; string fname=url.Split('/').Last();
                var vm=Regex.Match(fname,@"-([\d.]+)-[\d]+-linux"); return(vm.Success?vm.Groups[1].Value:ExtractVersion(fname),url,fname);
            }
            foreach(string baseUrl in new[]{"https://download.manjaro.org/","https://ftp.halifax.rwth-aachen.de/manjaro/"})
            {
                string? root=await GetStringAsync(baseUrl).ConfigureAwait(false); if(root is null)continue;
                string? edDir=Regex.Matches(root,@"href=""([a-zA-Z][a-zA-Z0-9_-]+)/""").Cast<Match>().Select(m=>m.Groups[1].Value).FirstOrDefault(a=>a.Equals(edition,StringComparison.OrdinalIgnoreCase)); if(edDir is null)continue;
                string? h2=await GetStringAsync($"{baseUrl}{edDir}/").ConfigureAwait(false); if(h2 is null)continue;
                string? ver=FindLatestVersion(h2); if(ver is null)continue;
                string? h3=await GetStringAsync($"{baseUrl}{edDir}/{ver}/").ConfigureAwait(false); if(h3 is null)continue;
                var mm=Regex.Match(h3,$@"(manjaro-{Regex.Escape(edDir)}-{Regex.Escape(ver)}-[\d]+-linux\d+\.iso)",RegexOptions.IgnoreCase); if(!mm.Success)continue;
                return(ver,$"{baseUrl}{edDir}/{ver}/{mm.Groups[1].Value}",mm.Groups[1].Value);
            }
            return Empty;
        }

        private async Task<(string, string, string)> ResolveMxLinuxAsync()
        {
            foreach(string m in new[]{"https://ftp.halifax.rwth-aachen.de/mxlinux/isos/MX/Final/Xfce/","https://mirrors.dotsrc.org/mxlinux/isos/MX/Final/Xfce/"})
            {
                string? html=await GetStringAsync(m).ConfigureAwait(false); if(html is null)continue;
                var hits=Regex.Matches(html,@"(MX-[\d.]+_Xfce_x64\.iso)").Cast<Match>().Select(x=>x.Groups[1].Value).ToList();
                if(hits.Count==0)continue; string fname=hits.OrderByDescending(f=>f).First();
                return(ExtractVersion(fname),m+fname,fname);
            }
            return Empty;
        }

        private async Task<(string, string, string)> ResolveNobaraAsync()
        {
            foreach(string src in new[]{"https://nobara-images.nobaraproject.org/","https://nobaraproject.org/download.html"})
            {
                string? html=await GetStringAsync(src).ConfigureAwait(false); if(html is null)continue;
                var hits=Regex.Matches(html,@"(Nobara-(\d+)-Official-(\d{4}-\d{2}-\d{2})\.iso)").Cast<Match>().Select(x=>(Filename:x.Groups[1].Value,Version:x.Groups[2].Value,Date:x.Groups[3].Value)).OrderByDescending(x=>(int.Parse(x.Version),x.Date)).ToList();
                if(hits.Count==0)continue; var(fname,ver,_)=hits[0]; return(ver,$"https://nobara-images.nobaraproject.org/{fname}",fname);
            }
            return Empty;
        }

        private async Task<(string, string, string)> ResolveHirensAsync()
        {
            const string url="https://www.hirensbootcd.org/files/HBCD_PE_x64.iso";
            return await IsReachableAsync(url,8).ConfigureAwait(false)?("1.0.8",url,"HBCD_PE_x64.iso"):Empty;
        }

        private async Task<(string, string, string)> ResolveDrWebAsync()
        {
            foreach(string vc in new[]{"902","901","900"})foreach(string b in new[]{"https://download.geo.drweb.com/pub/drweb/livedisk/","https://ftp.drweb.com/pub/drweb/livedisk/"})
            {string fname=$"drweb-livedisk-{vc}-cd.iso";string url=b+fname;if(!await IsReachableAsync(url,8).ConfigureAwait(false))continue;return(string.Join(".",vc.Select(c=>c.ToString())),url,fname);}
            return Empty;
        }

        private async Task<(string, string, string)> ResolveFinnixAsync()
        {
            foreach(string src in new[]{"https://www.finnix.org/","https://www.finnix.org/download/"})
            {
                string? html=await GetStringAsync(src).ConfigureAwait(false); if(html is null)continue;
                var m=Regex.Match(html,@"finnix-(\d+)\.iso",RegexOptions.IgnoreCase);
                if(m.Success){string ver=m.Groups[1].Value;string fname=$"finnix-{ver}.iso";return(ver,$"https://www.finnix.org/releases/{ver}/{fname}",fname);}
                string? latest=FindLatestVersion(html,@"href=""(\d+)/"""); if(latest is null)continue;
                string fname2=$"finnix-{latest}.iso"; return(latest,$"https://www.finnix.org/releases/{latest}/{fname2}",fname2);
            }
            return Empty;
        }

        private async Task<(string, string, string)> ResolveCachyOsAsync()
        {
            foreach(string src in new[]{"https://iso.cachyos.org/","https://iso.cachyos.org/desktop/","https://cachyos.org/download/"})
            {
                string? html=await GetStringAsync(src).ConfigureAwait(false); if(html is null)continue;
                var hits=Regex.Matches(html,@"cachyos-desktop-linux-(\d{6})\.iso",RegexOptions.IgnoreCase).Cast<Match>().Select(m=>m.Groups[1].Value).OrderByDescending(v=>v).ToList();
                if(hits.Count==0)continue; string dv=hits[0]; string fname=$"cachyos-desktop-linux-{dv}.iso";
                return(dv,$"https://iso.cachyos.org/desktop/{dv}/{fname}",fname);
            }
            for(int d=0;d<90;d+=7){string dv=DateTime.Today.AddDays(-d).ToString("yyMMdd");string fname=$"cachyos-desktop-linux-{dv}.iso";string url=$"https://iso.cachyos.org/desktop/{dv}/{fname}";if(await IsReachableAsync(url,5).ConfigureAwait(false))return(dv,url,fname);}
            return Empty;
        }

        private async Task<(string, string, string)> ResolveEndeavourOsAsync()
        {
            const string isoPattern=@"(EndeavourOS[_-][\w.()-]+-\d{4}\.\d{2}\.\d{2}(?:_R\d)?\.iso)";
            foreach(string m in new[]{"https://mirror.alpix.eu/endeavouros/iso/","https://ftp.halifax.rwth-aachen.de/endeavouros/iso/","https://ftp.fau.de/endeavouros/iso/"})
            {
                string? html=await GetStringAsync(m).ConfigureAwait(false); if(html is null)continue;
                var hits=Regex.Matches(html,isoPattern,RegexOptions.IgnoreCase).Cast<Match>().Select(x=>x.Groups[1].Value).Distinct().OrderByDescending(f=>f).ToList();
                if(hits.Count==0)continue; string fname=hits[0]; var vm=Regex.Match(fname,@"(\d{4}\.\d{2}\.\d{2})");
                return(vm.Success?vm.Groups[1].Value:ExtractVersion(fname),m+fname,fname);
            }
            return Empty;
        }

        private async Task<(string, string, string)> ResolveKodachiAsync()
        {
            const string FnamePattern   = @"linux-kodachi-xfce-([\d]+(?:\.[\d]+)+)-amd64\.iso";
            const string FallbackFname  = "linux-kodachi-xfce-9.0.1-amd64.iso";
            const string DownloadsPage  = "https://kodachi.cloud/downloads/";
            const string PhpDownloadUrl = "https://kodachi.cloud/downloads/download.php?f=xfce-iso";

            // FIX CS0219: versionFromPage entfernt (war assigned but never used)
            string fname = FallbackFname;
            string? html = await GetStringAsync(DownloadsPage).ConfigureAwait(false);
            if (html is not null)
            {
                var m = Regex.Match(html, FnamePattern, RegexOptions.IgnoreCase);
                if (m.Success) fname = $"linux-kodachi-xfce-{m.Groups[1].Value}-amd64.iso";
            }
            string ver = ExtractVersion(fname);

            foreach (string url in new[]
            {
                $"{DownloadsPage}{fname}",
                $"https://cdn.kodachi.cloud/downloads/{fname}",
                $"https://cdn.kodachi.cloud/{fname}",
                $"https://downloads.kodachi.cloud/{fname}",
                $"https://mirror.kodachi.cloud/{fname}",
            })
                if (await IsReachableAsync(url, 12).ConfigureAwait(false)) return (ver, url, fname);

            // PHP-Script: HEAD schlägt fehl (405), aber GET liefert ISO direkt
            return (ver, PhpDownloadUrl, fname);
        }

        private async Task<(string, string, string)> ResolveSourceForgeAsync(string project, string subPath, string isoRegex)
        {
            string? rss=await GetStringAsync($"https://sourceforge.net/projects/{project}/rss?path={subPath}").ConfigureAwait(false); if(rss is null)return Empty;
            var titles=Regex.Matches(rss,@"<title><!\[CDATA\[(/[^\]]*\.iso)\]\]></title>").Cast<Match>().Select(m=>m.Groups[1].Value.Trim()).ToList();
            foreach(string sfPath in titles)
            {
                string fname=sfPath.Split('/').Last(); if(!Regex.IsMatch(fname,isoRegex,RegexOptions.IgnoreCase))continue;
                string cleanPath=sfPath.TrimEnd('/').Replace("/download",string.Empty);
                return(ExtractVersion(fname),$"https://master.dl.sourceforge.net/project/{project}{cleanPath}?viasf=1",fname);
            }
            return Empty;
        }

        private Task<(string, string, string)> ResolveUbuntuGamepackAsync() =>
            ResolveSourceForgeAsync("ualinux", "/Ubuntu Pack/GamePack", @"ubuntu_game_pack-[\d.]+-amd64\.iso");

        /// <summary>
        /// Testet mehrere Mirror-URLs PARALLEL für eine feste Zeitspanne (statt einer festen
        /// Byte-Anzahl) und gibt sie absteigend nach gemessener Geschwindigkeit sortiert zurück —
        /// Grundlage für die Mirror-Auswahl in DownloadWorker, BEVOR der eigentliche Download eines
        /// Distros startet. Läuft bei ≤1 URL gar nicht erst (nichts zu vergleichen).
        ///
        /// ACHTUNG: Manche CDNs/Mirrors drosseln die ersten ein bis zwei Sekunden einer Verbindung
        /// und liefern erst danach die volle Geschwindigkeit — ein einfacher Gesamtdurchschnitt über
        /// das Testfenster würde solche Mirrors systematisch benachteiligen. Deshalb wird die
        /// Geschwindigkeit in ~400ms-Fenstern gemessen und das SCHNELLSTE beobachtete Fenster
        /// gewertet, nicht der Durchschnitt über die gesamte Testdauer.
        /// </summary>
        public async Task<List<(string Url, double Bps)>> RaceMirrorsAsync(IReadOnlyList<string> urls, TimeSpan sampleDuration, CancellationToken ct)
        {
            if (urls.Count <= 1) return urls.Select(u => (u, 0.0)).ToList();
            var speeds = await Task.WhenAll(urls.Select(u => ProbeMirrorSpeedAsync(u, sampleDuration, ct))).ConfigureAwait(false);
            return urls.Zip(speeds, (u, bps) => (Url: u, Bps: bps))
                // OrderByDescending ist stabil — bei gleicher (z.B. 0=nicht erreichbarer) Geschwindigkeit
                // bleibt die ursprüngliche Priorität (aufgelöste URL → primäre URL → Mirror1-5) erhalten.
                .OrderByDescending(x => x.Bps)
                .ToList();
        }

        private async Task<double> ProbeMirrorSpeedAsync(string url, TimeSpan duration, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(url)) return 0;
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(duration + TimeSpan.FromSeconds(3)); // Puffer für Verbindungsaufbau/DNS
                using var req  = new HttpRequestMessage(HttpMethod.Get, url);
                using var resp = await _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return 0;
                using var stream = await resp.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);

                byte[] buf = new byte[65_536];
                var sw = Stopwatch.StartNew();
                long total = 0, windowStart = 0; double windowStartT = 0, bestBps = 0; int read;
                while (sw.Elapsed < duration && (read = await stream.ReadAsync(buf, cts.Token).ConfigureAwait(false)) > 0)
                {
                    total += read;
                    double now = sw.Elapsed.TotalSeconds;
                    if (now - windowStartT >= 0.4)
                    {
                        double bps = (total - windowStart) / (now - windowStartT);
                        if (bps > bestBps) bestBps = bps;
                        windowStart = total; windowStartT = now;
                    }
                }
                return bestBps;
            }
            catch (Exception ex) { Debug.WriteLine($"[MirrorRace] {url}: {ex.Message}"); return 0; }
        }

        // SICHERHEIT: Verifiziert nur die BYTEGRÖSSE (siehe unten: written < MinIsoSizeBytes,
        // total > 0 && written < total), NICHT die Integrität des Inhalts — es gibt keine
        // Prüfsummen-/Signaturprüfung der heruntergeladenen ISO gegen einen bekannten Hash.
        // Das ist ein bewusster Kompromiss, kein Versehen: da URLs/Versionen dynamisch über
        // ResolveLatestAsync ermittelt werden (siehe dort), gibt es keine vorab hinterlegten
        // Referenz-Hashes zum Abgleich. Die Kette aus TLS (HTTPS zu offiziellen Anbietern) +
        // Größenabgleich reduziert das Risiko, ersetzt aber keine kryptografische Verifikation.
        // Wer maximale Sicherheit braucht, sollte heruntergeladene ISOs zusätzlich gegen die vom
        // jeweiligen Anbieter veröffentlichte Prüfsumme (sha256sum o.ä.) verifizieren.
        public async Task<bool> DownloadAsync(
            string url, string destinationPath,
            IProgress<(int Percent, string Detail)>? progress,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(url)||string.IsNullOrWhiteSpace(destinationPath)) return false;
            string? dir=Path.GetDirectoryName(destinationPath);
            if(!string.IsNullOrEmpty(dir))Directory.CreateDirectory(dir);
            string tempPath = destinationPath + ".part";
            long written = 0L;
            try
            {
                using var req  = new HttpRequestMessage(HttpMethod.Get, url);
                using var resp = await _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();
                long total = resp.Content.Headers.ContentLength ?? 0L;

                // Freispeicher-Check: sobald die Content-Length bekannt ist (praktisch immer bei
                // ISO-/Ventoy-Downloads), lieber jetzt klar abbrechen als nach halbem Download mitten
                // im Schreiben zu scheitern. Best-effort — ein Fehler bei der Prüfung selbst (z.B.
                // Pfad ergibt keine gültige Laufwerkswurzel) darf den Download nicht verhindern.
                if (total > 0)
                {
                    try
                    {
                        string? root = Path.GetPathRoot(Path.GetFullPath(destinationPath));
                        if (!string.IsNullOrEmpty(root))
                        {
                            var drive = new DriveInfo(root);
                            if (drive.IsReady && drive.AvailableFreeSpace < total)
                            {
                                progress?.Report((0, $"❌ Nicht genug Speicherplatz auf {root} (benötigt {FormatBytes(total)}, frei {FormatBytes(drive.AvailableFreeSpace)})"));
                                return false;
                            }
                        }
                    }
                    catch (Exception ex) { Debug.WriteLine($"[DownloadAsync] Freispeicher-Check übersprungen: {ex.Message}"); }
                }

                long lastBase = 0L; DateTime tick = DateTime.UtcNow;
                {
                    await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    await using var file   = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 131_072, FileOptions.Asynchronous);
                    byte[] buf = new byte[131_072]; int read;
                    while ((read = await stream.ReadAsync(buf, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await file.WriteAsync(buf.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                        written += read;
                        DateTime now = DateTime.UtcNow;
                        if ((now - tick).TotalMilliseconds >= 500 || (total > 0 && written == total))
                        {
                            double bps = (written - lastBase) / Math.Max(0.001, (now - tick).TotalSeconds);
                            lastBase = written; tick = now;
                            int pct = total > 0 ? (int)(written * 100L / total) : 0;
                            // War bisher nur "geschrieben / gesamt  geschwindigkeit" — beim Kopieren
                            // auf den Stick (TransferFormat.BuildDetail) gibt es die geschätzte
                            // Restzeit schon lange, beim eigentlichen Download fehlte sie bisher.
                            string spd = bps > 0 ? FormatBytes(bps) + "/s" : string.Empty;
                            string eta = total > 0 && bps > 0.01 ? $"  ·  noch {FormatEta((total - written) / bps)}" : string.Empty;
                            progress?.Report((pct, $"{FormatBytes(written)} / {FormatBytes(total)}" + (string.IsNullOrEmpty(spd) ? string.Empty : $"  {spd}") + eta));
                        }
                    }
                    await file.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                // BUGFIX: Die 300-MB-Mindestgröße ist als Rückfallebene für ISO-Downloads gedacht,
                // wenn der Server keine Content-Length liefert (siehe HelpDialog: "Ist online keine
                // Größe ermittelbar, greift als Rückfallebene die 300-MB-Mindestgröße"). DownloadAsync
                // wird aber generisch für JEDEN Download genutzt — auch für das ~16 MB kleine
                // Ventoy-ZIP (VentoyInstallWorker). Die Schwelle unbedingt anzuwenden ließ jeden
                // Ventoy-Download fälschlich als "zu klein" scheitern, obwohl er zu 100% vollständig
                // war (Content-Length war bekannt und stimmte exakt überein). Ist die erwartete Größe
                // bekannt (total > 0), ist der exakte Bytevergleich die präzisere und einzig korrekte
                // Prüfung — die 300-MB-Schwelle greift nur noch, wenn total unbekannt ist.
                if (total > 0)
                {
                    if (written < total) { TryDelete(tempPath); return false; }
                }
                else if (written < Constants.MinIsoSizeBytes) { TryDelete(tempPath); return false; }
                if (File.Exists(destinationPath)) File.Delete(destinationPath);
                File.Move(tempPath, destinationPath);
                return true;
            }
            catch (OperationCanceledException) { TryDelete(tempPath); return false; }
            catch (Exception ex) { TryDelete(tempPath); Debug.WriteLine($"[DownloadAsync] {ex.GetType().Name}: {ex.Message} ({url})"); return false; }
        }

        private static string FormatBytes(double bytes)
        { string[] u={"B","KB","MB","GB"}; double v=bytes; int i=0; while(v>=1024&&i<u.Length-1){v/=1024;i++;} return i==0?$"{(long)v} B":$"{v:F1} {u[i]}"; }

        // Spiegelt Core.Workers.TransferFormat.FormatEta — bewusst lokal dupliziert statt eine
        // Abhängigkeit von Services auf Workers einzuführen (Workers hängt bereits von Services ab,
        // nicht umgekehrt).
        private static string FormatEta(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) return "—";
            if (seconds < 1) return "<1s";
            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalHours   >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            if (ts.TotalMinutes >= 1) return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
            return $"{ts.Seconds}s";
        }

        private static void TryDelete(string path)
        { try{if(File.Exists(path))File.Delete(path);}catch{} }
    }
}
