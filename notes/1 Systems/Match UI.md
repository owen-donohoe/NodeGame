---
shell: 1
up: "[[Match]]"
down:
  - "[[Three UI stacks]]"
  - "[[GameplayHUDController]]"
  - "[[NodeSheetContent]]"
  - "[[DraftScreenController]]"
tags: [shell/1, realm/match, system/match-ui]
card: docs/systems/match-ui.md
sources:
  - Assets/UI/Scripts/Gameplay/GameplayHUDController.cs
  - Assets/UI/Scripts/Gameplay/NodeSheetContent.cs
  - Assets/UI/Scripts/Gameplay/DraftScreenController.cs
verified: ce9fb6bf
---

# Match UI

> [!question] The question
> The HUD, the node sheet with its Recruit/Upgrade buttons, the draft screen: how do they let the player act on the game while being forbidden from touching the game?

**Where you are:** [[Home]] › [[Match]] › **Match UI** (system)

## The problem

UI is where players *do* things (recruit, equip, collect, surrender), so it's the most tempting place to "just set the value". But every action must reach both machines identically ([[Commands]]). The UI also reads state that can change under it (rollback, a district captured mid-sheet), and it exists in two stacks during a migration.

## The idea

**UI reads state and shared eligibility helpers to *describe* what's possible, and acts only by enqueuing a `GameCommand` through one choke point (`NodeSheetContent.Send`). Bindings are invalidated when what they describe changes. Phase-level UI (draft, countdown) is reached through presenter interfaces so either stack can draw it.**

## How it works here

| Piece | Job |
|---|---|
| `GameplayHUDController` | UI Toolkit HUD: breach walls (always visible, "breach is the win condition"), resources, counts, banners, hold overlay, end card. "READ-ONLY, ABSOLUTELY" |
| `NodeSheet` + 4 contents | Core, Forge, Equip (Barracks/Camp/Arsenal/Sanctuary), Production (Farm/Mine/Market), plus bank and node-action content |
| `NodeSheetContent.Send` | The one way a sheet changes the game: stamp `issuedOnTick`, refuse if the node's district changed since binding, enqueue |
| `CommandEligibility`, `NodeActionModel` | "Can I recruit here, and what will it cost?", using the same helpers the processor uses |
| `DraftScreenController` | UI Toolkit draft: tap/drag/park a piece; asks `DraftManager` only "is it my turn?" and "confirm this" |
| `NodePanelManager` | Legacy uGUI tap arbiter that still decides which panel opens |

## Why this, not that

| Decision | Alternative | Why the alternative lost |
|---|---|---|
| One `Send` choke point | Each button builds and queues its own command | One place to enforce tick stamping and stale-binding refusal |
| Shared eligibility helpers | UI computes prices itself | Prices drift from the rules the processor enforces |
| Presenter interfaces for draft/countdown | Draft manager drives a concrete screen | Two stacks; neither should own phase rules |
| Refuse stale bindings | Trust that the sheet is current | Rollback or capture can change the node between tick and frame |

## What it costs later

- **Three UI trees** in the project ([[Three UI stacks]]); every change must know which runs.
- **uGUI draft is mouse-only; uGUI HUD has no hold overlay.**
- **The legacy tap arbiter stays** even when Toolkit draws everything.

## Patterns you've seen before

- 🔁 [[Presentation reads, never writes]]: stated in capitals in the HUD's header.
- 🔁 [[Hide latency with honest previews]]: the draft's parked piece is a preview until Confirm.

## Check yourself

> [!question]- You open a Village's sheet, and before you press Recruit the enemy captures it. What stops the recruit from going through for the wrong district?
> `Send` checks that the node's district type still matches the one it bound to; if not, the command isn't enqueued. Even if it were, `CommandProcessor` would refuse (wrong owner).

## Go deeper ↓

- [[Three UI stacks]]
- Scripts: [[GameplayHUDController]] · [[NodeSheetContent]] · [[DraftScreenController]]

Agent card: `docs/systems/match-ui.md`

## My understanding

<!-- Yours. AI never edits below this line. -->
