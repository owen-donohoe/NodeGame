---
shell: 2
up: "[[Tooling]]"
down:
  - "[[compile-check.ps1]]"
  - "[[sim-guard.ps1]]"
concepts:
  - "[[Deterministic lockstep]]"
tags: [shell/2, realm/lobby, realm/match, realm/backend, system/tooling]
card: docs/systems/tooling.md
sources:
  - dotnet/NodeWar.sln
  - dotnet/Directory.Build.props
  - dotnet/NodeWar.Simulation/NodeWar.Simulation.csproj
  - dotnet/NodeWar.Simulation.Tests/NodeWar.Simulation.Tests.csproj
  - dotnet/NodeWar.Lobby.Tests/NodeWar.Lobby.Tests.csproj
  - dotnet/NodeWar.View.Tests/NodeWar.View.Tests.csproj
  - dotnet/NodeWar.Network.Tests/SweepTests.cs
  - scripts/compile-check.ps1
  - Assets/Scripts/Editor/TestBridge.cs
  - Assets/Tests/EditMode/Outline/NodeWar.View.Outline.Tests.asmdef
verified: f576d156
---

# Test evidence

> [!question] The question
> Why run plain .NET tests, a Unity compile check and Editor checks instead of picking one runner for everything?

**Where you are:** [[Home]] › [[Tooling]] › **Test evidence** (mechanism)

## The problem

A headless test can prove a resource-ring calculation while never drawing a ring. A compiler can accept a callback while its button is absent from the scene. Evidence becomes misleading when the report silently expands “this part passed” into “the whole game works.”

## The idea

**Choose the check for the question, compile the actual shared source, and report the boundary of the result.**

## How it works here

`dotnet/NodeWar.sln` groups Editor-free projects. This is a map of responsibilities, not a passing-run receipt or a case-count inventory:

| Test project | What it checks |
|---|---|
| `NodeWar.Simulation.Tests` | Rules, construction, movement/economy, state hashing/copying and determinism fixtures |
| `NodeWar.Lobby.Tests` | Loadouts, eligibility, lobby models, settings and packet shapes |
| `NodeWar.View.Tests` | Pure startup-sequence policies, gesture/view/layout/art mathematics and authored UI contracts |
| `NodeWar.MatchLog.Tests` | Binary logs, recording, replay, refusal and corruption |
| `NodeWar.Network.Tests` | Lockstep under simulated loss, duplication, reordering, outages and rollback |
| `NodeWar.Progression.Tests` | Rating, ranks, arenas, catalog and settlement rules |
| `NodeWar.BalanceRig.Tests` | Repeatable headless setup, paired seats and diagnostics |
| `NodeWarCloud.Tests` | Cloud rules/services, persistence, matchmaking, settlement and release-content checks |

The simulation library links `Assets/Scripts/Game/Simulation/**/*.cs`; its tests link the shared EditMode test directory recursively, with a .NET-only balance-asset test added. The project's own comment says: “There is one copy of the truth, in Assets/, and two toolchains compiling it.” Other projects also use explicit source entries: new files do not universally join every suite automatically.

The library targets `netstandard2.1`, while the test host targets `net8.0`. Shared props pin C# 9 and disable default compile items. NUnit stays on 3.x so the classic assertions remain compatible with Unity's test framework. These constraints keep a modern .NET runner from quietly accepting source Unity cannot compile.

[[compile-check.ps1]] checks Unity-facing source using real Unity/package assemblies. It runs no tests. Outline EditMode tests have their own Unity assembly and are another reason the solution is not synonymous with all Editor coverage.

## Why this, not that

| Decision | Alternative | Gain | Cost |
|---|---|---|---|
| Shared source links | Maintain headless copies | Changes exercise the same implementation | Build inclusion and compatibility rules matter |
| Plain .NET tests | Start Unity for every pure assertion | Editor-free CI and fast feedback | No scene or rendering evidence |
| Separate compile-check | Assume untested UI compiles | Catches wrong APIs and broken call sites | Depends on already-built Unity/package references |
| Explicit measurements | Run all tuning sweeps routinely | Ordinary tests stay focused | A green run does not imply measurements ran |

## What it costs later

Keep source lists and XML resources up to date. Never parallelize fixtures that share process-global simulation settings: one test's balance can contaminate another's result. The live Editor bridge waits for idle and matches a fresh GUID before accepting completion, but an idle Editor may not yet have started compiling a new edit. [[Determinism receipts]] adds freshness checks, with limits of its own.

## Traps

- Network `SweepTests` is `[Explicit]`; its header calls it “Not a test: a measurement.” Ordinary totals do not say it ran.
- `compile-check` merges runtime and Editor code, so it cannot detect every Editor-only API leak into runtime.
- Matching test source does not guarantee identical case inventories: the .NET simulation project also includes a local test file.
- Neither pure tests nor compile-check proves scene/prefab wiring or visual quality.

## Patterns you've seen before

- 🔁 [[Same code, three places]]: share implementation instead of testing a separate imitation.
- 🔁 [[Pure core, Unity shell]]: extract calculations and policies so headless tests can exercise them.
- 🔁 [[Constants come from measurements]]: a sweep answers tuning questions that a pass count cannot.

## Check yourself

> [!question]- A resource-ring math test passes. What does that tell you about clipping on a phone?
> Nothing directly. The calculation passed for asserted inputs; layout, device rendering and clipping need visual checks in Unity.

> [!question]- Why not upgrade only the .NET runner's NUnit package to the newest major version?
> The shared tests must still compile with Unity's NUnit 3 assertion surface. The runner cannot independently change the language those tests use.

## Go deeper ↓

- [[compile-check.ps1]] · [[sim-guard.ps1]]
- Build definitions in frontmatter show exactly which sources each runner includes.

Sideways → [[Determinism receipts]] · [[SimulationStateHasher]] · [[Three UI stacks]]

## My understanding

<!-- Yours. AI never edits below this line. -->
