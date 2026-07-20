using System;
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
    }
}
