#!/usr/bin/env bash
# Usage: packaging/build-appimage.sh NEW-PUBLISH-DIR [2.4.2-stable.3]
# Needs .NET 8 SDK, C/C++ toolchain, pkg-config and libusb development files.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERSION="${2:-2.4.2-stable.3}"
PUBLISH_DIR="$(realpath -m "${1:-$ROOT/publish-out}")"
"$ROOT/packaging/publish.sh" "$PUBLISH_DIR"
cp -L "$(pkg-config --variable=libdir libusb-1.0)/libusb-1.0.so.0" "$PUBLISH_DIR/libusb-1.0.so.0"
cp "$ROOT/native/usb-update/LICENSE.libusb" "$PUBLISH_DIR/licenses/LICENSE.libusb"
printf 'libusb %s; corresponding source: https://github.com/libusb/libusb/releases/tag/v%s\n' \
  "$(pkg-config --modversion libusb-1.0)" "$(pkg-config --modversion libusb-1.0)" > "$PUBLISH_DIR/licenses/libusb-source.txt"
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT
APPDIR="$STAGE/AppDir"
mkdir -p "$APPDIR/usr/bin"
cp -a "$PUBLISH_DIR/." "$APPDIR/usr/bin/"
install -m755 "$ROOT/packaging/AppRun" "$APPDIR/AppRun"
install -m644 "$ROOT/packaging/kirin-tool-appimage.desktop" "$APPDIR/kirin-tool.desktop"
install -m644 "$ROOT/packaging/kirin-tool.png" "$APPDIR/kirin-tool.png"
TOOL="$(realpath -m "${APPIMAGETOOL:-$ROOT/appimagetool-x86_64.AppImage}")"
if [[ ! -f "$TOOL" ]]; then
  curl -fsSLo "$TOOL" https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage
fi
chmod +x "$TOOL"
OUT="${KIRIN_APPIMAGE_OUT:-$ROOT/kirin-tool-linux-$VERSION-x86_64.AppImage}"
RUNTIME_ARGS=()
if [[ -n "${APPIMAGE_RUNTIME:-}" ]]; then RUNTIME_ARGS=(--runtime-file "$APPIMAGE_RUNTIME"); fi
ARCH=x86_64 "$TOOL" --appimage-extract-and-run "${RUNTIME_ARGS[@]}" "$APPDIR" "$OUT"
chmod +x "$OUT"
echo "AppImage: $OUT"
