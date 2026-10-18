---
shell: pattern
seen-in:
  - "[[Netcode]]"
  - "[[Simulation]]"
  - "[[Input]]"
  - "[[Gesture recognition]]"
  - "[[Pending orders]]"
  - "[[TickRunner]]"
tags: [pattern]
---

# 🔁 Pure core, Unity shell

> [!question] The question
> Why do so many important classes in Node War have no `using UnityEngine`, and take the
> time as a parameter instead of reading a clock?

**Kind:** pattern. An idea that recurs at different shells of this codebase.

## The pattern

**Put the decisions in a plain C# class that has no engine, devices or clocks. Everything
it needs arrives as parameters. Wrap it in a thin Unity component that reads the engine and
passes the values in.** (Elsewhere this is called *functional core, imperative shell* or
*hexagonal architecture*.)

## Where you've seen it

| Pure core | Unity shell | What arrives as a parameter |
|---|---|---|
| `GameSimulation` / `CommandProcessor` | `TickRunner`, `GameManager` | Everything: state, commands |
| `LockstepCore` | `GameManager` + `NetworkManagerTransport` | Clocks, transport (`ILockstepTransport`) |
| `GestureClassifier` | `PointerGestureSource` | `PointerSample`s with time and mm |
| `PendingOrderTracker` | `PendingOrderView` | `now`, villager array |
| `BoardFraming`, `VillagerFlashMath`, `PaintGeometry` | Camera, `VillagerView`, lasso view | Geometry, observations |
| `NodeWar.Progression` (`MatchSettlement`, `Glicko2`) | Cloud Code module + `CloudSave*RecordStore` | Snapshots, winner, frozen "now". No I/O, no clock |
| `RankedQueuePresenter`, `DisconnectHold` | Lobby/HUD views | `now`, service interfaces |

## What it buys

- **Tests without Unity.** `dotnet test` runs the network over a simulated lossy link,
  replays finger traces, and checks the simulation's hash baselines in seconds.
- **The same code runs in more places:** the referee and the balance rig ([[Same code, three places]]).
- **Determinism by construction.** A class that can't read a clock can't depend on one.

## What it costs

- **More plumbing:** interfaces (`ILockstepTransport`, `ITickProvider`), adapters, constructor parameters.
- **The shell can still be wrong**, and tests can't see wiring. That's why compile-check and
  manual Editor checks exist.

## Spot it yourself

> [!question]- `CommandSystem` is a MonoBehaviour that builds Move commands. Is it a core or a shell?
> A shell. It reads Unity's keyboard and holds view references (the node highlight). The
> rules it relies on, such as validity, live in the pure `CommandProcessor`.

## My understanding

<!-- Yours. AI never edits below this line. -->
