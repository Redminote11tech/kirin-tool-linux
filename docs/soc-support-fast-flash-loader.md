# SoC Support & Fast Flash Loader — Analysis and Future-Addition Assessment

Status: **documentation only** — no code or blobs are added by this document.
Written 2026-09-05. Companion to `fastboot-windows-linux-diff.md` and
`FORK-NOTICE.md`.

## 1. Fast Flash Loader — Linux status: properly supported (1:1)

The "fast flash loader" (`fastbootf.ktl`, upstream's "fast flash loader"
switch) is a hisi980-only variant of the FASTBOOT loader stage used during
the VCOM bootloader-unlock flow, apparently optimized to reduce replug/cable
manipulation (the interaction text differs when it is active —
`Services/FirmwareUnlocker.cs:132`).

Verified in this port, identical to the Windows binary:

- UI: `UseFastFlashLoaderSwitch` ToggleSwitch on the Flash page
  (`MainWindow.axaml:300`); enabled per-cpu via
  `useFastFlashLoader = cpu == "hisi980" && UseFastFlashLoaderSwitch.IsChecked`
  (both in `HandleUnlockFastboot` and — since the parity pass — nowhere else,
  matching upstream).
- Loader substitution: `FirmwareUnlocker.UnlockFastboot(...)` swaps the
  `FASTBOOT` stage for `fastbootf.ktl` when the switch is on
  (`Services/FirmwareUnlocker.cs:87-89`).
- Blob: `loaders/hisi980/fastbootf.ktl` ships in the repo and in the pacman
  package (`/usr/lib/kirin-tool-linux/loaders/hisi980/fastbootf.ktl`),
  byte-identical to the Windows zip.
- Decryption path (`Security/CryptoUtil.DTL` with the embedded FBK key) and
  the VCOM upload sequence are shared with the normal loaders, unchanged.

No Linux-specific gap exists for this feature. It has the same on-device
verification status as every VCOM path (identical code + identical blobs to
the Windows tool; not hardware-tested in this session).

## 2. How per-SoC support is structured (why adding a SoC is "data, not code")

The VCOM unlock/flash flow boots an unlocked fastboot onto a locked device
via the USB bootrom ("head resend bootrom exploit" — credited to TASZK
Security Labs). For each supported SoC, the tool needs:

1. **Staged loader blobs** — `loaders/<cpu>/*.ktl` (`xloader`, `uce`,
   `fastboot`, sometimes `null`/`bl2`/`usbloader`), RSA-OAEP + AES encrypted,
   decrypted at runtime by the embedded FBK key. These are Huawei-derived
   bootloader images, obtained and decrypted by community researchers
   (credited upstream: kasnria001).
2. **Load addresses per stage** — the `FirmwareUnlocker.CpuAddresses` map
   (`Services/FirmwareUnlocker.cs:43-60`), e.g. hisi990:
   `null@0x22000, XLOADER@0x22000(merged), UCE@0x60000000, FASTBOOT@0x1A400000, BL2@0x1E400000`.
3. **Bootrom protocol compatibility** — the bootrom on that SoC generation
   must accept the VCOM upload sequence / be vulnerable to the exploit.
4. **Cable/procedure requirements** — newer SoCs (810/820/985/990) use the
   modified "Harmony TP" cable flow with replug prompts.
5. **A tester with the hardware** — nothing can be validated otherwise.

Given (1) and (2), adding a SoC is literally a map entry plus blob files. The
hard part is never the code — it is sourcing working, decrypted stage images
for that SoC and knowing the addresses, which is security-research work, not
porting work.

## 3. Kirin 990 4G — the plausible candidate, but unknown

Upstream explicitly excludes it ("Kirin 620-990 5G SoC support (as of 2.4.2,
excluding 990 4g)"). What is known/unknown:

- The 990 4G (e.g. Mate 30 4G, P40 4G-era SoCs) shares the CPU family with
  the supported `hisi990` (990 5G).
- **Unknown:** whether the 990 5G loader stages run unmodified on the 4G
  variant (same DRAM base addresses likely, but XLOADER/UCE/FASTBOOT images
  are usually built per-firmware and may differ), whether its bootrom has the
  same behavior, and whether the Harmony TP procedure applies identically.
- **Required before any attempt:** device dumps/research from a 990 4G owner,
  and a tester. If the 5G stages happen to work, support is a one-line map
  entry (or just documentation telling users to select hisi990).
- Risk profile: the upload writes stages to RAM and jumps to them; a mismatch
  typically fails safely (loader crashes, device re-enters VCOM) but this is
  not guaranteed — treat first runs as potentially hard-brick-risk and say so.

## 4. Kirin 9000 / 9000E / 9000s / 9010 — do not attempt (currently)

- Different generation entirely; the bootrom and boot chain postdate the
  990-era exploit family. There is no public evidence the TASZK bootrom
  technique or any existing `.ktl` stage applies.
- No loader blobs, no load addresses, no researcher documentation exist in
  anything upstream ships.
- Attempting blind loader uploads would be pure brick roulette.
- If community research (bootrom analysis, dumped/patched stages) ever
  emerges, revisit under the Beta track only.

## 5. Recommendation (both tracks)

- **Stable (`linux-v2.4.2`): do not attempt.** Supporting SoCs the Windows
  tool does not support would break the strictly-1:1 contract, and there is
  nothing to implement without the research artifacts anyway.
- **Beta: possible only with all three prerequisites**:
  1. community-provided research: working decrypted stage images + addresses
     (for 990 4G, evidence that the hisi990 set works is enough),
  2. a hardware tester volunteering,
  3. a guardrail amendment in `beta-potential-features.md`: **no new Huawei
     loader redistribution**. Prefer a *user-supplied loader* design (user
     drops `.ktl` files into a directory; the tool never ships them) so the
     fork does not distribute additional Huawei-derived blobs beyond what
     upstream already ships. Note the existing 18 loader sets carry the same
     legal nature; expanding the shipped set expands exposure for both repos.
- Fast Flash Loader scope stays upstream-identical (hisi980 only) on Stable;
  a Beta extension (e.g. a fast variant for other SoCs) would additionally
  require a working `fastbootf`-style stage for that SoC — same prerequisites
  as §5 above.

## 6. Summary answers

- **Is fast flash loader properly supported on Linux?** Yes — same switch,
  same code path, same blob, byte-identical; verified against the Windows
  binary and package contents.
- **Can we add Kirin 9000 / 990 4G?** Not by porting work. 990 4G is plausible
  later with community evidence + a tester (possibly already working via the
  existing hisi990 entry, untested). 9000-series needs bootrom research that
  does not publicly exist. Until then: **we don't attempt.**

---

## 7. ADDENDUM 2026-09-12 — Kirin 9000 (NOH-AN00) stock bootloader surface, live capture

First documented survey of a **Kirin 9000** stock bootloader from this fork
(Mate 40 Pro, NOH-AN00, build `NOH-AN00 4.2.0.196(C00E182R6P6)`, EMUI 14.2,
bootloader reached via `adb reboot bootloader` from manufacture mode).

Permitted (read-only) commands:

| Command | Response |
|---|---|
| `getvar devicemodel` | `NOH-AN00` |
| `getvar vendorcountry` | `all/cn` |
| `getvar rescue_ugs_port` | `UGSA` |
| `oem lock-state info` | `FB LockState: LOCKED`, `USER LockState: LOCKED` |
| `oem get-build-number` | `NOH-AN00 4.2.0.196(C00E182R6P6)` |

Blocked with `FAILED (remote: 'Command not allowed')`: `getvar all`,
`getvar product`, `version-bootloader`, `version-baseband`, `unlocked`,
`security-state`, `ptable`, `oem device-info` — i.e. the stock bootloader
whitelists a handful of identification queries and blocks everything else,
including every read/write surface Kirin Tool uses on Kirin ≤990 (`ptable`,
`oem oeminfowrite-*`, `oem dump-emmc`, rescue vars, …).

**Consequences for the §4 assessment (unchanged, now evidence-backed):**

- With FB+USER locked, no flashing or rebranding is possible through the
  stock bootloader — Kirin Tool's whole flashing/rebrand path on ≤990 assumes
  an unlocked fastboot first (VCOM loader upload), and no public loader or
  bootrom exploit exists for the 9000.
- Manufacture mode does not change this: it is a booted-OS USB configuration
  (see `usb-port-manufacture-mode.md`); its AT surface is factory-
  permission-gated and its ADB is unprivileged. Neither reaches the
  bootloader's write surface.
- Practical consequence: on a Kirin 9000 device the tool's realistic scope is
  **identification and state display** (devicemodel, vendorcountry,
  lock-state, build number all work) — the same read-only set could power a
  device-info page for currently-unsupported SoCs. Unlocking remains blocked
  on both the fastboot path (locked, write-blocked) and the VCOM path (no
  loader/exploit for the 9000 generation).

A `oem unlock` attempt was deliberately **not** performed: on a locked
stock bootloader it cannot succeed (the unlocked-fastboot loader the flow
requires does not exist for this SoC) and unlock attempts risk ARB
increment per the tool's own warnings.
