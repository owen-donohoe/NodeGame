---
shell: pattern
seen-in:
  - "[[Match]]"
  - "[[Netcode]]"
  - "[[Tick packets]]"
  - "[[Draft]]"
  - "[[Match log]]"
  - "[[Referee]]"
tags: [pattern]
---

# 🔁 Send intent, not results

> [!question] The question
> Why does Node War keep passing around *what someone did* instead of *what happened*?

**Kind:** pattern. An idea that recurs at different shells of this codebase. Spot it and
a lot of the architecture stops being surprising.

## The pattern

**If every party runs the same deterministic rules, sending the *cause* (an intent)
is enough: each party computes the *effect* (the state) itself.**

Intent is small and state is big. Intent can be checked after the fact, but a reported
result can only be believed or doubted.

## Where you've seen it

| Shell | Where | The intent sent | The result *not* sent |
|---|---|---|---|
| 0 | [[Match]] | Commands | The battle |
| 1 | [[Netcode]] | `TickInput` per tick | `SimulationState` |
| 2 | [[Tick packets]] | 24 bytes: "move villager 7 to node 12" | Villager 7's position on every later tick |
| 1 | [[Draft]] | Placement picks | The finished board (`MatchFactory` builds it from the picks on both sides) |
| 1 | [[Match log]] | The confirmed commands plus occasional hashes | A video or a final scoreboard |
| 2 | [[Referee]] | Replays the log | The winner the clients reported |

Each row is the same move at a different zoom level, which is why it is a pattern rather
than a coincidence.

## What it buys

- **Bandwidth that ignores scale.** 25 villagers walking for 30 seconds cost one command each.
- **Free replays and verification.** A recording is just the intents, and re-running them *is* the check.
- **One source of truth for the rules.** Every party runs the same simulation code, so
  there's no second copy of the rules that could disagree.

## What it costs

- **Determinism everywhere the intent is replayed:** both peers, the referee and the
  balance rig. One `float` in the simulation breaks all four.
- **Versions must match exactly.** Old intents under new rules give a different result.
  That's why builds swap a [[Build identity handshake]] and logs carry format tags.
- **Replaying takes compute time.** The referee re-simulates the whole match instead of reading a number.

## Where it doesn't apply, and why

- **Your rating in Cloud Save is stored as a result**, not re-derived from every match
  you've played. Recomputing years of history on each read would cost far more than
  storing one number, and nobody needs to replay a rating. *When the result is cheap to
  store and the history is expensive to replay, store the result.*
- **Emotes** are sent as intent but never enter the simulation. They are cosmetic, so
  they can skip the tick system entirely.

## Spot it yourself

> [!question]- Unity's Netcode for GameObjects typically syncs `NetworkVariable`s (positions, health). Is that intent or result? What does it trade?
> Result. It sends state, so clients don't need deterministic simulations and can safely
> differ (floats are fine). The price is bandwidth that grows with how much state changes,
> and a server that must be the authority.

> [!question]- Git stores commits as snapshots of files, not as the keystrokes you typed. Intent or result?
> Result, at file level. Replaying keystrokes would require a deterministic editor and
> would be slow and fragile. It's the same trade-off as the Cloud Save counter-example.

## My understanding

<!-- Yours. AI never edits below this line. -->
