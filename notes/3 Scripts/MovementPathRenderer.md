---
shell: 3
up: "[[Route lines]]"
concepts:
  - "[[Object pooling]]"
tags: [shell/3, realm/match, system/world-view, script]
file: Assets/Scripts/Game/View/MovementPathRenderer.cs
sources:
  - Assets/Scripts/Game/View/MovementPathRenderer.cs
verified: ce9fb6bf
---

# MovementPathRenderer

> [!question] The question
> What does the code that draws every route line on the board actually do each frame, and
> how do you find your way around 600 lines of it?

**Where you are:** [[Home]] › [[Match]] › [[World view]] › [[Route lines]] › **MovementPathRenderer** (script)

**Open:** [MovementPathRenderer.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/View/MovementPathRenderer.cs)

## What it is

One `MonoBehaviour` for the whole board. Every `LateUpdate` it decides which routes to
show, builds their geometry, and pushes it into a pool of Unity `LineRenderer`s. It reads
`SimulationState` and never writes it.

> [!info] The file in one breath
> `Initialize` (bind state, seat, settings, tick provider, camera) · `LateUpdate` (the loop)
> · `OpponentRouteVisible` (two gates) · `AlreadyDrawn` (dedupe) · `BuildRemainder`
> (geometry from the cache) · `DrawRoute` (style) · `ApplyGradient` · `GetLine` (pool).

## Read it in this order

### 1. The header comment

Read it fully. Three design decisions are explained there: one renderer for the board (to
deduplicate), reading the *cached* curve (so sprite and line can't drift apart), and the
opponent gate being *geometry*, not alpha.

### 2. `LateUpdate`: the whole frame

```csharp
if (opponentSettings.show)
    opponentGate.ComputeHopsFromPlayer(...);   // breadth-first search, skipped when not needed
for each villager i:
    route = OrderPresentation.BuildRoute(...)  // what the simulation says it's doing
    if enemy and not OpponentRouteVisible → skip
    if AlreadyDrawn(i) → skip                  // squad = one line
    BuildRemainder / BuildInterruptedRemainder → `remainder`
    DrawRoute(used++, i, ...)
disable pool[used..]                            // leftover lines switched off, not destroyed
```

Notice the **early-out for the breadth-first search**: it's only needed by the opponent
hop gate, so when opponent routes are off it isn't run at all. A small optimisation, explained right where it happens.

### 3. `OpponentRouteVisible`: the two gates

- **Hop gate:** the villager's node must be within N hops of your territory (the
  breadth-first search above).
- **Screen gate:** `WorldToViewportPoint`, then "how far outside the 0–1 box, in screen
  widths". Inside means alpha 1. Within `offScreenFade` means it's dimmed. Beyond that it's not drawn.

### 4. `BuildRemainder`: geometry

It reads `RouteCurveCache.TryGetCurrent`, which `VillagerView` filled this frame. If the
cache is empty (after a rollback replaced a path), it builds the curve itself. Then
`PathCurve.AppendRemainder(..., LegFraction(villager), legs, remainder)`, where
`LegFraction` is the same `moveProgress + TickAlpha` maths the sprite uses.

### 5. `DrawRoute` and `ApplyGradient`: style

- `textureScale = 1 / dashSpacing`. The comment explains the setting is *spacing*,
  because that is what's readable when tuning.
- **Colour belongs to the owner, not the viewer.** An enemy route is always the enemy's colour.
- **Your routes:** alpha goes from `freshAlpha` to `settledAlpha` over `settleSeconds`,
  timed with `Time.time`. That's fine here, because this is the view side.
- **Enemy routes:** a three-key alpha gradient, with the fade distance converted from
  nodes into a fraction of the stub.
- `colorKeys` and `alphaKeys` are **reused arrays**, so no garbage per line per frame.

### 6. `GetLine`: the pool

It creates a `GameObject` + `LineRenderer` only when `pool.Count <= index`. It sets up
world space, `LineAlignment.View`, tiled texture, no shadows, and a sorting layer chosen by
name (falling back to the lowest layer if the name doesn't exist).

## Who uses it

`GameManager` creates and initialises it. It reads `SimulationState`, `ITickProvider`,
`RouteCurveCache`, `OrderPresentation`, `RouteReveal`, `OpponentRouteGate` and `PathCurve`.
`PendingOrderView` borrows its `DashMaterial` so provisional lines match.

## Optimisation lessons in this one file

| Technique | Where |
|---|---|
| Object pool, disable instead of destroy | `GetLine`, the end of `LateUpdate` |
| Reused collections | `remainder`, `routeWaypoints`, `drawnOwn` / `drawnOpponent` |
| Reused gradient keys | `colorKeys`, `alphaKeys`, `gradient` |
| Skip work nobody will use | the breadth-first search guarded by `opponentSettings.show` |
| Deduplicate before drawing | `AlreadyDrawn` |

## Check yourself

> [!question]- Why is the route colour based on `villager.ownerID` rather than `localPlayerID`?
> So that the opponent's route is always in the opponent's colour, whichever seat you're in.

> [!question]- The pool has 12 lines, and this frame needs 7. What happens to lines 7–11?
> They're set `enabled = false`. They stay in the pool, ready for a later frame, with no
> destroy or recreate cost.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
