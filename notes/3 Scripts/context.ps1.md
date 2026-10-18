---
shell: 3
up: "[[Documentation guardrails]]"
down: []
concepts: []
tags: [shell/3, realm/lobby, realm/match, realm/backend, system/tooling, script]
card: docs/systems/tooling.md
file: scripts/context.ps1
sources:
  - scripts/context.ps1
verified: ce9fb6bf
---

# context.ps1

> [!question] The question
> How does “load your slice” become a repeatable command instead of an agent's judgment about what to read?

**Where you are:** [[Home]] › [[Tooling]] › [[Documentation guardrails]] › **context.ps1** (script)

**Open:** [context.ps1 in VS Code](vscode://file/C:/Dev/NodeGame/scripts/context.ps1)

## What it is

A context-pack generator. Its header describes the rule: “a piece of work loads its own systems in full and everything else by interface only.” It reads system cards, strips frontmatter and extracts sections; it does not analyze C# dependencies.

## Read it in this order

1. **Parameters and `$SimSystems`.** `-Systems` accepts comma-separated names; `-List`, or no systems, prints the inventory. The explicit simulation list is simulation, economy, commands, board and draft.
2. **`Get-Cards`, `Get-Body`, `Get-Section`.** Cards beginning with `_` are excluded; section extraction stops at the next level-two heading.
3. **The requested-card loop.** Full bodies are appended, then backticked system names in `Connects to` collect direct neighbors, excluding owned systems and duplicates.
4. **The neighbor loop.** Only Purpose and Interface are appended, with headings demoted for the combined document. It does not recurse through neighbors' neighbors.
5. **The simulation branch.** A requested name in `$SimSystems` appends the determinism contract if the file exists. This is name-based routing, not source-path discovery.
6. **Output.** Console by default; `-Out` writes relative to the current directory. The estimate multiplies whitespace-word count by 1.4 and is explicitly approximate.

## Lessons to take away

The pack makes ownership asymmetric: know your implementation deeply, know the neighbor's contract. That keeps an AI task from becoming an unbounded repository tour. The cost is that cards and connection lists must be maintained; this tool cannot detect an undocumented dependency for you.

Useful invocation for this thread:

```powershell
powershell -File scripts/context.ps1 -Systems ui-art,tooling
```

The default invocation prints text. `-Out` is a writing action and should point only where the task authorizes a file.

## Check yourself

> [!question]- If tooling connects to simulation, does requesting tooling automatically append the determinism contract?
> No. The branch checks requested system names against the explicit simulation list, not whether a neighbor is simulation.

> [!question]- Does a card's `sources` list decide which source code the pack prints?
> No. It prints card prose and selected neighbor sections, not the source files themselves. Reading the named code is a separate step.

## Go deeper ↓

This is the lowest shell. Below it is the code itself. Compare [[doc-budget.ps1]] for the structure this generator expects.

## My understanding

<!-- Yours. AI never edits below this line. -->
