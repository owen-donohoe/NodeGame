---
shell: concept
used-by:
  - "[[Route lines]]"
  - "[[MovementPathRenderer]]"
  - "[[PathCurve]]"
tags: [concept, topic/optimisation]
---

# Object pooling

> [!question] The question
> Why does a game stutter for a few frames now and then even when nothing much is happening,
> and how do you write per-frame code that avoids it?

**Kind:** concept. True outside Node War. Used by [[Route lines]], [[MovementPathRenderer]] and [[PathCurve]].

## The problem

In C#, `new` on a class (or `ToArray()`, LINQ, string concatenation, closures) allocates
memory on the *heap*. You never free it. The **garbage collector** (GC) periodically
pauses to find and reclaim unused memory. Allocate a little every frame, and every so often
the GC pauses the game, which shows up as a hitch, especially on phones.

## The idea

**Allocate once, reuse forever. Keep a *pool* of objects; take one when needed and put it
back (disable it) instead of destroying it. For temporary data, keep reusable "scratch"
collections and `Clear()` them instead of creating new ones.**

## In Node War

| Technique | Where |
|---|---|
| Pool of `LineRenderer`s, disabled not destroyed | `MovementPathRenderer.GetLine`, `PendingOrderView` |
| Static scratch lists | `PathCurve.points`, `legStarts` (with `CopyTo` for anything kept) |
| Reused gradient key arrays | `MovementPathRenderer.colorKeys/alphaKeys` |
| Reused event log | `TickRunner.tickEvents`, cleared per tick (that's why subscribers copy) |

## Costs and traps

- **Shared scratch state.** Whoever builds next overwrites it. PathCurve is "main thread
  only, not reentrant". Copy anything you keep.
- **Stale state in pooled objects.** Reset *every* property when reusing (width, colour, material, loop).
- **Pools never shrink.** Memory holds at the peak. That's fine for dozens of lines, but think before pooling thousands.
- **Measure first.** Unity's Profiler shows "GC Alloc" per frame. Pool where it's non-zero in hot paths, not everywhere.

## Check yourself

> [!question]- `List<Vector3> pts = new List<Vector3>();` inside `LateUpdate`, for 50 routes at 60 fps. Roughly how many allocations a second?
> 50 × 60 = 3,000 list allocations a second, plus their internal arrays growing. That's
> exactly the per-frame garbage the reused `remainder` list avoids.

## Learn more

- Robert Nystrom, [Object Pool](https://gameprogrammingpatterns.com/object-pool.html)
- Unity Manual, *Garbage collection best practices*

## My understanding

<!-- Yours. AI never edits below this line. -->
