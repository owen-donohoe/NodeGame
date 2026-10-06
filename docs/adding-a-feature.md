---
type: Checklist
title: Adding a Feature — Checklist
description: The 11-step order of operations for any new feature, from simulation state through view, test, and desync check.
tags: [process, checklist, simulation, testing]
generated: { by: human:DonohoeCUA, at: 2026-08-30T17:15:16-04:00 }
verified:
  # full history: docs/verification-log.md
  - { by: claude-opus-5, at: 2026-09-02T00:00:00Z }
  - { by: claude-opus-5-5, at: 2026-09-29T18:00:00Z }
  - { by: claude-opus-5-5, at: 2026-09-30T07:00:00Z }
  - { by: gpt-6-sol, at: 2026-09-30T07:00:00Z }
  - { by: claude-sonnet-5-5, at: 2026-10-03T00:41:16Z }
  - { by: gpt-6-sol, at: 2026-10-06T01:06:51Z }
  - { by: gpt-6-sol, at: 2026-10-06T01:08:47Z }
verified_at_commit: 673cc4b9
status: stable
sources:
  - id: sim-state
    resource: Assets/Scripts/Game/Simulation/SimulationState.cs
    title: NodeData, VillagerData, PlayerData, SimulationState
  - id: hasher
    resource: Assets/Scripts/Game/Simulation/SimulationStateHasher.cs
    title: SimulationStateHasher.ComputeHash
  - id: commands
    resource: Assets/Scripts/Game/Simulation/Commands.cs
    title: CommandType and GameCommand
  - id: command-processor
    resource: Assets/Scripts/Game/Simulation/CommandProcessor.cs
    title: CommandProcessor.ProcessCommand and ProcessEquipCommand
  - id: sim-loop
    resource: Assets/Scripts/Game/Simulation/GameSimulation.cs
    title: SpawnBonusVillagers, AssignAllCombatTargets, SimulateTick
  - id: balance
    resource: Assets/Scripts/Game/Simulation/GameBalanceData.cs
    title: GameBalanceData tuning fields
  - id: tests
    resource: Assets/Tests/EditMode/Tests/DeterminismBaselineTests.cs
    title: EditMode determinism tests
  - id: test-runner
    resource: scripts/run-tests.ps1
    title: EditMode test runner
  - id: match-factory
    resource: Assets/Scripts/Game/Simulation/MatchFactory.cs
    title: MatchFactory, where a new field gets its starting value
  - id: sim-version
    resource: Assets/Scripts/Game/Simulation/SimulationVersion.cs
    title: SimulationVersion.Current and bump policy
  - id: balance-hasher
    resource: Assets/Scripts/Game/Simulation/BalanceHasher.cs
    title: Balance fingerprint separate from state
  - id: balance-tests
    resource: Assets/Tests/EditMode/Tests/BalanceHasherTests.cs
    title: Coverage of balance fields
  - id: draft-manager
    resource: Assets/Scripts/Game/Core/DraftManager.cs
    title: Timeout placement fallback
  - id: game-manager
    resource: Assets/Scripts/Game/Core/GameManager.cs
    title: State allocation, drafted setup and new villager views
  - id: lockstep
    resource: Assets/Scripts/Game/Network/LockstepCore.cs
    title: Desync checkpoint timing
  - id: input-serializer
    resource: Assets/Scripts/Game/Network/InputSerializer.cs
    title: Wire layout and build identity comparison
  - id: match-log-format
    resource: Assets/Scripts/MatchLog/MatchLogFormat.cs
    title: Recorded identity, board and commands
  - id: command-layout-test
    resource: dotnet/NodeWar.MatchLog.Tests/MatchLogFormatTests.cs
    title: Command field layout guard
  - id: dotnet-runner
    resource: docs/skills/run-dotnet-tests.md
    title: Editor-free suite and receipt commands
  - id: project-rules
    resource: CLAUDE.md
    title: Scene, prefab and metadata editing boundary
---

# Adding a Feature — Checklist

Work through in order. Answer each question honestly before moving on —
skipping a "yes" answer is how desyncs and silent bugs get introduced.

1. **Does it affect game state?**
   If the feature changes anything a player can observe about the match
   (resources, positions, ownership, HP, timers), it must live in
   `Assets/Scripts/Game/Simulation/`, on `NodeData`, `VillagerData`,
   `PlayerData`, or `SimulationState`. If it's purely cosmetic, skip to
   step 8.

2. **Does it need a new field on simulation state?**
   - Add it to the correct struct/class (`NodeData` / `VillagerData` /
     `PlayerData` / `SimulationState`).
   - Type must be `int`, `bool`, an existing enum, or an array of one of
     those — no `float`/`double`, no `UnityEngine` types.
   - Set its initial value everywhere that entity is constructed
     (`MatchFactory.BuildNodes` / `InitializePlayers` /
     `InitializeVillagers`, the one starting board the live game, the
     referee and headless runs share, and anywhere else new instances are
     created mid-match).
   - **Add it to `SimulationStateHasher.ComputeHash` now, not later.**
     Every new mutable field on `NodeData`, `VillagerData`, `PlayerData`,
     or `SimulationState` must be included, in the same order/section as
     its siblings. Skipping this makes desync detection blind to bugs
     involving the field.
   - **Add it to `SimulationState.CopyFrom` too** (the rollback copy
     `LockstepCore` restores after a speculation; arrays are cloned, scalars
     assigned). `SimulationStateCopyTests` fails on a field it does not copy.
   - If it changes what the same inputs produce, bump
     `SimulationVersion.Current` in the same commit (see
     `docs/simulation-rules.md`). Neutral extension fields can use
     conditional hashing, as the era and v2 breach fields do; specify
     the neutral value explicitly (`nextBreacherID` is -1, while
     `breachBar` and `paidRespawns` start at 0).
     That preserves old hashes; avoiding a version bump also requires
     unchanged results for those existing inputs, as with eras.

3. **Does it need a new player-triggerable action?**
   - Add a `CommandType` in `Commands.cs` if no existing type fits.
   - Add a case in `CommandProcessor.ProcessCommand` that validates
     ownership/state/cost before mutating anything (follow
     `ProcessEquipCommand`'s shape: ownership check → state check → cost
     check → apply).
   - Capture the input in `Input/` (`CommandSystem`, and `BotPlayer` if
     the bot should be able to do it too) and push it through
     `InputBuffer`, with `issuedOnTick` set from `SimulationState.tickCount`
     (as `CommandSystem` and `NodeSheetContent.Send` do). Never mutate
     `SimulationState` directly from `Input/`, `UI/`, or `View/`.
   - If the command needs new data on the wire, extend `InputSerializer`
     (or `DraftSerializer` for draft-phase actions) — both peers must
     encode/decode it identically.
   - Any wire layout change bumps `ProtocolVersion.Current`
     (`Assets/Scripts/Backend/Shared/ProtocolVersion.cs`; `InputSerializer.ProtocolVersion`
     aliases it) in the same commit. A `GameCommand` change also needs a new TICKS tag in
     `MatchLogFormat` (`GameCommandLayout_RequiresCoordinatedSerializerChanges`
     detects changes to the command's field layout; it does not check
     that a new tag was added).

4. **Does it involve randomness?**
   - Never use `UnityEngine.Random` or anything seeded from wall-clock
     time inside `Simulation/`.
   - Derive a seed from already-replicated state (tick count, player ID,
     entity ID) — see `DraftManager.HandleTimeout`'s fallback
     `turnNumber * 7919 + activePlayer * 31` pattern when no valid parked
     placement exists. The chosen placement is sent to the other peer.
   - If the feature needs randomness mid-match (after `SimulationState`
     exists), any RNG state must itself live on `SimulationState` and
     only advance inside `SimulateTick`.

5. **Does it iterate collections?**
   - Only arrays or `List<T>`, iterated in index order. No
     `Dictionary`/`HashSet` iteration over anything that affects
     simulation results.
   - Any sort needs a total-order comparator with an ID tiebreaker —
     verify no two elements can compare equal (see
     `GameSimulation.AssignAllCombatTargets`).

6. **Does it change array sizes at runtime (spawning new entities)?**
   Follow the `GameSimulation.SpawnBonusVillagers` pattern:
   - Allocate a new, larger array; copy existing entries into it; append
     new entries at the end; assign the new array back onto
     `SimulationState` (e.g. `state.villagers = newArray`).
   - Enforce any relevant cap (see `bal.maxVillagersPerPlayer`) before
     appending.
   - On the `Core/` side, let `GameManager.Update` detect the length
     change (`state.villagers.Length > trackedVillagerCount`) and spawn
     matching view objects only for the new range
     (`GameManager.SpawnNewVillagerViews`) — don't respawn the whole set.
     A rollback can also shrink the array; `GameManager.OnRolledBack`
     drops the views past the restored count, so any other per-entity view
     object must tolerate that.

7. **Does it change the tick loop itself?**
   - Confirm where it fits in the canonical order: `movement → combat →
     claiming → production → healing → respawns → win-check` (as
     documented on `GameSimulation.SimulateTick`).
   - Insert at the correct, justified step — do not append a new step at
     the end by default, and do not reorder existing steps.
   - Claiming begins with `TickBreach` before `TickClaiming`; Rampart
     bonuses follow movement, and post-combat resume follows win-check.
     Refresh derived `nextBreacherID` last, after all mutations this tick.
     Version 2 checks only new breaches against the current threshold;
     simultaneous losses cancel and a sudden-death drop alone is not a loss.

8. **Is it purely visual (no simulation involvement)?**
   - Confirm it only reads `SimulationState` — no writes.
   - Wire any player interaction back through `InputBuffer` as a
     `GameCommand`, exactly like any other input.
   - Use `ITickProvider.TickAlpha` for interpolation so the feature works
     identically under `TickRunner` (local) and `LockstepCore`
     (networked).
   - **Ask which UI it belongs in before writing any of it.** Three trees
     are live and which one draws is a scene value, not a code value —
     `docs/architecture.md`, "Where the UI lives". A screen with a toggle
     needs the feature in whichever tree is *on*, or in both, and a change
     to only the off one is a change nobody sees.
   - A phase drawn by either stack goes behind a presenter interface
     (`IDraftPresenter`, `ICountdownPresenter`) rather than a concrete
     class, so the phase machine keeps the rules and neither stack has to
     know the other exists.

9. **Does it add a new tunable number?**
   Put it in the `GameBalanceData` or `BoardConfigData` held by the
   `GameBalance` / `BoardConfig` asset — don't hardcode it or add a new
   plumbing path. `MatchFactory.Configure` installs balance and path
   multipliers; `Build` / `Fill` consume the board data. A number that
   belongs to one suit or district goes on its
   `SuitStats` / `DistrictStats` entry, so it can differ by era, and is
   read through `GetSuitStats(type, era)` / `GetDistrictStats(type, era)`.
   Add a balance field to `BalanceHasher` (a test walks `GameBalanceData`,
   `SuitStats` and `DistrictStats` and fails if you do not), then export
   the balance for the server (`Tools > Node War >
   Backend > Export Balance For Server`) so the referee can verify matches
   played on it. `BalanceHasher` does not hash `BoardConfigData`; the
   board is recorded separately in the match log. The balance content
   hash is compared in the handshake, not folded into `SimulationStateHasher`.
   Preserve absent-field behaviour: nonpositive resource caps are uncapped,
   absent tempo axes use 100%, and a disabled breach channel keeps the legacy
   fixed-threshold path. Validate schedule lengths/order, positive percentages
   (including below 100) and safe production durations. Timer scaling includes
   the current tick and carries production remainders; cap every resource gain
   and starting value, not just the most visible production path.

10. **C# conventions.**
    - Keep `[SerializeField]` fields in the same file as their
      `MonoBehaviour` — don't split a class across files without a
      strong reason.
    - Never hand-edit `.unity` scenes, prefabs, or `.meta` files. When a
      feature explicitly requires a change, make it through the connected
      Editor.

11. **Write a test for any simulation change.**
    The project has the Unity Test Framework package installed
    (`com.unity.test-framework`, per `Packages/manifest.json`) and an
    EditMode suite at `Assets/Tests/EditMode/Tests/`
    (`NodeWar.Simulation.Tests.asmdef`, plus `DeterminismBaselineTests`,
    `EdgeWeightTests`, `MovementCorrectnessTests`, `SimulationSmokeTest`,
    and the shared `TestBoardFactory`).

    Run it with `dotnet test dotnet/NodeWar.sln` — no Editor, no licence,
    and it is what CI runs, so prefer it for any result you intend to rely
    on. The Unity runners answer a different question, whether the code
    works *in the Editor*: `scripts/run-tests.ps1` (batch mode — close the
    Editor first, it takes the project lock) or `scripts/run-tests-live.ps1`
    (drives the already-open Editor via `TestBridge`). See
    `docs/skills/run-dotnet-tests.md`, and note the `--logger` caveat there
    before generating a receipt. Any change to `Simulation/`
    should come with a test exercising the new behavior, added alongside
    the existing ones. At minimum, before calling the feature done, run a
    networked match on both peers past a `DESYNC_CHECK_INTERVAL` checkpoint
    (first at tick index 50, after 51 simulated ticks), let the peers exchange
    the hashes, and confirm no `[DESYNC]` log appears. A local match has no
    peer hash comparison and cannot establish this.
