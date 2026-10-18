---
shell: pattern
seen-in:
  - "[[Match]]"
  - "[[Simulation]]"
  - "[[The tick]]"
tags: [pattern]
---

# 🔁 Same code, three places

> [!question] The question
> The simulation runs on players' phones, on Unity's servers and on a developer's PC. Why is
> it one copy of the code, not three?

**Kind:** pattern. An idea that recurs at different shells of this codebase.

## The pattern

**When several parties must agree on a result, they run the *same source code*, not
separate implementations of the same rules.**

## Where you've seen it

| Code | Runs in |
|---|---|
| `Game/Simulation/` (state, tick, commands) | Both peers · the Cloud Code referee · the balance rig · dotnet tests |
| `MatchFactory` (tick-0 board) | Live matches · the referee · the rig ("MatchFactory is the one starting board") |
| `Backend/Shared/` DTOs and policies | Unity client · server module |

## What it buys

No "two implementations disagree" bugs, and one fix updates every place.

## What it costs

- **The code must fit the strictest place it runs:** no Unity (for the server) and deterministic (for the peers).
- **Deployment must match.** The same *source* doesn't help if the server runs last month's
  build, so versions and content hashes are checked.
- **Global statics bite.** A process running several matches (rig, referee) must run them
  one at a time or take a lock.

## My understanding

<!-- Yours. AI never edits below this line. -->
