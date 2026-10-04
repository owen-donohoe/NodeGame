---
type: Direction
title: Tempo phases, sudden death and the breach bar — proposal draft
description: A stateless tempo ramp and sudden death, and a sequential per-core breach bar with diminishing swarm scaling, planned against the tick loop as it stands on 2026-10-04. The board is smaller by map design (about 16 nodes); sudden death does the converging, and no node is ever removed mid-match.
tags: [simulation, balance, tempo, breach, game-design]
generated: { by: claude-sonnet-5-5, at: 2026-10-04T00:00:00Z }
status: draft
# No `sources:`. Describes work that has not happened. Everything here touches
# Simulation/, so implementation starts in plan mode and runs the determinism
# guard; nothing here is a plan to commit as written.
---

# Tempo phases, sudden death and the breach bar — proposal draft

Every item below changes what the same inputs produce. That means one
`SimulationVersion` bump (currently `1`) covering all of it, one deliberate
re-pin of the determinism baselines under that bump, and no change to
`GameCommand` or `InputSerializer`. Doing the three items in one release avoids
re-pinning twice.

## 1. Facts from the code that shape the design

Line references are to `Simulation/GameSimulation.cs` unless stated.

- **Breach is instant.** `ProcessBreach` (:1055) runs on arrival at the enemy core
  with no living defenders (:130–134) and again after a fight ends (:986–991). It
  adds to `breachCount`, then consumes the villager.
- **The win check is biased on ties.** `TickWinCondition` (:947) loops `p = 0..1`
  and returns on the first player at or over the threshold, with winner
  `1 - p`. If both players cross it on the same tick, player 0 loses. That needs
  two simultaneous breaches today and is rare, but sudden death makes it
  plausible. It should be fixed whatever else is decided.
- **Claim cost.** `baseClaimPerTick` is 17 and the shipped `claimThreshold` is 5000,
  so one claimer takes about 294 ticks (29 s) and four claimers (the cap) about 74
  ticks (7.4 s). Pushing against an opponent's lean multiplies by 4.
- **Production is a countdown.** `TickProduction` (:614) decrements
  `productionTicksRemaining` once per Working villager per tick (shipped values:
  food 40, material 50, metal 60 ticks).
- **Respawn matters mid-breach.** `TickRespawns` (:894) puts a villager back as
  `Idle` on its own core (`ResetToCore`, :916). `TickCombat` (:311–351) then
  pulls everyone on a node holding both players into a fight. A respawn during an
  attack on the core is already a defender appearing from nowhere.
- **Time and distance.** Respawn is 50 ticks (5 s). An edge costs
  `travelWeight × moveSpeedTicks`, which is 16 ticks (1.6 s) at the documented
  defaults of 4 and 4. I have not verified the shipped edge weight.
- **Tempo can be a pure function of `tickCount`.** `tickCount` is already hashed and
  restored by `CopyFrom`, so a time-based ramp needs no new state.

## 2. Tempo phases

**Shape.** Two steps, at 2:00 (tick 1200) and 3:00 (tick 1800), as you chose. The
reason to step rather than ramp smoothly: Supercell replaced Clash Royale's flat
double-elixir period with a ramp that adds a triple stage in the final minute,
and described the goal as making the end of a match faster and more demanding.
Distinct, announced stages are also easier to feel and to communicate.

**The flaw in copying Clash Royale directly.** Elixir is flat and identical for
both players, so its ramp is neutral. Here production scales with how many
nodes and workers you hold, so a production ramp magnifies whoever is ahead. RTS
design commentary frames snowball control as diminishing returns on economy and
army size. The ramp should therefore weight toward things that do not reward the
leader:

| Axis | Stage 1 (1200) | Stage 2 (1800) | Why |
|---|---|---|---|
| Claim rate | 150% | 200% | tug-of-war, neutral between players |
| Respawn speed | 125% | 150% | fights resolve faster, helps the player behind |
| Production | 110% | 125% | kept modest; this is the snowball axis |
| Movement | 100% | 100% | commitment keeps lockstep delay tolerable |

All values are placeholders. Set them from bot-vs-bot data (§5).

**How, with integers and no new state.**
- Claim: `rate = rate * pct / 100` on the final per-tick rate, composed with the
  existing Watchtower numerator and denominator. `17 × 150 / 100 = 25`; the
  truncation is deterministic and small.
- Timers (production, respawn): decrement by
  `C(t) − C(t−1)`, where `C(t)` is the cumulative scaled tick count,
  `floor((p0·min(t,t1) + p1·clamp(t−t1, 0, t2−t1) + p2·max(0, t−t2)) / 100)`.
  This yields a decrement of 1 or 2 that averages `pct/100` exactly, with no
  accumulator in the state.

**Data.** Arrays on `GameBalanceData` (stage start ticks and percentages per
axis); `BalanceHasher` must include them and the `GameBalance` asset class must
mirror them. A new `TickEventType.TempoStage` lets the feel layer play the
banner and sound. Events are append-only and cannot change a result.

## 3. Sudden death

**The ruling-out of the obvious copy.** Clash Royale decides regulation by tower
lead and uses overtime only when it is tied. Copied here, that makes "land one
breach, then turtle" dominant. Do not copy it.

**Recommended: a stateless threshold drop.** At a set tick the breach threshold
falls 3 → 2, and later 2 → 1. It needs no new state, and it is easy to explain.

**Costs, and what to do about them.**
- A player already at or over the new threshold loses on that tick. Warn with a
  countdown several seconds ahead.
- If both players are over on the same tick, `TickWinCondition` must stop
  favouring player 0. A deterministic, symmetric tie-break is needed: compare nodes
  owned, then breaches landed, then call a draw. A draw means `winnerID = -1` with
  `gameOver`, and I have not checked what the match log, the referee, or rating
  code do with that. Check before choosing a draw.

**Alternative: relative "next breach wins".** It requires a per-player baseline
field recorded when sudden death starts (hashed, copied, and added to the factory).
It is symmetric and easy to hype, but it erases a lead: at 2–0 up, the leader goes
from one breach from winning to one breach from winning and one from losing. I
would take the stateless version first and revisit only if playtests show
boundary losses feel bad.

**Ticks come from data.** Choose the sudden-death tick near the 75th percentile of
bot-vs-bot match length, not from a guess.

## 4. A smaller board, with no shrinking

"Shrinking board" means a smaller board from the start plus sudden death. No node
is lost or removed during a match, so there is no collapse rule, no eviction rule
and no change to pathfinding, and the hash needs no node field for it.

**Where each half lives.**
- *Smaller board* is map data: about 16 nodes in handmade, verified layouts
  (`BoardConfigData` and `MatchFactory`). It is not a simulation rule.
- *Convergence* is sudden death (§3) plus the tempo ramp (§2). Both act on rules
  rather than on the map, so every map plays the same way under them.

**What the smaller board changes about the numbers in this document.** Edge counts
between cores fall, so the reaction window in §5 (breach time against the 5 s
respawn) is tighter on a 16-node map than on the current 28-node one. Check the
breach table against each real map's shortest core-to-core route in ticks, and
set the sudden-death tick from bot-vs-bot lengths on the *new* maps, since match
length will drop with the board.

## 5. The breach bar

### Critique of "a bar per villager"

Independent bars per attacker, each speeding up with the number of attackers,
would all fill at the same moment and breach on the same tick. That is the
opposite of the slowing sequence you described. The sequence comes from **one**
bar per defended core that resets after each breach, where each breach consumes
one attacker, so the attacker count (and the rate) falls each time.

### Mechanics

- **New state `VillagerState.Breaching`.** An attacker arriving at the enemy core
  with no living defenders enters `Breaching` rather than being consumed
  immediately. Same for resuming after a fight (:986).
- **New step, `TickBreach`,** run inside the claiming step (after `TickClaiming`),
  so the canonical tick order does not change. For each core, count `Breaching`
  attackers `n`.
  - `n == 0`: the bar decays.
  - otherwise: `bar += S[min(n, cap)]`. At `bar >= barMax`, the **next breacher**
    (see below) is consumed (the same consumption rules as `ProcessBreach`),
    `breachCount` rises, and the bar resets to 0.
- **Who is consumed: the next breacher.** A first draft said "lowest `villagerID`",
  which is deterministic but arbitrary and invisible. Once the villager is
  highlighted (§ UI below) the rule has to make sense to a player, and an attacker
  should not lose a fully equipped Warrior while a plain villager stands beside it.
  Rank the breachers by a total order: **not combat-suited first**
  (`GameBalanceData.IsCombatSuit`), then **lowest HP**, then **lowest `villagerID`**.
  The ID tiebreak is what makes it total, as the determinism contract requires. The
  choice moves when HP changes in a fight, and the highlight moves with it.
- **Defenders interrupt for free.** If a defender is present or respawns,
  `TickCombat` already flips everyone on the node to `Fighting`. Those attackers
  stop counting as breachers; when the fight ends with no enemies, survivors
  re-enter `Breaching`.
- **State.** `PlayerData.breachBar`, meaning progress against that player's core,
  and `PlayerData.nextBreacherID` (−1 for none), the villager that would be consumed
  now. Both must be added to `SimulationStateHasher`, set in `MatchFactory`, and
  covered by the existing reflection test for `CopyFrom`. `nextBreacherID` is
  derived, and is stored anyway so the view reads state and never calls the ranking
  itself: the view rules forbid reaching into `GameSimulation`, and a second copy of
  the ranking in the view would drift from the real one. `TickBreach` recomputes it
  every tick after combat.
- **`UpdateVillagerClaimStates`** only re-evaluates Idle, Claiming and Working, so
  a `Breaching` villager is left alone, but check the Core-always-Idle rule (:686).
- **Orders.** A Breaching villager given a Move should be allowed to leave; the bar
  then decays. I have not read `CommandProcessor`'s restrictions on Move for this
  state, so confirm.
- **Blast radius.** The three most-used states appear in 17 files (78 occurrences,
  including tests); `VillagerView` has 15, `BotPlayer` 6. A new enum member needs
  each switch checked, and `compile-check.ps1` will find the broken ones.

### Swarm scaling

Let a lone attacker take `L = 40` ticks (4 s): `barMax = 4000`, and `S` is a
percentage added per tick, so breach time is `ceil(4000 / S[n])`.

| Table `S[1..4]` | Breach times n=1/2/3/4 (ticks) | 3 attackers, all 3 breaches | 4 attackers, 3 breaches |
|---|---|---|---|
| Linear `100,200,300,400` | 40 / 20 / 14 / 10 | 74 (7.4 s) | 44 (4.4 s) |
| **Diminishing `100,165,215,250`** | 40 / 25 / 19 / 16 | **84 (8.4 s)** | **60 (6.0 s)** |
| Capped at 3 `100,165,215,215` | 40 / 25 / 19 / 19 | 84 | 63 |

**Recommendation: diminishing.** Linear, which is how claiming scales, lets four
attackers finish in 4.4 s, which is less than the 5 s respawn, so the defender
cannot respond at all. Diminishing returns are also the standard RTS
anti-snowball tool. The first breach of a three-attacker rush lands in 1.9 s,
which still reads as a real threat.

**Decay when unopposed-then-abandoned:** about `barMax / 20` per tick (progress
gone in 2 s), so a failed rush wastes its work.

**Does not scale with tempo.** Sudden death already tightens the endgame; scaling
the bar as well would stack two effects that cannot be tuned apart.

**UI.** One bar over the core, visible to both players, with a pip per attacker and
an accelerating tick sound.

**The breacher highlight.** The villager in `nextBreacherID` is tinted a distinct
colour on both players' screens, so the attacker sees which unit is the sacrifice
and the defender sees who is about to go through.
- *Colour.* It must not be either player colour (the code uses blue
  `(0.4, 0.7, 1)` for player 0 and red `(1, 0.4, 0.5)` for player 1), the selection
  colour, or the lasso cue's gold. Choose from the palette in a design pass;
  magenta or violet are the open hues.
- *Not colour alone.* Add a mark (a ring or chevron) when `colourblindMarks` is on,
  which the settings already carry and default to on.
- *Fill.* Let the highlight ring fill with `breachBar / barMax`. The core bar and the
  ring then show the same number, and the eye finds the one that matters.
- *No new control.* Combat targeting is automatic (`fightPriority`, then ID), so the
  highlight is information, not a target the defender can choose.
- *Source.* It reads `PlayerData.nextBreacherID` and nothing else. It is a view
  effect on `VillagerView`, driven from state each frame.

## 6. Tests the sim changes need

Single-attacker time; swarm times matching the table; defender arrival interrupts
and survivors resume; a respawn arriving mid-channel; decay; the consumption
order (unsuited before suited, lower HP before higher, lower ID last) and
`nextBreacherID` matching the villager actually consumed; the bar surviving a rollback and `CopyFrom`; the hash including the
bar; tempo decrement averaging exactly `pct/100` over a long span; a same-tick
double breach; an old-version log refused by `MatchReplay`.

Re-pinning the baselines is expected here **and only here**: it pairs with the
`SimulationVersion` bump, which is the sanctioned path. A hash that differs
between CI legs is still a finding about the simulation and never gets re-pinned.

## 7. Order of work

1. **Measure.** A bot-vs-bot batch over `MatchFactory` and `MatchReplay` for
   match-length distribution and rush win rate. I did not verify that a
   bot-vs-bot batch runner exists; the pieces do.
2. **Input rework** (see [touch-input-spec](touch-input-spec.md)). View-only, no
   sim risk, and the biggest felt change.
3. **Breach bar, tempo, sudden death,** with the tie-break fix, in one version bump.

## 8. Decisions needed

1. Sudden death by stateless threshold drop, or relative "next breach wins"?
2. Is a draw acceptable as a tie-break outcome, which depends on how `winnerID = -1`
   is handled downstream?
