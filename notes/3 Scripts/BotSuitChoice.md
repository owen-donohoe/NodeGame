---
shell: 3
up: "[[Bot priorities]]"
down: []
concepts: []
tags: [shell/3, realm/match, system/bots, script]
card: docs/systems/bots.md
file: Assets/Scripts/Game/Input/BotSuitChoice.cs
sources:
  - Assets/Scripts/Game/Input/BotSuitChoice.cs
verified: ce9fb6bf
---

# BotSuitChoice

> [!question] The question
> When several drafted combat suits are affordable, which one does the bot ask to equip?

**Where you are:** [[Home]] › [[Match]] › [[Bots]] › [[Bot priorities]] › **BotSuitChoice** (script)

**Open:** [BotSuitChoice.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Input/BotSuitChoice.cs)

## What it is

A static, read-only policy query returning a `SuitType`, or None. The summary says “Chooses an affordable drafted combat suit using the actual match balance.” It chooses a preference; it does not equip, spend or replace authoritative validation.

## Read it in this order

### 1. The early guards

Reject missing state arrays, invalid player/node indices and a node the player does not own. No drafted suits means None. Notice the signature has no villager ID: it cannot prove that a particular unit is eligible to equip.

### 2. The candidate filter

Each drafted candidate must be a combat suit, be permitted at this district, have stats for the player's suit era (with normal era fallback), and fit the current food and material pools. These questions delegate to [[GameBalanceData]], keeping policy tied to actual match tuning.

### 3. The preference

An eligible Warrior returns immediately. Otherwise keep the lowest enum-valued eligible suit. That gives a repeatable fallback even when the loadout array order changes. It is a simple preference, not a comparison of damage, speed or tactical counters.

## Things to notice

Affordability is evaluated against current state, with no local reservation. If [[BotPlayer]] asks twice before commands apply, both answers may assume the same unspent resources. Later application still passes through [[CommandProcessor]].

This small policy seam is easier to change than embedding suit rankings throughout military code. A future counter-picking policy could inspect threats here, but would then need evidence that it improves the bot rather than merely producing different rig outcomes.

## Check yourself

> [!question]- A cheap non-Warrior and a costly Warrior both pass the filters. Which wins?
> Warrior. Cost is an affordability gate, not a ranking criterion once both are affordable.

> [!question]- Does Choose returning Warrior guarantee the next Equip command succeeds?
> No. It does not inspect a specific villager and does not reserve resources. The processor validates the eventual command against application-time state.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
