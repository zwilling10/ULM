// Core/Services/UsbService.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ULM.Core.Models;
using ULM.Infrastructure;

namespace ULM.Core.Services
{
    public interface IUsbService
    {
        List<UsbDrive> ListRemovableDrives();
        Task<(List<UsbService.StickIso> Found, List<UsbService.StickIso> Incomplete)> ScanStickVerifiedAsync(string letter, IReadOnlyList<IsoEntry> entries);
        List<RawUsbDiskCandidate> ListUsbDisksNeedingPreparation();
        bool PrepareRawUsbDisk(int diskIndex, char letter);
    }

    public sealed class UsbService : IUsbService
    {
        private static readonly Lazy<UsbService> _lazy = new(() => new UsbService());
        public static UsbService Instance => _lazy.Value;
        private UsbService() { }

        [DllImport("shell32.dll")]
        private static extern bool IsUserAnAdmin();

        public static bool IsAdmin()
        {
            try { return IsUserAnAdmin(); }
            catch { return false; }
        }

        // ── Laufwerke aufzählen ───────────────────────────────────────────
        public List<UsbDrive> ListRemovableDrives()
        {
            var result = new List<UsbDrive>();
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                foreach (string baseDir in new[]
                { $"/media/{Environment.UserName}", $"/run/media/{Environment.UserName}" })
                {
                    if (!Directory.Exists(baseDir)) continue;
                    foreach (string dir in Directory.EnumerateDirectories(baseDir))
                        result.Add(new UsbDrive(dir, Path.GetFileName(dir), 0, string.Empty));
                }
                return result;
            }

            // DriveType 2 = Wechseldatenträger (leer formatierte/Ventoy-Sticks). DriveType 5 =
            // CD-ROM — Windows stuft mit Rufus im ISO/DD-Image-Modus beschriebene Sticks (üblich
            // für die meisten Linux-Live-ISOs) oft als optisches Medium ein, obwohl es physisch
            // ein USB-Stick ist, weil das Image ein hybrides ISO ist. Ohne DriveType 5 wurden
            // solche Sticks nie erkannt — kein "neuer Stick"-Dialog, kein Ventoy-Angebot (Nutzer-
            // Testfeedback). Der bestehende ≥2GB-Größenfilter unten schließt echte, leere optische
            // Laufwerke (kein Medium → Size=0) weiterhin zuverlässig aus.
            const string script = @"
$vols = Get-CimInstance Win32_LogicalDisk | Where-Object { $_.DriveType -eq 2 -or $_.DriveType -eq 5 }
foreach ($v in $vols) {
  $id=$v.DeviceID; $label=$v.VolumeName; $size=[int64]($v.Size); $fs=$v.FileSystem
  if ($id -and $size -ge 2000000000 -and $label -notmatch '^(VTOYEFI|EFI|ESP)$') {
    Write-Output ($id.ToUpper() + '|' + $label + '|' + $size + '|' + $fs)
  }
}";
            string output = RunPowerShell(script, 8);
            foreach (string line in output.Split('\n',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] parts = line.Split('|');
                if (parts.Length < 3) continue;
                result.Add(new UsbDrive(
                    parts[0].ToUpperInvariant(),
                    parts.Length > 1 ? parts[1] : string.Empty,
                    parts.Length > 2 && long.TryParse(parts[2], out long s) ? s : 0L,
                    parts.Length > 3 ? parts[3] : string.Empty));
            }
            result.Sort((a, b) =>
            {
                bool aV = a.Label.Equals("ventoy", StringComparison.OrdinalIgnoreCase);
                bool bV = b.Label.Equals("ventoy", StringComparison.OrdinalIgnoreCase);
                return aV != bV ? (aV ? -1 : 1) : b.SizeBytes.CompareTo(a.SizeBytes);
            });
            return result;
        }

        public static string ListSignature(IEnumerable<UsbDrive> drives) =>
            string.Join(";", drives.Select(d => d.Letter.ToUpperInvariant()));

        // ── Rohe (buchstabenlose) USB-Datenträger: Erkennung ───────────────
        // Ermittelt den Win32_DiskDrive.Index des Datenträgers, der die Windows-Systempartition
        // (%SystemDrive%, i.d.R. C:) enthält — Grundlage für die Systemdatenträger-Sperre in
        // IsSafeToPrepare. Gibt null zurück, wenn die Ermittlung fehlschlägt (WMI-Fehler o.ä.) —
        // Aufrufer MÜSSEN das als "unsicher" behandeln (siehe IsSafeToPrepare-Kommentar).
        private static int? GetSystemDiskIndex()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return null;
            // BUGFIX: Ursprünglich per -Filter "DeviceID='$($env:SystemDrive)'" — RunPowerShell()
            // escaped ALLE doppelten Anführungszeichen im gesamten Skript blind (für die äußere
            // cmd/PowerShell-Argumentweitergabe), was dieses eingebettete Filter-Anführungszeichen
            // zerstörte und die Ausführung mit einem ParameterBindingException-Fehler abbrechen
            // ließ (empirisch reproduziert). GetSystemDiskIndex() gab dadurch IMMER null zurück —
            // ListUsbDisksNeedingPreparation() bricht bei null bewusst fail-closed mit einer leeren
            // Liste ab (siehe dort), wodurch die gesamte Erkennung nie anschlug, auch nicht beim
            // Nutzer-Testlauf. Where-Object statt -Filter braucht keine eingebetteten
            // Anführungszeichen und ist von diesem Escaping-Problem nicht betroffen.
            const string script = @"
$sys = Get-CimInstance Win32_LogicalDisk | Where-Object { $_.DeviceID -eq $env:SystemDrive }
if ($sys) {
  $sysPart = Get-CimAssociatedInstance -InputObject $sys -Association Win32_LogicalDiskToPartition -ErrorAction SilentlyContinue
  if ($sysPart) {
    $sysDisk = Get-CimAssociatedInstance -InputObject $sysPart -Association Win32_DiskDriveToDiskPartition -ErrorAction SilentlyContinue
    if ($sysDisk) { Write-Output $sysDisk.Index }
  }
}";
            string output = RunPowerShell(script, 8).Trim();
            return int.TryParse(output, out int idx) ? idx : null;
        }

        /// <summary>
        /// Erkennt physische USB-Datenträger, die noch VORBEREITET werden müssen, bevor sie als
        /// normales Laufwerk nutzbar sind — das sind zwei Fälle: (1) gar kein Laufwerksbuchstabe
        /// zugewiesen (z.B. mit Rufus im ISO/DD-Modus beschriebene Sticks, die in
        /// ListRemovableDrives() (basiert auf Win32_LogicalDisk, listet nur Datenträger MIT
        /// Buchstabe UND Dateisystem) nie auftauchen), UND (2) ein Buchstabe ist zwar zugewiesen,
        /// aber ohne erkanntes Dateisystem (RAW) — der Zustand, in dem ein Datenträger stecken
        /// bleiben kann, wenn ein vorheriger PrepareRawUsbDisk-Aufruf den Buchstaben zwar
        /// zugewiesen, das eigentliche Formatieren aber nicht geschafft hat (siehe BUGFIX-Kommentar
        /// bei IsFormattedFileSystem) — sonst bliebe so ein Stick für immer unsichtbar, weder hier
        /// noch in ListRemovableDrives() erkannt. Win32_DiskDrive arbeitet auf physischer Ebene,
        /// unabhängig von Laufwerksbuchstaben — die WMI-Entsprechung dessen, was Rufus selbst über
        /// SetupDiGetClassDevs/IOCTL_STORAGE_QUERY_PROPERTY auf niedrigerer Ebene macht.
        /// Schlägt die Systemdatenträger-Ermittlung fehl, wird eine leere Liste zurückgegeben
        /// (fail-closed) statt ungeprüft alle USB-Datenträger anzubieten.
        /// </summary>
        public List<RawUsbDiskCandidate> ListUsbDisksNeedingPreparation()
        {
            var result = new List<RawUsbDiskCandidate>();
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return result;

            int? systemDiskIndex = GetSystemDiskIndex();
            if (systemDiskIndex is null) return result;

            const string script = @"
$disks = Get-CimInstance Win32_DiskDrive | Where-Object { $_.InterfaceType -eq 'USB' }
foreach ($d in $disks) {
  $hasFileSystem = $false
  $parts = Get-CimAssociatedInstance -InputObject $d -Association Win32_DiskDriveToDiskPartition -ErrorAction SilentlyContinue
  foreach ($p in $parts) {
    $lds = Get-CimAssociatedInstance -InputObject $p -Association Win32_LogicalDiskToPartition -ErrorAction SilentlyContinue
    foreach ($ld in $lds) {
      if ($ld.FileSystem) { $hasFileSystem = $true }
    }
  }
  if (-not $hasFileSystem) {
    Write-Output ($d.Index.ToString() + '|' + [int64]$d.Size)
  }
}";
            string output = RunPowerShell(script, 10);
            foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] parts = line.Split('|');
                if (parts.Length < 2) continue;
                if (!int.TryParse(parts[0], out int idx)) continue;
                if (!long.TryParse(parts[1], out long size)) continue;
                if (size < 2_000_000_000) continue;
                if (!IsSafeToPrepare(idx, systemDiskIndex)) continue;
                result.Add(new RawUsbDiskCandidate(idx, size));
            }
            return result;
        }

        /// <summary>
        /// Bereitet einen rohen (buchstabenlosen) USB-Datenträger für die normale Erkennung vor:
        /// komplett neu partitionieren und formatieren, damit Windows ihm einen Laufwerksbuchstaben
        /// zuweist. fs=fat32 (nicht exfat) ist bewusst gewählt — dieser Schritt dient NUR dazu,
        /// Windows zur Buchstaben-Zuweisung zu bewegen; Ventoy2Disk formatiert den Stick bei der
        /// eigentlichen Einrichtung ohnehin komplett neu (siehe VentoyInstallWorker).
        ///
        /// SICHERHEIT: Zweite, unabhängige Systemdatenträger-Prüfung unmittelbar vor dem
        /// destruktiven diskpart-Aufruf — zusätzlich zur bereits in ListUsbDisksNeedingPreparation()
        /// erfolgten Prüfung. Verhindert, dass sich zwischen Anzeige/Erkennung und tatsächlicher
        /// Ausführung etwas an den Datenträger-Indizes geändert hat (z.B. durch ein zwischenzeitlich
        /// weiteres eingestecktes Laufwerk).
        /// </summary>
        /// <summary>
        /// BUGFIX: diskpart braucht für destruktive Datenträger-Operationen (clean/create
        /// partition/format) immer erhöhte Rechte — genau wie z.B. Rufus selbst beim Start per UAC
        /// danach fragt, bevor es überhaupt auf einen Datenträger zugreift. Die normale ULM-Instanz
        /// läuft bewusst NICHT elevated (asInvoker, siehe UniversalLinuxManager.csproj-Kommentar).
        /// UseShellExecute=true + Verb="runas" löst die UAC-Abfrage NUR für diesen einen
        /// diskpart-Aufruf aus (kein Neustart der ganzen ULM-App nötig, anders als beim bestehenden
        /// --ventoy-install-Mechanismus). Nachteil: UseShellExecute=true erlaubt kein
        /// Ein-/Ausgabe-Umleiten (RedirectStandardOutput/CreateNoWindow) — nicht weiter schlimm, da
        /// der Rückgabewert ohnehin nur über den ExitCode ausgewertet wird; ein diskpart-
        /// Konsolenfenster blitzt dabei kurz sichtbar auf.
        ///
        /// Lehnt der Nutzer die UAC-Abfrage ab (Win32Exception 1223) oder ist Windows aus einem
        /// anderen Grund am Starten von diskpart gehindert (z.B. 740 = fehlende Rechte, falls die
        /// UAC-Infrastruktur selbst deaktiviert ist), MUSS das als gescheiterte Vorbereitung gelten
        /// (false zurückgeben, loggen) statt die App abstürzen zu lassen — genau der Fehler, der im
        /// ersten echten Testlauf aufgetreten ist (Process.Start warf eine unbehandelte
        /// Win32Exception, die bis zum globalen DispatcherUnhandledException-Handler durchschlug).
        /// </summary>
        public bool PrepareRawUsbDisk(int diskIndex, char letter)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;

            int? systemDiskIndex = GetSystemDiskIndex();
            if (!IsSafeToPrepare(diskIndex, systemDiskIndex)) return false;

            // fs=ntfs statt fat32: FAT32 wird von Windows' format-Tools grundsätzlich ab 32 GB
            // verweigert (feste Microsoft-Beschränkung). ntfs quick hat keine Größenbegrenzung und
            // ist genauso schnell; die Wahl des Dateisystems ist hier ohnehin nur ein
            // Übergangszustand, Ventoy2Disk formatiert bei der eigentlichen Einrichtung alles
            // komplett neu.
            string script =
                $"select disk {diskIndex}\nclean\ncreate partition primary\n" +
                $"format fs=ntfs quick label=ULMPREP\nassign letter={letter}\nexit\n";
            string tempFile = Path.Combine(Path.GetTempPath(), "ulm_diskpart_raw.txt");
            string logFile  = Path.Combine(Path.GetTempPath(), "ulm_diskpart_raw_log.txt");
            File.WriteAllText(tempFile, script, Encoding.ASCII);
            try { File.Delete(logFile); } catch { }
            try
            {
                // BUGFIX (live gefunden 2026-08-17): diskpart bricht im /s-Skriptmodus bei einem
                // fehlgeschlagenen Einzelbefehl NICHT ab — "format" kann scheitern, während
                // "assign letter" trotzdem noch durchläuft und diskpart sich mit Exit-Code 0
                // beendet. Der bisherige Code vertraute deshalb fälschlich einem "Erfolg", obwohl
                // die Partition unformatiert (RAW) blieb — siehe IsFormattedFileSystem-Kommentar.
                // diskpart läuft deshalb jetzt über cmd.exe (WindowStyle=Hidden funktioniert auch
                // zusammen mit UseShellExecute=true/Verb="runas" und unterdrückt das kurz
                // aufblitzende Konsolenfenster, ohne die UAC-Erhöhung zu beeinträchtigen), das die
                // komplette diskpart-Ausgabe in logFile umleitet — UseShellExecute=true selbst
                // erlaubt kein direktes RedirectStandardOutput. Nach dem Lauf wird zusätzlich über
                // DriveInfo.DriveFormat geprüft, ob der zugewiesene Buchstabe wirklich ein
                // Dateisystem hat.
                var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", BuildDiskpartCommand(tempFile, logFile))
                { UseShellExecute = true, Verb = "runas", WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden };
                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc is null) return false;
                proc.WaitForExit(60_000);

                string? driveFormat = null;
                try
                {
                    var di = new DriveInfo(letter.ToString());
                    if (di.IsReady) driveFormat = di.DriveFormat;
                }
                catch { /* driveFormat bleibt null -> gilt unten als nicht formatiert */ }

                bool formatted = IsFormattedFileSystem(driveFormat);
                if (!formatted)
                    Debug.WriteLine($"[PrepareRawUsbDisk] Datenträger {diskIndex} blieb nach diskpart unformatiert (DriveFormat='{driveFormat}'). diskpart-Ausgabe:\n{TryReadDiskpartLog(logFile)}");
                return formatted;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Nutzer hat die UAC-Abfrage abgelehnt, oder Windows konnte cmd.exe aus einem
                // anderen Grund nicht erhöht starten — kein ULM-Fehler, einfach als gescheiterte
                // Vorbereitung werten.
                return false;
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
                try { File.Delete(logFile); } catch { }
            }
        }

        private static string TryReadDiskpartLog(string logFile)
        {
            try
            {
                if (!File.Exists(logFile)) return "(keine Log-Datei erzeugt)";
                string text = File.ReadAllText(logFile);
                return text.Length > 2000 ? text[^2000..] : text;
            }
            catch (Exception ex) { return $"(Log nicht lesbar: {ex.Message})"; }
        }

        public static string DriveRoot(string letter)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return letter.EndsWith(':') ? letter + "\\" : letter;
            return letter;
        }

        public static double DriveTotalMb(string letter)
        { try { return new DriveInfo(DriveRoot(letter)).TotalSize / 1_048_576.0; } catch { return 0; } }

        public static double DriveFreeMb(string letter)
        { try { return new DriveInfo(DriveRoot(letter)).AvailableFreeSpace / 1_048_576.0; } catch { return 0; } }

        public static bool IsVentoyInstalled(string letter)
        { try { return Directory.Exists(Path.Combine(DriveRoot(letter), "ventoy")); } catch { return false; } }

        /// <summary>
        /// Verschiebt eine ISO auf dem Stick in den Kategorie-Ordner (Stick-Wurzel\Kategorie\Dateiname),
        /// z.B. beim Import bisher unbekannter ISOs — hält die Ordnerstruktur konsistent mit dem
        /// normalen Download-/Kopier-Flow (CopyToUsbWorker, RunPipelineCopyConsumerAsync).
        /// </summary>
        public static bool MoveToCategoryFolder(string sourcePath, string letter, string category, string filename, Action<string>? log = null)
        {
            try
            {
                string destDir  = Path.Combine(DriveRoot(letter), category);
                string destPath = Path.Combine(destDir, filename);
                if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase)) return true;
                Directory.CreateDirectory(destDir);
                if (File.Exists(destPath)) File.Delete(destPath);
                File.Move(sourcePath, destPath);
                return true;
            }
            catch (Exception ex) { log?.Invoke($"⚠ Verschieben fehlgeschlagen ({filename}): {ex.Message}"); return false; }
        }

        // ── Formatieren ───────────────────────────────────────────────────
        public static bool DoFormat(string letter)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return true;
            string script =
                $"select volume {letter[0]}\nformat fs=exfat quick label=VENTOY\n" +
                $"assign letter={letter[0]}\nexit\n";
            string tempFile = Path.Combine(Path.GetTempPath(), "ulm_diskpart.txt");
            File.WriteAllText(tempFile, script, Encoding.ASCII);
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("diskpart", $"/s \"{tempFile}\"")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc is null) return false;
                proc.WaitForExit(60_000);
                return proc.ExitCode == 0;
            }
            finally { try { File.Delete(tempFile); } catch { } }
        }

        // ── Ventoy-Theme ──────────────────────────────────────────────────
        public static void EnsureVentoyTheme(string letter)
        {
            try
            {
                string themeDir = Path.Combine(DriveRoot(letter), "ventoy", "themes", "ulm");
                Directory.CreateDirectory(themeDir);
                UpdateVentoyMenu(letter, Array.Empty<IsoEntry>());
            }
            catch (Exception ex) { Debug.WriteLine($"[EnsureVentoyTheme] {ex.Message}"); }
        }

        // Vertikale Aufteilung (0-100% der Bildschirmhöhe), so gewählt, dass sich Titel,
        // Untertitel, Boot-Menü, Distro-Tipp (menu_tip, siehe UpdateVentoyMenu), Tasten-Hinweis
        // und Ventoys eigene native Disk-Info-Zeile (ventoy_left/ventoy_top, siehe UpdateVentoyMenu)
        // NICHT überlappen:
        //   Titel + Untertitel  2.0% – 9.0%  (ein zusammengehöriger Kopf-Block, kein Abstand
        //                                     zwischen den beiden Zeilen nötig)
        //   Boot-Menü          10.0% – 78.0%
        //   Distro-Tipp         81.0% (einzeilig)
        //   Tasten-Hinweis      88.0% (einzeilig) — BUGFIX: lag vorher bei 94%, nur 1% von
        //                                           Ventoys nativer Status-Zeile entfernt und
        //                                           überlappte sich sichtbar mit ihr
        //   Ventoy-Status       95.0% (Ventoy-eigene Zeile, siehe ventoy_top in UpdateVentoyMenu)
        private static void WriteThemeTxt(string dir, string letter, double totalMb, double freeMb, int isoCount)
        {
            string subtitle = $"Multiboot USB Stick Manager  v{Constants.AppVersion}   |   " +
                $"{letter}  {totalMb / 1024.0:F1} GB gesamt  |  {freeMb / 1024.0:F1} GB frei  |  {isoCount} ISOs";
            string c =
                "# Universal Linux Manager Boot-Theme\n" +
                "desktop-image: \"background.png\"\n" +
                "desktop-color: \"#0D1B2A\"\n" +
                "\n+ label {\n  top=2%\n  left=0%\n  width=100%\n  height=48\n  align=\"center\"\n" +
                "  text=\"UNIVERSAL LINUX MANAGER\"\n  color=\"#FFFFFF\"\n}\n" +
                "\n+ label {\n  top=7%\n  left=0%\n  width=100%\n  height=26\n  align=\"center\"\n" +
                $"  text=\"{subtitle}\"\n  color=\"#4A6FA5\"\n}}\n" +
                "\n+ boot_menu {\n  left=10%\n  top=10%\n  width=80%\n  height=68%\n" +
                "  item_color=\"#C8D4E0\"\n  selected_item_color=\"#FFFFFF\"\n" +
                "  item_height=42\n  item_padding=16\n  item_spacing=6\n" +
                "  scrollbar=true\n  scrollbar_width=6\n" +
                "  scrollbar_thumb_color=\"#0075BE\"\n  scrollbar_frame_color=\"#1A3355\"\n}\n" +
                "\n+ label {\n  top=88%\n  left=0%\n  width=100%\n  height=22\n  align=\"center\"\n" +
                "  text=\"Auf/Ab: Auswahl  |  ENTER: Booten  |  Esc: Zurueck\"\n  color=\"#4A6FA5\"\n}\n";
            File.WriteAllText(Path.Combine(dir, "theme.txt"), c, Encoding.UTF8);
        }

        private static void WriteBackgroundPng(string dir)
        {
            string dest = Path.Combine(dir, "background.png");
            const string rn = "ULM.background.png";
            var asm = Assembly.GetExecutingAssembly();
            Stream? s = asm.GetManifestResourceStream(rn);
            if (s is null) { Debug.WriteLine($"[WriteBackgroundPng] '{rn}' nicht gefunden."); return; }
            using (s) using (var f = File.Create(dest)) s.CopyTo(f);
        }

        // ── Ventoy-Menü ───────────────────────────────────────────────────
        public static void UpdateVentoyMenu(string letter, IReadOnlyList<IsoEntry> dbEntries)
        {
            try
            {
                string root      = DriveRoot(letter);
                string ventoyDir = Path.Combine(root, "ventoy");
                Directory.CreateDirectory(ventoyDir);
                string themeDir = Path.Combine(ventoyDir, "themes", "ulm");

                var stickIsos = new List<(string VentoyPath, string Filename, string Category)>();
                if (Directory.Exists(root))
                {
                    foreach (string subDir in Directory.GetDirectories(root).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                    {
                        string cat = Path.GetFileName(subDir);
                        if (string.Equals(cat, "ventoy", StringComparison.OrdinalIgnoreCase) ||
                            cat.StartsWith('$') || cat.StartsWith('.') ||
                            string.Equals(cat, "System Volume Information", StringComparison.OrdinalIgnoreCase)) continue;
                        foreach (string iso in Directory.GetFiles(subDir, "*.iso").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                        { string fn = Path.GetFileName(iso); stickIsos.Add(($"/{cat}/{fn}", fn, cat)); }
                    }
                    foreach (string iso in Directory.GetFiles(root, "*.iso").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                    { string fn = Path.GetFileName(iso); stickIsos.Add(($"/{fn}", fn, string.Empty)); }
                }

                if (Directory.Exists(themeDir))
                {
                    WriteBackgroundPng(themeDir);
                    WriteThemeTxt(themeDir, letter, DriveTotalMb(letter), DriveFreeMb(letter), stickIsos.Count);
                }

                var byFn = new Dictionary<string, IsoEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (var e in dbEntries) if (!string.IsNullOrWhiteSpace(e.Filename) && !byFn.ContainsKey(e.Filename)) byFn[e.Filename] = e;

                var catOrd = Constants.Categories.Select((cat, idx) => (cat, idx)).ToDictionary(x => x.cat, x => x.idx, StringComparer.OrdinalIgnoreCase);
                var aliases = new List<(string, string)>();
                var tips    = new List<(string, string)>();

                foreach (var (vPath, fname, _) in stickIsos.OrderBy(x => { int o = catOrd.TryGetValue(x.Category, out int ord) ? ord : 999; return (o, x.Category, x.Filename); }))
                {
                    string title = byFn.TryGetValue(fname, out var entry) ? entry.Name
                        : Path.GetFileNameWithoutExtension(fname).Replace('-', ' ').Replace('_', ' ');
                    aliases.Add((vPath, title));
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.Tip)) tips.Add((vPath, CondenseTip(entry.Tip)));
                }

                using var stream = new FileStream(Path.Combine(ventoyDir, "ventoy.json"), FileMode.Create, FileAccess.Write, FileShare.None);
                using var w     = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
                w.WriteStartObject();

                w.WritePropertyName("theme");
                w.WriteStartObject();
                // "file" MUSS mit "/" beginnen (Ventoy-Pfade sind immer absolut ab Stick-Wurzel) —
                // ohne führenden Slash findet Ventoy die theme.txt nicht und fällt lautlos auf das
                // Standard-Theme zurück. Das war der Grund, warum background.png nie erschien.
                w.WriteString("file", "/ventoy/themes/ulm/theme.txt"); w.WriteString("gfxmode", "1920x1080,1280x720,auto");
                w.WriteString("display_mode", "GUI"); w.WriteString("ventoy_left", "5%"); w.WriteString("ventoy_top", "95%"); w.WriteString("ventoy_color", "#0075BE");
                w.WriteEndObject();

                w.WritePropertyName("control"); w.WriteStartArray();
                // VTOY_MENU_TIMEOUT bewusst NICHT gesetzt: ein gesetzter Wert (auch "0") lässt
                // Ventoy den aktuell fokussierten Menüeintrag automatisch nach X Sekunden booten.
                // Im TreeView-Modus (VTOY_DEFAULT_MENU_MODE=1) ist der oberste Eintrag beim
                // Start aber ein Kategorie-Ordner (z.B. "[Antivirus]"), kein bootbares Image —
                // GRUB versuchte diesen automatisch zu booten und scheiterte mit "Failed to boot
                // both default and fallback entries. Press any key to continue.....". Ohne den
                // Key wartet Ventoy wie gewollt auf eine echte Nutzerauswahl.
                WCtrl(w, "VTOY_DEFAULT_MENU_MODE", "1"); WCtrl(w, "VTOY_TREE_VIEW_MENU_STYLE", "0");
                // ULM legt ISOs ausschließlich direkt unter der Stick-Wurzel oder genau einen
                // Kategorie-Ordner tief ab (siehe MoveToCategoryFolder/CopyToUsbWorker) — Level 1
                // deckt das vollständig ab. Ventoys Standard ("max") durchsucht rekursiv beliebig
                // tief und verlängert dadurch sichtbar die Text-Scanphase vor dem GUI-Theme
                // ("Booting DIR ...."); mit Level 1 fällt dieser Overhead weg.
                WCtrl(w, "VTOY_MAX_SEARCH_LEVEL", "1");
                w.WriteEndArray();

                if (aliases.Count > 0)
                {
                    w.WritePropertyName("menu_alias"); w.WriteStartArray();
                    // Der Anzeigename-Schlüssel heißt "alias", nicht "title" — mit "title" ignoriert
                    // Ventoy den Eintrag komplett und zeigt den rohen Dateinamen im Bootmenü.
                    foreach (var (vp, t) in aliases) { w.WriteStartObject(); w.WriteString("image", vp); w.WriteString("alias", t); w.WriteEndObject(); }
                    w.WriteEndArray();
                }

                if (tips.Count > 0)
                {
                    // menu_tip hat EIN "left"/"top"/"color" für die gesamte Tipp-Zeile plus ein
                    // "tips"-Array mit {image, tip} — kein switch/tip_left/tip_width/externe .txt-Datei,
                    // das gab es in Ventoy nie. Position bewusst unterhalb des Boot-Menüs (das bis 78%
                    // reicht) und oberhalb der Tasten-Hinweiszeile (94%), damit nichts überlappt.
                    w.WritePropertyName("menu_tip");
                    w.WriteStartObject();
                    w.WriteString("left", "10%"); w.WriteString("top", "81%"); w.WriteString("color", "#4A6FA5");
                    w.WritePropertyName("tips"); w.WriteStartArray();
                    foreach (var (vp, txt) in tips) { w.WriteStartObject(); w.WriteString("image", vp); w.WriteString("tip", txt); w.WriteEndObject(); }
                    w.WriteEndArray();
                    w.WriteEndObject();
                }

                w.WriteEndObject(); w.Flush();

                // Frühere ULM-Versionen schrieben eine externe menu_tip.txt (nicht-existentes Schema) —
                // auf bereits eingerichteten Sticks aufräumen, falls noch vorhanden.
                string staleTipFile = Path.Combine(ventoyDir, "menu_tip.txt");
                if (File.Exists(staleTipFile)) File.Delete(staleTipFile);
            }
            catch (Exception ex) { Debug.WriteLine($"[UpdateVentoyMenu] {ex.Message}"); }
        }

        /// <summary>
        /// menu_tip wird von Ventoy einzeilig ohne Zeilenumbruch gerendert — mehrzeilige oder
        /// sehr lange Beschreibungen liefen sonst unkontrolliert über den Bildschirmrand hinaus.
        /// </summary>
        private static string CondenseTip(string tip)
        {
            const int maxLen = 100;
            string oneLine = string.Join(' ', tip.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim())).Trim();
            return oneLine.Length > maxLen ? oneLine[..maxLen].TrimEnd() + "…" : oneLine;
        }

        private static void WCtrl(Utf8JsonWriter w, string k, string v)
        { w.WriteStartObject(); w.WriteString(k, v); w.WriteEndObject(); }

        // ── USB-Stick scannen ─────────────────────────────────────────────
        public record StickIso(string Filename, string Category, string FullPath, long Size);

        public List<StickIso> ScanStick(string letter, IReadOnlyList<IsoEntry> entries)
        {
            var result = new List<StickIso>();
            string root = DriveRoot(letter);
            if (!Directory.Exists(root)) return result;

            var allFiles = new List<string>();
            SafeRecursiveSearch(root, allFiles);

            foreach (string f in allFiles)
            {
                string parent   = Path.GetDirectoryName(f) ?? string.Empty;
                string dirName  = Path.GetFileName(parent);
                string category = string.Equals(parent, root.TrimEnd('\\', '/'),
                    StringComparison.OrdinalIgnoreCase) ? string.Empty : dirName;

                // IsoEntry.GetRobustLength enthält alle drei Methoden:
                // FileInfo.Refresh() + FileStream + Win32 GetFileAttributesEx
                long size = IsoEntry.GetRobustLength(f);

                result.Add(new StickIso(Path.GetFileName(f), category, f, size));
            }

            return result;
        }

        /// <summary>
        /// Scannt den Stick und prüft zusätzlich pro erkannter Distro die Original-Größe
        /// online (HEAD-Request via HttpService.GetExpectedSizeAsync). Dateien, deren Größe
        /// spürbar von der Online-Größe abweicht (oder — falls online nicht ermittelbar —
        /// unter Constants.MinIsoSizeBytes liegt), gelten als unvollständig/Datenmüll und
        /// werden NICHT in die reguläre Trefferliste aufgenommen, damit sie nicht fälschlich
        /// als UsbStatus.Ok durchgehen.
        /// </summary>
        public async Task<(List<StickIso> Found, List<StickIso> Incomplete)> ScanStickVerifiedAsync(string letter, IReadOnlyList<IsoEntry> entries)
        {
            var found = ScanStick(letter, entries);
            var byFn  = new Dictionary<string, IsoEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in entries)
                if (!string.IsNullOrWhiteSpace(e.Filename) && !byFn.ContainsKey(e.Filename)) byFn[e.Filename] = e;

            var incomplete = new List<StickIso>();
            foreach (var si in found)
            {
                if (!byFn.TryGetValue(si.Filename, out var entry)) continue; // unbekannte Datei — eigener Import-Flow
                long expected = await HttpService.Instance.GetExpectedSizeAsync(entry).ConfigureAwait(false);
                bool ok = expected > 0 ? si.Size >= expected * 0.98 : si.Size >= Constants.MinIsoSizeBytes;
                if (!ok) incomplete.Add(si);
            }

            var clean = incomplete.Count == 0 ? found : found.Where(f => !incomplete.Contains(f)).ToList();
            return (clean, incomplete);
        }

        private void SafeRecursiveSearch(string currentDir, List<string> resultFiles)
        {
            string dirName = Path.GetFileName(currentDir);
            if (string.Equals(dirName, "System Volume Information", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(dirName, "$RECYCLE.BIN",  StringComparison.OrdinalIgnoreCase) ||
                string.Equals(dirName, "ventoy",        StringComparison.OrdinalIgnoreCase) ||
                string.Equals(dirName, "VTOYEFI",       StringComparison.OrdinalIgnoreCase) ||
                dirName.StartsWith('.') || dirName.StartsWith('$'))
                return;
            try
            {
                foreach (string f in Directory.GetFiles(currentDir, "*.iso")) resultFiles.Add(f);
                foreach (string d in Directory.GetDirectories(currentDir))    SafeRecursiveSearch(d, resultFiles);
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }

        // SICHERHEIT: Die Escaping-Logik ersetzt NUR doppelte Anführungszeichen — kein Schutz vor
        // PowerShell-Metazeichen wie `, $(), ; oder |. Aktuell unkritisch, da 'command' in der
        // gesamten Codebasis ausschließlich mit einem festen, hier hart kodierten Skript
        // aufgerufen wird (ListRemovableDrives), niemals mit Benutzereingaben. Diese Funktion darf
        // NICHT mit ungeprüften/aus der DB stammenden Strings aufgerufen werden — das wäre eine
        // PowerShell-Command-Injection.
        public static string RunPowerShell(string command, int timeoutSeconds = 10)
        {
            string esc = command.Replace("\"", "`\"");
            var psi = new System.Diagnostics.ProcessStartInfo(
                "powershell", $"-NoProfile -ExecutionPolicy Bypass -Command \"{esc}\"")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc is null) return string.Empty;
            proc.WaitForExit(timeoutSeconds * 1_000);
            return proc.StandardOutput.ReadToEnd();
        }

        // ── Rohe (buchstabenlose) USB-Datenträger: reine Logik-Helfer ──────
        // systemDiskIndex ist bewusst nullable: konnte der Systemdatenträger-Index nicht ermittelt
        // werden (z.B. WMI-Fehler), MUSS das als "unsicher" gelten (fail-closed) — sonst würde ein
        // WMI-Ausfall versehentlich JEDEN Datenträger als sicher durchwinken (fail-open), bei einer
        // destruktiven Aktion (diskpart clean) inakzeptabel.
        internal static bool IsSafeToPrepare(int diskIndex, int? systemDiskIndex) =>
            systemDiskIndex.HasValue && diskIndex != systemDiskIndex.Value;

        internal static List<int> FindNewRawDiskIndices(IReadOnlyList<int> previousIndices, IReadOnlyList<int> currentIndices) =>
            currentIndices.Except(previousIndices).ToList();

        // A/B (historisch Diskette) und C (i.d.R. System) werden übersprungen — nicht aus
        // Sicherheitsgründen (IsSafeToPrepare deckt das bereits ab), sondern weil ein frisch
        // zugewiesener Buchstabe dort ohnehin von Windows selbst verweigert würde.
        internal static char? FindFreeDriveLetter(IEnumerable<char> usedLetters)
        {
            var used = new HashSet<char>(usedLetters);
            for (char c = 'D'; c <= 'Z'; c++)
                if (!used.Contains(c)) return c;
            return null;
        }

        // BUGFIX (live gefunden 2026-08-17): diskpart bricht im /s-Skriptmodus bei einem
        // fehlgeschlagenen Einzelbefehl NICHT ab — "format" kann scheitern, während "assign
        // letter" trotzdem noch durchläuft. Der Prozess-Exit-Code von diskpart sagt daher nur
        // "diskpart hat sich sauber beendet", NICHT "alle Befehle im Skript haben geklappt". Live
        // beobachtet: Ein Stick blieb nach "erfolgreich" gemeldeter Vorbereitung RAW mit
        // zugewiesenem Buchstaben — für ULM danach komplett unsichtbar (weder als roher
        // Datenträger ohne Buchstaben noch als normales Laufwerk erkannt, siehe
        // ListUsbDisksNeedingPreparation unten). Deshalb wird nach dem diskpart-Lauf zusätzlich
        // das TATSÄCHLICHE Ergebnis über DriveInfo.DriveFormat geprüft.
        internal static bool IsFormattedFileSystem(string? driveFormat) =>
            !string.IsNullOrWhiteSpace(driveFormat) && !driveFormat.Trim().Equals("RAW", StringComparison.OrdinalIgnoreCase);

        // UseShellExecute=true+Verb="runas" (nötig für die UAC-Erhöhung) erlaubt kein direktes
        // RedirectStandardOutput auf den diskpart-Prozess selbst — cmd.exe übernimmt die Umleitung
        // stattdessen (cmd.exe wird elevated gestartet, diskpart als dessen Kindprozess erbt die
        // Erhöhung ohne eigene UAC-Abfrage). Ohne diese Umleitung gab es bei einem Fehlschlag
        // bisher keinerlei diskpart-Fehlertext zur Diagnose — nur den (wie oben beschrieben)
        // unzuverlässigen Exit-Code.
        internal static string BuildDiskpartCommand(string scriptPath, string logPath) =>
            $"/c diskpart /s \"{scriptPath}\" > \"{logPath}\" 2>&1";
    }
}
