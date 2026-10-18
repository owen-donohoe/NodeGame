---
shell: pattern
seen-in:
  - "[[Netcode]]"
  - "[[Tick packets]]"
  - "[[CommandProcessor]]"
  - "[[GameSimulation]]"
tags: [pattern]
---

# 🔁 Treat bad input as a case you already handle

> [!question] The question
> A packet arrives corrupted. Do you write a new error path for it, or is there a cheaper,
> safer answer?

**Kind:** pattern. An idea that recurs at different shells of this codebase.

## The pattern

**Map a new failure onto a failure the system already survives. Then there's no new code
path to get wrong.**

## Where you've seen it

| Where | The new failure | Treated as |
|---|---|---|
| [[Tick packets]] | Malformed or truncated packet | **Lost** (redundancy already covers loss) |
| [[CommandProcessor]] | Illegal or stale command | **A no-op** (refusal leaves state untouched) |
| [[GameSimulation]] | Broken movement invariant | **Idle** (a normal state), recovered deterministically |
| [[Pending orders]] | Refused order | **Timeout** (the existing exit) |

## What it buys

Fewer branches, fewer untested paths, and no crash surface for hostile input.

## What it costs

Silence. A malformed packet is just a warning in the log, and a refused order is a fading
line. Diagnosis needs logs or counters (`stats.dupInputs`, warnings).

## My understanding

<!-- Yours. AI never edits below this line. -->
