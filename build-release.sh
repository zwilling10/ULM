#!/usr/bin/env bash
# build-release.sh — baut Universal Linux Manager als eine einzige, komplett
# portable .exe (self-contained, single-file) und legt sie in release/ ab,
# fertig zum Hochladen als GitHub-Release-Asset.
#
# Auf dem Zielsystem wird NICHTS zusätzlich benötigt (keine .NET-Installation,
# kein Installer) — die .NET-Runtime steckt bereits in der .exe.
#
# Nutzung:
#   ./build-release.sh            # normaler Release-Build
#   ./build-release.sh --zip      # zusätzlich als .zip verpacken

set -euo pipefail
cd "$(dirname "$0")"

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

if [ "${1:-}" = "--zip" ]; then
    ZIP_NAME="UniversalLinuxManager-v${VERSION}-win-x64.zip"
    (cd "$OUT_DIR" && rm -f "$ZIP_NAME" && zip -q "$ZIP_NAME" "$RELEASE_EXE_NAME")
    echo "📦 Zusätzlich gepackt: ${OUT_DIR}/${ZIP_NAME}"
fi
