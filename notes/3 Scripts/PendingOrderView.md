---
shell: 3
up: "[[Pending orders]]"
tags: [shell/3, realm/match, system/world-view, script]
file: Assets/Scripts/Game/View/PendingOrderView.cs
sources:
  - Assets/Scripts/Game/View/PendingOrderView.cs
verified: ce9fb6bf
---

# PendingOrderView

> [!question] The question
> Who draws the faint "I heard you" line the instant you give an order, and how does it avoid a flicker when the real line arrives?

**Where you are:** [[Home]] › [[Match]] › [[World view]] › [[Pending orders]] › **PendingOrderView** (script)

**Open:** [PendingOrderView.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/View/PendingOrderView.cs)

## What it is

A ~380-line MonoBehaviour: the Unity half of the pending-order feature. [[PendingOrderTracker]] is the pure half.

## Read it in this order

1. **Constants:** `TimeoutSeconds = 1.5`, `ProvisionalAlphaScale = 0.5` ("reads as provisional beside the one that replaces it"), and `SharedStartDistance = 0.3` for merging mid-edge villagers.
2. **`OnMoveIssued(target, ids)`:** for each villager, `Pathfinding.FindPath` over the current
   state → `tracker.Issue(...)`, plus a pulse on the ordered villagers.
3. **Hover (`BeginHover`, `SetHoverNode`, `EndHover`):** the same route drawn while an order-drag is still in progress.
4. **The draw pass (`LateUpdate`):** `tracker.Sweep(...)`, then draw the survivors with a pooled
   `LineRenderer`, deduplicated like the real routes, using the **same dash material** as
   [[MovementPathRenderer]] (`DashMaterial`).

## Notice

The comment is honest about the approximation: the route "is a close approximation rather
than a promise: it ignores the rounded corners". Documenting a deliberate shortcut keeps someone
from "fixing" it into a second curve builder.

## Check yourself

> [!question]- Why use the real renderer's dash material instead of its own?
> So the provisional line looks like a fainter version of the real one. The handover then reads
> as "the same line becoming solid", not two different lines swapping.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
