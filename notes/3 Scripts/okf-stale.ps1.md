---
shell: 3
up: "[[Documentation guardrails]]"
down: []
concepts: []
tags: [shell/3, realm/lobby, realm/match, realm/backend, system/tooling, script]
card: docs/systems/tooling.md
file: scripts/okf-stale.ps1
sources:
  - scripts/okf-stale.ps1
  - scripts/okf-stale-hook.ps1
  - scripts/hooks/pre-commit
verified: 41a0df69
---

# okf-stale.ps1

> [!question] The question
> How can a script tell you which documents to reread without pretending to understand whether their claims are true?

**Where you are:** [[Home]] › [[Tooling]] › [[Documentation guardrails]] › **okf-stale.ps1** (script)

**Open:** [okf-stale.ps1 in VS Code](vscode://file/C:/Dev/NodeGame/scripts/okf-stale.ps1)

## What it is

A read-only git/frontmatter freshness checker. `$SearchDirs` includes `docs`, `notes` (this vault, added alongside it), `.claude/rules`, `.claude/skills` and `.claude/commands`. It classifies changes as REVIEW or Touched, reports expired `stale_after` dates, and emits nonfatal warnings about incomplete evidence.

## Read it in this order

1. **`Read-Frontmatter`.** Understand the supported YAML subset: compact `verified`, older `verified_at_commit`, source lists and resource entries. It is hand-parsed because Windows PowerShell 5.1 does not ship a YAML module.
2. **`Get-LastCommit` and `Test-BodyChanged`.** For Markdown sources, skip commits changing only frontmatter, searching up to 50 commits. Otherwise stamping one document could endlessly stale other documents citing it.
3. **`Test-IsAncestor`.** A source's last substantive commit must be an ancestor of, or equal to, the verification commit. Unknown git answers become warnings rather than proof.
4. **`Get-PendingChangedPaths`.** Read both staged and unstaged tracked diffs. Uncommitted source changes cannot be discovered through `git log` alone.
5. **`Test-SurfaceChanged`.** Committed C# diffs escalate declaration-shaped lines; implementation-only changes become Touched. Non-C# files escalate. Directory sources are checked using that same declaration pattern over the directory diff.
6. **The reporting tail.** REVIEW is grouped by document and fanned out by source commit. Exit 1 means REVIEW or expired documents; Touched alone and warnings do not fail it.

## Lessons to take away

“A moved source is not the same thing as a falsified claim,” says the comment introducing triage. Review is a signal to compare a claim against a diff. A method-body edit can change behavior without tripping the declaration pattern, so the final reminder matters: “Trust it to triage, not to decide.”

Pending exact source paths escalate directly; trailing-slash directory sources can catch pending descendants. Untracked paths are not discovered by the tracked-diff lists. Missing/untracked declared sources warn and are skipped, so a clean exit is not a complete audit.

The pre-commit hook intentionally ignores this check's exit status. The session wrapper is silent on zero; otherwise it packages the report into hook context. Its context requires the committing agent to re-read affected cards and notes, correct drift and stamp the reviewed source commit in a follow-up commit, matching `CLAUDE.md`. `-JsonPath` exports the same findings for the merge dashboard.

## Check yourself

> [!question]- Why not use “last modified date” to decide whether a document is fresh?
> A timestamp does not say which source revision was reviewed. A verification commit gives the checker a revision relationship to inspect.

> [!question]- Can a directory containing changed USS escape REVIEW?
> Yes. Committed directory diffs use the C# declaration heuristic, so markup changes may not match. The quiet category is not proof that the prose remains true.

## Go deeper ↓

This is the lowest shell. Below it is the code itself. Compare [[doc-budget.ps1]]: it checks structure, not source freshness.

## My understanding

<!-- Yours. AI never edits below this line. -->
