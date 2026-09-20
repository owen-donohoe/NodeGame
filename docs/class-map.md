---
type: Reference
title: Class map
description: What each class in each layer is for, layer by layer. The catalogue half of architecture.md; read it when you need to know what a named class does, not to learn how the project fits together.
tags: [architecture, reference, classes]
generated: { by: claude-opus-5, at: 2026-09-20T13:00:00Z }
status: draft
sources:
  - id: sim-state
    resource: Assets/Scripts/Game/Simulation/SimulationState.cs
    title: SimulationState
  - id: sim-loop
    resource: Assets/Scripts/Game/Simulation/GameSimulation.cs
    title: GameSimulation.SimulateTick
  - id: game-manager
    resource: Assets/Scripts/Game/Core/GameManager.cs
    title: GameManager match lifecycle
  - id: lockstep
    resource: Assets/Scripts/Game/Network/LockstepRunner.cs
    title: LockstepRunner
  - id: tick-runner
    resource: Assets/Scripts/Game/Core/TickRunner.cs
    title: TickRunner
  - id: match-connection
    resource: Assets/Scripts/Game/Core/MatchConnection.cs
    title: MatchConnection
  - id: draft-manager
    resource: Assets/Scripts/Game/Core/DraftManager.cs
    title: DraftManager
  - id: draft-presenter
    resource: Assets/Scripts/Game/Core/IDraftPresenter.cs
    title: IDraftPresenter, the seam between the draft and its two UI stacks
  - id: uitk-draft
    resource: Assets/UI/Scripts/Gameplay/DraftScreenController.cs
    title: UI Toolkit draft screen
  - id: lobby-manager
    resource: Assets/Scripts/Lobby/LobbyManager.cs
    title: LobbyManager and the useUIToolkitLobby toggle
  - id: node-panel-manager
    resource: Assets/Scripts/Game/UI/Panel/NodePanelManager.cs
    title: NodePanelManager and SetSuppressed
  - id: uitk-lobby
    resource: Assets/UI/Scripts/LobbyUIController.cs
    title: UI Toolkit lobby shell
  - id: uitk-hud
    resource: Assets/UI/Scripts/Gameplay/GameplayHUDController.cs
    title: UI Toolkit in-match HUD
  - id: uitk-sheet
    resource: Assets/UI/Scripts/Gameplay/NodeSheet.cs
    title: UI Toolkit node sheet
  - id: uitk-sheet-content
    resource: Assets/UI/Scripts/Gameplay/NodeSheetContent.cs
    title: NodeSheetContent.Send, the UI Toolkit command path
  - id: outline-driver
    resource: Assets/Scripts/Game/View/OutlineDriver.cs
    title: OutlineDriver, the only setter of outline intents
  - id: outline-registry
    resource: Assets/Scripts/Game/View/Outline/OutlineRegistry.cs
    title: OutlineRegistry and the outlined-only ID lifecycle
  - id: outline-style
    resource: Assets/Scripts/Game/View/Outline/OutlineStyle.cs
    title: OutlineStyle, whose numeric order is the priority order
  - id: outline-feature
    resource: Assets/Scripts/Game/View/Outline/OutlineRendererFeature.cs
    title: OutlineRendererFeature, the URP entry point
---

# Class map

Split out of `docs/architecture.md` on 2026-09-20. That document is 551 lines
and was read whole by four of five delegated agents, because it said "Start
here"; this half -- the catalogue -- was the half none of them needed. Read
`architecture.md` to learn how the project fits together. Read this when you
already know that and need what a named class does.

## Key classes per layer

**Lobby/**
- `LobbyManager` — panel navigation and startup (Homepage, GameMode,
  Profile, Shop, GroupSelection).
- `PlayerProfile` — persistent player identity/progression singleton.
- `LoadoutData`, `NodeDefinition`, `SuitDefinition` — data describing a
  player's drafted nodes/suits.

**Core/**
- `GameManager` — match lifecycle state machine (`PreDraft → Drafting →
  PostDraft → Countdown → Playing`); builds `SimulationState` and spawns
  node/villager views.
- `DraftManager` — runs the pre-match node-placement draft as its own
  turn-based phase machine (`WaitingForReady → InitialReveal →
  ActiveDraft → Complete`). Draws nothing itself: it drives an
  `IDraftPresenter`, and asks it one question back — whether a piece is
  parked but unconfirmed, so a turn that times out takes the cell the
  player already chose rather than a random one.
- `IDraftPresenter` / `ICountdownPresenter` — the two places a phase has
  to be drawn by whichever UI stack is on. Same shape for the same reason:
  the phase owns the rules, the presenter owns the pixels.
- `TickRunner` — local (non-networked) fixed-tick driver.
- `MatchConnection` — persists match configuration across the Lobby →
  Gameplay scene load.
- `CameraController` — camera rig and per-player orientation.
- `MatchTransitionController` — scripted transition sequences (startup
  wave, post-draft reveal, breakdown-on-game-over).
- `ITickProvider` — shared interface exposing tick-interpolation alpha so
  View code doesn't need to know whether `TickRunner` or `LockstepRunner`
  is driving the match.

**Simulation/**
- `SimulationState` — the entire mutable match state: `NodeData[]`,
  `VillagerData[]`, `PlayerData[]`, tick count, game-over/winner.
- `GameSimulation.SimulateTick` — the deterministic tick loop.
- `CommandProcessor` — validates and applies a `GameCommand` to
  `SimulationState`.
- `Commands.cs` — `GameCommand` struct and `CommandType` enum.
- `Pathfinding` — Dijkstra over the node graph with ownership-based
  integer cost multipliers.
- `GameBalance`, `BoardConfig` — `ScriptableObject` tuning data, read once
  at match start.
- `DraftState` — grid occupancy and per-player slots during the draft
  phase.
- `SimulationStateHasher` — deterministic integer fingerprint of
  `SimulationState`, used for desync detection.

**Network/**
- `LockstepRunner` — networked tick driver; stalls a tick until both
  local and remote inputs exist for it.
- `NetworkManager` — transport abstraction (send/receive raw packets).
- `InputSerializer` — wire format for tick inputs and heartbeats.
- `DraftSerializer` — wire format for draft-phase packets (ready,
  placement, loadout).

**Input/**
- `PointerGestureSource` — the shared pointer reader for selection and
  commands; publishes taps, pans, lassos, pinches and right-click destinations.
- `TapRouter` — the tap priority ladder: villager, then node-with-selection
  (move), then node (panel), then empty (clear).
- `SelectionSystem` — tracks selected villagers; applies lasso results.
- `CommandSystem` — turns player actions into `GameCommand`s.
- `InputBuffer` — queue of commands awaiting the next tick.
- `BotPlayer` — generates commands for an AI-controlled side.
- `HitFlashRouter` — the single bridge from gesture events to renderers,
  so the input layer never touches a `SpriteRenderer` itself.
- `LassoGeometry` / `ScreenMetrics` / `GestureThresholds` — pure helpers:
  polygon containment and smoothing, millimetre-to-pixel conversion, and
  the tunable thresholds.

**UI/** (uGUI, `Assets/Scripts/Game/UI/`)
- `HUDManager` — top-level in-match HUD.
- `NodePanelManager` — per-node detail/action panel. Can be told to stand
  down by `SetSuppressed` when the UI Toolkit sheet is serving instead.
- `DistrictPanelPolicy` — decides which districts open a panel at all.
  Both panel stacks defer to it, so neither has its own answer.
- `DraftUI` — draft-phase interface, with `DraftPlacementController`
  (drag/park/confirm state machine), `DraftSlotUI` and
  `DraftConfirmPresenter`. Implements `IDraftPresenter`. Off by default
  now, and mouse-only: it reads `Mouse.current` and cancels on right-click
  or Escape, neither of which a phone has.
- `GameOverPanel` — end-of-match result display.
- `SelectionLasso` — draws the in-progress lasso stroke.
- `LassoArmedCue` — ring pulse confirming the long press armed.

**`Assets/UI/`** (UI Toolkit)
- `LobbyUIController` / `NavigationController` / `LobbyPage` — the lobby
  shell. The tab pages (`ShopPage`, `HomePage`, `WorkshopPage`,
  `SocialPage`) sit side by side in one track that slides between them.
  `ProfilePage` (expands from the trophy strip) and the `LobbyPushPage`s
  (`SettingsPage`, `MatchHistoryPage`) are overlays above the chrome, not
  tabs.
- `LobbySheet`, `LobbyContextMenu`, `LobbyToast` — one of each for the
  whole lobby, handed to pages rather than built per page. `PlayPopup` is
  content shown in the sheet.
- `LoadoutCatalog` — what a loadout slot may hold and what the player owns
  (not globally granted, not Crossroads, unlocked). The Workshop, Home and
  the battle sheet all ask it; the slot rules themselves are in the
  UnityEngine-free `LoadoutEditor`, which `dotnet/NodeWar.Lobby.Tests`
  covers.
- `MatchLauncher` — the lobby's route into a match.
- `GameplayHUDController` — the in-match HUD, bound by `GameManager`.
- `NodeSheet` — the node panel as a bottom sheet. It does not decide when
  to open; `NodePanelManager` still owns that.
- `NodeSheetContent` and its three subclasses — `ForgeContent`,
  `CoreContent`, `EquipContent` cover all six actionable districts.
  `Send` is the only path to the simulation.
- `DraftScreenController` — the draft screen, and the one place in this
  tree that owns an interaction end to end. The chrome and the placement
  cannot be separated here: the drag begins on a UI Toolkit card and ends
  on the 3D board, so it reads `Pointer.current` (mouse *or* touch) for
  everything past the card press, positions the Confirm pair from the
  parked cell's world position each frame, and instantiates the same
  world-space ghost prefab the uGUI draft used. It writes nothing to
  `SimulationState` — during the draft there is not one yet.
- `DraftPieceInfo` — a district's name, monogram and tint for the draft
  cards. Names come from the lobby's `NodeDefinition` assets rather than a
  switch statement, so the draft and the Workshop cannot disagree about
  what a player picked; the enum name is the fallback for the four base
  draft districts no loadout slot can hold.
- `SafeAreaBinder` — the UI Toolkit reader of `Screen.safeArea`.
- Layouts in `Assets/UI/Layouts/*.uxml`, styles in `Assets/UI/Styles/*.uss`.
  One theme serves both scenes: `Tokens.uss` (palette, then tokens named for
  their job) and `Components.uss` (the `ui-` classes both draw - button,
  sheet, bar, chip, toast, player mark, monogram tile). Neither is imported by
  a layout; both arrive through `UnityDefaultRuntimeTheme.tss`, which the
  Lobby and HUD `PanelSettings` share. So a token edit changes both scenes.
  Page stylesheets (`Lobby.uss`, `HUD.uss`, `NodeSheet.uss`, one per lobby
  page) hold only what that screen draws, plus any token only it uses.
  Precedence runs the other way from CSS intuition: a rule arriving through
  the theme loses to any rule in a stylesheet a layout imports, whatever the
  specificity.

  Three `PanelSettings` assets, not one — Lobby, HUD and Draft. They share
  the theme and the 390x844 reference frame, but each is a separate surface
  with its own sort order, and one asset tuned for the match is one asset
  that has to be re-checked whenever the draft changes.

**View/**
- `NodeView` / `NodePresentation` / `NodeSlotManager` — node visuals,
  villager slotting on a node.
- `VillagerView` — villager visuals and movement interpolation.
- `NodeClaimBar`, `VillagerHealthRing` — world-space status indicators.
- `NodeHighlight` — the expanding ring used for move-order destinations
  and, configured smaller, for the lasso-armed cue.
- `VillagerTouchTarget` — constant-screen-size tap collider, built at
  runtime so the villager prefab needs no edit.
- `VillagerFlash` — touch-down white flash amount, composed over the
  per-state tint by `VillagerView`.
- `PathCurve` — rounds a node path into the curve a route is drawn along and
  a villager walks. Corner rounding rather than Chaikin, because the
  waypoints are leg boundaries the sprite has to arrive on.
- `PathCurveSettings` — the one shared instance of that curve shape, owned by
  `GameManager` and handed to both consumers so they cannot disagree.
- `MovementPathRenderer` — dotted routes for the local player, one line per
  distinct remaining route so a squad reads as one, fading as an order ages.
- `OpponentRouteSettings` — the rules of the one information gate in the
  game. Everything else is fully visible to both players, so an opponent
  route hands the player something new rather than withholding it.
- `OutlineDriver` — the only thing that sets outline intents. Reads hover,
  villager selection and the open node, and is read-only against the
  simulation. `GameManager` builds it before the views that register with
  it, and hands it the same `SelectionSystem` the tap path uses, so hover
  and selection agree about ownership by construction rather than by two
  copies of the same rule.

**View/Outline/** — its own assembly, `NodeWar.View.Outline`

The group-silhouette outline system. It is separate from `View/` proper
because its ID lifecycle is the part with real edge cases in it, and an
assembly of its own is what lets those cases be unit tested against a plain
object with no GameObject, no scene and no render pipeline — the reason
`IOutlineGroup` exists at all.

- `OutlineRendererFeature` — the entry point into URP, and the project's
  first custom renderer feature. It must be listed in **both**
  `Assets/Settings/Mobile_Renderer.asset` and `PC_Renderer.asset`.
- `OutlineMaskPass` / `OutlineCompositePass` — draw the registered groups as
  IDs into an offscreen target, then dilate ID boundaries into outline colour.
- `OutlineRegistry` / `OutlineIdAllocator` — ID allocation and the draw list.
  **A group holds an ID only while it is actually outlined**; registration is
  not what grants one, a style other than `None` is. So "nothing is outlined"
  is an empty list and both passes are skipped, and nothing needs cleanup when
  a node or villager goes away.
- `OutlineGroup` / `IOutlineGroup` — marks a node or villager root as one
  silhouette, so seams inside it grow no line. Added at runtime, like
  `NodeHighlight` and `VillagerTouchTarget`, so no prefab needs editing.
- `OutlineStyle` — the transient states an outline expresses. **The numeric
  order is the priority order**, so reordering the enum silently changes which
  state wins a conflict; `OutlineStyleTests` pins it so a reorder fails a test
  instead of changing the game. Player ownership is deliberately not a style.
- `OutlineSettings` / `OutlineScreenBounds` — the palette and thickness asset
  (`Assets/Settings/OutlineSettings.asset`), and the screen-space scissor.

Tested by `Assets/Tests/EditMode/Outline/`, its own test assembly.

