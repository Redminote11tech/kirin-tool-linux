# Portable AppImage

The portable.1 artifact replaces the CachyOS-specific stable.3 AppImage. Build in
Ubuntu 20.04 / glibc 2.31 with baseline x86-64 flags, not on the rolling-release
host. No x86-64-v3/AVX2 requirement is introduced by the native client/USB bridge.
.NET/Skia may select optional CPU optimizations at runtime. The self-contained
.NET 8 runtime is bundled; Linux desktop graphics/font libraries, ICU and OpenSSL
remain host dependencies. This does not mean literally every Linux system works.

Build example inside a Ubuntu 20.04 container with .NET 8 SDK installed:

```sh
apt-get update
apt-get install -y build-essential pkg-config libusb-1.0-0-dev zlib1g-dev python3 curl binutils
./packaging/build-appimage.sh /work/new-publish 2.4.2-stable.3-portable.1
```

Supply APPIMAGETOOL and APPIMAGE_RUNTIME paths for offline assembly. The shared
publish script builds managed/native/probe components. AppImage assembly forces
fresh native rebuilds and checks every bundled ELF for glibc <= 2.31 and higher
x86 ISA notes. libusb 1.0.30 is built from SHA256-pinned official source with the
netlink backend, avoiding host libudev/systemd dependencies. Corresponding source
and LGPL license accompany the release. Native flags are -O2 -Wall -march=x86-64
-mtune=generic. Do not bundle libc/ld-linux or replace system glibc.

AppRun sets KIRIN_APPIMAGE=1, including when run from an extracted AppDir. Only
that mode shows the existing yellow notice style with title "Apply udev rules".
The notice appears when common vendor-wide Huawei USB or VCOM desktop permission
rules cannot be confirmed. It reads /etc, /run, /usr/local/lib, /usr/lib and /lib
udev rule directories, respecting filename precedence and /dev/null masks. Rules
can be split between files; it checks content, not just a known filename. Commented
rules, other vendors, product-specific rules and unreadable effective files do not
silently establish complete mode coverage. This conservative check is not a full
udev interpreter and does not promise that the current device has permission.

Copy commands writes a self-contained script to the clipboard. It uses uaccess
without distro-specific group names, installs /etc/udev/rules.d/51-kirin-tool-fastboot.rules
and reloads rules. The user executes it in a terminal and reconnects the phone.
The app never invokes sudo, writes host rules or touches a device for this check.
It rechecks when activated or when "Check again" is clicked. Existing complete
rules hide the notice. The installed Arch app does not show this AppImage notice.
Commands remain valid after the image closes; no transient mount path is copied.

Validation includes the actual copied clipboard text, shell syntax without
execution, rule precedence/masking, visible/hidden UI states, old-glibc/limited-CPU
launch checks and packaged UI regression tests. No new phone protocol validation
is implied. Stable's existing flashing/USB validation limitations remain.
