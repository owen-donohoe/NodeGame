---
shell: 3
up:
  - "[[Smooth movement between ticks]]"
  - "[[World view]]"
tags: [shell/3, realm/match, system/world-view, script]
file: Assets/Scripts/Game/View/VillagerView.cs
sources:
  - Assets/Scripts/Game/View/VillagerView.cs
verified: ce9fb6bf
---

# VillagerView

> [!question] The question
> One villager on screen: where does its position, colour, flash and swing come from each frame?

**Where you are:** [[Home]] › [[Match]] › [[World view]] › **VillagerView** (script)

**Open:** [VillagerView.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/View/VillagerView.cs)

## What it is

A MonoBehaviour per villager (about 690 lines). `Initialize(state, id)` binds it to
`state.villagers[id]`. From then on it **reads** that entry and draws it.

## Read it in this order

1. **`Initialize`** and the setters (`SetTickProvider`, `SetVillagerPositioners`, `SetPathCurveSettings`, `SetBreachCueSettings`): `GameManager` wires everything in.
2. **`OnTickSimulated` → `ObserveTick`:** once per *tick*, it compares this tick's HP and cooldown
   with the last observation. HP went down in the same life means **flash**. The cooldown reset in combat means **swing**.
   Pure helpers (`VillagerFlashMath`, `VillagerSwingMath`) make those decisions testable.
3. **`BuildRouteCurve`:** builds the whole route's [[PathCurve]] from *node centres* (not per-villager
   slots, so squads share one curve), or reuses the cached one from [[RouteCurveCache]].
4. **`Update`, the moving branch:** the interpolation from [[Smooth movement between ticks]]:
   `legT = moveProgress / legTicks + TickAlpha / legTicks` → `PathCurve.PositionOnLeg`. A
   `departureOffset` blends the sprite from its idle slot onto the path.
5. **`Update`, the standing branch:** lerps towards its slot on the node (`VillagerPositioner`).
6. **The visual state entry point:** one method sets renderer enable/disable and colour, with the flash composed *over* the state tint.

## Design lessons

- **Per-tick observation vs per-frame drawing:** effects that depend on *changes* are detected
  in `ObserveTick` (once per tick, so catch-up ticks don't merge). Drawing happens in `Update`.
- **Reduced motion** is respected (no swing pulse).

## Check yourself

> [!question]- Why build the curve from node centres instead of each villager's slot position?
> Two villagers ordered together would otherwise produce slightly different curves, and the
> route renderer couldn't merge their lines into one. The slot offset is applied separately.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
