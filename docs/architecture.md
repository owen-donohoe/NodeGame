---
type: Architecture
title: Architecture
description: The seven layers of Assets/Scripts/, where the three UI trees live and which one runs, how information flows between them, and the lockstep networking model.
tags: [architecture, layers, networking, lockstep, ui]
generated: { by: human:DonohoeCUA, at: 2026-08-30T17:15:16-04:00 }
verified:
  # full history: docs/verification-log.md
  - { by: claude-opus-5, at: 2026-09-14T00:00:00Z }
verified_at_commit: 2241e47
status: stable
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

Two objects are carried across the Lobby → Gameplay scene load via
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

## Key classes per layer

Moved to [`class-map.md`](class-map.md). It is the catalogue -- what each named
class is for -- and it was the half of this document that delegated agents read
in full and did not need. The four `Outline/` sources moved with it.

## Where a villager is, mid-edge

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
