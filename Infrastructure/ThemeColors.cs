// Infrastructure/ThemeColors.cs
using System.Windows;
using System.Windows.Media;

namespace ULM.Infrastructure
{
    /// <summary>
    /// Typisierter Zugriff auf die aktuell aktive Farbpalette (Themes/AppTheme.xaml oder
    /// Themes/DarkTheme.xaml, je nach ThemeService.IsDarkActive). Code-behind-Fenster (die keine
    /// eigene XAML mit DynamicResource-Bindungen haben) lesen darüber IMMER die aktuell gültige
    /// Farbe zum Zeitpunkt ihrer Konstruktion — reicht aus, da diese Fenster modal sind und beim
    /// nächsten Öffnen ohnehin neu gebaut werden, nachdem ein Theme-Wechsel bereits angewendet wurde.
    /// </summary>
    public static class ThemeColors
    {
        public static Brush Bg        => Get("BrushBg");
        public static Brush White     => Get("BrushWhite");
        public static Brush Header    => Get("BrushHeader");
        public static Brush HeaderBar => Get("BrushHeaderBar");
        public static Brush Blue      => Get("BrushBlue");
        public static Brush Mid       => Get("BrushMid");
        public static Brush Dim       => Get("BrushDim");
        public static Brush Border    => Get("BrushBorder");
        public static Brush Card      => Get("BrushCard");
        public static Brush Green     => Get("BrushGreen");
        public static Brush Amber     => Get("BrushAmber");
        public static Brush Red       => Get("BrushRed");
        public static Brush Teal      => Get("BrushTeal");
        public static Brush LBlue     => Get("BrushLBlue");
        public static Brush LGreen    => Get("BrushLGreen");
        public static Brush LRed      => Get("BrushLRed");
        public static Brush LAmber    => Get("BrushLAmber");

        private static Brush Get(string key) => (Brush)Application.Current.Resources[key];
    }
}
