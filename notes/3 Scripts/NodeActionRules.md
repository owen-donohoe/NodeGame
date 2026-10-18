---
shell: 3
up: "[[District actions]]"
down: []
concepts: []
tags: [shell/3, realm/match, system/economy, script]
card: docs/systems/economy.md
file: Assets/Scripts/Game/Simulation/NodeActionRules.cs
sources:
  - Assets/Scripts/Game/Simulation/NodeActionRules.cs
verified: ce9fb6bf
---

# NodeActionRules

> [!question] The question
> Where can a button and the command processor ask the same question without either changing the match?

**Where you are:** [[Home]] › [[Match]] › [[Economy]] › [[District actions]] › **NodeActionRules** (script)

**Open:** [NodeActionRules.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Simulation/NodeActionRules.cs)

## What it is

A static collection of read-only predicates and counts. Its summary gives the boundary: “Read-only eligibility shared by node commands, automatic production and UI.” It does not spend resources or spawn units. [[CommandProcessor]] owns those changes.

## Read it in this order

### 1. Refusal enums, then `ValidateVillage`

Recruit and ForgeMinion return `bool` plus an out refusal. The checks are ordered: invalid identity and wrong district precede ownership, enemies, cooldown, price and population. A node can fail several conditions, but the first failure is the reported reason.

### 2. `IsBody`, `HasLivingEnemyAtNode`, `CountPopulation`

These are three different definitions. `IsBody` excludes Minion. Enemy denial looks for enemy, non-Dead, unconsumed bodies at the node. Population counts every owned unconsumed unit, including dead units and minions. Reusing the enemy filter for population would change recruitment limits.

### 3. `CanRecruit` and `CanForgeMinion`

Both consult [[GameBalanceData]] for checked price/readiness arithmetic. Both distinguish a price above the positive resource cap from a temporarily insufficient pool. Waiting cannot fix a price that can never fit.

### 4. `CanSetAutoRecruit` and `CanUpgradeFortress`

Enabling repeat accepts only values 0 and 1 on an owned Village; it does not require recruitment to be affordable now. Fortress accepts currency 0 for materials or 1 for metal, requires `villagerID == -1`, and indexes the price at `fortressLevel + 1` through level 3. It checks enemy presence but not district health.

## Things to notice

The source says “Minions participate in combat, but never in economic presence or labour.” That comment explains `IsBody`; it does not prohibit minions from satisfying the separate bank collector predicate in [[BankRules]]. Shared definitions help only when callers choose the correct one.

Explicit predicates are easy to inspect, but adding actions means extending this list and the processor together. A universal action framework might reduce repetition later; it would also hide some currently obvious conditions.

## Check yourself

> [!question]- Auto-recruit cannot afford a body. Must setting its flag to 1 be refused?
> No. Setting the owned Village's repeat intent is separate from whether the current purchase can succeed.

> [!question]- Is a dead minion included by `CountPopulation`?
> Yes if it belongs to the player and is not consumed. That function does not test Dead or IsBody.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
