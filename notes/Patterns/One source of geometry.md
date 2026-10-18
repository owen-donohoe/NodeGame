---
shell: pattern
seen-in:
  - "[[Route lines]]"
  - "[[Smooth movement between ticks]]"
  - "[[PathCurve]]"
tags: [pattern]
---

# 🔁 One source of geometry

> [!question] The question
> The sprite walks a curve and the route line draws a curve. Why must they be literally the
> same list of points?

**Kind:** pattern. An idea that recurs at different shells of this codebase. A
presentation-side cousin of [[Same code, three places]].

## The pattern

**When two visuals must line up, derive both from one computed value. Don't compute it twice
with "the same" settings.**

## Where you've seen it

| Shared thing | Producers and consumers |
|---|---|
| Route curve (`RouteCurveCache`) | Built by `VillagerView` and read by `MovementPathRenderer` |
| Leg fraction (`moveProgress + TickAlpha`) | Sprite placement and the line's start point |
| Dash material | Real routes and `PendingOrderView` previews |
| Paint samples | Lasso membership and lasso rendering |

The comment in `MovementPathRenderer` explains why: *"a second copy of the corner radius that
drifted would put every villager visibly beside its own route."*

## What it costs

An order of execution (the producer must run first, hence `LateUpdate`) and a cache to keep valid across rollback.

## My understanding

<!-- Yours. AI never edits below this line. -->
