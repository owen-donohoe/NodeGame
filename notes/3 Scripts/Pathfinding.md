---
shell: 3
up:
  - "[[The tick]]"
  - "[[CommandProcessor]]"
tags: [shell/3, realm/match, system/simulation, script]
file: Assets/Scripts/Game/Simulation/Pathfinding.cs
sources:
  - Assets/Scripts/Game/Simulation/Pathfinding.cs
verified: ce9fb6bf
---

# Pathfinding

> [!question] The question
> How does a villager pick its route across the board, and how do you make a shortest-path
> algorithm give the *same* answer on two machines when two routes cost exactly the same?

**Where you are:** [[Home]] › [[Match]] › [[Simulation]] › **Pathfinding** (script)

**Open:** [Pathfinding.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Simulation/Pathfinding.cs)

## What it is

`FindPath(state, owner, start, end, speed)` runs **Dijkstra's algorithm** over the node graph
and returns node IDs from start to end, or an empty array when there's no route. It's used by
`CommandProcessor` (orders), the order preview, and route drawing.

## Read it in this order

### 1. Cost of a step

```csharp
int ticks = GameSimulation.CalculateLegTicks(...);            // physical travel time
int multiplier = GetPreferenceMultiplier(state, neighbor, owner);
// owned 50, partly owned 75, unowned 100, enemy partly 150, enemy owned 200 (%)
long scaledCost = Math.Max(1, ((long)ticks * multiplier + 99) / 100);   // ceiling division
newDist = dist[current] + scaledCost + EstimatedGateTicks(...);         // + expected time to break an enemy Pier
```

Villagers **prefer friendly territory**: the same distance costs half as much through your own land.

### 2. The loop: O(n²) Dijkstra without a priority queue

Each step scans *all* unvisited nodes for the smallest distance. For a board of dozens of nodes
that's trivially fast, and it has a hidden advantage: **the scan order is fixed (index 0 up), so
ties always resolve to the lowest index.**

### 3. The tiebreaker

```csharp
if (newDist < dist[neighbor] || (newDist == dist[neighbor] && current < cameFrom[neighbor]))
```

On equal cost, the predecessor with the lower ID wins. Without this, two equally short routes
could be chosen differently depending on visiting order, and two machines might disagree. It's the
"all sorts need total-order comparators with ID tiebreakers" rule from CLAUDE.md, applied to a graph search.

### 4. Special rule

An enemy Core can be a start or an end, never something you pass *through*.

## Why this, not that

| Option | Verdict |
|---|---|
| **O(n²) Dijkstra with deterministic ties** (chosen) | Simple, deterministic, and fast enough for small boards |
| Dijkstra with a binary-heap priority queue | O(E log V), but heap order on ties needs care to stay deterministic |
| A* | Faster on big maps with a good heuristic, but unnecessary here |
| Precomputed all-pairs paths | Instant lookup, but costs depend on *changing* ownership, so they'd constantly go stale |

## Things to notice

- **`public static int OwnedMultiplier = 50;`** These are *mutable statics*: process-global configuration. That's one reason the rig and referee run one match at a time.
- **Allocations per call** (`dist`, `cameFrom`, `visited`, a `List` for reconstruction): an optimisation opportunity if profiling ever points here.

## Check yourself

> [!question]- Two routes to the target cost exactly 40. How does each machine pick the same one?
> Ties at each node keep the lower-ID predecessor, and the node scan always proceeds in index
> order. So both machines build identical `cameFrom` arrays and reconstruct the same path.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
