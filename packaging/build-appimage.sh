#!/usr/bin/env bash
# Build a Kirin Tool for Linux AppImage.
#
# Usage: packaging/build-appimage.sh [publish-dir] [version]
#   publish-dir  a dotnet publish output tree (default: publish-out). If it
#                does not exist, the script publishes it first. A bundled
#                fastboot binary (fastboot-src) is compiled in automatically.
#   version      AppImage version string (default: 2.4.2)
#
# Output: ./kirin-tool-linux-<version>-x86_64.AppImage
#
# Requirements: dotnet-sdk 8, gcc/g++/make (bundled fastboot), curl or wget,
# and either FUSE or nothing at all — appimagetool runs via
# --appimage-extract-and-run, so no root or FUSE setup is needed.

set -euo pipefail

VERSION="${2:-2.4.2}"
PUBLISH_DIR="${1:-publish-out}"
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO_ROOT"

if [ ! -x "$PUBLISH_DIR/Kirin-Tool" ]; then
    echo "==> Publishing self-contained app to $PUBLISH_DIR"
    export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
    dotnet publish Kirin-Tool.csproj \
        -c Release -r linux-x64 --self-contained true \
        -p:PublishTrimmed=false -p:DebugType=none -p:DebugSymbols=false \
        -o "$PUBLISH_DIR"
fi

if [ ! -x "$PUBLISH_DIR/fastboot/fastboot" ]; then
    echo "==> Building bundled Huawei-capable fastboot"
    make -C fastboot-src
    mkdir -p "$PUBLISH_DIR/fastboot"
    cp fastboot-src/fastboot "$PUBLISH_DIR/fastboot/fastboot"
fi

echo "==> Assembling AppDir"
APPDIR="$(mktemp -d)/AppDir"
mkdir -p "$APPDIR/usr/bin"
cp -a "$PUBLISH_DIR/." "$APPDIR/usr/bin/"
install -m 755 packaging/AppRun "$APPDIR/AppRun"
install -m 644 packaging/kirin-tool-appimage.desktop "$APPDIR/kirin-tool.desktop"
install -m 644 packaging/kirin-tool.png "$APPDIR/kirin-tool.png"

echo "==> Fetching appimagetool"
TOOL="${APPIMAGETOOL:-appimagetool-x86_64.AppImage}"
if [ ! -f "$TOOL" ]; then
    if command -v curl >/dev/null 2>&1; then
        curl -fsSLo "$TOOL" https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage
    else
        wget -qO "$TOOL" https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage
    fi
fi
chmod +x "$TOOL"

echo "==> Building AppImage"
OUT="kirin-tool-linux-$VERSION-x86_64.AppImage"
"$REPO_ROOT/$TOOL" --appimage-extract-and-run "$APPDIR" "$REPO_ROOT/$OUT"
chmod +x "$OUT"

echo "==> Done: $OUT"
