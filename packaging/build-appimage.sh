#!/usr/bin/env bash
# Portable AppImage: run in Ubuntu 20.04 (glibc 2.31) or an older supported build image.
# Usage: packaging/build-appimage.sh NEW-PUBLISH-DIR [VERSION]
# See docs/portable-appimage.md. Never copy optimized host libraries into the image.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERSION="${2:-2.4.2-stable.3-portable.1}"
PUBLISH_DIR="$(realpath -m "${1:-$ROOT/publish-out}")"
HOST_GLIBC="$(getconf GNU_LIBC_VERSION | awk '{print $2}')"
if [[ "$(printf '%s\n' "$HOST_GLIBC" 2.31 | sort -V | tail -n1)" != 2.31 ]]; then
    echo "Portable AppImage must be built with glibc <= 2.31; host is $HOST_GLIBC. See docs/portable-appimage.md." >&2
    exit 1
fi
export KIRIN_NATIVE_CFLAGS='-O2 -Wall -march=x86-64 -mtune=generic'
export KIRIN_NATIVE_LDFLAGS=''
make -C "$ROOT/fastboot-src" clean
make -C "$ROOT/native/usb-update" clean
"$ROOT/packaging/publish.sh" "$PUBLISH_DIR"
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT
LIBUSB_SOURCE="${KIRIN_LIBUSB_SOURCE:-$STAGE/libusb-1.0.30.tar.bz2}"
if [[ ! -f "$LIBUSB_SOURCE" ]]; then
    curl -fsSLo "$LIBUSB_SOURCE" https://github.com/libusb/libusb/releases/download/v1.0.30/libusb-1.0.30.tar.bz2
fi
printf '%s  %s\n' fea36f34f9156400209595e300840767ab1a385ede1dc7ee893015aea9c6dbaf "$LIBUSB_SOURCE" | sha256sum -c -
tar --no-same-owner -xf "$LIBUSB_SOURCE" -C "$STAGE"
# Netlink backend avoids carrying libudev/systemd ABI requirements into the bundle.
(cd "$STAGE/libusb-1.0.30" && CFLAGS="$KIRIN_NATIVE_CFLAGS" LDFLAGS='' ./configure --disable-static --disable-udev && make -j2)
cp -L "$STAGE/libusb-1.0.30/libusb/.libs/libusb-1.0.so.0" "$PUBLISH_DIR/libusb-1.0.so.0"
cp "$ROOT/native/usb-update/LICENSE.libusb" "$PUBLISH_DIR/licenses/LICENSE.libusb"
printf 'libusb 1.0.30; baseline x86-64; netlink backend; corresponding source: https://github.com/libusb/libusb/releases/tag/v1.0.30\n' > "$PUBLISH_DIR/licenses/libusb-source.txt"
cp "$ROOT/docs/portable-appimage.md" "$PUBLISH_DIR/docs/"
python3 "$ROOT/packaging/check-portable-elf.py" "$PUBLISH_DIR"
printf 'Build glibc: %s\nCPU: baseline x86-64\nlibusb: 1.0.30, netlink, no libudev dependency\nSDK: %s\nCompiler: %s\n' \
 "$HOST_GLIBC" "$(dotnet --version)" "$(gcc -dumpfullversion)" > "$PUBLISH_DIR/PORTABLE-BUILD"
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
