---
shell: 3
up: "[[Input]]"
tags: [shell/3, realm/match, system/input, script]
file: Assets/Scripts/Game/Input/SelectionSystem.cs
sources:
  - Assets/Scripts/Game/Input/SelectionSystem.cs
  - Assets/Scripts/Game/Input/SelectionRules.cs
verified: ce9fb6bf
---

# SelectionSystem

> [!question] The question
> "Which villagers are selected?" sounds trivial. Why does it need its own system, and why is selection *not* part of the game state?

**Where you are:** [[Home]] › [[Match]] › [[Input]] › **SelectionSystem** (script)

**Open:** [SelectionSystem.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Input/SelectionSystem.cs)

## What it is

The owner of the local player's selection: a list of villager IDs. It reads `SimulationState`
(who's yours, alive, idle, on which node) and never writes it. The pure rules
(eligibility, residents, picking) live in `SelectionRules`.

Main operations: `IsSelectable`, `TapVillager`, `SelectSingle`, `SelectAllIdle`, `SelectIdleOnNode`,
`SelectAllOnNode`, `AreAllResidentsSelected`, `ApplyLasso` (paint selection), `ClearSelection`,
`DropVillagersFrom(count)`.

## Read it in this order

1. **`IsSelectable`:** yours, alive, not consumed. Public, because the gesture source asks it whether a villager is even a tap target.
2. **`SelectSingle` / `TapVillager`:** returns `false` and leaves the selection alone when invalid, so the *caller* decides what an invalid tap means.
3. **`ApplyLasso`:** projects each eligible villager to the screen and keeps those covered by the painted stroke. An empty capture keeps the old selection.
4. **`DropVillagersFrom(count)`:** after a [[Rollback]], the villager array may be shorter, so selected IDs past the end are removed.

## Why selection isn't game state

Selection is **local intent-in-progress**. Your opponent doesn't need it, the referee doesn't
need it, and it must never affect the simulation. Only the *command* produced from it
(a Move per selected villager) enters the game. That keeps selection free to use floats,
screen positions and UI timing. See [[Presentation reads, never writes]].

## Check yourself

> [!question]- Why does a rollback need to touch the selection at all?
> Villagers recruited during a speculative span disappear when the state is restored. A
> selection still pointing at their IDs would make the next order reference villagers that don't exist.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
