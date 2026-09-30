# Stable bundled fastboot — provenance and patches

Base: AOSP platform/system/core android-6.0.1_r1 fastboot/libsparse. Original
license notices remain in the source and NOTICE. Format generators, ZIP update
support and non-Linux host backends are trimmed from this build.

The stable read-path parity repair replaces the old guessed DATA upload framing.
Frozen Windows binary inspection shows getvar:emmc:<partition> / getvar:storage:<partition>
return an address and length, then upload_emmc/upload_storage requests consume
OKAY, exact raw bytes, and final OKAY. Requests are at most 16 MiB, read buffers
at most 1 MiB. The local filename is not sent to the device. Native disk errors
must be reported as failures; temporary output is renamed only after success.
The host receipt and query-failure marker do not change the wire protocol.

The managed stable caller retains its existing fallback behavior and does not
require beta's capability marker/receipt checks. Version is
kirin-tool-linux-2.0-stable vendor-storage-upload-v1. Ordinary native DATA download
handling retains its pre-audit implementation; exact-size enforcement is beta-only.

Unsupported memory/memupload commands fail explicitly instead of guessing an
upload protocol. They have no app UI implementation. OEM commands exceeding the
64-byte wire-command limit are refused instead of silently truncated.

UltraFlash is absent. Windows may invoke it during ordinary flash. Static client
analysis and fake USB tests do not prove device compatibility. See
[stable-release.md](../docs/stable-release.md).

Build: `make -C fastboot-src`; hardware-free tests: `make -C fastboot-src test`.
The test target links fake USB functions and never opens a device. Tests cover
both vendor sequences, a 16-MiB boundary, short reads, malformed metadata,
query/start/tail errors, early EOF, disk failure and prior-file preservation.
