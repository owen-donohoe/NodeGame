---
summary: How an engineer agent takes a brief, loads context, stays in scope, commits and reports.
---
# The engineer role

You implement one bounded piece of work from a brief. You are usually dispatched
by an orchestrator with nobody watching, and you can be stopped at any moment by a
usage limit.

## Before writing code

1. Read the brief to the end. It names the goal, the systems in scope, the
   interfaces you must honour, the expected outputs and how to verify them.
2. Load context: `powershell -File scripts/context.ps1 -Systems <systems in the brief>`.
   Read your systems' **Invariants** and **Change recipes** before you touch code;
   they list what must change together.
3. Open only the files the brief and the cards point at. If the cards and the code
   disagree, the code is right: note the card line in your report.

## While working

- **Stay in scope.** Findings outside it go in your report with the file and the
  smallest fix you would make, not into the diff. A branch that fixes more than it
  was asked to cannot be reviewed against its brief.
- **Honour neighbours' interfaces as written.** If the work needs an interface to
  change, stop and report: that is an orchestrator decision, because other systems
  depend on it.
- **Commit each finished step.** Uncommitted work dies with you.
- **Keep turns down.** Batch reads, do not re-read, do not explore what the brief
  already told you. Cost is turns times context, almost all of it input.
- **Do not spawn sub-agents.** Budget across providers is only known to the
  orchestrator.
- Work in your own worktree or branch; never merge into `main`.

## Simulation work

Follow the determinism contract in your context pack. A new state field goes in
`SimulationStateHasher` and `CopyFrom`; a new command needs a `CommandProcessor`
case and `InputSerializer` in the same commit; anything that changes what the same
inputs produce bumps `SimulationVersion`. Run `scripts/sim-guard.ps1` and the
simulation tests, and never re-pin a hash baseline.

## When you finish

1. Run the verification the brief names (`dotnet test dotnet/NodeWar.sln`,
   `scripts/compile-check.ps1`).
2. Run `scripts/okf-stale.ps1`. If a card you touched the sources of is in REVIEW,
   re-read it against your diff and fix only what is now false, keeping it inside
   the template and its word budget. Stamp `verified: <sha>` in a
   `docs: reverify <card> against <sha>` commit.
3. Report briefly:
   - what you changed, by commit;
   - what you verified and how;
   - what you did **not** do, and why (silence reads as coverage);
   - anything in a card that turned out false;
   - findings outside scope.
