---
shell: 3
up:
  - "[[Input]]"
  - "[[Command pipeline]]"
tags: [shell/3, realm/match, system/input, script]
file: Assets/Scripts/Game/Input/TapRouter.cs
sources:
  - Assets/Scripts/Game/Input/TapRouter.cs
verified: ce9fb6bf
---

# TapRouter

> [!question] The question
> A tap landed on node 12. Is it an order, an inspection or a deselect? Where is that decided, and why only there?

**Where you are:** [[Home]] › [[Match]] › [[Input]] › **TapRouter** (script)

**Open:** [TapRouter.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Input/TapRouter.cs)

## What it is

"Decides what a tap means, once, in one place." A 233-line MonoBehaviour that subscribes to
`PointerGestureSource` tap events and dispatches each to exactly one of `SelectionSystem`,
`CommandSystem`, `NodePanelManager` or `CameraController`.

## Read it in this order

1. **The header comment:** before this class, "the order those three ran in was the real
   arbitration, and it was implicit." The ladder below makes it explicit.
2. **`HandleTap`:** the ladder, as a `switch` on target kind:
   - **Villager** → `selection.TapVillager` (add/remove by default). Not selectable? Treat it as empty ground.
   - **Node with a selection** → `commands.IssueMoveTo(node)`. *This is where orders come from.*
   - **Node without a selection** → `panel.OpenForNode(node)`.
   - **Empty ground** → `ClearEverything()`.
3. **The other handlers:** `HandleHold` (info), `HandleDoubleTapNode` (select idle residents),
   `HandleTripleTapNode` (select all residents), `HandleTwoFingerTap`, `HandleDoubleTapGround`.
4. **`Subscribe` / `Unsubscribe` with the `subscribed` flag:** safe against `OnEnable` running twice ([[Events and observers]]).

## Design lesson

**Make arbitration explicit.** When several systems react to the same input, the order in which
they happen to run *becomes* the rules. Writing that order down as one ladder turns invisible
behaviour into readable code.

## Check yourself

> [!question]- You have villagers selected and tap the node they're all standing on. What happens?
> `AreAllResidentsSelected(node)` is true, so the tap returns without ordering. Ordering villagers to the node they already stand on would do nothing useful.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
