# Simulation Determinism Contract

`Assets/Scripts/Game/Simulation/` is shared, lockstep-replicated logic.
Peers exchange only `GameCommand`s (see `docs/architecture.md`'s
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

**No `UnityEngine` references in `Simulation/`.**
Why: keeps the simulation platform-independent and free of any Unity
subsystem (physics, time, math) that isn't guaranteed to behave
identically on every machine running the game. The `GameBalance` and
`BoardConfig` `ScriptableObject`s are the sanctioned exception: they are
read-only tuning data loaded once before a match starts
(`GameSimulation.SetBalance`, `CommandProcessor.SetBalance`) and never
mutated by the tick loop, so they never carry non-deterministic state
into the simulation.

**Collections: arrays or `List<T>` only. No `Dictionary`/`HashSet`
iteration.**
Why: hash-based collection enumeration order is not guaranteed to be
identical across runs/machines/insertion histories, so iterating one to
apply gameplay effects can process entities in a different order on each
peer. `SimulationState`'s `nodes`, `villagers`, and `players` are all flat
arrays indexed by ID for this reason. (`LockstepRunner` does use
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
existing precedent is `DraftManager.HandleTimeout`, which derives a
deterministic seed from already-replicated values
(`turnNumber * 7919 + activePlayer * 31`) rather than drawing from a
stored generator — draft logic runs before `SimulationState` exists.
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
`Core/`/`Network/` concern (`TickRunner`/`LockstepRunner`); `Simulation/`
itself only ever counts ticks.

**Tick order is canonical and must not be reordered:**
```
movement → combat → claiming → production → healing → respawns → win-check
```
Why: each step reads state the previous step produced (e.g. claiming
depends on where combat left villagers standing this tick); reordering
changes game behavior in a way that's easy to miss testing against
yourself but will desync against any peer/build still running the old
order. (`GameSimulation.SimulateTick` also runs a rampart-bonus pass
right after movement, and a post-combat-resume pass at the very end,
after win-check — the method's own doc comment explains why that final
pass is a separate step rather than folded into combat.) A new step must
be inserted at a specific, justified point in this sequence, not appended
by default.

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
(e.g. `worldPosition`, `edges`) are intentionally excluded — keep it that
way rather than hashing static data.

## Desync detection

Every 50 ticks (`LockstepRunner.DESYNC_CHECK_INTERVAL`), each peer
computes `SimulationStateHasher.ComputeHash(simState)` and attaches it to
its next outgoing tick-input packet. When a peer receives the other
side's hash for a tick it also hashed, it compares the two
(`LockstepRunner.CompareHash`). A mismatch logs
`"[DESYNC] Tick N Local: X Remote: Y"` and fires `OnDesync` — proof the
two simulations have diverged as of that tick, not a description of why.
This is the primary safety net for every rule above; treat a desync
report as evidence one of them was broken somewhere before that tick.
