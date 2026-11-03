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
  - Assets/Scripts/Game/Simulation/ShoalPath.cs
verified: d0fa993c
---

# Board

## Purpose
Make live peers, recorded replay and headless/referee runs start from the same legal board. Map agreement precedes drafting, so a placement cannot refer to different terrain on the two machines.

## Interface
`BoardConfigData` contains grid dimensions, `TerrainType[] terrain`, `bool[] districtSlots`, `MapSymmetry symmetry` (default FlipZ), `InitialDistrictPlacement[] initialPlacements`, each player's base draft districts, starting villagers/resources, default link weight and five ownership path multipliers. Cell arrays use `z * gridCols + x`. Each initial placement has grid X/Z, district type, owner (-1/0/1) and signed claim bar.

`Config/BoardConfig` is a Unity asset exposing `MapId` and plain `Data`, plus presentation/draft settings. `Config/GameBalance.LoadShared()` loads the shared balance asset; `Data` returns `GameBalanceData`. Assets remain outside Simulation. `PremadeMaps.Catalog` implements `IBoardCatalog`; the shipped map ID is `hourglass-01`.

`MatchSetup` is immutable `{ MapId, BoardHash, SimulationVersion, BalanceHash }`. `ForShippedMap(mapId, balanceHash)` constructs the local identity; `Verify(received, catalog, localSimulationVersion, ...)` checks agreement against local content. `BoardHasher.Hash(board)` fingerprints map data; `BalanceHasher.Hash(balance)` fingerprints tuning, including ordered suit/district era entries. The `simulation`-owned `SimulationVersion.Current` identifies rule semantics. `DistrictRoster.IsActive(type)` defines the runtime district acceptance set.

`MatchFactory.Configure(balance, board)` installs static balance on `GameSimulation`/`CommandProcessor` and path multipliers. Call it before running a built match. `Build(balance, board, DraftPlacement[] draft, PlayerSetup[] players)` returns new state; `Fill(state, balance, board, draft, players)` populates an existing instance. Both use `RequireBuildable` before mutation. `PlayerSetup` carries drafted suit/district integer arrays plus enum-indexed suit/district era arrays.

`ShoalPath.Derive(board)` returns a starting-board Lake path mask, or null for unreachable Land anchors; paths separate before mirroring. `ShoalPath.Connect(board, resolvedAfterShoals, placed)` returns an unmirrored filler mask connecting resolved Land and drafted Piers. `MatchFactory.ResolveTerrain(board, placed, draftComplete)` returns new terrain: non-Piers raise Land immediately; completion raises shoals, then filler. `CellToNode` maps sparse cells to IDs; `NodeToCell` reverses it. `BuildNodes`, `CellLinks`, `InitializePlayers` and `InitializeVillagers` construct state. `FindCoreNodeID(state, playerID)` requires two Cores on different rows and assigns seats by row.

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
There is one recorded-match tick-0 builder. On authored maps every node reaches the Core after drafting. IDs follow ascending cell order; links join orthogonal neighbours only. Resolved Land has nodes; remaining Lake has nodes only with a drafted Pier. Ocean and DeepLake never have nodes, slots or placements. Runtime/log/draft accept active district values only; retired numbers never become aliases. Mutable match state holds board identity and constructed nodes, not configuration.

## How it works
Peers independently resolve map ID from the catalog, compare setup, then draft against identical slot/terrain arrays. Legality requires an empty non-fixed slot: ordinary districts accept Land or Lake; Piers require Lake outside the shoal mask. Fixed placements require authored Land. Authored maps enforce symmetry, reachable anchors, a resolved land route and a 14–20 node budget counting Land, shoals and remaining Lake slots once, before connecting filler.

After validation, the builder creates a sparse graph, applies fixed and drafted placements, and initialises players and villagers. Drafted districts are unowned, Fixed and use their placer's era; board fixed placements use era 0. Non-Fixed simulation slots can instead upgrade to the claimer's drafted category choice. Only Cores start player-owned. Era tables default to zero when absent.

Filler repeatedly runs multi-source integer Dijkstra from the high-Core component to the first unconnected node. Entering undrafted Lake costs `10 + 20 * max(0, landSides - 1)`, counting existing nodes and prior filler. Equal costs use lower predecessor, then cell index. Piers are targets, never filler. Ocean/DeepLake barriers leave sealed components unchanged.

Identity has separate purposes: board hash covers dimensions, terrain, symmetry, slots, fixed placements, both draft pools, starting numbers and path/link tuning; balance hash covers numeric tuning; simulation version covers rule behavior; protocol version covers wire layout. Node terrain and `state.boardHash` enter state hashing unconditionally. Balance does not enter the state hash and must agree before play.

## Decisions
- Catalog identity avoids sending an arbitrary board over live transport and lets the server vouch for maps.
- Plain configuration data keeps asset loading and Unity types outside deterministic rules.
- Shared construction prevents live/referee/testing starts from silently diverging.

## Pressure points
Process-wide configured statics prevent concurrent matches with different settings; referee replay uses a lock. `Build`/`Fill` and `Configure` are separate responsibilities and callers can omit configuration. Sparse cell/node mappings require care in UI/tools. Board content and frozen recording layouts must evolve together. State hashing's separate balance agreement remains a dependency, not an intrinsic guarantee.

## Change recipes
- Add a map: author catalog data, validate authored symmetry/land route/node limits and all placements, then test builder and setup refusal paths; ship the same catalog to referee.
- Add board fields: update `BoardHasher`, builder defaults and recording. Preserve BOARD and BOARD_V2; BOARD_V3 carries symmetry.
- Add tuning: update `GameBalanceData`, `BalanceHasher`, safety checks and client/server export parity; balance-only edits need no simulation bump.
- Change same-input behavior, including tick-0 construction: bump `SimulationVersion.Current` in the same commit and coordinate baseline pins. Wire-layout edits separately bump `ProtocolVersion.Current`; a deliberate baseline re-pin also needs simulation-version coordination.

## Tests
Terrain/map, placement legality, MatchFactory/setup, BoardHasher, BalanceHasher, release-content and determinism-baseline suites run through `dotnet/NodeWar.sln`. Replay tests enforce recorded identity. These do not verify Unity asset/scene wiring or map presentation; inspect those in the editor.
