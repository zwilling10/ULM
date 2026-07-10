# Universal Linux Manager (ULM)

Portabler Windows-Manager für Ventoy-Multiboot-USB-Sticks mit Linux-Live-ISOs. Lädt aktuelle Versionen automatisch von den offiziellen Quellen, kopiert sie auf den Stick und hält den ganzen Katalog dauerhaft aktuell — auch ohne hinterlegte URLs.

🔗 **[Projektseite & Download](https://zwilling10.github.io/ULM/)** · **[Neueste Version](../../releases/latest)**

## Funktionen

- **Automatische ISO-Downloads** — über 20 dedizierte Erkennungsroutinen lösen für jede unterstützte Distro immer die aktuellste Download-URL direkt beim Anbieter auf, keine hartkodierten Links
- **Ventoy-Integration** — kopiert Downloads direkt auf den Stick, aktualisiert das Bootmenü automatisch
- **Gesundheitscheck & Duplikat-Schutz** — läuft automatisch nach jedem Download oder Scan
- **Datenmüll-Schutz** — Online-Größenprüfung erkennt unvollständige Downloads zuverlässig
- **Selbstlernende Erkennung** — auch manuell hinzugefügte oder importierte Distros werden automatisch einer passenden Quelle zugeordnet und dauerhaft gemerkt
- **Komplett portabel** — eine einzige .exe, self-contained, kein Installer, keine .NET-Installation auf dem Zielsystem nötig

## Download

Fertige, portable `.exe` unter [Releases](../../releases/latest) — einfach herunterladen und starten. Keine Installation, keine Administratorrechte nötig (außer für die optionale Ventoy-Installation/-Aktualisierung).

**Anforderungen:** Windows 10 / 11 (x64)

## Aus dem Quellcode bauen

Voraussetzung: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```bash
git clone https://github.com/zwilling10/ULM.git
cd ULM
./build-release.sh          # baut eine portable Single-File-EXE nach release/
./build-release.sh --zip    # zusätzlich als .zip verpackt
```

Oder direkt mit `dotnet publish`:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Architektur

MVVM (C# / WPF, .NET 8):

- `Core/Models` — Domänenmodell (`IsoEntry`, `UsbDrive`, Konstanten)
- `Core/Services` — `HttpService` (URL-Auflösung/Downloads), `UsbService` (Laufwerks-/Ventoy-Verwaltung), `IsoDatabaseService` (INI-Persistenz)
- `Core/Workers` — Hintergrund-Worker für Downloads, Scans, Versionschecks
- `ViewModels` / `Views` — MVVM-Bindung, Dialoge

## Lizenz

[MIT](LICENSE) © 2025 ULM Project
