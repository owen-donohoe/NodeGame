---
type: Executor Skill
title: Run the simulation suite without Unity
description: How to run Assets/Tests/EditMode/ through the plain .NET projects in dotnet/, the receipt it produces, and why this is the runner CI uses.
tags: [testing, executor, dotnet, ci, receipt]
generated: { by: claude-opus-5, at: 2026-08-31T00:00:00Z }
verified:
  # full history: docs/verification-log.md
  - { by: claude-sonnet-5-5, at: 2026-10-10T17:16:17Z }
verified_at_commit: 7a0cf4f0c658b5c63b51415ca2b4c2bd06169836
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
  - id: tests-loadout-screen
    resource: dotnet/NodeWar.Lobby.Tests/LoadoutScreenModelTests.cs
    title: Passive preview, explicit Use, capacity refusal and chosen-only clear
  - id: tests-workshop-session
    resource: dotnet/NodeWar.Lobby.Tests/WorkshopInventorySessionTests.cs
    title: Inventory request tokens, busy gate and stale-response policy
  - id: tests-loadout-info
    resource: dotnet/NodeWar.Lobby.Tests/LoadoutInfoModelTests.cs
    title: Base-villager preview deltas and tree comparison compatibility
  - id: tests-district-info
    resource: dotnet/NodeWar.Lobby.Tests/DistrictInfoFactsTests.cs
    title: Static district facts from supplied balance without live bindings
  - id: tests-skin-band
    resource: dotnet/NodeWar.Lobby.Tests/SkinBandMathTests.cs
    title: Looping skin band identity, snapping, rebasing and allocation checks
  - id: tests-loadout-capacity
    resource: dotnet/NodeWar.Lobby.Tests/LoadoutCapacityTests.cs
    title: Canonical arena capacities, local completeness, resizing and wire refusals
  - id: tests-loadout-match-setup
    resource: dotnet/NodeWar.Lobby.Tests/LoadoutMatchSetupTests.cs
    title: Per-player grants, board identity, setup equality and Equip/Respawn behavior
  - id: tests-loadout-migration
    resource: dotnet/NodeWar.Lobby.Tests/LoadoutMigrationTests.cs
    title: Legacy choices, type-indexed equipment and capacity-preserving saved aliases
  - id: workflow
    resource: .github/workflows/determinism.yml
    title: The determinism CI gate
  # The test files themselves. Declared because this document states how many
  # cases to expect, and that number is only checkable against them.
  - id: tests-bank-production
    resource: Assets/Tests/EditMode/Tests/StorehouseBankTests.cs
    title: Storehouse self-production and capture lifecycle with determinism variants
  - id: tests-bank-collection
    resource: Assets/Tests/EditMode/Tests/BankCollectionTests.cs
    title: D3 progressive collection, lock and loot with independent determinism variants
  - id: tests-node-command-wire
    resource: dotnet/NodeWar.Lobby.Tests/NodeCommandWireTests.cs
    title: Stable six-int node command payloads and packet refusals
  - id: tests-node-command-log
    resource: dotnet/NodeWar.MatchLog.Tests/MatchLogFormatTests.cs
    title: Binary command round trips including ForgeMinion and Collect, and unknown command refusal
  - id: tests-storehouse-migration
    resource: dotnet/NodeWarCloud.Tests/StorehouseMigrationTests.cs
    title: Catalog retirement and saved Storehouse migration
  - id: tests-pier-gate
    resource: Assets/Tests/EditMode/Tests/PierGateTests.cs
    title: D4 gate, highway, routing and leg-clock scenarios with determinism variants
  - id: tests-order-presentation
    resource: dotnet/NodeWar.View.Tests/OrderPresentationTests.cs
    title: Interrupted gate intent and read-only latched movement clock
  - id: tests-bank-actions
    resource: dotnet/NodeWar.Lobby.Tests/BankActionTests.cs
    title: D5 bank action model queues only commands and its reasons match the simulation
  - id: tests-bank-lockstep
    resource: dotnet/NodeWar.Network.Tests/BankRulesLockstepTests.cs
    title: E4 scripted district health, banks and mobile minions under a lossy link with rollback
  - id: tests-bank-replay
    resource: dotnet/NodeWar.MatchLog.Tests/BankRulesReplayTests.cs
    title: E4 version-4 script replay, refusals and midpoint copy
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
  - id: bank-acceptance-fixture
    resource: dotnet/BankRulesFixture.cs
    title: Scripted E4 hourglass acceptance and exact witnesses
  - id: timeline-metrics-tests
    resource: dotnet/NodeWar.BalanceRig.Tests/TimelineMetricsTests.cs
    title: Metrics with minions excluded from idle body and stall counts
  - id: district-health-tests
    resource: Assets/Tests/EditMode/Tests/DistrictHealthTests.cs
    title: District health drain, regeneration and passive-effect gating
---

# Run the simulation suite without Unity

The shared simulation test cases from [run-editmode-tests](run-editmode-tests.md), executed by
`dotnet test` instead of Unity's Test Runner. No Editor, no licence, no Windows requirement.

The solution holds eight test projects. Run everything for pass/fail:

```
dotnet test dotnet/NodeWar.sln
```

The table below records the historical **3788-case** inventory before P1.1; it is not
a receipt for the current branch. P1.1 adds 11 lobby cases covering two chosen slots,
old three-suit packet/save normalization, type-indexed equipment retention and fixed
arena-1 display locks at every rank. At `dae33286`, both the solution test command and
stock compile check were blocked before compilation by MSBuild access denial to
`C:\Users\owend\AppData\Local\Microsoft SDKs` (MSB4184). Re-run both outside that
sandbox to obtain current pass counts; no new passing total is claimed here.

P1.3 adds 19 lobby cases in `UnlockStateTests` and `LoadoutChoicesTests`, linked
explicitly alongside the two pure sources. They cover parent/highest-arena acquisition,
current-arena fieldability, exact ownership, skin unions/filtering, stable choice
ordering, preview/display safety and no variants for districts or non-tree suits.
Both required commands remain blocked before compilation by the same SDK access
denial at this code commit; these additions are not a passing-test receipt.

P1.4 adds 14 lobby cases: six in `LoadoutInfoModelTests` and eight in
`DistrictInfoFactsTests`, with explicit links to the pure preview helpers and
`DistrictFallback`. They cover base-villager comparisons, signed improvements,
bonus HP and absolute costs, variant lookup/fallback, parent-comparison compatibility,
active district descriptions, supplied production/bank/health/action/equip tuning,
Fortress and Infirmary potential, and absence of live state/action bindings.
At `704e4336`, both required commands were attempted once and stopped before
compilation with the SDK access denial above. The lead must run both commands;
no passing-test receipt or live-sheet/Editor verification is claimed.

P1.2 adds four cases: Lobby `PreviousProtocol_IsRejected`, Cloud
`PreviousProtocol_IsRejected` and `CurrentProtocol_IsAccepted`, and MatchLog
`ThreeChosenSuitHistory_RoundTripsWithoutTrimming`. They pin protocol-5 refusal,
protocol-6 allocation and historical resolved Warrior-plus-three-choice arrays
with type-indexed eras. At `e935573c`, the solution test command and stock compile
check were each attempted once and blocked before compilation by the SDK access
denial above. The lead must run both, including the existing replay/referee suites;
these additions are not a passing-test receipt.

P1.7 adds 23 lobby cases in `SkinBandMathTests`, explicitly linked with the
UnityEngine-free `SkinBandMath` source. They cover signed wrapping, increasing-index
midpoint ties, empty/single choices, 13-slot identity, symmetric scale/fade,
large-count neighbors, rebasing, buffer reuse and a warmed zero-allocation check.
At `08bb45e8`, both required commands were attempted once and blocked before
compilation by the SDK access denial above. The lead must run both; these additions
are not a passing-test receipt and do not verify UI gestures or Editor wiring.

P1.2b adds capacity and save-migration cases covering canonical low 2/2 and
high 3/5 shapes, default/offline fallback, promotion/demotion, copied equipment,
editor fill/clear/drop, repeated save round trips, both-seat wire round trips
including blanks, refused noncanonical raw pairs and effective-slot completeness.
The shared capacity policy and new test file are explicitly linked in the Lobby
project. Existing low-mode assertions now distinguish effective counts from
maxima; legacy 3/2 wire packets are refused rather than migrated, and locks stop
at arena 2. At `c5cdc2e5`, both required commands were attempted once and blocked
by SDK access denial before compilation. The lead must run both and verify Editor
imports/profile flow; no passing receipt or launch-blocking integration is claimed.

P1.2c adds 19 lobby cases in `LoadoutMatchSetupTests`, explicitly linked with
the pure `LoadoutMatchSetup` source. They cover low base-plus-chosen pools,
high chosen-only pools, Warrior selected once or absent, both mixed seats,
blank-slot mode, unchanged board pools/hash, invalid shapes/IDs/duplicate choices,
MatchFactory input retention and equal tick-0 hashes, refused undrafted Warrior,
accepted drafted Guardian and unsuited paid Respawn. At `95da9bdd`, the solution
test command and compile-check were each attempted once and blocked by MSB4184
before compilation. The lead must run both, generate the new asset's metadata,
and manually verify mixed-seat launches in Unity; no passing receipt is claimed.

P1.2e adds 11 lobby cases in LoadoutCapacityTests: Warrior/high offers, low grant
exclusions, incomplete high-mode retention, local-only promotion/demotion,
sanitization blocking and detached equipment snapshots. LoadoutRules now supplies
pure offering/local-editor predicates; the Lobby csproj already links both it and
LoadoutEditor, and no other test csproj links LoadoutRules. No new source links or
asset metadata are needed. Both required commands were attempted once and blocked
before compilation by MSB4184 SDK access denial. The lead must run the full solution
and compile-check, plus Editor checks for chips/locks, promotion persistence,
complete/short decks, fresh-rank launch, cancellation and both mixed-arena seats.
These added cases are not a passing-test receipt or a UI event-delivery check.

P1.5 adds 10 lobby cases in LoadoutScreenModelTests, explicitly linked with
its pure LoadoutScreenModel source. They cover passive preview at both canonical
capacities, first-empty placement and full refusal for both sides, duplicate Use
without unequipping, chosen-only clearing, equipment retention, source isolation,
blank preview and denied-use refusal. At dd757cf8, both required commands were
attempted once and blocked before compilation by MSB4184 SDK access denial.
The existing Library junction was verified. The lead must run both commands and
check portrait layout, pointer delivery, locks, sorting, preview/Use states,
base-villager histogram and clipping in Unity; no passing receipt is claimed.
Skins is a visible control with a no-op action pending P1.8; Variants opens at P1.6.

P1.6 adds 8 lobby cases in WorkshopInventorySessionTests, explicitly linked with
its pure WorkshopInventorySession source. They cover the token policy only: a
close and reopen, or a switch of inspected item, stales an earlier token; the single
busy gate refuses a second request; a completion after Close still releases the
gate; and a matching token is accepted. Task wiring and the Variants sheet are
not covered. At 7a0cf4f0 the full solution passed 3966 cases (Lobby 758) and
compile-check reported clean. Check in the Editor: two panels never stack,
header and rows stay visible, and the equipped variant survives a page reload.

The explicit Network `Sweep` and Cloud
`NewExportMatchesClientBalanceAndCatalog` are outside these passing totals; run the
release-content gate with a freshly migrated client balance/export before shipping.

| Project | Cases | Covers |
|---|---|---|
| `NodeWar.Simulation.Tests` | 720 | `Assets/Tests/EditMode/Tests/`: the version-4 determinism baseline, link weights, movement, production, combat fixes, `MatchFactory`, terrain maps and draft legality, `MatchSetup`, sticky orders, restore, the capture bonus, tick order, eras, breach/tempo, paid respawns, resource caps, Recruit/SetAutoRecruit, Town rewards, Barracks equipping, Infirmary healing and workers, Fortress resistance and district-health drain/regeneration/passive gating, Workshop forging and mobile minion lifecycle, Storehouse self-production/re-claim, progressive collection and lock/Restore lifecycle, remaining-bank capture loot paid once, Pier gates and retreat, latched leg clocks and asymmetric reversal, owner highway, physical-tick routing and current gate estimates, command refusal, vocabulary compatibility and balance hashing; also the dotnet-only balance-asset text guard |
| `NodeWar.Lobby.Tests` | 758 | loadout wire format (eras and skins included), loadout editor rules, Workshop era chips, suit trees, item tints, families, the in-match command checks, handshake and emote packets, game settings and input bindings, controls view model, arena rank display, trophy bar, match history rows, the ranked queue presenter and rendezvous, draft loadout packets, the node-action model (Recruit, Repeat, Fortress upgrade), `MatchSetup`/`MatchSetupAck` packets, the pre-rename loadout and profile compatibility cases, `DistrictMigration` of saved decks, and node-command round trips/refusals including ForgeMinion=8, Collect=9 start/cancel and current Market packet refusal, and the bank action model (collection only queues commands; Workshop forging uses shared node-action eligibility; lock and storage reasons match the simulation) |
| `NodeWar.View.Tests` | 1580 | the UnityEngine-free view maths: camera POV, indicator placement, route reveal, emote rate limit, resource rings/bars and shared full phase, production readout, breach walls, playtest debug, sheet resources, board art and board framing, terrain presentation, order presentation (including amber Pier intent and read-only latched interpolation), the district fallback descriptors and art keys, district-health segment visibility/layout, Storehouse bank pips (a row centred above the node along camera-up), grey minion tint, Storehouse-only bank sheet height and Workshop/Storehouse fallback art, icon/context resolution and usage, required UXML names, draft handover |
| `NodeWar.MatchLog.Tests` | 103 | the match log format (round trip, unknown chunks, truncation), the recorder, `MatchReplay`, ERAS and SKINS, BOARD_V2 and SETUP (round trip, conflicting or missing chunks, terrain replay, the core-rules and bank-rules replays: the version-4 script replays every command, a version-3 log and tampered hashes are refused, a midpoint copy replays identically), node-command round trips (including ForgeMinion=8 and Collect=9 start/cancel) and unknown-command refusal |
| `NodeWar.Network.Tests` | 128 | `LockstepCore` (the networked tick driver) and `InputDelayController` under an in-memory lossy link: clean, loss, burst loss, duplication, reordering, latency, jitter, outages, frame spikes and a late start, each judged against the same match on a perfect link; the adaptive input delay; the lifecycle of an ended or paused core. `SweepTests` is explicit (`--filter "Category=Sweep"`) and prints the numbers behind the tuning constants; a terrain-board lockstep scenario the 1,500-tick core-rules acceptance on `hourglass-01-acceptance` and the bank-rules acceptance (Workshop forging, minion collection/death, Storehouse drain/lock/Restore/capture payout, Fortress health/aura recovery, remote Collect start/cancel, Pier gate, Recruit/Town) on `hourglass-01-banks-acceptance`, each over a lossy link with rollback |
| `NodeWar.Progression.Tests` | 149 | Glicko-2, RR, arenas, catalog validation, era unlocks, match settlement |
| `NodeWar.BalanceRig.Tests` | 53 | the headless balance rig, scenario validation, repeatable execution against an exported JSON balance, its diagnostics and timeline metrics (including banks/gates and minions excluded from idle-body and Barracks-stall metrics, while living minions remain in final population totals), and setup compatibility with the shared map |
| `NodeWarCloud.Tests` | 475 | the Cloud Code module: player state, accounts, catalog, inventory, Equip and the equipped clamp, the referee and its balance catalog, match records and their store, Matchmaker allocation, map and era eligibility, persistence vocabulary, `ReleaseContentTests` (a fresh balance export against the client balance and catalog, and v4 content: active types, Storehouse eras, sim-3 records and the refused sim-3 allocation), district migration of stored inventory including Market13 to Storehouse18 retirement and ownership/era preservation, match reporting and settlement, match history, the rank table, the ranked queue fake and status mapping, the report-service fake, ranked rendezvous, confirmation and leaving |

## E4 acceptance evidence

At `36c57c73`, the full solution passed all 3791 cases, and `sim-guard.ps1` was clean.
The bank acceptance runs 1500 ticks, witnessing every required transition before
comparing per-tick hashes/population and at least 29 checkpoints per peer. Loss,
duplication, reordering and an outage exercise rollback against the perfect reference.
The binary log round-trip includes ForgeMinion and Collect start/cancel, refuses v3,
rejects checkpoint/final-hash tampering and restores an independent midpoint copy.
The `_Determinism` twins run from independent initial states.

Stock `compile-check.ps1` reports four missing Minion/Workshop enum references from
stale Editor DLLs. A temporary copy with the explicit worktree root and current
.NET-built Simulation/MatchLog DLLs compiles cleanly (250 sources, 330 references).
Neither result verifies scene/prefab wiring or Play Mode visuals.

D35 paired rig smoke: use export `-1242191275`, map `hourglass-01`, seeds 4100-4101,
1500 ticks, zero delay, both seats, loadout Barracks/Workshop/Storehouse/Fortress:

```powershell
dotnet run --project dotnet/NodeWar.BalanceRig --no-build -- --matches 2 --seed 4100 --cap 1500 --swap-seats on --loadout Barracks,Workshop,Storehouse,Fortress --balance dotnet/NodeWarCloud/NodeWarCloud/Balances/-1242191275.json --out "$env:TEMP\nodewar-e4-paired.csv"
```

All four matches capped with no breaches. **Current bot limitations:** it does not
recruit, upgrade, forge or remotely collect, so this checks repeatable rig execution
on the new districts and map; it supplies no balance conclusion.

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

Expect 720 passed, and both version-4 pinned fingerprints from
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
