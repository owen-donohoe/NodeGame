---
type: Architecture
title: Architecture
description: The seven layers of Assets/Scripts/, how information flows between them, and the lockstep networking model.
tags: [architecture, layers, networking, lockstep]
generated: { by: human:DonohoeCUA, at: 2026-08-30T17:15:16-04:00 }
verified:
  - { by: claude-opus-5, at: 2026-08-31T00:00:00Z }
verified_at_commit: e90548a
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
---

# Architecture

Node War is a 1v1 real-time strategy game built in Unity 6 (namespace
`NodeWar`), with lockstep peer-to-peer networking. All gameplay code lives
under `Assets/Scripts/`, split into seven layers.

## The seven layers

```
Assets/Scripts/
  Lobby/       Layer 1
  Game/
    Core/      Layer 2
    Simulation/Layer 3
    Network/   Layer 4
    Input/     Layer 5
    UI/        Layer 6
    View/      Layer 7
```

**1. Lobby/** — Pre-match menu flow: game mode selection, player profile,
loadout/node/suit selection. Runs entirely in the Lobby scene, before a
`SimulationState` exists.

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

Exactly one component reads a pointer device: `PointerGestureSource`. It
resolves a press once into a tap, a pan or a long-press lasso, raycasts
once for what was under it, and publishes the outcome. Everything else in
this layer consumes that outcome rather than polling input itself.
Thresholds are authored in millimetres and converted against screen
density, so they mean the same thing to a finger on any device.

**6. UI/** — HUD, panels, menus during a match. Reads `SimulationState` to
render; writes nothing to it.

**7. View/** — World-space presentation of nodes and villagers
(sprites/animation/interpolation). Reads `SimulationState` to render;
writes nothing to it.

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
  ActiveDraft → Complete`).
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
- `PointerGestureSource` — the only device reader. Resolves a press into a
  tap, pan or long-press lasso and publishes it.
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

**UI/**
- `HUDManager` — top-level in-match HUD.
- `NodePanelManager` — per-node detail/action panel.
- `DraftUI` — draft-phase interface.
- `GameOverPanel` — end-of-match result display.
- `SelectionLasso` — draws the in-progress lasso stroke.
- `LassoArmedCue` — ring pulse confirming the long press armed.
- `SafeAreaFitter` — insets a rect to `Screen.safeArea`; the only reader
  of it in the project.

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
