---
shell: 3
up: "[[Pending orders]]"
tags: [shell/3, realm/match, system/world-view, script]
file: Assets/Scripts/Game/View/PendingOrderTracker.cs
sources:
  - Assets/Scripts/Game/View/PendingOrderTracker.cs
verified: ce9fb6bf
---

# PendingOrderTracker

> [!question] The question
> A provisional order line must disappear at exactly the right moment. What decides that, in 99 lines?

**Where you are:** [[Home]] › [[Match]] › [[World view]] › [[Pending orders]] › **PendingOrderTracker** (script)

**Open:** [PendingOrderTracker.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/View/PendingOrderTracker.cs)

## What it is

A plain class (not a MonoBehaviour) holding a list of `Order { villagerID, targetNode, issuedAt, routeNodes }`.
"Time is passed in rather than read, which is also why it is not a MonoBehaviour." A good first file to read in full.

## The whole logic

```csharp
Issue(villager, target, route, now)  → Cancel(villager); add       // newest order wins
Sweep(now, villagers)                → remove every order where IsOver
IsOver(order):
    now − issuedAt ≥ timeout                    → true   // the only exit for a refused command
    villager missing / dead / consumed          → true
    villager Moving with targetNodeID == target → true   // the simulation took over
```

`Sweep` runs *before* drawing, so "the frame the simulation's route appears is the frame the
provisional one is gone, neither a gap nor two lines".

## Check yourself

> [!question]- Why iterate backwards (`for i = Count − 1; i ≥ 0; i--`) when removing?
> Removing an item shifts everything after it down by one. Going backwards means the shift
> only affects items already visited, so nothing is skipped.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
