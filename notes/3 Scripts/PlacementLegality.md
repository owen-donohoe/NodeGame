---
shell: 3
up:
  - "[[Building tick zero]]"
  - "[[Board]]"
tags: [shell/3, realm/lobby, system/board, script]
file: Assets/Scripts/Game/Simulation/PlacementLegality.cs
sources:
  - Assets/Scripts/Game/Simulation/PlacementLegality.cs
verified: ce9fb6bf
---

# PlacementLegality

> [!question] The question
> "Can this district go on this cell?" Six different places ask it. Why is the answer only allowed to live in one 57-line file?

**Where you are:** [[Home]] › [[Lobby]] › [[Board]] › **PlacementLegality** (script)

**Open:** [PlacementLegality.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Simulation/PlacementLegality.cs)

## What it is

"The one spatial rule for putting a drafted district on a board… Every path that places or previews a district (local, received, timeout, rig, highlight) asks this and nothing else."

## `CanPlace`, top to bottom

```csharp
if (!IsDraftable(district)) return false;            // active roster, not None/Core
if (x, z out of bounds) return false;
if (terrain / slots arrays missing or wrong size) return false;   // defensive, never index past the end
int cell = z * board.gridCols + x;                   // row-major index
if (occupied[cell]) return false;
if (IsFixedCell(board, x, z)) return false;          // the Cores are always taken
if (!board.districtSlots[cell]) return false;
return district == Pier ? terrain[cell] == Lake : terrain[cell] == Land;
```

Notice `long cells = (long)gridCols * gridRows;`: a multiplication done in `long` before comparing lengths, so an absurd board size can't overflow.

## Why one file

If the draft UI highlighted cells by one rule and the receiver validated by another, a placement could be shown as legal, then refused by the peer, and the two drafts would diverge. A single predicate makes that impossible.

## Check yourself

> [!question]- Which check stops a Pier being placed on Ocean?
> The last line: a Pier needs `Lake`. (Ocean cells also have no district slot.)

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
