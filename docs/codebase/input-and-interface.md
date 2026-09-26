---
type: Guide
title: Input and interface
description: Intent routing, UI ownership, and the migration between presentation stacks.
tags: [guide, input, ui, mobile]
status: draft
sources:
  - id: pointer
    resource: Assets/Scripts/Game/Input/PointerGestureSource.cs
    title: PointerGestureSource.cs
  - id: router
    resource: Assets/Scripts/Game/Input/TapRouter.cs
    title: TapRouter.cs
  - id: sheet
    resource: Assets/UI/Scripts/Gameplay/NodeSheetContent.cs
    title: NodeSheetContent.cs
  - id: hud
    resource: Assets/UI/Scripts/Gameplay/GameplayHUDController.cs
    title: GameplayHUDController.cs
  - id: panel
    resource: Assets/Scripts/Game/UI/Panel/NodePanelManager.cs
    title: NodePanelManager.cs
---

# Input and interface

[Atlas](README.md) · Previous: [Match lifecycle](match-lifecycle.md) · Next: [World presentation](world-presentation.md)

Input turns a physical action into intent. UI both produces intent and explains the observed world. Neither owns the outcome of a gameplay action. This separation allows a phone, desktop, bot, or future policy to operate the same game.

## Resolve a gesture once

`PointerGestureSource` is the common pointer reader for live selection and move orders. It distinguishes taps, pans, long-press lasso, pinch behavior, and desktop right-click destinations. It performs the relevant hit resolution and publishes a target. Consumers should not independently raycast the same press and disagree about what the user touched.

`TapRouter` applies meaning in priority order: a selectable villager, a move to a node when a selection exists, a node panel, or empty-space clearing. `SelectionSystem` owns selected villagers and selection eligibility. An opponent villager is not a selectable target, so a press can fall through to the node beneath it. Ownership policy lives with selection, not in the low-level gesture reader.

UI blocking has to cover both uGUI and UI Toolkit. A press on a sheet or HUD must not also command the board behind it. Right-click move routing uses the current pointer location for this check rather than trusting yesterday's hover state.

Thresholds use millimetres converted through screen metrics. That makes interaction tolerances describe physical input rather than a fixed number of pixels. Keyboard commands remain an optional additional vocabulary. Camera middle-drag and wheel input still have a direct desktop path; full input unification is an extension, not a claim that every device read is centralized already.

## Commands are the shared language

`CommandSystem`, gameplay sheet content, and `BotPlayer` eventually feed `InputBuffer`. The tick driver drains that intent into the [simulation command processor](simulation.md). This is why a bot is an input producer and why UI cannot make a paid respawn happen by adjusting a player's food or a villager's state itself.

For UI Toolkit node actions, `NodeSheetContent.Send` is the common command path. Eligibility helpers make refusal conditions visible before a press, while the processor remains responsible for actual legality at execution time. State can change between rendering a button and executing its command, especially across network input delay.

Selection and open-panel state are local conveniences. They need not be replicated. Replicating commands instead of pointer coordinates also lets two players use different camera views and screen layouts without interpreting each other's gestures.

## Three code trees, one boundary

| Tree | Role | Why it still matters |
|---|---|---|
| `Assets/UI/` | UI Toolkit lobby, draft, HUD, sheets, settings, end screen | Primary replacement presentation |
| `Assets/Scripts/Game/UI/` | uGUI gameplay UI and world-space UI | Contains fallback presentation and shared arbitration such as `NodePanelManager` |
| `Assets/Legacy/` | Older uGUI lobby | Still compiled; active only when the replacement path is not selected |

The active paths are selected by serialized scene fields: `useUIToolkitLobby`, `useUIToolkitHUD`, and `useUIToolkitDraft`. Code defaults do not prove what a scene displays. The HUD and draft switches are separate because those phases can migrate independently.

`NodePanelManager` still arbitrates opening a node panel. When the Toolkit sheet is present, the old panel display is suppressed so two stacks do not compete for one tap. The Toolkit `NodeSheet` owns the sheet presentation, not a second interpretation of board gestures. `DistrictPanelPolicy` is shared to keep both stacks' district eligibility consistent.

Draft is a special case: `DraftScreenController` owns a card-to-board drag end to end. It uses the presenter contract for draft coordination and handles pointer motion outside the card. It does not write gameplay simulation state.

## Readouts explain processes, not just totals

The HUD shows resources and ongoing production; node sheets explain local production, allocation, equipment, or recovery. `ResourceProduction` reads the simulation's timers and era-aware balance. `ResourceRingMath` and `ResourceRing` separate the reusable calculation from the element that draws it. This is why view-math tests can catch a ring-placement or production-readout regression without starting Unity.

Indicators connect off-screen events to the player's attention; emotes provide an out-of-band social channel. Neither changes resource totals or match results. Match-local and saved mute settings can affect presentation without altering replay compatibility. See [world presentation](world-presentation.md) and [networking](networking.md).

## Layout and services have different ownership

The lobby shell owns navigation and shared sheet, context menu, and toast surfaces. Pages receive these common services rather than each building competing overlays. Workshop equip is asynchronous and shows the state returned by the inventory service; account conflicts use an explicit flow. Network latency is a UI state, not a reason to pretend the equip succeeded.

UXML defines structure, USS defines layout and appearance, and C# binds behavior. Shared tokens/components arrive through the runtime theme; page styles carry screen-specific rules. Unity theme precedence differs from a browser's ordinary stylesheet intuition, so an imported page style can override a theme rule even when the theme selector looks stronger.

Separate Lobby, HUD, and Draft PanelSettings share a theme and reference frame while retaining their own surface settings. Safe-area handling and future compact/wide layouts belong here. A layout change should be able to rearrange actions without adding new gameplay rules or packet types.

Before editing UI, read [Where the UI lives](../architecture.md#where-the-ui-lives), inspect the scene switches, and identify who already owns the interaction. The most expensive UI mistake in this project is creating a second owner for an existing action.

## Source landmarks

[PointerGestureSource.cs](../../Assets/Scripts/Game/Input/PointerGestureSource.cs) | [TapRouter.cs](../../Assets/Scripts/Game/Input/TapRouter.cs) | [NodeSheetContent.cs](../../Assets/UI/Scripts/Gameplay/NodeSheetContent.cs) | [GameplayHUDController.cs](../../Assets/UI/Scripts/Gameplay/GameplayHUDController.cs) | [NodePanelManager.cs](../../Assets/Scripts/Game/UI/Panel/NodePanelManager.cs)
