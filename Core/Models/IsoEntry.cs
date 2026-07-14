// Core/Models/IsoEntry.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace ULM.Core.Models
{
    public enum UsbStatus { Unknown, Ok, Outdated, Missing }

    public sealed class IsoEntry
    {
        // ── Persistente Felder ──────────────────────────────────────────
        public string Name        { get; set; } = string.Empty;
        public string Category    { get; set; } = "Einsteiger";
        public string Url         { get; set; } = string.Empty;

        private string _filename = string.Empty;
        // SICHERHEIT: Filename wird an mehreren Stellen ungeprüft in Path.Combine() verwendet
        // (Download-Ziel in Workers.DownloadWorker, Kopier-Ziel in RunPipelineCopyConsumerAsync/
        // CopyToUsbWorker, UsbService.MoveToCategoryFolder). Der Wert stammt nicht nur aus der
        // Standard-Datenbank, sondern auch aus frei editierbaren Quellen: DB-Editor
        // (IsoEditDialog), Stick-Import (ImportStickIsosDialog) und einer ggf. von anderswo
        // übernommenen ulm_isos.ini. Path.GetFileName() erzwingt hier zentral, dass NUR der reine
        // Dateiname übernommen wird — ohne diese Sperre könnte ein Wert wie "..\..\..\evil.dll"
        // Dateien außerhalb des vorgesehenen Download-/Stick-Ordners schreiben (Path-Traversal).
        public string Filename
        {
            get => _filename;
            set => _filename = string.IsNullOrWhiteSpace(value) ? string.Empty : Path.GetFileName(value.Trim());
        }

        public string Mirror1     { get; set; } = string.Empty;
        public string Mirror2     { get; set; } = string.Empty;
        public string Mirror3     { get; set; } = string.Empty;
        public string Mirror4     { get; set; } = string.Empty;
        public string Mirror5     { get; set; } = string.Empty;
        public string GithubRepo  { get; set; } = string.Empty;
        public string GithubAsset { get; set; } = string.Empty;
        public string Tip         { get; set; } = string.Empty;

        // ── Laufzeit-Felder ─────────────────────────────────────────────
        public UsbStatus UsbStatus         { get; set; } = UsbStatus.Unknown;
        public string    UsbSize           { get; set; } = string.Empty;
        public bool      UrlOk             { get; set; }
        public bool      UrlChecked        { get; set; }
        public string    RemoteVersion     { get; set; } = string.Empty;
        public string    RemoteUrl         { get; set; } = string.Empty;
        public string    RemoteFilename    { get; set; } = string.Empty;
        public bool      UpdateAvailable   { get; set; }
        public bool      VerifiedComplete  { get; set; }
        public bool      IsSelected        { get; set; }
        public string    DownloadStatus    { get; set; } = string.Empty;
        public bool      ImportedFromStick { get; set; }

        /// <summary>
        /// Gibt alle konfigurierten Download-URLs in Prioritätsreihenfolge zurück.
        /// resolvedUrl (aufgelöst) → RemoteUrl → Url → Mirror1-5.
        /// Duplikate und leere Strings werden herausgefiltert.
        /// </summary>
        public IEnumerable<string> AllDownloadUrls(string? resolvedUrl = null)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // FIX CS8600: string? statt string — resolvedUrl ist nullable,
            // daher ist das Array string?[] und u muss nullable sein.
            foreach (string? u in new string?[] { resolvedUrl, RemoteUrl, Url, Mirror1, Mirror2, Mirror3, Mirror4, Mirror5 })
            {
                if (string.IsNullOrWhiteSpace(u)) continue;
                string normalized = NormalizeSourceForgeUrl(u);
                if (seen.Add(normalized)) yield return normalized;
            }
        }

        // BUGFIX: manuell eingetragene SourceForge-Links zeigen gelegentlich auf einen konkret
        // gepinnten Mirror (z.B. "altushost-bul.dl.sourceforge.net") statt auf SourceForges eigenen
        // Auto-Redirector "master.dl.sourceforge.net" — typischerweise aus der Browser-Adresszeile
        // kopiert, NACHDEM der Redirector bereits aufgelöst hat. Ein gepinnter Mirror kann für den
        // jeweiligen Nutzer deutlich langsamer sein als der automatisch gewählte, und trägt bei
        // manchen SourceForge-Links zusätzlich signierte, zeitlich begrenzte Parameter (z.B. "e="
        // als Ablauf-Unixzeit) — die URL kann also nach kurzer Zeit komplett ausfallen. Wird hier
        // beim Lesen normalisiert (nicht beim Speichern), damit bereits vorhandene Datenbank-
        // Einträge automatisch mitkorrigiert werden, ohne dass der Nutzer sie manuell nachbearbeiten
        // muss, und ohne den im DB-Editor sichtbaren Wert zu verfälschen.
        private static readonly Regex PinnedSourceForgeMirror =
            new(@"^https?://(?!master\.dl\.sourceforge\.net)[a-z0-9.-]+\.dl\.sourceforge\.net/project/([^?#]+)",
                RegexOptions.IgnoreCase);

        internal static string NormalizeSourceForgeUrl(string url)
        {
            Match m = PinnedSourceForgeMirror.Match(url);
            return m.Success ? $"https://master.dl.sourceforge.net/project/{m.Groups[1].Value}?viasf=1" : url;
        }

        // ── Abgeleitete Eigenschaften ───────────────────────────────────
        public string NormalizedCategory =>
            Category == "Leichtgewichtig" ? "Leichtgewicht" : Category;

        public bool HasResolvedUpdate =>
            UpdateAvailable && !string.IsNullOrEmpty(RemoteVersion) && !string.IsNullOrEmpty(RemoteUrl);

        public bool HasOnlineVersionInfo => !string.IsNullOrEmpty(RemoteVersion);

        // ── Robuste Datei-Suche ────────────────────────────────────────
        public string? FindLocalPath(string downloadDirectory)
        {
            if (string.IsNullOrWhiteSpace(Filename) || string.IsNullOrWhiteSpace(downloadDirectory)) return null;
            string flat = Path.Combine(downloadDirectory, Filename);
            if (File.Exists(flat)) return flat;
            if (!Directory.Exists(downloadDirectory)) return null;
            try
            {
                foreach (string f in Directory.EnumerateFiles(downloadDirectory, "*.iso", SearchOption.AllDirectories))
                    if (string.Equals(Path.GetFileName(f), Filename, StringComparison.OrdinalIgnoreCase))
                        return f;
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
            return null;
        }

        public bool IsLocallyAvailable(string downloadDirectory)
        {
            string? path = FindLocalPath(downloadDirectory);
            return path is not null && GetRobustLength(path) >= Constants.MinIsoSizeBytes;
        }

        /// <summary>
        /// Lokal ODER auf dem zuletzt gescannten Stick vorhanden (UsbStatus.Ok). Von Stick
        /// importierte Distros haben oft keine lokale Kopie — für den manuellen Update-Check
        /// sollen sie trotzdem wie reguläre Einträge behandelt werden, solange sie irgendwo
        /// tatsächlich vorliegen.
        /// </summary>
        public bool IsAvailableAnywhere(string downloadDirectory) =>
            IsLocallyAvailable(downloadDirectory) || UsbStatus == UsbStatus.Ok;

        public long LocalFileSize(string downloadDirectory)
        {
            string? path = FindLocalPath(downloadDirectory);
            return path is null ? 0L : GetRobustLength(path);
        }

        // ── Windows API für zuverlässige Dateigröße ────────────────────
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "GetFileAttributesExW")]
        private static extern bool Win32GetFileAttributesEx(string lpFileName, int fInfoLevelId, out Win32FileAttributeData lpFileInformation);

        [StructLayout(LayoutKind.Sequential)]
        private struct Win32FileAttributeData
        {
            public uint dwFileAttributes;
            public long ftCreationTime;
            public long ftLastAccessTime;
            public long ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public long FileSize => ((long)nFileSizeHigh << 32) | (long)nFileSizeLow;
        }

        public static long GetRobustLength(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return 0L;
            try { var fi = new FileInfo(path); fi.Refresh(); if (fi.Exists && fi.Length > 0) return fi.Length; } catch { }
            try { using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); if (fs.Length > 0) return fs.Length; } catch { }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            { try { if (Win32GetFileAttributesEx(path, 0, out var data) && data.FileSize > 0) return data.FileSize; } catch { } }
            return 0L;
        }

        public static bool TryDelete(string path, Action<string>? log = null)
        {
            if (string.IsNullOrWhiteSpace(path)) return true;
            if (!File.Exists(path)) return true;
            try { File.Delete(path); return true; }
            catch (Exception ex) { log?.Invoke($"⚠ Löschen fehlgeschlagen ({Path.GetFileName(path)}): {ex.Message}"); return false; }
        }

        public void ResetRuntimeState()
        {
            UsbStatus = UsbStatus.Unknown; UsbSize = string.Empty;
            UrlOk = false; UrlChecked = false; RemoteVersion = string.Empty;
            RemoteUrl = string.Empty; RemoteFilename = string.Empty;
            UpdateAvailable = false; DownloadStatus = string.Empty; VerifiedComplete = false;
        }

        public override string ToString() => Name;
    }
}
