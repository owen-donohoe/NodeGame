---
shell: 3
up: "[[Map identity]]"
tags: [shell/3, realm/lobby, system/board, script]
file: Assets/Scripts/Game/Simulation/BoardHasher.cs
sources:
  - Assets/Scripts/Game/Simulation/BoardHasher.cs
verified: ce9fb6bf
---

# BoardHasher

> [!question] The question
> How do you fingerprint a whole map in 82 lines, and why does it use a different hash function from the game-state hasher?

**Where you are:** [[Home]] › [[Lobby]] › [[Board]] › [[Map identity]] › **BoardHasher** (script)

**Open:** [BoardHasher.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Simulation/BoardHasher.cs)

## What it is

`Hash(BoardConfigData) → int`, covering, in fixed order: grid size, link weight, starting villagers and resources, the five path multipliers, the fixed placements, terrain, the slot mask, and both base draft pools.

## The algorithm: FNV-1a over integers

```csharp
const int Prime = 16777619; const uint Offset = 2166136261u;
h = (int)Offset;
h = Mix(h, value)  →  (h ^ value) * Prime
```

The comment states the property: "Changing any one value, with everything else fixed, always changes the result: each step is a bijection." XOR with a fixed `h` is reversible, and multiplying by an odd number modulo 2³² is reversible, so no single-value change can collide.

## Details

- **Null vs empty:** a null array mixes in `-1` as its length, so "a board that lost its terrain can never pass for one that was authored empty".
- **The draft is excluded on purpose:** "it is agreed after the board is, and is replicated by its own placements."
- **Compare with [[SimulationStateHasher]]:** both are integer-only and fixed-order. The state hasher's `× 31` scheme is frozen by pinned baselines; this one was free to pick a stronger mix.

## Check yourself

> [!question]- Why include starting resources in a "board" hash?
> They change the match from tick 0. Two boards with identical terrain but different starting food would desync, so they must not count as the same map.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
