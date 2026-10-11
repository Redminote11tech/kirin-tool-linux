# Windows fastboot whole-binary audit — 2.4.3

Static inspection of both frozen Windows clients, October 11, 2026. Neither EXE
was executed; Wine was not used. No device operations were performed. This is a
whole-program inventory and feature audit, not a claim of perfect recovered source
or tested compatibility with every phone. Raw proprietary binaries, decompiler
exports, firmware and local device logs are not redistributed with this report.

## Scope and evidence

| Client | SHA256 | Initial function export |
| --- | --- | --- |
| Legacy fastboot.exe | `64c7a960a550c48003eb438964c1924b2ba68765087e3f71e0237a06545124f1` | 563 COFF symbol rows; 560 function entries; all 560 decompiled |
| XML HiSilicon 3.3.5 fastboot.exe | `df7d0492c3f356ef057c669da896749b156bf7ec5ed36d4001c9559794807084` | 4,600 COFF symbol rows; 4,573 function entries; 4,538 initially decompiled, 35 retried |

COFF aliases explain why symbol rows and function counts differ. One additional
fmt-library seed could not be exported as a separate C function; boundary repair
attempts did not produce usable decompiler output. Its assembly remains in the full
disassembly. Exports include PE sections and
imports, complete code disassembly, ASCII strings, every discovered function and
bounded Ghidra decompilation. Main's first five-second decompile timed out; a longer
retry recovered its command dispatcher. The large modern EXE contains debug symbols
and runtime libraries as well as fastboot code; its size is not evidence of a secret
Huawei unlock implementation.

After retries, 4,570 of 4,573 exported modern functions decompiled (32 of 35
retries succeeded). Remaining failures are narrow `std::stoi`, wide money parsing
and `DES_ede3_cbc_encrypt`, plus the separate fmt seed described above. All main
dispatch and identified vendor handler entries have C exports. These counts describe
static coverage, not correctness or tested behavior. Decompiled function boundaries and inferred
C++ types can be imperfect, especially across no-return error paths. Assembly and
COFF entries were used to distinguish adjacent OEM and wipe-super routines.

## Feature comparison

| Recovered behavior | Current Linux behavior |
| --- | --- |
| Normal getvar, OEM forwarding, device selection, download/flash, erase, boot, update, formatting | Existing native CLI paths; older AOSP base |
| `oem memory NAME FILE`, `oem memupload ADDRESS LENGTH FILE` | Native vendor upload handlers already ported |
| `oem dump-emmc PARTITION FILE`, `oem dump-storage PARTITION FILE` | Native partition geometry query and range upload already ported |
| UFS-eye upload routine | Native handler already ported; Windows routine and help exist, but modern OEM dispatch examined here does not establish reachability of that routine |
| UltraFlash begin/end around flashing; environment opt-out | Already ported, with documented fallback and transport-error differences |
| Named upload destination `stdout`, large transfer streaming | Linux writes local files; special stdout and full modern streaming redesign are not ported |
| A/B slot selection and secondary image management | Modern Windows slot workflow is not ported |
| fastbootd reboot/GSI, logical partition create/delete/resize, snapshot merge/cancel | Modern commands/workflows are not ported |
| `wipe-super`, host liblp metadata/image generation | Not ported; different from the app's existing positional SUPER merger |
| `fetch`, stage/get_staged, newer boot headers/DTB options and vbmeta flag handling | Modern implementation is not ported; ordinary vendor partition dump uses a different protocol |
| Windows Android USB driver and platform I/O | Linux uses its native USB backend; Windows driver code is not applicable |
| `oem get-psid` | Generic device OEM command; new explicit read action in 2.4.3 |

The missing modern AOSP features are a catalogue for future work, not new capabilities
silently enabled in this release. Their usefulness depends on device-side support.

## Vendor paths and important findings

- Main dispatcher: XML `0x00405f40`. OEM dispatcher: `0x0040a810`. Named memory:
  `0x00405280`; range memory: `0x00405570`; eMMC: `0x00405670`; storage geometry:
  `0x00405990` / `0x00405b40`; storage upload: `0x00405bf0` / `0x00405d70`;
  UFS-eye routine: `0x00405da0`; upload/save helper: `0x00404ef0`.
- Storage geometry helper parses hexadecimal address/length. It is an internal
  helper, not an extra storage-information button or unlock command.
- Existing vendor range uploads use `upload_KIND:ADDRESS:LENGTH`, with fixed-width
  hexadecimal operands. The frozen host protocol does not supply a Kirin 9000
  rescue partition map or a factory fastboot loader.
- `oem allow-flash-super` appears at `0x00559e7d` as a **wire command inside
  do_wipe_super (`0x0040ab40`)**, used when the metadata's super block-device name
  requires it. Windows then generates a flashable image with liblp. It is not a
  separate host OEM dispatcher command. Generic Linux OEM forwarding can send
  that wire command; it does not implement the Windows host image-generation path.
- No literal `get-psid` occurs in either host's ASCII strings. The host forwards
  OEM strings; firmware implements the command and decides whether to accept it.
  No host-only secret handshake to override lock policy was identified.

## Serial-number semantics and Kirin 9000

The command is `fastboot oem get-psid`. A [firsthand device capture](https://github.com/kayger44/MEDIATAB-T5-FRP-BYPASS-VERSION.8.0.0)
shows separate SN, IMEI, IMEI1 and MEID fields on an AGS2-L09. This establishes that
some Huawei firmware returns SN, not that the command works on NOH-AN00 or NAM-AL00.
2.4.3 accepts explicit identifiers only after a successful query and rejects
conflicting fields; NVE SN reads remain separate. It never substitutes USB serial
or IMEI for SN and does not auto-query or write identifiers.

Local NOH-AN00 base firmware has combined ERECOVERY (45 MiB), ERECOVERY_RAMDISK
(32 MiB) and ERECOVERY_VENDOR (24 MiB), without ERECOVERY_KERNEL. ERECOVERY contains
an Android boot image. Existing Kirin 980's split-kernel plan therefore does not
match this archive. Kirin 9000's `ERECOVERY -> rescue_recovery` alias is explicitly
experimental: inferred from the combined layout, not proven by this host audit.
Actual local flash failures remain evidence of rejection, not of successful support.
A host port cannot resolve a device's partition-length lookup or locked-write policy.
