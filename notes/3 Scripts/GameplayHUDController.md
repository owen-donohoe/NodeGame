---
shell: 3
up:
  - "[[Match UI]]"
  - "[[Three UI stacks]]"
tags: [shell/3, realm/match, system/match-ui, script]
file: Assets/UI/Scripts/Gameplay/GameplayHUDController.cs
sources:
  - Assets/UI/Scripts/Gameplay/GameplayHUDController.cs
verified: ce9fb6bf
---

# GameplayHUDController

> [!question] The question
> The in-match HUD is ~1,800 lines. What does it decide, and what is it forbidden to decide?

**Where you are:** [[Home]] › [[Match]] › [[Match UI]] › **GameplayHUDController** (script)

**Open:** [GameplayHUDController.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/UI/Scripts/Gameplay/GameplayHUDController.cs)

## What it is

The UI Toolkit HUD (`[RequireComponent(typeof(UIDocument))]`), live only when `GameManager.useUIToolkitHUD` is on. It's the counterpart to the uGUI `HUDManager`, with an interchangeable `Initialize` signature.

## Read the header. It's a design brief

- **WHAT IT SHOWS is settled:** "both breach walls are always visible, and only the villager count collapses. Breach is the win condition, so it is first and it never hides."
- **YOU ARE ON THE LEFT:** the left wall is whoever is being controlled (maybe player 1 online), and each wall carries its player mark, "so position never has to say who is who".
- **READ-ONLY, ABSOLUTELY:** no `GameSimulation`, no `CommandProcessor`; node-sheet commands go through `InputBuffer`.
- **Namespace vs folder:** `NodeWar.UI` despite living under `Assets/UI/Scripts`. "The folder says which stack it is in; the namespace says which layer."

## Then

- **Events out** (`HoldActionClicked`, `SurrenderConfirmed`, `ReturnToLobby`): it *requests* lifecycle actions. `GameManager` and the server decide.
- **It implements `ICountdownPresenter`**, so the countdown can be drawn by either stack.
- **Timing constants** (`BannerMilliseconds = 3200`, `CardFadeMilliseconds = 220`) are tied to USS transitions.

## Check yourself

> [!question]- Why does surrender raise an event instead of ending the match itself?
> A ranked result is the server's decision. The HUD can only ask; `GameManager` sends the request and shows whatever the server returns.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
