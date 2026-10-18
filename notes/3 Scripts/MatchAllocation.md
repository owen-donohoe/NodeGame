---
shell: 3
up: "[[Ranked matchmaking]]"
concepts:
  - "[[Idempotency]]"
tags: [shell/3, realm/backend, system/cloud, script]
file: dotnet/NodeWarCloud/NodeWarCloud/MatchAllocation.cs
sources:
  - dotnet/NodeWarCloud/NodeWarCloud/MatchAllocation.cs
verified: ce9fb6bf
---

# MatchAllocation

> [!question] The question
> Matchmaker says "these two". What does the server do before the match is allowed to exist, and how does it clean up if something fails halfway?

**Where you are:** [[Home]] › [[Backend]] › [[Cloud]] › [[Ranked matchmaking]] › **MatchAllocation** (script)

**Open:** [MatchAllocation.cs in VS Code](vscode://file/C:/Dev/NodeGame/dotnet/NodeWarCloud/NodeWarCloud/MatchAllocation.cs)

## What it is

`Allocate(matchId, players, now)`: the rules behind `Matchmaker_Allocate`. Storage and time are
passed in, so it's testable. `RankedMap` pins every ranked match to `hourglass-01` ("neither player proposes or votes").

## Read `Allocate`

1. **Idempotent retry:** if the record already exists, return success. "Retries keep the original pre-match snapshots."
2. **Validate tickets:** two distinct players, protocol/sim/content present, equal, and current, plus a known balance.
3. **Read both players:** complete records, no discipline block, arena gap ≤1.
4. **Claim both** (`ActiveMatchClaims.Claim`): fails if either already holds an active match.
5. **Re-read under the claims, and re-check block and arena.** "A previous settlement may have finished between initialization and acquiring the claims. Only snapshot state read under our claims."
6. **Create the record** with snapshots (`MatchRecords.Create`). A conflict means another retry created it, which is fine.
7. **`finally`: compensate.** If not created *and* no record exists, release both claims. "Do not release a concurrent retry's successfully created record."

## Design lessons

- **Check, claim, re-check, then act** (a race-safe ordering).
- **Compensation instead of transactions:** without a transaction, a failed multi-step operation
  must undo its own partial work (releasing claims). This is sometimes called the *saga* pattern.
- **When unsure, leave it to expire:** if even the cleanup read fails, the claims simply time out after 2 hours.

## Check yourself

> [!question]- Why read the players' state a *second* time after claiming them?
> Between the first read and the claim, an earlier match could have settled and changed their
> rating or arena. Snapshotting the first read would freeze stale data into this match.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
