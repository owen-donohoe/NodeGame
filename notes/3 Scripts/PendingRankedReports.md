---
shell: 3
up: "[[Backend client]]"
concepts:
  - "[[Idempotency]]"
tags: [shell/3, realm/backend, system/backend-client, script]
file: Assets/Scripts/Backend/PendingRankedReports.cs
sources:
  - Assets/Scripts/Backend/PendingRankedReports.cs
verified: ce9fb6bf
---

# PendingRankedReports

> [!question] The question
> You win a ranked match on a train and lose signal before the upload. How does the evidence survive until you're back online?

**Where you are:** [[Home]] › [[Backend]] › [[Backend client]] › **PendingRankedReports** (script)

**Open:** [PendingRankedReports.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Backend/PendingRankedReports.cs)

## What it is

A ~100-line static class: a **durable outbox**. `Add(playerId, matchId, log)` writes
`PendingReports/<playerId>__<matchId>.nwml`. `RetryAsync()` uploads every file belonging to the
signed-in player.

## Read the header comment. It explains every design choice

- **Why it exists:** "an upload lost to one network error must not be lost for good: the player's
  claim would stay held and their next queue would ask them to forfeit a match they may have won."
- **One file per match, named for the player**, so another account on this device never uploads it.
- **Delete on *any* answer**, accepted or refused: "the same bytes would get the same answer again."
  Keep it only when the call itself fails.
- **Separate from `LocalMatchLogStore`**, which keeps only the newest 20 and "would eventually delete an unsent log".

## Code details

- **Coalesced retries:** `if (running == null || running.IsCompleted) running = RetryAllAsync();`
  Concurrent callers share one pass instead of uploading twice.
- **`Array.Sort(files, StringComparer.Ordinal)`**: "so retries never depend on what the file system
  returns." A determinism habit, even outside the simulation.
- **"Never throws":** every failure is logged and the file is kept for the next pass.

## Check yourself

> [!question]- Why is it safe to upload the same log twice (say the app crashed after the server accepted it, but before the file was deleted)?
> The server keeps one accepted report per player and returns "awaiting opponent" or the
> settled state for a repeat. The upload is idempotent from the server's side ([[Make retries safe]]).

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
