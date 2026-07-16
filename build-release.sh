#!/usr/bin/env bash
# build-release.sh — baut Universal Linux Manager als eine einzige, komplett
# portable .exe (self-contained, single-file) und legt sie in release/ ab,
# fertig zum Hochladen als GitHub-Release-Asset.
#
# Auf dem Zielsystem wird NICHTS zusätzlich benötigt (keine .NET-Installation,
# kein Installer) — die .NET-Runtime steckt bereits in der .exe.
#
# Nutzung:
#   ./build-release.sh                        # normaler Release-Build (nur portable EXE)
#   ./build-release.sh --zip                   # zusätzlich als .zip verpacken
#   ./build-release.sh --installer             # zusätzlich klassischen Setup.exe bauen (installer/ULM.iss)
#   ./build-release.sh --zip --installer        # beides zusammen, Reihenfolge egal

set -euo pipefail
cd "$(dirname "$0")"

DO_ZIP=0
DO_INSTALLER=0
for arg in "$@"; do
    case "$arg" in
        --zip)       DO_ZIP=1 ;;
        --installer) DO_INSTALLER=1 ;;
        *) echo "❌ Unbekannte Option: $arg" >&2; exit 1 ;;
    esac
done

PROJECT="UniversalLinuxManager.csproj"
CONFIG="Release"
RID="win-x64"
OUT_DIR="release"
PUBLISH_DIR="bin/${CONFIG}/net8.0-windows/${RID}/publish"

VERSION=$(grep -oP '(?<=<Version>)[^<]+' "$PROJECT" || echo "0.0.0")
EXE_NAME="UniversalLinuxManager.exe"
RELEASE_EXE_NAME="UniversalLinuxManager-v${VERSION}-win-x64.exe"

echo "▶ Baue portable EXE (Version ${VERSION}) …"
dotnet publish "$PROJECT" \
    -c "$CONFIG" \
    -r "$RID" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true \
    -p:PublishReadyToRun=true

if [ ! -f "${PUBLISH_DIR}/${EXE_NAME}" ]; then
    echo "❌ Fehler: ${PUBLISH_DIR}/${EXE_NAME} wurde nicht erzeugt." >&2
    exit 1
fi

mkdir -p "$OUT_DIR"
cp "${PUBLISH_DIR}/${EXE_NAME}" "${OUT_DIR}/${RELEASE_EXE_NAME}"

SIZE=$(du -h "${OUT_DIR}/${RELEASE_EXE_NAME}" | cut -f1)
echo "✅ Fertig: ${OUT_DIR}/${RELEASE_EXE_NAME}  (${SIZE})"
echo "   Läuft ohne Installation auf jedem Windows 10/11 x64 — einfach kopieren und starten."

if [ "$DO_ZIP" = "1" ]; then
    ZIP_NAME="UniversalLinuxManager-v${VERSION}-win-x64.zip"
    # BUGFIX: 'zip' ist auf dem GitHub-Actions windows-latest-Runner nicht installiert — das ließ
    # den Release-Workflow bisher IMMER an dieser Stelle scheitern (exit 127), obwohl die EXE
    # selbst längst fertig gebaut war, und noch BEVOR "gh release create" lief. Kein Release
    # wurde dadurch je automatisch veröffentlicht. PowerShells Compress-Archive ist auf jedem
    # Windows-System (10/11, jeder CI-Runner) garantiert vorhanden — kein externes Tool nötig.
    (cd "$OUT_DIR" && rm -f "$ZIP_NAME" && powershell -NoProfile -Command "Compress-Archive -Path '$RELEASE_EXE_NAME' -DestinationPath '$ZIP_NAME' -Force")
    echo "📦 Zusätzlich gepackt: ${OUT_DIR}/${ZIP_NAME}"
fi

if [ "$DO_INSTALLER" = "1" ]; then
    # ISCC.exe (Inno Setup Command-Line Compiler) steht auf dem GitHub-Actions windows-latest-
    # Runner bereits vorinstalliert unter dem Standardpfad zur Verfügung — lokal ggf. über
    # https://jrsoftware.org/isdl.php installieren. Erst PATH prüfen, dann den Standard-
    # Installationsort, damit sowohl CI als auch eine lokale Standardinstallation funktionieren.
    ISCC=""
    if command -v iscc >/dev/null 2>&1; then
        ISCC="iscc"
    elif [ -f "/c/Program Files (x86)/Inno Setup 6/ISCC.exe" ]; then
        ISCC="/c/Program Files (x86)/Inno Setup 6/ISCC.exe"
    elif [ -f "/c/Program Files/Inno Setup 6/ISCC.exe" ]; then
        ISCC="/c/Program Files/Inno Setup 6/ISCC.exe"
    fi

    if [ -z "$ISCC" ]; then
        echo "❌ Fehler: --installer angefordert, aber ISCC.exe (Inno Setup) wurde nicht gefunden." >&2
        echo "   Installieren via https://jrsoftware.org/isdl.php oder ohne --installer bauen." >&2
        exit 1
    fi

    echo "▶ Baue Setup.exe (Version ${VERSION}) …"
    # BUGFIX: Git-Bash (auch der "shell: bash"-Runner in GitHub Actions unter windows-latest)
    # wandelt Argumente, die wie "/D..." aussehen, automatisch in einen Windows-Pfad um (MSYS-
    # Pfadkonvertierung) — ISCC.exe bekäme dann statt "/DAppVersion=2.32.0" einen kaputten
    # Pfad übergeben und meldet "You may not specify more than one script filename." dafür wird
    # hier für genau diesen Aufruf abgeschaltet.
    MSYS_NO_PATHCONV=1 "$ISCC" "/DAppVersion=${VERSION}" "installer/ULM.iss"

    SETUP_EXE="${OUT_DIR}/UniversalLinuxManager-Setup-v${VERSION}-win-x64.exe"
    if [ ! -f "$SETUP_EXE" ]; then
        echo "❌ Fehler: ${SETUP_EXE} wurde nicht erzeugt." >&2
        exit 1
    fi
    SETUP_SIZE=$(du -h "$SETUP_EXE" | cut -f1)
    echo "✅ Fertig: ${SETUP_EXE}  (${SETUP_SIZE})"
fi
