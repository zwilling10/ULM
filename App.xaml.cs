// App.xaml.cs
using System;
using System.IO;
using System.Windows;
using ULM.Infrastructure;
using ULM.Core.Services;
using ULM.Views;
using ULM.Views.Dialogs;

namespace ULM
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Theme so früh wie möglich anwenden — noch bevor irgendein Fenster (Setup-Dialog,
            // VentoyInstallWindow oder MainWindow) konstruiert wird, damit von Anfang an die
            // richtige Farbpalette aktiv ist und kein kurzes Aufblitzen im falschen Theme auftritt.
            ThemeService.Initialize();

            // ─────────────────────────────────────────────────────────────
            // Elevation-on-demand: Ventoy Admin-Modus
            //
            // Wenn ULM mit --ventoy-install gestartet wurde, ist dies die
            // Admin-Instanz (gestartet von der normalen Instanz via runas).
            // Es wird NUR das VentoyInstallWindow gezeigt — kein normaler
            // Startup, kein MainWindow, keine Datenbankinitialisierung.
            //
            // Ablauf:
            //   1. Normale ULM-Instanz läuft (ohne Admin)
            //   2. Benutzer klickt "Ventoy installieren"
            //   3. Normale Instanz startet sich selbst mit --ventoy-install E: false true
            //      + Verb="runas" → Windows zeigt UAC einmalig
            //   4. DIESE Admin-Instanz startet hier, zeigt VentoyInstallWindow
            //   5. Ventoy2Disk.exe läuft still (erbt Admin-Rechte)
            //   6. Nach Abschluss: Shutdown(0) = Erfolg, Shutdown(1) = Fehler
            //   7. Normale Instanz liest ExitCode, aktualisiert Anzeige
            // ─────────────────────────────────────────────────────────────
            int ventoyIdx = Array.IndexOf(e.Args, "--ventoy-install");
            if (ventoyIdx >= 0)
            {
                string letter     = ventoyIdx + 1 < e.Args.Length
                    ? e.Args[ventoyIdx + 1] : "C:";
                bool   updateMode = ventoyIdx + 2 < e.Args.Length
                    && e.Args[ventoyIdx + 2] == "true";
                bool   secureBoot = ventoyIdx + 3 < e.Args.Length
                    && e.Args[ventoyIdx + 3] == "true";

                // Kein normaler Startup — nur das Installationsfenster zeigen
                var win = new VentoyInstallWindow(letter, updateMode, secureBoot);
                MainWindow = win;
                win.Show();
                return;
            }

            // ─────────────────────────────────────────────────────────────
            // Normaler Startup (ohne Admin)
            // ─────────────────────────────────────────────────────────────

            // Globale Exception-Handler.
            // SICHERHEIT: zeigt volle Exception-Details (inkl. Stacktrace, lokale Dateipfade) im
            // Klartext an — akzeptabel, da ULM eine rein lokale Single-User-Anwendung ohne Server-
            // Komponente ist (niemand außer dem Nutzer selbst sieht diese Meldung). Falls Fehler-
            // meldungen künftig irgendwo automatisch übermittelt werden (Telemetrie, Crash-Reports),
            // muss ex.ToString() vorher bereinigt werden, um keine lokalen Pfade preiszugeben.
            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show(
                    $"Unbehandelter Fehler (UI-Thread):\n\n{args.Exception}",
                    "ULM — Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                MessageBox.Show(
                    $"Unbehandelter Fehler (Hintergrund-Thread):\n\n{args.ExceptionObject}",
                    "ULM — Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            };
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                MessageBox.Show(
                    $"Unbehandelter Fehler (Task):\n\n{args.Exception}",
                    "ULM — Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                args.SetObserved();
            };

            AppPaths paths = AppPaths.Instance;

            // STEP 1: Erststart prüfen
            bool isFirstRun = true;
            if (File.Exists(paths.SettingsIni))
            {
                string savedBase = IniService.Read(
                    paths.SettingsIni, "App", "BaseDirectory", string.Empty);
                if (!string.IsNullOrWhiteSpace(savedBase) && Directory.Exists(savedBase))
                {
                    paths.Apply(savedBase);
                    isFirstRun = false;
                }
            }

            // STEP 2+3: Einrichtungsfenster — fasst Arbeitsordner-Wahl (nur beim allerersten
            // Start), Willkommenstext (nur beim allerersten Start) und Modus-Wahl in EINEM
            // Fenster zusammen.
            //
            // BUGFIX: "SkipWelcome" schaltete bisher NUR den Willkommenstext ab — das Fenster
            // selbst (samt Modus-Auswahl) erschien trotzdem bei JEDEM Start erneut, egal ob die
            // Checkbox gesetzt war. Das widersprach der Erwartung "Häkchen gesetzt → nächstes Mal
            // sofort starten". "SkipSetupDialog" steuert jetzt das GESAMTE Fenster: ist es gesetzt,
            // wird gar kein Dialog mehr konstruiert — der zuletzt gespeicherte Modus (ExpertMode)
            // wird direkt übernommen. Ist die App noch nie eingerichtet worden (isFirstRun) oder hat
            // der Nutzer die Checkbox nie gesetzt, erscheint das Fenster weiterhin wie gewohnt.
            bool skipSetupDialog = !isFirstRun && IniService.Read(paths.SettingsIni, "App", "SkipSetupDialog", "0") == "1";
            bool lastExpert      = IniService.Read(paths.SettingsIni, "App", "ExpertMode", "0") == "1";

            if (!skipSetupDialog)
            {
                var setupDlg = new SetupDialog(showDirectory: isFirstRun, showWelcome: isFirstRun, currentExpertMode: lastExpert, currentThemeMode: ThemeService.CurrentMode)
                { WindowStartupLocation = WindowStartupLocation.CenterScreen };
                if (setupDlg.ShowDialog() != true) { Shutdown(); return; }

                if (isFirstRun)
                {
                    paths.Apply(setupDlg.ChosenDirectory);
                    IniService.Write(paths.SettingsIni, "App", "BaseDirectory", setupDlg.ChosenDirectory);
                }
                if (setupDlg.DontShowAgain)
                    IniService.Write(paths.SettingsIni, "App", "SkipSetupDialog", "1");
                lastExpert = setupDlg.ExpertModeChosen;
                // Erst NACH Dialog-Schluss anwenden (nicht live während der Auswahl im Dialog
                // selbst) — MainWindow wird direkt danach mit der korrekten Palette konstruiert.
                ThemeService.SetMode(setupDlg.ChosenThemeMode);
            }

            // STEP 4: Hauptfenster
            try
            {
                var mainWindow = new MainWindow();
                MainWindow = mainWindow;
                mainWindow.SetInitialMode(lastExpert);
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Hauptfenster konnte nicht erstellt werden:\n\n{ex}",
                    "ULM — Startfehler", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }
    }
}
