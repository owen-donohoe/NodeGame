---
glob: Assets/Scripts/Game/{View,UI}/**
---

# View / UI Rules

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
