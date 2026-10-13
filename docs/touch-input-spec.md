---
type: Direction
title: Controls and input bindings
description: Landed controls contracts, including swept paint selection, deferred node taps, settings v6 migration and the safe-area selection count.
tags: [input, mobile, desktop, selection, settings, ux]
generated: { by: claude-sonnet-5-5, at: 2026-10-04T00:00:00Z }
verified:
  - { by: claude-opus-5-5, at: 2026-10-13T16:34:59Z }
verified_at_commit: f0a21c0d5c61571e9e99ebcef25e6ceb551ad606
status: draft
# Runtime architecture lives in architecture.md; this document records the
# controls contract and explicitly pending device checks.
sources:
  - resource: Assets/Scripts/Game/Input/GestureClassifier.cs
  - resource: Assets/Scripts/Game/Input/GestureThresholds.cs
  - resource: Assets/Scripts/Game/Input/PointerGestureSource.cs
  - resource: Assets/Scripts/Game/Input/PaintGeometry.cs
  - resource: Assets/Scripts/Game/Input/LassoGeometry.cs
  - resource: Assets/Scripts/Game/Input/SelectionSystem.cs
  - resource: Assets/Scripts/Game/Input/SelectionRules.cs
  - resource: Assets/Scripts/Game/Input/TapRouter.cs
  - resource: Assets/Scripts/Game/UI/Selection/SelectionLasso.cs
  - resource: Assets/Resources/UI/PaintCoverage.shader
  - resource: Assets/Scripts/Lobby/Data/InputBindings.cs
  - resource: Assets/Scripts/Lobby/Data/GameSettingsData.cs
  - resource: Assets/UI/Scripts/ControlsViewModel.cs
  - resource: Assets/UI/Scripts/SettingsPage.cs
  - resource: Assets/UI/Scripts/Gameplay/GameplayHUDController.cs
  - resource: Assets/UI/Layouts/GameplayHUD.uxml
  - resource: Assets/UI/Styles/HUD.uss
---

# Controls and input bindings

Scope: view, input and settings only. Nothing here touches `Simulation/`, a
`GameCommand`, or the serializer. A binding changes which *intent* is published;
the command a move order produces is identical either way. That is the reason
this cannot desync a match, and it is the same rule `GameSettingsData` already
states for every setting.

Hold-to-paint stays the default. The legacy Lasso API names and persisted
`InputAction.LassoSelect = 2` remain, with the player-facing label “Paint select”.

## 1. What already exists

Read from `PointerGestureSource`, `GestureThresholds`, `TapRouter`,
`SelectionSystem`, `CameraController`, `GameSettingsData`, `SettingsPage` and
`GameplayHUDController`.

- **A settings system.** `GameSettingsData` is a UnityEngine-free struct with a
  version (`CurrentVersion = 6`), `Normalized` migration, `Differ`, and tests in
  `NodeWar.Lobby.Tests`. It is saved through `PlayerProfile.SetSettings`.
  `SettingsPage` builds rows as a switch plus, for multi-choice values, a label
  that cycles on tap (`CycleFrameCap`). An in-match panel also reads it.
- **The "input button" is the HUD zoom handle.** `hud-recentre` in
  `GameplayHUDController.RegisterZoomHandle`: a click returns the camera to your
  core (`CameraController.RecentreOnHome`), a vertical drag zooms. It uses raw
  pointer events, so one press cannot do both.
- **Hold time is configurable**, default 0.3 s, range 0.15–1. Hold stillness is
  a separate 1.5 mm accumulated-path limit; tap slop remains 4 mm.
- **Villager taps add/remove by default.** A selectable villager under the finger
  beats the node; nearest projected centre wins, with ID breaking ties.
- **Paint captures centres, and an empty paint preserves selection.** See §5.
- **Node double/triple bindings default ON.** Singles and doubles defer; §4
  defines the node-only chain and §6 its selection/order policy.
- **No touch route for Equip or Respawn** (keyboard only, TODO for a suit picker).
  Out of scope here.

## 2. Principles

- **The player picks the trade-off.** Defaults include add/remove villager taps,
  hold-to-paint and both node selection bindings.
- **One table, one action per input.** A slot maps to exactly one action, so two
  inputs can never fight over the same trigger by construction.
- **Nothing the player can change is able to brick the game.** The tap grammar is
  locked on, Reset is always reachable, and a bad combination warns rather than
  blocks.
- **Everything stays in millimetres and seconds,** like `GestureThresholds`.

## 3. The binding table

Each row is an **input** the player can switch on or off, and the **action** it
performs. Where an input allows more than one action, the action label cycles on
tap, the way the frame-cap row does. Actions are restricted to what makes sense
for that input.

| Slot | Input | Default | Allowed actions |
|---|---|---|---|
| Tap a villager | tap on your villager | **locked on**, Add/remove | Add/remove · Replace |
| Hold + drag | hold still, then drag | ON, Paint select | Paint select · Pan |
| Drag | one finger or left button, drag from ground | ON, Pan | Pan · Paint select |
| Drag from a villager | drag starting on your villager | OFF, Order | Order (with live path) |
| Two-finger drag | two fingers moving together | ON, Pan | Pan · Paint select |
| Pinch | two fingers spreading or closing | ON, Zoom | Zoom |
| Double-tap ground | two taps on empty ground | OFF, Return to core | Return to core · Select all idle · Toggle fit/default zoom |
| Double-tap a villager | two taps on your villager | OFF, Select all idle | Select all idle · Select all on this node |
| Two-finger tap | a quick two-finger tap | OFF, Clear selection | Clear selection · Return to core |
| Double-tap + drag | tap, then tap and drag | OFF, Zoom | Zoom (one-handed) |
| Hold (no drag) | hold still on a node or villager | OFF, Open info | Open info |
| Middle-drag (mouse) | middle button drag | ON, Pan | Pan |
| Scroll wheel (mouse) | scroll | ON, Zoom | Zoom |
| Double-tap a node | two taps on the same node | ON, Select idle on node | Select idle on node |
| Triple-tap a node | three taps on the same node | ON, Select all on node | Select all on node |

**Why "Drag from a villager" is OFF by default.** It is new, and today a press that
starts on a villager and moves becomes a camera pan. Switching it on changes that.
It is the feature that gives the immediate path preview, so it is a candidate for
ON after playtesting. Either way, **pressing a villager and dragging off never
selects it** (§6).

**Locked, shown but not editable,** so the table is honest about the whole input
grammar: tap a node (order if a selection exists, otherwise inspect), tap empty
ground (clear), right-click a node (order), and anything over UI.

## 4. How overlaps resolve

One action per slot removes duplicate-trigger conflicts. The slots that *overlap
in time* are these, and each has a fixed rule:

- **Drag vs Hold + drag.** Both on: told apart by the hold timer (today's
  behaviour, and the source of the slow-drag misfire, softened in §5). Only Drag
  on: the drag starts at the slop with no timer at all, so paint bound to Drag
  is instant. Only Hold + drag on: moving before the hold completes does nothing.
  Neither on: no one-finger drag exists.
- **Villager/ground tap vs double.** Their first tap commits immediately on
  release. An enabled second-tap action replaces that second single; their
  release-to-next-press gap remains 0.3 s, with 8 mm slop. Double-drag keeps this
  existing gap and never uses node taps as its preceding tap.
- **Node single/double/triple.** The original node ID and first press position
  identify a chain. A matching next press within 8 mm and gap <= 0.22 s continues
  it. Single expires strictly after one 0.22 s gap; with triple enabled, double
  waits a further gap after its release. Third release commits triple immediately.
  `GestureClassifier.AdvanceTime` runs after presses every Update, including
  no-pointer frames; matching pressed continuations suspend expiry until resolved.
  The deferred payload carries `TargetClass`/`TargetId`, so dispatch never uses
  mutable down-target state or a fresh raycast. Intermediate releases issue no
  moves, rings or `MoveIssued` events.
- **Node binding toggles.** Both OFF: single is immediate. Double ON/triple OFF:
  double commits on second release. Double OFF/triple ON: two taps resolve to
  one single after timeout; three still select all. Both ON is the default.
  Hold, drag, changed target/UI, second finger, secondary click, cancel/disable,
  or changed bindings/hold time abandon unresolved chains without flushing orders.
- **Pinch vs Two-finger drag.** Both may run at once (midpoint moves, span
  changes). Either can be off.
- **Two-finger tap vs Two-finger drag.** Split by the tap slop.
- **Double-tap + drag vs Double-tap.** The second press decides on release or
  movement; the double-tap action fires on release only.
- **Same action on two slots** (for example Pan on Drag and on Two-finger drag)
  is allowed.

**Warnings, never blocks.** The Controls section shows a short note when:
1. no input pans the camera (the camera button still recentres);
2. no input zooms (pinch, scroll, double-tap + drag and the camera button's drag);
3. Drag and Hold + drag are both on ("told apart by timing: a slow drag can read
   as a hold");
4. two slots bind the same action redundantly.

## 5. Paint selection and hold

Hold arming keeps the configurable timer and 1.5 mm accumulated-path stillness
limit; pan suppression starts when paint arms. The brush disc is visible then,
even without drag. Tap slop remains 4 mm.

The approved radius is **5 mm on screen at every zoom**, converted with
`ScreenMetrics` (160 dpi fallback). `PointerGestureSource` snapshots density and
`CurrentPaintRadiusPx` at begin and retains them through completion subscribers.
World reach grows through camera projection when zooming out; no extra zoom
multiplier changes the physical screen radius.

One recorded point is a disc; successive points sweep inclusive capsules.
There is no implicit closing edge or polygon interior: an open U selects only
painted coverage. Decimation and the point cap define the authoritative stroke;
rendering and CPU membership use those same samples, without independent
smoothing or an unsampled selecting tail.

`SelectionSystem.ApplyLasso` projects local, alive, unconsumed villager centres,
using the transform then positioner fallback, rejects z <= 0, and checks expanded
bounds then capsule membership. Collider/sprite/touch radius is irrelevant.
Empty/invalid capture preserves selection; nonempty capture replaces it.
`SelectionLasso` stamps an opaque union mask and composites translucent tint
once with an outer outline, so retracing cannot darken overlap. The shader is
loaded from Resources (`UI/PaintCoverage`); the separate armed pulse remains.

## 6. Tap rules

| Selection | Tap on | Result |
|---|---|---|
| none | own villager | select it |
| none | node single | after the node gap, open node panel |
| none | empty | nothing |
| any | own villager, not selected | add (or replace, per the Tap a villager choice) |
| any | own villager, selected | remove |
| any | node single | after the node gap, move the whole selection unless every eligible resident is selected |
| any | node double | replace with idle local residents on this node; no order or ring |
| any | node triple | replace with all eligible local residents on this node; no order or ring |
| any | empty | clear selection and close panel (unchanged) |
| any | enemy villager | falls through to the node beneath (unchanged) |

**Villager selection commits on release, never on press.** A press that starts on a villager
and moves past the slop is not a tap, so the villager stays unselected, whatever
the drag then does (pan, or nothing if Drag is off). The touch-down flash is
feedback only and must not look like a selection. This is already how tap works
(`OnTap` fires on release within the slop); the rule is here so the drag-from-villager
slot cannot break it.

Tap resolves villager-first, so a tap cannot order onto a node while your own
villager is under the finger. **Drag from a villager** fixes it when enabled,
because a drag release resolves nodes only (the `nodesOnly: true` path right-click
already uses). A drag that ends on the node it started from is treated as a tap,
so a rolling thumb does not lose the tap. Pick the villager whose centre is
nearest the touch, not the first raycast hit.

Residents are local, alive, unconsumed and non-Moving with that `currentNodeID`;
double additionally requires `Idle`. Moving arrivals, dead/consumed/enemy villagers
do not qualify. Output is ordered by ID. A nonempty resident set entirely selected
makes a node single a selection-preserving no-op, including with extra selections.
Empty resident sets do not qualify; partial coverage still moves. The router
revalidates node ID at dispatch; a real move uses the current issuing tick and
the existing `IssueMoveTo` path. Right-click/order-drag retain their order behaviour.

## 7. Order drag (the optional slot)

- Only a villager that is **already selected** can start an order drag, and it
  drags the whole selection. Pressing an unselected villager and dragging does not
  select it and does not order anything; that drag is a pan (or nothing, if Drag is
  off). Orders therefore always start from a deliberate tap.
- While dragging, draw the route to the node under the finger with `PathCurve`,
  from a read-only `Pathfinding.FindPath`, recomputed when the hovered node
  changes. Draw it as "pending" until the command commits. This is the immediate
  feedback that does not wait out the lockstep delay.
- Release on a node issues the order through `IssueMoveTo`; release on empty ground
  cancels and keeps the selection.

## 8. Camera button

The existing HUD zoom handle, made optional. Two toggles:
- **Show camera button** (default ON). Off hides the control and removes its hit
  area, so it stops swallowing touches (a press over UI is `Blocked`).
- **Drag to zoom** (default ON). Off leaves a plain return-to-core button.

The return-to-core click cannot be turned off on its own, since a button that does
nothing is worse than none. The zoom readout still appears during any zoom, from
any input.

## 8a. Controls side (left-handed)

A **Controls side** setting, Right or Left, default **Right**, which is where the
button sits today (`.hud__recentre-dock` in `HUD.uss`: `right: 12px; bottom: 38%`).
It mirrors the camera button. The selection count stays bottom-centre within the
safe rectangle. It does not touch the zoom readout (centred), the node sheet
(full width) or the opponent's side of anything.

Implementation is one USS class on the HUD root (for example `hud--left`) that
swaps `right: 12px` for `left: 12px` on those elements, rather than a second
layout. It sets at startup and when the setting changes, with no per-element code.
Respect the safe area on the chosen edge, since a notch or rounded corner can sit
under the left thumb as easily as the right.

## 8b. Selection bar

The count/clear button sits bottom-centre within the safe rectangle, with a
12-panel-unit bottom gap. Stable names are `hud-selbar-dock` (picking Ignore),
`hud-selbar` and `hud-selbar-count`. The controller converts left/right/bottom
safe insets to panel units and refreshes them on size/inset changes. Controls side
does not mirror this dock. Empty or **Show selection bar** OFF (default ON) gives
display:none and no hit area. The count gives way to X on hover/press; captured
pointer release inside clears, while drag-out/cancel does not. Ground still clears.

## 9. Data, persistence and the traps in it

- **Where.** `GameSettingsData.CurrentVersion = 6`. Controls fields include an `inputBindings`
  array indexed by slot (each entry an `enabled` flag and an action index),
  `holdTime`, `showCameraButton`, `cameraButtonZoom`, `controlsSide` (0 Right,
  1 Left) and `showSelectionBar`. The table, defaults, allowed
  actions and the warning check are UnityEngine-free so `NodeWar.Lobby.Tests` can
  cover them, as it does the existing settings.
- **Append-only IDs.** Slots 0–12 stay unchanged; `DoubleTapNode = 13` and
  `TripleTapNode = 14` make `SlotCount = 15`. Both default ON and are single-action
  rows: existing `SelectAllIdle = 7` and `SelectAllOnNode = 9`, routed node-locally.
  `LassoSelect = 2` remains persisted with the “Paint select” label. No action
  renumbering; settings v6 is independent of network protocol v6.
- **Migration.** Version 0 becomes full defaults. Pre-v5 saves receive the
  existing controls defaults; v5 keeps all old entries, including disabled/custom
  actions, while missing node slots are appended with defaults. Normalization
  clones the array, repairs unknown actions to the slot default without enabling
  disabled unlocked slots, and is idempotent. Existing v6 OFF node slots stay OFF.
  Other preferences, including audio, are preserved subject to normal clamping.
- **Capture/Differ.** `SettingsPage.Capture` already copies `current` and clones
  `inputBindings`; unrelated page edits retain controls without sharing arrays.
  `GameSettingsData.Differ` already compares bindings. Keep both guarantees when
  adding future slots; P2 needed no extra scalar setting or capture rewrite.
- **Trap 3: bindings are per device.** A phone's table is wrong on a desktop. I
  did not verify whether `PlayerProfile` syncs between devices. If it does, bindings
  must be stored per device class or kept out of the sync.
- **Zero is not off.** The struct's own comment warns that a zeroed struct is
  indistinguishable from "everything off". Padding by version covers it; add a test
  that a version-0 and a version-4 save both open with the full default table.

## 10. Settings UI

A **Controls** section in the Settings page, with the same switch-row pattern and
a compact copy in the in-match panel so a bad combination can be undone mid-match.

```
CONTROLS
 Hold + drag         [ON ]  Paint select  ›
 Drag                [ON ]  Pan camera    ›
 Two-finger drag     [ON ]  Pan camera    ›
 Pinch               [ON ]  Zoom
 Drag from villager  [OFF]  Order
 Double-tap ground   [OFF]  Return to core ›
 Double-tap villager [OFF]  Select all idle ›
 Double-tap node     [ON ]  Select idle on node
 Triple-tap node     [ON ]  Select all on node
 Two-finger tap      [OFF]  Clear selection ›
 Double-tap + drag   [OFF]  Zoom
 Hold (no drag)      [OFF]  Open info
 Tap a villager      [always] Add / remove ›
 Camera button       [ON ]
   Drag to zoom      [ON ]
 Selection bar       [ON ]
 Controls side       Right ›
 Hold time ───●──── 0.30 s
 ⚠ (any warnings from §4)
 Reset controls to default
```

Rows whose input has one allowed action show it as plain text, not a cycler.

## 11. Code map and tests

| File | Change |
|---|---|
| `Lobby/Data/GameSettingsData.cs`, `InputBindings.cs` | settings v6, append-only node slots, preserving padding and `Differ` |
| `UI/Scripts/SettingsPage.cs` | Controls section; `Capture` copies `current` |
| `Input/PointerGestureSource.cs` | read bindings; new triggers (double tap, two-finger tap, double-tap + drag, hold); the stillness rule; two-finger pan |
| `Input/SelectionSystem.cs`, `SelectionRules.cs`, `TapRouter.cs` | add/remove, node residents, no-op fully-selected node, empty-paint preservation |
| `Input/PaintGeometry.cs`, `LassoGeometry.cs`, `UI/Selection/SelectionLasso.cs`, `Resources/UI/PaintCoverage.shader` | shared open-capsule coverage and single-composite rendering |
| `Input/CommandSystem.cs` | order-drag preview hook |
| `Core/CameraController.cs` | recentre and fit/default zoom intents; desktop middle-drag and scroll still read directly |
| `UI/Scripts/Gameplay/GameplayHUDController.cs` | camera-button toggles; apply `hud--left`; selection bar |
| `UI/Styles/HUD.uss`, `UI/Layouts/GameplayHUD.uxml` | camera mirroring; centred non-picking safe-area count dock |

The classifier is UnityEngine-free and driven by synthetic pointer traces. Pure
View/Lobby tests pin paint membership, node deadlines/cancellation/toggles,
resident eligibility and settings migration. XML tests check required HUD names
and picking declarations, without claiming layout or device input verification.

**Tests that matter most:**
1. *Preserve non-node traces.* Villager/ground timing and existing double-drag
   remain unchanged; node traces deliberately pin the deferred 0.22 s grammar.
2. *Toggle safety.* Existing pointer-slot combinations and the four node-binding
   combinations must stay valid, return to idle and retain their defined grammar.
3. *Padding and migration* for versions 0, 4, 5 and 6, including disabled/custom
   old entries, disabled new slots and unknown actions.
4. *`Capture` carries every new field.*

The implementation is landed. Future edits must preserve the contracts above;
Editor/device checks cover rendering, layout and delivery beyond pure tests.

## 12. Unverified

- Whether `PlayerProfile` syncs across devices (§9, trap 3).
- Physical-device feel of the 1.5 mm hold-stillness figure and 0.22 s node gap.
- Physical safe areas/notches, all interface scales, sheet overlap, brush edge
  correspondence across zoom/seat/perspective, point cap/resize/cancel and phone
  allocation/fill cost require separate device/manual evidence.
- Real villager slot spacing in millimetres; if slots overlap within 8 mm,
  nearest-pick is required.
- Where the camera button sits and whether it competes with the node sheet.
- How the in-match panel hosts a list this long.

## 13. Decisions needed

Resolved:
- **Drag from a villager** is OFF by default, and a press on a villager that drags
  off never selects it.
- **Mouse defaults** stay left-drag pans, hold-drag paints.
- **Left-handed side** is in scope (§8a).

The count/X button and full in-match Controls/Reset are implemented. Phase 2
fixes the 5 mm screen radius, 0.22 s node gap, both node bindings ON and the
fully-selected-node no-op policy. Device checks above remain verification work.
