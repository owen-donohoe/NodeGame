---
type: Direction
title: Loadout screen, selection, readability and perimeter claim bar — rework plan
description: A five-phase plan for the lobby loadout screen (T layout, variants and skins panels), paint/triple-tap selection, bigger and clearer villagers, ownership outlines and zoom dither, and a claim bar drawn around the node edge with a view-only speed remap. Written to be executed by an agent. Presentation only; nothing here touches Simulation/.
tags: [ui, lobby, loadout, input, selection, readability, claim-bar, mobile, plan]
generated: { by: claude-sonnet-5-5, at: 2026-10-10T00:00:00Z }
status: draft
# No `sources:`. Describes work that has not happened. When a phase lands, its
# content belongs in architecture.md (and game-model.md for the claim bar) and
# the section here should be deleted. Written against the code as it stands on
# 2026-10-10, branch feat/banks at 1d3e600e.
---

# Loadout screen, selection, readability and perimeter claim bar

Scope: view, input, lobby and settings only. **Nothing here changes `Simulation/`**, a
`GameCommand`, the serializer's game rules or any hash baseline. If a hash baseline moves, a
step touched the simulation by mistake and must be undone.

This supersedes the lobby-tree portion of [suit-tree-spec](suit-tree-spec.md): the tree *view*
is removed, the tree's *rule table* (`Backend/Shared/SuitTree.cs`) stays because the server
enforces it. It extends [touch-input-spec](touch-input-spec.md) (new binding slots, the settings
traps in its section 9 apply unchanged).

## 0. Why

Direction must be easy in a fast game on a small phone, especially vertically. Three problems:

1. Choosing a loadout is opaque. There is nowhere to see what a suit or district does, no
   variants view, no skins.
2. In a match, ownership, fights and selection are hard to read. Districts are uncoloured,
   enemy villagers are indistinct when zoomed out, fights need zooming to read, and selecting
   many villagers means tracing a lasso around them.
3. The claim bar is a floating half-and-half strip that says nothing about the board around it.

## 1. Decisions already made (do not re-open)

| Decision | Choice |
|---|---|
| Claim-speed visuals | **View-only remap** of the existing `claimBar`. `Simulation/` untouched. |
| Auto-centering camera | **Dropped.** Camera stays manual. |
| Order of work | **Lobby first**, then input, villagers, ownership, claim bar. |
| Loadout shape | Superseded by section 4: two suits and two districts below arena 2 (the granted pieces show as locked tiles), three suits and five districts from arena 2. |
| Locked in arena 1 | Districts: Farm, Mine, Village. Suit: Warrior. |
| "Buy another box" | **Placeholder.** No box, store or currency exists. Visible, shows a "Coming soon" toast. |

Art is out of scope. Use placeholder colour tiles and monograms (`ItemFamily`).

## 2. Findings that shape the work

- `LoadoutData` has `SuitSlots = 3`, `DistrictSlots = 2`. `DraftSerializer` and the save file
  count-prefix both arrays, and `LoadoutData.Normalized` pads or trims.
- **Every district ID in a loadout becomes a draft piece.** `DraftManager` (around line 787)
  calls `AddLoadoutNode` for each `loadout.districtIDs[i]`. Farm, Mine and Village already
  arrive through the board's base pool (`baseDraftDistrictsP0/P1` = 1, 2, 3). Storing them in
  loadout slots would double them in the draft and change match inputs. **Therefore locked slots
  are display-only in this work** (see 4.1). This revises an earlier idea of storing locked
  entries in the arrays.
- Warrior is `isGlobal`: `GameManager.BuildDraftedSuits` grants it on top of the chosen suits.
  `LoadoutCatalog.IsSuitOffered` refuses it a slot. The new "Warrior + 2 chosen" therefore means
  **chosen suit slots go from 3 to 2** and Warrior is shown locked beside them.
- The suit tree UI (removed in phase 1) was the
  UI. `Backend/Shared/SuitTree.cs` (root plus two sidegrades per suit, gated by arena and parent
  ownership) is shared with Cloud Code and stays.
- `PlayerProfile.AllContentUnlocked = true`. Unlock gating is unshipped, so every "locked" state
  in the lobby needs a data seam that works today (see 4.4).
- Claim maths (`GameSimulation.TickClaiming`, `FrontierPercent`): +25% per net owned neighbour,
  clamped to 0..2 steps; enemy neighbours only cancel the bonus and never push below 100%; the
  Fortress divides the enemy rate by 1 + resistance (25/40/50%). The bar is signed in
  `[-claimThreshold, +claimThreshold]` (5000 in the shipped asset).
- Input: `PointerGestureSource` (events `OnLassoBegin/Point/Complete`), `GestureClassifier`,
  `TapRouter`, `SelectionSystem.ApplyLasso`, `LassoGeometry`, `InputBindings`, `GameSettingsData`.

## 3. Rules for the executing agent

- Read `CLAUDE.md`, `docs/architecture.md` ("Where the UI lives") and `.claude/rules/view-ui.md`
  before editing. UI Toolkit lives under `Assets/UI/`.
- Work on a new branch off the current one (for example `feat/loadout-rework`), never `main`.
  Commit each step as it finishes. Do not merge.
- Never hand-edit `.unity`, `.prefab` or `.meta` files. UXML and USS are text and fine to edit.
  Anything needing a scene or prefab change (new HUD object, world-space prefab) goes through a
  connected editor (`docs/skills/drive-the-editor.md`) or is listed in the final report as a
  manual step.
- After each step: `dotnet test dotnet/NodeWar.sln` where the step has UnityEngine-free logic,
  and `scripts/compile-check.ps1` for everything else.
- Do not spawn sub-agents (`.claude/skills/delegation.md`).
- At commit time, if `scripts/okf-stale.ps1` reports a document in REVIEW, follow the
  re-verification rule in `CLAUDE.md`.
- `git status` currently shows unrelated modified assets (`Assets/Settings/*`,
  `Assets/UI/Fonts/*-SDF.asset`, `ProjectSettings/GraphicsSettings.asset`). Do not stage them.

---

## 4. Phase 1 — Lobby loadout screen (landed)

Phase 1 has landed on the loadout-rework branch. Its design now lives in
[architecture](architecture.md) (the `WorkshopPage` bullet, the loadout rules bullet and
the icon-context table) and in the code: `LoadoutRules`, `LoadoutCapacityPolicy`,
`LoadoutScreenModel`, `UnlockState`, `LoadoutChoices`, `LoadoutInfoModel`,
`DistrictInfoFacts`, `WorkshopInventorySession`, `SkinBandMath` and `SkinBand`. The suit
tree UI is deleted; `Backend/Shared/SuitTree.cs` and `InventoryClamp` stay. Art is still
placeholder colour tiles and monograms (`ItemFamily`).

Decisions recorded from that work, which override the earlier text in section 1:

- **Protocol 6.** `ProtocolVersion` is bumped to 6 because chosen capacities and free grants
  differ; old clients are refused. Cloud Code is redeployed by hand after merge.
- **Arena 2 and above free every slot:** three suits and five districts, no free base
  district pool and no Warrior grant. Below arena 2 the deck is two suits and two districts
  with the grants.
- **A deck may omit Warrior** at arena 2 and above. Villagers then start with no suit, and
  a Warrior can be equipped only if it was drafted.
- **Launch is blocked on an empty chosen slot** at every arena
  (`LoadoutRules.IsComplete`, rechecked by `MatchLauncher`). Home's Battle button
  reads as disabled with "Fill your loadout"; tapping it shows that toast and opens the
  Workshop.
- **Variants only for tree suits.** Districts and suits without `SuitTree` sidegrades hide
  the button; no sidegrades or server IDs were invented.
- **"Buy another box"** stays a placeholder that shows a "Coming soon" toast.

---

## 5. Phase 2 — Selection and input (`Assets/Scripts/Game/Input/`, HUD)

The runtime changes are landed. Current contracts belong in
[architecture's input layer](architecture.md#the-seven-layers),
[SelectionLasso and HUD entries](architecture.md#key-classes-per-layer), and
[the controls contract](touch-input-spec.md): paint in §5, deferred node grammar
in §§4/6, count/clear safe-area layout in §8b and settings v6 in §9.

Code entry points are `PaintGeometry`/`LassoGeometry`,
`PointerGestureSource.OnLassoBegin/Point/Complete` and `CurrentPaintRadiusPx`,
`SelectionSystem.ApplyLasso`, `SelectionLasso` with
`Assets/Resources/UI/PaintCoverage.shader`, `GestureClassifier.AdvanceTime`,
`TapRouter`, `SelectionRules`, `InputBindings` and `GameSettingsData`.
The count dock lives in `GameplayHUD.uxml`, `HUD.uss` and
`GameplayHUDController`; preserve `hud-selbar-dock`, `hud-selbar` and
`hud-selbar-count` in later phases. Pure tests live in `NodeWar.View.Tests`
and `NodeWar.Lobby.Tests`. Device acceptance remains separate from tests and
Editor compilation; a landed contract does not assert that every manual check
has been performed. Claim visuals and the future phases below are unchanged.

## 6. Phase 3 — Villager readability (landed)

The runtime changes are landed. Current contracts live in
[architecture's View layer](architecture.md#key-classes-per-layer): the `VillagerView`,
`VillagerHealthBar`, `VillagerTiltMath`, `VillagerFlash` and `VillagerSwingMath`
entries cover the 1.35x size, the shared 20 degree world-sprite tilt, the damaged-only
HP bar, the damage flash and the swing on the real cooldown reset.

Code entry points are `VillagerLayoutMath`, `VillagerTiltMath`, `VillagerHealthMath`,
`VillagerFlashMath` and `VillagerSwingMath` (pure, with tests in `NodeWar.View.Tests`),
and the `VillagerView` per-tick observation. A landed contract does not assert that
device or phone-sized readability checks have been performed.
## 7. Phase 4 — Ownership readability (view only)

Follows the 60-30-10 idea: 60% main colour, 30% secondary, 10% accent plus accent shape, where
the accent colour and shape belong to the owner. District palettes come with the art pass; this
phase needs only the owner colour and shape from `PlayerColors`.

1. **Territory outline:** a thin outline at each node edge in the owner's colour. Alpha is a
   function of camera zoom: clearly visible zoomed out, nearly transparent zoomed in.
2. **Enemy villager outline:** enemy villagers are outlined inside their node (`OutlineDriver`,
   `OutlineStyle`; the numeric order of `OutlineStyle` is the priority order).
3. **Zoom-out dither:** a dither texture in the owner's colour over the district art and *under*
   villagers, fading out as the camera zooms in. Decide between a per-node decal and a URP
   renderer feature after profiling on a phone-class device.
4. Tests: the zoom-to-alpha curve as a pure function.

## 8. Phase 5 — Perimeter claim bar

Replaces the half-and-half `NodeClaimBar`.

1. **Geometry:** the "square" is the node footprint. Progress starts bottom-middle, runs to the
   bottom-right corner, up to top-right, across to top-left, down to bottom-left, and back to
   bottom-middle. Thick, rounded stroke lying on the floor.
2. **Rendering:** node size is constant, so bake **one** rounded-square stroke (mesh or texture)
   shared by all nodes. A shader progress scalar reveals it, set through a per-node
   `MaterialPropertyBlock`. The leaning owner's colour fills from the start; neutralising
   shrinks it. No per-node meshes, no per-frame allocation.
3. **Sparks:** the leading point ("the point") emits a short-lived spark burst. Pool one
   `ParticleSystem` per actively claimed, on-screen node.
4. **Speed remap (view only):**
   - A pure function `f(progress) -> perimeter fraction`, `progress = |claimBar| / threshold`.
   - Each of the four edges has a weight. An owned neighbour across that edge raises it, an
     enemy neighbour lowers it, a Fortress pulse lowers it for the pulse's duration.
   - Weights are normalised so `f(0)=0`, `f(1)=1` and `f` is monotonic. **Total claim time is
     unchanged by construction**; only where the point is quick or slow differs.
   - The simulation's own opponent-edge effect is only "cancel the bonus"; showing a stronger
     slow-down on enemy edges is visual emphasis and must not be described as a rule.
5. **Fortress pulse:** a wave out from a healthy Fortress (and its linked nodes); while it
   crosses the point's edge that edge's weight drops. This also explains the Fortress visually.
6. **District health:** the old health segment moves to a short arc or mini bar on the
   footprint. `StructurePresentation.HealthSegment` stays as the maths.
7. **Open check before building:** a node can link to more than four neighbours (diagonals,
   Pier). Define the mapping from link direction to the four edges (nearest side) and what
   happens when two links fall on one edge. Document the choice here when decided.
8. Tests: the remap is monotonic, exact at the endpoints, normalised, and linear when all
   weights are equal; zero-weight and single-edge cases do not divide by zero.

## 9. Out of scope

- Anything under `Simulation/`, the auto-centering camera, the art pass, a real unlock system,
  a real "box" purchase or currency.

## 10. Verification (whole project)

- `dotnet test dotnet/NodeWar.sln` after each phase. Simulation hash baselines must not move.
- `scripts/compile-check.ps1` after every `Assets/UI` or `Assets/Scripts` edit.
- Hand-check in the Editor at a phone-sized game view (`docs/skills/drive-the-editor.md` if a
  ready instance exists; otherwise list the manual checks in the report):
  - **Lobby:** locks, header kept, card grouping and ordering, greyscale and navy states, the
    era notice, Variants and Skins sheets, the looping band, Home still shows two suit chips.
  - **Match:** paint select, triple-tap, no ring, centred count button, outlines and dither
    across zoom, health bars and flashes, the perimeter bar through a contested capture
    including a Fortress pulse.
- Update `architecture.md` (icon-context table, input section, UI description) and
  `game-model.md` (claim visuals) where they change, and re-verify them at commit time.
- Final report: what shipped, what needs a connected editor, the `ProtocolVersion` decision,
  and anything left open (especially 8.7).
