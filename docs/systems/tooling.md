---
system: tooling
summary: Build checks, tests, deterministic receipts, documentation guards and editor tools used to verify changes.
sources:
  - .github/workflows/
  - Assets/Scripts/Backend/Editor/
  - Assets/Scripts/Editor/TestBridge.cs
  - Assets/Scripts/Game/Simulation/NodeWar.Simulation.asmdef
  - Assets/Tests/
  - Assets/UI/Editor/UIToolkitDraftSetup.cs
  - Assets/UI/Editor/UIToolkitHUDSetup.cs
  - Assets/UI/Editor/UIToolkitLobbySetup.cs
  - docs/attesters/
  - docs/computations/
  - dotnet/NodeWar.sln
  - scripts/
verified: e9255fad
---

# Tooling

## Purpose
Separate evidence about rules, compilation and Unity wiring so a passing check is not mistaken for broader coverage. Automate mechanical boundaries and freshness signals while keeping semantic review explicit.

## Interface
`dotnet test dotnet/NodeWar.sln` builds/runs the Editor-free solution. Project counts and exact receipt commands live in [run-dotnet-tests](../skills/run-dotnet-tests.md); do not maintain another count here.

| Test project | Coverage |
|---|---|
| NodeWar.Simulation.Tests | Shared Simulation rules, construction, hashing/copying, determinism fixtures, economy and movement. |
| NodeWar.Lobby.Tests | Loadouts/wire shapes, command eligibility, settings/bindings, lobby models and draft/handshake packets. |
| NodeWar.View.Tests | UnityEngine-free gesture/view/layout/art/POV/route maths and UI resource contracts. |
| NodeWar.MatchLog.Tests | Binary format, recorder, identity refusal, replay, corruption and scripted acceptance. |
| NodeWar.Network.Tests | Lockstep/delay under loss, duplication, reordering, outages and rollback against a perfect reference. |
| NodeWar.Progression.Tests | Rating, rank points, arenas, catalog, unlocks and settlement rules. |
| NodeWar.BalanceRig.Tests | Headless setup, repeatability, paired seats and diagnostic metrics. |
| NodeWarCloud.Tests | Cloud services, persistence, inventory, matchmaking, referee, settlement and release-content checks. |

`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/compile-check.ps1` type-checks Unity-facing source against real Unity assemblies without running the Editor. A fresh worktree needs its ignored Library junction first: from its root, `cmd /c mklink /J Library C:\Dev\NodeGame\Library`. It resolves project/version/source paths relative to the script; stale Unity-generated assemblies can still affect references.

`scripts/sim-guard.ps1 [-Path ...]` returns 0/1 for mechanical determinism violations. `scripts/okf-stale.ps1` reads source/stamp ancestry and staged changes, classifying REVIEW declarations, touched implementation and expired documents. `okf-stale-hook.ps1` exposes session-start warnings and the committing agent's review/stamp obligation. The checker includes the Obsidian vault and exports findings with `-JsonPath`. `scripts/hooks/pre-commit`, installed via `core.hooksPath`, blocks simulation violations for staged Simulation C# changes and reports freshness advisories without blocking.

`scripts/doc-budget.ps1 [-Only name,...]` checks card frontmatter, ordered headings, body budget and history exclusions; global mode also checks routing/listing and shared boundary text. `scripts/context.ps1 -Systems name,... [-Out path]` emits full requested cards, neighbours' Purpose/Interface and the determinism contract for simulation systems; `-List` inventories cards.

`scripts/run-tests.ps1` runs batch Unity EditMode with Editor closed; `run-tests-live.ps1` drives an open Editor. `Assets/Scripts/Editor/TestBridge` watches `TestResults/trigger.txt`, runs `TestRunnerApi`, writes NUnit3 `results.xml` then `done.txt` carrying the same GUID. A stale completion cannot satisfy a new request.

`docs/computations/determinism-baseline.md` defines sanctioned fixtures and pins; `docs/attesters/hash_baseline.ps1` checks required passing cases and receipt freshness against committed/working source. Generate the receipt from the Simulation test project alone with absolute `LogFilePath=<repo-root>/TestResults/results.xml`; never apply that logger to the entire solution.

## Connects to
- `simulation`: guards forbidden constructs, state/hash/copy behavior and platform-identical baselines.
- `board`: validates construction/maps and hashes/configuration exports.
- `economy`: balance coverage/release-content checks compare client tuning and server export.
- `commands`: serializer/log compatibility and refusal tests guard action evolution.
- `netcode`: lossy-link scenarios verify confirmed results, rollback and delay policy.
- `match-log`: replay/format tests and deterministic recordings establish reproducibility.
- `cloud`: Cloud test project and export tools cover referee/catalog compatibility.
- `progression`: pure rules and settlement tests run without Unity.
- `input`: synthetic traces and settings migrations cover gesture contracts.
- `world-view`: pure maths and separate Outline assembly tests cover geometry/ID lifecycle.
- `match-ui`: compilation/resource checks complement manual event/layout verification.
- `bots`: rig tests and exported-balance CLI runs measure repeatable scripted behavior.
- `ui-art`: editor builders generate shared district visuals and UI scaffolding.

## Invariants
Tests compile shared source, not divergent copies. Keep Simulation at netstandard2.1, C# compatibility and NUnit 3.x aligned with Unity. Do not parallelise fixtures using process-global simulation settings. Never repin platform-divergent hashes to make CI green. Tests/type-checks cannot establish scene/prefab wiring; never hand-edit scene, prefab or metadata YAML.

## How it works
`.github/workflows/determinism.yml` runs sim-guard, the Release solution suite, a separate Simulation receipt run and the attester on Windows/Linux, retaining both receipts even on failure. Full git history supports freshness checks. Sanctioned baseline tests assert exact fingerprints and simulation-version equality; attestation additionally checks that required cases ran and the receipt postdates watched code.

Unity and .NET link the same simulation tests recursively. Other projects explicitly link their pure sources/tests where configured; new files may need project entries. Outline tests under `Assets/Tests/EditMode/Outline/` have their own Unity assembly. Explicit network Sweep and fresh client/export release gates need deliberate invocation; ordinary green totals do not imply they ran.

Editor menus under Tools > Node War build UI Toolkit Lobby/HUD/Draft wiring, create District Visuals, migrate BoardConfig data and generate/export backend catalog/balance. Use these through Unity. Export content-addressed server data and check client/catalog parity before shipping.

[drive-the-editor](../skills/drive-the-editor.md) and [run-editmode-tests](../skills/run-editmode-tests.md) define Unity CLI/test procedures. Start with `unity status`; only a ready Editor is usable. Specify project path when ambiguous. If unavailable, continue with .NET/compile checks and flag wiring as manual; Safe Mode requires fixing compilation, never YAML fallback.

`merged-docs.yml` runs after PR merges to main or manual dispatch. `merge-doc-review.ps1` updates `notes/Merge review.md` from freshness findings; CI commits the dashboard and maintains one tracking issue. Neither changes explanatory prose or stamps; the committing agent reviews sources and updates cards/notes, preserving personal sections.

## Decisions
- Two OS legs test exact output, not merely successful execution.
- Advisory freshness permits the required follow-up review stamp after a source commit has a SHA.
- Project-only receipts prevent multiple loggers overwriting the same file.

## Pressure points
doc-budget blocks in CI (`docs.yml`) and in the pre-commit hook, but the hook is opt-in per clone (`git config core.hooksPath scripts/hooks`). Regex guards cannot prove sort totality, pass order, hash registration or boundary compliance. Live Editor tests can use stale assemblies. Attester freshness checks skip unavailable commit/source information rather than failing closed. Hook installation and explicit release gates remain operational dependencies.

## Change recipes
- Change rules: run semantic determinism review, relevant/new tests, full solution and required baseline checks; coordinate version/pins when behavior changes.
- Add pure tests: confirm Unity/.NET inclusion and meaningful assertions, then run the owning project/solution. Unity-facing edits also need compile-check.
- Change docs: budget-check cards; when source changes leave REVIEW, reread against the diff and make the required follow-up reverify commit. Stamp only reviewed material.
- Change tooling: test success/failure paths, stale receipts and worktree-relative paths; preserve distinct runtime/wiring coverage claims.

## Tests
Tooling evidence includes solution tests, guard exit codes, attester refusal/freshness checks and per-card budget checks. Editor builders, CLI availability, real render/input delivery and scene wiring still require connected-editor/manual verification.
