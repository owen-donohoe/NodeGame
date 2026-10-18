---
shell: 3
up:
  - "[[Match startup sequence]]"
  - "[[Match flow]]"
tags: [shell/3, realm/match, system/match-flow, script]
file: Assets/Scripts/Game/Core/GameManager.cs
sources:
  - Assets/Scripts/Game/Core/GameManager.cs
verified: 20bb7952
---

# GameManager

> [!question] The question
> The biggest MonoBehaviour in the game (~1,750 lines) assembles every match. How do you find your way around it?

**Where you are:** [[Home]] › [[Match]] › [[Match flow]] › **GameManager** (script)

**Open:** [GameManager.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Core/GameManager.cs)

> [!info] Startup decisions
> [[MatchStartupSequence]] owns phases and idempotent unpause. `GameManager` supplies the callback and keeps Unity/DOTween wiring.

## What it is

The Gameplay scene's composition root. Its public `state` is "observable match state, not permission for external writes". Navigate by its `// ===== … =====` banners: INITIALIZATION, DRAFT PHASE, POST-DRAFT INITIALIZATION, TESTING MODE, UPDATE, then views, input, UI, recording, endings.

## Read it in this order

1. **`MatchStartupSequence.MatchPhase`**: the spine, exposed through the manager's `matchPhase` property.
2. **`Awake`:** `Application.runInBackground = true`, a new `SimulationState`, `MatchFactory.Configure`, terrain view, **a balance-identity check** ("Playing anything else would pass the handshake and desync mid-match"), `InputBuffer`, camera, then draft or skip-draft.
3. **`StartDraftPhase`:** the presenter attached *before* `Initialize` (the comment explains why), and presence heartbeats starting for ranked matches.
4. **`CreateDraftPresenter`:** exactly one draft surface ([[Three UI stacks]]).
5. **`OnDraftComplete` → `InitializeFromDraftResult`:** cache loadouts → `MatchFactory.Fill` → driver choice (`StartNetworkPlay` / `StartLocalPlay` + bot) → `BeginRecording` → spawn views → UI → transition.
6. **`OnTransitionComplete`:** delegates to `startup.CompleteTransition()`. **`SkipDraftAndInitialize`** passes initialization and its optional animation-only wave to the sequence, then unpauses explicitly without waiting for an event ([[Match startup sequence]]).
7. **`Update` / `LateUpdate`:** drives `lockstep.Update(Time.time, Time.realtimeSinceStartup, Time.deltaTime, Time.unscaledDeltaTime)` *before* the phase gate (so packets pump during transitions), ticks result/hold/presence trackers, spawns views for new villagers, detects game over. `LateUpdate` flushes the network.

## Lesson

A **composition root** is fine, even good: one place knows the wiring. The problem is *size*. Lifecycle, UI selection, recording and backend endings sharing one class is what the match-flow card flags, and extracting pure pieces (like the startup seam) is the way to shrink it.

## Check yourself

> [!question]- Why must `lockstep.Update` run before the `if (matchPhase != Playing) return;` line?
> During draft transitions and on the end card the link must stay alive (packets, heartbeats, resends, emotes). Gating it behind Playing would let the peer think you'd disconnected.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
