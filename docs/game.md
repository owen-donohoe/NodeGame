---
summary: Node War's player-facing rules: territory, districts, resources, units, drafting, eras and victory.
sources:
  - Assets/Scripts/Game/Simulation/SimulationState.cs
  - Assets/Scripts/Game/Simulation/GameSimulation.cs
  - Assets/Scripts/Game/Simulation/GameBalanceData.cs
  - Assets/Scripts/Game/Simulation/BoardConfigData.cs
  - Assets/Scripts/Game/Simulation/BankRules.cs
  - Assets/Scripts/Game/Simulation/PierGate.cs
  - Assets/Scripts/Game/Simulation/Pathfinding.cs
  - Assets/Scripts/Game/Simulation/Commands.cs
  - Assets/Scripts/Game/Simulation/CommandProcessor.cs
  - Assets/Scripts/Game/Simulation/DraftState.cs
  - Assets/Scripts/Backend/Shared/LoadoutCapacityPolicy.cs
  - Assets/Scripts/Backend/Shared/SuitTree.cs
  - Assets/Scripts/Backend/Shared/ContentTable.cs
  - Assets/Scripts/Game/Simulation/MatchFactory.cs
  - Assets/Data/Game/Balance/Resources/DefaultGameBalance.asset
  - Assets/Scripts/Game/Simulation/DistrictHealth.cs
  - Assets/Scripts/Game/Simulation/NodeActionRules.cs
verified: a35341b2
---

# Node War

Node War is a 1v1 real-time strategy game on a graph of nodes. Players start at opposing Cores, claim territory, employ workers, equip fighters and trade attackers for breaches of the enemy Core. The opening loss threshold is three breaches. Sudden death lowers it to one, but a fresh breach must trigger the loss.

The game advances at 10 ticks per second. Numbers below describe the shipped balance where specified, otherwise code defaults. Balance and map are match content: a selected balance can tune these values. The [system cards](systems/simulation.md) explain implementation; this document describes the rules players experience.

## Board and opening

A map contains Land, Lake and Ocean cells. Land carries nodes; Ocean never does. Lake carries a node only where a drafted Pier occupies a designated district slot. Connections join orthogonally adjacent node-bearing cells, making open water impassable.

The shipped `hourglass-01` map is a mirrored 7×7 grid ringed by ocean. Cores sit on the second row from each player's edge. A central lake separates two land banks that meet at the Core rows. One Lake slot interrupts the left bank: placing a Pier bridges it. A land-only route always remains between Cores; a Pier is never the only crossing. Authored maps have 14–20 possible nodes counting legal Piers.

Each player begins with one owned Core and three villagers. Other districts begin neutral. A player may have at most 25 units; dead bodies still occupy population, permanently consumed units do not.

Before play, both players agree to the same map and rules, then manually place districts in a turn-based draft. Below arena index 2, each player chooses two suits and two districts and receives Warrior plus the map's Farm/Mine/Village base pool. From arena index 2, each chooses three suits and five districts without those grants; Warrior can be omitted. P0 places first. Ordinary districts require empty Land slots; Pier requires an empty Lake slot; Cores and Ocean cannot receive picks. A piece with no legal cell is skipped. Draft ends when neither player has a playable piece.

On timeout, a still-legal parked piece is confirmed. Otherwise the lowest playable pick receives a legal fallback placement. Drafted districts use their placer's era and keep their type/era when captured. They do not transform into the capturing player's loadout choice. Rules also support upgrade-category slots whose full capture selects the claimer's drafted district, falling back to the base district.

## Villagers and orders

A body is Idle, Moving, Working, Claiming, Fighting, Dead or Breaching. Default base HP is 5; base attack damage is 1 and attack cooldown 20 ticks. Production suits assign automatically at suitable owned workplaces and disappear on departure. Combat suits are deliberately equipped and last until death.

Movement follows weighted connections. Default link weight is 4; physical crossing time is link weight × the unit's movement duration. Default body movement duration is 4 ticks, giving 16 ticks per ordinary default link. Entering one's own Pier halves travel time by default, rounding up. A crossing's duration is fixed at departure, so capture mid-crossing does not change it.

Routes prefer friendly territory using relative costs: owned 50, friendly partial claim 75, neutral 100, enemy partial claim 150, enemy-owned 200. Costs use physical travel time and round upward, minimum 1. An own Pier uses its faster physical time but multiplier 100. Enemy Piers add expected time to neutralise their gate, except when starting there. A route can start or end at an enemy Core but cannot pass through it. Equal-cost alternatives resolve consistently.

Every node arrival can interrupt movement for enemies or a Core breach. Retargeting during a crossing does not teleport the unit backward: continuing through the approached node preserves progress; changing direction costs the distance already covered to turn back. Ordering the previous node is a valid cancellation. Repeated orders cannot stall a unit indefinitely.

Orders remain intent through fights and gates. An order during combat changes the destination without interrupting the fight or resetting attacks. Impossible-route orders change nothing. Once combat resolves, survivors resume toward their destination; newly resumed work or claiming begins next tick. A body ordered elsewhere does not work or claim while merely waiting on an intermediate node. Reaching the destination clears the order.

An enemy Pier is a gate. A body standing there may retreat only to its previous node; any other departure waits while the Pier is claimed down to neutral. If the claimer limit is full, it waits idle. Enemy Pier pressure stops at neutral before onward movement becomes available.

## Territory and district health

Each non-Core node has one tug-of-war claim bar. Full claim gives ownership. The shipped threshold is 5000; code default is 10000. Up to four claimers push at default 17 per body per tick. Pressure against an opponent's existing lean is multiplied by 4, so neutralising established ground is faster than building a new claim. Crossing the centre removes ownership before the opposite full claim can complete. Both players' claimers present freeze the bar while combat resolves.

Connected territory gives a capture bonus: count linked friendly-owned neighbours minus enemy-owned neighbours, clamp between 0 and 2, then add 25% per step. The default maximum is 150% rate. Neighbour ownership is taken at tick start, so captures occurring together cannot cascade bonuses within that tick. This frontier bonus also scales breach progress. Tempo scales claim rates; Fortress resistance reduces hostile claim and breach rates.

Friendly unopposed bodies standing on an owned node restore a bar pushed toward neutral, up to four contributors. Restore uses ordinary claim rate and tempo, without enemy-lean multiplication or frontier bonus. Idle, working or claiming bodies qualify; minions neither restore nor prevent it. Cores do not restore.

Fortress, Storehouse and Infirmary add district health in front of the owner's claim bar: default maximum 3000 and regeneration 17 per tick. Enemy claim pressure drains health first, then the bar. Regeneration requires the bar fully at its owner's end. Freshly drafted/captured districts start at zero health; neutralisation and capture reset it.

These districts' passive effects require full health, disabling as soon as health falls below maximum. They resume only after full regeneration. Fortress level survives health damage but resets when ownership is lost. There is no separate structure attack action or soldier damage to structures.

## Districts

| District | Player-facing role |
|---|---|
| Empty node | Connector, territory and meeting point. |
| Core | Home/respawn point and enemy breach target; friendly arrivals idle unless contested. |
| Farm | Farmers produce one food per completion directly into the owner's pool. |
| Mine | Miners produce one material per completion directly into the owner's pool. |
| Forge | Smelters spend one material to produce one metal while allocation is positive. |
| Village | Paid recruitment and optional automatic repeat; capture itself grants no bodies. |
| Town | Each player's first full claim pays two bodies by default, once for that player. |
| Barracks | Equips drafted Warrior, Guardian, Scout, Berserker or Medic suits. |
| Workshop | Pays metal to forge a mobile collector minion, subject to cooldown/population. |
| Infirmary | Healthy local healing and up to two Acolytes helping respawns. |
| Fortress | Paid levels provide healthy resistance to hostile claim/breach pressure. |
| Storehouse | Healthy autonomous production into a local bank awaiting collection. |
| Pier | Lake bridge, friendly highway and enemy departure gate. |

Town rewards appear at the Town and are limited by population room. Each player's entitlement is spent on the first full claim even if no room remains; reward cannot be deferred. Capturing the opponent's Town can pay the capturing player's own entitlement. Later captures do nothing for a player already paid.

Fortress levels advance sequentially from 0 through 3. Default upgrade prices are 4/8/12 materials or 1/2/3 metal. A living enemy body prevents upgrading. A healthy level 1/2/3 Fortress provides 25/40/50% resistance on itself and its owner's linked nodes, including a covered Core. Hostile rate is divided by `1 + resistance`, minimum 1. Auras do not add together: the strongest applies. Ownership and active resistance are taken at tick start. Losing ownership resets level to 0.

A healthy Infirmary heals friendly living stationary units on its node on its global interval, default 10 ticks, alongside ordinary healing. At most two lowest-ID eligible non-combat bodies work as Acolytes; additional bodies wait. Enemy bodies prevent the worker benefit; minions do not count as denying bodies. Each counted Acolyte adds default 1 to passive respawn countdown decrement and reduces paid-respawn price by 20%. Multiple counted workers' benefits add.

Only the active districts above appear in legal drafts/loadouts/boards. Retired district names are not alternative playable types. Watcher has no workplace; Merchant is not assigned. Their names do not imply additional production.

## Resources, production and banks

Food, materials and metal are the three gameplay resources. HUD magic is display-only. Default storage caps are 30 food, 30 materials and 10 metal. Starts and gains clamp to caps; a nonpositive configured cap means uncapped.

Ordinary production uses per-worker timers, at most two workers per node. Shipped Farm/Mine/Forge completion intervals are 40/50/60 ticks, identical across eras. More workers produce more output. A completion into a full pool is wasted but restarts the cycle. A Forge cannot waste material when metal storage is full. Allocation permits conversion rather than reserving a stockpile.

Storehouses need no worker, minion or worker slot. While owned, healthy and not full, each alternates food and materials into a bank at default 80 ticks per unit, capacity 5. Its timer pauses while ineligible/full and carries excess progress between completions. Farms, Mines and Forges pay directly into pools, never these banks.

Bank collection locks on neutral ownership, incomplete owner-side claim or an anchored living enemy body. A stationary friendly Idle/Working collector, including an Idle minion, or a remote Collect order cashes it out. Progress defaults to 5 per tick, with 16 per transferred unit, independent of tempo. Collection tries food, then materials, then metal, skipping full pools. Locks or all relevant pools being full pause progress.

Collection stops on empty bank or cancellation. Cancelling resets progress only without another stationary collector. Neutralisation keeps remaining stock but cancels collection. Full capture pays remaining stock once to the capturer, capped by storage, then clears stock and the production timer. A recaptured Storehouse produces again once fully healthy.

## Recruitment and minions

Village recruitment requires no visiting worker. Default food cost is `6 + 3N`, where N is the player's successful recruit count. Cooldown is that many seconds at 10 ticks/second. Success adds a base unsuited Idle body and advances the persistent count, separate from paid respawns.

Cooldown, insufficient food, price above a positive food cap, full population, enemy body presence or invalid tuning refuse the action without spending or advancing anything. At the default cap, the ninth recruit costs 30; the tenth costs 33 and cannot be purchased.

Auto-recruit is an absolute on/off setting. Setting it does not recruit or start cooldown and needs no current food/space/readiness. Enabled Villages retry after production in node order, sharing their owner's food and escalating count but keeping separate cooldowns. Failure leaves repeat on; losing a Village clears repeat and readiness. Cooldowns are independent of tempo; player recruit count persists.

Workshop forging costs 3 metal, with a 30-tick per-era cooldown, and never repeats automatically. The minion has default 8 HP and movement duration 2, counts toward population, and can be directed to walk and collect an owned Storehouse bank. It cannot claim, restore, work, equip, breach, respawn or deal damage. Enemies can target it; death consumes it permanently and frees population. Enemy minions do not lock banks or deny Restore, recruitment, forging, Fortress upgrading or respawn eligibility.

## Combat suits, fighting and recovery

Equipping a combat suit requires an Idle body without a combat suit, its owner's permitted district, a suit in the player's resolved drafted suits and enough food/materials. This includes the free Warrior at low arenas. Missing suit stats prevent equipping. Warrior, Guardian, Scout and Berserker provide their balance-defined fighting/movement stats; Medic heals the most damaged friendly unit on its node instead of attacking. Combat-suited bodies never work.

Opposing living units sharing a node enter combat. Attackers assign targets round-robin, with targets ordered by descending fight priority then unit ID. Attacks follow cooldowns; district ownership does not directly reduce damage. A Medic heals instead on its combat turn.

At zero HP a body dies, loses its suit/path and waits to respawn at its Core with base stats. Default passive delay is 50 ticks. Ordinary healing interval is 20 ticks in the shipped balance, versus code default 30. Infirmary healing follows its own district interval in addition to ordinary opportunities.

A paid respawn returns a dead body immediately. Default base price is 1 food, escalating as `base price * (successful paid respawns + 1)`. Add Infirmary percentage discounts, subtract the rounded-down discount and pay at least 1 food. Failure never advances the counter. Passive countdown applies tempo first, then adds Acolyte boosts.

## Breaches, tempo and victory

An attacker on an undefended enemy Core channels a shared breach bar for that defender. Maximum is 4000. Shipped rates for one/two/three/four-or-more attackers are 67/111/144/167 per tick; code defaults are 50/83/108/125, making a lone default attacker take 80 ticks before modifiers. Frontier bonus and Fortress resistance apply; tempo does not. With no attackers, progress decays by 200 per tick. Combat interrupts the channel; survivors resume contributing the following tick.

Completion adds one breach against the defender and permanently consumes one attacker. Non-combat suits go first, then lowest HP, then lowest ID. The bar resets, discarding excess; at most one completion per defender per tick. Breaching trades a unit for a point.

A player loses only when a new breach this tick leaves their count at or above the current threshold. Simultaneous losses cancel and play continues. At tick 2400 (four minutes), sudden death lowers threshold from 3 to 1. That drop alone never ends a match, even with prior breaches. A balance with channel disabled retains instant arrival breaches and a fixed loss threshold.

Tempo stages start at ticks 1200 and 1800 (two/three minutes). Claim rates become 150% then 200%; production timer speed becomes 110% then 125%; passive respawn timer speed becomes 80% then 67%, lengthening unassisted recovery. Fractional timer progress carries over rather than being lost. Collection, recruit cooldowns and breach channel rates remain tempo-independent.

## Eras and skins

Arena indices run 0–5. Every player owns the starter kit: Farm, Mine, Town, Barracks, Warrior and Guardian. Other districts and suits each have a debut era; reaching an era grants nothing directly. Ownership survives demotion; generic district equipment is clamped to the current arena. Tree suits instead use their branch's unlock arena: variant 0 is a root and variants 1/2 are sibling choices, so the variant number alone does not determine availability. The server checks ownership and the applicable eligibility rule. Matchmaking keeps opponents within one arena.

Every district/suit has era-specific gameplay stats. Districts use the placer's era, or the claimer's era for category upgrades; base fallback/fixed board districts use era 0. Combat suits use their owner's fielded era; production and district effects use district era. Missing era stats fall back to era 0; missing district stats mean zero effects. The shipped eras 1–5 copy era 0 until tuned.

Skins are cosmetic variants. They are visible to opponents but never change match rules.

## Available actions

Players issue Move, Forge allocation, Equip, paid Respawn, Recruit, SetAutoRecruit, UpgradeFortress, ForgeMinion and Collect. Collect can be requested while locked and waits; requesting an empty bank does nothing. Each action is checked when applied, so showing availability before a state change does not guarantee success. These actions are the complete player-controlled gameplay vocabulary.
