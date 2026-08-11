# Uli-Avatar-Assistent Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ein schwebender 🐧-Button ("Uli") im Windows-Hauptfenster öffnet ein nicht-modales Chat-Fenster mit lokalem Frage-Katalog (Q&A, kein Cloud-LLM), gebaut als eigenständiges `ULM.Assistant`-Projekt.

**Architecture:** Neues Class-Library-Projekt `ULM.Assistant` (`net8.0-windows`, WPF), das **keinerlei Referenz** auf die Haupt-App oder umgekehrt-referenzierbare Projekte hat (Core/Infrastructure sind nur Ordner der Haupt-App, kein eigenes Projekt — siehe Spec-Korrektur). Sprache wird per Dependency Injection (`Func<AssistantLanguage>`) hereingereicht. Nur die Haupt-App referenziert `ULM.Assistant` (`<ProjectReference>`), niemals umgekehrt.

**Tech Stack:** .NET 8, WPF, `System.Text.Json` (bereits im Projekt verwendet, keine neue Abhängigkeit), xUnit (bestehendes `ULM.Tests`-Projekt).

## Global Constraints

- **Git ist in diesem Repo-Ordner aktuell kaputt** (toter Worktree-Zeiger, siehe `docs/superpowers/specs/2026-08-10-avatar-assistant-design.md`, Abschnitt "Offene Punkte"). Jeder `git commit`/`git checkout -b`-Schritt in diesem Plan ist **best-effort** — schlägt er fehl, einfach überspringen und mit dem nächsten Schritt weitermachen. Kein Blocker.
- **Noch nichts veröffentlichen/mergen** — Nutzer testet erst lokal (siehe Spec). Alle Änderungen bleiben auf einem Feature-Branch (falls Git funktioniert) bzw. einfach als Arbeitsstand im Ordner.
- `ULM.Assistant` referenziert **niemals** `UniversalLinuxManager.csproj` und **niemals** `ULM.Infrastructure`/`ULM.Core`-Namespaces — jede Kopplung zur Haupt-App läuft ausschließlich über einfache `Func<T>`-Properties, die die Haupt-App von außen setzt.
- Alle sichtbaren Texte (Chat-Chrome UND Katalog-Inhalte) existieren in Deutsch UND Englisch — keine Ausnahmen.
- Build-Befehl Hauptprojekt: `dotnet build UniversalLinuxManager.csproj -c Release`
- Test-Befehl: `dotnet test ULM.Tests/ULM.Tests.csproj -c Release`
- Namenskonvention: `RootNamespace` von `ULM.Assistant.csproj` ist `ULM.Assistant`; Unterordner `Models/`, `Services/`, `Views/` ergeben die Namespaces `ULM.Assistant.Models`, `ULM.Assistant.Services`, `ULM.Assistant.Views`.

---

### Task 1: Projekt-Grundgerüst + Datenmodelle

**Files:**
- Create: `ULM.Assistant/ULM.Assistant.csproj`
- Create: `ULM.Assistant/Models/AssistantLanguage.cs`
- Create: `ULM.Assistant/Models/FaqEntry.cs`
- Create: `ULM.Assistant/Models/ChatMessage.cs`

**Interfaces:**
- Produces: `ULM.Assistant.Models.AssistantLanguage` (enum: `German`, `English`) — von allen späteren Tasks als Sprach-Parameter verwendet.
- Produces: `ULM.Assistant.Models.FaqEntry` (Properties: `Id`, `KeywordsDe`, `KeywordsEn`, `QuestionLabelDe`, `QuestionLabelEn`, `AnswerDe`, `AnswerEn`, `RelatedIds` — alle `string`/`List<string>`) — Kernmodell für Task 2+3.
- Produces: `ULM.Assistant.Models.ChatSender` (enum: `User`, `Uli`) und `ULM.Assistant.Models.ChatMessage` (Properties: `Sender`, `Text`) — von Task 5 verwendet.

- [ ] **Step 1: `ULM.Assistant.csproj` anlegen**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>ULM.Assistant</RootNamespace>
  </PropertyGroup>

  <!-- Erlaubt ULM.Tests Zugriff auf 'internal' Klassen/Methoden (Test-Hooks für
       FaqCatalogService, analog zum Muster in UniversalLinuxManager.csproj). -->
  <ItemGroup>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
      <_Parameter1>ULM.Tests</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>
</Project>
```

- [ ] **Step 2: `Models/AssistantLanguage.cs` anlegen**

```csharp
// ULM.Assistant/Models/AssistantLanguage.cs
namespace ULM.Assistant.Models
{
    // Eigenständiges Sprach-Enum, bewusst NICHT identisch mit ULM.Infrastructure.AppLanguage
    // der Haupt-App — ULM.Assistant referenziert die Haupt-App nicht. Die Haupt-App bildet
    // AppLanguage beim Setzen von AvatarButton.GetLanguage auf dieses Enum ab (siehe Task 7).
    public enum AssistantLanguage { German, English }
}
```

- [ ] **Step 3: `Models/FaqEntry.cs` anlegen**

```csharp
// ULM.Assistant/Models/FaqEntry.cs
using System.Collections.Generic;

namespace ULM.Assistant.Models
{
    // Ein Thema in Ulis Fragen-Katalog. Wird per System.Text.Json direkt aus/in
    // assistant_faq.json (de)serialisiert — Property-Namen müssen daher exakt zu den
    // JSON-Schlüsseln passen (siehe FaqCatalogService.DefaultCatalog in Task 3).
    public sealed class FaqEntry
    {
        public string Id { get; set; } = "";
        public List<string> KeywordsDe { get; set; } = new();
        public List<string> KeywordsEn { get; set; } = new();
        public string QuestionLabelDe { get; set; } = "";
        public string QuestionLabelEn { get; set; } = "";
        public string AnswerDe { get; set; } = "";
        public string AnswerEn { get; set; } = "";
        public List<string> RelatedIds { get; set; } = new();
    }
}
```

- [ ] **Step 4: `Models/ChatMessage.cs` anlegen**

```csharp
// ULM.Assistant/Models/ChatMessage.cs
namespace ULM.Assistant.Models
{
    public enum ChatSender { User, Uli }

    public sealed class ChatMessage
    {
        public ChatSender Sender { get; init; }
        public string Text { get; init; } = "";
    }
}
```

- [ ] **Step 5: Build-Check — Projekt kompiliert eigenständig**

Run: `dotnet build "ULM.Assistant/ULM.Assistant.csproj"`
Expected: `Build succeeded.` (0 Warnungen, 0 Fehler)

- [ ] **Step 6: Commit (best-effort, siehe Global Constraints)**

```bash
git add ULM.Assistant/
git commit -m "feat(assistant): scaffold ULM.Assistant project with data models"
```

---

### Task 2: FaqMatchingEngine (Keyword-Matching, TDD)

**Files:**
- Create: `ULM.Assistant/Services/FaqMatchingEngine.cs`
- Test: `ULM.Tests/AssistantFaqMatchingEngineTests.cs`
- Modify: `ULM.Tests/ULM.Tests.csproj` (ProjectReference auf `ULM.Assistant` hinzufügen)

**Interfaces:**
- Consumes: `ULM.Assistant.Models.FaqEntry`, `ULM.Assistant.Models.AssistantLanguage` (aus Task 1)
- Produces: `ULM.Assistant.Services.FaqMatchingEngine.Match(IReadOnlyList<FaqEntry> catalog, AssistantLanguage language, string userInput) : string?` — von `ChatWindow` (Task 5) verwendet.

- [ ] **Step 1: `ULM.Tests.csproj` um Referenz auf `ULM.Assistant` erweitern**

Füge im bestehenden `<ItemGroup>` mit den `ProjectReference`-Einträgen in `ULM.Tests/ULM.Tests.csproj` eine zweite Zeile hinzu:

```xml
  <ItemGroup>
    <ProjectReference Include="..\UniversalLinuxManager.csproj" />
    <ProjectReference Include="..\ULM.Assistant\ULM.Assistant.csproj" />
  </ItemGroup>
```

- [ ] **Step 2: Fehlschlagende Tests schreiben**

```csharp
// ULM.Tests/AssistantFaqMatchingEngineTests.cs
using System.Collections.Generic;
using ULM.Assistant.Models;
using ULM.Assistant.Services;
using Xunit;

namespace ULM.Tests
{
    public class AssistantFaqMatchingEngineTests
    {
        private static List<FaqEntry> BuildCatalog() => new()
        {
            new FaqEntry
            {
                Id = "download-start",
                KeywordsDe = new() { "download", "herunterladen" },
                KeywordsEn = new() { "download" },
            },
            new FaqEntry
            {
                Id = "ventoy-setup",
                KeywordsDe = new() { "ventoy", "stick einrichten" },
                KeywordsEn = new() { "ventoy", "setup stick" },
            },
        };

        [Fact]
        public void Match_SingleKeywordHit_ReturnsCorrectId()
        {
            var result = FaqMatchingEngine.Match(BuildCatalog(), AssistantLanguage.German, "Wie richte ich Ventoy ein?");
            Assert.Equal("ventoy-setup", result);
        }

        [Fact]
        public void Match_NoKeywordHit_ReturnsNull()
        {
            var result = FaqMatchingEngine.Match(BuildCatalog(), AssistantLanguage.German, "Wie ist das Wetter heute?");
            Assert.Null(result);
        }

        [Fact]
        public void Match_CaseInsensitive_StillMatches()
        {
            var result = FaqMatchingEngine.Match(BuildCatalog(), AssistantLanguage.German, "DOWNLOAD bitte");
            Assert.Equal("download-start", result);
        }

        [Fact]
        public void Match_TieBreak_ReturnsFirstCatalogEntry()
        {
            var catalog = new List<FaqEntry>
            {
                new FaqEntry { Id = "first",  KeywordsDe = new() { "stick" } },
                new FaqEntry { Id = "second", KeywordsDe = new() { "stick" } },
            };
            var result = FaqMatchingEngine.Match(catalog, AssistantLanguage.German, "mein stick");
            Assert.Equal("first", result);
        }

        [Fact]
        public void Match_EnglishKeywords_UsedWhenLanguageIsEnglish()
        {
            var result = FaqMatchingEngine.Match(BuildCatalog(), AssistantLanguage.English, "setup stick please");
            Assert.Equal("ventoy-setup", result);
        }
    }
}
```

- [ ] **Step 3: Tests laufen lassen — müssen fehlschlagen (Klasse existiert noch nicht)**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj --filter AssistantFaqMatchingEngineTests`
Expected: Build-Fehler `CS0246: The type or namespace name 'FaqMatchingEngine' could not be found`

- [ ] **Step 4: `FaqMatchingEngine` implementieren**

```csharp
// ULM.Assistant/Services/FaqMatchingEngine.cs
using System.Collections.Generic;
using System.Linq;
using ULM.Assistant.Models;

namespace ULM.Assistant.Services
{
    // Reine Keyword-Zählung, keine KI: für jeden Katalog-Eintrag wird gezählt, wie viele
    // seiner Keywords (case-insensitive) im eingegebenen Text vorkommen. Höchster Treffer
    // gewinnt; bei Punktgleichstand gewinnt der erste Eintrag in Katalog-Reihenfolge (striktes
    // ">" beim Vergleich unten sorgt dafür, dass ein späterer Eintrag einen früheren mit
    // gleichem Score nie verdrängt).
    public static class FaqMatchingEngine
    {
        public static string? Match(IReadOnlyList<FaqEntry> catalog, AssistantLanguage language, string userInput)
        {
            string text = (userInput ?? "").ToLowerInvariant();
            string? bestId = null;
            int bestScore = 0;

            foreach (var entry in catalog)
            {
                var keywords = language == AssistantLanguage.German ? entry.KeywordsDe : entry.KeywordsEn;
                int score = keywords.Count(k => text.Contains(k.ToLowerInvariant()));
                if (score > bestScore)
                {
                    bestScore = score;
                    bestId = entry.Id;
                }
            }

            return bestId;
        }
    }
}
```

- [ ] **Step 5: Tests laufen lassen — müssen bestehen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj --filter AssistantFaqMatchingEngineTests`
Expected: `Passed! - Failed: 0, Passed: 5`

- [ ] **Step 6: Commit (best-effort)**

```bash
git add ULM.Assistant/Services/FaqMatchingEngine.cs ULM.Tests/AssistantFaqMatchingEngineTests.cs ULM.Tests/ULM.Tests.csproj
git commit -m "feat(assistant): add keyword-based FaqMatchingEngine with tests"
```

---

### Task 3: FaqCatalogService (Laden, Fallback, Standard-Katalog, TDD)

**Files:**
- Create: `ULM.Assistant/Services/FaqCatalogService.cs`
- Test: `ULM.Tests/AssistantFaqCatalogServiceTests.cs`

**Interfaces:**
- Consumes: `ULM.Assistant.Models.FaqEntry` (Task 1)
- Produces: `ULM.Assistant.Services.FaqCatalogService.Instance : FaqCatalogService` (Singleton, lädt/erzeugt `assistant_faq.json` neben der EXE), `.Catalog : IReadOnlyList<FaqEntry>` — von `ChatWindow` (Task 5) konsumiert. `internal FaqCatalogService(string jsonPath)` und `internal static List<FaqEntry> DefaultCatalog()` — von Tests konsumiert.

- [ ] **Step 1: Fehlschlagende Tests schreiben**

```csharp
// ULM.Tests/AssistantFaqCatalogServiceTests.cs
using System;
using System.IO;
using System.Linq;
using ULM.Assistant.Services;
using Xunit;

namespace ULM.Tests
{
    public class AssistantFaqCatalogServiceTests
    {
        [Fact]
        public void LoadOrDefault_MissingFile_ReturnsDefaultCatalog()
        {
            string missingPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.json");
            var catalog = FaqCatalogService.LoadOrDefault(missingPath);
            Assert.True(catalog.Count > 0);
        }

        [Fact]
        public void LoadOrDefault_CorruptJson_ReturnsDefaultCatalog()
        {
            string path = Path.Combine(Path.GetTempPath(), $"corrupt_{Guid.NewGuid()}.json");
            File.WriteAllText(path, "{ this is not valid json ][");
            try
            {
                var catalog = FaqCatalogService.LoadOrDefault(path);
                Assert.True(catalog.Count > 0);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void LoadOrDefault_ValidJson_LoadsExactEntries()
        {
            string path = Path.Combine(Path.GetTempPath(), $"valid_{Guid.NewGuid()}.json");
            File.WriteAllText(path, """
            [
              {
                "Id": "test-entry",
                "KeywordsDe": ["test"],
                "KeywordsEn": ["test"],
                "QuestionLabelDe": "Testfrage?",
                "QuestionLabelEn": "Test question?",
                "AnswerDe": "Testantwort.",
                "AnswerEn": "Test answer.",
                "RelatedIds": []
              }
            ]
            """);
            try
            {
                var catalog = FaqCatalogService.LoadOrDefault(path);
                Assert.Single(catalog);
                Assert.Equal("test-entry", catalog[0].Id);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void Constructor_WithJsonPath_ExposesLoadedCatalog()
        {
            string missingPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.json");
            var service = new FaqCatalogService(missingPath);
            Assert.True(service.Catalog.Count > 0);
        }

        [Fact]
        public void DefaultCatalog_AllEntries_HaveUniqueIdsAndCompleteBilingualText()
        {
            var catalog = FaqCatalogService.DefaultCatalog();
            var ids = catalog.Select(e => e.Id).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());

            foreach (var entry in catalog)
            {
                Assert.False(string.IsNullOrWhiteSpace(entry.QuestionLabelDe));
                Assert.False(string.IsNullOrWhiteSpace(entry.QuestionLabelEn));
                Assert.False(string.IsNullOrWhiteSpace(entry.AnswerDe));
                Assert.False(string.IsNullOrWhiteSpace(entry.AnswerEn));
                Assert.True(entry.KeywordsDe.Count > 0);
                Assert.True(entry.KeywordsEn.Count > 0);

                foreach (var relatedId in entry.RelatedIds)
                    Assert.Contains(relatedId, ids);
            }
        }
    }
}
```

- [ ] **Step 2: Tests laufen lassen — müssen fehlschlagen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj --filter AssistantFaqCatalogServiceTests`
Expected: Build-Fehler `CS0246: The type or namespace name 'FaqCatalogService' could not be found`

- [ ] **Step 3: `FaqCatalogService` implementieren (inkl. 10-Themen-Standard-Katalog)**

```csharp
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
                AnswerDe = "Ist ein Ventoy-Stick angeschlossen, bietet ULM nach dem Download automatisch an, direkt zu kopieren (Pipeline-Modus). Bereits lokal vorhandene ISOs lassen sich jederzeit über '🔁 Verpasste Kopien nachholen' erneut auf den Stick übertragen.",
                AnswerEn = "If a Ventoy stick is connected, ULM automatically offers to copy right after the download (pipeline mode). ISOs already downloaded locally can be copied again anytime via '🔁 Catch up missed copies'.",
                RelatedIds = new() { "ventoy-setup", "common-errors" },
            },
            new FaqEntry
            {
                Id = "ventoy-setup",
                KeywordsDe = new() { "ventoy", "einrichten", "aktualisieren", "bootfähig" },
                KeywordsEn = new() { "ventoy", "setup", "update", "bootable" },
                QuestionLabelDe = "Wie richte ich Ventoy auf einem Stick ein?",
                QuestionLabelEn = "How do I set up Ventoy on a stick?",
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
                AnswerDe = "Ist bei der Ventoy-Einrichtung 'Secure Boot' aktiviert, unterstützt der Stick danach auch Systeme mit aktiviertem Secure Boot in der UEFI-Firmware. Ohne Häkchen funktioniert der Stick nur, wenn Secure Boot im BIOS/UEFI deaktiviert ist.",
                AnswerEn = "If 'Secure Boot' is checked during Ventoy setup, the stick will also work on systems with Secure Boot enabled in UEFI firmware. Without it, the stick only works if Secure Boot is disabled in BIOS/UEFI.",
                RelatedIds = new() { "ventoy-setup" },
            },
        };
    }
}
```

- [ ] **Step 4: Tests laufen lassen — müssen bestehen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj --filter AssistantFaqCatalogServiceTests`
Expected: `Passed! - Failed: 0, Passed: 5`

- [ ] **Step 5: Commit (best-effort)**

```bash
git add ULM.Assistant/Services/FaqCatalogService.cs ULM.Tests/AssistantFaqCatalogServiceTests.cs
git commit -m "feat(assistant): add FaqCatalogService with 10-topic default catalog and disk fallback"
```

---

### Task 4: AssistantStrings (Chat-Chrome-Texte, TDD)

**Files:**
- Create: `ULM.Assistant/Services/AssistantStrings.cs`
- Test: `ULM.Tests/AssistantStringsTests.cs`

**Interfaces:**
- Consumes: `ULM.Assistant.Models.AssistantLanguage` (Task 1)
- Produces: `ULM.Assistant.Services.AssistantStr` (enum: `WindowTitle`, `Greeting`, `InputPlaceholder`, `SendButton`, `Fallback`, `BackToOverview`), `ULM.Assistant.Services.AssistantStrings.T(AssistantStr key, AssistantLanguage language) : string` — von `ChatWindow` (Task 5) verwendet.

- [ ] **Step 1: Fehlschlagenden Test schreiben**

```csharp
// ULM.Tests/AssistantStringsTests.cs
using ULM.Assistant.Models;
using ULM.Assistant.Services;
using Xunit;

namespace ULM.Tests
{
    public class AssistantStringsTests
    {
        [Theory]
        [InlineData(AssistantStr.WindowTitle)]
        [InlineData(AssistantStr.Greeting)]
        [InlineData(AssistantStr.InputPlaceholder)]
        [InlineData(AssistantStr.SendButton)]
        [InlineData(AssistantStr.Fallback)]
        [InlineData(AssistantStr.BackToOverview)]
        public void T_BothLanguages_ReturnNonEmptyDistinctText(AssistantStr key)
        {
            string de = AssistantStrings.T(key, AssistantLanguage.German);
            string en = AssistantStrings.T(key, AssistantLanguage.English);
            Assert.False(string.IsNullOrWhiteSpace(de));
            Assert.False(string.IsNullOrWhiteSpace(en));
            Assert.NotEqual(de, en);
        }
    }
}
```

- [ ] **Step 2: Test laufen lassen — muss fehlschlagen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj --filter AssistantStringsTests`
Expected: Build-Fehler `CS0246: The type or namespace name 'AssistantStrings' could not be found`

- [ ] **Step 3: `AssistantStrings` implementieren**

```csharp
// ULM.Assistant/Services/AssistantStrings.cs
using ULM.Assistant.Models;

namespace ULM.Assistant.Services
{
    // Eigenständiges, winziges Lokalisierungs-Set NUR für die Chrome-Texte des Chat-Fensters
    // (Begrüßung, Platzhalter, Fallback, …) — bewusst getrennt von der Haupt-App's
    // LocalizationService/Str (ULM.Assistant referenziert die Haupt-App nicht). Die eigentlichen
    // Katalog-Texte (Fragen/Antworten) liegen direkt zweisprachig in FaqEntry (Task 1+3).
    public enum AssistantStr { WindowTitle, Greeting, InputPlaceholder, SendButton, Fallback, BackToOverview }

    public static class AssistantStrings
    {
        public static string T(AssistantStr key, AssistantLanguage language) =>
            language == AssistantLanguage.German ? De(key) : En(key);

        private static string De(AssistantStr key) => key switch
        {
            AssistantStr.WindowTitle      => "Uli — Hilfe",
            AssistantStr.Greeting         => "Hallo! Ich bin Uli 🐧. Wähle unten ein Thema oder tippe deine Frage:",
            AssistantStr.InputPlaceholder => "Frage eingeben …",
            AssistantStr.SendButton       => "Senden",
            AssistantStr.Fallback         => "Das habe ich leider nicht verstanden — hier sind die Themen, bei denen ich helfen kann:",
            AssistantStr.BackToOverview   => "⬅ Zur Übersicht",
            _ => "",
        };

        private static string En(AssistantStr key) => key switch
        {
            AssistantStr.WindowTitle      => "Uli — Help",
            AssistantStr.Greeting         => "Hi! I'm Uli 🐧. Pick a topic below or type your question:",
            AssistantStr.InputPlaceholder => "Type your question …",
            AssistantStr.SendButton       => "Send",
            AssistantStr.Fallback         => "I'm sorry, I didn't understand that — here are the topics I can help with:",
            AssistantStr.BackToOverview   => "⬅ Back to overview",
            _ => "",
        };
    }
}
```

- [ ] **Step 4: Test laufen lassen — muss bestehen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj --filter AssistantStringsTests`
Expected: `Passed! - Failed: 0, Passed: 6`

- [ ] **Step 5: Commit (best-effort)**

```bash
git add ULM.Assistant/Services/AssistantStrings.cs ULM.Tests/AssistantStringsTests.cs
git commit -m "feat(assistant): add bilingual chat chrome text (AssistantStrings)"
```

---

### Task 5: ChatWindow (Chat-UI: Verlauf, Eingabe, Vorschlag-Buttons)

**Files:**
- Create: `ULM.Assistant/Views/ChatMessageView.cs`
- Create: `ULM.Assistant/Views/ChatWindow.xaml`
- Create: `ULM.Assistant/Views/ChatWindow.xaml.cs`

**Interfaces:**
- Consumes: `FaqCatalogService.Instance.Catalog`, `FaqMatchingEngine.Match(...)`, `AssistantStrings.T(...)`, `AssistantLanguage`, `ChatMessage`/`ChatSender` (Tasks 1-4)
- Produces: `ULM.Assistant.Views.ChatWindow(AssistantLanguage language)` (public constructor, `Window`, nicht-modal via `Show()`) — von `AvatarButton` (Task 6) instanziiert.

Kein automatisierter Test in diesem Task (reine WPF-UI, wie die übrigen `Views/Dialogs`-Klassen der Haupt-App auch ohne UI-Tests auskommen) — manuelle Verifikation folgt in Task 8.

- [ ] **Step 1: `Views/ChatMessageView.cs` anlegen (bindbare Anzeige-Hülle)**

```csharp
// ULM.Assistant/Views/ChatMessageView.cs
using System.Windows;
using System.Windows.Media;
using ULM.Assistant.Models;

namespace ULM.Assistant.Views
{
    // Bindbare Hülle um ChatMessage für die WPF-Anzeige (Blasenfarbe + Ausrichtung je nach
    // Sender) — bewusst getrennt von Models.ChatMessage, damit das reine Datenmodell frei von
    // WPF-Typen (Brush, HorizontalAlignment) bleibt. Liest Farben zur Laufzeit per String-
    // Schlüssel aus Application.Current.Resources — funktioniert automatisch im aktuell
    // aktiven Hell/Dunkel-Theme der Haupt-App, ganz ohne Projekt-Referenz dorthin (dieselbe
    // Technik wie AppRes.Brush(...) in Views/Dialogs/DownloadDialogs.cs der Haupt-App).
    public sealed class ChatMessageView
    {
        public string Text { get; }
        public Brush BubbleBrush { get; }
        public HorizontalAlignment BubbleAlignment { get; }

        public ChatMessageView(ChatMessage message)
        {
            Text = message.Text;
            bool fromUser = message.Sender == ChatSender.User;
            BubbleAlignment = fromUser ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            BubbleBrush = Application.Current?.Resources[fromUser ? "BrushBlue" : "BrushCard"] as Brush
                ?? Brushes.LightGray;
        }
    }
}
```

- [ ] **Step 2: `Views/ChatWindow.xaml` anlegen**

```xml
<Window x:Class="ULM.Assistant.Views.ChatWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Width="380" Height="520" MinWidth="320" MinHeight="400"
        Background="{DynamicResource BrushBg}"
        WindowStartupLocation="Manual"
        ResizeMode="CanResizeWithGrip">
    <Grid Margin="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <TextBlock x:Name="HeaderText" Grid.Row="0" FontSize="16" FontWeight="Bold"
                   Foreground="{DynamicResource BrushHeader}" Margin="0,0,0,10"/>

        <ScrollViewer x:Name="MessagesScroll" Grid.Row="1" VerticalScrollBarVisibility="Auto">
            <ItemsControl x:Name="MessagesList">
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <Border Margin="0,4" Padding="10,7" CornerRadius="10"
                                Background="{Binding BubbleBrush}"
                                HorizontalAlignment="{Binding BubbleAlignment}"
                                MaxWidth="270">
                            <TextBlock Text="{Binding Text}" TextWrapping="Wrap" FontSize="12.5"
                                       Foreground="{DynamicResource BrushHeader}"/>
                        </Border>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </ScrollViewer>

        <WrapPanel x:Name="SuggestionsPanel" Grid.Row="2" Margin="0,8,0,8"/>

        <Grid Grid.Row="3">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>
            <TextBox x:Name="InputBox" Grid.Column="0" FontSize="12.5" Padding="6"
                     VerticalContentAlignment="Center" KeyDown="InputBox_KeyDown"/>
            <Button x:Name="SendButton" Grid.Column="1" Margin="6,0,0,0" Padding="12,6"
                    Style="{DynamicResource BtnPrimary}" Click="SendButton_Click"/>
        </Grid>
    </Grid>
</Window>
```

- [ ] **Step 3: `Views/ChatWindow.xaml.cs` anlegen**

```csharp
// ULM.Assistant/Views/ChatWindow.xaml.cs
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ULM.Assistant.Models;
using ULM.Assistant.Services;

namespace ULM.Assistant.Views
{
    public partial class ChatWindow : Window
    {
        private readonly AssistantLanguage _language;
        private readonly IReadOnlyList<FaqEntry> _catalog;
        private readonly ObservableCollection<ChatMessageView> _messages = new();

        public ChatWindow(AssistantLanguage language)
        {
            InitializeComponent();
            _language = language;
            _catalog  = FaqCatalogService.Instance.Catalog;

            Title = AssistantStrings.T(AssistantStr.WindowTitle, _language);
            HeaderText.Text = "🐧 " + AssistantStrings.T(AssistantStr.WindowTitle, _language);
            SendButton.Content = AssistantStrings.T(AssistantStr.SendButton, _language);
            InputBox.Text = "";
            MessagesList.ItemsSource = _messages;

            AddUliMessage(AssistantStrings.T(AssistantStr.Greeting, _language));
            ShowMainTopics();
        }

        private void ShowMainTopics()
        {
            SuggestionsPanel.Children.Clear();
            foreach (var entry in _catalog)
                SuggestionsPanel.Children.Add(BuildSuggestionButton(entry));
        }

        private void ShowRelated(FaqEntry current)
        {
            SuggestionsPanel.Children.Clear();
            foreach (var relatedId in current.RelatedIds)
            {
                var related = _catalog.FirstOrDefault(e => e.Id == relatedId);
                if (related != null) SuggestionsPanel.Children.Add(BuildSuggestionButton(related));
            }

            var back = new Button
            {
                Content = AssistantStrings.T(AssistantStr.BackToOverview, _language),
                Style = Application.Current.Resources["BtnGhost"] as Style,
                Margin = new Thickness(0, 0, 6, 6),
            };
            back.Click += (_, _) => ShowMainTopics();
            SuggestionsPanel.Children.Add(back);
        }

        private Button BuildSuggestionButton(FaqEntry entry)
        {
            string label = _language == AssistantLanguage.German ? entry.QuestionLabelDe : entry.QuestionLabelEn;
            var btn = new Button
            {
                Content = label,
                Style = Application.Current.Resources["BtnSecondary"] as Style,
                Margin = new Thickness(0, 0, 6, 6),
                Tag = entry.Id,
            };
            btn.Click += (_, _) => AnswerTopic(entry);
            return btn;
        }

        private void AnswerTopic(FaqEntry entry)
        {
            string label  = _language == AssistantLanguage.German ? entry.QuestionLabelDe : entry.QuestionLabelEn;
            string answer = _language == AssistantLanguage.German ? entry.AnswerDe        : entry.AnswerEn;
            AddUserMessage(label);
            AddUliMessage(answer);
            ShowRelated(entry);
        }

        private void SendButton_Click(object sender, RoutedEventArgs e) => SubmitInput();

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) SubmitInput();
        }

        private void SubmitInput()
        {
            string text = InputBox.Text.Trim();
            if (text.Length == 0) return;
            InputBox.Text = "";
            AddUserMessage(text);

            string? matchId = FaqMatchingEngine.Match(_catalog, _language, text);
            var match = matchId is null ? null : _catalog.FirstOrDefault(e => e.Id == matchId);
            if (match is null)
            {
                AddUliMessage(AssistantStrings.T(AssistantStr.Fallback, _language));
                ShowMainTopics();
            }
            else
            {
                string answer = _language == AssistantLanguage.German ? match.AnswerDe : match.AnswerEn;
                AddUliMessage(answer);
                ShowRelated(match);
            }
        }

        private void AddUserMessage(string text) => _messages.Add(new ChatMessageView(new ChatMessage { Sender = ChatSender.User, Text = text }));
        private void AddUliMessage(string text)  => _messages.Add(new ChatMessageView(new ChatMessage { Sender = ChatSender.Uli,  Text = text }));
    }
}
```

- [ ] **Step 4: Build-Check**

Run: `dotnet build "ULM.Assistant/ULM.Assistant.csproj"`
Expected: `Build succeeded.` (0 Fehler — insbesondere prüft dies, dass `x:Class="ULM.Assistant.Views.ChatWindow"` korrekt zur `InitializeComponent()`-Codegenerierung passt)

- [ ] **Step 5: Commit (best-effort)**

```bash
git add ULM.Assistant/Views/ChatMessageView.cs ULM.Assistant/Views/ChatWindow.xaml ULM.Assistant/Views/ChatWindow.xaml.cs
git commit -m "feat(assistant): add ChatWindow UI (message bubbles, input, suggestion buttons)"
```

---

### Task 6: AvatarButton (schwebender 🐧-Button)

**Files:**
- Create: `ULM.Assistant/Views/AvatarButton.xaml`
- Create: `ULM.Assistant/Views/AvatarButton.xaml.cs`

**Interfaces:**
- Consumes: `ULM.Assistant.Views.ChatWindow` (Task 5), `ULM.Assistant.Models.AssistantLanguage` (Task 1)
- Produces: `ULM.Assistant.Views.AvatarButton` (`UserControl`, öffentliche Property `GetLanguage : Func<AssistantLanguage>`) — von der Haupt-App (Task 7) in `MainWindow.xaml` eingebunden und in `MainWindow.xaml.cs` mit `LocalizationService.Current` verdrahtet.

- [ ] **Step 1: `Views/AvatarButton.xaml` anlegen**

```xml
<UserControl x:Class="ULM.Assistant.Views.AvatarButton"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Width="52" Height="52">
    <Button x:Name="RootButton" Click="RootButton_Click"
            Width="52" Height="52" Padding="0"
            Content="🐧" FontSize="26"
            Cursor="Hand"
            ToolTip="Uli">
        <Button.Template>
            <ControlTemplate TargetType="Button">
                <Border Background="{DynamicResource BrushBlue}" CornerRadius="26">
                    <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                </Border>
            </ControlTemplate>
        </Button.Template>
    </Button>
</UserControl>
```

- [ ] **Step 2: `Views/AvatarButton.xaml.cs` anlegen**

```csharp
// ULM.Assistant/Views/AvatarButton.xaml.cs
using System;
using System.Windows;
using System.Windows.Controls;
using ULM.Assistant.Models;

namespace ULM.Assistant.Views
{
    // Schwebender 🐧-Button, den die Haupt-App irgendwo im Hauptfenster platziert. GetLanguage
    // wird von der Haupt-App gesetzt (Dependency Injection statt Projekt-Referenz — siehe Spec,
    // Architektur-Korrektur). Ein zweiter Klick, während das Chat-Fenster schon offen ist, holt
    // es nur nach vorne, statt ein zweites zu öffnen.
    public partial class AvatarButton : UserControl
    {
        public Func<AssistantLanguage> GetLanguage { get; set; } = () => AssistantLanguage.English;

        private ChatWindow? _openWindow;

        public AvatarButton()
        {
            InitializeComponent();
        }

        private void RootButton_Click(object sender, RoutedEventArgs e)
        {
            if (_openWindow is { IsLoaded: true })
            {
                _openWindow.Activate();
                return;
            }

            _openWindow = new ChatWindow(GetLanguage());
            _openWindow.Closed += (_, _) => _openWindow = null;
            _openWindow.Show();
        }
    }
}
```

- [ ] **Step 3: Build-Check**

Run: `dotnet build "ULM.Assistant/ULM.Assistant.csproj"`
Expected: `Build succeeded.` (0 Fehler)

- [ ] **Step 4: Commit (best-effort)**

```bash
git add ULM.Assistant/Views/AvatarButton.xaml ULM.Assistant/Views/AvatarButton.xaml.cs
git commit -m "feat(assistant): add floating AvatarButton that opens ChatWindow"
```

---

### Task 7: Einbindung in die Haupt-App

**Files:**
- Modify: `UniversalLinuxManager.csproj`
- Modify: `Views/MainWindow.xaml`
- Modify: `Views/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `ULM.Assistant.Views.AvatarButton`, `ULM.Assistant.Models.AssistantLanguage` (Tasks 1-6), `ULM.Infrastructure.LocalizationService.Current`, `ULM.Infrastructure.AppLanguage` (bestehend)

- [ ] **Step 1: `UniversalLinuxManager.csproj` — Projekt-Referenz + Glob-Ausschluss hinzufügen**

Füge im bestehenden `<ItemGroup>` mit dem `<Compile Remove="Linux\**" />`-Block (siehe Kommentar dort) einen analogen Ausschluss für `ULM.Assistant\**` hinzu — sonst kompiliert der implizite Compile-Glob des Hauptprojekts die `.xaml.cs`-Dateien von `ULM.Assistant` versehentlich doppelt mit (bekannte Falle, bereits zweimal beim Linux-Projekt aufgetreten, siehe `docs/superpowers/plans/2026-07-28-linux-gui-phase1.md`):

```xml
    <!-- ULM.Assistant/ enthaelt das eigenstaendige ULM.Assistant-Projekt (eigenes .csproj,
         eigene .xaml/.cs-Dateien) — ohne diesen Ausschluss kompiliert der implizite Compile-
         Glob dieses Hauptprojekts sie versehentlich mit (x:Class-Duplikate). -->
    <Compile Remove="ULM.Assistant\**" />
    <None    Remove="ULM.Assistant\**" />
    <Content Remove="ULM.Assistant\**" />
    <EmbeddedResource Remove="ULM.Assistant\**" />
```

Füge zusätzlich in einer neuen `<ItemGroup>` die Projekt-Referenz hinzu:

```xml
  <ItemGroup>
    <ProjectReference Include="ULM.Assistant\ULM.Assistant.csproj" />
  </ItemGroup>
```

- [ ] **Step 2: `Views/MainWindow.xaml` — Namespace + AvatarButton einbinden**

Füge im `<Window ...>`-Wurzelelement (direkt neben den bestehenden `xmlns:x=...`-Zeilen) hinzu:

```xml
        xmlns:assistant="clr-namespace:ULM.Assistant.Views;assembly=ULM.Assistant"
```

Füge als LETZTES Kind-Element im äußeren `<Grid>` (dem mit den 6 `RowDefinition`-Zeilen, direkt vor dem schließenden `</Grid>` am Ende der Datei) hinzu — als letztes Kind liegt es über allen anderen Zeilen, `Grid.RowSpan="6"` lässt es über alle Reihen hinweg (Header bis Statusleiste) schweben:

```xml
        <assistant:AvatarButton x:Name="UliButton" Grid.Row="0" Grid.RowSpan="6"
                                 HorizontalAlignment="Right" VerticalAlignment="Bottom"
                                 Margin="0,0,20,20" Panel.ZIndex="999"/>
```

- [ ] **Step 3: `Views/MainWindow.xaml.cs` — Sprache verdrahten**

Füge zu den bestehenden `using`-Zeilen am Dateianfang hinzu:

```csharp
using ULM.Assistant.Models;
```

Füge im `MainWindow()`-Konstruktor, direkt nach der Zeile `DataContext = _vm;`, hinzu:

```csharp
            UliButton.GetLanguage = () => LocalizationService.Current == AppLanguage.German
                ? AssistantLanguage.German
                : AssistantLanguage.English;
```

- [ ] **Step 4: Build-Check Hauptprojekt**

Run: `dotnet build UniversalLinuxManager.csproj -c Release`
Expected: `Build succeeded.` (0 Fehler — bestätigt insbesondere, dass kein Referenz-Zyklus entsteht und der Glob-Ausschluss korrekt greift)

- [ ] **Step 5: Commit (best-effort)**

```bash
git add UniversalLinuxManager.csproj Views/MainWindow.xaml Views/MainWindow.xaml.cs
git commit -m "feat(assistant): wire AvatarButton into MainWindow"
```

---

### Task 8: Vollständige Verifikation

**Files:** Keine neuen Dateien — reine Verifikation.

- [ ] **Step 1: Komplette Testsuite ausführen**

Run: `dotnet test ULM.Tests/ULM.Tests.csproj -c Release`
Expected: Alle bisherigen 198 Tests weiterhin grün + die 15 neuen Assistant-Tests aus Task 2-4 (5 Matching + 5 Catalog + 6 Strings — wobei ein Test mehrfach über `[Theory]` läuft, siehe Task 4) grün, `Failed: 0`.

- [ ] **Step 2: Release-Build der Haupt-App**

Run: `dotnet build UniversalLinuxManager.csproj -c Release`
Expected: `Build succeeded.`, 0 Fehler, 0 neue Warnungen.

- [ ] **Step 3: App manuell starten und Uli auf Deutsch testen**

Run: `& "bin\Release\net8.0-windows\win-x64\UniversalLinuxManager.exe"` (Pfad kann je nach Publish-Konfiguration leicht abweichen — im Zweifel den tatsächlichen Pfad unter `bin\Release\` prüfen)

Manuell prüfen (ULM läuft standardmäßig auf Deutsch, falls nicht zuvor auf Englisch umgestellt):
1. Unten rechts im Hauptfenster ist der schwebende 🐧-Button sichtbar, auch nach Tab-Wechsel.
2. Klick öffnet ein nicht-modales Fenster ("Uli — Hilfe"), Hauptfenster bleibt weiterhin bedienbar.
3. Begrüßungstext + 10 Themen-Buttons erscheinen.
4. Klick auf einen Themen-Button zeigt Frage (rechts) + Antwort (links) + neue Anschluss-Buttons inkl. "⬅ Zur Übersicht".
5. Freitext "wie lade ich was runter" im Eingabefeld + Enter → Download-Thema wird erkannt und beantwortet.
6. Freitext "asdkjaslkdj" (Unsinn) → Fallback-Satz + Themenübersicht erscheint erneut.
7. Fenster schließen und erneut über den Button öffnen → Chat startet frisch bei der Begrüßung (kein alter Verlauf).

- [ ] **Step 4: Sprachumschaltung auf Englisch prüfen**

Über '⚙ Einstellungen' im Hauptfenster auf Englisch umstellen, ULM neu starten (Sprachwechsel wirkt laut Bestandsverhalten erst nach Neustart), dann Schritt 3 (Punkte 1-6) auf Englisch wiederholen — Fenstertitel "Uli — Help", alle Buttons/Texte auf Englisch.

- [ ] **Step 5: Zusammenfassung dokumentieren**

Kurze Notiz in `docs/superpowers/plans/2026-08-10-avatar-assistant.md` (dieses Dokument) unterhalb dieses Tasks ergänzen: Testergebnis (Anzahl Tests grün), manuelle Verifikation bestanden ja/nein mit Datum, offene Punkte (z.B. falls `assistant_faq.json` beim ersten Start aus irgendeinem Grund nicht angelegt wurde).

- [ ] **Step 6: Commit (best-effort) — kein Merge/Push ohne ausdrückliche Nutzer-Freigabe**

```bash
git add docs/superpowers/plans/2026-08-10-avatar-assistant.md
git commit -m "docs(assistant): record verification results for Uli avatar assistant"
```
