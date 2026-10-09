---
type: Skill
title: cs-review
description: Layer-compliance and convention review to run before committing any significant C# change.
tags: [skill, review, architecture, csharp]
generated: { by: human:DonohoeCUA, at: 2026-08-30T17:15:16-04:00 }
verified:
  # full history: docs/verification-log.md
  - { by: gpt-6.1-sol, at: 2026-10-09T19:20:20Z }
verified_at_commit: 36c57c73087ced5dd842a653f676c83b51c83031
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
   - Does the pre-draft setup (MatchSetup, MatchSetupAck, SetupAgreement) only
     carry and compare map ID, board hash and versions, with the board itself
     coming from the shipped PremadeMaps.Catalog rather than the wire?
   - Does a UI price or eligibility call the simulation's own helper
     (NodeActionRules, BankRules, CountInfirmaryWorkers) rather than restate the rule?
   - Do saved-data conversions for retired districts stay in Backend/Shared
     DistrictMigration, with the runtime and wire accepting only active
     DistrictRoster types and no aliases?
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
   - New CommandType has a CommandProcessor case (Recruit, SetAutoRecruit, ForgeMinion and Collect
     validate through NodeActionRules or BankRules, which presentation may call read-only)?
   - GameCommand struct and InputSerializer updated together?
   - Wire layout changes (current protocol is 5) bump ProtocolVersion.Current in
     Backend/Shared/ProtocolVersion.cs, which InputSerializer.ProtocolVersion aliases?
   - Simulation behavior changes bump SimulationVersion.Current, with
     balance-only edits tracked by the content hash?
     Current and the sanctioned baseline pin are 4. E1 removed unconditional
     structure terms, retaining the leg clock and adding zero-neutral district
     health: fingerprints 2084609368 and -1780012649. E4 does not move them.

## Output format
Report each category as PASS, FAIL, or N/A.
For any FAIL: file, line, problem, one-line suggested fix.
End with a list of required changes before commit and 
a list of optional improvements.
