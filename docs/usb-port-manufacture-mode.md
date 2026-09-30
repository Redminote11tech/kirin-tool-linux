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

---

## 7. LIVE CAPTURE 2026-09-12 — NOH-AN00 (Mate 40 Pro) in manufacture mode

First real-device capture of the feature, on **NOH-AN00 (Mate 40 Pro,
Kirin 9000)** with USB Port Settings = Manufacture mode:

**USB enumeration** (`lsusb -v`, `udevadm`): PID `12d1:107e` (lsusb DB name
"P10 smartphone" is bogus; product string is `NOH-AN00`), 4 vendor-specific
interfaces:

| If | SubClass | Protocol | Bound by CachyOS kernel | Interpretation |
|----|----------|----------|--------------------------|----------------|
| 0 | 0x13 (19) | 0x21 (33) | `usbserial_generic` → ttyUSB0 | silent (no spontaneous output) |
| 1 | 0x13 (19) | 0x22 (34) | `usbserial_generic` → ttyUSB1 | **live AT interpreter** |
| 2 | 0x13 (19) | 0x23 (35) | `usbserial_generic` → ttyUSB2 | silent |
| 3 | 0x42 (66) | 0x01 | `usbserial_generic` → ttyUSB3 (wrongly) | **ADB** (class 255/0x42/1) |

Notes: `usbserial_generic` claiming interface 3 *blocks adb* (interface busy
for libusb). Unbind it to use ADB:
`echo 3-2:1.3 | sudo tee /sys/bus/usb/drivers/usbserial_generic/unbind`.
Also worth binding the silent ports to the `option` driver instead of
generic (`echo 12d1 107e | sudo tee /sys/bus/usb-serial/drivers/option1/new_id`)
— option's per-interface quirk tables often unmask Huawei protocols.

**AT gate result** (probe scripts in `tools/serial-probe/`):
- `AT` → `OK`. Everything else — every `AT+` and `AT^` command tried
  (CGMI/CGMM/CGMR/CGSN/CIMI/CSQ/CPAS, E1/V1/Z/&F, ^GETPORTMODE, ^VERSION?,
  ^SN?, ^HVER?, ^AUTH (all forms), ^NVWREX=? …, even garbage) → `\r\nno
  permission\r\n`.
- Interpretation: the manufacture-mode AT interpreter runs, but the command
  permission layer is closed. Per repair-community reports, sensitive
  factory AT operations on modern Huaweis are authorized cryptographically /
  server-side (the reason paid tools exist); no public unlock is documented.
- ttyUSB0/2: no response to text, AT set, or simple DIAG-frame pings
  (`0x7e…`) — likely binary diag/log streams needing a proper session
  handshake; not identified.

**Where this leaves the paths from §3:**
- Path C (hidden fastboot/AT OEM commands): the AT surface exists but is
  permission-gated; without factory authorization nothing is permitted.
  Deprioritized.
- Path D (ADB) is now the primary research route: manufacture mode exposes a
  dedicated ADB interface; after the unbind, test `adb devices` —
  manufacture-mode ADB commonly connects without the RSA prompt. Then
  `getprop` / `settings list` to find the key storing the port mode.
- Paths A/B (oeminfo/nve diff) remain available via fastboot dumps and are
  still the most likely storage location.

## 8. ADDENDUM 2026-09-12 (ADB session) — storage identified: Android persistent properties

With ADB authorized on the NOH-AN00 (PQY0220B18020151, non-root production
build, EMUI 14.2 / Android 12):

**The mechanism is found:**

- `/vendor/etc/init/usb_port.rc` defines the `usb_port` daemon
  (`/vendor/bin/usb_port`, running as `system`) plus `usb_port -w 1`
  (usb_port_n) and `usb_port -m` (usb_monitor), and init **property bridges**:
  - `vendor.set_usb_config`   → `sys.usb.config`            (runtime switch)
  - `vendor.set_p_usb_config` → `persist.sys.usb.config`    (persisted default)
- Live state: `sys.usb.config` = `manufacture,adb` (current, manufacture
  mode), `persist.sys.usb.config` = `hisuite,mtp,mass_storage,adb`
  (persisted default). Mode strings are comma-separated USB function lists —
  "manufacture" and "hisuite" are function names understood by the vendor USB
  stack, not magic numbers.
- `dumpsys usb` confirms the active functions (`ADB` + `0x4000`) and exposes
  Huawei's extension service `hwUsbExService`
  (`huawei.android.hardware.usb.IHwUsbManagerEx`).

**What is blocked, and why:**

- `setprop vendor.set_usb_config / vendor.set_p_usb_config` from `adb shell`
  → SELinux denial (`shell` context may not set vendor properties). The
  init-bridge write path exists but requires a context allowed to set vendor
  properties: i.e. **root** (or the `usb_port` daemon itself).
- `adb root` → "adbd cannot run as root in production builds".
- `cmd usb` on this build → "No shell command implementation".
- Calling `hwUsbExService` directly → HwBinder, SELinux-gated from shell.

**Conclusions for P-22:**

1. **Storage identified:** the port mode lives in Android persistent
   properties (`persist.sys.usb.config`), managed by the vendor `usb_port`
   daemon — **not** in oeminfo, **not** in nve. Paths A/B from §3 are ruled
   out for this device (EMUI 14.2). Path C stays gated (§7). Path D works
   read-only.
2. **What the tool can do without root:** *read and display* the current
   (`sys.usb.config`) and persisted (`persist.sys.usb.config`) USB mode over
   ADB — safe, useful, and implementable now. Switching modes
   programmatically requires root (`setprop vendor.set_usb_config <list>` →
   init bridge applies it) or the factory authorization the AT gate demands.
3. Switching is trivially possible for the *user* via ProjectMenu — the
   feature's remaining tool value is display/guidance + root-only switching.

Documented live evidence: probe transcripts in §7; this section's property
dumps taken over ADB on 2026-09-12. One live observation worth keeping:
during earlier testing the persisted config was `hisuite,…` while the active
config was `manufacture,adb` — i.e. the ProjectMenu toggle applied the
runtime config while the persisted default had not been changed, which is
why the mode can revert after reboot unless the daemon persists it.
