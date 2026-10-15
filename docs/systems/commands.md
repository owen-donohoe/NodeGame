---
system: commands
summary: The shared action vocabulary and validation path from player intent to deterministic state changes.
sources:
  - Assets/Scripts/Game/Simulation/Commands.cs
  - Assets/Scripts/Game/Simulation/CommandProcessor.cs
  - Assets/Scripts/Game/Input/CommandSystem.cs
  - Assets/Scripts/Game/Input/InputBuffer.cs
  - Assets/Scripts/Game/Network/InputSerializer.cs
  - Assets/UI/Scripts/Gameplay/CommandEligibility.cs
verified: a1da5182
---

# Commands

## Purpose
Represent player actions identically for local play, network peers, bots and replay. Validation at application time prevents stale or illegal UI intent from spending resources or changing ownership-dependent state.

## Interface
`GameCommand` is six fields: `CommandType type`, and integers `playerID`, `villagerID`, `targetNodeID`, `issuedOnTick`, `value`. Its wire payload is six 32-bit values, 24 bytes. `CommandTypes.IsKnown(type)` is the shared acceptance list for network and log readers; None is known and has no action.

| Type | Action and payload |
|---|---|
| Move (1) | `villagerID` moves toward `targetNodeID`; preserves intent through interruptions. |
| SetAllocation (2) | Owned Forge `targetNodeID`, `value` allocation amount; positive permits material conversion. |
| Equip (3) | `villagerID`, `value = (int)SuitType`; current owned district must permit the drafted suit. |
| Respawn (4) | Pay for dead `villagerID` to return immediately to its Core. |
| Recruit (5) | Village `targetNodeID`, `villagerID = -1`, `value = 0`. |
| SetAutoRecruit (6) | Village `targetNodeID`, `villagerID = -1`, absolute `value` 0/1. |
| UpgradeFortress (7) | Fortress `targetNodeID`, `villagerID = -1`, `value` 0 materials / 1 metal. |
| ForgeMinion (8) | Workshop `targetNodeID`, `villagerID = -1`, `value = 0`. |
| Collect (9) | Owned bank `targetNodeID`, `villagerID = -1`, `value` 1 start / 0 cancel. |

`InputBuffer.EnqueueCommand(command)` queues intent; `DrainCommands()` returns the ordered batch and clears the queue. Producers stamp `issuedOnTick = state.tickCount`; the driver determines application tick. `CommandProcessor.SetBalance(GameBalanceData)` installs tuning; `ProcessCommand(state, command, TickEventLog log = null)` validates and mutates, returning void. Failed actions leave state unchanged. `GetRespawnCost(state, playerID, additionalPaidRespawns = 0)` exposes escalating discounted food cost.

`CommandSystem.Initialize(state, buffer, selection, playerID)` binds human input. `IssueMoveTo(targetNode)` queues selected-unit moves. `SetGestureRouted`/`SetGestureSource` integrate pointer routing. `OrderDragBegan`, `OrderDragHover`, `OrderDragEnded` expose gesture preview; `MoveIssued(targetNode, IReadOnlyList<int> ids)` acknowledges queued intent, not successful simulation application. It copies IDs before clearing selection.

`InputSerializer.Serialize(TickInput)` / `TryDeserialize(bytes, out TickInput)` carry `forTick`, optional `stateHash`, delay negotiation bytes and `GameCommand[] commands`. `BuildIdentity { ushort protocol, ushort sim, int content }` is compared by `InputSerializer.Compare`; protocol aliases `Backend.ProtocolVersion.Current`.

`CommandEligibility` in gameplay UI provides `Equip`, `EquipVillager`, `EquipSuit`, `HasDrafted`, `RespawnCost`, `Respawn`, `RespawnTarget` and `Allocation`; refusal enums describe displayable reasons. These read state and tuning; the processor remains authoritative. Economic node actions use simulation helpers instead.

## Connects to
- `input`: selection/gestures call `CommandSystem`; producers enqueue through `InputBuffer`.
- `simulation`: processor writes state before `SimulateTick`; move orders become sticky intent.
- `economy`: `NodeActionRules`, `BankRules` and balance arithmetic validate spending and node actions.
- `netcode`: drains batches, transports `TickInput`, orders P0 then P1 and invokes processor.
- `match-flow`: local `TickRunner` follows the same command-before-tick path.
- `match-ui`: reads `CommandEligibility` and shared economic helpers; panels enqueue actions.
- `bots`: emits the same `GameCommand` vocabulary as humans.
- `match-log`: records/replays command batches; layout changes require coordinated log tags.
- `world-view`: `MoveIssued` starts pending-order feedback; applied state replaces that preview.
- `tooling`: serializer/log compatibility and refusal tests guard command evolution.

## Invariants
Input, UI and views never mutate `SimulationState`. Validate ownership, state, tuning and cost before applying; an input preview cannot authorise an action. Node actions use the documented sentinel/value conventions. Enum numbers are persisted; keep wire and log acceptance coordinated. Buffer order and cross-player ordering are gameplay inputs.

## How it works
Human commands, action panels and bots append intent. Local or lockstep drivers drain the buffer into a tick batch. Network deserialisation rejects unknown types; the confirmed command stream is applied before advancing simulation. Recorders observe that same stream.

Equip requires an Idle, non-combat body on its owner's permitted district, a drafted combat suit and available era stats/resources. Combat suits persist until death. Respawn rejects living/consumed units; success increments `paidRespawns`, resets the body and can emit VillagerRespawned through the optional log. Village/Workshop actions append units through the shared spawn path. SetAutoRecruit changes only the repeat flag; Collect may be accepted while locked and waits for eligibility.

Move validation refuses impossible routes without replacing intent. In-flight retargeting preserves the current leg when possible or charges reversal distance; orders during combat change intent without resetting attack clocks. Processor cases do not create a second tick loop.

## Decisions
- Flat integer commands serve transport and replay without Unity types.
- UI refusal helpers explain availability; simulation revalidates because commands can arrive after state changes.
- Absolute toggles survive repeated delivery semantics better than relative flips.

## Pressure points
`value` is overloaded by command type and unused fields have conventions rather than types. Some UI eligibility mirrors processor checks, so drift remains possible outside shared node helpers. Legacy keyboard equip targets Warrior directly. `MoveIssued` can be mistaken for application feedback. New fields affect multiple frozen formats; a command schema would reduce coordination risk without weakening runtime validation.

## Change recipes
- Add a command: allocate its enum value, update `CommandTypes.IsKnown`, add a validating processor case and serializer acceptance in the same commit; update log acceptance and round-trip/unknown-type tests. Wire producers through `InputBuffer`, stamping issue tick; support bots if applicable.
- Add payload fields: coordinate `InputSerializer`, bump `ProtocolVersion.Current`, and add a new match-log TICKS tag rather than rewriting the existing format.
- Add state: initialise it, hash it, copy it, test refusal atomicity and rollback; bump simulation version when identical inputs change results.
- Add node actions to both live UI stacks using shared eligibility/prices, with runtime uGUI construction.

## Tests
Simulation command/economic refusal tests, serializer round trips and unknown-type tests, plus match-log layout/round-trip guards run in `dotnet/NodeWar.sln`. Lossy lockstep/replay fixtures check ordering and replay. These do not prove pointer routing or both UI stacks render the action; verify those in Unity.
