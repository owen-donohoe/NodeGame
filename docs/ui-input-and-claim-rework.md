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
| Loadout shape | 3 suit slots and 5 district slots, permanently. In arena/era 1 only 2 districts and 2 suits are selectable. The rest are locked. Locks lift as eras advance. |
| Locked in arena 1 | Districts: Farm, Mine, Village. Suit: Warrior. |
| "Buy another box" | **Placeholder.** No box, store or currency exists. Visible, opens a "coming soon" toast. |

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
- The suit tree: `SuitTreeView`, `SuitTreeModel`, `SuitTree.uss` and the Workshop wiring are the
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

## 4. Phase 1 — Lobby loadout screen (`Assets/UI/`)

### 4.1 Slot model (first; the rest sits on it)

UnityEngine-free, so `NodeWar.Lobby.Tests` can cover it.

1. Add `LoadoutRules` (new file beside `LoadoutEditor.cs` or in `Assets/Scripts/Lobby/`):
   `LockedDistricts(int arena)` and `LockedSuits(int arena)` returning lobby IDs. Arena 0/1:
   `node_farm`, `node_mine`, `node_village` and `suit_warrior`. The lists shrink in later arenas
   until all five district slots are selectable. Take the arena from the player's rank
   (`RankRecord.Arena`) with a safe fallback to arena 1 when offline.
2. **Chosen counts:** `LoadoutData.SuitSlots` 3 → 2. `DistrictSlots` stays 2. The *displayed*
   rows are `locked + chosen` = 5 districts and 3 suits. When a lock lifts, the chosen count
   rises with it (a later change to `SuitSlots` / `DistrictSlots`, deliberately an edit to
   constants as the existing comment says).
3. `LoadoutData.Normalized` already trims. A save with three chosen suits keeps the first two.
   Confirm the dropped third suit leaves nothing orphaned in `suitEras`.
4. `LoadoutEditor` is unchanged apart from counts. Locked slots are not in it at all, so equip,
   clear and `DropUnavailable` cannot touch them. `LoadoutCatalog.IsSuitOffered` still refuses
   Warrior a chosen slot.
5. `HomePage` builds `suitChips` from `LoadoutData.SuitSlots` and queries
   `home-loadout-suit-N` in `HomePage.uxml`. Dropping to two chosen suits leaves a missing
   element. Update the UXML to two suit chips (and its comment about five chips) or Home breaks.
6. Wire safety: peers on old and new builds disagree about suit count. `LoadoutData.Normalized`
   reconciles it, but decide whether `ProtocolVersion` should bump (see
   `Backend/Shared/ProtocolVersion.cs`, `InputSerializer`). Default: **do not bump**, state the
   reasoning in the commit, and flag it in the final report.
7. Tests to change or add in `dotnet/NodeWar.Lobby.Tests`:
   `LoadoutCompatibilityTests.DeckCounts_Unchanged` (asserts 2 and 3), `LoadoutEditorTests`,
   `LoadoutWireTests`, `LoadoutEraTests` (they derive from the constants, so most follow).
   Add: lock lists per arena, old three-suit save migration, serializer round trip at 2 suits.

### 4.2 Layout (the approved mockup)

Phone portrait. Top to bottom:

```
+-------------------------------------+
| (avatar) username          trophy N |   header: KEEP existing username + trophy
+-------------------------------------+
| Districts [Farm][Mine][Vil][ A ][ + ]|   5 slots, art tile + name each
| Suits     [Warrior][ B ][ + ]        |   3 slots
+-----------------------+-------------+
| INFO (left, 54%)      | [Dist|Suits]|   segmented tab
|  preview (idle)       | Unlocked    |
|  name                 |  [card][card]|   2-column cards, art pops over top
|  stats / histogram    |  ...        |
|  description          | Not unlocked|
|  ! unlockable in era N|  [card][card]|
|  [ Use / Selected ]   |  ...        |
|  [Variants] [Skins]   |             |
+-----------------------+-------------+
```

- **Header:** the existing username and trophy count in the lobby chrome are retained on this
  page and above both panels. Do not rebuild them. Check how `LobbyChrome` / `TrophyBarLogic`
  and the other pages present them, and keep the Workshop inside that chrome.
- **Slots:** each shows a small art tile plus name. Locked slots are dimmed with a lock icon and
  ignore taps. Empty chosen slots show a "+" tile. Tapping a filled chosen slot unequips.
- **Cards (right column):** the segmented tab switches Districts / Suits. Each card shows the
  defining art popping slightly above the card top, clipped so it never enters the next card.
  Two sections: **Unlocked**, then **Not unlocked**. Within *Not unlocked*, items that can be
  unlocked now come before items that cannot. Tapping a card previews it in the info pane; it
  does not equip.
- **Info pane (left):** idle preview (a district, or a villager wearing the suit), name, stats,
  description, then buttons stacked at the bottom, in thumb range:
  1. **Use** / **Selected** / **Not owned** (full width, ~38 px tall).
  2. **Variants** and **Skins**, side by side.
- **Stats:** suits show a horizontal histogram of each stat's delta from the BASE villager
  (zero line in the middle, better to the right, worse to the left) plus a short description.
  Districts show the same facts the in-game node sheet shows plus a description. Reuse the
  `NodeSheet*Content` data helpers and `DistrictFallback.Describe` rather than writing text.
- **Visual states:** unowned items are greyscale in card and preview. Items that cannot yet be
  unlocked show a dark navy tile in the preview. Tapping **Not owned** when the unlock
  qualifications are met does nothing. Otherwise the pane shows
  `! This is unlockable in era N` (N from the arena gate in `SuitTree` / the catalog).
- **Selected card** has a 2 px accent border. No per-card colours beyond the placeholder family
  colour.

### 4.3 Variants and Skins panels

Both open as a **bottom sheet inside the same frame** (not a new page), so the player keeps
their frame of reference. The header and loadout rows stay visible above.

**Variants** (stacked vertically):
1. Large preview image.
2. Name ("Guardian, variant 1").
3. Stats histogram / district facts.
4. Description and the `! This is unlockable in era N` notice when locked.
5. A horizontally scrollable strip of variants: owned first, then locked ones greyed.
6. **Back** (left) and **Use** (right) as tall buttons (~46 px) at the bottom, spaced well apart
   for thumbs.

Variants are **not era-specific**. A locked variant shows the same notice; if the unlock
qualifications are met, tapping does nothing (no unlock flow yet).

**Skins** (stacked vertically, top to bottom):
1. Large preview of the item wearing the chosen skin.
2. Status line: owned, or locked.
3. **"Buy another box"** call-to-action (placeholder, see section 1).
4. At the bottom, an icon **band**: a horizontal row of skin icons that loops endlessly in both
   directions and runs off both screen edges (not a circle). The centred icon is larger and
   outlined; others shrink and fade with distance. Drag to scroll, tap an icon to centre it,
   release snaps to the nearest skin. The preview above shows the centred skin.
5. **Back** and **Use**.

Skins apply to suits *and* districts. Existing data: `LoadoutData.skinIDs` and catalog skin IDs
(`LoadoutTypes.WithEquipment`). Locked skins render greyscale; **Use** reads "Not owned".

Band implementation (UI Toolkit): one container element with a pointer-drag scroller. Keep about
13 reusable child elements and lay them out by `index - position` with modulo wrap over the skin
list, recycling as it moves, so the cost is constant. No per-frame allocation.

### 4.4 Data seams while unlock gating is unshipped

`PlayerProfile.IsSuitUnlocked` / `IsDistrictUnlocked` return true. The screen must still be
correct when they do not. Add one UnityEngine-free `UnlockState` per item
(`Owned`, `CanUnlock`, `CannotUnlock`, plus `UnlockEra`) computed in one place from the profile,
the server `PlayerState.Inventory.OwnedVariants` and `SuitTree`. Everything in the screen reads
that, so ordering, greyscale/navy and the era notice need no further plumbing later. Until real
gating exists, expose a debug override so the locked states can be seen in the Editor.

### 4.5 Remove the tree

Delete `SuitTreeView.cs`, `SuitTreeModel.cs`, `Assets/UI/Styles/SuitTree.uss`, the Workshop
tree button / overlay in `WorkshopPage.uxml`, the `SuitTree` icon context and its
`LobbyIconUsage` entries, and tests that exist only for them. Keep `Backend/Shared/SuitTree.cs`
and `InventoryClamp`. Variant equip keeps using the existing server path (`EquipFromTreeAsync`
in `WorkshopPage`); rename it to something not mentioning the tree. Update `docs/architecture.md`
(its icon table lists `SuitTree` contexts) and the `UIArtTheme` contexts note.

### 4.6 Files

`Assets/UI/Scripts/WorkshopPage.cs` (rewrite), `Assets/UI/Layouts/WorkshopPage.uxml`,
`Assets/UI/Styles/Workshop.uss`, `LoadoutCatalog.cs`, `LoadoutEditor.cs`,
`Assets/Scripts/Lobby/Data/LoadoutData.cs`, `Assets/UI/Scripts/HomePage.cs` + `HomePage.uxml`,
`ItemFamily.cs`, new `LoadoutRules.cs` and `UnlockState.cs`, plus the deleted tree files.

---

## 5. Phase 2 — Selection and input (`Assets/Scripts/Game/Input/`, HUD)

1. **Paint select replaces the lasso.**
   - A brush circle drawn under the finger, radius derived from camera zoom (set in
     millimetres like `GestureThresholds`, converted to world). Larger zoomed out.
   - Dragging sweeps a capsule between samples (a "pill"); a press with no drag is a circle.
     Anything inside the swept area is selected, exactly the set the lasso selected.
   - Draw the swept area outlined with a translucent fill inside (positive space highlighted).
   - Reuse the events (`OnLassoBegin/Point/Complete`) and `SelectionSystem.ApplyLasso`. In
     `LassoGeometry` replace the point-in-polygon test with distance to the stroke's segments.
   - A paint that captures nobody leaves the selection untouched (the touch-input spec's rule).
   - Hold-to-paint keeps today's arming (`longPressTime`); the binding label changes from lasso
     to paint in `InputBindings` / the Controls panel.
2. **Triple-tap a node** selects all of your villagers on it. New `InputBindings` slot,
   `TapRouter` handling, `GameSettingsData` version bump with `Normalized` padding. Carry the
   new fields through `SettingsPage.Capture` and `Differ` (touch-input-spec traps 1 and 2).
   Double-tap on a district keeps selecting non-working villagers.
3. **No move ring** when a tap lands on a node whose villagers are all already selected.
4. **Count button** (the "N villagers" button shown with a selection) is centred,
   bottom-middle: `GameplayHUD.uxml` and `HUD.uss`. Keep the safe area.
5. Tests: capsule/point-to-segment maths, tap-grammar table incl. triple-tap, settings
   migration from version 4/5. All UnityEngine-free in `NodeWar.View.Tests` / `Lobby.Tests`.

## 6. Phase 3 — Villager readability (view only)

1. Bigger villagers (`VillagerView`, `VillagerPositioner`). Re-check spacing in crowded nodes
   and `VillagerTouchTarget` sizes.
2. Replace `VillagerHealthRing` with a conventional world-space health bar. Keep the breach
   ring (`VillagerView.UpdateBreachRing`).
3. White flash on damage via `VillagerFlash` / `HitFlashRouter`.
4. Attacks on each villager's own clock. First read `TickEvents` and the combat step to see what
   the simulation exposes. If there is no per-villager swing event, stagger the view animation
   per villager ID. Never write sim state to do this.
5. Tilt sprites more vertical so they read as 2D cut-outs (`SpriteOrientationOffset`,
   `Billboard`). Expose the angle as a tunable.

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
