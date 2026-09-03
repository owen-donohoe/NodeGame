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
verified_at_commit: 67fea34
status: stable
sources:
  - id: tests
    resource: Assets/Tests/EditMode/Tests/DeterminismBaselineTests.cs
    title: DeterminismBaselineTests and the pinned constants
    last_modified: 2026-08-30T16:44:10-04:00
  - id: fixture
    resource: Assets/Tests/EditMode/Tests/TestBoardFactory.cs
    title: TestBoardFactory.BuildThreeNodeBoard
    last_modified: 2026-08-30T16:44:10-04:00
  - id: hasher
    resource: Assets/Scripts/Game/Simulation/SimulationStateHasher.cs
    title: SimulationStateHasher.ComputeHash
    last_modified: 2026-08-30T17:51:21-04:00
  - id: sim-loop
    resource: Assets/Scripts/Game/Simulation/GameSimulation.cs
    title: GameSimulation.SimulateTick
    last_modified: 2026-08-30T17:51:21-04:00
  - id: contract
    resource: docs/simulation-rules.md
    title: Simulation Determinism Contract
  - id: build
    resource: dotnet/NodeWar.Simulation/NodeWar.Simulation.csproj
    title: The build definition the gate compiles the fixture through
  - id: gate
    resource: .github/workflows/determinism.yml
    title: The CI job that runs this computation
---

# Computation

Build a known board, advance it a known number of ticks from a known command set, and fold the
resulting `SimulationState` into one integer with `SimulationStateHasher.ComputeHash`. Compare that
integer against the recorded baseline.

Two fixtures are sanctioned, both on `TestBoardFactory.BuildThreeNodeBoard` — player 0's Core
(node 0) and player 1's Core (node 2) joined by one neutral connector (node 1), one Idle villager
each, edge weights of 1, and `GameBalanceData.Default()`:

| Fixture | Ticks | Commands | Baseline hash |
|---|---|---|---|
| `EmptyTick` | 100 | none | `17457352` |
| `MoveAndCombat` | 4 | both villagers `Move` to node 1 | `626950565` |

`TestBoardFactory` also holds `BuildSquareBoard`, a 2x2 grid added for movement-retargeting tests.
It is **not sanctioned** and no baseline is pinned against it. Only the two fixtures above are
attested; adding a third to this table means recording and defending a new constant.

`EmptyTick` exercises the idle path: healing fires at ticks 30/60/90 but both villagers are at
`maxHP`, so only `tickCount` moves. `MoveAndCombat` exercises movement and combat entry: each
villager crosses one edge at `travelWeight (1) × baseMoveSpeedTicks (4)` = 4 ticks, arrives on
node 1 simultaneously, and `TickCombat` puts both into `Fighting`.

The baselines live as `const int` in `DeterminismBaselineTests.cs`. That file is the computation;
this document is its contract.

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
3. It reports the commit the verdict applies to.

A run whose receipt fails either check is **unattested**. Treat the determinism gate as unsatisfied
and do not claim the simulation change is safe.

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
2. Update the `const` and the table above in the **same commit** as the change that caused it.
3. Note it in the commit message. A baseline that moves in its own isolated commit has lost the
   context that made it reviewable.

Never update a baseline to make a red test green without knowing which change moved it.
