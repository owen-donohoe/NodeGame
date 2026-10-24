---
system: match-log
summary: Records confirmed matches as versioned inputs and replays them headlessly for verification and future playback.
sources:
  - Assets/Scripts/MatchLog/
  - Assets/Scripts/Backend/LocalMatchLogStore.cs
  - dotnet/NodeWar.MatchLog/
  - dotnet/NodeWarCloud/NodeWarCloud/Referee.cs
  - dotnet/NodeWarCloud/NodeWarCloud/RefereeModule.cs
  - dotnet/NodeWarCloud/NodeWarCloud/BalanceCatalog.cs
verified: a1da5182
---

# Match log

## Purpose
One input log serves local replay data, server result verification and headless analysis. It records the match actually confirmed, allowing a referee to rebuild it without trusting a client's claimed outcome.

## Interface
UnityEngine-free `NodeWar.MatchLog` lives in `Assets/Scripts/MatchLog/` and is compiled into the dotnet library. `MatchLogFormat.Write(MatchLog)` returns bytes; `TryRead(byte[], out MatchLog, out string error)` parses without requiring gameplay playback.

`MatchLog` contains `header`, `setup`, `board`, two `loadouts`, ordered `draft`, sparse `ticks`, `hashes`, and optional `result`. `MatchLogHeader` contains `protocol`, `sim`, `content`, `matchId`, two ordered `playerIds`, `localPlayer`, `tier`, `seed`, `startUnixSeconds`, and `kind` (Local/Bot/Networked). Ranked IDs and slot order come from the server record; local logs use local IDs. Start time is client time, not authoritative server time.

Each `PlayerLoadout` has resolved `suits`, chosen `districts`, type-indexed `suitEras`/`districtEras`, and cosmetic `skins`. `LoggedTick` is `{ tick, GameCommand[] commands }` before simulation; `HashCheckpoint` is `{ tick, hash }` after simulation. `MatchResult` contains `reason` (Win/Disconnect/Abandoned/Surrender), `winner`, `endTick`, `finalHash`, `firstDesyncTick`.

`MatchRecorder(header, setup, board, loadouts, draft)` accepts `RecordTick(tickBefore, applied)`, `RecordHash(tickAfter, hash)`, `RecordDesync(tick)` and `Finish(MatchResult)`; `ToBytes()` serializes its `Log`. `GameManager` subscribes tick-driver events for drafted matches; Testing mode does not record. Only confirmed ticks reach the recorder.

`MatchReplay.Run(MatchLog, GameBalanceData)` returns `ReplayOutcome`: `ok`, `error`, `firstMismatchTick`, `endTick`, `gameOver`, `winner`, `finalHash`, and per-Core `breaches`. It rebuilds through `MatchFactory`, applies recorded commands and simulates up to the recorded end; `MaxTicks` is 200000.

`Referee.Verify(byte[])` returns `RefereeVerdict`, adding replay timing/counts to equivalent outcome fields. `RefereeModule.VerifyMatch(context, logBase64)` exposes the Cloud Code `VerifyMatch` function. Logs are limited to 512 KiB; balance data must exist in `BalanceCatalog` under the header content hash. This verifies consistency, not player membership or ranked settlement eligibility.

`LocalMatchLogStore.Save(matchId, bytes)` writes `.nwml` under `Application.persistentDataPath/MatchLogs`, retaining the newest twenty. Pending ranked uploads use a separate store so retention cannot delete unsent reports.

## Connects to
- `simulation`: `MatchFactory`, `CommandProcessor`, `SimulateTick` and `SimulationStateHasher` rebuild and verify play.
- `commands`: serialized `GameCommand` layouts are part of TICKS compatibility.
- `board`: `MatchSetup`, `BoardConfigData` and server `IBoardCatalog` establish the actual board.
- `draft`: placements and both resolved loadouts define tick-zero state.
- `match-flow`: constructs headers, subscribes recorder events and finishes logs.
- `netcode`: confirmed input/hash events exclude speculation and preserve P0-before-P1 order.
- `backend-client`: `PendingRankedReports` uploads finished ranked logs independently of local retention.
- `cloud`: `ReportMatch` adds roster, snapshot ownership and agreement checks around the referee.
- `progression`: recorded era tables and cosmetic IDs preserve each fielded loadout.
- `tooling`: format/replay tests guard compatibility and confirmed recordings.

## Invariants
Known chunk tags never change meaning. Framing compatibility does not imply simulation compatibility. Preserve historical resolved suit arrays and era tables, including protocol-5 Warrior-plus-three selections. Skins never affect state or hashes. No concurrent replay/configuration in a process while simulation configuration remains static.

## How it works
The file starts with `NWML` and little-endian u16 `FormatVersion` (1). Each chunk is u16 tag, u32 byte length, payload. Unknown tags skip by length; truncation, duplicate known chunks, missing required data and conflicting boards fail parsing.

Tags are HEADER=1, BOARD=2, LOADOUTS=3, DRAFT=4, TICKS=5, HASHES=6, RESULT=7, ERAS=8, SKINS=9, BOARD_V2=10, SETUP=11, BOARD_V3=12. Empty command ticks are omitted. RESULT may be absent in a parsed file, but replay requires it. ERAS and SKINS augment loadouts independently of chunk order. There is no SIGNATURES chunk implementation.

Simulation versions 3–4 write BOARD_V2 plus SETUP; version 5 onward requires BOARD_V3 plus SETUP. BOARD_V2 adds terrain (including DeepLake), slots and both base draft pools to BOARD. BOARD_V3 appends a validated symmetry byte; old board readers retain their layouts. Older logs use BOARD without SETUP. Current referee verification requires a known map ID and compares the recorded board and setup hash with its own catalog board.

Replay checks simulation version, setup consistency, tick/checkpoint ordering, every logged checkpoint and the final hash. A Win must reach simulation game-over with the declared winner. A consistent unfinished log can return breaches without inventing a winner. Referee instances serialize replay through one process-wide lock because factory configuration mutates simulation statics.

## Decisions
- Version stored chunks rather than require logs to share the live packet layout.
- Store board data plus catalog-checked identity rather than trust a client-proposed board hash.
- Replay the full simulation rather than verify checkpoints alone.
- Default incompatible simulation replays to unavailable rather than reinterpret old inputs under new rules.

## Pressure points
Unsigned commands permit fabricated consistent logs. Cross-version simulation DLL dispatch and snapshot conversion are absent; format readability alone cannot preserve playback. The dedicated replay viewer is unbuilt. Static configuration serializes referee throughput. Local retention and server replay retention are separate policies; server cleanup remains deferred.

## Change recipes
- Add payload semantics: allocate a new tag, retain old readers/layouts, test unknown-tag skipping and round trips; change FormatVersion only for framing breaks.
- Change simulation outcomes: bump `SimulationVersion`, preserve supported balance exports and explicitly choose which old simulations remain playable.

## Tests
`NodeWar.MatchLog.Tests` guards framing, malformed/truncated inputs, historical loadouts and replay hashes. `NodeWarCloud.Tests` guards referee refusals, catalog boards and reporting. Simulation baselines guard deterministic behavior. These do not verify a replay scene, deployed Cloud Code limits or command authenticity.
