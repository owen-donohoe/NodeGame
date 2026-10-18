---
shell: concept
used-by:
  - "[[NetworkManager]]"
tags: [concept, topic/csharp, topic/concurrency]
---

# Threads and the main thread

> [!question] The question
> Why can't a network receive just run in `Update`, and why does almost everything in Unity
> have to happen on "the main thread"?

**Kind:** concept. True outside Node War. Used by [[NetworkManager]].

## The idea

A **thread** is an independent line of execution. Unity runs your scripts on one **main
thread**, and most Unity APIs (GameObjects, transforms, rendering) may *only* be touched from
it. Some operations **block**: they wait, doing nothing, until something happens (a packet
arrives). A blocking call on the main thread freezes the game.

**The standard fix:** a background thread does the blocking wait and pushes results into a
**thread-safe queue**. The main thread drains that queue once per frame.

```csharp
// background thread
while (running) { byte[] p = udp.Receive(ref ep); lock (queueLock) queue.Enqueue(p); }
// main thread, each frame
lock (queueLock) { while (queue.Count > 0) packets.Add(queue.Dequeue()); }
```

`lock` makes sure only one thread touches the queue at a time. Without it, both threads could
modify the queue at once and corrupt it.

## In Node War

- `NetworkManager` (DirectUDP) works exactly this way.
- `LockstepCore` and the simulation are **single-threaded on purpose**: thread timing differs
  between machines, and determinism can't depend on it.
- `async`/`await` (Relay, UGS calls) *usually* continues on the main thread in Unity, but it's
  asynchronous: your object may be destroyed by the time the `await` finishes.

## Traps

- **Touching Unity objects from a background thread.** Exceptions, or subtle corruption.
- **Forgetting `lock`.** Rare, unreproducible crashes.
- **`async void`.** Exceptions escape to nowhere. Prefer `async Task`.

## Learn more

- Microsoft, [Threading in C#](https://learn.microsoft.com/dotnet/standard/threading/)
- Unity Manual, *Unity's main thread and async*

## My understanding

<!-- Yours. AI never edits below this line. -->
