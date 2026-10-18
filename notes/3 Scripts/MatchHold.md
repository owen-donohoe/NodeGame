---
shell: 3
up: "[[Disconnect holds]]"
tags: [shell/3, realm/backend, system/cloud, script]
file: dotnet/NodeWarCloud/NodeWarCloud/MatchHold.cs
sources:
  - dotnet/NodeWarCloud/NodeWarCloud/MatchHold.cs
verified: ce9fb6bf
---

# MatchHold

> [!question] The question
> Where does the server turn heartbeats into "your opponent abandoned, you win"?

**Where you are:** [[Home]] › [[Backend]] › [[Cloud]] › [[Disconnect holds]] › **MatchHold** (script)

**Open:** [MatchHold.cs in VS Code](vscode://file/C:/Dev/NodeGame/dotnet/NodeWarCloud/NodeWarCloud/MatchHold.cs)

## What it is

About 210 lines: "Server-measured presence and disconnect holds." Three entry points:
`Presence(matchId, caller, holding, now)`, `ResolveHold(matchId, caller, now)`, and `GetResult(matchId, caller)`.

## Read the constants first

```csharp
ClaimSeconds = 10;            // caller must have held this long
SeenSeconds = 10;             // opponent unseen this long = absent
ConnectionGraceSeconds = 15;  // too early in the match to blame anyone
VoidSeconds = 60;             // both holding this long → void
OpponentHoldSeconds = 50;
NotHoldingSeconds = 30;       // seen but refusing to hold = absent
```

They map directly onto the table in [[Disconnect holds]]. The header explains the cadence:
honest clients heartbeat every 4 s, and every 1 s while holding, "so a self-declared hold cannot prove absence".

## Design lessons

- **Active presence touches only the caller's key** (`presence-N`), so there's no contention with reporting.
- **Decisions are committed on the record** (`abandonedBy`) under its write lock, before any player
  writes, so every later path settles the same winner.
- **Server time only.** Every "how long" is measured with the server's `now`.

## Check yourself

> [!question]- Your opponent's app froze but is still technically connected and sending presence, and it isn't holding. When can you claim?
> After 30 s of them being "seen but not holding", they count as absent. Once your own hold and the connection-age conditions are met, you can claim.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
