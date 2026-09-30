# Bundled fastboot — provenance and safety patches

Base: AOSP platform/system/core `android-6.0.1_r1` fastboot and libsparse.
Original Apache/BSD notices remain in source. Linux-only build; format/raw image
filesystem generators and update ZIP support are trimmed out.

The September 2026 safety changes replace the earlier guessed DATA upload handler
with independently written `fb_dump_partition`. Static analysis of the frozen
Windows release shows a getvar emmc/storage address-and-length query followed by
vendor upload_emmc/upload_storage requests, OKAY, exact raw bytes, and final OKAY.
Requests are capped at 16 MiB, read buffers at 1 MiB. A local filename is never
sent as part of the vendor command. Malformed/zero/overflowing ranges are rejected.

Writes, flush, fsync and close must succeed before an atomic rename publishes the
backup. Failures preserve the previous destination and remove partial output.
Success prints `KIRIN_DUMP_OK bytes=N`; only metadata-query failures print the
fallback marker. Unsupported memory/memupload operations fail explicitly.
Both normal and sparse downloads now require the exact negotiated DATA size.
OEM command construction rejects oversized commands.

Version: `kirin-tool-linux-2.0 vendor-storage-upload-v1`. Managed backup code
requires this capability and checks both exit status and receipt/file length.
The application can still use system fastboot for ordinary commands, but cannot
certify backups made by an old or unrelated client.

UltraFlash is not implemented. Windows can enable it inside ordinary flash;
see [comparison](../docs/fastboot-windows-linux-diff.md). No claim of identical
hardware behavior is made. Read protocol evidence comes from binary inspection;
real-device traces and readback remain outstanding.

Build and hardware-free native regression tests:

```sh
make -C fastboot-src
make -C fastboot-src test
```

The test target links fake USB functions, never usb_linux.c. It exercises both
vendor sequences, the 16-MiB boundary, short reads, malformed metadata, query and
transfer failures, disk-write failure, previous-file preservation and rejected
DATA negotiation. Rebuild/package this fastboot with the application; installing
only a new managed executable does not upgrade an existing bundled fastboot.
