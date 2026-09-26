---
type: Guide
title: Deterministic simulation
description: State, command validation, tick order, hashing, and the limits of isolation.
tags: [guide, simulation, determinism]
status: draft
sources:
  - id: loop
    resource: Assets/Scripts/Game/Simulation/GameSimulation.cs
    title: GameSimulation.cs
  - id: commands
    resource: Assets/Scripts/Game/Simulation/CommandProcessor.cs
    title: CommandProcessor.cs
  - id: hash
    resource: Assets/Scripts/Game/Simulation/SimulationStateHasher.cs
    title: SimulationStateHasher.cs
  - id: factory
    resource: Assets/Scripts/Game/Simulation/MatchFactory.cs
    title: MatchFactory.cs
  - id: events
    resource: Assets/Scripts/Game/Simulation/TickEvents.cs
    title: TickEvents.cs
---

# Deterministic simulation

[Atlas](README.md) · Previous: [World and rules](world-and-rules.md) · Next: [Match lifecycle](match-lifecycle.md)

The simulation is a plain C# library under `Assets/Scripts/Game/Simulation/`. Its job is to answer: given this starting board, configured rules, and ordered commands, what happens next? It must answer identically on every peer and in headless replay.

This is stronger than “the code usually behaves the same.” No Unity physics result, frame duration, current time, floating-point rounding, or unordered collection traversal may decide an outcome. All of these would introduce an input that a replay or remote peer did not receive.

## State, commands, and configuration

`SimulationState` contains match data, including players, nodes, villagers, tick count, and result. Small data structures and stable integer IDs make the whole match inspectable without a scene. `GameCommand` is a compact intent record rather than an executable callback. `CommandProcessor` is the final gameplay authority on whether an intent is valid.

A disabled Equip button is useful feedback; it is not enforcement. A bot, old client, or malicious packet can bypass a button. Ownership, state, cost, and gameplay legality must still be checked inside the command processor. UI eligibility helpers can explain the same conditions, but cannot replace enforcement.

The normal mutation path is input → buffer → tick driver → command processor → tick simulation. Initial construction through `MatchFactory` is a separate controlled operation inside the simulation boundary. View and UI never modify match state to make the screen “catch up.”

Configuration is currently partly process-global. `GameSimulation` and `CommandProcessor` hold static balance references, and `Pathfinding` holds five static route multipliers. `MatchFactory.Configure` installs them before use. A state object therefore does **not** currently make a match fully isolated. Running two differently configured matches interleaved in one process is unsafe, even when their state arrays are separate. This drives the referee lock and sequential test policy described in [replay](logs-and-replay.md) and [workflow](engineering-workflow.md).

## Tick order is part of the rules

Drivers apply commands before advancing the world. At 10 Hz, the core sequence is movement → combat → claiming → production → healing → respawns → win-check. The implementation also refreshes Rampart bonuses between movement and combat, then resumes surviving units' post-combat activity after the win-check.

These details are semantic. A new arrival must receive the right defensive modifiers before damage is resolved. A survivor must not kill its opponent and then unexpectedly claim within that same tick because a helper was moved into the combat loop. Rearranging calls for code neatness can change the game without changing any numeric tuning.

Tick timers replace wall-clock timers for gameplay. Integer progress and total-order comparisons make ties reproducible. Combat ordering, graph traversal, array iteration, and ID assignment all participate in determinism. Follow the full [simulation contract](../simulation-rules.md) when editing this layer; this chapter explains its purpose rather than replacing it.

## State fingerprints and compatibility are different checks

`SimulationStateHasher` fingerprints mutable match state in a fixed order. Peers compare checkpoints to discover divergence. A new outcome-relevant field that is omitted from the hasher creates a blind spot: two matches may disagree before the detector can see why.

The existing hasher intentionally excludes some construction-only node data. Era additions preserve era-zero fingerprints by folding era entries only when nonzero, including their identity/index so different types do not collapse into the same sequence. Those are deliberate compatibility rules, not permission to omit new mutable fields casually.

Three related identifiers answer different questions:

| Identifier | Question |
|---|---|
| Protocol version | Can these clients interpret each other's packet layouts? |
| Simulation version | Do they implement the same rule semantics? |
| Balance content hash | Are they using the same balance numbers? |

A state hash answers a fourth question: did two executions actually reach the same fingerprint at this tick? None of these integers authenticates a player or signs commands. See [networking](networking.md) and [backend trust](backend-and-progression.md).

The repository pins deterministic baseline hashes and the simulation version. A behavioral change requires an explicit compatibility decision; changing expected hashes merely to quiet a failing test destroys the evidence the test provides.

## Events describe outcomes without becoming inputs

`TickEventLog` records moments such as combat starting, a death, a claim, or a breach. Continuous conditions remain in state. The simulation appends events and never reads them back. Passing a null log must leave the same outcome.

The driver clears and reuses a log per tick, then notifies subscribers. A frame can execute several ticks, so an effect consumer must handle each notification and copy any information it needs to retain. Keeping a reference to the reused log is not a durable event history.

This is the seam for indicators and screen shake: deterministic facts flow outward; animation timing stays in presentation. The log is neither hashed state nor the persistent replay format. A cosmetic event omission can break an effect without invalidating the game; a gameplay step that starts reading the event log would invalidate that separation.

## Why this boundary is worth preserving

The same sources compile under Unity and plain .NET. That makes fast tests, server replay, future training, and batch balance experiments possible without building a second implementation. It also isolates visual experimentation: camera motion, material effects, and different mobile/desktop layouts cannot change a match if they remain consumers.

The cost is discipline at every extension. New rules need deterministic data, command serialization when applicable, state hashing, compatibility review, and tests. New presentation often needs none of those. Choosing the correct side of the boundary is the most consequential design decision in a feature. See the [decision map](decision-map.md) for the related tradeoffs.

## Source landmarks

[GameSimulation.cs](../../Assets/Scripts/Game/Simulation/GameSimulation.cs) | [CommandProcessor.cs](../../Assets/Scripts/Game/Simulation/CommandProcessor.cs) | [SimulationStateHasher.cs](../../Assets/Scripts/Game/Simulation/SimulationStateHasher.cs) | [MatchFactory.cs](../../Assets/Scripts/Game/Simulation/MatchFactory.cs) | [TickEvents.cs](../../Assets/Scripts/Game/Simulation/TickEvents.cs)
