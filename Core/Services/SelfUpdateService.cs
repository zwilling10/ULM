using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ULM.Core.Models;

namespace ULM.Core.Services
{
    // Welche Verteilungsform gerade läuft — bestimmt, welches Release-Asset automatisch
    // heruntergeladen und wie das Update angewendet wird (Silent-Installer vs. Self-Replace).
    public enum InstallKind { Portable, Installed }

    public interface ISelfUpdateService
    {
        InstallKind DetectInstallKind(string currentExePath);
        Task<string?> DownloadUpdateAsync(UlmUpdateInfo info, InstallKind kind, string tempDir,
            IProgress<(int Percent, string Detail)>? progress, CancellationToken ct);
        void ApplyUpdateAndRestart(string downloadedFilePath, InstallKind kind, string currentExePath);
    }

    public sealed class SelfUpdateService : ISelfUpdateService
    {
        private static readonly Lazy<SelfUpdateService> _lazy = new(() => new SelfUpdateService());
        public static SelfUpdateService Instance => _lazy.Value;
        private SelfUpdateService() { }

        // Inno Setup legt den Deinstaller standardmäßig als "unins000.exe" neben die installierte
        // .exe ({app}\unins000.exe) — dank PrivilegesRequired=lowest in installer/ULM.iss landet die
        // Installation immer unter LocalAppData\Programs, eine Registry-Abfrage ist nicht nötig.
        internal const string InstalledMarkerFileName = "unins000.exe";

        public InstallKind DetectInstallKind(string currentExePath)
        {
            string? dir = Path.GetDirectoryName(currentExePath);
            if (!string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, InstalledMarkerFileName)))
                return InstallKind.Installed;
            return InstallKind.Portable;
        }

        // Wählt die zur erkannten Verteilungsform passende Asset-URL — reine Logik, testbar ohne
        // Netzwerk. Leer, falls das jeweilige Asset im Release fehlt (siehe UlmUpdateInfo-Doku).
        internal static string SelectDownloadUrl(UlmUpdateInfo info, InstallKind kind)
            => kind == InstallKind.Installed ? info.SetupExeUrl : info.PortableExeUrl;

        // Lädt automatisch die zur Verteilungsform passende Datei nach tempDir herunter (nutzt das
        // bestehende HttpService.DownloadAsync, kein neuer Download-Code). Liefert null, wenn kein
        // passendes Asset existiert, der Download fehlschlägt oder — falls eine Referenz-Prüfsumme
        // aus dem SHA256SUMS-Release-Asset vorliegt — der Hash nicht übereinstimmt (die Datei wird in
        // diesem Fall gelöscht statt potenziell manipuliert übernommen zu werden). Fehlt die
        // Referenz-Prüfsumme (älteres Release ohne SHA256SUMS-Asset), läuft der Download ungeprüft
        // durch wie bisher. Der Aufrufer fällt bei null in jedem Fall auf den bestehenden manuellen
        // UpdateDownloadDialog zurück.
        public async Task<string?> DownloadUpdateAsync(UlmUpdateInfo info, InstallKind kind, string tempDir,
            IProgress<(int Percent, string Detail)>? progress, CancellationToken ct)
        {
            string url = SelectDownloadUrl(info, kind);
            if (string.IsNullOrWhiteSpace(url)) return null;
            Directory.CreateDirectory(tempDir);
            string fileName = Path.GetFileName(new Uri(url).AbsolutePath);
            string dest = Path.Combine(tempDir, fileName);
            bool ok = await HttpService.Instance.DownloadAsync(url, dest, progress, ct).ConfigureAwait(false);
            if (!ok) return null;

            string? expectedHash = kind == InstallKind.Installed ? info.SetupSha256 : info.PortableSha256;
            if (!string.IsNullOrEmpty(expectedHash))
            {
                string actualHash = await IsoEntry.ComputeSha256Async(dest, ct).ConfigureAwait(false);
                if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine($"[SelfUpdate] Hash-Mismatch für {dest} — erwartet {expectedHash}, erhalten {actualHash}");
                    try { File.Delete(dest); } catch (Exception ex) { Debug.WriteLine($"[SelfUpdate] Löschen nach Hash-Mismatch fehlgeschlagen: {ex.Message}"); }
                    return null;
                }
            }
            return dest;
        }

        // Installer-Variante: startet das heruntergeladene Setup silent — installer/ULM.iss hat
        // CloseApplications=yes, das schliesst die laufende ULM-Instanz zuverlässig (per echtem
        // Restart-Manager-Log verifiziert: "RestartManager found an application..." /
        // "Shutting down applications using our files." greifen JEDES Mal). SmartScreen erscheint
        // hier weiterhin einmalig (unsigniertes Setup.exe) — das wird bewusst NICHT umgangen.
        //
        // BUGFIX (User-Report: "Neustart passiert nicht"): frühere Version verliess sich für den
        // Neustart auf Inno Setups RestartApplications=yes (Windows Restart Manager). Per echtem
        // Vergleichstest (gleicher Ablauf, /LOG ausgewertet) erwies sich das als unzuverlässig —
        // "Attempting to restart applications." im Setup-Log wurde nie von einer tatsächlichen
        // Restart-Zeile gefolgt, in den meisten Läufen blieb ULM einfach zu. Root Cause ist also
        // nicht ein falsches Kommandozeilen-Flag, sondern die Abhängigkeit von einem bekanntermassen
        // unzuverlässigen Windows-Mechanismus. Fix: denselben bereits für die Portable-Variante
        // bewährten Ansatz verwenden — ein externes PowerShell-Skript wartet UNABHÄNGIG von ULMs
        // eigenem Prozess (der ja gleich von CloseApplications beendet wird) auf das Setup-Ende und
        // startet ULM danach selbst neu, statt sich auf Inno Setups eigenen Restart zu verlassen.
        // installer/ULM.iss behält deshalb bewusst NUR CloseApplications=yes — RestartApplications
        // wurde entfernt, um bei den seltenen Fällen, in denen Inno doch selbst neu startet, keinen
        // doppelten ULM-Prozess zu riskieren.
        //
        // Portable-Variante: eine laufende .exe kann sich unter Windows nicht selbst überschreiben,
        // daher übernimmt ebenfalls ein per PowerShell gestartetes Hilfsskript das Kopieren nach
        // Prozessende.
        public void ApplyUpdateAndRestart(string downloadedFilePath, InstallKind kind, string currentExePath)
        {
            if (kind == InstallKind.Installed)
            {
                var setupProcess = Process.Start(new ProcessStartInfo(downloadedFilePath, "/SILENT /SUPPRESSMSGBOXES /NORESTART")
                { UseShellExecute = true });
                // ULM bleibt hier bewusst am Laufen: CloseApplications=yes braucht den Prozess beim
                // RM-Scan noch laufend vor, um ihn schliessen und dabei den Datei-Lock auf die eigene
                // .exe freigeben zu können. Der Neustart erfolgt NICHT mehr über RestartApplications
                // (siehe Kommentar oben), sondern über das unten gestartete, unabhängige Skript.
                if (setupProcess != null)
                {
                    string scriptPath = Path.Combine(Path.GetDirectoryName(downloadedFilePath)!, "apply_installed.ps1");
                    File.WriteAllText(scriptPath, BuildRestartAfterInstallScript(setupProcess.Id, currentExePath));
                    Process.Start(new ProcessStartInfo("powershell.exe",
                        $"-WindowStyle Hidden -ExecutionPolicy Bypass -File \"{scriptPath}\"")
                    { UseShellExecute = false, CreateNoWindow = true });
                }
                return;
            }

            string portableScriptPath = Path.Combine(Path.GetDirectoryName(downloadedFilePath)!, "apply.ps1");
            string script = BuildApplyScript(Environment.ProcessId, downloadedFilePath, currentExePath);
            File.WriteAllText(portableScriptPath, script);
            Process.Start(new ProcessStartInfo("powershell.exe",
                $"-WindowStyle Hidden -ExecutionPolicy Bypass -File \"{portableScriptPath}\"")
            { UseShellExecute = false, CreateNoWindow = true });
            System.Windows.Application.Current.Shutdown();
        }

        // Reine String-Erzeugung, testbar ohne Prozess-/Dateisystem-Zugriff. Wartet auf das Ende des
        // Setup-Prozesses (nicht auf ULM selbst — das wird ja parallel von CloseApplications beendet)
        // und startet danach die (vom Installer bereits ersetzte) Ziel-.exe neu. Kein Copy-Item nötig,
        // das übernimmt der Installer selbst — anders als bei der Portable-Variante.
        internal static string BuildRestartAfterInstallScript(int setupProcessId, string targetExePath)
        {
            string safeTargetExePath = targetExePath.Replace("'", "''");
            return
                $"while (Get-Process -Id {setupProcessId} -ErrorAction SilentlyContinue) {{ Start-Sleep -Milliseconds 300 }}\n" +
                // Kurzer Puffer: Setup meldet sein Prozessende ggf. bevor der Datei-Handle auf die
                // frisch installierte .exe vollständig freigegeben ist.
                "Start-Sleep -Milliseconds 500\n" +
                $"Start-Process -FilePath '{safeTargetExePath}'\n" +
                "Remove-Item -Path $PSCommandPath -ErrorAction SilentlyContinue\n";
        }

        // Reine String-Erzeugung, testbar ohne Prozess-/Dateisystem-Zugriff. Wartet auf Prozessende,
        // kopiert die neue Datei über die alte (Retry-Loop, da der Datei-Lock erst kurz nach
        // Prozessende freigegeben wird), startet ULM vom ursprünglichen Pfad neu und räumt auf.
        // Bricht der Kopierversuch dauerhaft ab (Datei weiterhin gesperrt), bleibt die alte .exe
        // unangetastet — kein Datenverlust, der Check läuft beim nächsten Start erneut.
        internal static string BuildApplyScript(int processId, string newExePath, string targetExePath)
        {
            string safeNewExePath = newExePath.Replace("'", "''");
            string safeTargetExePath = targetExePath.Replace("'", "''");
            return
                $"while (Get-Process -Id {processId} -ErrorAction SilentlyContinue) {{ Start-Sleep -Milliseconds 300 }}\n" +
                "for ($i = 0; $i -lt 20; $i++) {\n" +
                $"    try {{ Copy-Item -Path '{safeNewExePath}' -Destination '{safeTargetExePath}' -Force; break }}\n" +
                "    catch { Start-Sleep -Milliseconds 500 }\n" +
                "}\n" +
                $"Start-Process -FilePath '{safeTargetExePath}'\n" +
                $"Remove-Item -Path '{safeNewExePath}' -ErrorAction SilentlyContinue\n" +
                "Remove-Item -Path $PSCommandPath -ErrorAction SilentlyContinue\n";
        }
    }
}
