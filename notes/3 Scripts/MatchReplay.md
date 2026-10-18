---
shell: 3
up:
  - "[[Referee]]"
  - "[[Match log]]"
tags: [shell/3, realm/backend, system/match-log, script]
file: Assets/Scripts/MatchLog/MatchReplay.cs
sources:
  - Assets/Scripts/MatchLog/MatchReplay.cs
verified: ce9fb6bf
---

# MatchReplay

> [!question] The question
> Given a log file, how do you re-run an entire match without Unity and prove it ends the way it claims?

**Where you are:** [[Home]] › [[Backend]] › [[Match log]] › [[Referee]] › **MatchReplay** (script)

**Open:** [MatchReplay.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/MatchLog/MatchReplay.cs)

## What it is

A 181-line `static class`, "the referee's core and the start of every other headless use of a
log". `Run(log, balance)` returns a `ReplayOutcome` (ok, error, first mismatch tick, end tick,
game over, winner, final hash, breaches).

## Read `Run` top to bottom: refuse early, then replay

1. **Refusals before any work:** missing header, the wrong simulation version ("this build runs N"),
   no result, end tick out of range (`MaxTicks = 200000`, about 5.5 hours, so "a hostile end tick
   cannot keep a server busy"), missing loadouts.
2. **`CheckOrder`:** ticks and hashes strictly increasing and within the match, so the main loop can walk both lists with one index each.
3. **Board and setup validity:** the setup must describe *this* board. (Whether it's a *shipped* map is the [[Referee]]'s question.)
4. **Build tick 0:** `MatchFactory.Configure` + `MatchFactory.Build(balance, board, draft, loadouts)`, the same factory live matches use.
5. **The loop:**
   ```csharp
   while (state.tickCount < endTick) {
       if (state.gameOver) Fail("Game ended early");          // honest logs never run past game over
       if (log.ticks[next].tick == state.tickCount) apply its commands via CommandProcessor;
       GameSimulation.SimulateTick(state);
       if (log.hashes[nextHash].tick == state.tickCount && hash differs) Fail(mismatch);
   }
   ```
6. **The final checks:** the final hash matches, and a claimed Win must really be game over with that winner.

## Notice

- **No event log, no Unity**: `SimulateTick(state)` with no `TickEventLog`. That's the "null means nothing is recorded" rule from [[TickEvents]].
- **The header comment states its limit plainly:** consistency, not honesty. Signed commands close the gap.
- **Static configuration** means "never run two replays at once in one process", which is why [[Referee]] takes a lock.

## Check yourself

> [!question]- A log claims a win at tick 9,000, but the simulation reaches game over at tick 8,700. What happens?
> At tick 8,700 the loop sees `gameOver` while `tickCount < endTick`, and fails with "Game ended at tick 8700 but the log runs to 9000".

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
