---
shell: 3
up: "[[Desync detection]]"
tags: [shell/3, realm/match, system/simulation, script]
file: Assets/Scripts/Game/Simulation/SimulationStateHasher.cs
sources:
  - Assets/Scripts/Game/Simulation/SimulationStateHasher.cs
verified: ce9fb6bf
---

# SimulationStateHasher

> [!question] The question
> How do you turn an entire game state into one `int`, so that any difference changes it and
> the same state always gives the same answer?

**Where you are:** [[Home]] › [[Match]] › [[Simulation]] › [[Desync detection]] › **SimulationStateHasher** (script)

**Open:** [SimulationStateHasher.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Simulation/SimulationStateHasher.cs)

## What it is

A ~200-line `static class` with one public method, `ComputeHash(SimulationState)`, which walks
every gameplay field of players, nodes and villagers in array order.

## Read it in this order

1. **The header's four rules:** include every field that can diverge, exclude view-only data,
   be deterministic, and use a fixed order.
2. **The opening lines:** `hash = 17; hash = hash * 31 + field;`, all inside `unchecked`.
3. **The player loop:** notice the pattern for **newer fields**:
   ```csharp
   if (state.players[i].paidRespawns != 0) { hash = hash * 31 + 2002; hash = hash * 31 + paidRespawns; }
   ```
   It's mixed in only when non-zero, with a tag (2002), so older matches hash exactly as they did before the field existed.
4. **`HashNodeExtension`:** the same idea packaged as a helper for node fields.
5. **Arrays:** the length first, then the elements. Otherwise `[1,2]+[3]` and `[1]+[2,3]` could collide.

## Why `* 31`?

31 is an odd prime, so multiplying by it mixes bits well and never loses information to
factors of two. It's the same constant Java uses for `String.hashCode`. The exact choice matters
less than **never changing it**, because every pinned baseline and stored log depends on it.

## Rule

Every new `SimulationState` field goes here, in the same commit (CLAUDE.md). The
`determinism-guard` skill checks for it.

## Check yourself

> [!question]- Why hash an array's length before its elements?
> Without it, different splits of the same sequence across consecutive arrays could produce
> the same hash. The length acts as a separator.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
