---
shell: 3
up: "[[Match log]]"
tags: [shell/3, realm/match, system/match-log, script]
file: Assets/Scripts/MatchLog/MatchRecorder.cs
sources:
  - Assets/Scripts/MatchLog/MatchRecorder.cs
verified: ce9fb6bf
---

# MatchRecorder

> [!question] The question
> What actually writes down a match as it's played? It's 80 lines. Why so few?

**Where you are:** [[Home]] › [[Backend]] › [[Match log]] › **MatchRecorder** (script)

**Open:** [MatchRecorder.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/MatchLog/MatchRecorder.cs)

## What it is

A small class that builds a `MatchLog` in memory. The constructor takes everything known
before tick 0 (header, setup, board, loadouts, draft). Then:

| Method | Called from | Does |
|---|---|---|
| `RecordTick(tickBefore, applied)` | the tick driver's `CommandsApplied` (confirmed only) | Appends a *clone* of the commands. Empty ticks are skipped |
| `RecordHash(tickAfter, hash)` | `HashComputed` | Appends a checkpoint |
| `RecordDesync(tick)` | `OnDesync` | Remembers the first desync tick |
| `Finish(result)` | match end | The **first** call wins, and later callbacks can't replace it |
| `ToBytes()` | after Finish | `MatchLogFormat.Write` |

## Read the comments

- *"Captures the order actually applied by the driver. Regrouping commands by player here would
  replay a different match when players interleave."* (Local play mixes both players in buffer order.)
- *"Drivers may reuse their input buffers next tick, so retain a copy."* That's why `.Clone()` is there.
- *"Several lifecycle callbacks may end a match. The first owns the result."* Disconnect, game
  over and shutdown can all fire, and only the first counts.

## Why it's small

It does no logic, just *observes*. The recording rules (confirmed only, P0 before P1) are
enforced by the drivers that raise the events. Keeping the recorder dumb means it can't record
something different from what was played.

## Check yourself

> [!question]- What would go wrong without `.Clone()`?
> The driver reuses or overwrites its arrays. The log would end up holding references that
> later change, recording commands that weren't the ones applied on that tick.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
