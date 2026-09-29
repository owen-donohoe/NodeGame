---
type: Skill
title: write-sim-test
description: Procedure for adding test coverage when simulation behaviour changes.
tags: [skill, testing, simulation]
generated: { by: human:DonohoeCUA, at: 2026-08-30T17:15:16-04:00 }
verified:
  - { by: claude-opus-5, at: 2026-08-31T00:00:00Z }
  - { by: claude-opus-5, at: 2026-09-02T00:00:00Z }
  - { by: claude-opus-5-5, at: 2026-09-29T18:00:00Z }
verified_at_commit: a43b5f9
status: stable
sources:
  - id: tests
    resource: Assets/Tests/EditMode/Tests/DeterminismBaselineTests.cs
    title: Existing EditMode test patterns
    last_modified: 2026-09-25T20:27:51-04:00
  - id: fixture
    resource: Assets/Tests/EditMode/Tests/TestBoardFactory.cs
    title: TestBoardFactory shared fixtures
    last_modified: 2026-09-02T10:22:51-04:00
  - id: runner
    resource: scripts/run-tests.ps1
    title: EditMode test runner
    last_modified: 2026-09-18T08:08:19-04:00
  - id: match-factory
    resource: Assets/Scripts/Game/Simulation/MatchFactory.cs
    title: Shared drafted board and static configuration
    last_modified: 2026-09-25T22:52:42-04:00
  - id: balance-data
    resource: Assets/Scripts/Game/Simulation/GameBalanceData.cs
    title: Per-era suit and district entries
    last_modified: 2026-09-25T22:52:42-04:00
  - id: sim-state
    resource: Assets/Scripts/Game/Simulation/SimulationState.cs
    title: Player era table defaults
    last_modified: 2026-09-25T22:52:42-04:00
  - id: hasher
    resource: Assets/Scripts/Game/Simulation/SimulationStateHasher.cs
    title: Conditional era hashing
    last_modified: 2026-09-25T22:52:42-04:00
  - id: balance-tests
    resource: Assets/Tests/EditMode/Tests/BalanceHasherTests.cs
    title: Coverage of balance fields
    last_modified: 2026-09-25T22:52:42-04:00
  - id: baseline-contract
    resource: docs/computations/determinism-baseline.md
    title: Pinned fingerprint and version policy
    last_modified: 2026-09-28T14:44:45-04:00
  - id: dotnet-runner
    resource: docs/skills/run-dotnet-tests.md
    title: Editor-free suite and receipt commands
    last_modified: 2026-09-26T10:16:37-04:00
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
- Reuse TestBoardFactory for small fixtures. For a complete drafted match,
  use MatchFactory.Build so the test starts from the same tick-0 board as
  the live game and referee
- Install the balance on both GameSimulation and CommandProcessor before
  running commands or ticks. MatchFactory.Configure also sets Pathfinding's
  board multipliers; Build alone does not configure those statics. Do not
  run fixtures in parallel in one process
- For era-specific behavior, supply the player's era tables and the relevant
  SuitStats / DistrictStats entries. Missing player entries mean era 0;
  balance lookups fall back to era 0. Keep arrays independent between runs
- Use explicit integer values -- no magic numbers without comments
- Document what the starting state represents

Step 3: Define the command sequence
- List the GameCommands in the order they will be applied
- Use real CommandTypes from the actual codebase
- Document why each command is in the sequence

Step 4: Advance ticks
- Run SimulateTick the exact number of ticks needed
- Document what should happen each tick
- Do not over-tick -- test the minimum needed to verify behavior

Step 5: Assert expected state
- Assert specific integer field values on SimulationState
- Assert villager states, node ownership, resource counts 
  as relevant
- One assertion per logical outcome -- do not bundle unrelated 
  assertions

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
from SimulationStateHasher.
