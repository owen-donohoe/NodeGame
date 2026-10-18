---
shell: 3
up:
  - "[[Tick packets]]"
  - "[[Input delay]]"
  - "[[Rollback]]"
  - "[[Desync detection]]"
concepts:
  - "[[Fixed timestep]]"
tags: [shell/3, realm/match, system/netcode, script]
file: Assets/Scripts/Game/Network/LockstepCore.cs
sources:
  - Assets/Scripts/Game/Network/LockstepCore.cs
verified: ce9fb6bf
---

# LockstepCore

> [!question] The question
> The heart of online play is 1,226 lines. How is it organised, and where do you look for each behaviour?

**Where you are:** [[Home]] › [[Match]] › [[Netcode]] › **LockstepCore** (script)

**Open:** [LockstepCore.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Network/LockstepCore.cs)

## What it is

The online tick driver. It implements `ITickProvider` (like [[TickRunner]]) and `IEmoteChannel`.
It generates and sends local inputs, receives the peer's, runs ticks when both are present,
speculates when the peer goes quiet, adapts the input delay, and exchanges hashes.
**No UnityEngine**: `GameManager` passes in the clocks every frame, and logging goes to a sink.

> [!info] Navigate by the section banners (`// ===== … =====`)
> | Section | Mechanism note |
> |---|---|
> | Constants (lines ~37–110), each with its reason | all of them |
> | `Initialize`, `Unpause`, `Update`, `Flush`, `EndMatch` | the frame loop |
> | `SPECULATION` | [[Rollback]] |
> | `ADAPTIVE INPUT DELAY` | [[Input delay]] |
> | `INPUT GENERATION` | [[Tick packets]] (the 7 copies) |
> | `TICK EXECUTION` | [[Command pipeline]] (P0 then P1) |
> | `NETWORK RECEIVE` | [[Never trust the wire]] (range checks, `FromPeer`) |
> | `DESYNC` | [[Desync detection]] |
> | `RESEND / HEARTBEAT` | stalls and holds |
> | `NET STATS` | diagnostics (`[NETSTAT]` log lines) |

## Read it in this order

### 1. The constants block

Read every comment. Each constant explains its *reason* and often its *measurement*
(`REDUNDANT_INPUTS = 6` cites the sweep data). This block alone teaches the netcode.

### 2. `Update`: one frame

```
pump packets → (paused? heartbeat, return)
→ holding? (both inputs back? resume : resend, return)
→ speculating? (span confirmed? ReplaySpan : resend)
→ catch-up? accumulator += dt × 1.5 : × 1
→ while accumulator ≥ tick:
      both inputs → Live tick
      else can speculate → Speculative tick
      else → stall: resend, cap accumulator, break
→ window exhausted? abandon + hold
→ no tick for 2 s? hold
```

It's a [[Fixed timestep]] loop with three ways to spend each tick: live, speculative or stall.

### 3. `ExecuteTick(tick, mode)`

Sorts commands into P0 and P1, raises `CommandsApplied` (confirmed only), applies P0 then P1
through `CommandProcessor`, calls `SimulateTick`, then on confirmed ticks hashes every 50,
compares the peer's hash, and cleans up. `TickMode` (Live / Speculative / Replay) decides
which side effects happen. **This one switch is how "never record or hash a guess" is enforced.**

### 4. `ProcessIncomingPackets`

The defensive reader: malformed means drop, an implausible `forTick` means drop, a duplicate is ignored.
It's the [[Treat bad input as a case you already handle]] pattern in action.

## C# details worth noticing

- `Dictionary<int, TickInput> localInputs/remoteInputs`: allowed, because it's *looked up*
  by tick, never *iterated* into the simulation (`.claude/rules/simulation.md`).
- `internal const REDUNDANT_INPUTS`: the dotnet test projects *link the Unity source files
  directly* (`<Compile Include="../../Assets/…" Link=…>` in the `.csproj`), so the code compiles
  into the test assembly itself, and `internal` members are visible to the tests. This file-linking
  is also how [[Same code, three places]] works for the rig and the referee.

## Tests

`dotnet/NodeWar.Network.Tests`: `HarnessPeer` and `SimLink` run two cores over a simulated
link with loss, jitter and outages, and check that confirmed hashes match a perfect-link run.
`SweepTests` tune the constants.

## Check yourself

> [!question]- Where would you look to find out why the game paused for 2 seconds in a real match?
> The `[LOCKSTEP]` warnings ("No tick for 2s… holding", "Played 20 ticks without the opponent…")
> and the periodic `[NETSTAT]` line (stalls, resends, holds, reconnects).

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
