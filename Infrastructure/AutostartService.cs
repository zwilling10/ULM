// Infrastructure/AutostartService.cs
using System;
using Microsoft.Win32;

namespace ULM.Infrastructure
{
    // ═══════════════════════════════════════════════════════════════════
    // AutostartService — verwaltet den optionalen Windows-Autostart über
    // einen HKCU-Run-Key (kein Admin-Recht nötig). Die Registry selbst ist
    // die einzige Quelle der Wahrheit für den aktivierten Zustand — es gibt
    // bewusst KEIN Duplikat-Flag in ulm_settings.ini, damit der Zustand nie
    // auseinanderlaufen kann (z.B. wenn die EXE manuell verschoben oder der
    // Registry-Eintrag extern gelöscht wird).
    // ═══════════════════════════════════════════════════════════════════
    public static class AutostartService
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName  = "UniversalLinuxManager";

        public static bool IsEnabled()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
                string? existing = key?.GetValue(ValueName) as string;
                return IsSameExecutable(existing, Environment.ProcessPath);
            }
            catch { /* Registry nicht lesbar (Gruppenrichtlinie o.ä.) -> als deaktiviert behandeln */ return false; }
        }

        public static void Enable()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
                key?.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
            }
            catch { /* Registry nicht beschreibbar (Gruppenrichtlinie o.ä.) -> bewusst ignoriert, Setup läuft weiter */ }
        }

        public static void Disable()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
                key?.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            catch { /* siehe Enable() */ }
        }

        internal static bool IsSameExecutable(string? registryValue, string? currentExePath)
        {
            if (string.IsNullOrWhiteSpace(registryValue) || string.IsNullOrWhiteSpace(currentExePath))
                return false;
            string trimmed = registryValue.Trim().Trim('"');
            return string.Equals(trimmed, currentExePath, StringComparison.OrdinalIgnoreCase);
        }
    }
}
