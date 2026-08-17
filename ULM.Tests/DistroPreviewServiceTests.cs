// ULM.Tests/DistroPreviewServiceTests.cs
using ULM.Core.Services;
using Xunit;

namespace ULM.Tests
{
    /// <summary>
    /// Testet DistroPreviewService.ParseProfileHtml gegen ein reales, live abgerufenes
    /// DistroWatch-Profilseiten-Fixture (ThorOS, 2026-08-17) — kein echter Netzwerkaufruf.
    /// ParseProfileHtml ist internal, für dieses Testprojekt sichtbar via InternalsVisibleTo
    /// (siehe UniversalLinuxManager.csproj).
    /// </summary>
    public class DistroPreviewServiceTests
    {
        // Trimmter, aber strukturell exakter Ausschnitt einer echten DistroWatch-Profilseite.
        // Enthält bewusst deutsche Sidebar-Labels ("Basiert auf:", "Status:" etc.) — die dürfen
        // vom Parser NICHT gelesen werden, nur die href-Query-Parameter (basedon=, origin=, ...).
        private const string SampleHtml = """
            <img src="images/icon-large/thoros.png" border="0" title="ThorOS" vspace="23" hspace="32" align="left">
            <a href="images/slinks/thoros.png"><img src="images/slinks/thoros-small.png" border="0" title="ThorOS" vspace="6" hspace="6" align="right" style="width: 100%; max-width: 480px;"></a>
            <ul><li><b>Betriebssystem-Typ:</b> <a href="search.php?ostype=Linux#simple">Linux</a><br></li><li><b>Basiert auf:</b> <a href="search.php?basedon=Debian (Stable)#simple">Debian (Stable)</a><br></li><li><b>Herkunft:</b> <a href="search.php?origin=USA#simple">USA</a>
            <br></li><li><b>Architektur:</b> <a href="search.php?architecture=x86_64#simple">x86_64</a><br></li><li><b>Desktop:</b> <a href="search.php?desktop=GNOME#simple">GNOME</a><br></li><li><b>Kategorie:</b> <a href="search.php?category=Desktop#simple">Desktop</a>, <a href="search.php?category=Large+Language+Model#simple">Large Language Model</a>, <a href="search.php?category=Live+Medium#simple">Live Medium</a><br></li><li><b>Status:</b> <font color="green">Aktiv</font><br></li><li><b>Popularität:</b> <a href="dwres.php?resource=popularity">488 (18 Treffer pro Tag)</a>
            </li></ul>
            ThorOS is a Debian-based desktop Linux distribution featuring the GNOME desktop. Its principal feature is "voice control".
                <br><br>
            <b><a href="dwres.php?resource=popularity">Popularität</a></b>
            """;

        [Fact]
        public void ParseProfileHtml_ExtractsAllFactsFromRealPageStructure()
        {
            var result = DistroPreviewService.ParseProfileHtml("ThorOS", "thoros", SampleHtml);

            Assert.Equal("ThorOS", result.Name);
            Assert.Equal("Debian (Stable)", result.BasedOn);
            Assert.Equal("USA", result.Origin);
            Assert.Equal("x86_64", result.Architecture);
            Assert.Equal("GNOME", result.Desktop);
            Assert.True(result.IsActive);
            Assert.Equal(488, result.PopularityRank);
            Assert.Equal(18, result.PopularityHitsPerDay);
            Assert.StartsWith("ThorOS is a Debian-based desktop Linux distribution", result.Description);
            Assert.DoesNotContain("<br", result.Description);
        }

        [Fact]
        public void ParseProfileHtml_InactiveStatus_ColorNotGreen()
        {
            string html = SampleHtml.Replace("""<font color="green">Aktiv</font>""", """<font color="red">Eingestellt</font>""");
            var result = DistroPreviewService.ParseProfileHtml("ThorOS", "thoros", html);
            Assert.False(result.IsActive);
        }

        [Fact]
        public void ParseProfileHtml_MissingStatusTag_IsActiveNull()
        {
            string html = SampleHtml.Replace("""<li><b>Status:</b> <font color="green">Aktiv</font><br></li>""", "");
            var result = DistroPreviewService.ParseProfileHtml("ThorOS", "thoros", html);
            Assert.Null(result.IsActive);
        }

        [Fact]
        public void ParseProfileHtml_MissingFact_ReturnsEmptyStringNotNull()
        {
            string html = SampleHtml.Replace("""<li><b>Herkunft:</b> <a href="search.php?origin=USA#simple">USA</a>""", "");
            var result = DistroPreviewService.ParseProfileHtml("ThorOS", "thoros", html);
            Assert.Equal(string.Empty, result.Origin);
        }

        // Regressionstest für einen live gefundenen Bug (2026-08-17, SkillFishOS): DistroWatch-
        // Seiten haben VOR der Fakten-Liste ein Navigations-Dropdown-Menü (ebenfalls ein <ul>),
        // direkt gefolgt von einem <script>-Block mit dessen JavaScript. Ein naives "erstes </ul>
        // im Dokument"-Muster griff dieses Nav-Menü statt der Fakten-Liste und lieferte den
        // JavaScript-Quelltext als "Beschreibung" zurück (live im Popup beobachtet).
        [Fact]
        public void ParseProfileHtml_IgnoresEarlierNavigationMenuUlBeforeFactsList()
        {
            const string navMenuPrefix = """
                <ul class="dropdown-content">
                  <li><a href="/">Startseite</a></li>
                </ul>
                <script language="JavaScript">
                <!--
                function ClearMenus()
                {
                    var dropdowns = document.getElementsByClassName("dropdown-content");
                }
                //-->
                </script>

                """;
            string html = navMenuPrefix + SampleHtml;

            var result = DistroPreviewService.ParseProfileHtml("ThorOS", "thoros", html);

            Assert.StartsWith("ThorOS is a Debian-based desktop Linux distribution", result.Description);
            Assert.DoesNotContain("ClearMenus", result.Description);
            Assert.DoesNotContain("<script", result.Description);
        }
    }
}
