---
shell: 3
up: "[[Ranked matchmaking]]"
tags: [shell/3, realm/backend, system/cloud, script]
file: dotnet/NodeWarCloud/NodeWarCloud/MatchRendezvous.cs
sources:
  - dotnet/NodeWarCloud/NodeWarCloud/MatchRendezvous.cs
verified: 56ab2583
---

# MatchRendezvous

> [!question] The question
> Two matched players need to swap a Relay join code privately, confirm they're connected, and be able to leave cleanly. How does one record handle all three?

**Where you are:** [[Home]] › [[Backend]] › [[Cloud]] › [[Ranked matchmaking]] › **MatchRendezvous** (script)

**Open:** [MatchRendezvous.cs in VS Code](vscode://file/C:/Dev/NodeGame/dotnet/NodeWarCloud/NodeWarCloud/MatchRendezvous.cs)

## What it is

Server rules for the connect and leave phase: "No match rules live in the network layer or the client; this is the one place that decides them."

| Method | Does |
|---|---|
| `Rendezvous(matchId, caller, joinCode)` | Slot 0 may publish its code while Open and unconnected. Everyone gets the roster, their slot, the code, the connected flag and the map |
| `ConfirmConnected(matchId, caller, now)` | Records each slot's first confirmation. When both have confirmed, `connectedUnixSeconds` is set |
| `Leave(matchId, caller, forfeit, now)` | Before the match started: void. After: requires an explicit forfeit, which is committed under the lock, then settled via [[MatchSettler]] |

Every method is a read → decide → conditional write loop ([[Write locks]]).

## Details worth noticing

- **Ignore, don't refuse:** a code from slot 1, or a late code from slot 0, is "silently ignored
  rather than refused". The caller still gets the current state.
- **Input limits:** join code ≤32 characters, so stored values can't be abused.
- **Repeated confirmations change nothing**, so the operation is idempotent.
- **`confirmedUnixSeconds ??= new long[2]`** handles older records that stored null (schema evolution in one line).

## Check yourself

> [!question]- Why does the *server* decide whether leaving is a void or a forfeit, rather than the client?
> The client could always claim "it hadn't started yet". Only the record's server-time
> confirmations and accepted reports prove whether the match was played.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
