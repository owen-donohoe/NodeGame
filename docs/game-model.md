---
type: Domain Model
title: Game Model
description: What Node War is — the match model, board, villagers, districts, suits, resources, and win condition, as the simulation actually implements them.
tags: [game-design, domain-model, districts, suits, combat, claiming]
generated: { by: claude-opus-5, at: 2026-08-31T00:00:00Z }
verified_at_commit: bc701d1
status: draft
sources:
  - id: sim-state
    resource: Assets/Scripts/Game/Simulation/SimulationState.cs
    title: DistrictType, SuitType, VillagerState, NodeData, VillagerData, PlayerData
    last_modified: 2026-08-30T17:51:21-04:00
  - id: sim-loop
    resource: Assets/Scripts/Game/Simulation/GameSimulation.cs
    title: GameSimulation.SimulateTick and all tick steps
    last_modified: 2026-08-30T17:51:21-04:00
  - id: balance
    resource: Assets/Scripts/Game/Simulation/GameBalanceData.cs
    title: GameBalanceData.Default, IsCombatSuit, CanEquipSuitAtNode, GetSlotTypeForDistrict
    last_modified: 2026-08-29T10:56:17-04:00
  - id: board
    resource: Assets/Scripts/Game/Simulation/BoardConfigData.cs
    title: BoardConfigData.Default and InitialNodePlacement
    last_modified: 2026-08-30T17:51:21-04:00
  - id: pathfinding
    resource: Assets/Scripts/Game/Simulation/Pathfinding.cs
    title: Pathfinding.FindPath and ownership preference multipliers
    last_modified: 2026-08-29T01:52:16-04:00
  - id: commands
    resource: Assets/Scripts/Game/Simulation/Commands.cs
    title: CommandType and GameCommand
    last_modified: 2026-08-14T00:06:30-04:00
  - id: command-processor
    resource: Assets/Scripts/Game/Simulation/CommandProcessor.cs
    title: CommandProcessor.ProcessCommand
    last_modified: 2026-08-29T10:56:17-04:00
  - id: draft-state
    resource: Assets/Scripts/Game/Simulation/DraftState.cs
    title: DraftState grid occupancy and per-player slots
    last_modified: 2026-08-24T09:08:13-04:00
  - id: design-history
    resource: docs/design-history/README.md
    title: Design history and v2.1 reconciliation
---

# Game Model

Node War is a **1v1 real-time strategy game played on a graph of nodes**, simulated
deterministically at 10 ticks per second. Two players start from opposing Core nodes and compete to
claim territory, produce resources, equip combat units, and breach the enemy Core three times.

This document describes *what the game is*. [architecture](architecture.md) describes how the code
is layered; [simulation-rules](simulation-rules.md) describes the determinism contract the
simulation must uphold.

All numbers below are the **code defaults** from `GameBalanceData.Default()` and
`BoardConfigData.Default()`. A real match reads its values from the `GameBalance` and `BoardConfig`
`ScriptableObject`s, so treat these as the shape of the tuning, not as fixed constants.

## The board

The board is a grid (default 4 columns × 7 rows) of nodes connected by edges. Each `NodeData`
carries a grid position, an `Edge[]` of connections, a district type, an owner, and a signed claim
bar.

An `Edge` has a `travelWeight` (default 4). Crossing it takes `travelWeight × moveSpeedTicks` ticks,
so movement cost is a property of the board, not of real time.

Both players begin owning one Core, placed at opposite ends of the grid. Everything else is
unowned and contested.

## Villagers

Each player starts with 3 villagers. A villager is always in exactly one `VillagerState`:

| State | Meaning |
|---|---|
| `Idle` | On a node, doing nothing |
| `Moving` | Traversing a path produced by `Pathfinding.FindPath` |
| `Working` | Producing a resource on an owned production district |
| `Claiming` | Pushing the claim bar on a node the player does not own |
| `Fighting` | On a node where both players have living villagers |
| `Dead` | Awaiting respawn at the owner's Core |

Villagers carry HP (default 5), attack damage, move speed, an attack cooldown, and a
`fightPriority` used as the combat targeting sort key. A player is capped at 25 villagers.

## Movement and pathfinding

`Pathfinding.FindPath` is Dijkstra over the node graph with **integer ownership preference
multipliers** — the simulation is integer-only, so fractional preference is expressed as a
percentage:

| Node relative to the mover | Multiplier |
|---|---|
| Owned | 50 (0.5×) |
| Partially owned (claim bar leaning their way) | 75 |
| Unowned | 100 |
| Enemy partially owned | 150 |
| Enemy owned | 200 (2.0×) |

Cost is `ceil(travelWeight × multiplier / 100)`, minimum 1. Villagers therefore prefer to travel
through friendly territory and route around enemy ground unless the detour is long.

Movement is checked on **every node arrival**, not just at the destination: arriving on a node with
living enemies interrupts the path and starts a fight, and arriving on the enemy Core triggers a
breach or a fight.

## Claiming

Every non-Core node has a signed `claimBar`. Positive is player 0, negative is player 1, and
`claimThreshold` (default 10000) in either direction transfers ownership.

Claiming villagers push the bar by `baseClaimPerTick × claimers` per tick, capped at 4 claimers per
node. Pushing *against* an opponent's existing lean is multiplied by `decrementMultiplier`
(default 4), so taking ground back from an established claim is faster than establishing it — the
bar is a tug-of-war, not a per-player progress meter. Crossing zero drops the node to neutral
(`ownerID = -1`) before it can be claimed the other way.

A node with **both** players' claimers present is frozen; combat resolves it instead.

When a claim completes, a non-`Fixed` node becomes whichever district the claiming player drafted
for that slot type, falling back to the node's `baseDistrictType`. Some nodes grant bonus villagers
on claim.

## Districts

`slotType` determines which drafted upgrade a node can become when claimed.

| District | Slot | Role |
|---|---|---|
| `None` | Fixed | Empty connector / crossroads |
| `Core` | Fixed | Home node. Villagers here are always Idle. The breach target. |
| `Farm` | Fixed | Farmer works it → +1 food |
| `Mine` | Fixed | Miner works it → +1 material |
| `Forge` | Fixed | Smelter converts 1 material → 1 metal, only while `materialAllocation > 0` |
| `Village` | Fixed | Grants bonus villagers on claim |
| `Camp` | Army | Equip Warrior or Scout |
| `Barracks` | Army | Equip Warrior, Guardian, Berserker or Scout |
| `Arsenal` | Army | Equip Warrior, Guardian or Scout |
| `Shrine` | Healing | Faster passive healing for its owner's villagers standing on it |
| `Sanctuary` | Healing | Acolyte works it → faster respawns; also the only Medic equip point |
| `Watchtower` | Affect | Watcher works it → boosts claim rate on **adjacent** friendly-claimed nodes |
| `Rampart` | Affect | Occupants gain max HP and damage reduction; slows enemy claim decrement |
| `Market` | ResourceSpecial | Merchant works it → alternates +1 food and +1 material |

## Suits

A suit is a villager's role. Production suits (`Farmer`, `Miner`, `Smelter`, `Merchant`, `Acolyte`,
`Watcher`) are **assigned automatically** on arrival at the matching owned district and stripped
when the villager leaves.

Combat suits (`Warrior`, `Guardian`, `Scout`, `Berserker`, `Medic`) are **equipped deliberately**
via an `Equip` command and are **permanent until death**. Equipping requires all of: the villager
is `Idle` and not already combat-suited, it is standing on a node its owner controls, that node's
district permits the suit, the player **drafted** that suit before the match, and the player can
pay its food and material cost. A combat-suited villager never works — it idles on owned nodes.

`Medic` is the exception in combat: instead of attacking, it heals the most-damaged friendly
villager on its node.

## Resources

Three resources per player: **food**, **materials**, **metal**. Materials feed the Forge, which
consumes them to make metal. Resources pay for suits and for respawns. All production runs on
per-villager tick timers, so output is a function of how many workers a player keeps alive and
employed — capped at 2 workers per node.

## Combat

When both players have living villagers on the same node, everyone there is forced into `Fighting`.

Targets are assigned **round-robin**, with each side's fighters sorted by `fightPriority`
descending then `villagerID` ascending — a total order with no ties, which the determinism contract
requires. Each fighter attacks when its cooldown expires. A defender standing on a Rampart takes
reduced damage, to a floor of 1.

At 0 HP a villager dies, drops its path, and respawns at its owner's Core after `respawnTicks`
(default 50), reset to base stats with no suit. A player may also spend food on a `Respawn` command
to bring a dead villager back immediately instead of waiting. Each Acolyte working a Sanctuary both
speeds the passive countdown and reduces that food cost.

Combat is deliberately resolved across two separate tick steps. Damage and deaths happen in the
combat step; survivors decide what to do next in a final post-combat resume step after the
win-check. See the reasoning on `TickPostCombatResume`.

## Breach and the win condition

A villager that reaches the **enemy Core with no living defenders on it** breaches:

1. The defending player's `breachCount` increments.
2. The breaching villager is **permanently consumed** — flagged `isConsumed`, never respawns.

A breach is a trade: a unit for a point. At `breachThreshold` (default 3) breaches against a
player, the match ends and the *other* player wins.

## The pre-match draft

Before play, players run a turn-based placement draft, tracked by `DraftState`, choosing where
their district upgrades sit on the grid. What a player drafts determines what their claimed nodes
become for each slot type.

The draft is a **manual placement** system. The v2.1 design document describes a different
auto-population scheme; the code is canon. See [design-history](design-history/README.md).

## Player commands

Every player action reaches the simulation as exactly one of four `GameCommand` types:

| Command | Effect |
|---|---|
| `Move` | Path a villager to a target node |
| `SetAllocation` | Set an owned Forge's `materialAllocation`, gating its material→metal conversion |
| `Equip` | Put a combat suit on an Idle villager standing on an owned district that permits it |
| `Respawn` | Pay food to return a dead villager to its Core **immediately**, skipping the timer |

There is no other way to affect game state. See [simulation-rules](simulation-rules.md).
