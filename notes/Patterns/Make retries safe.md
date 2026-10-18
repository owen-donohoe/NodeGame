---
shell: pattern
seen-in:
  - "[[Backend]]"
  - "[[Cloud]]"
  - "[[Match settlement]]"
  - "[[Write locks]]"
  - "[[Backend client]]"
  - "[[Commands]]"
  - "[[Tick packets]]"
tags: [pattern, topic/distributed-systems]
---

# 🔁 Make retries safe

> [!question] The question
> Networks lose messages, servers crash and players close the app. Why does almost every
> part of Node War that crosses a network boundary assume it might run twice?

**Kind:** pattern. An idea that recurs at different shells of this codebase.

## The pattern

**Design every cross-boundary operation so that repeating it is harmless. Then the answer to
any failure is simply "try again".** The concept behind it is [[Idempotency]].

## Where you've seen it

| Shell | Where | Repeated thing | Why it's harmless |
|---|---|---|---|
| 2 | [[Tick packets]] | Each input sent 7 times, plus resends | Stored by `forTick`, duplicates ignored |
| 1 | [[Commands]] | `SetAutoRecruit`, `Collect` | Absolute values, not toggles |
| 1 | [[Draft]] (`DraftAck`) | Acks | Cumulative counts |
| 1 | [[Backend client]] | Pending ranked uploads | The server keeps only one accepted report per player |
| 2 | [[Write locks]] | Conflicting writes | Re-read, re-decide, retry |
| 2 | [[Match settlement]] | Settlement of a match | `SettledMatchIds` guard in the same batch |
| 1 | [[Cloud]] | Allocation | Returns the existing record |

The same idea appears at the packet level, the command level and the database level. That's
what makes it a pattern of *this* codebase, not a one-off fix.

## What it buys

Simple, robust failure handling everywhere: retry loops instead of special recovery code.

## What it costs

Each operation must be designed for it. "Add 20" is natural, "set to 340 unless already done"
takes thought. Guards take space and need bounds.

## My understanding

<!-- Yours. AI never edits below this line. -->
