---
summary: Every Node War system, every connector between them, the three flows, the cross-cutting versions and boundaries, and the top pressure points.
sources:
  - docs/systems/
verified: a1da5182
---

# System map

The orchestrator's view: what exists, where, and how it connects, without code. Each
system has a card in `docs/systems/`; this file never repeats a card's internals.

## The shape

Two peers run the same deterministic simulation from the same commands (lockstep).
Presentation reads simulation state and never writes it; every change is a command.
A Unity Gaming Services backend sits beside the match: it matches players, receives
both players' match logs, replays them headlessly as a referee, and settles rating,
rank and inventory. The lobby talks only to that backend and to the match launcher.

## Systems

| System | Lives in | Role |
|---|---|---|
| `simulation` | `Game/Simulation/` | Match state, tick loop, movement, combat, claiming, win; state hash |
| `economy` | `Game/Simulation/` | Resources, production, district behaviours, banks, recruiting, minions |
| `commands` | `Simulation/Commands`, `Input/`, `Network/InputSerializer` | Player intent as `GameCommand`, buffered and applied |
| `board` | `Simulation/MatchFactory`, `PremadeMaps`, `Game/Config/` | Maps, terrain, the tick-0 board, balance data, version hashes |
| `draft` | `Simulation/DraftState`, `Core/DraftManager` | Pre-match placement turns |
| `match-flow` | `Game/Core/` | Match lifecycle, tick drivers, scenes, presenters |
| `netcode` | `Game/Network/` | Lockstep transport, input delay, speculation, holds, emotes |
| `input` | `Game/Input/`, camera | Gestures, selection, orders, camera |
| `world-view` | `Game/View/` | World presentation of the board, nodes, villagers, paths, indicators |
| `match-ui` | `Assets/UI/Scripts/Gameplay/`, `Game/UI/` | In-match HUD, node sheet, draft screen, end card |
| `bots` | `Input/BotPlayer`, `dotnet/NodeWar.BalanceRig` | The scripted bot and the headless rig |
| `match-log` | `Assets/Scripts/MatchLog/`, referee | Log format, recorder, replay, referee |
| `backend-client` | `Assets/Scripts/Backend/` | Client services, fakes, ranked flow presenters |
| `cloud` | `dotnet/NodeWarCloud/` | Cloud Code functions, records, settlement, matchmaking |
| `progression` | `dotnet/NodeWar.Progression/`, catalog | Catalog, variants, inventory rules, rating, arenas |
| `lobby-ui` | `Assets/UI/Scripts/`, `Assets/Scripts/Lobby/` | Lobby screens and their local data |
| `ui-art` | `Assets/UI/` theme and icons | UI art theme, icon kinds, art lookup |
| `tooling` | `dotnet/*.Tests`, `scripts/`, CI, `Editor/` | Tests, guards, CI, editor tooling |

## Flow 1: a match starts

1. `lobby-ui` builds a loadout and asks `match-flow` to launch (local, LAN, private
   code, or ranked through `backend-client`'s queue presenter and `cloud`'s
   matchmaker and rendezvous).
2. `netcode` connects the peers and agrees the setup (map, board hash, simulation
   version, balance hash) before any draft packet is accepted.
3. `draft` runs placement turns; `match-ui` draws them; picks travel through `netcode`.
4. `board`'s `MatchFactory` builds the tick-0 `SimulationState` from map, draft and
   loadouts. The same factory serves live matches, the referee and the rig.

## Flow 2: a tick

1. `input` turns pointer and key intent into `GameCommand`s on the `InputBuffer`
   (`commands`); `bots` do the same for the bot.
2. The tick driver in `match-flow` (local) or `netcode` (lockstep) drains both
   players' commands in a fixed order into `CommandProcessor`, then runs
   `SimulateTick` (`simulation`, `economy`).
3. The tick emits events; `world-view` and `match-ui` read state and events to draw.
   Nothing in presentation writes state.
4. Every 50 ticks `netcode` exchanges the state hash; a mismatch is a desync.
5. `match-log`'s recorder captures the applied commands and hashes.

## Flow 3: a match ends

1. `simulation` decides the winner; `match-flow` shows the end card (`match-ui`).
2. The log is saved locally and, for ranked, uploaded by `backend-client` until the
   server answers.
3. `cloud` checks the log against its match record, replays it with `match-log`'s
   referee (built on `board` and `simulation`), and when both reports agree settles
   through `progression` (rating, RR, arena, inventory).
4. `backend-client` follows the result for the end card and refreshes lobby state.

## Connectors that cross a trust or build boundary

- `simulation`, `economy`, `commands`, `board`, `draft` compile into
  `NodeWar.Simulation` (no UnityEngine) and also into the .NET test projects, the rig
  and the Cloud Code module. Anything Unity-only there breaks all four.
- `Backend/Shared/` compiles into Unity and into `cloud`: records, catalog rules and
  protocol versions are shared source, so the client and server cannot disagree on a
  rule, only on data.
- Clients read their own protected Cloud Save records; only `cloud` writes them.
- Skins and other cosmetics travel to the opponent and into the log but never into
  `simulation` or the hash.
- Which UI tree runs (UI Toolkit or legacy uGUI) is a scene value, not code.

## Versions: who bumps what

| Version | Bump when | Owner |
|---|---|---|
| `SimulationVersion` | The same inputs produce a different result | `board` |
| Balance / content hash | Balance data changes (checked in the handshake) | `board` |
| `ProtocolVersion` | A packet or loadout wire shape changes | `netcode`, `backend-client` |
| Match log format and chunk tags | Framing breaks (format) or a payload changes (new tag) | `match-log` |
| Catalog IDs | Never renamed or reused once deployed | `progression` |

## Top pressure points

Each card lists its own; these are the ones that shape what can be built next.

- **Two players are built in.** Claiming is a signed bar between P0 and P1, the win
  check and pathfinding assume one opponent, the draft alternates, maps mirror,
  lockstep orders P0 then P1, logs hold two loadouts, settlement is pairwise. 2v2 and
  free-for-all are a redesign across `simulation`, `draft`, `netcode`, `match-log`,
  `cloud` and `match-ui`, not a feature.
- **Presentation reads state directly.** `world-view` types read `SimulationState`
  in place, so a lobby that shows districts and villagers without a match needs a
  separate presentation-only path.
- **The referee proves consistency, not authenticity.** A client can fabricate both
  players' commands and recompute hashes until per-player command signatures exist.
- **UGS is event-driven only.** No inbound webhooks, push messages are best-effort,
  20 indexed Cloud Save keys project-wide, 600 Cloud Code calls per player-minute.
  Social and economy features must store first and push second.
- **Three UI trees coexist** (UI Toolkit, legacy uGUI in-match, retired uGUI lobby);
  every presentation change must know which one runs.
- **Variants are per-era power tiers today** and are being redesigned as sidegrades
  whose era only gates unlocking; balance tables, catalog IDs and eligibility move
  together.
