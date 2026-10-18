---
shell: concept
used-by:
  - "[[Input]]"
  - "[[World view]]"
  - "[[TickRunner]]"
  - "[[Command pipeline]]"
tags: [concept, topic/csharp, topic/architecture]
---

# Events and observers

> [!question] The question
> How does the simulation tell the view "a villager just died" without the simulation
> knowing the view exists?

**Kind:** concept. True outside Node War. Used by [[Input]], [[World view]], [[TickRunner]] and [[Command pipeline]].

## The idea

**The *subject* exposes an event. Anyone interested *subscribes* a handler. When something
happens, the subject *raises* the event, and every handler runs. The subject knows only
that the list of handlers exists, not who is on it.** This is the Observer pattern, and C#
builds it in with `event`.

```csharp
public event Action<TickEventLog> TickSimulated;   // subject declares it
runner.TickSimulated += OnTick;                    // observer subscribes
TickSimulated?.Invoke(tickEvents);                 // subject raises it (?. = "if anyone is listening")
runner.TickSimulated -= OnTick;                    // observer unsubscribes, on teardown!
```

## In Node War

| Subject | Event | Observers |
|---|---|---|
| `ITickProvider` | `TickSimulated(TickEventLog)` | Villager flash and swing, indicators, screen shake |
| `TickRunner` / `LockstepCore` | `CommandsApplied`, `HashComputed` | The match recorder (for the log) |
| `CommandSystem` | `MoveIssued`, `OrderDragBegan/Hover/Ended` | `PendingOrderView` |
| `GestureClassifier` | `Published(GestureEvent)` | `PointerGestureSource`, and from there `TapRouter`, selection, camera |
| `LockstepCore` | `OnDesync`, `HoldStarted`, `RolledBack`… | `GameManager`, UI |

**The direction is what matters.** Events point *outward* from the simulation and drivers
towards presentation and recording. That's how [[Presentation reads, never writes]] works
in practice: the simulation never references a view, so it can't depend on one.

## Rules this codebase adds

- **Events are output, never input to rules.** `TickEventLog` is never read by the simulation,
  and the recording events "exist for recording only; nothing may act on the simulation through them".
- **Copy what you keep.** The log is reused for every tick, and `MoveIssued`'s list is copied
  before the selection is cleared.
- **Unsubscribe symmetrically.** `TapRouter` subscribes in `OnEnable` and unsubscribes in
  `OnDisable`/`OnDestroy`, with a `subscribed` flag so it's never double-subscribed.

## Traps

- **Forgetting to unsubscribe.** The subject keeps the observer alive (a memory leak), and
  handlers run on destroyed objects.
- **Subscribing twice.** The handler runs twice per event.
- **Throwing in a handler.** The remaining handlers may not run.
- **Using events to make game decisions** in a deterministic system.

## Check yourself

> [!question]- Why does `TickSimulated` fire inside the tick loop rather than once per frame?
> A frame can run several ticks. Firing per tick means a death on the first of three ticks
> still triggers its effect.

## Learn more

- Robert Nystrom, [Observer](https://gameprogrammingpatterns.com/observer.html), from *Game Programming Patterns*
- Microsoft, [Events (C# programming guide)](https://learn.microsoft.com/dotnet/csharp/programming-guide/events/)

## My understanding

<!-- Yours. AI never edits below this line. -->
