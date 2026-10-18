---
name: system-docs
description: Keep Node War's system cards, map and router small and true. Use when a change alters a system's interface, connectors, invariants or decisions; when okf-stale reports a card in REVIEW; when adding, splitting or retiring a system; or before writing anything into docs/.
---

# system-docs

The docs exist so that work loads one slice of context: a system's card in full,
its neighbours by interface, and the map for orchestrators. They stay useful only if
they stay short and only if they say what is true now.

## What lives where

| File | Holds | Budget (words) |
|---|---|---|
| `CLAUDE.md` / `AGENTS.md` | What every agent needs every turn | 1,100 / 900 |
| `docs/README.md` | The router: one line per doc and card | 600 |
| `docs/map.md` | Every system and every connector between them; the flows; cross-cutting versions and boundaries; top pressure points | 2,000 |
| `docs/systems/<system>.md` | One system: purpose, interface, connectors, invariants, mechanism, decisions, pressure points, change recipes, tests | 1,100 |
| `docs/contracts/determinism.md` | The simulation contract | 2,600 |
| `docs/game.md` | What the game is, as a player meets it | 3,200 |
| Notion Roadmap | Future plans, current work, history | none |

`scripts/doc-budget.ps1` enforces the budgets, the card headings and the ban on
history, and fails CI.

## When a card must change

Change it in the same branch as the code when the change alters what a neighbour or
a planner relies on:

- the **Interface** (entry points, events, data shapes, records, packets);
- a **Connects to** line (also update `docs/map.md`);
- an **Invariant**, a **Decision** or a **Pressure point**;
- a **Change recipe** (what must change together).

An internal refactor that keeps all of those does not touch the card; okf-stale
may still flag it, and then re-reading and stamping is enough.

## Writing rules

- Present tense, what is true now. No dates, PR numbers, line numbers, "used to",
  "was renamed", or plans. Plans go to the Roadmap; history is git.
- Name types and files a reader must find; leave the rest to the code.
- **Interface first.** A neighbour reads only Purpose and Interface
  (`scripts/context.ps1` extracts exactly those), so those two sections must be
  enough to use the system correctly.
- **Decisions** carry the rejected alternative, one line each.
- **Pressure points** are candid: debt, scaling limits, and where a different
  architecture would pay. Planners read them before building on top.
- Over budget? Cut what the code already says. If a card is still too big, the
  system is two systems: split it.

## Re-verifying a card (REVIEW from okf-stale)

1. `git diff <verified>..HEAD -- <the card's sources>`.
2. Re-read the card against that diff. Fix only what is now false; keep the
   template and the budget.
3. Set `verified: <sha of the code commit>` and commit
   `docs: reverify <card> against <sha>` on the same branch.

## Adding a system

1. Copy `docs/systems/_template.md` to `docs/systems/<name>.md` (lowercase, hyphens).
2. Fill it; `sources:` are repo-relative files or directories ending in `/`;
   `verified:` is the commit you checked against.
3. Add it to `docs/README.md` and to `docs/map.md` (system table and connectors).
4. Mirror its connectors in each neighbour's **Connects to**.
5. Add the name to the Roadmap's `Systems` options.
6. Remove what it takes over from any other card.

## Retiring or merging a system

Delete or merge the card, remove it from README and map, fix neighbours' Connects to,
and run `scripts/doc-budget.ps1`.
