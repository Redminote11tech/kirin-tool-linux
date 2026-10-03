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
