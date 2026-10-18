---
shell: 3
up: "[[Route lines]]"
tags: [shell/3, realm/match, system/world-view, script]
file: Assets/Scripts/Game/View/OrderPresentation.cs
sources:
  - Assets/Scripts/Game/View/OrderPresentation.cs
verified: ce9fb6bf
---

# OrderPresentation

> [!question] The question
> Given only the simulation state, what route should be drawn for a villager, and in what style?

**Where you are:** [[Home]] › [[Match]] › [[World view]] › [[Route lines]] › **OrderPresentation** (script)

**Open:** [OrderPresentation.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/View/OrderPresentation.cs)

## What it is

An 80-line `static class` of pure functions: "read-only intent derived from the current
simulation, including rollback". It has no Unity types, so it's testable.

## Read it in this order

1. **`Interrupted(v)`:** has a target, but isn't moving (fighting, gated…), so the route is drawn *amber and dashed*.
2. **`GateBlocked`:** standing at an enemy Pier that blocks departure.
3. **`Style`:** amber/dashed when interrupted, with alpha fading to 25% over 0.35 s (instant with reduced motion).
4. **`BuildRoute(state, v, mine, revealLegs)`:**
   - Moving: the rest of `movePath` from `movePathIndex`.
   - Interrupted: recompute a path to the target, with special cases (sieging the enemy Core gives an intent *marker*; no route gives a straight *connector*).
   - Opponent: **cut to `revealLegs + 1` nodes**. This is the information gate.

## Design lesson

**Derive, don't store.** The route isn't saved anywhere in the view. It's recomputed from state
every frame. After a rollback it's automatically right, because there's nothing stale to invalidate.

## Check yourself

> [!question]- Why recompute an interrupted villager's path instead of reusing its old `movePath`?
> The board changed (that's why it stopped). The old path may no longer be the route it will take when it resumes.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
