# Native fastboot parity — 2.4.5

This release replaces the packaged 2015 fastboot base with a pinned modern AOSP
engine and ports the independently reconstructed Huawei extensions onto it.
The UI and Kirin 9000 capability policy remain unchanged. Neither Windows EXE
was run, and no physical phone operation was used for implementation or testing.

The static reference remains the supplied legacy client SHA256
`64c7a960a550c48003eb438964c1924b2ba68765087e3f71e0237a06545124f1` and
HiSilicon XML 3.3.5 SHA256
`df7d0492c3f356ef057c669da896749b156bf7ec5ed36d4001c9559794807084`.
The [whole-binary audit](windows-fastboot-audit-v2.4.3.md) records command
addresses, exports and decompiler limitations. Its missing-feature table is the
historical 2.4.3 status; the current status is below. Raw exports stay private.

## Coverage

| Windows host feature | Native release implementation | Validation |
| --- | --- | --- |
| Getvar/OEM forwarding, PSID, NVE queries | AOSP command dispatcher; explicit getvar refuses with nonzero exit status | Fake CLI success/refusal; managed response parsers |
| Named DDR memory and raw memory ranges | Reconstructed Huawei geometry and upload handlers | Partial reads, malformed replies, range overflow, binary stdout, atomic output |
| eMMC/storage dumps | Reconstructed geometry query plus 16 MiB upload chunks | Wire tests, chunk boundary, tail failures, original-file preservation |
| UFS-eye upload routine | Native explicit handler | Validated wire/range behavior; Windows dispatcher reachability remains unproven |
| UltraFlash start/end, normal fallback, environment opt-out | Reconstructed wrapper around ordinary/sparse flash_buf paths | Accepted/refused/I/O/malformed DATA traces; exact refusal reason preserved |
| Staging/get_staged and range fetch | Modern AOSP driver plus atomic output and fetch-size validation | Success, refusal, wrong lengths, tail failure, existing-file preservation |
| Slots and secondary image planning | Modern AOSP slot and image-plan implementation | Explicit slot command, slot-qualified flash trace, parser tests |
| Fastbootd reboot/reconnect | Modern AOSP workflow | Fake reconnect, already-userspace, refusal and wrong-mode tests |
| Logical create/delete/resize, snapshot and GSI | Modern AOSP commands/workflows | Fake command traces and error propagation |
| ZIP update/flashall, liblp wipe-super, sparse/64-bit image handling | Modern AOSP source and linked libraries | Compiled; upstream requirement/task parsing tests; end-to-end fixture coverage incomplete |
| Formatting | Modern AOSP fs generator plus bundled mke2fs.android, make_f2fs and configuration | Built helpers; offline image-generation smoke checks |
| New boot headers, DTB, vbmeta options | Modern AOSP boot/vendor-boot implementation | Compiled; OS version/patch-level parser tests; all header variants not tested |
| TCP/UDP device selection and transport | Modern AOSP transport implementation | Compiled; network serial parser tests; live transport not validated |
| Legacy authenticated flashing | nonce/lock commands and unlock-message download sequence retained | Fake wire traces; no unlock operation performed |
| Windows USB/platform drivers | Replaced with native Linux backend | Linux build and portable CPU/runtime smoke checks; no Windows-driver reuse |

The source is android-tools 35.0.2, SHA256
`d2c3222280315f36d8bfa5c02d7632b47e365bfe2e77e99a3564fb6576f04097`.
It provides the standard workflows already identifiable in the modern Windows
client, instead of attempting to reconstruct its entire C++ runtime from Ghidra.
The source archive, modifications and build instructions accompany the binary
bundle under sources/; dependency notices are in licenses/. Filesystem helpers
remain separate programs and their GPL corresponding source is included.

## Evidence and limits

62 full CLI checks use a transport that cannot open USB devices. Thirteen driver
tests pass, including ten Huawei tests. Ten upstream workflow/parser tests pass;
four explicitly skip because the Android product fixtures are absent. Managed
checks cover ten response-parser cases, 47 Kirin 9000/PSID cases and 35 stable
parity cases. Source catalogs and successful compilation do not establish every
branch's behavior. Actual phone/transport and complete image-workflow validation
remain outstanding; there is no honest 100 percent hardware-parity claim.

Newer AOSP adds workflows that did not exist in the frozen client. Its optimized
super-image path uses standard FlashPartition directly; Huawei UltraFlash wraps
the ordinary/sparse flash_buf path reconstructed from the supplied EXE. UFS-eye
is an explicit Linux extension where Windows reachability was not established.
Windows platform drivers, output formatting and implementation defects are not
reproduced byte-for-byte. Failed reads preserve existing destination files.

None of these host features override firmware policy or discover a protected
bootloader's allowed partition map. NOH-AN00's earlier UltraFlash/PSID refusals
and rescue partition-length lookup errors remain unresolved device-side results.
Do not infer successful Kirin 9000 rescue flashing from this host-client work.
No new factory loader, rescue alias or 5G-loader substitution is introduced.
