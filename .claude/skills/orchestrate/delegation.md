---
type: Skill
title: delegation
description: How much a delegated agent costs, and how to spend two budgets down together instead of emptying one.
tags: [skill, workflow, agents, budget]
generated: { by: claude-opus-5, at: 2026-09-20T11:30:00Z }
status: draft
---

# delegation

## When to use

Before dispatching any work through Paseo. Read this *before* choosing how
many agents to create, not after they are running.

No `sources:` block: nothing in this repo can make this file stale. What can
make it stale is a change to the plan, the models, or Paseo itself — re-measure
with the command at the bottom rather than trusting the numbers below.

## What a delegated agent costs

Read real totals from `~/.codex/sessions/<date>/rollout-*.jsonl` (command at the bottom);
an agent's own estimate has been off by 20×. Treat the Codex allowance as **~6M tokens per
rolling 5-hour window** until a measurement says otherwise.

- **Input is everything.** Output is under 1%. Cost is `turns × context`, because the whole
  conversation is re-sent every turn; five extra turns to find a file pay for the context
  five more times. Reasoning tier barely moves it.
- **Naming the files and the candidate fix halves the cost** (0.70M vs 1.27M for comparable
  tasks). A fresh agent per commit with only its own spec section costs 0.3–0.5M; the same
  coder kept across commits grew to 10M.
- **Audits, web research and whole-system plans are the expensive shapes.** Audits read
  everything and change little; every fetched page or file is re-sent each turn.
- **A supervisor's cost is its context.** Plan in one agent, then review from a fresh one
  whose brief names only the files in hand, working from diffs.

| Shape | Estimate |
|---|---|
| Bounded task, files and fix named | **0.7M** |
| Bounded task, agent finds its own way | **1.2M** |
| Multi-commit feature (per commit) | **0.4–0.5M**, plus 15–30% auto-review |
| Audit over large unfamiliar files | **1.9M** |
| Doc re-verification of a large cluster | **~3M** |
| Whole-system plan, or anything with web search | **4M+** |

Multiply by the number of agents and compare against what is left in the window. A Claude
Sonnet agent with the design fully specified is roughly 10× cheaper than Codex for the same
bounded task, and spends the Claude budget instead.

## The rule

**Two Codex agents in flight at once. No more.** Dispatch a wave, wait for both,
then dispatch the next wave. A wave of two costs ~2M — a third of the window —
and leaves room to change course based on what the first wave found.

**Budget half the window per session, not all of it.** The remainder is not
waste; it is what is left for the user's own Codex use and for a second wave
after a finding changes the plan. Emptying a budget is not the same as using it.

**Both budgets, deliberately.** The point of running Anthropic and OpenAI side
by side is to approach 100% on both, not 100% on one. Before each wave, decide
which budget the work should come from and say so. Rough split: Claude
orchestrates, reviews, and does work where the context is already loaded;
Codex takes bounded implementation and anything wide enough to be worth paying
a cold start for.

**Do small, well-specified work yourself.** A delegated agent pays ~1M tokens
to rebuild context this session already holds. On 2026-09-20 the four
simulation test files were written directly, and cost a small fraction of what
one agent spent producing two commits. Delegation buys parallelism and
context isolation — it does not buy cheapness.

**Kill or reuse deliberately:**
- **Reuse** an idle agent for a follow-up that shares its context. It has
  already paid to read those files.
- **Kill** it before handing out an unrelated task. Its old context rides
  along in every later turn, so an unrelated second task on a warm agent costs
  more than a fresh one, not less.

## Making a dispatched task cheaper

Every item here removes turns, and turns are the whole cost.

- **Name the files.** "Audit `View/Outline/`" costs an exploration phase.
  "Audit these ten files, listed" does not.
- **Hand over what has already been checked.** Three of this repo's open issues
  were stale on 2026-09-20. An agent rediscovering that spends real turns on it.
  Say what is already fixed and cite the line that settles it.
- **Cap the scope at one issue, or one section of one issue.** Three sections
  in one prompt cost 1.27M; the same work in three prompts would cost more,
  but a prompt that grows without bound cannot be estimated at all.
- **Require a commit per step.** An agent killed by a usage limit dies
  unrecoverably and mid-edit. On 2026-09-20 one lost an uncommitted change that
  had to be salvaged by hand from its worktree. Committed work survives.
- **Give it the `Library` junction line** — see `nodewar-tooling-gotchas`.
  Without it `compile-check.ps1` fails in a fresh worktree and the agent spends
  turns diagnosing a tooling problem instead of doing the task.

## Measuring afterwards

Codex writes a full rollout log per session. This prints the cumulative cost of
each agent run on a given day:

```bash
cd ~/.codex/sessions/<YYYY>/<MM>/<DD>
for f in *.jsonl; do
  cwd=$(head -1 "$f" | grep -o '"cwd":"[^"]*"' | head -1)
  tot=$(grep -o '"total_token_usage":{[^}]*}' "$f" | tail -1)
  echo "$cwd $tot"
done
```

`total_token_usage` is cumulative for the session; the bare `total_tokens` key
also appears inside each turn's `last_token_usage`, so match the outer object
or the number will be one turn's worth rather than the run's.

Do this after any large fan-out and correct the estimates above. A guess that
is never checked against the log is how a 6M window gets spent in 35 minutes.
