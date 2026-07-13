// Core/Models/Constants.cs
using System.Reflection;

namespace ULM.Core.Models
{
    public static class Constants
    {
        public const string AppTitle        = "Universal Linux Manager";

        // Einzige Quelle der Wahrheit ist <Version>/<AssemblyVersion> in der .csproj — wird beim
        // Build automatisch in die Assembly geschrieben. Vorher stand die Versionsnummer zusätzlich
        // hartkodiert im HelpDialog-Titel (und hier als eigener const string) und musste bei jedem
        // Release manuell an mehreren Stellen synchron gehalten werden — das lief bereits einmal
        // auseinander (Constants.AppVersion "2.27" vs. tatsächlich ausgelieferte 2.27.1).
        public static readonly string AppVersion   = ReadVersion();
        public static string AppFullTitle => $"{AppTitle} v{AppVersion}";

        private static string ReadVersion()
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            if (v is null) return "0.0.0";
            return v.Build > 0 ? $"{v.Major}.{v.Minor}.{v.Build}" : $"{v.Major}.{v.Minor}";
        }

        public const long MinIsoSizeBytes   = 314_572_800L; // 300 MB
        public const int  MaxParallelSlots  = 6;
        public const int  AutoCheckIntervalDays = 3;
        public const long MaxLogSizeBytes   = 5 * 1024 * 1024; // 5 MB — ab hier wird ulm_log.txt rotiert

        public static readonly string[] Categories =
        {
            "Gaming", "Sicherheit", "Einsteiger", "Leichtgewicht",
            "Fortgeschrittene", "Rettung", "Antivirus", "WinPE"
        };

        public static string CategoryLabel(string category) => category switch
        {
            "Gaming"           => "🎮 Gaming",
            "Sicherheit"       => "🔒 Sicherheit & Privatsphäre",
            "Einsteiger"       => "💻 Einsteiger (Komfort & Design)",
            "Leichtgewicht"    => "🪶 Leichtgewicht (Geschwindigkeit & Effizienz)",
            "Fortgeschrittene" => "⚙ Fortgeschrittene (Unabhängigkeit & Stabilität)",
            "Rettung"          => "🛠 Rettung (Backup & Wiederherstellung)",
            "Antivirus"        => "🛡 Antivirus (Schutz & Bereinigung)",
            "WinPE"            => "🪟 WinPE (Windows-Tools)",
            _                  => category
        };

        // Glary Design System — Farben
        public const string ColorBlue   = "#0075BE";
        public const string ColorGreen  = "#27AE60";
        public const string ColorAmber  = "#E67E22";
        public const string ColorRed    = "#C0392B";
        public const string ColorTeal   = "#1ABC9C";
        public const string ColorBg     = "#F0F4F8";
        public const string ColorWhite  = "#FFFFFF";
        public const string ColorHeader = "#0F2540";
        public const string ColorMid    = "#4A6785";
        public const string ColorDim    = "#8BA3BE";
        public const string ColorBorder = "#C5D5E5";
        public const string ColorCard   = "#E4EBF2";
        public const string ColorLBlue  = "#D6ECFA";
        public const string ColorLGreen = "#E8F8F0";
        public const string ColorLRed   = "#FDE8E8";
        public const string ColorLAmber = "#FEF5E7";
    }
}
