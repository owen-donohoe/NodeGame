---
system: board
summary: Agreed map identity, placement legality and the single builder for every match's starting state.
sources:
  - Assets/Scripts/Game/Config/
  - Assets/Scripts/Game/Simulation/BalanceHasher.cs
  - Assets/Scripts/Game/Simulation/BoardConfigData.cs
  - Assets/Scripts/Game/Simulation/BoardHasher.cs
  - Assets/Scripts/Game/Simulation/DistrictRoster.cs
  - Assets/Scripts/Game/Simulation/MapAuthoringRules.cs
  - Assets/Scripts/Game/Simulation/MatchFactory.cs
  - Assets/Scripts/Game/Simulation/MatchSetup.cs
  - Assets/Scripts/Game/Simulation/PlacementLegality.cs
  - Assets/Scripts/Game/Simulation/PremadeMaps.cs
verified: a1da5182
---

# Board

## Purpose
Make live peers, recorded replay and headless/referee runs start from the same legal board. Map agreement precedes drafting, so a placement cannot refer to different terrain on the two machines.

## Interface
`BoardConfigData` contains grid dimensions, `TerrainType[] terrain`, `bool[] districtSlots`, `InitialDistrictPlacement[] initialPlacements`, each player's base draft districts, starting villagers/resources, default link weight and five ownership path multipliers. Cell arrays use `z * gridCols + x`. Each initial placement has grid X/Z, district type, owner (-1/0/1) and signed claim bar.

`Config/BoardConfig` is a Unity asset exposing `MapId` and plain `Data`, plus presentation/draft settings. `Config/GameBalance.LoadShared()` loads the shared balance asset; `Data` returns `GameBalanceData`. Assets remain outside Simulation. `PremadeMaps.Catalog` implements `IBoardCatalog`; the shipped map ID is `hourglass-01`.

`MatchSetup` is immutable `{ MapId, BoardHash, SimulationVersion, BalanceHash }`. `ForShippedMap(mapId, balanceHash)` constructs the local identity; `Verify(received, catalog, localSimulationVersion, ...)` checks agreement against local content. `BoardHasher.Hash(board)` fingerprints map data; `BalanceHasher.Hash(balance)` fingerprints tuning, including ordered suit/district era entries. The `simulation`-owned `SimulationVersion.Current` identifies rule semantics. `DistrictRoster.IsActive(type)` defines the runtime district acceptance set.

`MatchFactory.Configure(balance, board)` installs static balance on `GameSimulation`/`CommandProcessor` and path multipliers. Call it before running a built match. `Build(balance, board, DraftPlacement[] draft, PlayerSetup[] players)` returns new state; `Fill(state, balance, board, draft, players)` populates an existing instance. Both use `RequireBuildable` before mutation. `PlayerSetup` carries drafted suit/district integer arrays plus enum-indexed suit/district era arrays.

`CellToNode` returns the sparse cell→ID mapping; `NodeToCell` reverses it. `BuildNodes`, `CellLinks`, `InitializePlayers` and `InitializeVillagers` are construction helpers. `FindCoreNodeID(state, playerID)` requires exactly two Cores on different rows and assigns seats by row.

`PlacementLegality.CanPlace(board, occupied, district, x, z)` and `IsDraftable` are the shared spatial predicate. `MapAuthoringRules.ValidateBoard`, `ValidateDraft` and `ValidateAuthoredMap` return booleans with error text; `HasLandRoute` checks Core connectivity without Piers. Builder refusal throws `ArgumentException`.

## Connects to
- `simulation`: receives tick-0 state, `boardHash`, default link tuning and installed balance.
- `economy`: initial pools are capped; district/suit stats come from configured balance.
- `draft`: uses legality/slot masks and returns placements for `MatchFactory`.
- `match-flow`: `GameManager` calls the builder, including skip-draft/testing placement paths.
- `netcode`: `SetupAgreement` proposes/verifies `MatchSetup` before draft packets are honoured; live wire carries identity, not board arrays.
- `match-log`: records board and setup; replay reconstructs through `MatchFactory`.
- `cloud`: referee checks map against its catalog and balance against a content-addressed export.
- `progression`: selected suit/district variants become `PlayerSetup` era tables.
- `tooling`: map validation, balance exports and content/hash tests guard shipped data.
- `bots`: rig setup configures and builds the same catalog board through `MatchFactory`.
- `input`: Core positions and dimensions supply camera anchors and bounds.
- `world-view`: sparse cell/node mappings and terrain drive ground and district placement.

## Invariants
There is one recorded-match tick-0 builder. IDs follow ascending cell order with water gaps removed; links join orthogonally adjacent node-bearing cells only. Land has nodes, Ocean never does, Lake only with a drafted Pier. Runtime/log/draft accept active district values only; reserved retired numbers never become aliases. Board data does not belong in mutable match state except its identity and constructed nodes.

## How it works
Peers independently resolve map ID from the catalog, compare setup, then draft against identical slot/terrain arrays. Legality requires an empty non-Core district slot, ordinary districts on Land and Pier on Lake. Authored maps additionally enforce mirrored layout, a land-only route and 14–20 possible nodes counting legal Piers; buildable fixture validation is distinct from shipped-map validation.

After validation, the builder creates a sparse graph, applies fixed and drafted placements, and initialises players and villagers. Drafted districts are unowned, Fixed and use their placer's era; board fixed placements use era 0. Non-Fixed simulation slots can instead upgrade to the claimer's drafted category choice. Only Cores start player-owned. Era tables default to zero when absent.

Identity has separate purposes: board hash covers dimensions, terrain, slots, fixed placements, both draft pools, starting numbers and path/link tuning; balance hash covers numeric tuning; simulation version covers rule behavior; protocol version covers wire layout. Node terrain and `state.boardHash` enter state hashing unconditionally. Balance does not enter the state hash and must agree before play.

## Decisions
- Catalog identity avoids sending an arbitrary board over live transport and lets the server vouch for maps.
- Plain configuration data keeps asset loading and Unity types outside deterministic rules.
- Shared construction prevents live/referee/testing starts from silently diverging.

## Pressure points
Process-wide configured statics prevent concurrent matches with different settings; referee replay uses a lock. `Build`/`Fill` and `Configure` are separate responsibilities and callers can omit configuration. Sparse cell/node mappings require care in UI/tools. Board content and frozen recording layouts must evolve together. State hashing's separate balance agreement remains a dependency, not an intrinsic guarantee.

## Change recipes
- Add a map: author catalog data, validate authored symmetry/land route/node limits and all placements, then test builder and setup refusal paths; ship the same catalog to referee.
- Add board fields: update `BoardHasher`, builder defaults and recording. Preserve frozen BOARD; use BOARD_V2 or a new tag for unsupported data.
- Add tuning: update `GameBalanceData`, `BalanceHasher`, safety checks and client/server export parity; balance-only edits need no simulation bump.
- Change same-input behavior, including tick-0 construction: bump `SimulationVersion.Current` in the same commit and coordinate baseline pins. Wire-layout edits separately bump `ProtocolVersion.Current`; a deliberate baseline re-pin also needs simulation-version coordination.

## Tests
Terrain/map, placement legality, MatchFactory/setup, BoardHasher, BalanceHasher, release-content and determinism-baseline suites run through `dotnet/NodeWar.sln`. Replay tests enforce recorded identity. These do not verify Unity asset/scene wiring or map presentation; inspect those in the editor.
