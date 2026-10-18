---
system: draft
summary: Turn-based district placement that produces the agreed match board before real-time play begins.
sources:
  - Assets/Scripts/Game/Simulation/DraftState.cs
  - Assets/Scripts/Game/Core/DraftManager.cs
  - Assets/Scripts/Game/Core/IDraftPresenter.cs
  - Assets/Scripts/Game/Network/DraftSerializer.cs
verified: a1da5182
---

# Draft

## Purpose
Let each player choose district positions on a shared map before ticks begin. Deterministic placement validation and explicit transport agreement keep both peers' starting boards identical while presentation and turn clocks remain outside simulation rules.

## Interface
`DraftState(BoardConfigData board)` holds `DraftPhase phase`, `currentTurnPlayerID`, `turnNumber`, `DraftPick[] player0Picks/player1Picks`, `List<DraftPlacement> confirmedPlacements`, immutable board data, grid dimensions, row-major `bool[] occupied` and two `consecutiveTimeouts`. `DraftPick` contains `districtType`, `isConsumed`, `isFromLoadout`. `DraftPlacement` contains `playerID`, `districtType`, `gridX`, `gridZ`, `wasTimeout`. `DraftResult` contains `DraftPlacement[] placements`.

`CanPlace`, `HasLegalCell`, `LegalCells`, `IsPlayable`, `PlayerHasPlayablePick`, `GetLowestPlayablePickIndex` and `IsDraftFinished` expose availability. `AcceptLocal(playerID, slotIndex, x, z)` returns the accepted pick index or -1 without spending it. `AcceptReceived(playerID, district, x, z)` finds the first unspent matching pick, also returning index/-1. The manager validates phase/turn; these acceptance methods validate pick/cell.

`Apply(playerID, slotIndex, x, z, wasTimeout)` consumes an accepted pick, records placement/occupancy and updates timeout tracking. `AdvanceToNextValidTurn()` skips players without playable picks. `ChooseTimeout` takes player and parked slot/cell and returns a legal choice through out parameters; `FindRandomLegalCell` and `FindNearestLegalCell` support selection. `DraftPlanner.TestingPlacements(board)` supplies legal skip-draft placements.

`Core/DraftManager.Initialize` receives `BoardConfig`, `NetworkManager`, seat, network/bot flags, camera, cell prefab, local `LoadoutData` and terrain view. `SetDraftUI(IDraftPresenter)` binds presentation. UI calls `ConfirmLocalPlacement(slotIndex, x, z)`, `SetHighlightedPick`, `IsLocalPlayerTurn`, `GridToWorld` and `WorldToGrid`. Read `State`, `LocalPlayerID`, `GetLocalLoadout` and `GetRemoteLoadout`; `OnDraftComplete` is `Action<DraftResult>`, `OnDraftDisconnect` is `Action`.

`IDraftPresenter` receives `Initialize(manager, localPlayerID)`, `ShowWaiting`, `ShowInitialReveal(initialPlacements)`, `SweepIn(state, seat)`, `SweepOut`, `UpdateTimer(remaining, total)`, `OnTurnChanged(state, seat)` and `OnPlacementConfirmed(placement)`. `TryGetPendingPlacement(out slotIndex, out gridX, out gridZ)` reports timeout choice; `GetPersistentPlacements()` returns world objects handed to match transitions. Screen implementation belongs to match UI.

`DraftSerializer` encodes setup/setup-ack, ready, loadout and placement packets. `SerializeDraftPlacement(playerID, districtType, gridX, gridZ, wasTimeout)` sends district identity, not slot index; matching deserialisation returns those values. `TryDeserializeDraftLoadout` provides checked loadout decoding. `InputSerializer` carries draft acknowledgements with ready/loadout receipt and applied-placement count; the manager owns retries/progress.

## Connects to
- `board`: `MatchSetup` agreement precedes drafting; all placement checks delegate to `PlacementLegality`; output feeds `MatchFactory`.
- `match-flow`: `GameManager` starts the manager, caches loadouts/actual seat and consumes `OnDraftComplete`.
- `netcode`: `SetupAgreement` gates draft packets; serializers, acknowledgements and retries synchronise choices.
- `match-ui`: implements `IDraftPresenter`, reports parked placement and calls confirmation; draft rules remain here.
- `progression`: human effective loadout pools and era tables determine available pieces/variants.
- `bots`: configured bot pools use map base districts plus bot loadout districts and automatic picks.
- `world-view`: camera, terrain and grid/world conversion support placement presentation.
- `simulation`: drafted districts become Fixed, unowned nodes at the placer's era when the match is built.
- `economy`: district types and placer's era select the starting economic stats.
- `cloud`: ranked eligibility checks chosen capacities and placed district multisets against snapshots.
- `lobby-ui`: normalized chosen `LoadoutData` supplies pieces and the applicable free grants.
- `match-log`: resolved loadouts and ordered placements are recorded to rebuild tick-zero state.

## Invariants
No placement before map/rules agreement. A valid cell is an empty district slot, never a Core: Land for ordinary districts, Lake for Pier, never Ocean. Inactive district values are refused without runtime aliases. A refused choice consumes nothing. A pick with no legal cell is unplayable rather than forced. Presentation cannot choose rule exceptions.

## How it works
Phases are WaitingForReady → InitialReveal → ActiveDraft → Complete. P0 drafts first. Initial fixed placements occupy their cells. Each player's pool combines applicable base/loadout pieces; human effective pools and configured bot pools resolve independently. Turns advance through players with at least one playable pick; the draft ends when neither has one.

The active peer validates local confirmation, applies it and transmits the chosen district/cell. The receiving peer checks phase/seat/turn and resolves the first matching unspent pick against the same legality. Pending delivery prevents premature local progress; completion waits for outstanding ready/loadout/placement acknowledgements. The manager stops polling before handing control to the playing driver so it cannot swallow gameplay packets from the shared inbound queue.

On timeout, a still-legal parked piece wins. Otherwise the lowest playable pick gets a legal cell from seed `turnNumber * 7919 + playerID * 31`; the chosen placement is transmitted. Consecutive timeouts can trigger disconnect for networked human matches; configured turn duration and timeout limits live outside tick simulation.

## Decisions
- Pure `DraftState` owns spatial/pick rules; the MonoBehaviour owns clocks, networking and phase orchestration.
- A presenter interface lets either UI stack draw the same phase without coupling the manager to concrete screens.
- Sending district identity lets peers resolve their own equivalent pick index.

## Pressure points
The manager combines retry bookkeeping, clocks, bot choices and world presentation helpers. Shared destructive inbound polling requires a precise handoff to gameplay. Acceptance helpers do not enforce phase/turn themselves, so callers must preserve the manager's guards. Duplicate district picks depend on first-unspent resolution; extending pick-specific metadata would need a stronger wire identity.

## Change recipes
- Add a district/pool rule: update active roster, shared legality and board validation; test no-cell skips, previews and timeout fallback.
- Add draft payload: update `DraftSerializer`, validation, retry/ack behavior and protocol version if layout changes; test malformed packets and duplicate delivery.
- Add UI: implement `IDraftPresenter` in the active stacks, including pending-placement reporting; keep timers/pixels from mutating placement rules.
- Change result data: coordinate `DraftResult`, player setup era resolution, `MatchFactory` and recording.

## Tests
`DraftLegalityTests` covers pure acceptance, occupancy, legal cells, timeout and skip behavior; board/setup and draft wire tests cover shared data through `dotnet/NodeWar.sln`. Unity verification remains necessary for both presenter stacks, parked-piece input, timers and phase/network handoff.
