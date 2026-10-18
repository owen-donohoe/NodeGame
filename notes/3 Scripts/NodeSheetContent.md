---
shell: 3
up: "[[Match UI]]"
tags: [shell/3, realm/match, system/match-ui, script]
file: Assets/UI/Scripts/Gameplay/NodeSheetContent.cs
sources:
  - Assets/UI/Scripts/Gameplay/NodeSheetContent.cs
verified: ce9fb6bf
---

# NodeSheetContent

> [!question] The question
> The Recruit button on a Village sheet: what code turns that press into a game action, and what stops it acting on a stale screen?

**Where you are:** [[Home]] › [[Match]] › [[Match UI]] › **NodeSheetContent** (script)

**Open:** [NodeSheetContent.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/UI/Scripts/Gameplay/NodeSheetContent.cs)

## What it is

The abstract base for every node sheet's content. "There are four of these and there should stay four": Forge, Core, Equip (shared by four districts) and Production (shared by three). It's a plain class, not a MonoBehaviour: "content is elements in a panel, not objects in a scene."

## Read the header's "THE BOUNDARY" paragraph, then `Send`

```csharp
protected void Send(GameCommand command)
{
    if (Input == null || State == null || NodeID < 0 || NodeID >= State.nodes.Length) return;
    if (State.nodes[NodeID].districtType != boundDistrictType) return;   // district changed since binding
    command.issuedOnTick = State.tickCount;                              // lockstep needs WHEN
    Input.EnqueueCommand(command);
}
```

Three protections in five lines:
1. **Unbound content can't send** (`Unbind` nulls `Input`): "Detached or closed content must not submit commands from stale controls."
2. **District changed?** Refuse.
3. **Tick stamp** at the moment of the press.

## The lifecycle hooks

`OnBind` (build the layout), `OnRebind` (every binding), `Refresh` (every frame; read state here), `Unbind`. This is a small template-method pattern: the base class owns the flow, and subclasses fill in the steps.

## Check yourself

> [!question]- Why is "the only way any of these changes the game is by enqueuing a GameCommand" enforced by a `protected` method?
> Subclasses can't reach `InputBuffer` any other way the base class sanctions, so every content type inherits the stale-binding and tick-stamp rules for free.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
