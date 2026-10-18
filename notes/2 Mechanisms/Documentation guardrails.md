---
shell: 2
up: "[[Tooling]]"
down:
  - "[[context.ps1]]"
  - "[[doc-budget.ps1]]"
  - "[[okf-stale.ps1]]"
concepts: []
tags: [shell/2, realm/lobby, realm/match, realm/backend, system/tooling]
card: docs/systems/tooling.md
sources:
  - scripts/context.ps1
  - scripts/doc-budget.ps1
  - scripts/merge-doc-review.ps1
  - .github/workflows/merged-docs.yml
  - scripts/okf-stale.ps1
  - scripts/okf-stale-hook.ps1
  - scripts/hooks/pre-commit
  - .github/workflows/docs.yml
  - CLAUDE.md
verified: c727e9ad
---

# Documentation guardrails

> [!question] The question
> How do you stop an AI agent from loading a huge pile of stale prose, then confidently repeating it?

**Where you are:** [[Home]] › [[Tooling]] › **Documentation guardrails** (mechanism)

## The problem

Documentation can fail in opposite ways: too much detail to read, or a compact statement that no longer matches the code. A larger context window does not decide which paragraph is true. The project gives agent cards a bounded format and named sources, then separates loading, structure checks and review signals.

## The idea

**Load only the task's slice, mechanically enforce the card's shape and size, and use source ancestry to choose what a reader must reverify.**

## How it works here

| Stage | Tool | What the agent learns |
|---|---|---|
| Before work | [[context.ps1]] | Full owned cards; neighbors' Purpose/Interface only |
| Structural gate | [[doc-budget.ps1]] | Whether bounded cards follow the required template and routing |
| Freshness triage | [[okf-stale.ps1]] | Which declared sources moved after their verification stamp |
| Session reminder | `okf-stale-hook.ps1` | Nonzero freshness report injected as hook context |
| Commit reminder | `scripts/hooks/pre-commit` | Blocking mechanical checks plus advisory freshness |

The context pack follows direct `Connects to` neighbors, not their entire implementation graphs. A hard-coded simulation-system list decides whether to append the determinism contract. The size estimate is approximate, not tokenizer output.

The budget checker caps a system card's body at 1100 whitespace-separated words, excluding HTML comments and frontmatter. It checks ordered headings, frontmatter shape and history exclusions. Global mode additionally checks entry-file budgets, card routing/listing, and identical simulation boundary blocks in `CLAUDE.md` and `AGENTS.md`. It does not validate the learning vault's skeleton.

Freshness asks whether a source's substantive last commit is an ancestor of the document's stamp. Markdown frontmatter-only changes are skipped when finding that substantive commit, preventing one review stamp from triggering endless review of other documents. Pending tracked staged/unstaged source edits escalate too. Committed C# changes are triaged using declaration-shaped diff lines; method-body-only edits appear as Touched. Non-C# files escalate. Directory sources use the declaration heuristic over their diff, an important limitation for directories full of markup.

The checker itself says, “Trust it to triage, not to decide.” Its search roots are `docs`, `notes` (this vault, added with it) and the three listed `.claude` directories. A note is only checked because its directory is listed there; carrying `sources` and `verified` fields is not enough on its own.

## Why this, not that

| Decision | Alternative | Gain | Cost |
|---|---|---|---|
| Bounded cards plus interface neighbors | Load every document | Keeps the task's ownership and contracts visible | Out-of-slice questions must be surfaced |
| Build failure for mechanical format/size rules | Ask agents to remember every rule | Repeatable enforcement | Requires cutting prose and maintaining routing |
| Ancestry plus declaration heuristic | Warn on every touched line | Less warning noise | Real behavior drift can land in Touched |
| Advisory freshness at commit | Block until the review stamp exists | Allows a follow-up stamp to name the source commit | Someone must actually reread the diff |

## What it costs later

Sources and stamps become maintained data. Missing or untracked sources generate warnings, not a failed exit by themselves. Directory paths need care, including the trailing slash used for pending descendants. The scripts can point at a review obligation but cannot perform it; stamping without reading defeats the arrangement.

## Traps

- “Current” means no escalated change was found, not that every sentence was proven.
- `-Only` budget mode checks selected cards, not global budgets, routing or boundary equality.
- Hooks are opt-in per clone. CI is a separate enforcement path for the doc budget.
- Session context assigns re-reading and follow-up stamping to the committing agent, matching `CLAUDE.md`. The merged-PR workflow refreshes [[Merge review]] and a GitHub tracking issue from the same findings; it never stamps or rewrites explanatory prose.

## Patterns you've seen before

- 🔁 [[Pure core, Unity shell]]: the related principle is to separate mechanical decisions from work requiring a human or Editor context.
- 🔁 [[Same code, three places]]: session and commit reminders reuse one freshness checker instead of inventing different freshness policies.
- 🔁 [[Constants come from measurements]]: freshness comments explain that noisy warnings motivated declaration-based triage; its limits remain explicit.

## Check yourself

> [!question]- An agent changes damage logic inside an existing C# method. The card appears only as Touched. Can you skip reading it?
> No. The declaration heuristic can miss changed behavior. Review the relevant claims against the actual diff.

> [!question]- Why ignore markdown changes that only update verification frontmatter?
> Otherwise reviewing one document could stale every document citing it, and their new stamps could perpetually stale one another.

## Go deeper ↓

- [[context.ps1]] · [[doc-budget.ps1]] · [[okf-stale.ps1]]

Sideways → [[Test evidence]] · [[Determinism receipts]]

## My understanding

<!-- Yours. AI never edits below this line. -->
