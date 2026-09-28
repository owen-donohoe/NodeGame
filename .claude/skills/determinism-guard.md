---
type: Skill
title: determinism-guard
description: Checklist for reviewing any change in Assets/Scripts/Game/Simulation/ against the determinism contract.
tags: [skill, simulation, determinism, review]
generated: { by: human:DonohoeCUA, at: 2026-08-30T17:15:16-04:00 }
verified:
  - { by: claude-opus-5, at: 2026-08-31T00:00:00Z }
  - { by: claude-opus-5, at: 2026-09-02T00:00:00Z }
  - { by: claude-opus-5, at: 2026-09-13T00:00:00Z }
  - { by: claude-opus-5, at: 2026-09-13T01:00:00Z }
verified_at_commit: 1f5c20b
status: stable
sources:
  - id: contract
    resource: docs/simulation-rules.md
    title: Simulation Determinism Contract
    last_modified: 2026-09-28T14:44:08-04:00
  - id: sim-loop
    resource: Assets/Scripts/Game/Simulation/GameSimulation.cs
    title: GameSimulation.SimulateTick
    last_modified: 2026-09-25T22:52:42-04:00
  - id: hasher
    resource: Assets/Scripts/Game/Simulation/SimulationStateHasher.cs
    title: SimulationStateHasher.ComputeHash
    last_modified: 2026-09-25T22:52:42-04:00
  - id: sim-version
    resource: Assets/Scripts/Game/Simulation/SimulationVersion.cs
    title: SimulationVersion.Current and bump policy
    last_modified: 2026-09-25T20:27:51-04:00
  - id: baseline-tests
    resource: Assets/Tests/EditMode/Tests/DeterminismBaselineTests.cs
    title: Pinned version equality check
    last_modified: 2026-09-25T20:27:51-04:00
  - id: balance-hasher
    resource: Assets/Scripts/Game/Simulation/BalanceHasher.cs
    title: Balance fingerprint separate from state
    last_modified: 2026-09-25T22:52:42-04:00
  - id: balance-data
    resource: Assets/Scripts/Game/Simulation/GameBalanceData.cs
    title: Per-era suit and district entries
    last_modified: 2026-09-25T22:52:42-04:00
  - id: match-factory
    resource: Assets/Scripts/Game/Simulation/MatchFactory.cs
    title: Shared drafted board and static configuration
    last_modified: 2026-09-25T22:52:42-04:00
  - id: input-serializer
    resource: Assets/Scripts/Game/Network/InputSerializer.cs
    title: Wire layout and build identity comparison
    last_modified: 2026-09-28T01:56:48-04:00
  - id: command-layout-test
    resource: dotnet/NodeWar.MatchLog.Tests/MatchLogFormatTests.cs
    title: Command field layout guard
    last_modified: 2026-09-25T22:04:00-04:00
  - id: draft-manager
    resource: Assets/Scripts/Game/Core/DraftManager.cs
    title: Timeout placement fallback
    last_modified: 2026-09-26T08:36:52-04:00
---

# determinism-guard

## When to use
When creating or reviewing any code in Assets/Scripts/Game/Simulation/.
Invoke explicitly: "run determinism-guard on this change."

## Procedure
Read the changed or proposed code, then check each item:

1. UnityEngine boundary
   - Any UnityEngine namespace reference anywhere in Simulation/ aside from legitimate references in comments/documentation?
   - Any UnityEngine type used as a parameter or return value?

2. Numeric types
   - Any float, double, or decimal in simulation logic?
   - Any division that could produce fractional results?

3. Time and frame APIs
   - Any Time.deltaTime, Time.time, Time.fixedDeltaTime?
   - Any System.DateTime or System.Environment.TickCount?

4. Randomness
   - Any UnityEngine.Random usage?
   - Any System.Random constructed without a seed, or seeded from
     wall-clock time or per-machine state?
   - SimulationState holds no RNG field today, so "stored in
     SimulationState" is not yet the test. The precedent is
     DraftManager.HandleTimeout's random-cell fallback, which derives a
     seed from already-replicated values when there is no valid parked
     placement. The active peer sends the resulting placement. If a change
     introduces stored RNG state, does it
     live on SimulationState and advance only inside SimulateTick?

5. Collections
   - Any Dictionary or HashSet iterated in simulation code?
   - Any LINQ OrderBy without a complete tiebreaker?
   - Any List.Sort without a total-order comparator?

6. Sort order
   - Does every sort have an ID-based tiebreaker as the final 
     comparison key?

7. Tick order
   - Does any change reorder or skip steps in the canonical 
     tick sequence?
   - Canonical order: movement -> combat -> claiming -> 
     production -> healing -> respawns -> win-check
   - Preserve the Rampart-bonus pass after movement and the post-combat
     resume pass after win-check too.

8. Hasher registration
   - Does any new SimulationState field appear in 
     SimulationStateHasher?
   - Does any removed field get removed from the hasher too?
   - A field hashed only when non-zero (the era fields) counts as
     registered, but only if it is 0 in every match that existed before
     it; otherwise it must be hashed unconditionally.
   - Conditional hashing alone does not waive a SimulationVersion bump:
     do existing inputs still produce the same results and hashes, as
     they did when eras were added? If not, bump SimulationVersion.Current
     and review the pinned baseline version with the change. The version
     test checks equality with that pin, not whether hash constants were edited.
   - Balance is not in SimulationStateHasher. Does a new GameBalanceData,
     SuitStats or DistrictStats field reach BalanceHasher? Per-suit and
     per-district numbers belong on their era entries, not new globals.
     The handshake's content-hash comparison mitigates issue #59; it does
     not put balance into the state hash.
   - Does starting-state setup still go through MatchFactory for live
     drafted matches, the referee and headless matches? Its Configure
     method installs process-global statics, so matches and test fixtures
     must not run concurrently in one process.

9. Command/serializer pairing
   - Does any new CommandType have a case in CommandProcessor?
   - Does any GameCommand struct change update InputSerializer?
   - Does a wire layout change bump InputSerializer.ProtocolVersion, and
     a GameCommand layout change also update MatchLogFormat with a new
     TICKS tag? The layout test flags changed fields; it does not verify
     that a new tag was added.

10. View boundary
    - Does any simulation code read from or call into View or UI?

## Output format
Report each item as PASS, FAIL, or N/A.
For any FAIL: state the file, line, and exact problem.
For any N/A: one-line explanation of why it does not apply.
End with overall PASS or FAIL and a summary of required fixes.
Do not suggest fixes inline -- report problems only.
