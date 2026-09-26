---
type: Guide
title: World presentation
description: Interpolation, camera POV, sprites, outlines, indicators, and event-driven feedback.
tags: [guide, rendering, view, game-feel]
status: draft
sources:
  - id: villager
    resource: Assets/Scripts/Game/View/VillagerView.cs
    title: VillagerView.cs
  - id: camera
    resource: Assets/Scripts/Game/Core/CameraController.cs
    title: CameraController.cs
  - id: outline
    resource: Assets/Scripts/Game/View/OutlineDriver.cs
    title: OutlineDriver.cs
  - id: indicators
    resource: Assets/Scripts/Game/View/IndicatorDirector.cs
    title: IndicatorDirector.cs
  - id: shake
    resource: Assets/Scripts/Game/View/ScreenShakeDirector.cs
    title: ScreenShakeDirector.cs
  - id: art
    resource: Assets/Scripts/Game/View/DistrictVisual.cs
    title: DistrictVisual.cs
---

# World presentation

[Atlas](README.md) · Previous: [Input and interface](input-and-interface.md) · Next: [Networking](networking.md)

The simulation knows a graph and tick progress. The view creates spatial continuity, legibility, and feedback from that information. Its freedom to use floating-point positions and frame time depends on never feeding those results back into gameplay.

## Smooth motion over discrete truth

`NodeView`, `NodePresentation`, and `NodeSlotManager` turn nodes and their occupants into world objects. `VillagerView` computes a displayed position from the current route leg, integer movement progress, and the tick provider's interpolation information. The simulation's `currentNodeID` is the last node stood on, not an authoritative floating-point location mid-edge.

`PathCurve` rounds routes for both route drawing and movement presentation. Shared `PathCurveSettings` prevent the dotted line and the walking sprite from following different curves. Curves respect leg waypoints because those are the simulation's arrival boundaries; arbitrarily smoothing them away would make the picture imply an arrival that has not occurred.

Route caches avoid rebuilding unchanged curves every frame. Squad routes can be grouped so several villagers following the same remaining path read as one order. Route visibility is also an information policy: opponent-route gates control the extra intent exposed by drawing an enemy's destination. This is presentation disclosure over already replicated state, not server-enforced fog of war.

## Camera side is not player identity

`CameraController` resolves a viewer to a board-facing side and applies POV changes through one entry point. `ViewSide` contains Unity-free geometry for this. Player, spectator, and local debug switching use the same model rather than each assuming that a particular player number means a fixed yaw.

Changing POV affects more than the camera transform. Billboard facing, sprite depth order, framing, and draft pieces must agree. Custom-axis sprite sorting uses a stable horizontal direction for the current side; sorting along every small live camera movement would cause close sprites to flicker in front of one another.

Within a node, authored `SortHeight` establishes vertical precedence and depth rank resolves equal-height sprites, with a stable tiebreaker. Sorting groups keep a node's art coherent. This is why a camera bug can appear to be an art or unit-overlap bug: the shared spatial interpretation has changed.

## Outlines express interaction state

`OutlineDriver` reads hover, selected villagers, and the open node, then sets outline intents. The outline renderer lives in its own assembly and uses a registry, temporary IDs, a mask pass, and a composite pass. A group represents a silhouette, preventing internal sprite seams from each growing separate borders.

IDs are held while a group actually has an outline rather than for every registered object forever. That keeps the draw list and cleanup behavior tied to visible work. `IOutlineGroup` lets allocation/registry behavior be tested without a scene. The numeric priority of `OutlineStyle` is meaningful and pinned by tests.

URP renderer-feature wiring is part of this system, so both mobile and PC renderer assets matter. Compilation alone cannot prove the feature is installed or renders correctly. Always-on Present-outline work in another branch is outside the implementation described here.

## Separate a condition from the moment it began

An active fight can be observed in `SimulationState`; the instant it started comes from `TickEventLog`. `IndicatorDirector` combines moments and conditions, applying debounce and grace so a one-tick flicker does not repeatedly summon attention. `IndicatorLayer` draws the resulting decisions inside the Toolkit HUD, handles screen-edge placement, and lets a tap focus the camera.

Placement accounts for the board region, HUD docks, open node sheet, and overlapping icons. Pure placement math is tested independently from UI objects. Timers on pooled elements need careful ownership: a detached element's scheduled callback can resume after reuse, so generation-aware scheduling belongs to the layer rather than an old element lifetime.

`ScreenShakeDirector` already consumes tick events and chooses the strongest applicable shake per tick. It observes local captures, neutralization, breaches, and game-over state without changing them. Earlier direction prose proposing a first event-to-shake connection is historical relative to this implementation. A broader audio/effects coordinator remains an extension, not an existing generic feel framework.

Presentation hitstop, if added, must leave the network/tick loop running. Freezing simulation time to improve an impact animation would change the timing obligations that [lockstep](networking.md) depends on.

## Art has a destination, but migration is incomplete

`DistrictVisual` and `DistrictVisualTable` exist as an authored catalog of board prefab, icon, sticker, and accent. They define a place for consistent district appearance and an asset checklist. The source explicitly says the runtime callers have not yet migrated to that table; older prefab/icon/sticker mappings still serve consumers. Having an asset type is not the same as having one live lookup path.

This is a useful future seam: migrate consumers deliberately with fallback behavior, then add sound/effect slots when there is a consumer for them. [Art manifest](../art-manifest.md) and [UI art manifest](../ui-art-manifest.md) describe asset needs and historical snapshots; inspect current assets before treating a listed gap as current.

Rendering work should be verified from both player POVs, through the draft transition, with population changes and on both renderer configurations. Those observations complement the [automated math tests](engineering-workflow.md); they are not covered by them.

## Source landmarks

[VillagerView.cs](../../Assets/Scripts/Game/View/VillagerView.cs) | [CameraController.cs](../../Assets/Scripts/Game/Core/CameraController.cs) | [OutlineDriver.cs](../../Assets/Scripts/Game/View/OutlineDriver.cs) | [IndicatorDirector.cs](../../Assets/Scripts/Game/View/IndicatorDirector.cs) | [ScreenShakeDirector.cs](../../Assets/Scripts/Game/View/ScreenShakeDirector.cs) | [DistrictVisual.cs](../../Assets/Scripts/Game/View/DistrictVisual.cs)
