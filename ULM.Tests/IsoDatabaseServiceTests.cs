using System;
using System.IO;
using System.Linq;
using ULM.Core.Models;
using ULM.Core.Services;
using ULM.Infrastructure;
using Xunit;

namespace ULM.Tests;

/// <summary>
/// Regression fuer den finalen Whole-Branch-Review: IsoDatabaseService.Save()/LoadFromIni()
/// verwenden einen HANDGESCHRIEBENEN, festen INI-Feld-Katalog (keinen Reflection-Serializer).
/// Sha256/Sha256Source (Task 1, IsoEntry) waren dort bisher nicht gelistet -> sie wurden beim
/// Speichern stillschweigend verworfen und beim Laden defaulteten sie auf string.Empty. Effekt:
/// nach jedem App-Neustart war der Referenz-Hash JEDES Eintrags leer, wodurch
/// DetectVersionlessHashMismatchesAsync (Task 6) und VerifyStickIntegrityAsync (Task 8) jeden
/// Eintrag stillschweigend uebersprungen haben ("0 ISO(s) verifiziert").
///
/// IsoDatabaseService ist ein Singleton mit privatem Konstruktor (kein DI-Einstiegspunkt), und
/// AppPaths (ebenfalls Singleton) bestimmt den INI-Pfad. Ein isolierter Konstruktor-Test ist daher
/// nicht moeglich, ohne Produktionscode fuer den Test umzubauen (nicht Teil dieses Fixes). Statt
/// Reflection auf private Felder wird deshalb ueber die oeffentliche API getestet: AppPaths.Instance
/// wird voruebergehend auf ein Temp-Verzeichnis umgebogen (SetPaths setzt nur Pfade, legt keine
/// Ordner an), die Singleton-Eintragsliste wird gesichert/geleert und im finally wiederhergestellt,
/// damit dieser Test keine anderen Tests beeinflusst, die denselben Prozess-Singleton verwenden.
/// </summary>
public class IsoDatabaseServiceRoundTripTests
{
    [Fact]
    public void SaveThenLoad_PreservesSha256AndSha256Source()
    {
        AppPaths paths = AppPaths.Instance;
        IsoDatabaseService db = IsoDatabaseService.Instance;

        string originalBase = paths.BaseDirectory;
        var originalEntries = db.Entries.ToList();
        string tempDir = Path.Combine(Path.GetTempPath(), $"ulm-db-roundtrip-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempDir);
            paths.SetPaths(tempDir);

            while (db.Count > 0) db.Remove(0);

            string expectedHash = string.Concat(Enumerable.Repeat("a1", 32)); // 64 hex-Zeichen
            var entry = new IsoEntry
            {
                Name         = "Roundtrip-Test-Distro",
                Filename     = "roundtrip-test.iso",
                Sha256       = expectedHash,
                Sha256Source = "OfficialChecksum",
            };
            db.Add(entry);
            db.Save();

            // Erzwingt echtes Neu-Einlesen von der (jetzt vorhandenen) INI-Datei im Temp-Verzeichnis
            // — genau der Codepfad, der bei einem echten App-Neustart durchlaufen wird.
            db.Load();

            IsoEntry? reloaded = db.Entries.SingleOrDefault(e => e.Name == "Roundtrip-Test-Distro");
            Assert.NotNull(reloaded);
            Assert.Equal(expectedHash, reloaded!.Sha256);
            Assert.Equal("OfficialChecksum", reloaded.Sha256Source);
        }
        finally
        {
            while (db.Count > 0) db.Remove(0);
            foreach (var e in originalEntries) db.Add(e);
            paths.SetPaths(originalBase);
            try { Directory.Delete(tempDir, recursive: true); } catch { /* Best-effort Cleanup */ }
        }
    }

    /// <summary>
    /// Regression: ein Programmabsturz/-Neustart MITTEN in einem laufenden Download hinterließ
    /// bisher keine gespeicherte Ziel-Größe — der reguläre Save() läuft erst, nachdem der GESAMTE
    /// Download-Batch fertig ist (MainViewModel.StartDownload). SaveExpectedSize() muss die beim
    /// Verbindungsaufbau bereits bekannte Content-Length SOFORT persistieren (wie SaveFilenames()),
    /// damit sie einen Absturz vor Batch-Ende übersteht.
    /// </summary>
    [Fact]
    public void SaveExpectedSize_PersistsImmediately_SurvivesReloadWithoutFullSave()
    {
        AppPaths paths = AppPaths.Instance;
        IsoDatabaseService db = IsoDatabaseService.Instance;

        string originalBase = paths.BaseDirectory;
        var originalEntries = db.Entries.ToList();
        string tempDir = Path.Combine(Path.GetTempPath(), $"ulm-db-expsize-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempDir);
            paths.SetPaths(tempDir);

            while (db.Count > 0) db.Remove(0);

            var entry = new IsoEntry { Name = "Crash-Mid-Download-Distro", Filename = "crash-test.iso" };
            db.Add(entry);
            db.Save(); // Grundzustand (ExpectedSizeBytes=0) liegt bereits auf Platte

            db.SaveExpectedSize(entry, 4_500_000_000L); // Server-Content-Length wird beim Download-Start bekannt
            // Ab hier: kein weiterer db.Save() — simuliert Prozessabsturz mitten im Download.

            db.Load(); // wie bei echtem App-Neustart

            IsoEntry? reloaded = db.Entries.SingleOrDefault(e => e.Name == "Crash-Mid-Download-Distro");
            Assert.NotNull(reloaded);
            Assert.Equal(4_500_000_000L, reloaded!.ExpectedSizeBytes);
        }
        finally
        {
            while (db.Count > 0) db.Remove(0);
            foreach (var e in originalEntries) db.Add(e);
            paths.SetPaths(originalBase);
            try { Directory.Delete(tempDir, recursive: true); } catch { /* Best-effort Cleanup */ }
        }
    }

    [Fact]
    public void LoadFromIni_MissingShaKeys_DefaultsToEmpty_BackwardCompatibleWithOldDatabases()
    {
        AppPaths paths = AppPaths.Instance;
        IsoDatabaseService db = IsoDatabaseService.Instance;

        string originalBase = paths.BaseDirectory;
        var originalEntries = db.Entries.ToList();
        string tempDir = Path.Combine(Path.GetTempPath(), $"ulm-db-oldformat-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempDir);
            paths.SetPaths(tempDir);

            // Simuliert eine ALTE ulm_isos.ini ohne Sha256/Sha256Source-Keys (vor diesem Fix).
            string oldFormatIni =
                "[General]\r\n" +
                "Count = 1\r\n" +
                "\r\n" +
                "[ISO_0]\r\n" +
                "Name        = Alte Distro\r\n" +
                "Category    = Einsteiger\r\n" +
                "URL         = \r\n" +
                "Filename    = alte-distro.iso\r\n" +
                "Mirror1     = \r\n" +
                "Mirror2     = \r\n" +
                "Mirror3     = \r\n" +
                "Mirror4     = \r\n" +
                "Mirror5     = \r\n" +
                "GitHubRepo  = \r\n" +
                "GitHubAsset = \r\n" +
                "Tip         = \r\n";
            File.WriteAllText(paths.DatabaseIni, oldFormatIni);

            db.Load();

            IsoEntry? loaded = db.Entries.SingleOrDefault(e => e.Name == "Alte Distro");
            Assert.NotNull(loaded);
            Assert.Equal(string.Empty, loaded!.Sha256);
            Assert.Equal(string.Empty, loaded.Sha256Source);
        }
        finally
        {
            while (db.Count > 0) db.Remove(0);
            foreach (var e in originalEntries) db.Add(e);
            paths.SetPaths(originalBase);
            try { Directory.Delete(tempDir, recursive: true); } catch { /* Best-effort Cleanup */ }
        }
    }
}
