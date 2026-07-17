// Core/Services/HttpService.DistroResolvers.cs
using System;
using System.Collections.Generic;
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
    public sealed partial class HttpService
    {
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

        /// <summary>
        /// Stufe 2 (siehe Spec): versucht, die vom Anbieter veröffentlichte offizielle Prüfsumme für
        /// die gegebene Datei zu finden — aktuell nur Ubuntu/Debian/Fedora (stabilstes, einfach
        /// parsbares Format). Liefert null bei jedem Fehler (kein Resolver, Netzwerkfehler, Format
        /// nicht erkannt) — das ist KEIN harter Fehler, der Aufrufer fällt dann auf
        /// Sha256Source = "LocalDownload" zurück.
        /// </summary>
        public async Task<string?> ResolveOfficialChecksumAsync(IsoEntry entry, string filename)
        {
            string nl = NormalizeForMatch(entry.Name);
            try
            {
                if (nl.Contains("ubuntu") && !nl.Contains("lubuntu"))
                {
                    var vm = Regex.Match(filename, @"ubuntu-(\d+\.\d+(?:\.\d+)?)-");
                    if (!vm.Success) return null;
                    string? content = await GetStringAsync($"https://releases.ubuntu.com/{vm.Groups[1].Value}/SHA256SUMS").ConfigureAwait(false);
                    return content is null ? null : ParseSha256SumsLine(content, filename);
                }
                if (nl.Contains("debian"))
                {
                    foreach (string m in new[] { "https://ftp.halifax.rwth-aachen.de/debian-cd/current-live/amd64/iso-hybrid/", "https://ftp.fau.de/debian-cd/current-live/amd64/iso-hybrid/", "https://cdimage.debian.org/debian-cd/current-live/amd64/iso-hybrid/" })
                    {
                        string? content = await GetStringAsync(m + "SHA256SUMS").ConfigureAwait(false);
                        string? hash = content is null ? null : ParseSha256SumsLine(content, filename);
                        if (hash != null) return hash;
                    }
                    return null;
                }
                if (nl.Contains("fedora"))
                {
                    foreach (string m in new[] { "https://ftp.fau.de/fedora/linux/releases/", "https://ftp.halifax.rwth-aachen.de/fedora/linux/releases/" })
                    {
                        var vm = Regex.Match(filename, @"Fedora-Workstation-Live-(\d+)-");
                        if (!vm.Success) continue;
                        string isoDir = $"{m}{vm.Groups[1].Value}/Workstation/x86_64/iso/";
                        foreach (string candidate in new[] { "CHECKSUM", $"{Path.GetFileNameWithoutExtension(filename)}-CHECKSUM" })
                        {
                            string? content = await GetStringAsync(isoDir + candidate).ConfigureAwait(false);
                            string? hash = content is null ? null : ParseBsdStyleChecksum(content, filename);
                            if (hash != null) return hash;
                        }
                    }
                    return null;
                }
                return null;
            }
            catch (Exception) { return null; }
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

    }
}
