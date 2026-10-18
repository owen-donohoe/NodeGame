---
shell: 2
up: "[[Cloud]]"
down:
  - "[[CloudSavePlayerRecordStore]]"
  - "[[MatchReporting]]"
concepts:
  - "[[Optimistic concurrency]]"
tags: [shell/2, realm/backend, system/cloud]
sources:
  - dotnet/NodeWarCloud/NodeWarCloud/CloudSavePlayerRecordStore.cs
  - dotnet/NodeWarCloud/NodeWarCloud/MatchReporting.cs
verified: ce9fb6bf
---

# Write locks

> [!question] The question
> Two server calls read the same record, both change it, and both save. Without
> protection, one change silently vanishes. How does Node War stop that without a database transaction?

**Where you are:** [[Home]] › [[Backend]] › [[Cloud]] › **Write locks** (mechanism)

## The problem: the lost update

```
time →   call A: read inventory (v7) ── equip skin ─────────── write ✓
         call B: read inventory (v7) ──── settlement clamp ─────────── write ✓  ← A's equip is gone
```

Both calls read version 7. Whoever writes second overwrites the first, and nobody notices.

## The idea

**[[Optimistic concurrency]]: every read returns a *write lock* token (effectively a version
stamp). A write sends back the token it read. If the record changed in between, Cloud Save
refuses with HTTP 409 Conflict. The caller then re-reads, re-decides and retries.**

```
call A: read (lock L7) → write with L7 ✓  (record is now L8)
call B: read (lock L7) → write with L7 ✗ 409 Conflict → re-read (L8) → re-decide → write with L8 ✓
```

## How it works here

The same loop appears in `MatchReporting.Report`, `MatchSettler.Settle`, `Equip` and claims:

```csharp
for (attempt = 0; ; attempt++) {
    var read = await store.ReadLockedAsync();          // state + locks
    var decision = Decide(read.State);                  // pure rules
    try { await store.WriteAsync(decision, read.WriteLocks); break; }
    catch (RecordConflictException) when (attempt + 1 < RetryLimit) { /* loop: fresh read */ }
}
```

Details that matter:
- **Re-decide, don't re-apply.** On conflict, `MatchReporting` *discards* its in-flight record
  and its "already added my report" flag, then re-reads everything. The other writer may have
  changed what the right decision is.
- **Lock what you decided from.** Equip reads *and* writes only `inventory`'s lock, so a
  concurrent settlement that clamped inventory makes the stale equip conflict.
- **Batch writes are atomic per player.** Settlement writes rating, rank, inventory and history
  in one `SetProtectedItemBatchAsync` with four locks. All succeed or none do.
- **A null lock means "no check".** The first creation of a key can't be guarded, so the code
  includes an existing key (`rating`) with its lock in the same batch as a fence.
- **Retries are bounded** (3), and then the error surfaces.

## Why this, not that

| Option | Gain | Pay | Verdict |
|---|---|---|---|
| **Optimistic locks** (chosen) | No waiting. Conflicts are rare and cheap to retry | Every write path needs a retry loop and must re-decide | Chosen. It's what Cloud Save provides |
| Pessimistic locks (take a lock, then work) | No retries | Needs a lock service, and a crashed holder blocks everyone | Not available in Cloud Save, and risky in serverless |
| Database transactions (SQL) | Atomic multi-row and multi-player updates | Needs your own database | Not available here. See [[Key-value stores vs SQL]] |
| Last write wins | Simplest | Silent data loss | Never for player records |

## What it costs later

- **Cross-player work can half-complete.** Locks protect *one* player's batch. Settlement writes
  player 0, then player 1, so it must be [[Idempotency|idempotent]] to finish safely on retry ([[Match settlement]]).
- **Hot records conflict more.** As traffic grows, a frequently written key (a shared match
  record) needs smaller keys or less contention, which is why presence has per-slot keys.

## Check yourself

> [!question]- Why must the retry re-run the *decision*, not just resend the same write with the new lock?
> The conflict means someone else changed the data. The old decision was based on stale data.
> Resending it with a fresh lock would bypass the protection and reintroduce the lost update.

> [!question]- Settlement's batch for player 0 succeeds, and then the function crashes before player 1. What now?
> Player 0's `SettledMatchIds` contains the match, and player 1's doesn't. On the next call,
> settlement runs again: it skips player 0 (already settled) and settles player 1. Idempotency finishes the job.

## Go deeper ↓

- [[CloudSavePlayerRecordStore]] · [[MatchReporting]]

## My understanding

<!-- Yours. AI never edits below this line. -->
