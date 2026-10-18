---
shell: 2
up:
  - "[[Cloud]]"
  - "[[Netcode]]"
down:
  - "[[MatchHold]]"
concepts:
  - "[[Server authority]]"
tags: [shell/2, realm/backend, system/cloud, system/netcode]
sources:
  - dotnet/NodeWarCloud/NodeWarCloud/MatchHold.cs
  - dotnet/NodeWarCloud/NodeWarCloud/MatchDiscipline.cs
verified: ce9fb6bf
---

# Disconnect holds

> [!question] The question
> The connection between two players breaks. Each sees the *other* go silent. Who left, who
> wins, and who gets punished, when both stories look identical from the inside?

**Where you are:** [[Home]] › [[Backend]] › [[Cloud]] › **Disconnect holds** (mechanism)

## The problem

When a peer-to-peer link breaks, both sides see exactly the same thing: silence. A rage-quitter
and a player whose router died are indistinguishable to the peer. Awarding the win to "whoever
stayed" would reward people who pull their *opponent's* plug. Never resolving it would trap honest players.

## The idea

**Ask a third party. Both clients heartbeat to the server (*presence*) throughout the match. When
the link breaks, each client enters a *hold* and asks the server, which compares *its own* view of
who is still talking. Only server-measured absence can win or lose a match.**

## How it works here

Clients send `Presence(matchId, holding)` every 4 s from the draft onward, and every 1 s while urgent.
Each call writes only the caller's `presence-N` key ([[Player records in Cloud Save]]).

| Situation (server time) | Result |
|---|---|
| Connected ≥15 s, caller holding ≥10 s, opponent absent >10 s | Caller may claim: the opponent abandoned, the caller wins, the opponent gets a strike |
| Opponent seen but *not* holding for 30 s | Counts as absent (they're ignoring the hold) |
| Both seen and both holding (60 s / 50 s) | **Void**: the link broke, nobody is at fault |
| Client offline for 90 s | The client may leave locally |

On the client: wait 10 s, allow action until 60 s, then request resolution (`DisconnectHold`).

**Discipline** (`MatchDiscipline`, using pure `DisconnectPenalty` rules): abandoning gives a strike.
Levels 1–2 no block, 3–4 two minutes, 5 one hour, 6 one day, 7+ two days. One level decays per
16 strike-free hours. Surrender loses without a strike, and both players leaving gives no strike.

## Why this, not that

| Option | Verdict |
|---|---|
| **Server presence decides** (chosen) | The only party that can tell "you left" from "the link broke" |
| Whoever stays wins | Rewards attacking the opponent's connection |
| Always void on disconnect | Rage-quitting a losing game becomes free |
| Peers vote | Both peers see the same silence, so there's nothing to vote on |

## What it costs later

"Mutual fake holds remain indistinguishable from a broken peer link" (cloud card). Two
colluding players can always produce a void.

## Check yourself

> [!question]- Why does presence write a per-slot key instead of updating the match record?
> Heartbeats every few seconds from two players would constantly conflict with each other and
> with reporting on a shared record ([[Write locks]]).

## Go deeper ↓

- [[MatchHold]]

## My understanding

<!-- Yours. AI never edits below this line. -->
