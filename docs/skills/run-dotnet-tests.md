---
type: Executor Skill
title: Run the simulation suite without Unity
description: How to run Assets/Tests/EditMode/ through the plain .NET projects in dotnet/, the receipt it produces, and why this is the runner CI uses.
tags: [testing, executor, dotnet, ci, receipt]
generated: { by: claude-opus-5, at: 2026-08-31T00:00:00Z }
verified:
  # full history: docs/verification-log.md
  - { by: claude-sonnet-5-5, at: 2026-10-08T16:13:42Z }
verified_at_commit: 9dd245606088c93c1d0725327ad1613355b69e15
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
  - id: tests-link-weight
    resource: Assets/Tests/EditMode/Tests/LinkWeightTests.cs
    title: Link weight cases
  - id: tests-movement
    resource: Assets/Tests/EditMode/Tests/MovementCorrectnessTests.cs
    title: Movement correctness cases
  - id: tests-smoke
    resource: Assets/Tests/EditMode/Tests/SimulationSmokeTest.cs
    title: Simulation smoke test
---

# Run the simulation suite without Unity

The shared simulation test cases from [run-editmode-tests](run-editmode-tests.md), executed by
`dotnet test` instead of Unity's Test Runner. No Editor, no licence, no Windows requirement.

The solution holds eight test projects. Run everything for pass/fail:

```
dotnet test dotnet/NodeWar.sln
```

Expect **3409 cases** at the time of writing (the table says where each lives, so a changed
total is easy to place), with none failing. `NodeWar.Network.Tests` also contains an explicit
`Sweep` case that a plain run reports as skipped.

| Project | Cases | Covers |
|---|---|---|
| `NodeWar.Simulation.Tests` | 457 | `Assets/Tests/EditMode/Tests/`: the version-3 determinism baseline, link weights, movement, production, combat fixes, `MatchFactory`, terrain maps and draft legality, `MatchSetup`, sticky orders, restore, the capture bonus, tick order, eras, breach/tempo, paid respawns, resource caps, Recruit/SetAutoRecruit, command refusal, vocabulary compatibility and balance hashing; also the dotnet-only balance-asset text guard |
| `NodeWar.Lobby.Tests` | 571 | loadout wire format (eras and skins included), loadout editor rules, Workshop era chips, suit trees, item tints, families, the in-match command checks, handshake and emote packets, game settings and input bindings, controls view model, arena rank display, trophy bar, match history rows, the ranked queue presenter and rendezvous, draft loadout packets, `MatchSetup`/`MatchSetupAck` packets, the pre-rename loadout and profile compatibility cases, and node-command round trips/refusals |
| `NodeWar.View.Tests` | 1546 | the UnityEngine-free view maths: camera POV, indicator placement, route reveal, emote rate limit, resource rings/bars and shared full phase, production readout, breach walls, playtest debug, sheet resources, board art and board framing, terrain presentation, order presentation, icon/context resolution and usage, required UXML names, draft handover |
| `NodeWar.MatchLog.Tests` | 90 | the match log format (round trip, unknown chunks, truncation), the recorder, `MatchReplay`, ERAS and SKINS, BOARD_V2 and SETUP (round trip, conflicting or missing chunks, terrain replay), node-command round trips and unknown-command refusal |
| `NodeWar.Network.Tests` | 122 | `LockstepCore` (the networked tick driver) and `InputDelayController` under an in-memory lossy link: clean, loss, burst loss, duplication, reordering, latency, jitter, outages, frame spikes and a late start, each judged against the same match on a perfect link; the adaptive input delay; the lifecycle of an ended or paused core. `SweepTests` is explicit (`--filter "Category=Sweep"`) and prints the numbers behind the tuning constants; a terrain-board lockstep scenario |
| `NodeWar.Progression.Tests` | 148 | Glicko-2, RR, arenas, catalog validation, era unlocks, match settlement |
| `NodeWar.BalanceRig.Tests` | 36 | the headless balance rig, scenario validation, repeatable execution against an exported JSON balance, its diagnostics and timeline metrics, and setup compatibility with the shared map |
| `NodeWarCloud.Tests` | 439 | the Cloud Code module: player state, accounts, catalog, inventory, Equip and the equipped clamp, the referee and its balance catalog, match records and their store, Matchmaker allocation, map and era eligibility, persistence vocabulary, match reporting and settlement, match history, the rank table, the ranked queue fake and status mapping, the report-service fake, ranked rendezvous, confirmation and leaving |

## Producing the receipt

**Do not pass `--logger` to the solution.** Every project would write the same absolute
`LogFilePath`, and the last to finish overwrites the other — leaving a receipt with no determinism
cases in it, which the attester correctly rejects but which reads like a broken tool rather than a
misuse.

The receipt is a claim about the simulation, so produce it from that project alone:

```
dotnet test dotnet/NodeWar.Simulation.Tests/NodeWar.Simulation.Tests.csproj \
  --logger "nunit;LogFilePath=<repo-root>/TestResults/results.xml"
```

Expect 457 passed, and both version-3 pinned fingerprints from
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

The dotnet project additionally includes `DefaultBalanceAssetTests.cs`, a text guard for the
serialized balance's v2 keys. It is not an Editor test, so total case counts need not match
between runners. The simulation suite skips no cases.

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
