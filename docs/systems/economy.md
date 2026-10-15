---
system: economy
summary: Resource production, district effects, recruitment, minions and bank collection inside deterministic match rules.
sources:
  - Assets/Scripts/Game/Simulation/GameBalanceData.cs
  - Assets/Scripts/Game/Simulation/GameSimulation.cs
  - Assets/Scripts/Game/Simulation/BankRules.cs
  - Assets/Scripts/Game/Simulation/NodeActionRules.cs
  - Assets/Scripts/Game/Simulation/DistrictHealth.cs
  - Assets/Scripts/Game/Simulation/PierGate.cs
  - Assets/Scripts/Game/Simulation/CommandProcessor.cs
  - Assets/Scripts/Game/Simulation/SimulationState.cs
verified: a1da5182
---

# Economy

## Purpose
Turn territory and living workers into resources, bodies and defensive effects. Shared read-only eligibility keeps UI prices and automatic actions aligned with authoritative command validation.

## Interface
`GameBalanceData` holds globals plus `SuitStats[] suitStats` and `DistrictStats[] districtStats`, keyed by type and era. `GetSuitStats`/`TryGetSuitStats(type, era)` and `GetDistrictStats(type, era)` fall back to era 0; missing suit stats refuse equip, missing district stats give zero effects. `CanEquipSuitAtNode`, `IsCombatSuit` and `GetUpgradeCategoryForDistrict` centralise role/category rules. Player era arrays index enum values; short/missing arrays mean era 0.

`NodeActionRules.CanRecruit`, `CanForgeMinion`, `CanSetAutoRecruit` and `CanUpgradeFortress` expose eligibility to commands, automatic passes and UI. Recruit/forge checks return a boolean with an out refusal enum; balance helpers supply cost/readiness. `CountPopulation` includes dead but excludes consumed units; `IsBody` excludes Minion. `HasLivingEnemyAtNode` tests bodies for economic denial.

`GameBalanceData.TryRecruitCostAndCooldown`, `TryForgeReadyTick`, `PaidRespawnCost`, `ClampResource`, `HasResourceRoom` and `AddResource` supply common arithmetic. `CoreRulesValid`, `FortressStatsValid` and `BankTuningValid` reject unsafe/incomplete tuning.

`BankRules` exposes `Total`, `Locked`, `HasStationaryCollector`, `CanCollect`, `CollectionState`, `TickCollection`, `CanProduce`, `ProductionTicks`, `HasWorkerSlot` and `PayBank`. `CollectState` is Empty, Neutral, Locked, StorageFull, Waiting or Collecting. Node structs returned by collection must be written back by simulation callers; UI only reads.

`DistrictHealth.Healthy(state, node)` gates passive effects using installed simulation balance. `PierGate.IsEnemyPier` and `BlocksDeparture(state, villager, nextNode)` share gate eligibility. `GameSimulation.CountInfirmaryWorkers(state, nodeID, playerID)` is the common worker count for simulation and paid-respawn UI pricing. Writes belong to `CommandProcessor` and tick passes.

## Connects to
- `simulation`: stores resources, district/bank/cooldown state and executes ordered passes.
- `commands`: actions spend through shared eligibility; Collect and allocation control deterministic work.
- `board`: `MatchFactory.Configure` installs balance and initial pools are cap-clamped.
- `draft`: placements determine district type and placer's era.
- `match-ui`: reads shared action rules, bank status and prices, then queues commands.
- `progression`: supplies owned/loadout eras; simulation reads era tables, never inventory or cosmetics.
- `cloud`: exported balance content must match the client's balance hash for referee replay.
- `tooling`: balance validation, hash coverage and release-content checks guard tuning exports.

## Invariants
Food, materials and metal are the only simulation resources. Positive caps clamp starts and every gain; nonpositive caps mean uncapped. No UI copy of eligibility or prices. Ordinary worker capacity remains independent of minions. Every mutable economic field participates in state hashing/copying; tuning participates in `BalanceHasher`.

## How it works
Farm/Farmer and Mine/Miner pay one resource directly into player pools. Forge/Smelter consumes one material for one metal only with positive `materialAllocation`; metal capacity prevents spending. Up to two workers per node count by default. Combat suits never work; arrival assigns production suits and departure strips them. Worker timers carry tempo-scaled overshoot; completions at capacity still cycle.

Only owned, fully healthy Storehouses bank stock: alternating food/materials, default capacity 5 and 80 ticks/unit. Full/ineligible production pauses. Collection is locked by neutral ownership, an incomplete owner-side bar or an anchored enemy body. Stationary friendly Idle/Working collectors (including Idle minions), or remote Collect, transfer food then materials then metal, skipping capped pools. Default progress is 5/tick with 16/unit, independent of tempo. Lock/full pools pause progress; emptying stops collection. Cancel clears progress only without another stationary collector. Neutralisation preserves stock and cancels collection; full capture pays capped stock once then clears bank/timer.

Fortress, Storehouse and Infirmary default to 3000 district health, regenerating 17/tick only at a full owner-side bar. Enemy claim pressure drains health before claim. Positive-max passive effects require full health; zero-max districts are healthy. Ownership changes reset health, recruit readiness/repeat, Fortress level and collection.

Village Recruit costs `6 + 3 * recruitCount` food by default; cooldown is the price in seconds. Success appends a base body and increments the player's persistent count. Repeat retries after production in node-ID order; readiness is not tempo-scaled. Town pays each player two bodies on their first full claim, spending entitlement before the population-cap check; `townPaidMask` survives ownership changes.

Workshop forging costs 3 metal with a per-era 30-tick cooldown, reusing `recruitReadyTick`, and never repeats automatically. Its 8-HP minion moves at duration 2, collects and counts toward population. It never claims, restores, works, equips, breaches, respawns or damages enemies; combat can kill it permanently. Enemy minions do not deny economic actions.

Fortress upgrades sequentially through levels 1–3, paying materials or metal. Healthy auras cover self and owned neighbours; highest resistance wins, lowest source ID breaks ties. Infirmaries heal locally and count at most two lowest-ID eligible Acolytes for additive passive-respawn boosts and paid-respawn discounts. Pier accelerates entry for its owner; enemies may only retreat to their previous node until neutralisation.

## Decisions
- Per-era stats keep variant tuning in one balance rather than branching rules by arena.
- Shared helpers expose decisions without giving presentation permission to mutate state.

## Pressure points
Economic logic spans tick passes, command mutation and helpers; ownership reset and health gating are easy to apply incompletely. Forge allocation is a gate rather than a resource reservation. Historical inactive stats remain serialised/hashed. Static installed balance constrains parallel matches. More district actions would benefit from a uniform read-only action description while preserving explicit deterministic writes.

## Change recipes
- Tune/add stats: use the suit/district era entry, update `BalanceHasher`, validation, client asset and server export; verify release-content parity.
- Add an action: share eligibility with both UI stacks and automatic passes, then implement command spending; hash/copy/init new state.
- Add gains/effects: cover all cap paths and ownership resets; health-dependent effects use `DistrictHealth.Healthy`.

## Tests
Simulation economy, bank, recruit, Town, Fortress, Infirmary, minion and cap/tempo tests run through `dotnet/NodeWar.sln`. CoreRules/BankRules scripted witnesses also replay through lossy lockstep and match logs. BalanceHasher and ReleaseContent tests guard tuning coverage/export parity; Unity action-panel rendering remains manual.
