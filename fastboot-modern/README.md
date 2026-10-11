# Native Huawei fastboot engine

The default packaged engine uses the pinned AOSP sources in android-tools 35.0.2
and independently written Huawei extensions reconstructed from the supplied
Windows clients. No Wine, proprietary decompiler exports or firmware are shipped.
The original fastboot-src directory is a historical compatibility reference;
new releases build this engine through packaging/publish.sh.

Source archive:
https://github.com/nmeum/android-tools/releases/download/35.0.2/android-tools-35.0.2.tar.xz
SHA256: `d2c3222280315f36d8bfa5c02d7632b47e365bfe2e77e99a3564fb6576f04097`.
The archive contains AOSP components and an independent CMake build system; it is
not a proprietary Windows binary or a new import of the upstream Kirin GUI.

Build prerequisites: GCC 10 or newer with C++20, CMake, Ninja, Python 3, curl,
Google Test headers (also required by production libziparchive), pkg-config, zlib, protobuf, brotli, lz4, pcre2 and zstd development packages.

```sh
./fastboot-modern/build.sh /tmp/new-kirin-native-bundle
```

For an offline build, set KIRIN_FASTBOOT_SOURCE_ARCHIVE to the shipped original
archive. KIRIN_FASTBOOT_BUILD_DIR selects a separate cache/build directory. Output
includes fastboot, mke2fs.android, make_f2fs, mke2fs.conf, notices, original source
and the complete modifications/build instructions. The executable's C++/GCC
runtime support is linked statically; this is not a fully static libc build.
Portable AppImages use the Ubuntu 20.04 / glibc 2.31 baseline with GCC 10 and
baseline x86-64 flags. Native distro builds follow the distro's glibc version.

prepare.py applies exact, idempotent patches to the SHA256-pinned source. It
adds Huawei memory/storage upload and UltraFlash, old authenticated flashing
commands, explicit-query failure status, and atomic staged/fetch output with
range-length validation. Raw vendor stdout output keeps diagnostics on stderr.
HUAWEI_ULTRAFLASH=0 or off opts out. Only a device FAIL permits fallback; I/O or
unexpected DATA aborts. Device refusal reasons remain visible.

After building, ./fastboot-modern/test.sh runs the hardware-free test targets.
Google Test and Mock development packages are required. Tests can also be enabled by passing absolute paths for
KIRIN_PARITY_TEST_SOURCE=huawei_vendor_test.cpp and
KIRIN_PARITY_CLI_MOCK=cli_mock_transport.cpp to CMake after preparation.
Build/run kirin_driver_tests and kirin_workflow_tests, then run cli_tests.py with
kirin_cli_tests. The CLI/workflow test targets exclude the native USB backend;
the substitute cannot open a USB device. Test targets never ship in the app.
Some upstream workflow tests require Android product fixtures absent from the
source archive and are explicitly skipped; see the parity report for counts.

See ../docs/native-fastboot-parity-v2.4.5.md for command coverage and limitations.
Compiled command coverage is not a guarantee of success on a locked phone.
