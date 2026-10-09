---
type: Skill
title: write-sim-test
description: Procedure for adding test coverage when simulation behaviour changes.
tags: [skill, testing, simulation]
generated: { by: human:DonohoeCUA, at: 2026-08-30T17:15:16-04:00 }
verified:
  # full history: docs/verification-log.md
  - { by: claude-sonnet-5-5, at: 2026-10-09T01:54:09Z }
verified_at_commit: c23a378c216fcc99b426dac0973ce4560144e4d4
status: stable
sources:
  - id: tests
    resource: Assets/Tests/EditMode/Tests/DeterminismBaselineTests.cs
    title: Existing EditMode test patterns
  - id: fixture
    resource: Assets/Tests/EditMode/Tests/TestBoardFactory.cs
    title: TestBoardFactory shared fixtures
  - id: runner
    resource: scripts/run-tests.ps1
    title: EditMode test runner
  - id: match-factory
    resource: Assets/Scripts/Game/Simulation/MatchFactory.cs
    title: Shared drafted board and static configuration
  - id: balance-data
    resource: Assets/Scripts/Game/Simulation/GameBalanceData.cs
    title: Per-era suit and district entries
  - id: sim-state
    resource: Assets/Scripts/Game/Simulation/SimulationState.cs
    title: Player era table defaults
  - id: hasher
    resource: Assets/Scripts/Game/Simulation/SimulationStateHasher.cs
    title: Conditional era hashing
  - id: balance-tests
    resource: Assets/Tests/EditMode/Tests/BalanceHasherTests.cs
    title: Coverage of balance fields
  - id: bank-rules
    resource: Assets/Scripts/Game/Simulation/BankRules.cs
    title: Shared lock, minion eligibility and worker capacity
  - id: bank-tests
    resource: Assets/Tests/EditMode/Tests/BankProductionTests.cs
    title: Bank production, Storehouse and capture lifecycle scenarios
  - id: collection-tests
    resource: Assets/Tests/EditMode/Tests/BankCollectionTests.cs
    title: Progressive collection, lock, raid and ownership lifecycle scenarios
  - id: pier-gate
    resource: Assets/Scripts/Game/Simulation/PierGate.cs
    title: Shared position gate and lowest-ID claim slots
  - id: pier-tests
    resource: Assets/Tests/EditMode/Tests/PierGateTests.cs
    title: Pier transit, retreat, highway, reversal and physical-tick routing
  - id: baseline-contract
    resource: docs/computations/determinism-baseline.md
    title: Pinned fingerprint and version policy
  - id: dotnet-runner
    resource: docs/skills/run-dotnet-tests.md
    title: Editor-free suite and receipt commands
---

# write-sim-test

## When to use
When adding or modifying simulation behavior that needs test coverage.
Invoke explicitly: "use write-sim-test to create a test for this."

## Procedure
Step 0: Inspect the existing test framework and existing test patterns before generating tests

Step 1: Identify what is being tested
- What system or behavior changed?
- What is the expected outcome given known inputs?
- Is this a correctness test, a determinism test, or both?

Step 2: Set up initial state
- Create a minimal SimulationState with only what the test needs
- Reuse TestBoardFactory for small hand-built fixtures (set `terrain` and leave
  `boardHash` at 0; they have no board identity). For a complete drafted match,
  use MatchFactory.Build so the test starts from the same tick-0 board as the
  live game and referee: on BoardFixtures.LandGrid / LandGrid3x3 when the shape
  is incidental, or on PremadeMaps.Hourglass01 when terrain or Pier legality is
  the point. Node IDs on a terrain board are cell order with the gaps closed up,
  so look them up with MatchFactory.CellToNode rather than computing z * cols + x
- Install the balance on both GameSimulation and CommandProcessor before
  running commands or ticks. MatchFactory.Configure also sets Pathfinding's
  board multipliers; Build alone does not configure those statics. Do not
  run fixtures in parallel in one process
- For era-specific behavior, supply the player's era tables and the relevant
  SuitStats / DistrictStats entries. Missing player entries mean era 0;
  balance lookups fall back to era 0. Keep arrays independent between runs
- Use explicit integer values -- no magic numbers without comments
- Initialize v2 player fields deliberately: breachBar and paidRespawns at 0,
  nextBreacherID at -1. Default balance enables the channel, tempo and caps;
  disable those explicitly when testing legacy behaviour
- For banks, initialize bankFood/bankMaterials/bankMetal, minionProductionRemaining
  and storehouseNextResource to 0, and storehouseInitialised to false.
  collectProgress starts at 0 and collectRequested at false. Supply positive
  minionHP/minionMetalCost/bankCapacity tuning; historical zero tuning disables minions.
  Fortress HP and Storehouse productionTicks belong to their per-era DistrictStats.
- Document what the starting state represents
- For recruitment, initialize player recruitCount and node recruitReadyTick to 0,
  and autoRecruit to false. Use explicit positive recruit tuning; historical
  exports with missing tuning must not silently enable free recruits.

Step 3: Define the command sequence
- List the GameCommands in the order they will be applied
- Use real CommandTypes from the actual codebase
- Document why each command is in the sequence

Step 4: Advance ticks
- Run SimulateTick the exact number of ticks needed
- Document what should happen each tick
- Do not over-tick -- test the minimum needed to verify behavior
- For tempo, include the stage boundary tick and a below-100 timer axis;
  assert inclusive integer integration and production remainder carry
- For breaches, distinguish arrival, channel completion and order resume
  (TickOrderResume); test no loss on a threshold drop alone and
  simultaneous-loss cancellation
- For sticky orders, assert targetNodeID survives a fight, that a move with no route is
  refused with the hash unchanged, that an order given while Fighting leaves the attack
  clock alone, and that work or claim begun at resume counts from the next tick
- For claiming, assert against the tick-start owner snapshot: the capture bonus
  (clamped neighbour balance, tempo applied after) and restore must give the
  same result whatever order the nodes are processed in

Step 5: Assert expected state
- Assert specific integer field values on SimulationState
- Assert villager states, node ownership, resource counts 
  as relevant
- One assertion per logical outcome -- do not bundle unrelated 
  assertions
- For caps, assert wasted completions still cycle, Forge spends no material
  at the metal cap, and cap 0 remains uncapped for global resource pools. Storehouse
  alternates bank food/materials every 80 production ticks; bank capacity is five total.
- For paid respawns, assert only successful commands increment paidRespawns
  and the Infirmary (at most two counted workers, lowest villager ID, none under enemy presence) discounts the escalated cost with integer rounding/minimum 1
- For an integration scenario over loss, duplication and reordering with replay and
  rollback, use `dotnet/CoreRulesFixture` (map `hourglass-01-acceptance`, a test-only copy that
  never advertises its hash under the shipped map ID) rather than the shipped board. For banks,
  gates and structure raids use `dotnet/BankRulesFixture` (map `hourglass-01-banks-acceptance`):
  its witness asserts each rule actually fired, and its balance adds a Warrior suit entry, since
  the default balance has no suit stats and Equip would otherwise refuse.
- For the Fortress, assert level-by-level costs in either currency, refusal under enemy presence,
  non-stacking auras read from tick-start state, the divisor floor of 1 on claim and breach,
  and that ownership loss resets the level.
- For structures, assert HP16 takes 16 lone attack ticks, lowest-ID four attackers,
  Medic and surplus soldiers claiming, no attack on transit/own nodes or Cores,
  post-combat damage deferred to the next tick, and no destruction-tick claim by participants.
  Fortress upgrades above level 1 preserve damaged HP; destruction resets the level,
  rebuy costs level 1, and the aura disappears only on the following tick.
- For minions, assert ordinary output pays the pool while minion output banks; InstallMinion
  costs three metal and retains the lowest-ID human in the remaining worker position.
  Refuse invalid district/value/owner, locked nodes and existing structures without mutation.
  Lock is neutral ownership, an anchored enemy (including departing Moving enemies), or
  an incomplete owner bar. Enemy presence does not pause minion production. Neutral
  ownership and a full bank pause its timer without Forge input spend or catch-up; missing
  Forge allocation/material wastes a cycle. Carry production overshoot and use district era.
  Any full claim, including the previous owner's re-claim, destroys a dormant Minion and
  pays its remaining bank with pool caps/overflow discard. Storehouse grants a free Minion
  once; its construction flag survives destruction/capture. Structure destruction pays
  remaining loot before clearing Minion automation/bank; a same-tick civilian capture
  cannot pay it again, and structure participants cannot claim on that tick.
  Collect=9 uses value 1 to start and 0 to cancel; an empty bank, non-owner or invalid
  value refuses without hash mutation. A locked owner's start is accepted and paused.
  Collection follows minion production and precedes auto-recruit: one shared node clock,
  +collectProgressPerTick (default 5) per active unlocked tick, transfer one at >=collectProgressPerUnit
  (default 16) and subtract it; the constants live in GameBalanceData, not in BankRules. A full five
  drains at ticks 4,7,10,13,16, independent of tempo. Food/materials/metal priority skips
  full pools; all eligible pools full freezes progress. No requester resets progress;
  empty bank resets progress/request; cancel resets unless a stationary collector remains.
  Use BankRules.Locked and HasStationaryCollector rather than restating eligibility; the
  CollectionState and InstallReason helpers must agree with CanCollect and CanInstallMinion.
  Lock pauses without reset; Restore completion unlocks collection in the same tick.
  Ownership changes cancel request/progress, including neutralisation.
  Assert bank total > 0 implies Minion; hash, copy and register every added node field.
  InstallMinion=8 and Collect=9 keep the six-int wire/log shape and do not bump ProtocolVersion.
  Market=13 remains historical saved data, migrates to Storehouse=18, and is refused
  on current packets; do not test active Market worker production.
- For Piers, initialize moveLegDurationTicks to 0 off-leg; BeginLeg latches it and
  ClearLeg clears it on arrival, corrupt-path recovery, combat, death, breach and
  respawn. Per-era pierTravelDivisor defaults to 2; historical zero tuning has
  ordinary physical travel, and release content must supply a positive divisor.
  Test enemy transit stopping with its final target, garrison combat first, and
  neutralisation using the existing global decrementMultiplier (default 4).
  The neutralisation tick stops at zero: transit resumes after win-check and
  moves next tick, while a destination continues ordinary full capture.
  The shared position gate blocks over-cap Idle villagers, combat survivors,
  fresh Move commands and every-tick resume. Only previousNodeID retreat escapes;
  blocked Move retains targetNodeID. Lowest IDs occupy maxClaimersPerNode slots;
  excess Idle villagers never advance the bar. Owner travel uses ceiling/minimum
  one and never rescales after mid-edge ownership changes. Continuing preserves
  progress and duration; reversing an 8-tick latch at 3 returns in exactly 3.
  Route in the unit's physical ticks with existing preferences; own Pier uses
  preference 100 once. Gate delay is ceil(current opposing bar / lone ClaimRate)
  with current frontier/resistance, never on the start node. Test gate36/detour32,
  gate24/detour32, speed-dependent routes, lowest-ID ceiling ties, odd durations,
  next-leg latching, copy/hash completeness and read-only amber intent.
- For Town, assert each player is paid once on their first full claim (including a
  raider taking the enemy Town), the reward is capped by population room and still
  consumes the entitlement, and ownership changes never re-pay or reset `townPaidMask`.
- For Recruit, assert pre-increment price and cooldown, exact food/count/body
  changes, refusal hash equality, dead-inclusive population, and overflow refusal.
  Test per-Village cooldowns with shared player count, ascending-node automatic
  attempts after production, ownership-loss reset, and absolute idempotent toggles.
  Keep the food cap at 30: N=8 costs 30; N=9 costs 33 and must be refused.

Step 6: Add determinism variant (always, for simulation tests)
- Run the identical scenario a second time from scratch
- Hash both resulting states with SimulationStateHasher
- Assert hash A == hash B
- This proves repeatability within one build, not compatibility with an
  older build. Keep the pinned assertions in DeterminismBaselineTests too;
  never re-pin them just to make a failure green. A deliberate re-pin must
  follow docs/computations/determinism-baseline.md, including the coordinated
  SimulationVersion.Current and BaselinesPinnedAtSimVersion update
- Adding era fields preserves era-0 hashes by hashing those fields only
  when non-zero. BalanceHasherTests checks balance-field coverage;
  balance itself is outside SimulationStateHasher
- The current baseline pin is version 4 (-2085505832 and 534653207 after D4).
  Unconditional structure kind/HP terms on all three bare nodes moved the previous
  C7 fingerprints (647286254 and 357327383). Balance tuning extensions are tagged
  and zero-neutral; historical absent tuning does not enable Fortress upgrading.
  D2 bank/timer/construction and D3 collection fields are tagged, indexed and zero/false-neutral, so
  both D1 pins remain unchanged through D3. D4 hashes moveLegDurationTicks
  unconditionally after moveProgress, adding a zero term for each of the two
  villagers in both completed fixtures: EmptyTick -563755666 to -2085505832,
  MoveAndCombat -2013445737 to 534653207. Those line-board routes are unchanged;
  the unreleased PR D version stays 4. Conditional hashing does not make older logs replayable
- Name this test with _Determinism suffix

Step 7: Run the tests
- Run `dotnet test dotnet/NodeWar.sln` for the simulation and the other
  UnityEngine-free suites, with no Editor or licence. On this Windows
  machine, if dotnet is not on PATH, use
  `& 'C:\Program Files\dotnet\dotnet.exe' test dotnet/NodeWar.sln`
- For a determinism receipt, follow docs/skills/run-dotnet-tests.md: pass
  the NUnit logger to the simulation test project alone, not the solution
- Use scripts/run-tests.ps1 only when checking the suite through Unity's
  batch runner (currently 6000.6.1f1); close the Editor first so it can take
  the project lock. The .NET run does not verify Editor or scene wiring

## Output format
Produce two test methods:
1. The correctness test asserting expected state
2. The determinism test asserting hash equality
Both in the existing test assembly and namespace.
Flag any SimulationState fields that appear to be missing 
from SimulationStateHasher or from SimulationState.CopyFrom (the latter is
enforced by SimulationStateCopyTests, which sets every field by reflection).
