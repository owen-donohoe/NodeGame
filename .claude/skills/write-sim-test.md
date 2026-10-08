---
type: Skill
title: write-sim-test
description: Procedure for adding test coverage when simulation behaviour changes.
tags: [skill, testing, simulation]
generated: { by: human:DonohoeCUA, at: 2026-08-30T17:15:16-04:00 }
verified:
  # full history: docs/verification-log.md
  - { by: claude-sonnet-5-5, at: 2026-10-08T16:47:09Z }
verified_at_commit: 9b4ea209b2004f3ccfdc3209fb4133b051b12ab0
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
- For sticky orders, assert targetNodeID survives a fight and an unreachable
  destination, that an order given while Fighting leaves the attack clock alone,
  and that work or claim begun at resume counts from the next tick
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
  at the metal cap, Market still alternates, and cap 0 remains uncapped
- For paid respawns, assert only successful commands increment paidRespawns
  and the Infirmary (at most two counted workers, lowest villager ID, none under enemy presence) discounts the escalated cost with integer rounding/minimum 1
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
- The current baseline pin is version 3 (411123996 and 2101726457). The neutral
  recruit fields retain those two fingerprints; the terrain addition re-pinned both,
  because terrain and boardHash are always hashed.
  Conditional hashing does not make older simulation-version logs replayable
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
