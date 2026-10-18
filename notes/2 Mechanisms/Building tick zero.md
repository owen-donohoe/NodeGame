---
shell: 2
up: "[[Board]]"
down:
  - "[[MatchFactory]]"
  - "[[PlacementLegality]]"
tags: [shell/2, realm/lobby, system/board]
sources:
  - Assets/Scripts/Game/Simulation/MatchFactory.cs
  - Assets/Scripts/Game/Simulation/MapAuthoringRules.cs
verified: ce9fb6bf
---

# Building tick zero

> [!question] The question
> A grid of terrain plus a list of drafted districts goes in; a complete `SimulationState` comes out. What happens in between, and why is the order of node IDs a rule?

**Where you are:** [[Home]] › [[Lobby]] › [[Board]] › **Building tick zero** (mechanism)

## The problem

The board is authored as a rectangular grid, but the game is a *graph* of nodes. Ocean cells and undrafted Lake cells aren't nodes. Node IDs, link order and starting villagers must come out *identically* everywhere, because IDs are in every command and link order affects pathfinding.

## The idea

**Validate first (refuse with an exception before touching anything), then build in a fixed order: cell → node mapping in ascending cell order, links in left-right-down-up order, fixed placements, drafted placements, players, villagers.**

## How it works here

1. **`MatchFactory.Configure(balance, board)`:** installs the static balance and path multipliers the simulation reads. Must run first.
2. **`RequireBuildable`:** `MapAuthoringRules.ValidateBoard` + `ValidateDraft` (every placement legal against the board and those before it). Throws `ArgumentException`; "nothing is guessed or defaulted".
3. **`CellToNode`:** every Land cell, plus Lake cells with a drafted Pier, gets an ID in ascending row-major cell order, "gaps closed up".
4. **`CellLinks`:** "Left, right, down, up: the order the live game has always used, and link order is part of the simulation (pathfinding visits links in it)." A missing neighbour is skipped, never jumped.
5. **Placements:** the board's fixed ones (Cores, era 0), then drafted ones: *unowned*, Fixed, at the placer's era.
6. **`InitializePlayers`:** Cores found *by position*: P0 owns the highest-Z Core, P1 the lowest. Exactly two Cores on different rows, or throw.
7. **Starting villagers** and resources from the board.

`Build` returns a new state; `Fill` populates an existing one (the live game creates its state early and hands that same object to everything that reads it).

## Check yourself

> [!question]- Why find Cores by position instead of trusting each placement's `ownerID`?
> "So a misconfigured board cannot hand a player the wrong core." The geometry is the authority, and the ownership field is forced to match it.

> [!question]- Someone changes `CellLinks` to up-down-left-right. What breaks?
> Pathfinding visits links in that order, so equal-cost routes could resolve differently: a same-inputs behaviour change. That needs a `SimulationVersion` bump, and old replays stop verifying.

## Go deeper ↓

- [[MatchFactory]] · [[PlacementLegality]]

## My understanding

<!-- Yours. AI never edits below this line. -->
