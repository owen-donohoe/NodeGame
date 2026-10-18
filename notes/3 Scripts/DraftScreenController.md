---
shell: 3
up:
  - "[[Match UI]]"
  - "[[Draft turns]]"
tags: [shell/3, realm/lobby, system/match-ui, script]
file: Assets/UI/Scripts/Gameplay/DraftScreenController.cs
sources:
  - Assets/UI/Scripts/Gameplay/DraftScreenController.cs
verified: ce9fb6bf
---

# DraftScreenController

> [!question] The question
> Dragging a district card from the UI onto the 3D board: how is one gesture that crosses two worlds handled, without the UI owning any draft rules?

**Where you are:** [[Home]] › [[Lobby]] › [[Draft]] › **DraftScreenController** (script)

**Open:** [DraftScreenController.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/UI/Scripts/Gameplay/DraftScreenController.cs)

## What it is

The UI Toolkit draft screen (~1,440 lines), implementing `IDraftPresenter`. It owns the chrome (banner, timer, card bar, the proxy under the finger, Confirm) *and* the placement interaction, "because on this stack they cannot be separated: the drag starts on a UI Toolkit element and ends on the 3D board". It asks `DraftManager` only two things: is it my turn, and please confirm this placement.

## Read the header's sections. Each is a lesson in touch UX

- **Three input routes, one parked state:** tap card then tap cell; tap card then press-and-move on the board; drag card onto a cell. "All three end in the same parked state, which is why there is one `handSlot` and not a mode flag."
- **A card press resolves late:** under the slop it was a tap, past it a drag. Nothing is shown until that's decided, "because a card that armed itself on touch-down would flicker under every drag."
- **A board press resolves at once:** "every part of a landed placement is reversible… so landing early costs nothing and waiting for the lift is a delay you can feel." Only Confirm is held back.
- **Pointer, not Mouse:** the uGUI draft reads `Mouse.current` (desktop only); this one uses pointer events, so touch works.

## Check yourself

> [!question]- Why can the board accept a piece instantly, while a card must wait to see whether it's a tap or a drag?
> Landing on the board is reversible (move it, pick it up, cancel), so being early costs nothing. Arming a card shows a visible state change that would flicker on every drag.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
