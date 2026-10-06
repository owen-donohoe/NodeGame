---
type: Skill
title: cs-review
description: Layer-compliance and convention review to run before committing any significant C# change.
tags: [skill, review, architecture, csharp]
generated: { by: human:DonohoeCUA, at: 2026-08-30T17:15:16-04:00 }
verified:
  # full history: docs/verification-log.md
  - { by: claude-opus-5, at: 2026-09-14T00:00:00Z }
  - { by: claude-opus-5-5, at: 2026-09-29T18:00:00Z }
  - { by: claude-opus-5-5, at: 2026-09-30T07:00:00Z }
  - { by: gpt-6-sol, at: 2026-09-30T07:00:00Z }
  - { by: claude-sonnet-5-5, at: 2026-10-03T00:41:16Z }
  - { by: gpt-6-sol, at: 2026-10-06T01:06:52Z }
  - { by: gpt-6-sol, at: 2026-10-06T01:08:47Z }
verified_at_commit: 673cc4b9
status: stable
sources:
  - id: architecture
    resource: docs/architecture.md
    title: The seven layers and adjacent backend and replay systems
    last_modified: 2026-09-28T14:43:33-04:00
  - id: contract
    resource: docs/simulation-rules.md
    title: Simulation Determinism Contract
    last_modified: 2026-09-26T10:16:37-04:00
---

# cs-review

## When to use
Before committing any significant C# change.
Invoke explicitly: "run cs-review on the changes in this session."

## Procedure
Read all files modified in this session, then check:

1. Architecture compliance
   - Does each file respect its layer's ownership rules?
   - Does anything in Simulation/ reference UnityEngine?
   - Does any presentation code write to SimulationState, or call
     GameSimulation or CommandProcessor? Check all three trees, not
     just the layer named UI: Assets/Scripts/Game/{View,UI}/,
     Assets/UI/ (UI Toolkit), and Assets/Legacy/. Read-only command/cost
     helpers are allowed. The documented local playtest SetBalance exception
     must stay Editor/development-only, non-networked and state-write-free,
     installing a copy and warning about the changed hash/replay eligibility.
   - Does every gameplay GameCommand sent from presentation code go through
     InputBuffer with issuedOnTick stamped?
   - Does anything in Network/ contain game logic?
   - Do Backend/ and dotnet/NodeWarCloud/ keep rated progression,
     inventory and match settlement server-owned, with client UI using
     the async service contracts and returned state?
   - Does Backend/Shared/ remain UnityEngine-free so the same DTOs,
     rules and service contracts compile into Cloud Code?
   - Does MatchLog/ preserve applied command order and replay through MatchFactory,
     CommandProcessor and GameSimulation rather than duplicate their
     rules? Does the Cloud Code referee serialize replays that share
     simulation statics?

2. Single-file principle
   - Are SerializeField variables in the same file as their 
     MonoBehaviour?
   - Was a class split into separate files without strong reason?

3. Unity lifecycle
   - Any expensive operations in Update() that belong in a 
     slower callback?
   - Any FindObjectOfType or GetComponent calls in Update()?
   - Any Awake() code that depends on another object's Awake() 
     having run first?

4. Serialization risks
   - Any SerializeField added to a class that is instantiated 
     at runtime rather than placed in the scene?
   - Any field rename that would break existing serialized data?

5. Unnecessary complexity
   - Any abstraction added before it is needed?
   - Any interface with one implementation and no concrete testing or
     replacement need? IRankedQueueView is an intentional presentation
     seam even with one shipped view.
   - Any generic type parameter that adds complexity without 
     clear benefit?

6. Conventions
   - New SimulationState fields added to SimulationStateHasher and CopyFrom,
     with their explicit neutral defaults initialized by MatchFactory?
   - New CommandType has a CommandProcessor case?
   - GameCommand struct and InputSerializer updated together?
   - Wire layout changes bump ProtocolVersion.Current in
     Backend/Shared/ProtocolVersion.cs, which InputSerializer.ProtocolVersion aliases?
   - Simulation behavior changes bump SimulationVersion.Current, with
     balance-only edits tracked by the content hash?
     Current and the sanctioned baseline pin are 2; numeric fingerprints
     remained unchanged in the coordinated v2 re-pin.

## Output format
Report each category as PASS, FAIL, or N/A.
For any FAIL: file, line, problem, one-line suggested fix.
End with a list of required changes before commit and 
a list of optional improvements.
