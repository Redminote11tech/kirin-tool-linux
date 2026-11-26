# Contributing to Kirin Tool for Linux

Thanks for helping improve Kirin Tool for Linux!

This project aims to be a **faithful, reliable Linux port of Kirin-Tool** — preserving upstream behavior and wire protocols while fixing Linux-specific issues and genuine safety/correctness bugs.

---

## AI-Assisted Contributions & Audits

**AI coding, AI audits, and AI security reviews are welcome here.** They have already helped catch real bugs and polish Linux support.

What is **not** welcome is unverified, mock-only "slop" that breaks real Huawei devices or real Linux workflows. If you use an AI assistant or automated auditor to produce a PR, **you are responsible for verifying every assumption against the real codebase, real tools, and real firmware formats** before opening the PR.

---

## PR Verification Framework (Required Before Opening a PR)

Because this tool flashes low-level bootloaders and partitions on physical phones, breaking regressions can brick devices or silently break recovery workflows. Every PR should follow these rules:

### 1. Verify against real formats and real tool output — not just tiny synthetic mocks
Synthetic unit tests are great, but they can hide fatal assumptions if the mock doesn't match reality:
* **`UPDATE.APP` parsing:** Real Huawei `UPDATE.APP` headers store per-4KB-block CRC16 tables, so `headerLength` routinely exceeds 4 KB (`KERNEL` ~12 KB, `ERECOVERY_RAMDISK` ~16 KB, `PREAS` ~340 KB, `SUPER` ~2 MB). Never add arbitrary caps without checking real Huawei firmware packages.
* **`fastboot` output parsing:** Both AOSP `fastboot` and our bundled Huawei `fastboot` (`fastboot-src/src/engine.c`) print `OKAY [...]` to **`stderr`** (not `stdout`), and commands like `erase` and `flash` print `OKAY` at the **end** of the line (`Erasing 'frp'    OKAY [  0.005s]`). Never assume `stdout`-only or line-leading `OKAY`.
* **VCOM / USB Update protocols:** Do not change wire opcodes, framing, timeouts, partition ordering (e.g., `HISIUFS_GPT` before `XLOADER`), or cable-replug prompts without verified real-device logs.

### 2. Trace the full call chain, not just one helper in isolation
* Check every caller and UI state affected by your change.
* For example, `GetSelectedCpu()` returns `"none"` when the user hasn't picked a CPU in the dropdown yet — any capability check must account for default UI states and what message box the UI actually displays (`MainWindow.ChipCapabilities.cs`).

### 3. Keep scope focused and faithful to the port
* Prefer small, single-purpose PRs (one bug or one closely related group of fixes per PR).
* Open an issue to discuss large new surfaces (new CLIs, servers, protocols, packaging/CI rewrites) before implementing them — unsolicited scope expansions are likely to be closed.

### 4. Run the build and test suites, and preserve audit history
Before submitting:
```bash
dotnet build Kirin-Tool.csproj -c Release
```
Run the relevant test projects under `tests/` (`StableParity`, `UsbTransport`, `Kirin9000`, `UdevNotice`, `UsbDiagnostics`).

* If your change touches a file tracked in `tests/StableParity/boundary.json`, **append** your change note to the existing `reference` string — do **not** overwrite previous audit history.
* Do not introduce new compiler warnings over the `main` baseline.

### 5. Be honest about what was and wasn't tested
In your PR description, clearly state:
* What you verified with real hardware, real `UPDATE.APP` files, or real `fastboot` binaries.
* What was only verified via build / offline unit tests / source inspection.

Honesty saves everyone time and prevents accidental hardware regressions.
