---
glob: Assets/Scripts/Game/Simulation/**
---

# Simulation Rules

- This is the deterministic simulation layer. Both peers run this
  code identically. Any violation causes a desync.
- No UnityEngine references of any kind
- Integer math only. No float, no double, no decimal
- No System.DateTime, Time.deltaTime, Time.time, or any
  frame/wall-clock API
- No UnityEngine.Random, and nothing seeded from wall-clock time.
  SimulationState holds no RNG today. Derive a seed from already-
  replicated state (see DraftManager.HandleTimeout). If a mid-match
  feature needs randomness, the RNG state must itself live on
  SimulationState and advance only inside SimulateTick.
- Collections: arrays or List<T> only. No Dictionary/HashSet
  *iteration* over anything affecting simulation results. Holding one
  is not the violation; enumerating it in an order the two peers may
  not share is. (LockstepRunner keeps a Dictionary for local input
  bookkeeping; it never enters SimulationState or the hash.)
- All sort operations must use total-order comparators with an
  ID-based tiebreaker. No ties permitted.
- Canonical tick order must not be reordered:
  movement -> combat -> claiming -> production ->
  healing -> respawns -> win-check
- Every new field added to SimulationState must also be added
  to SimulationStateHasher
- New GameCommand types require a corresponding case in
  CommandProcessor
- Before touching this folder: read docs/simulation-rules.md
