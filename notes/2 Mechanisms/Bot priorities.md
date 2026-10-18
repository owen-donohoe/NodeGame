---
shell: 2
up: "[[Bots]]"
down:
  - "[[BotPlayer]]"
  - "[[BotSuitChoice]]"
concepts: []
tags: [shell/2, realm/match, system/bots]
card: docs/systems/bots.md
sources:
  - Assets/Scripts/Game/Input/BotPlayer.cs
  - Assets/Scripts/Game/Input/BotSuitChoice.cs
verified: ce9fb6bf
---

# Bot priorities

> [!question] The question
> If defence wants a villager and the economy wants the same villager, who gets to issue the order?

**Where you are:** [[Home]] › [[Match]] › [[Bots]] › **Bot priorities** (mechanism)

## The problem

Independent “helpful” routines can fight each other. Defence sends a worker home; production sends it back; expansion sends it somewhere else. All three saw the same unchanged state, because queued commands have not applied yet.

## The idea

**Evaluate priorities in a fixed sequence, reserve units as orders are queued, and remember movement cooldowns across evaluations.**

## How it works here

| Order | Pass | Main policy |
|---|---|---|
| 0 | Core emergency | Recall living units except existing breachers; override movement cooldowns |
| 1 | Core intercept | Count enemies targeting the Core or within 3 path edges; prefer soldiers |
| 2 | Paid respawn | Budget food locally, including planned respawns in later prices |
| 3 | Node defence | Defend owned Farm, Mine and Barracks; prefer soldiers |
| 4 | Expansion | Seek Village, Farm, Mine, Barracks, second Village, second Farm, second Mine |
| 5 | Military | Equip at Barracks; attack once the ready pack reaches 3 |
| 6 | Economy | Adjust Farm and Mine labour from current resource levels |

`claimedThisTick` is a per-evaluation reservation, not ownership or claim-bar state. It resets at the beginning of `Evaluate`. Movement and equip issuance mark a unit, preventing later passes from queuing another order for it.

`commandCooldownUntil` survives that reset. Ordinary movement waits `defaultLinkWeight * moveSpeedTicks + 1` ticks before another movement command is allowed. The comment says “Set cooldown: 1 tick more than crossing one edge”. That is a policy estimate using the default weight, not the actual whole route's arrival time. Emergency movement ignores this cooldown but still preserves breachers and existing reservations.

Candidate sorting caches path costs per source node and target for the evaluation, then sorts by cost with villager ID as the tiebreak. `PathCost` asks [[Pathfinding]] for a route and sums its link weights; it does not simply count straight-line distance.

The economy thresholds are hardcoded: below 10 resource, desire two workers; from 10, one; from 20, keep one Farm worker but zero Mine workers. These are desired counts, subject to available units. Inbound units are subtracted before sending more.

[[BotSuitChoice]] prefers an affordable drafted Warrior, otherwise the lowest enum-valued eligible combat suit. It consults actual era stats. Several queued equips can see the same unspent pool, so this choice is not a spending reservation; ordinary processor validation still matters.

## Why this, not that

| Choice | Gain | Cost |
|---|---|---|
| Ordered passes with reservations | Easy to inspect who gets each unit | Early priorities can starve later ones |
| Movement cooldown | Reduces oscillating orders | Can delay a useful ordinary redirection |
| Emergency override | Urgent defence can react immediately | Another policy exception to preserve |
| Search all possible plans instead | Could coordinate resource and army decisions | Much more computation and harder explanations |

## What it costs later

New passes must state where they sit and what they reserve. New spending actions need more than per-unit reservations if they must share a budget. The existing respawn pass budgets planned prices, while equips do not reserve their combined costs.

Expansion returns after trying its first missing district type, even if no target exists. More map variety can expose policy stalls without any simulation bug. The bot also has no Recruit, Workshop, Fortress-upgrade or remote Collect strategy.

## Traps

- Reading `claimedThisTick` as a simulation field; it belongs only to the bot.
- Assuming Core emergency pulls breachers off the enemy Core. The code explicitly protects them.
- Treating hardcoded thresholds as tuned balance data.
- Counting queued equips as already equipped for the same evaluation's wolfpack.

## Patterns you've seen before

- 🔁 [[Send intent, not results]]: every decision becomes a normal command.
- 🔁 [[Pure core, Unity shell]]: evaluation reads plain state and needs no frame clock.

## Check yourself

> [!question]- Defence queues a move. Why does the later economy pass not queue a conflicting move for that unit?
> The first issuance marks `claimedThisTick`. Later candidate queries and movement issuance respect that reservation even though simulation state has not changed.

> [!question]- Can two apparently affordable equip choices both succeed when together they cost too much?
> The chooser sees current resources for each unit, so both may be queued. Commands apply in order and are validated there; a later equip may be refused.

## Go deeper ↓

- [[BotPlayer]]: priority sequence, reservations and movement issuance
- [[BotSuitChoice]]: preference versus eligibility

Sideways → [[Pathfinding]] · [[Commands]] · [[Economy]]

## My understanding

<!-- Yours. AI never edits below this line. -->
