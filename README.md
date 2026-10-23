<div align="center">

<img src="packaging/kirin-tool.png" alt="Kirin Tool application icon" width="96" height="96">

# Kirin Tool for Linux

**Huawei firmware tools, a native Linux desktop.**

Extract UPDATE.APP partitions · Flash compatible Kirin devices · Read device information
 [![Latest release](https://img.shields.io/github/v/release/Redminote11tech/kirin-tool-linux?style=flat-square&color=5865f2)](https://github.com/Redminote11tech/kirin-tool-linux/releases/latest) [![Build](https://img.shields.io/github/actions/workflow/status/Redminote11tech/kirin-tool-linux/build.yml?style=flat-square&label=build)](https://github.com/Redminote11tech/kirin-tool-linux/actions/workflows/build.yml) [![Platform](https://img.shields.io/badge/Linux-x86__64-1793d1?style=flat-square&logo=linux&logoColor=white)](#download-and-install) [![License](https://img.shields.io/badge/source_available-BSL_1.1-8b5cf6?style=flat-square)](LICENSE)

[**Download**](https://github.com/Redminote11tech/kirin-tool-linux/releases/latest) · [Features](#features) · [Device support](#device-support) · [Documentation](#documentation) · [Report an issue](https://github.com/Redminote11tech/kirin-tool-linux/issues)

</div>

An unofficial, independently maintained Linux port of [Kirin-Tool](https://github.com/kethily-daniel/Kirin-Tool) for Huawei / HiSilicon Kirin devices. It brings UPDATE.APP firmware extraction, fastboot flashing, device information and supported loader workflows to Linux, with an Avalonia desktop interface, native icons and Inter fonts. Prebuilt **AppImage** and **Arch Linux / CachyOS packages** are available.

**Source available under the [Business Source License 1.1](LICENSE), with no Additional Use Grant.** This is not an OSI-approved open-source license. Read the license before copying, modifying or using the software; production use is restricted until the applicable Change Date.

> [!WARNING]
> Flashing, unlocking and rebranding can erase data or damage a device. Back up your data, use firmware matching your device, and check the support limits below. You are responsible for device operations; a successful host build does not establish hardware compatibility.

## Features

Version **2.4.7** brings the original Fluent icons, dismissible warning panels and icon-bearing action buttons to native Avalonia. The title bar shows the version and Linux-port credit. See the [UI comparison and limitations](docs/windows-ui-parity.md).

| Tool | What you can do |
| :--- | :--- |
| **UPDATE.APP Extractor Tools** | Open Base, Cust or Preload firmware, view every archive entry, tick the partitions you want and choose an output folder. No phone or chipset selection is needed. |
| **Familiar partition progress** | See per-partition status in the existing progress dialog: green for extracted images, red for failures, with cancellation support. |
| **Huawei fastboot** | Use the bundled native AOSP-based client with reconstructed Huawei dump/upload, memory, UFS-eye and UltraFlash extensions. No Wine is required. |
| **Device information** | Read model, vendor/country, lock state and firmware versions where the bootloader permits them. NVE SN and OEM SN / PSID are separate queries. |
| **Firmware and loader workflows** | Flash, dump, rebrand and use bootloader-unlock workflows on compatible supported devices, subject to firmware permissions and loader requirements. |
| **USB Update** | Use the Linux userspace libusb transport without a dedicated DKMS module. The VCOM loader path remains serial-based. |
| **Portable desktop app** | Run an AppImage with the .NET runtime and libusb bundled. A reusable **Apply udev rules** notice offers copyable setup commands when usable rules cannot be confirmed. |

Version **2.4.5** adds modern native fastboot host functionality, including staging/fetch, slots, fastbootd, logical partitions, ZIP/image plans and filesystem helpers. Some features are CLI functionality rather than new GUI controls. See the [coverage and validation report](docs/native-fastboot-parity-v2.4.5.md) for tested paths and remaining limits; full device parity is not established.

## Download and install

Download the prebuilt files from [GitHub Releases](https://github.com/Redminote11tech/kirin-tool-linux/releases/latest). You do not need to build the application or install a separate .NET runtime.

| Format | Download v2.4.7 | Compatibility |
| :--- | :--- | :--- |
| **Arch package** | [`.pkg.tar.zst`](https://github.com/Redminote11tech/kirin-tool-linux/releases/download/v2.4.7/kirin-tool-linux-2.4.7-1-x86_64.pkg.tar.zst) | Arch Linux and CachyOS, x86-64; native fastboot requires glibc 2.38+. |
| **AppImage** | [`.AppImage`](https://github.com/Redminote11tech/kirin-tool-linux/releases/download/v2.4.7/kirin-tool-linux-2.4.7-x86_64.AppImage) | Linux x86-64, glibc 2.31+; baseline CPU build with host desktop libraries still required. |
| **Native fastboot bundle** | [`.tar.xz`](https://github.com/Redminote11tech/kirin-tool-linux/releases/download/v2.4.7/kirin-fastboot-35.0.2-huawei-linux-x86_64.tar.xz) | CLI client, filesystem helpers, corresponding sources and notices. |

SHA256 checksums and build provenance accompany the release. Use the AppImage if you need the older glibc baseline. Neither packaged format introduces a mandatory x86-64-v3 requirement; older physical CPU GUI compatibility is not fully verified.

### CachyOS / Arch Linux

From the directory containing the downloaded package:

```bash
sudo pacman -U ./kirin-tool-linux-2.4.7-1-x86_64.pkg.tar.zst
kirin-tool
```

You can also launch **Kirin Tool** from your application menu. The package replaces the earlier `kirin-tool-linux-unstable` package in the same transaction; accept its removal when pacman prompts. Close the application before upgrading, then reopen it.

The package installs Huawei udev rules. If USB access is denied, follow the install notes and add your user to the fastboot and VCOM groups:

```bash
sudo usermod -aG adbusers,uucp "$USER"
sudo udevadm control --reload-rules && sudo udevadm trigger
```

Log out and back in after changing groups, then reconnect the phone. A Huawei-capable fastboot is included and used automatically; system `android-tools` is only a fallback if the bundled binary is missing.

### AppImage

```bash
chmod +x ./kirin-tool-linux-2.4.7-x86_64.AppImage
./kirin-tool-linux-2.4.7-x86_64.AppImage
```

If FUSE2 is unavailable, run without mounting the image:

```bash
./kirin-tool-linux-2.4.7-x86_64.AppImage --appimage-extract-and-run
```

On Arch/CachyOS you can alternatively install FUSE2 with `sudo pacman -S fuse2`.

When needed, **Apply udev rules** lets you copy commands to run in your terminal. The notice disappears when usable matching rules are detected. The application does not install host rules automatically. See [AppImage compatibility and USB setup](docs/portable-appimage.md) for host dependencies and rule-detection details.

## Device support

| Chipset / device family | Current scope |
| :--- | :--- |
| **Included Kirin 620–990 5G loader families** | Existing loader and device workflows are retained. Availability depends on the selected chipset, firmware, device mode and required cable/procedure; the range does not mean every model or operation is validated. |
| **Kirin 980** | Includes the original fast flash loader option. The user's Mate 20 Pro VCOM run provides hardware evidence for that specific setup. |
| **Kirin 990 4G** | **Unsupported.** No matching loader was added; do not substitute the 990 5G loaders. |
| **Kirin 9000** | **Experimental:** read-only information plus an experimental attempt to enter USB Update. Other device operations display **Notice — Unsupported for kirin 9000**. No factory/VCOM loader or successful rescue-flash validation is supplied. |
| **Qualcomm-based Huawei devices** | Some standard information queries can work. The Kirin loader/unlock flows do not establish Qualcomm support; firmware can reject queries or writes. |
| **Offline firmware tools** | UPDATE.APP extraction and file tools work without a connected device, including when a device operation is unsupported. |

Kirin 9000 testing on the Mate 40 Pro showed UltraFlash refusal (`Command not allowed`) and rescue writes rejected with `partition length get error`. Read SN / PSID can also be refused. These remain device-side restrictions or unresolved behavior, not fixed support claims. See the [Mate 40 Pro investigation](docs/mate40-bootloader-analysis.md).

Stable and main share one build path. Former candidate 6 features are included; the separate beta branch retains its own changes. Read the [release scope](docs/stable-release.md) before using experimental workflows.

## Extract partitions from UPDATE.APP

1. Open **Extractor Tools** in the sidebar.
2. Browse to a Base, Cust or Preload `UPDATE.APP`.
3. Tick the partitions to save, or use **Select All**.
4. Choose an output folder and click **Extract Selected**.
5. Check the per-partition results: **green = extracted**, **red = failed**.

Use separate output folders when extracting multiple archives with overlapping names. Existing files are not overwritten; duplicate names within an archive receive numbered filenames. Completed images remain if you cancel a batch. Extraction copies raw partition bytes and does not verify firmware signatures, decompress images or merge SUPER. See the [extractor guide](docs/partition-extractor.md).

## Documentation

| Guide | Details |
| :--- | :--- |
| [Release scope and known limitations](docs/stable-release.md) | Current features, experimental operations and installation. |
| [UPDATE.APP partition extraction](docs/partition-extractor.md) | Selection, output naming, cancellation and archive validation. |
| [Portable AppImage](docs/portable-appimage.md) | Build baseline, dependencies and udev onboarding. |
| [Native fastboot engine](fastboot-modern/README.md) | Source pins, prerequisites, builds and Huawei extensions. |
| [Fastboot parity and validation](docs/native-fastboot-parity-v2.4.5.md) | Verified host paths, incomplete coverage and hardware limits. |
| [Windows fastboot audit](docs/windows-fastboot-audit-v2.4.3.md) | Static analysis of the supplied Windows clients. |
| [Mate 40 Pro bootloader analysis](docs/mate40-bootloader-analysis.md) | FASTBOOT / BL2 containers and unresolved rescue behavior. |
| [Hardware test checklist](docs/testing-checklist.md) | What device validation still needs to establish. |
| [Fork provenance](docs/FORK-NOTICE.md) | Frozen upstream source and independent development. |

### Build from source

Install the .NET 8 SDK and [native build prerequisites](fastboot-modern/README.md), then build all managed and native components together. The output directory must not already exist:

```bash
git clone https://github.com/Redminote11tech/kirin-tool-linux.git
cd kirin-tool-linux
./packaging/publish.sh publish-out
./publish-out/Kirin-Tool
```

For a portable AppImage, follow the [older-glibc build environment instructions](docs/portable-appimage.md). Building on a rolling-release host does not reproduce that compatibility baseline.

### Troubleshooting and reports

Fastboot invocation logs for system installs and AppImages are available at `~/.local/state/Kirin-Tool/log/` when the application directory is not writable. A failed operation's bootloader reply is more useful than its progress color alone.

[Open an issue](https://github.com/Redminote11tech/kirin-tool-linux/issues) with the app version, distribution, device model/chipset, firmware, connection mode, attempted operation and relevant error lines. Redact serial numbers, PSIDs, IMEIs and other private identifiers before sharing logs.

## Credits and license

This Linux fork derives from upstream **v2.4.2**, distributed under BSL 1.1, and is maintained independently by **[Redminote11tech](https://github.com/Redminote11tech)**. It does not sync newer upstream source. Original copyright notices are retained.

- **Kethily Daniel & NDXCode** — original Kirin-Tool authors.
- **[TASZK Security Labs SL](https://taszk.io)** — head resend bootrom exploit.
- **[kasnria001](https://github.com/kasnria001)** — decrypted xloaders and community help.
- **Redminote11tech** — Linux port and maintenance.

The [LICENSE](LICENSE) is authoritative: BSL 1.1 permits the listed non-production uses, has no Additional Use Grant, and changes each version to **GPL-3.0-or-later** after its applicable four-year Change Date. Bundled third-party components retain their own licenses and notices; corresponding native sources accompany releases.

<details>
<summary><strong>Support the original authors</strong></summary>

Donate through [Ko-fi](https://ko-fi.com/kirintool), or use the original authors' cryptocurrency addresses:

BTC: `bc1q70khwtwdp0jtpvfmct4gu8ktktzv5rurk0kdky`

ETH: `0xF7585F5d27bB7f10558a99CBa53459Be1969b090`

LTC: `ltc1qmyuy4vqevmszxjpl3dqdtkqpg7fper5vw48ulz`

SOL: `4CrT7dSL6kTCkJU4Fu4DT1LisU4rLFwbpE5GFS5wdEcR`

USDT SPL(SOL): `4CrT7dSL6kTCkJU4Fu4DT1LisU4rLFwbpE5GFS5wdEcR`

USDT Tether (TRC20): `TD2sJbEmYa93sJhDBmTrvqmScqQSVTCfCK`

</details>

---

*Independent community software, unaffiliated with Huawei or HiSilicon and not sponsored or endorsed by the original Kirin-Tool authors. Huawei, HiSilicon and Kirin names and trademarks belong to their respective owners.*
