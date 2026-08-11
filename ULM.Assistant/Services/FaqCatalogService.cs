// ULM.Assistant/Services/FaqCatalogService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ULM.Assistant.Models;

namespace ULM.Assistant.Services
{
    // Lädt Ulis Fragen-Katalog aus assistant_faq.json (liegt neben der EXE). Existiert die
    // Datei noch nicht, wird sie beim ersten Start aus DefaultCatalog() erzeugt — damit ist sie
    // ab dann ohne Neu-Kompilieren editierbar (wie ulm_isos.ini bei der Haupt-App). Ist die
    // Datei kaputt, greift DefaultCatalog() rein im Speicher (Datei wird NICHT überschrieben,
    // um mögliche manuelle Nutzer-Änderungen nicht zu zerstören) — der Chat funktioniert so
    // oder so immer, auch in einem schreibgeschützten EXE-Ordner.
    public sealed class FaqCatalogService
    {
        private static readonly Lazy<FaqCatalogService> _lazy = new(CreateDefault);

        public static FaqCatalogService Instance => _lazy.Value;

        public IReadOnlyList<FaqEntry> Catalog { get; }

        // Interner Pfad-Konstruktor für Tests (ULM.Tests hat via InternalsVisibleTo Zugriff) —
        // schreibt bewusst NIE Dateien (reines Lesen-oder-Fallback), damit Tests seiteneffektfrei
        // bleiben. Das Schreiben des Erststart-Bootstraps passiert nur in CreateDefault().
        internal FaqCatalogService(string jsonPath)
        {
            Catalog = LoadOrDefault(jsonPath);
        }

        private static FaqCatalogService CreateDefault()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "assistant_faq.json");
            if (!File.Exists(path)) TryWriteDefaultCatalog(path);
            return new FaqCatalogService(path);
        }

        private static void TryWriteDefaultCatalog(string path)
        {
            try
            {
                string json = JsonSerializer.Serialize(DefaultCatalog(), new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch
            {
                // z.B. schreibgeschützter EXE-Ordner (Programme, CD-ROM) — Chat funktioniert
                // trotzdem, nur ohne eine später editierbare Datei. Analog zu AppPaths'
                // IsWritableDirectory-Fallback in der Haupt-App.
            }
        }

        internal static List<FaqEntry> LoadOrDefault(string jsonPath)
        {
            try
            {
                if (!File.Exists(jsonPath)) return DefaultCatalog();
                string json = File.ReadAllText(jsonPath);
                var entries = JsonSerializer.Deserialize<List<FaqEntry>>(json);
                return entries is { Count: > 0 } ? entries : DefaultCatalog();
            }
            catch
            {
                return DefaultCatalog();
            }
        }

        internal static List<FaqEntry> DefaultCatalog() => new()
        {
            new FaqEntry
            {
                Id = "iso-search-filter",
                KeywordsDe = new() { "suchen", "filtern", "kategorie" },
                KeywordsEn = new() { "search", "filter", "category" },
                QuestionLabelDe = "Wie finde ich eine bestimmte Distro?",
                QuestionLabelEn = "How do I find a specific distro?",
                ChipLabelDe = "ISO suchen",
                ChipLabelEn = "Search ISO",
                AnswerDe = "Nutze die Kategorie-Checkbox über jeder Gruppe, um alle Distros einer Kategorie auf einmal an-/abzuwählen, oder '🔍 ISO suchen' für eine Online-Suche nach neuen Distros bei DistroWatch.",
                AnswerEn = "Use the category checkbox above each group to select/deselect all distros in a category at once, or '🔍 Search ISO' for an online search for new distros on DistroWatch.",
                RelatedIds = new() { "download-start", "db-entry-edit" },
            },
            new FaqEntry
            {
                Id = "download-start",
                KeywordsDe = new() { "download", "herunterladen" },
                KeywordsEn = new() { "download" },
                QuestionLabelDe = "Wie starte ich einen Download?",
                QuestionLabelEn = "How do I start a download?",
                ChipLabelDe = "Download starten",
                ChipLabelEn = "Start download",
                AnswerDe = "Checkbox links neben der gewünschten Distro aktivieren, dann '⬇ Herunterladen' klicken. Mehrere Distros gleichzeitig sind möglich. ULM testet dabei automatisch alle Mirror und startet mit der schnellsten Quelle.",
                AnswerEn = "Enable the checkbox next to the distro you want, then click '⬇ Download'. Multiple distros at once are fine. ULM automatically tests all mirrors and starts with the fastest source.",
                RelatedIds = new() { "download-parallel-slots", "copy-to-stick", "common-errors" },
            },
            new FaqEntry
            {
                Id = "download-parallel-slots",
                KeywordsDe = new() { "parallel", "gleichzeitig", "slots" },
                KeywordsEn = new() { "parallel", "simultaneous", "slots" },
                QuestionLabelDe = "Wie viele Downloads laufen gleichzeitig?",
                QuestionLabelEn = "How many downloads run at the same time?",
                ChipLabelDe = "Parallele Downloads",
                ChipLabelEn = "Parallel downloads",
                AnswerDe = "ULM lädt mehrere ausgewählte ISOs parallel herunter, um die Bandbreite optimal zu nutzen. Die Anzahl der gleichzeitigen Downloads lässt sich im Fortschrittsfenster einsehen.",
                AnswerEn = "ULM downloads several selected ISOs in parallel to make the best use of your bandwidth. The number of simultaneous downloads is shown in the progress window.",
                RelatedIds = new() { "download-start" },
            },
            new FaqEntry
            {
                Id = "copy-to-stick",
                KeywordsDe = new() { "kopieren", "stick", "usb" },
                KeywordsEn = new() { "copy", "stick", "usb" },
                QuestionLabelDe = "Wie kommt eine ISO auf meinen USB-Stick?",
                QuestionLabelEn = "How does an ISO get onto my USB stick?",
                ChipLabelDe = "Auf Stick kopieren",
                ChipLabelEn = "Copy to stick",
                AnswerDe = "Ist ein Ventoy-Stick angeschlossen, bietet ULM nach dem Download automatisch an, direkt zu kopieren (Pipeline-Modus). Bereits lokal vorhandene ISOs lassen sich jederzeit über '🔁 Verpasste Kopien nachholen' erneut auf den Stick übertragen.",
                AnswerEn = "If a Ventoy stick is connected, ULM automatically offers to copy right after the download (pipeline mode). ISOs already downloaded locally can be copied again anytime via '🔁 Catch up missed copies'.",
                RelatedIds = new() { "ventoy-setup", "common-errors", "manual-iso-placement" },
            },
            new FaqEntry
            {
                Id = "manual-iso-placement",
                KeywordsDe = new() { "eigene iso", "eigene isos", "manuell hinzufügen", "fremde iso" },
                KeywordsEn = new() { "own iso", "own isos", "manually add", "custom iso" },
                QuestionLabelDe = "Wo speichere ich eine eigene/fremde ISO auf dem Stick?",
                QuestionLabelEn = "Where do I store my own/custom ISO on the stick?",
                ChipLabelDe = "Eigene ISO",
                ChipLabelEn = "Custom ISO",
                AnswerDe = "Kopiere die .iso-Datei einfach irgendwo auf den Ventoy-Stick — Ventoy findet sie unabhängig vom Ordner. Beim nächsten Scan erkennt ULM sie automatisch als unbekannt und öffnet einen Import-Dialog: Name und Kategorie vergeben, danach verschiebt ULM die Datei selbst in den passenden Kategorie-Ordner und aktualisiert das Bootmenü.",
                AnswerEn = "Just copy the .iso file anywhere onto the Ventoy stick — Ventoy finds it regardless of folder. On the next scan, ULM automatically detects it as unknown and opens an import dialog: assign a name and category, then ULM moves the file into the matching category folder itself and refreshes the boot menu.",
                RelatedIds = new() { "copy-to-stick", "db-entry-edit" },
            },
            new FaqEntry
            {
                Id = "ventoy-setup",
                KeywordsDe = new() { "ventoy", "einrichten", "aktualisieren", "bootfähig" },
                KeywordsEn = new() { "ventoy", "setup", "update", "bootable" },
                QuestionLabelDe = "Wie richte ich Ventoy auf einem Stick ein?",
                QuestionLabelEn = "How do I set up Ventoy on a stick?",
                ChipLabelDe = "Ventoy einrichten",
                ChipLabelEn = "Set up Ventoy",
                AnswerDe = "Wird ein neuer, unformatierter Stick erkannt, fragt ULM automatisch, ob Ventoy eingerichtet werden soll — ACHTUNG: eine Neuinstallation löscht ALLE Daten auf dem Stick! Eine Aktualisierung behält bestehende ISOs. Läuft als Administrator (UAC) in einem eigenen Fenster.",
                AnswerEn = "When a new, unformatted stick is detected, ULM automatically asks whether to set up Ventoy — WARNING: a fresh install ERASES ALL data on the stick! An update keeps existing ISOs. Runs as administrator (UAC) in its own window.",
                RelatedIds = new() { "copy-to-stick", "secure-boot" },
            },
            new FaqEntry
            {
                Id = "language-switch",
                KeywordsDe = new() { "sprache", "deutsch", "englisch" },
                KeywordsEn = new() { "language", "german", "english" },
                QuestionLabelDe = "Wie wechsle ich die Sprache?",
                QuestionLabelEn = "How do I switch the language?",
                ChipLabelDe = "Sprache wechseln",
                ChipLabelEn = "Switch language",
                AnswerDe = "Über '⚙ Einstellungen' oben rechts im Hauptfenster die gewünschte Sprache wählen. Der Wechsel wirkt nach einem Neustart von ULM.",
                AnswerEn = "Choose your language via '⚙ Settings' at the top right of the main window. The switch takes effect after restarting ULM.",
                RelatedIds = new(),
            },
            new FaqEntry
            {
                Id = "db-entry-edit",
                KeywordsDe = new() { "datenbank", "eintrag", "hinzufügen", "bearbeiten" },
                KeywordsEn = new() { "database", "entry", "add", "edit" },
                QuestionLabelDe = "Wie füge ich eine eigene Distro hinzu?",
                QuestionLabelEn = "How do I add my own distro?",
                ChipLabelDe = "Datenbank-Eintrag",
                ChipLabelEn = "Database entry",
                AnswerDe = "Über '🗃 Datenbank' im Hauptfenster lassen sich Einträge anzeigen, bearbeiten oder neu anlegen — inklusive Name, Kategorie und Download-URL.",
                AnswerEn = "Use '🗃 Database' in the main window to view, edit, or add entries — including name, category, and download URL.",
                RelatedIds = new() { "iso-search-filter" },
            },
            new FaqEntry
            {
                Id = "integrity-update-check",
                KeywordsDe = new() { "integrität", "prüfsumme", "hash", "update prüfen" },
                KeywordsEn = new() { "integrity", "checksum", "hash", "check updates" },
                QuestionLabelDe = "Wie prüfe ich, ob eine ISO unbeschädigt ist oder ob es Updates gibt?",
                QuestionLabelEn = "How do I check if an ISO is undamaged or if updates are available?",
                ChipLabelDe = "Integrität / Updates",
                ChipLabelEn = "Integrity / updates",
                AnswerDe = "'🔒 Integrität prüfen' vergleicht die Datei auf dem Stick mit dem beim Download gespeicherten SHA-256-Referenzhash. '↻ Updates prüfen' fragt manuell ab, ob neuere Versionen verfügbar sind (läuft beim Start ohnehin automatisch).",
                AnswerEn = "'🔒 Verify integrity' compares the file on the stick against the SHA-256 reference hash saved at download time. '↻ Check for updates' manually checks for newer versions (also runs automatically at startup).",
                RelatedIds = new() { "download-start" },
            },
            new FaqEntry
            {
                Id = "common-errors",
                KeywordsDe = new() { "fehler", "fehlgeschlagen", "nicht erkannt" },
                KeywordsEn = new() { "error", "failed", "not detected" },
                QuestionLabelDe = "Der Download ist fehlgeschlagen oder mein Stick wird nicht erkannt — was tun?",
                QuestionLabelEn = "The download failed or my stick isn't detected — what now?",
                ChipLabelDe = "Häufige Fehler",
                ChipLabelEn = "Common errors",
                AnswerDe = "Download fehlgeschlagen: ULM versucht automatisch alle hinterlegten Mirror; schlagen alle fehl, hilft oft ein erneuter Versuch später oder '🔧 Quelle manuell suchen'. Stick nicht erkannt: Laufwerks-Dropdown im Hauptfenster prüfen — bei mehreren Sticks muss der richtige ausgewählt sein; '↻' neben dem Dropdown liest Laufwerke neu ein.",
                AnswerEn = "Download failed: ULM automatically tries all configured mirrors; if all fail, trying again later or '🔧 Search source manually' often helps. Stick not detected: check the drive dropdown in the main window — with multiple sticks, make sure the right one is selected; '↻' next to the dropdown re-scans drives.",
                RelatedIds = new() { "download-start", "ventoy-setup" },
            },
            new FaqEntry
            {
                Id = "secure-boot",
                KeywordsDe = new() { "secure boot" },
                KeywordsEn = new() { "secure boot" },
                QuestionLabelDe = "Was bedeutet die Secure-Boot-Checkbox?",
                QuestionLabelEn = "What does the Secure Boot checkbox mean?",
                ChipLabelDe = "Secure Boot",
                ChipLabelEn = "Secure Boot",
                AnswerDe = "Ist bei der Ventoy-Einrichtung 'Secure Boot' aktiviert, unterstützt der Stick danach auch Systeme mit aktiviertem Secure Boot in der UEFI-Firmware. Ohne Häkchen funktioniert der Stick nur, wenn Secure Boot im BIOS/UEFI deaktiviert ist.",
                AnswerEn = "If 'Secure Boot' is checked during Ventoy setup, the stick will also work on systems with Secure Boot enabled in UEFI firmware. Without it, the stick only works if Secure Boot is disabled in BIOS/UEFI.",
                RelatedIds = new() { "ventoy-setup" },
            },
        };
    }
}
