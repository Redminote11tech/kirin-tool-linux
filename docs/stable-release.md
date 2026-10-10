# Kirin Tool Linux 2.4.2-stable.3

October 10, 2026. Candidate 6 revision 3 is merged into stable at the maintainer’s
request to reduce maintenance. Stable and main publish one source tree and build
path; beta remains separate. Archived candidates and old packages retain rollback
history. No newer upstream source was imported: the frozen BSL reference remains
45440d47296ef4c250fc03730259dbeebb245ebc. Preserve LICENSE and notices.

## Changes since the public stable.1 release

- OEMInfo Convert validates untouched fields without crashing.
- Linux COM1 option-driver modem-control adaptation, modal cable prompt ownership
  and bounded tty rediscovery/reopen. The user validated this VCOM sequence on a
  Mate 20 Pro / Kirin 980 using candidate 4; packet acceptance remains unchanged.
- Offline UPDATE.APP extraction from Base, Cust and Preload with partition
  selection and destination folder. Shared progress rows show saved images green,
  failure/cancellation red and unattempted images neutral. Partial files are removed.
- Vector navigation icons, bundled Inter and layout closer to the Windows UI.
- Native OEM dispatch repair, named memory, memory ranges, UFS-eye uploads and
  UltraFlash negotiation. Rejected capability probes fall back to standard flash;
  transport errors abort without replay. HUAWEI_ULTRAFLASH=0 or off disables it.
  See [native-windows-port.md](native-windows-port.md) for commands and limitations.
- USB Update uses a small userspace libusb bridge for Huawei DBAdapter ff/13/23;
  VCOM remains serial. No DKMS module is installed. See
  [usb-update-userspace.md](usb-update-userspace.md) for protocol/transport scope.
- Arch and AppImage use a shared fresh-publish script and ship native binaries,
  licenses, docs and BUILD-COMMIT. AppImage includes libusb and its license;
  host udev permissions must still be installed manually.

## Validation and limits

Hardware-free tests cover extraction/byte integrity/>4-GiB offsets, navigation,
progress/modal dialogs, USB stream behavior, discovery mocks, fastboot parsing,
actual CLI dispatch and vendor wire sequencing. Verify the extracted artifacts
against the fresh source build; release manifests identify each binary and hash.

No new physical USB test was performed for stable.3. Userspace USB Update timing,
new memory/UFS-eye/UltraFlash operations and real partition dumps remain
hardware-unvalidated. An empty/rejected NVE SN response remains unresolved.
No new loader or Kirin 990 4G/9000 support is claimed. Linux implements selected
vendor features, not all modern HiSilicon fastboot functionality. No Wine needed.

Stable retains the Windows positional SUPER merger, including potential block
placement and stale-checksum defects on unsuitable inputs. Byte parity tests do
not prove output geometry/checksum validity. Windows-style permissive response
handling remains; strict AA ACK enforcement is not introduced. Managed partition
dumps may delete an existing output before native invocation; use a new folder.

Beta’s extent merger, stricter batch validation and changed write/failure policies
are not merged. The stable label is a release channel, not universal device safety
or hardware-validation certification. Do not perform phone operations as automated
release tests. See [testing-checklist.md](testing-checklist.md).

## Install

Arch/CachyOS: install the attached kirin-tool-linux-2.4.2-9-x86_64.pkg.tar.zst
with pacman -U, close/reopen, then launch kirin-tool. This updates the stable package;
the old unstable package is separate and can be removed after migration if desired.

AppImage: chmod +x, then run kirin-tool-linux-2.4.2-stable.3-x86_64.AppImage.
FUSE2 is required for mounting, or pass --appimage-extract-and-run. It was built
and verified on CachyOS x86_64; compatibility with older distribution ABIs is not
established. The host needs graphics libraries, libudev and appropriate USB rules.
The image includes docs/51-kirin-tool-fastboot.rules; install it into
/etc/udev/rules.d, reload rules and reconnect the phone. Neither artifact installs
legacy kirin_usb_update kernel modules.
