---
type: Skill
title: cs-review
description: Layer-compliance and convention review to run before committing any significant C# change.
tags: [skill, review, architecture, csharp]
generated: { by: human:DonohoeCUA, at: 2026-08-30T17:15:16-04:00 }
verified:
  - { by: claude-opus-5, at: 2026-08-31T00:00:00Z }
  - { by: claude-opus-5, at: 2026-09-02T00:00:00Z }
  - { by: claude-opus-5, at: 2026-09-02T02:00:00Z }
verified_at_commit: b5d3099
status: stable
sources:
  - id: architecture
    resource: docs/architecture.md
    title: The seven layers and their ownership
  - id: contract
    resource: docs/simulation-rules.md
    title: Simulation Determinism Contract
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
   - Does anything in View/ or UI/ write to SimulationState?
   - Does anything in Network/ contain game logic?

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
   - Any interface introduced for a class that has exactly one 
     implementation?
   - Any generic type parameter that adds complexity without 
     clear benefit?

6. Conventions
   - New SimulationState fields added to SimulationStateHasher?
   - New CommandType has a CommandProcessor case?
   - GameCommand struct and InputSerializer updated together?

## Output format
Report each category as PASS, FAIL, or N/A.
For any FAIL: file, line, problem, one-line suggested fix.
End with a list of required changes before commit and 
a list of optional improvements.
