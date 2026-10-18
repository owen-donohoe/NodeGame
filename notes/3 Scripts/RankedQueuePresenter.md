---
shell: 3
up:
  - "[[Ranked matchmaking]]"
  - "[[Backend client]]"
concepts:
  - "[[State machines]]"
tags: [shell/3, realm/backend, system/backend-client, script]
file: Assets/Scripts/Backend/Shared/RankedQueuePresenter.cs
sources:
  - Assets/Scripts/Backend/Shared/RankedQueuePresenter.cs
verified: ce9fb6bf
---

# RankedQueuePresenter

> [!question] The question
> The "Searching…" screen involves preflight checks, polling, cancellation, requeues, a bot offer and connecting. Where does all that logic live, without being tangled into UI code?

**Where you are:** [[Home]] › [[Backend]] › [[Ranked matchmaking]] › **RankedQueuePresenter** (script)

**Open:** [RankedQueuePresenter.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Backend/Shared/RankedQueuePresenter.cs)

## What it is

A ~700-line, UnityEngine-free class that "drives one whole ranked-queue attempt". It talks to
`IRankedQueueService`, `IRankedMatchService` and an `IRankedQueueView`, and gets time from the caller
through `Tick(nowSeconds)`.

## Read it in this order

1. **`IRankedQueueView` (top of the file):** everything the screen can *show* (searching, found,
   requeue, forfeit prompt, waiting for result, bot offer, blocked countdown) and *ask*
   (`CancelRequested`, `ForfeitConfirmed`, `BotAccepted`). Restyling the queue means writing a new view; the presenter stays.
2. **Constants:** poll every 2 s, bot offer at 90 s, 3 automatic requeues, 3 consecutive poll exceptions tolerated.
3. **`enum Phase`:** `Idle, Preflight, ForfeitPrompt, WaitingForResult, Queue, CancellingForBot,
   Connecting, Rendezvous, Blocked`. A [[State machines|state machine]]: `Tick` does only what the current phase needs.
4. **`StartAsync` → preflight:** retry pending reports, fetch state, deal with an existing active match (forfeit prompt or wait), show discipline blocks.
5. **`RequestCancelAsync`:** "Pending calls are invalidated and clean up their results when they
   return." Async answers that arrive after a cancel must be ignored.

## Design lessons

- **Presenter pattern** (MVP): logic in a testable class, the view as a thin interface.
- **Time passed in** ([[Pure core, Unity shell]]): `NodeWar.Lobby.Tests` can drive a whole queue attempt in milliseconds.
- **Async + cancellation:** every awaited call must check "am I still the active attempt?" when it returns.

## Check yourself

> [!question]- Why does a failed rendezvous requeue automatically instead of showing an error?
> "Most such failures are a transient opponent connection problem, not something the player caused."
> Retrying up to 3 times hides the opponent's bad luck from you.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
