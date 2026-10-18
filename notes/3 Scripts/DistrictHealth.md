---
shell: 3
up:
  - "[[District actions]]"
  - "[[Storehouse collection]]"
down: []
concepts: []
tags: [shell/3, realm/match, system/economy, script]
card: docs/systems/economy.md
file: Assets/Scripts/Game/Simulation/DistrictHealth.cs
sources:
  - Assets/Scripts/Game/Simulation/DistrictHealth.cs
  - Assets/Scripts/Game/Simulation/GameSimulation.cs
  - Assets/Scripts/Game/Simulation/GameBalanceData.cs
verified: ce9fb6bf
---

# DistrictHealth

> [!question] The question
> Does “healthy” mean a district has any health left, or that it is fully repaired?

**Where you are:** [[Home]] › [[Match]] › [[Economy]] › [[District actions]] › **DistrictHealth** (script)

**Open:** [DistrictHealth.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Simulation/DistrictHealth.cs)

## What it is

A tiny static passive-effect gate. Its summary is “Shared passive-effect health gate, using the node's district era.” It does not damage or regenerate districts.

## Read it in this order

### 1. Where the maximum comes from

`Healthy` looks up `healthMax` using `GameSimulation.Balance`, the node's district type and its district era. It does not accept a caller-supplied balance. The `state` parameter is present but unused in this implementation.

### 2. The return condition

```csharp
return max == 0 || node.districtHealth == max;
```

Zero maximum means healthy without a health system. Positive maximum requires exact full health, not merely positive health. At the default maximum of 3000, a Storehouse with 2999 is ineligible for production.

### 3. Follow the lifecycle in `GameSimulation`

Ownership changes reset district health to zero. Enemy claiming pressure drains district health before pushing the claim bar. Healing regenerates owned positive-max districts only at a full owner-side bar. Fortress, Storehouse and Infirmary default to maximum 3000 and regeneration 17 per tick.

Production runs before healing in [[The tick]]. A Storehouse repaired to full in healing gets its production eligibility on a later production pass, not retroactively in the pass already completed.

## Things to notice

One shared full-health predicate keeps passive effects from quietly choosing different definitions of “damaged”. A gradual effectiveness curve would offer more intermediate behaviour but require another formula and consistent use everywhere.

The static balance dependency is a future cost: two simulations with different installed tuning cannot safely run concurrently in the same process just because each holds a separate state object.

## Check yourself

> [!question]- The district has zero health and its tuning has `healthMax = 0`. Is it healthy?
> Yes. Zero maximum means the gate is disabled for that district.

> [!question]- Why does passing another state to `Healthy` not select another balance?
> The implementation reads the static `GameSimulation.Balance`; it does not read tuning from that state parameter.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
