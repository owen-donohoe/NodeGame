---
shell: 3
up:
  - "[[Simulation]]"
  - "[[Rollback]]"
tags: [shell/3, realm/match, system/simulation, script]
file: Assets/Scripts/Game/Simulation/SimulationState.cs
sources:
  - Assets/Scripts/Game/Simulation/SimulationState.cs
verified: ce9fb6bf
---

# SimulationState

> [!question] The question
> If you printed an entire Node War match at one instant, what would you see? This file *is* the answer.

**Where you are:** [[Home]] › [[Match]] › [[Simulation]] › **SimulationState** (script)

**Open:** [SimulationState.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Simulation/SimulationState.cs)

## What it is

The data model of a match: enums (`DistrictType`, `TerrainType`, `VillagerState`, `SuitType`),
three structs (`NodeData`, `VillagerData`, `PlayerData`), a `Link` struct, and the
`SimulationState` class that holds them in arrays.

```
SimulationState
├── nodes[]      NodeData: id, grid x/z, links[], district, claimBar (±), owner, health, bank, era…
├── villagers[]  VillagerData: id, owner, current/target node, movePath[], moveProgress,
│                 legDuration, state, suit, hp, attack, cooldowns, isConsumed…
├── players[]    PlayerData: id, core node, food/materials/metal, breaches, drafted suits/districts, eras
├── tickCount, gameOver, winnerID, defaultLinkWeight
└── boardHash    so two peers on different maps diverge at the first checkpoint
```

## Things to notice

- **All integers.** Even `claimBar` is signed: positive for player 0, negative for player 1. One number, no floats.
- **IDs are array indices.** `villagers[7].villagerID == 7`. New units are *appended*, and dead
  ones stay (`isConsumed`), so IDs never shift.
- **Structs in arrays.** Fast and compact, but remember the copy trap: `var v = villagers[i]; v.hp = 0;` changes a copy ([[GameSimulation]]).
- **"Values are persisted in match logs: never renumber."** Enum numbers are a contract ([[Numbers on the wire are forever]]).
- **`CopyFrom(source)`:** restores *into this object*, because views hold references to it ([[Rollback]]).
  "Like SimulationStateHasher, this must name every field." `SimulationStateCopyTests` catch omissions by reflection.

## The three-place rule for a new field

Add it here → hash it in [[SimulationStateHasher]] → copy it in `CopyFrom`, and initialise it in
`MatchFactory`. Miss one and you get silent desyncs or rollback bugs.

## Check yourself

> [!question]- Why keep dead minions in the array (`isConsumed`) instead of removing them?
> Removing one would shift every later index, so every villager ID after it would change, and
> commands, views and logs referencing IDs would point at the wrong units.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
