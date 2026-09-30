# Flashing safety review — 2026-09-30

Status: **uncommitted hardening candidates, NOT a stable-parity release**. See
[the branch/parity review](stable-parity-review.md). Reproducible software defects addressed and hardware-free regression tests
added. **Not certified safe to flash a phone.** No phone writes were performed in
this review. A successful host test cannot establish firmware compatibility,
rollback eligibility, loader reliability, or power-loss recovery.

## Fixed hazards

| Severity / hazard | Correction |
|---|---|
| Critical potential: sparse merge relocates partition data | Preserve block offsets/holes; compare overlapping data; reject conflicts and malformed chunks; publish atomically |
| High: missing/truncated input silently skipped, or later flash continues after failure | Extract/preflight whole selected batch; missing images/headers fail; stop at first failure |
| High: guessed backup protocol and truncated backups reported successful | Implement inspected vendor read sequence; exact byte count, disk error checks, capability marker, atomic output |
| Compatibility correction: strict ACK check withdrawn | Stable retains frozen Windows response handling; new USB handshake/unlock failure enforcement remains a divergent candidate |
| High: stale mapped image or invalid header/image pair | Open actual mapped inputs before connecting; validate lengths/names; refuse empty batches |
| Operational: cancellation/window close permits misleading completion | Defer USB cancellation to partition boundaries; suppress reboot on observed cancellation; keep progress dialog open until work ends |

Loader inputs are decrypted and checked before connecting. Multiple matching
serial ports are refused. Standard fastboot batches preflight image files and
partition names. An initial unique-device check is not persistent device identity
binding across disconnect/reconnect; connect only the intended phone.

## Reproduction and tests

```sh
dotnet run --project tests/SafetyTests/SafetyTests.csproj
make -C fastboot-src test
make -C fastboot-src
```

The managed console harness targets .NET 9 and references the .NET 8 application.
It checks sparse output with an independent decoder, conflicting overlaps,
non-4096 blocks, malformed files, USB input/header preflight, uppercase SUPER,
and fastboot process exit/receipt/fallback handling with a fake
executable. It does not execute the Avalonia UI against a phone. Native fake USB
tests exercise the actual vendor reader; see its PATCHES.md. Host tests do not
simulate every device timing condition or interruption.

## Remaining validation limits

* UltraFlash is used internally by the inspected Windows XML client and is absent
  on Linux. Standard fastboot equivalence on every supported handset is unproven.
* USB-update merged SUPER uses the inherited header-adjustment scheme. Its image
  length is corrected, but package checksum/signature metadata is not regenerated
  or independently authenticated. Device acceptance and correctness require a
  known-good Windows comparison and device-specific readback; this flow must not
  be described as hardware-validated.
* Sparse images containing checksum chunks or a nonzero image checksum are
  deliberately rejected by the new merger; it does not silently strip unchecked
  integrity metadata. RAW, FILL and DONT_CARE are supported.
* File length validates completeness, not content identity. Backups still need a
  known-good read comparison before relying on them for recovery.
* The proposed strict ACK check was withdrawn: a separate downloader is not
  sufficient evidence to change the frozen Windows VCOM response policy.
* The frozen source comparison preserves vendor-specific loader/header behavior;
  it cannot prove that those original behaviors are safe for a particular model.

Before a device-level validation claim, record exact phone model/SoC, firmware and
rollback state, capture the matching Windows read-only backup transaction, compare
backup bytes, and validate writes/readback on a recoverable test device. The fixes
and passing tests alone are insufficient grounds to risk an irreplaceable phone.
