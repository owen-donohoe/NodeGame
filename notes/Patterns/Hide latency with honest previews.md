---
shell: pattern
seen-in:
  - "[[Pending orders]]"
  - "[[Command pipeline]]"
  - "[[Smooth movement between ticks]]"
  - "[[Gesture recognition]]"
tags: [pattern]
---

# 🔁 Hide latency with honest previews

> [!question] The question
> The game can't act on your order for half a second. How do you make it *feel* instant
> without the screen ever claiming something the game hasn't done?

**Kind:** pattern. An idea that recurs at different shells of this codebase.

## The pattern

**Show intent immediately, in a visibly provisional style. Show results only once the
authority (the simulation) produces them. Hand over between the two without a gap or a double.**

## Where you've seen it

| Where | The honest preview | What it never fakes |
|---|---|---|
| [[Pending orders]] | Faint dashed route plus a pulse, the moment you order | Villager movement |
| [[Gesture recognition]] (order-drag) | Route drawn while your finger is still down | The order itself (sent on drop) |
| `CommandSystem.IssueMoveTo` | Destination node highlight | Arrival |
| [[Smooth movement between ticks]] | Smooth position *between* two real ticks | A position past the latest tick |

## What it buys

Responsiveness without any rollback or prediction errors on the game's own state.

## What it costs

- **Two visual paths** that must stay in step (provisional and real).
- **Refusals look like fades**, because the preview can't know the order will be refused.

## The contrast

Shooters do *client-side prediction*: they move your character immediately and correct it
later. That's a dishonest preview with reconciliation. It's right for twitch games, but it
would break lockstep's promise that the screen *is* the simulation.

## My understanding

<!-- Yours. AI never edits below this line. -->
