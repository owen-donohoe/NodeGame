---
system: match-flow
summary: Match lifecycle, driver selection and the composition of state, presentation, recording and disconnect handling.
sources:
  - Assets/Scripts/Game/Core/EmoteRateLimiter.cs
  - Assets/Scripts/Game/Core/EmoteType.cs
  - Assets/Scripts/Game/Core/FrameRateCap.cs
  - Assets/Scripts/Game/Core/GameManager.cs
  - Assets/Scripts/Game/Core/ICountdownPresenter.cs
  - Assets/Scripts/Game/Core/IEmoteChannel.cs
  - Assets/Scripts/Game/Core/ITickProvider.cs
  - Assets/Scripts/Game/Core/LocalEmoteChannel.cs
  - Assets/Scripts/Game/Core/MatchStartupSequence.cs
  - Assets/Scripts/Game/Core/MatchConnection.cs
  - Assets/Scripts/Game/Core/MatchTransitionController.cs
  - Assets/Scripts/Game/Core/TickRunner.cs
  - Assets/Scripts/Game/Debug/
verified: 1c42739e1decb37891c6d57fe53d26388c42ad39
---

# Match flow

## Purpose
Compose one playable match from setup, deterministic rules and Unity presentation. Own lifecycle and teardown so neither a transition nor a lost connection leaves ticking, recording or input attached to the wrong phase.

## Interface
`GameManager` is the Gameplay composition root; its public `SimulationState state` is observable match state, not permission for external writes. `Awake` reads `MatchConnection.Instance`; `MatchStartupSequence` owns the phase machine PreDraft → Drafting → PostDraft → Countdown → Playing. Scene-configured UI toggles choose presenters/HUD stacks.

`MatchConnection` persists `networkManager`, `localPlayerID`, `isNetworked`, `isBotMatch`, `isSpectator`, `LoadoutData loadout`, ranked flag, server `matchId` and ordered `playerIds` across scene loading. `Shutdown()` releases the connection when returning to lobby. Server player slot 0 corresponds to simulation seat 0.

`ITickProvider` exposes `float TickAlpha` and `event Action<TickEventLog> TickSimulated`. Each event represents one tick, including catch-up ticks; the log is reused, so subscribers copy retained entries. Both local and network drivers implement this presentation boundary.

`TickRunner.Initialize(state, InputBuffer)` sets up local ticking, initially paused. `SetBot(BotPlayer)` attaches evaluation before each tick; `Unpause()` starts advancement. Its own Unity `Update` accumulates frame time, drains ordered commands, applies them and calls `SimulateTick`. `CommandsApplied(int preTick, GameCommand[] batch)` observes nonempty batches; `HashComputed(int postTick, int hash)` exposes checkpoints for recording. `TickAlpha` interpolates between ticks. It stops advancing finished matches.

`MatchTransitionController.SetCountdownPresenter(ICountdownPresenter)` binds countdown visuals. `PlayPostDraftTransition(localPlayerID, placeholders, nodePresentations, state, BoardConfig)` handles reveal/handoff; `PlayNodeStartupWave` and `PlayNodeBreakdownWave` animate board presentation. `OnRequestPlayerSideSwitch(int)` and `OnStartupTransitionComplete` return control to the manager. `ICountdownPresenter.PlayCountdown(Action onComplete)` completes the phase without owning rules.

`FrameRateCap.Apply(frameCap)` applies presentation frame-rate preferences. `IEmoteChannel.Send(EmoteType)` and `EmoteReceived(int playerID, EmoteType)` abstract local/network cosmetic messaging; `LocalEmoteChannel` supplies the local path and `EmoteRateLimiter` limits delivery.

`EmoteType` is the shared cosmetic vocabulary; transport implementation belongs to `netcode`, draft presenter contract to `draft`, and screen implementations to `match-ui`.

## Connects to
- `board`: `MatchFactory.Configure` and `Fill` provide the shared starting state from resolved setup.
- `draft`: starts `DraftManager`, supplies presenter and consumes `OnDraftComplete`/disconnect.
- `simulation`: owns live state and observes game-over; only drivers/processor advance it.
- `commands`: creates `InputBuffer` and command producers for the selected driver.
- `netcode`: creates and updates `LockstepCore`, supplies clocks and reacts to holds, rollback and desync.
- `match-log`: connects driver command/hash observers to `MatchRecorder`, then finishes and saves the log.
- `backend-client`: presence, `DisconnectHold`, surrender, result tracking and ranked reporting cross here.
- `input`: constructs selection/gesture routing and applies seat/camera changes.
- `world-view`: builds views and shared settings, binds tick observers and disposes directors.
- `match-ui`: selects draft/countdown/HUD presenters and displays hold/end state.
- `bots`: local runner evaluates the configured `BotPlayer`.
- `lobby-ui`: starts Gameplay through persistent connection and scene transition; receives return flow.

## Invariants
Drivers stay paused until startup/countdown completes, including skip-draft testing. Cache both loadouts and the actual local seat before destroying draft objects. Network/local composition shares the same factory and state instance. Cleanup subscriptions and network ownership once; presentation cannot independently finish ranked outcomes.

## How it works
Lobby has no match state; Gameplay builds a match; GFX Testing is separate visual iteration. `PlayerProfile` persists identity/settings. `MatchConnection` survives the lobby/gameplay boundary. Temporary `SceneTransition` survives asynchronous scene load to cover/reveal it, then destroys itself; loading coverage blocks input without pausing networking.

The draft path records placements, resolves per-seat loadouts through `LoadoutMatchSetup`, builds nodes/units, replaces draft placeholders and runs transitions/countdown. Testing uses `DraftPlanner.TestingPlacements` and the same factory. Local play installs `TickRunner`; network play is driven from `GameManager.Update` through `LockstepCore.Update` with scaled and unscaled clocks.

The manager binds recorder `RecordTick`/`RecordHash` to the chosen driver, records desync with post-tick count, and finishes/saves bytes through `LocalMatchLogStore`. Normal results and disconnect endings converge on end-card/recording cleanup; ranked reporting/result tracking can outlive simulation completion.

Lockstep hold signals drive `DisconnectHold` and the UI Toolkit overlay. Ranked hold resolution requires server result; returning from background checks whether the server already decided. The uGUI path has no hold overlay and retains immediate disconnect ending. Rollback removes views/selection beyond restored population and rebinds arrays; subsequent growth creates only new views.

Local Editor/development debug uses `DebugPlayerSwitch`: Tab switches local seat where allowed; F6 schedules sudden death in five seconds through a modified balance copy, F7/F11 expose metal/magic readouts. `PlaytestDebugMath.Allowed` requires local runner, Playing and no game-over/network match. It never mutates state and warns that overridden tuning invalidates normal balance identity/replay assumptions.

## Decisions
- Presenter interfaces leave phase completion with Core while either UI stack owns pixels.
- A shared tick-provider surface keeps views independent of driver type.
- Persistent connection carries launch configuration rather than reconstructing network ownership after loading.

## Pressure points
`GameManager` combines lifecycle, UI composition, recording and backend endings in one large class. Startup decisions and idempotent unpause live in a UnityEngine-free seam; asynchronous presentation wiring and cleanup still need Unity checks. Hold behavior differs by UI stack. Public state is mechanically writable despite the architectural boundary. Termination remains coupled to MonoBehaviour/DOTween.

## Change recipes
- Add a phase: define driver/input gating and completion callback; wire both relevant presenter stacks and every startup path.
- Add a subscriber: bind the chosen provider, handle rollback/growth and dispose during teardown.
- Add an ending: coordinate recorder reason, server authority, result UI and single cleanup path.
- Add debug controls: keep development/local guards and modified tuning outside state writes.

## Tests
Eight startup-sequence tests cover draft transition/fallback, skip-draft with/without wave, paused phases and repeated completion over real simulation ticks. View tests also cover playtest/framing/handover maths; match-log/network/backend suites cover their components through `dotnet/NodeWar.sln`. Compile-check covers orchestration types. Manual Unity checks remain necessary for draft/testing unpause, transitions, both presenter stacks, return/background flow and scene wiring.
