---
type: Direction
title: Terrain, maps, districts, recruiting, banks, magic and the bot — design draft
description: A smaller premade-map board with land, lake and ocean terrain; piers and lighthouses; claim restore; recruiting Villages and one-off Towns; banks and minions; a magic well; a revised district roster; and the bot, replay and balance-rig work that measures it. Design and intended heuristics, not implementation.
tags: [game-design, maps, terrain, districts, economy, bot, replay, balance]
generated: { by: claude-sonnet-5-5, at: 2026-10-07T00:00:00Z }
status: draft
# No `sources:`. Describes work that has not happened. Everything here that
# touches Simulation/ starts in plan mode and runs the determinism guard, and
# all of it is meant to ship under one SimulationVersion bump. Every number is
# a placeholder to be set from the balance rig. Where a rule is marked
# "proposed" or "assumed", the author did not hear it confirmed.
---

# Terrain, maps, districts, recruiting, banks, magic and the bot

Written 2026-10-07 from a design conversation. It records what was decided, what
was only proposed, and what the code was found to do today. It is direction, not
a plan: it does not say how to build any of it.

Line references are to `Assets/Scripts/Game/Simulation/GameSimulation.cs`.

## 1. Facts from the code that shaped the design

- **The economy is not the bottleneck; bodies and travel are.** A worker yields
  food every 40 ticks (4 s). A Warrior (2 food, 1 material) costs about 13
  worker-seconds. One farm worker fills the 30-food cap in about two minutes. A
  lone claimer takes 29 s per node, a villager start count is 3, and the cap is 25.
- **Metal has no sink.** Nothing in `Simulation/` spends it; only the Forge makes it.
- **Resources are global pools,** so where a Farm sits matters only for travel and
  vulnerability.
- **Camp, Barracks and Arsenal differ only in which suits they permit.** They have no
  stats of their own. Shrine, Sanctuary and the Medic suit overlap as heal-ish things.
- **Market is strictly worse per worker than Farm or Mine** (one resource per 50 ticks
  against 40 or 50).
- **Using a node requires owning it.** Work and equip check `ownerID` (:706, and
  `CommandProcessor` :211). Ownership is lost only when the claim bar crosses zero
  (:565).
- **Nothing restores a partly pushed node.** A node pushed to bar 100 above zero stays
  owned at bar 100 indefinitely: owner villagers work and never push the bar back up.
- **The village bonus is repeatable.** `bonusVillagersOnClaim` is granted on every
  completed claim (:797) and never consumed, so a village that flips grants +2 to each
  capturer each time, up to the cap. Losing a village does not remove the villagers.
- **Post-combat resume can abandon an order.** A survivor with a remaining path may stop
  and work (:1027) or start claiming (:1052) at an intermediate node, and the old path
  is reused without re-planning (:1020).
- **The 25-villager cap is unreachable on a small map.** With two villages the realistic
  population is 5 to 7.

## 2. Vocabulary

| Term | Meaning |
|---|---|
| **Terrain** | The board layer: land, lake, ocean |
| **District** | What is built on a cell (Farm, Pier, Lighthouse…). The code already says `DistrictType`. |
| **Node** | A traversable cell: land, or a lake cell that has a district |
| **Slot** | A position where a district can go |
| **Link** | An edge carrying an interaction between two nodes |

A district declares which terrain it may be placed on. Land holds ordinary districts.
Lake holds only Piers and Lighthouses. Ocean is the boundary and is never a node. A
lake cell without a district is not traversable.

## 3. Maps

- **Premade, symmetric, 14 to 20 nodes** (16 is the general case). Symmetry gives each
  side an identical map; a swap-sides win-rate check, run per map, verifies it.
- **The terrain rides in `BoardConfigData`,** so match logs and replays carry it.
  `MatchFactory.BuildNodes` and `GridEdges` change to skip ocean and lake cells and to
  build a lighthouse's single entrance edge.
- **A land route between the cores must exist without any pier,** checked at authoring
  time, because a deck may omit piers.
- **Lighthouse edges stay two-way.** The retargeting rule that turns a mid-edge
  villager around needs the reverse edge, so one-way edges are out. This has not been
  checked against every consumer of edges.
- **Ranked rotation.** One fixed daily track, arena-specific, rolling over at **3 am true
  Eastern time**. The server computes the day number: subtract 3 hours from Eastern
  local time, take the calendar date, and index the track by days since a fixed epoch
  modulo the track length. Daylight-saving transitions happen at 2 am, so 3 am exists
  exactly once every day and the day number never skips or repeats. Clients only
  receive a map ID. The Cloud Code runtime must have time zone data; verify this.
- **The map is fixed at pairing** and travels in the handshake and the log. The board
  should be covered by the balance/handshake hash so a modified client cannot change it.
- **Mixed-arena matches use the lower arena's pool.** A player therefore has at most two
  candidate maps on any day: their own arena's, or the next arena down if they are the
  higher of a mixed pair. Both are known in advance.
- **Map UX.** A scrollable map-info view (like the "i" in Brawl Stars) shows both
  candidates and upcoming maps. A deck swap is offered when a lower arena becomes
  queue-able, before any opponent exists, so there is no blind-commit or counter-pick
  problem. Each map remembers the last deck used on it. Loadouts keep three savable
  presets.
- **Local play** uses maps the host has access to. Because maps ship in the build and the
  handshake already compares balance, the guest always has the data.
- **One `MatchSetup` record** (map ID and rules) is authored by the ranked server, a local
  host or the later level-select screen.

### Draft placement UX

When a district is picked, highlight every legal cell and tint the others slightly, with
minimal transition. The highlight and the draft validator must share **one legality
function** so they cannot disagree. A faint outline or pulse on legal cells, as well as the
tint, keeps it readable for colour-blind players. Hovering a legal cell can preview link
effects (section 9). Ocean is never legal.

## 4. Claiming, restore, capture and routing

- **Restore.** A node still owned by P but below full restores toward full while P's
  villagers are present and no enemy is, and workers keep working meanwhile. Ownership is
  lost at the zero crossing, as today. Call it *restore* so it does not collide with HP
  healing (Shrine, Medic). Proposed: idle combat-suited villagers also restore, and the
  rate equals claim speed per present villager, capped at 4.
- **Capture speed from nearby friendly nodes.** Direction agreed; numbers not set. Keep it
  a small capped integer bonus that counts the defender's adjacency as resistance, so it
  is a net frontier contest. It replaces Watchtower's claim boost. Adjacency is graph
  adjacency, so a lake with no pier gives nothing across it. Breach speed against a core
  scales the same way. A raid that takes a node also removes the bonus from its
  neighbours, so cutting a chain is a real tactic.
- **Snowball check.** The metric is how well the node lead at 2:00 predicts the winner. If
  it is near 100% the cap is too high.
- **Sticky orders.** An order persists until arrival or player override. The final target
  stays set through any interruption (a fight, a gate) and a single resume step re-plans
  from the current node using current ownership. A villager stops to work or claim only
  at its destination, or when it has no order. This also removes the bot's per-villager
  cooldown workaround.
- **Attack versus claim** (proposed; the suit rule is new):

| Verb | Acts on | Outcome | Speed |
|---|---|---|---|
| **Claim** | The ownership bar | Occupation: the node flips, the minion dies as a by-product, the capturer takes the bank | Slow (29 s alone, 7 s with four) |
| **Attack (raid)** | A structure's HP: minion, magic well, Fortress | Destruction: loots the bank and removes the upgrade, node stays the enemy's | Fast, about an edge traverse |
| **Breach** | The core's breach bar | Wins the match | Existing |

Any villager can claim. Only combat-suited units attack structures, automatically on
arrival when no defender is present (the same trigger as a breach channel). The bank lock
applies to either. This is a behaviour change: soldiers claim today, and the bot's
Expansion uses non-soldiers.

## 5. Terrain districts: Pier and Lighthouse

- Piers and Lighthouses are chosen from the deck's node slots.
- **Pier.** Traversable by anyone while unowned. While owned it blocks the enemy, who must
  neutralise it (global 4× decrement) before continuing, and then **auto-resumes** its
  move. With the global 4× a lone raider opens an owned pier in about 7 s and four in
  under 2 s, so a garrison is the real defence; strengthening comes from the Fortress
  node ability. Owned piers are 0.5× highways for the owner. The pathfinder treats an
  enemy-owned pier as passable at a cost proportional to its capture time.
- **Lighthouse.** A one-edge dead end that can be stood on and claimed. Vision (see
  fog below) and support for adjacent piers.
- Drafted districts start unowned, so there is an early race to claim your pier.

## 6. Resources

- **Food and material:** generic balancing currencies. Cheap, rarely limiting.
- **Metal:** the advanced, endgame-leaning resource, made from material at the Forge.
  It buys machines, upgrades, a powerful suit and the minion upgrade. Designing for
  intention-changing choices, not required spending.
- **Magic:** a scarce strategic budget (section 8).
- **Pools stay global.** Storage is global and sources are spatial. A second-base
  scheme was considered and rejected as hard to track and early-attack-hostile.

## 7. Banks, Storehouse and minions

- **Bank is a mechanic,** not a district.
- **Ordinary districts do not bank.** A villager is always present, so output goes
  straight to the pool, and a captured Farm holds nothing stealable.
- **Storehouse** (replacing Market, renamed to avoid confusion with Exchange) banks by
  default, needs **no worker**, and has a built-in minion. Slow, bodyless income.
- **Minion.** A metal-bought upgrade lets another district work without a villager. Its
  output then pools in a bank, which is stealable and lockable. A minion is fixed in
  place, has HP, and is killable. It dies when the node flips, and the capturer takes the
  bank.
- **Collection** is by tap from anywhere or by a villager standing on the node. It is a
  **progressive timer**: +1 at a time until empty, about one edge traverse (16 ticks) for a
  full bank of 5, so an attacker gets whatever has not yet moved. The same dwell applies to
  passing villagers, otherwise a walking villager would be a snipe loophole.
- **Lock.** A bank cannot be collected while an enemy is on the node or the claim bar
  leans against the owner, until restored.
- **Consequences.** Automation trades a body for attention and risk. Vision matters for
  economy: a Lookout one edge ahead gives time to start collecting. A one-tap
  collect-all is proposed.
- **Structures with HP.** The minion, the magic well and possibly Fortress need one
  shared mechanism: a structure that attackers channel against when no defender is
  present, modelled on the breach bar but without consuming an attacker. Build it once.

## 8. Magic and the well

- A **preplaced, destructible district** at the centre.
- **Pool:** 100 at the start. +50 at 2:00 and again at 3:00, capped at 100, with nothing in
  between. (These match the existing tempo-stage ticks 1200 and 1800.)
- **Draw:** 5 per second, requires a villager at the well, any time unless the pool is at
  0. Personal capacity 50.
- **Sudden death:** 1 per second moves from each player into the well. A full bar drains in
  50 s, so unspent magic is gone by about 4:50 unless held at the well, where a drawer
  gains five times as fast as they lose.
- **Budget feel.** Supply is up to 200 through 3:00, so about two full bars per player if
  split evenly. Two players × 50 = the 100 pool cap, so the whole stock fits the well
  exactly. One bar is intended to buy either one big attack or several small expansion
  uses, so saving is a real choice.
- **Why it is structured this way.** One structure gives scarcity (a finite shared pool),
  anti-hoarding (drained magic leaves your bar) and an anti-turtle clock (sudden death
  forces both players to the middle).
- **Risk.** Whoever holds the well at sudden death gets a large swing, making the last
  minute a king of the hill. Cap the drain rate so it cannot end a match instantly.
- Open: what happens to stored magic and to the drain if the well is destroyed, how the
  well is destroyed, and what exactly magic buys. Expansion uses should be small and fixed,
  and the attack use should be close to game-deciding. All numbers need tuning.
- Magic is display-only in the HUD today and not a simulation resource.

## 9. Population: Village, Town and recruiting

- **Village** is a **recruiting district** with no claim bonus. Cost and cooldown are both
  `base + step × N`, where N is the player's **match-long recruit count** (per side, not
  population-based, because a population-based count lets everyone but one villager die and
  respawn cheaply, and does not help a player who is down after a breach). Base 6 with a
  step of 3 or 4 is the starting point. Assumed: the counter is separate from the paid
  respawn counter, and the formula applies to both cost (food) and cooldown (seconds).
- **Pace if fed** (projected, not measured): a step of 3 gives about +5, +7 and +11 recruits
  at 1, 2 and 4 minutes of continuous running; a step of 4 gives about +4, +7 and +10.
- **Because cost equals cooldown, a running Village drains exactly 1 food per second,**
  the output of four farm workers. Food is the real limiter, so the opening order is
  Farm first, Village second. Needs rig testing.
- **Recruiting needs a `Recruit` command.** An auto-repeat toggle on the Village follows
  the Forge allocation pattern and avoids a tap every few seconds.
- **Town.** +2 villagers, **once per player**, on claim. Taking the enemy's Town also pays
  the raider +2, so Towns are targets and placement matters (near your core is safe,
  forward gains area control but risks the raider getting +2). After the bonus a Town does
  nothing, which makes it a dead slot; either accept it as a cheap fast-start pick or give
  it a small steady effect.
- **Slot setup is 5 node slots and 3 suit slots.** Progression (my reading, to confirm):
  early arenas have 2 node slots with the Village, Farm and Mine preplaced; then 4 node slots
  with only the Village preplaced; then the full 5 and 3. At the full setup a body source is
  effectively chosen, not guaranteed.
- **The population cap** (25) is unreachable on a small map and should be sized to the map.

## 10. District roster

| Role | District | What it does |
|---|---|---|
| Bodies | Village | Recruiting engine (section 9) |
| Bodies | Town | +2 once per player on claim |
| Currency | Farm, Mine | Food, materials; a Farm beside a lake could run faster so terrain matters |
| Metal | Forge | Material to metal; allocation becomes a real decision once metal has a sink |
| Fight | Barracks (merged) | Equips every drafted suit; replaces Camp and Arsenal. Side variants upgrade elsewhere (for example a Machinist from a Warrior) |
| Reach | Outpost | Owner's villagers respawn here instead of at the core. Placement rule: not adjacent to enemy territory |
| Defence | Fortress (reworked Rampart) | Spend material or metal to raise capture resistance on itself and adjacent nodes. Highest aura wins instead of stacking, diminishing returns on spending, no dormancy. Can add extra "health" per material as a node ability |
| Recovery | Infirmary (Shrine + Sanctuary) | Heals HP and speeds respawn on and at it; a raid target |
| Info | Lookout (replaces Watchtower) | Vision radius and early warning |
| Flex | Exchange | Converts a surplus into what you lack |
| Income | Storehouse | Bodyless, slow, banked income (section 7) |
| Terrain | Pier, Lighthouse | Section 5 |
| Objective | Magic well | Section 8 |

Fog is a view-layer function over state with no simulation cost. It conflicts with solo
hotseat testing (needs a debug toggle), and the bot must obey it. A Scout is the natural
fog-scouting unit. Vision radii proposed: 1 for ordinary owned nodes, 2 for a Lookout, 3
for a lighthouse across water.

## 11. Making interactions readable

- Prefer qualitative effects to percentages ("the ore flows straight in" over "+20%").
- Put the effect on the edge: draw the link, animate what flows, badge the receiving node.
- At most three interaction types: *supplies*, *aura* and *chain* (the capture bonus).
- Make every link large enough to see (about 50% or a qualitative change), and cap how
  many exist so each is learnable.
- Preview before placing. Interactions run along edges, not grid adjacency.
- **A deck without any one optional node must still have a viable plan.** The late game
  offers three paths (mass bodies, machines via metal, magic burst) and a deck commits to
  one. The rig tracks each node's inclusion rate among winning decks; above about 90% it
  is mandatory and is flagged.

## 12. Defence, attack and sports analogies

Today node count has no direct win value; only breaches win, so expansion pays through
economy and army only. The defender is strong: free respawn at the core, 2× path cost on
enemy ground, Rampart, and attackers consumed per breach. Sudden death is the only
global anti-turtle clock. Analogies worth keeping:

- Rugby league's tackle count: attack must progress or turn over (the breach decay is the
  same idea).
- Shot and play clocks (the global sudden death).
- Soccer's counter-attack: an all-in attack leaves the core open, and a breach needs an
  empty core.

Healing and respawn fairness is mostly **respawn time against how long the winner's push
takes.** A lone breacher needs 60 to 80 ticks against a 50-tick respawn. Measure the
fraction of won fights that become a breach within N seconds; 20 to 40% feels right, near
0% means defenders are too strong. The assumption that the resource gap cannot be recouped
inside the recovery time should be tested, not trusted.

## 13. The bot

The scripted priority list in `BotPlayer.cs` hard-codes an expansion order and a wolfpack
of 3, so it would not survive a rotating map pool. Recommended structure:

1. **Delayed perception.** The bot sees the world d ticks ago, through fog and limited
   attention. Delay is applied once, here.
2. **Forecast.** Movement, claims and breach progress are deterministic, so everything
   already in flight is extrapolated forward by d. Delay then costs only information about
   new enemy decisions, which a human also cannot predict.
3. **Strategic layer (every 1 to 2 s).** Intents (Expand, Defend, Attack, Rebalance
   economy, Retreat), each scored as value × feasibility − risk. Plans reject any option
   whose time margin is below reaction delay + travel + a caution buffer, so thin margins
   mean act now ("cut the food and defend"). Node value is time-weighted: production
   discounted as caps fill, position weighted by phase, plus frontier value from the
   capture rule.
4. **Assignment.** Match villagers to intents by travel ticks, keep a defensive reserve,
   group claimers in 3 to 4, and keep an intent for a commitment period.
5. **Execution.** Turns assignments into commands through an action budget. Threats
   pre-empt the budget and get a shorter reaction time. Each command is revalidated against
   current state before sending, and stale ones are dropped.

- **Personas** are weight sets (rusher, expander, turtler). A persona × map round-robin
  should be roughly non-transitive; if one persona dominates, the game is solved badly.
- **Skill knobs:** reaction delay, action budget, mistake rate, caution, and whether it
  obeys fog.
- **Rollout planning** (clone via `CopyFrom`) suits narrow tactical calls such as "can I
  breach before they respawn?".
- **The bot logs the winning intent and why,** and the replay viewer shows it.
- **The intended playstyle is a written target timeline** (for example first claim by
  0:10, third node by 0:45, first army around 1:30) taken from the pacing design, not from
  the bot. Measured bot timeline against that spec shows whether it models a good player.
- **New bot actions:** Recruit, Collect, Attack, Draw, and a magic reserve setting.
- RL stays later, with the scripted bot as its sparring partner, as in [direction](direction.md).

## 14. Replay and measurement

- `MatchReplay` is a referee today: it re-runs commands and checks hashes. A **viewer**
  adds periodic snapshots (via `CopyFrom`) for scrubbing, a mode that bypasses the live
  input buffer, a command-timeline overlay and the bot-decision overlay.
- **Replay as the playtest substitute.** Record your own matches and compute inter-command
  gaps and idle-villager time from the log to measure "an action every 5 to 10 s".
- **Decision rate ≈ free agents ÷ task duration.** At 10+ villagers per-unit orders
  overload the player, so the UX batches (group or lasso, one tap to the target).
- **Rig metrics to add:** a timeline per match (node flips, idle villagers, commands), the
  decision demand curve per 10 s window, how often the opening is contested, snowball
  (node lead at 2:00 against winner), node inclusion rates, the fight-to-breach
  conversion above, and variants of the starting villager count (3, 4, 5) and the recruit
  step (3, 4).
- Baseline the current 28-node game with the new timeline metrics before changing the
  board.

## 15. Simulation impact

- One `SimulationVersion` bump covers all of it, and old logs stop replaying.
- **New hashed state:** terrain, bank contents and timers, minion HP, recruit counters and
  cooldowns, magic and the well pool, Town claim flags, structure HP, collect progress.
- **New commands:** `Recruit` and `Collect`, an auto-recruit toggle, and Attack handled by
  suit. Each needs a `CommandProcessor` case and an `InputSerializer` change in the same
  commit.
- The tie-break fix from [tempo-and-breach-proposal](tempo-and-breach-proposal.md) still
  applies.
- The repeatable village bonus (section 1) is removed by the recruiting design.

## 16. Assumptions to confirm

1. The recruit counter is separate from the paid-respawn counter, and `base + step × N`
   applies to both food cost and cooldown seconds.
2. The slot-progression reading in section 9.
3. The attack/claim distinction by suit (section 4).
4. Restore: idle soldiers count as restorers, at the claim rate.
5. Capture-speed numbers, and that the defender's adjacency counts as resistance.
6. Sticky orders: a survivor stops to work or claim only at its destination.
7. Outpost is accepted with the placement rule.
8. The magic well's destruction rules and what magic buys.

## 16a. Decisions, 2026-10-07

Confirmed by the user after this draft was written. Where these disagree with the
sections above, these win.

- **The current 28-node board is removed, not kept and not baselined.** It is not a
  useful benchmark. The rig's timeline metrics are still wanted, measured on the new map.
- **Rename for meaning.** While terrain lands, `node`, `district`, `terrain` (and
  `slot`, `link`) are renamed system-wide to match the vocabulary in section 2.
- **Recruit:** a match-long counter separate from paid respawn; food cost and cooldown in
  seconds are both `6 + 3 × N`.
- **Restore:** as section 4, idle combat-suited villagers included, claim rate per present
  villager, capped at 4.
- **Capture bonus:** your adjacent owned nodes minus the defender's, clamped to 0 to 2
  bonus steps, applied to claim and breach speed. Placeholder numbers for the rig.
- **Sticky orders:** a villager stops to work or claim only at its destination, or with no
  order. When an order is interrupted, its path line changes colour and fades; it does not
  disappear.
- **Town** is a dead slot after its one-off bonus.
- **Merges** (Barracks from Camp and Arsenal, Infirmary from Shrine and Sanctuary, Fortress
  from Rampart, Watchtower retired in favour of the capture bonus) land with the core
  rules. Storehouse, banks, minions, structure HP, the attack verb and pier blocking follow
  after.
- **Deferred:** the bot, fog and Lookout, the replay viewer, ranked map rotation, the
  map-info screen, magic and the well, and the lighthouse.
- **First map:** an hourglass. Cores at either end; the left route crosses a narrow gap
  only by a pier, the right route is a wider land pass, and open lake lies between them.
  Mirrored so both sides see the same board.
- **One `SimulationVersion` bump** covers the terrain and the core rules; nothing deploys
  between them. Cloud Code is redeployed after they land.

## 17. Order of work

1. Add the timeline metrics to the balance rig and baseline the current board.
2. Make maps and terrain data-driven, with the legality function.
3. Build the replay viewer and the decision-timeline analysis.
4. Add bot personas and the round-robin, then the new districts and rules above under a
   single version bump.
