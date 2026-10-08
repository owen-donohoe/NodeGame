---
type: Skill
title: determinism-guard
description: Checklist for reviewing any change in Assets/Scripts/Game/Simulation/ against the determinism contract.
tags: [skill, simulation, determinism, review]
generated: { by: human:DonohoeCUA, at: 2026-08-30T17:15:16-04:00 }
verified:
  # full history: docs/verification-log.md
  - { by: claude-sonnet-5-5, at: 2026-10-08T16:47:09Z }
verified_at_commit: e7e968a80076be4a12902191f076b8c141a6c9d0
status: stable
sources:
  - id: contract
    resource: docs/simulation-rules.md
    title: Simulation Determinism Contract
  - id: sim-loop
    resource: Assets/Scripts/Game/Simulation/GameSimulation.cs
    title: GameSimulation.SimulateTick
  - id: hasher
    resource: Assets/Scripts/Game/Simulation/SimulationStateHasher.cs
    title: SimulationStateHasher.ComputeHash
  - id: sim-version
    resource: Assets/Scripts/Game/Simulation/SimulationVersion.cs
    title: SimulationVersion.Current and bump policy
  - id: baseline-tests
    resource: Assets/Tests/EditMode/Tests/DeterminismBaselineTests.cs
    title: Pinned version equality check
  - id: balance-hasher
    resource: Assets/Scripts/Game/Simulation/BalanceHasher.cs
    title: Balance fingerprint separate from state
  - id: balance-data
    resource: Assets/Scripts/Game/Simulation/GameBalanceData.cs
    title: Per-era suit and district entries
  - id: match-factory
    resource: Assets/Scripts/Game/Simulation/MatchFactory.cs
    title: Shared drafted board and static configuration
  - id: input-serializer
    resource: Assets/Scripts/Game/Network/InputSerializer.cs
    title: Wire layout and build identity comparison
  - id: command-layout-test
    resource: dotnet/NodeWar.MatchLog.Tests/MatchLogFormatTests.cs
    title: Command field layout guard
  - id: draft-manager
    resource: Assets/Scripts/Game/Core/DraftManager.cs
    title: Timeout placement fallback
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
   - Does tempo use inclusive cumulative integer integration and consecutive
     differences, with production remainder carry? Are positive percentages
     below 100 accepted and production durations safe for the largest decrement?
   - Do nonpositive resource caps retain uncapped behaviour, with every gain
     and starting value clamped for positive caps? At metal capacity a Forge
     must not spend materials; a wasted Market completion still alternates.

3. Time and frame APIs
   - Any Time.deltaTime, Time.time, Time.fixedDeltaTime?
   - Any System.DateTime or System.Environment.TickCount?

4. Randomness
   - Any UnityEngine.Random usage?
   - Any System.Random constructed without a seed, or seeded from
     wall-clock time or per-machine state?
   - SimulationState holds no RNG field today, so "stored in
     SimulationState" is not yet the test. The precedent is
     DraftState.ChooseTimeout's random-cell fallback (reached from
     DraftManager.HandleTimeout), which derives a seed from
     already-replicated values when there is no valid parked placement and
     picks only a cell PlacementLegality allows. The active peer sends the
     resulting placement. If a change
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
   - Preserve the Rampart-bonus pass after movement and the order-resume
     pass (TickOrderResume) after win-check too. TickBreach precedes TickClaiming
     inside claiming; the derived nextBreacherID refresh is last, after resume.
   - Does a rule that depends on neighbouring owners (the capture bonus,
     breach frontier) read the tick-start owner snapshot, not live owners? Are
     rates computed in long and bounded, so node order and large balances
     cannot change or overflow a result?
   - Do worker-count rules (the Infirmary's two counted Acolytes) pick by a total order
     (lowest villager ID) and share one function between the tick and any UI price?
   - A Town reward is paid inside the claim-complete step, with its entitlement
     spent before the population-cap check (no deferred credit), tracked by the
     hashed `townPaidMask`.
   - Auto-recruit follows ordinary production and precedes healing, in ascending
     node ID. Commands and the automatic pass share eligibility and mutation;
     recruitment cooldown is not tempo-scaled.
   - With the breach channel enabled, does a loss require a breach this tick
     at the current threshold, with simultaneous losses cancelled? A drop
     alone must not lose; disabling the channel keeps the legacy win path.

8. Hasher registration
   - Does any new SimulationState field appear in 
     SimulationStateHasher?
   - Does any removed field get removed from the hasher too?
   - Does it also appear in SimulationState.CopyFrom? SimulationStateCopyTests
     sets every field by reflection and fails on one that is not copied.
   - Neutral extension fields can be conditionally hashed with explicit
     defaults: era fields, breachBar and paidRespawns are zero-neutral;
     nextBreacherID is -1-neutral. Check initialization as well as hashing.
     Breaching must stay appended after Dead to preserve enum values.
     Recruit count and ready tick are zero-neutral; autoRecruit is false-neutral.
     Their tagged hash contributions include player/node index, and ownership
     loss clears only the node fields, leaving the match-long player count intact.
   - Conditional hashing alone does not waive a SimulationVersion bump:
     do existing inputs still produce the same results and hashes, as
     they did when eras were added? If not, bump SimulationVersion.Current
     and review the pinned baseline version with the change. The version
     test checks equality with that pin, not whether hash constants were edited.
     Current and BaselinesPinnedAtSimVersion are both 3; the v2 re-pin retained
     the two numeric baseline hashes for the short, non-breaching fixtures; the v3
     re-pin (terrain board) moved both, to 411123996 and 2101726457, because
     boardHash and terrain are always hashed.
   - DistrictType numbers are persisted and explicit: active set 0-6 and 13-17
     (DistrictRoster.IsActive), retired numbers reserved. Does any new path accept
     an inactive type or an alias? Only DistrictMigration (saved data) maps old
     numbers, and the log reader refuses inactive types for simulation version 3 and later.
   - Board data (terrain, slot mask, base pools, placements, link tuning) is
     identity, not state: does a new BoardConfigData field reach BoardHasher, so
     boardHash and MatchSetup see it, and BOARD_V2 (or a new tag) so the log
     carries it? The BOARD chunk layout is frozen.
   - Balance is not in SimulationStateHasher. Does a new GameBalanceData,
     SuitStats or DistrictStats field reach BalanceHasher? Per-suit and
     per-district numbers belong on their era entries, not new globals.
     The handshake's content-hash comparison mitigates issue #59; it does
     not put balance into the state hash.
   - Does starting-state setup still go through MatchFactory for live
     drafted matches, the skip-draft mode, the referee and headless matches,
     and does every placement or preview ask PlacementLegality rather than
     a second rule? RequireBuildable refuses, never repairs, a bad board. Its Configure
     method installs process-global statics, so matches and test fixtures
     must not run concurrently in one process.

9. Command/serializer pairing
   - Does any new CommandType have a case in CommandProcessor and an entry
     in CommandTypes.IsKnown?
   - Are new enum values explicitly accepted in InputSerializer and MatchLogFormat,
     with unknown-type refusal and six-field round-trip coverage? Recruit=5 and
     SetAutoRecruit=6 keep the existing payload and TICKS shape. Node commands
     require villagerID=-1; Recruit value=0, repeat toggle value=0 or 1.
   - Does any GameCommand struct change update InputSerializer?
   - Is a map or rules agreement still made before any draft packet is
     honoured (MatchSetup, SetupAgreement), with the board built from the
     shipped catalog rather than taken from the wire, and does the referee
     check the log's map against its own catalog?
   - Does a wire layout change (a GameCommand field, or a TickInput header
     byte such as senderDelay and requestedDelay) bump ProtocolVersion.Current
     in Backend/Shared/ProtocolVersion.cs, which InputSerializer.ProtocolVersion
     aliases? Does a GameCommand layout change also update MatchLogFormat with a new
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
