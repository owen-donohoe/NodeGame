---
shell: 3
up: "[[Route lines]]"
tags: [shell/3, realm/match, system/world-view, script]
file: Assets/Scripts/Game/View/RouteCurveCache.cs
sources:
  - Assets/Scripts/Game/View/RouteCurveCache.cs
verified: ce9fb6bf
---

# RouteCurveCache

> [!question] The question
> How do the sprite and the route line share one curve without rebuilding it twice per frame, and how does the cache know when the route changed?

**Where you are:** [[Home]] › [[Match]] › [[World view]] › [[Route lines]] › **RouteCurveCache** (script)

**Open:** [RouteCurveCache.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/View/RouteCurveCache.cs)

## What it is

A static, per-villager cache of curve points and leg starts. `VillagerView` is the only writer
(`Store`), and `MovementPathRenderer` reads it (`TryGetCurrent`).

## The clever bit: reference identity as a version check

```csharp
TryGetCurrent(villagerID, currentMovePath, out points, out legStarts)
// hit only if the cached entry was built from the SAME movePath array object
```

`CommandProcessor.ApplyMove` always assigns a **freshly allocated** `movePath` array on a new
Move, and the simulation never edits it in place while walking. So "same array reference"
means "same route", which is a single pointer comparison instead of comparing contents.

This is a neat trick, and it carries an implicit contract: **if anyone ever mutates `movePath`
in place, the cache silently serves a stale curve.** The header comment documents that
contract, which is the only thing protecting it.

## Other details

- **Copies out of `PathCurve`'s scratch buffers** right after `Build`, because the next villager's build would overwrite them.
- **Arrays grown lazily**, like the renderer's other per-villager tracking.
- **Reset on play-mode start**: the same static-survives-domain-reload guard as [[LocalBuildIdentity]].

## Check yourself

> [!question]- What would break if a future change made `GameSimulation` shorten `movePath` in place after a reversal?
> The reference would stay the same, so the cache would keep returning the old curve. The sprite
> and line would follow a route the villager no longer walks. (Today, `CollapseReversalLeg` allocates a new array, which honours the contract.)

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
