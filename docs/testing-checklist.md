# Windows/Linux comparison checklist — 2026-09-30

Purpose: establish fidelity per function, model and firmware. Passing one device
or one OS mode does not validate all 18 loader sets. Do not test destructive
functions just to complete a checklist. The dirty working tree currently contains
unvalidated beta candidates; see [stable-parity-review.md](stable-parity-review.md).

Record for each test: date, Windows release SHA-256, Linux commit plus patch hash,
packaged fastboot SHA-256/version, device model/SoC, exact firmware/region,
bootloader/FRP state, USB mode, operation, input hashes, selected partitions/order,
logs, outcome and recovery method. Keep full logs privately: they can contain
serial numbers, identifiers and device-specific backup data.

## A — Verify offline: no phone connected

Use a fresh output directory and copies of inputs. Disconnect the phone before
launching either application. Some UI actions combine conversion with live pull
or offer flashing afterwards; disable live pull and decline all flash prompts.
Do not assume every button has a read-only preview.

- [ ] Launch both; compare all pages, available operations, supported CPU choices,
  FastFlash-loader availability, warnings and selection defaults. Appearance may
  differ; hidden defaults and operation meaning must not.
- [ ] Hash all loaders and payload; compare with the exact frozen Windows release.
  Treat the host UltraFlash feature separately from `fastbootf.ktl`.
- [ ] Open representative BASE/CUST/PRELOAD UPDATE.APP and partition-table inputs.
  Compare names, declared lengths, offsets, order, extraction hashes and case.
  Check XloaderPatcher list restoration: Windows re-enables all entries, whereas
  existing Linux code restores original selections.
  Include path spaces, mixed case and non-ASCII host paths.
- [ ] Compare generated/read XML: identifiers, image paths, ordering and skipped
  secure partitions. Display names are not necessarily flash identifiers.
- [ ] For SUPER, use real input pairs from each relevant firmware family. Inventory
  sparse header version/size, block size/count, CRC chunks, header checksum, holes
  and overlaps. Compare Windows output bytes AND independently decoded block
  positions. Windows producing a file is not a correctness pass. Keep all results
  offline if outputs disagree or either output is malformed.
- [ ] Convert copied OEMInfo files with identical options; compare output hash and
  changed offsets. Do not accept a post-conversion flash prompt.
- [ ] Through a fake executable/serial transport only, compare command sequence,
  parameters, stage order, timeout/cancellation handling and failure reporting.
  Test missing files, truncated packages, wrong ACK bytes, no response, failed
  later partition, disk-full and disconnect simulations. NEVER induce a USB
  disconnect or power failure on a real phone for these checks.
- [ ] Run candidate regressions: `dotnet run --project tests/SafetyTests/SafetyTests.csproj`
  and `make -C fastboot-src test`. They do not access USB hardware; they are not a
  substitute for a frozen-Windows differential harness.
- [ ] Inspect the built package's app, loader and fastboot hashes. Confirm which
  fastboot is actually selected; a PATH/system fallback changes the test subject.

Pass: all differences explained and classified as platform adaptation, existing
parity bug, inherited upstream defect, or deliberate beta behavior. Keep failed
fixtures and exact outputs. Do not promote beta rejection policies to stable just
because their unit tests pass.

## B — Cautiously verify: device reads / mode changes

Use only the intended device, stable power/cable and known matching firmware.
A vendor read command is lower risk than a write, not guaranteed side-effect-free.
Check the action's actual command sequence first. Capture existing Windows/Linux
transactions with USBPcap/usbmon if available; do not invent probing OEM commands.

| Check | Compare / success evidence | Stop condition |
|---|---|---|
| Detection in an already-established fastboot mode | Same serial/device; Linux chooses intended interface; version and resolved client recorded | Ambiguous identity or unexpected mode |
| Device Information | Model, versions, lock state and serial against Windows and device labels | Unexpected command, inconsistent field, or parser reports success on failure |
| Read SN | Read-only NVE query and exact returned value | A command containing a write value or unexpected state change |
| Partition listing / XML preparation | Same identifiers, range/size and supported-device restrictions | Missing/different partition or unsafe inferred range |
| Backup / live OEMInfo pull, without conversion flash | Same partition and range; two reads from unchanged state; exact size and byte/hash agreement with Windows | Query mismatch, short data, receipt mismatch or different bytes; investigate before writes |
| Ordinary reboot | Only if boot/recovery access is already known; correct target mode and device identity after reconnect | Recovery unknown, unexpected write preparation, wrong identity |

Do not run backup comparisons across intervening writes or boots that may change
the partition and then interpret every difference as a protocol bug. A matching
backup is evidence for that read path; it does not prove restoration will work.
The UI currently excludes some SoCs from partition dumping; do not bypass that
restriction to complete this checklist.

“Reboot to USB Update” in this app is NOT an ordinary reboot-only test: it can
prepare and flash rescue recovery partitions. Keep it in category C.

## C — Risky: only on an expendable, recoverable test device

Require a device-specific recovery method demonstrated independently, verified
matching images and backups, and tolerance for complete data loss or permanent
failure. A backup alone is not a recovery method. If these prerequisites are
unavailable, mark these tests **NOT TESTED**; do not use the valuable phone.

| Function | What must be compared with Windows |
|---|---|
| VCOM “unlock fastboot” / loader transfer, normal and FastFlash choice | Exact stage/blob hashes, addresses, frames, response reads, timing/replug prompts and resulting mode; persistent lock state checked separately |
| Software testpoint enter/exit | Same patched inputs/PRELOADER scheme, target writes, transitions and demonstrated exit/recovery |
| Fastboot XML selected-partition flash | Identical targets/order/data, native client behavior, exit handling and readback; Windows UltraFlash on/off must be recorded |
| Selected UPDATE.APP flash and Full OTA | Same extraction/merge decisions, target mapping, secure skips and partition order; independent merged-data check before use |
| USB-update Full OTA | Header/image pairing, sparse data, response sequence, header checksum/signature acceptance, reboot and readback |
| “Reboot to USB Update” / rescue preparation | Recovery-to-rescue partition mapping and all writes before mode change |
| Bootloader unlock | Actual security-state transition and wipe behavior, not just an OKAY message |
| FRP removal | All erase commands, per-step failure behavior and resulting state; do not use a personal device |
| Enable downgrade | Version-metadata writes/erases and actual resulting state; this is not proof anti-rollback is disabled |
| Write SN / model / vendor | Exact intended field changes and preservation of other OEMInfo/NVE data |
| Flash converted OEMInfo | Matching model/layout, precise write/readback and preservation of device-specific identifiers |

Run one controlled scenario at a time. Windows-first then Linux on a modified
device is not an equivalent A/B comparison: establish the same starting state
through the already-proven recovery procedure, or explicitly record the limitation.
Do not “restore a baseline” on a device whose recovery path is itself untested.
Record failures as failures, not invitations to try alternate loaders/addresses.

## Release evidence

- [ ] Stable: every included patch mapped to frozen Windows evidence or a proven
  Linux-only parity bug. No changes imported from newer read-only source.
- [ ] Beta: deliberate behavioral differences listed, including accepted-input
  changes and failure/cancellation sequencing.
- [ ] Serial/blob modifications satisfy the existing hardware-test/hash gate.
- [ ] Test results identify exact supported model/firmware combinations; untested
  combinations remain explicitly untested.
- [ ] No claim that host tests, unchanged blobs, an OKAY response or a successful
  boot alone establishes complete flashing safety or universal parity.
