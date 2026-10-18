---
shell: 3
up: "[[Backend client]]"
tags: [shell/3, realm/backend, system/backend-client, script]
file: Assets/Scripts/Backend/BackendServices.cs
sources:
  - Assets/Scripts/Backend/BackendServices.cs
verified: ce9fb6bf
---

# BackendServices

> [!question] The question
> When lobby code needs "the player's state" or "the ranked queue", where does it get it, and how can the same code run offline in the Editor?

**Where you are:** [[Home]] › [[Backend]] › [[Backend client]] › **BackendServices** (script)

**Open:** [BackendServices.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Backend/BackendServices.cs)

## What it is

"The one place a caller gets a backend service." It's a static **service locator**: properties
`PlayerState`, `Account`, `Inventory`, `MatchReports`, `MatchHistory`, `RankedQueue` and `RankedMatch` each return
either the `Ugs*Service` or the `Local*Service` fake. Builds always use UGS, and the Editor can switch
(*Tools › Node War › Backend › Use Local Fakes*).

## Read it in this order

1. **`CloudModule = "NodeWarCloud"`**: the server module's name, used by every UGS call.
2. **`LastKnownState` and `Remember(state, requestedFor)`**: the client-side cache of the last
   server-returned state, **guarded by account**: "discarded… if that player is no longer the one
   signed in — the account can change while the call is in flight." (`RememberGuard`)
3. **`SimulatedOffline`**: dev builds can simulate a dropped connection (F8–F10), so failure paths can be tested by hand.
4. **`ResetOnEnterPlayMode`**: clears every static, the same Editor fix as [[LocalBuildIdentity]].

## Design lessons

- **Interface + two implementations** means offline development and testable presenters.
- **A cache keyed by its owner.** Async answers can arrive late, so always check that they still belong to the current context.
- **Service locator trade-off:** easy access from anywhere, at the cost of hidden dependencies. Constructor
  injection would make dependencies explicit; the presenters do take interfaces, which is the testable part.

## Check yourself

> [!question]- You sign out and into another account while a `GetPlayerState` call is in flight. What stops the old account's rating appearing?
> When the answer arrives, `Remember` compares the player it was requested for with the current
> player. They differ, so the state is dropped and `StateChanged` isn't raised.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
