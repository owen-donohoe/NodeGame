---
shell: pattern
seen-in:
  - "[[Tooling]]"
  - "[[Test evidence]]"
  - "[[Determinism receipts]]"
  - "[[Documentation guardrails]]"
  - "[[Referee]]"
  - "[[Desync detection]]"
  - "[[Pending orders]]"
  - "[[Headless balance matches]]"
tags: [pattern, topic/ai-workflow]
---

# 🔁 Evidence has a boundary

> [!question] The question
> "All checks pass." "The referee verified it." "The hashes match." Each sounds like proof. Why does this codebase keep writing down exactly what each one does *not* prove?

**Kind:** pattern. An idea that recurs at different shells of this codebase, and the one that matters most when working with AI agents.

## The pattern

**Every check answers one specific question. State that question, and state what it leaves open, next to the result. Never let "this passed" quietly grow into "it works".**

## Where you've seen it

| Where | The check | What it proves | What it leaves open (in the code's own words, where they exist) |
|---|---|---|---|
| [[Referee]] / [[MatchReplay]] | Full replay of a log | The log is consistent | "not that it is honest": fabricated commands with recomputed hashes still pass |
| [[Desync detection]] | 32-bit state hash | The two states differ, or very probably don't | *What* differs, and forgery (not its job) |
| [[Determinism receipts]] | Pinned fingerprints on Windows and Linux | Those scenarios produce those hashes there | Every other scenario, and unwatched dependencies |
| [[Test evidence]] | `dotnet test` | Asserted behaviour of the pure code | Scenes, prefabs, rendering, touch |
| [[compile-check.ps1]] | Type check against Unity assemblies | Call sites type-check | Runtime behaviour: "not a test run" |
| [[sim-guard.ps1]] | Regex scan | Banned syntax is absent | Tiebreakers, tick order, hashing: "the MECHANICAL SUBSET" |
| [[okf-stale.ps1]] | Source ancestry | Which docs *might* be stale | Whether a claim is false: "Trust it to triage, not to decide" |
| [[doc-budget.ps1]] | Size and shape | Cards are short and well-formed | Whether they're true |
| [[Pending orders]] | Provisional route | The order was issued | That it will succeed ("a close approximation rather than a promise") |
| [[Headless balance matches]] | Bot vs bot statistics | How *this policy* performs | Optimal play or fun |

## Why it matters with AI

An agent's report is a claim. Asking "which check, about which code, and what does it leave open?" turns it into evidence you can weigh. This session's own example: the startup PR reported "4,106 tests pass, compile-check clean" and, separately, "not run in Play mode". The second line is the boundary.

## What it costs

More words in every report, and the discipline to keep human or Editor checks visible instead of hiding them behind a green tick.

## Spot it yourself

> [!question]- An agent says "fixed, tests pass" after changing a HUD button. What do you ask?
> Which tests? Pure-model tests can pass while the button is unwired. Ask for compile-check, plus an Editor or Play-mode check of the actual button, or an honest "not verified in Unity".

## My understanding

<!-- Yours. AI never edits below this line. -->
