---
shell: 3
up: "[[Match settlement]]"
concepts:
  - "[[Idempotency]]"
tags: [shell/3, realm/backend, system/cloud, script]
file: dotnet/NodeWarCloud/NodeWarCloud/MatchSettler.cs
sources:
  - dotnet/NodeWarCloud/NodeWarCloud/MatchSettler.cs
verified: ce9fb6bf
---

# MatchSettler

> [!question] The question
> 128 lines that update two players' ratings, safely and exactly once. What does each block do?

**Where you are:** [[Home]] › [[Backend]] › [[Cloud]] › [[Match settlement]] › **MatchSettler** (script)

**Open:** [MatchSettler.cs in VS Code](vscode://file/C:/Dev/NodeGame/dotnet/NodeWarCloud/NodeWarCloud/MatchSettler.cs)

## What it is

A `sealed class` used by both `MatchReporting` (agreed reports) and `MatchRendezvous`
(forfeits), so every way a match can be won settles identically. It depends on two
abstractions: `Func<string, ISettlementPlayerStore>` (a store per player ID) and `InventoryRules`.

## Read it in this order

### 1. `Agreed(record)`

```csharp
var accepted = record.reports.Where(r => r.accepted).ToList();
return accepted.Count == 2 && different players && same winner && same endTick && same finalHash;
```

Refused reports may sit in the list for audit, but only accepted ones count.

### 2. `Settle`, part 1: compute once

```csharp
var outcome = MatchSettlement.Settle(new SettlementInput {
    Players = /* from record.players: the PRE-MATCH snapshots */,
    Winner = winner,
    NowUnixSeconds = record.settlementUnixSeconds   // frozen "now"
}, Config);
record.outcomes = /* won, rrDelta, rrAfter, arenaAfter, promoted, demoted */;
```

Every input is frozen on the record, so any retry computes the identical outcome.

### 3. `Settle`, part 2: apply per player

```csharp
for (int p = 0; p < 2; p++)
  for (attempt = 0; attempt < RetryLimit; attempt++) {
      var read = await store.ReadForSettlementAsync();       // state + write locks
      if (settledIds.Contains(record.matchId)) break;        // ← idempotency guard
      if (!committed && claim no longer this match) return false;  // ← void instead
      settledIds.Insert(0, matchId); trim to 200;
      state.Rating = …; state.Rank = …;
      inventory.GrantDefaults(state);
      InventoryClamp.ClampToArena(state.Inventory, state.Rank.Arena);
      history.Insert(0, matchId); trim to 20;
      try { await store.WriteForSettlementAsync(state, read.WriteLocks); break; }
      catch (RecordConflictException) when (attempt + 1 < RetryLimit) { }
  }
```

Notice:
- **The guard check comes *after* the fresh read** and inside the retry loop. A conflict might
  mean another call just settled this player.
- **`catch … when (…)`** is a C# *exception filter*. On the last attempt the filter is false,
  so the exception propagates instead of being swallowed.
- **`Insert(0, …)` then trim** keeps the newest first, with a fixed maximum size.

## Who uses it

`MatchReporting.Report` (agreement or both-left) and `MatchRendezvous` (committed forfeits).
Tests: `NodeWarCloud.Tests` cover partial settlement and conflicts with in-memory stores.

## Check yourself

> [!question]- What would go wrong if the `settledIds.Contains` check ran *before* the retry loop instead of inside it?
> A conflict retry would skip the check. If the conflicting writer was another call settling
> the same player, this call would then apply the match a second time.

> [!question]- Why return `false` (rather than throw) when the claim no longer matches?
> It's an expected outcome, not a bug. The caller turns it into a Void match. Throwing would
> make the caller retry something that can't succeed.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
