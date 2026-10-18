---
shell: 3
up: "[[Headless balance matches]]"
down: []
concepts:
  - "[[Events and observers]]"
tags: [shell/3, realm/match, system/bots, script]
card: docs/systems/bots.md
file: dotnet/NodeWar.BalanceRig/MatchRunner.cs
sources:
  - dotnet/NodeWar.BalanceRig/MatchRunner.cs
  - dotnet/NodeWar.BalanceRig/Program.cs
  - dotnet/NodeWar.BalanceRig/RigSetup.cs
verified: ce9fb6bf
---

# MatchRunner

> [!question] The question
> Where does a prepared seed become a real headless match, and which ordering makes its result repeatable?

**Where you are:** [[Home]] › [[Match]] › [[Bots]] › [[Headless balance matches]] › **MatchRunner** (script)

**Open:** [MatchRunner.cs in VS Code](vscode://file/C:/Dev/NodeGame/dotnet/NodeWar.BalanceRig/MatchRunner.cs)

## What it is

The rig's static match lifecycle, alongside `PreparedMatch`, `MatchResult` and `RunHooks`. `Program.Main` loads options and setup, then calls this lifecycle for each seed and optional swapped seat. This class drives the simulation; [[BotPlayer]] only supplies commands.

## Read it in this order

### 1. `Prepare` and `RandomDraft`

Prepare builds player setups and either accepts a draft or generates one using `new Random(seed)`. Default player setups grant Warrior, bring the loadout districts and use era 0. Draft generation calls shared placement legality and skips a pick with no legal cell instead of forcing it onto forbidden terrain.

### 2. `SwapSeats`

Mirror board cells, placements and draft through `RigSetup.mirror`, then exchange player setups and draft pools. Keep seed and pair ID; change seat. It prepares the corresponding match from the other side rather than generating unrelated randomness.

### 3. `Run`

Configure the static simulation settings, build through [[MatchFactory]], create two bots and a shared [[InputBuffer]], then loop: evaluate → drain/apply → tick → observe. The comment explains the key contract: “Commands apply P0 then P1 every tick whatever order the bots evaluated in.”

### 4. `ApplyCommands`

Fresh commands are held until current tick plus input delay. Due commands are grouped by player 0 then player 1 while retaining each player's issuance order. The observer sees that exact batch before application. The next post-tick state is one tick number higher, so command timing and post-state timing must not be confused.

### 5. `Finish` and `TrackFleet`

If simulation did not end, mark capped and leave winner −1. Final living-unit totals include minions; soldier and body diagnostics use their narrower filters. Breach counts describe breaches suffered by each side, so reading them as attacks made would invert the story.

## Things to notice

`RunHooks.afterTick` receives the actual mutable state reference. The read-only observer rule is a contract, not a language-enforced guarantee. Instrumentation must never influence the next tick.

Mirroring improves comparison but does not cancel ID-based advantages. Likewise, separate state instances do not isolate static simulation configuration; run matches sequentially within a process.

## Check yourself

> [!question]- If bot 1 evaluates first in a future driver, should its commands apply first?
> No. ApplyCommands restores P0-then-P1 order while preserving each player's own issuance sequence.

> [!question]- At the cap, P0 has more districts but gameOver is false. What is the winner?
> −1. The cap limits the experiment; it does not add a new victory rule.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
