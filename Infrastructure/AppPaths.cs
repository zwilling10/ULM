// Infrastructure/AppPaths.cs
using System;
using System.IO;

namespace ULM.Infrastructure
{
    /// <summary>
    /// Singleton mit allen Arbeits-Pfaden der Anwendung.
    ///
    /// ── Portabilität ──────────────────────────────────────────────────
    /// ULM ist eine portable App: Alle Daten liegen im Unterordner
    /// "ULM_Data" NEBEN der EXE — nicht in Documents oder AppData.
    ///
    /// Beispiele je nach Einsatzort:
    ///   USB-Stick:   E:\ULM\ULM_Data\
    ///   Desktop:     C:\Users\Max\Desktop\ULM\ULM_Data\
    ///   Netzlaufwerk:\\Server\Tools\ULM\ULM_Data\
    ///
    /// Dadurch können EXE + Daten gemeinsam kopiert, auf einem
    /// USB-Stick mitgenommen oder auf anderen PCs gestartet werden,
    /// ohne dass etwas "verloren geht" oder neu konfiguriert werden muss.
    ///
    /// Ausnahmen (read-only Quellpfad):
    ///   Wenn der EXE-Ordner nicht beschreibbar ist (z.B. wenn die EXE
    ///   direkt in C:\Programme liegt), wird automatisch auf
    ///   %LOCALAPPDATA%\ULM ausgewichen, damit die App trotzdem läuft.
    /// ──────────────────────────────────────────────────────────────────
    /// </summary>
    public sealed class AppPaths
    {
        private static readonly Lazy<AppPaths> _lazy =
            new(() => new AppPaths());

        public static AppPaths Instance => _lazy.Value;

        private AppPaths()
        {
            // AppContext.BaseDirectory = Ordner der EXE.
            // Funktioniert korrekt bei Single-File-Publish (EXE entpackt
            // sich temporär in %TEMP%, BaseDirectory zeigt aber auf den
            // ECHTEN Speicherort der EXE, nicht den Temp-Ordner).
            ScriptDirectory = AppContext.BaseDirectory
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Settings-Datei liegt immer direkt neben der EXE
            SettingsIni = Path.Combine(ScriptDirectory, "ulm_settings.ini");

            // Standard-Datenpfad = neben der EXE (portabel)
            // Fallback auf LocalAppData wenn EXE-Ordner nicht beschreibbar
            string portableBase = Path.Combine(ScriptDirectory, "ULM_Data");
            string defaultBase  = IsWritableDirectory(ScriptDirectory)
                ? portableBase
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ULM");

            // Nur Pfade vorbelegen — kein CreateDirectory hier!
            SetPaths(defaultBase);
        }

        // ── Feste Pfade (immer neben der EXE) ──────────────────────────
        public string ScriptDirectory { get; }
        public string SettingsIni     { get; }

        // ── Variable Pfade (abhängig vom gewählten Basispfad) ──────────
        public string BaseDirectory { get; private set; } = string.Empty;
        public string DownloadDir   { get; private set; } = string.Empty;
        public string DatabaseIni   { get; private set; } = string.Empty;
        public string LogFile       { get; private set; } = string.Empty;

        // ── Temp-Pfade (system-weit, kein Portabilitätsproblem) ────────
        public string TempDownloadDir { get; } =
            Path.Combine(Path.GetTempPath(), "ULM_Downloads");

        public string VentoyTempDir { get; } =
            Path.Combine(Path.GetTempPath(), "ULM_Ventoy");

        public string VentoyZipPath =>
            Path.Combine(VentoyTempDir, "ventoy_latest.zip");

        /// <summary>
        /// Standard-Basispfad (für Anzeige in Dialogen / Einstellungen).
        /// Portabler Betrieb: neben der EXE.
        /// Fallback: LocalAppData wenn EXE-Ordner read-only.
        /// </summary>
        public string DefaultBase =>
            IsWritableDirectory(ScriptDirectory)
                ? Path.Combine(ScriptDirectory, "ULM_Data")
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ULM");

        /// <summary>
        /// Setzt nur die Pfad-Properties, legt aber KEINE Ordner an.
        /// Für Vorschau vor Benutzerbestätigung.
        /// </summary>
        public void SetPaths(string baseDirectory)
        {
            BaseDirectory = baseDirectory;
            DownloadDir   = Path.Combine(baseDirectory, "ISOs");
            DatabaseIni   = Path.Combine(baseDirectory, "ulm_isos.ini");
            LogFile       = Path.Combine(baseDirectory, "ulm_log.txt");
        }

        /// <summary>
        /// Setzt die Pfade UND legt alle benötigten Ordner an.
        /// Wird erst nach Benutzerbestätigung aufgerufen.
        /// </summary>
        public void Apply(string baseDirectory)
        {
            SetPaths(baseDirectory);
            Directory.CreateDirectory(DownloadDir);
        }

        /// <summary>
        /// Schneller Schreibbarkeits-Check: versucht eine Testdatei zu
        /// erstellen und löscht sie sofort wieder.
        /// Verhindert Abstürze wenn ULM aus einem read-only Ordner gestartet
        /// wird (z.B. %ProgramFiles%, CD-ROM, schreibgeschützter Netzwerkpfad).
        /// </summary>
        private static bool IsWritableDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                string probe = Path.Combine(path, ".ulm_write_test");
                File.WriteAllText(probe, "test");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
