---
shell: 3
up: "[[District actions]]"
down: []
concepts: []
tags: [shell/3, realm/match, system/economy, script]
card: docs/systems/economy.md
file: Assets/Scripts/Game/Simulation/PierGate.cs
sources:
  - Assets/Scripts/Game/Simulation/PierGate.cs
verified: ce9fb6bf
---

# PierGate

> [!question] The question
> Why can a villager at an enemy Pier retreat but not continue through it?

**Where you are:** [[Home]] › [[Match]] › [[Economy]] › [[District actions]] › **PierGate** (script)

**Open:** [PierGate.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Simulation/PierGate.cs)

## What it is

A static position rule and a deterministic claimer-slot helper. Its summary says: “A Pier gate is a position rule, shared by every departure path.” It makes no writes to the node or villager.

## Read it in this order

### 1. `IsEnemyPier`

Read the villager's current node, require district type Pier and owner `1 - v.ownerID`. The decision is ownership-based. This helper does not inspect health or the claim bar.

### 2. `BlocksDeparture`

Departure is blocked when all three are true: the unit is not already Moving, it is on an enemy Pier, and the proposed next node is not `previousNodeID`.

That last test leaves retreat open. A rule that simply prohibited every departure would trap the unit instead. Existing movement is also explicitly excluded by the state check; this is a departure decision, not a mid-leg cancellation.

### 3. `HasClaimerSlot`

Count eligible friendly bodies at the same node with lower `villagerID`. Exclude consumed units, nonpositive HP and minions. Eligible states are Idle, Claiming, Working and Fighting. A candidate has room if the number ahead is below the supplied cap.

There is no sort: counting lower IDs gives a stable priority independent of the sequence in which callers ask for slots. The cap is a parameter, not a hidden fixed constant in this helper.

## Things to notice

Shared position rules help commands and automatic departures agree. Placing a gate check only in a UI route preview would draw the right warning while leaving the simulation able to take another departure path.

The methods assume valid current-node references; they are small internal rule helpers, not general parsers for malformed commands. Validation belongs at the command boundary.

## Check yourself

> [!question]- An enemy Pier is still owned by the enemy but has lost health. Does this helper stop gating it?
> No. IsEnemyPier tests type and owner, not health. Ownership must cease to be enemy ownership for that predicate to become false.

> [!question]- With cap 2, do two eligible friendly lower-ID bodies leave this candidate a slot?
> No. `ahead < cap` is false when both are 2. Lower IDs have priority.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
