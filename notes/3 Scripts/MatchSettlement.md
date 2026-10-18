---
shell: 3
up:
  - "[[Rating and rank]]"
  - "[[Match settlement]]"
tags: [shell/3, realm/backend, system/progression, script]
file: dotnet/NodeWar.Progression/MatchSettlement.cs
sources:
  - dotnet/NodeWar.Progression/MatchSettlement.cs
verified: ce9fb6bf
---

# MatchSettlement

> [!question] The question
> What's the pure maths of one ranked result: two players in, two new ratings and ranks out, with no database in sight?

**Where you are:** [[Home]] › [[Backend]] › [[Progression]] › [[Rating and rank]] › **MatchSettlement** (script)

**Open:** [MatchSettlement.cs in VS Code](vscode://file/C:/Dev/NodeGame/dotnet/NodeWar.Progression/MatchSettlement.cs)

## What it is

A 106-line pure function: `Settle(SettlementInput, SettlementConfig) → SettlementOutcome`.
"Settles trusted server snapshots using the supplied server time, without I/O."

## Read `Settle`

1. **Guard clauses:** exactly two players, winner 0 or 1, configs present, period > 0. Each throws a precise `Argument…Exception`.
2. **Decay both ratings for idle weeks** (`DecayForInactivity`), skipping new players (`LastMatchUnixSeconds == 0`).
3. **For each player:** `Glicko2.UpdateSingleMatch(me, opponent, score)` → `RankPoints.RRDelta` →
   `Arenas.ApplyRRDelta` → promoted/demoted flags.

Notice `before[1 − i]`: each player is rated against the opponent's **pre-match** rating, never the
one just updated. Otherwise the order (player 0 first) would change player 1's result.

## Design lessons

- **Configuration as an object** (`SettlementConfig` with defaults) instead of scattered constants, so tests can vary it.
- **Immutable results** (`readonly struct PlayerSettlement`).
- **No I/O, no clock:** the caller ([[MatchSettler]]) supplies frozen inputs, so the same inputs always give the same outcome, which makes retries safe.

## Check yourself

> [!question]- What bug would appear if player 1 were rated against player 0's *updated* rating?
> The results would depend on processing order. Player 1's change would be computed against a
> rating that already includes this match, which double-counts it.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
