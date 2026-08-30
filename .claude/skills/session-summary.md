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
- Should any GitHub issues be created?

Step 5: Suggested commit message
- Format: type(scope): description
- type: fix, feat, refactor, test, docs, chore
- scope: sim, network, view, ui, input, core, lobby
- description: present tense, under 72 characters
- Body: two to three lines explaining what and why
- Example:
  feat(sim): add elapsed tick counter to SimulationState
  
  Tracks total ticks since match start as an integer field.
  Added to SimulationStateHasher. EditMode test added.

Step 6: Next logical step
- One sentence: what should the next session start with?

## Output format
Sections in this order:
CHANGES | TESTS | REMAINING | COMMIT MESSAGE | NEXT STEP
Keep each section to five lines or fewer.
No trailing additions after NEXT STEP.
