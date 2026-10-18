---
shell: 3
up: "[[Input delay]]"
tags: [shell/3, realm/match, system/netcode, script]
file: Assets/Scripts/Game/Network/InputDelayController.cs
sources:
  - Assets/Scripts/Game/Network/InputDelayController.cs
verified: ce9fb6bf
---

# InputDelayController

> [!question] The question
> 147 lines decide how laggy your commands feel. How does it decide, and why is it so cautious?

**Where you are:** [[Home]] › [[Match]] › [[Netcode]] › [[Input delay]] › **InputDelayController** (script)

**Open:** [InputDelayController.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Network/InputDelayController.cs)

## What it is

An `internal sealed class` with no clock of its own. `LockstepCore` calls
`OnLiveTick(now, late, slack, disturbed, peerDelay)` after each live tick, and reads `Request`
(the delay to ask the peer for, or 0 for "no opinion") when packing each input.

## Read it in this order

1. **The header comment.** The whole design in 18 lines: watch your own stalls, ask the peer, raise fast, lower slow.
2. **Constants.** `BaseDelay = 2`, `MaxDelay = 6`, `Window = 30`, `LateToRaise = 3`,
   `CalmToLower = 200`, `SlackToLower = 0.14`, and cooldowns of 1.5 s and 10 s.
3. **`OnLiveTick`.** Disturbed means clear the window and return. Otherwise push into the ring,
   then raise if there are ≥3 late ticks and the cooldown has passed, or lower if 200 calm ticks
   with minimum slack ≥ 0.14 s and the 10 s cooldown has passed.
4. **`Push`.** A **ring buffer** of 30 booleans with a running `lateCount`: O(1) per tick, no allocation.
5. **`CalmSlackMin`.** The minimum over the *last 200* calm ticks, not all time. The comment
   explains: an all-time minimum "would remember a thin tick from minutes ago and never let the delay down".

## Data structure to learn here: the ring buffer

```csharp
if (filled == Window && late[next]) lateCount--;   // the oldest value falls out
late[next] = wasLate;                              // the newest goes in its place
if (wasLate) lateCount++;
next = (next + 1) % Window;                        // wrap around
```

A fixed array used as a sliding window: "how many of the last 30?" without a list that grows, and without recounting.

## Check yourself

> [!question]- Why is lowering gated on *minimum* slack instead of average slack?
> The average can look comfortable while a few ticks are barely on time. Lowering the delay
> would turn exactly those into stalls. The minimum guarantees every recent tick had ≥140 ms to spare.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
