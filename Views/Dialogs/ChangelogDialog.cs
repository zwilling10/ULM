// Views/Dialogs/ChangelogDialog.cs
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ULM.Infrastructure;

namespace ULM.Views.Dialogs
{
    // ═══════════════════════════════════════════════════════════════════
    // ChangelogDialog — "Was ist neu?"
    //
    // Wird einmalig gezeigt, wenn sich die Version seit dem letzten Start
    // geändert hat (siehe MainWindow.OnLoaded: LastSeenVersion-Abgleich in
    // ulm_settings.ini). Bei jedem Release oben einen neuen Eintrag in
    // 'History' ergänzen — neueste Version zuerst. Jeder Eintrag braucht
    // sowohl NotesDe als auch NotesEn (gleiche Reihenfolge/Anzahl) —
    // LocalizationService.Current entscheidet zur Laufzeit, welche Liste
    // angezeigt wird.
    // ═══════════════════════════════════════════════════════════════════
    public sealed class ChangelogDialog : Window
    {
        private static readonly (string Version, string[] NotesDe, string[] NotesEn)[] History =
        {
            ("2.43.0",
            new[]
            {
                "Neu: Unformatierte (\"rohe\") USB-Sticks werden jetzt zuverlässig erkannt und lassen sich in einem einzigen Rutsch (nur noch eine Benutzerkontensteuerung-Abfrage statt zwei) als Ventoy-Stick einrichten.",
                "Fehlerbehebung: Nach eigener Ventoy-Installation wurde der Stick manchmal kurzzeitig fälschlich wieder als \"kein Ventoy\" erkannt und der destruktive Löschen-Dialog angeboten; außerdem wurden lokal bereits heruntergeladene ISOs beim Einstecken eines frisch eingerichteten Sticks nicht mehr automatisch zum Kopieren angeboten.",
                "Neu: Die automatische Quellauflösung findet jetzt auch Distros über eine gezielte SourceForge-Projektsuche, wenn die offizielle Projekt-Homepage keinen direkten Download-Link bietet.",
                "Fehlerbehebung: Die Sammel-Kopfzeile im Download-Fortschrittsfenster blieb nach einem manuellen Quellen-Nachtrag (\"🔧 Quelle manuell suchen\") auf der alten Bilanz des ersten Versuchs stehen, statt den laufenden Fortschritt zu zeigen; außerdem zeigte das Fenster \"Download-Fortschritt\", obwohl nur lokal vorhandene ISOs auf den Stick kopiert wurden.",
                "Fehlerbehebung: Der DB-Gesundheitscheck öffnete sein Ergebnis-Popup auch bei automatischen Prüfungen im Hintergrund (z.B. nach \"ISO suchen\") — erscheint jetzt nur noch nach einem Klick auf den Gesundheitscheck-Button.",
                "Neu: In \"🔍 ISO suchen\" reicht jetzt ein Häkchen pro Zeile, um eine Distro zur Datenbank hinzuzufügen und direkt herunterzuladen — die separate \"Direkt herunterladen\"-Checkbox ist entfallen.",
            },
            new[]
            {
                "New: Unformatted (\"raw\") USB sticks are now detected reliably and can be set up as a Ventoy stick in a single pass (just one User Account Control prompt instead of two).",
                "Fix: After ULM's own Ventoy installation, the stick was sometimes briefly misdetected as \"no Ventoy\" again and the destructive delete dialog was offered; also, already-downloaded ISOs were no longer automatically offered for copying when a freshly set-up stick was plugged in.",
                "New: Automatic source resolution now also finds distros via a targeted SourceForge project search when the official project homepage doesn't offer a direct download link.",
                "Fix: After adding a source manually (\"🔧 Search source manually\"), the summary header in the download progress window kept showing the old tally from the first attempt instead of the ongoing progress; also, the window showed \"Download progress\" even when only locally available ISOs were being copied to the stick.",
                "Fix: The DB health check opened its result popup even for automatic background checks (e.g. after \"Search ISO\") — now only appears after clicking the health-check button.",
                "New: In \"🔍 Search ISO\", a single checkbox per row is now enough to add a distro to the database and download it directly — the separate \"Download directly\" checkbox has been removed.",
            }),
            ("2.42.0",
            new[]
            {
                "Neu: In \"🔍 ISO suchen\" zeigt ein neues 🔍-Vorschau-Icon pro Zeile eine Karte mit Screenshot, Kurzfakten (Basiert auf, Desktop, Herkunft, Architektur, Status, Popularität), Beschreibung und Tags — bevor man eine Distro herunterlädt, direkt in der App und ohne DistroWatch im Browser aufzurufen.",
            },
            new[]
            {
                "New: In \"🔍 Search ISO\", a new 🔍 preview icon per row shows a card with screenshot, quick facts (based on, desktop, origin, architecture, status, popularity), description and tags — before downloading a distro, right in the app and without opening DistroWatch in a browser.",
            }),
            ("2.41.2",
            new[]
            {
                "Fehlerbehebung: In \"🔍 ISO suchen\" schlug die automatische Kategorie-Zuordnung bei einigen DistroWatch-Neuzugängen fehl (z.B. wurde \"ThorOS\" als \"Einsteiger\" statt \"Fortgeschrittene\" vorgeschlagen) — die Zuordnung nutzte teils Tag-Namen, die es bei DistroWatch gar nicht gibt.",
            },
            new[]
            {
                "Fix: In \"🔍 Search ISO\", automatic category assignment failed for some new DistroWatch entries (e.g. \"ThorOS\" was suggested as \"Beginner\" instead of \"Advanced\") — the assignment partly relied on tag names that don't actually exist on DistroWatch.",
            }),
            ("2.41.1",
            new[]
            {
                "Fehlerbehebung: Im Reiter \"Aktuellste\" von \"🔍 ISO suchen\" wurde fälschlich \"Keine Live-Medium-Distros gefunden\" angezeigt, weil DistroWatch die Linkstruktur seiner Neuzugänge-Liste geändert hatte.",
            },
            new[]
            {
                "Fix: The \"Latest\" tab of \"🔍 Search ISO\" incorrectly showed \"No live-medium distros found\" because DistroWatch had changed the link structure of its new-additions list.",
            }),
            ("2.41.0",
            new[]
            {
                "Neu: Uli, der kleine Assistent unten rechts im Hauptfenster, beantwortet häufige Fragen zu Suche, Download, Kopieren auf den Stick, Ventoy-Einrichtung und mehr — komplett lokal, ohne Internetverbindung oder Cloud-KI.",
                "Neu: USB-Sticks, die z.B. mit Rufus im ISO/DD-Modus beschrieben wurden und deshalb keinen Laufwerksbuchstaben mehr bekommen, werden jetzt ebenfalls erkannt und können nach Bestätigung für Ventoy vorbereitet werden.",
                "Neu: Ein Willkommens-Fenster beim allerersten Start erklärt kurz die wichtigsten Funktionen, bevor die Arbeitsordner-Auswahl folgt.",
                "Fehlerbehebung: USB-Sticks, die von Rufus als CD-ROM statt als Wechseldatenträger erkannt wurden, tauchten bisher nicht in der Laufwerksliste auf.",
            },
            new[]
            {
                "New: Uli, the small assistant in the bottom-right of the main window, answers common questions about search, download, copying to the stick, Ventoy setup and more — entirely local, without an internet connection or cloud AI.",
                "New: USB sticks that were written with, e.g., Rufus in ISO/DD mode and therefore no longer receive a drive letter are now also detected and can be prepared for Ventoy after confirmation.",
                "New: A welcome window on the very first start briefly explains the most important features before the working-folder selection follows.",
                "Fix: USB sticks that Windows detected as a CD-ROM instead of a removable drive (e.g. after being written by Rufus) previously didn't show up in the drive list.",
            }),
            ("2.40.0",
            new[]
            {
                "Neu: ULM ist jetzt zweisprachig (Deutsch/Englisch) — umschaltbar über \"⚙ Einstellungen\". Betrifft das komplette Programm: Hauptfenster, alle Dialoge, Fehlermeldungen, Protokoll- und Statusanzeigen.",
                "Neu: ISO-Beschreibungen in der Datenbank können jetzt zusätzlich auf Englisch hinterlegt werden und werden im Englisch-Modus automatisch angezeigt.",
                "Neu: Die Projektseite (Download-Seite) hat jetzt ebenfalls einen Sprachumschalter (Deutsch/Englisch).",
                "Fehlerbehebung: Die Kategorie-Auswahl in den Datenbank-Dialogen zeigte bisher immer die internen deutschen Bezeichnungen statt der übersetzten Kategorie.",
                "Sicherheit: Release-Builds betten ab sofort keine lokalen Datei-Pfade mehr als Debug-Metadaten in die EXE ein.",
            },
            new[]
            {
                "New: ULM is now bilingual (German/English) — switchable via \"⚙ Settings\". Covers the entire program: main window, all dialogs, error messages, log and status displays.",
                "New: ISO descriptions in the database can now also be stored in English and are shown automatically in English mode.",
                "New: The project page (download page) now also has a language switch (German/English).",
                "Fix: Category selection in the database dialogs previously always showed the internal German labels instead of the translated category.",
                "Security: Release builds no longer embed local file paths as debug metadata in the EXE.",
            }),
            ("2.39.1",
            new[]
            {
                "Fehlerbehebung: Nach einem automatischen Selbst-Update (installierte Variante) startete ULM manchmal nicht von selbst neu, obwohl die Installation erfolgreich war — man musste es manuell über das Icon erneut öffnen. ULM verlässt sich für den Neustart jetzt nicht mehr auf Windows' eingebauten Mechanismus dafür, sondern startet sich zuverlässig selbst neu.",
            },
            new[]
            {
                "Fix: After an automatic self-update (installed variant), ULM sometimes didn't restart on its own even though the installation succeeded — it had to be reopened manually via the icon. ULM no longer relies on Windows' built-in mechanism for the restart and instead reliably restarts itself.",
            }),
            ("2.39.0",
            new[]
            {
                "Neu: Ein automatisch heruntergeladenes Update wird jetzt vor der Installation per SHA256-Prüfsumme verifiziert — bei einer Abweichung wird es verworfen, statt es zu übernehmen.",
                "Neu: Jedes Release enthält ab sofort zusätzlich eine SHA256SUMS-Datei, mit der sich heruntergeladene Dateien eigenständig auf Unversehrtheit prüfen lassen.",
            },
            new[]
            {
                "New: An automatically downloaded update is now verified via SHA256 checksum before installation — if it doesn't match, it's discarded instead of being applied.",
                "New: Every release now additionally includes a SHA256SUMS file that lets you independently verify the integrity of downloaded files.",
            }),
            ("2.38.1",
            new[]
            {
                "Fehlerbehebung: Eine nach einem von ULM selbst durchgeführten Stick-Update überflüssig gewordene alte ISO-Version wurde fälschlich als „unbekannte Distro“ statt als zu löschendes Duplikat gemeldet — dabei konnte der Katalog-Eintrag versehentlich auf die alte Version zurückfallen.",
                "Fehlerbehebung: Die Versionsermittlung für Debian konnte je nachdem, welcher Spiegelserver zuerst antwortete, mal eine ältere, mal die aktuelle Version als „neueste“ melden.",
            },
            new[]
            {
                "Fix: An old ISO version made obsolete by a stick update ULM performed itself was incorrectly reported as an \"unknown distro\" instead of a duplicate to be deleted — in the process, the catalog entry could accidentally fall back to the old version.",
                "Fix: Version detection for Debian could report either an older or the current version as \"latest\", depending on which mirror server responded first.",
            }),
            ("2.38.0",
            new[]
            {
                "Neu: Findet ULM eine neuere Programmversion, wird sie jetzt automatisch im Hintergrund heruntergeladen. Das Banner bietet danach „Jetzt installieren & neu starten“ an — ULM installiert bzw. ersetzt sich selbst und startet mit der neuen Version neu. Schlägt der automatische Download ausnahmsweise fehl, bleibt wie bisher die manuelle Auswahl zwischen portabler EXE und Setup-Installer verfügbar.",
            },
            new[]
            {
                "New: If ULM finds a newer program version, it's now automatically downloaded in the background. The banner then offers \"Install now & restart\" — ULM installs or replaces itself and restarts with the new version. If the automatic download fails in rare cases, the manual choice between the portable EXE and the setup installer remains available as before.",
            }),
            ("2.37.0",
            new[]
            {
                "Neu: Schlägt ein Download mangels gefundener Quelle fehl, erscheint der Button „🔧 Quelle manuell suchen“ jetzt sofort direkt im Download-Fortschritt-Fenster — nicht erst nach mehreren aufeinanderfolgenden automatischen Fehlschlägen in der Hauptliste. Nach dem Eintragen einer Quelle startet der Download für diesen Eintrag automatisch neu.",
                "Neu: Erscheint der „🔧“-Button in der Hauptliste neu, weil die automatische Auflösung wiederholt scheitert, gibt es jetzt einen kurzen Hinweis dazu — bei genau einem betroffenen Eintrag ein sich selbst schließendes Popup, bei mehreren gleichzeitig ein dezentes Banner statt mehrerer Popups.",
            },
            new[]
            {
                "New: If a download fails because no source was found, the \"🔧 Search source manually\" button now appears immediately right in the download progress window — not only after several consecutive automatic failures in the main list. After entering a source, the download for that entry restarts automatically.",
                "New: When the \"🔧\" button newly appears in the main list because automatic resolution keeps failing, there's now a brief notice about it — a self-closing popup for exactly one affected entry, or a subtle banner instead of multiple popups when several appear at once.",
            }),
            ("2.36.1",
            new[]
            {
                "Fehlerbehebung: Beim Start vom Ventoy-Stick erschien manchmal statt des Bootmenüs die Meldung „Failed to boot both default and fallback entries“ oder ein Absturz mit „alloc magic is broken“ — beides behoben.",
                "Fehlerbehebung: Im Bootmenü stand oben eine veraltete Versionsnummer und unten überlagerten sich mehrere Textzeilen; Titel, Versionsnummer und Stick-Auslastung (Speicherplatz, Anzahl ISOs) werden jetzt live und stets aktuell angezeigt.",
            },
            new[]
            {
                "Fix: When booting from the Ventoy stick, the message \"Failed to boot both default and fallback entries\" or a crash with \"alloc magic is broken\" sometimes appeared instead of the boot menu — both fixed.",
                "Fix: The boot menu showed an outdated version number at the top and several lines of text overlapped at the bottom; title, version number and stick usage (free space, number of ISOs) are now shown live and always up to date.",
            }),
            ("2.36.0",
            new[]
            {
                "Neu: Button „🔧 Quelle manuell suchen/eintragen“ pro Distro-Zeile — erscheint nur noch als Sicherheitsnetz für echte Härtefälle, bei denen die automatische Quellensuche wiederholt erfolglos bleibt. Öffnet ein Fenster mit den bekannten Bearbeiten-Feldern plus Suchfunktion: findet ULM selbst nichts, öffnet ein Klick auf „Suchen“ stattdessen direkt eine vorausgefüllte Browser-Suche.",
                "Fehlerbehebung: „URLs prüfen“ fand für Einträge ohne bekannte Quelle (z.B. per „ISO suchen“ hinzugefügt) nie automatisch eine Quelle und meldete sie immer als nicht erreichbar — nutzt jetzt dieselbe Selbstlern-Auflösung wie Updates-Prüfung und Download.",
                "Fehlerbehebung: Hiren's BootCD PE wurde bei jedem Check fälschlich als „Update verfügbar“ gemeldet, obwohl sich nichts geändert hatte; ULM liest die tatsächliche Version jetzt von der Hiren's-Downloadseite, statt eine feste Versionsnummer anzunehmen.",
            },
            new[]
            {
                "New: \"🔧 Search/enter source manually\" button per distro row — now only appears as a safety net for genuine edge cases where automatic source resolution keeps failing. Opens a window with the familiar edit fields plus a search function: if ULM itself finds nothing, clicking \"Search\" instead directly opens a pre-filled browser search.",
                "Fix: \"Check URLs\" never automatically found a source for entries without a known source (e.g. added via \"Search ISO\") and always reported them as unreachable — now uses the same self-learning resolution as the update check and download.",
                "Fix: Hiren's BootCD PE was incorrectly reported as \"Update available\" on every check even though nothing had changed; ULM now reads the actual version from the Hiren's download page instead of assuming a fixed version number.",
            }),
            ("2.35.1",
            new[]
            {
                "Fehlerbehebung: Findet ULM ein neueres Update (z.B. direkt nach dem Übernehmen einer vom Stick importierten ISO oder beim Gesundheitscheck), wird es jetzt sofort zum Aktualisieren angeboten — vorher erschien es zunächst nur als „Update verfügbar“ in der Liste, die eigentliche Frage kam erst beim nächsten Programmstart.",
                "Fehlerbehebung: Nach einer von ULM selbst durchgeführten Stick-Aktualisierung konnte zusätzlich zur Frage „Alte ISO löschen?“ fälschlich ein „ISO importieren?“-Dialog für genau diese alte Datei erscheinen.",
                "Fehlerbehebung: Der Gesundheitscheck-Dialog und ein anschließendes Update-Angebot konnten sich in seltenen Fällen überlagern — erscheinen jetzt nacheinander.",
                "Das Fenster \"ISO bearbeiten\" (Datenbank bearbeiten) passt seine Höhe jetzt automatisch an den Bildschirm an, damit alle Felder ohne Scrollen sichtbar sind.",
            },
            new[]
            {
                "Fix: When ULM finds a newer update (e.g. right after taking over an ISO imported from the stick, or during the health check), it's now offered for updating immediately — previously it only showed as \"Update available\" in the list first, and the actual prompt only appeared on the next program start.",
                "Fix: After a stick update performed by ULM itself, an \"Import ISO?\" dialog could incorrectly appear for that exact old file, in addition to the \"Delete old ISO?\" prompt.",
                "Fix: The health-check dialog and a subsequent update offer could, in rare cases, overlap on screen — they now appear one after another.",
                "The \"Edit ISO\" window (edit database) now automatically adjusts its height to the screen so all fields are visible without scrolling.",
            }),
            ("2.35.0",
            new[]
            {
                "Neu: ULM prüft beim Start im Hintergrund, ob eine neuere Programmversion verfügbar ist, und zeigt in dem Fall ein Hinweis-Banner an. Per Klick lässt sich direkt die portable EXE oder der Setup-Installer herunterladen; ULM legt die Datei ab und öffnet den Ordner — gestartet wird sie wie gewohnt selbst.",
                "Neu: Schlägt ULM selbst ein Stick-Update vor und wird es durchgeführt, fragt das Programm anschließend, ob die alte, ersetzte ISO auf dem Stick gelöscht oder behalten werden soll.",
                "Fehlerbehebung: Auf den Stick kopierte ISOs, die ULM noch nicht kennt, werden jetzt bereits beim Programmstart erkannt und zum Übernehmen angeboten — vorher erst, nachdem der Stick ab- und wieder eingesteckt wurde.",
                "Fehlerbehebung: Zwei Datenbank-Einträge mit identischem Dateinamen (z.B. wenn mehrere importierte Einträge beim Versionscheck auf dieselbe aktuelle ISO zusammenfielen) blieben doppelt bestehen — solche exakten Duplikate werden jetzt automatisch entfernt.",
            },
            new[]
            {
                "New: On startup, ULM checks in the background whether a newer program version is available and shows a notice banner if so. A click lets you download the portable EXE or the setup installer directly; ULM saves the file and opens the folder — you start it yourself as usual.",
                "New: When ULM itself suggests a stick update and it's carried out, the program then asks whether the old, replaced ISO on the stick should be deleted or kept.",
                "Fix: ISOs copied to the stick that ULM doesn't yet know about are now detected right at program startup and offered for import — previously only after the stick was unplugged and plugged back in.",
                "Fix: Two database entries with an identical filename (e.g. when several imported entries converged on the same current ISO during the version check) used to remain as duplicates — such exact duplicates are now removed automatically.",
            }),
            ("2.34.0",
            new[]
            {
                "Neu: Sind mehrere USB-Sticks gleichzeitig angeschlossen, fragt ULM jetzt aktiv nach, mit welchem gearbeitet werden soll — sowohl beim Programmstart als auch beim Einstecken eines weiteren Sticks während der Laufzeit. Vorher wurde stillschweigend der erste erkannte Stick gewählt.",
                "Die Hilfe (❔) wurde um die neue Mehrfach-Stick-Auswahl und den Status-Reiter ergänzt.",
            },
            new[]
            {
                "New: If several USB sticks are connected at the same time, ULM now actively asks which one to work with — both at program startup and when plugging in another stick while running. Previously, the first detected stick was silently chosen.",
                "The help (❔) has been extended to cover the new multi-stick selection and the status tab.",
            }),
            ("2.33.0",
            new[]
            {
                "Fehlerbehebung: „Abbrechen“ während einer laufenden Stick-Integritätsprüfung zeigte zwar sofort „Abbruch.“ im Protokoll, die Prüfung lief im Hintergrund aber unbeeinflusst bis zum Ende weiter (bei mehreren ISOs über USB teils mehrere Minuten) — wirkt jetzt sofort.",
                "Neu: Reiter „Status“ (nur im Experten-Modus) — zeigt den aktuell laufenden Vorgang mit Datei/Fortschritt/Zähler, automatische Hintergrund-Scans, die nächste geplante automatische Aktion sowie einen Verlauf der letzten Hintergrund-Ereignisse. Ziel: volle Transparenz ohne einen Blick in den Task-Manager.",
                "Neu: optionaler Windows-Installer (Setup.exe) als Alternative zur portablen EXE — mit Startmenü-Eintrag und Deinstaller; fragt beim Deinstallieren nach, bevor heruntergeladene ISOs/Einstellungen mitgelöscht werden.",
                "Laufwerks-Überwachung von 4 auf 8 Sekunden verlangsamt — Erkennung von Stick-Wechseln bleibt aktiv, pollt aber seltener.",
            },
            new[]
            {
                "Fix: \"Cancel\" during a running stick integrity check immediately showed \"Cancelled.\" in the log, but the check kept running unaffected in the background until it finished (sometimes several minutes for multiple ISOs over USB) — now takes effect immediately.",
                "New: \"Status\" tab (expert mode only) — shows the currently running operation with file/progress/counter, automatic background scans, the next scheduled automatic action, and a history of recent background events. Goal: full transparency without having to glance at Task Manager.",
                "New: optional Windows installer (Setup.exe) as an alternative to the portable EXE — with a Start menu entry and uninstaller; asks for confirmation on uninstall before also deleting downloaded ISOs/settings.",
                "Drive monitoring slowed from 4 to 8 seconds — detection of stick changes stays active but polls less often.",
            }),
            ("2.32.0",
            new[]
            {
                "Fehlerbehebung: ein durch Programmabsturz oder harten Kill mitten im Download unterbrochenes ISO konnte nach dem Neustart ungeprüft auf den Stick kopiert werden — die erwartete Zielgröße wird jetzt schon beim Download-Start gespeichert (nicht erst am Ende) und übersteht damit auch einen Absturz.",
                "Fehlerbehebung: der „(schneller)“-Mirror-Wechsel-Button erschien bisher bei jedem Download mit weiteren Mirror-Kandidaten, selbst bei bereits sehr guter Geschwindigkeit — erscheint jetzt erst nach Anlaufzeit und nur bei spürbar mittelmäßiger Übertragung.",
                "Fehlerbehebung: im kombinierten „Download → Stick-Kopie“-Modus zeigte die Gesamt-Fortschritts-Anzeige und die Abschluss-Meldung fälschlich vollen Erfolg, obwohl nur der Download geklappt hatte und die anschließende Stick-Kopie fehlgeschlagen war — beide zeigen jetzt das echte Kopier-Ergebnis.",
                "Neu: Hash-Status-Symbol in der Hauptliste — zeigt auf einen Blick, ob eine gespeicherte Prüfsumme vorhanden ist bzw. ob die letzte Integritätsprüfung eine Abweichung gefunden hat.",
                "Neu: Fortschrittsbalken färben sich abhängig vom Fortschritt (gedämpft am Anfang, grün kurz vor Fertigstellung).",
                "Neu: „🔁 Verpasste Kopien nachholen“ (vorher „Auf Stick kopieren“) — manuelles Sicherheitsnetz, falls die automatische Kopier-Nachfrage abgelehnt wurde oder eine Kopie fehlgeschlagen ist.",
                "Download-Fortschrittsfenster passt seine Höhe jetzt automatisch an die Bildschirmgröße an, damit bei mehreren parallelen Downloads mehr Zeilen ohne Bildlaufleiste sichtbar sind; die %-Anzeige wurde dabei bisher teils von der Bildlaufleiste verdeckt.",
                "Lange Tooltip-Texte wurden bisher als eine einzige, bildschirmbreite Zeile angezeigt — brechen jetzt lesbar um.",
            },
            new[]
            {
                "Fix: an ISO interrupted mid-download by a program crash or hard kill could be copied to the stick unchecked after restart — the expected target size is now saved right at the start of the download (not only at the end), so it survives a crash too.",
                "Fix: the \"(faster)\" mirror-switch button previously appeared for every download with further mirror candidates, even when speed was already very good — now only appears after a warm-up period and only for noticeably mediocre transfer speed.",
                "Fix: in the combined \"download → copy to stick\" mode, the overall progress display and the completion message incorrectly showed full success even though only the download had worked and the subsequent stick copy had failed — both now show the actual copy result.",
                "New: hash-status icon in the main list — shows at a glance whether a stored checksum exists, or whether the last integrity check found a mismatch.",
                "New: progress bars change color depending on progress (muted at the start, green shortly before completion).",
                "New: \"🔁 Catch up on missed copies\" (previously \"Copy to stick\") — a manual safety net in case the automatic copy prompt was declined or a copy failed.",
                "The download progress window now automatically adjusts its height to the screen size, so more rows are visible without a scrollbar during several parallel downloads; the % display used to be partly covered by the scrollbar.",
                "Long tooltip texts used to be shown as a single, screen-wide line — they now wrap readably.",
            }),
            ("2.31.1",
            new[]
            {
                "Fehlerbehebung: ULM meldete eine bereits aktuelle Stick-ISO fälschlich als veraltet, wenn eine alte Version nie gelöscht wurde — bietet jetzt stattdessen das Löschen der alten Datei an.",
                "Neu: SHA-256-Integritätsprüfung — nach Download/Import wird ein Referenzhash gespeichert, bei Ubuntu/Debian/Fedora zusätzlich gegen die offizielle Prüfsumme verifiziert. Manuelle Prüfung über den neuen Button 'Integrität prüfen'.",
            },
            new[]
            {
                "Fix: ULM incorrectly reported an already up-to-date stick ISO as outdated when an old version was never deleted — now offers to delete the old file instead.",
                "New: SHA-256 integrity check — after download/import, a reference hash is stored, and for Ubuntu/Debian/Fedora it's additionally verified against the official checksum. Manual check via the new \"Check integrity\" button.",
            }),
            ("2.31.0",
            new[]
            {
                "Neu: Autostart-Option — Checkbox im Einrichtungsfenster startet ULM ab sofort automatisch mit Windows, kein Admin-Recht nötig",
            },
            new[]
            {
                "New: Autostart option — a checkbox in the setup window now starts ULM automatically with Windows, no admin rights needed",
            }),
            ("2.30.0",
            new[]
            {
                "Neu: „🔍 ISO suchen“ zeigt jetzt zwei Online-Listen von DistroWatch — „🆕 Aktuellste“ (neu hinzugefügte Distros) und „🔥 Beliebteste“ (Popularitäts-Ranking), beide gefiltert auf garantiert per USB-Stick bootfähige Live-Medium-Distros, mit Kategorie-Vorschlag, Tooltip und optionalem Direkt-Download. Die frühere reine Textsuche entfällt (dafür: „🗃 Datenbank“)",
                "Neu: Mirror-Race — vor jedem Download werden alle konfigurierten Mirror-Quellen kurz parallel getestet und automatisch mit der schnellsten begonnen, statt einfach der ersten",
                "Neu: Geschwindigkeits-Wächter bricht dauerhaft extrem langsame Downloads automatisch ab und wechselt zur nächsten Quelle; bleibt nur eine langsame Quelle übrig, fragt ULM aktiv nach, ob trotzdem fortgefahren werden soll",
                "Neu: Freispeicher-Vorabprüfung — summiert vor Beginn eines Downloads die Größe ALLER markierten Distros und warnt, wenn der Speicherplatz im Arbeitsordner oder auf dem Stick nicht reicht, statt erst mittendrin zu scheitern",
                "DB-Gesundheitscheck-Fenster: Versions-/Status-Text saß ohne Abstand an der rechten Fensterkante — jetzt mit sichtbarem Rand",
                "Diverse Dialoge (DB-Gesundheitscheck, ISO-Editor, Stick-Import, Download-Fenster) im Dark Mode: mehrere Texte hatten keine explizite Vorder-/Hintergrundfarbe und blieben dadurch teils unlesbar hell",
            },
            new[]
            {
                "New: \"🔍 Search ISO\" now shows two online lists from DistroWatch — \"🆕 Latest\" (newly added distros) and \"🔥 Most Popular\" (popularity ranking), both filtered to live-medium distros guaranteed to be bootable from a USB stick, with category suggestion, tooltip and optional direct download. The previous plain text search has been removed (use \"🗃 Database\" instead)",
                "New: Mirror race — before every download, all configured mirror sources are briefly tested in parallel and the fastest one is used automatically, instead of simply the first",
                "New: Speed guard automatically cancels persistently extremely slow downloads and switches to the next source; if only a slow source remains, ULM actively asks whether to continue anyway",
                "New: Free-space pre-check — before starting a download, adds up the size of ALL checked distros and warns if there isn't enough space in the working folder or on the stick, instead of failing partway through",
                "DB health-check window: version/status text sat flush against the right window edge with no spacing — now has a visible margin",
                "Various dialogs (DB health check, ISO editor, stick import, download window) in dark mode: several texts had no explicit foreground/background color and stayed partly unreadable light",
            }),
            ("2.29.1",
            new[]
            {
                "Einrichtungsfenster passte sich bisher nicht an kleine Bildschirme an — auf 800x600 ragte es über den Bildschirm hinaus und der 'Übernehmen'-Button war unsichtbar. Größe richtet sich jetzt nach dem tatsächlichen Bildschirm-Arbeitsbereich, Kopf- und Fußzeile bleiben immer sichtbar.",
            },
            new[]
            {
                "The setup window previously didn't adapt to small screens — at 800x600 it extended beyond the screen and the 'Apply' button was invisible. Its size now follows the actual screen work area, and the header and footer always stay visible.",
            }),
            ("2.29.0",
            new[]
            {
                "Neu: Dark Mode — Design-Wahl System/Hell/Dunkel im Setup-Dialog oder jederzeit über den Knopf oben rechts im Hauptfenster, schaltet sofort um (kein Neustart nötig)",
                "\"System\" übernimmt automatisch die Windows-Design-Einstellung und folgt ihr auch live, wenn sie sich während der Laufzeit ändert",
                "Alle Listen, Dialoge und Eingabefelder wurden für gute Lesbarkeit im Dark Mode durchgestylt und geprüft",
            },
            new[]
            {
                "New: Dark mode — choose System/Light/Dark in the setup dialog, or any time via the button in the top right of the main window; switches instantly (no restart needed)",
                "\"System\" automatically follows the Windows theme setting, and keeps following it live if it changes while the app is running",
                "All lists, dialogs and input fields were styled and checked for good readability in dark mode",
            }),
            ("2.28.1",
            new[]
            {
                "Fenstertitel zeigte fälschlich immer eine fest hinterlegte, veraltete Versionsnummer statt der tatsächlich installierten — jetzt dynamisch aus der Programmversion gelesen",
                "Neues Programm-Icon (passend zum Logo der Projektseite) für EXE, Taskleiste und Fenster-Titelleiste",
            },
            new[]
            {
                "The window title used to always incorrectly show a hardcoded, outdated version number instead of the one actually installed — now read dynamically from the program version",
                "New program icon (matching the project page logo) for the EXE, taskbar and window title bar",
            }),
            ("2.28.0",
            new[]
            {
                "Neuer Automatismus löst für JEDE unbekannte/importierte Distro automatisch die Download-Quelle auf (DistroWatch- und SourceForge-Suche als zusätzliche Stufen) — nicht mehr nur für fest hinterlegte Distros",
                "Neu gefundene Download-Quellen für importierte ISOs werden jetzt zuverlässig dauerhaft gespeichert, statt bei jedem Neustart wieder zu verschwinden",
                "Erreichbarkeits-Checks werden kurz zwischengespeichert und der automatische Scan pausiert leicht zwischen Einträgen — verringert fälschliche 'nicht erreichbar'-Meldungen durch Bot-/Anti-Scraping-Schutz externer Server",
                "Download-Fortschritt zeigt jetzt die geschätzte Restzeit (ETA) an",
                "Freispeicher-Check vor jedem Download und Kopiervorgang — bricht rechtzeitig mit klarer Meldung ab statt mittendrin zu scheitern",
                "Log-Datei (ulm_log.txt) wird ab 5 MB automatisch rotiert, wächst also nicht mehr unbegrenzt",
                "Optionales GitHub-Token (Experten-Modus) hebt das API-Anfragelimit für GitHub-basierte Erkennung und den ULM-Update-Check deutlich an",
                "ULM prüft jetzt selbst im Hintergrund auf neue Versionen und meldet sie im Protokoll",
                "Neuer „Was ist neu?“-Dialog zeigt nach einem Update automatisch die Änderungen seit der zuletzt genutzten Version",
                "Ersteinrichtungs-Dialog: 'Nicht mehr anzeigen' übersprang bisher nur den Begrüßungstext, jetzt tatsächlich den kompletten Dialog beim nächsten Start; Farben an die App angeglichen",
            },
            new[]
            {
                "New automatic mechanism resolves the download source for EVERY unknown/imported distro automatically (DistroWatch and SourceForge search as additional steps) — no longer only for hardcoded distros",
                "Newly found download sources for imported ISOs are now reliably saved permanently, instead of disappearing again on every restart",
                "Reachability checks are briefly cached and the automatic scan pauses slightly between entries — reduces false \"unreachable\" reports caused by bot/anti-scraping protection on external servers",
                "Download progress now shows the estimated remaining time (ETA)",
                "Free-space check before every download and copy operation — stops in time with a clear message instead of failing partway through",
                "The log file (ulm_log.txt) is now automatically rotated at 5 MB, so it no longer grows indefinitely",
                "An optional GitHub token (expert mode) significantly raises the API rate limit for GitHub-based detection and the ULM update check",
                "ULM now checks for new versions itself in the background and reports them in the log",
                "New \"What's new?\" dialog automatically shows the changes since the last version used, after an update",
                "Initial setup dialog: 'Don't show again' previously only skipped the welcome text — it now actually skips the entire dialog on the next start; colors aligned with the app",
            }),
            ("2.27.1",
            new[]
            {
                "Ventoy-Installation läuft jetzt tatsächlich still im Hintergrund (vorher fielen ungültige Kommandozeilenparameter lautlos auf die interaktive Ventoy-Oberfläche zurück)",
                "Doppelte Installationsfenster/Abfragen bei der Ventoy-Einrichtung behoben",
                "Ventoy-ZIP-Download schlug durch eine fälschlich angewendete 300-MB-Mindestgrößenprüfung immer fehl",
                "Automatischer Gesundheitscheck läuft jetzt gezielt nur noch bei neuen, unverifizierten Einträgen statt bei jedem Stick-Scan",
            },
            new[]
            {
                "Ventoy installation now actually runs silently in the background (previously, invalid command-line parameters silently fell back to the interactive Ventoy UI)",
                "Fixed duplicate installation windows/prompts during Ventoy setup",
                "Ventoy ZIP download always failed due to an incorrectly applied 300 MB minimum-size check",
                "Automatic health check now runs specifically only for new, unverified entries instead of on every stick scan",
            }),
        };

        public ChangelogDialog(string previousVersion, string currentVersion)
        {
            Title = LocalizationService.T(Str.Changelog_Title);
            Width = 560; MinHeight = 260; MaxHeight = 560;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = (Brush)Application.Current.Resources["BrushBg"];

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var root   = new StackPanel { Margin = new Thickness(22) };

            root.Children.Add(new TextBlock
            {
                Text = string.Format(LocalizationService.T(Str.Changelog_UpdatedHeader), previousVersion, currentVersion),
                FontSize = 15, FontWeight = FontWeights.Bold,
                Foreground = (Brush)Application.Current.Resources["BrushHeader"],
                Margin = new Thickness(0, 0, 0, 16),
            });

            // Nur Versionen NEUER als 'previousVersion' anzeigen, nicht die gesamte Historie —
            // wer von einer älteren Version kommt, sieht so alle übersprungenen Änderungen auf
            // einen Blick, statt nur die allerletzte.
            var relevant = History.Where(h => IsNewer(h.Version, previousVersion)).ToList();
            if (relevant.Count == 0) relevant = History.Take(1).ToList();

            bool english = LocalizationService.Current == AppLanguage.English;
            foreach (var (version, notesDe, notesEn) in relevant)
            {
                root.Children.Add(new TextBlock
                {
                    Text = $"Version {version}", FontWeight = FontWeights.SemiBold, FontSize = 12.5,
                    Foreground = (Brush)Application.Current.Resources["BrushBlue"],
                    Margin = new Thickness(0, 8, 0, 6),
                });
                foreach (string note in english ? notesEn : notesDe)
                    root.Children.Add(new TextBlock
                    {
                        Text = "•  " + note, TextWrapping = TextWrapping.Wrap, FontSize = 11.5,
                        Foreground = (Brush)Application.Current.Resources["BrushMid"],
                        Margin = new Thickness(4, 0, 0, 5), LineHeight = 17,
                    });
            }

            var btn = new Button
            {
                Content = LocalizationService.T(Str.Changelog_Btn_Understood), Width = 130, HorizontalAlignment = HorizontalAlignment.Right,
                Style = (Style)Application.Current.Resources["BtnPrimary"], Margin = new Thickness(0, 18, 0, 0),
            };
            btn.Click += (_, _) => Close();
            root.Children.Add(btn);

            scroll.Content = root;
            Content = scroll;
            KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Escape) Close(); };
        }

        // Simpler numerischer Teil-für-Teil-Vergleich reicht hier — Changelog-Versionsnummern sind
        // immer reine "Major.Minor.Patch"-Strings ohne Suffixe.
        private static bool IsNewer(string a, string b)
        {
            int[] pa = a.Split('.').Select(p => int.TryParse(p, out int n) ? n : 0).ToArray();
            int[] pb = b.Split('.').Select(p => int.TryParse(p, out int n) ? n : 0).ToArray();
            for (int i = 0; i < System.Math.Max(pa.Length, pb.Length); i++)
            {
                int x = i < pa.Length ? pa[i] : 0, y = i < pb.Length ? pb[i] : 0;
                if (x != y) return x > y;
            }
            return false;
        }
    }
}
