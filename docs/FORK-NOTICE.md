# Fork Provenance & License Position Notice

This document records where this project comes from, under which license, and
this fork's compliance position. It is informational, not legal advice.

## 1. Provenance

| | |
|---|---|
| Upstream project | https://github.com/kethily-daniel/Kirin-Tool |
| Upstream license (at fork time and as of 2026-09-05) | Business Source License 1.1 (`Additional Use Grant: None`, Change License GPL-3.0-or-later) |
| Upstream `main` as of 2026-09-05 | commit `818aa1a658225446074b9949a52ce6842f5efdca` (14-commit history) |
| This fork | https://github.com/Redminote11tech/kirin-tool-linux |
| Fork base | Upstream Kirin-Tool v2.4.2 sources, ported WPF → Avalonia for Linux by Redminote11tech |
| Fork tip (stable, strictly 1:1 faithful track) | `d3da144` on branch `linux-v2.4.2` |
| Divergent track | branch `beta` (POTENTIAL features only; see `beta-potential-features.md`) |
| Relationship disclaimer | Unofficial community port; NOT affiliated with, sponsored by, or endorsed by the upstream authors or Huawei |

The fork retains, conspicuously and in every distributed copy:

- the full BSL 1.1 `LICENSE` text with `Copyright (c) 2026 Kethily Daniel & NDXCode`
- the README license summary, upstream credits, and donation links
- the "not affiliated / not endorsed" disclaimers

The pacman package installs the LICENSE file into `/usr/share/licenses/`.

## 2. Rights this fork relies on (BSL 1.1, verbatim grant)

> "The Licensor hereby grants you the right to copy, modify, create derivative
> works, redistribute, and make non-production use of the Licensed Work."

Consequently the following are licensed acts, not infringements:

1. The fork itself and its publication on GitHub (redistribution of a
   derivative work).
2. Modification and extension of the sources (the entire Linux port).
3. Distribution of built packages (the BSL restricts *Production Use*, not
   redistribution); every copy carries the license per the display requirement.

This fork is and remains **non-production, personal, non-commercial** use:
no fees, no business use, no monetization — the one clause (`Additional Use
Grant: None` / Production Use) that would need a commercial license is
respected.

## 3. Per-version licensing — why later upstream license changes cannot reach this fork

BSL 1.1 states: *"This License applies separately for each version of the
Licensed Work and the Change Date may vary for each version."*

Therefore:

- The upstream code this fork is derived from (v2.4.2-era) is licensed under
  BSL 1.1 **permanently for those copies**. A later relicensing of upstream
  applies only to versions first distributed under the new license.
- On the Change Date (four years from first public distribution of each
  version), the BSL grant terminates and is replaced by **GPL-3.0-or-later**
  for that version — by the license's own mechanism, regardless of any
  later licensor decision.

## 4. On the reported possibility of an upstream switch to an MS-R* license

Two similarly-named Microsoft licenses exist and are frequently confused:

- **MS-RSL — Microsoft *Reference* Source License**: view-only. No
  redistribution of source *or* binaries at all. (This is the one matching
  "no redistribution".)
- **MS-RL — Microsoft *Reciprocal* License**: OSI-approved; *permits*
  redistribution, with a file-level copyleft (covered files must ship with
  their source under MS-RL).
- **MS-CL — Microsoft *Community* License**: also permits redistribution with
  project-level copyleft.

Analysis for this fork:

- Any upstream relicensing governs only **upstream versions first distributed
  under it**. It cannot retroactively revoke the BSL grant attached to the
  v2.4.2-era code this fork derives from (see §3).
- If future upstream versions were to become view-only (MS-RSL-style), this
  fork would simply stop syncing upstream and continue under BSL for the
  inherited code — with its own work (the Avalonia port, Linux serial
  discovery, packaging, the `fastboot-src` OEM-dump patch) carrying the same
  obligations until the Change Date.
- MS-RL/MS-CL on future versions would be *less* restrictive than MS-RSL and
  would permit redistribution of those versions with source.

## 5. On DMCA threats

A takedown claim requires infringement of an exclusive copyright right. For a
fork that redistributes under the license the work was distributed with —
retaining the license text, copyright notices, and disclaimers — the
distribution is licensed and therefore non-infringing. If a takedown were
nevertheless filed, the standard GitHub counter-notice process applies, and
knowingly false takedown claims carry §512(f) exposure for the claimant.

This fork's compliance evidence, for that scenario:

- this notice + the retained `LICENSE` (§1)
- per-version BSL terms and Change Date mechanics (§3)
- the non-production, non-commercial nature of the project (§2)
- public fork history with attribution in every commit message and the README

## 6. Compliance checklist going forward (both tracks)

- [x] LICENSE shipped in repo and in every package
- [x] Original copyright notices retained in all files
- [x] Upstream credits, links, and donation addresses retained
- [x] "Not affiliated / not endorsed" disclaimers prominent in README
- [x] No production/commercial use
- [ ] Re-check at the Change Date: inherited code becomes GPL-3.0-or-later;
      decide how to mark that transition in this repo
- [ ] If upstream relicenses future versions, record the date and the new
      terms here before deciding whether (and how) to sync

## 7. Scope of this document

Prepared 2026-09-05 by the fork maintainer as a factual record and good-faith
license analysis. It is **not legal advice**; for an actual takedown,
escalation, or commercial-use question, consult a lawyer.

---

## 8. ADDENDUM 2026-09-11: upstream relicensed to a view-only license

**Event.** On 2026-09-11 (today), upstream replaced BSL 1.1 with a
"Kirin-Tool Read-Only Reference License" (upstream commit `81f89f4`,
"Change license", 2026-09-11 16:32 CEST). The commit replaced the LICENSE
file, stamped a view-only license header onto every file, and included code
changes to App/MainWindow/UI files. The new license permits *viewing only*:
no copying, cloning, forking, caching, modification, derivative works, or
distribution, for commercial or non-commercial purposes, plus an explicit
AI/LLM-training prohibition and an EU TDM opt-out.

**This fork is unaffected, for three independent reasons:**

1. **The fork base was distributed under BSL 1.1.** The code this fork
   derives from is upstream v2.4.2, first publicly distributed 2026-06-28
   (upstream commit `45440d4`, last BSL-era commit `818aa1a`). BSL 1.1 —
   which grants copy/modify/redistribute/non-production-use rights — governs
   *those copies* permanently. The new license attaches only to upstream
   versions first distributed under it (i.e., post-`818aa1a` content). A
   licensor can change terms for future distributions; it cannot retroactively
   revoke the grant attached to copies already distributed.
2. **Per-version licensing is written into BSL itself:** "This License applies
   separately for each version of the Licensed Work and the Change Date may
   vary for each version of the Licensed Work released by Licensor."
3. **The Change Date mechanism is a one-way covenant:** for the inherited
   version, BSL terminates and **GPL-3.0-or-later** applies from
   **2030-06-28** (four years from 2026-06-28). That conversion is a promise
   in the license text the code was distributed under and does not depend on
   the licensor's future conduct.

**Sync policy (effective immediately):** this fork **stops syncing upstream**.
No file, diff, or content from upstream after commit `818aa1a` (2026-06-28,
BSL era) may be copied into this fork — upstream content from `81f89f4`
onward is view-only. Any future functionality this fork needs will be written
independently or sourced from properly licensed projects. The fork's
retained `LICENSE` file (BSL 1.1) is the evidence of the terms under which
the inherited code was received; it must remain in place unchanged.

**GitHub ToS note (secondary):** independently of copyright license, GitHub's
Terms of Service grant users the right to view and fork public repositories
through the service's functionality. That covers repository forking as a
platform act; it does not license building or distributing products from
post-relicensing content — which is why the sync stop above is the safe
policy regardless.

**AI restriction note:** the new license's AI/LLM training prohibition
applies to the upstream content published under it. It has no bearing on this
fork (no model training occurs here) and, notably, no such restriction
existed in BSL 1.1 for the code this fork inherited.

Checklist item from §6 ("If upstream relicenses future versions, record the
date and the new terms here") — **recorded**: 2026-09-11, Kirin-Tool
Read-Only Reference License, sync stopped.
