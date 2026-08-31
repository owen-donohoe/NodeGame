---
type: Design History
title: Design history
description: Banner and reconciliation for the v2.1 master design PDF — historical for planning purposes, still substantially the plan.
tags: [design, history, reconciliation]
generated: { by: human:DonohoeCUA, at: 2026-08-31T08:58:49-04:00 }
verified:
  - { by: claude-opus-5, at: 2026-08-31T00:00:00Z }
verified_at_commit: bc701d1
status: stable
sources:
  - id: v21-pdf
    resource: docs/design-history/NODE_WAR_MASTER_DESIGN_V2_1.pdf
    title: Node War Master Design v2.1
    author: human:DonohoeCUA
  - id: districts
    resource: Assets/Scripts/Game/Simulation/SimulationState.cs
    title: DistrictType and SuitType — the arena-tier content this doc reconciles
    last_modified: 2026-08-30T17:51:21-04:00
  - id: draft-state
    resource: Assets/Scripts/Game/Simulation/DraftState.cs
    title: DraftState — the placement draft the document does not describe
    last_modified: 2026-08-24T09:08:13-04:00
  - id: bot
    resource: Assets/Scripts/Game/Input/BotPlayer.cs
    title: BotPlayer — Phase B
    last_modified: 2026-08-29T10:56:17-04:00
---

# Design history

**`NODE_WAR_MASTER_DESIGN_V2_1.pdf` is historical. As of 2026-08-31, Notion is
authoritative for future work.** The document is preserved, not deleted — it
records where the project was heading, and much of it is still the plan.

The banner could not be added to the first page of the PDF itself (no PDF
editing tooling on this machine), so it lives here instead. The original file
was copied from `~/Downloads` unmodified.

## What changed since v2.1 was written

The document is dated August 2026 and its header reads *"Status: Phase 8
networking verified. Architectural restructure planned next."* That restructure
is done. A reconciliation against the code at commit `db19485` found:

- **Phase A (Foundation) is complete.** Two-scene architecture, `MatchConnection`
  with `DontDestroyOnLoad`, corrected initialisation order, return-to-lobby.
  All three of the document's "known bugs" are resolved.
- **Phase B (Local bot) is complete.** `BotPlayer` came in at 637 lines against
  a 200–300 line budget.
- **The draft is a different system than Part VIII describes.** The document
  specifies picking 3 node types that auto-populate designated slots. The code
  implements a turn-based manual placement draft. **The code is canon.**
- **Relay shipped before LAN discovery**, inverting the document's Phase D order.
- **Arena-tier content already exists.** Camp, Shrine, Arsenal, Sanctuary,
  Watchtower, Rampart and Market are built and balanced. Only the unlock gating
  is missing — and it is stubbed permanently open.
- **A suit system exists that the document never mentions.** Five suits with
  per-suit stats and per-district equip gating.
- **Test infrastructure exists.** The document never mentions testing at all.
- **The networking direction has changed.** The document defers a dedicated
  authoritative server indefinitely; the current plan is a migration to a
  server-authoritative hybrid.

Full reconciliation and all current planning live in Notion. See `CLAUDE.md`
for identifiers.
