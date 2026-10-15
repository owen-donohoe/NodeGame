---
system: bots
summary: The shipped scripted opponent and a repeatable headless rig for evaluating its behavior across maps and balances.
sources:
  - Assets/Scripts/Game/Input/BotPlayer.cs
  - Assets/Scripts/Game/Input/BotSuitChoice.cs
  - dotnet/NodeWar.BalanceRig/
verified: a1da5182
---

# Bots

## Purpose
Provide a local opponent through the same command path as a human, and run that opponent headlessly for repeatable balance diagnostics. Rig measurements describe this policy's performance; they do not establish optimal play or player fun.

## Interface
`BotPlayer(state, InputBuffer, playerID, defaultLinkWeight, GameBalanceData)` binds a policy to one seat; an overload uses default balance. `Evaluate()` reads current state and appends `GameCommand`s with `issuedOnTick = state.tickCount`. It never calls `SimulateTick` or directly mutates state. Drivers invoke evaluation before processing that tick's inputs.

`BotSuitChoice.Choose(state, balance, playerId, nodeId)` returns an affordable, drafted, permitted combat suit with era stats, or None. It prefers Warrior; otherwise the lowest enum-valued eligible suit wins. This is a policy choice, not an equip eligibility override.

`RigSetupLoader.Load(balancePath, boardPath, loadout, v2Overlay = false)` resolves exported tuning, board, pools and mirroring. `LoadShippedBoard(mapId, setup)` uses the shared catalog; `LoadBalance` checks filename/hash; `ParseLoadout` validates district choices. `RigSetup` retains source/effective balance hashes and map/mirror data.

`MatchRunner.Prepare(setup, seed, draft = null)` returns `PreparedMatch { setup, draft, players, seed, pairID, seat }`; `RandomDraft` makes legal placements. `SwapSeats(prepared)` mirrors the same draft/board and swaps player setups. `Run(setup, seed, capTicks, inputDelay, trace = null)` or `Run(prepared, capTicks, inputDelay, trace, RunHooks)` produces a rig `MatchResult`. `RunHooks` optionally enables timeline, observes command batches before application and state after ticks. Observers must remain read-only.

Result contains seed/pair/seat, ticks, winner/capped flag, breaches/times, final resources/ownership/population, soldier/Barracks diagnostics and optional timeline. `TimelineMetrics.ObserveCommands(appliedTick, batch)`, `ObserveTick(postState, TickEventLog)` and `Complete(result)` collect measurements. `Report.WriteCsv`, `Row` and `Summary` produce aggregate output; `TimelineReport` produces windows/flips CSV; `Snowball.Evaluate` reports eligible lead-to-win samples and exclusions.

CLI: `dotnet run --project dotnet/NodeWar.BalanceRig -- --matches 100 --seed 1 --cap 6000 --out out/rig.csv`. Options include `--loadout Barracks,...|none`, `--delay`, `--swap-seats on|off`, `--balance file.json`, `--board file.asset`, `--v2-overlay` and `--trace SEED`. See the rig README for invocation/column definitions.

## Connects to
- `commands`: bot output enters `InputBuffer` and receives ordinary processor validation.
- `simulation`: reads state, uses shared pathfinding and advances only through the runner's command/tick loop.
- `economy`: suit affordability and worker allocation depend on supplied tuning; population metrics distinguish bodies/minions.
- `board`: rig installs `MatchFactory.Configure` and builds with `MatchFactory.Build`, using catalog terrain/identity.
- `draft`: rig generates legal placements; live bot draft orchestration remains with `DraftManager`.
- `match-flow`: `TickRunner.SetBot` evaluates the local opponent before ticks.
- `cloud`: exported balances are the same content-addressed inputs used by referee replay.
- `tooling`: `NodeWar.BalanceRig.Tests` guards setup, repeatability and measurements.

## Invariants
Live and headless bot source is linked, not copied. Apply commands P0 then P1, preserving each player's issue order. Match cap is a capped outcome, never an invented winner. Runs execute sequentially because simulation configuration is process-global. Randomness in pre-match rig drafting does not become tick-time randomness.

## How it works
Each evaluation clears per-pass reservations, grows per-villager cooldown arrays as population grows and resets path-cost caches. Priority is Core emergency, Core intercept, paid respawn, node defence, expansion, military, economy. Earlier priorities reserve villagers so later ones cannot issue competing orders. Path costs rank candidates; cooldowns reduce repeated movement commands. Military equips through `BotSuitChoice` and sends groups; economy redistributes ordinary workers.

The rig links `BotPlayer`, `BotSuitChoice` and `InputBuffer` from Assets into a .NET console app. It installs balance/path settings, builds the same sparse starting board as live/referee matches, creates two bots and repeatedly evaluates, delays/applies commands, ticks and observes until game-over/cap. Arbitrary legacy boards without terrain are refused.

`--matches N` means N seeds. Seat swap defaults on: each seed produces two rows sharing `pair_id`, original seat first, from one draft. Shipped maps use declared symmetry (hourglass is top-bottom reflection); explicit board files use a half-turn. Equal paired outcomes are not promised because ID tiebreaks can favour a seat.

Balance filename must match its source hash. Print source/effective hashes separately when applying the optional overlay; only effective tuning is played. Reports carry schema and starting-board identity so incompatible boards are visible.

For `name.csv`, emit match summaries, `name.windows.csv` and `name.flips.csv`. Windows are ten seconds of ticks keyed by application tick, with a partial final window. Commands count records, including refused actions. Idle metrics count living unconsumed Idle bodies, excluding minions; living minions remain in final population totals. Flips are complete ownership changes, including neutralisation; recapture means a claim by someone other than the last non-neutral owner.

Opening contest is non-Core CombatStarted in post-ticks 1–600. Lead is P0 minus P1 non-Core ownership at tick 1200. Snowball asks whether that leader wins, excluding ties, capped games and matches ending before measurement; report denominator and each exclusion, or n/a.

## Decisions
- A fixed scripted opponent supplies a stable baseline before any learning policy exists.
- Paired seeds expose seat effects while keeping draft randomness comparable.
- Read-only timeline observers collect diagnostics without adding simulation state.

## Pressure points
The bot does not Recruit, SetAutoRecruit, upgrade Fortress, forge minions or remotely Collect. New-district rig runs therefore test repeatability, not their strategic value. Warrior preference and scripted worker/attack heuristics can dominate balance conclusions. Static simulation settings block in-process parallel matches. The runner is a balance harness, not a Gymnasium/RL action-observation environment.

## Change recipes
- Improve policy: emit validated commands, preserve priority/reservations and add repeatable scenarios; test both seats and relevant era/loadout affordability.
- Add metrics: observe applied commands/post-tick state, define denominators and minion handling, extend schema/output/tests together.
- Compare tuning: use content-addressed exports, paired seeds and identical cap/delay/map; retain hashes and capped/excluded counts.

## Tests
`NodeWar.BalanceRig.Tests` covers setup validation, repeatable runs, paired setup and timeline diagnostics. Simulation tests guard action legality; suite counts/commands live in `docs/skills/run-dotnet-tests.md`. Headless results do not verify live Unity pacing, controls or presentation.
