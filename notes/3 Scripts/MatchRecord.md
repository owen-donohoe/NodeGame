---
shell: 3
up:
  - "[[Player records in Cloud Save]]"
  - "[[Referee]]"
tags: [shell/3, realm/backend, system/cloud, script]
file: dotnet/NodeWarCloud/NodeWarCloud/MatchRecord.cs
sources:
  - dotnet/NodeWarCloud/NodeWarCloud/MatchRecord.cs
verified: 2ef5a5fb
---

# MatchRecord

> [!question] The question
> What does the server remember about one ranked match, and why does almost every field have a comment explaining *when* it's set?

**Where you are:** [[Home]] › [[Backend]] › [[Cloud]] › **MatchRecord** (script)

**Open:** [MatchRecord.cs in VS Code](vscode://file/C:/Dev/NodeGame/dotnet/NodeWarCloud/NodeWarCloud/MatchRecord.cs)

## What it is

The schema of the `record` key in the private `match-<id>` item, serialised as JSON.
"Server-only stored fields. **Never rename: names are the persisted schema.**"

```
MatchRecord
├── identity    matchId, playerIds[2], createdUnixSeconds, protocol, sim, content, mapId, boardHash
├── snapshots   players[2]: { Rating, Rank, OwnedVariants }   ← frozen at allocation
├── lifecycle   state (Open/Pending/Settled/Void/Disputed), pendingUnixSeconds, settlementUnixSeconds
├── evidence    reports[]: { playerIndex, winner, endTick, finalHash, accepted, error, unfinished, breaches, loadouts, pendingLogBase64 }
├── result      outcomes[2]: { won, rrDelta, rrAfter, arenaAfter, promoted, demoted }
├── connection  joinCode, confirmedUnixSeconds[2], connectedUnixSeconds
└── decisions   forfeitedBy, abandonedBy, pendingTimeoutVoid, disciplineApplied, bothLeft
```

## Read the field comments. They're a lesson in schema design

- **When it's set and what null means:** "Null for Open/Pending/Void/Disputed records." "0 means not confirmed."
- **Backward compatibility:** "Older records may have null here, treated as all-zero." Old JSON must still load.
- **Decision fields are committed before side effects:** `forfeitedBy` is "committed under the
  record's write lock before any player write… so every settlement pass settles the same winner."
- **Durable flags as completion guards:** `disciplineApplied` makes the discipline step safe to retry ([[Idempotency]]).
- **Outcomes are "never re-derived from current state"**: they're what this match produced, kept for history.

`MatchRecords.Create` builds a fresh record with **deep-copied** snapshots (`new List<string>(...)`) so later edits to player state can't leak into them.

## Check yourself

> [!question]- Why store `outcomes` at all, when ratings are already updated on the player records?
> History and the end card need "+18 RR, promoted" *for this match*. Player records only hold
> the current totals, and re-deriving per-match deltas later would be impossible.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
