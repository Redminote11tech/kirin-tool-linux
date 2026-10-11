#!/usr/bin/env bash
# Pinned native AOSP engine plus independently reconstructed Huawei extensions.
# Build/help tests only; this script never opens a device.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
OUT="$(realpath -m "${1:?Usage: build.sh OUTPUT-DIRECTORY}")"
WORK="${KIRIN_FASTBOOT_BUILD_DIR:-$HERE/.build}"
ARCHIVE="${KIRIN_FASTBOOT_SOURCE_ARCHIVE:-$WORK/android-tools-35.0.2.tar.xz}"
SHA=d2c3222280315f36d8bfa5c02d7632b47e365bfe2e77e99a3564fb6576f04097
mkdir -p "$WORK" "$(dirname "$ARCHIVE")"
if [[ ! -f "$ARCHIVE" ]]; then
    curl -fL --retry 3 -o "$ARCHIVE" https://github.com/nmeum/android-tools/releases/download/35.0.2/android-tools-35.0.2.tar.xz
fi
printf '%s  %s\n' "$SHA" "$ARCHIVE" | sha256sum -c -
SOURCE="$WORK/source/android-tools-35.0.2"
PATCH_SHA="$(cd "$HERE" && sha256sum prepare.py huawei_vendor.cpp atomic_output.h | sha256sum | cut -d' ' -f1)"
if [[ ! -f "$SOURCE/CMakeLists.txt" || ! -f "$SOURCE/.kirin-patch-sha" || "$(cat "$SOURCE/.kirin-patch-sha")" != "$PATCH_SHA" ]]; then
    mkdir -p "$WORK/source"
    # Restore pristine files before applying a changed patch version.
    tar --no-same-owner -xJf "$ARCHIVE" -C "$WORK/source"
fi
python3 "$HERE/prepare.py" "$SOURCE"
printf '%s\n' "$PATCH_SHA" > "$SOURCE/.kirin-patch-sha"
cmake -S "$SOURCE" -B "$WORK/build" -G Ninja \
    -DANDROID_TOOLS_PATCH_VENDOR=OFF -DANDROID_TOOLS_USE_BUNDLED_FMT=ON \
    -DCMAKE_BUILD_TYPE=Release -DCMAKE_C_FLAGS='-O2 -march=x86-64 -mtune=generic' \
    -DCMAKE_CXX_FLAGS='-O2 -march=x86-64 -mtune=generic' \
    -DCMAKE_EXE_LINKER_FLAGS='-static-libstdc++ -static-libgcc' \
    -DKIRIN_PARITY_TEST_SOURCE= \
    ${KIRIN_FASTBOOT_CMAKE_ARGS:-}
cmake --build "$WORK/build" --target fastboot mke2fs.android make_f2fs -j "${KIRIN_BUILD_JOBS:-4}"
mkdir -p "$OUT/fastboot" "$OUT/licenses" "$OUT/sources/fastboot-modern"
for name in fastboot mke2fs.android make_f2fs; do
    install -m755 "$WORK/build/vendor/$name" "$OUT/fastboot/$name"
done
install -m644 "$SOURCE/vendor/extras/ext4_utils/mke2fs.conf" "$OUT/fastboot/mke2fs.conf"
# Ship corresponding source, exact modifications and reproducible instructions
# with the GPL filesystem helpers, instead of relying on a future download.
cp "$ARCHIVE" "$OUT/sources/android-tools-35.0.2.tar.xz"
cp "$HERE/"{build.sh,test.sh,prepare.py,huawei_vendor.cpp,atomic_output.h,README.md,tests.cmake,huawei_vendor_test.cpp,cli_mock_transport.cpp,cli_tests.py} "$OUT/sources/fastboot-modern/"
python3 - "$SOURCE" "$OUT/licenses/NOTICE.fastboot-modern" <<'PY'
import pathlib, sys
root=pathlib.Path(sys.argv[1]);output=pathlib.Path(sys.argv[2])
with output.open('w') as stream:
    # Retain the complete vendored notice catalog: mixed AOSP/dependency licenses.
    for path in sorted(root.rglob('*')):
        if path.is_file() and path.name.upper().startswith(('LICENSE','NOTICE','COPYING')):
            stream.write('\n===== '+str(path.relative_to(root))+' =====\n')
            stream.write(path.read_text(errors='replace')+'\n')
PY
for notice in /usr/share/doc/gcc-*-base/copyright /usr/share/licenses/gcc*/COPYING.RUNTIME /usr/share/licenses/gcc*/RUNTIME.LIBRARY.EXCEPTION; do
    if [[ -f "$notice" ]]; then
        key="$(printf '%s' "$notice" | tr '/' '_')"
        cp "$notice" "$OUT/licenses/NOTICE.$key"
    fi
done
printf 'Patch SHA256: %s\n' "$PATCH_SHA" > "$OUT/fastboot/PATCH-PROVENANCE"
printf 'android-tools 35.0.2\nArchive SHA256: %s\nHuawei extensions: shipped in sources/fastboot-modern\n' "$SHA" > "$OUT/fastboot/SOURCE-PROVENANCE"
"$OUT/fastboot/fastboot" --version
