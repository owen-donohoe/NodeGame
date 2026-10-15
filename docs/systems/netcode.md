---
system: netcode
summary: Exchanges deterministic tick inputs, agrees match setup, and keeps peer play recoverable across packet loss.
sources:
  - Assets/Scripts/Game/Network/
  - Assets/Scripts/Game/Core/MatchConnection.cs
  - Assets/Scripts/Game/Core/IEmoteChannel.cs
  - Assets/Scripts/Game/Core/LocalEmoteChannel.cs
  - Assets/Scripts/Game/Core/EmoteRateLimiter.cs
  - Assets/Scripts/Backend/Shared/ProtocolVersion.cs
  - dotnet/NodeWar.Network.Tests/
verified: a1da5182
---

# Netcode

## Purpose
Peers exchange commands rather than simulation state. Lockstep preserves identical confirmed results while adaptive delay, redundant delivery and bounded speculation hide ordinary network disruption.

## Interface
`LockstepCore` is UnityEngine-free and implements `ITickProvider.TickAlpha` and `IEmoteChannel`. `Initialize(SimulationState, InputBuffer, ILockstepTransport, playerID, ticksPerSecond, time, realTime)` binds a match; the Unity owner supplies clocks through `Update(time, realTime, deltaTime, unscaledDeltaTime)`, then `Unpause`, `Flush` and `EndMatch` as appropriate. `ILockstepTransport` supplies `Send`, `ReceiveAll`, `Flush`.

Observers receive `LocalInputDue`, `TickSimulated(TickEventLog)`, `ConfirmedTickSimulated(int)`, `CommandsApplied(int, GameCommand[])`, `HashComputed(int, int)`, `OnDesync(int)`, `HoldStarted`, `HoldEnded`, `SpeculationChanged(bool)` and `RolledBack(int villagerCountBefore)`. Replay of a speculative span emits confirmed/recording events without emitting visual tick cues again.

`NetworkManagerTransport` adapts Unity's `NetworkManager`: `StartAsHost/StartAsClient` use DirectUDP; `StartAsRelayHost/StartAsRelayClient` use Unity Relay with DTLS. `Send`, `ReceiveAll`, `Flush`, `Shutdown`, connection flags and `JoinCode` serve launch/draft consumers.

`InputSerializer` encodes little-endian packets. `PacketType` values are Handshake=0, HandshakeAck=1, TickInput=2, Heartbeat=3, DraftReady=4, DraftPlacement=5, DraftLoadout=6, DraftAck=7, Emote=8, HandshakeReject=9, MatchSetup=10, MatchSetupAck=11. `TickInput` contains `forTick`, `stateHash`, `senderDelay`, `requestedDelay`, `GameCommand[] commands`; each command occupies 24 bytes. `DraftSerializer` owns setup and draft payloads; `DraftAck` acknowledges ready/loadout reception and applied placement count.

`BuildIdentity` is `{ ushort protocol, ushort sim, int content }`. `LocalBuildIdentity.Current` supplies it; `SetupFor(mapId)` supplies `MatchSetup` with map ID, board hash, simulation version and balance hash. `ProtocolVersion.Current` is 6, aliased by `InputSerializer`; simulation identity comes from `SimulationVersion.Current`, content from shared balance hashing. `SetupAgreement.Host/Guest`, `NextToSend`, `Receive`, `Agreed`, `Refused` and `AcceptsDraftPackets` gate draft delivery against the shipped board catalog.

Persistent `Core/MatchConnection` carries `networkManager`, local slot, network/bot/spectator flags, loadout, ranked flag, server match ID and ordered player IDs across scenes. `Shutdown` clears the connection.

`IEmoteChannel.Send(EmoteType)` and `EmoteReceived(int, EmoteType)` work through the core or `LocalEmoteChannel`. `EmoteRateLimiter.TryAccept(now)` and `NextAllowedTime(now)` support HUD send/receive limits.

## Connects to
- `commands`: `InputBuffer` supplies local commands; sender-stamped commands enter `CommandProcessor` in slot order.
- `simulation`: `SimulateTick`, state hashes and `SimulationState.CopyFrom` implement confirmed play and rollback.
- `board`: `SetupAgreement` validates `MatchSetup` against `IBoardCatalog`.
- `draft`: acknowledged draft delivery starts only after setup agreement.
- `match-flow`: owns clocks, `MatchConnection`, core lifetime and hold events.
- `match-log`: confirmed `CommandsApplied` and `HashComputed` feed `MatchRecorder` through the match owner.
- `backend-client`: `DisconnectHold` and `PresenceHeartbeat` resolve ranked silence through server authority.
- `world-view`: tick interpolation, tick cues and rollback cleanup follow core events.
- `match-ui`: emotes, unstable-link banners and hold controls observe channels and status.
- `cloud`: ticket/record checks enforce the protocol, simulation and content identity used by peers.
- `lobby-ui`: `MatchLauncher` connects Relay/LAN peers and carries ranked roster order across scenes.
- `tooling`: lossy-link reference scenarios verify confirmed results, rollback and adaptive delay.

## Invariants
Apply all P0 commands before P1 commands; stamp command ownership from sender slot, never wire claims. Limit each player to 64 commands per tick and reject implausible tick numbers. DirectUDP reads only the connected endpoint. Identity mismatch refuses connection; setup disagreement refuses draft. Never record, hash or settle speculative state.

## How it works
Start at two ticks of input delay with empty preseeded inputs. Every new input sends itself and six preceding inputs; resend the entire potentially unacknowledged span independently of input generation. Holds resend every 250 ms and keep heartbeats alive. Catch-up runs at 1.5x when sufficiently behind.

`InputDelayController` asks the peer for an absolute delay between two and six ticks: three late ticks in thirty raise it; sustained slack lowers it. Header requests are ordered, bounded and rate-limited; raising delay generates extra due inputs immediately. Disturbed periods do not train adaptation.

After 300 ms of missing input, copy the confirmed state and predict empty remote input for at most twenty ticks. Show the warning after five speculative ticks. When the entire span arrives, always restore and replay it, retaining local orders. Exhaustion or speculative game over restores and holds; no progress for two seconds also holds. Hash comparison occurs every fifty confirmed ticks.

`GameManager` maps hold events to `DisconnectHold`: wait ten seconds, permit action until sixty, then request resolution. Ranked play waits for a server answer; prolonged server failure offers leaving after ninety seconds. Emotes bypass ticks: five-byte packets send twice and deduplicate by sequence. HUD limits accept fewer than five per second and ten per five seconds, with mute; pumping continues after game over.

## Decisions
- Keep P2P plus post-match referee rather than pay for continuously hosted simulation.
- Refuse incompatible live identities rather than translate commands between simulations.
- Copy once per disruption and always replay rather than snapshot every tick or replay from tick zero.
- Keep emotes cosmetic rather than introduce simulation commands.

## Pressure points
Unsigned inputs cannot prove authenticity. Checkpoint packets omit the checkpoint tick. Speculative cues can play before correction. uGUI lacks the hold overlay; draft disconnect handling remains separate. Two-player testing still determines whether rewind and adaptive-delay tuning feel right.

## Change recipes
- Change wire/setup semantics: update serializers, shared protocol constant, handshake/lobby identity and compatibility tests together.
- Change rollback: preserve confirmed-only recording, local future inputs and view cleanup; update copy coverage and lossy-link reference tests.

## Tests
`NodeWar.Network.Tests` compares confirmed hashes with perfect-link reference play under loss, jitter, outages and rollback; explicit `SweepTests` tune constants. Lobby serializer/setup tests and simulation copy tests cover contracts. Use compile-check for Unity adapters; Relay, device backgrounding and scene wiring require manual play.
