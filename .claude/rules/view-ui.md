---
# Quoted, and no nested braces: the value starts with '{', which YAML would
# otherwise read as a flow mapping. A rules file that fails to parse stops
# enforcing this boundary silently.
glob: "{Assets/Scripts/Game/View/**,Assets/Scripts/Game/UI/**,Assets/UI/**,Assets/Legacy/**}"
---

# View / UI Rules

Applies to all three presentation trees, not just the layer named UI:
`Assets/Scripts/Game/{View,UI}/` (uGUI and world-space), `Assets/UI/`
(UI Toolkit), and `Assets/Legacy/` (retired uGUI, still compiled).
See docs/architecture.md, "Where the UI lives".

- View and UI are read-only consumers of SimulationState
- Never write to SimulationState from View or UI
- Never call methods on GameSimulation or CommandProcessor
  from View or UI
- All state changes go through: GameCommand -> InputBuffer ->
  CommandProcessor -> SimulateTick
- Floats, Unity APIs, Time.deltaTime, DOTween, and
  interpolation are all fine here
- UI may write GameCommands to InputBuffer -- that is the
  correct and only way to affect game state
- Stamp the issuing tick on every command. NodeSheetContent.Send
  is the worked example: it sets issuedOnTick from
  SimulationState.tickCount, because lockstep must agree on when
  a command happened, not only what it was

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
