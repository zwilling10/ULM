// Infrastructure/ThemeService.cs
using System;
using System.Windows;
using Microsoft.Win32;

namespace ULM.Infrastructure
{
    public enum AppThemeMode { System, Light, Dark }

    /// <summary>
    /// Schaltet zur Laufzeit zwischen Themes/AppTheme.xaml (hell) und Themes/DarkTheme.xaml
    /// (dunkel) um, indem die gemergte ResourceDictionary an Position 0 in
    /// Application.Current.Resources ausgetauscht wird. Implizite Styles (TargetType, kein
    /// x:Key) sowie DynamicResource-Bindungen reagieren darauf automatisch — StaticResource-
    /// Bindungen in XAML tun das NICHT, deshalb müssen Fenster, die live umschalten sollen
    /// (aktuell nur MainWindow), DynamicResource statt StaticResource verwenden.
    /// </summary>
    public static class ThemeService
    {
        public static AppThemeMode CurrentMode { get; private set; } = AppThemeMode.System;
        public static event Action? ThemeChanged;

        public static bool IsDarkActive => CurrentMode switch
        {
            AppThemeMode.Dark  => true,
            AppThemeMode.Light => false,
            _                  => IsWindowsDarkModeActive(),
        };

        private static bool _systemEventsHooked;

        public static void Initialize()
        {
            string saved = IniService.Read(AppPaths.Instance.SettingsIni, "App", "ThemeMode", "System");
            CurrentMode = Enum.TryParse(saved, out AppThemeMode m) ? m : AppThemeMode.System;
            ApplyResourceDictionary();

            if (!_systemEventsHooked)
            {
                _systemEventsHooked = true;
                try
                {
                    // Folgt einem Windows-Design-Wechsel live nach, aber NUR solange der Nutzer
                    // "System" gewählt hat — bei expliziter Hell/Dunkel-Wahl wird das ignoriert.
                    SystemEvents.UserPreferenceChanged += (_, args) =>
                    {
                        if (args.Category == UserPreferenceCategory.General && CurrentMode == AppThemeMode.System)
                            Application.Current?.Dispatcher.Invoke(() => { ApplyResourceDictionary(); ThemeChanged?.Invoke(); });
                    };
                }
                catch { /* auf Systemen ohne SystemEvents-Unterstützung einfach ignorieren */ }
            }
        }

        public static void SetMode(AppThemeMode mode)
        {
            if (CurrentMode == mode) return;
            CurrentMode = mode;
            IniService.Write(AppPaths.Instance.SettingsIni, "App", "ThemeMode", mode.ToString());
            ApplyResourceDictionary();
            ThemeChanged?.Invoke();
        }

        private static void ApplyResourceDictionary()
        {
            if (Application.Current is null) return;
            string source = IsDarkActive ? "Themes/DarkTheme.xaml" : "Themes/AppTheme.xaml";
            var newDict = new ResourceDictionary { Source = new Uri(source, UriKind.Relative) };
            var dicts = Application.Current.Resources.MergedDictionaries;
            if (dicts.Count > 0) dicts[0] = newDict; else dicts.Add(newDict);
        }

        private static bool IsWindowsDarkModeActive()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                object? val = key?.GetValue("AppsUseLightTheme");
                if (val is int i) return i == 0;
            }
            catch { /* Registry nicht lesbar (Gruppenrichtlinie o.ä.) -> heller Fallback */ }
            return false;
        }
    }
}
