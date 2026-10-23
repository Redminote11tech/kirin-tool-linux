#!/usr/bin/env bash
# One fresh app/native publish path for Arch and AppImage. No USB operations.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$(realpath -m "${1:-$ROOT/publish-out}")"
if [[ -e "$OUT" ]]; then echo "Publish destination must be new: $OUT" >&2; exit 1; fi
cd "$ROOT"
COMMIT="${KIRIN_BUILD_COMMIT:-}"
if [[ -z "$COMMIT" && -f BUILD-COMMIT ]]; then COMMIT="$(cat BUILD-COMMIT)"; fi
if [[ -z "$COMMIT" ]]; then COMMIT="$(git rev-parse HEAD)"; fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
# Avoid makepkg host-specific CPU flags; both formats use the same native inputs.
export CFLAGS="${KIRIN_NATIVE_CFLAGS:--O2 -Wall -march=x86-64 -mtune=generic}" LDFLAGS="${KIRIN_NATIVE_LDFLAGS:-}"
dotnet publish Kirin-Tool.csproj -c Release -r linux-x64 --self-contained true \
  -p:PublishTrimmed=false -p:DebugType=none -p:DebugSymbols=false -p:NuGetAudit=false \
  -p:SourceRevisionId="$COMMIT" -o "$OUT"
"$ROOT/fastboot-modern/build.sh" "$OUT"
for probe in UsbUpdateProbe VcomOpenProbe; do
  TEMP="$(mktemp -d)"
  dotnet publish "tests/$probe/$probe.csproj" -c Release -r linux-x64 --self-contained false \
    -p:NuGetAudit=false -p:SourceRevisionId="$COMMIT" -o "$TEMP"
  for suffix in '' .dll .deps.json .runtimeconfig.json; do cp "$TEMP/$probe$suffix" "$OUT/"; done
  rm -rf "$TEMP"
done
mkdir -p "$OUT/licenses" "$OUT/docs"
cp LICENSE "$OUT/licenses/LICENSE"
cp native/usb-update/LICENSE "$OUT/licenses/LICENSE.usb-bridge"
cp fastboot-src/NOTICE "$OUT/licenses/NOTICE.fastboot-src"
cp docs/{stable-release,testing-checklist,usb-update-userspace,partition-extractor,native-windows-port,windows-ui-parity,portable-appimage,windows-fastboot-audit-v2.4.3,native-fastboot-parity-v2.4.5,mate40-bootloader-analysis}.md "$OUT/docs/"
cp packaging/51-kirin-tool-fastboot.rules "$OUT/docs/"
printf '%s\n' "$COMMIT" > "$OUT/BUILD-COMMIT"
