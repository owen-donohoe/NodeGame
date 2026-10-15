---
name: orchestrate
description: Plan, split, delegate and review a piece of Node War work as the orchestrator. Use when starting a Roadmap item or any multi-step feature, when deciding how to brief coding agents, or when judging whether a design is heading down a locally optimal path that a better architecture would replace.
---

# orchestrate

The orchestrator knows every connector and never needs the code. It works from
`docs/map.md` and the system cards, decides the shape of the work, writes briefs an
engineer can implement without guessing, and keeps the docs and the Roadmap true.

## 1. Load the slice

1. The Roadmap item (Notion **Roadmap**, data source
   `collection://9aa1895c-262d-4843-80a0-6bda9d3fcfbd`; list rows in view mode on
   `view://d9127d3c-a1b0-445c-be79-e892b8d39580`, which has no quota, or fetch the
   item's page directly). Its `Systems` property names
   the cards in scope; its body holds the decisions already made. Do not re-ask them.
2. `docs/map.md`, whole.
3. `powershell -File scripts/context.ps1 -Systems <the item's systems>`.

Stop there. Read code only to settle a specific question a card cannot answer, and
note the gap so the card gets fixed.

## 2. Systems review: before planning anything

Read the **Pressure points** of every card in scope, then answer in writing:

- **Does this work deepen a pressure point?** If it builds on a known weakness, say
  which, and what it will cost to undo later.
- **Is there a better architecture?** Name the alternative that would remove the
  problem instead of working around it. Estimate the switch cost now against later.
  A path that is "optimised" inside a bad boundary is the expensive kind of debt:
  flag it before the first brief, not after the third.
- **Which interfaces change?** Every changed interface is a contract with each
  neighbour in the map. List them; they are decided here, not by engineers.

Then check each field that applies. Write one line per field: fine, or the risk.

| Field | Question |
|---|---|
| Determinism | Does it touch `Simulation/`? Version bump, hasher, `CopyFrom`, baselines? |
| Wire and storage formats | Protocol version, log chunk, Cloud Save record shape, catalog IDs, migrations? |
| Trust | What does the client assert that the server must check? |
| Server cost and quotas | Calls per player-minute (600 cap), Cloud Save reads and writes, 20 indexed keys project-wide, push fan-out, no inbound webhooks |
| Network | Bytes per tick, latency added, behaviour under loss |
| Client performance | Per-frame allocations, draw calls, mobile thermal |
| Player-visible latency | Taps to result, loading, waits on the server |
| Tests | What proves it; what only an Editor or a device can show |
| Docs | Which cards and map lines change |

## 3. Plan

Split the work so each brief touches one or two systems and ends in a committable,
working state. Order briefs so interfaces land before their consumers. Record the
plan in the Roadmap item's body as a checklist; mark the item **Now**.

## 4. Write briefs

Each brief stands alone. An engineer with only the brief and its context pack must
be able to finish without asking. Template:

```
Goal: one sentence, the observable result.
Branch/worktree: where to work; base commit.
Systems: <a>,<b>  (engineer runs scripts/context.ps1 -Systems a,b)
Read first: docs/roles/engineer.md
Interfaces to honour: the neighbour contracts this must not break, by name.
Interface changes decided: exact new signatures, records, packets (or "none").
Expected outputs: files, types, behaviour, tests that must exist.
Out of scope: what not to touch, including tempting cleanups.
Verify: exact commands, plus any manual check to report as not done.
Commits: the expected sequence.
Report: per docs/roles/engineer.md.
```

Name files and the candidate fix when known: it halves agent cost.

## 5. Delegate

Read `delegation.md` in this skill folder for measured costs. The rules:

- Estimate before dispatch: the agent route against doing it yourself, and which
  budget it draws on. A bounded brief with files named costs about 0.7-1.2M GPT
  tokens; an audit or whole-system read costs 2-5M.
- At most two agents in flight, in waves. Reuse a standing agent per profile; start
  a fresh one only when its context contradicts the new task or is so large that a
  turn costs more than a cold start. Archive the old one.
- Re-read profiles with `list_profiles` before each wave. Defaults: GPT Engineer
  (gpt-6.1-sol) for implementation and review; Claude Engineer (Sonnet) for small,
  fully specified slices when GPT is short; Astra only when Opus is out.
- Agents work in their own worktrees and commit each step. Their sandbox may not run
  `dotnet test`; run it yourself from a worktree under `.claude/worktrees/`.

## 6. Review

- Diff against the brief: scope, interfaces honoured, outputs present.
- `dotnet test dotnet/NodeWar.sln`, `scripts/compile-check.ps1`; for `Simulation/`,
  the `determinism-guard` skill and `scripts/sim-guard.ps1`.
- Read every claim an agent makes about tests from the output, not the report.

## 7. Close

- Cards: follow the `system-docs` skill. Interface, connector, invariant or decision
  changes update the card in the same branch; a new connector updates `docs/map.md`.
- `scripts/doc-budget.ps1` and `scripts/okf-stale.ps1` clean.
- Roadmap: tick the item's checklist. It moves to **Done** when its PR merges
  (`/update` reads that from git). New future work becomes a **Future** row with its
  `Systems` set, never a note buried in a card.
