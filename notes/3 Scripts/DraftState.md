---
shell: 3
up: "[[Draft turns]]"
tags: [shell/3, realm/lobby, system/draft, script]
file: Assets/Scripts/Game/Simulation/DraftState.cs
sources:
  - Assets/Scripts/Game/Simulation/DraftState.cs
verified: ce9fb6bf
---

# DraftState

> [!question] The question
> The draft's rules with no Unity, no clock and no network: what's in them, and why do they live in `Simulation/`?

**Where you are:** [[Home]] › [[Lobby]] › [[Draft]] › [[Draft turns]] › **DraftState** (script)

**Open:** [DraftState.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Simulation/DraftState.cs)

## What it is

"Complete authoritative state of the draft phase. Maintained identically on both clients from the sequence of placement events." About 380 lines: phase, current turn, turn number, both players' picks, confirmed placements, a row-major `occupied[]` grid, and consecutive timeouts.

## Read it in this order

1. **The header:** "Where a district may go is never decided here." Every path asks [[PlacementLegality]] through `CanPlace`.
2. **Availability:** `HasLegalCell`, `LegalCells` (row-major `bool[]`: the highlight, the timeout and the bot all choose from it), `IsPlayable`, `PlayerHasPlayablePick`, `IsDraftFinished` (neither player has anything playable).
3. **Acceptance:** `AcceptLocal(player, slot, x, z)` and `AcceptReceived(player, district, x, z)` return a pick index or −1 *without spending anything*; `Apply` then spends it. Two steps, so a refused choice "consumes nothing".
4. **`ChooseTimeout`:** the parked piece, else the lowest playable pick at a seeded cell.
5. **`FindRandomLegalCell(seed, …)`:** `((seed × 1103515245 + 12345) & 0x7FFFFFFF) % count`, then walk to the *n*-th legal cell. It's a one-step linear congruential generator, deterministic on both peers.
6. **`FindNearestLegalCell`:** Manhattan distance, "the lower cell index winning a tie": the ID-tiebreak rule again.

## Why it's in `Simulation/`

It's shared, deterministic, Unity-free logic that both peers, the bot and the rig run, so it obeys the same contract as the tick rules even though it runs before tick 0.

## Check yourself

> [!question]- Why split "accept" from "apply"?
> Validation shouldn't have side effects. The caller can check, then decide (send, show, refuse) before anything is spent.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
