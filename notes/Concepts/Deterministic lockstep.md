---
shell: concept
used-by:
  - "[[Match]]"
  - "[[Netcode]]"
  - "[[Simulation]]"
tags: [concept, topic/networking, topic/determinism]
---

# Deterministic lockstep

> [!question] The question
> What is the networking model that Age of Empires used over dial-up modems, why do RTS
> games still use it, and how does it compare with what shooters and fighting games do?

**Kind:** concept. True outside Node War. Used by [[Match]], [[Netcode]] and [[Simulation]].

## The idea

**Every player runs the full simulation. Players exchange only their inputs for each turn.
No one advances to turn N until they have everyone's inputs for turn N. If the simulation
is deterministic, everyone stays identical forever.**

## The family of models

| Model | What's sent | Who is the authority | Typical genre | Weakness |
|---|---|---|---|---|
| **Deterministic lockstep** | Inputs | Nobody (everyone computes) | RTS (Age of Empires, StarCraft), Node War | Waits for the slowest player. Hidden info isn't secret. Desyncs are catastrophic |
| **Lockstep + rollback** (GGPO) | Inputs, with prediction | Nobody | Fighting games | Rewinds and re-simulates often, so the simulation must be cheap |
| **Client-server with state sync** | State snapshots or deltas | The server | Shooters, MMOs, most Unity Netcode games | Bandwidth grows with world size, and you pay for servers |
| **Client-server with prediction and reconciliation** | Inputs up, state down | The server | Competitive shooters | Complex. Players see slightly different pasts (lag compensation) |

Node War is lockstep with a *limited* rollback: it only speculates when the opponent goes
quiet for more than 300 ms, and for at most 2 s ([[Netcode]]).

## Why RTS games choose it

An RTS has **hundreds of units but few inputs per second**. Sending state would mean every
unit's position on every update. Sending inputs is a handful of commands per second,
whatever the army size. The famous number from Age of Empires: 1,500 archers fighting over a
28.8 kbit/s modem.

## What it demands

1. **Bit-exact determinism.** See [[Why floats break determinism]].
2. **Identical starting state** (the same map, picks and balance), checked in a handshake.
3. **Identical input order.** Node War uses seat order: player 0's commands, then player 1's.
4. **Desync detection.** Periodic state hashes.
5. **Latency hiding.** Input delay, plus previews for responsiveness.

## Check yourself

> [!question]- Why can't a lockstep RTS have truly secret fog of war?
> Every client holds the full state, because it has to compute it. A modified client can
> simply draw what it isn't supposed to. Secret information needs an authority that withholds it.

> [!question]- Fighting games use rollback constantly, but Node War mostly doesn't. Why?
> A fighting game has two characters and must feel instant (60 Hz, no input delay is
> tolerable). An RTS tolerates 200 ms of input delay, and re-simulating many units for many
> ticks costs more. Node War uses delay as the main tool and rollback only as a fallback.

## Learn more

- Terrano & Bettner, [1500 Archers on a 28.8](https://www.gamedeveloper.com/programming/1500-archers-on-a-28-8-network-programming-in-age-of-empires-and-beyond)
- Glenn Fiedler, [Deterministic Lockstep](https://gafferongames.com/post/deterministic_lockstep/)
- Gabriel Gambetta, [Fast-Paced Multiplayer](https://www.gabrielgambetta.com/client-server-game-architecture.html): the client-server family, explained interactively

## My understanding

<!-- Yours. AI never edits below this line. -->
