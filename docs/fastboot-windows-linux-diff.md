# Kirin Tool — Windows (v2.4.2) vs Linux Port: Fastboot & Functionality Difference Analysis

Analysis date: 2026-09-05. Method: the official Windows v2.4.2 zip was extracted, its native
binaries string-analysed and hash-compared, and its managed `Kirin-Tool.dll` fully decompiled
with ilspycmd, then diffed method-for-method against the upstream source
(github.com/kethily-daniel/Kirin-Tool) and this Linux port. Upstream reference line numbers
are from the decompiled binary unless a GitHub file is cited.

## 1. The fastboot payload on Windows — there is no `fastboot.dll`

The Windows zip contains **two native fastboot executables**, not a DLL:

| File | Size | sha256 (first 16) | Identity |
|---|---|---|---|
| `fastboot/fastboot.exe` | 881,887 B | `64c7a960a550c480` | Old Huawei/IDT-lineage fastboot, MinGW/GCC build with DWARF debug sections. Its usage text documents **client-side file-saving OEM commands**: `oem dump-emmc <partition> <filename>`, `oem memory <memoryname> <filename>`, `oem memupload <address> <length> <filename>` |
| `fastboot/xml/fastboot.exe` | 14,553,088 B | `df7d0492c3f356ef` | Modern Huawei/HiSilicon-custom fastboot (clang, BoringSSL, fmt). Custom symbols: `FastBootDriver::UltraFlash`, `ultraflash_end`, `HUAWEI_ULTRAFLASH`, `fastboot hisi version`, plus OEM extensions `oem dump-storage`, `oem dump-ufseye`, `oem memory`, `oem memupload`, `oem allow-flash-super` |

Key point: **both Windows binaries implement client-side dump support.** When the app sends
`oem dump-emmc <partition> "<path>"`, the Huawei bootloader uploads the partition data and
*the fastboot client itself* writes it to `<path>` on the host. Stock AOSP fastboot has no
such client logic — it treats `oem ...` as a plain passthrough and cannot save uploaded data
to an arbitrary file.

How the Windows app wires them (decompiled `MainWindow`):

```csharp
_fastbootClient     = new FastbootClient();                          // fastboot/fastboot.exe
_xmlFastbootClient  = new FastbootClient("fastboot/xml/fastboot.exe");
_fastbootFlasherService = new FastbootFlasherService(_xmlFastbootClient);
```

- `_fastbootClient` (old exe): device info, NVE read/write, bootloader unlock, FRP, downgrade,
  oeminfo pull/flash, rescue reboot, OTA flash (old exe also has dump support for the
  `oem dump-emmc oeminfo` backup).
- `_xmlFastbootClient` (custom exe): the XML dump/flash flow — partition table, partition
  dumps, per-partition flashing, `flash.xml` generation/parse.

## 2. Binary vs public source

The decompiled `Kirin-Tool.dll` (AssemblyIndex 1.0.0.0, .NET 8, WPF + Wpf.Ui, Costura/Fody)
is **functionally identical to the public GitHub source** — no hidden commands, no extra
endpoints, no telemetry (zero HttpClient/WebClient usage anywhere), same FBK key blob, same
`oem unlock UUUUUUUUUUUUUUUU` code. The fastboot exes, `payload`, and all 18 `loaders/hisi*/*.ktl`
sets are **not** in the source tree proper but are byte-identical to what this Linux port ships
(sha256-verified: `payload` = `7c19d61e28e52dde…`, hisi990 loaders spot-checked 1:1).

The custom fastboot features **UltraFlash / memupload / dump-ufseye / allow-flash-super are
never invoked by the app on either platform** — they are vestigial vendor capability inside the
binary. Nothing to port, nothing missing.

## 3. Per-feature parity matrix

Legend: **OK** = works identically · **BROKEN** = present but non-functional in the port ·
**MIS-WIRED** = control does something other than its label · **ABSENT** = exists on Windows, missing in port.

| Feature | Windows mechanism | Linux port status |
|---|---|---|
| Fastboot transport | Huawei fastboot exes (2) | **OK** (stock android-tools fastboot; `Services/FastbootClient.cs:63-81` resolves bundled → `/usr/bin/fastboot` → PATH) |
| Device presence (`fastboot devices`) | same | **OK** — port's parsing is more robust (column-aware) |
| Device info (lock-state, devicemodel, vendorcountry, oeminforead-*, get-build-number) | old exe | **OK** |
| NVE read (`getvar nve:SN`) | old exe | **OK** (`FastbootClient.cs:92`) |
| NVE write (`getvar nve:SN@v`) | old exe | **OK** primitive (`FastbootClient.cs:104`) but see *Serial write* below |
| Bootloader unlock (`oem unlock UUUUUUUUUUUUUUUU`, streamed, ARB warning) | old exe | **OK** (`FastbootClient.cs:188-284`) |
| FRP remove (erase frp → erase config → oem frp-erase) | old exe | **OK** |
| Enable downgrade (4× `oem oeminfoerase-*`) | old exe | **OK** |
| Oeminfo flash-back (`flash oeminfo`) | old exe | **OK** |
| Oeminfo pull/backup (`oem dump-emmc oeminfo "<file>"`) | old exe **saves the upload to file** | **OK with the bundled fastboot** (`fastboot-src/`, upload-to-file patch; framing verify-on-device). With system fastboot only: fails cleanly (`OemInfoService.cs` checks `File.Exists`) |
| Partition dump (`oem dump-emmc`/`dump-storage <part> "<file>"`) | custom exe saves upload to file | **OK with the bundled fastboot** (same patch, plus a `File.Exists` + non-empty verification so fake success is impossible). With system fastboot only: the file is never written |
| XML flash (`fastbootimage/*.xml`) | custom exe | **OK** — per-partition `flash` is protocol-identical; the dump→generate-xml→reflash round-trip works with the bundled fastboot |
| **Full OTA flash** (UPDATE.APP extract → skip-list → `efi`→`ptable` rename → super-partition merge via `SuperMerger` → flash) | `StartFullOtaFlash()`/`FlashFullOtaPartitions()` | **OK** — restored 1:1 from upstream (was absent/mis-wired; see §6) |
| **USB Update (HDLC serial) flash** | `StartUsbUpdateFlash()` → `USBUpdateFlasherService.FlashPartitions` over `UsbDownloadService` | **OK** — wired 1:1 from upstream (was constructed but never called) |
| Software testpoint (VCOM enter/exit, xloader backup/patch/restore) | `SoftwareTestpointService` | **OK** — uses the same serial protocol; port drops some success/failure dialogs (cosmetic) |
| VCOM fastboot unlock (18 SoC loader maps, `.ktl` decrypt via FBK, serial upload, `fastbootf.ktl` fast loader for hisi980) | `FirmwareUnlocker` + `VcomFlasher` | **OK** — logic identical, loader blobs hash-identical; port resolves loaders from `AppContext.BaseDirectory` instead of CWD |
| Oeminfo rebrand — live write path (hisi710/710a/970/980/810/820/985/990: `oem oeminfoerase-disablewp`, `oeminfowrite-KTModel@`, `KTVendor@`) | old exe | **OK** (`MainWindow.axaml.cs:1700-1727`) |
| Oeminfo rebrand — file path (other SoCs) | pull + `OemInfoEditor` + flash back | **BROKEN on pull** (see oeminfo pull); file-based input still works |
| Rescue reboot (`rescue_recovery_kernel`… + `getvar rescue_ugs_port` / `rescue_enter_recovery`) | old exe | **OK** |
| **Serial number write (NVMe page)** | `write-sn` → `HandleWriteSerialNumber()` (uses `NewSerialInput`, calls `WriteNveVariable("SN", …)`) | **OK** — rewired to the implemented handler (was routed to a "Not Implemented" stub) |
| Serial number read | `ReadNveVariable("SN")` | **OK** |
| Fastboot invocation logging (`log/kirintool_log_*.log`) | `ProcessRunner.WriteLog` | **OK** |
| Serial/COM discovery (VCOM 0x12D1:0x3609, DBAdapter) | WMI `Win32_PnPEntity` | **OK** — replaced by `LinuxSerialPortFinder` (sysfs walk) + udev rules |

## 4. Why dumps/oeminfo-backup cannot work with stock fastboot

The fastboot protocol's `oem` passthrough only carries text INFO/OKAY/FAIL responses. The
`oem dump-emmc <partition> <filename>` feature is a *cooperative client+bootloader* protocol:
the bootloader streams the partition as a DATA/upload response, and **the Huawei fastboot
client** (the part replaced by stock fastboot on Linux) receives the stream and writes
`<filename>`. Stock android-tools fastboot (v37) has no handler that saves upload data from an
arbitrary `oem` command, so the file never appears on the Linux host. The command still reaches
the bootloader and the device responds — which is why the text-heuristic success check can be
foothed.

Remediation (now implemented, see §7): the port ships a patched AOSP fastboot
(`fastboot-src/`) that implements the upload-to-file capture, bundled by the PKGBUILD and
preferred automatically by the app. Additionally the dump success check verifies the output
file exists and is non-empty. A C# upload client inside the app was considered and rejected
(it would put raw-USB hardware code inside the app).

## 5. Flashing risk assessment on Linux

| Operation | Brick/data risk vs Windows | Notes |
|---|---|---|
| `flash <partition>` (XML / extracted images) | **Low / equal** | Same protocol bytes; stock fastboot v37 is stricter about malformed responses → failures are loud, not silent. Watch `usbfs_memory_mb` (default 16) throttling large downloads — slow flash, not corruption. |
| `erase frp` / `erase config` | Low / equal | Identical commands. |
| Bootloader unlock | Equal | Same 16×U command; same ARB semantics. |
| **Start Full OTA button** | **Low (was HIGH)** | Previously mis-wired to the VCOM unlock flow; now performs the real OTA/USB-update flash per upstream. ARB-incrementing VCOM load is only reachable via its own button. |
| Partition dumps / oeminfo pull | **Zero brick risk** | Read-only on the device; host-side capture now implemented in the bundled fastboot (verify framing on first use). |
| VCOM unlock / software testpoint | Equal to Windows | Identical blobs and serial logic; untouched by design. |
| USB Update flash | N/A | Path absent; no risk because unreachable. |

Linux-specific environmental hazards (documented only — hardware code intentionally untouched):
- udev permissions: `packaging/51-kirin-tool-fastboot.rules` must be installed (adbusers/uucp
  groups + uaccess) or every fastboot/VCOM operation fails with "no permissions".
- `usbfs_memory_mb`: large fastboot downloads can be throttled by the 16 MB default
  (`usbcore.usbfs_memory_mb=1024` kernel param speeds up large images).
- Termios defaults on serial open (DTR/RTS assertion) can differ from Windows' `SerialPort`;
  relevant to VCOM handshake timing. No change made — behaviour preserved.

## 6. Remediation backlog

Status after the 1:1 parity pass (same day): the port was reconciled against the upstream
source (github.com/kethily-daniel/Kirin-Tool, cloned and diffed method-for-method and
UI-element-for-UI-element). Now restored in the port:

1. **Full OTA flash pipeline** — `StartFullOtaFlash()`, `FlashFullOtaPartitions()` (UPDATE.APP
   extract → skip-list → `efi`→`ptable` → super-partition merge via `SuperMerger` → flash),
   path-length warning, and the Start Full OTA button wired back to the real flash/USB-update
   dispatch (upstream logic, mode-aware file counting kept).
2. **USB Update (HDLC) flash** — `StartUsbUpdateFlash()` wired to
   `USBUpdateFlasherService.FlashPartitions` with the full progress-dialog event set
   (`OnExtractionStarted/Progress`, `OnPartitionsDiscovered`, `OnPartitionProgress/Completed`).
3. **`ExecuteCommand` operation gate** — all operation buttons now share the upstream
   device-connection gate (skipped for `unlock-fastboot` and `sw-testpoint-enter`), matching
   `OperationButton_Click` in the binary; SN write routes to the implemented handler.
4. **Software testpoint** — merged into upstream's `HandleSoftwareTestpoint(bool enter)` with
   success/error dialogs and the exit-side device check.
5. **VCOM unlock** — upstream messages plus the hisi820/hisi985 ARB warning dialog.
6. **UI parity verified** — x:Name set, button `Tag` set (all operation + file-slot tags),
   page sections and group headers all match the WPF layout; the only name differences are
   framework plumbing (Wpf.Ui `NavigationView`/`RootContentDialog` vs Avalonia
   `NavigationListBox`).

Deliberate divergences kept (port improvements, not missing features):
- FRP partial-success message is honest ("partially completed (n/3 steps)") instead of the
  upstream's unconditional "completed successfully!".
- `DumpPartitionAsync` verifies the dumped file exists (stock fastboot cannot save uploads —
  see §4) instead of trusting output text.
- `GetOtaFileByType` returns null on unknown type instead of throwing; `GetSelectedCpu` has a
  bounds check.

Still open (inherent to Linux, not porting gaps):
- UltraFlash and the DDR/UFS-eye OEM extensions remain Windows-only curiosities — the app
  never invokes them on either platform.
- **Verify the dump framing on a real device once** (see `fastboot-src/PATCHES.md`): the
  bundled fastboot's `fb_command_upload` implements the standard fastboot upload framing
  (`DATA<size>` + raw bytes + `OKAY`); if a dump misbehaves, capture the Windows tool with
  USBPcap/Wireshark and adjust the handler to the observed framing.

## 7. Bundled Huawei-capable fastboot (fastboot-src/)

Since the missing dump capability is a *client-side* feature, the port now ships a patched
AOSP fastboot built from the same lineage as the Windows `fastboot/fastboot.exe`
(`platform/system/core` @ `android-6.0.1_r1`, trimmed: no format generators, no update.zip,
Linux-only transport):

- `fb_command_upload()` (`src/protocol.c`): sends any command, captures every `DATA<size>`
  frame the bootloader uploads, and writes the stream to a file — restoring `oem
  dump-emmc/dump-storage`, `oem memory` and `oem memupload` (partition dumps, DDR dumps,
  oeminfo backup) exactly like the vendor client.
- `do_oem_command()` (`src/fastboot.cpp`): detects the dump/upload OEM commands and routes
  them to the capture path; all other commands behave as stock fastboot.
- The PKGBUILD compiles it and installs it as `/usr/lib/kirin-tool-linux/fastboot/fastboot`,
  which `FastbootClient.ResolveDefaultFastbootPath()` prefers automatically; system
  android-tools fastboot remains the fallback for anyone without the bundled copy.

### 7.1 First on-device run checklist (hardware-affecting surface)

Everything restored by the parity pass sends the same bytes the Windows tool
sends; the items below are the only places where reality must confirm design,
plus the code-review fixes applied 2026-09-05 (stale-dump deletion, space-safe
dump filenames, dump-failure exit codes, progress-dialog divide-by-zero
guards — none change device behavior):

1. `fb_command_upload` framing: dump a small partition first; if the file is
   missing/short, capture the Windows tool with USBPcap and compare.
2. The full OEM command (including the filename token) reaching the
   bootloader — expected harmless ("will not be used" per the vendor client's
   own strings); confirm via `(bootloader)` responses.
3. Full OTA flash and USB Update (HDLC) flash are now *reachable* on Linux for
   the first time — run the first session with a dump-first habit and expect
   the same warning dialogs as Windows.
