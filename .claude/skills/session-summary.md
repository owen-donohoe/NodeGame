---
type: Skill
title: session-summary
description: End-of-session review of the working diff before committing.
tags: [skill, process, git]
generated: { by: human:DonohoeCUA, at: 2026-08-30T17:15:16-04:00 }
verified:
  - { by: claude-opus-5, at: 2026-08-31T00:00:00Z }
verified_at_commit: bc701d1
status: stable
---

# session-summary

## When to use
At the end of a working session, before committing.
Invoke explicitly: "run session-summary."

## Procedure

Step 1: Review git diff
- Run git diff to see all changes in this session
- Identify which files changed and why

Step 2: Summarize changes
- What was the goal of this session?
- What was actually implemented?
- What was explicitly not changed and why?

Step 3: Test results
- Which tests were run?
- Did they pass?
- Were any new tests added?

Step 4: Remaining issues
- What is still broken or incomplete?
- What follow-on work does this session create?
- Does anything belong in Notion Tasks? Do not create it here —
  Notion is written only during /update. Name it and let me decide.
  (This project does not track work in GitHub issues.)

Step 5: Suggested commit message
- Format: type: description — no scope parentheses. That is what
  this repo's history actually uses; a scope is optional and rare.
- type: fix, feat, refactor, test, docs, chore, build, ci
- description: present tense, under 72 characters
- Body: two to three lines explaining what and why
- If the change finishes a Notion task, add a `done: <task title>`
  line. That trailer is what marks the task Done on the next
  /update; without it the task moves to In Progress at most.
- Example:
  feat: add elapsed tick counter to SimulationState

  Tracks total ticks since match start as an integer field.
  Added to SimulationStateHasher. Simulation test added.

Step 6: Next logical step
- One sentence: what should the next session start with?

## Output format
Sections in this order:
CHANGES | TESTS | REMAINING | COMMIT MESSAGE | NEXT STEP
Keep each section to five lines or fewer.
No trailing additions after NEXT STEP.
