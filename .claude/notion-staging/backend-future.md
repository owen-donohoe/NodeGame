# Backend future work

Staging for unresolved work and decisions from BACKEND-PLAN.md. Existing result tracking, holds, surrender, discipline, speculation, queue/rendezvous retries and the headless balance rig are implemented and are excluded. Current implementations remain authoritative where the plan's status tables lag.

## Command signatures and single-log settlement
Issue a session key per player, sign each player's command batches and retain peer signatures in a new SIGNATURES chunk. Verify signatures so one honest upload can settle a timed-out Pending match; unsigned consistency currently cannot prove opponent input or prevent withholding/disputes.
Dependencies: ranked allocation/session identity, command and wire compatibility design, protocol bump, new TICKS tag if its payload changes; deferred beyond soft launch.

## Signed match-start loadouts
Consider server-issued signed loadouts tied to the match session, preventing modified clients from fielding unowned choices before post-match refusal. Snapshot eligibility already exists; this task concerns proof at match start.
Dependencies: session-key/signature design, draft/loadout exchange, current variant redesign and server ownership rules.

## Replay viewer and download service
Build a dedicated scene reusing world views with a playback ITickProvider and an IReplayService-backed download path. Provide play/pause, half/normal/double/quad speed, step, seek and rewind; stop visibly on checkpoint mismatch and snap views during seek.
Dependencies: server-authorized log retrieval and history integration, playback lifecycle, view reset; seek re-simulates from tick zero under the later decision, rather than the superseded keyframe plan. The prerequisite balance rig exists.

## Replay retention and abandoned-record cleanup
Implement deferred retention: retain a match while either player's last twenty references it, then remove its private replay/record data. Clean abandoned records without losing pending settlement, discipline retries or dispute evidence; claim expiry only bounds player lockout, not storage growth.
Dependencies: lifecycle-safe reference checks, retry/idempotency policy and real storage measurements. Reconcile the proposed single replay copy with current per-slot log storage.

## Crash rejoin by command replay
Give MatchConnection a recoverable session identity and let a relaunched client retrieve the surviving peer's complete confirmed command stream, rebuild with MatchReplay and catch up. Transient packet recovery already exists; process death loses the in-memory session.
Dependencies: authenticated session recovery, retained command log, peer reconnection/Relay coordination and server terminal-result checks. Re-simulate rather than add snapshot transfer.

## First production deployment
Provision and deploy the module, catalog/balances and ranked queue to production, then verify a real two-player ranked flow with the user. Release builds target that environment; development deployment alone does not make them playable.
Dependencies: release readiness review, UGS access, current content exports, queue rules and environment checks. Confirm live provisioning state before acting; do not infer it from repository files.

## Account deletion during settlement
Define a terminal outcome and recovery policy if either account disappears between snapshot, partial player writes and record completion. Conditional retries cannot by themselves decide whether a deleted participant should block the survivor forever.
Dependencies: account lifecycle semantics, MatchSettler/idempotency design and failure-injection tests; accepted audit work before release.

## Supported referee version tuples
Decide which protocol/simulation/content tuples remain supported and retain matching referee builds/balances for those tuples. The current runner rejects a different simulation version, even when the log format is readable.
Dependencies: release/version policy, old simulation artifacts, routing/deployment strategy and catalog compatibility; accepted audit work before release.

## Cross-version replay conversion decision
Keep incompatible replays unavailable by default; implement conversion only when requested. Options are running an old simulation DLL or producing a snapshot replay by running once on its matching simulation; converting input format cannot preserve old rules.
Dependencies: supported-version policy, old builds and replay viewer/storage design; open replay-conversion question.

## General client timeouts and retry policy
Set bounded policies for asynchronous platform/service calls, cancellation and stale completions. Queue/rendezvous have specific deadlines and retries already; the broader service-outage policy remains deferred.
Dependencies: service contracts, account-scoped remember guards, UI recovery states and fault-injection tests.

## Draft disconnect recovery
Reconcile draft-specific disconnect termination with ranked presence/hold handling so a confirmed record does not simply remain held until queue preflight offers a forfeit. Decide whether draft interruptions resume, void or forfeit.
Dependencies: DraftManager, ranked connection/confirmation semantics and shared hold UX; recheck current draft timers rather than reuse the plan's obsolete timeout.

## Speculation feel and real-device validation
Play two clients across a real network and on backgrounded phones, using packet-drop controls to judge rewind, banners and recovery. Tune the twenty-tick speculative window from observed feel; already-played speculative cues can be corrected only after they are seen.
Dependencies: current lockstep/rollback implementation, separate player identities and device builds. This is validation/tuning, not rebuilding speculation.

## Rank, Workshop and history visual verification
Verify the completed rank display, era-chip and history-page layouts in Unity, especially equipped-versus-locked styling and narrow phone widths. Source status explicitly leaves these visuals unverified; dotnet tests do not cover layout/wiring.
Dependencies: connected Editor, relevant scenes and representative server/local state.

## Connecting-state presentation
Give queue-to-Relay handoff a clear connecting visual and recoverable feedback during automatic requeue. The swappable presenter/view contract exists; the stronger presentation remains later UI work.
Dependencies: IRankedQueueView, lobby styling and live rendezvous failure cases.

## Social chapter
Plan the separate Social surface for groups, friends, custom battles, chat and replay entry points. Decide how private Host/Join moves from the play popup as that surface arrives.
Dependencies: social product decisions, identity/accounts, private launch flow and replay viewer.

## Steam account linking
Integrate Steamworks.NET web API tickets with LinkWithSteamAsync/SignInWithSteamAsync so Steam and UPA resolve to the same UGS Player ID. Preserve the explicit conflict/switch policy rather than merge accounts.
Dependencies: Steam App ID/dashboard setup, installed authentication SDK verification and Steam platform integration.

## Shop, boxes, daily rewards and deals
Keep the combined shop stage on hold: server wallet/box/daily records, Remote Config prices/drop tables, atomic Purchase validation/deduction/grant and server-only OpenBox rolls. Default merchandise is cosmetic skins; do not silently make progression purchasable.
Dependencies: catalog/inventory authority, economic/product design and the open variants-for-sale decision.

## Real-money purchases
Replace placeholder money buttons with Unity IAP and Cloud Code receipt validation. Real-money grants must be server-verified and retry-safe.
Dependencies: shop stage, platform store setup, receipt/entitlement and refund policies.

## Position evaluation
Build a headless estimate of who is winning from SimulationState for RL reward/opponents and finer abandonment decisions than Core breaches. Calibrate it before using it to recognize lost-position quits or excuse strikes.
Dependencies: existing balance rig, representative outcomes and explicit evaluation/discipline policy.

## Play on after a player leaves
Decide whether the remaining player continues against idle prediction without rollback or a BotPlayer replacement. This changes what a ranked result represents, so specify settlement/abandonment semantics before implementation.
Dependencies: position-evaluation decision, runner ownership, bot handoff and server result policy.

## Live server-authoritative experiment
Keep live authority parked unless hidden information or live anti-cheat justifies hosting. If reopened, first build a local headless dotnet input-ordering/broadcast host to measure feel and effort before paying for servers.
Dependencies: explicit product decision, transport/session design and operational cost model; reuse current simulation/log/headless infrastructure.

## Peer-blocking abuse analysis
Detect repeated suspicious mutual-hold void patterns from retained logs and server presence rather than assume every broken peer link is malicious. A client faking both presence and hold remains indistinguishable in a single incident.
Dependencies: retained evidence, signature work where relevant, aggregated observations and a false-positive/discipline policy.

## Authoritative log start timestamp
Decide whether ranked headers should use the record's server-issued creation/start time instead of GameManager's client clock. The planned header describes server time, while current logs still contain client time.
Dependencies: connection DTO/header plumbing and a precise distinction between allocation, connection and gameplay start.

## Storage size and compression measurement
Measure real and compressed match-log sizes before changing storage or compression. Most ticks are empty; preservation, upload payload limits and per-match duplicate logs matter more than an assumed ratio.
Dependencies: representative finished matches, retention/replay download design and deployed service limits.

## Release content synchronization
Establish the release check that every shipped gameplay balance is exported, validated and deployed under its content hash. A client-only balance change makes the referee refuse otherwise valid matches.
Dependencies: balance export tooling, build identity and module deployment; applies to each new shipped balance.

## Development audit-fixture cleanup
Inspect and remove remaining forged-allocation reproduction records and fake player state in development without touching legitimate data. The source identifies match IDs forged-test-1 through forged-test-3 and possible someoneElse123/456/789 player records.
Dependencies: authenticated development Cloud Save access, exact record verification and scoped deletion authorization.

## Era synchronization and matchmaking cap
Decide whether cross-arena matches clamp variants to the lower arena. Default remains no synchronization, same-arena preference followed by a hard ±1 arena cap; widening to ±2 is contingent on removing the power gap.
Dependencies: side-variant redesign, fairness goals and queue telemetry; open matchmaking question.

## Demotion ownership and availability policy
Confirm the open policy default: keep earned variants after demotion but permit only currently eligible equipment. Reconcile that default with side-variants whose era gates unlock rather than ongoing use.
Dependencies: variant redesign, inventory migration and catalog/equip/referee agreement.

## Smurf arena selection
Decide whether the cap uses rank.arena alone or max(rank arena, arena implied by hidden MMR). Default uses rank.arena; a change affects both queue rules and allocator hard checks.
Dependencies: matchmaking telemetry, fairness policy and a defined MMR-to-arena mapping.

## Minimum RR loss
Decide whether every ranked loss must still cost at least MinGain. The current default keeps that floor; evaluate it against the intended relationship between hidden skill and visible rank.
Dependencies: rank calibration, progression goals and ranked outcome data.

## Arena-boundary demotion protection
Decide whether reaching an arena should protect against immediate demotion. The current default has no protection; adding it changes rank transitions and equipped availability.
Dependencies: progression policy, RR calibration and inventory eligibility behavior.

## Placement matches
Decide whether explicit placement matches improve new-player matching. The current default relies only on high starting rating deviation.
Dependencies: new-player queue/outcome data and rating-period policy.

## Arena identity and rating calibration
Choose arena count, names, era themes and per-arena rewards; defaults are six placeholder arenas. Tune RR gains/divergence, thresholds and Glicko parameters with evidence rather than treating placeholders as final balance.
Dependencies: game progression design, balance rig/telemetry, RankTable/server configuration synchronization and variant redesign.

## Inactivity rating period
Confirm the open default of one week per complete inactivity period before rating-deviation decay. A different period changes return-player uncertainty, not simulation timing.
Dependencies: ranked activity data and Glicko calibration.

## Replay retention window decision
Confirm the open default of each player's last twenty replays before implementing lifecycle cleanup. Decide how dispute evidence interacts with removal from both history windows.
Dependencies: storage measurements, replay product policy and retention implementation design.

## Bot fallback timing decision
Confirm the open default of offering an unranked bot match after ninety seconds without breaking the arena cap. Existing fallback already uses that value; the remaining question is product tuning.
Dependencies: queue wait data and bot-match acceptance/completion data.

## Purchasable variant scope
Resolve whether boxes or the shop can ever grant gameplay variants. Default remains skins only, avoiding payment for gameplay power; reconsider explicitly if side-variant economics change that premise.
Dependencies: variant redesign and shop fairness/product policy.

## RL observation and action design
Define variant-aware RL observations/actions after the board and side-variant model settle. Reuse headless simulation and ranked logs for training/evaluation rather than route training through the account backend.
Dependencies: final board/variant vocabulary, balance rig and position-evaluation work.

## Retire the temporary backend plan
Move these unresolved items into Notion Phases/Tasks through the authorized update workflow and remove BACKEND-PLAN.md in its own commit once current architecture and future work have durable owners. Do not preserve obsolete branch/status history as system-card facts.
Dependencies: completed card review, Notion reconciliation authorization and confirmation that no still-true contract is lost.
