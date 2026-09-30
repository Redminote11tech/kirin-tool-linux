# Stable fidelity review — 2026-09-30

## Finding

The current working tree is **not ready to be called a faithful stable release**.
It contains uncommitted parity repairs mixed with independent hardening. No changes
from this review have been committed, pushed, or moved into `beta`. The fact that
the checked-out branch is named `linux-v2.4.2` does not make its dirty tree stable.
This assessment supersedes earlier blanket claims of parity or safety.

The governing acceptance rule in [the track document](beta-potential-features.md)
is that stable changes must exist in Windows or fix a Linux parity bug. The same
document freezes hardware blobs and serial code even for beta until device tests
and hashes exist. Thus beta is a place to develop candidates, not an exemption
from validation. Frozen reference: Windows v2.4.2 commit
`45440d47296ef4c250fc03730259dbeebb245ebc` plus the inspected release binaries.
Later read-only-licensed upstream content has not been imported.

## ACK correction

Frozen `Services/VcomFlasher.cs:WriteAndVerify` writes and flushes, then reads one
byte with a ten-second cancellation token only when `expectAck` is true. It does
not test the byte or read count. The release DLL decompiles to the same behavior.
Explicit no-read stages include the initial start frame, ordinary tail and parts
of the exploit sequence. A device that produces no byte at an ACK-expected stage
can already time out on Windows; the source alone does not establish a legacy
no-ACK exception at that stage.

The proposed hard `0xAA` check was not faithful and has been withdrawn. VcomFlasher
now matches its pre-audit Linux HEAD exactly. That HEAD retains Windows packet
construction, response policy and stage exceptions, with platform discovery and
resource-management adaptations. The commented Windows `!= AA || != 55` expression
is always true; it is neither executable behavior nor evidence that 55 is valid.
A different project's AA rule cannot establish all Kirin Tool loader behavior.

Six strict-ACK assertions were removed from the candidate harness. They tested the
new policy, not Windows compatibility. No replacement ACK policy is claimed tested
on hardware. The withdrawn diff is retained outside the repo in audit/.

## Sparse correction and uncertainty

Windows selects inputs by file size, drops the larger input's last chunk and the
smaller input's first chunk, concatenates the remaining bytes, writes block size
4096 and adjusts chunk count. It does not reject CRC chunk type or nonzero header
checksum. Consequently some CRC chunks can pass through, others can be dropped,
and retained checksums need not describe the new data. This is not verified CRC
support. The existing Linux HEAD additionally rewrites total_blocks; a synthetic
case demonstrated that this can make incorrectly relocated data structurally valid.

The replacement extent merger fixes that demonstrated relocation, but is a
**divergent candidate**, not a stable parity fix. Besides CRC/header checksums it
restricts header layout/version, block geometry, exact lengths and conflicting
overlaps. These can reject inputs Windows attempts to process. No representative
Huawei firmware corpus has yet established the real compatibility impact.

Do not solve this by silently stripping checksums, guessing their semantics, or
silently reverting to a known corrupting merge. For strict stable, isolate the
original algorithm and establish Windows-equivalent outputs on representative
packages; disclose its inherited limits. For beta, validate supported checksums
and merged output semantics against an independent sparse reader and a real corpus
before changing the accepted input set. The candidate remains in the working tree;
it has not been promoted as stable.

## Where parity matters

| Area | Evidence / difference | Status |
|---|---|---|
| Loader and payload bytes | 56 loaders plus payload compared byte-identical to inspected Windows release | Strong static evidence; not proof of device compatibility |
| VCOM frames, addresses, exploit stages | Inherited construction and response behavior after strict ACK withdrawal; Linux discovery differs | Close static parity; serial timing/driver behavior unverified |
| Loader orchestration | Candidate decrypts/preflights all stages first, rejects missing stages, changes final device detection | Divergent failure/timing behavior; beta candidate |
| USB-update packets / SW testpoint | Inherited packet scheme and PRELOADER adjustment; candidate changes preflight, mapped inputs, handshake/unlock failure enforcement and cancellation | No end-to-end wire parity claim; frozen-code changes need device evidence |
| SUPER merging | Existing Linux header-count difference plus candidate algorithm/rejection changes | Known non-parity; not stable-ready |
| Standard fastboot flash | Similar command intent, different native executable; Windows XML binary can internally enable UltraFlash | Material wire-protocol gap; no blanket equivalence |
| Partition / OEMInfo backup | Old Linux DATA framing was a demonstrable mismatch; replacement follows inspected vendor getvar/upload sequence | Parity-repair candidate; read trace and byte comparison still needed |
| XML / OTA partition loops | Candidate preflights all inputs and stops on first failure | Changed failure sequencing; beta policy, not strict Windows parity |
| FRP and downgrade | Existing multi-step command sequences continue after failures | Previous blanket fail-stop statement was too broad; destructive tests require expendable device |
| SN, model/vendor and OEMInfo writes | Command intent inherited; native output parsing and device semantics not exhaustively validated | No hardware validation claim |
| Crypto / CRC16 / OEMInfo conversion service | Security/CryptoUtil.cs, Utils/Crc16.cs and Services/OemInfoService.cs have no diff between frozen source and pre-audit Linux HEAD; candidate changes OEMInfo temp path/dump routing | Good static evidence for these files, not the whole conversion path |
| XloaderPatcher list restoration (pre-existing) | Windows RestoreListTxt enables every list entry; Linux restores saved original selections from list.txt.orig | Intentional-looking behavioral departure already in stable HEAD; patch-byte logic unchanged in this file comparison |
| OEMInfoEditor (pre-existing) | Linux adds an overall-buffer bound check and limits padding at EOF | Different malformed/truncated input handling; normal output equivalence needs golden files; this check is not proof of per-entry bounds safety |
| UI layout, file dialogs, app-relative paths | WPF to Avalonia/platform adaptations | Pixel equality is not needed; selected partition, order, warnings, cancellation and success reporting do matter |
| Packaging | Candidate refreshes app/native binary instead of reusing stale files | Build-correctness fix; verify packaged hashes and client selection |

## Sorting the current candidates

* Potential stable parity repairs: case-sensitive SUPER lookup, interface descriptor
  discovery, XML path normalization, correctly quoted process arguments, and the
  vendor backup protocol. Each still needs a focused Windows comparison, not just
  the label “fix.” Separate multi-device refusal from descriptor discovery.
* Beta-only behavior changes under the stated rule: whole-batch preflight and
  fail-stop, stricter input/name validation, sparse replacement, multiple-device
  rejection, mandatory backup capability marker, unsupported-memory rejection,
  native DATA-length rejection, and changed cancellation/reboot behavior.
* Serial-code candidates require the additional device-test gate from the existing
  track document even on beta. Do not cherry-pick whole files that mix categories.
* UI wording and packaging can be evaluated independently, but no message should
  call a loader transfer a verified persistent bootloader unlock.

A correct test suite can show that a new policy is implemented correctly while
saying nothing about Windows equivalence. The previous 61 checks included six
such ACK-policy checks. The remaining harness is a candidate regression suite,
not a stable parity certification. See [the testing checklist](testing-checklist.md).
