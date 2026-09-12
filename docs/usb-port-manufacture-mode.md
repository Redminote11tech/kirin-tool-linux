# USB Port Settings / "Manufacture Mode" — Research Note

Status: **documentation / research plan only** — nothing implemented.
Written 2026-09-12. Companion to `beta-potential-features.md` (added there as
P-22) and `soc-support-fast-flash-loader.md`.

## 1. What "USB port settings / manufacture mode" is (best available understanding)

On Huawei/EMUI/HarmonyOS devices there is a hidden engineering menu
(ProjectMenu, typically `*#*#2846579#*#*` in the dialer) with a
**Background Settings → USB Port Settings** entry offering, per community
documentation, three modes:

| Mode | Believed behavior (community knowledge — UNVERIFIED by us) |
|---|---|
| **Default mode** | Consumer USB enumeration: charging + MTP, ADB only when USB debugging is enabled and authorized |
| **Hisuite mode** | Standard HiSuite-facing enumeration (MTP + ADB + HiSuite protocol handshake) |
| **Manufacture mode** | Factory/engineering enumeration: port comes up with ADB/serial/diagnostic interfaces enabled from boot, without consumer gating |

Two important clarifications up front:

- This is a **booted-OS USB configuration** setting. It does **not** affect
  bootrom/VCOM (`0x12D1:0x3609`), fastboot mode, or bootloader unlock — those
  are boot-level and independent. Manufacture mode is therefore *not* a
  prerequisite for any flashing flow this tool performs.
- The exact interfaces/PIDs each mode exposes, and **where the setting is
  stored** on the device, have **not** been verified by us. Community sources
  describe the behavior, but the storage location is the whole question for
  implementation.

## 2. Verified research results (2026-09-12)

The feature does **not exist anywhere in the tool chain**, so adding it is
new functionality (Beta track only — it would break the strictly-1:1 Stable
contract):

- **Windows Kirin-Tool.dll** (decompiled, 47 files): no manufacture/port-mode/
  hisuite-related code. The only "hisuite" string is the rescue-flow response
  check (`start to hisuite`, `Services/USBUpdate/UsbDownloadService`-related
  rescue logic — unrelated).
- **Linux port source**: same (only the rescue check + an unrelated USB
  descriptor attribute name in `LinuxSerialPortFinder`).
- **Old Huawei fastboot (`fastboot/fastboot.exe`)**: no such OEM commands.
- **Modern Huawei fastboot (`fastboot/xml/fastboot.exe`)**: no such OEM
  commands. Its documented OEM surface is dump/storage/DDR/UFS-eye and
  `oem allow-flash-super` only.
- **`OemInfoEditor`** (the oeminfo record editor): knows only model/vendor/
  region records (`TargetIdsMap` versions 6/8/9). No port-mode record.

Conclusion: if a USB-port-mode switch is to be added, it is a **greenfield
feature** — the implementation question is entirely "where does the device
store this setting and how is it changed", which no available binary answers.

## 3. Candidate implementation paths (all unverified)

| Path | Mechanism | Evidence needed |
|---|---|---|
| **A. oeminfo record** | Setting stored as an oeminfo entry; read/edit via the existing `OemInfoEditor`/`FlashOemInfo` flow | **Identify the record** by diffing oeminfo dumps before/after toggling the setting in ProjectMenu on a real device |
| **B. nve/nvme variable** | Setting stored as an `nve:` variable; read/write via the existing `getvar nve:` primitive (`ReadNveVariable`/`WriteNveVariable`) | Dump/enumerate nve vars before/after toggle and diff |
| **C. bootloader OEM command** | A hidden `fastboot oem ...` command not in the help text | Probe candidate commands on an unlocked device and observe responses (low expectation — no strings found in either client) |
| **D. ADB/settings route** | Change it from the booted OS via ADB (settings/props), outside fastboot mode | Root/ADB research on the target OS; separate feature, different scope |

Path A is the most attractive technically (the tool already reads *and*
writes oeminfo), but also the most dangerous: oeminfo is device-identity
data; writing the wrong record is exactly what the rebranding feature
deliberately guards against.

## 4. The identification procedure (enables A and B)

The bundled Huawei-capable fastboot added to this fork makes the diff-based
identification possible for the first time on Linux:

1. Device with an unlocked bootloader and a working dump (see
   `fastboot-windows-linux-diff.md` §7.1 first-run checklist).
2. Dump oeminfo (`PullOemInfo`) → save as `before.img`.
3. On the booted device: ProjectMenu → USB Port Settings → switch mode
   (e.g. Default → Manufacture). Reboot.
4. Dump oeminfo again → `after.img`.
5. Binary-diff `before`/`after` (`cmp -l`, or the OemInfoEditor record parser
   to name the changed entry). Also try path B by dumping/listing nve
   variables before/after if a dump mechanism for them is identified.
6. Repeat for Hisuite mode to distinguish "port mode" records from noise
   (timestamps, counters).

This procedure requires **one volunteer device** and changes nothing on it
except the ProjectMenu setting itself — it is read-only research.

## 5. Risk assessment (for the eventual feature)

- Read-only research (§4): no device risk beyond the ProjectMenu toggle.
- Implementing writes (paths A/B): **medium-high risk** — oeminfo/nvme are
  identity-critical; a wrong record write can break branding, serial, or
  boot behavior. Mitigations that already exist in the tool: mandatory
  oeminfo backup before edit (`PullOemInfo` + `flash oeminfo` restore),
  bounds-checked record editing (`OemInfoEditor`), version-guarded
  (`TargetIdsMap`).
- Whatever path wins, the feature must refuse to write unless a backup of the
  target partition was taken in the same session.

## 6. Track decision

- **Stable (`linux-v2.4.2`): no.** The Windows tool has no such feature
  (verified §2); adding it would break the strictly-1:1 contract.
- **Beta: P-22 (added to `beta-potential-features.md`), research-required.**
  Do not implement until the §4 diff procedure has identified the storage
  location on real hardware. Until then, "we don't know what manufacture
  mode does on your specific device" remains the honest answer — community
  descriptions of the three modes are plausible but unverified by this
  project.
