---
shell: 3
up: "[[The tick]]"
concepts:
  - "[[Why floats break determinism]]"
tags: [shell/3, realm/match, system/simulation, script]
file: Assets/Scripts/Game/Simulation/GameSimulation.cs
sources:
  - Assets/Scripts/Game/Simulation/GameSimulation.cs
verified: ce9fb6bf
---

# GameSimulation

> [!question] The question
> The biggest rules file in the game is about 1,500 lines. How do you read it without
> drowning, and what should you look for?

**Where you are:** [[Home]] › [[Match]] › [[Simulation]] › [[The tick]] › **GameSimulation** (script)

**Open:** [GameSimulation.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Simulation/GameSimulation.cs)

## What it is

A `static class` holding every per-tick rule: movement, combat, breach, claiming, production,
healing, respawns, win check and order resumption. The rest of the game calls one public
entry point, `SimulateTick(state, log)`, plus a few read-only helpers for views
(`GetMoveLegDurationTicks`, `BaseMoveSpeedTicks`).

> [!info] How to read 1,500 lines
> **Don't read top to bottom.** Read `SimulateTick` (about 45 lines) as the table of
> contents. Every call in it is a section marked `// ===== STEP N: … =====`. Then read only
> the step you're working on.

## Read it in this order

### 1. `SimulateTick`: the table of contents

The doc comment lists the steps. The body *is* the canonical order from [[The tick]]. Notice:

```csharp
state.tickCount++;
int[] ownersAtTickStart = new int[state.nodes.Length];
for (...) ownersAtTickStart[i] = state.nodes[i].ownerID;   // the snapshot
```

The snapshot is read by claiming. It is also a fresh allocation every tick, which is one
of the "optimisation opportunities, only if ordering stays identical" on the card.

### 2. Step 2: movement (`TickAllMovement` → `TickMovement`)

```csharp
VillagerData v = state.villagers[villagerIndex];   // copy (struct)
...
if (v.moveLegDurationTicks == 0) BeginLeg(state, ref v);
v.moveProgress++;
if (v.moveProgress >= v.moveLegDurationTicks) { /* arrive: check enemies, the enemy Core, Pier gates */ }
...
state.villagers[villagerIndex] = v;                // write the copy back
```

Two lessons:
- **Struct copy-modify-write.** Forget the last line and nothing changes.
- **Defensive recovery.** If the movement invariant is broken (no next node), the villager
  is reset to Idle *deterministically* instead of crashing. Both peers recover identically.

### 3. `CalculateLegTicks`: integer rules in miniature

```csharp
long ticks = Math.Max(1, (long)GetLinkWeight(state, from, to) * speed);
if (owned Pier) ticks = (ticks + divisor - 1) / divisor;   // ceiling division
return (int)Math.Min(int.MaxValue, Math.Max(1, ticks));
```

`long` to avoid overflow, ceiling division to round on purpose, a lower bound of 1 so a leg
never takes zero ticks, and a clamp back to `int`. This is what writing rules without floats looks like.

### 4. The rest, step by step

Use the `// ===== STEP` markers. For combat, look for the **round-robin target order**:
attackers by ID, targets by descending `fightPriority`, then ascending ID. Every sort has an
ID tiebreaker, so equal items always come out in the same order.

## What to look for in *any* change here

| Check | Why |
|---|---|
| No `float`, `double`, `Random`, clock | [[Why floats break determinism]] |
| Loops over arrays, not dictionaries | Iteration order must be the same everywhere |
| Sorts have an ID tiebreaker | Equal keys must always order the same way |
| New fields are hashed and copied | Otherwise desync detection and rollback break |
| Reads tick-start snapshots where they exist | Otherwise results depend on array order |
| `SimulationVersion` bump if same inputs → new result | Old replays must not validate against new rules |

The repo has a `determinism-guard` skill and `scripts/sim-guard.ps1` that check the mechanical part of this.

## Check yourself

> [!question]- Why does `CalculateLegTicks` multiply into a `long` first?
> `linkWeight × speed` could exceed `int.MaxValue` on a strange board. Overflow in C# (unchecked)
> wraps silently to a negative number, and a negative leg time would be a deterministic, but
> nonsensical, rule. Using `long` and clamping prevents it.

> [!question]- You add `int shield` to `VillagerData` and use it in combat. What else must change?
> Initialise it in `MatchFactory` and in spawns; add it to `SimulationStateHasher`; make sure
> `CopyFrom` copies it; define a neutral default; add tests; and if existing replays would now
> play differently, bump `SimulationVersion` and review the baselines.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
