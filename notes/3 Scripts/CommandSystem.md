---
shell: 3
up: "[[Command pipeline]]"
tags: [shell/3, realm/match, system/commands, script]
file: Assets/Scripts/Game/Input/CommandSystem.cs
sources:
  - Assets/Scripts/Game/Input/CommandSystem.cs
verified: ce9fb6bf
---

# CommandSystem

> [!question] The question
> Where exactly is a Move command built, and what else happens in that same instant?

**Where you are:** [[Home]] › [[Match]] › [[Commands]] › [[Command pipeline]] › **CommandSystem** (script)

**Open:** [CommandSystem.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Input/CommandSystem.cs)

## What it is

A 241-line MonoBehaviour that turns human intent into `GameCommand`s on the `InputBuffer`.
`IssueMoveTo(node)` is "the only place a Move command is built": both desktop right-click
and touch tap end up here.

## Read it in this order

### 1. `IssueMoveTo`, the core

```csharp
for each selected villagerID:
    inputBuffer.EnqueueCommand(new GameCommand {
        type = Move, playerID = localPlayerID, villagerID = id,
        targetNodeID = node, issuedOnTick = simState.tickCount });
nodeViews[node].TriggerHighlight(color);                          // instant feedback
MoveIssued?.Invoke(node, new List<int>(selection.SelectedVillagerIDs));  // copy first!
selectionSystem.ClearSelection();
```

Four things in one frame: the commands, a highlight, an event for the [[Pending orders]]
preview, and clearing the selection. The order matters: the list is copied *before* the selection is cleared.

### 2. The order-drag events

`OrderDragBegan`, `OrderDragHover`, `OrderDragEnded` let the view preview a route *while the
finger is still down*. `HandleOrderDrop` issues the move *before* the drag ends, so the
pending route and the hover route swap in the same frame.

### 3. `Update`: legacy keyboard shortcuts

E equips Warrior (note `// TODO: suit picker UI`) and R respawns the first dead villager.
They're a reminder that this class predates the gesture system.

## Check yourself

> [!question]- Why is `issuedOnTick` stamped here, when the command won't be applied for a few ticks?
> So the log and the peers agree on *when* the player acted, not just what they did. The application tick is decided separately, by the driver.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
