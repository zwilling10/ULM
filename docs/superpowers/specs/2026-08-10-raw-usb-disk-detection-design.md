# Design: Erkennung nicht gemounteter USB-Sticks (Rufus-ISO/DD-Modus)

**Datum:** 2026-08-10
**Status:** Entwurf, abschnittsweise vom Nutzer freigegeben
**Scope:** Nur Windows-WPF-Hauptapp (`Core/Services/UsbService.cs`, `MainViewModel.cs`,
`MainWindow.xaml.cs`). Linux-GUI (nutzt `lsblk`, ein völlig anderer Mechanismus) ist nicht betroffen.

## Ausgangslage / Bug

`UsbService.ListRemovableDrives()` fragt ausschließlich `Win32_LogicalDisk` ab — das listet nur
Datenträger, denen Windows bereits einen **Laufwerksbuchstaben** zugewiesen hat. Ein mit Rufus im
ISO/DD-Image-Modus beschriebener Stick (üblich für viele Linux-Live-ISOs) bekommt von Windows
teils gar keinen Buchstaben zugewiesen — er erscheint nur in der Datenträgerverwaltung als
Rohdatenträger. Solche Sticks tauchen dadurch nie in `_vm.Drives` auf; die
"Neuer USB-Stick erkannt — als Ventoy einrichten?"-Erkennung feuert nie.

(Ein separater, bereits behobener Bug betraf Sticks, die zwar einen Buchstaben bekommen, aber von
Windows als `DriveType 5`/CD-ROM statt `DriveType 2`/Wechseldatenträger eingestuft werden — siehe
Commit `8a21af8`. Das hier ist ein tieferliegender, zusätzlicher Fall: **gar kein** Buchstabe.)

## Ziel

Auch Sticks ohne Windows-Laufwerksbuchstaben zuverlässig erkennen und in den bestehenden
Ventoy-Einrichtungs-Dialog überführen — für den Nutzer optisch identisch zum bisherigen Ablauf bei
einem normalen, bereits formatierten Stick.

## Nicht-Ziele

- Keine Änderung an der eigentlichen Ventoy-Installation selbst (`VentoyInstallWorker`,
  `Ventoy2Disk.exe`/VTOYCLI-Aufruf) — die bekommt weiterhin nur einen Laufwerksbuchstaben, wie
  bisher auch. Ventoys eigene CLI akzeptiert ausschließlich `/Drive:X:`, keine Datenträger-Nummer.
- Keine rohen Win32-P/Invoke-APIs (SetupAPI/DeviceIoControl, wie Rufus sie intern nutzt) — WMI
  liefert dasselbe Ergebnis (`Win32_DiskDrive`, `InterfaceType`) über den bereits im Projekt
  etablierten PowerShell/WMI-Weg, mit deutlich weniger riskantem Low-Level-Code für eine
  destruktive Aktion.
- Kein separater Warnhinweis vor dem bestehenden Ventoy-Dialog — der Ablauf soll sich für den
  Nutzer nicht vom bisherigen unterscheiden.

## Architektur — Erkennung

Neue Methode in `Core/Services/UsbService.cs`, parallel zu `ListRemovableDrives()`:

```powershell
Get-CimInstance Win32_DiskDrive | Where-Object { $_.InterfaceType -eq 'USB' }
```

Das listet **physische** Datenträger unabhängig von zugewiesenen Laufwerksbuchstaben — die
WMI-Entsprechung dessen, was Rufus über `SetupDiGetClassDevs`/`IOCTL_STORAGE_QUERY_PROPERTY` auf
niedrigerer Ebene macht. Für jeden gefundenen USB-Datenträger wird per WMI-Assoziationsabfrage
geprüft, ob **irgendeine** seiner Partitionen bereits einen Laufwerksbuchstaben besitzt:

```
ASSOCIATORS OF {Win32_DiskDrive.DeviceID='...'} WHERE AssocClass=Win32_DiskDriveToDiskPartition
→ für jede Partition:
ASSOCIATORS OF {Win32_DiskPartition...} WHERE AssocClass=Win32_LogicalDiskToPartition
```

Liefert das für **keine** Partition ein Ergebnis (oder hat der Datenträger gar keine Partitionen),
gilt er als Kandidat. Ergebnis pro Kandidat: Datenträger-Index (`Win32_DiskDrive.Index`, für den
`diskpart select disk`-Befehl) + Größe in Bytes (kein Label/Dateisystem — gibt es bei einem rohen
Datenträger noch nicht).

## Sicherheitsschichten

Destruktive Aktion (der Datenträger wird komplett neu partitioniert) — zwei Schichten, nicht nur
eine:

1. **USB-Filter** (`InterfaceType = 'USB'`) schließt interne Datenträger (SATA/NVMe) von
   vornherein aus.
2. **Systemdatenträger-Sperre**: separat wird ermittelt, welcher Datenträger-Index die
   Windows-Systempartition (`%SystemDrive%`, i.d.R. `C:`) enthält (via
   `Win32_LogicalDisk` → `Win32_LogicalDiskToPartition` → `Win32_DiskDriveToDiskPartition` →
   `Win32_DiskDrive.Index`). Jeder Kandidat mit diesem Index wird verworfen. Diese Prüfung läuft
   **zweimal**: einmal beim Auflisten (verhindert, dass so ein Datenträger überhaupt als Kandidat
   angeboten wird) und **erneut unmittelbar vor** dem `diskpart`-Aufruf (verhindert, dass sich
   zwischen Anzeige und Ausführung etwas an den Datenträger-Indizes geändert hat, z.B. durch ein
   zwischenzeitlich weiteres eingestecktes Laufwerk).
3. Der bestehende Größenfilter (≥2GB) bleibt zusätzlich bestehen.

## Vorbereitung + Einbindung in den bestehenden Ablauf

Neue Methode `UsbService.PrepareRawUsbDisk(int diskIndex, char letter)`, strukturell analog zum
bestehenden `DoFormat(string letter)` (temporäres `diskpart`-Skript, `/s`-Aufruf):

```
select disk {diskIndex}
clean
create partition primary
format fs=fat32 quick label=ULMPREP
assign letter={letter}
exit
```

`fat32` statt `exfat`, weil dieser Formatierungsschritt nur dazu dient, Windows zur
Buchstaben-Zuweisung zu bewegen — Ventoy2Disk formatiert den Stick beim eigentlichen Einrichten
ohnehin komplett neu. Nach erfolgreicher Vorbereitung wird der neu zugewiesene Buchstabe einfach in
den **bestehenden, unveränderten** Ablauf eingespeist: `MainViewModel.RefreshDrives()` erneut
aufrufen (der Stick erscheint jetzt ganz normal in `_vm.Drives`), danach greift
`OnNewDriveInserted()` in `Views/MainWindow.xaml.cs` wie gewohnt — derselbe
"Neuer USB-Stick erkannt — als Ventoy einrichten?"-Dialog, dieselbe `VentoyInstallWindow`/
`VentoyInstallWorker`-Kette, komplett unverändert.

Die Erkennung roher Datenträger läuft im bestehenden 8-Sekunden-`_driveTimer` mit
(`CheckDriveChanges()` in `Views/MainWindow.xaml.cs`) — kein neuer Timer. Ein neues
`RawUsbDiskDetected`-Event auf `MainViewModel` (analog zum bestehenden Signatur-Vergleichsmuster
für `Drives`) feuert, wenn ein Datenträger-Index als neuer roher Kandidat auftaucht, der beim
letzten Poll noch nicht da war.

## Fehlerbehandlung

- Schlägt `PrepareRawUsbDisk` fehl (z.B. schreibgeschützter/defekter Datenträger), wird das
  geloggt (gleiches Muster wie bestehende `UsbService`-Methoden — kein Absturz), kein Dialog
  erscheint, der Datenträger bleibt beim nächsten Poll weiterhin als roher Kandidat sichtbar (kein
  dauerhaft blockierter Zustand).
- Kein freier Laufwerksbuchstabe verfügbar: gleiche Behandlung — loggen, kein Dialog, kein
  Absturz.

## Testing

`Win32_DiskDrive`/`diskpart`-Aufrufe shellen zur Laufzeit gegen echtes Windows und sind ohne echte
USB-Hardware nicht sinnvoll automatisiert testbar — genau wie die bereits bestehende, ungetestete
`ListRemovableDrives()`. Wo möglich, werden reine Logik-Teile (z.B. die
"welcher Datenträger-Index ist neu seit dem letzten Poll"-Diff-Funktion, die
Systemdatenträger-Ausschluss-Prüfung gegen eine übergebene Liste) als separate, unit-testbare
Methoden mit injizierbaren Eingaben herausgezogen — analog zum bestehenden
`internal`/`InternalsVisibleTo`-Testmuster im Projekt. Die eigentlichen WMI-/`diskpart`-Aufrufe
selbst bleiben ungetestet (Nutzer verifiziert manuell mit echter Hardware, wie beim vorherigen
`DriveType`-Fix).

## Offene Punkte

- Muss vom Nutzer auf echter Hardware mit dem tatsächlich betroffenen Rufus-Stick verifiziert
  werden — keine Möglichkeit, USB-Hardware/WMI in der Entwicklungsumgebung zu simulieren.
