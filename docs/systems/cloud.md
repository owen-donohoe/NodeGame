---
system: cloud
summary: Owns ranked allocation, player records, verified outcomes, progression settlement and disconnect discipline in UGS.
sources:
  - dotnet/NodeWarCloud/Matchmaker/
  - dotnet/NodeWarCloud/NodeWarCloud/Balances/
  - dotnet/NodeWarCloud/NodeWarCloud/Catalog/
  - dotnet/NodeWarCloud/NodeWarCloud/CloudSaveMatchRecordStore.cs
  - dotnet/NodeWarCloud/NodeWarCloud/CloudSavePlayerRecordStore.cs
  - dotnet/NodeWarCloud/NodeWarCloud/InventoryPlayerStateService.cs
  - dotnet/NodeWarCloud/NodeWarCloud/InventoryRules.cs
  - dotnet/NodeWarCloud/NodeWarCloud/MatchAllocation.cs
  - dotnet/NodeWarCloud/NodeWarCloud/MatchDiscipline.cs
  - dotnet/NodeWarCloud/NodeWarCloud/MatchEligibility.cs
  - dotnet/NodeWarCloud/NodeWarCloud/MatchHistory.cs
  - dotnet/NodeWarCloud/NodeWarCloud/MatchHistoryModule.cs
  - dotnet/NodeWarCloud/NodeWarCloud/MatchHold.cs
  - dotnet/NodeWarCloud/NodeWarCloud/MatchLogUpload.cs
  - dotnet/NodeWarCloud/NodeWarCloud/MatchmakerAllocatorModule.cs
  - dotnet/NodeWarCloud/NodeWarCloud/MatchRecord.cs
  - dotnet/NodeWarCloud/NodeWarCloud/MatchRecordStore.cs
  - dotnet/NodeWarCloud/NodeWarCloud/MatchRendezvous.cs
  - dotnet/NodeWarCloud/NodeWarCloud/MatchReporting.cs
  - dotnet/NodeWarCloud/NodeWarCloud/MatchReportingModule.cs
  - dotnet/NodeWarCloud/NodeWarCloud/MatchSettler.cs
  - dotnet/NodeWarCloud/NodeWarCloud/ModuleSetup.cs
  - dotnet/NodeWarCloud/NodeWarCloud/NodeWarCloud.csproj
  - dotnet/NodeWarCloud/NodeWarCloud/PlayerStateModule.cs
  - dotnet/NodeWarCloud/NodeWarCloud/Properties/
  - dotnet/NodeWarCloud/NodeWarCloud/RankedMatchModule.cs
  - dotnet/NodeWarCloud/NodeWarCloud/ServerCatalog.cs
verified: a1da5182
---

# Cloud

## Purpose
UGS Cloud Code and Cloud Save hold authority over ranked identity, results and progression while peers run gameplay. The server refuses client-proposed authority and settles only eligible outcomes.

## Interface
Module `NodeWarCloud` exposes these Cloud Code functions; player calls use authenticated `context.PlayerId`, not a supplied player ID:
- `GetPlayerState()` → `PlayerState`; initializes protected records, grants defaults and normalizes inventory.
- `Equip(EquippedRecord changes)` → `PlayerState`; validates base, ownership and current-arena eligibility under an inventory write lock.
- `VerifyMatch(string logBase64)` → `RefereeVerdict`; consistency verification only, without ranked settlement.
- `ReportMatch(string matchId, string logBase64)` → `MatchReportingResult` (`state`, `message`, `playerState`).
- `GetMatchHistory()` → `List<MatchHistoryEntry>` from caller history and stored outcomes.
- `Rendezvous(string matchId, string joinCode)` → `RendezvousResult` (roster, slot, code, connection state, map ID/hash); only slot 0 publishes a code.
- `ConfirmConnected(string matchId)` → completion; both confirmations establish connection time.
- `LeaveMatch(string matchId, bool forfeit)` → `LeaveMatchResult` (outcome, message, seconds left, player state).
- `GetMatchResult(string matchId)` → `MatchResultView` (state/cause, optional win/RR/arena, promotion/demotion, player state).
- `Presence(string matchId, bool holding)` → `PresenceResult` (opponent age, server hold duration, optional terminal result).
- `ResolveHold(string matchId)` → `ResolveHoldResult` (outcome, message, result).
- `Matchmaker_Allocate(AllocateRequest)` → `AllocateResponse`; `Matchmaker_Poll(PollRequest)` → `PollResponse`. These callbacks reject player identities; allocation/assignment custom data carries `{ matchId }`.

Ranked-match functions require caller membership. `MatchAllocation` consumes the server-to-server two-player roster and ticket `{ protocol, sim, content }`; it creates `MatchRecord` before play. `RankedMap` chooses catalog map `hourglass-01` and its board hash, never client proposals.

`CloudSavePlayerRecordStore` uses Protected player keys: `rating` (`RatingRecord`), `rank` (`RankRecord`), `inventory` (`InventoryRecord`), `history` (`HistoryRecord`), `activeMatch` (`ActiveMatchRecord`), `discipline` (`DisciplineRecord`). Players read; only server writes. `CloudSaveMatchRecordStore` uses Private custom item `match-<id>`: `record`, `presence-0`, `presence-1`, `log-0`, `log-1`. These are server-readable only; logs are base64 per reporting slot, not a public download API.

`MatchRecord` contains roster, build/map identity, creation time, pre-match `MatchPlayerSnapshot[]` (rating, rank, owned variants), lifecycle state, reports, outcomes, Relay code, connection timestamps, committed forfeit/abandonment and discipline markers. `IMatchRecordStore`, `ISettlementPlayerStore` and `IDisciplinePlayerStore` separate storage from rules.

Deploy from repository root: `ugs deploy dotnet/NodeWarCloud -e development`; queue: `ugs deploy dotnet/NodeWarCloud/Matchmaker -s matchmaker -e development`. Use UGS CLI 2.0.0 for Matchmaker. Editor chooses its configured environment, development builds use `development`, releases use `production`. Production provisioning/deployment remains a release prerequisite; repository files do not prove live deployment state.

## Connects to
- `backend-client`: async services call endpoints and display returned authoritative records/results.
- `match-log`: referee decodes and replays uploads; private log keys retain evidence.
- `progression`: `MatchSettler` invokes `MatchSettlement`, inventory grants/clamp and discipline rules.
- `board`: allocation and referee use the server catalog's map and hash.
- `draft`: eligibility validates loadouts, chosen capacities and placed district multisets.
- `netcode`: tickets/records enforce matching protocol, simulation and content identity.
- `tooling`: catalog/balance exports and UGS deployment supply server-known content.
- `economy`: content-addressed balance exports provide the same economic tuning to referee replay.

## Invariants
Use server time. Stored field identities remain compatible. Conditional updates re-read and revalidate on conflict. Commit a forfeit decision under the match lock before player writes. One player holds one unexpired active claim. Client claims never choose roster, ratings, map or winner.

## How it works
`ranked.mmq` defines one player per ticket, two-player matches, a 300-second pool timeout, equal build identity, arena gap at most one, same-arena preference for thirty seconds, and rating windows 100/250/500 at 0/20/60 seconds, unbounded at 120. Rules explicitly enable themselves and read protected rating/arena. Allocation rechecks hard caps, known balance, current versions and discipline; the soft rating window stays queue-only. Idempotent retries return the existing record. Claims expire after two hours.

`ReportMatch` checks record/header identity, server map, snapshot ownership and draft eligibility, then runs the referee. Protocol 6 low arenas choose two suits plus free Warrior and two districts plus catalog base pools; high arenas choose three suits and five districts without free grants. Historical protocols retain their eligibility path. Fewer placements are valid for unplaceable pieces.

First accepted winning report becomes Pending; two must agree on winner/end tick/final hash. Disagreement becomes Disputed. Refusals do not consume the accepted slot and are capped. Pending timeout is ten minutes, evaluated lazily. Two consistent unfinished reports use the earlier log's Core breaches: fewer wins, ties void, no strikes.

`MatchSettler` uses pre-match snapshots, updates ratings/RR, grants/clamps inventory and retains twenty history IDs. Each player's four-record conditional batch includes a separate 200-ID `RatingRecord.SettledMatchIds` guard. Partial cross-player settlement retries safely. `LeaveMatch` voids unstarted/expired matches, completes committed outcomes, or requires explicit forfeit for played matches; surrender loses without a strike.

Presence writes only the caller's slot key, never `record`. A claim needs connection age ≥15 seconds, caller hold ≥10 seconds and opponent absence >10 seconds. An opponent seen but not holding after thirty seconds also counts absent. Mutual seen holds permit void after caller/opponent holds of sixty/fifty seconds. Decisions precede claim-expiry handling; terminal answers report them.

Abandonment strikes; a lone accepted report at timeout adds a non-report, striking from the second within seven days. Levels 1–2 have no block, 3–4 two minutes, 5 one hour, 6 one day, 7+ two days; one level decays per sixteen strike-free hours. Separate protected discipline writes finish before claim release, guarded by `disciplineApplied`.

## Decisions
- Use UGS rather than custom hosting or UGS Economy.
- Allocate server-side before play rather than trust first-report rosters or settlement-time snapshots.
- Use write-lock retries and bounded idempotency rather than assume cross-player transactions.
- Use server presence rather than infer fault from symmetric peer silence.

## Pressure points
Unsigned logs permit fabrication and withholding. First record creation lacks atomic create-if-absent across workers. Retention/abandoned-record cleanup, account deletion during settlement and supported old referee builds need explicit policies. Mutual fake holds remain indistinguishable from a broken peer link.

## Change recipes
- Add an endpoint/record field: coordinate Shared DTOs, module authorization, stores, UGS/fake clients and conflict/idempotency tests.
- Ship content: export catalog and every balance hash, deploy module and queue, verify environment before ranked play.

## Tests
`NodeWarCloud.Tests` covers authorization, allocation, reporting, conflicts, partial settlement, holds and discipline; `NodeWar.Progression.Tests` covers pure rules. Live queue/Relay and deployment verification remain separate.
