---
paths:
  - "Assets/Scripts/Game/Simulation/**"
---

# Simulation Rules

The ten-point boundary is in `CLAUDE.md`, which is already loaded. It is not
repeated here: a rule read twice is not obeyed twice, and this file used to be
the second of three copies.

What is only here:

- **Holding a Dictionary is not the violation. Enumerating one is.** The ban is
  on iteration order the two peers may not share. `LockstepRunner` keeps a
  Dictionary for local input bookkeeping; it never enters `SimulationState` or
  the hash, and it is fine.
- **Seeds come from already-replicated state.** `DraftManager.HandleTimeout` is
  the worked example.
- **Nothing may be seeded from wall-clock time**, which is a narrower statement
  than the ban on clock APIs and easier to violate by accident.

Then: `docs/simulation-rules.md` for the full contract, and
`.claude/skills/determinism-guard.md` for the checks `scripts/sim-guard.ps1`
cannot make — sort tiebreakers, tick order, hasher registration,
command/serializer pairing, the view boundary.
