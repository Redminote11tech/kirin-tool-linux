# Candidate 6 revision 3: native Windows client features

This unstable build ports selected HiSilicon fastboot features to the bundled
Linux client. It does not run the Windows executable or require Wine. Stable and
beta are unchanged. No real phone has validated these additions.

## Binary evidence and wire behavior

Reference: frozen Windows XML `fastboot.exe`, SHA256
`df7d0492c3f356ef057c669da896749b156bf7ec5ed36d4001c9559794807084`.
Targeted Ghidra decompilation and objdump disassembly are retained in
`audit/native-windows-port/` in the parent workspace. No newer upstream source
was imported. The source license and frozen Windows provenance remain applicable.

- Named memory: `fastboot oem memory NAME OUTPUT` queries `getvar:memory:NAME`.
  The reply supplies a 16-digit address, separator and 16-digit length.
- Memory range: `fastboot oem memupload ADDRESS LENGTH OUTPUT` uses hexadecimal
  arguments and `upload_memory:%016llx:%016llx`.
- UFS-eye: `fastboot oem dump-ufseye INDEX SIZE LANE OUTPUT` uses hexadecimal
  arguments. INDEX must be nonzero, SIZE is 1 through 0x1000000, LANE is 0 or 1.
  The address field is INDEX | (LANE << 8), matching the Windows handler.
  It sends `upload_ufseye:%016llx:%016llx`.
- Uploads consume OKAY, exactly the requested raw bytes, then OKAY. Requests are
  at most 16 MiB, read buffers at most 1 MiB. Output names are host-only. Files
  are atomically replaced only after success; errors remove partial files and
  preserve existing outputs. Overflow and malformed arguments fail locally.
- Normal flashing queues `ultraflash:PARTITION` before existing download/flash
  commands and `ultraflash:` afterward, including all sparse-image chunks.
  A device FAIL during the capability handshake disables UltraFlash for the
  remaining invocation and permits standard flashing. Transport errors and
  malformed replies abort; they do not replay writes. End-command failures are
  reported, even though the Windows end helper ignored its return value.
  `HUAWEI_ULTRAFLASH=0` or `off` disables negotiation, matching the Windows setting.

The existing storage/emmc extraction and other standard command paths remain.
This is not a complete clone of modern HiSilicon fastboot: modern AOSP CLI
features, Windows driver code, 64-bit streaming redesign and the special upload
filename `stdout` are not ported. New diagnostic commands are native CLI handlers,
not extra device-operation buttons. These are not Kirin 990/9000 loaders or proof
of support for those chipsets.

## Native UI

The 1000 x 700 window and 320-pixel sidebar retain their original dimensions.
Navigation has independently drawn vector outline icons, stable page identifiers
and accessible labels. The header, card layout, control corners and font weights
are closer to the Windows screenshot. Inter is bundled by Avalonia.Fonts.Inter;
Segoe UI and proprietary Windows icon/font files are not redistributed.
Extractor progress still uses green success, red failure/cancellation and neutral
unattempted rows. No flashing or VCOM handler changes were needed for navigation.

## Verification

`make -C fastboot-src test` checks vendor upload framing, chunk boundaries,
cleanup and CLI dispatch. The 23 additional fake-USB CLI tests cover UltraFlash
ordering, fallback, transport errors, end failures, multi-partition behavior and
all three new upload handlers. They never open USB hardware. Headless rendered
UI checks cover all nine navigation icons, page routing and extraction results.
The actual extracted Arch package is compared with the fresh publish and native
binaries and then runs the UI harnesses.
