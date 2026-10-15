---
system: match-ui
summary: Presents draft, match status and node actions through UI Toolkit while preserving legacy tap arbitration and fallbacks.
sources:
  - Assets/UI/Scripts/Gameplay/
  - Assets/UI/Layouts/GameplayHUD.uxml
  - Assets/UI/Layouts/NodeSheet.uxml
  - Assets/UI/Layouts/DraftScreen.uxml
  - Assets/UI/Editor/UIToolkitHUDSetup.cs
  - Assets/UI/Editor/UIToolkitDraftSetup.cs
  - Assets/Scripts/Game/UI/
  - Assets/Scripts/Game/Core/IDraftPresenter.cs
  - Assets/Scripts/Game/Core/ICountdownPresenter.cs
  - Assets/Scripts/Game/Core/GameManager.cs
verified: a1da5182
---

# Match UI

## Purpose
Players inspect the board, issue node actions and understand draft/match progress without UI writing simulation state. UI Toolkit provides the active presentation; compiled uGUI components preserve fallback presentation and shared tap arbitration.

## Interface
`GameManager` calls `GameplayHUDController.Initialize(state, DebugPlayerSwitch, breachThresholdValue, InputBuffer, ITickProvider, GameBalanceData, NodePanelManager, SelectionSystem)`. Bind camera, `IndicatorDirector`, `IEmoteChannel`, opponent-route and breach-cue settings through `BindCamera`, `BindIndicators`, `BindEmotes`, `BindRouteSettings`, `BindBreachCues`. `SetDebugBalance` changes the readout's balance reference.

The HUD implements `ICountdownPresenter.PlayCountdown(Action onComplete)` and exposes `ShowMatchEnd`, `ShowMatchEndWith`, `ShowDisconnected`, `ShowRankedResult(RankedResultStatus)`, `ShowHold(HoldStatus)`, `HideHold`, connection-banner methods, `EnableSurrender` and `SurrenderFailed`. Events `HoldActionClicked`, `SurrenderConfirmed`, `ReturnToLobby` request lifecycle actions; they do not decide ranked results.

`NodePanelManager` remains the tap arbiter: `OpenForNode`, `NodeOpened(int)`, `NodeClosed`, `NodeInspected(int)`, `InspectedNodeID`, `SetSuppressed(bool)`. Inspection is independent of visible panels. `NodeSheet(layout)` binds `(SimulationState, InputBuffer, ITickProvider, GameBalanceData)`, then `Open(node, controlledPID)`, `Update(controlledPID)`, `Close`, `UpdateSafeArea`; `Closed` reports user dismissal. `DistrictPanelPolicy.HasSheet` determines Toolkit eligibility; legacy panels use `IsFunctional`.

`NodeSheetContent.Bind(state, input, ticks, balance, nodeID, controlledPID, actions)` establishes a binding; `Refresh`, `SetViewer`, `Unbind`, `Tall`, `Compact`, `InvolvedResources` define its presentation contract. Protected `Send(GameCommand)` is the command choke point; `SetResourceText` expands `{food}`, `{materials}`, `{metal}` into icons/text.

Content implementations:
- `CoreContent`: paid respawn price, target and refusal.
- `ForgeContent`: worker allocation/metal production.
- `EquipContent`: Barracks, Camp, Arsenal and Sanctuary suit/unit choices.
- `ProductionContent`: owned Farm/Mine workers and production.
- `BankContent`: Storehouse contents, health/production, progress/status and Collect/Cancel through `BankActionModel.Describe/TryCollect`.
- `NodeActionContent`: Village, Workshop, Town, Infirmary, Fortress; `NodeActionModel.Describe` and `TryRecruit/TryForgeMinion/TrySetAutoRecruit/TryUpgradeFortress` build eligible commands. Enemy panels are informational; Town has paid flags without actions.

`ResourceRing.SetValue/SetCap/SetProduction/SetReducedMotion` and equivalent `ResourceBar` methods render resources. `ResourceRingMath`, `ResourceHudMath`, `ResourceCaps`, `ResourceProduction`, `ResourceVisibility`, `ResourceRingColors`, `SheetResourceMath` and `SheetResourceReadout` separate fill, caps, production markers, visibility, color and sheet expansion. `ProgressDial.Progress` drives circular progress.

`IndicatorLayer.Bind`, `LateUpdate`, avoidance/sheet queries and `Suppress/Resume` project director output. `EmotePanel.Bind/Attach/Detach/ApplySettings/SetNodeSheetOpen` owns picker, bubbles and rate limits. `MatchSettingsPanel.Open/Close/Toggle/ForceClose`, `Changed(GameSettingsData)` and `SurrenderConfirmed` serve persisted controls and confirmed ranked surrender.

`DraftScreenController` and uGUI `DraftUI` implement `IDraftPresenter`: `Initialize`, `ShowWaiting`, `ShowInitialReveal`, `SweepIn/Out`, `UpdateTimer`, `OnTurnChanged`, `OnPlacementConfirmed`, persistent-placement retrieval and pending-placement query. `DraftHandover.ProxyOpacity/GhostOpacity` crossfade the dragged UI proxy and world ghost; `DraftPieceInfo` supplies names/monograms/tints.

## Connects to
- `match-flow`: owns presenters, initialization, countdown, holds, surrender and match-end requests.
- `commands`: owns `CommandEligibility` despite its Gameplay path; content sends validated `GameCommand`s through `InputBuffer`.
- `simulation`: read-only state/balance and shared action helpers supply prices and status.
- `economy`: resource totals, production and Storehouse state feed gauges/content.
- `input`: selection/tap routing opens sheets and drives camera/count controls.
- `draft`: presenter displays turn state and forwards placement intentions without building simulation state.
- `world-view`: indicator output, world ghosts and camera projection supply presentation coordinates.
- `netcode`: `IEmoteChannel` and connection status feed cosmetic/hold feedback.
- `backend-client`: `HoldStatus` and `RankedResultStatus` display server outcomes.
- `lobby-ui`: shared Controls/settings and `SceneTransition` support preferences and return flow.
- `ui-art`: theme icons, district visuals, tokens and gauge properties supply appearance.
- `tooling`: compilation, pure presentation models and authored XML checks complement manual layout verification.

## Invariants
UI never writes `SimulationState`. Reject detached/stale sheet bindings after capture, district change or rollback; models validate the bound node array/tick. Exactly one draft presenter exists. Hiding legacy HUD must preserve its arbitration root. Decorative layers cannot swallow board input.

## How it works
Scene values choose stacks: `LobbyManager.useUIToolkitLobby`, `GameManager.useUIToolkitHUD`, `useUIToolkitDraft`. The Toolkit HUD suppresses legacy panels and hides `HUD_Canvas`/disables `HUDManager` only after successful initialization. Missing roots retain legacy fallbacks. `CreateDraftPresenter` chooses one active scene controller or instantiated uGUI prefab; HUD and draft toggles are independent.

Layouts are `GameplayHUD.uxml`, `NodeSheet.uxml`, `DraftScreen.uxml`; HUD/NodeSheet/Draft USS styles them. Editor commands `Tools > Node War > Set Up UI Toolkit HUD` and `Set Up UI Toolkit Draft` create roots/PanelSettings and wire controllers. Draft setup copies ghost, placed-piece and sticker references from the assigned uGUI draft prefab.

The HUD clones PanelSettings for aspect-aware scale, restoring/destroying the copy on disable. Safe-area count dock `hud-selbar-dock` remains bottom-centered; only its count button picks. Camera-side settings mirror the camera handle, not selection. Rings show food/materials; bars show metal and display-only magic (zero). Sheet-involved resources expand; reduced motion suppresses bounces/full-resource motion. IndicatorLayer sits behind chrome; emote bubbles can appear above the end card. Saved mute overrides match-local mute.

## Decisions
- Select presenter implementations rather than make draft rules depend on either UI stack.
- Retain legacy arbitration rather than migrate tap ownership with presentation.
- Read shared eligibility/helpers rather than duplicate prices in controls.

## Pressure points
Three UI stacks and scene toggles complicate verification. uGUI draft is mouse-only and lacks Toolkit parity; uGUI HUD lacks the hold overlay. Magic is display-only. Placeholder art and long Controls lists need device validation. Node-sheet camera clearance still depends on legacy infrastructure.

## Change recipes
- Add a node action: extend command-owned eligibility/model, content mapping and resource declarations together; test stale-binding refusal.
- Replace a layout/root: preserve required names/types/picking, use Editor builders for serialized wiring, verify fallback and both aspect orientations.

## Tests
`NodeWar.View.Tests` and `NodeWar.Lobby.Tests` cover pure HUD/action/draft math, eligibility and authored XML contracts. Compile-check covers Unity controllers. Touch delivery, safe areas, ghost handover, overlays and scene wiring require Editor/device checks.
