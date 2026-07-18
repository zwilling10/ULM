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

        // BUGFIX: bei einer .0-Patch-Version (z.B. AssemblyVersion 2.28.0.0) wurde der Build-Teil
        // bisher weggelassen ("2.28" statt "2.28.0") — das weicht vom <Version> in der .csproj, dem
        // Git-Tag und dem GitHub-Release-Namen ab (alle "2.28.0"), was in Fenstertitel/Hilfe-Dialog
        // wie eine veraltete Version aussieht, obwohl es die aktuell gebaute ist.
        private static string ReadVersion()
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            if (v is null) return "0.0.0";
            return $"{v.Major}.{v.Minor}.{v.Build}";
        }

        public const long MinIsoSizeBytes   = 314_572_800L; // 300 MB
        public const int  MaxParallelSlots  = 6;
        public const int  AutoCheckIntervalDays = 3;
        public const int  ManualSearchFailureThreshold = 3; // ab so vielen Fehlschlagversuchen in Folge gilt ein Eintrag als Haertefall
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

    }
}
