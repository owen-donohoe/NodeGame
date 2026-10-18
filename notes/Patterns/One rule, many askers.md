---
shell: pattern
seen-in:
  - "[[Economy]]"
  - "[[District actions]]"
  - "[[Storehouse collection]]"
  - "[[Draft]]"
  - "[[Board]]"
  - "[[Match UI]]"
  - "[[Build identity handshake]]"
tags: [pattern]
---

# 🔁 One rule, many askers

> [!question] The question
> A Recruit button, the command processor and the auto-recruit pass all need to know "can this Village recruit right now?" Why is there exactly one function that answers, and why does it never change anything?

**Kind:** pattern. An idea that recurs at different shells of this codebase.

## The pattern

**When several places need the same decision, write the decision once as a read-only query, and let every caller ask it. Changing state stays a separate, explicit step owned by one place.**

If each caller wrote its own check, they'd drift: the button would promise something the processor refuses, or the draft highlight would show a cell the peer rejects.

## Where you've seen it

| Shell | The one rule | Who asks it | Who writes |
|---|---|---|---|
| 1 | `NodeActionRules.CanRecruit` / `CanForgeMinion` / `CanUpgradeFortress` ([[District actions]]) | Node sheet buttons, `CommandProcessor`, the auto-recruit pass | `CommandProcessor` only |
| 2 | `BankRules.CollectionState` ([[Storehouse collection]]) | Bank status UI, collection pass | The tick's collection pass |
| 3 | `PlacementLegality.CanPlace` ([[PlacementLegality]]) | Local confirm, received packet, timeout, bot, rig, highlight | `DraftState.Apply` |
| 2 | `DistrictHealth.Healthy` | Every passive effect | The claim/health pass |
| 3 | `MatchSetup.Verify` ([[MatchSetup]]) | Peer setup agreement, and (by the same idea) the referee's map check | Nobody; it's a gate |
| 1 | `CommandEligibility` ([[Match UI]]) | UI refusal reasons | The processor revalidates |

## What it buys

- **Agreement by construction**: a button can't disagree with the rule that enforces it.
- **Testable rules**: a pure query is easy to test exhaustively (`DraftLegalityTests`, economy refusal tests).
- **Safe UI**: asking is harmless, so presentation can ask freely ([[Presentation reads, never writes]]).

## What it costs

- **Asking isn't enough.** A "yes" when drawn can become "no" by the time the command applies, so the writer must ask again.
- **Refusal order is a design choice.** When several reasons apply, the rule decides which one the player sees.
- **Some UI checks still mirror processor checks** (the commands card's pressure point), and every mirror is a place this pattern hasn't reached yet.

## Spot it yourself

> [!question]- Find a decision in the codebase that two places compute separately. What would it take to make it one rule?
> Open-ended. The commands card names one: some `CommandEligibility` checks duplicate processor checks outside the shared node helpers. Moving them into a shared helper both call is the fix.

## My understanding

<!-- Yours. AI never edits below this line. -->
