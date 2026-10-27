---
system: input
summary: Device gestures, selection, camera control and persistent bindings that translate interaction into player intent.
sources:
  - Assets/Scripts/Game/Input/PointerGestureSource.cs
  - Assets/Scripts/Game/Input/GestureClassifier.cs
  - Assets/Scripts/Game/Input/GestureThresholds.cs
  - Assets/Scripts/Game/Input/GestureTarget.cs
  - Assets/Scripts/Game/Input/PaintGeometry.cs
  - Assets/Scripts/Game/Input/LassoGeometry.cs
  - Assets/Scripts/Game/Input/SelectionSystem.cs
  - Assets/Scripts/Game/Input/SelectionRules.cs
  - Assets/Scripts/Game/Input/TapRouter.cs
  - Assets/Scripts/Game/Input/ScreenMetrics.cs
  - Assets/Scripts/Game/Input/HitFlashRouter.cs
  - Assets/Scripts/Game/Core/CameraController.cs
  - Assets/Scripts/Game/Core/BoardFraming.cs
  - Assets/Scripts/Lobby/Data/InputBindings.cs
  - Assets/Scripts/Lobby/Data/GameSettingsData.cs
verified: 794baded
---

# Input

## Purpose
Resolve each pointer trace once into selection, camera or order intent. Keep physical thresholds and persistent bindings consistent across mouse/touch while leaving deterministic state changes to commands.

## Interface
`PointerGestureSource.Initialize(Camera)` adapts Input System/UI hits to the pure `GestureClassifier`; `ApplySettings(GameSettingsData)` updates bindings. `SetVillagerFilter`/`SetSelectedFilter` inject ownership/selection eligibility; `NodeAt(screenPosition)` resolves nodes. Events include `OnPointerDown`, `OnTap`, `OnSecondaryClick`, double taps for ground/villager/node, `OnTripleTapNode`, `OnHold`, `OnTwoFingerTap`; pan/lasso/zoom begin/update/end or complete; order-drag begin/update/end and `OnOrderDrop(int nodeID)`. Tap payload is `GestureTarget` kind/ID. Read `State`, `Thresholds`, `CurrentStroke`, `CurrentPaintRadiusPx` and `PanSuppressed`.

`GestureClassifier.ProcessFrame(IReadOnlyList<PointerSample>)` and `AdvanceTime(now)` publish `GestureEvent`s; `Cancel`, `Configure` and `ApplySettings` reset/reconfigure recognition. Samples contain time, position, pointer ID/button/phase, UI flag and target class/ID; events retain kind, position/scale, stroke points and target identity. `GestureThresholds` uses millimetres/seconds; `ScreenMetrics` converts with 160-DPI fallback. `PaintGeometry`/`LassoGeometry` supply pure stroke/membership maths.

`SelectionSystem.Initialize(state, playerID)` exposes `SelectedVillagerIDs`, `IsSelectable`, `TapVillager`, `SelectSingle`, `SelectAllIdle`, node-local idle/all selection, `AreAllResidentsSelected`, `ApplyLasso` and `ClearSelection`. Bind transforms/positioners for projection and `SetGestureSource`; `SetPlayerID` switches ownership and `DropVillagersFrom(count)` prunes rollback growth. `SelectionRules` supplies pure eligibility/resident/picking rules.

`TapRouter.Initialize(source, selection, commandSystem, panelManager)` dispatches resolved taps; `SetCameraController` supports camera intents. `HitFlashRouter.Initialize(source)` and `SetVillagerTransforms` route touch-down feedback independently of selection.

`CameraController.SetGestureSource` binds gesture pan/zoom; `ApplyInputSettings` updates controls. `SetViewer(mode, playerID)` resolves home/spectator side; `SetHomeAnchor`, `SetDraftMode`, `RotateView` and `SetPOV(side, pitch)` control framing. `POVChanged(Quaternion)`, `ZoomChanged(float)` and `ZoomGestureActiveChanged(bool)` notify presentation. HUD calls `RecentreOnHome`, `ToggleFitDefaultZoom`, zoom gesture/normalised zoom methods, and focus-session methods. `FocusOnWorldPoint` counts as manual camera movement. `Shake` composes presentation offsets. Pure `BoardFraming` computes centre, expanded bounds and width-fit/toggle zoom.

`InputBindings` defines allowed/default actions, normalisation and warnings. `GameSettingsData.Normalized` migrates settings; `Differ` compares them; `PlayerProfile.SetSettings` persists them. Controls fields include slot-indexed bindings, hold time, camera-button visibility/zoom, controls side and selection-bar visibility. Display preferences include frame cap and graphics quality (Low/Medium/High/Ultra). Graphics defaults to Medium on mobile and High elsewhere.

## Connects to
- `commands`: `TapRouter` calls `CommandSystem.IssueMoveTo`; drag events feed order previews, then enqueue identical commands.
- `simulation`: reads state for selection and route eligibility; never writes it.
- `match-flow`: builds routing, applies local seat/settings and prunes selection after rollback.
- `world-view`: transforms support picking; paint, flash and pending-route views consume gestures; camera POV updates billboards/sorting.
- `match-ui`: UI hits block board gestures; HUD camera handle/settings and node inspection consume intents.
- `lobby-ui`: Controls edits/reset persist through settings; migration preserves unrelated preferences.
- `board`: initial Core positions and dimensions establish camera anchors/bounds.
- `tooling`: synthetic gesture traces and settings-migration tests guard input contracts.

## Invariants
One action per slot. Locked tap/right-click/UI grammar stays reachable. Villagers select on release, never press; dragging an unselected villager cannot select/order it. IDs are append-only: LassoSelect=2 remains “Paint select”; node slots 13/14 make 15 slots. Settings version 7 is independent of network protocol. Camera/view changes never alter simulation results.

## How it works
Default villager taps add/remove. Hold-drag paints; ground drag and two-finger drag pan; pinch, middle-drag and scroll are enabled. Order-drag, ground/villager double taps, two-finger tap, double-drag zoom and stationary hold-info are optional. Node double/triple selection defaults on. Controls warnings identify unusable/redundant combinations without blocking; Reset remains available.

Tap slop is 4 mm; hold defaults to 0.3 seconds (0.15–1 configurable) with 1.5 mm accumulated stillness. Node chains match original ID/first position within 8 mm and a 0.22-second gap: single/double defer, third release commits immediately. Press continuations suspend expiry; cancellation/UI/changed target/bindings abandon pending chains without issuing orders. Non-node doubles retain 0.3-second timing and immediate first singles.

Local villagers win picking by nearest projected centre then ID. Node double selects Idle residents; triple selects all living, unconsumed, nonmoving local residents. Single inspects with no selection, otherwise orders unless every eligible resident is already selected. Right-click/order-drag remain orders. Order-drag requires an already selected villager and moves the whole selection; empty drop cancels.

Paint uses a fixed 5 mm screen-radius disc swept through inclusive capsules, no closed polygon interior. Authoritative decimated/capped samples drive membership and rendering. Projected living local centres in front of camera qualify; empty capture preserves selection, nonempty replaces it. Density/radius are snapshotted through completion.

POV is quarter-turn side, not player ID. `SetPOV` changes camera, billboard facing and horizontal CustomAxis sorting together. Pan/focus use ground projections; world-axis bounds expand to the grid. Middle-drag/scroll still read devices directly in the camera.

## Decisions
- Pure classification/selection rules enable synthetic traces without Unity.
- Device-independent physical thresholds avoid pixel-dependent gesture meaning.

## Pressure points
Camera device polling duplicates the gesture path. Deferred tap identity/cancellation and binding migration are sensitive seams. Cross-device binding persistence is unverified. Physical safe areas, timing/spacing and brush fill/allocation cost need device evidence.

## Change recipes
- Add a slot: append IDs/defaults/allowed actions, preserve disabled/custom entries, clone arrays and test migrations/Differ/Capture. Version 0 gets defaults; pre-v5 gets controls defaults; v5 pads node slots; v6 OFF stays OFF.
- Change gestures: preserve cancellation and non-node traces; share authoritative paint samples; update both UI controls surfaces.
- Change camera: use `SetPOV` for orientation and shared framing maths; keep simulation untouched.
- Add display preferences: migrate absent values, preserve explicit choices and unrelated controls, update Differ and both settings surfaces.

## Tests
View tests cover classifier traces, geometry, resident/picking rules and camera maths; Lobby tests cover settings/bindings/Controls migration. Compile-check covers Unity adapters. Manual/device checks establish input delivery, physical spacing, safe areas and brush/render correspondence.
