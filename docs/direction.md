---
type: Direction
title: Where the architecture goes next
description: The three changes the next phase of work needs — a feel layer, one input vocabulary across desktop and mobile, and a headless match environment for bot training and balance testing. Written as direction, not as a plan.
tags: [architecture, game-feel, art, rl, mobile, desktop]
generated: { by: claude-opus-5, at: 2026-09-17T00:00:00Z }
status: draft
# No `sources:`. This describes work that has not happened yet, so it has no
# code to go stale against. When a section lands, its content belongs in
# architecture.md and the section here should be deleted.
---

# Where the architecture goes next

Written 2026-09-17, against the stated order of work: **art and feel first,
then gameplay tuning, with an RL bot and a desktop build somewhere in there.**

This is direction, not a plan. It says what shape each thing should take and
why, and deliberately stops short of designing any of it. Every recommendation
here is picked for *reasonably functional* over *correct* — where there is a
cheap 80% answer and an expensive 99% one, the cheap one is written down and
the expensive one is named so you know what you traded away.

## The one thing already right

`Assets/Scripts/Game/Simulation/` is a pure, deterministic, integer-only
library with no Unity in it, and `dotnet/NodeWar.Simulation.csproj` already
compiles *the same source files* into a plain .NET library with no Editor and
no licence.

That single fact is what makes all three things below cheap:

- **Feel** is cheap because presentation can be layered on top of a simulation
  that does not care what it looks like, with no risk to the match.
- **Desktop and mobile** is cheap because the platform difference is entirely a
  presentation difference. There is no "mobile game logic".
- **RL** is cheap — genuinely, unusually cheap — because the hard part of
  building a training environment is normally *getting a fast, deterministic,
  headless simulation*, and that is done.

The corresponding risk is that all three of these phases are presentation work,
and presentation work is exactly what erodes a simulation boundary. The rule
in `CLAUDE.md` holds harder over the next six months than it has so far.

---

# 1. A feel layer

## The problem

There is no place for game feel to live. `CameraController.Shake()` is the
canonical example — it is written, it is good, it is wired to nothing, and the
reason is not that shake is unwanted but that **nothing tells the presentation
layer that something happened.**

Today a view finds out about the world by looking at `SimulationState` and
noticing it is different from last frame. That works for continuous things (a
bar's fill, a villager's position). It is the wrong shape for events — a hit
landing, a node flipping owner, a villager dying, a core taking a breach. Every
feel feature that wants one of those has to independently diff the state and
guess when the moment was, which means the diffing logic gets written once per
effect. That is the same failure the camera audit found, just not written yet.

## The change — landed

`SimulateTick` now produces a `TickEventLog` of what happened, alongside the
state it already produces. What it is and why it is safe now lives in
[architecture](architecture.md#what-a-tick-did).

## What it unlocks, in order of cheapness

Once that list exists, every one of these is "subscribe to one event type":

- **Shake** finally has a caller. Breach lands, core hit, big combat.
- **Hit flash / damage tint** on a node or villager.
- **Sound.** There is no audio in the project at all, and the tick event list
  is the correct and only place to hang it. A sound effect is the single
  highest ratio of felt-quality to effort available to this project right now.
- **Floating numbers**, claim-flip bursts, death puffs.
- **Hitstop** — a very short freeze on a heavy event. Cheap and disproportionately
  effective. Note that this must scale *presentation* time only; the tick loop
  must keep running or lockstep breaks. That is a real trap and worth stating
  once here.

## The one coordinating piece

Add a single `FeelDirector` MonoBehaviour that receives the tick event list and
decides what plays. Not because effects need orchestration, but because
twelve independent subscribers all calling `Shake()` in the same tick produces
mush, and a budget ("at most one shake per tick, strongest wins") is three
lines in one place versus a rule nobody can enforce spread across twelve.

`CameraController.Shake` already implements "strongest wins" internally. That
is the right instinct and should be the director's rule too.

## Art data

Separate from feel, and worth doing first because it is smaller: **one
ScriptableObject per district**, holding that district's sprite, its UI icon,
its accent colour, and later its hit effect and its sound.

Right now a district's art is a stack of 6–18 `SpriteRenderer` layers inside a
prefab, and — per [ui-inventory](ui-inventory.md) — *nothing in the project
loads a sprite by name*. The UI needs a district icon and has no way to ask for
one. The board needs the prefab. A `DistrictVisual` asset is the single place
both can ask, and it is also the thing you hand to an artist as a checklist.

Do this before commissioning or drawing anything. It converts "we need art" into
a list of named, empty slots.

## What to deliberately not build

- **A general-purpose event bus with subscriptions, priorities and filters.** A
  list you iterate once per frame is enough and will stay enough.
- **A tweening/sequencing framework.** DOTween is already in the project.
- **Making feel data-driven.** Author effects in code and prefabs first. Pull
  the numbers into assets only once you are tuning them more than writing them.

---

# 2. Desktop and mobile

## You are further along than you think

The instinct is that supporting two platforms means a platform abstraction
layer. It does not, and building one here would be the single biggest waste of
effort available.

What is already correct:

- `PointerGestureSource` already resolves mouse and touch through **one** code
  path, because `Touchscreen` derives from `Pointer`. The Editor exercises the
  same code the phone runs.
- `GestureThresholds` is authored **in millimetres**, not pixels — so a tap slop
  means the same thing to a finger on a phone and a mouse on a 27" monitor.
- `SafeAreaBinder` handles notches, and the UI is UI Toolkit, which does flex
  layout natively.

That is the hard 80%. What remains is smaller than it looks.

## The change: one input vocabulary, two layouts

**Define the game's input as a fixed vocabulary of intents, and map every device
onto it. No feature ever asks what platform it is on.**

```
tap           → mouse left click     · finger tap
drag          → mouse left drag      · one finger drag
long-press    → mouse left hold      · finger hold
pinch/zoom    → scroll wheel         · two-finger pinch
```

This is mostly already true. The exception is the camera, which reads
`Mouse.current` directly for middle-drag and scroll, creating a second pan
implementation that behaves differently — that is [issue #41](https://github.com/owen-donohoe/NodeGame/issues/41),
finding B1. **Fixing that is the whole of the desktop input story.** Move
middle-drag and scroll into `PointerGestureSource` as `OnPan*` and `OnZoom*`,
and the camera stops knowing what a mouse is.

Keyboard is additive and optional: shortcuts are a desktop nicety, not a second
input model.

## Layout: ask about the screen, never about the platform

Two layout modes, chosen by **aspect ratio and width in millimetres**, not by
`Application.platform`:

- `layout--compact` — tall and narrow. Phone portrait. Controls at the bottom
  within thumb reach, sheets slide up from the bottom edge, one column.
- `layout--wide` — landscape. Desktop, tablet, phone in landscape. Controls can
  use the edges, sheets can be side panels, more than one column.

Implement it as a **single USS class on the root element**, set once at startup
and on resize. Not two UXML trees — one tree, styled two ways. UI Toolkit is
good at this and it is the reason the UI was rebuilt in it.

The test that this is right: a resized desktop window and a phone in landscape
must be indistinguishable to the code. If a desktop build ever needs a
`#if UNITY_STANDALONE`, something has gone wrong in the layer below.

## The actual work, honestly listed

1. Fix the camera's direct device reads (issue #41, B1).
2. Add the two layout classes and a small script that picks one.
3. Go through the UI once at desktop aspect and fix what looks stretched.
4. Add a Standalone build target. Unity does this; it is a dropdown.
5. Mouse hover states — genuinely new work, because touch has no hover. Keep it
   to cursor changes and highlight-on-hover. Do not build a tooltip system.

Budget a week of evenings, not a phase. The reason it is this cheap is the
millimetre thresholds and the shared pointer path, which were the expensive
decisions and are already made.

## What to deliberately not build

- A platform abstraction layer, an `IInputProvider` interface, or a settings
  screen for control schemes.
- Controller support. Later, if ever.
- A separate desktop UI. It is the same UI at a different aspect ratio.

---

# 3. An RL environment

You have not done RL before. This section is written accordingly: it explains
the shape, names the standard tools so you are not inventing any of them, and
is honest about where it will go wrong.

## Set expectations first

Three things worth knowing before starting, because all three are surprising:

1. **RL is much harder than MNIST, and failing is the normal state.** MNIST
   going badly is not a signal about your ability. A supervised model that
   fails tells you it failed. An RL agent that fails looks exactly like an RL
   agent that is still learning, for hours. Most of the skill is building
   things that let you tell the difference.
2. **You will not write a learning algorithm.** You will use PPO from a library.
   Writing your own is a study exercise, not a path to a bot.
3. **Almost all the work is the environment and the reward, not the AI.** Which
   is good news, because the environment is the part you already have.

## Why not ML-Agents

The obvious answer is Unity ML-Agents, and for this project it is the wrong
one. ML-Agents runs training through a live Unity player, at something near
frame rate, over a socket. Your simulation is a 10Hz integer tick loop that
compiles to a plain .NET library — it can run **thousands of matches per
minute** with no Unity at all. Going through ML-Agents would voluntarily give up
two or three orders of magnitude of training speed, which for a beginner is the
difference between an experiment you can iterate on over a coffee and one you
run overnight and learn nothing from.

Use ML-Agents if the agent ever needs to see the rendered game. It does not.

## The shape

```
dotnet/NodeWar.Env/          a console app. Holds a SimulationState,
   │                         steps it, prints observations, reads actions.
   │                         Speaks one line of JSON per message over stdio.
   │
   ▼ stdin / stdout
python/nodewar_env.py        a Gymnasium Env subclass that launches that
   │                         process and wraps it. ~150 lines.
   ▼
python/train.py              Stable-Baselines3 PPO. ~20 lines.
```

Three pieces, one of which is twenty lines. The protocol being line-delimited
JSON over stdio is the deliberate 80% choice: it is trivially debuggable (you
can drive the environment by typing at it), needs no networking, and is fast
enough because the Python side is the bottleneck anyway. If it ever is not, the
same design swaps to a socket or shared memory without any other change.

Tools: **Gymnasium** for the environment interface (the standard everything
expects), **Stable-Baselines3** for PPO. Both are well documented and both are
designed for exactly this.

## What has to change in the repo

Very little, which is the point.

1. **Done: `MatchFactory`.** `Simulation/MatchFactory.cs` builds a match's
   tick-0 state from a `BoardConfigData`, draft placements and per-player
   setup, and sets the statics the simulation reads. It is a move of
   `GameManager`'s setup, which now calls it, so a headless match starts
   exactly as a live one does. That is `reset()`. (`TestBoardFactory` stays in
   the test assembly for the small hand-built boards the unit tests use.)
2. **Done: a headless runner.** `MatchReplay` (`Assets/Scripts/MatchLog/`)
   drives `MatchFactory` and `SimulateTick` from a recorded match log, and the
   Cloud Code referee runs it; it is the shape `step()` takes, minus the policy.
   Every drafted match also leaves a `.nwml` log, which is imitation and
   evaluation data.
3. **Skip or randomise the draft.** Matches currently begin with a placement
   draft. For training, generate a random legal placement. Training the draft
   itself is a separate and much later problem.
4. **Eras are part of the observation.** Each player fields an era per suit
   and district type (`PlayerData.suitEras` / `districtEras`), and each
   district plays its placer's era. A policy that ignores them sees a board
   that behaves inconsistently once eras differ.

One constraint the environment inherits: the simulation reads balance and
path costs from statics, so **one process runs one match at a time**. Run
parallel environments as separate processes.

`BoardConfigData` and `GameBalanceData` are already inside `Simulation/`, so
configuration crosses the boundary cleanly already. That was lucky and it saves
a lot.

## The two design decisions that actually matter

Everything above is mechanical. These two are not, and getting them roughly
right matters more than any hyperparameter.

### Action space — decide less often, and in two parts

`GameCommand` is `{type, playerID, villagerID, targetNodeID, value}`. Naively
flattening that is an enormous action space and will not train.

Two cuts make it tractable, and both are cheap:

- **Decide every 10 ticks, not every tick.** One second of game time per
  decision. A villager crossing the board takes many seconds; no strategy in
  this game needs 10Hz control. This alone divides the problem by ten and costs
  nothing a human player would notice.
- **Factorise the action into two heads**: *which villager* and *which node*.
  A 28-node board with ~25 villagers is 28 + 25 outputs instead of 28 × 25.
  This is standard practice and Stable-Baselines3 supports it via `MultiDiscrete`.

Start with **movement only**. Ignore `SetAllocation`, `Equip` and `Respawn`
until a bot can move villagers sensibly. Adding an action type later is easy;
debugging four at once is not.

### Reward — the sparse-reward trap

The win condition is breaching the enemy core. A random agent will *never* do
this, so a reward of "+1 for winning" gives a learning signal of exactly zero,
forever. This is the single most common way a first RL project dies, and it
looks identical to a bug.

Give it a gradient to climb:

```
+ small   per tick, per node owned          (territory is good)
+ medium  on claiming a node                (progress is good)
+ large   on damaging the enemy core        (the actual goal)
+ huge    on winning
- mirror  of each of the above for the opponent doing it to you
```

The per-tick territory term is the important one: it is dense, so there is
always a signal. Tune the ratios later; getting them wrong produces a bot with
a bad strategy, which is a *much* better problem than a bot that never learns.

## Opponents, in order

1. **Against `BotPlayer`.** There are already 748 lines of scripted AI. A fixed,
   competent opponent is a far gentler curriculum than self-play, and it gives
   you an unambiguous scoreboard: what fraction of matches does the agent win?
2. **Against past versions of itself**, once it beats the bot.
3. **Full self-play**, only if needed.

Do not start at 3. It is the impressive one and it is the one where you cannot
tell progress from noise.

## The order to actually do it in

Each step is checkable on its own, which is the property that makes RL survivable.

1. Build the environment. **Drive it with a random agent.** Confirm episodes
   start, terminate, and that the same seed gives the same match. No learning
   yet.
2. Wire up Gymnasium and PPO and let it run. Expect nothing. You are testing
   that the plumbing works.
3. Add the dense reward. Now watch for the agent beating random play. This is
   the first real milestone and the one that tells you the setup is sound.
4. Put `BotPlayer` in as the opponent and track win rate.
5. Only now touch hyperparameters, network size, or self-play.

## The side benefit, which may be the main benefit

A headless environment that runs thousands of matches a minute is a **balance
testing rig**, and you will have it long before you have a working bot.

Run `BotPlayer` against itself ten thousand times overnight and you can answer
questions that are otherwise unanswerable: Is there a dominant opening? Does
player 1 have an advantage? Do matches end, or stalemate? Is any district never
worth taking? How long is a typical match?

That directly serves the stated third phase — *tweaking gameplay so it is fun* —
and it argues for building the environment **earlier than the training**. The
environment is useful on its own. The bot is a bonus on top of it.

---

# Suggested order

Interleaved rather than sequential, because the cheap items unblock the
expensive ones.

| When | What | Why then |
|---|---|---|
| First | `DistrictVisual` assets | Turns "we need art" into a named list. Small. |
| Done | Tick event list | Landed: `TickEventLog`, see architecture.md. |
| Then | Sound, shake, hit flash, hitstop | The actual feel phase. Shake gets its caller. |
| Alongside | Camera input fix (issue #41 B1) | Small, and it is the whole desktop input story. |
| Alongside | Two layout classes, desktop build | A week of evenings, not a phase. |
| Then | Environment over `MatchFactory` (factory and replay runner landed) | Useful immediately as a balance rig. |
| Then | Balance passes using it | The "make it fun" phase, with data. |
| Last | Actually train a bot | The environment has to be boring and trustworthy first. |

## The one rule that holds through all of it

Every item above is presentation, tooling or configuration. **None of it is a
reason to put a float, a `Time.deltaTime`, or a `UnityEngine` reference inside
`Simulation/`.** The tick event list is the one new thing crossing that
boundary and it crosses outward only.

If that holds, the RL environment keeps working, lockstep keeps working, and
the determinism CI keeps meaning something. If it stops holding, all three
break at once and the failure shows up as a desync weeks later.
