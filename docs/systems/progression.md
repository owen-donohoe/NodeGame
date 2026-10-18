---
system: progression
summary: Defines hidden rating, visible arena rank, catalog identity, inventory eligibility and deterministic settlement rules.
sources:
  - dotnet/NodeWar.Progression/
  - Assets/Scripts/Backend/Catalog/
  - Assets/Scripts/Backend/Shared/CatalogIds.cs
  - Assets/Scripts/Backend/Shared/CatalogKeys.cs
  - Assets/Scripts/Backend/Shared/ContentTable.cs
  - Assets/Scripts/Backend/Shared/InventoryClamp.cs
  - Assets/Scripts/Backend/Shared/LoadoutCapacityPolicy.cs
  - Assets/Scripts/Backend/Shared/RankTable.cs
  - Assets/Scripts/Backend/Shared/SuitTree.cs
  - Assets/Scripts/Backend/Shared/DistrictMigration.cs
verified: e9255fad
---

# Progression

## Purpose
Hidden skill selects opponents while visible RR gives players an arena progression. Shared catalog and eligibility rules keep server settlement, client displays and local tooling aligned without putting account authority in gameplay simulation.

## Interface
`NodeWar.Progression` is a UnityEngine-free netstandard library; rating math uses doubles outside `Simulation/`. It performs no I/O and owns no clock.

`Rating` exposes R, RD, Sigma (defaults 1500, 350, 0.06). `Glicko2.Update`, `UpdateSingleMatch(player, opponent, score, config)`, `DecayForInactivity(rating, idlePeriods, config)` and `ExpectedScore(a, b)` compute hidden skill. `Glicko2Config` defaults to Tau 0.5, convergence epsilon 0.000001, RD floor 30 and cap 350.

`RankPoints.RRDelta(currentRR, hiddenAfterMatch, won, RankConfig)` returns signed visible movement. `Arenas.ArenaForRR` and `ApplyRRDelta(RankState, delta, ArenaConfig)` map it to `RankState` (RR, Arena, HighestArena). Arena indices are zero-based. Shared `RankTable.Thresholds` is 0/300/700/1200/1800/2500; `Names` supplies Arena 1 through Arena 6. Server configuration and display must agree.

`MatchSettlement.Settle(SettlementInput, SettlementConfig)` consumes exactly two `SettlementPlayer`s (rating, last-match time, rank), `Winner` in {0,1} and `NowUnixSeconds`. `SettlementOutcome.Players` returns updated ratings/times/ranks, signed RRDelta and Promoted/Demoted. Voids never call it. Catalog grants and equipment changes belong to the cloud settlement wrapper.

`CatalogItem` has stable `Id`, `Kind` (Variant/Skin), `BaseId`, `Era`, `Retired`. `CatalogValidation.Validate(catalog, arenaCount)` checks shape/uniqueness; `ValidateAgainstPrevious(previous, next)` forbids deletion or identity reuse while allowing retirement changes. `EraUnlocks.GrantsFor(catalog, highestArena, owned)` returns sorted missing active variants through that era (no longer called by the server); `IsUsable(item, currentArena)` checks generic era eligibility independently of ownership/retirement.

Editor `CatalogDefinition` holds `CatalogEntry` rows. `CatalogTools.Generate` adds missing variants/default skins and retires inactive district bases; `Export` validates and writes `dotnet/NodeWarCloud/NodeWarCloud/Catalog/catalog.json`. Menus are `Tools > Node War > Backend > Generate Catalog` and `Export Catalog For Server`.

Shared `CatalogIds` builds/parses `suit.<base>`, `district.<base>`, `<baseId>.e<n>` and `skin.<baseId>.default`; `EraCount` is six. `CatalogKeys` explicitly maps numeric types to persisted bases and lobby IDs (`suit_...`, `node_...`); historical keys remain decodable. `CatalogBases.All()` enumerates current exportable bases.

Shared `ContentTable` holds placeholder content data: `DebutEra(baseId)` (the era a base becomes obtainable; `UnknownEra` otherwise), `Starters`/`IsStarter` (owned from first sign-in: Farm, Mine, Town, Barracks, Warrior, Guardian and the production suits) and `Debuting(era)`. Owning a base means owning its era-0 variant.

`SuitTree.Nodes`, `TryGet`, `Root`, `IsAvailable`, `CanOwn`, `OwnershipProblems` describe suit branches. `SuitTreeNode` carries BaseId, Variant, ParentVariant, Arena, Unlock (Cost/DropOnly), Cost, derived Id/ParentId. Current tree suits have variant 0 roots and sibling variants 1/2; availability uses the node's unlock arena, not its variant number. `CanOwn` additionally requires the reached arena and parent ownership. This table does not itself charge currency or grant purchases.

`InventoryClamp.ClampToArena(InventoryRecord, arena)` mutates only unusable equipped variants and needed fallback ownership. `LoadoutCapacityPolicy.SuitCapacity`, `DistrictCapacity`, `GetsFreeGrants` define arena-dependent choices. `DistrictMigration.Deck(source, slots)`, `Apply(PlayerState)`, `Detached(PlayerState)` and `CanonicalType` migrate saved decks/inventory to the active roster.

`DisconnectPenalty.Decay/Strike/NonReport(state, now)` are pure discipline rules consumed by the server; account persistence lives elsewhere.

## Connects to
- `cloud`: `MatchSettler` wraps pure settlement with locked persistence, grants, clamp and history; equip checks catalog and ownership.
- `backend-client`: shared records, local inventory fake and rank displays consume the same IDs/policies.
- `lobby-ui`: Workshop uses suit trees, owned IDs and capacity policy; rank displays use `RankTable`.
- `draft`: chosen capacities, free grants and equipped variants determine match loadouts.
- `simulation`: variant indices select gameplay balance; skins never enter state or hashes.
- `match-log`: eras and cosmetic skin IDs preserve the recorded loadout.
- `tooling`: Editor exports catalog/balance data for server validation.
- `board`: fielded variants become `PlayerSetup` era tables for tick-zero construction.
- `economy`: era-indexed balance lookups consume eligible variant indices without reading inventory.

## Invariants
Persisted IDs are never casually renamed/reused; domain enum names are not storage keys. Ownership survives demotion. Equipped eligibility uses current arena; automatic grants give only the starter kit, never era content. Tree availability and generic era eligibility are different rules. Server-only rating doubles never enter simulation. Skins remain cosmetic.

## How it works
Settlement decays both pre-match ratings for complete inactive periods (default one week, skipping new players), then updates each against the other's decayed pre-match rating. Each match is one rating period.

RR derives from hidden rating after the match versus implied rating `1000 + 0.5 * RR`: base gain 20 plus divergence 0.04, win/loss mirrored, magnitude clamped 8–40 and rounded away from zero. Arena application floors RR at zero and retains HighestArena; there is no boundary protection or placement-match phase.

Grants add the starter bases' era-0 variants and default skins; other content awaits crates or admin grants. Suit equip/referee checks use `SuitTree`, so ownership alone does not imply equip availability. Generic demotion clamp selects the highest owned eligible era, granting era 0 if necessary. Tree clamp falls back to its root, even before the root's debut arena; equip validation may still refuse that root.

Below arena index 2, choose two suits and two districts, receiving free Warrior/base-district grants. Higher arenas choose three suits and five districts without those grants. Count-pair helpers recognize only low (2,2) and high (3,5) shapes.

District migration remaps retired IDs, removes duplicate deck choices, preserves historical ownership and chooses canonical equipment before clamping. Server and fake paths reuse it.

## Decisions
- Use Glicko-2 plus visible RR rather than expose raw matchmaking rating as rank.
- Keep rules pure and shared rather than duplicate authority inside Unity UI.
- Retain ownership after demotion rather than revoke earned variants.
- Keep explicit persisted keys and retirement rather than regenerate identity from renamed types.

## Pressure points
Variants are being redesigned as side-variants whose era only gates unlock; IDs `suit.<base>.e<n>` may be renamed before the first production deploy. Catalog generation still uses six generic eras while suit eligibility uses a three-node tree, and `ContentTable` debut eras gate neither equip nor draft yet. Nothing grants non-starter content to players until crates exist. Export validation is duplicated between Editor and server. Balance and RR constants need calibration; arena names/themes remain placeholders.

## Change recipes
- Change rank tuning: update shared thresholds/server config together, test boundary behavior and simultaneous settlement.
- Change variant identity: coordinate catalog, tree, ownership migration, equip/clamp, referee eligibility, wire/loadout and log compatibility; export both catalog and gameplay balance.

## Tests
`NodeWar.Progression.Tests` covers rating, RR, arenas, catalog validation, grants, settlement and penalties. `NodeWar.Lobby.Tests` covers shared IDs, tree/clamp/migration/capacity and local services; `NodeWarCloud.Tests` guards server eligibility and threshold agreement. Editor exports and Workshop layout require Unity verification.
