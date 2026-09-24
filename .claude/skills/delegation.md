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

## What a delegated agent actually costs

Measured 2026-09-20 from `~/.codex/sessions/<date>/rollout-*.jsonl`. Six Codex
agents, `gpt-6-astra` at high reasoning except where noted, dispatched within
six minutes of each other:

| Task | Turns | Tokens |
|---|---:|---:|
| Audit `View/Outline/` (~1,900 lines, unfamiliar) | 23 | 1.89M |
| Camera audit, three sections of one issue | 21 | 1.27M |
| Dead-code sweep, two items, files named for it | 21 | 1.25M |
| Write lobby tests for one class | 18 | 0.89M |
| Two research questions, `gpt-5.6-luna` at low | 18 | 0.86M |
| Handshake audit (died at the limit, 8 turns in) | 8 | 0.22M |

A second wave later the same day, with the estimates above in hand:

| Task | Turns | Tokens |
|---|---:|---:|
| Web research: this repo's doc system vs published practice | 34 | **4.13M** |
| Fix one issue, file and candidate fix named in the prompt | 14 | **0.70M** |

A third wave, 2026-09-21, two feature implementations with files, decisions
and verification commands named in the prompt (estimated 2.4M together):

| Task | Tokens |
|---|---:|
| Four indicator producers plus a gate extraction, 6 commits, 12 tests | **1.20M** |
| Emotes end to end: packet, limiter, settings v3, HUD, 27 tests | **1.41M** |

Both fall on the "bounded, files named" and "finds its own way" estimates
below, and both needed a review pass: `done:` commit prefixes (which mark
Notion tasks on `/update`), one UI overlap, and one per-poll allocation.

**Total 6.4M tokens in about 35 minutes. That took the Codex budget from 0% to
100%.** The limit reset roughly five hours after the first call, so treat the
Codex allowance as **~6M tokens per rolling 5-hour window** until measurement
says otherwise.

Three things that table says, which are not obvious:

- **Output is noise. Input is everything.** Across all six, output was 5–9k
  tokens each — under 1%. Cost is `turns × context size`, because the whole
  conversation is re-sent every turn. An agent that takes five more turns to
  find a file pays for its entire context five more times.
- **Reasoning effort is not the lever.** The cheap `luna` profile on low
  reasoning still cost 0.86M, within 30% of `astra` on high. Turn count and
  file size set the price; the model tier barely moves it.
- **Audits are the worst ratio.** The most expensive agent produced the least:
  1.89M tokens for two small commits, because auditing means reading
  everything and changing almost nothing.

Those two are the controlled experiment for everything below.

- **Naming the file and the candidate fix halves the cost.** 0.70M against
  1.27M for a comparable task the day before, 14 turns against 21. Same model,
  same settings. The difference was the prompt.
- **Web research is the most expensive shape there is, by a wide margin.**
  4.13M for one agent — 69% of the window, alone. Every fetched page lands in
  context and is re-sent every turn afterwards. Budget a web-research task as
  most of a window, or split the questions across two waves.

Working estimate before dispatching:

| Shape | Estimate |
|---|---|
| Bounded task, files and fix named | **0.7M** |
| Bounded task, agent finds its own way | **1.2M** |
| Audit over large unfamiliar files | **1.9M** |
| Anything with web search | **4M+** |

Multiply by the number of agents and compare against what is left in the
window. Five agents is 5–9M against a ~6M window; that arithmetic could have
been done in advance, and should have been. A wave of two containing one web
research task is 4.8M, which is also too much — the pair matters, not just the
count.

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
