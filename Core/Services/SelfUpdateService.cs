using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

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
        // passendes Asset existiert oder der Download fehlschlägt — der Aufrufer fällt dann auf den
        // bestehenden manuellen UpdateDownloadDialog zurück.
        public async Task<string?> DownloadUpdateAsync(UlmUpdateInfo info, InstallKind kind, string tempDir,
            IProgress<(int Percent, string Detail)>? progress, CancellationToken ct)
        {
            string url = SelectDownloadUrl(info, kind);
            if (string.IsNullOrWhiteSpace(url)) return null;
            Directory.CreateDirectory(tempDir);
            string fileName = Path.GetFileName(new Uri(url).AbsolutePath);
            string dest = Path.Combine(tempDir, fileName);
            bool ok = await HttpService.Instance.DownloadAsync(url, dest, progress, ct).ConfigureAwait(false);
            return ok ? dest : null;
        }

        // Installer-Variante: startet das heruntergeladene Setup silent — installer/ULM.iss hat
        // CloseApplications=yes/RestartApplications=yes, das schließt laufende ULM-Instanzen
        // zuverlässig und startet sie nach der Installation automatisch neu. SmartScreen erscheint
        // hier weiterhin einmalig (unsigniertes Setup.exe) — das wird bewusst NICHT umgangen.
        //
        // Portable-Variante: eine laufende .exe kann sich unter Windows nicht selbst überschreiben,
        // daher übernimmt ein per PowerShell gestartetes Hilfsskript das Kopieren nach Prozessende.
        public void ApplyUpdateAndRestart(string downloadedFilePath, InstallKind kind, string currentExePath)
        {
            if (kind == InstallKind.Installed)
            {
                Process.Start(new ProcessStartInfo(downloadedFilePath, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART")
                { UseShellExecute = true });
            }
            else
            {
                string scriptPath = Path.Combine(Path.GetDirectoryName(downloadedFilePath)!, "apply.ps1");
                string script = BuildApplyScript(Environment.ProcessId, downloadedFilePath, currentExePath);
                File.WriteAllText(scriptPath, script);
                Process.Start(new ProcessStartInfo("powershell.exe",
                    $"-WindowStyle Hidden -ExecutionPolicy Bypass -File \"{scriptPath}\"")
                { UseShellExecute = false, CreateNoWindow = true });
            }
            System.Windows.Application.Current.Shutdown();
        }

        // Reine String-Erzeugung, testbar ohne Prozess-/Dateisystem-Zugriff. Wartet auf Prozessende,
        // kopiert die neue Datei über die alte (Retry-Loop, da der Datei-Lock erst kurz nach
        // Prozessende freigegeben wird), startet ULM vom ursprünglichen Pfad neu und räumt auf.
        // Bricht der Kopierversuch dauerhaft ab (Datei weiterhin gesperrt), bleibt die alte .exe
        // unangetastet — kein Datenverlust, der Check läuft beim nächsten Start erneut.
        internal static string BuildApplyScript(int processId, string newExePath, string targetExePath) =>
            $"while (Get-Process -Id {processId} -ErrorAction SilentlyContinue) {{ Start-Sleep -Milliseconds 300 }}\n" +
            "for ($i = 0; $i -lt 20; $i++) {\n" +
            $"    try {{ Copy-Item -Path '{newExePath}' -Destination '{targetExePath}' -Force; break }}\n" +
            "    catch { Start-Sleep -Milliseconds 500 }\n" +
            "}\n" +
            $"Start-Process -FilePath '{targetExePath}'\n" +
            $"Remove-Item -Path '{newExePath}' -ErrorAction SilentlyContinue\n" +
            "Remove-Item -Path $PSCommandPath -ErrorAction SilentlyContinue\n";
    }
}
