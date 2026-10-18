---
shell: 3
up:
  - "[[Referee]]"
  - "[[Write locks]]"
tags: [shell/3, realm/backend, system/cloud, script]
file: dotnet/NodeWarCloud/NodeWarCloud/MatchReporting.cs
sources:
  - dotnet/NodeWarCloud/NodeWarCloud/MatchReporting.cs
verified: ce9fb6bf
---

# MatchReporting

> [!question] The question
> What happens, line by line, when a player's game calls `ReportMatch` with its log?

**Where you are:** [[Home]] › [[Backend]] › [[Match log]] › [[Referee]] › **MatchReporting** (script)

**Open:** [MatchReporting.cs in VS Code](vscode://file/C:/Dev/NodeGame/dotnet/NodeWarCloud/NodeWarCloud/MatchReporting.cs)

## What it is

The rules behind the `ReportMatch` function: one `Report` method that is a **read → decide →
conditional write** loop over the match record, plus `Verify` (parse, eligibility, referee)
and `DecideIfBothLeft`.

## Read it in this order

### 1. The loop's shape

```csharp
while (true) {
    var read = await matches.ReadAsync(matchId);   // record + its write lock
    /* … decide, mutating `record` … */
    try { await matches.WriteAsync(record, read.WriteLock); }
    catch (RecordConflictException) { if (++conflicts >= RetryLimit) throw; haveOwnReport = false; }
}
```

This is the [[Write locks]] pattern in its fullest form. Read the comment on `haveOwnReport`:
on conflict, *everything* decided in that pass is discarded.

### 2. The decision ladder (inside the loop)

In priority order:
1. Unknown match or caller not on the roster → refuse.
2. Terminal state (Settled / Void / Disputed) → apply discipline, release claims, return the state.
3. Any `pendingLogBase64` → copy the log to `log-N` first (crash recovery).
4. **Committed decision** (forfeit or agreement) → `MatchSettler.Settle` → Settled (or Void).
5. Claims expired or gone → Void.
6. Pending for over 10 minutes → Void (timeout), evaluated *lazily* on whichever call comes next.
7. Otherwise: if this caller already has an accepted report, wait for the opponent. If not, `Verify`
   their log, add the report, and move Open → Pending, or to Disputed if two disagree.

**Lazy evaluation** is worth noticing. There's no timer running on the server. Timeouts are
checked when someone next calls. That's normal in serverless design, because no process stays alive to wait.

### 3. `Verify`

The size cap, `MatchLogFormat.TryRead`, `MatchEligibility.Check`, `referee.Verify`, then
"accepted" means *ok, game over, and the winner is 0 or 1*. An unfinished but consistent log is
kept with its breaches, in case both players left.

### 4. `DecideIfBothLeft`

It uses the **earlier** end tick, *"the last point two honest logs still agree on"*, because
breaches only rise. Fewer breaches wins, a tie voids, and nobody gets a strike.

## Check yourself

> [!question]- Why does the log get stored as `pendingLogBase64` on the report first, and only then copied to `log-N`?
> Only the call whose record write *wins* the conflict should own `log-N`. Keeping the bytes on
> the report until the copy completes means a crash between the two steps is recovered on the
> next call (step 3 of the ladder).

> [!question]- With no server timers, how does a Pending match ever time out if nobody calls again?
> It doesn't, until a later server call looks at the record. The decision is made then, using
> server time. The client's result tracking and retry paths keep making calls after a match,
> which is what triggers it in practice. (Exercise: find which functions besides `ReportMatch`
> evaluate the timeout.)

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
