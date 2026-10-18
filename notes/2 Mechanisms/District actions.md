---
shell: 2
up: "[[Economy]]"
down:
  - "[[NodeActionRules]]"
  - "[[GameBalanceData]]"
  - "[[DistrictHealth]]"
  - "[[PierGate]]"
  - "[[CommandProcessor]]"
concepts:
  - "[[Idempotency]]"
tags: [shell/2, realm/match, system/economy]
card: docs/systems/economy.md
sources:
  - Assets/Scripts/Game/Simulation/NodeActionRules.cs
  - Assets/Scripts/Game/Simulation/GameBalanceData.cs
  - Assets/Scripts/Game/Simulation/CommandProcessor.cs
  - Assets/Scripts/Game/Simulation/GameSimulation.cs
  - Assets/Scripts/Game/Simulation/DistrictHealth.cs
  - Assets/Scripts/Game/Simulation/PierGate.cs
verified: ce9fb6bf
---

# District actions

> [!question] The question
> Why does recruitment need shared refusal reasons, and why is a Town payout a different kind of promise?

**Where you are:** [[Home]] › [[Match]] › [[Economy]] › **District actions** (mechanism)

## The problem

An affordable action can still be illegal: the node is wrong, an enemy is present, a cooldown remains or population is full. The UI needs to explain that refusal; authoritative code needs to enforce it. Capturing a Town introduces another question: if its bodies cannot fit, is the player owed them later?

## The idea

**Share eligibility and pricing, keep spending in the processor, and store one-time capture entitlement separately from repeatable purchases.**

## How it works here

| Action | Shared decision | Successful write |
|---|---|---|
| Village Recruit | `CanRecruit` plus `TryRecruitCostAndCooldown` | Spend food, append one body, increment player recruit count, set node readiness |
| SetAutoRecruit | Owned Village and value 0 or 1 | Set an absolute repeat flag |
| Workshop ForgeMinion | `CanForgeMinion` plus per-era cooldown | Spend metal, append one minion, set node readiness |
| UpgradeFortress | Owned Fortress, level below 3, valid tuning and chosen currency | Spend next level's price and increment level |
| Town full claim | Player's bit absent in `townPaidMask` | Mark entitlement used, then spawn only what fits |

Recruit's default price is `6 + 3 * recruitCount`. The count is the player's persistent purchase history, not the number of living bodies or purchases at this Village. The source says: “Price is also the cooldown in seconds, using the pre-recruit count.” With default 10 Hz, a first purchase at tick 100 costs 6 and sets readiness to 160. The next purchase costs 9 and waits 90 ticks.

Auto-recruit calls the same `TryRecruit` after production, in ascending node-ID order. Two ready Villages compete for the same food and persistent count in that order. Readiness is an absolute tick, unaffected by production tempo. Ownership changes clear readiness and the repeat flag.

Workshop defaults are 3 metal, 30 cooldown ticks and an 8-HP minion. It shares the node's `recruitReadyTick` field but has no automatic repeat pass. Population counts dead and living unconsumed units, including minions. Enemy minions do not trigger economic denial: `IsBody` excludes them.

Town's default bonus is two bodies per player on their first full claim of that Town. Its comment settles the cap question: “Consume the entitlement before the cap check; no deferred credit.” Recapturing it does not refund missed bodies.

Fortress levels 1–3 cost 4/8/12 materials or 1/2/3 metal in `Default()`. Those are sequential prices, not a direct jump to a chosen level. Passive health eligibility is a separate concern from purchase eligibility: [[DistrictHealth]] gates effects, whereas `CanUpgradeFortress` does not require full health.

## Why this, not that

| Choice | Gain | Cost |
|---|---|---|
| Read-only eligibility with refusal enums | UI and actions share the same answer | Ordering chooses which of several refusals is shown |
| Global escalating recruit price | Purchase history follows the player | Moving to another Village does not make bodies cheap again |
| Automatic retry through `TryRecruit` | No second purchase implementation | Node-ID order matters when food is scarce |
| Spend Town entitlement before capacity | No deferred reward queue | A full population wastes the bonus |
| Queue missed Town bodies instead | Preserve reward value | New persistent state and future payout rules |

## What it costs later

A new district action needs more than a button. It needs shared eligibility, explicit processor writes and a decision about repeat behaviour. State additions must survive copying and hashing. Capture rules must decide what resets and what persists: readiness resets, player recruit history and Town entitlement do not.

## Traps

- Copying the Recruit formula into [[NodeSheetContent]] instead of calling the helper.
- Treating SetAutoRecruit as “toggle”: repeated value 1 must keep it enabled.
- Assuming “Minions participate in combat” means they count as economic enemy presence.
- Mistaking a healthy district for an automatically legal action; price, ownership and capacity are separate checks.

## Patterns you've seen before

- 🔁 [[Presentation reads, never writes]]: showing an action does not spend resources.
- 🔁 [[Make retries safe]]: SetAutoRecruit carries an absolute value.
- 🔁 [[Send intent, not results]]: Recruit asks for a purchase; the processor decides the result.

## Check yourself

> [!question]- Can you reset the next recruit's price by buying at a different Village?
> No. Price reads the player's recruit count. The cooldown belongs to the node, but the price history belongs to the player.

> [!question]- You first claim a Town with room for only one of its two default bodies. What happens on recapture?
> You receive one now. Your payout bit was already consumed, so recapture grants no replacement for the missed body.

## Go deeper ↓

- [[NodeActionRules]]: eligibility and refusal ordering
- [[GameBalanceData]]: checked price/cooldown arithmetic
- [[CommandProcessor]]: spending and spawning
- [[DistrictHealth]] · [[PierGate]]: shared passive and position rules beside the purchases

## My understanding

<!-- Yours. AI never edits below this line. -->
