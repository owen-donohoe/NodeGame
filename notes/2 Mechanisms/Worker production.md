---
shell: 2
up: "[[Economy]]"
down:
  - "[[GameBalanceData]]"
  - "[[GameSimulation]]"
  - "[[BankRules]]"
concepts:
  - "[[Fixed timestep]]"
  - "[[Why floats break determinism]]"
tags: [shell/2, realm/match, system/economy]
card: docs/systems/economy.md
sources:
  - Assets/Scripts/Game/Simulation/GameSimulation.cs
  - Assets/Scripts/Game/Simulation/GameBalanceData.cs
  - Assets/Scripts/Game/Simulation/BankRules.cs
  - Assets/Scripts/Game/Simulation/CommandProcessor.cs
verified: ce9fb6bf
---

# Worker production

> [!question] The question
> What does a worker's countdown actually buy, and what happens when the destination resource is full?

**Where you are:** [[Home]] › [[Match]] › [[Economy]] › **Worker production** (mechanism)

## The problem

Owning a Farm is not the same as working it. A unit must occupy the labour role, production must advance in deterministic time, and the resource cap must apply without accidentally consuming materials for metal that cannot fit.

## The idea

**Give each Working villager an integer countdown, pay at completion, and add the duration back so any overshoot carries into the next cycle.**

## How it works here

| District / worker suit | Completion | `Default()` duration, before tempo |
|---|---|---|
| Farm / Farmer | +1 food directly to owner | 30 ticks |
| Mine / Miner | +1 material directly to owner | 40 ticks |
| Forge / Smelter | Spend 1 material for +1 metal if allowed | 50 ticks |

At the default 10 ticks/second, these are 3, 4 and 5 seconds per worker. The default ordinary capacity is two workers per node. Combat suits do not become production workers; arrival assigns a production suit where eligible.

`TickProduction` visits villagers in array order. It ignores non-Working and consumed units, and timers with no positive duration. Each eligible countdown loses the production-tempo decrement, or 1 when that schedule is invalid. On completion it awards at most one unit and adds `productionTicksMax` back.

For example, remaining 1 minus decrement 2 becomes −1. Adding a duration of 30 leaves 29. Resetting it to 30 would discard work already done. The balance validator explains the single-payout constraint: “A single completion per tick requires each duration to cover the largest possible decrement.”

Forge checks allocation, available materials and metal room together. Its source comment is explicit: “A blocked conversion still cycles without consuming materials.” `materialAllocation > 0` is permission to convert; the conversion does not decrement that allocation.

Within [[The tick]], ordinary production precedes Storehouse production, bank collection and auto-recruit. A repeated recruit can therefore spend food produced earlier in that same tick.

## Why this, not that

| Choice | Gain | Cost |
|---|---|---|
| Countdown per worker | Labour and individual arrival time matter | More per-unit state and timer resets |
| Add duration after completion | Keeps tempo overshoot | Must validate durations for one payout per tick |
| Cycle even at a full player pool | Predictable production cadence | Some completions are wasted |
| Pause all workers at the cap instead | No wasted work | Different timing after spending; unlike the current worker rule |
| Reserve materials when allocating instead | Guaranteed future Forge input | Would lock resources away; current allocation is only a gate |

## What it costs later

Production speed is more than changing a duration. Tempo validity, suit assignment, worker capacity and direct cap arithmetic all affect the result. Introducing another conversion should explicitly choose whether failed completions cycle, and whether input is spent only when output fits.

## Traps

- Assuming [[Storehouse collection|banks]] use this full-pool rule. Full banks pause their production timer.
- Resetting a negative countdown to its full duration and losing overshoot.
- Spending a material before checking metal room.
- Using real seconds inside [[GameSimulation]] instead of integer tick progress.

## Patterns you've seen before

- 🔁 [[Same code, three places]]: each peer, replay and rig executes the same payouts.
- 🔁 [[Pure core, Unity shell]]: workers need ticks, not Unity clocks.

## Check yourself

> [!question]- A Smelter finishes with allocation 1, materials 4 and metal at its positive cap. What changes?
> Its timer cycles. It spends no material and gains no metal; the allocation remains 1.

> [!question]- At ordinary speed, two Farm workers each complete a cycle. Does the Farm bank two food?
> No. Each adds one food directly to the player's pool, subject to its cap. Storehouses are the bank producers.

## Go deeper ↓

- [[GameBalanceData]]: durations, caps and tempo validation
- [[GameSimulation]]: `TickProduction` and arrival/worker assignment
- [[BankRules]]: ordinary worker capacity

## My understanding

<!-- Yours. AI never edits below this line. -->
