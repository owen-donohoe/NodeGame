---
type: Architecture
title: Architecture
description: The seven layers of Assets/Scripts/, where the three UI trees live and which one runs, how information flows between them, and the lockstep networking model.
tags: [architecture, layers, networking, lockstep, ui]
generated: { by: human:DonohoeCUA, at: 2026-08-30T17:15:16-04:00 }
verified:
  - { by: claude-opus-5, at: 2026-08-31T00:00:00Z }
  - { by: claude-opus-5, at: 2026-09-02T00:00:00Z }
  - { by: claude-opus-5, at: 2026-09-02T02:00:00Z }
  - { by: claude-opus-5, at: 2026-09-02T04:00:00Z }
  - { by: claude-opus-5, at: 2026-09-13T00:00:00Z }
  - { by: claude-opus-5, at: 2026-09-13T01:00:00Z }
  - { by: claude-opus-5, at: 2026-09-14T00:00:00Z }
verified_at_commit: 2241e47
status: stable
sources:
  - id: sim-state
    resource: Assets/Scripts/Game/Simulation/SimulationState.cs
    title: SimulationState
    last_modified: 2026-08-30T17:51:21-04:00
  - id: sim-loop
    resource: Assets/Scripts/Game/Simulation/GameSimulation.cs
    title: GameSimulation.SimulateTick
    last_modified: 2026-08-30T17:51:21-04:00
  - id: game-manager
    resource: Assets/Scripts/Game/Core/GameManager.cs
    title: GameManager match lifecycle
    last_modified: 2026-08-30T17:51:21-04:00
  - id: lockstep
    resource: Assets/Scripts/Game/Network/LockstepRunner.cs
    title: LockstepRunner
    last_modified: 2026-08-30T22:15:29-04:00
  - id: tick-runner
    resource: Assets/Scripts/Game/Core/TickRunner.cs
    title: TickRunner
    last_modified: 2026-08-24T09:08:13-04:00
  - id: match-connection
    resource: Assets/Scripts/Game/Core/MatchConnection.cs
    title: MatchConnection
    last_modified: 2026-08-14T00:06:30-04:00
  - id: draft-manager
    resource: Assets/Scripts/Game/Core/DraftManager.cs
    title: DraftManager
    last_modified: 2026-08-30T22:15:29-04:00
  - id: draft-presenter
    resource: Assets/Scripts/Game/Core/IDraftPresenter.cs
    title: IDraftPresenter, the seam between the draft and its two UI stacks
    last_modified: 2026-09-17T09:11:22-04:00
  - id: uitk-draft
    resource: Assets/UI/Scripts/Gameplay/DraftScreenController.cs
    title: UI Toolkit draft screen
    last_modified: 2026-09-17T09:11:22-04:00
  - id: lobby-manager
    resource: Assets/Scripts/Lobby/LobbyManager.cs
    title: LobbyManager and the useUIToolkitLobby toggle
    last_modified: 2026-09-03T16:45:30-04:00
  - id: node-panel-manager
    resource: Assets/Scripts/Game/UI/Panel/NodePanelManager.cs
    title: NodePanelManager and SetSuppressed
    last_modified: 2026-09-03T16:54:53-04:00
  - id: uitk-lobby
    resource: Assets/UI/Scripts/LobbyUIController.cs
    title: UI Toolkit lobby shell
    last_modified: 2026-09-03T16:17:06-04:00
  - id: uitk-hud
    resource: Assets/UI/Scripts/Gameplay/GameplayHUDController.cs
    title: UI Toolkit in-match HUD
    last_modified: 2026-09-03T16:54:53-04:00
  - id: uitk-sheet
    resource: Assets/UI/Scripts/Gameplay/NodeSheet.cs
    title: UI Toolkit node sheet
    last_modified: 2026-09-03T16:54:53-04:00
  - id: uitk-sheet-content
    resource: Assets/UI/Scripts/Gameplay/NodeSheetContent.cs
    title: NodeSheetContent.Send, the UI Toolkit command path
    last_modified: 2026-09-03T16:54:53-04:00
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

# Architecture

Node War is a 1v1 real-time strategy game built in Unity 6 (namespace
`NodeWar`), with lockstep peer-to-peer networking. Gameplay code lives
under `Assets/Scripts/`, split into seven layers.

Presentation is mid-migration and does **not** all live there. Two sibling
trees hold the rest, and both are live code: see
[Where the UI lives](#where-the-ui-lives).

## The seven layers

```
Assets/Scripts/
  Lobby/       Layer 1
  Game/
    Core/      Layer 2
    Simulation/Layer 3
    Network/   Layer 4
    Input/     Layer 5
    UI/        Layer 6      uGUI, in-match
    View/      Layer 7
      Outline/              own assembly, NodeWar.View.Outline
    Config/    not a layer   GameBalance / BoardConfig ScriptableObjects
    Debug/     not a layer   development aids
  Editor/      not a layer   TestBridge and other editor-only tooling

Assets/UI/                  UI Toolkit — the replacement for layers 1 and 6
Assets/Legacy/              retired uGUI lobby, still compiled
```

The three marked *not a layer* carry no gameplay rules and sit outside the
information flow below. `Config/` matters anyway: it is where a new tunable
number goes, and the only place the `GameBalance` and `BoardConfig`
`ScriptableObject`s exist.

**1. Lobby/** — Pre-match menu flow: game mode selection, player profile,
loadout/node/suit selection. Runs entirely in the Lobby scene, before a
`SimulationState` exists. This layer is now data and state only
(`LobbyManager`, `PlayerProfile`, `LoadoutData`, the definition assets);
its uGUI panels moved to `Assets/Legacy/Lobby/` and its live presentation
is `Assets/UI/`.

**2. Core/** — Match lifecycle orchestration. Owns the top-level state
machine (`GameManager`), the pre-match draft (`DraftManager`), local tick
timing (`TickRunner`), and camera/transition control. This is the layer
that constructs `SimulationState` and wires every other layer together.

**3. Simulation/** — All gameplay rules and the entire mutable match
state. Pure C#, no `UnityEngine` dependency (see `docs/simulation-rules.md`
for the full contract this layer must uphold, since it must produce
identical results on both peers).

**4. Network/** — Lockstep transport. Turns `GameCommand`s and heartbeats
into packets, drives the networked tick loop, and detects
desyncs/disconnects.

**5. Input/** — Captures player (or bot) intent and turns it into
`GameCommand`s queued for the next tick. Never mutates `SimulationState`
directly.

`PointerGestureSource` is the single pointer reader for live selection and
move orders. It resolves a primary press into a tap, a pan or a long-press
lasso and publishes the target. Desktop right-clicks publish a node-only
destination to `CommandSystem`, after a current-position UI raycast blocks
both uGUI and UI Toolkit (including the HUD and node sheet). Consumers do
not repeat the world raycast. `GameManager` enables gesture routing for
selection, commands and panel arbitration; command keyboard shortcuts
remain independent. `CameraController` still reads desktop middle-drag
and scroll directly; those camera controls are outside this routing.
Thresholds are authored in millimetres and converted against screen
density, so they mean the same thing to a finger on any device.

What counts as a tap target is asked, not assumed: an opponent's villager
is not one, and the press falls through it to the node beneath. The rule
lives with the selection owner rather than in the gesture source, so the
input layer never learns game ownership.

**6. UI/** — HUD, panels, menus during a match, in uGUI. Reads
`SimulationState` to render; writes nothing to it. Being replaced by
`Assets/UI/`, which is not a subset of this layer — see below.

**7. View/** — World-space presentation of nodes and villagers
(sprites/animation/interpolation). Reads `SimulationState` to render;
writes nothing to it.

## Where the UI lives

Three trees, deliberately. The phone-UI rebuild runs the UI Toolkit stack
*alongside* the uGUI one rather than replacing it in place, so either can
be selected without a branch and nothing is lost while the new one is
still being proven.

| Tree | Holds | State |
|---|---|---|
| `Assets/Scripts/Game/UI/` | in-match uGUI: `HUDManager`, `NodePanelManager`, draft UI, world-space bars | compiled; `NodePanelManager` still owns tap arbitration, but the uGUI HUD band and the uGUI draft are both off |
| `Assets/UI/` | UI Toolkit: the whole lobby, plus the in-match HUD, node sheet, countdown, end screen and draft | live; all three toggles are on |
| `Assets/Legacy/` | the retired uGUI lobby panels | compiled, unreachable when the new lobby is on |

**Which one runs is a scene value, not a code value.** All three toggles are
`[SerializeField]` booleans, so their live setting exists only in scene
and prefab serialisation — reading the code will not tell you which UI is
on screen:

- `LobbyManager.useUIToolkitLobby` — swaps the uGUI lobby panels for
  `Assets/UI/`. Falls back to uGUI with a warning if the root is
  unassigned.
- `GameManager.useUIToolkitHUD` — activates the UI Toolkit HUD and, when
  that HUD carries a node sheet, calls `NodePanelManager.SetSuppressed`
  so the two panels never race one tap.
- `GameManager.useUIToolkitDraft` — activates the UI Toolkit draft screen.
  Deliberately **separate from the HUD toggle**: the draft and the match
  never overlap, so there is no reason a half-finished migration has to
  move them together. Falls back to the uGUI draft with a warning if the
  root is unassigned, because a draft you cannot see is a match you cannot
  start.

The two in-match toggles pick between presenters rather than between
prefabs. `DraftManager` holds an `IDraftPresenter`, the same shape as
`ICountdownPresenter` and for the same reason: the turn machine owns the
rules and must not know which stack is drawing them. The two are turned
off differently — the uGUI draft is a prefab that simply is not
instantiated, the UI Toolkit one a scene object that is not activated — so
`GameManager.CreateDraftPresenter` is the single place that guarantees
exactly one of them exists.

All three roots are built by editor commands under **Tools > Node War**, not
by hand (`Assets/UI/Editor/`). A `PanelSettings` asset and a scene object
carrying a `UIDocument` are Unity-serialised, and hand-written YAML with
guessed GUIDs is how scenes get quietly corrupted. The draft setup goes one
step further and copies the ghost prefab, the placed-piece prefab and the
sticker table off the uGUI draft prefab `GameManager` already points at,
rather than looking them up by path — a second answer to "which prefab is
the draft's" is a second thing to keep in step.

`Assets/UI/` is not a fourth layer. It sits exactly where layers 1 and 6
sit in the information flow, under the same rule as every other consumer:
it reads `SimulationState` and reaches the simulation only by enqueuing a
`GameCommand` on `InputBuffer`. `NodeSheetContent.Send` is the single
choke point for that, and nothing under `Assets/UI/` calls
`GameSimulation` or `CommandProcessor`.

Legacy code is kept compiling rather than commented out or deleted, so
that a break in it is a compiler error rather than a discovery made later.
See `Assets/Legacy/README.md`.

## Information flow

```
Pointer (mouse / touch)        or  BotPlayer
        │
        ▼
 PointerGestureSource                   (Input/)
        │  one press -> tap | pan | lasso, resolved once
        ▼
 TapRouter / SelectionSystem            (Input/)
        │  decides what the gesture meant; tracks selection
        ▼
   CommandSystem / BotPlayer            (Input/)
        │  produces GameCommand
        ▼
     InputBuffer                        (Input/)
        │  queued until next tick
        ▼
 TickRunner (local) / LockstepRunner (networked)   (Core/ / Network/)
        │  drains buffer, in order
        ▼
 CommandProcessor.ProcessCommand        (Simulation/)
        │  validates, then mutates
        ▼
     SimulationState                    (Simulation/)
        │
        ▼
 GameSimulation.SimulateTick            (Simulation/)
        │  advances the tick: movement → combat → claiming →
        │  production → healing → respawns → win-check
        ▼
     SimulationState  (updated)
        │
        ▼
   UI/ and View/  read SimulationState and render
```

`SimulationState` is the single source of truth. Nothing outside
`Simulation/` writes to it directly — see `docs/simulation-rules.md`.

### What a tick did

Beside the state it produces, a tick can report the moments it passed through,
into a `TickEventLog` (`Simulation/TickEvents.cs`):

```
SimulationState   what the world IS.        Hashed. Replicated.
TickEventLog      what just HAPPENED.       Not hashed. Output only.
```

A `TickEvent` is flat and integer-only, in the style of `GameCommand`: a type
plus a node, villager, player and value, `-1` where unused. The types are
`CombatStarted`, `VillagerDied`, `VillagerRespawned` (value 1 if paid),
`NodeNeutralised`, `NodeClaimed` and `Breach`.

- **Moments only.** A fight still going, or a node still being pushed, is read
  off `SimulationState` the way the claim bar always was. A log that had to say
  "still happening" every tick would just be the state again.
- **Output only, and off the state.** The simulation appends and never reads
  back, so a wrong entry is a cosmetic bug. The log is not a `SimulationState`
  field, so it is not in `SimulationStateHasher` and never replicates. Both
  peers write the same entries anyway, from integer state in tick order.
- **Passed in, null by default.** `SimulateTick(state, log)` and
  `ProcessCommand(state, command, log)` record only when handed a log, so tests
  and any headless run are unchanged. `ProcessCommand` takes it because a paid
  respawn happens there, outside `SimulateTick`.
- **The driver owns it.** `TickRunner` and `LockstepRunner` clear one log before
  each tick, and after the tick raise `ITickProvider.TickSimulated` with it.
  They raise it inside their catch-up loop, so a frame that runs three ticks
  raises it three times. The log is reused, so subscribers copy what they need.

## Scene structure

Three `.unity` scenes exist under `Assets/Scenes/`:

- **`Lobby.unity`** — menu flow (`LobbyManager` and its panels). No match
  or `SimulationState` exists yet.
- **`Gameplay.unity`** — an active match. `GameManager.Awake()` reads
  `MatchConnection.Instance` to decide whether to run the draft, a bot
  match, or a networked match, then builds `SimulationState` and starts
  the tick loop.
- **`GFX Testing.unity`** — a separate scene, not part of the lobby →
  match flow; used for isolated visual/graphics iteration.

Three objects are carried across the Lobby → Gameplay scene load via
`DontDestroyOnLoad`:

- **`MatchConnection`** — created when a match is started from the lobby
  (local play, bot match, or a networked connection). Holds
  `networkManager`, `localPlayerID`, `isNetworked`, `isBotMatch`, and the
  chosen `LoadoutData`. Read once by `GameManager.Awake()` in the Gameplay
  scene, then shut down (`MatchConnection.Shutdown()`) when returning to
  the lobby.
- **`PlayerProfile`** — the persistent player-identity singleton
  (username, uuid, trophies, unlocked suits/nodes, selected loadout),
  loaded from/saved to local JSON. Survives every scene transition for
  the life of the application.

- **`SceneTransition`** (`Assets/UI/Scripts/`) — created on demand by
  `SceneTransition.Load` for bot, local and online match starts and return to
  Lobby. Its full-screen sheet slides from below, loads asynchronously in
  Single mode while covered, then exits above after load completion and at
  least one destination frame (minimum cover time 0.25 s). The temporary
  `DontDestroyOnLoad` root and runtime `PanelSettings` are destroyed after
  reveal. The panel matches HUD's 390×844 width-based scaling and uses sort
  order 32768, above the other panels and uGUI. Resources UXML and a TSS
  wrapper import the existing shared theme; no Editor setup is needed.
  A full-screen picking shield and consumption of control-device input
  events block UI and direct polling during the transition without pausing
  the frame loop or network. Peers load independently; MatchConnection's
  NetworkManager ownership and the draft ready handshake are unchanged.
  Lobby tab/page navigation does not use this entry point.

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
- `CameraController` — camera rig, framing, and the one place the viewing
  side changes. See [Camera POV and sprite order](#camera-pov-and-sprite-order).
- `MatchTransitionController` — scripted transition sequences (startup
  wave, post-draft reveal, breakdown-on-game-over).
- `ITickProvider` — shared interface exposing tick-interpolation alpha so
  View code doesn't need to know whether `TickRunner` or `LockstepRunner`
  is driving the match.

**Simulation/**
- `SimulationState` — the entire mutable match state: `NodeData[]`,
  `VillagerData[]`, `PlayerData[]`, tick count, game-over/winner.
- `GameSimulation.SimulateTick` — the deterministic tick loop.
- `TickEventLog` — what a tick did, output only and unhashed. See
  [What a tick did](#what-a-tick-did).
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
- `ViewSide` — the UnityEngine-free maths behind the camera POV. Tested by
  `dotnet/NodeWar.View.Tests`.
- `SortHeight` / `SpriteDepthSorter` — height and depth order inside a node or
  draft piece.
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

### Camera POV and sprite order

The camera looks from one of four sides. A **view side** is 0..3 quarter turns,
yaw = 90 x side. It is a viewing direction, not a player ID: nothing maps a
player to a yaw by table.

- **One way in.** `CameraController.SetPOV(side, pitch)` is the only thing that
  turns the camera. The draft, a networked or bot match, the local-test player
  switch and a spectator all reach it (through `SetDraftMode` or `SetViewer`),
  and it is where the sort axis, the shared `Billboard` facing and the
  `POVChanged` event change together. `RotateView(±1)` steps a spectator through
  the same call; Q and E do it from the keyboard while `MatchConnection.isSpectator`.
- **One place picks the side.** `ResolveViewer(mode, playerID)` puts a player
  behind their own core, snapped to the nearest quarter turn from the board
  centre, so it holds for two or four players and any layout. On the default
  board that gives P0 yaw 180 and P1 yaw 0. A spectator gets side 1, side-on for
  a board that runs along Z. Core positions come from the board's
  `initialPlacements` (so the draft can resolve before any node exists) and are
  refined by `SetHomeAnchor`.
- **Framing follows the side.** The per-side default, the home anchor and its
  push toward the opponent, and the draft's rig offset are all computed along the
  side's forward direction, not from a player slot. Pan, momentum and focus work
  from ground points under the pointer, so they turn with the camera. Bounds are
  the exception on purpose: world-axis rectangles on the rig position, which say
  where on the board the camera may look and do not depend on the side.
- **Sort mode is `CustomAxis`, not `Orthographic`**, because orthographic
  sorting measures along the live camera direction and flickers between close
  sprites as the camera moves. The axis is the side's horizontal forward, so it
  points away from the camera and Unity draws the nearer sprite last. It changes
  in `SetPOV` and nowhere else, never while panning.
- **Height, then depth.** Each node (its `GFX`) and each draft piece is one
  `SortingGroup`, ordered against its neighbours by that axis. Inside it,
  `SpriteDepthSorter` sets `sortingOrder = height x 256 + depth rank`. Height is
  the authored `SortHeight` (default 0): a sprite physically on top of another
  always draws in front of it. Equal heights order by depth along the axis, ties
  by index. It recomputes on `POVChanged` and when sprites are added, never per
  frame. The ground quad is hoisted out of `GFX` at runtime so it stays its own
  group on the Ground layer.
- **Draft pieces** (`DraftPlacementPreview`, ghost and confirmed) take the
  camera's yaw and follow it if the side changes; the sticker is one height above
  the cube.

### Where a villager is, mid-edge

A villager in transit has no position of its own. `currentNodeID` is the node
it last stood on, and how far it has come is `moveProgress` counted in ticks
along the leg `movePath[movePathIndex]` to `movePath[movePathIndex + 1]`,
against `edgeWeight * moveSpeedTicks`.

Normally `movePath[movePathIndex]` and `currentNodeID` are the same node. The
one exception carries meaning: when they differ, the villager is **walking a
reversal** -- it was retargeted part-way across an edge, and is returning to
`currentNodeID` from the node named at `movePathIndex`, which it turned around
before ever reaching. Expressing the return as forward travel along the
reversed leg is what lets the tick loop stay ignorant of it: `TickMovement`
counts up and arrives exactly as it does on any other leg.

Two consequences a reader needs. Anything that zeroes `moveProgress` while
keeping `movePath` -- combat, above all -- must call `CollapseReversalLeg`
first, or the villager is left standing on a node it never reached. And the
view must anchor its interpolation to `movePath[movePathIndex]` rather than to
the sprite, or it draws a line the simulation is not walking.

## Networking model

Node War uses **lockstep**: peers never send simulation state, only
`GameCommand`s. Both machines run the identical deterministic simulation
(`GameSimulation.SimulateTick`) from the identical sequence of commands
and must therefore arrive at identical results every tick.

- **`ITickProvider`** — the shared interface (`TickAlpha` property)
  implemented by both tick drivers, so `View/` can read
  tick-interpolation progress without caring which one is active.
- **`TickRunner`** — used for local (non-networked) play, including
  bot matches. Accumulates `Time.deltaTime`, drains `InputBuffer` each
  tick, calls `CommandProcessor` then `GameSimulation.SimulateTick`
  directly with no network wait.
- **`LockstepRunner`** — used for networked matches. Same accumulator
  loop as `TickRunner`, but a tick only executes once both the local and
  the remote `TickInput` for that tick number have arrived; it enforces a
  fixed command-processing order (all of P0's commands, then all of P1's)
  and applies an input delay so local input for tick *N* is generated and
  sent ahead of when tick *N* actually simulates, to hide network latency.
- **Desync detection** — every 50 ticks
  (`LockstepRunner.DESYNC_CHECK_INTERVAL`), each peer computes
  `SimulationStateHasher.ComputeHash(simState)` and includes it in its
  next outgoing packet; the receiving peer compares it against its own
  hash for the same tick and fires `OnDesync` on mismatch.
- **Disconnect detection** — both `DraftManager` (during the draft) and
  `LockstepRunner` (during the match) track time since the last received
  packet and fire a disconnect callback if it exceeds a timeout,
  independent of heartbeat packets sent to keep the connection alive.
