// Core/Services/IsoDatabaseService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ULM.Core.Models;
using ULM.Infrastructure;

namespace ULM.Core.Services
{
    /// <summary>
    /// Verwaltet die ISO-Datenbank: Laden, Speichern, CRUD-Operationen.
    /// INI-Format: [General] Count=N, [ISO_0] … [ISO_N-1]
    ///
    /// Mirror-Felder: Mirror1-5 werden gelesen/geschrieben.
    /// DefaultDatabase-Format: 12 Spalten
    ///   [Name, Category, Url, Filename, Mirror1, Mirror2, Mirror3,
    ///    Mirror4, Mirror5, GitHubRepo, GitHubAsset, Tip]
    /// </summary>
    public sealed class IsoDatabaseService
    {
        private static readonly Lazy<IsoDatabaseService> _lazy =
            new(() => new IsoDatabaseService());
        public static IsoDatabaseService Instance => _lazy.Value;

        private readonly AppPaths _paths = AppPaths.Instance;
        private readonly List<IsoEntry> _entries = new();

        private IsoDatabaseService() { }

        public IReadOnlyList<IsoEntry> Entries => _entries;
        public int Count => _entries.Count;

        // ── Laden ────────────────────────────────────────────────────────
        public void Load()
        {
            _entries.Clear();
            if (File.Exists(_paths.DatabaseIni)) LoadFromIni();
            else LoadDefaults();
        }

        // SICHERHEIT: Url/Mirror1-5/GitHubRepo werden hier ungeprüft übernommen und landen später
        // direkt in ausgehenden HTTP-Requests (HttpService.GitHubResolveUrlAsync, AllDownloadUrls,
        // IsReachableAsync). Für den Normalfall (Nutzer pflegt die eigene ulm_isos.ini über den
        // DB-Editor) ist das unproblematisch — der Nutzer vertraut sich selbst. Wird diese Datei
        // aber von woanders übernommen (z.B. eine "Community-Datenbank" von einer fremden Quelle
        // kopiert), könnten böswillige URLs beliebige Ziele referenzieren, die vom Rechner des
        // Nutzers aus erreichbar sind (z.B. interne Netzwerkadressen). ULM behandelt ulm_isos.ini
        // also implizit als vertrauenswürdig — sie sollte nur aus Quellen übernommen werden, denen
        // ebenso vertraut wird wie der ULM-Installation selbst.
        private void LoadFromIni()
        {
            var data = IniService.ReadAll(_paths.DatabaseIni);
            int count = 0;
            if (data.TryGetValue("General", out var general))
                int.TryParse(general.GetValueOrDefault("Count", "0"), out count);
            if (count <= 0)
                count = data.Keys.Count(k => k.StartsWith("ISO_", StringComparison.OrdinalIgnoreCase));

            for (int i = 0; i < count; i++)
            {
                string section = $"ISO_{i}";
                if (!data.TryGetValue(section, out var d)) continue;
                _entries.Add(new IsoEntry
                {
                    Name        = d.GetValueOrDefault("Name",        string.Empty),
                    Category    = d.GetValueOrDefault("Category",    "Einsteiger"),
                    Url         = d.GetValueOrDefault("URL",         string.Empty),
                    Filename    = d.GetValueOrDefault("Filename",    string.Empty),
                    Mirror1     = d.GetValueOrDefault("Mirror1",     string.Empty),
                    Mirror2     = d.GetValueOrDefault("Mirror2",     string.Empty),
                    Mirror3     = d.GetValueOrDefault("Mirror3",     string.Empty),
                    Mirror4     = d.GetValueOrDefault("Mirror4",     string.Empty),
                    Mirror5     = d.GetValueOrDefault("Mirror5",     string.Empty),
                    GithubRepo  = d.GetValueOrDefault("GitHubRepo",  string.Empty),
                    GithubAsset = d.GetValueOrDefault("GitHubAsset", string.Empty),
                    Tip         = d.GetValueOrDefault("Tip",         string.Empty).Replace("\\n", "\n"),
                    // BUGFIX (finaler Review): Sha256/Sha256Source wurden hier bisher NICHT gelesen
                    // und landeten dadurch nach jedem Neuladen als leerer String — die gesamte
                    // Integritaetspruefung (DetectVersionlessHashMismatchesAsync/
                    // VerifyStickIntegrityAsync) uebersprang danach stillschweigend jeden Eintrag.
                    // GetValueOrDefault liefert fuer ALTE ulm_isos.ini-Dateien ohne diese Keys
                    // klaglos string.Empty zurueck — kein Migrationsschritt noetig.
                    Sha256       = d.GetValueOrDefault("Sha256",       string.Empty),
                    Sha256Source = d.GetValueOrDefault("Sha256Source", string.Empty),
                });
            }

            if (_entries.Count == 0) LoadDefaults();
        }

        public void LoadDefaults()
        {
            _entries.Clear();
            foreach (var row in DefaultDatabase) { var e = new IsoEntry(); ApplyRow(e, row); _entries.Add(e); }
            Save();
        }

        // ── Speichern ────────────────────────────────────────────────────
        public void Save()
        {
            string? dir = Path.GetDirectoryName(_paths.DatabaseIni);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            sb.AppendLine("[General]");
            sb.AppendLine($"Count = {_entries.Count}");
            sb.AppendLine();

            for (int i = 0; i < _entries.Count; i++)
            {
                IsoEntry e = _entries[i];
                sb.AppendLine($"[ISO_{i}]");
                sb.AppendLine($"Name        = {e.Name}");
                sb.AppendLine($"Category    = {e.Category}");
                sb.AppendLine($"URL         = {e.Url}");
                sb.AppendLine($"Filename    = {e.Filename}");
                sb.AppendLine($"Mirror1     = {e.Mirror1}");
                sb.AppendLine($"Mirror2     = {e.Mirror2}");
                sb.AppendLine($"Mirror3     = {e.Mirror3}");
                sb.AppendLine($"Mirror4     = {e.Mirror4}");
                sb.AppendLine($"Mirror5     = {e.Mirror5}");
                sb.AppendLine($"GitHubRepo  = {e.GithubRepo}");
                sb.AppendLine($"GitHubAsset = {e.GithubAsset}");
                sb.AppendLine($"Tip         = {e.Tip.Replace("\n", "\\n")}");
                // BUGFIX (finaler Review): Sha256/Sha256Source wurden hier bisher NICHT geschrieben
                // (fester INI-Feld-Katalog statt Reflection-Serializer) — der Referenz-Hash ging bei
                // jedem Save() (z.B. nach Download/Stick-Import) stillschweigend verloren, sobald die
                // App neu startet und die Datenbank neu laedt. Siehe LoadFromIni() fuer die passende
                // Gegenseite.
                sb.AppendLine($"Sha256      = {e.Sha256}");
                sb.AppendLine($"Sha256Source = {e.Sha256Source}");
                sb.AppendLine();
            }

            File.WriteAllText(_paths.DatabaseIni, sb.ToString(), Encoding.UTF8);
        }

        public void SaveFilenames()
        {
            for (int i = 0; i < _entries.Count; i++)
                if (!string.IsNullOrWhiteSpace(_entries[i].Filename))
                    IniService.Write(_paths.DatabaseIni, $"ISO_{i}", "Filename", _entries[i].Filename);
        }

        // ── CRUD ─────────────────────────────────────────────────────────
        public void Add(IsoEntry entry)    => _entries.Add(entry);
        public void Remove(int index)      { if (index >= 0 && index < _entries.Count) _entries.RemoveAt(index); }
        public void MoveUp(int index)      { if (index > 0 && index < _entries.Count) (_entries[index], _entries[index - 1]) = (_entries[index - 1], _entries[index]); }
        public void MoveDown(int index)    { if (index >= 0 && index < _entries.Count - 1) (_entries[index], _entries[index + 1]) = (_entries[index + 1], _entries[index]); }

        // ── Standard-Datenbank ───────────────────────────────────────────
        // 12 Spalten pro Zeile:
        // [0] Name   [1] Category  [2] Url      [3] Filename
        // [4] Mirror1 [5] Mirror2  [6] Mirror3  [7] Mirror4   [8] Mirror5
        // [9] GitHubRepo  [10] GitHubAsset  [11] Tip
        private static readonly string[][] DefaultDatabase =
        {
            // ── Antivirus ─────────────────────────────────────────────────
            ["Dr.Web LiveDisk 9.0.1", "Antivirus",
             "", "drweb-livedisk-900-cd.iso", "", "", "", "", "", "", "",
             "🛡 Dr.Web LiveDisk — Antivirus Live-System\n" +
             "✅ Bootet OHNE Installation (Debian-basiert)\n" +
             "• Erkennt und entfernt Malware, Viren, Trojaner\n" +
             "• Web: https://free.drweb.com/livedisk/"],

            // ── Rettung ───────────────────────────────────────────────────
            ["SystemRescue 13.00", "Rettung",
             "", "systemrescue-13.00-amd64.iso", "", "", "", "", "", "", "",
             "🛠 SystemRescue — Rettungssystem\n" +
             "✅ Bootet OHNE Installation (Arch-basiert)\n" +
             "• GParted, TestDisk, PhotoRec, fsck, Partclone\n" +
             "• Web: https://www.system-rescue.org/"],

            ["GParted Live 1.8.1-2", "Rettung",
             "", "gparted-live-1.8.1-2-amd64.iso", "", "", "", "", "", "", "",
             "💽 GParted Live — Partitionierung\n" +
             "✅ Bootet OHNE Installation (grafisch)\n" +
             "• Partitionen erstellen, verschieben, vergrößern\n" +
             "• Web: https://gparted.org/"],

            ["Clonezilla 3.3.0-33", "Rettung",
             "", "clonezilla-live-3.3.0-33-amd64.iso", "", "", "", "", "", "", "",
             "💾 Clonezilla — Backup & Systemklon\n" +
             "✅ Bootet OHNE Installation (Text-Menü)\n" +
             "• Disk-Klon und Image-Backup\n" +
             "• Web: https://clonezilla.org/"],

            ["Rescuezilla 2.6.1", "Rettung",
             "", "rescuezilla-2.6.1-64bit.oracular.iso", "", "", "", "", "",
             "rescuezilla/rescuezilla", "rescuezilla-*-64bit*.iso",
             "🖥 Rescuezilla — Grafisches Backup-Tool\n" +
             "✅ Bootet OHNE Installation (Ubuntu Live-Desktop)\n" +
             "• Clonezilla-kompatibel mit GUI\n" +
             "• Web: https://rescuezilla.com/"],

            ["Finnix 251", "Rettung",
             "", "finnix-251.iso", "", "", "", "", "", "", "",
             "🛠 Finnix — Systemwartungs-Live-CD\n" +
             "✅ Bootet OHNE Installation (Debian Sid, Bash)\n" +
             "• Festplattenreparatur, Diagnose, Datenrettung\n" +
             "• Web: https://www.finnix.org/"],

            // ── Leichtgewicht ─────────────────────────────────────────────
            ["Debian 13.4.0 Trixie Live XFCE", "Leichtgewicht",
             "", "debian-live-13.4.0-amd64-xfce.iso", "", "", "", "", "", "", "",
             "🖥 Debian — Das stabilste Linux\n" +
             "✅ Bootet OHNE Installation (XFCE)\n" +
             "• Läuft ab 512 MB RAM\n" +
             "• Web: https://www.debian.org/"],

            ["Linux Mint 22.3 (MATE)", "Leichtgewicht",
             "", "linuxmint-22.3-mate-64bit.iso", "", "", "", "", "", "", "",
             "🖥 Linux Mint MATE — Bestes für Windows-Umsteiger\n" +
             "✅ Bootet OHNE Installation\n" +
             "• Klassische Taskleiste wie Windows\n" +
             "• Web: https://linuxmint.com/"],

            ["Linux Mint 22.3 (XFCE)", "Leichtgewicht",
             "", "linuxmint-22.3-xfce-64bit.iso", "", "", "", "", "", "", "",
             "🖥 Linux Mint XFCE — Schlank & schnell\n" +
             "✅ Bootet OHNE Installation\n" +
             "• Ideal für ältere Hardware\n" +
             "• Web: https://linuxmint.com/"],

            ["Lubuntu 24.04.4 LTS", "Leichtgewicht",
             "", "lubuntu-24.04.4-desktop-amd64.iso", "", "", "", "", "", "", "",
             "🪶 Lubuntu — Ubuntu ultraleicht (LXQt)\n" +
             "✅ Bootet OHNE Installation\n" +
             "• Läuft auf 512 MB RAM\n" +
             "• Web: https://lubuntu.me/"],

            ["Manjaro 26.0.3 (XFCE)", "Leichtgewicht",
             "", "manjaro-xfce-26.0.3-260228-linux618.iso", "", "", "", "", "", "", "",
             "⚙ Manjaro XFCE — Arch-basiert, sehr ressourcenschonend\n" +
             "✅ Bootet OHNE Installation\n" +
             "• Web: https://manjaro.org/"],

            ["MX Linux 25.1 XFCE", "Leichtgewicht",
             "", "MX-25.1_Xfce_x64.iso", "", "", "", "", "", "", "",
             "🪶 MX Linux — #1 DistroWatch, sehr effizient\n" +
             "✅ Bootet OHNE Installation (XFCE)\n" +
             "• Web: https://mxlinux.org/"],

            // ── Einsteiger ────────────────────────────────────────────────
            ["Ubuntu 26.04 LTS", "Einsteiger",
             "", "ubuntu-26.04-desktop-amd64.iso", "", "", "", "", "", "", "",
             "🖥 Ubuntu — Weltweite #1\n" +
             "✅ Bootet OHNE Installation (GNOME)\n" +
             "• 5 Jahre LTS-Support\n" +
             "• Web: https://ubuntu.com/"],

            ["Linux Mint 22.3 (Cinnamon)", "Einsteiger",
             "", "linuxmint-22.3-cinnamon-64bit.iso", "", "", "", "", "", "", "",
             "🖥 Linux Mint Cinnamon — Modern & elegant\n" +
             "✅ Bootet OHNE Installation\n" +
             "• Web: https://linuxmint.com/"],

            ["Zorin OS 18 Core", "Einsteiger",
             "", "Zorin-OS-18-Core-64-bit-r3.iso", "", "", "", "", "", "", "",
             "🖥 Zorin OS — Schönster Windows-Ersatz\n" +
             "✅ Bootet OHNE Installation\n" +
             "• Windows-11-ähnliches Layout\n" +
             "• Web: https://zorin.com/os/"],

            ["Pop!_OS 24.04 LTS NVIDIA", "Einsteiger",
             "", "pop-os_24.04_amd64_nvidia_9.iso", "", "", "", "", "", "", "",
             "🖥 Pop!_OS — NVIDIA-optimierter Live-Desktop\n" +
             "✅ Bootet OHNE Installation\n" +
             "• NVIDIA-Treiber direkt im ISO\n" +
             "• Web: https://pop.system76.com/"],

            // ── Fortgeschrittene ──────────────────────────────────────────
            ["Fedora 44 Workstation", "Fortgeschrittene",
             "", "Fedora-Workstation-Live-44-1.7.x86_64.iso", "", "", "", "", "", "", "",
             "⚙ Fedora — Modernste GNOME-Distribution\n" +
             "✅ Bootet OHNE Installation\n" +
             "• Cutting Edge — neueste Technologien\n" +
             "• Web: https://fedoraproject.org/"],

            ["Manjaro 26.0.3 (KDE)", "Fortgeschrittene",
             "", "manjaro-kde-26.0.3-260228-linux618.iso", "", "", "", "", "", "", "",
             "⚙ Manjaro KDE — Arch-Linux mit KDE Plasma 6\n" +
             "✅ Bootet OHNE Installation\n" +
             "• AUR, Rolling Release\n" +
             "• Web: https://manjaro.org/"],

            ["Manjaro 26.0.3 (GNOME)", "Fortgeschrittene",
             "", "manjaro-gnome-26.0.3-260228-linux618.iso", "", "", "", "", "", "", "",
             "⚙ Manjaro GNOME — Arch-Linux mit GNOME\n" +
             "✅ Bootet OHNE Installation\n" +
             "• Web: https://manjaro.org/"],

            ["EndeavourOS Titan 2026.03", "Fortgeschrittene",
             "", "EndeavourOS_Titan-2026.03.06.iso", "", "", "", "",
             "", "EndeavourOS/ISO", "EndeavourOS_*.iso",
             "🚀 EndeavourOS — Arch Linux mit Live-Desktop\n" +
             "✅ Echter Live-Boot, Rolling Release, AUR\n" +
             "• Web: https://endeavouros.com/"],

            // ── Sicherheit ────────────────────────────────────────────────
            ["Tails 7.7.1", "Sicherheit",
             "", "tails-amd64-7.7.1.iso", "", "", "", "", "", "", "",
             "🔒 Tails — Maximum Privatsphäre\n" +
             "✅ Bootet OHNE Installation (RAM-only)\n" +
             "• Alle Verbindungen über Tor\n" +
             "• Web: https://tails.net/"],

            ["Parrot Security 7.1", "Sicherheit",
             "", "Parrot-security-7.1_amd64.iso", "", "", "", "", "", "", "",
             "🔒 Parrot Security — Pen-Testing & Forensik\n" +
             "✅ Bootet OHNE Installation (MATE)\n" +
             "• Web: https://www.parrotsec.org/"],

            // Linux Kodachi: 5 Mirror konfiguriert für maximale Ausfallsicherheit.
            // Mirror1-2: SourceForge CDN (sehr stabil, weltweite Edge-Server)
            // Mirror3-5: kodachi.cloud CDN-Knoten (schneller bei DE-Nutzern)
            // Hinweis: URLs enthalten die Version — nach Versionsupdate via
            // "Versionscheck" werden Url+Filename aktualisiert.
            // Mirror1-5 mit veralteter Version fallen automatisch auf newer
            // Url-Feld zurück (AllDownloadUrls: resolvedUrl wird zuerst versucht).
            ["Linux Kodachi 9.0.1", "Sicherheit",
             "https://kodachi.cloud/downloads/linux-kodachi-xfce-9.0.1-amd64.iso",
             "linux-kodachi-xfce-9.0.1-amd64.iso",
             // Mirror1: SourceForge Köln (DE, sehr schnell)
             "https://netcologne.dl.sourceforge.net/project/linuxkodachi/kodachi-desktop/linux-kodachi-xfce-9.0.1-amd64.iso?viasf=1",
             // Mirror2: SourceForge Master
             "https://master.dl.sourceforge.net/project/linuxkodachi/kodachi-desktop/linux-kodachi-xfce-9.0.1-amd64.iso?viasf=1",
             // Mirror3: SourceForge Download (direkter Link)
             "https://downloads.sourceforge.net/project/linuxkodachi/kodachi-desktop/linux-kodachi-xfce-9.0.1-amd64.iso",
             // Mirror4: kodachi.cloud CDN-1
             "https://cdn.kodachi.cloud/downloads/linux-kodachi-xfce-9.0.1-amd64.iso",
             // Mirror5: kodachi.cloud CDN-2
             "https://downloads.kodachi.cloud/linux-kodachi-xfce-9.0.1-amd64.iso",
             "", "",
             "🔒 Linux Kodachi — Privacy & Anonymität\n" +
             "• Tor, I2P, VPN, DNSCrypt vorinstalliert\n" +
             "• RAM-only Modus: hinterlässt keine Spuren\n" +
             "• 5 Mirror konfiguriert (SourceForge + kodachi.cloud CDN)\n" +
             "• Web: https://www.digi77.com/linux-kodachi/"],

            // ── Gaming ────────────────────────────────────────────────────
            ["Nobara Linux 41 (Gaming GNOME)", "Gaming",
             "", "Nobara-41-Official-2024-12-31.iso", "", "", "", "", "", "", "",
             "🎮 Nobara Linux — Gaming-Distro (ProtonGE)\n" +
             "✅ Bootet OHNE Installation\n" +
             "• ProtonGE, Steam, Lutris, OBS, MangoHud vorinstalliert\n" +
             "• Web: https://nobaraproject.org/"],

            ["CachyOS 2026.03 (Gaming KDE)", "Gaming",
             "", "cachyos-desktop-linux-260308.iso", "", "", "", "",
             "", "cachyos/cachyos-cachy-iso", "cachyos-desktop-linux-*.iso",
             "🎮 CachyOS — Performance-Linux für Gaming\n" +
             "✅ Bootet OHNE Installation (KDE Plasma 6)\n" +
             "• x86-64-v3/v4-Optimierung + BORE/EEVDF Scheduler\n" +
             "• Web: https://cachyos.org/"],

            ["Ubuntu GamePack 24.04", "Gaming",
             "", "ubuntu_game_pack-24.04-amd64.iso", "", "", "", "", "", "", "",
             "🎮 Ubuntu GamePack — Gaming-Ubuntu\n" +
             "✅ Bootet OHNE Installation (GNOME)\n" +
             "• Steam, Lutris, PlayOnLinux vorinstalliert\n" +
             "• Web: https://ualinux.com/"],

            // ── WinPE ─────────────────────────────────────────────────────
            ["Hiren's BootCD PE x64 v1.0.8", "WinPE",
             "", "HBCD_PE_x64.iso", "", "", "", "", "", "", "",
             "🪟 Hiren's BootCD PE — Windows-Rettungssystem\n" +
             "✅ Win10 PE x64 — Bootet vollständig im RAM\n" +
             "• Über 100 Diagnose-, Recovery- und Dateitools\n" +
             "• Web: https://www.hirensbootcd.org/"],
        };

        private static void ApplyRow(IsoEntry e, string[] row)
        {
            // Format: [Name, Cat, Url, Filename, M1, M2, M3, M4, M5, GitHubRepo, GitHubAsset, Tip]
            // Indizes: [0]   [1]  [2]  [3]       [4] [5] [6] [7] [8] [9]         [10]          [11]
            e.Name        = row.ElementAtOrDefault(0)  ?? string.Empty;
            e.Category    = row.ElementAtOrDefault(1)  ?? "Einsteiger";
            e.Url         = row.ElementAtOrDefault(2)  ?? string.Empty;
            e.Filename    = row.ElementAtOrDefault(3)  ?? string.Empty;
            e.Mirror1     = row.ElementAtOrDefault(4)  ?? string.Empty;
            e.Mirror2     = row.ElementAtOrDefault(5)  ?? string.Empty;
            e.Mirror3     = row.ElementAtOrDefault(6)  ?? string.Empty;
            e.Mirror4     = row.ElementAtOrDefault(7)  ?? string.Empty;
            e.Mirror5     = row.ElementAtOrDefault(8)  ?? string.Empty;
            e.GithubRepo  = row.ElementAtOrDefault(9)  ?? string.Empty;
            e.GithubAsset = row.ElementAtOrDefault(10) ?? string.Empty;
            e.Tip         = (row.ElementAtOrDefault(11) ?? string.Empty).Replace("\\n", "\n");
        }
    }
}
