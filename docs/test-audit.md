---
type: Test Audit
title: Unit test coverage audit
description: Risk-ranked inventory review and proposals for packet quality and hot-path budgets at a1d12190.
tags: [testing, determinism, networking, performance]
runtime: [dotnet-test, unity-editmode]
executor:
  resource: docs/skills/run-dotnet-tests.md
  receipt: [commit, projects, tests_passed]
generated: { by: codex, at: 2026-10-02T00:00:00Z }
status: draft
sources:
  - { id: solution, resource: dotnet/NodeWar.sln, title: Six test projects }
  - { id: contract, resource: docs/simulation-rules.md, title: Simulation determinism contract }
  - { id: baseline, resource: docs/computations/determinism-baseline.md, title: Pinned fingerprints }
  - { id: input, resource: Assets/Scripts/Game/Network/InputSerializer.cs, title: Input and control packets }
  - { id: draft, resource: Assets/Scripts/Game/Network/DraftSerializer.cs, title: Draft packets }
  - { id: copy-tests, resource: Assets/Tests/EditMode/Tests/SimulationStateCopyTests.cs, title: Copy completeness and rollback }
  - { id: workflow, resource: .github/workflows/determinism.yml, title: Cross-platform gate }
---

# Unit test coverage audit

Scope: `chore/test-audit` at `a1d12190`. This is an inventory-led audit with selective body and implementation inspection, not exhaustive branch coverage. The mechanically generated, untracked `test-inventory.txt` is working evidence and is intentionally excluded from commits. Zero class-name references are candidates, not proof of missing coverage. The supplied baseline is about 1,300 passing cases; the runner document's 1,118 is historical, not a current count.

No production changes, scene/prefab edits, workflow edits, baseline updates or verification stamps are part of this audit. The main checkout and its uncommitted network changes were not inspected. Those changes are considered only from the supplied brief.

## 1. Concept map

Depth describes the evidence for the named concept, not overall completeness. Paths below are relative to the project directory unless stated otherwise.

### NodeWar.Simulation.Tests

Sources are linked from `Assets/Tests/EditMode/Tests/`, not duplicated in dotnet.

| Concept and invariant | Test files | Depth |
|---|---|---|
| Cross-platform fingerprints and simulation-version pinning | DeterminismBaselineTests.cs | Adequate: two pinned scenarios, small boards |
| Every balance field influences identity, ordering matters | BalanceHasherTests.cs | Thorough: reflection-based field checks |
| State snapshots preserve every field, isolate mutable arrays, and replay identically | SimulationStateCopyTests.cs | Thorough for copy registration; adequate for rollback scenarios |
| Movement progress, reversal, interruption and corrupt-path recovery | MovementCorrectnessTests.cs, EdgeWeightTests.cs | Adequate |
| Path costs, ownership weighting and deterministic equal-cost ties | PathfindingTests.cs, MatchFactoryTests.cs | Adequate; mutable global configuration explicitly exposed |
| Starting board, canonical edges, player setup and villager identity | MatchFactoryTests.cs | Adequate |
| Claim rates, caps, neutralisation, ownership and simultaneous-win precedence | ClaimingAndWinTests.cs, SimulationFixTests.cs | Adequate |
| Movement/combat happen before claiming in the same tick | ClaimingAndWinTests.cs | Thin: two phase relationships |
| Production costs and timers, market alternation and shortages | ProductionTests.cs, EraTests.cs | Adequate |
| Combat target ordering, healing intervals, rampart removal and arrival worker caps | SimulationFixTests.cs | Adequate |
| Paid/unpaid respawns and consumed villagers | RespawnTimerTests.cs, SimulationFixTests.cs, CommandRefusalTests.cs | Adequate |
| Commands reject hostile ownership, invalid indices and unavailable actions without effects | CommandRefusalTests.cs, SimulationFixTests.cs | Adequate |
| Era lookup/fallback, draft upgrades and era-sensitive hashes | EraTests.cs | Adequate; not a complete state-field hash guard |
| Events report transitions once and leave simulation hashes unchanged | TickEventTests.cs | Adequate |
| Minimal tick execution | SimulationSmokeTest.cs | Thin |

### NodeWar.Lobby.Tests

| Concept and invariant | Test files | Depth |
|---|---|---|
| Loadout normalisation, wire layout, UTF-8, eras/skins and legacy optional sections | LoadoutWireTests.cs, LoadoutEraTests.cs, DraftLoadoutPacketTests.cs | Thorough for loadouts; thin for other draft packets |
| Versioned handshake identity, endian layout and comparison precedence | HandshakePacketTests.cs | Adequate; receiving lifecycle is outside this suite |
| Emote packet field/length refusal | EmotePacketTests.cs | Adequate |
| Display eligibility agrees with simulation command acceptance/costs | CommandEligibilityTests.cs | Thorough at the UI-rule seam |
| Loadout editing, ownership eligibility, era chips and cosmetic choices | LoadoutEditorTests.cs, EraChipsTests.cs, ItemFamilyTests.cs, ItemTintTests.cs | Adequate |
| Settings migration preserves user choices and clamps malformed values | GameSettingsTests.cs | Thorough |
| Trophy-window movement, rank boundaries and history-row presentation | TrophyBarLogicTests.cs, RankDisplayTests.cs, MatchHistoryRowTests.cs | Adequate |
| Ranked queue cancellation, stale async replies, retries, bot races and block expiry | RankedQueuePresenterTests.cs | Thorough |
| Host/guest rendezvous, timeout, failure and cancellation | RankedRendezvousTests.cs | Adequate |
| Disconnect hold, claim/resume races, heartbeat cadence and server result display | DisconnectHoldTests.cs, PresenceHeartbeatTests.cs, RankedResultTrackerTests.cs | Thorough for pure presenters; no transport proof |

TickInput serialization itself has no dedicated fixture here at the audited base. InputSerializer references in handshake/emote tests do not cover its tick layout. Adding a test class also requires an explicit Compile item because default compile globs are disabled.

### NodeWar.View.Tests

| Concept and invariant | Test files | Depth |
|---|---|---|
| Camera side, rotation, perspective and deterministic sprite-depth ties | ViewSideTests.cs | Thorough for pure maths |
| Indicator hysteresis, behind-camera projection, clamping and priority/cap merging | IndicatorPlacementTests.cs | Adequate |
| Opponent route reveal windows and own-route comparisons | RouteRevealTests.cs | Adequate |
| Emote windows and refusal cooldown | EmoteRateLimiterTests.cs | Adequate |
| Resource-ring bounds, interpolation, spend ghosts and easing | ResourceRingMathTests.cs | Thorough for numerical rules |
| Production jobs, affordability, owner filtering and stable nearest-job ordering | ResourceProductionTests.cs | Thorough |
| Draft proxy/ghost continuity and monotonic handover | DraftHandoverTests.cs | Adequate |

The inventory's `Assets/Tests/EditMode/Outline/` fixtures cover allocator, registry, bounds, style priority and thickness. They are separate Unity assembly coverage: neither the simulation project's `Tests/**/*.cs` glob nor View.Tests' explicit list includes them. Do not include their cases in six-project dotnet totals.

### NodeWar.MatchLog.Tests

| Concept and invariant | Test files | Depth |
|---|---|---|
| Framing, endian/command layout, unknown chunks, malformed counts/enums and arbitrary bytes | MatchLogFormatTests.cs | Thorough |
| Era/skin optional chunks and backwards compatibility | MatchLogErasTests.cs | Adequate |
| Recording copies commands, preserves input order and retains first terminal/desync result | MatchRecorderTests.cs | Adequate |
| Replay reproduces commands/checkpoints, refuses tampering and invalid final results | MatchReplayTests.cs, ReplayTests.cs | Adequate |

### NodeWar.Progression.Tests

| Concept and invariant | Test files | Depth |
|---|---|---|
| Glicko reference calculation, uncertainty, inactivity and input refusal | Glicko2Tests.cs | Thorough |
| RR clamping/rounding and inclusive arena promotion/demotion | RankPointsTests.cs, ArenasTests.cs | Adequate |
| Two-player settlement symmetry, snapshots, decay and configuration refusal | MatchSettlementTests.cs | Thorough |
| Catalog identity, variant uniqueness and retirement/migration stability | CatalogTests.cs | Thorough |
| Era grants, demotion ownership and ordinal identity comparison | EraUnlocksTests.cs | Adequate |
| Disconnect penalty decay, strike escalation and non-report windows | DisconnectPenaltyTests.cs | Adequate |

### NodeWarCloud.Tests

| Concept and invariant | Test files | Depth |
|---|---|---|
| Account flow, caller identity, remembered identity and player JSON/default records | AccountTests.cs, RememberGuardTests.cs, PlayerStateTests.cs, MatchRendezvousTests.cs | Adequate |
| Catalog identity and embedded/disk agreement, rank-table agreement | CatalogIdsTests.cs, CatalogTests.cs, RankTableTests.cs | Adequate |
| Inventory grants/equipment/clamps and conditional-write races | InventoryTests.cs, InventoryClampTests.cs, DisciplineStorageTests.cs | Thorough |
| Allocation versions, eligibility, two-player claims, lease recovery and storage failure | MatchAllocationTests.cs, MatchEligibilityTests.cs, MatchRecordTests.cs | Thorough |
| Detached match-store writes, optimistic conflicts and per-player log storage | MatchRecordStoreTests.cs | Adequate for in-memory contract |
| Rendezvous confirmation, leave/forfeit races and committed-decision recovery | MatchRendezvousTests.cs | Thorough |
| Server hold/presence time boundaries, concurrent claims and read-only result polling | MatchHoldTests.cs | Thorough |
| Report agreement/dispute, partial settlement recovery, idempotency and claim ownership | MatchReportingTests.cs | Thorough |
| Discipline once-only writes, conflicts, retries and claim-release ordering | MatchDisciplineTests.cs, DisciplineStorageTests.cs | Thorough |
| Upload transport caps, referee tamper refusal, multi-balance concurrency and long replay | MatchLogUploadTests.cs, RefereeTests.cs | Thorough correctness; thin efficiency regression budgets |
| History privacy/order and local service/queue fake contracts | MatchHistoryTests.cs, LocalMatchReportServiceTests.cs, RankedQueueServiceTests.cs | Adequate |

## 2. Duplication

One confirmed same-layer redundant case: `ClaimingAndWinTests.Claim_ReachingTheThresholdFlipsTheOwnerAndPublishesTheClampedBar` asserts player-0 ownership and the clamped bar after a one-point-short claim. `SimulationFixTests.Claim_ReachingThresholdPreservesClampedBarAndUpgrade(0, false)` has the same board/action/assertions and additionally checks idle state and persistence on a later tick. Keep the parameterized SimulationFix test (both players and upgrades); remove the smaller ClaimingAndWin case in a later cleanup. No removals are part of this audit.

Do not remove the determinism companions merely because their setup matches a correctness test: they compare two executions rather than expected gameplay. The two threshold determinism tests also differ (ten-tick peer comparisons versus a one-tick parameterized hash). Likewise, `CommandEligibilityTests` versus command refusal tests exercise different layers; progression settlement versus cloud reporting exercise pure maths versus persisted settlement. `ReplayTests` records a command stream and validates checkpoints, so its resemblance to `MatchReplayTests.HonestLog_Replays` is not enough to declare it redundant. The smoke test has a distinct degenerate one-node fixture; low assertion depth is not proof of duplication. No other redundancy was confirmed by the selective inspection.

## 3. Gaps, ranked by risk

| Rank | Untested behaviour / current coverage limit | Why it matters | One-line test idea |
|---|---|---|---|
| 1: critical | Lockstep timing, resend, speculative confirmation/rollback and recovery have zero automated transport scenarios | Missing or misordered inputs can stall or diverge a match despite a deterministic tick | Drive two pure lockstep cores with injected time through scripted lossy links; compare confirmed hashes every tick |
| 2: critical | No reflection-driven registration guard for every mutable state field in SimulationStateHasher; balance has one, CopyFrom has one | An omitted field hides divergence from peers/referee | Mutate each registered mutable field/array element independently and require a changed hash, with an explicit reviewed static-field exclusion list |
| 3: high | Only movement-before-claiming and combat-before-claiming have explicit tick-order fixtures | Phase changes can alter same-tick resources, deaths, healing, respawns and wins while repeatability stays green | Build minimal distinguishing states for claiming→production, production→healing, healing→respawn and respawn→win, plus rampart/resume placement |
| 4: high | TickInput has no direct field/layout/property fixture; parser validates count/length but skips type validation and accepts all tick/command enum values; no application count/byte cap | Malformed traffic can reach receiver filtering/command processing or cause excessive work | Pin every field's byte offset, test count overflow/refusal, then receiver-test tick windows, enum refusal and bounded command queues |
| 5: high | CopyFrom completeness is covered, but rollback orchestration only has a short missing-move replay | Wrong snapshot selection, repeated rollback or speculative terminal effects can corrupt the live match | Replay delayed commands across production/death/respawn and a speculative win, assert confirmed hashes and exactly-once external effects |
| 6: high | DraftReady/Placement expose throwing Deserialize APIs; DraftAck lacks a fixture; complete handshake refusal lifecycle absent | A malformed or lost control packet can corrupt/stall draft or start incompatible peers | Test checked receiver entry points for strict fixed sizes, player/district/grid/flags bounds and retries; reject before simulation startup |
| 7: high | Seeded long command-stream determinism under alternate delivery and tie-heavy full-board combat is absent | Small pinned scenarios miss rare interaction/order bugs and global configuration leaks | Replay a fixed-seed valid command stream on independently built boards with equal-priority fighters, retaining first-divergent tick |
| 8: medium-high | Server tests strongly cover logical conflicts, but CloudSaveMatchRecordStore's real SDK serialization/conditional-write contract and mixed-endpoint crash schedules lack direct evidence | Fakes can miss write-lock/partial-failure semantics, undermining settlement integrity | Contract-test an SDK seam with missing/stale locks and faults at every write, then interleave Report/Leave/ResolveHold retries |
| 9: medium | Client disk-backed LocalMatchLogStore/PendingRankedReports and UGS adapter lifecycle are not covered by the six-project inventory | Offline/crashed clients may lose or duplicate a report despite correct server idempotency | Use a temporary store and fake SDK to crash/reload/retry, swap accounts and preserve only the intended pending report |
| 10: medium | No automated hot-path allocation/time ceilings | Rollback replay happens inside one frame; frame stalls and cloud replay costs can grow unnoticed | Warm each operation, measure allocation deltas and enforce loose categorized wall-time ceilings |
| 11: lower | Lobby/UI pure maths are well tested; actual navigation, gesture arbitration, camera projection and scene/presenter wiring are outside dotnet | Correct maths cannot prove the live scene calls it correctly or UI never writes replicated state | Add targeted Unity integration/manual checks for draft→match→result, overlays, scene toggles and command enqueueing |

Rank 1's smallest useful extraction is a Unity-free `LockstepCore` outside Simulation/, retaining input bookkeeping, tick selection, speculation, snapshot/replay, resend policy and hash comparison. Inject monotonic time through `Advance(now, receivedPackets)` and return outgoing packets plus confirmed/event decisions. Keep MonoBehaviour transport polling/flushing and view callbacks as the adapter. Do not extract gameplay rules or add clocks to Simulation/. Connect two cores to an in-memory link with loss, duplication, reordering, delay and independent directional drop controls.

The supplied live changes are untested here: speculation only beyond a silent peer, 1.5x backlog catch-up, current input plus two predecessors, forced resend on return from silence, per-frame transport flush and NETSTAT stats. Each needs a named scenario/adapter contract in that design; this branch cannot certify them. LockstepRunner is not directly headless-testable as written because it mixes MonoBehaviour, Unity time, transport and simulation driving. Extraction and adapter checks are proposals only.

## 4. Packet quality testing (proposal)

Build bounded, seeded round-trip generators for every PacketType: TickInput, Handshake/Ack/Reject, Heartbeat, DraftReady, DraftPlacement, DraftLoadout, DraftAck and Emote. Assert every transmitted field and exact byte length, independent endian/layout vectors, null/empty command equivalence, all defined command types, signed integer boundaries and Unicode IDs. Keep seeds and first failing bytes in diagnostics. Property round-tripping must not silently normalize an invalid field unless the contract explicitly permits it.

For checked parsing APIs, enumerate every strict prefix and append trailing bytes; exercise zero-length, null, oversized buffers and seeded random/structured garbage (valid headers with mutated fields). Legacy loadout section boundaries are deliberately valid: assert the exact accepted boundaries, not that every prefix fails. DraftReady/Placement currently require a checked receive seam before arbitrary garbage can promise no exceptions.

Separate framing from semantic receive policy. Test command-count -1/0/maximum/cap+1/int.MaxValue before allocation; packet sizes just under/at/over a documented transport budget; forTick at old/current/ahead limits and their adjacent values; player/villager/node and enum ranges; unknown ack flag bits; era and grid limits. Current TickInput deserialization rejects structurally impossible counts, but does not reject enum/tick/tag values or enforce a fixed command cap. Record these as production-validation gaps rather than adding false passing rejection assertions. Rejection of version/content mismatches belongs to handshake/connection setup, since TickInput itself has no version field. Verify no rejected identity reaches draft or starts ticks.

Pin size formulas: TickInput is `13 + 24*C` bytes, handshake variants 9, heartbeat 1, DraftReady 5, DraftPlacement 18, DraftAck 6, Emote 5. Establish an application UDP payload budget against the configured transport/relay, then derive the command cap and measure DraftLoadout worst-case IDs/skins. A provisional 1,200-byte payload admits 49 commands (1,189 bytes); it is a proposal, not an existing limit. Account for the resend policy's three separate packets per input and aggregate bandwidth, not only individual packet sizes.

Lossy-link scenarios should reproduce the F8/F9/F10-style manual drop controls as semantic drop schedules without depending on Unity keys: isolated loss, burst loss/silence and sustained loss followed by recovery, each in both directions and in each single direction. Verify the actual key mapping when porting live controls. Add duplicate bursts, reversed deliveries, long delayed packets crossing rollback windows, asymmetric frame rates and reconnect at speculative game-over. While disconnected, assert bounded buffers/speculation and no false confirmed win; after a finite outage, require both peers to reach the same confirmed target tick with identical hashes at every checkpoint.

Use fixed injected-time steps and explicit deadlines: for example recover a 20-tick outage within 100 additional ticks after link restoration, then calibrate against the chosen window/cadence. Count total transmissions and duplicate deliveries independently of hash equality. Set a scenario-specific bound from three packets per new input plus the configured resend cadence/window and a small allowance; do not invent a universal duplicate cap that masks a resend storm. Permanent loss tests should assert safe hold, not impossible convergence. Pin 1.5x catch-up accumulator behaviour, forced resend on silence exit, and adapter flush once per frame; stats must reflect observed unique/duplicate counts.

## 5. Efficiency testing (proposal)

Use `[Category("Performance")]`, no parallel execution (simulation configuration is global), fixed workloads and explicit warm-up. Prefer `GC.GetAllocatedBytesForCurrentThread` deltas: put construction, delegate creation, assertions and logging outside the measured span; it counts managed allocation on that thread, not retained memory or worker-thread allocations. Fixed-work allocation ceilings provide a stable regression signal; do not demand zero from paths that deliberately clone or allocate combat scratch arrays today. Add a separate zero-allocation ComputeHash assertion after warming it.

Cover a fully populated default late-game board with workers, active contested combat, movement paths, healing and respawn timers; count villagers/nodes explicitly. Measure (a) SimulateTick over a fixed N, (b) CopyFrom plus exactly 20 ticks with replay inputs where appropriate, and (c) repeated ComputeHash on a fixed state. Assert the workload really advances, stays nonterminal and exercises intended activity so a cheap inert board cannot satisfy the budget. Snapshot copying/replay is particularly important because all 20 ticks can execute in a single rollback frame. The proof of concept should use generous allocation ceilings and seconds-scale time ceilings, recording local measurements rather than claiming a production frame SLA.

Next add serializer allocation per packet/command and worst-case size, and the server's cached-balance referee replay plus settlement/store call counts. RefereeTests already includes a 12,000-tick spike scenario and concurrent multi-balance replay; that is correctness/load evidence, not a stable regression ceiling. For async server work, current-thread allocation is incomplete: use a synchronous replay seam or report allocations separately, and prefer bounded operation/store counts for settlement.

CI proposal only: retain `.github/workflows/determinism.yml`'s OS matrix and pinned receipts. Split normal tests with `--filter 'TestCategory!=Performance'`, then run a Release performance step with `--filter 'TestCategory=Performance'` and separate artifacts. Allocation gates can run on both OS legs; run wall-time trend measurements on a scheduled stable runner, with very generous hard ceilings if included on shared CI. Keep the simulation-only receipt command unchanged or ensure it still includes all sanctioned baseline cases. Do not write multiple project receipts to one logger path. The POC category is included by today's unfiltered solution command until this proposed split is adopted.

## 6. Recommended next steps

1. Add the packet robustness POC in Lobby.Tests, including its explicit Compile entry (small: half a day).
2. Add populated-board tick/copy-replay/hash allocation and loose-time POCs in the existing simulation test directory (small: half a day).
3. Add mutable-state hasher reflection registration and remaining phase-order fixtures (medium: 1–2 days).
4. Extract the smallest pure lockstep core and create the lossy-link harness, preserving current behaviour first (large: 3–5 days).
5. Port silence, catch-up, resend and rollback-terminal scenarios; enforce recovery/duplicate budgets and adapter flush checks (medium: 2–3 days).
6. Specify and implement receive semantic bounds/packet budgets with coordinated protocol review, then extend property tests to all packets (medium: 1–2 days).
7. Add server SDK-store contract/crash-schedule tests and client pending-report persistence tests (medium: 2–3 days).
8. Expand efficiency coverage and propose the CI category split; collect stable-runner baselines before tightening timing (medium: 1–2 days).
9. Remove the single confirmed redundant claim case and perform targeted Unity wiring checks (small: half a day plus manual session).

Estimates are engineering time, excluding review and hosted-service availability. The report precedes the POCs intentionally; execution counts and measured performance belong to their test-run results and commit descriptions, not a fabricated verification stamp.
