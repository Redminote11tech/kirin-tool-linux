# Kirin Tool Linux 2.4.3

October 11, 2026. Stable and main share one source tree; beta remains separate.
The frozen Windows source reference remains 45440d47296ef4c250fc03730259dbeebb245ebc.
No newer upstream source or new loader was imported. Preserve LICENSE and notices.

## Changes

- Kirin 9000 appears as Experimental in the chipset selector. Read Info, NVE SN,
  SN / PSID and experimental Reboot to USB Update remain available. Other device
  operations show a popup with heading **Notice** and **Unsupported for kirin 9000**.
  Offline extraction and file tools remain available.
- Kirin 9000 rescue preparation expects ERECOVERY, ERECOVERY_RAMDISK and
  ERECOVERY_VENDOR. Missing required images prevent all image writes. The combined
  ERECOVERY destination `rescue_recovery` is an experimental inference, not verified
  on hardware or recovered from the signed device bootloader. Existing chips retain
  their ERECOVERY_KERNEL mapping. There is no Kirin 9000 VCOM/factory loader.
- Read SN / PSID sends `fastboot oem get-psid` and displays explicit returned fields.
  NVE SN is a separate read. A device can reject either command; USB serial and IMEI
  are never silently substituted for SN. This does not bypass firmware restrictions.
- OEM version parsing accepts Qualcomm Huawei's colonless replies and Kirin's
  leading-colon replies. Rescue progress displays the actual fastboot FAILED line,
  including lock-state and partition-length errors.
- A whole-binary static audit of both frozen Windows fastboot clients identifies
  remaining host features. See [windows-fastboot-audit-v2.4.3.md](windows-fastboot-audit-v2.4.3.md).

Retained: selected UPDATE.APP extraction with green/red progress rows, vector icons
and Inter, native vendor memory/storage/UFS-eye uploads and UltraFlash negotiation,
userspace USB Update transport and portable AppImage udev onboarding.

## Validation and limits

47 Kirin9000/PSID checks cover actual blocked UI handlers, popup heading/body,
synthetic rescue archives, process argument dispatch, missing-image preflight and
bootloader error presentation. Existing extractor, read parser, stable boundary,
udev, USB Update, OEMInfo and VCOM dialog regression checks pass. Test clients are
local stubs; no phone operations or host udev changes are part of release validation.

Successful Kirin 9000 rescue writes and USB Update entry remain unverified. Local
NOH logs showed rejected writes (`partition length get error`), not successful
support. Host changes cannot make a locked/restricted phone accept a command.
The release does not add Kirin 990 4G support or reuse a 5G loader for it.

Stable retains the Windows positional SUPER merger and existing transport response
policies; beta's extent merger and stricter batch policies are not merged.
The modern Windows host SUPER generator, A/B/dynamic partitions and other audited
AOSP features are catalogued, not claimed as newly ported.

## Install

Arch/CachyOS:

```sh
sudo pacman -U ./kirin-tool-linux-2.4.3-1-x86_64.pkg.tar.zst
kirin-tool
```

The package conflicts/replaces kirin-tool-linux-unstable, allowing removal in the
same transaction. It includes Huawei udev rules and uses distribution libraries.

Portable AppImage:

```sh
chmod +x ./kirin-tool-linux-2.4.3-x86_64.AppImage
./kirin-tool-linux-2.4.3-x86_64.AppImage
```

Baseline x86-64 and glibc 2.31+, with .NET and libusb bundled. Host desktop/font,
ICU/OpenSSL requirements still apply. FUSE2 or `--appimage-extract-and-run` is needed.
An **Apply udev rules** notice offers copyable commands when matching usable rules
are missing; the app does not run sudo or install rules itself. See
[portable-appimage.md](portable-appimage.md) for compatibility and checks.
