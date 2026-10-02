---
paths:
  - "Assets/Scripts/Game/Network/**"
---

# Network Rules

- The network layer only transports data between machines: tick
  inputs (commands and checkpoint hashes), handshake and draft
  packets, heartbeats and emotes
- It never contains game logic, rules, or state mutation. The one
  exception is LockstepRunner restoring a confirmed state with
  SimulationState.CopyFrom when a speculative span rolls back (8.2e);
  that restores a state the simulation produced, it decides nothing
- LockstepRunner drives the tick loop in networked play but
  never calls SimulateTick directly with invented inputs
- InputSerializer and the GameCommand struct must always be
  updated together -- the serializer depends on exact struct layout
- If a change requires modifying GameCommand, update
  InputSerializer in the same commit
- No gameplay constants or balance values belong here
- Any packet layout change bumps ProtocolVersion.Current
  (Assets/Scripts/Backend/Shared/ProtocolVersion.cs, which
  InputSerializer.ProtocolVersion aliases and the server allocator checks) in
  the same commit. A GameCommand change also needs a new TICKS tag in
  MatchLogFormat: match logs outlive builds, so a known tag never
  changes meaning
- The runners' CommandsApplied / HashComputed events exist for
  recording only; nothing may act on the simulation through them
