---
shell: 3
up: "[[Referee]]"
tags: [shell/3, realm/backend, system/match-log, script]
file: dotnet/NodeWarCloud/NodeWarCloud/Referee.cs
sources:
  - dotnet/NodeWarCloud/NodeWarCloud/Referee.cs
verified: ce9fb6bf
---

# Referee (script)

> [!question] The question
> What does the server wrap around [[MatchReplay]] before trusting it with a real match?

**Where you are:** [[Home]] › [[Backend]] › [[Match log]] › [[Referee]] › **Referee.cs** (script)

**Open:** [Referee.cs in VS Code](vscode://file/C:/Dev/NodeGame/dotnet/NodeWarCloud/NodeWarCloud/Referee.cs)

## What it is

114 lines: `Verify(byte[]) → RefereeVerdict`. "Checks log consistency, not command authenticity or eligibility for rating."

## Read `Verify`

1. **Size cap** of 512 KB, before parsing anything.
2. **Parse:** `MatchLogFormat.TryRead`.
3. **Known balance:** `balances.TryGet(log.header.content)`. The server must hold the exact balance data the match used ([[Build identity handshake]]).
4. **`CheckMap`** (current simulation only): the map ID must be one the server ships, and *both* the
   recorded board and the setup's hash must equal the server's own board. "The hash a client writes is
   never the authority: an attacker can recompute it for a board of their own."
5. **`lock (replayLock) MatchReplay.Run(...)`**: a process-wide lock, because `MatchFactory.Configure`
   mutates static simulation settings. Two replays at once would corrupt each other.
6. **`catch … when (ArgumentException or InvalidOperationException or IndexOutOfRange or Arithmetic)`**:
   "Syntactically readable input can still be unsuitable for the sim." Bad logs become a verdict, not a crash.
7. **`finally`** records `elapsedMs`, including the time spent waiting behind another replay.

## Design lessons

- **Defence in depth:** size, parse, catalog, replay, exception filter. Each layer catches what the previous one can't.
- **A lock as a known bottleneck:** throughput is one replay at a time per process. It's documented
  as a pressure point rather than hidden.

## Check yourself

> [!question]- Why catch only specific exception types instead of `catch (Exception)`?
> Those types mean "this input broke the simulation". Anything else (out of memory, a bug in
> the server) should surface as a real error rather than be disguised as "invalid match log".

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
