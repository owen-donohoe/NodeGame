---
type: Skill
title: determinism-guard
description: Checklist for reviewing any change in Assets/Scripts/Game/Simulation/ against the determinism contract.
tags: [skill, simulation, determinism, review]
generated: { by: human:DonohoeCUA, at: 2026-08-30T17:15:16-04:00 }
verified:
  - { by: claude-opus-5, at: 2026-08-31T00:00:00Z }
verified_at_commit: e90548a
status: stable
sources:
  - id: contract
    resource: docs/simulation-rules.md
    title: Simulation Determinism Contract
  - id: sim-loop
    resource: Assets/Scripts/Game/Simulation/GameSimulation.cs
    title: GameSimulation.SimulateTick
    last_modified: 2026-08-30T17:51:21-04:00
  - id: hasher
    resource: Assets/Scripts/Game/Simulation/SimulationStateHasher.cs
    title: SimulationStateHasher.ComputeHash
    last_modified: 2026-08-30T17:51:21-04:00
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
   - Any System.Random constructed without a seed?
   - Any new Random() without storing it in SimulationState?

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

8. Hasher registration
   - Does any new SimulationState field appear in 
     SimulationStateHasher?
   - Does any removed field get removed from the hasher too?

9. Command/serializer pairing
   - Does any new CommandType have a case in CommandProcessor?
   - Does any GameCommand struct change update InputSerializer?

10. View boundary
    - Does any simulation code read from or call into View or UI?

## Output format
Report each item as PASS, FAIL, or N/A.
For any FAIL: state the file, line, and exact problem.
For any N/A: one-line explanation of why it does not apply.
End with overall PASS or FAIL and a summary of required fixes.
Do not suggest fixes inline -- report problems only.
