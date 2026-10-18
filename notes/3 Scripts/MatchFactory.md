---
shell: 3
up: "[[Building tick zero]]"
tags: [shell/3, realm/lobby, system/board, script]
file: Assets/Scripts/Game/Simulation/MatchFactory.cs
sources:
  - Assets/Scripts/Game/Simulation/MatchFactory.cs
verified: ce9fb6bf
---

# MatchFactory

> [!question] The question
> The one function every match in every environment starts from: how is it laid out, and what does each step guarantee?

**Where you are:** [[Home]] › [[Lobby]] › [[Board]] › [[Building tick zero]] › **MatchFactory** (script)

**Open:** [MatchFactory.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Simulation/MatchFactory.cs)

## What it is

"Builds a match's starting SimulationState without Unity. The live game, the match-log referee and headless play (bot training, balance runs) all start a match here, so there is one copy of the rules for what a board looks like at tick 0. A second copy would be free to drift, and a referee whose starting board differed from the players' would reject every honest log."

Also defined here: `PlayerSetup { suits, districts, suitEras, districtEras }`, which is what one player brings.

## Methods in call order

| Method | Does |
|---|---|
| `Configure(balance, board)` | Sets every static the simulation reads. Must come first |
| `Build(...)` / `Fill(state, ...)` | New state, or populate an existing one (the live game's state object already exists) |
| `RequireBuildable(board, draft)` | Validate or throw before touching anything |
| `CellToNode` / `NodeToCell` | Sparse grid ↔ node ID maps, ascending cell order |
| `BuildNodes` | Nodes, links, fixed placements, then draft placements (unowned, placer's era) |
| `CellLinks(x, z, …)` | Left, right, down, up; missing neighbours skipped |
| `InitializePlayers` | Cores by position: P0 highest Z |
| `InitializeVillagers` | Starting units |

## Lesson

**One constructor for a shared world.** Whenever several environments must agree on an initial state, give them a single builder and test it. This class is the [[Same code, three places]] pattern in its purest form.

## Check yourself

> [!question]- What does `Fill` guarantee if the board is invalid?
> Nothing is modified. "A refused board leaves it untouched", because validation runs before mutation.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
