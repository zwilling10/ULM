// Infrastructure/IniService.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ULM.Infrastructure
{
    /// <summary>
    /// Einfacher, thread-unsicherer INI-Parser ohne externe Abhängigkeiten.
    /// Geeignet für sequentiellen Zugriff aus dem UI-Thread oder aus
    /// hintereinander ausgeführten Hintergrundoperationen.
    /// </summary>
    public static class IniService
    {
        // ── Einzelwert lesen ─────────────────────────────────────────────
        public static string Read(
            string path,
            string section,
            string key,
            string defaultValue = "")
        {
            if (!File.Exists(path)) return defaultValue;

            try
            {
                bool inSection = false;
                string target  = $"[{section}]";

                foreach (string rawLine in File.ReadLines(path, Encoding.UTF8))
                {
                    string line = rawLine.Trim();
                    if (line.StartsWith('['))
                    {
                        inSection = string.Equals(line, target,
                            StringComparison.OrdinalIgnoreCase);
                        continue;
                    }
                    if (!inSection || line.Length == 0 || line[0] is ';' or '#')
                        continue;

                    int eq = line.IndexOf('=');
                    if (eq < 1) continue;

                    if (string.Equals(line[..eq].Trim(), key,
                            StringComparison.OrdinalIgnoreCase))
                        return line[(eq + 1)..].Trim();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IniService.Read] {ex.Message}");
            }

            return defaultValue;
        }

        // ── Alle Sektionen + Keys lesen ──────────────────────────────────
        public static Dictionary<string, Dictionary<string, string>> ReadAll(string path)
        {
            var result = new Dictionary<string, Dictionary<string, string>>(
                StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(path)) return result;

            try
            {
                string? currentSection = null;

                foreach (string rawLine in File.ReadLines(path, Encoding.UTF8))
                {
                    string line = rawLine.Trim();

                    if (line.StartsWith('[') && line.EndsWith(']'))
                    {
                        currentSection = line[1..^1];
                        if (!result.ContainsKey(currentSection))
                            result[currentSection] =
                                new Dictionary<string, string>(
                                    StringComparer.OrdinalIgnoreCase);
                        continue;
                    }

                    if (currentSection is null ||
                        line.Length == 0 || line[0] is ';' or '#')
                        continue;

                    int eq = line.IndexOf('=');
                    if (eq < 1) continue;

                    result[currentSection][line[..eq].Trim()] =
                        line[(eq + 1)..].Trim();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IniService.ReadAll] {ex.Message}");
            }

            return result;
        }

        // ── Einzelwert schreiben ─────────────────────────────────────────
        public static void Write(
            string path, string section, string key, string value)
        {
            var data = ReadAll(path);

            if (!data.TryGetValue(section,
                    out Dictionary<string, string>? dict))
            {
                dict = new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);
                data[section] = dict;
            }

            dict[key] = value ?? string.Empty;
            Flush(path, data);
        }

        // ── Key entfernen ────────────────────────────────────────────────
        public static void Delete(string path, string section, string key)
        {
            if (!File.Exists(path)) return;

            var data = ReadAll(path);
            if (data.TryGetValue(section, out var dict))
                dict.Remove(key);

            Flush(path, data);
        }

        // ── Kompletten Inhalt schreiben ──────────────────────────────────
        private static void Flush(
            string path,
            Dictionary<string, Dictionary<string, string>> data)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            using var writer = new StreamWriter(path, append: false, Encoding.UTF8);

            foreach (var (sectionName, dict) in data)
            {
                writer.WriteLine($"[{sectionName}]");
                foreach (var (k, v) in dict)
                    writer.WriteLine($"{k} = {v}");
                writer.WriteLine();
            }
        }
    }
}
