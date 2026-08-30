---
glob: Assets/Scripts/Game/Network/**
---

# Network Rules

- The network layer transports commands between machines only
- It never contains game logic, rules, or state mutation
- LockstepRunner drives the tick loop in networked play but
  never calls SimulateTick directly with invented inputs
- InputSerializer and the GameCommand struct must always be
  updated together -- the serializer depends on exact struct layout
- If a change requires modifying GameCommand, update
  InputSerializer in the same commit
- No gameplay constants or balance values belong here
