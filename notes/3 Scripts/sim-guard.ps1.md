---
shell: 3
up:
  - "[[Test evidence]]"
  - "[[Determinism receipts]]"
down: []
concepts:
  - "[[Why floats break determinism]]"
tags: [shell/3, realm/match, system/tooling, script]
card: docs/systems/tooling.md
file: scripts/sim-guard.ps1
sources:
  - scripts/sim-guard.ps1
  - scripts/hooks/pre-commit
  - .github/workflows/determinism.yml
verified: ce9fb6bf
---

# sim-guard.ps1

> [!question] The question
> Which determinism mistakes can be rejected before compiling, and which still need someone to understand the change?

**Where you are:** [[Home]] › [[Tooling]] › [[Test evidence]] › **sim-guard.ps1** (script)

**Open:** [sim-guard.ps1 in VS Code](vscode://file/C:/Dev/NodeGame/scripts/sim-guard.ps1)

## What it is

A read-only scanner for forbidden constructs in Simulation C# files, with an optional repo-relative `-Path`. It returns 0 for no matches and 1 for violations or a missing scan path. Its header calls it the “MECHANICAL SUBSET” of determinism review.

## Read it in this order

1. **`$Rules`.** Each regex carries both a name and a reason. The list catches UnityEngine references; float/double/decimal types; listed wall-clock and frame APIs; listed collection types; and parameterless `new Random()` / `new System.Random()`.
2. **`Get-CodeOnlyLines`.** A small scanner removes comments, strings and character literals while keeping original line numbers. It tracks block comments across lines and handles ordinary/verbatim strings within its scanner model.
3. **The recursive `.cs` loop.** Every nonblank code-only line is tested against each rule. A hit records file, original line, reason and code text.
4. **The clean-result message.** Even success reminds the reader to check sort tiebreakers, tick order, hasher registration, command/serializer pairing and the view boundary.

## Lessons to take away

The header explains why stripping prose matters: a comment containing “double-counting” once looked like a floating-point violation to naive grep. False positives teach people to ignore a guard. The scanner also exists because quotes inside comments and comment markers inside strings confuse a naive replacement pass.

Its collection rule bans matching type syntax; it does not inspect whether a particular dictionary is actually iterated. Its RNG regex catches parameterless construction; it does not prove an explicitly seeded RNG is replicated and advanced in the right place. Passing is evidence about these patterns, not a proof of determinism.

The pre-commit hook triggers on staged added/copied/modified Simulation C# paths, then scans the working-tree directory. It does not extract and scan only staged blobs. CI runs the guard independently on both OS legs. Neither path replaces meaningful tests or semantic review.

## Check yourself

> [!question]- A sort compares only distance and leaves equal distances unordered. Will this script necessarily reject it?
> No. Total-order reasoning is outside its regex rules. A reader must check the ID tiebreaker and relevant tests.

> [!question]- Why is a passing guard weaker than the determinism contract?
> It recognizes a finite set of syntax patterns. It cannot establish canonical tick order, complete state hashing or that the view never writes simulation state.

## Go deeper ↓

This is the lowest shell. Below it is the code itself. Compare [[Determinism receipts]] and [[SimulationStateHasher]] for complementary evidence.

## My understanding

<!-- Yours. AI never edits below this line. -->
