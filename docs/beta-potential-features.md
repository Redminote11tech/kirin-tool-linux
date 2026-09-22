# Kirin Tool for Linux — Beta Track: POTENTIAL Features (NOT planned for implementation)

Status: **ideas only**. Nothing in this document is scheduled, promised, or in progress.
Written 2026-09-05 after the 1:1 parity pass (see `fastboot-windows-linux-diff.md`).

## Why split into two tracks

| | **Stable** (strictly 1:1 faithful) | **Beta** (divergent) |
|---|---|---|
| Branch | `linux-v2.4.2` | `beta` (branch off stable once current work is committed) |
| Goal | Be a faithful Linux port of Kirin Tool for Windows: same features, same behavior, same warnings, same command sequences | Improve on the Windows tool in ways Windows never had — Linux-native, safety, quality-of-life |
| Acceptance rule for a change | It must exist in the Windows binary/upstream, or fix a parity bug (a feature that exists on Windows but misbehaves on Linux) | Anything goes, subject to the guardrails below |
| Sync direction | Upstream Windows → Stable | Stable → Beta (cherry-pick parity fixes); **Beta never merges back to Stable** |
| Example | Restoring `StartFullOtaFlash`, the bundled `fastboot-src` dump client, SN-write rewiring | Auto-backups, hotplug detection, dry-run mode (all below) |

Suggested git setup (when ready):

```
git branch beta            # create the divergent track at the current point
git checkout linux-v2.4.2  # stable stays the default/1:1 branch
```

## Guardrails that apply to Beta too (never violate)

1. **No telemetry, no network calls** — the Windows tool has none; neither track gets any.
2. **No auto-flash / silent writes.** Every write operation keeps an explicit user action.
3. **Hardware blobs stay byte-identical unless a change is verified on device**: `loaders/`,
   `fastboot/payload`, and the VCOM/serial code (`VcomFlasher`, `UsbDownloadService`,
   `XloaderPatcher`, `SoftwareTestpointService`) are treated as frozen; changes require a
   working device test and hashes recorded before/after.
4. **No new Huawei loader redistribution** beyond what upstream already ships (legal exposure).
5. License stays BSL-1.1 (→ GPL-3.0 at Change Date); AOSP-derived code stays isolated in
   `fastboot-src/` with its own NOTICE.

## POTENTIAL features (Beta only)

### Safety & data protection
- **P-01 Automatic pre-op backups** — snapshot oeminfo / nvme / partition dumps to a
  timestamped backup dir before any destructive step, with a restore manager UI.
- **P-02 Dry-run mode** — resolve and print every fastboot/serial command an operation *would*
  run, without executing; a toggle per operation.
- **P-03 Partition write allowlist** — user-editable list of partitions that may never be
  flashed (e.g. `modem_secure`, `nvme`, `persist`), enforced below the UI layer.
- **P-04 Brick-risk labels per partition** — surface risk ratings (bootloader/xloader vs
  userdata) next to every checkbox in the flash dialogs.
- **P-05 Image verification before flash** — optional sha256 manifest check of selected images
  and size-vs-partition sanity via `getvar partition-size:`.
- **P-06 ARB / lock-state awareness** — read and display ARB-relevant state before operations
  that increment it, instead of a static warning dialog.

### Linux-native integration
- **P-07 In-app fastboot client (C#/libusb)** — replace the external fastboot binary entirely;
  removes the android-tools dependency and the bundled-binary size cost. Large effort.
- **P-08 USB hotplug monitoring** — watch udev/netlink for fastboot/VCOM/DBAdapter appear
  events instead of polling `fastboot devices`; instant UI updates.
- **P-09 First-run permission wizard** — ✅ lite implemented (beta): read-only startup
  diagnostics (`Utils/LinuxEnvironmentCheck`) detect missing udev rules and missing
  adbusers/uucp membership and show the exact fix commands. No pkexec wizard yet.
- **P-10 usbfs buffer check** — ✅ lite implemented (beta): same startup diagnostics detect
  the 16 MB `usbfs_memory_mb` default and print the tuning commands.
- **P-11 Packaging breadth** — ✅ CI + AppImage: GitHub Actions workflow builds the
  self-contained app + bundled fastboot, the pacman package, and an AppImage
  (`packaging/build-appimage.sh`, any-distro, no root/FUSE) as artifacts
  (tag push / manual). Flatpak remains open.
- **P-12 Headless CLI mode** — scriptable `kirin-tool-cli flash --xml ...` using the same
  services; enables automation and CI testing.

### Dump / backup power features (builds on the new bundled fastboot)
- **P-13 Dump progress & resume** — parse INFO progress from the bootloader during
  `oem dump-*`, show %, and support resume of interrupted dumps.
- **P-14 Full-partition-table backup set** — one click dumps every dumpable partition into a
  manifest'd backup folder.
- **P-15 NVMe/nve variable explorer** — read/write arbitrary `nve:` vars (not just SN) with
  backup/restore per var.
- **P-22 USB Port Settings / Manufacture mode** — storage identified (`persist.sys.usb.config`
  + vendor `usb_port` daemon, see `usb-port-manufacture-mode.md`). ✅ lite implemented (beta):
  read-only display of active + persisted USB mode over ADB (`Services/UsbPortModeReader`,
  DeviceInfo page button). Programmatic switching requires root — intentionally not performed.

### UX / quality
- **P-16 Error translation** — ✅ lite implemented (beta): `Utils/FastbootErrorHints` maps
  known failure patterns (locked bootloader "Command not allowed", factory "no permission",
  usbfs stalls, usbfs/memory errors) to actionable hints, applied to partition-flash
  completion messages and dump failures. Full coverage across all dialogs remains open.
- **P-17 Firmware package browser** — inspect UPDATE.APP contents (partition list, sizes)
  before choosing anything.
- **P-18 Profiles** — saved per-device presets (CPU selection, file slots, operation sets).
- **P-19 Theming & i18n** — dark mode; localized strings.
- **P-20 Structured session logs** — one JSON/text session log per operation, shareable for
  support (still local-only).
- **P-21 Opt-in update check** — query GitHub releases only with explicit user consent.

### Explicitly out of scope for both tracks
- UltraFlash / `oem dump-ufseye` / `memupload` UI exposure (no public protocol docs; no app
  usage on Windows either).
- Anything that phones home, cloud-unlock services, or packaged firmware distribution.
- Kirin SoCs beyond the 18 loader sets already shipped (would require new loader blobs).

## Prioritization sketch (if Beta ever starts)

High value / low risk: P-09, P-10, P-16, P-01, P-06.
High value / medium risk: P-08, P-02, P-13, P-05.
Long-term / large: P-07, P-12, P-11.
