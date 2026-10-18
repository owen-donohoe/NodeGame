---
shell: 2
up: "[[Progression]]"
concepts:
  - "[[Rating systems]]"
tags: [shell/2, realm/backend, system/progression]
sources:
  - dotnet/NodeWar.Progression/RankPoints.cs
  - dotnet/NodeWar.Progression/Glicko2.cs
  - dotnet/NodeWar.Progression/MatchSettlement.cs
verified: ce9fb6bf
---

# Rating and rank

> [!question] The question
> After a win, exactly how many Rank Points do you get, and why does a new player climb so
> fast while losing so little?

**Where you are:** [[Home]] › [[Backend]] › [[Progression]] › **Rating and rank** (mechanism)

## The problem

The visible number must be *motivating* (steady gains, small losses while you're still
climbing to your level) and *honest* (in the long run it should reflect skill). If it just
showed the hidden rating, it would jump around. If it ignored the hidden rating, it would be
a grind meter unrelated to skill.

## The idea

**Treat RR as a claim about skill: RR implies a rating of `1000 + 0.5 × RR`. After each
match, nudge the RR gain or loss by how far your *real* (hidden) rating is from that implied
rating. If you're better than your rank says, wins pay more and losses cost less, until RR catches up.**

## How it works here

Settlement, per match (`MatchSettlement.Settle`):

1. **Decay for inactivity:** each player's RD grows for complete idle weeks (skipped for new players).
2. **Glicko-2 update:** each player is rated against the *other's* pre-match (decayed) rating. One match is one rating period.
3. **RR delta** (`RankPoints.RRDelta`):

```
implied    = 1000 + 0.5 × RR
correction = 0.04 × (hiddenR_after − implied)
magnitude  = 20 + correction   (if won)
           = 20 − correction   (if lost)
magnitude  = clamp(magnitude, 8, 40), rounded away from zero
delta      = +magnitude if won, −magnitude if lost
```

4. **Arena:** RR is floored at 0, the arena comes from the thresholds 0/300/700/1200/1800/2500, and the highest arena reached is kept.
5. **Inventory clamp:** equipped variants that are now ineligible for the current arena are swapped for eligible ones. Ownership is kept.

### Worked example

A new player (RR 0, so implied rating 1000) wins and their hidden R becomes, say, 1660:
`correction = 0.04 × 660 = 26.4`, so the magnitude is `20 + 26.4 = 46.4`, clamped to **+40 RR**.
If instead they lose and R becomes 1340: `correction = 0.04 × 340 = 13.6`, so the magnitude is
`20 − 13.6 = 6.4`, clamped to **−8 RR**.

So new players with real skill climb at +40 per win and lose 8, until RR "catches up" with
their hidden rating and gains settle towards ±20.

## Why this, not that

| Option | Gain | Pay | Verdict |
|---|---|---|---|
| **Hidden Glicko-2 + RR with convergence** (chosen) | Fair matchmaking plus a motivating, convergent rank | Two numbers to explain. RR can lag skill | Chosen |
| Show the Glicko rating | One honest number | Volatile and demoralising | No |
| Fixed ±25 RR | Simple | Rank never tracks skill. A pure grind | No |
| Placement matches | Fast initial placement | Extra phase and UI | Not implemented. The convergence partly replaces it |

## What it costs later

- Constants are placeholders. Calibrate from real matches (see [[Constants come from measurements]]).
- There's no "boundary protection" (you can drop an arena immediately), which is a design choice to revisit.
- Server config and client display must share thresholds (`RankTable`).

## Check yourself

> [!question]- A player at RR 1000 (implied 1500) has hidden R 1400 and wins. What's their gain?
> correction = 0.04 × (1400 − 1500) = −4, so the magnitude is 20 + (−4) = 16, giving **+16 RR**.
> They're over-ranked, so wins pay less.

> [!question]- Why are ratings `double` here when the simulation forbids floats?
> Settlement runs once, on the server, and the *result* is stored. No two machines need to
> agree on the computation bit for bit.

## My understanding

<!-- Yours. AI never edits below this line. -->
