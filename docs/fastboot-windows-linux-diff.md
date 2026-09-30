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

Stable/beta separation is recorded in [stable-release.md](stable-release.md).

* Stable SuperMerger is restored byte-for-byte from frozen Windows source. This
  removes the earlier Linux-only total_blocks rewrite. The independent extent
  merger and its changed accepted input set remain beta-only.
* The previous Linux backup backend guessed DATA framing. The stable native reader
  now follows the observed Windows getvar/upload_emmc/upload_storage sequence.
  Device captures remain outstanding; managed fallback behavior is unchanged.
* Stable keeps the inherited VCOM ACK policy and pre-audit USB-update sequencing.
  New failure/preflight/cancellation policies remain on beta. Platform filename,
  XML separator and USB descriptor fixes are included on stable.

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

See [stable-release.md](stable-release.md) for tests and remaining limits, and
[fastboot patches](../fastboot-src/PATCHES.md) for the native implementation.
