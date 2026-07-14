// Core/Workers/Workers.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ULM.Core.Models;
using ULM.Core.Services;
using ULM.Infrastructure;

namespace ULM.Core.Workers
{
    internal static class TransferFormat
    {
        public static string FormatBytes(double bytes)
        {
            string[] u = { "B", "KB", "MB", "GB" }; double v = Math.Max(0, bytes); int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return i == 0 ? $"{(long)v} B" : $"{v:F1} {u[i]}";
        }
        public static string FormatEta(double s)
        {
            if (double.IsNaN(s) || double.IsInfinity(s) || s < 0) return "—";
            if (s < 1) return "<1s";
            var ts = TimeSpan.FromSeconds(s);
            if (ts.TotalHours >= 1)   return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            if (ts.TotalMinutes >= 1) return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
            return $"{ts.Seconds}s";
        }
        public static string BuildDetail(double bps, long done, long total)
        {
            string speed = FormatBytes(bps) + "/s";
            if (total > 0) { string eta = bps > 0.01 ? FormatEta((total - done) / bps) : "—"; return $"{speed}  ·  noch {eta}  ·  {FormatBytes(done)} / {FormatBytes(total)}"; }
            return $"{speed}  ·  {FormatBytes(done)}";
        }
    }

    public sealed class FormatWorker
    {
        private readonly string _letter;
        public event Action<bool>? Completed;
        public FormatWorker(string letter) => _letter = letter;
        public Task RunAsync() => Task.Run(() => Completed?.Invoke(UsbService.DoFormat(_letter)));
    }

    public sealed class VentoyInstallWorker
    {
        private readonly string _letter;
        private readonly bool   _updateMode;
        private readonly bool   _secureBoot;
        private readonly CancellationTokenSource _cts = new();
        public event Action<string>?      ProgressLog;
        public event Action<int, string>? Progress;
        public event Action<bool>?        Completed;
        public VentoyInstallWorker(string letter, bool updateMode, bool secureBoot)
        { _letter = letter; _updateMode = updateMode; _secureBoot = secureBoot; }
        public void Cancel() => _cts.Cancel();
        public Task RunAsync() => Task.Run(async () =>
        {
            try
            {
                AppPaths paths = AppPaths.Instance;
                Directory.CreateDirectory(paths.VentoyTempDir);
                string exePath = FindVentoy2DiskExe(paths.VentoyTempDir);
                if (string.IsNullOrEmpty(exePath))
                {
                    ProgressLog?.Invoke("Lade neueste Ventoy-Version von GitHub …"); Progress?.Invoke(5, "Lade Ventoy …");
                    string archiveUrl = await FetchLatestVentoyUrlAsync().ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(archiveUrl)) { ProgressLog?.Invoke("Fehler: Konnte Ventoy-URL nicht abrufen."); Completed?.Invoke(false); return; }
                    ProgressLog?.Invoke($"Lade herunter: {archiveUrl}");
                    bool dlOk = await HttpService.Instance.DownloadFileAsync(archiveUrl, paths.VentoyZipPath, _cts.Token, (p, d) => { ProgressLog?.Invoke($"Download: {p}%  {d}"); Progress?.Invoke(5 + p / 3, d); }).ConfigureAwait(false);
                    if (!dlOk || _cts.IsCancellationRequested) { ProgressLog?.Invoke("Fehler: Download fehlgeschlagen."); Completed?.Invoke(false); return; }
                    ProgressLog?.Invoke("Entpacke Ventoy …"); Progress?.Invoke(42, "Entpacke …");
                    string extractDir = Path.Combine(paths.VentoyTempDir, "extracted");
                    if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                    // SICHERHEIT: ZipFile.ExtractToDirectory prüft seit .NET Core 2.1 selbst, dass
                    // kein Zip-Eintrag per "../"-Pfaden aus extractDir ausbrechen kann (wirft sonst
                    // eine IOException) — kein zusätzlicher manueller Zip-Slip-Schutz nötig. Die
                    // anschließende Flatten-Kopie unten liest ausschließlich Dateien, die bereits
                    // innerhalb von extractDir liegen (Directory.GetFiles), bleibt also im selben
                    // sicheren Rahmen. Die Quelle selbst (ventoy_latest.zip) kommt ausschließlich
                    // von der offiziellen GitHub-Releases-API des Ventoy-Projekts über HTTPS
                    // (FetchLatestVentoyUrlAsync) — keine benutzergesteuerte URL.
                    ZipFile.ExtractToDirectory(paths.VentoyZipPath, extractDir, true);
                    foreach (string f in Directory.GetFiles(extractDir, "*", SearchOption.AllDirectories))
                    { string rel = Path.GetRelativePath(extractDir, f); int sep = rel.IndexOf(Path.DirectorySeparatorChar); string flat = sep >= 0 ? rel[(sep + 1)..] : rel; string dst = Path.Combine(paths.VentoyTempDir, flat); Directory.CreateDirectory(Path.GetDirectoryName(dst)!); File.Copy(f, dst, true); }
                    exePath = FindVentoy2DiskExe(paths.VentoyTempDir);
                }
                if (string.IsNullOrEmpty(exePath)) { ProgressLog?.Invoke("Fehler: Ventoy2Disk.exe nicht gefunden."); Completed?.Invoke(false); return; }

                // BUGFIX: "-i -y {letter}:" / "-u {letter}:" / "-s" sind KEINE gültigen
                // Ventoy2Disk.exe-Argumente — die echte CLI-Automatisierung heißt "VTOYCLI" und
                // erwartet "/I" bzw. "/U" plus "/Drive:X:" (siehe ventoy.net/en/doc_windows_cli.html).
                // Unerkannte Argumente ließen Ventoy2Disk.exe bisher lautlos in seinen normalen
                // interaktiven GUI-Modus zurückfallen — DAS war der Grund, warum trotz Automatisierung
                // immer noch ein Ventoy-eigenes Fenster erschien, das manuell bedient werden musste.
                // Secure Boot ist bei "/I" standardmäßig AKTIV; "/NOSB" schaltet es ab (umgekehrte
                // Polarität zum bisherigen, ohnehin nicht existierenden "-s"-Flag). "/NOUSBCheck"
                // verhindert, dass Ventoys eigene Geräteprüfung ein Laufwerk ablehnt, das ULM bereits
                // selbst als gültiges Wechsellaufwerk validiert hat (siehe UsbService.ListRemovableDrives).
                string workDir   = Path.GetDirectoryName(exePath)!;
                string driveArg  = $"/Drive:{_letter[0]}:";
                string args = _updateMode
                    ? $"VTOYCLI /U {driveArg}"
                    : $"VTOYCLI /I {driveArg} /NOUSBCheck" + (_secureBoot ? string.Empty : " /NOSB");

                // Ventoys eigene Status-Dateien (offiziell dokumentiert für den CLI-Modus) sind die
                // zuverlässigste Quelle für Fortschritt/Ergebnis — zuverlässiger als der Exit-Code
                // einer GUI-Anwendung. Reste eines vorherigen Laufs zuerst entfernen, damit eine
                // alte cli_done.txt nicht fälschlich als aktuelles Ergebnis gelesen wird.
                string doneFile    = Path.Combine(workDir, "cli_done.txt");
                string percentFile = Path.Combine(workDir, "cli_percent.txt");
                string logFile     = Path.Combine(workDir, "cli_log.txt");
                foreach (string f in new[] { doneFile, percentFile, logFile }) { try { File.Delete(f); } catch { } }

                ProgressLog?.Invoke($"Starte Ventoy2Disk.exe auf {_letter} (VTOYCLI, still) …"); Progress?.Invoke(55, "Installiere Ventoy …");
                var psi = new ProcessStartInfo(exePath, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = workDir };
                using Process? proc = Process.Start(psi);
                if (proc is null) { ProgressLog?.Invoke("Fehler: Ventoy2Disk.exe konnte nicht gestartet werden."); Completed?.Invoke(false); return; }

                // Stdout/Stderr fortlaufend abziehen statt erst nach WaitForExit — verhindert einen
                // Deadlock, falls der Kindprozess mehr schreibt, als der OS-Pipe-Puffer fasst, während
                // wir parallel per Status-Dateien auf das Ergebnis warten.
                var stdoutTask = proc.StandardOutput.ReadToEndAsync();
                var stderrTask = proc.StandardError.ReadToEndAsync();

                bool? doneOk = null;
                var sw = Stopwatch.StartNew();
                while (sw.Elapsed < TimeSpan.FromMinutes(5))
                {
                    if (_cts.IsCancellationRequested) { try { proc.Kill(); } catch { } break; }
                    if (File.Exists(percentFile) && TryReadText(percentFile, out string pctRaw) &&
                        int.TryParse(pctRaw.Trim(), out int pct))
                        Progress?.Invoke(55 + Math.Clamp(pct, 0, 100) * 33 / 100, $"Installiere Ventoy … {pct}%");
                    if (File.Exists(doneFile) && TryReadText(doneFile, out string doneRaw) &&
                        int.TryParse(doneRaw.Trim(), out int doneCode))
                    { doneOk = doneCode == 0; break; }
                    if (proc.HasExited) break;
                    await Task.Delay(400, CancellationToken.None).ConfigureAwait(false);
                }
                if (!proc.HasExited) { try { proc.WaitForExit(10_000); } catch { } }

                string stdout = await stdoutTask.ConfigureAwait(false);
                string stderr = await stderrTask.ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(stdout)) ProgressLog?.Invoke($"[Ventoy] {stdout.Trim()}");
                if (!string.IsNullOrWhiteSpace(stderr))  ProgressLog?.Invoke($"[Ventoy STDERR] {stderr.Trim()}");
                if (File.Exists(logFile) && TryReadText(logFile, out string cliLog) && !string.IsNullOrWhiteSpace(cliLog))
                    ProgressLog?.Invoke($"[Ventoy CLI-Log]\n{cliLog.Trim()}");

                if (!proc.HasExited) { ProgressLog?.Invoke("Fehler: Timeout."); try { proc.Kill(); } catch { } Completed?.Invoke(false); return; }

                // cli_done.txt ist die primäre Erfolgsquelle (offiziell dokumentiert); der
                // Prozess-Exitcode dient nur als Rückfallebene, falls die Datei aus irgendeinem
                // Grund nie erschienen ist (z.B. unerwartet abweichendes Ventoy-Verhalten).
                bool ok = doneOk ?? proc.ExitCode == 0;
                ProgressLog?.Invoke(ok ? $"✅ Ventoy {(_updateMode ? "aktualisiert" : "installiert")}." : $"❌ Fehlgeschlagen (ExitCode {proc.ExitCode}{(doneOk is not null ? $", cli_done={(doneOk.Value ? 0 : 1)}" : "")}).");
                if (ok && !_updateMode) { Progress?.Invoke(88, "Richte Theme ein …"); UsbService.EnsureVentoyTheme(_letter); }
                Progress?.Invoke(100, ok ? "Fertig." : "Fehlgeschlagen."); Completed?.Invoke(ok);
            }
            catch (Exception ex) { ProgressLog?.Invoke($"Fehler: {ex.GetType().Name}: {ex.Message}"); Completed?.Invoke(false); }
        });
        private static string FindVentoy2DiskExe(string dir)
        { if (!Directory.Exists(dir)) return string.Empty; string d = Path.Combine(dir, "Ventoy2Disk.exe"); if (File.Exists(d)) return d; foreach (string f in Directory.GetFiles(dir, "Ventoy2Disk.exe", SearchOption.AllDirectories)) return f; return string.Empty; }

        // Ventoy2Disk.exe hält cli_percent.txt/cli_done.txt beim Schreiben ggf. kurz offen —
        // FileShare.ReadWrite plus try/catch verhindert, dass ein zufälliger Lesezugriff mitten im
        // Schreibvorgang die ganze Installation mit einer Exception abbrechen lässt.
        private static bool TryReadText(string path, out string content)
        {
            try { using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); using var sr = new StreamReader(fs); content = sr.ReadToEnd(); return true; }
            catch { content = string.Empty; return false; }
        }
        private static async Task<string> FetchLatestVentoyUrlAsync()
        {
            const string Fallback = "https://github.com/ventoy/Ventoy/releases/download/v1.0.97/ventoy-1.0.97-windows.zip";
            // Nutzt HttpService.Instance.GitHubToken, falls vom Nutzer hinterlegt (siehe
            // HttpService.AddGitHubAuthHeader) — hebt das API-Limit von 60 auf 5000 Anfragen/Std.
            try
            {
                using var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, "https://api.github.com/repos/ventoy/Ventoy/releases/latest");
                req.Headers.UserAgent.ParseAdd("ULM/2.27");
                string? token = HttpService.Instance.GitHubToken;
                if (!string.IsNullOrWhiteSpace(token)) req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
                using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(12) };
                using var resp = await client.SendAsync(req).ConfigureAwait(false);
                string json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                foreach (var a in doc.RootElement.GetProperty("assets").EnumerateArray())
                {
                    string n = a.GetProperty("name").GetString() ?? "";
                    string u = a.GetProperty("browser_download_url").GetString() ?? "";
                    if (!string.IsNullOrEmpty(u) && n.Contains("windows", StringComparison.OrdinalIgnoreCase) && n.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) return u;
                }
            }
            catch { }
            return Fallback;
        }
    }

    public sealed class DownloadSlotArgs
    {
        public string IsoName { get; set; } = string.Empty;
        public string Status  { get; set; } = string.Empty;
        public int    Percent { get; set; }
        // True nur während eines laufenden Mirror-Download-Versuchs, für den noch mindestens ein
        // weiterer (bereits vom Mirror-Race gemessener) Kandidat übrig ist — steuert die
        // Sichtbarkeit des "(schneller)"-Buttons im Fortschrittsfenster.
        public bool   CanRequestFasterMirror { get; set; }
    }

    public sealed class DownloadWorker
    {
        private readonly List<IsoEntry> _entries;
        private readonly string         _downloadDir;
        private readonly int            _maxConcurrent;
        private readonly CancellationTokenSource _cts = new();
        public event Action<int, string>?       OverallProgress;
        public event Action<DownloadSlotArgs>?  SlotUpdated;
        public event Action<IsoEntry, bool>?    ItemCompleted;
        public event Action<int, int, int>?     Completed;
        public event Action<string>?            LogMessage;

        // Der jeweils laufende Mirror-Versuch pro Distro (Name → Versuch), damit ein von außen
        // (Anwender-Klick auf "(schneller)") ausgelöster RequestFasterMirror()-Aufruf GENAU diesen
        // einen Versuch abbrechen kann, ohne den ganzen Batch oder andere parallele Downloads zu
        // berühren. Siehe RequestFasterMirror weiter unten.
        private sealed class ActiveAttempt
        {
            public required CancellationTokenSource Cts;
            public bool HasMoreMirrors;
            public bool ManualSkipRequested;
        }
        private readonly ConcurrentDictionary<string, ActiveAttempt> _activeAttempts = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Bricht den GERADE laufenden Mirror-Download-Versuch für die genannte Distro ab, damit der
        /// nächste (vom Mirror-Race bereits gemessene) Kandidat versucht wird — z.B. wenn der aktuelle
        /// Server zwar deutlich über der Geschwindigkeits-Wächter-Schwelle liegt, dem Anwender aber
        /// trotzdem zu langsam ist. Anders als beim automatischen Geschwindigkeits-Wächter wird dieser
        /// Wechsel NICHT als "dauerhaft langsam" geloggt und fließt nicht in die "trotzdem fortfahren?"-
        /// Bewertung ein (siehe ManualSkipRequested). Gibt false zurück, wenn gerade kein Versuch läuft
        /// oder kein weiterer Kandidat mehr übrig ist (Button ist dann in der UI ohnehin ausgeblendet).
        /// </summary>
        public bool RequestFasterMirror(string entryName)
        {
            if (!_activeAttempts.TryGetValue(entryName, out var active) || !active.HasMoreMirrors) return false;
            active.ManualSkipRequested = true;
            try { active.Cts.Cancel(); } catch (ObjectDisposedException) { return false; }
            return true;
        }

        /// <summary>
        /// Wird aufgerufen, wenn alle Mirror-Versuche für eine Distro ausgeschöpft sind, aber
        /// mindestens einer davon NICHT wegen eines echten Fehlers, sondern nur wegen dauerhafter
        /// Langsamkeit abgebrochen wurde (Geschwindigkeits-Wächter) — d.h. es GIBT eine erreichbare
        /// Quelle, nur eben eine sehr langsame. (EntryName, Host) → true = trotzdem mit dieser
        /// Quelle fortfahren. Synchron, da DownloadWorker in einem Hintergrund-Task läuft und hier
        /// auf die Anwender-Antwort wartet, bevor es weitergeht (blockiert nur DIESEN Download-Slot,
        /// nicht die anderen parallelen). Der Aufrufer (MainViewModel) übernimmt via Dispatcher.Invoke
        /// synchron auf den UI-Thread zu wechseln.
        /// </summary>
        public Func<string, string, bool>? ConfirmSlowDownloadAnyway;
        public DownloadWorker(List<IsoEntry> entries, int maxConcurrent, string downloadDir,
            IsoDatabaseService? db, string drive, bool copyAfter, bool deleteAfter)
        { _entries = entries; _downloadDir = downloadDir; _maxConcurrent = maxConcurrent > 0 ? maxConcurrent : 1; }
        public void Cancel() => _cts.Cancel();

        // ── Geschwindigkeits-Wächter-Konstanten ─────────────────────────────
        // Anlaufzeit, bevor überhaupt gewertet wird (manche CDNs brauchen ein paar Sekunden bis
        // zur vollen Geschwindigkeit), plus die Zeit, die die Übertragung DANACH ununterbrochen
        // unter der Schwelle bleiben muss, bevor abgebrochen wird.
        private static readonly TimeSpan WarmupGrace         = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan SlowSustainedWindow = TimeSpan.FromSeconds(20);
        private const double SlowSpeedThresholdBytesPerSec   = 1_048_576; // ~1 MB/s

        /// <summary>Extrahiert die aktuelle Übertragungsrate aus dem von DownloadAsync gelieferten
        /// Detail-Text (Format "... 12.4 MB/s ..."); -1 wenn (noch) keine Rate im Text steht.</summary>
        private static double ParseSpeedBytesPerSec(string detail)
        {
            var m = Regex.Match(detail, @"([\d.,]+)\s*(B|KB|MB|GB)/s");
            if (!m.Success) return -1;
            if (!double.TryParse(m.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) return -1;
            return m.Groups[2].Value switch { "GB" => v * 1_073_741_824, "MB" => v * 1_048_576, "KB" => v * 1024, _ => v };
        }

        public Task RunAsync() => Task.Run(async () =>
        {
            int total = _entries.Count, finished = 0, successCount = 0;
            if (total == 0) { Completed?.Invoke(0, 0, 0); return; }
            var semaphore = new SemaphoreSlim(_maxConcurrent);
            var tasks = new List<Task>();
            for (int i = 0; i < total; i++)
            {
                int index = i; var entry = _entries[index];
                // BUGFIX: Bricht der Anwender ab, während dieser Slot noch auf einen freien Platz
                // wartet (alle _maxConcurrent Downloads belegt), wirft WaitAsync eine
                // OperationCanceledException. Ungefangen entkam die aus dieser Task.Run-Lambda, ließ
                // RunAsync() mit einer faulted Task enden und stürzte über das ungeschützte
                // "await worker.RunAsync()" in der async-void-Methode MainViewModel.StartDownload als
                // echte unbehandelte Exception ab (AppDomain.UnhandledException-Dialog). Ein Abbruch
                // ist hier ein erwarteter, kein außergewöhnlicher Fall — daher schlicht abbrechen wie
                // im nicht-werfenden Fall direkt darunter.
                try { await semaphore.WaitAsync(_cts.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                if (_cts.IsCancellationRequested) break;
                tasks.Add(Task.Run(async () =>
                {
                    var sa = new DownloadSlotArgs { IsoName = entry.Name, Status = "Ermittle Version...", Percent = 0 };
                    bool ok = false;
                    try
                    {
                        SlotUpdated?.Invoke(sa);
                        string ver, resolvedUrl, fname;

                        // ── URL-Ermittlung ──────────────────────────────────
                        if (entry.HasOnlineVersionInfo && !string.IsNullOrWhiteSpace(entry.RemoteUrl) &&
                            await HttpService.Instance.IsReachableAsync(entry.RemoteUrl, 8).ConfigureAwait(false))
                        { ver = entry.RemoteVersion; resolvedUrl = entry.RemoteUrl; fname = entry.RemoteFilename; }
                        else
                        { (ver, resolvedUrl, fname) = await HttpService.Instance.ResolveLatestAsync(entry).ConfigureAwait(false); }

                        if (string.IsNullOrWhiteSpace(resolvedUrl))
                        {
                            sa.Status = "❌ Keine URL gefunden"; SlotUpdated?.Invoke(sa);
                            LogMessage?.Invoke($"   ❌ {entry.Name}: Keine Download-URL ermittelt.");
                            return;
                        }

                        entry.Filename = fname;
                        if (!string.IsNullOrWhiteSpace(ver)) entry.RemoteVersion = ver;

                        // ── Mirror-Fallback: mehrere Quellen versuchen ────
                        // Reihenfolge: aufgelöste URL → primäre URL → Mirror1-5. Ein CDN-Ausfall
                        // (z.B. kodachi.cloud) blockiert den Download nicht, wenn Fallback-Spiegel
                        // konfiguriert sind.
                        //
                        // SourceForge-Fächer: eine SourceForge-Download-URL zeigt immer auf
                        // master.dl.sourceforge.net, das den Ziel-Mirror serverseitig wählt — oft
                        // einen für den jeweiligen Nutzer langsamen (real gemessen: 3 vs. 14 Mbit/s
                        // für dieselbe Datei). ExpandSourceForgeMirrors fächert eine solche URL in
                        // mehrere "?use_mirror=<name>"-Kandidaten auf, die tendenziell auf
                        // verschiedenen Servern landen — erst DADURCH hat das Mirror-Race unten
                        // überhaupt echte Auswahl zu messen (vorher waren alle Kandidaten derselbe
                        // master-Server). Nicht-SourceForge-URLs bleiben unverändert.
                        string destPath = Path.Combine(_downloadDir, fname);
                        var urlsToTry = entry.AllDownloadUrls(resolvedUrl)
                            .SelectMany(IsoEntry.ExpandSourceForgeMirrors)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .Take(8).ToList();

                        // ── Mirror-Race: bevor der eigentliche Download beginnt, alle Kandidaten
                        // parallel für ~3s antesten und den schnellsten zuerst versuchen (siehe
                        // HttpService.RaceMirrorsAsync — misst in Zeitfenstern statt fixer Byte-Zahl,
                        // damit spät hochfahrende CDNs nicht fälschlich als langsam gelten).
                        if (urlsToTry.Count > 1)
                        {
                            sa.Status = $"🔎 Teste {urlsToTry.Count} Mirror(s) …"; sa.Percent = 0; SlotUpdated?.Invoke(sa);
                            var raced = await HttpService.Instance.RaceMirrorsAsync(urlsToTry, TimeSpan.FromSeconds(3), _cts.Token).ConfigureAwait(false);
                            urlsToTry = raced.Select(r => r.Url).ToList();
                            LogMessage?.Invoke($"   🔎 {entry.Name}: Mirror-Test — " +
                                string.Join(", ", raced.Select(r => $"{TryGetSourceLabel(r.Url)} {(r.Bps > 0 ? $"{r.Bps * 8 / 1_000_000:F1} Mbit/s" : "nicht erreichbar")}")));
                        }

                        string usedUrl = resolvedUrl;
                        int mirrorIdx  = 0;
                        string? slowAbortedUrl = null, slowAbortedHost = null;

                        foreach (string tryUrl in urlsToTry)
                        {
                            mirrorIdx++;
                            string host = TryGetSourceLabel(tryUrl);
                            string label = mirrorIdx == 1 ? $"⬇ {host}" : $"⬇ Mirror {mirrorIdx}: {host}";
                            bool hasMoreMirrors = mirrorIdx < urlsToTry.Count;
                            sa.Status = $"{label} …"; sa.Percent = 0; sa.CanRequestFasterMirror = hasMoreMirrors; SlotUpdated?.Invoke(sa);
                            LogMessage?.Invoke($"   🔗 {entry.Name}: {tryUrl}");

                            // ── Geschwindigkeits-Wächter: bleibt die Übertragung (nach Anlaufzeit —
                            // manche CDNs drosseln die ersten Sekunden, siehe Mirror-Race oben) längere
                            // Zeit unter der Schwelle, lohnt sich ein Abbruch + Wechsel zum nächsten
                            // Mirror mehr als stundenlang auf einer lahmen Quelle zu warten (real
                            // beobachtet: 325-517 KB/s bei mehreren GB → 4+ Stunden ETA). Eigener
                            // linked Token statt _cts direkt — der Abbruch soll nur DIESEN Versuch
                            // treffen, nicht den ganzen Batch.
                            using var mirrorCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                            var attemptSw = Stopwatch.StartNew();
                            var lastFastEnough = attemptSw.Elapsed;
                            bool abortedForSlowness = false;

                            // Registriert diesen Versuch für RequestFasterMirror (Anwender-Klick auf
                            // "(schneller)") — der Server liegt über der Geschwindigkeits-Wächter-
                            // Schwelle (sonst würde der Wächter selbst schon abbrechen), ist dem
                            // Anwender aber trotzdem zu langsam.
                            var active = new ActiveAttempt { Cts = mirrorCts, HasMoreMirrors = hasMoreMirrors };
                            _activeAttempts[entry.Name] = active;
                            try
                            {
                                ok = await HttpService.Instance.DownloadFileAsync(
                                    tryUrl, destPath, mirrorCts.Token,
                                    (p, d) =>
                                    {
                                        sa.Status = $"{label}  {d}"; sa.Percent = p; SlotUpdated?.Invoke(sa);
                                        double bps = ParseSpeedBytesPerSec(d);
                                        if (bps < 0 || bps >= SlowSpeedThresholdBytesPerSec) lastFastEnough = attemptSw.Elapsed;
                                        else if (!abortedForSlowness && attemptSw.Elapsed > WarmupGrace && attemptSw.Elapsed - lastFastEnough > SlowSustainedWindow)
                                        {
                                            abortedForSlowness = true;
                                            LogMessage?.Invoke($"   🐢 {entry.Name}: {host} dauerhaft langsam (< {TransferFormat.FormatBytes(SlowSpeedThresholdBytesPerSec)}/s) — breche ab" +
                                                (hasMoreMirrors ? " und versuche nächste Quelle …" : "."));
                                            mirrorCts.Cancel();
                                        }
                                    })
                                    .ConfigureAwait(false);
                            }
                            finally { _activeAttempts.TryRemove(entry.Name, out _); }

                            if (ok) { usedUrl = tryUrl; break; }

                            // Anwender hat manuell "(schneller)" geklickt — kein Fehler, keine
                            // Langsamkeits-Meldung, einfach weiter zum nächsten (bereits gemessenen)
                            // Mirror. Zählt bewusst NICHT als slowAbortedUrl: der Server war schnell
                            // genug (sonst hätte der Wächter selbst schon abgebrochen), es gibt also
                            // nichts, worüber am Ende noch "trotzdem fortfahren?" gefragt werden müsste.
                            if (active.ManualSkipRequested)
                            {
                                LogMessage?.Invoke($"   ⚡ {entry.Name}: Nutzer fordert schnelleren Mirror an — wechsle von {host} …");
                                if (_cts.IsCancellationRequested) break;
                                continue;
                            }

                            // Nur den ERSTEN Langsamkeits-Abbruch merken — dank Mirror-Race sind die
                            // Kandidaten schon nach gemessener Geschwindigkeit sortiert, der erste
                            // langsam-abgebrochene ist also die beste bekannte (wenn auch lahme) Quelle.
                            if (abortedForSlowness) { slowAbortedUrl ??= tryUrl; slowAbortedHost ??= host; }
                            // Download fehlgeschlagen — nächsten Mirror versuchen (Slowness-Abbruch
                            // hat seine eigene Meldung oben schon geloggt, keine doppelte Meldung)
                            else
                                LogMessage?.Invoke($"   ⚠ {entry.Name}: {host} fehlgeschlagen{(mirrorIdx < urlsToTry.Count ? " — versuche nächsten Mirror …" : ".")}");
                            if (_cts.IsCancellationRequested) break;
                        }
                        // Button "(schneller)" ist nur WÄHREND eines laufenden Mirror-Versuchs
                        // sinnvoll (siehe Schleife oben) — in jeder Folgephase (Nachfrage-Dialog,
                        // Abschluss) ausblenden.
                        sa.CanRequestFasterMirror = false;

                        // Alle Mirror ausgeschöpft, keiner erfolgreich — aber mindestens einer war
                        // erreichbar und nur zu langsam (kein echter Fehler). Statt endgültig
                        // aufzugeben: Anwender/Experte fragen, ob trotzdem mit dieser einzig bekannten
                        // (aber langsamen) Quelle fortgefahren werden soll — diesmal OHNE
                        // Geschwindigkeits-Wächter, der Abbruch würde sonst sofort wieder greifen.
                        if (!ok && slowAbortedUrl != null && !_cts.IsCancellationRequested)
                        {
                            bool proceed = ConfirmSlowDownloadAnyway?.Invoke(entry.Name, slowAbortedHost!) ?? false;
                            if (proceed)
                            {
                                LogMessage?.Invoke($"   ▶ {entry.Name}: Fährt trotz Langsamkeit mit {slowAbortedHost} fort …");
                                string slowLabel = $"⬇ {slowAbortedHost} (langsam)";
                                sa.Status = $"{slowLabel} …"; sa.Percent = 0; SlotUpdated?.Invoke(sa);
                                ok = await HttpService.Instance.DownloadFileAsync(
                                    slowAbortedUrl, destPath, _cts.Token,
                                    (p, d) => { sa.Status = $"{slowLabel}  {d}"; sa.Percent = p; SlotUpdated?.Invoke(sa); })
                                    .ConfigureAwait(false);
                                if (ok) usedUrl = slowAbortedUrl;
                                else LogMessage?.Invoke($"   ❌ {entry.Name}: {slowAbortedHost} letztlich doch fehlgeschlagen.");
                            }
                            else
                                LogMessage?.Invoke($"   ⏭ {entry.Name}: Übersprungen — Anwender hat den langsamen Download abgelehnt.");
                        }

                        if (ok)
                        {
                            entry.UpdateAvailable = false; entry.VerifiedComplete = true;
                            sa.Percent = 100; sa.Status = "✅ Fertig";
                            Interlocked.Increment(ref successCount);
                            LogMessage?.Invoke($"   ✅ {entry.Name}: Download abgeschlossen ({fname}) via {TryGetHost(usedUrl)}");
                        }
                        else
                        {
                            sa.Status = "❌ Fehlgeschlagen — alle Mirror versucht";
                            LogMessage?.Invoke($"   ❌ {entry.Name}: Download fehlgeschlagen. {urlsToTry.Count} Mirror(s) versucht.");
                        }
                        SlotUpdated?.Invoke(sa);
                    }
                    catch (Exception ex)
                    {
                        sa.Status = $"Fehler: {ex.Message}"; SlotUpdated?.Invoke(sa);
                        LogMessage?.Invoke($"   ❌ {entry.Name}: {ex.GetType().Name}: {ex.Message}");
                    }
                    finally
                    {
                        ItemCompleted?.Invoke(entry, ok);
                        int done = Interlocked.Increment(ref finished);
                        OverallProgress?.Invoke((done * 100) / total, $"Lade herunter... ({done}/{total})");
                        semaphore.Release();
                    }
                }));
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
            Completed?.Invoke(successCount, total - successCount, 0);
        });

        private static string TryGetHost(string url)
        { try { return new Uri(url).Host; } catch { return url.Length > 40 ? url[..40] + "…" : url; } }

        // Für die Protokoll-/Statusanzeige: SourceForge-"?use_mirror=<name>"-Kandidaten haben ALLE
        // denselben Host (master.dl.sourceforge.net) — der eigentliche Auslieferungs-Server steckt
        // erst im Redirect. Damit das Race-Protokoll die Kandidaten unterscheidbar zeigt, wird bei
        // diesen URLs der angeforderte Mirror-Name statt des Hosts angezeigt.
        private static string TryGetSourceLabel(string url)
        {
            var um = Regex.Match(url, @"[?&]use_mirror=([a-z0-9_.-]+)", RegexOptions.IgnoreCase);
            return um.Success ? $"sourceforge.net→{um.Groups[1].Value}" : TryGetHost(url);
        }
    }

    public sealed class CopyToUsbWorker
    {
        private const int BufferSize = 4 * 1024 * 1024;
        private readonly List<IsoEntry> _entries;
        private readonly string         _letter;
        private readonly string         _downloadDir;
        private readonly CancellationTokenSource _cts = new();
        public event Action<int, string>?            Progress;
        public event Action<string, int, string>?    FileProgress;
        public event Action<bool, int, long, string>? Completed;
        public CopyToUsbWorker(List<IsoEntry> entries, string letter, bool _, string downloadDir)
        { _entries = entries; _letter = letter; _downloadDir = downloadDir; }
        public void Cancel() => _cts.Cancel();
        public Task RunAsync() => Task.Run(async () =>
        {
            long totalCopied = 0; int copiedCount = 0;
            try
            {
                string root = UsbService.DriveRoot(_letter);
                var valid = _entries.Where(e => e.IsLocallyAvailable(_downloadDir)).ToList();
                int total = valid.Count;
                if (total == 0) { Completed?.Invoke(true, 0, 0L, "Keine Dateien."); return; }
                long totalBytes = valid.Sum(e => e.LocalFileSize(_downloadDir));

                // Freispeicher-Check vor dem Kopieren: lieber jetzt klar abbrechen als nach der
                // Hälfte der Dateien mit einem vollen USB-Stick mittendrin zu scheitern.
                try
                {
                    var drive = new DriveInfo(root);
                    if (drive.IsReady && drive.AvailableFreeSpace < totalBytes)
                    {
                        Completed?.Invoke(false, 0, 0L, $"Nicht genug Speicherplatz auf {_letter} (benötigt {TransferFormat.FormatBytes(totalBytes)}, frei {TransferFormat.FormatBytes(drive.AvailableFreeSpace)}).");
                        return;
                    }
                }
                catch { /* Freispeicher-Check ist best-effort */ }

                byte[] buf = new byte[BufferSize];
                foreach (var e in valid)
                {
                    if (_cts.IsCancellationRequested) break;
                    string src = Path.Combine(_downloadDir, e.Filename);
                    string tdir = Path.Combine(root, e.Category); Directory.CreateDirectory(tdir);
                    string dst = Path.Combine(tdir, e.Filename);
                    long fs = e.LocalFileSize(_downloadDir), copied = 0;
                    var sw = Stopwatch.StartNew(); long lastMark = 0; double lastEl = 0;
                    FileProgress?.Invoke(e.Name, 0, "Startet …");
                    using var srcS = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.SequentialScan | FileOptions.Asynchronous);
                    using var dstS = new FileStream(dst, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous);
                    int read;
                    while ((read = await srcS.ReadAsync(buf, _cts.Token).ConfigureAwait(false)) > 0)
                    {
                        if (_cts.IsCancellationRequested) break;
                        await dstS.WriteAsync(buf.AsMemory(0, read), _cts.Token).ConfigureAwait(false);
                        copied += read; totalCopied += read;
                        double el = sw.Elapsed.TotalSeconds;
                        if (el - lastEl >= 0.4 || copied >= fs)
                        {
                            double bps = (copied - lastMark) / Math.Max(0.001, el - lastEl);
                            lastMark = copied; lastEl = el;
                            int pct = fs > 0 ? (int)((copied * 100L) / fs) : 0;
                            FileProgress?.Invoke(e.Name, pct, TransferFormat.BuildDetail(bps, copied, fs));
                            Progress?.Invoke(totalBytes > 0 ? (int)((totalCopied * 100L) / totalBytes) : 0, $"Kopiert {copiedCount + 1}/{total}…");
                        }
                    }
                    await dstS.FlushAsync().ConfigureAwait(false);
                    bool ok = !_cts.IsCancellationRequested && new FileInfo(dst).Length == fs;
                    if (!ok) { try { File.Delete(dst); } catch { } FileProgress?.Invoke(e.Name, 0, "Abgebrochen"); }
                    else     { FileProgress?.Invoke(e.Name, 100, "Fertig"); copiedCount++; }
                }
                Completed?.Invoke(!_cts.IsCancellationRequested, copiedCount, totalCopied, _cts.IsCancellationRequested ? "Abgebrochen" : "Fertig");
            }
            catch (Exception ex) { Completed?.Invoke(false, copiedCount, totalCopied, $"Fehler: {ex.Message}"); }
        });
    }

    /// <summary>
    /// Prüft die Erreichbarkeit der konfigurierten Download-URLs.
    ///
    /// FIX: Frühere Implementierung gab immer "OK" zurück (CheckUrlValidAsync = true).
    ///
    /// Neue Implementierung:
    ///   1. RemoteUrl (vom Versionscheck aufgelöste URL) — wird zuerst geprüft
    ///   2. Url (primäre DB-URL) — Fallback
    ///   3. Mirror1-5 — werden ebenfalls geprüft wenn primäre URL fehlschlägt
    ///
    /// Ergebnis: Einträge mit KEINER erreichbaren URL werden als "nicht erreichbar"
    ///   markiert und im UI entsprechend angezeigt.
    /// </summary>
    public sealed class UrlCheckWorker
    {
        private readonly IReadOnlyList<IsoEntry> _entries;
        private readonly CancellationTokenSource _cts = new();
        public event Action<int>?          ProgressPercent;
        public event Action<int, bool>?    EntryChecked;
        public event Action<bool, string>? Completed;
        public UrlCheckWorker(IReadOnlyList<IsoEntry> entries) => _entries = entries;
        public void Cancel() => _cts.Cancel();

        public Task RunAsync() => Task.Run(async () =>
        {
            try
            {
                int count = _entries.Count;
                for (int i = 0; i < count; i++)
                {
                    if (_cts.IsCancellationRequested) break;
                    var e = _entries[i];
                    bool ok = false;

                    // Alle konfigurierten URLs in Prioritätsreihenfolge prüfen.
                    // Sobald eine erreichbar ist → Eintrag gilt als "OK".
                    foreach (string url in e.AllDownloadUrls())
                    {
                        ok = await HttpService.Instance.IsReachableAsync(url, 6).ConfigureAwait(false);
                        if (ok) break;
                        if (_cts.IsCancellationRequested) break;
                    }

                    e.UrlOk = ok; e.UrlChecked = true;
                    EntryChecked?.Invoke(i, ok);
                    ProgressPercent?.Invoke(((i + 1) * 100) / count);
                }
                Completed?.Invoke(!_cts.IsCancellationRequested, _cts.IsCancellationRequested ? "Abgebrochen" : "OK");
            }
            catch (Exception ex) { Completed?.Invoke(false, ex.Message); }
        });
    }

    public sealed class VersionCheckEntryResult
    {
        public string Name          { get; init; } = string.Empty;
        public string LocalVersion  { get; init; } = string.Empty;
        public string RemoteVersion { get; init; } = string.Empty;
        public bool   HasUpdate     { get; init; }
        public bool   Resolved      { get; init; }
    }

    public sealed class AutoVersionCheckWorker
    {
        private readonly UpdateScanWorker _internalWorker;
        public event Action<int, List<int>>?          Completed;
        public event Action<int, int>?                Progress;
        public event Action<VersionCheckEntryResult>? EntryChecked;
        public bool AnyUrlDiscovered => _internalWorker.AnyUrlDiscovered;
        public AutoVersionCheckWorker(IReadOnlyList<IsoEntry> entries, string downloadDir = "")
        {
            _internalWorker = new UpdateScanWorker(entries, string.IsNullOrEmpty(downloadDir) ? AppPaths.Instance.DownloadDir : downloadDir, checkAllEntries: true);
            _internalWorker.Progress     += (c, t) => Progress?.Invoke(c, t);
            _internalWorker.EntryChecked += r => EntryChecked?.Invoke(r);
            _internalWorker.Completed    += (r, u) => Completed?.Invoke(r, u);
        }
        public void Cancel()   => _internalWorker.Cancel();
        public Task RunAsync() => _internalWorker.RunAsync();
    }

    public sealed class UpdateScanWorker
    {
        private readonly IReadOnlyList<IsoEntry> _entries;
        private readonly string                  _downloadDir;
        private readonly bool                    _checkAllEntries;
        private readonly CancellationTokenSource _cts = new();
        public event Action<int, int>?                Progress;
        public event Action<int, List<int>>?           Completed;
        public event Action<VersionCheckEntryResult>?  EntryChecked;

        // BUGFIX: HttpService.ResolveLatestAsync setzt bei einer neu entdeckten Quelle für einen
        // zuvor URL-losen Eintrag (typischerweise importiert/manuell hinzugefügt) entry.Url direkt
        // im Speicher (siehe "selbstlernende" Persistenz dort) — das allein reicht aber nicht, wenn
        // niemand danach _db.Save() aufruft. Bisher lösten Aufrufer das Speichern nur bei einem
        // ECHTEN Versions-Update aus (updates.Count > 0); eine neu gefundene URL für eine bereits
        // aktuelle ISO ist aber KEIN "Update" (gleiche Version wie vorhanden) und wurde dadurch nie
        // gespeichert. Ergebnis: die im Speicher gefundene URL ging bei jedem Neustart verloren,
        // und die aufwändige, netzwerklastige Auflösungskette (Websuche/DistroWatch/SourceForge)
        // musste jedes Mal komplett neu durchlaufen werden — mit entsprechend höherer Fehlerquote
        // bei jedem einzelnen Lauf. Dieses Flag macht "wurde etwas Dauerhaftes neu entdeckt" als
        // eigenes, von "gibt es ein Versions-Update" unabhängiges Signal sichtbar.
        public bool AnyUrlDiscovered { get; private set; }

        public UpdateScanWorker(IReadOnlyList<IsoEntry> entries, string downloadDir, bool checkAllEntries = false)
        { _entries = entries; _downloadDir = downloadDir; _checkAllEntries = checkAllEntries; }
        public void Cancel() => _cts.Cancel();
        public Task RunAsync() => Task.Run(async () =>
        {
            var updates = new List<int>(); int count = _entries.Count, resolved = 0, processed = 0;
            for (int i = 0; i < count; i++)
            {
                if (_cts.IsCancellationRequested) break;
                var e = _entries[i]; string localFn = e.Filename ?? string.Empty;
                if (!_checkAllEntries && !e.IsAvailableAnywhere(_downloadDir)) { Progress?.Invoke(++processed, count); continue; }
                bool urlWasEmpty = string.IsNullOrWhiteSpace(e.Url);
                var (remoteVer, url, fname) = await HttpService.Instance.ResolveLatestAsync(e).ConfigureAwait(false);
                Progress?.Invoke(++processed, count);
                bool res = !string.IsNullOrWhiteSpace(remoteVer) && !string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(fname);
                bool hasUpdate = false;
                if (res)
                {
                    resolved++; e.RemoteVersion = remoteVer; e.RemoteUrl = url; e.RemoteFilename = fname;
                    // WICHTIG: ein anderer Dateiname bedeutet NICHT automatisch eine neuere Version
                    // (Mirror-spezifische Benennung, Groß-/Kleinschreibung, Build-Suffixe …) — nur
                    // ein echter Versionsvergleich verhindert falsche "Update verfügbar"-Meldungen für
                    // ISOs, die bereits aktuell sind. Die vom Eintrag repräsentierte Version wird aus
                    // dem Dateinamen ODER (falls leer, z.B. per „ISO suchen“ hinzugefügt) aus dem Namen
                    // abgeleitet — so gilt "Foo 7.9.1" ohne Dateiname NICHT blind als Update, sobald
                    // online 7.9.1 gefunden wird (siehe HttpService.IsUpdateAvailable-Tests).
                    hasUpdate = HttpService.IsUpdateAvailable(
                        string.IsNullOrWhiteSpace(localFn) ? e.Name : localFn, remoteVer, !string.IsNullOrWhiteSpace(fname));
                    e.UpdateAvailable = hasUpdate;
                    if (hasUpdate) lock (updates) updates.Add(i);
                    if (urlWasEmpty && !string.IsNullOrWhiteSpace(e.Url)) AnyUrlDiscovered = true;
                }
                EntryChecked?.Invoke(new VersionCheckEntryResult
                {
                    Name          = e.Name,
                    LocalVersion  = HttpService.ExtractVersion(string.IsNullOrWhiteSpace(localFn) ? e.Name : localFn),
                    RemoteVersion = remoteVer,
                    HasUpdate     = hasUpdate,
                    Resolved      = res
                });
                // BUGFIX: ohne Pause feuert der Scan alle Anfragen (HEAD/GET an Origin-Server,
                // Suchanfragen an DuckDuckGo für unbekannte Distros) im Sekundentakt hintereinander
                // ab. Genau dieses Muster – viele automatisierte Anfragen kurz hintereinander an
                // denselben Dienst – ist es, was Bot-/Anti-Scraping-Schutz (Cloudflare-Tarpitting,
                // DuckDuckGo-Anomalieerkennung) triggert und dann fälschlich als "nicht erreichbar"
                // zurückkommt, obwohl die Quelle bei isolierter Prüfung erreichbar ist. Eine kleine,
                // gleichmäßige Pause zwischen Einträgen entschärft das generisch für JEDE Distro,
                // ohne distro-spezifisches Sonderverhalten.
                if (i < count - 1) await Task.Delay(300).ConfigureAwait(false);
            }
            Completed?.Invoke(resolved, updates);
        });
    }

    public sealed class UsbScanWorker
    {
        private readonly string                  _letter;
        private readonly IReadOnlyList<IsoEntry> _entries;
        public event Action<string, List<UsbService.StickIso>, List<UsbService.StickIso>>? Completed;
        public UsbScanWorker(string letter, IReadOnlyList<IsoEntry> entries) { _letter = letter; _entries = entries; }
        public Task RunAsync() => Task.Run(async () =>
        {
            var (found, incomplete) = await UsbService.Instance.ScanStickVerifiedAsync(_letter, _entries).ConfigureAwait(false);
            Completed?.Invoke(_letter, found, incomplete);
        });
    }

    public static class HttpServiceExtensions
    {
        public static Task<bool> CheckUrlValidAsync(this HttpService service, string url, CancellationToken token)
            => service.IsReachableAsync(url, 6);

        public static Task<string> GetLatestVentoyUrlAsync(this HttpService service)
            => Task.FromResult(string.Empty);

        /// <summary>
        /// Delegiert an HttpService.DownloadAsync mit dem konfigurierten
        /// Browser-User-Agent Singleton-Client (kein new HttpClient()!).
        /// </summary>
        public static Task<bool> DownloadFileAsync(
            this HttpService service, string url, string destPath,
            CancellationToken token, Action<int, string>? progress)
        {
            var iProgress = progress is null
                ? (IProgress<(int Percent, string Detail)>?)null
                : new Progress<(int Percent, string Detail)>(t => progress(t.Percent, t.Detail));
            return service.DownloadAsync(url, destPath, iProgress, token);
        }
    }
}
