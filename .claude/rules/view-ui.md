---
paths:
  - "Assets/Scripts/Game/View/**"
  - "Assets/Scripts/Game/UI/**"
  - "Assets/UI/**"
  - "Assets/Legacy/**"
---

# View / UI Rules

Applies to all three presentation trees, not just the layer named UI:
`Assets/Scripts/Game/{View,UI}/` (uGUI and world-space), `Assets/UI/`
(UI Toolkit), and `Assets/Legacy/` (retired uGUI, still compiled).
See docs/architecture.md, "Where the UI lives".

`CLAUDE.md` has the one-line version. Only here:

- Never call into `GameSimulation` or `CommandProcessor` either. Reading
  state is allowed; reaching past it is not.
- Writing a `GameCommand` to `InputBuffer` is the one correct way for UI to
  affect the game.
- Stamp the issuing tick. `NodeSheetContent.Send` is the worked example:
  `issuedOnTick` from `SimulationState.tickCount`, because lockstep must
  agree on *when* a command happened, not only what it was.
- Floats, Unity APIs, `Time.deltaTime`, DOTween, interpolation: all fine. The
  determinism contract stops at this layer's edge.

## The draft is the one screen with no SimulationState

The draft runs before the simulation is built, so there is no state to
read and no InputBuffer to write to. Everything above still holds; it
just has a different subject.

- The draft screen reads DraftState, which DraftManager hands it. It
  never reaches into DraftManager for it
- It changes the draft in exactly one way: ConfirmLocalPlacement. That
  is the phase's equivalent of a GameCommand, and like one it may be
  silently refused -- the cell can be taken, the turn can pass -- so
  clear local state before calling, never after
- DraftManager asks the presenter one question back
  (TryGetPendingPlacement). Answer it about what is on screen; do not
  answer it with a rule. The rule about what a timeout does is
  DraftManager's
- A presenter implements IDraftPresenter and is chosen by GameManager.
  Neither stack may assume it is the one running
