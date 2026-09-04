---
type: Executor Skill
title: Run the simulation suite without Unity
description: How to run Assets/Tests/EditMode/ through the plain .NET projects in dotnet/, the receipt it produces, and why this is the runner CI uses.
tags: [testing, executor, dotnet, ci, receipt]
generated: { by: claude-opus-5, at: 2026-08-31T00:00:00Z }
verified_at_commit: fc94a0f
status: draft
sources:
  - id: solution
    resource: dotnet/NodeWar.sln
    title: The .NET solution
  - id: sim-project
    resource: dotnet/NodeWar.Simulation/NodeWar.Simulation.csproj
    title: Simulation library, netstandard2.1
  - id: test-project
    resource: dotnet/NodeWar.Simulation.Tests/NodeWar.Simulation.Tests.csproj
    title: Test project, linked sources
  - id: shared-props
    resource: dotnet/Directory.Build.props
    title: Shared LangVersion and compile-item settings
  - id: lobby-test-project
    resource: dotnet/NodeWar.Lobby.Tests/NodeWar.Lobby.Tests.csproj
    title: Lobby test project, linked sources
  - id: workflow
    resource: .github/workflows/determinism.yml
    title: The determinism CI gate
  # The test files themselves. Declared because this document states how many
  # cases to expect, and that number is only checkable against them.
  - id: tests-determinism
    resource: Assets/Tests/EditMode/Tests/DeterminismBaselineTests.cs
    title: Determinism baseline cases
    last_modified: 2026-08-30T16:44:10-04:00
  - id: tests-edge-weight
    resource: Assets/Tests/EditMode/Tests/EdgeWeightTests.cs
    title: Edge weight cases
    last_modified: 2026-08-30T17:51:21-04:00
  - id: tests-movement
    resource: Assets/Tests/EditMode/Tests/MovementCorrectnessTests.cs
    title: Movement correctness cases
    last_modified: 2026-09-02T10:22:51-04:00
  - id: tests-smoke
    resource: Assets/Tests/EditMode/Tests/SimulationSmokeTest.cs
    title: Simulation smoke test
    last_modified: 2026-08-29T10:56:17-04:00
---

# Run the simulation suite without Unity

The same 14 simulation test cases as [run-editmode-tests](run-editmode-tests.md), executed by
`dotnet test` instead of Unity's Test Runner. No Editor, no licence, no Windows requirement.

The solution holds two test projects. Run everything for pass/fail:

```
dotnet test dotnet/NodeWar.sln
```

Expect **60 passed** — 14 from `NodeWar.Simulation.Tests`, 46 from `NodeWar.Lobby.Tests`.

| Project | Cases | Covers |
|---|---|---|
| `NodeWar.Simulation.Tests` | 14 | the determinism baseline, edge weights, movement correctness, a smoke test |
| `NodeWar.Lobby.Tests` | 46 | `LoadoutData`'s wire format and the loadout editor rules |

## Producing the receipt

**Do not pass `--logger` to the solution.** Both projects would write the same absolute
`LogFilePath`, and the last to finish overwrites the other — leaving a receipt with no determinism
cases in it, which the attester correctly rejects but which reads like a broken tool rather than a
misuse.

The receipt is a claim about the simulation, so produce it from that project alone:

```
dotnet test dotnet/NodeWar.Simulation.Tests/NodeWar.Simulation.Tests.csproj \
  --logger "nunit;LogFilePath=<repo-root>/TestResults/results.xml"
```

Expect 14 passed, and both pinned fingerprints from
[computations/determinism-baseline](../computations/determinism-baseline.md) matching.
`.github/workflows/determinism.yml` runs these as two separate steps for exactly this reason.

## There is one copy of the source

The test files are **not** duplicated here. `dotnet/NodeWar.Simulation.Tests` compiles
`Assets/Tests/EditMode/Tests/**/*.cs` by linked reference, exactly as
`dotnet/NodeWar.Simulation` compiles `Assets/Scripts/Game/Simulation/**/*.cs`. Unity and .NET each
build their own assembly from the same text.

That is what makes the two runners comparable rather than merely similar: a change to a test is a
change to both suites, and neither can quietly drift from the other. Both globs are recursive, so a
new file is picked up by both build systems without a second file list to maintain.

Two constraints follow from the arrangement and must be preserved:

* **The library targets `netstandard2.1`**, which is the API Compatibility Level Unity builds this
  project at. That makes the .NET build a check in the other direction too: a `net8.0`-only API
  fails here, before Unity ever sees it. `LangVersion` is pinned to 9.0 in
  `dotnet/Directory.Build.props` for the same reason.
* **NUnit stays on 3.x.** NUnit 4 removed the classic assertion model (`Assert.AreEqual`,
  `Assert.IsNotNull`) that these tests use and that Unity's Test Framework ships. Upgrading the
  package here would break the Unity build of the same files.

## Do not enable parallel execution

`GameSimulation.bal`, `CommandProcessor.bal`, and the five `public static int` multipliers on
`Pathfinding` are process-global mutable state, installed through `SetBalance` by each fixture.
Fixtures running concurrently would race them and produce fingerprints that depend on scheduling —
the exact failure this suite exists to detect, introduced by the harness rather than the code.

NUnit's default is sequential. The requirement is simply never to add `[Parallelizable]` or a
`LevelOfParallelism` setting.

## Choosing between this and the Unity runners

| | `dotnet test` | `run-tests.ps1` | `run-tests-live.ps1` |
|---|---|---|---|
| Needs Unity | No | Yes, Editor closed | Yes, Editor open |
| Needs a licence | No | Yes | Yes |
| Runs on Linux | Yes | No | No |
| Can report stale code | No | No | **Yes** |
| Used by CI | Yes | No | No |

Prefer this runner for any receipt you intend to rely on. It always compiles before it runs, so the
stale-assembly hazard documented in [run-editmode-tests](run-editmode-tests.md) — a fully green run
against assemblies that predate the edit — cannot occur on this path.

The Unity runners remain the right choice when the question is whether the code works *in the
Editor*, which is not a question this runner can answer.

## The receipt

`NunitXml.TestLogger` writes NUnit3 XML: a `<test-run>` root, and a `<test-case>` per test carrying
`fullname` and `result`. This is byte-compatible with what Unity's runners produce, which is
deliberate — `docs/attesters/hash_baseline.ps1` parses either without modification.

Pass an **absolute** `LogFilePath`. Left relative, `dotnet test` writes under the test project's own
`TestResults/` directory, and the attester looks only at the one at the repository root.

## In CI

`.github/workflows/determinism.yml` runs this on `ubuntu-latest` and `windows-latest` on every push
and pull request, then runs the attester on each leg.

The matrix carries the weight. Because the tests assert exact integers rather than absence of
crashes, two green legs are a statement that the simulation computes bit-identical results across
operating systems and runtimes — the property lockstep depends on, and one nothing verified before
this runner existed.

**A hash that differs between legs is a finding about the simulation, not a CI problem.** Re-pinning
a baseline to make the matrix green would discard the only signal this job exists to produce. See
the re-pinning rules in
[computations/determinism-baseline](../computations/determinism-baseline.md).

## Exit codes

| Code | Meaning |
|---|---|
| 0 | Every test passed |
| 1 | A test failed, or the build failed |

As with the Unity runners, that answers "did every test pass", which is not the same question as
"is the determinism gate satisfied at this commit". `docs/attesters/hash_baseline.ps1` answers the
second.
