---
type: Skill
title: phase-plan
description: Read-then-propose procedure for starting a phase. Largely superseded by Notion Phases, which own future work; kept for the read-before-proposing discipline.
tags: [skill, planning]
generated: { by: human:DonohoeCUA, at: 2026-08-30T17:15:16-04:00 }
verified:
  - { by: claude-opus-5, at: 2026-08-31T00:00:00Z }
verified_at_commit: bc701d1
status: stable
---

# phase-plan

## When to use
When starting a new phase or significant feature.
Invoke explicitly: "use phase-plan for [goal]."

## Procedure

Step 1: Read the relevant codebase
- Identify all files relevant to the goal
- Understand the current state before proposing changes
- Do not propose anything yet

Step 2: Identify what exists
- What is already implemented that this builds on?
- What is partially implemented?
- What is missing entirely?

Step 3: Check the simulation boundary
- Does this feature touch Simulation/?
- If yes: what new state fields are needed?
- If yes: what new commands are needed?
- If yes: what are the determinism implications?

Step 4: Propose a sequenced plan
- Break the work into steps that can each be committed 
  independently
- Order steps so each one leaves the project in a working state
- Flag any step that carries architectural risk
- Flag any step that requires a test before proceeding

Step 5: Identify risks and dependencies
- What could go wrong?
- What must exist before this starts?
- What existing behavior could this break?

## Output format
Sections in this order:
CURRENT STATE | PLAN (numbered steps) | RISKS | FIRST STEP
Be specific about file names and class names.
Do not begin implementation. Wait for explicit approval.
