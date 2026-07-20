using System;
using System.IO;

namespace ULM.Core.Services
{
    // Welche Verteilungsform gerade läuft — bestimmt, welches Release-Asset automatisch
    // heruntergeladen und wie das Update angewendet wird (Silent-Installer vs. Self-Replace).
    public enum InstallKind { Portable, Installed }

    public interface ISelfUpdateService
    {
        InstallKind DetectInstallKind(string currentExePath);
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
    }
}
