---
shell: 3
up: "[[Bot priorities]]"
down: []
concepts: []
tags: [shell/3, realm/match, system/bots, script]
card: docs/systems/bots.md
file: Assets/Scripts/Game/Input/BotPlayer.cs
sources:
  - Assets/Scripts/Game/Input/BotPlayer.cs
verified: ce9fb6bf
---

# BotPlayer

> [!question] The question
> How do you read a long heuristic opponent without confusing its choices with the game's actual rules?

**Where you are:** [[Home]] › [[Match]] › [[Bots]] › [[Bot priorities]] › **BotPlayer** (script)

**Open:** [BotPlayer.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Input/BotPlayer.cs)

## What it is

A state-reading command producer in `NodeWar.Input`. The source describes “Priority-based evaluation with per-villager command cooldowns to prevent oscillation.” It holds a reference to simulation state but does not directly mutate it or advance ticks.

## Read it in this order

### 1. Constructor and `Evaluate`

Prefer the constructor that receives actual `GameBalanceData`; the shorter overload uses `Default()`. `Evaluate` stops if game-over, grows scratch arrays as population grows, preserves old cooldown entries, clears reservations and invalidates path-cost stamps. Then it calls the seven passes in priority order.

### 2. `IssueMove` and `IssueMoveForce`

These reveal what a decision means: enqueue a Move with `issuedOnTick = state.tickCount`, reserve that villager and set its next command tick. Ordinary moves respect cooldown. Forced emergency moves ignore cooldown, but both protect breachers, existing reservations and redundant destinations.

### 3. The priority passes

Read [[Bot priorities]] beside the method sequence. `RespawnDead` maintains a local food budget and planned respawn count. `Expansion` returns after its first unmet district goal. `Military` calls [[BotSuitChoice]] and uses a wolfpack threshold of 3. `EconomyManagement` handles Farm and Mine, using hardcoded resource thresholds 10 and 20.

The comment “Emergency: override cooldowns, but preserve attackers already breaching” explains an intentional exception. Protecting an existing attack avoids undoing a nearly completed offensive action in response to pressure at home.

### 4. Query and path helpers

Candidate queries differ: non-soldiers check cooldowns; soldiers are still checked when movement is issued. `SortByPathCostTo` uses cached costs, nearest first, then ID. `PathCost` sums link weights on the route returned by [[Pathfinding]]; `PathLength` counts its edges. They answer different questions.

## Things to notice

The class queues only Move, Equip and Respawn. It has no purchase policy for Village recruitment, minions or Fortress levels. A method named “Expansion” does not imply it understands every current district.

Reservations are per unit, not a general resource budget. Equips can compete for the same current food/materials and be refused later. Separating policy from [[CommandProcessor]] keeps ordinary validation authoritative, but future policy sophistication may need planned-spending accounting.

## Check yourself

> [!question]- Why preserve old cooldown entries when resizing the array?
> Population growth must not accidentally make every existing villager immediately available for another movement order.

> [!question]- Does Evaluate call SimulateTick after choosing its moves?
> No. It queues commands. The driver owns command application and tick advancement, allowing both live and headless callers to use it.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
