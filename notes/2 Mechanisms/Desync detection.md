---
shell: 2
up:
  - "[[Netcode]]"
  - "[[Simulation]]"
down:
  - "[[SimulationStateHasher]]"
tags: [shell/2, realm/match, system/simulation, system/netcode]
sources:
  - Assets/Scripts/Game/Simulation/SimulationStateHasher.cs
  - Assets/Scripts/Game/Network/LockstepCore.cs
verified: ce9fb6bf
---

# Desync detection

> [!question] The question
> Two machines are supposed to hold identical game states. How do they check, cheaply,
> without sending the whole state?

**Where you are:** [[Home]] › [[Match]] › [[Simulation]] › **Desync detection** (mechanism)

## The problem

A determinism bug (one float, one unordered loop) makes the two simulations drift apart
silently. Each player sees a different game, and nobody knows until someone complains that
"my army vanished". Comparing whole states would cost kilobytes per check.

## The idea

**Reduce the whole state to one 32-bit number (a hash). Every 50 confirmed ticks, each side
sends its hash in the next tick packet. If the numbers differ, the states differ: that's a desync.**

## How it works here

```
tick 50, 100, 150… (confirmed only)
  local:  hash = SimulationStateHasher.ComputeHash(state)  →  stored, and sent in the next TickInput.stateHash
  remote: arrives in their TickInput  →  CompareHash  →  mismatch?  →  OnDesync(tick)
```

The hasher (`SimulationStateHasher`):

```csharp
unchecked {
    int hash = 17;
    hash = hash * 31 + state.tickCount;
    hash = hash * 31 + (state.gameOver ? 1 : 0);
    // … every player field, every node field, every villager field, in array order
}
```

- **Multiply by 31 and add** is a classic *polynomial* hash: order matters, so swapping two values changes the result.
- **`unchecked`** lets the integer overflow wrap around deliberately. That's part of the hash's mixing.
- **Fixed order** (array index order) keeps it deterministic. That's another reason the simulation uses no dictionaries.
- **Tagged optional fields:** newer fields are only mixed in when non-zero, with a tag number
  (2000, 2001, 2010…). A match that never uses a newer feature keeps the *same* hash as before,
  so old baselines and logs stay valid.
- **View-only data is excluded** (for example, a node's grid position for drawing).

The same hashes go into the [[Match log]], and the referee checks every one on replay.

## Why this, not that

| Option | Gain | Pay | Verdict |
|---|---|---|---|
| **Periodic 32-bit hash** (chosen) | 4 bytes per check | Detects *that*, not *what*. Collisions are possible but astronomically unlikely to matter | Chosen |
| Send the full state | Shows exactly what differs | Kilobytes per check | Only worth it as a debug tool |
| Hash every tick | Catches a desync sooner | CPU cost every tick | 50 ticks (5 s) is enough to catch bugs |
| Cryptographic hash (SHA-256) | Resists deliberate forgery | Slower, and forgery isn't the threat here (both sides compute it) | Unneeded |

## What it costs later

- **Forget to hash a new field and desyncs in it go unseen.** It's a CLAUDE.md rule: "every new `SimulationState` field must be added to `SimulationStateHasher`".
- **Hash changes need care.** Pinned test baselines record expected hashes, and a hash differing between CI machines is a real determinism bug, never a reason to re-pin.

## Check yourself

> [!question]- Why is the hash taken only on confirmed ticks?
> A speculative state may be rolled back. The peer never had it, so comparing it would raise false desyncs.

> [!question]- Why does adding a new field "only when non-zero, with a tag" protect old replays?
> Matches that never use the feature hash exactly as they did before the field existed, so
> their recorded checkpoint hashes still match on replay.

## Go deeper ↓

- [[SimulationStateHasher]]

## My understanding

<!-- Yours. AI never edits below this line. -->
