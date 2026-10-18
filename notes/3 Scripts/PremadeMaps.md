---
shell: 3
up: "[[Map identity]]"
tags: [shell/3, realm/lobby, system/board, script]
file: Assets/Scripts/Game/Simulation/PremadeMaps.cs
sources:
  - Assets/Scripts/Game/Simulation/PremadeMaps.cs
verified: ce9fb6bf
---

# PremadeMaps

> [!question] The question
> Where does the actual map live, and why is it C# code rather than a Unity asset?

**Where you are:** [[Home]] › [[Lobby]] › [[Board]] › [[Map identity]] › **PremadeMaps** (script)

**Open:** [PremadeMaps.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Simulation/PremadeMaps.cs)

## What it is

"The shipped map definitions, as plain data. The one map today is `hourglass-01`; the live game, the rig and the Cloud Code referee all build their board here, so there is a single copy of the terrain to disagree with." `PremadeMaps.Catalog` is the `IBoardCatalog` everyone checks against.

## Things to notice

- **Fresh copy per call:** "Each call returns a fresh board, so a caller may mutate what it gets." No shared mutable map.
- **Symmetry is declared per map** (`MapSymmetry.FlipZ` or `HalfTurn`). `hourglass-01` mirrors top to bottom. `Mirror(...)` maps a cell to its counterpart, which authoring rules use to check that the map is fair.
- **Code, not an asset:** a Unity `ScriptableObject` can't be loaded by the Cloud Code referee. Plain C# compiles into the client, the server and the rig alike.

## Check yourself

> [!question]- What must happen on the server when a second map ships?
> The server's build must include the same `PremadeMaps` (redeploy the Cloud Code module). Otherwise the referee refuses logs naming the new map as "unknown map".

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
