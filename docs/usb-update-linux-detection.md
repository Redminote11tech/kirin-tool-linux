> Historical candidate 1–4 implementation. The DKMS code and separate unstable
> recipe are archived in Git and no longer included in stable.3. For current setup
> use [usb-update-userspace.md](usb-update-userspace.md). Commands below describe
> the retired implementation, not current installation instructions.

# USB Update interface discovery investigation

The Oct 3 Mate 20 Pro capture reports Huawei 12d1:107e with interfaces
ff/13/21, ff/13/22, ff/13/23, ff/42/01 (ADB), and ff/48/01 (HDB).
The first three have no interface string and no bound kernel driver.
Values above are hexadecimal; lsusb prints subclass 19 and protocol 35
for the DBAdapter interface.

Huawei's hw_cdcacm.inf extracted for inspection from the locally available
HiSuite_14.0.0.370_OVE.exe maps USB\\VID_12D1&Subclass_13&Prot_23 to
DBAdapterDeviceDesc = "DBAdapter Reserved Interface". No driver files or
newer Kirin upstream code are incorporated. The frozen Windows Kirin
implementation selects this driver-provided friendly name.

Linux now accepts ff/13/23 on an existing Huawei USB serial port in addition
to its previous descriptor-name match. Device vendor filtering still happens
before interface matching. It does not select by PID alone or choose ADB,
HDB, modem, PCUI, or storage interfaces by this new identity predicate.

The isolated candidate now includes drivers/kirin-usb-update, a narrowly
matched GPL serial adapter using Linux's generic bulk transport. The application
still uses System.IO.Ports. No flashing protocol or ACK changes were made.
The byte range from DoHandshake through the end of UsbDownloadService remains
identical to stable a7bbc80. Stable and beta working trees are unchanged.

32 stable checks pass; the driver identity/layout predicates pass exhaustive
subclass/protocol and endpoint-count checks. Module build succeeds for the
installed 7.2.6-1-cachyos-bore-lto kernel. Hardware attachment and SerialPort
open/close remain pending. See the adapter README for the staged no-write probe.
This is an isolated candidate, not a published or validated stable release.

The candidate also fixes the blank partition list: Avalonia DataContext now
binds after PartitionItems has been initialized. Completion keeps the caller's
USB Update success message. Headless tests reproduced the empty list before
this change and verify three rendered rows, success/failure text and controls
for both constructors after it. No fastboot operation was executed by the test.

A side-by-side Arch/CachyOS package is provided as kirin-tool-linux-unstable.
It includes the adapter's DKMS source and ModemManager exclusion rule, and a
self-contained `kirin-usb-update-probe` command for list/open-close verification.
It does not replace kirin-tool-linux. Driver loading requires local sudo; the
agent could not load it because sudo required the user's password.

## Candidate 2 correction and live check

Candidate 1's installed DLL reproduced the old empty-row UI failure. Source
build checks had passed but were insufficient: comparing package contents with
that stale publish directory also passed. Candidate 2 uses a clean Release
build into a fresh publish directory and runs the UI harness against the
actual published and extracted package DLLs.

A live tty also exposed a separate discovery defect: resolving
/sys/class/tty/ttyUSB0/device directly with .NET resolved its relative target
against the class alias and produced /sys/ttyUSB0. Resolve the class entry
first, then its device link. A synthetic sysfs topology regression test covers
this; the stable parity harness now reports 33 checks.

With the user's adapter loaded, the corrected no-write probe identified
/dev/ttyUSB0 and opened/closed it with the application's SerialPort settings.
No handshake or payload was written. This validates discovery and opening,
not firmware transfer, disconnect recovery, or software-testpoint success.

## Candidate 3: COM 1.0 port-open failure

After USB Update, 12d1:3609 exposes ff/ff/ff and bulk endpoints 81/01.
The host already binds option and creates ttyUSB0. An isolated open with the
old VCOM settings reproduced IOException: Broken pipe at
SerialStream.set_DtrEnable(true), before any handshake or loader write.
Opening with DTR disabled and Handshake.None succeeded on the same device.
Linux option translates modem-line changes into CDC control requests.

Only the Linux option-bound VCOM path now uses those bulk-only settings;
Windows and other drivers retain their original configuration. FindIdtDevicePort
still filters 12d1:3609. No ACK checks or loader packet routines changed.
35 parity checks pass. The VcomOpenProbe calls only Connect and Dispose;
it does not call SendStartFrame or any upload routine. Port opening success
is not validation of loader transfer or unlock success.

## Candidate 4: cable-prompt ownership and tty lifecycle

Unlock displays a modal progress window. Its cable prompt previously used an
overlay in the main owner window instead of a dialog owned by the active
progress window. The unlock callback now supplies the progress window as prompt
owner. Headless tests verify the resulting owned dialog, enabled Done/Cancel
buttons, task results and return to progress; native window-manager modality
is not simulated by those tests.

For Linux only, release the serial handle before the intermediate cable prompt.
After Done, wait up to 15 seconds for 12d1:3609 and reopen before the next loader.
Do not resend SendStartFrame or the completed null loader. Cancel exits before
reopening. Windows retains its original sequence. Packet construction and ACK
handling remain unchanged. Complete physical replug/loader continuation has
not been exercised by the agent; no loader commands were sent during this fix.
