---
type: Direction
title: Controls and input bindings — spec draft
description: A player-configurable input table (each input has an on/off toggle and an action), a Controls section in Settings, a toggleable camera button, and the tap-to-add selection rules. Defaults reproduce today's behaviour. Written against the code as it stands on 2026-10-04.
tags: [input, mobile, desktop, selection, settings, ux]
generated: { by: claude-sonnet-5-5, at: 2026-10-04T00:00:00Z }
status: draft
# No `sources:`. Describes work that has not happened. When a section lands, its
# content belongs in architecture.md and the section here should be deleted.
---

# Controls and input bindings — spec draft

Scope: view, input and settings only. Nothing here touches `Simulation/`, a
`GameCommand`, or the serializer. A binding changes which *intent* is published;
the command a move order produces is identical either way. That is the reason
this cannot desync a match, and it is the same rule `GameSettingsData` already
states for every setting.

This replaces the earlier version of this spec, which proposed removing the
hold-to-lasso and moving the camera to two fingers. The direction is now the
opposite: hold-to-lasso stays as the default, and every input is the player's
choice.

## 1. What already exists

Read from `PointerGestureSource`, `GestureThresholds`, `TapRouter`,
`SelectionSystem`, `CameraController`, `GameSettingsData`, `SettingsPage` and
`GameplayHUDController`.

- **A settings system.** `GameSettingsData` is a UnityEngine-free struct with a
  version (`CurrentVersion = 4`), `Normalized` migration, `Differ`, and tests in
  `NodeWar.Lobby.Tests`. It is saved through `PlayerProfile.SetSettings`.
  `SettingsPage` builds rows as a switch plus, for multi-choice values, a label
  that cycles on tap (`CycleFrameCap`). An in-match panel also reads it.
- **The "input button" is the HUD zoom handle.** `hud-recentre` in
  `GameplayHUDController.RegisterZoomHandle`: a click returns the camera to your
  core (`CameraController.RecentreOnHome`), a vertical drag zooms. It uses raw
  pointer events, so one press cannot do both.
- **Hold-to-lasso is live and its hold time is already marked for settings.**
  `GestureThresholds.longPressTime` (0.3 s, range 0.15–1) carries a TODO to expose
  it in player settings as an accessibility control.
- **Hold-to-lasso's flaw is known.** In `ContinuePress`, a press that has moved
  no more than `tapSlopMm` (4 mm) *from where it started* when 0.3 s elapses arms
  the lasso, and `PanSuppressed` then locks the camera for the stroke. A slow
  drift of 3 mm counts as "still". See §5 for a fix that keeps the gesture.
- **A small dead zone.** The pan branch needs `held < longPressTime`, the lasso
  branch needs `moved <= slop`. A frame that both exceeds the slop and passes the
  timer matches neither; the stroke ends as "no gesture".
- **A tap never adds to a selection** (`TapRouter.HandleTap` → `SelectSingle`
  clears first), and **a villager under the finger beats the node under it**, so
  tapping a node that has one of your own villagers on it cannot order a move.
- **Villager picking takes the first raycast hit,** not the one nearest the finger.
- **A large lasso that captures nobody clears the selection** (`ApplyLasso`
  clears before it loops), contradicting the "no-op" intent for small strokes.
- **No touch route for Equip or Respawn** (keyboard only, TODO for a suit picker).
  Out of scope here.

## 2. Principles

- **The player picks the trade-off; the defaults reproduce today's behaviour.**
  A fresh profile plays exactly as the game does now, plus the one approved
  change (tap-to-add).
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
| Hold + drag | hold still, then drag | ON, Lasso select | Lasso select · Pan |
| Drag | one finger or left button, drag from ground | ON, Pan | Pan · Lasso select |
| Drag from a villager | drag starting on your villager | OFF, Order | Order (with live path) |
| Two-finger drag | two fingers moving together | ON, Pan | Pan · Lasso select |
| Pinch | two fingers spreading or closing | ON, Zoom | Zoom |
| Double-tap ground | two taps on empty ground | OFF, Return to core | Return to core · Select all idle · Toggle fit/default zoom |
| Double-tap a villager | two taps on your villager | OFF, Select all idle | Select all idle · Select all on this node |
| Two-finger tap | a quick two-finger tap | OFF, Clear selection | Clear selection · Return to core |
| Double-tap + drag | tap, then tap and drag | OFF, Zoom | Zoom (one-handed) |
| Hold (no drag) | hold still on a node or villager | OFF, Open info | Open info |
| Middle-drag (mouse) | middle button drag | ON, Pan | Pan |
| Scroll wheel (mouse) | scroll | ON, Zoom | Zoom |

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
  on: the drag starts at the slop with no timer at all, so a lasso bound to Drag
  is instant. Only Hold + drag on: moving before the hold completes does nothing.
  Neither on: no one-finger drag exists.
- **Tap vs Double-tap.** A tap always fires immediately; a double-tap action is
  an *addition*, never a replacement, so taps never wait. That is only safe if the
  first tap is harmless to repeat, so double-tap exists only for empty ground
  (first tap clears) and for villagers (first tap selects). It is deliberately not
  offered on nodes, where the first tap would issue an order.
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

## 5. Hold tuning, keeping the gesture

The current arming rule is *net displacement from the start ≤ 4 mm*. A hesitant
drag that drifts 3 mm over 300 ms passes it. Separate the two ideas:
- **Tap slop** stays 4 mm (it decides tap versus pan).
- **Hold stillness** becomes a tighter, separate limit, tested on the *path length
  since touch-down*, for example 1.5 mm. A slow drag exceeds it and is read as a
  pan; a deliberate hold does not.

This is a hypothesis about thumbs, not a measurement. `GestureDebugOverlay`
already exists to check it on a device. Alongside it, a **Hold time** slider
(0.15–1 s, default 0.3) in Controls closes the existing TODO.

## 6. Tap rules

| Selection | Tap on | Result |
|---|---|---|
| none | own villager | select it |
| none | node | open node panel (unchanged) |
| none | empty | nothing |
| any | own villager, not selected | add (or replace, per the Tap a villager choice) |
| any | own villager, selected | remove |
| any | node | move order for the whole selection (unchanged) |
| any | empty | clear selection and close panel (unchanged) |
| any | enemy villager | falls through to the node beneath (unchanged) |

**Selection commits on release, never on press.** A press that starts on a villager
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

A lasso that captures nobody leaves the selection untouched.

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
It mirrors every control that is placed for a thumb: the camera button and the
selection bar below. It does not touch the zoom readout (centred), the node sheet
(full width) or the opponent's side of anything.

Implementation is one USS class on the HUD root (for example `hud--left`) that
swaps `right: 12px` for `left: 12px` on those elements, rather than a second
layout. It sets at startup and when the setting changes, with no per-element code.
Respect the safe area on the chosen edge, since a notch or rounded corner can sit
under the left thumb as easily as the right.

## 8b. Selection bar

This was in the first draft of this spec and is restored. A persistent strip, in
thumb reach on the controls side, never appearing over the board: the selected
count, small suit icons, and an **X** that calls `ClearSelection`. It exists only
while something is selected. **Show selection bar** is a toggle, default ON.
Tapping empty ground still clears. Equip and Respawn, which are keyboard-only today,
would live here when they get a touch route.

## 9. Data, persistence and the traps in it

- **Where.** Fields on `GameSettingsData`, version 4 → 5: an `inputBindings`
  array indexed by slot (each entry an `enabled` flag and an action index),
  `holdTime`, `showCameraButton`, `cameraButtonZoom`, `controlsSide` (0 Right,
  1 Left) and `showSelectionBar`. The table, defaults, allowed
  actions and the warning check are UnityEngine-free so `NodeWar.Lobby.Tests` can
  cover them, as it does the existing settings.
- **Migration.** An older save has no array. `Normalized` pads missing or short
  arrays with the defaults, and an unknown action index falls back to that slot's
  default rather than to the nearest value, the same rule `ClampFrameCap` uses. A
  new slot added later is a version bump that pads the same way.
- **Trap 1: `SettingsPage.Capture` rebuilds the struct field by field.** Its own
  comment says a field edited elsewhere "is carried through, or the first lobby
  change would reset both to false." Every new field here has the same hazard. Change
  `Capture` to copy `current` and overwrite only what the page edits, so new fields
  are carried by default.
- **Trap 2: `Differ` compares field by field.** It must compare the array, or a
  binding change is never written to disk.
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
 Hold + drag         [ON ]  Lasso select  ›
 Drag                [ON ]  Pan camera    ›
 Two-finger drag     [ON ]  Pan camera    ›
 Pinch               [ON ]  Zoom
 Drag from villager  [OFF]  Order
 Double-tap ground   [OFF]  Return to core ›
 Double-tap villager [OFF]  Select all idle ›
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
| `Lobby/Data/GameSettingsData.cs` | v5 fields, padding, `Differ`; new UnityEngine-free `InputBindings` (table, defaults, validator) |
| `UI/Scripts/SettingsPage.cs` | Controls section; `Capture` copies `current` |
| `Input/PointerGestureSource.cs` | read bindings; new triggers (double tap, two-finger tap, double-tap + drag, hold); the stillness rule; two-finger pan |
| `Input/SelectionSystem.cs`, `TapRouter.cs` | add/remove, select-all actions, lasso captures-nobody |
| `Input/CommandSystem.cs` | order-drag preview hook |
| `Core/CameraController.cs` | recentre and fit/default zoom intents; middle-drag and scroll through the gesture source (issue #41, B1) |
| `UI/Scripts/Gameplay/GameplayHUDController.cs` | camera-button toggles; apply `hud--left`; selection bar |
| `UI/Styles/HUD.uss`, `UI/Layouts/GameplayHUD.uxml` | `hud--left` mirrored rules; selection bar element |

**Extract the classifier** into a UnityEngine-free class driven by synthetic
pointer traces, as `InputDelayController` is. The input rules become traces: slow
drag, hitch frame, stacked press, double-tap with a wobble.

**Tests that matter most:**
1. *Defaults reproduce today.* Golden traces through the old and new classifier
   with the default table must publish the same events. This is the regression
   net for the whole refactor.
2. *Exhaustive toggles.* For every on/off combination of the ten slots (1024), the
   classifier never throws, always returns to idle on release, and the tap grammar
   still works.
3. *Padding and migration* for versions 0 to 4, and an unknown action index.
4. *`Capture` carries every new field.*

**Order:** data model and validator (pure C#, no risk) → classifier extraction with
the default table and the golden-trace test → the new triggers → the Controls UI
→ the camera button toggles.

## 12. Unverified

- Whether `PlayerProfile` syncs across devices (§9, trap 3).
- The 1.5 mm hold-stillness figure.
- Real villager slot spacing in millimetres; if slots overlap within 8 mm,
  nearest-pick is required.
- Where the camera button sits and whether it competes with the node sheet.
- How the in-match panel hosts a list this long.

## 13. Decisions needed

Resolved:
- **Drag from a villager** is OFF by default, and a press on a villager that drags
  off never selects it.
- **Mouse defaults** stay as today (left-drag pans, hold-drag lassos).
- **Left-handed side** is in scope (§8a).

Open:
1. Does the selection bar (§8b) have the right contents, or should it be only the
   count and the X?
2. Should the in-match panel carry the full Controls list, or only Reset and the
   side toggle?
