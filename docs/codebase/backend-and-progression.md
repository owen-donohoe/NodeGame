---
type: Guide
title: Backend and progression
description: Account identity, protected records, inventory, eras, and the limits of current server authority.
tags: [guide, backend, progression, accounts]
status: draft
sources:
  - id: services
    resource: Assets/Scripts/Backend/BackendServices.cs
    title: BackendServices.cs
  - id: startup
    resource: Assets/Scripts/Backend/GameServices.cs
    title: GameServices.cs
  - id: state
    resource: Assets/Scripts/Backend/Shared/PlayerState.cs
    title: PlayerState.cs
  - id: endpoints
    resource: dotnet/NodeWarCloud/NodeWarCloud/PlayerStateModule.cs
    title: PlayerStateModule.cs
  - id: store
    resource: dotnet/NodeWarCloud/NodeWarCloud/CloudSavePlayerRecordStore.cs
    title: CloudSavePlayerRecordStore.cs
  - id: matchmaking
    resource: dotnet/NodeWar.Progression/Matchmaking.cs
    title: Matchmaking.cs
  - id: mapping
    resource: Assets/Scripts/Lobby/Data/LoadoutTypes.cs
    title: LoadoutTypes.cs
---

# Backend and progression

[Atlas](README.md) · Previous: [Networking](networking.md) · Next: [Logs and replay](logs-and-replay.md)

The backend separates persistent account decisions from an individual running match. Cloud Code exposes operations; Cloud Save holds protected player records; Unity clients ask for changes and display returned state. Gameplay still executes in peer lockstep. These are complementary authority boundaries, not a completed dedicated-server migration.

## Service interfaces keep the UI independent of UGS

`GameServices` coordinates UGS initialization and sign-in. Concurrent readiness callers share an attempt, failed attempts can be retried, and a signed-out session can initialize again. Editor environment selection comes from project settings; development and production builds choose their corresponding environment explicitly.

`BackendServices` supplies account, player-state, and inventory interfaces. UGS implementations call real services. Editor local fakes allow offline work and share an in-memory store for player state and inventory. This supports UI development without making every Workshop interaction a cloud deployment exercise.

Fakes are a behavioral testing seam, not a production fallback on failed authentication. Builds use UGS. A local fake also cannot prove remote permissions, concurrent persistence, browser account linking, or deployment configuration. The server catalog remains the place to test custom and retired content.

`LastKnownState` supports synchronous consumers such as match launch. It is only returned when associated with the currently signed-in account. A cache is not fresh server validation; it is the latest observed answer. This distinction becomes important for future signed loadouts and match admission.

## Account identity outlives scenes

Anonymous sign-in can resume an existing cached session or create a guest. Unity Player Accounts linking protects access to progress; signing in to an existing account and linking the current guest are different operations. `AccountFlow` coordinates pending UI, conflicts, and the warning that changing accounts can abandon guest progress.

The link prompt policy distinguishes meaningful progress from automatically granted starter inventory. Otherwise every new guest would immediately be told they had valuable progress to protect. The local flag remembering that a prompt was shown is a preference, not account authority.

## Protected records have separate jobs

| Record | Contents | Reason for separation |
|---|---|---|
| `rating` | Hidden Glicko-2 rating, uncertainty, volatility, last-match time | Skill estimation differs from visible rewards |
| `rank` | Visible RR, current arena, highest arena | Player progression and unlock history |
| `inventory` | Owned variants/skins and per-base equipment | Durable entitlement and selected appearance/power |
| `history` | Match IDs | References to eventual shared replay/result records |

`PlayerStateModule` uses the authenticated execution context rather than accepting an arbitrary player ID for ordinary state/equip operations. The Cloud Save adapter uses protected records and service credentials for writes. DTO field names and record keys are persistence schema: renaming one is not a harmless C# cleanup.

Shared backend types compile both in Unity and Cloud Code. They are Unity-free but are **not** the deterministic match simulation. Dictionaries and floating-point rating math are appropriate here; applying the simulation's integer-only rule to all backend code would confuse two separate requirements.

## Stable catalog identity meets gameplay eras

A catalog base names a gameplay type, such as `suit.warrior` or `district.rampart`. An era variant names a power configuration, such as `suit.warrior.e3`; a skin names cosmetic presentation. IDs are durable. Catalog validation protects old identities against rename, removal, or reuse; retirement is different from deleting a thing players may own.

`GetPlayerState` fills missing records and grants default era-zero variants and skins. Inventory rules validate an equip patch against ownership, the item's base, and current-arena usability before writing. An invalid patch is refused as a whole. That all-or-nothing validation does not imply a general cross-request transactional settlement system; the current Cloud Save adapter does not supply ranked-match settlement or a compare-and-swap workflow.

Progression distinguishes ownership from usability. Reaching an arena can grant its variants permanently, while a current arena constrains what may be newly equipped. Existing selections retained on demotion and the absence of trusted match admission mean that this foundation should not be described as complete competitive loadout enforcement.

`LoadoutTypes` translates between older lobby IDs, simulation enums, and catalog base IDs. At launch, equipped era tables and skin IDs are attached to `LoadoutData`, then sent through draft exchange and recorded in the log. `PlayerSetup` passes eras into the factory; skins stay out. This one mapping seam prevents each screen and transport from inventing its own spelling of an item.

## Rating rules exist before ranked play

`dotnet/NodeWar.Progression` contains Glicko-2, visible RR adjustments, arena thresholds, era unlock rules, catalog validation, and matchmaking selection rules. Hidden rating estimates skill; visible RR gives progression a legible pace. Arena affects usable power, so a rating-only match can still be unfair.

The matchmaking rules enforce compatible build identity, an arena gap no greater than one, and a rating window that widens with wait time. Candidate ordering favors the same arena, closer rating, longer wait, then stable ticket identity. These are tested calculations over tickets, not a deployed queue or complete online matchmaking experience.

Similarly, storing a rating record and computing an RR delta do not award results after a match. Authenticated reporting, duplicate-safe settlement, opponent identity, and replay storage are still necessary. See [future extensions](future-extensions.md).

## Local profile and server records coexist

`PlayerProfile` still persists local JSON containing preferences, selected loadout, UI memory, and older trophy/unlock/box fields. Some screens still reflect that transitional model. `BackendServices` carries server-returned progression and inventory. Neither deleting all local profile data nor treating it as the source of truth for server-owned items would be correct.

When extending a screen, decide whether it shows a local preference, cached server state, or an authoritative server operation. Preserve that distinction through loading, errors, account switching, and match launch. It is the main migration constraint for finishing progression without letting two sources of truth drift apart.

## Source landmarks

[BackendServices.cs](../../Assets/Scripts/Backend/BackendServices.cs) | [GameServices.cs](../../Assets/Scripts/Backend/GameServices.cs) | [PlayerState.cs](../../Assets/Scripts/Backend/Shared/PlayerState.cs) | [PlayerStateModule.cs](../../dotnet/NodeWarCloud/NodeWarCloud/PlayerStateModule.cs) | [CloudSavePlayerRecordStore.cs](../../dotnet/NodeWarCloud/NodeWarCloud/CloudSavePlayerRecordStore.cs) | [Matchmaking.cs](../../dotnet/NodeWar.Progression/Matchmaking.cs) | [LoadoutTypes.cs](../../Assets/Scripts/Lobby/Data/LoadoutTypes.cs)
