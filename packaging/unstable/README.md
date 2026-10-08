# Candidate 6 local Arch package

The PKGBUILD packages a prebuilt `bundle.tar.gz` (checksum pinned). Assemble:

- `bundle/app`: clean self-contained linux-x64 publish, bundled native fastboot,
  and UsbUpdateProbe / VcomOpenProbe executable, DLL, deps and runtimeconfig.
- Root: LICENSE, NOTICE.fastboot-src, kirin-tool.png,
  native/usb-update/LICENSE as LICENSE.usb-bridge,
  docs/usb-update-userspace.md, docs/partition-extractor.md, and packaging/51-kirin-tool-fastboot.rules
  as 61-kirin-userspace.rules.

Update the archive checksum when rebuilding. Run `makepkg -df --noconfirm`.
The Arch package uses system libusb. It must not contain DKMS sources, a
kernel module, or the earlier USB Update kernel binding rule.
The unique rule filename permits installation alongside the stable package.
