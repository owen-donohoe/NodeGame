---
shell: 3
up: "[[Referee]]"
tags: [shell/3, realm/backend, system/cloud, script]
file: dotnet/NodeWarCloud/NodeWarCloud/MatchEligibility.cs
sources:
  - dotnet/NodeWarCloud/NodeWarCloud/MatchEligibility.cs
verified: ce9fb6bf
---

# MatchEligibility

> [!question] The question
> A log can replay perfectly and still be cheating, for example using a suit the player doesn't own. Where is that caught?

**Where you are:** [[Home]] › [[Backend]] › [[Match log]] › [[Referee]] › **MatchEligibility** (script)

**Open:** [MatchEligibility.cs in VS Code](vscode://file/C:/Dev/NodeGame/dotnet/NodeWarCloud/NodeWarCloud/MatchEligibility.cs)

## What it is

A `static class` with one public method, `Check(record, log) → error or null`. It runs *before*
the referee replays, comparing the log against the **server's match record**, never against the client's word.

## The checks, in order

1. **Header matches the record:** match ID, both player IDs in order, protocol, sim, content.
2. **`CheckMap`:** the log's setup and board equal the record's server-assigned map and hash.
3. **`CheckBudgets` (protocol ≥6):** loadout size fits the player's **snapshot arena**: low arenas get
   2 suits plus the free Warrior and 2 districts, high arenas 3 suits and 5 districts. No duplicates,
   the low-arena Warrior is present, and draft placements don't exceed what was chosen plus the base pool.
4. **`CheckVariant` for every suit, district and placement:** the era is allowed for the snapshot
   arena, *and* the variant was in `OwnedVariants` **in the pre-match snapshot**.

## Design lessons

- **Validate against the snapshot.** Ownership is judged as of match start, so buying, unlocking or
  losing something mid-match can't change the verdict.
- **Return a string error, not a bool.** Every refusal tells you *why*, and the reason is stored on the report for audit.
- **Budget counting with arrays:** `budgets[p][type]++` for chosen districts, `--` per placement, and
  refuse at zero. It's simple multiset arithmetic.

## Check yourself

> [!question]- A player drafts an era-3 Archer while in Arena 2. Which check refuses it, and with what message?
> `CheckVariant`: "Drafted era exceeds the snapshot arena." (Or, for tree suits, the unlock-arena check in `SuitTree.IsAvailable`.)

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
