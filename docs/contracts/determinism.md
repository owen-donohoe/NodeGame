---
summary: Rules that preserve identical match state from identical setup and ordered commands across peers and replay.
sources:
  - Assets/Scripts/Game/Simulation/
verified: b2a42caf
---

# Determinism contract

## Boundary and arithmetic

Simulation is shared by live peers, local play and replay. Identical setup and ordered commands must produce identical state. All gameplay mutations belong in Simulation, through commands and ticks; views, UI and input only read state and enqueue `GameCommand`s through `InputBuffer`.

- No `UnityEngine` references anywhere in `Simulation/`, including tuning types. `GameBalance` and `BoardConfig` assets remain in Config; plain `GameBalanceData`/`BoardConfigData` cross the boundary.
- Integer-only math: no float, double or decimal. Positions, timers, weights and stats use integers; express fractions as scaled integers. Use `long` intermediates where percentage products require them.
- No wall-clock/frame APIs, including `DateTime`, `Time.time` or `Time.deltaTime`. Only tick count and entity counters measure time. Core/network drivers decide when to call ticks.
- Arrays or `List<T>` only, processed in index order. Never iterate dictionaries/hash sets to decide simulation results. Network-local tick-keyed bookkeeping is outside state and this restriction.
- Every sort has a total-order comparator with an ID tiebreaker. Combat targets sort descending fight priority, then ascending villager ID; distinct entities cannot compare equal.
- Never use Unity random, wall-clock seeds or machine-local randomness. Derive seeds from replicated values. Draft timeout fallback uses `turnNumber * 7919 + playerID * 31`, after preferring a valid parked placement, and transmits the selected legal placement. There is no stored match RNG today; any future RNG state belongs on `SimulationState`, is hashed/copied, and advances only inside `SimulateTick`.

Install read-only tuning before play through `GameSimulation.SetBalance`, `CommandProcessor.SetBalance` and factory configuration. Tick logic never mutates it. Local Editor/development playtest overrides install a modified copy, are blocked for networked matches and warn that balance identity/recorded replay no longer matches the asset.

## Canonical tick

Increment tick count, snapshot node owners and healthy Fortress resistance, emit tempo events, then:

1. Movement.
2. Combat.
3. Claiming: breach, then district health/claim bars.
4. Production: workers, Storehouse output, bank collection, auto-recruit.
5. Healing.
6. Respawns.
7. Win-check.
8. Order resume.
9. Refresh every defender's derived `nextBreacherID` after all mutations.

Never reorder passes. Insert new work at an explicit, justified point. Neighbour ownership/frontier and Fortress resistance use tick-start snapshots, never owners modified earlier in node iteration. Auto-recruit visits ascending node ID through the command processor's validated recruit path; readiness is not tempo-scaled.

Tempo timer integration uses cumulative scaled ticks 1 through t; decrement is `C(t) - C(t-1)`. Positive percentages below 100 are valid. Production carries overshoot. Schedule ticks are positive/strictly increasing, axis lengths match, percentages are positive, and production durations safely accommodate the maximum decrement. Sudden-death thresholds are positive and strictly decrease from the opening threshold. `CoreRulesValid` refuses overflowing percentage products.

## State hashing and rollback

`SimulationStateHasher.ComputeHash` folds mutable fields in fixed order: players, nodes, villagers and fixed field sequence. Register every added mutable field on state/entity types in the same change. Exclude immutable node grid coordinates/links; hash node terrain and `state.boardHash` unconditionally. `BoardHasher` covers dimensions, terrain, symmetry, slots, placements, base pools, starting values and link/path tuning.

Neutral extensions may hash conditionally only with explicit neutral defaults and distinct tags/indexes. Era tables hash nonzero index/value pairs, keeping suit/district tables separate; node district era hashes when nonzero. Missing/short player era tables mean era 0. Era additions may avoid a version bump only when existing inputs retain results and hashes.

Required extension state includes breach progress, paid respawns and `nextBreacherID` (neutral -1, not implicit zero); recruit count/readiness/repeat; Town entitlement mask; Fortress level; district health; bank stock/timer/resource alternation/collection; and latched leg duration. `Breaching` follows `Dead` without renumbering earlier states. Removed fields leave hash/copy guards together.

Hash registrations include indexed zero/false-neutral bank tags 2020–2024 and 2026–2027, district health 2028, Town mask 4012 and Fortress level 4013. Town masks never reset on capture. Ownership loss resets node recruit fields and Fortress level, never player recruit count. District health starts at zero; latched leg duration is unconditional. Balance tags cover Fortress arrays (3008–3010), Town bonus (3007), health (3021/3022), minion globals/Workshop cooldown (3023–3026); absent/zero extension values preserve neutral contributions. Historical `bonusVillagersOnClaim` remains balance data but no rule reads it.

Every new field must also round-trip through `SimulationState.CopyFrom`. Restore into the same instance because views retain its reference. Clone all mutable arrays, including nested paths and player tables; only immutable node links are shared. `SimulationStateCopyTests` populates every state/entity field by reflection and rejects missing copies, aliased mutable data and unsupported field types.

Core's rollback restore is the sole state write outside construction/commands/ticks: it restores a simulation-produced agreed state, never applies gameplay decisions. Lockstep speculates up to 20 ticks beyond missing input, then restores the last confirmed copy and replays real input or holds. Speculative ticks are neither recorded nor checkpoint-hashed; confirmed replay is.

## Gameplay rules that protect ordering

Claim bars are signed (P0 positive, P1 negative), clamp to threshold, and cross neutral before changing owner. Bodies cap at four claimers; opposing claimers freeze claiming. Rates use `long`. Frontier percentage is `100 + captureBonusPercentPerStep * clamp(friendly adjacent owners - enemy adjacent owners, 0, captureBonusMaxSteps)`, from tick-start owners. Enemy lean applies decrement multiplication; tempo scales claiming. Restore uses unopposed owner bodies and tempo without frontier/decrement bonus. Minions neither restore nor prevent Restore.

Fortress arrays contain levels 0–3; invalid cost/resistance trios disable upgrades rather than being repaired. Tick-start highest healthy aura wins per node, ties to lowest source ID. Resistance divides claim/breach rate after frontier (and claim tempo), floor 1; breach remains tempo-independent. Levels persist through health damage but reset on ownership loss.

Enemy claiming drains district health before moving the bar. Healing regenerates health in ascending node order only at a full owner-side bar. `DistrictHealth.Healthy` requires full positive-max health; zero max is always healthy. Apply it to Fortress resistance, Storehouse production, Infirmary healing/workers/respawn benefits. `OnOwnershipChanged` resets health, recruit fields, Fortress level and collection; full capture pays then clears remaining bank stock once.

Only owned healthy Storehouses produce bank stock, alternating food/materials with timer overshoot; full banks pause. `BankRules` owns locks, collection and payout. Neutral ownership, an anchored enemy body or incomplete owner bar locks collection. Stationary friendly Idle/Working collectors (including Idle minions) or remote Collect transfer one unit at a time, food/materials/metal order, skipping capped pools. Progress uses balance values, never tempo; locks/full pools pause it. Neutralisation preserves stock and cancels collection. Capture pays capped stock and clears it. Cancel resets progress only without a stationary collector.

Every resource start/gain clamps to positive caps; nonpositive caps mean uncapped. Ordinary worker completions at capacity still cycle. A metal-capped Forge consumes no material. Worker capacity remains available independently of minions. Zero cap contributions are omitted by balance hashing; negative raw caps still change the hash.

Recruit eligibility/cost comes from `NodeActionRules` and `TryRecruitCostAndCooldown`; only processor/auto-recruit spend. Successful recruits increment the persistent count and append bodies through the shared spawn helper. Town pays once per player, spending entitlement before population-cap checks, never deferring it. Runtime growth allocates a larger array, copies old entities and appends stable IDs under the cap. Views detect growth; rollback shrinkage removes excess views.

Workshop forging appends Minion using existing unit fields, shares node readiness and never auto-repeats. `IsBody` excludes minions from claim/work/Restore/breach/equip/respawn and economic enemy checks. Combat still targets them; they deal no damage and death permanently consumes them. Existing suit/HP/motion/consumed hash/copy registrations cover them.

Infirmary workers derive from state, no new counters: at most two eligible Acolytes in lowest-ID order, none with enemy bodies. `CountInfirmaryWorkers` serves both simulation and UI price. Add boosts after tempo respawn decrement; local healing uses global `tickCount % healIntervalTicks`. Paid-respawn price escalates from successful paid count, applies additive discounts with integer rounding, minimum 1; failure never advances count.

Pier departure permits retreat to `previousNodeID`; other routes wait Claiming (Idle if claimer capacity is full) until enemy Pier neutralises. Enemy Pier claim stops at neutral. Physical duration is link weight × move speed, divided with ceiling by destination own-Pier divisor and latched on departure. Ownership change cannot change an in-flight clock. Reversal pays covered distance. Dijkstra uses those physical ticks, ownership preferences on non-own-Pier legs, enemy gate neutralisation cost except at start, and lower predecessor ID for ties. Enemy Core may start/end a route but is never transit.

Move intent survives combat, gates/full nodes and interruptions. Impossible-route orders change nothing. Combat orders cannot reset attack clocks. `TickOrderResume` replans after all rule passes; resumed work/claim/travel cannot contribute until next tick.

With a valid breach channel, loss requires a new breach this tick at/above `BreachThresholdAt(tickCount)`; simultaneous losses cancel. A sudden-death drop alone never loses. Completion consumes one candidate (non-combat suit, lowest HP, lowest ID), resets progress and discards overshoot. Disabled channel retains instant arrival breach and fixed-threshold legacy win logic.

`TickEventLog` remains outside state/hash, append-only output never read by rules. Moving it onto state or reading it to decide results makes it state requiring full registration. Drivers clear/reuse it per tick; subscribers copy retained entries.

## Commands, identifiers and formats

Preserve explicit persisted district numbers: active 0–6 and 14–19; retired 7–13 remain reserved. Runtime placement, boards, draft/loadouts and current logs reject inactive values without aliases; saved-data migration alone canonicalises retired numbers. Older-version log parsing preserves its historical identifiers but cannot bypass replay version refusal.

New commands require processor case plus serializer/log acceptance in the same commit. `CommandTypes.IsKnown` is the acceptance list; unknown values are refused. Payload remains six fields/24 bytes. Recruit=5, SetAutoRecruit=6, UpgradeFortress=7, ForgeMinion=8, Collect=9. Node actions use villager -1; Recruit/ForgeMinion value 0, repeat absolute 0/1, Fortress currency 0 materials/1 metal, Collect 1 start/0 cancel. Validate before spending. Wire changes bump protocol in the same commit; command field changes need a new TICKS tag and coordinated serializer, log and round-trip/refusal tests.

## Setup and build identity

`MatchFactory.Configure` installs simulation/processor balance and path settings. `Build`/`Fill` alone build every recorded tick-0 board, including testing, live and referee. Validate board/draft through shared legality before mutation; reject with `ArgumentException`. Require exactly two Cores on different rows. Resolve starting terrain before construction: non-Pier placements raise Lake slots to Land, completion raises derived shoals, and remaining drafted Lake Piers carry Lake nodes. Ocean and DeepLake never carry nodes. Assign ascending cell IDs and link adjacent nodes. Drafted districts are Fixed at placer's era; fixed board placements use era 0. Minimal unit fixtures are not alternate recorded-match builders.

Agree immutable `MatchSetup` (map ID, board hash, simulation version, balance hash) before accepting draft packets. Host proposes, guest verifies against catalog; live wire sends no board arrays. Lobby refreshes/freezes effective local arena/loadout, refuses empty chosen slots and rechecks the frozen arena. Never resize remote canonical shapes to local rank. This validates setup without changing tick semantics.

Handshake compares protocol, simulation version and balance content hash. Protocol is wire layout; bump `SimulationVersion.Current` in the same commit as changed same-input behavior, result-affecting state or deliberate baseline re-pin. Balance-only/per-era tuning changes move content hash and need no simulation bump. Balance hashing includes ordered suit/district entries and tempo/breach/cap tuning, separately from state hashing. Coverage tests enforce added tuning fields. Validate bank groups, minion tuning, Workshop cooldown and safe schedules; preserve absent-field behavior.

Current simulation/baseline version is 5; pins are `EmptyTick100Hash = 2084609368`, `MoveAndCombat4Hash = -1780012649`. Baseline tests check pinned version equality, not unauthorised hash-only edits; coordination remains a review obligation. Never re-pin differing platform hashes to hide divergence.

Logs record identities, board and SETUP (tag 11). BOARD_V3 (tag 12, simulation 5 onward) appends symmetry to BOARD_V2 (tag 10). Preserve BOARD and BOARD_V2 layouts; unsupported fields need newer tags. Replay refuses another simulation version and inconsistent setup/board/balance. Referee additionally requires catalog board identity and content-addressed balance export; export every shipped balance and check client/catalog parity. Static configuration prohibits concurrent matches in a process; referee serialises replay behind a lock.

## Checkpoints and verification

At positive tick indices divisible by 50, hash after simulation: first index 50 means state count 51. Send hash in next tick-input packet. `CompareHash` compares nonzero received hash to the latest local hash; packets carry no checkpoint tick. Mismatch logs DESYNC and fires `OnDesync`, identifying divergence rather than its cause.

Observers cannot mutate results: `CommandsApplied` reports nonempty P0-then-P1 batches before application with pre-tick count; `HashComputed` uses post-tick count; `ConfirmedTickSimulated` fires after every confirmed tick including rollback replay, never speculative ticks.

Add behavior/hash/copy tests for simulation changes; run `dotnet test dotnet/NodeWar.sln`, determinism guards and pinned cross-platform baselines. Verify a two-peer match beyond a checkpoint with exchanged hashes and no DESYNC; local play cannot establish peer agreement. Compile checks and simulation tests do not establish Unity scene/prefab wiring.
