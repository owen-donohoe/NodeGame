---
type: Guide
title: World and rules
description: The strategic model behind nodes, villagers, suits, resources, and eras.
tags: [guide, domain, gameplay]
status: draft
sources:
  - id: state
    resource: Assets/Scripts/Game/Simulation/SimulationState.cs
    title: SimulationState.cs
  - id: rules
    resource: Assets/Scripts/Game/Simulation/GameSimulation.cs
    title: GameSimulation.cs
  - id: balance
    resource: Assets/Scripts/Game/Simulation/GameBalanceData.cs
    title: GameBalanceData.cs
  - id: board
    resource: Assets/Scripts/Game/Simulation/BoardConfigData.cs
    title: BoardConfigData.cs
---

# World and rules

[Atlas](README.md) · Next: [Simulation](simulation.md)

The player's scarce resource is not just food or metal. It is the small population of villagers that must simultaneously expand territory, produce resources, defend, and attack. Sending workers forward costs production at home. Equipping a combat suit commits a worker to fighting until death. Breaching consumes the attacker permanently. Those tradeoffs connect the economy and combat instead of making them independent minigames.

## A graph is the battlefield

Nodes hold districts, ownership, occupation, and claim progress; edges define travel. The default board is a four-by-seven grid, but gameplay navigation follows explicit connections rather than physics positions. A villager reaches nodes and crosses weighted edges. World coordinates, curved route graphics, colliders, and camera orientation belong to presentation.

This representation makes topology meaningful: a route through friendly territory can be strategically preferable even when it is geometrically longer. Integer Dijkstra costs combine edge travel weight with ownership preferences. Route preference and actual crossing duration are related but distinct: preferences choose a path, while ticks determine progress along it. The details live in [Pathfinding](../../Assets/Scripts/Game/Simulation/Pathfinding.cs).

The board data and graph should not be mistaken for an unrestricted map editor. The normal [match factory](match-lifecycle.md) currently constructs a grid and applies fixed and drafted placements. Replay must be able to describe whatever setup a future map generator creates.

## Units are state machines with stable identities

Villagers have IDs, owners, health, suit, timers, and movement state. Their major states are Idle, Moving, Working, Claiming, Fighting, and Dead. An ID remains the identity used by commands and view objects; a renderer is not the unit itself.

Movement is interruptible at node arrivals. Meeting enemies starts combat before a route can continue through them. An order during a crossing preserves the meaning of distance already traveled rather than teleporting the unit to a new path's beginning. Death and consumption are different: ordinary death schedules respawn; a successful breach permanently spends that villager.

Arrays make these entities cheap to inspect in a fixed order and easy to hash. The view tracks the array's population and creates objects for newly granted villagers; the [feature checklist](../adding-a-feature.md) points to `SpawnBonusVillagers` as the pattern to follow. Changing an entity's representation therefore touches both deterministic identity and presentation lifetime.

## Territory produces both space and capability

Claiming is a signed tug-of-war. Player 0 pushes one direction, player 1 the other. Moving back through zero neutralizes the node before ownership can transfer. Both sides occupying a fight do not freely claim at the same time; combat determines who can resume useful work. Claiming, production, and combat therefore depend on the [tick order](simulation.md#tick-order-is-part-of-the-rules).

Districts express the value of territory:

| Family | Districts | Strategic job |
|---|---|---|
| Home and connectors | Core, None | Starting position, breach target, and movement structure |
| Economy and population | Farm, Mine, Forge, Market, Village | Food/materials/metal and additional villagers |
| Equipment | Camp, Barracks, Arsenal | Places where drafted combat suits can be equipped |
| Recovery | Shrine, Sanctuary | Healing or respawn support |
| Influence and defense | Watchtower, Rampart | Adjacent claim support or stronger occupation |

District slot types allow some nodes to become the claiming player's drafted district for that slot. The normal manual placement draft also puts fixed district choices directly on the board. These are related mechanisms, not interchangeable descriptions of the draft. Historical auto-population proposals should not override the code's placement model.

## Suits connect the economy to combat

Production suits are assigned by work location and removed when leaving. Combat suits are explicitly equipped, subject to ownership, current unit state, the district's capabilities, the player's drafted choices, and resource cost. A combat-suited villager does not continue ordinary production. Medic changes the combat contribution into healing rather than ordinary attacks.

The four player command families are Move, SetAllocation, Equip, and Respawn. They are intentionally small. Their consequences can be large because the simulation determines follow-on state: a move can become a fight, a claim, work, or a breach. Input systems express intent and do not script those consequences themselves.

The Forge illustrates the division: `SetAllocation` controls its material allocation; the tick rules decide whether and when a Smelter actually converts resources. A progress ring estimates and presents that process but cannot produce a resource. See [input and interface](input-and-interface.md).

## Winning is a population trade

Reaching an undefended enemy Core breaches it. The defender's breach count increases and the attacker is consumed. The default threshold is three breaches. Ordinary combat casualties can recover through timed respawn or a paid `Respawn` command, with Sanctuary workers affecting recovery.

This explains why “attack the Core” is not simply reducing a building's hit points. A winning rush must supply enough units through the graph while retaining enough economy and defense to survive. Balance changes affect these coupled loops; headless [replay and future balance tools](future-extensions.md) offer a way to measure their combined effect.

## Configuration has several owners

`BoardConfigData` describes starting layout, resources, population, edge weights, and route preferences. `GameBalanceData` describes rule tuning, suit statistics, and district statistics. Unity `BoardConfig` and `GameBalance` assets are authoring wrappers that deliver this plain data to the simulation.

Values in `Default()` methods are useful test and fallback data. The shipped Resources balance can differ. A design discussion should name which one it means, especially when comparing replays or reviewing a branch that tunes an asset.

Eras add per-player, per-type gameplay configuration. A suit uses its owner's equipped era; a drafted district uses its placer's era; a slot upgraded on capture takes the appropriate claimer configuration; board-authored fixed placements start at era zero. Higher-era entries presently copy era-zero tuning, but the mechanism supports differentiated stats. Skins represent appearance and never enter gameplay state. The ownership and usability rules sit in [backend and progression](backend-and-progression.md); their deterministic application belongs to [simulation](simulation.md).

## Source landmarks

[SimulationState.cs](../../Assets/Scripts/Game/Simulation/SimulationState.cs) | [GameSimulation.cs](../../Assets/Scripts/Game/Simulation/GameSimulation.cs) | [GameBalanceData.cs](../../Assets/Scripts/Game/Simulation/GameBalanceData.cs) | [BoardConfigData.cs](../../Assets/Scripts/Game/Simulation/BoardConfigData.cs)
