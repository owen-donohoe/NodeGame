# Backend, accounts, match logs, progression: working plan

> **Temporary file. Delete it** once its stages are entered as Notion
> **Phases**/**Tasks** (through `/update`) and the lasting architecture is in
> `docs/`. Remove it in its own commit. Nothing here is a source of truth: the
> code, `docs/` and Notion own everything (CLAUDE.md). If this file disagrees
> with them, this file is wrong.
>
> Written 2026-09-25, second revision; state updated 2026-09-26. Read all of it before starting any stage.
> Where a question is still open, §13 gives the default to build towards.

---

## 1. What is being built

| Area | Summary |
|---|---|
| Backend | Unity Gaming Services (UGS): Cloud Code + Cloud Save + Remote Config. No custom server. |
| Accounts | Unity Player Accounts (UPA) now, Steam linked later, all resolving to one UGS Player ID. |
| Match integrity | P2P lockstep stays. A server **referee** replays the uploaded match log headless and decides the result. |
| Match log | One versioned, forward-compatible file format. It is the referee's input, the replay, and RL data. |
| Ranked | Hidden Glicko-2 MMR for matching; visible RR moves players through **arenas**. |
| Progression | Arenas are **eras**. Reaching an arena unlocks that era's **variants** of suits and districts. Variants **change gameplay** (deliberate power creep tied to arena). **Skins** are cosmetic only. |
| Matchmaking | Hard cap: ±1 arena. Inside that, an MMR window that widens with queue time. |
| Replays | Stored server-side once, listed for both players, watched in a dedicated scene with play/pause/speed/seek/rewind. |
| Shop + boxes | **On hold.** They come last, as one stage, on top of the catalog and inventory built for progression. |

**Everything that matters is server-authoritative**: balances, unlocks, rolls, ratings, results. The client asks; the server may refuse.

---

## 2. Decisions made, and why

| Decision | Why |
|---|---|
| Build on UGS now, not local-first | The costly part of a later port is moving *authority* (sync "client decides" becomes async "server may refuse"). The project already used UGS auth + Relay. |
| No custom server or VPS | Unity hosts it all. The free tiers cover development and a soft launch. |
| No UGS Economy | Economy stopped accepting new projects on 2026-09-08. Currencies/inventory are built on Cloud Code + Cloud Save. |
| Unity Player Accounts for sign-in | One integration gives cross-device accounts on every platform. Steam is added later by **linking**, so no migration is needed. |
| P2P lockstep + end-of-match referee | Authoritative results, replays and dispute evidence without paying for game servers. Server-authoritative simulation stays possible later (§9). |
| Verify after the match, not live | Cloud Code is request/response, not a stream. The P2P hash check (every 50 ticks) still catches desyncs mid-match. |
| Glicko-2 MMR + RR on top | Glicko-2 tracks uncertainty (RD) and volatility (σ); it suits 1v1. Each match is its own rating period. |
| Variants change gameplay; skins do not | Power creep by era is the progression. Consequently variants live in `Simulation/`, the state hash and the match log; skins never touch any of them. |
| Matchmaking capped to ±1 arena | Power differs between arenas (eras), and MMR does not measure it. |
| Live packets: detect mismatch. Stored logs: stay compatible. | Two different simulations can never play each other, so the wire only needs a clear refusal. Replays outlive builds, so the file format needs versioning. |
| Replay viewer is its own scene | Seeking and rewinding need snapshots and a playback tick provider that the live match does not have. |
| Rules in UnityEngine-free C# | The same code runs in Cloud Code and `dotnet test`. |
| Client uses async service interfaces | Each has a UGS implementation and a **local fake**. The fake is permanent tooling for offline Editor work and tests. |
| Environment chosen by build type | Editor: Project Settings value (`development`). Development build: `development`. Release: `production`. Test builds never write to production. |

---

## 3. Current state (2026-09-26)

### Branch

`feat/backend` is PR #72 against `main` at `b4cc316f` (it contains
`feat/rating`); #73 (docs atlas) is stacked on it. Stages 7-8 are built on
`feat/stage7`, branched from `feat/backend`. `feat/present-outline` (#71)
carries a balance change and so needs a balance re-export when it lands.
Stages 0-6 are done on `feat/backend`, audited 2026-09-27: every sub-branch
merge is intact, nothing was orphaned, and the deployed module (12:56Z on
09-26) postdates the eras commit. The lasting architecture is written up in
`docs/architecture.md` (*Backend, match logs and the referee*),
`docs/simulation-rules.md` (era hashing, `MatchFactory`) and
`docs/game-model.md` (*Eras*); this file only tracks the stages.

- `dotnet test dotnet/NodeWar.sln`: **840 passing** in six projects
  (per-project counts in `docs/skills/run-dotnet-tests.md`).
- `compile-check.ps1` clean.

### UGS project

- Project **"Node"**, id `b0178b5c-011c-4e8c-8913-6ffe4286c614`, org `owendonohoe2020`.
- Environments: `production` `57a165d3-0f39-40a3-9cc5-30aa0a010663`; `development` `43bfa2d2-5974-435a-b82d-b0af55911d8c`.
- Unity Player Accounts is an identity provider for the **whole project** (no per-environment setting), client ID `b6e214b9-6b3b-43e8-8182-f273dc818064`, in `Assets/Resources/UnityPlayerAccountSettings.asset`.
- UGS CLI logged in with service account `Account_1` (org level: Manage organization > Service accounts). Roles: Environments Viewer, Cloud Code, Cloud Save, Remote Config. No Player Authentication role, so `ugs player` commands are refused.
- Module `NodeWarCloud` is deployed to `development` with GetPlayerState, Equip and VerifyMatch. Deploy from PowerShell with `C:\Program Files\dotnet` on PATH: `ugs deploy dotnet/NodeWarCloud -e development`. Production has never been deployed to.
- `ugs cloud-save data player get` reads only the default access class; the player records are protected, so check them in the dashboard or through GetPlayerState.
- Remote Config has no settings. Matchmaker is **not enabled** (it may require a payment method on file).
- **Never paste a service-account secret into a chat.** The user runs `ugs login` in their own terminal.

### Still to carry

- The referee checks a log's consistency, not that its eras were owned: that needs both Player IDs in the log (Stage 7/8), or signed loadouts.
- Every shipped balance must be exported (`Export Balance For Server`) and deployed, or the referee refuses matches played on it.
- The Workshop era row needs its style pass (the chips wrap at phone width; the equipped chip is disabled, so it renders as faded as the locked ones).

---

## 4. Architecture

```
dotnet/NodeWar.Progression/        netstandard2.1  rules: Glicko-2, RR, arenas,
                                                   matchmaking window, catalog
                                                   validation, era unlocks
dotnet/NodeWar.Progression.Tests/  net8.0
<match log library>                netstandard2.1  read/write the match log (§8).
                                                   UnityEngine-free; used by the
                                                   client, Cloud Code and tests
<cloud code module project>/       net8.0          references Progression, the
                                                   match log library, and (after
                                                   the Stage 5 spike) Simulation
Assets/Scripts/Backend/            NodeWar.Backend: GameServices, accounts,
                                                   service interfaces, UGS impls,
                                                   local fakes
Assets/UI/...                      UI Toolkit: account, rank, history, workshop
                                                   pages; replay scene UI
```

- Where the match log library lives: follow the existing pattern in `dotnet/` that compiles Unity-free sources from `Assets/` (look at how the sim and lobby test projects include their sources) rather than inventing a second one.
- **Cloud Code** runs .NET (up to .NET 9) and cannot use UnityEngine.
- **Service interfaces**, all async and all able to fail: `IAccountService`, `IPlayerStateService`, `IInventoryService`, `IMatchReportService`, `IReplayService`, `IMatchmakingService`. Every call awaits `GameServices.EnsureReadyAsync()` first. The UI shows what the server returns, never an optimistic guess.
- **Cloud Save**: player data in the **protected** access class (player reads, only the server writes):
  - `rating` (r, rd, sigma, lastMatchUtc)
  - `rank` (rr, arena, highestArena)
  - `inventory` (variants owned, skins owned, equipped loadout)
  - `history` (last N match IDs)
  - later, for Stage 10: `wallet`, `boxes`, `daily`
- **Catalog**: authored in the Editor (ScriptableObjects). An export step writes it as data that Cloud Code and Remote Config read. **Item IDs are stable strings, never renamed or reused.** If the server doesn't know an ID, it refuses it.
- **Server time only** for anything timed. **Randomness only on the server.**
- **Simulation boundary**: the backend only *reads* `Simulation/` (the referee). Variants (Stage 6) and snapshots (Stage 9) are the two stages that *change* it. Both start in plan mode.

---

## 5. Stages

Order agreed with the user. "Session" means one focused working window.
Matchmaking may move earlier once rating exists (after Stage 7); keep it after reporting.

### Stage 0: environment. **DONE** (§3)

### Stage 1: backend skeleton. **DONE**

1. Cloud Code module project (net8.0) referencing `NodeWar.Progression`. Find out how it is packaged and deployed (`ugs deploy` and its module-reference / `.ccmr` form).
2. Cloud Code `GetPlayerState`: creates first-time defaults in Cloud Save and returns the whole state. This is the hello-world. **Do not build `ClaimDaily`**: daily rewards belong to the shop, which is on hold.
3. Client: `IPlayerStateService` + UGS impl + local fake in `Assets/Scripts/Backend/`.
4. End to end: call from the Editor against `development`, see the data in the dashboard and through `ugs cloud-save data player list`.
- Done when: the round trip works and the fake passes the same client-side tests.

### Stage 2: accounts (Unity Player Accounts). **DONE**, verified live 2026-09-25

- **Dashboard:** enable Unity Player Accounts as an Authentication identity provider for both environments. The user does this if the CLI cannot.
- **Flow:**
  - First launch stays anonymous (current behaviour).
  - "Link account": `PlayerAccountService.StartSignInAsync()` → on sign-in, `AuthenticationService.LinkWithUnityAsync(accessToken)`.
  - New device: `SignInWithUnityAsync(accessToken)` gives the same Player ID, and so the same Cloud Save data.
  - Verify these API names against the installed `com.unity.services.authentication` version before writing code.
- **Conflict** (the UPA account is already linked to another Player ID): show a dialog with "Switch to that account (this device's anonymous progress is abandoned)" or "Cancel". Never merge.
- **Sign out:** clear the cached credentials. Otherwise the next launch silently signs in as the old anonymous player.
- **Steam, later:** Steamworks.NET → web API auth ticket → `LinkWithSteamAsync` / `SignInWithSteamAsync`. It links to the same Player ID alongside UPA. It needs a Steam App ID configured in the dashboard. If a player links both, either one signs in.
- **Profile identity:** `PlayerProfile.uuid` becomes the UGS Player ID, and `username` becomes the UGS player name.
- **UI, basic first:**
  - `SettingsPage` Account section: status (Guest / signed in as X), Link, Sign out.
  - Conflict dialog.
  - A one-time "link your account" prompt, shown once there is progress worth losing (first ranked result or first unlock).

  The style pass afterwards uses `Tokens.uss` / `Components.uss`. UXML/USS are text and may be edited. Assigning a new layout in the lobby scene is scene wiring: do it through a connected Editor, never by editing `.unity` text.

### Stage 3: versioned handshake. **DONE**

- **Constants:**
  - `ProtocolVersion` (wire layout).
  - `SimVersion`: bumped whenever the same inputs would produce a different game.
  - `ContentHash`: over `GameBalanceData` + variant tables.
- **Rule:** when a pinned determinism baseline changes on purpose, bump `SimVersion` in the same commit.
- **Handshake:** carries all three. On mismatch, send `HandshakeReject` with a reason. The UI says "Opponent is on a different version."
- **Lobby:** put the same values in lobby data, so incompatible players never reach Relay.
- **Pairing:** `GameCommand` / `InputSerializer` changes stay paired in one commit (CLAUDE.md), and any wire change bumps `ProtocolVersion`.

### Stage 4: match log format. **DONE**

Format defined in §8. The deliverables:
- A writer that records during a live match.
- A reader.
- Tests: round-trip; an older reader skipping a newer chunk; a truncated file refused.

### Stage 5: referee spike, then headless runner. **DONE**

1. **Spike first.** Can a Cloud Code module reference the Simulation sources, and does a full match replay finish within Cloud Code's execution-time and memory limits?
   - If yes: continue.
   - If no: fall back to verifying hash checkpoints only, with a full replay run offline on disputes. Record which one and why.
2. **Headless match runner:** builds a `SimulationState` from a match log header and runs `SimulateTick` to completion. It is the same component as the headless `MatchFactory` in `docs/direction.md` (RL). Build it once.

**Spike result, 2026-09-26: full replay, continue.** Module `NodeWarCloud` references `NodeWar.Simulation` and `NodeWar.MatchLog` and deploys fine. `VerifyMatch` on a 12,000-tick (~20 min) log of 81 KB replayed in 100 ms server-side on the first call and 57 ms warm (192–283 ms round trip), against Cloud Code's 15 s execution and 256 MB limits (docs, same date). The referee serializes replays with one lock because the simulation's balance and path costs are statics; at these timings that costs nothing. A log played on a balance the server does not hold is refused ("unknown balance"): every shipped balance must be exported (`Tools > Node War > Backend > Export Balance For Server`) and deployed.

### Stage 6: progression — catalog, eras, skins, inventory. **DONE** (the referee does not yet check loadout ownership; signed loadouts not built)

- **Eras:** arena N = era N (e.g. early → metal → … → magic in arena 4, mastered in 5). Each suit and district has one variant per era, and each variant is a balance entry.
- **Unlock:** reaching arena N grants era-N variants (server-side, in `ReportMatch`). What happens on demotion is §13.
- **Simulation:**
  - Drafted suits/nodes carry their era.
  - Balance lookups become per (type, era).
  - New state fields are registered in `SimulationStateHasher`.
  - Follow `docs/adding-a-feature.md` and `.claude/skills/determinism-guard.md`.
- **Wire:** `DraftLoadout` carries variants (gameplay) and skins (cosmetic). As built: `ProtocolVersion` 2; `SimVersion` stays 1, because era fields hash only when non-zero, so era-0 matches and older logs keep their hashes.
- **Skins:** never in `SimulationState`, never hashed, never read by the simulation. They are in the match log only so replays look right, and they travel to the opponent only for display.
- **Server validation:** the referee refuses a result whose loadout used a variant the player doesn't own, or isn't allowed at the match's tier. Better still: the server issues a signed loadout at match start (it ties into §8's session keys), so a modified client can't field unowned variants at all.
- **Catalog:** Editor-authored, exported for the server (§4).
- **Inventory/equip:** Cloud Code `Equip(loadout)` validates ownership and writes `inventory.equipped`.
- **UI:** the Workshop shows variants and locked eras. A skins panel handles preview and equip.

### Stages 7 and 8, as re-planned 2026-09-27

Decided with the user on 2026-09-27:
- **Private lobby matches are never rated.** Only a match the server created (through Matchmaker) is ranked.
- **Matchmaker is the queue** (not a Lobby-based queue).
- **Signatures come last** (7.6). Nothing ships between Stages 7 and 8, so until 7.6 a match settles only when **both** logs arrive and agree.

**Why 7 and 8 are interleaved.** Once private lobbies are unrated, only Matchmaker can create a ranked match, so a lobby-based `BeginMatch` would be thrown away. The server-side **match record** is designed once, around Matchmaker, and settlement is built against it with fakes. It is verified live once Stage 8 creates real records. That costs a live end-to-end test until 8.2; it buys no throwaway endpoint and no development-only ranked path that could leak into production.

| Step | What | Depends on | Status (2026-09-27) |
|---|---|---|---|
| 7.1 | `MatchSettlement.Settle`: pure rules plus tests (`NodeWar.Progression`) | nothing | **Done** |
| R | Research: Matchmaker for P2P/Relay, Cloud Save and Cloud Code limits | nothing | **Done** |
| 7.2 | Match record and `ReportMatch` in Cloud Code, against fakes | 7.1, R | **Done**, plus review fixes; not deployed |
| 7.3 | Rank page (reuse `TrophyBarLogic`); Workshop era-chip style pass | nothing (reads `PlayerState`) | **Done**; visuals unverified |
| 8.1 | Matchmaker queue config; ticket creation that yields a match record | R, 7.2's record | **Done** (2026-09-28): allocator live in development; a queued pair produced record `match-<id>` with both Matchmaker-supplied player IDs |
| 8.2 | Client: queue UI, match found → Relay → draft, log header from the record | 8.1 | 8.2a and 8.2b **done**; a full ranked match played end to end on 2026-09-29. Next 8.2c (disconnects) and 8.2d (strikes): decisions D7-D14 |
| 7.4 | Client: upload at match end (`IMatchReportService` + UGS impl + fake) | 7.2, 8.2 | 7.4a service and the upload call **done** (with 8.2b); the result-screen state ("waiting for the result", RR change) is still to do |
| 7.5 | Replay storage, retention, `MatchHistoryPage` | R, 7.2 | 7.5a server history and 7.5c client page **done** (visuals unverified); 7.5b retention **deferred** (unlimited custom items, small logs; revisit with real volume) |
| 7.6 | Session keys and per-command signatures; single-log settlement | all of the above | Not started |

#### R: research result (2026-09-27, docs-sourced; "inferred" items need a live check)

- **No network migration.** The project already runs `com.unity.services.multiplayer` 2.3.1 (no standalone lobby/relay/matchmaker packages). Matchmaker supports client-hosted P2P over Relay without Multiplay. Its results can feed the **existing** Lobby/Relay orchestration: the host creates the lobby, and the opponent joins by match ID. Sessions (`MatchmakeSessionAsync`) is optional and not adopted.
- **Ratings stay server-side:** queue rules can read `ExternalData.CloudSave` from the **Protected** access class.
- ~~Roster answer: Cloud Code creates every ticket (`QueueRanked`/`PollRanked`).~~ **Superseded 2026-09-28** by the pool's Cloud Code hosting (8.0): Matchmaker hands the roster to our `Allocate` itself. See "8.1 / 8.2 Matchmaking" for the decisions.
- **Storage:** player data 5 MiB per access class per player; a **Private custom item** holds up to 5 MiB per access class, readable by servers only, and custom items are unlimited. Match record **and** replay go in one Private custom item per match (`match-<id>`). Files (1 GiB per player) are owner-only, so not used.
- **Concurrency:** `writeLock` → 409 on version mismatch; omitting it on update bypasses the check. Cross-player Protected writes with `ServiceToken`: confirmed.
- **Cloud Code limits:** request 1 MB (a 512 KiB log is ~699 KB in base64: fits), response 2 MiB, 15 s, 256 MB per worker, 600 requests/min/player.
- **Matchmaker pricing:** not listed on Unity's pricing page; enabling it in the dashboard asked for no payment method (checked 2026-09-28).

#### 8.0 Matchmaker set up in `development` (2026-09-28, with the user, in the dashboard)

- Queue `ranked` (1 player per ticket) and default pool `ranked-pool` (timeout 300 s, **Client Hosting**), with the six match rules of `dotnet/NodeWarCloud/Matchmaker/ranked.mmq`, pasted as JSON and confirmed parsed in the logic builder. Production has no queue.
- **The repo file is the source of truth**, deployed with **UGS CLI 2.0.0** (`%LOCALAPPDATA%\ugs2\ugs.exe`, the GitHub release binary; npm still serves 1.9.0): `ugs deploy dotnet/NodeWarCloud/Matchmaker -s matchmaker -e development`. CLI 1.9.0 calls Multiplay's `ListFleets` on every Matchmaker operation and gets 403; 2.0.0 (2026-09-24) removed Multiplay because the service is being decommissioned. No role fixes 1.9.0.
- **Every rule states `"enableRule": true`.** The dashboard stored `false` for rules pasted without it (the docs say it defaults to true), which silently disabled every rule until the 2.0.0 redeploy fixed it on 2026-09-28.
- **A better roster source exists: "Hosting via Cloud Code".** A pool can name a Cloud Code module plus *allocate* and *poll* endpoints that Matchmaker calls itself when a match forms. That is the server-side match-formed hook R could not find: the roster arrives server-to-server, so clients could create their own tickets (rating and arena still come from Protected Cloud Save through the rules) and `Allocate` creates the match record. **Evaluate it before building `QueueRanked`/`PollRanked`.** Contract, from Unity's `matchmaker-hosting-providers` examples and the SDK: implement `IMatchmakerAllocator` (needs `Com.Unity.Services.CloudCode.Apis` **0.0.24**; we have 0.0.22) with `Allocate(IExecutionContext, AllocateRequest)` → `AllocateResponse(AllocateStatus.Created|Error)` plus `AllocationData`, and `Poll(IExecutionContext, PollRequest)` → `PollResponse(PollStatus.Pending|Allocated|Error)` plus `AssignmentData`. `AllocateRequest` carries `MatchId` and `MatchmakingResults` (`QueueName`, `PoolName`, `MatchProperties.Players[].Id/CustomData`, `Teams[].PlayerIds`): the roster, server to server. Open: which `AssignmentData` form suits P2P (the examples only use `IpPort`); the live spike answers it. The pool references it with `moduleName`, `allocateFunctionName`, `pollFunctionName`.

#### 7.1 Settlement rules (pure, `NodeWar.Progression`)

`MatchSettlement.Settle(SettlementInput, SettlementConfig) → SettlementOutcome`. No I/O and no clock: `NowUnixSeconds` is an input.

1. **Decay first.** For each player with `LastMatchUnixSeconds > 0`: `idle = max(0, (now − last) / InactivityPeriodSeconds)` (integer division; default one week), then `Glicko2.DecayForInactivity`. `last == 0` is a new player: no decay.
2. **Simultaneous update.** Each player's `UpdateSingleMatch` uses the opponent's **decayed, pre-match** rating, never the opponent's updated one. Score 1 for the winner, 0 for the loser.
3. **RR.** `RankPoints.RRDelta(currentRR, hiddenAfterMatch, won)`, then `Arenas.ApplyRRDelta` (floors at 0, tracks `HighestArena`).
4. **Outcome per player:** new `Rating`, `LastMatchUnixSeconds = now`, the signed `RRDelta`, the new `RankState`, `Promoted` / `Demoted`.
5. **A winner outside {0, 1} throws `ArgumentException`.** The caller decides what is rated; `Settle` never sees a void match.

Era grants and the equipped clamp are **not** in 7.1: they need the catalog, and live in 7.2.

#### 7.2 Match record and `ReportMatch` (Cloud Code)

- **Match record**, server-owned, keyed by match ID (storage per R). Fields: `matchId`; both `playerIds`; `createdUnixSeconds` (server time); expected `protocol` / `sim` / `content`; a **pre-match snapshot** per player of rating, rank and equipped variants (never re-read at settlement); state `Open → Pending → Settled | Void | Disputed`; the reports received (uploader, verdict winner, `endTick`, `finalHash`).
- **`ReportMatch(matchId, logBase64)`**:
  1. The caller (`context.PlayerId`) must be one of the record's players. The log header's match ID and player IDs must equal the record's.
  2. Run the existing `Referee`. Then check each player's logged eras against their snapshot: every era a player fielded must be one they had equipped. This closes Stage 6's deferred ownership check.
  3. **Agreement means equal verdict `winner`, `endTick` and `finalHash`.** Not byte equality: `localPlayer` differs between the two logs by design.
  4. First valid report → `Pending`. A second that agrees → settle. One that disagrees → `Disputed`: keep both, apply no rating. A refused report is recorded; before 7.6 the other log alone cannot settle.
  5. **Settle:** `MatchSettlement.Settle` from the snapshots → era grants (`EraUnlocks.GrantsFor` on the new `HighestArena`) → **clamp equipped variants** above the new current arena down to the highest owned era at or below it (demotion) → prepend the match ID to both `history` records (keep 20) → `Settled`.
- **Concurrency.** Two uploads can arrive together. Record state changes use Cloud Save write locks: on a conflict, re-read and re-decide. A settlement that dies halfway must be safely re-runnable, so each player's `history` is the guard: a player whose history already holds the match ID is not settled again.
- **Timeout.** A match `Pending` longer than 10 minutes becomes `Void` lazily, when either player next calls `ReportMatch` or `GetPlayerState`. No scheduler.
- **Decided after Sol's review (2026-09-27):**
  - *Era rule is "owned and ≤ snapshot arena", not "equals what was equipped".* Fielding an owned, eligible era gives no edge that equipping it would not, and the opponent sees the loadout in the draft either way. Do not tighten it to the equipped era.
  - *Timeout is evaluated only when a report arrives.* A `Pending` match changes nothing in a player's state, and a late second report is voided on arrival. `GetPlayerState` needs no hook until the queue must refuse a player with an open match (8.x).
  - *A refused report does not take the player's slot*: the first **accepted** report is authoritative; refusals are kept for audit, capped.
  - *Equip writes inventory with a write lock*, and re-reads and revalidates on conflict, so it cannot undo a settlement's clamp or grants.
  - *The idempotency guard is a separate bounded list of settled match IDs* (last 200, on the player's rating record, written in the same batch as the settlement), not the 20-entry history.
- **The equipped clamp goes in `Backend/Shared`**, called from both `InventoryRules` (server) and `LocalInventoryService` (fake). The two equip validators are already duplicated; do not add a third copy.

#### 7.3 Rank display and Workshop style pass (client, no new page)

- The trophy strip (`LobbyChrome`) and the Profile arena road (`ProfilePage`) show local `PlayerProfile.Trophies` behind `TODO(arenas)`. Drive both from the server's `RankRecord` (`BackendServices.LastKnownState`) instead: RR, arena, progress to the next threshold. The top arena is open-ended.
- **One threshold table.** Add `RankTable` to `Backend/Shared` (thresholds 0/300/700/1200/1800/2500, and a display name per arena, "Arena 1"…"Arena 6" until the user names them). A Cloud test asserts it equals `new ArenaConfig().Thresholds`, and 7.2 builds its configs from it.
- The pure display maths (arena, RR within the arena, span, fill 0-1) is a UnityEngine-free class, tested in `NodeWar.Lobby.Tests`. `TrophyBarLogic`'s sliding window does not fit arenas and stays for anything else still using it.
- `BackendServices` raises a `StateChanged` event from `Remember`, so the strip refreshes when an Equip or a later report returns new state. With no known state (offline, or before the first fetch), show "Arena –" as today.
- Workshop: the era chips wrap at phone width; the equipped chip is disabled and so looks as faded as a locked one. Give equipped its own class (selected look, not disabled look) in `Workshop.uss`, and make the row fit or scroll horizontally at 360 px.

#### 8.1 / 8.2 Matchmaking

Matchmaker is enabled and the queue is deployed (8.0). Rules as §7: same `ProtocolVersion` / `SimVersion` / `ContentHash`; arena difference ≤ 1; MMR window 100 / 250 / 500, unbounded from **120 s**; an unranked bot match offered at 90 s (client side).

**Decisions (2026-09-28), with the options weighed:**

| # | Decision | Options | Chosen, and why |
|---|---|---|---|
| D1 | Where the server learns the roster | (a) Cloud Code creates and polls tickets (b) **pool hosting via Cloud Code: Matchmaker calls our `Allocate`** (c) clients report a match ID for the server to check | **(b)**: the roster arrives server to server; no custom queue endpoints; documented. Costs: SDK bump 0.0.22 → 0.0.24; the P2P assignment form is unproven. (a) is more code on an inferred API; (c) trusts clients. |
| D2 | Who creates tickets | (a) Cloud Code proxy (b) **the client, via `com.unity.services.multiplayer`'s matchmaker API (not Sessions)** | **(b)**: rules read rating and arena from Protected Cloud Save; the client supplies only protocol/sim/content, and lying about those yields a record whose logs the server refuses. |
| D3 | When the match record is created | (a) lazily, at the first report (b) **in `Allocate`, idempotently** | **(b)**: the pre-match snapshot must predate play. A retried `Allocate` finds the record and answers Created again. |
| D4 | Hard caps in `Allocate` | (a) trust the queue rules (b) **re-check invariants: balance known to the server, equal versions, arena gap ≤ 1** | **(b)**: cheap, and it survives a queue misconfiguration (the dashboard silently disabled every rule once). The soft rating window stays queue-only. |
| D5 | `NodeWar.Progression/Matchmaking.cs` | (a) delete now (b) keep (c) **keep until the live queue is proven, then delete in its own commit** | **(c)**, done 2026-09-28: deleted once the live queue was proven (its same-arena rule had already drifted from the deployed one). |
| D6 | Rendezvous (8.2) | (a) public lobby filtered by match ID (b) **host publishes its lobby join code into the match record; the guest reads it through Cloud Code** | **(b)**: only the record's two players can read it. Two small endpoints. |

**Packages:**

- **8.1a Allocator (server).** Bump `Com.Unity.Services.CloudCode.Apis` to 0.0.24 (all Cloud tests must still pass). `MatchAllocation` (pure, testable) + `MatchmakerAllocatorModule : IMatchmakerAllocator`:
  - `Allocate`: player IDs from `MatchmakingResults.MatchProperties.Players[].Id` (exactly two, distinct), versions from each player's ticket `CustomData` (`protocol`, `sim`, `content`), both players' `PlayerState` read with the service token (get-or-create), D4 checks, then `MatchRecords.Create` + store write with no lock (create). An existing record for the match ID → `Created` (idempotent); a failed check → `Error` with a reason. `AllocationData` = `{ matchId }`.
  - `Poll`: record exists → `Allocated`; missing → `Error`. `AssignmentData`: whatever non-`IpPort` form the SDK offers; if none, `IpPort` with a placeholder is **not** acceptable without the spike's evidence. Record the finding here.
  - SDK 0.0.24 finding (8.1a): `AssignmentData.Custom(Dictionary<string, object>)` is the non-IpPort factory (`AssignmentType.Custom`); Poll uses it with `{ matchId }`. `MatchmakingResults.MatchProperties` is a dictionary: `Players` is decoded to `Unity.Services.Matchmaker.Model.Player` (`Id`, `CustomData`). The live spike must still prove the ticket assignment. Cloud Save null-lock creation is not atomic create-if-absent; an existence re-check cannot eliminate simultaneous first writes across workers.
  - Tests with fakes: two players → record with snapshots; retry idempotent; unknown balance / version mismatch / arena gap 2 / one player / duplicate player → `Error`, no record.
- **Live queue verified (2026-09-28, `scripts/matchmaker-spike.ps1`, Client Hosting):** two anonymous players with records pair in seconds under all six rules; each ticket status reads `{"assignmentType":"MatchIdAssignment","status":"Found","matchId":"<guid>"}`. **Requirement found:** a player with *no* Cloud Save records fails every Cloud Save rule ("not compatible with the match definition"); the rule defaults are not applied to a brand-new player. So a client must have called `GetPlayerState` (which creates the records) before enqueueing. 8.2a enforces it.
- **8.1b result (2026-09-28): done.** Pool hosting `{ "type": "CloudCode", "moduleName": "NodeWarCloud", "allocateFunctionName": "Matchmaker_Allocate", "pollFunctionName": "Matchmaker_Poll" }` (the dashboard calls it "Third Party Hosting"). Two players with records → each ticket `{"assignmentType":"CustomAssignment","customData":{"matchId":"<id>"},"status":"Found","matchId":"<id>"}`, and Cloud Save private custom item `match-<id>` holds the record with both player IDs from the Matchmaker, the content hash and both snapshots.
- **8.1b Pool switch + live spike (lead), as planned.** Deploy the module; set the pool's hosting to Cloud Code (`moduleName`, `allocateFunctionName`, `pollFunctionName`) in `ranked.mmq`; deploy with CLI 2.0.0. Spike: two anonymous players (Authentication REST) create tickets (Matchmaker REST) → confirm `Allocate` ran, a `match-<id>` record exists, and what each ticket's assignment returns.
- **8.2a Client queue service.** `IRankedQueueService` (`EnqueueAsync`, `PollAsync`, `CancelAsync`) + fake + UGS impl over the matchmaker client API in `com.unity.services.multiplayer` 2.3.1 (check its exact type names first). Ticket `CustomData` = `LocalBuildIdentity` values. No UI, no lobby.
- **8.2b Rendezvous + queue UI + draft handoff**, after 8.1b's evidence. Network-adjacent: main session, plan mode.

**Found in the 8.2b review (2026-09-28):**
- **The host is fixed by the record, not chosen.** `MatchEligibility.Check` requires `header.playerIds[p] == record.playerIds[p]`, and settlement awards `winner` by that index. So `record.playerIds[0]` hosts as simulation player 0 and `[1]` joins as player 1. Each client must learn the record's order and its opponent's ID from the server.
- **Every ranked log is refused today.** `GameManager.BeginRecording` writes a fresh GUID match ID and a blank opponent ID. 8.2b carries `matchId` and both player IDs through `MatchConnection` into the header (7.4 depends on it).
- **D6's "lobby join code" is the Relay join code.** `MatchLauncher` joins Relay directly; no UGS Lobby is involved.
- **No wire change in 8.2b.** The handshake is unchanged, so no `ProtocolVersion` bump. Accepted: the handshake does not prove the peer's identity; the code is readable only by the record's two players.
- **A failed rendezvous strands both players for 2 h.** Claims are released only inside `ReportMatch` on a terminal record, and an Open record with no reports never gets there. D7 closes this.

**Decisions (2026-09-28), with the user:**

| # | Decision |
|---|---|
| D7 | **`AbandonMatch(matchId)`**: before the guest confirms the connection, voids the record and releases both claims. After the connection, abandoning is a **forfeit** (a different result, settled by D11), and the "already in a match" screen offers "You left a match in progress. Forfeit to queue again?" |
| D8 | **Rendezvous deadlines, halved from the first proposal:** guest waits 15 s for the code, host waits 22 s for the guest, handshake 15 s. On any, abandon (D7) and **re-queue automatically**, looping with "Opponent's connection failed — finding a new match…". Retention over explicit choice; a strong "connecting you" visual is later UI work. |
| D9 | **Re-queue does not trust ticket message text.** On any failed ticket the client calls `GetPlayerState`: no live `activeMatch` → it was the innocent side, re-queue (capped, ~3 tries); a live claim → the forfeit screen (D7). The same check runs before creating a ticket. |
| D10 | **Unranked bot offered at 90 s** (§7). The ticket stays live until the player accepts; accepting deletes the ticket first, then starts the bot match. Unrated by construction: no match record, so the report upload must never run for it. The Found-at-the-same-second race leaves the opponent to time out and re-queue (D8). |
| D11 | **In-match disconnect, two stages (8.2c).** Replaces the 2 s `DISCONNECT_TIMEOUT` end. The simulation stays stalled: a panel "Opponent disconnected" asks for 5 s; then the text becomes a 5 s countdown ring; the ring ending ends the match. A reconnect covers a transient network drop (both peers keep state; unacked inputs already resend). It cannot cover a crash: that would need a mid-match snapshot, which touches `Simulation/`. |
| D12 | **Who disconnected is decided by server presence.** Both peers see the same silence, so during the grace window each client calls `Presence(matchId)` about once a second. Only one seen → that player wins by forfeit, the other takes the loss and a strike; the unseen client shows "Reconnecting…" rather than "Opponent disconnected". Both seen (the peer link broke) → Void, no strike. Neither → Void. Accepted: blocking only peer traffic turns a loss into a Void; detect the pattern from logs later. Works before 7.6. |
| D13 | **Strike ladder (8.2d), server-owned** (Protected Cloud Save; the client only displays it). In-match disconnects (D12) only; rendezvous failures cannot be attributed and never count. Disconnects 1-2: none; 3: 2 min; 4: 2 min; 5: 1 h; 6: 1 day; 7+: 2 days (cap). Every **16 h since the player's last queue** without a new disconnect drops one step (2 days → 1 day → 1 h → 2 min → none). Client checks before enqueue and shows the countdown; `Allocate` refuses a blocked player as a backstop (which fails the opponent's ticket too; D9 re-queues them). |
| D14 | **Scope.** 8.2b: rendezvous, draft handoff, D7-D10, and ranked becomes the default play button; Host/Join stay in the play popup (not default) until the Social chapter. 8.2c: D11-D12. 8.2d: D13. The Social tab (groups, friends, custom battle, chat, replays) is its own lobby chapter, later. |

**8.2b shape.** One Cloud Code function `Rendezvous(matchId, joinCode?)`: the caller must be in the record; it returns both player IDs, the caller's slot and the code once published; slot 0 publishes its code under the record's write lock. The guest polls every 2 s. A UnityEngine-free rendezvous state machine (tested like `RankedQueuePresenter`) drives the existing `MatchLauncher`, which gains ranked deadlines. `IRankedQueueView` gains connecting, re-queueing and already-in-a-match states.

**8.2b done: built 2026-09-28, played end to end 2026-09-29 (Editor + development build over Relay: queue, rendezvous, draft, match, settlement, history).** Server: `Rendezvous`, `ConfirmConnected`, `LeaveMatch` (`MatchRendezvous`, `RankedMatchModule`); `MatchSettler` extracted from `MatchReporting` and shared by forfeits. A forfeit commits `forfeitedBy` to the record under its lock *before* any player write, and `ReportMatch` finishes a committed forfeit, so two opposing forfeits cannot settle different winners in the record and the players. Client: `RankedRendezvous`; `RankedQueuePresenter` now runs preflight (leave or forfeit a held match), queue, rendezvous, automatic re-queue (3) and the bot offer at 90 s; `MatchLauncher.HostRanked/JoinRanked` with the D8 deadlines; the log header takes the record's ID and order; ranked logs upload at match end (the 7.4 call; its result UI is still to do). Verified live with `scripts/matchmaker-spike.ps1 -EnsureRecords -Rendezvous`: slots, publish and read, and leaving before connecting voids the record and frees both claims. The two-player run found three bugs, all fixed: every draft loadout packet was dropped since the eras-and-skins wire change (0652b06), so no networked draft could start; lockstep deadlocked for good on one lost input packet because only the newest input was resent; the search timer counted from before a forfeit prompt. Notes from the run: a release build talks to `production`, which has no module, so test with a Development Build; one forfeit between two new players opens a ~320 rating gap, which the queue bridges only after 60 s.

**Live two-player test:** Editor plus one standalone build (separate PlayerPrefs keys, so different players), then a second PC across a real network. Two builds on one machine share `HKCU\Software\<Company>\<Product>` and would sign in as the same player.

#### 7.5 Replay storage

Decided after R. Default: server-owned game data keyed by match ID, one copy per match, deleted once it has left **both** players' last 20. The history page lists stored matches, each "replay coming" until Stage 9. Measure compressed log size before choosing (most ticks are empty).

#### 7.6 Signatures

As §8: a server-issued session key per player in the match record; each player signs their own command batches; the peer's log keeps them (`SIGNATURES` chunk). A wire change: bump `ProtocolVersion`, and add a new `TICKS` tag if the command layout changes. After 7.6, one valid signed log may settle a `Pending` match on timeout.

#### Objections weighed, and why the plan stands

| Objection | Answer |
|---|---|
| `ReportMatch` is built before anything can create a ranked match, so it goes unverified live | Until 8.2. Fakes and `dotnet test` cover it meanwhile; the alternative, a development-only rated path, risks leaking into production for a tester's convenience. |
| Waiting for both logs lets a loser block a loss by never uploading | Until 7.6. Accepted because nothing ships between 7 and 8. The lazy timeout voids the match rather than leaving it open. |
| A snapshot at creation instead of a read at settlement looks redundant | Settlement changes rank, and the opponent's settlement can land first. Eligibility and rating must use the state the match was played at. |
| Cross-player writes are not transactional in Cloud Save | Each player's history is the idempotency guard, so a partial settlement re-runs safely. |
| Matchmaker may force a Sessions migration of the whole network layer | Resolved: no. The multiplayer package is already installed, and the assignment feeds the existing Lobby/Relay path (D1, D6). |
| Delegating Cloud Code work contradicts §12 | §12 predates §15. `ReportMatch` is now fully specified and testable with fakes. The network handoff (8.2) and signatures (7.6) stay in the main session. |
| Doubles in settlement | `NodeWar.Progression` is server-only and never enters `Simulation/`; §6 already allows it. |

### Stage 9: replay viewer scene (plan mode: snapshots touch `Simulation/`)

- **Separate scene.** It reuses the View layer, driven by a playback `ITickProvider` that feeds logged commands instead of network input.
- **Controls:** play/pause, 0.5×/1×/2×/4×, step, seek bar, rewind.
- **Seek and rewind:**
  - Restore the nearest keyframe snapshot at or before the target tick, then simulate forward.
  - Keyframes are taken every K ticks (default 50, aligned with hash checks) and built on load or on first playback, not stored in the log.
- **Snapshot = full `SimulationState` copy.** Every `SimulationState` field must be in both the hasher and the snapshot. Add a test: snapshot → restore → hash equal, and simulate-forward from a restore equals the uninterrupted run.
- **Verification:** the log's hash checkpoints confirm playback matches the original match. On a mismatch, show "replay out of sync" and stop.
- **View:** when seeking, snap and skip interpolation/tweens.
- **Old replays:** a replay whose `SimVersion` differs from the running build is handled per §8 (old sim DLL or snapshot conversion). Until that exists, the history list marks it "unavailable in this version."

### Stage 10: shop + boxes (**ON HOLD**, one stage)

- Wallet (soft/hard), catalog prices in Remote Config, `Purchase(itemId)` (validate, deduct, grant in one call), `OpenBox()` (server roll against a drop table), daily rewards and deals.
- Box progress is an integer on the server.
- Real-money buttons are placeholders; Unity IAP with receipts validated in Cloud Code comes later.
- **Constraint to settle first:** variants change gameplay. If boxes or purchases can grant variants, the game becomes pay-to-win. Default: variants come only from arena progression; boxes and the shop sell skins only.

### Side spike: server-authoritative simulation (after Stage 5, optional)

A local headless `dotnet` host that receives both players' inputs, orders them, runs the sim and broadcasts. No hosting cost. The purpose is to measure feel and effort against §9 before paying for anything.

---

## 6. Rating details

### Glicko-2 (per player, stored)

| Variable | Meaning |
|---|---|
| **r** | Skill estimate, 1500-based |
| **RD** | Certainty. Starts 350, shrinks with play (floor 30), grows with inactivity (cap 350) |
| **σ** | Volatility. Rises on streaks, keeping RD and step size larger |

- **Expected score E** is computed per match, not stored: win probability against this opponent, discounted by *their* RD.
- **Update** ≈ RD² × (actual − E).
- Use `double` in `NodeWar.Progression`. That is fine: it runs on the server, never in `Simulation/`.

### RR (`RankPoints.RRDelta`)

- Implied rating of a rank = `ImpliedRatingAtZero + RatingPerRR × RR`.
- Win = `BaseGain + Divergence × (hiddenR − implied)`. A loss is the mirror image.
- Clamped to [MinGain, MaxGain], rounded away from zero.
- Effect: a hidden rating above the rank means bigger wins and smaller losses. A player at ~50% holds position.

### Placeholder numbers (to be tuned)

| Setting | Value |
|---|---|
| Arena thresholds (RR) | 0 / 300 / 700 / 1200 / 1800 / 2500 |
| BaseGain | 20 |
| MinGain / MaxGain | 8 / 40 |
| Divergence | 0.04 |
| Implied rating | 1000 + 0.5 per RR |
| Tau | 0.5 |

---

## 7. Matchmaking rules

- **Required:** same `ProtocolVersion`, `SimVersion` and `ContentHash`.
- **Hard cap:** the two players' arenas differ by at most 1. Arena here = `rank.arena`. If §13's smurf question is decided otherwise, use `max(arena, arena implied by MMR)`.
- **MMR window**, widening with wait: ≤100 at 0s, ≤250 at 20s, ≤500 at 60s, then unbounded *within the arena cap*.
- **Priority:** prefer same-arena opponents at equal wait. A cross-arena match is the fallback, not the norm.
- **Nobody in range:** keep waiting. After X seconds (default 90), offer an unranked bot match. Never break the arena cap.
- **Why a "bottom half of the arena above / top half below" band adds little:**
  - Power follows the era a player *fields*, and eras step at arena boundaries.
  - An RR band only makes cross-arena matches rarer. When one happens, the power gap is the same full era.
  - MMR already pairs by skill. What it can't see is the era gap.
- **Two real levers for the era gap:**
  - (a) Same-arena preference with a stricter wait before crossing. This is the default above.
  - (b) **Era sync:** a cross-arena match is played at the lower arena's era; the higher player's variants are clamped down. It is fair, but the higher player loses their edge. This is an open decision (§13).
- If (b) is adopted, the arena cap could later relax to ±2, since power no longer differs.

---

## 8. Match log format, replays, versioning

### Why a format of its own

The live wire only needs "same version or refuse". A log outlives builds: it must be readable by later builds, extendable without breaking older readers, and must name the exact simulation that produced it.

### Layout

- **Framing:** little-endian integers, like the existing serializers.
- **File header:** magic `NWML`, `FormatVersion` (u16), then chunks until EOF.
- **Chunk:** `tag (u16) | length (u32) | payload`. A reader skips unknown tags by length. Known tags never change meaning; to change a payload, add a new tag. Bump `FormatVersion` only for a break in the framing itself.

| Chunk | Contents |
|---|---|
| `HEADER` | `ProtocolVersion`, `SimVersion`, `ContentHash`, match ID, both UGS Player IDs, arena/era tier, seed, start time (server-issued) |
| `BOARD` | Board as data (layout, nodes, edges). Never "board #3": the board is being redesigned |
| `LOADOUTS` | Both loadouts: suits, districts, variants, skins |
| `DRAFT` | Draft placements in order |
| `TICKS` | Per tick with commands: tick, count, commands. Empty ticks omitted |
| `HASHES` | (tick, state hash) every 50 ticks |
| `RESULT` | Winner, end tick, final hash, reason (win / surrender / disconnect) |
| `SIGNATURES` | Per player: session-key signature over that player's commands |

**As built (v1, 2026-09-26):** tags 1-7 are HEADER, BOARD, LOADOUTS, DRAFT, TICKS, HASHES, RESULT, then ERAS (8) and SKINS (9). BOARD is `BoardConfigData` rather than a free-form graph, the variants are per-type era tables in ERAS, and `SIGNATURES` is not written yet (it arrives with Stage 7's session keys). Match ID, start time and the opponent's Player ID are local placeholders until the server issues them.

- **Signing:** at match start the server issues each player a session key. Each player signs their own commands. Any single uploaded log is then verifiable: a cheater cannot forge the opponent's inputs.
- **Size:** a few bytes per command, with most ticks empty. Compression is optional; measure first.

### Replays across versions

- A replay is inputs only. **A `SimVersion` change breaks it**, and format conversion cannot fix that: the inputs were decisions made against the old rules.
- Options for replays that must survive:
  - **Keep old simulation builds** (separate DLLs, cheap because the sim is Unity-free).
  - **Convert to a snapshot replay on demand** (a button): run once on the matching old sim, store states at intervals, and playback needs no sim.
- The user accepts that replays break at major versions.
- **Default:** mark old replays unavailable. Build conversion only when asked.

---

## 9. Referee vs server-authoritative simulation

| | P2P lockstep + referee (building this) | Server-authoritative simulation |
|---|---|---|
| Cost | ~Free: one Cloud Code call per match | Game-server hosting per match-minute. Cloud Code cannot host it |
| Trusted result | Yes, after the match | Yes, live |
| Rage-quit | Honest log suffices (signed commands) | Server continues; reconnect is easy |
| Hidden info (fog of war) | **Exposed**: both clients hold full state | Protected: each client gets only what it can see |
| Bots/macros | Undetected | Undetected |
| Feel | Unchanged | Similar: lockstep already has input delay; 10Hz suits a server |
| Operations | None | Regions, scaling, uptime |

- **Direction:** the referee now. Server-authoritative simulation later, if fog of war or live anti-cheat becomes a requirement.
- **What carries over:** the match log format and headless runner are shared by both, so a later switch changes the transport, not the game.
- **Before paying for hosting:** run the side spike (§5).

---

## 10. Effect on RL

- **The server has no direct effect:** training runs the headless sim.
- **Two indirect benefits:**
  - The Stage 5 runner *is* most of the RL environment (`docs/direction.md` §3).
  - Ranked match logs are imitation-learning and evaluation data.
- **Eras add a dimension** to what a model sees and does (variant per slot). Design RL observations after the board redesign and the era model settle.

---

## 11. Pitfalls

- **Account linking must work before any progress is worth losing.** An anonymous account dies with a reinstall.
- **Never trust client values:** MMR in tickets, results, loadouts/variants, balances, time, rolls.
- **Variants are simulation changes.** Each one goes through the full determinism checklist, and each one bumps `SimVersion`.
- **Skins must never reach `SimulationState`.** If a skin ever affects an outcome, the design is broken.
- **Replay snapshots must cover every `SimulationState` field.** A missed field makes rewind silently wrong. The snapshot round-trip test guards this.
- **Cloud Code limits** (execution time, memory, payload size) decide the referee design. The spike comes first.
- **Matchmaker may need a payment method.** Ask before enabling it.
- **No secrets in the repo or in chats.**
- **Don't commit with `done:`** unless a Notion task is finished.
- **Don't stamp `verified:` / `verified_at_commit`** on docs. That is the user's act.

---

## 12. How to run the work

Agreed 2026-09-27 for Stages 7-8: the waves and contract in §15.
`.claude/skills/delegation.md` still governs: at most two agents in flight,
in waves, with cost estimated first.

Standing constraints:
- **Plan mode first:** Stages 3, 4, 5, 6, 9 (network path, match log, `Simulation/` reads or changes).
- **Checks:**
  - `dotnet test dotnet/NodeWar.sln` for rules, lobby, log format and sim.
  - `scripts/compile-check.ps1` for Unity-side code.
  - The Editor via the `unity` CLI when `unity status` is ready. Scene wiring is done this way, never by editing YAML.
  - `ugs` CLI for deployed state.
- **Well suited to delegation** (mechanical, specified, testable): rules with tests (matchmaking window, era unlocks, catalog validation), local fakes, log reader/writer tests once the format is fixed, UXML/USS style passes.
- **Keep in the main session:** Cloud Code/UGS integration, account flow, handshake, match log format design, referee, anything in `Network/` or `Simulation/`.

---

## 13. Open questions, with defaults to build towards

| Question | Default until decided |
|---|---|
| Era sync in cross-arena matches (§7 b)? | No. Same-arena preference, ±1 cap |
| Demotion below an arena: keep that era's variants? | Keep ownership; usable only while in or above that arena |
| Smurfs (high MMR, low arena): which arena counts for the cap? | `rank.arena` |
| Minimum RR loss (currently always ≥ MinGain)? | Keep |
| Demotion protection at arena boundaries? | None |
| Placement matches, or just high starting RD? | High starting RD only |
| Arena count, names, era themes, per-arena rewards | 6 arenas (thresholds §6); names/themes TBD by the user |
| Inactivity period length | 1 week |
| Replay retention per player | Last 20 |
| Bot fallback in queue | Unranked, offered after 90s |
| Can boxes/shop grant variants? | No: skins only |
| Replay conversion across `SimVersion` | Not built; old replays marked unavailable |

---

## 14. Sources

- UGS billing FAQ (free tiers, no-card services): https://support.unity.com/hc/en-us/articles/6821475035412-Billing-FAQ-Unity-Gaming-Services
- UGS pricing estimator: https://unity-player-services-pricing-estimator.ds.unity3d.com/
- Economy sunset notice: https://discussions.unity.com/t/clarification-on-long-term-support-and-sunset-notice-for-existing-unity-economy-projects/1734744
- Cloud Code C# modules: https://docs.unity.com/ugs/en-us/manual/cloud-code/manual/modules
- Cloud Code .NET version: https://discussions.unity.com/t/cloud-code-c-updates-net-version-upgrade-and-module-details-page/952579
- Cloud Code module cost: https://docs.unity.com/en-us/cloud-code/modules/reference/cost
- Cloud Code via UGS CLI: https://docs.unity.com/ugs/en-us/manual/cloud-code/manual/modules/how-to-guides/write-modules/cli
- UGS CLI project roles: https://docs.unity.com/legacy-services-docs/guides/ugs-cli/latest/general/troubleshooting/project-roles/
- UGS CLI login: https://services.docs.unity.com/guides/ugs-cli/latest/general/base-commands/login/
- Unity environments: https://docs.unity.com/en-us/services/service-environments
- Remote Config package: https://docs.unity3d.com/6000.4/Documentation/Manual/com.unity.remote-config.html
- Unity Authentication (identity providers, Unity Player Accounts, Steam, linking): https://docs.unity.com/ugs/en-us/manual/authentication/manual/overview
- Glicko-2 paper: https://glicko.net/glicko/glicko2.pdf

---

## 15. Delegated work, Stages 7-8: contract, waves, budget

### Contract every delegated agent gets (paste it, do not paraphrase)

1. **Worktree first.** `git worktree add .claude/worktrees/<pkg> -b stage7/<pkg> feat/stage7`, work only there, then from its root `cmd /c mklink /J Library C:\Dev\NodeGame\Library`. `dotnet` lives in `C:\Program Files\dotnet`: prepend it to PATH.
2. **Read before writing:** this file's section for your package, `CLAUDE.md`, and only the files the prompt names. Ask nothing; if the spec is ambiguous, pick the reading closest to the spec, and list it under "Assumptions" in the final report.
3. **Never:** touch `Assets/Scripts/Game/Simulation/`; hand-edit `.unity`, `.prefab` or `.meta`; prefix a commit with `done:`; stamp `verified:` / `verified_at_commit`; deploy (`ugs deploy`); push; put a secret anywhere; trust a client-supplied value (MMR, arena, result, time, loadout).
4. **New `.cs` files under `Assets/`** have no `.meta`. Leave them missing and list them: the lead creates them through the Editor.
5. **Code rules.** `Backend/Shared` and anything compiled into Cloud Code: C# 9, no UnityEngine. Stored record fields are never renamed (add a new one). Server time only. Doubles are fine in `NodeWar.Progression`, never in `Simulation/`. Match the surrounding code's comment density and naming.
6. **Commit after every step that compiles**, message `feat|test|fix: <what>` plus the co-author line from the prompt. Work that is not committed is lost if the agent dies at a usage limit.
7. **Checks, with receipts in the final report:** `dotnet test dotnet/NodeWar.sln` (paste the per-project pass counts **before and after**; the difference must equal the tests added, because the projects under `dotnet/` list sources explicitly with `<Compile Include>` and an unlisted file is silently not built), and `scripts/compile-check.ps1` for anything under `Assets/`. Red is reported as red, not worked around.
8. **Final report:** commits (sha + subject); files touched; tests added; assumptions; anything left undone and why. Under 300 words.

### Who does what

| Role | Profile | Takes |
|---|---|---|
| Lead (Claude, this session) | Opus | Specs, R's decision, `.meta`s through the Editor, merges into `feat/stage7`, deploys, 8.2 (network handoff), 7.6, final read of each package |
| Engineer | Codex astra, high (Claude Sonnet when GPT is spent) | 7.1-7.5 done; 8.1a, 8.2a |
| Reviewer | Codex Sol, high | **First-pass review of every package**, against its section here and the contract: correctness, concurrency, trust boundaries, tests that assert the spec rather than the implementation |
| Utility | Codex luna, low | R (research), mechanical fixes from a review |

**Why review goes to Sol first.** Reading diffs is where the lead's budget went unaccounted before (GPT ~70% vs Claude 100% at the end of past sessions). Sol reads the whole diff; the lead reads Sol's findings plus the hunks they cite, and reruns the test suite (cheap output) rather than rereading everything.

### Waves and estimates (GPT window ≈ 6M tokens per rolling 5 h)

| Wave | Agents | GPT est. | Lead (Claude) work |
|---|---|---:|---|
| 1 | 7.1 Engineer · R Utility (web, capped at ~15 fetches) | 0.8M + 2.5M | Read R's report; decide storage and matchmaking shape; update 7.2/8.x here |
| 2 | 7.2 Engineer · 7.3 Engineer | 1.4M + 1.0M | `.meta`s; merge |
| 2r | Sol reviews 7.1 + 7.2 | 1.2M | Read findings; fixes go back to the same Engineer (warm context) |
| 3 | 8.1 Engineer · 7.5 Engineer | 1.2M + 1.2M | Matchmaker enablement with user; deploy to `development` |
| 3r | Sol reviews 8.1 + 7.5 | 1.2M | as 2r |
| 4 | 7.4 Engineer | 0.8M | **8.2 in the main session** (plan mode if Sessions) |
| 5 | Sol reviews the whole of `feat/stage7` vs `feat/backend` | 1.9M | Live two-client ranked match on `development`; fixes |
| — | 7.6 signatures | Sol review 0.8M | Main session (wire change) |

GPT total ≈ **15M, about 2.5 windows**; waves 1-2r fit the first window (~6.9M, so 2r may slip past the reset). The lead's share is specs, the R decision, `.meta`s, merges, deploys, 8.2 and 7.6, plus reading findings rather than whole diffs. Measure after each wave with the command in `.claude/skills/delegation.md` and correct this table.

**Measured 2026-09-27** (replace the estimates above with these): Codex R research 0.41M (capped at 15 fetches); Codex 7.3 1.05M; Codex 7.1 + 7.2 on one reused agent 2.97M; **Sol whole-branch review 2.44M** (2x estimate). The Codex window emptied in ~35 min. Then Claude Sonnet via the Agent tool: the three review fixes 208k; 7.4a 94k; 7.5a 122k. Lessons: small bounded packages with files named cost 0.1-0.2M on Sonnet; Sol reviews should get only the changed files and their spec lines; start a fresh agent once one's context passes ~1.5M instead of reusing it.

### Queued by the user (2026-09-28), after 8.1a and 8.1b land

1. **Audit Stages 7-8 for bad code, systems view first:** trust boundaries, failure modes (a service down, a call retried, a player leaving mid-flow), data ownership, then line-level issues.
2. **Check that commits and branches line up as intended:** `feat/stage7` against `feat/backend` / PR #72, the stacked PR #73, leftover worktrees and branches, merges that went sideways.
3. **A minimal, swappable ranked-queue UI:** an entry point plus a searching / found / failed status view, driven through an interface so the visuals can be replaced without touching `IRankedQueueService`.

#### Systems audit result (Sol, 2026-09-28) and triage

- **Fixed live:** players could call `Matchmaker_Allocate` directly and forge a match against any opponent (reproduced in development). Allocate/Poll now refuse calls carrying a player identity; the Matchmaker calls as a service (`b1d40bf0`, verified both ways). Dev junk from the reproduction: records `forged-test-1..3` and possibly records for fake player IDs `someoneElse123/456/789`.
- **Fix, package A (server):** one active ranked match per player, claimed in Allocate (refused if another unexpired claim exists), released on Settled/Void/Disputed, expiring so a stuck match cannot lock a player out (the Critical: a win and a loss settled from the same snapshot erased the loss). Allocate accepts only this server's `SimulationVersion` and `ProtocolVersion`. `GetPlayerState` writes inventory with the lock (as Equip). The in-memory store throws the production conflict exception.
- **Fix, package B (client):** `BackendServices.Remember` binds a result to the player the request started for. Delete `NodeWar.Progression/Matchmaking.cs` and its tests (D5: the live queue is proven, and its same-arena rule already disagrees with the deployed 30 s rule).
- **Accepted before release:** account deletion mid-settlement (define a terminal policy before launch); keeping old referee builds per supported version tuple; unsigned logs forcing `Disputed` (7.6).
- **Deferred:** abandoned-record cleanup (7.5b retention; the claim expiry bounds harm); client timeouts and retries (8.2b).
- **Audit fixes A and B merged and verified live (2026-09-28).** One active ranked match per player (`activeMatch = { matchId, expiresUnixSeconds }`, 2 h): A+B matched, then A+C was refused with "Player already has an active match." Allocate accepts only this server's `SimulationVersion` and `ProtocolVersion.Current` (now one constant in `Backend/Shared`, aliased by `InputSerializer.ProtocolVersion`). `GetPlayerState` writes inventory under its lock. `Remember` binds to the requesting player. `Matchmaking.cs` deleted. 1021 tests.
- **For 8.2b:** a refused allocation fails *both* tickets, so the innocent opponent (C above) must re-queue automatically on that message; and the client must not offer the queue while the player holds an active match.
