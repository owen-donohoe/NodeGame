---
type: Guide
title: Engineering workflow and repository map
description: Source ownership, build surfaces, test coverage, and practical change paths.
tags: [guide, testing, repository, workflow]
status: draft
sources:
  - id: solution
    resource: dotnet/NodeWar.sln
    title: NodeWar.sln
  - id: compilation
    resource: scripts/compile-check.ps1
    title: compile-check.ps1
  - id: ci
    resource: .github/workflows/determinism.yml
    title: determinism.yml
  - id: instructions
    resource: CLAUDE.md
    title: CLAUDE.md
  - id: feature
    resource: docs/adding-a-feature.md
    title: adding-a-feature.md
---

# Engineering workflow and repository map

[Atlas](README.md) · Previous: [Logs and replay](logs-and-replay.md) · Next: [Decision map](decision-map.md)

The repository has two build surfaces over shared code: Unity for the interactive client, and hand-maintained .NET projects for portable rules and tests. Understanding which surface owns a failure is more useful than treating every C# file as one application.

## Where to look

| Location | Responsibility | Start here when… |
|---|---|---|
| `Assets/Scripts/Game/Simulation/` | Match rules and data | An outcome, command, or deterministic state is wrong |
| `Assets/Scripts/Game/Core/` | Lifecycle, drivers, camera, presenter seams | A match fails to start, transition, or stop |
| `Assets/Scripts/Game/Network/` | Transport, lockstep, serialization | Peers disagree or cannot connect |
| `Assets/Scripts/Game/Input/` | Gestures, selection, commands, scripted bot | An action is interpreted incorrectly |
| `Assets/Scripts/Game/UI/` | uGUI and shared panel arbitration | A fallback or world-space UI path misbehaves |
| `Assets/Scripts/Game/View/` | World objects, routes, POV, feedback | The correct state looks wrong |
| `Assets/UI/` | Toolkit scripts, UXML layouts, USS styles, editor setup | Lobby/HUD/draft presentation needs work |
| `Assets/Legacy/` | Retained uGUI lobby | A fallback compilation or old UI dependency breaks |
| `Assets/Scripts/Lobby/` | Profile, loadout data, lobby coordination | Selection or local persistence is wrong |
| `Assets/Scripts/Backend/` | Client services, shared records/rules, exports | Account/inventory integration is wrong |
| `Assets/Scripts/MatchLog/` | Portable format, recorder, replay | Reconstruction disagrees with live play |
| `dotnet/NodeWar.Progression/` | Rating, arenas, catalog, matching rules | Persistent progression calculations change |
| `dotnet/NodeWarCloud/` | Cloud Code module and exported content | Server behavior or deployment artifacts change |
| `Assets/Data/`, `Assets/Settings/`, `Assets/Scenes/`, `Assets/Prefabs/` | Authored data and serialized wiring | Code works but the running scene does not |
| `Packages/`, `ProjectSettings/` | Unity dependencies and project configuration | Build/runtime configuration differs |
| `scripts/`, `.github/workflows/`, `docs/attesters/` | Build checks, guards, evidence | A verification claim needs to be reproduced |

Generated Unity root `.csproj` files are not the same as the hand-written projects in `dotnet/`. The latter link the actual `Assets/` source instead of copying it. Portable libraries target APIs compatible with Unity; test/server hosts can target .NET 8. Shared C# language settings and NUnit 3 preserve compatibility with Unity's test ecosystem.

## Use the right evidence for the claim

Run the portable suite from the repository root:

```powershell
dotnet test dotnet/NodeWar.sln
```

The solution covers simulation, lobby/wire/pure UI rules, view maths, match logs/replay, progression, and Cloud Code behavior. Outline tests have their own Unity test assembly and are not evidence supplied by the six-project .NET solution. See [test runner instructions](../skills/run-dotnet-tests.md) for the maintained project details and receipt procedure.

For Unity-dependent source outside those portable projects:

```powershell
powershell -NoProfile -File scripts/compile-check.ps1
```

The compile check uses installed Unity assemblies and package assemblies from `Library`. In a fresh worktree, the documented local setup can junction its `Library` to the existing project cache. That avoids a full import for type checking, but is not a reason to open two editors against one shared Library. The script also does not reproduce Unity's full assembly graph: runtime/editor assembly separation and scene/prefab wiring need separate verification.

For a determinism receipt, run only the simulation project with an absolute NUnit log path, then attest it. Do not send every project to the same receipt file; the outputs overwrite each other.

```powershell
$receipt = Join-Path (Get-Location) 'TestResults/results.xml'
dotnet test dotnet/NodeWar.Simulation.Tests/NodeWar.Simulation.Tests.csproj --logger "nunit;LogFilePath=$receipt"
powershell -NoProfile -File docs/attesters/hash_baseline.ps1
```

CI runs the solution, mechanical simulation guard, receipt, and attester on Linux and Windows. Both platforms passing pinned integer fingerprints provides evidence about reproducibility that a single local run cannot. A changed baseline is a rules finding until explained; it is not a CI nuisance to overwrite.

Do not enable parallel simulation fixtures while balance/path configuration is static. Independent state instances are insufficient isolation. Referee concurrency tests exercise its serialization gate; they do not establish that arbitrary simulation callers can run concurrently.

## Change the owner, then follow its dependents

| Change | Follow-through |
|---|---|
| New gameplay command | Validation, serializer/protocol review, producer UI/bot, replay, refusal tests |
| New state field | Initialization, hashing, reset/lifetime, compatibility, affected replay tests |
| Balance tuning | Authored balance, content hash, server export, era tables, readouts, balance tests |
| New district or suit | Enums/rules, balance, loadout/catalog identity, draft, art/UI mapping, export |
| New HUD action | Existing gesture/panel owner, eligibility feedback, command path, manual layout test |
| Backend record change | Stored-schema compatibility, fake and server rules, returned-state UI, account switching |
| New visual feedback | State/event source, event lifetime, pooling/teardown, renderer configuration |

These are dependency reminders, not replacements for [adding a feature](../adding-a-feature.md). Plan before changing `Simulation/`; keep edits in an isolated branch/worktree; do not merge main automatically. Unity scenes, prefabs, and metadata must be edited through the editor rather than hand-written YAML.

## Debug by boundary

If the UI shows the wrong production value but resources accumulate correctly, start with the readout's state/balance interpretation. If both peers agree on an incorrect outcome, start with rules and configuration. If they disagree, compare compatibility identity, setup, command ordering, and hash checkpoints before looking at animation. If a correct local replay is refused remotely, check exported balance availability and simulation identity before changing the referee.

For backend failures, distinguish interface/fake behavior, real authentication, Cloud Code deployment, and protected storage. Unit tests can prove rule decisions without proving service permissions or account browser flows. Record manual checks honestly rather than extending a compile result into a runtime claim.

## Documentation is part of the system, with limited authority

`CLAUDE.md` owns project instructions and routing; `AGENTS.md` is its Codex counterpart. Existing architecture/rule docs and this atlas link source owners. Freshness tooling can flag changed sources, but a timestamp cannot establish that prose is accurate. Do not stamp `verified:` or `verified_at_commit` simply because a merge or test passed.

Notion owns current and planned work. Keep PR conflict tables, branch tips, and temporary integration status in PRs rather than this guide. Keep architectural consequences here so a future engineer can understand the code without reconstructing a task board.

## Source landmarks

[NodeWar.sln](../../dotnet/NodeWar.sln) | [compile-check.ps1](../../scripts/compile-check.ps1) | [determinism.yml](../../.github/workflows/determinism.yml) | [CLAUDE.md](../../CLAUDE.md) | [adding-a-feature.md](../../docs/adding-a-feature.md)
