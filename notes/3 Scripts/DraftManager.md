---
shell: 3
up: "[[Draft turns]]"
tags: [shell/3, realm/lobby, system/draft, script]
file: Assets/Scripts/Game/Core/DraftManager.cs
sources:
  - Assets/Scripts/Game/Core/DraftManager.cs
verified: ce9fb6bf
---

# DraftManager

> [!question] The question
> Who runs the draft's clock, network and turn machine, and how is a turn-based exchange made reliable over UDP?

**Where you are:** [[Home]] › [[Lobby]] › [[Draft]] › [[Draft turns]] › **DraftManager** (script)

**Open:** [DraftManager.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Core/DraftManager.cs)

## What it is

A MonoBehaviour (~820 lines) that `GameManager` creates, initialises, and destroys after `OnDraftComplete`. It wraps the pure [[DraftState]] with clocks, networking, bot moves and presentation helpers (`GridToWorld`, `WorldToGrid`).

## Read it in this order

1. **The header's lifecycle list and its networking rule:** "delivers nothing reliably on either transport. Every packet the peer must see is retained and resent until the peer acknowledges it, and every receive handler treats a repeat as a no-op."
2. **`RESEND_INTERVAL = 0.1`**, then `Update`: the phase machine, timer, polling and resends.
3. **`ConfirmLocalPlacement`:** the presenter's one way in.
4. **`HandleTimeout`:** `DraftState.ChooseTimeout` → apply → transmit.
5. **The central apply method:** "Called for local, remote, bot, and timeout placements, all of which have already been accepted by DraftState." It's one path for four sources ([[Treat bad input as a case you already handle]] in spirit).
6. **`CompleteDraft`:** waits in Complete until the peer has acknowledged everything, then raises `OnDraftComplete`.

## Pressure point to notice

It shares the network's inbound queue with gameplay. It must stop polling before the lockstep driver takes over, or it would consume the first tick packets.

## Check yourself

> [!question]- Why does the bot "place after a configurable delay" rather than instantly?
> Presentation: an instant bot turn looks broken and gives the player nothing to watch. The delay is presentation; the *choice* is still deterministic (nearest legal cell to its Core).

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
