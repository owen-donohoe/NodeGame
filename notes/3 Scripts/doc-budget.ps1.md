---
shell: 3
up: "[[Documentation guardrails]]"
down: []
concepts: []
tags: [shell/3, realm/lobby, realm/match, realm/backend, system/tooling, script]
card: docs/systems/tooling.md
file: scripts/doc-budget.ps1
sources:
  - scripts/doc-budget.ps1
  - scripts/hooks/pre-commit
  - .github/workflows/docs.yml
verified: ce9fb6bf
---

# doc-budget.ps1

> [!question] The question
> Why can adding perfectly readable documentation make a build fail?

**Where you are:** [[Home]] › [[Tooling]] › [[Documentation guardrails]] › **doc-budget.ps1** (script)

**Open:** [doc-budget.ps1 in VS Code](vscode://file/C:/Dev/NodeGame/scripts/doc-budget.ps1)

## What it is

A deterministic structure/size gate for agent documentation, returning 0 when clean and 1 with violations otherwise. Its header explains the intention: “Docs drift when they grow into history books and when they repeat each other.” A bounded context system needs enforceable limits, not an ever-growing reading list.

## Read it in this order

1. **`$CardWords`, `$Budgets`, `$Headings`.** Cards have a 1100-word body budget and exactly the nine ordered headings from Purpose through Tests. Entry documents have individual caps.
2. **`Split-Frontmatter` and `Count-Words`.** Budgets apply to bodies; HTML comments are stripped. Counting uses non-whitespace matches, not model tokens.
3. **`Test-History`.** Outside comments and fenced code, reject date patterns, issue/PR-number patterns and line-reference patterns. These are mechanical patterns, not semantic recognition of all history.
4. **The card loop.** Check system name against filename, summary length, sources key and verification SHA shape, then headings and body size. Presence of `sources:` is not validation that each path exists.
5. **Global-only checks.** Check other document budgets, card links in README/map and equal marked simulation-boundary blocks in `CLAUDE.md` and `AGENTS.md`.
6. **The error list and exit.** Violations are collected so one run shows the work to fix.

## Lessons to take away

`-Only ui-art,tooling` narrows to those cards and skips global checks. A targeted pass cannot certify the routing map, entry-file budgets or boundary equality.

The docs workflow invokes global mode on pushes and pull requests. The opt-in pre-commit hook runs it when staged paths touch `docs/`, `CLAUDE.md` or `AGENTS.md`. The learning vault is outside this check's card template and budgets; this script does not validate its shell skeleton.

An agent cannot resolve a size failure by claiming the extra detail is useful. The hook's instruction is “Cut the docs, do not raise the budget.” Preserve the contract and move explanatory depth to the appropriate place. Conversely, passing this gate says nothing about whether a concise claim is factually correct; that still needs source review and [[okf-stale.ps1]]'s triage.

## Check yourself

> [!question]- Does a valid `verified: abcdef0` prove someone read the source?
> No. It passes a shape check. This script does not inspect the review process or establish the truth of that stamp.

> [!question]- Why ban line numbers in cards?
> They age as source shifts. The checker keeps cards focused on current contracts and file-level pointers, with history elsewhere.

## Go deeper ↓

This is the lowest shell. Below it is the code itself. [[context.ps1]] consumes the bounded cards this gate protects.

## My understanding

<!-- Yours. AI never edits below this line. -->
