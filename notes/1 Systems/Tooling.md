---
shell: 1
up: "[[Home]]"
down:
  - "[[Test evidence]]"
  - "[[Determinism receipts]]"
  - "[[Documentation guardrails]]"
concepts:
  - "[[Deterministic lockstep]]"
tags: [shell/1, realm/lobby, realm/match, realm/backend, system/tooling]
card: docs/systems/tooling.md
sources:
  - dotnet/NodeWar.sln
  - scripts/compile-check.ps1
  - scripts/sim-guard.ps1
  - scripts/merge-doc-review.ps1
  - .github/workflows/merged-docs.yml
  - scripts/okf-stale.ps1
  - scripts/doc-budget.ps1
  - scripts/context.ps1
  - scripts/hooks/pre-commit
  - .github/workflows/determinism.yml
  - .github/workflows/docs.yml
  - docs/attesters/hash_baseline.ps1
verified: 41a0df69
---

# Tooling

> [!question] The question
> An agent says “all checks pass.” Which checks, about which code, and what can still be broken?

**Where you are:** [[Home]] › **Tooling** (system). Spans [[Lobby]], [[Match]] and [[Backend]].

## The problem

A convincing explanation is cheap to produce. Evidence is more specific: a test exercised particular inputs; a compiler checked particular files; a freshness report compared particular revisions. Calling all of these “verified” hides the gaps. An AI agent can accidentally rely on stale docs, omit a test file from a project, or mistake a green old receipt for proof about a new edit.

## The idea

**Make each claim answer to a check with a clear boundary, and keep human review visible where a mechanical check cannot decide.**

## How it works here

| Question | Evidence | What it leaves open |
|---|---|---|
| Did the selected rules and pure helpers behave as asserted? | `dotnet test dotnet/NodeWar.sln` | Rendering, scene wiring, untested behavior |
| Do Unity-facing call sites type-check? | [[compile-check.ps1]] | Runtime behavior and Unity's real assembly separation |
| Did forbidden constructs enter Simulation? | [[sim-guard.ps1]] | Tick order, comparator totality, complete hashing |
| Did the sanctioned fingerprints run on current watched source? | [[Determinism receipts]] | All other scenarios and unwatched dependencies |
| Are agent cards compact and structurally consistent? | [[doc-budget.ps1]] | Truth of their prose |
| Which declared sources moved since review? | [[okf-stale.ps1]] | Whether a claim actually became false |
| What should this task load? | [[context.ps1]] | Work outside the declared slice |

The pre-commit hook blocks mechanical simulation violations when staged Simulation C# paths trigger it, and doc-budget failures when staged docs/entry files trigger it. Freshness remains advisory. Installation is per clone through `core.hooksPath`; keeping the hook in the repository does not activate it everywhere.

After PR merges, `merged-docs.yml` refreshes [[Merge review]] and one tracking issue using the same source/stamp findings. It updates the queue; the committing agent still reviews prose and stamps only material read against the diff. Personal **My understanding** sections stay untouched.

## Why this, not that

| Decision | Alternative | Gain | Cost |
|---|---|---|---|
| Compile shared source in plain .NET | Require Unity for every check | Fast Editor-free evidence and CI | Only the selected pure source is covered |
| Separate compilation, tests and wiring claims | One “green” label | Honest limits in an agent's report | More than one check to select |
| Mechanical blockers plus advisory freshness | Block every moved source | Objective mistakes stop early; review can follow a source commit | Advice still needs an owner |
| Windows/Linux exact-hash checks | Repeat twice on one machine | Exercises a platform boundary | Still only covers the sanctioned fixtures |

## What it costs later

The checks themselves need maintenance. Explicit source lists need new entries; cached Unity assemblies can age; regexes need semantic review alongside them. The attester can skip unavailable freshness information, so read its reasons rather than treating the final PASS word as complete provenance.

## Traps

- “Tests passed” does not say an explicit measurement suite ran. Network `SweepTests` requires deliberate selection.
- A completion GUID prevents accepting an old live-Editor completion signal; it does not prove Unity compiled the latest edit.
- Budget compliance is not factual accuracy. A short hallucination is still a hallucination.
- Matching frontmatter alone does not enroll a note in a scan; the directory must be in `okf-stale.ps1`'s `$SearchDirs`. This vault's `notes/` was added there when the vault was created, so its notes are checked like cards.

## Patterns you've seen before

- 🔁 [[Same code, three places]]: shared simulation source serves Unity, .NET checks and server-side use without a copied ruleset.
- 🔁 [[Pure core, Unity shell]]: testable policies are separated from Editor and scene concerns.
- 🔁 [[Constants come from measurements]]: the explicit network sweep is evidence for tuning, separate from ordinary pass/fail.
- 🔁 [[Never trust the wire]]: similarly, a receipt is inspected for required cases before it is accepted as evidence.

## Check yourself

> [!question]- An agent changes a HUD callback and reports only Simulation tests passing. What's missing?
> Evidence about that changed Unity-facing code: compile-check for call-site/type errors, then Unity verification for the event wiring and interaction. Simulation tests answer a different question.

> [!question]- Why doesn't a freshness warning automatically block a source commit?
> The prescribed follow-up review names that commit's SHA, which does not yet exist before commit. The hook reports the work still needed without pretending the heuristic can perform the review.

## Go deeper ↓

- [[Test evidence]] · [[Determinism receipts]] · [[Documentation guardrails]]
- [[compile-check.ps1]] · [[sim-guard.ps1]] · [[okf-stale.ps1]] · [[doc-budget.ps1]] · [[context.ps1]]

Sideways → [[Desync detection]] · [[SimulationStateHasher]] · [[Why floats break determinism]]

## My understanding

<!-- Yours. AI never edits below this line. -->
