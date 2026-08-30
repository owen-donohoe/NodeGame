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
     (`GameManager.InitializeVillagers` / `InitializePlayers` /
     `InitializeNodesFromDraft`, and anywhere else new instances are
     created mid-match).
   - **Add it to `SimulationStateHasher.ComputeHash` now, not later.**
     Every new mutable field on `NodeData`, `VillagerData`, `PlayerData`,
     or `SimulationState` must be included, in the same order/section as
     its siblings. Skipping this makes desync detection blind to bugs
     involving the field.

3. **Does it need a new player-triggerable action?**
   - Add a `CommandType` in `Commands.cs` if no existing type fits.
   - Add a case in `CommandProcessor.ProcessCommand` that validates
     ownership/state/cost before mutating anything (follow
     `ProcessEquipCommand`'s shape: ownership check → state check → cost
     check → apply).
   - Capture the input in `Input/` (`CommandSystem`, and `BotPlayer` if
     the bot should be able to do it too) and push it through
     `InputBuffer`. Never mutate `SimulationState` directly from `Input/`,
     `UI/`, or `View/`.
   - If the command needs new data on the wire, extend `InputSerializer`
     (or `DraftSerializer` for draft-phase actions) — both peers must
     encode/decode it identically.

4. **Does it involve randomness?**
   - Never use `UnityEngine.Random` or anything seeded from wall-clock
     time inside `Simulation/`.
   - Derive a seed from already-replicated state (tick count, player ID,
     entity ID) — see `DraftManager.HandleTimeout`'s
     `turnNumber * 7919 + activePlayer * 31` pattern.
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

7. **Does it change the tick loop itself?**
   - Confirm where it fits in the canonical order: `movement → combat →
     claiming → production → healing → respawns → win-check` (as
     documented on `GameSimulation.SimulateTick`).
   - Insert at the correct, justified step — do not append a new step at
     the end by default, and do not reorder existing steps.

8. **Is it purely visual (no simulation involvement)?**
   - Confirm it only reads `SimulationState` — no writes.
   - Wire any player interaction back through `InputBuffer` as a
     `GameCommand`, exactly like any other input.
   - Use `ITickProvider.TickAlpha` for interpolation so the feature works
     identically under `TickRunner` (local) and `LockstepRunner`
     (networked).

9. **Does it add a new tunable number?**
   Put it on `GameBalance` or `BoardConfig` as an inspector-exposed field,
   read through the existing `bal` / `boardConfig` reference already
   available in `Simulation/` — don't hardcode it or add a new plumbing
   path.

10. **C# conventions.**
    - Keep `[SerializeField]` fields in the same file as their
      `MonoBehaviour` — don't split a class across files without a
      strong reason.
    - Don't touch `.unity` scenes, prefabs, or `.meta` files unless the
      feature explicitly requires it.

11. **Write a test for any simulation change.**
    The project has the Unity Test Framework package installed
    (`com.unity.test-framework`, per `Packages/manifest.json`), but no
    test assembly or test files exist in the repo yet. Any change to
    `Simulation/` should come with a test exercising the new behavior; if
    no test assembly exists yet, that setup is part of the task, not
    something to skip. At minimum, before calling the feature done, run a
    local match long enough to cross a `DESYNC_CHECK_INTERVAL` boundary
    (50 ticks) and confirm no `[DESYNC]` log appears.
