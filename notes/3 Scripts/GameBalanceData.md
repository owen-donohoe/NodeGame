---
shell: 3
up:
  - "[[Worker production]]"
  - "[[District actions]]"
  - "[[Storehouse collection]]"
down: []
concepts:
  - "[[Why floats break determinism]]"
tags: [shell/3, realm/match, system/economy, script]
card: docs/systems/economy.md
file: Assets/Scripts/Game/Simulation/GameBalanceData.cs
sources:
  - Assets/Scripts/Game/Simulation/GameBalanceData.cs
verified: ce9fb6bf
---

# GameBalanceData

> [!question] The question
> How can one deterministic ruleset support different era numbers without branching into six different games?

**Where you are:** [[Home]] › [[Match]] › [[Economy]] › [[Worker production]] › **GameBalanceData** (script)

**Open:** [GameBalanceData.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Simulation/GameBalanceData.cs)

## What it is

A serializable struct of global tuning, `SuitStats[]` and `DistrictStats[]`, plus lookup, validation and arithmetic helpers. `EraCount = 6`. Tuning lives here; ownership, resources and countdown progress live in [[SimulationState]].

## Read it in this order

### 1. `SuitStats` and `DistrictStats`

Stats are keyed by type and era. Some fields are historical rather than active mechanics. The Village bonus comment says “Historical JSON field; inactive on Village.” A field remaining in a data format is not proof the tick uses it.

### 2. The lookup methods near the bottom

Look for an exact era first, then era 0. Missing suit stats return false; missing district stats return a struct with zero effects. The suit comment explains duplicate semantics: “First-match, like every table lookup here, so array order decides a duplicate and the balance hash covers the order.” Reordering entries can therefore change behaviour.

### 3. `Default` and `UniformDistrictStats`

Defaults establish a readable baseline: 10 Hz, caps 30/30/10, maximum population 25, worker capacity 2. `UniformDistrictStats` copies the district template across all six eras. `WithEveryEra` fills missing later suit eras from era 0 without replacing explicit entries.

`Default()` leaves `suitStats = null`. It is not a complete promise that a bot can equip; loaded match tuning must actually supply the suit entry.

### 4. Price and cap helpers

Recruit uses checked widened arithmetic for price and tick readiness and refuses overflow. `PaidRespawnCost` splits percentage multiplication to avoid overflow and clamps the result to at least 1. Resource caps are active only when positive. `AddResource` keeps legacy unchecked increment behaviour for uncapped pools, so “uncapped” is not arbitrary-precision storage.

### 5. Validators

`FortressStatsValid` requires four entries for levels 0–3 and nondecreasing resistance up to 100. `BankTuningValid` needs positive capacity and progress values. Production tempo validation requires durations large enough for a single completion per tick. These protect assumptions made by the consumers rather than repairing unsafe tuning halfway through a match.

## Things to notice

Data-driven variants avoid repeated arena-specific code, but move responsibility into lookup semantics, content validation and hash coverage. Changing a default does not rewrite an already exported JSON file; [[Headless balance matches]] distinguishes source tuning from an optional effective overlay.

## Check yourself

> [!question]- Era 4 has no entry, but era 0 does. What is played?
> The era-0 entry. If neither exists, suit lookup fails and district lookup yields zero-effect stats.

> [!question]- Why not reset invalid production durations to a “reasonable” number during a tick?
> That would invent tuning inside the rules. Validation makes the single-completion assumption explicit before it is relied upon.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
