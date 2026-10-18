---
shell: pattern
seen-in:
  - "[[Netcode]]"
  - "[[Tick packets]]"
tags: [pattern, topic/optimisation]
---

# 🔁 Constants come from measurements

> [!question] The question
> Why is it 6 redundant copies, and not 5 or 10? Who decided, and how would you check?

**Kind:** pattern. An idea that recurs at different shells of this codebase.

## The pattern

**When a number trades one cost against another, measure the trade-off with a repeatable
rig, pick the knee of the curve, and write the measurement next to the constant.**

## Where you've seen it

| Constant | Measured by | Recorded where |
|---|---|---|
| `REDUNDANT_INPUTS = 6` | `SweepTests` over a simulated lossy link (6 seeds, 60 s) | The comment above the constant, with the table |
| Other netcode tuning constants | The explicit `SweepTests` in `NodeWar.Network.Tests` ("explicit `SweepTests` tune constants", netcode card) | The tests themselves |
| Balance numbers | The balance rig (`dotnet/NodeWar.BalanceRig`) | Balance assets |

## What it buys

You can argue with a number, rerun the measurement after a change, and see when a constant has gone stale.

## What it costs

A rig to build and maintain, and the discipline of updating the comment when the numbers change.

## Spot it yourself

> [!question]- Find a constant in the codebase without a measurement comment. How would you measure it?
> Open-ended. For example, `PendingOrderView.TimeoutSeconds = 1.5f` could be checked against
> the worst normal tap-to-apply latency measured across real matches.

## My understanding

<!-- Yours. AI never edits below this line. -->
