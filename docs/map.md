---
summary: Every Node War system, every connector between them, the three flows, the cross-cutting versions and boundaries, and the top pressure points.
sources:
  - docs/systems/
verified: 41a0df69
---

# System map

The orchestrator's view: what exists, where, and how it connects, without code. Each
system has a card in `docs/systems/`; this file never repeats a card's internals.

## The shape

Two peers run the same deterministic simulation from the same commands (lockstep).
Presentation reads simulation state and never writes it; every change is a command.
A Unity Gaming Services backend matches ranked players, checks uploaded logs with
a headless referee, and settles eligible results. Local profile/settings remain
client data; rating, ranked outcomes and inventory remain server-owned.

## Systems

| System | Lives in | Role |
|---|---|---|
| `simulation` | `Game/Simulation/` | Match state, tick loop, movement, combat, claiming, win; state hash |
| `economy` | `Game/Simulation/` | Resources, production, district behaviours, banks, recruiting, minions |
| `commands` | `Game/Simulation/Commands.cs`, `CommandProcessor.cs`, `Game/Input/CommandSystem.cs`, `InputBuffer.cs`, UI `CommandEligibility.cs` | Command vocabulary, intent buffering and authoritative application |
| `board` | `Game/Simulation/` factory/map/hash types, `Game/Config/` | Terrain, tick-zero construction, balance assets and content fingerprints |
| `draft` | `Game/Simulation/DraftState.cs`, `Game/Core/` draft manager/presenter, `Game/Network/DraftSerializer.cs` | Placement rules, turn orchestration and draft contract/wire data |
| `match-flow` | `Game/Core/` lifecycle/driver/channel types, `Game/Debug/` | Lifecycle, local ticking, persistent connection and countdown/channel contracts |
| `netcode` | `Game/Network/` except draft serializer | Lockstep transport, input delay, speculation, holds, emote delivery |
| `input` | `Game/Input/` gesture/selection routing, Core camera, Lobby settings data | Physical input, selection, camera and bindings; command application belongs to `commands` |
| `world-view` | `Game/View/`, `Game/UI/Selection/`, `Game/UI/Worldspace/` | World art, units, paths, outlines, indicators and paint rendering |
| `match-ui` | `Assets/UI/Scripts/Gameplay/` except command eligibility; `Game/UI/` panels/draft; match layouts/styles | HUD, node sheet, draft screen, end card and screen implementations |
| `bots` | `Game/Input/BotPlayer.cs`, `BotSuitChoice.cs`, `dotnet/NodeWar.BalanceRig/` | Scripted opponent and headless diagnostics |
| `match-log` | `Assets/Scripts/MatchLog/`, Backend local store, Cloud referee/balance catalog | Format, recorder, replay and consistency verification |
| `backend-client` | `Assets/Scripts/Backend/` client services and shared contracts/presenters | Async services, fakes, account-scoped state and ranked controllers |
| `cloud` | `dotnet/NodeWarCloud/` allocation/storage/settlement/module types and Matchmaker queue | Cloud functions, authoritative records, settlement and matchmaking; referee belongs to `match-log` |
| `progression` | `dotnet/NodeWar.Progression/`, Backend catalog and shared eligibility/ID policies | Pure rating/settlement, catalog, variants, arena and inventory policies |
| `lobby-ui` | `Assets/UI/Scripts/` lobby types, lobby layouts, `Assets/Scripts/Lobby/` except input settings, `Assets/Legacy/` | Pages, launch/account flows and local profile/loadout data |
| `ui-art` | `Assets/UI/` theme/icon types, fonts, shared/lobby styles and theme drawer | Semantic art lookup, procedural fallback and theme authoring |
| `tooling` | `dotnet/NodeWar.sln`, `Assets/Tests/`, `scripts/`, CI and shared Editor infrastructure | Verification procedures, guards, receipts and build/setup tooling |

`Game/...` abbreviates `Assets/Scripts/Game/...`. Card `sources:` identify ownership;
references to a neighbour's types describe consumption, not a second owner.

## Connector index

Each reciprocal card relationship appears once below. These are interface/data
neighbours, not permission to mutate a neighbour's state. The explicit one-way
dependency is `bots` → `cloud`: the rig reads exported balance files without
calling Cloud Code.

| System | Other neighbours (each pair listed once) |
|---|---|
| `backend-client` | `bots`, `cloud`, `lobby-ui`, `match-flow`, `match-log`, `match-ui`, `netcode`, `progression` |
| `board` | `bots`, `cloud`, `draft`, `economy`, `input`, `match-flow`, `match-log`, `netcode`, `progression`, `simulation`, `tooling`, `world-view` |
| `bots` | `commands`, `draft`, `economy`, `lobby-ui`, `match-flow`, `simulation`, `tooling` |
| `cloud` | `draft`, `economy`, `match-log`, `netcode`, `progression`, `tooling` |
| `commands` | `economy`, `input`, `match-flow`, `match-log`, `match-ui`, `netcode`, `simulation`, `tooling`, `world-view` |
| `draft` | `economy`, `lobby-ui`, `match-flow`, `match-log`, `match-ui`, `netcode`, `progression`, `simulation`, `world-view` |
| `economy` | `match-ui`, `progression`, `simulation`, `tooling` |
| `input` | `lobby-ui`, `match-flow`, `match-ui`, `simulation`, `tooling`, `world-view` |
| `lobby-ui` | `match-flow`, `match-ui`, `netcode`, `progression`, `ui-art` |
| `match-flow` | `match-log`, `match-ui`, `netcode`, `simulation`, `world-view` |
| `match-log` | `netcode`, `progression`, `simulation`, `tooling` |
| `match-ui` | `netcode`, `simulation`, `tooling`, `ui-art`, `world-view` |
| `netcode` | `simulation`, `tooling`, `world-view` |
| `progression` | `simulation`, `tooling` |
| `simulation` | `tooling`, `world-view` |
| `tooling` | `ui-art`, `world-view` |
| `ui-art` | `world-view` |

## Flow 1: a match starts

1. `lobby-ui` edits local choices and freezes refreshed rank/equipment through
   `MatchLauncher`. Ranked uses `backend-client` queue/rendezvous controllers and
   `cloud` allocation; private/local modes remain unranked. Server slot order and
   match identity cross the scene boundary through `match-flow`'s `MatchConnection`.
2. `netcode` connects the peers and agrees the setup (map, board hash, simulation
   version, balance hash) before any draft packet is accepted.
3. `draft` runs placement turns; `match-ui` draws them; picks travel through `netcode`.
4. `board`'s `MatchFactory` builds the tick-0 `SimulationState` from map, draft and
   loadouts. The same factory serves live matches, the referee and the rig.

## Flow 2: a tick

1. `input` turns pointer and key intent into `GameCommand`s on the `InputBuffer`
   (`commands`); `bots` do the same for the bot.
2. `match-flow`'s local `TickRunner` drains buffer order; `netcode`'s `LockstepCore`
   stamps sender ownership and applies all P0 commands before P1. Each calls
   `CommandProcessor` then `SimulateTick` (`simulation`, `economy`).
3. The tick emits events; `world-view` and `match-ui` read state and events to draw.
   Nothing in presentation writes state.
4. `netcode` exchanges periodic confirmed state hashes; a mismatch is a desync.
   Speculation predicts idle input, then restores and replays; speculative cues
   can draw, but recording/hashing observes only the confirmed pass.
5. `match-flow` connects confirmed command/hash events to `match-log`'s recorder
   for drafted matches; Testing mode does not record.

## Flow 3: a match ends

1. A played win comes from `simulation`; ranked surrender/disconnect outcomes come
   from `cloud`. `match-flow` shows the appropriate end card through `match-ui`.
2. The log is saved locally and, for ranked, uploaded by `backend-client` until the
   server answers.
3. `cloud` checks roster, build/map and snapshot eligibility, then uses `match-log`'s
   referee over `board`/`simulation`. Agreeing winning reports settle through
   `progression`; disagreement disputes, and lone reports can time out to void.
   Committed forfeits/abandonments and two unfinished logs have separate server
   resolution paths. `MatchSettler` owns retry-safe persistence and inventory updates.
4. `backend-client` follows the result for the end card and refreshes lobby state.

## Connectors that cross a trust or build boundary

- Shared sources under `Game/Simulation/` compile without UnityEngine into peers,
  tests, rig and referee. The `commands`, `board` and `draft` cards also cover
  Unity-facing producers/assets/managers outside that folder; their entire cards
  are not headless assemblies.
- `Backend/Shared/` DTOs, contracts and policies are linked into Unity/server builds.
  Shared source reduces duplication; deployed build/version/content agreement is
  still required. Async calls can fail or refuse; accepted state is account-scoped.
- Clients read their own protected Cloud Save records; only `cloud` writes them.
- Private match records, presence and uploaded logs are server-only. Matchmaker's
  allocator callbacks reject player identities; ranked-match endpoints require membership.
- `NodeWar.Progression` uses doubles outside simulation; tick rules remain integer-only.
- Skins and other cosmetics travel to the opponent and into the log but never into
  `simulation` or the hash.
- Which UI tree runs (UI Toolkit or legacy uGUI) is a scene value, not code.

## Versions: who bumps what

| Version | Bump when | Owner |
|---|---|---|
| `SimulationVersion` | Same-input semantics or deliberate baseline re-pins change; construction coordinates with rules | `simulation` (construction through `board`) |
| Balance / content hash | Balance data changes (checked in the handshake) | `board` |
| `ProtocolVersion` | Wire layout, transport compatibility or setup/grant semantics become incompatible | `backend-client` constant; `netcode` consumers |
| Match log format and chunk tags | Framing breaks (format) or a payload changes (new tag) | `match-log` |
| Catalog IDs | Preserve identities; retire rather than reuse | `progression` |
| Settings version | Device preference schema/migration changes independently of protocol | `input`, consumed by `lobby-ui`/`match-ui` |

## Top pressure points

Each card lists its own; these are the ones that shape what can be built next.

- **Two-seat interfaces recur.** Signed claiming, two Cores/loadouts, P0-before-P1
  command order and pairwise settlement couple `simulation`, `board`, `draft`,
  `netcode`, `match-log` and `cloud`.
- **Configuration is process-global.** Simulation/path statics require sequential
  rig runs and a referee lock; construction/configuration remain separate calls.
- **Presentation reads live arrays.** Views, sheet bindings and selections must
  survive rollback/population changes; `GameManager` couples their composition and cleanup.
- **The referee proves consistency, not authenticity.** A client can fabricate both
  players' commands and recompute hashes until per-player command signatures exist.
- **Persistence is not cross-player transactional.** Settlement needs locks and
  idempotency; first-record creation, account deletion and retention remain pressure points.
- **Three UI trees coexist** (UI Toolkit, legacy uGUI in-match, retired uGUI lobby);
  every presentation change must know which one runs.
- **Catalog and suit eligibility differ.** Generic six-era grants coexist with
  three-node suit trees. IDs, migrations, equip/referee checks and balance exports
  must stay coordinated; ownership alone does not prove current equip availability.
- **Bots have limited action coverage.** The policy omits recruitment, upgrades,
  minion forging and remote collection, constraining strategic balance conclusions.
