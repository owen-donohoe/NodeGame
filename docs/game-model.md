---
type: Domain Model
title: Game Model
description: What Node War is — the match model, board, villagers, districts, suits, resources, win condition and eras, as the simulation actually implements them.
tags: [game-design, domain-model, districts, suits, combat, claiming]
generated: { by: claude-opus-5, at: 2026-08-31T00:00:00Z }
verified:
  # full history: docs/verification-log.md
  - { by: claude-sonnet-5-5, at: 2026-10-08T16:13:42Z }
verified_at_commit: 048597d1
status: draft
sources:
  - id: sim-state
    resource: Assets/Scripts/Game/Simulation/SimulationState.cs
    title: DistrictType, SuitType, VillagerState, NodeData, VillagerData, PlayerData
  - id: sim-loop
    resource: Assets/Scripts/Game/Simulation/GameSimulation.cs
    title: GameSimulation.SimulateTick and all tick steps
  - id: balance
    resource: Assets/Scripts/Game/Simulation/GameBalanceData.cs
    title: GameBalanceData.Default, IsCombatSuit, CanEquipSuitAtNode, GetUpgradeCategoryForDistrict
  - id: board
    resource: Assets/Scripts/Game/Simulation/BoardConfigData.cs
    title: BoardConfigData and InitialDistrictPlacement
  - id: pathfinding
    resource: Assets/Scripts/Game/Simulation/Pathfinding.cs
    title: Pathfinding.FindPath and ownership preference multipliers
  - id: commands
    resource: Assets/Scripts/Game/Simulation/Commands.cs
    title: CommandType and GameCommand
  - id: command-processor
    resource: Assets/Scripts/Game/Simulation/CommandProcessor.cs
    title: CommandProcessor.ProcessCommand
  - id: draft-state
    resource: Assets/Scripts/Game/Simulation/DraftState.cs
    title: DraftState grid occupancy and per-player slots
  - id: design-history
    resource: docs/design-history/README.md
    title: Design history and v2.1 reconciliation
  - id: match-factory
    resource: Assets/Scripts/Game/Simulation/MatchFactory.cs
    title: Shared drafted board and static configuration
  - id: balance-asset
    resource: Assets/Data/Game/Balance/Resources/DefaultGameBalance.asset
    title: Currently identical values across eras
---

# Game Model

Node War is a **1v1 real-time strategy game played on a graph of nodes**, simulated
deterministically at 10 ticks per second. Two players start from opposing Core nodes and compete to
claim territory, produce resources, equip combat units, and breach the enemy Core. The opening
loss threshold is three breaches; sudden death lowers it to one.

This document describes *what the game is*. [architecture](architecture.md) describes how the code
is layered; [simulation-rules](simulation-rules.md) describes the determinism contract the
simulation must uphold.

All numbers below are the **code defaults** from `GameBalanceData.Default()` and the shipped map in
`PremadeMaps`. A real match reads its balance from the `GameBalance` `ScriptableObject`, and its map
from the map ID a `BoardConfig` names, so treat these as the shape of the tuning, not as fixed
constants. The checked-in `DefaultGameBalance` asset does not yet list the capture-bonus or recruit
fields, so confirm in the Editor what a match actually plays before relying on those numbers.

## The board

A map is a grid of cells, each made of one **terrain**: `Land`, `Lake` or `Ocean`. Land cells carry a
node. Ocean never does. A Lake cell carries one only if a `Pier` district is drafted on it, which is
only possible where the map marks that Lake cell as a district slot; open lake is impassable water.
Each `NodeData` carries a grid position, its terrain, a `Link[]` of connections, a district type,
an owner, and a signed claim bar. The board is therefore sparse: node IDs follow cell order with the
gaps closed up, and a link joins only orthogonally adjacent cells that both have nodes.

The one shipped map is `hourglass-01`, a 7×7 grid ringed by ocean. Each player's Core sits on the
second row from their own edge, and a lake fills the middle of the board, so the land runs round it
in two banks that meet at the Core rows. It is mirrored top to bottom. The left bank is cut by
one Pier slot, a lake cell halfway down; drafting a Pier there bridges it, but a map must always
keep a land route between the Cores, so a Pier is never the only way across. A map is authored data
(`BoardConfigData`, built by `PremadeMaps`) and named by its ID; a map is also checked to have
14 to 20 nodes counting every legal Pier.

A `Link` has a `travelWeight` (default 4). Crossing it takes `travelWeight × moveSpeedTicks` ticks,
so movement cost is a property of the board, not of real time.

Both players begin owning one Core, placed at opposite ends of the grid. Everything else is
unowned and contested.

## Villagers

Each player starts with 3 villagers. A villager is always in exactly one `VillagerState`:

| State | Meaning |
|---|---|
| `Idle` | On a node, doing nothing |
| `Moving` | Traversing a path produced by `Pathfinding.FindPath` |
| `Working` | Producing a resource on an owned production district |
| `Claiming` | Pushing the claim bar on a node the player does not own |
| `Fighting` | On a node where both players have living villagers |
| `Dead` | Awaiting respawn at the owner's Core |
| `Breaching` | Filling the breach bar on an undefended enemy Core |

Villagers carry HP (default 5), attack damage, move speed, an attack cooldown, and a
`fightPriority` used as the combat targeting sort key. A player is capped at 25 villagers.

## Movement and pathfinding

`Pathfinding.FindPath` is Dijkstra over the node graph with **integer ownership preference
multipliers** — the simulation is integer-only, so fractional preference is expressed as a
percentage:

| Node relative to the mover | Multiplier |
|---|---|
| Owned | 50 (0.5×) |
| Partially owned (claim bar leaning their way) | 75 |
| Unowned | 100 |
| Enemy partially owned | 150 |
| Enemy owned | 200 (2.0×) |

Cost is `ceil(travelWeight × multiplier / 100)`, minimum 1. Villagers therefore prefer to travel
through friendly territory and route around enemy ground unless the detour is long. An enemy Core
may start or end a route but is never a transit node: a path does not run through it.

Movement is checked on **every node arrival**, not just at the destination: arriving on a node with
living enemies interrupts the path and starts a fight, and arriving on the enemy Core triggers a
breach channel or a fight (an instant breach only when the channel is disabled).

A villager in transit has no position of its own. `currentNodeID` is the node it last stood on, and
how far it has come is a tick count along the link it is crossing.

**Retargeting mid-link costs the ground already covered.** Ordering a moving villager somewhere new
does not rewind it onto the node behind it. If the new route continues through the node it is
already approaching, the crossing carries over and the order is free. If the new route leaves in
another direction, the villager turns around and re-walks exactly the distance it had covered before
taking it. Ordering it back to the node it just left is a legitimate order, and is how a player
cancels one.

Turning around therefore has a price, and repeated orders cannot stall a villager in place.

**Orders are sticky.** A move order records its destination as the villager's intent
(`targetNodeID`), and the intent outlives whatever interrupts the walk. A fight does not cancel it,
and an order given mid-fight changes only the intent, never the attack clock or the fight. An
unreachable or blocked destination is not dropped either: the villager waits and retries. Once
combat has resolved, the final order-resume step of the tick replans from where each survivor
stands and sets it walking again, or breaches, or arrives. Work and claim begun there count from
the next tick. A villager whose intent points elsewhere does not work or claim on the node it
stands on. Arriving at the destination clears the intent.

## Claiming

Every non-Core node has a signed `claimBar`. Positive is player 0, negative is player 1, and
`claimThreshold` in either direction transfers ownership (10000 in `GameBalanceData.Default()`, 5000
in the shipped `DefaultGameBalance` asset, which is what a match plays on).

Claiming villagers push the bar by `baseClaimPerTick × claimers` per tick, capped at 4 claimers per
node. Pushing *against* an opponent's existing lean is multiplied by `decrementMultiplier`
(default 4), so taking ground back from an established claim is faster than establishing it — the
bar is a tug-of-war, not a per-player progress meter. Crossing zero drops the node to neutral
(`ownerID = -1`) before it can be claimed the other way. The bar never passes `claimThreshold`
either way.

**The capture bonus** rewards a connected frontier. The claim rate is multiplied by
`100 + captureBonusPercentPerStep × steps`%, where `steps` is the number of linked neighbours the
claimer owned minus the number the opponent owned, taken at the **start of the tick** and clamped
between 0 and `captureBonusMaxSteps` (code defaults 25% and 2, so up to 150%). Ground next to your
own territory falls faster; a node behind enemy lines gets no bonus. The same frontier percentage
scales breach progress against a Core. The Watchtower's adjacent-claim boost and the Rampart's
decrement reduction no longer exist in claiming; the capture bonus replaces the first.

The current tempo percentage then scales the claim rate, using integer division. A node with
**both** players' claimers present is frozen; combat resolves it instead.

**Restore.** An owned node whose bar has been pushed back toward neutral recovers when its owner's
villagers stand on it unopposed (working, idle or claiming, up to four, no enemy present): they push
the bar back toward the threshold at the ordinary claim rate, tempo-scaled but without the
decrement multiplier or capture bonus. Cores do not restore.

When a claim completes, a non-`Fixed` node becomes whichever district the claiming player drafted
for that slot type, falling back to the node's `baseDistrictType`. Village claims
do not spawn bonus villagers.

## Districts

`upgradeCategory` determines which drafted upgrade a non-`Fixed` node can become when claimed.
The table gives each district's category. The manual draft's `MatchFactory` board
places every district as `Fixed`, so those districts keep their type and placer's era on capture.

| District | Category | Role |
|---|---|---|
| `None` | Fixed | Empty connector / crossroads |
| `Core` | Fixed | Home node. Friendly arrivals idle unless contested. The breach target. |
| `Farm` | Fixed | Farmer works it → +1 food |
| `Mine` | Fixed | Miner works it → +1 material |
| `Forge` | Fixed | Smelter converts 1 material → 1 metal, only while `materialAllocation > 0` |
| `Village` | Fixed | Paid Recruit action and optional automatic repeat; no claim bonus |
| `Camp` | Army | Equip Warrior or Scout |
| `Barracks` | Army | Equip Warrior, Guardian, Berserker or Scout |
| `Arsenal` | Army | Equip Warrior, Guardian or Scout |
| `Shrine` | Healing | Faster passive healing for its owner's villagers standing on it |
| `Sanctuary` | Healing | Acolyte works it → faster respawns; also the only Medic equip point |
| `Watchtower` | Affect | Watcher works it. Its claim boost was retired in favour of the capture bonus; its stats are still in the balance but no tick rule reads them |
| `Rampart` | Affect | Owner's occupants gain max HP and damage reduction |
| `Market` | ResourceSpecial | Merchant works it → alternates +1 food and +1 material |
| `Pier` | Fixed | Drafted only on a Lake slot. Turns that cell into a node that connects its land neighbours; grants nothing and blocks nobody |

## Suits

A suit is a villager's role. Production suits (`Farmer`, `Miner`, `Smelter`, `Merchant`, `Acolyte`,
`Watcher`) are **assigned automatically** on arrival at the matching owned district and stripped
when the villager leaves.

Combat suits (`Warrior`, `Guardian`, `Scout`, `Berserker`, `Medic`) are **equipped deliberately**
via an `Equip` command and are **permanent until death**. Equipping requires all of: the villager
is `Idle` and not already combat-suited, it is standing on a node its owner controls, that node's
district permits the suit, the player **drafted** that suit before the match, and the player can
pay its food and material cost. The balance must contain that suit's stats at the player's era
or at era 0; an unlisted suit cannot be equipped. A combat-suited villager never works — it idles
on owned nodes.

`Medic` is the exception in combat: instead of attacking, it heals the most-damaged friendly
villager on its node.

## Resources

Three resources per player: **food**, **materials**, **metal**. Materials feed the Forge, which
consumes them to make metal. Resources pay for suits, respawns and recruits. Ordinary production runs on
per-villager tick timers, so output is a function of how many workers a player keeps alive and
employed — capped at 2 workers per node.

The default storage caps are 30 food, 30 materials and 10 metal. A cap of 0 or less is uncapped;
missing cap fields therefore retain the old behaviour. Starting resources and every gain are
clamped. A production completion at capacity is wasted but its timer still cycles. A Forge at
the metal cap does not consume a material; a Market still alternates food/materials after a
wasted completion. Spending is unchanged. Magic in the HUD is display-only, not a fourth
simulation resource.

Tempo stages begin at ticks 1200 and 1800 (two and three minutes). Claim rates become 150% then
200%, production timer decrements 110% then 125%, and passive respawn timer decrements 80% then
67%. Timer scaling integrates integer percentages over ticks 1 through the current tick;
each decrement is the difference between consecutive cumulative totals. Production carries
any remainder into the next cycle rather than losing it on completion.

## Combat

When both players have living villagers on the same node, everyone there is forced into `Fighting`.

Targets are assigned **round-robin**: attackers stay in `villagerID` order, while each side's
target list is sorted by `fightPriority` descending then `villagerID` ascending — a total order
with no ties, which the determinism contract requires. Each fighter attacks when its cooldown
expires. A defender with the bonus from an owned Rampart takes
reduced damage, to a floor of 1.

At 0 HP a villager dies, drops its path, and respawns at its owner's Core after `respawnTicks`
(default 50), reset to base stats with no suit. A player may also spend food on a `Respawn` command
to bring a dead villager back immediately instead of waiting. Each Acolyte working a Sanctuary both
speeds the passive countdown and reduces that food cost by its Sanctuary's era-specific values.
Workers' boosts and cost-reduction percentages add. Tempo scales the passive countdown first,
then Sanctuary adds its boost. Each successful paid respawn increments the player's match-long
`paidRespawns`: the next cost is `respawnCostFood × (paidRespawns + 1)`. Sanctuary reductions
apply to that escalated cost, subtracting the integer-rounded-down discount, with a minimum
payment of 1 food. Failed commands do not advance the counter.

Combat is deliberately resolved across two separate tick steps. Damage and deaths happen in the
combat step; survivors decide what to do next in a final order-resume step after the
win-check. See the reasoning on `TickOrderResume`.

## Breach and the win condition

A villager that reaches the **enemy Core with no living defenders on it** enters `Breaching`.
Each defender has a `breachBar`; attackers fill it together during the breach pass before
claiming. The code default maximum is 4000, with rates 50/83/108/125 per tick for one/two/three/
four-or-more attackers: a lone attacker takes 80 ticks. With no attackers, the bar decays by
200 per tick. Combat interrupts the channel. A survivor resuming after combat starts filling
on the following tick. The Editor asset currently uses 67/111/144/167 instead, and healing
every 20 ticks rather than the code default 30.

On completion, at most once per defender per tick:

1. The defending player's `breachCount` increments.
2. The breaching villager is **permanently consumed** — flagged `isConsumed`, never respawns.
3. The bar resets to zero, discarding excess progress. The consumed attacker is chosen by
   non-combat suit first, then lowest HP, then lowest villager ID. `nextBreacherID` is refreshed
   after all tick steps to describe the next candidate, or -1 when none exists.

A breach is a trade: a unit for a point. A player loses only when a **new breach this tick**
leaves their count at or above the current threshold. At tick 2400 (four minutes), sudden death
drops that threshold straight from 3 to 1; the drop alone never ends the match, even if a Core
already took a breach. Simultaneous losses cancel and play continues. With the breach channel
disabled (`breachBarMax = 0`), arrival breaches remain instant, the threshold stays fixed and
the legacy win check applies.

## The pre-match draft

Before play, the two peers agree on the map and rules (`MatchSetup`: map ID, board fingerprint,
simulation version and balance), and refuse each other on any difference. Then players run a
turn-based placement draft, tracked by `DraftState`, choosing where their districts sit on the
grid. Where a district may go is one rule, `PlacementLegality`: inside the grid, on an empty
slot cell that is not a Core, ordinary districts on Land and a Pier only on a Lake slot. A pick
with no legal cell left is skipped, not forced, and the draft ends when neither player has a
playable pick. On a timeout the parked piece is placed if still legal, otherwise the lowest
playable pick goes to a legal cell chosen from a seed derived from replicated state.
`MatchFactory` builds the starting board from those placements,
at their placers' eras. They begin unowned and `Fixed`; claiming one does not replace it with the
claimer's loadout. The simulation still supports non-`Fixed` slots, whose claim upgrades use the
claimer's drafted district for that slot type.

Both players' base draft districts on `hourglass-01` are the same three, Farm, Mine and Village. Production is slower than the code defaults: a worker yields food every 40
ticks, material every 50 and metal every 60, and a Market's secondary material every 55
(`DefaultGameBalance`, identical in every era).

The draft is a **manual placement** system. The v2.1 design document describes a different
auto-population scheme; the code is canon. See [design-history](design-history/README.md).

## Eras

Progression is by **arena** (0–5, climbed with rank points), and arena N is **era N**. Every suit
and every district has one variant per era, and a variant **changes gameplay**: its numbers are its
own entry in the balance (`SuitStats.era`, `DistrictStats` per district and era). Power creep with
arena is the progression, deliberately; matchmaking keeps opponents within one arena of each other
because rating cannot see the era gap.

- A player owns a variant once they have reached its arena, and may field it only while at or above
  that arena. The server grants and checks this; the lobby only shows it.
- In a match, a district plays **its placer's era**: the era of whoever drafted it there, or of the
  claimer when a claim upgrades a non-`Fixed` slot. A slot falling back to its base district uses
  era 0, as do the board's own fixed placements. A combat suit plays the era its owner fields;
  a worker's production and district effects use the district's era.
- Era tables on a player may be missing or short, which means era 0. A balance lookup first tries
  the requested era, then era 0. Missing suit stats refuse equipping; missing district stats
  supply zero-valued effects. These numbers live on `SuitStats` / `DistrictStats`, not new globals.
- Today eras 1–5 are copies of era 0, so every match plays as before until someone tunes them.
- **Skins** are cosmetic variants of the same items. They travel to the opponent and into the match
  log, and never reach the simulation.

## Player commands

Every player action reaches the simulation as one of six active `GameCommand` types:

| Command | Effect |
|---|---|
| `Move` | Path a villager to a target node |
| `SetAllocation` | Set an owned Forge's `materialAllocation`, gating its material→metal conversion |
| `Equip` | Put a combat suit on an Idle villager standing on an owned district that permits it |
| `Respawn` | Pay food to return a dead villager to its Core **immediately**, skipping the timer |
| `Recruit` (5) | Pay food to append one base, unsuited Idle villager at an owned, uncontested Village |
| `SetAutoRecruit` (6) | Set an owned Village's repeat flag to the absolute value 0 or 1 |

Recruit needs no worker or visitor. With pre-recruit player count N, the default
price is `6 + 3N` food and the Village cooldown is that many seconds, converted
with `ticksPerSecond` (default 10). Successful recruits increment the match-long
`recruitCount`, separately from paid respawns. Population includes dead bodies
but excludes consumed ones. Cooldown, insufficient food, a price above a positive
food cap, a full population, living enemy presence, or invalid/overflowing tuning
refuse without spending, spawning, or changing counters and cooldown.

Automatic recruitment runs after ordinary production in ascending node ID;
Villages share their owner's recruit count and food pool but have separate
cooldowns. The flag stays on when an attempt is refused and retries each tick.
SetAutoRecruit does not require food, population room or readiness and does not
recruit or start cooldown. Losing a Village clears its flag and ready tick;
the player's count persists. Cooldown is not tempo-scaled. The food cap stays 30:
the ninth recruit costs 30; the tenth costs 33 and is refused. Node commands use
`villagerID = -1`; Recruit requires `value = 0`, while the toggle requires 0 or 1.

There is no other way to affect game state. See [simulation-rules](simulation-rules.md).
