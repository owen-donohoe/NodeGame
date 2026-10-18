---
shell: concept
used-by:
  - "[[Backend]]"
  - "[[Cloud]]"
tags: [concept, topic/security, topic/architecture]
---

# Server authority

> [!question] The question
> Which machine gets the final say on each piece of data, and why can't the answer just be
> "the player's own device"?

**Kind:** concept. True outside Node War. Used by [[Backend]] and [[Cloud]].

## The idea

**For every piece of data, name one *authority*: the only party allowed to decide its value.
Everyone else holds a copy and *asks* for changes. Anything a player controls (device,
memory, network traffic) can be modified, so fairness-critical data needs an authority the player doesn't control.**

## Node War's authority map

| Data | Authority | Why |
|---|---|---|
| Match state during play | **Nobody alone**: both peers compute it; the hashes and the referee check it | Lockstep ([[Deterministic lockstep]]) |
| Match result for ranked | **Cloud Code** (referee plus agreement) | Neither player can be trusted |
| Rating, rank, inventory, discipline | **Cloud Code** (Protected Cloud Save) | Fairness and value |
| Settings, local profile | **The device** | Only affects that player |
| Roster, map, slot order | **Cloud Code** (allocation) | Clients don't get to choose opponents or maps |
| Time for server decisions | **Server clock** | Client clocks can be changed |

## The general rule

> Let the client *propose*, let the authority *dispose*.

The client can predict, preview and display. Only the authority commits.

## Traps

- **"We'll validate it on the client."** Validation on the client is for UX. Validation on the authority is for correctness.
- **A client-supplied player ID.** Use the authenticated identity.
- **Client timestamps** for anything that matters.

## Learn more

- Gabriel Gambetta, [Client-Server Game Architecture](https://www.gabrielgambetta.com/client-server-game-architecture.html): "never trust the client".

## My understanding

<!-- Yours. AI never edits below this line. -->
