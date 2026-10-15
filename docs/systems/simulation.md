---
system: simulation
summary: Deterministic match state, movement, combat, territory and victory shared by live play and replay.
sources:
  - Assets/Scripts/Game/Simulation/SimulationState.cs
  - Assets/Scripts/Game/Simulation/GameSimulation.cs
  - Assets/Scripts/Game/Simulation/Pathfinding.cs
  - Assets/Scripts/Game/Simulation/TickEvents.cs
  - Assets/Scripts/Game/Simulation/SimulationStateHasher.cs
  - Assets/Scripts/Game/Simulation/SimulationVersion.cs
verified: a1da5182
---

# Simulation

## Purpose
Produce identical match results from identical starting state and ordered commands. Live peers, local play and the referee share these rules; presentation observes their results.

## Interface
`SimulationState` owns ID-indexed `NodeData[] nodes`, `VillagerData[] villagers`, `PlayerData[] players`, `tickCount`, `gameOver`, `winnerID`, `defaultLinkWeight` and `boardHash`. Nodes carry ownership, signed claim, district/era and economic state. Villagers carry role, HP, motion, sticky `targetNodeID`, combat/production/respawn clocks and `isConsumed`. Players carry resources, Core, breach/recruit/paid-respawn counters and drafted suit/district IDs with era tables.

Drivers apply ordered commands, then call `GameSimulation.SimulateTick(state, TickEventLog log = null)` once per simulation tick (normally 10 Hz). `SetBalance(GameBalanceData)` installs read-only tuning before play. `Pathfinding.FindPath(state, askingOwnerId, startNode, endNode, moveSpeedTicks)` returns a node-ID path or no route. `GetMoveLegDurationTicks` supplies physical motion duration to readers.

`SimulationStateHasher.ComputeHash(state)` returns the deterministic integer fingerprint. `state.CopyFrom(source)` restores into the same object, cloning mutable arrays and sharing immutable node links; network rollback retains references held by views.

`TickEventLog` exposes `Count` and an indexer over flat `TickEvent { type, nodeID, villagerID, playerID, value }`; unused IDs are -1. Types are CombatStarted, VillagerDied, VillagerRespawned (value 1 for paid), NodeNeutralised, NodeClaimed, Breach, TempoStage and SuddenDeath. Drivers clear/reuse the log and publish it through `ITickProvider.TickSimulated`; subscribers copy retained events. Commands can append paid-respawn events before the tick.

## Connects to
- `commands`: `CommandProcessor` mutates this state before each tick.
- `economy`: production, health, recruitment and bank rules occupy canonical tick passes.
- `board`: `MatchFactory` supplies tick-0 state, tuning and board identity.
- `netcode`: drivers call ticks, hash confirmed checkpoints and restore with `CopyFrom`.
- `match-flow`: `GameManager` owns the live state and reacts to game-over and array growth.
- `match-log`: replay runs the same state/tick path; identity includes `SimulationVersion`.
- `world-view`: reads state and physical leg durations; consumes tick events.
- `match-ui`: reads state and tempo/sudden-death events without mutation.
- `bots`: planners read state and emit commands, never advance rules themselves.

## Invariants
Follow [determinism](../contracts/determinism.md): integers, no Unity/time APIs, ordered arrays/lists and total-order sorts. Hash and copy every mutable field. Keep IDs stable when appending units; consumed bodies remain addressable. Event output never feeds rules and stays outside state/hash. Same-input behavior changes require a coordinated simulation-version/baseline review.

## How it works
Increment the tick, snapshot owners and healthy Fortress resistance, emit tempo events, then run movement → combat → claiming → production → healing → respawns → win-check. Claiming runs breach before district-health/claim updates; production runs workers, Storehouses, bank collection, then auto-recruit. Order resume follows win-check; refresh each defender's derived `nextBreacherID` last.

Movement checks enemies/Core on every arrival. Dijkstra costs physical leg ticks with integer ownership preferences; an own Pier receives its travel divisor without friendly preference, an enemy Pier adds expected neutralisation time. Equal costs choose the lower predecessor ID; an enemy Core cannot be transit. Leg duration is latched on departure. Retargeting preserves a continuing crossing or charges the distance already walked to reverse. Combat interruptions preserve intent; resume replans after all passes, so newly resumed work/claim starts next tick.

Combat assigns targets round-robin: attackers in ID order, targets by descending `fightPriority`, ascending ID. Cooldowns govern damage; Medics heal instead. Bodies die and await Core respawn; minions are permanently consumed. Survivors resume only in the final pass.

Claim bars are positive for P0, negative for P1, bounded by threshold. Opposing claimers freeze the bar. Enemy pressure drains district health first; crossing zero neutralises before capture. Frontier bonuses and Fortress resistance read tick-start snapshots. Friendly unopposed bodies restore their owner's damaged claim bar without frontier/decrement bonuses.

Undefended enemy-Core arrivals channel a defender-owned breach bar. Completion consumes one attacker, increments breaches and discards excess progress; candidate order is non-combat suit, lowest HP, lowest ID. A channel-enabled loss requires a new breach this tick at the current threshold; simultaneous losses cancel. A sudden-death threshold drop alone never wins. Disabled channels retain instant arrival breaches and the fixed legacy threshold.

## Decisions
- Flat state and explicit ticks let the same rules run without Unity in peers and referee.
- Output events describe moments; ongoing conditions remain state rather than repeated events.
- In-place restore preserves observers' state references across speculation.

## Pressure points
Static balance/path settings prohibit concurrent differently configured matches in one process. The large tick class couples movement, ownership and economy through pass order. State hashing omits balance, relying on setup/handshake agreement. Reflection copy tests catch missing fields, but neutral hash extensions and baseline/version coordination still require review. Snapshot and path allocations offer optimisation opportunities only if ordering remains identical.

## Change recipes
- Add state: initialise in `MatchFactory` and runtime spawns, hash it, copy it, define neutral defaults, and test mutation plus rollback independence.
- Change a rule: place it explicitly in canonical order, retain tick-start neighbour reads, bump `SimulationVersion.Current` when existing inputs change, and coordinate pinned baselines.
- Spawn units: append a larger array under the population cap; let `GameManager` create new views and tolerate rollback shrinkage.
- Add feedback: append an optional event; never read it to decide gameplay.

## Tests
`dotnet test dotnet/NodeWar.sln` covers simulation, determinism baselines, movement/link weights, sticky orders, claiming/restore, breach, state hash and `SimulationStateCopyTests`; lossy lockstep and replay suites cover confirmed replay. Rule changes also need a two-peer run beyond a hash checkpoint. These checks do not verify Unity scene wiring or visual interpolation.
