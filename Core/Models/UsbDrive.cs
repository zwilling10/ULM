// Core/Models/UsbDrive.cs
namespace ULM.Core.Models
{
    /// <summary>
    /// Repräsentiert ein erkanntes USB-Laufwerk (immutabel).
    /// </summary>
    public sealed record UsbDrive(
        string Letter,
        string Label,
        long   SizeBytes,
        string FileSystem)
    {
        /// <summary>Anzeigename für ComboBox: "E:  VENTOY  (32,0 GB)"</summary>
        public string DisplayName
        {
            get
            {
                string size = FormatBytes(SizeBytes);
                return string.IsNullOrWhiteSpace(Label)
                    ? $"{Letter}  ({size})"
                    : $"{Letter}  {Label}  ({size})";
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "0 B";
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double val = bytes;
            int i = 0;
            while (val >= 1024 && i < units.Length - 1) { val /= 1024; i++; }
            return i == 0 ? $"{(long)val} B" : $"{val:F1} {units[i]}";
        }

        public override string ToString() => DisplayName;
    }
}
