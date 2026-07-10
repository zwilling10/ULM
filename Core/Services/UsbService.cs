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
    public sealed class UsbService
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

            const string script = @"
$vols = Get-CimInstance Win32_LogicalDisk | Where-Object { $_.DriveType -eq 2 }
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
                WriteThemeTxt(themeDir);
                WriteBackgroundPng(themeDir);
                UpdateVentoyMenu(letter, Array.Empty<IsoEntry>());
            }
            catch (Exception ex) { Debug.WriteLine($"[EnsureVentoyTheme] {ex.Message}"); }
        }

        // Vertikale Aufteilung (0-100% der Bildschirmhöhe), so gewählt, dass sich Titel,
        // Boot-Menü, Distro-Tipp (menu_tip, siehe UpdateVentoyMenu) und die Tasten-Hinweiszeile
        // NICHT überlappen — mit durchgehend mind. ~3% Sicherheitsabstand zwischen den Blöcken:
        //   Titel        2.0% – 6.5%
        //   Boot-Menü   10.0% – 78.0%
        //   Distro-Tipp 81.0% (einzeilig, siehe ventoy_left/ventoy_top in UpdateVentoyMenu)
        //   Tasten-Hinweis 94%
        private static void WriteThemeTxt(string dir)
        {
            const string c =
                "# Universal Linux Manager Boot-Theme\n" +
                "desktop-image: \"background.png\"\n" +
                "desktop-color: \"#0D1B2A\"\n" +
                "\n+ label {\n  top=2%\n  left=0%\n  width=100%\n  height=48\n  align=\"center\"\n" +
                "  text=\"UNIVERSAL LINUX MANAGER\"\n  color=\"#FFFFFF\"\n}\n" +
                "\n+ boot_menu {\n  left=10%\n  top=10%\n  width=80%\n  height=68%\n" +
                "  item_color=\"#C8D4E0\"\n  selected_item_color=\"#FFFFFF\"\n" +
                "  item_height=42\n  item_padding=16\n  item_spacing=6\n" +
                "  scrollbar=true\n  scrollbar_width=6\n" +
                "  scrollbar_thumb_color=\"#0075BE\"\n  scrollbar_frame_color=\"#1A3355\"\n}\n" +
                "\n+ label {\n  top=94%\n  left=0%\n  width=100%\n  height=22\n  align=\"center\"\n" +
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
                if (Directory.Exists(themeDir)) WriteBackgroundPng(themeDir);

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
                WCtrl(w, "VTOY_MENU_TIMEOUT", "0"); WCtrl(w, "VTOY_DEFAULT_MENU_MODE", "1"); WCtrl(w, "VTOY_TREE_VIEW_MENU_STYLE", "0");
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
    }
}
