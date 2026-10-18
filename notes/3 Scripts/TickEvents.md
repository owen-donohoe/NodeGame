---
shell: 3
up:
  - "[[Simulation]]"
  - "[[World view]]"
concepts:
  - "[[Events and observers]]"
tags: [shell/3, realm/match, system/simulation, script]
file: Assets/Scripts/Game/Simulation/TickEvents.cs
sources:
  - Assets/Scripts/Game/Simulation/TickEvents.cs
verified: ce9fb6bf
---

# TickEvents

> [!question] The question
> How does the simulation tell the view "a villager just died" without the view comparing states, and without that message ever affecting the game?

**Where you are:** [[Home]] › [[Match]] › [[Simulation]] › **TickEvents** (script)

**Open:** [TickEvents.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Simulation/TickEvents.cs)

## What it is

92 lines: an enum of 8 event types, a flat `TickEvent` struct, and a `TickEventLog` (a
reusable list). The simulation appends to it during a tick, and the driver hands it to
`TickSimulated` subscribers afterwards.

| Event | Fields used |
|---|---|
| CombatStarted | node |
| VillagerDied | villager, node, owner |
| VillagerRespawned | villager, owner, value 1 = paid |
| NodeNeutralised | node, who lost, value = who pushed |
| NodeClaimed | node, new owner, value = old owner |
| Breach | villager, defender |
| TempoStage | value = stage index |
| SuddenDeath | value = new threshold |

## Read the comments. They're the design

- **"Only moments."** Ongoing conditions (a fight still going) are read from state. A log that
  said "still happening" every tick "would be the state again".
- **"Output only."** The simulation never reads it back, "so even a wrong entry is a cosmetic
  bug and cannot move the match". That's why it isn't in `SimulationState` or the hash.
- **"Passed in… null means nothing is recorded."** Tests and headless runs get no events for free.
- **Flat and integer-only, "in the style of GameCommand"**: unused fields are −1.

## Check yourself

> [!question]- A villager takes damage and is healed back to full in the same tick. The view compares HP between ticks. What does it miss, and how would an event fix it?
> It sees no HP change, so no flash. The world-view card lists this as a pressure point. An
> explicit "damaged" event would report the moment even though the end state didn't change.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
