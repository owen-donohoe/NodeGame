---
shell: 2
up: "[[World view]]"
down:
  - "[[PathCurve]]"
  - "[[TickRunner]]"
concepts:
  - "[[Fixed timestep]]"
tags: [shell/2, realm/match, system/world-view]
sources:
  - Assets/Scripts/Game/View/VillagerView.cs
  - Assets/Scripts/Game/Core/ITickProvider.cs
  - Assets/Scripts/Game/Core/TickRunner.cs
verified: ce9fb6bf
---

# Smooth movement between ticks

> [!question] The question
> The simulation moves a villager forward once every 100 ms. Your screen draws every
> ~16 ms. Where is the villager on the five frames in between?

**Where you are:** [[Home]] › [[Match]] › [[World view]] › **Smooth movement between ticks** (mechanism)

## The problem

If the sprite moved only when the tick changed, villagers would hop 10 times a second.
The view needs a position *between* ticks. That position must never contradict the
simulation: a sprite that's visually 3 nodes away from where the game thinks it is gets
clicked wrong, and units look like they arrive late.

## The idea

**Each frame, compute the leg fraction from the simulation's own integer clock, plus how
far we are into the *current* tick (`TickAlpha`, 0 to 1), and place the sprite at that
fraction along the *same* curve the route line uses.**

## How it works here

```
            tick 6           tick 7           tick 8
 sim:  ──────●────────────────●────────────────●──────▶   (moveProgress 6 → 7 → 8, leg = 15 ticks)
 view: ──────●──•──•──•──•──•─●──•──•──•──•──•─●──────▶   (one dot per frame)
                   ▲
                   TickAlpha = 0.4 → legT = (6 + 0.4) / 15 = 0.427
```

The code in `VillagerView`:

```csharp
int totalTicksForEdge = GameSimulation.GetMoveLegDurationTicks(simState, villager);
float edgeProgress  = (float)villager.moveProgress / totalTicksForEdge;   // whole ticks walked
float subTickAlpha  = tickProvider.TickAlpha / totalTicksForEdge;         // part of this tick
float legT = Mathf.Clamp01(edgeProgress + subTickAlpha);
targetPos = PathCurve.PositionOnLeg(curvePoints, curveLegStarts, legIndex, legT);
```

### Where `TickAlpha` comes from

The tick driver uses an *accumulator* ([[Fixed timestep]]): each frame it adds the
frame's time, runs whole ticks while it has at least 0.1 s saved up, and keeps the
remainder. `TickAlpha = accumulator / tickInterval`, which is exactly "how far into the
next tick we are." Both drivers (`TickRunner`, `LockstepCore`) provide it through
`ITickProvider`, so the view never knows which one is running.

### The curve is shared

The sprite walks along [[PathCurve]]'s rounded route. [[Route lines]] draws that *same*
cached curve. If they used two copies, units would visibly walk beside their own lines.

## Why this, not that

| Option | What you'd gain | What you'd pay | Verdict here |
|---|---|---|---|
| **Interpolate from known ticks** (chosen) | Always faithful: the sprite is between two *real* positions | Up to one tick of visual delay | Chosen |
| Extrapolate (predict past the latest tick) | Looks a little more immediate | Wrong whenever the prediction is wrong, and then it must snap back | Rejected. The game already has snaps from rollback, and doesn't need more |
| `Vector3.Lerp(current, target, speed × dt)` | One line of code | Never arrives exactly, lags at speed, and isn't tied to the tick clock | Used only for *slot* settling at a node, not for travel |
| Raise the tick rate to 60 Hz | No interpolation needed | 6× the simulation work, log size and network pressure | Not worth it for an RTS |

## What it costs later

- **The leg duration must be fixed at departure** (`moveLegDurationTicks`). If it could
  change mid-leg, the sprite would jump. That's why the simulation fixes it when the villager departs.
- **Rollback can move the truth backwards.** After a correction, interpolation follows the
  restored state, and that is where visible snaps come from ([[Rollback]]).
- **Reversal is a special case.** A villager turning around mid-leg is represented as
  forward travel along the reversed leg, so the view anchors to `movePath[movePathIndex]`,
  never to a guessed sprite position.

## Traps

- **Using `Time.time` deltas to move sprites** independently of ticks. They drift away from the simulation.
- **Building a second route curve** for drawing.
- **Assuming one tick per frame.** A slow frame can run several ticks, and alpha still lands in [0, 1).

## Patterns you've seen before

- 🔁 [[Presentation reads, never writes]]: interpolation adds smoothness the simulation never sees.
- 🔁 [[Hide latency with honest previews]]: a cousin of this idea. Both make the screen
  feel faster without lying about the game.

## Check yourself

> [!question]- A leg takes 15 ticks. `moveProgress = 14`, `TickAlpha = 0.9`. What is `legT`, and what happens next tick?
> (14 + 0.9) / 15 = 0.993, a hair from the node. Next tick `moveProgress` reaches 15, the
> villager arrives, the leg index advances, and `legT` restarts from 0 on the next leg,
> exactly at the node, with no jump.

> [!question]- Why divide `TickAlpha` by the leg's tick count?
> `TickAlpha` is a fraction of *one tick*. The leg is 15 ticks long, so one tick is 1/15 of
> the leg. The partial tick must be scaled the same way.

## Go deeper ↓

- [[PathCurve]]: the shared curve geometry
- [[TickRunner]]: where `TickAlpha` is computed

## My understanding

<!-- Yours. AI never edits below this line. -->
