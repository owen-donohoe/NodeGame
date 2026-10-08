---
type: Determinism Contract
title: Simulation Determinism Contract
description: The rules Assets/Scripts/Game/Simulation/ must uphold so both peers produce identical state from identical commands.
tags: [simulation, determinism, lockstep, desync]
generated: { by: human:DonohoeCUA, at: 2026-08-30T17:15:16-04:00 }
verified:
  # full history: docs/verification-log.md
  - { by: claude-sonnet-5-5, at: 2026-10-08T17:01:14Z }
verified_at_commit: bdb967ed1524de151f220a3be1733bec8177e56d
status: stable
sources:
  - id: sim-loop
    resource: Assets/Scripts/Game/Simulation/GameSimulation.cs
    title: GameSimulation.SimulateTick and AssignAllCombatTargets
  - id: sim-state
    resource: Assets/Scripts/Game/Simulation/SimulationState.cs
    title: SimulationState, NodeData, VillagerData, PlayerData
  - id: hasher
    resource: Assets/Scripts/Game/Simulation/SimulationStateHasher.cs
    title: SimulationStateHasher.ComputeHash
  - id: pathfinding
    resource: Assets/Scripts/Game/Simulation/Pathfinding.cs
    title: Pathfinding integer cost multipliers
  - id: lockstep
    resource: Assets/Scripts/Game/Network/LockstepCore.cs
    title: LockstepCore.DESYNC_CHECK_INTERVAL and CompareHash
  - id: draft-manager
    resource: Assets/Scripts/Game/Core/DraftManager.cs
    title: DraftManager.HandleTimeout seed derivation
  - id: match-factory
    resource: Assets/Scripts/Game/Simulation/MatchFactory.cs
    title: MatchFactory, the one starting board and the statics it sets
  - id: balance-data
    resource: Assets/Scripts/Game/Simulation/GameBalanceData.cs
    title: Per-era SuitStats and DistrictStats
  - id: match-replay
    resource: Assets/Scripts/MatchLog/MatchReplay.cs
    title: MatchReplay, refusing other simulation versions
  - id: sim-version
    resource: Assets/Scripts/Game/Simulation/SimulationVersion.cs
    title: SimulationVersion.Current and bump policy
  - id: balance-hasher
    resource: Assets/Scripts/Game/Simulation/BalanceHasher.cs
    title: Balance fingerprint separate from state
  - id: input-serializer
    resource: Assets/Scripts/Game/Network/InputSerializer.cs
    title: Wire layout and build identity comparison
  - id: build-identity
    resource: Assets/Scripts/Game/Network/LocalBuildIdentity.cs
    title: Shared balance content hash
  - id: baseline-tests
    resource: Assets/Tests/EditMode/Tests/DeterminismBaselineTests.cs
    title: Pinned version equality check
  - id: balance-tests
    resource: Assets/Tests/EditMode/Tests/BalanceHasherTests.cs
    title: Coverage of balance fields
  - id: game-manager
    resource: Assets/Scripts/Game/Core/GameManager.cs
    title: State allocation, drafted setup and new villager views
  - id: referee
    resource: dotnet/NodeWarCloud/NodeWarCloud/Referee.cs
    title: Balance lookup and serialized replays
  - id: match-log-format
    resource: Assets/Scripts/MatchLog/MatchLogFormat.cs
    title: Recorded identity, board and commands
  - id: match-launcher
    resource: Assets/UI/Scripts/MatchLauncher.cs
    title: Versioned lobby handshake
---

# Simulation Determinism Contract

`Assets/Scripts/Game/Simulation/` is shared, lockstep-replicated logic.
Peers replicate gameplay inputs as `GameCommand`s (see `docs/architecture.md`'s
networking model) and trust that identical commands produce identical
`SimulationState` on both machines. Every rule below exists to protect
that guarantee — a violation doesn't crash anything, it silently diverges
the two simulations until `SimulationStateHasher` catches it, often many
ticks after the actual bug ran.

## Rules

**Integer-only math. No `float`, no `double`.**
Why: floating-point rounding is not guaranteed to produce bit-identical
results across different CPUs/platforms, so two peers doing the "same"
float math can drift apart over many ticks. All positions, timers,
weights, and stats in `Simulation/` are `int`. Fractional tuning is
expressed as a scaled integer instead — see `Pathfinding`'s cost
multipliers (`50` = 0.5x, `100` = 1.0x, `200` = 2.0x).

Tempo timer integration uses `long` intermediates: cumulative scaled ticks
include ticks 1 through t, and each decrement is `C(t) - C(t-1)`.
Percentages below 100 are valid (slower respawns); production carries
overshoot into the next cycle. Schedule ticks must be positive and
strictly increasing, axis arrays must match and percentages must be
positive. Production durations must be safe for the largest decrement.
Sudden-death thresholds must be positive and strictly decreasing from
the opening threshold.

**No `UnityEngine` references in `Simulation/`.**
Why: keeps the simulation platform-independent and free of any Unity
subsystem (physics, time, math) that isn't guaranteed to behave
identically on every machine running the game.

There is no exception to this — including for tuning data, which is worth
stating because the arrangement can look like one. The `GameBalance` and
`BoardConfig` `ScriptableObject`s live in `Assets/Scripts/Game/Config/`
and never cross into `Simulation/`. What crosses is their *contents*, as
plain structs: `GameSimulation.SetBalance` and
`CommandProcessor.SetBalance` both take a `GameBalanceData`, which is an
ordinary `struct` declared inside `Simulation/` with no `UnityEngine`
reference. It is read-only tuning installed before a match starts and
never mutated by the tick loop. The Editor/development local-playtest
exception installs a modified copy through `SetBalance`, never changes
`SimulationState`, and is blocked in networked matches; its warning makes
clear that the balance hash and recorded replay no longer match the asset.

Do not put a `ScriptableObject` in `Simulation/` on the strength of this
paragraph — `scripts/sim-guard.ps1` blocks it, correctly.

**Collections: arrays or `List<T>` only. No `Dictionary`/`HashSet`
iteration.**
Why: hash-based collection enumeration order is not guaranteed to be
identical across runs/machines/insertion histories, so iterating one to
apply gameplay effects can process entities in a different order on each
peer. `SimulationState`'s `nodes`, `villagers`, and `players` are all flat
arrays indexed by ID for this reason. (`LockstepCore` does use
`Dictionary` for its own local input bookkeeping, keyed by tick number —
that data never enters `SimulationState` or the hash, so it's outside
this rule.)

**All sorts must use a total-order comparator with an ID tiebreaker — no
ties allowed.**
Why: `Sort` is not guaranteed stable, so if a comparator can return `0`
for two distinct elements, the two peers can legally end up with them in
different relative order. `GameSimulation.AssignAllCombatTargets` sorts
combat targets by `fightPriority` descending, then falls back to
`villagerID` ascending — no two villagers ever compare equal.

**Randomness must be seeded and derived from replicated state.**
Why: `UnityEngine.Random` (or any source seeded from wall-clock time or
per-machine state) produces different sequences on different peers. The
existing precedent is `DraftManager.HandleTimeout`'s fallback: if there is
no valid parked placement, `DraftState.ChooseTimeout` derives a deterministic
seed from already-replicated values (`turnNumber * 7919 + playerID * 31`) and picks
one of the cells `PlacementLegality` allows, so never water or ocean. A
valid parked placement wins instead, and the active peer sends the chosen
placement to the other peer. This runs before the match starts ticking,
not before the live game's `SimulationState` object is allocated.
`SimulationState` does not currently contain a stored RNG field; if a
mid-match feature needs randomness, the contract is that any RNG state
must live on `SimulationState` (so it round-trips through the hash) and
only ever advance inside `SimulateTick`, never from `Core/`, `Input/`,
`UI/`, or `View/`.

**No wall-clock or frame time.**
Why: `System.DateTime`/`Time.deltaTime`/`Time.time` reflect real elapsed
time, which is never identical between two machines to the precision
determinism requires. The only notion of time inside `Simulation/` is
`SimulationState.tickCount` and per-entity tick counters
(`moveProgress`, `productionTicksRemaining`, `respawnTicksRemaining`,
etc.). Deciding *when* to call `SimulateTick` based on real time is a
`Core/`/`Network/` concern (`TickRunner`/`LockstepCore`); `Simulation/`
itself only ever counts ticks.

**Tick order is canonical and must not be reordered:**
```
movement → combat → claiming (breach → claim) → production → healing → respawns → win-check
```
Why: each step reads state the previous step produced (e.g. claiming
depends on where combat left villagers standing this tick); reordering
changes game behavior in a way that's easy to miss testing against
yourself but will desync against any peer/build still running the old
order. (`GameSimulation.SimulateTick` also builds a resistance snapshot
from the tick-start owners and Fortress levels, `TickBreach` immediately before `TickClaiming`, and
order resume (`TickOrderResume`) after win-check. The final derived refresh of every
player's `nextBreacherID` follows resume, so it reflects all mutations
this tick. Tempo events are emitted after incrementing the tick count,
before movement. The tick also snapshots every node's owner at its start;
`TickBreach` and `TickClaiming` read that snapshot, never the owners they are
changing, so the order in which nodes are processed cannot move a result.) A new step must
be inserted at a specific, justified point in this sequence, not appended
by default. Production runs ordinary workers, then auto-recruit, before healing.
Auto-recruit visits ascending node ID and uses the same validated recruit path
as the command processor; no tempo scaling is applied to its ready tick. The Infirmary
rules add no state: which Acolytes count (`GameSimulation.CountInfirmaryWorkers`, at most two,
lowest villager ID first, none if any enemy stands there) is derived from existing fields, and the
simulation and the UI's paid-respawn price both call that one function. Its boost is added to the
respawn decrement after the tempo adjustment, and its heal runs on `tickCount % healIntervalTicks`.

**View and UI never write to `SimulationState`.**
Why: any write from outside the tick path runs on that machine's own
frame timing and never replicates to the peer, immediately desyncing the
match. The only path into the simulation is:
```
GameCommand → InputBuffer → CommandProcessor.ProcessCommand → (next tick) → SimulateTick
```

## `SimulationStateHasher` requirement

`SimulationStateHasher.ComputeHash` folds every mutable field of
`SimulationState` into one `int`, in fixed array-index order (players,
then nodes, then villagers, each field in a fixed sequence). **Any new
mutable field added anywhere in `NodeData`, `VillagerData`, `PlayerData`,
or `SimulationState` itself must be added to this method.** A field left
out is invisible to desync detection: bugs involving it will show up as
silent, undiagnosable gameplay divergence instead of a caught desync.
Fields that are set once at construction and never mutated during play
(on `NodeData`: `gridX`/`gridZ`, `links`) are
intentionally excluded — keep it that way rather than hashing static data.
The board's identity is hashed instead, in two parts: each node's `terrain`, and
`SimulationState.boardHash`, the `BoardHasher` fingerprint of the whole board
(dimensions, terrain, slot mask, fixed placements, both base draft pools, the
starting numbers and link tuning) that `MatchFactory` sets once. Both are hashed
unconditionally, so two peers on different maps diverge at the first checkpoint.

**Era fields are hashed only where they are not 0**: `PlayerData.suitEras`
/ `districtEras` (index and value, the two tables kept apart by an offset),
`NodeData.districtEra` and `VillagerData`'s remaining era fields. Omitting zero
era fields preserves their pre-era hash contribution, while any era the peers disagree
on still moves the hash. Version-1 logs are nevertheless refused by a
version-2 replay. Neutral extension fields may use conditional hashing
with explicit defaults, not just zero. The v2 player fields `breachBar`,
`paidRespawns` and derived `nextBreacherID` are covered; the last starts
at -1, not the struct's implicit zero. `VillagerState.Breaching` is
appended after `Dead`, preserving existing enum values.

C3 adds zero-neutral `PlayerData.recruitCount` and `NodeData.recruitReadyTick`,
plus false-neutral `NodeData.autoRecruit`. Each non-neutral hash contribution
has a distinct tag and player/node array index. `MatchFactory` initializes them
explicitly; the existing struct-array copy carries them, with hash-mutation and
copy-independence tests. Ownership loss resets the node fields, never the player
count. A recruit spawns through the same body-spawning helper that Town rewards use.
`NodeData.bonusVillagersOnClaim` is gone; `DistrictStats.bonusVillagersOnClaim` survives
only as a historical balance JSON field that no rule reads.

C7 replaces the per-villager Rampart buffs (`hasRampartBonus`, `rampartBonusEra`, removed from
`VillagerData`) with the zero-neutral `NodeData.fortressLevel` (0 to 3), hashed under tag 4013 with
the node index, initialized by `MatchFactory`, copied with the node array and reset to 0 whenever
ownership is lost. `DistrictStats.fortressMaterialsCosts`, `fortressMetalCosts` and
`fortressResistancePercent` (four entries each, level 0 first) are hashed by `BalanceHasher`
under tags 3008 to 3010 with the stats index, and an invalid trio disables upgrading rather than
being repaired. `NodeActionRules.CanUpgradeFortress` is the shared eligibility. Resistance is
computed once per tick from the tick-start snapshot (`BuildResistanceSnapshot`: the
highest aura on a node wins, ties to the lowest source node) and divides the claim rate and
the Core breach rate after the frontier and tempo steps, with a floor of 1; breach keeps its
tempo independence.

C4 adds the false-neutral `NodeData.townPaidMask` (bit 0 and bit 1 for the players whose
first full claim of that Town has paid), hashed only when non-zero under tag 4012 with
the node index, initialized by `MatchFactory`, copied with the node array, and never
reset by ownership change. The reward is spent before the population-cap check so a full
roster cannot defer it. `DistrictStats.townBonusVillagers` is conditionally hashed by
`BalanceHasher` (tag 3007 with its array index) and must be nonnegative.

`DistrictType` values are now explicit in the source and persisted in match logs: the
active set is 0–6 and 13–17 (`DistrictRoster.IsActive`), and the retired numbers are
reserved and never reused. Placement legality, board validation and the log reader refuse
an inactive type (the log reader for simulation version 3 and later; version 2 logs keep their historical numbers), with no aliasing in the runtime; only saved-data migration in
`Backend/Shared` maps old numbers to new ones.

Recruit=5, SetAutoRecruit=6 and UpgradeFortress=7 have explicit processor cases and serializer/log
acceptance. `CommandTypes.IsKnown` is the one list of valid types; the serializer
and the log reader refuse anything else. The six existing command fields,
24-byte wire payload and TICKS shape are unchanged. Recruit eligibility and cost
live in `NodeActionRules` and `GameBalanceData.TryRecruitCostAndCooldown`, which
UI may call read-only; only `CommandProcessor` and the auto-recruit pass spend.

Claiming, restoring and breaching take their frontier input from the tick-start
owner snapshot above: the capture bonus is `100 + captureBonusPercentPerStep *
clamp(net adjacent friendly links, 0, captureBonusMaxSteps)` percent, where each
adjacent link counts for the owner it had when the tick began. Rates are computed
in `long`, bodies are capped at four, the signed claim bar clamps to
`claimThreshold`, and an owner's present, unopposed villagers restore their own
node's bar (`TryRestoreOwnedNode`) by the same tempo-scaled arithmetic.
`GameBalanceData.CoreRulesValid` rejects balances whose percentage products would
overflow. The captureBonus and recruit balance fields are conditionally hashed by
`BalanceHasher` and so need no bump on their own.

A movement order is intent that survives interruption: `VillagerData.targetNodeID`
stays set through combat, a blocked route or a full node, and `TickOrderResume`
replans from the villager's current state once per tick, after every rule pass,
so work, claim or travel it begins cannot count until the next tick. This adds
no state field beyond those already hashed and copied. Pathfinding refuses to
route through the enemy Core as a transit node; a route may still start or
end there.

`TickEventLog` is outside this rule because it is outside `SimulationState`:
the simulation only ever appends to it and never reads it back, so nothing in
it can change a result, and it is deliberately not hashed. That holds only
while both halves are true. A step that **reads** the log, or a log that
moves **onto** `SimulationState`, makes it state, and then it needs hashing
like everything else. See `docs/architecture.md`, *What a tick did*.

## `SimulationState.CopyFrom`: the rollback point

`LockstepCore` plays on past a missing opponent input for up to 20 ticks
(8.2e). Then it rolls back to a copy of the last confirmed state and either
replays the span with the real inputs or holds. `CopyFrom` makes that copy
**into the same instance**, because views, selection and the HUD hold the
reference. It copies every array fresh except node `links`, which are
fixed once the board is built.

**Every field a state type gains must be copied too**, exactly as it must
be hashed. `SimulationStateCopyTests` enforces this: it sets every field of
`SimulationState`, `NodeData`, `VillagerData` and `PlayerData` by
reflection and fails on any that does not come through, or on a field type
it does not know how to fill. A field that survives a rollback it should
not have is a desync, not a cosmetic bug.

Restoring a copy is the one state write the core makes outside
`CommandProcessor` and `SimulateTick`. It is not game logic: it only puts
back a state the simulation itself produced, at a tick both peers agree
on. Speculative ticks are never recorded or hashed; the replay after them
is the confirmed pass.

The v2 player scalars also round-trip through the full `PlayerData` copy:
breach progress, the next candidate and the paid-respawn counter cannot
survive a rollback independently of the rest of the player.

## `SimulationVersion` and the content hash

Two builds that play the same inputs differently must refuse each other
instead of desyncing. The lobby handshake (`InputSerializer`'s
`BuildIdentity`, sent from `MatchLauncher`) compares three numbers
(protocol 5 also adds the pre-draft `MatchSetup` exchange, below):
`InputSerializer.ProtocolVersion` (wire layout),
`SimulationVersion.Current`, and a content hash,
`BalanceHasher.Hash` over the shared `GameBalance` asset.

The current simulation version and baseline pin are **3** (v3 added terrain and a
board fingerprint to the hashed state and re-pinned both baselines; C7 then removed the
unconditional per-villager `hasRampartBonus` hash term and moved them again, to 647286254
and 357327383, still within version 3). With a valid
breach channel enabled, a loss requires a breach this tick at or above
`BreachThresholdAt(tickCount)`; simultaneous losses cancel. Lowering the
threshold alone never loses a match. Disabling the channel retains
instant arrival breaches and the fixed-threshold legacy win path.

- **Bump `SimulationVersion.Current`** in the same commit as any change
  that alters what the same inputs produce: tick rules, a state field
  that feeds a result, a deliberate re-pin of the determinism baselines.
  `DeterminismBaselineTests.SimVersion_MatchesPinnedBaselines` checks that
  the current version equals the version pinned beside the baselines.
  It does not detect someone changing only the hash constants; the
  coordinated bump is a review requirement. Eras needed no bump because
  existing era-0 inputs still produce the same results and hashes.
- **Balance edits need no bump.** They move the content hash, which the
  handshake already compares. `BalanceHasherTests` fails when a
  `GameBalanceData` field is added without being hashed.
- The match log (`Assets/Scripts/MatchLog/`) records all three, and
  `MatchReplay` refuses a log from another `SimulationVersion`, so a
  replay only runs on the simulation that produced it. A current log also
  carries the board (BOARD_V2, tag 10: terrain, slot mask, base draft pools) and
  the SETUP chunk (tag 11, a `MatchSetup`); `MatchReplay` requires the setup to
  describe the log's own board, version and balance, and the referee further
  requires the map ID to be in its catalog and the board to hash to the
  catalog's own. The referee looks
  the balance up by content hash, so every shipped balance must be
  exported for the server (`Tools > Node War > Backend > Export Balance
  For Server`).
- Per-era numbers live in the balance (`GameBalanceData.districtStats`,
  `SuitStats.era`), so tuning an era is a balance edit, not a bump.

`BalanceHasher` hashes `GameBalanceData`, including each `SuitStats` and
`DistrictStats` entry and their array order. It is separate from
`SimulationStateHasher`: balance is still absent from the state hash.
It covers tempo/sudden-death schedules and breach tuning as well as
resource caps. Zero caps are omitted as a legacy hash extension; nonzero
caps are tagged separately. Nonpositive caps are uncapped in gameplay,
including negative values, whose raw values still affect the balance hash.
All resource gains and starting values clamp to a positive cap. Wasted
completions still cycle; a metal-capped Forge consumes no material and
a Market still alternates. The handshake therefore mitigates issue #59;
it does not fix that omission.

## The starting board: `MatchFactory`

`MatchFactory` (`Simulation/`) is the one place a match's tick-0 state is
built: `Configure` sets the statics the simulation reads (balance on
`GameSimulation` and `CommandProcessor`, the `Pathfinding` multipliers),
and `Build`/`Fill` lay out the nodes, the board's fixed placements, the
draft's placements at their placer's era, both players and their starting
villagers. The board is sparse: only Land cells and Lake cells with a drafted
Pier become nodes, numbered in ascending cell order, and a `Link` joins only
adjacent cells that both have nodes. `RequireBuildable` (through
`MapAuthoringRules.ValidateBoard` and `ValidateDraft`, which ask
`PlacementLegality`) refuses a board or draft that is not legal with an
`ArgumentException` before touching state, and `FindCoreNodeID` throws unless the
board has exactly two Cores on different rows. The live game (`GameManager`),
the skip-draft testing mode (`DraftPlanner.TestingPlacements` into `Fill`), the
referee (`MatchReplay`) and any headless run all start here, so they cannot
disagree about tick 0. A change to it changes every match: treat it like a
tick-rule change. Small unit-test fixtures construct their own minimal boards;
none is a second builder for a recorded match.

The map is agreed before anything is drafted. `MatchSetup` (map ID, board hash,
simulation version, balance hash) is what two peers, a log and the server
compare; the board itself never travels the wire in a live match, each side builds
it from `PremadeMaps.Catalog` (today `hourglass-01`, with `TerrainType` Land, Lake
and Ocean). `SetupAgreement` has the host propose it and the guest verify it, and
neither honours a draft packet until it holds.

Those statics also mean **two matches cannot run at once in one process**.
The Cloud Code referee serializes replays behind one lock for this reason.

## Desync detection

At positive tick indices divisible by 50
(`LockstepCore.DESYNC_CHECK_INTERVAL`), each peer computes
`SimulationStateHasher.ComputeHash(simState)` after simulating and attaches
it to its next outgoing tick-input packet. The first checkpoint is tick
index 50, when `simState.tickCount` is 51. `LockstepCore.CompareHash`
compares a non-zero received hash with the most recent stored local hash;
the packet carries no checkpoint tick to match explicitly. A mismatch logs
`"[DESYNC] Tick N Local: X Remote: Y"` and fires `OnDesync` — proof the
two simulations have diverged as of that tick, not a description of why.
This is the primary safety net for every rule above; treat a desync
report as evidence one of them was broken somewhere before that tick.

For match recording, `CommandsApplied` reports a non-empty command batch
in P0-then-P1 order with the pre-tick count, before applying it.
`HashComputed` reports the checkpoint with the post-tick count, one greater
than the tick index used by `OnDesync`. These recording events observe the
tick path; they do not add another way to change the simulation.
