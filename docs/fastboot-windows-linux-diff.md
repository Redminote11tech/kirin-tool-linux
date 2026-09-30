# Windows / Linux flashing comparison — corrected 2026-09-30

This supersedes the earlier parity report. Its statements that vendor uploads use
standard DATA framing, that UltraFlash is never used, and that flashing works
identically were incorrect. Static inspection is not device validation.

## Reference and licensing boundary

The Windows application reference is frozen BSL 1.1 commit
`45440d47296ef4c250fc03730259dbeebb245ebc` (v2.4.2), together with the locally
available v2.4.2 release. No newer upstream read-only-licensed source was imported.
See [FORK-NOTICE](FORK-NOTICE.md) and the repository LICENSE for the actual terms;
this is a source-available fork, not an unrestricted open-source grant.

Inspected release hashes (SHA-256):

| Artifact | Hash |
|---|---|
| Windows Kirin-Tool.dll | `8f7333fa1e7b488e772ee2addc0c5ba1ef32feb9b14bec03ab9dc98e9fc39860` |
| Legacy fastboot.exe | `64c7a960a550c48003eb438964c1924b2ba68765087e3f71e0237a06545124f1` |
| XML fastboot.exe | `df7d0492c3f356ef057c669da896749b156bf7ec5ed36d4001c9559794807084` |

All 56 loader files and the payload compared byte-for-byte with the local Linux
copies. Application decompilation supported comparison of the relevant flashing
paths; it does not prove every behavior of the entire product equivalent.

## Intentional Windows behavior retained

The frozen release includes the PRELOADER header adjustment for software testpoint
on EMUI 10.1, Kirin 820/985 loader changes, and anti-rollback warnings. The loader
blobs and special exploit stages that intentionally omit ACK checks remain intact.
The hisi980 `fastbootf.ktl` fast-loader choice is distinct from host UltraFlash.

## Material differences and candidate fixes

The working tree contains divergent hardening that is not approved for stable;
see [branch/parity review](stable-parity-review.md).

* Both application implementations had a positional sparse merger. The Linux port
  corrected its block count without correcting its layout: data could move to a
  different logical block while becoming acceptable to libsparse. The replacement
  preserves logical extents, compares overlaps, and rejects conflicting data.
* The previous Linux backup implementation guessed DATA framing. Windows legacy
  `dump-emmc` queries `getvar:emmc:<partition>`, obtains a 64-bit address/length,
  sends `upload_emmc:<address>:<length>`, consumes OKAY, exact raw bytes, then OKAY.
  Modern `dump-storage` uses the corresponding storage commands. The replacement
  implements this observed client sequence, checks disk writes, and publishes a
  backup only on complete success. A real-device capture is still outstanding.
* Linux now preflights selected images, refuses truncated extraction, preserves the frozen VCOM response policy, propagates handshake/unlock failures, and stops subsequent flash
  requests after a failure. These are safety corrections, not claims of vendor
  protocol equivalence.

## UltraFlash correction

Modern Windows fastboot invokes UltraFlash internally from normal `do_flash`.
In the inspected executable, `do_flash` at `0x40c8b0` calls UltraFlash at
`0x40c997` (target `0x464490`) and ultraflash_end at `0x40cfd6`. The disable flag
at `0x584918` defaults to zero. Main reads `HUAWEI_ULTRAFLASH`; `0` or `off`
disables it. The implementation also has a normal-flash fallback path.

Consequently, absence of an explicit UltraFlash command in the C# application
never established its absence on the wire. Linux currently uses standard
fastboot flashing and does not implement UltraFlash. It has not been established
that every device/firmware accepted by Windows behaves equivalently on Linux.
Undocumented memory upload and UFS-eye extensions are not supported.

See [flashing-safety.md](flashing-safety.md) for tests and remaining limits, and
[fastboot patches](../fastboot-src/PATCHES.md) for the native implementation.
