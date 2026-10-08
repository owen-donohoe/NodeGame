---
type: Attested Computation
title: Determinism baseline
description: The sanctioned simulation fingerprint check — runs a known board for a known tick count and compares the state hash against a recorded baseline.
tags: [determinism, testing, attested, simulation]
runtime: [dotnet-test, unity-editmode]
parameters:
  - { name: fixture, type: string, required: true }
  - { name: ticks, type: integer, required: true }
executor:
  resource: docs/skills/run-dotnet-tests.md
  receipt: [commit, fixture, ticks, state_hash, tests_passed]
attester:
  resource: docs/attesters/hash_baseline.ps1
generated: { by: claude-opus-5, at: 2026-08-31T00:00:00Z }
verified:
  # full history: docs/verification-log.md
  - { by: claude-sonnet-5-5, at: 2026-10-08T17:01:14Z }
verified_at_commit: bdb967ed1524de151f220a3be1733bec8177e56d
status: stable
sources:
  - id: tests
    resource: Assets/Tests/EditMode/Tests/DeterminismBaselineTests.cs
    title: DeterminismBaselineTests and the pinned constants
    last_modified: 2026-09-25T20:27:51-04:00
  - id: fixture
    resource: Assets/Tests/EditMode/Tests/TestBoardFactory.cs
    title: TestBoardFactory.BuildThreeNodeBoard
    last_modified: 2026-09-02T10:22:51-04:00
  - id: hasher
    resource: Assets/Scripts/Game/Simulation/SimulationStateHasher.cs
    title: SimulationStateHasher.ComputeHash
    last_modified: 2026-09-25T22:52:42-04:00
  - id: sim-loop
    resource: Assets/Scripts/Game/Simulation/GameSimulation.cs
    title: GameSimulation.SimulateTick
    last_modified: 2026-09-25T22:52:42-04:00
  - id: contract
    resource: docs/simulation-rules.md
    title: Simulation Determinism Contract
    last_modified: 2026-09-28T14:44:08-04:00
  - id: build
    resource: dotnet/NodeWar.Simulation/NodeWar.Simulation.csproj
    title: The build definition the gate compiles the fixture through
    last_modified: 2026-08-31T20:07:54-04:00
  - id: gate
    resource: .github/workflows/determinism.yml
    title: The CI job that runs this computation
    last_modified: 2026-09-03T21:46:34-04:00
  - id: sim-version
    resource: Assets/Scripts/Game/Simulation/SimulationVersion.cs
    title: SimulationVersion.Current and bump policy
    last_modified: 2026-09-25T20:27:51-04:00
  - id: balance-hasher
    resource: Assets/Scripts/Game/Simulation/BalanceHasher.cs
    title: Balance fingerprint separate from state
    last_modified: 2026-09-25T22:52:42-04:00
  - id: attester-script
    resource: docs/attesters/hash_baseline.ps1
    title: Required cases and receipt freshness checks
    last_modified: 2026-08-31T20:07:23-04:00
  - id: dotnet-runner
    resource: docs/skills/run-dotnet-tests.md
    title: Editor-free suite and receipt commands
    last_modified: 2026-09-26T10:16:37-04:00
---

# Computation

Build a known board, advance it a known number of ticks from a known command set, and fold the
resulting `SimulationState` into one integer with `SimulationStateHasher.ComputeHash`. Compare that
integer against the recorded baseline.

Two fixtures are sanctioned, both on `TestBoardFactory.BuildThreeNodeBoard` — player 0's Core
(node 0) and player 1's Core (node 2) joined by one neutral connector (node 1), one Idle villager
each, link weights of 1, and `GameBalanceData.Default()`:

| Fixture | Ticks | Commands | Baseline hash |
|---|---|---|---|
| `EmptyTick` | 100 | none | `647286254` |
| `MoveAndCombat` | 4 | both villagers `Move` to node 1 | `357327383` |

`TestBoardFactory` also holds `BuildSquareBoard`, a 2x2 grid added for movement-retargeting tests.
It is **not sanctioned** and no baseline is pinned against it. Only the two fixtures above are
attested; adding a third to this table means recording and defending a new constant.

`EmptyTick` exercises the idle path: healing fires at ticks 30/60/90 but both villagers are at
`maxHP`, so only `tickCount` moves. `MoveAndCombat` exercises movement and combat entry: each
villager crosses one link at `travelWeight (1) × baseMoveSpeedTicks (4)` = 4 ticks, arrives on
node 1 simultaneously, and `TickCombat` puts both into `Fighting`.

The baselines live as `const int` in `DeterminismBaselineTests.cs`, alongside
`BaselinesPinnedAtSimVersion = 3`. `SimVersion_MatchesPinnedBaselines` checks that this version
matches `SimulationVersion.Current`. It checks version equality, not whether someone edited only
the hash constants. That file is the computation; this document is its contract.

Era fields enter `SimulationStateHasher` only when non-zero. Both fixtures remain era 0 and
never breach, so the v2 re-pin in `d84cb1d` kept both numeric hashes unchanged while updating
their pinned version from 1 to 2 in the rule-change commit. Neutral breach extension fields
do not move these fingerprints. Adding eras had needed no bump; the breach/tempo rule change
did. Version-1 logs are refused by version-2 replay even when their era-0 hashes would match.
The separate `BalanceHasher` covers balance data, including the new schedules, breach tuning
and caps; these are state fingerprints, not balance fingerprints.

C3's recruit count and ready tick are zero-neutral, and its repeat flag is
false-neutral, with tagged indexed contributions only for non-neutral values.
Neither sanctioned fixture recruits or captures a Village, so these additions
and the auto-recruit production pass retain both pinned state fingerprints.

**v3 re-pin (terrain board, B4).** `SimulationState.boardHash` (after `defaultLinkWeight`) and
`NodeData.terrain` (after `baseDistrictType`) are hashed unconditionally, and the version went 2 → 3.
The three-node fixtures hold `boardHash = 0` and `Land` on every node, so no board or rule difference
moved the numbers: the hash simply folds in four more terms (one for the state, one per node), which
changes the polynomial. `17457352 → 411123996` (`EmptyTick`) and `626950565 → 2101726457`
(`MoveAndCombat`). Both were computed twice in separate processes.

**C7 re-pin (Fortress, still version 3).** C7 removed the unconditional per-villager `hasRampartBonus`
hash term (and the conditional `rampartBonusEra` one) when it replaced the Rampart buffs with the
node-level `fortressLevel`. The fixtures hold two villagers, so the polynomial lost two terms and both
fingerprints moved: `411123996 → 647286254` (`EmptyTick`) and `2101726457 → 357327383`
(`MoveAndCombat`). No rule difference reaches these fixtures. Version 3 is unreleased, so it was not bumped again. `BreachTempoTests.
LegacyNoTempoRetainsVersionOneBaselineHashPaths` runs the same two fixtures and carries the same two
constants.

## Where it runs

Two runtimes execute this computation from one copy of the source, and they produce the same
receipt:

| Runtime | Executor | Needs Unity |
|---|---|---|
| `dotnet-test` | [../skills/run-dotnet-tests](../skills/run-dotnet-tests.md) | No |
| `unity-editmode` | [../skills/run-editmode-tests](../skills/run-editmode-tests.md) | Yes, installed and licensed |

`dotnet-test` is the declared executor because it is what the gate relies on: it needs no licence,
runs on Linux, and always compiles before it runs. The Unity runners remain correct and answer a
question the .NET one cannot — whether the code works in the Editor.

`.github/workflows/determinism.yml` runs the `dotnet-test` executor on `ubuntu-latest` and
`windows-latest` on every push and pull request, then runs the attester on each leg. Because the
fixtures assert exact integers, two green legs assert something stronger than "the tests pass":
that the fingerprints are identical across operating system and runtime. That is the property
lockstep depends on, and nothing verified it before the gate existed.

**A hash that differs between legs is a finding about the simulation, not a CI problem.** See
*Re-pinning* below; the rule there is unchanged by having more than one runtime.

## What a caller may vary

Only `fixture` and `ticks`, and only to the pairs in the table above. A different board or tick
count is a **different computation** and needs its own recorded baseline — it is not this one run
with new arguments. Adding a fixture means adding a `const`, a test, and a row here, in one commit.

## What the attester checks

`docs/attesters/hash_baseline.ps1` reads the `TestResults/results.xml` receipt and returns a verdict.
It is deterministic PowerShell with no LLM in the loop, because a verifier that can be talked into a
pass is not a verifier.

1. **Both determinism cases ran and passed.** A receipt that simply omits them is not a pass — a
   suite where the determinism tests were filtered out, renamed, or silently skipped fails here
   rather than sliding through as green.
2. **The receipt is for current code.** `results.xml` must be newer than the last commit touching
   `Assets/Scripts/Game/Simulation/`. A stale receipt from before the change under review proves
   nothing about it.
3. **The receipt is for the working tree too.** It must be at least as new as the newest `.cs`
   source under `Assets/Scripts/Game/Simulation/` and `Assets/Tests/EditMode/Tests/`. A receipt
   predating an uncommitted edit fails even if it is newer than the last simulation commit.
4. It reports the commit the verdict applies to.

A run whose receipt fails a check is **unattested**. Treat the determinism gate as unsatisfied
and do not claim the simulation change is safe.

The attester requires the two fingerprint cases, not `SimVersion_MatchesPinnedBaselines`; the
full suite run in CI checks that test. If the last simulation commit date is unavailable or
unparseable, or no watched `.cs` sources are found, the attester reports the corresponding
freshness check as skipped rather than failing closed.

## Why this exists

Before the baselines were pinned, both tests asserted only `hashA == hashB` — two states built in
the same process from the same code. That is close to a tautology. It catches in-process
nondeterminism and nothing else, and it is blind to the failure the project actually fears: a change
that silently alters simulation output, desyncing two peers on different builds. The file was named
`DeterminismBaselineTests` and pinned nothing.

Naming the receipt and the verdict is what surfaced that. See
[simulation-rules](../simulation-rules.md) for the contract these fingerprints protect, and
[../skills/run-editmode-tests](../skills/run-editmode-tests.md) for how to produce a receipt.

## Re-pinning

A baseline changing is a signal, not an obstacle. When a deliberate balance or logic change moves
it:

1. Confirm the change is intended and understand *why* the hash moved.
2. Update the hash `const`, the table above, `SimulationVersion.Current`, and
   `BaselinesPinnedAtSimVersion` in the **same commit** as the change that caused it. A pure
   balance edit normally needs no simulation-version bump because the handshake compares the
   balance hash; deliberately re-pinning these fixtures also updates their pinned version.
3. Note it in the commit message. A baseline that moves in its own isolated commit has lost the
   context that made it reviewable.

Never update a baseline to make a red test green without knowing which change moved it.
