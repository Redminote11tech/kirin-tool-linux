# Bundled fastboot — provenance and patches

This directory contains the fastboot client that ships with Kirin Tool for Linux
(`fastboot/fastboot` next to the app binary). The app prefers this bundled
binary and falls back to the system `fastboot` (android-tools) when absent —
see `Services/FastbootClient.cs`.

## Provenance

Upstream: AOSP `platform/system/core` @ tag `android-6.0.1_r1`
(fastboot + libsparse), Apache-2.0 / BSD-style Google notices preserved in the
file headers. This is the same code lineage as the Huawei-modified
`fastboot/fastboot.exe` shipped inside Kirin Tool for Windows (same
`fb_command`/`protocol.c` core, AdbWinApi-era build).

## Trimmed (not buildable here, not used by the tool)

- `format` / `flash:raw` fs generators: `fs.c`, ext4_utils, f2fs (system/extras)
  — `fb_format_supported` now always returns 0, `fb_perform_format` prints an
  error. The Kirin Tool never formats partitions.
- `update <zip>` command and libziparchive dependency (update.zip flow).
- All host-platform code except Linux (`usb_linux.c`, `util_linux.c`).

## Added (Kirin Tool Linux port)

1. `fb_command_upload()` in `src/protocol.c` and the `oem_upload_filename()`
   hook in `do_oem_command()` in `src/fastboot.cpp`:
   for the Huawei OEM commands `oem dump-emmc`, `oem dump-storage`,
   `oem memory` and `oem memupload`, the bootloader uploads raw data in
   standard fastboot `DATA<size>` frames; this client captures the stream and
   writes it to the trailing filename argument — the client-side half that the
   vendor fastboot client performs on Windows and that stock fastboot lacks.
   This is what makes partition dumps and OEMInfo backups work on Linux.
2. `FASTBOOT_REVISION` reports `kirin-tool-linux-1.0 (AOSP android-6.0.1_r1)`.

3. `do_oem_command()` hardening (2026-09-05 code review):
   - the dump filename is taken from the parsed `argv` token, not the
     space-joined command, so save paths containing spaces work;
   - the OEM command join buffer is bounds-checked (upstream `strcat` into a
     fixed 256-byte buffer overflowed on long `oem` commands);
   - a failed upload prints `FAILED (<error>)` and exits with code 1 —
     previously a failed dump exited 0, which could fool the app's
     output-based success heuristic into reporting a fake success.

## Verification status

The upload framing follows the standard fastboot protocol (device sends
`DATA<8-hex-size>`, raw bytes, then `OKAY`/`FAIL`). As of 2026-09-05 this has
NOT been verified against real Huawei hardware; if a dump fails or produces an
empty file, capture the Windows tool doing the same dump (USBPcap/Wireshark)
and adjust `fb_command_upload` to the observed framing. Non-dump commands are
stock fastboot behavior and unaffected.

Hardware-affecting surface of the patch (the only parts that touch the wire):
1. the upload framing itself (`fb_command_upload`),
2. the full OEM command — including the local filename token — is sent to the
   bootloader, matching the Windows client (its embedded deprecation string
   says the filename "will not be used" by the device; unverified on hardware).
Everything else in the binary is stock android-6.0.1 fastboot behavior.

## Build

    make          # produces ./fastboot
    make clean
