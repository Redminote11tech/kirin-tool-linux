# Kirin Tool Linux 2.4.2-stable.1

Local stable compatibility package, 2026-09-30. This release is not a claim of
hardware validation. It preserves the frozen v2.4.2 Windows behavior where ported;
known gaps below remain. No newer upstream read-only source was imported.

## Included on stable

* Restore the frozen BSL Windows SuperMerger, XloaderPatcher list restoration,
  SoftwareTestpointService cleanup and OEMInfoEditor behavior. These undo earlier
  Linux-only behavior changes; they do not repair inherited Windows defects.
* Keep the existing Windows-compatible VCOM response handling: no strict ACK-byte
  check. USB-update handshake, failure sequencing and cancellation stay at the
  pre-audit stable implementation.
* Fix Linux filesystem differences: use the actual extracted SUPER filename,
  normalize Windows XML path separators, and read USB interface descriptors from
  the serial interface ancestor. Keep first-match discovery behavior.
* Preserve flash argument boundaries through ProcessStartInfo.ArgumentList,
  without beta's new partition-name/file validators or batch policies.
* Replace Linux's incorrect guessed DATA upload backend with the vendor
  getvar/emmc-or-storage/upload sequence observed in the frozen Windows clients.
  This is a native read-path parity repair; no UltraFlash write implementation is
  added. Existing managed fallback/selection behavior is retained. Local disk
  writes must finish before the native reader reports a successful backup.
* Rebuild app and fastboot during packaging. Ship license/notices, source commit,
  release notes and testing checklist. Stop tracking generated bin/ files.

## Kept separate on beta

The extent-based merger and checksum rejection, whole-batch preflight, fail-stop
partition loops, stricter header/image/name checks, backup capability/receipt
requirements, no-retry-after-transfer-failure policy, multiple-device refusal,
loader preflight and altered serial/cancellation handling belong to beta.
Native exact-DATA-size enforcement is beta-only. The strict AA ACK proposal was
withdrawn entirely. Beta serial changes remain unvalidated candidates under the
existing hardware-test gate; beta is not certified safe either.

The beta checkout preserves its existing UI features and error hints. No beta
branch has been merged into stable. Candidate history is retained on
`audit/hardening-candidates-20260930` and integrated into `beta`.

## Known limitations retained or unresolved

**SUPER:** stable uses the exact Windows positional algorithm. It can misplace
blocks and carry stale checksums on unsuitable input layouts. The tests establish
Windows byte parity, including CRC-containing inputs, not correct output geometry
or valid checksums. Use the offline corpus checks before any device write. Restoring
Windows behavior is not a safety fix for an inherited upstream defect.

**Fastboot:** Linux's native client is not the Windows modern XML client. Windows
can use UltraFlash inside normal flash; Linux cannot. Some vendor memory/UFS-eye
operations are not implemented. No universal device/firmware equivalence is claimed.
The vendor backup sequence is verified with fake USB and binary inspection, not a
real-device trace. Managed stable fallback may retry another read command after a
failure, as before; beta changes that policy.

**Serial and security writes:** Windows-style response acceptance and continuation
behavior are retained. They can report incomplete/partial results poorly; FRP,
downgrade and flash operations remain destructive. Stable does not include beta's
new preflight/fail-stop guarantees. The merged USB-update header scheme remains
hardware-unverified. Device enumeration is not persistent identity binding.

## Validation and provenance

Frozen Windows source: `45440d47296ef4c250fc03730259dbeebb245ebc`.
Pre-separation Linux stable: `8b8a2b46cd6b1cf455cb277ac55a42fc942eeb9b`.
The installed package's BUILD-COMMIT identifies its exact source. External release
checksums and manifest identify the package and shipped components.

Run hardware-free checks from the source root:

```sh
dotnet run --project tests/StableParity/StableParity.csproj -- .
make -C fastboot-src test
```

StableParity checks protected-source hashes against explicit frozen/pre-audit
references, absence of beta-only classes, Windows merger output bytes and argument
and XML path handling. Native tests exercise the vendor reader without usb_linux.
See [testing-checklist.md](testing-checklist.md) before hardware validation.
