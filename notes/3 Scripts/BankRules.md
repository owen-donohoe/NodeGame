---
shell: 3
up: "[[Storehouse collection]]"
down: []
concepts:
  - "[[State machines]]"
tags: [shell/3, realm/match, system/economy, script]
card: docs/systems/economy.md
file: Assets/Scripts/Game/Simulation/BankRules.cs
sources:
  - Assets/Scripts/Game/Simulation/BankRules.cs
  - Assets/Scripts/Game/Simulation/GameSimulation.cs
verified: ce9fb6bf
---

# BankRules

> [!question] The question
> Which parts of a bank are queries, and which part actually transfers stock?

**Where you are:** [[Home]] › [[Match]] › [[Economy]] › [[Storehouse collection]] › **BankRules** (script)

**Open:** [BankRules.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Simulation/BankRules.cs)

## What it is

A static helper containing bank queries, production eligibility, worker-slot counting and payment. Unlike [[NodeActionRules]], some methods mutate state: `TickCollection` increments player pools and returns a changed node; `PayBank` increments pools for capture.

## Read it in this order

### 1. `Total`, `Locked`, `HasStationaryCollector`

`Total` widens the sum to `long`. A lock includes neutral ownership, an incomplete owner-side bar or an enemy body. A collector must be friendly, positive-HP, unconsumed and Idle or Working. The collector predicate does not call `IsBody`, so an Idle minion qualifies.

### 2. `CollectibleResource` and `CollectionState`

The private selector chooses food, materials, then metal, skipping full pools. The presentation query uses that same selector. Its precedence is Neutral → Locked → Empty → StorageFull → Collecting/Waiting. Status is derived from state rather than stored as another field that could drift.

### 3. `TickCollection`

Follow its early returns: empty clears request/progress; absent intent clears progress; lock or full destinations pause. Eligible progress increases by the configured increment. One threshold crossing pays one unit and retains the remainder unless that emptied the bank.

The caller in [[GameSimulation]] assigns the result back:

```csharp
state.nodes[nodeID] = BankRules.TickCollection(state, state.nodes[nodeID], bal);
```

Discarding that return would keep pool increments while losing node stock/progress changes. A struct copy makes this contract easy to miss.

### 4. `CanProduce`, `HasWorkerSlot`, `PayBank`

Storehouse production needs ownership, health, valid tuning, positive duration and bank room. Banks consume no ordinary worker slot. `PayBank` uses widened addition, clamps capture loot and leaves clearing the node to its caller.

## Things to notice

“A locked owner's request is accepted; the production pass pauses it.” Command eligibility and current payment eligibility deliberately differ. Requiring an unlocked bank just to request collection would lose the useful ability to express intent during a contest.

The split between pure queries and mutating helpers is convenient but requires care: [[Match UI]] should read `CollectionState`, never call `TickCollection` to discover what it would do.

## Check yourself

> [!question]- Does `PayBank` clear stock after paying?
> No. The ownership-change caller pays, then clears the bank and its production state. The helper only credits player pools.

> [!question]- Can you safely ignore the result of `TickCollection` because it receives `SimulationState`?
> No. Player pools mutate through the state reference, but the node is passed by value and its changed copy must be stored back.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
