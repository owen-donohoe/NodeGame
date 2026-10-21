---
system: backend-client
summary: Provides account-scoped async services and ranked flow controllers for Unity UI and match lifecycle callers.
sources:
  - Assets/Scripts/Backend/BackendServices.cs
  - Assets/Scripts/Backend/GameServices.cs
  - Assets/Scripts/Backend/PendingRankedReports.cs
  - Assets/Scripts/Backend/Shared/AccountService.cs
  - Assets/Scripts/Backend/Shared/DisconnectHold.cs
  - Assets/Scripts/Backend/Shared/InventoryService.cs
  - Assets/Scripts/Backend/Shared/MatchHistoryService.cs
  - Assets/Scripts/Backend/Shared/SocialService.cs
  - Assets/Scripts/Backend/Shared/ProfileService.cs
  - Assets/Scripts/Backend/Shared/PlayerStatsRules.cs
  - Assets/Scripts/Backend/UgsProfileService.cs
  - Assets/Scripts/Backend/Shared/GuildService.cs
  - Assets/Scripts/Backend/Shared/GuildPolicies.cs
  - Assets/Scripts/Backend/Shared/MatchReportService.cs
  - Assets/Scripts/Backend/Shared/MatchReportTypes.cs
  - Assets/Scripts/Backend/Shared/NodeWar.Backend.Shared.asmdef
  - Assets/Scripts/Backend/Shared/PlayerState.cs
  - Assets/Scripts/Backend/Shared/PlayerStateLogic.cs
  - Assets/Scripts/Backend/Shared/PlayerStateService.cs
  - Assets/Scripts/Backend/Shared/PresenceHeartbeat.cs
  - Assets/Scripts/Backend/Shared/ProtocolVersion.cs
  - Assets/Scripts/Backend/Shared/RankedMatchTypes.cs
  - Assets/Scripts/Backend/Shared/RankedQueuePresenter.cs
  - Assets/Scripts/Backend/Shared/RankedQueueService.cs
  - Assets/Scripts/Backend/Shared/RankedRendezvous.cs
  - Assets/Scripts/Backend/Shared/RankedResultTracker.cs
  - Assets/Scripts/Backend/Shared/RememberGuard.cs
  - Assets/Scripts/Backend/UgsAccountService.cs
  - Assets/Scripts/Backend/UgsInventoryService.cs
  - Assets/Scripts/Backend/UgsMatchHistoryService.cs
  - Assets/Scripts/Backend/UgsMatchReportService.cs
  - Assets/Scripts/Backend/UgsPlayerStateService.cs
  - Assets/Scripts/Backend/UgsRankedMatchService.cs
  - Assets/Scripts/Backend/UgsRankedQueueService.cs
verified: b006bbaa
---

# Backend client

## Purpose
Client code asks services for authoritative account, inventory and ranked state. Shared presenters isolate asynchronous failure and connection orchestration from UI layouts. Local fakes serve tests and offline Editor work.

## Interface
`GameServices.InitializeAsync()` initializes UGS without requiring a player; account sign-in uses it. Data services await `EnsureReadyAsync()` for initialization plus sign-in. Editor environment follows Project Settings; Development Builds use `development`, releases `production`.

`BackendServices` selects `Ugs*Service` or `Local*Service` for `Account`, `PlayerState`, `Inventory`, `MatchReports`, `MatchHistory`, `Profiles`, `RankedQueue`, `RankedMatch`. Editor menu `Tools > Node War > Backend > Use Local Fakes` selects fakes; builds use UGS. `Social` and `Guild` remain local. `Tools > Node War > Backend > Admin` calls admin grants in Play mode. `LastKnownState`/`StateChanged` expose accepted current-account state; `RememberGuard` rejects previous-account results and partial inventory snapshots. Cloud module: `NodeWarCloud`.

Shared `ProtocolVersion.Current` is 7; the network serializer aliases this constant, and server allocation checks it alongside simulation/content identity.

Service contracts in UnityEngine-free `Backend/Shared/`:
- `IAccountService`: `Current`, `Changed`, `EnsureSignedInAsync`, `LinkAsync`, `SwitchToLinkedAccountAsync`, `SignInWithAccountAsync`, `SignOutAsync`. `AccountInfo` carries status, player ID and display name; `LinkResult` distinguishes conflict from success.
- `IPlayerStateService.GetAsync()` returns `PlayerState`; `IInventoryService.EquipAsync(EquippedRecord changes)` and `IHandleService.SetHandleAsync(name, tag, requestId)` (`BackendServices.Handles`; server only) return accepted state.
- `IMatchReportService.ReportAsync(matchId, byte[] log)` returns `MatchReportingResult` with nullable `state`, `message`, `playerState`; UGS sends base64 to `ReportMatch`.
- `IMatchHistoryService.GetAsync()` returns `List<MatchHistoryEntry>`: ID, opponent ID and snapshotted `OpponentName`/`OpponentTag`, state, creation time, optional win/RR delta/arena.
- `IGuildService.GetMineAsync/GetAsync/SearchAsync/BrowseAsync/GetChatAsync/SendChatAsync` supplies membership, details, search, browse and chat. Views include descriptions, member roles/seniority/online/RR, own role, announcements and weekly rewards. `GuildMembers.Sorted` copies with ID ties; `IsOfficer` gates moderation. `LocalGuildService.InGuild` switches sample membership. `GuildFilters`, `GuildChatText` and `GuildBrowseCache` guard filters, 200-character messages and session browse caching.
- `ISocialService.GetLiveMatchesAsync/GetFriendsAsync` return `LiveMatch`/`PlayerCard` lists. `IProfileService.GetAsync(playerId)` returns `PublicProfileView`: card, nullable stats, lobby loadout IDs and recent summaries. `UgsProfileService` calls `GetProfileView`, rejects account changes, and supports empty ID for self. Server presence is Offline; stats are Matches/Wins/Losses/Streak/BestRR. Local profiles use accepted self state/local slots/history or deterministic samples.
- `IRankedQueueService.EnqueueAsync()` returns ticket ID; `PollAsync(ticketId)` returns `RankedQueueResult` (Searching/Found/Failed/TimedOut/Cancelled, match ID, message); `CancelAsync(ticketId)` deletes it.
- `UgsRankedQueueService(rated: false)` reuses ticketing with `noRR: true` in isolated `unranked-1v1`; default is `ranked`. Backend configuration must match by authoritative RR and support unrated allocation/rendezvous with no progression settlement.
- `IRankedMatchService`: `RendezvousAsync(matchId, joinCode)`, `ConfirmConnectedAsync(matchId)`, `LeaveAsync(matchId, forfeit)`, `GetResultAsync(matchId)`, `PresenceAsync(matchId, holding)`, `ResolveHoldAsync(matchId)` map to Cloud Code `Rendezvous`, `ConfirmConnected`, `LeaveMatch`, `GetMatchResult`, `Presence`, `ResolveHold`.

`PlayerState` groups `RatingRecord` (R/Rd/Sigma, last match, settled IDs), `RankRecord` (RR/Arena/HighestArena), `InventoryRecord` (owned variants/skins, chosen suit/node IDs, `EquippedRecord` base-ID-to-variant/skin dictionaries), `HistoryRecord` (match IDs, stats, twenty retained summaries), `ActiveMatchRecord` (match ID, expiry), `DisciplineRecord` (level, strike/decay/block timestamps, non-reports, struck IDs) `WalletRecord` (version, `Coins`, `Premium`, last fifty `WalletTransaction`s) and `ProfileRecord` (`Name`, `Tag`, `NameNorm`, last free rename). Their Cloud Save keys are `rating`, `rank`, `inventory`, `history`, `activeMatch`, `discipline`, `wallet`, `profile`. `HandleRules` (validation, normalisation, rename cost) and `HandleDisplay` (Name#TAG, capped headers, first-run name) are shared. `WalletLedger.Apply` is the only balance change: it refuses a repeated request ID or a negative result. `PlayerStateDefaults`/`WalletLedger.Starting` set first-time values.

`RankedQueuePresenter.StartAsync(now)`, `Tick(now)`, `RequestCancelAsync()` and `Dispose()` drive `IRankedQueueView`; view events are `CancelRequested`, `ForfeitConfirmed`, `BotAccepted`. View methods show idle/search/found/failure/cancel/connecting/requeue/forfeit/waiting/bot-offer/blocked states. `MatchFound` and `BotMatchAccepted` hand off outcomes. Shared `BotOfferSeconds` is 90; poll exceptions fail immediately while cancelling the ticket.

`RankedRendezvous.Start/Tick/Cancel` drives `IRankedConnection.Host(matchId, playerIds)` or `Join(matchId, playerIds, joinCode)`, reads its phase/code/failure and raises `Connected` or `Failed`. `RendezvousResult` supplies roster, caller slot, code, connection flag, map ID and board hash. Slot 0 hosts, slot 1 joins.

`DisconnectHold.Start/Resume/Act/Tick` exposes `Current: HoldStatus` and `Changed`; `PresenceHeartbeat.Tick/ProbeNow/Stop` exposes `Urgent`, `Unreachable`, `Decided` and `ReachabilityChanged`. `RankedResultTracker.Start/Stop/Tick` exposes result status via `Current`/`Changed`. `MatchResultView` carries terminal state/cause, nullable win/RR/arena, promotion/demotion and player state; presence/hold responses may embed it.

## Connects to
- `cloud`: UGS implementations call module functions; queue tickets call Matchmaker directly.
- `progression`: shared inventory, rank, catalog and capacity policies support accepted-state display and local fakes.
- `lobby-ui`: account controls, Workshop, history and `IRankedQueueView` consume services and state events.
- `match-flow`: `IRankedConnection` wraps launch; ordered ranked identity reaches `MatchConnection` and recording.
- `netcode`: build identity goes into tickets; holds/speculation trigger urgent server presence.
- `match-log`: finished ranked bytes enter `PendingRankedReports`.
- `match-ui`: hold and result controllers provide server-backed overlay/end-card states.
- `bots`: accepting fallback cancels the ticket before launching an unranked match.

## Invariants
Services are async and can refuse or throw. UI displays returned state rather than optimistic grants or ratings. Results never cross account boundaries. Local report/history/queue fakes simulate responses, not real server settlement. Non-ranked matches never upload ranked reports.

## How it works
Anonymous authentication is the initial identity. UPA linking preserves its UGS Player ID; a conflict offers explicit account switching without merging. Sign-out clears cached credentials. `LinkPromptPolicy` prompts once progress matters; Steam linking is unbuilt.

Queue preflight retries pending reports, fetches state and clears or offers forfeiting an active match. It displays discipline blocks before enqueueing. UGS initializes player records before creating tickets containing protocol/sim/content/noRR. Polling runs every two seconds; poll exceptions expose Retry immediately. Match/rendezvous failures recheck active claims and permit three automatic requeues. After ninety seconds a bot offer leaves the ticket live until accepted.

Rendezvous exchanges a Relay code through the record, without UGS Lobby. Guest code wait is fifteen seconds; host guest wait is twenty-two after publication. Failure leaves the record and cancels the connection. Both sides confirm connection before draft handoff.

Presence runs every four seconds from draft onward, every second when urgent. Holds wait ten seconds, allow action until sixty, and require a ranked server decision; ninety seconds offline permits leaving. Result tracking polls every two seconds for forty seconds and remembers accepted returned state.

`PendingRankedReports.Add(playerId, matchId, log)` persists under `PendingReports`; `RetryAsync()` coalesces callers and sends only the signed-in player's files after match end, lobby load and queue preflight. Exceptions retain files; any server answer, including refusal, removes that pending upload.

## Decisions
- Keep authoritative services behind async UGS adapters rather than local authority; Social/Guild supply placeholder local data.
- Use direct client tickets and private server rendezvous rather than a queue proxy or public lobby.
- Offer explicit account switching rather than merge anonymous progress on link conflict.

## Pressure points
Platform calls lack a universal timeout/retry policy. Pending reports can accumulate during prolonged failure. Fakes cannot validate production races. Shared records span client/server serialization, so field changes need coordinated compatibility. Replay download/viewer services are absent.

## Change recipes
- Add a service operation: update Shared contract/DTO, UGS adapter, fake and presenter tests; bind returned state to the requesting account.
- Replace queue visuals: implement `IRankedQueueView`; retain presenter cancellation, preflight and bot-accept sequencing.

## Tests
`NodeWar.Lobby.Tests` covers shared presenters, account/remember rules, fakes and inventory/display policies; `NodeWarCloud.Tests` covers server contracts. Compile-check covers Unity service adapters. UPA browser sign-in, real Relay and scene wiring need manual verification.
