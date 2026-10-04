---
type: Direction
title: Suit variants and the skill tree — proposal draft
description: How the per-arena suit unlocks and branching variants map onto the era and catalog system that already exists, what that system cannot express yet, a content budget for the options, and the lobby tree and info-panel design. Written against the code as it stands on 2026-10-04.
tags: [lobby, progression, suits, skill-tree, ux, balance]
generated: { by: claude-sonnet-5-5, at: 2026-10-04T00:00:00Z }
status: draft
# No `sources:`. Describes work that has not happened. Part of it touches
# Simulation/ tables only if variants stay stat-only; anything with a new ability
# is a Simulation change and starts in plan mode.
---

# Suit variants and the skill tree — proposal draft

## 1. What you described

- Every arena unlocks three new suits.
- From the second arena on, earlier suits gain two new variants each, so there are
  two axes of progress: more suits, and deeper suits.
- The lobby shows that progression as a skill tree, with an info panel for each suit
  and variant.
- A rare box drop can hand out a variant directly.

## 2. What already exists

- **Variants already exist, and are called eras.** `SuitStats` is keyed by suit and
  `era`; `PlayerData.suitEras[suit]` is the one variant a player fields for each
  suit; `GameBalanceData.EraCount = 6` sizes the tables; `WithEveryEra` copies era 0
  to later eras that have no entry. Today eras 1–5 are copies of era 0.
- **Variants are owned items, not just a function of arena.**
  `InventoryRecord.OwnedVariants` lists them, and `Equipped.Variants[baseId]` holds
  exactly **one** variant per base. `LoadoutTypes.ErasFromEquipped` turns that into
  `suitEras` at match launch. The server grants and checks ownership.
- **Arena currently caps what you can field.** `InventoryClamp.ClampToArena` demotes
  an equipped variant whose era exceeds the player's arena after a demotion, keeping
  ownership. `game-model.md` states the rule as "arena N plays era N".
- **Only five combat suits exist:** Warrior, Guardian, Scout, Berserker, Medic
  (`SuitType`). The other six suits (Farmer, Miner, Smelter, Merchant, Acolyte,
  Watcher) are assigned automatically by district and are not equipped.
- **A variant's numbers are seven fields:** `bonusHP`, `attackDamage`,
  `moveSpeedTicks`, `attackCooldownMax`, `foodCost`, `materialCost`, `fightPriority`.
- **A suit's equip points are decided by district,** in `CanEquipSuitAtNode`
  (Camp: Warrior or Scout; Barracks: Warrior, Guardian, Berserker or Scout; Arsenal:
  Warrior, Guardian or Scout; Sanctuary: Medic).

## 3. Where the design collides with it

1. **Era means "later is stronger"; a tree means "a different choice".** The model
   says power creep with arena is deliberate. A tree of sidegrades is the opposite.
   Running both gives three overlapping progressions (arena power, tree depth, more
   suits), which is more than a player can read. I recommend that for **suits** the
   era slot becomes the *variant slot* and stops being power creep. Districts keep
   eras as they are.
2. **The arena rule breaks.** "Variant index ≤ arena" stops being true once a tree
   node's index no longer equals its arena. `ClampToArena` and the matchmaking
   argument ("opponents within one arena because rating cannot see the era gap")
   both assume it. With sidegrades the power gap shrinks, but the option-count gap
   stays, so the ±1 arena rule should stay for now.
3. **The slot count is a hard limit.** `EraCount` is 6. A suit that debuts in arena 0
   and gains 2 variants every later arena would need 11 slots.
4. **One variant per suit per match.** `Equipped.Variants` holds one entry per base,
   so a match can field Warrior or Warrior-Vanguard or Warrior-Raider, never two.
   This is good: it makes each tree choice mutually exclusive, which is what makes
   it a choice.
5. **New suits are not free.** Each needs a `SuitType` value (append only, since
   `(int)suit` is hashed), a `CanEquipSuitAtNode` rule, an entry in
   `LoadoutTypes.SuitForLobbyId`, a bump of `SuitTypeCount`, art, equip UI
   (`BarracksPanelContent`), and bot behaviour. Variants that only change numbers
   cost almost none of this.

## 4. Content budget: the two readings of "more variants each arena"

Arenas 0 to 5 (six, matching `EraCount`), three new suits each.

| Policy | Rule | Suits | Variants | Table rows | Fits 6 slots? |
|---|---|---|---|---|---|
| **A** | each suit gets 2 variants in the arena *after* it debuts | 18 | 30 | 48 | yes (3 per suit) |
| B | every arena adds 2 variants to *every* existing suit | 18 | 90 | 108 | no (a suit from arena 0 reaches 11) |

Your example (arena 2 gives the first three suits two variants each) reads as A, and
B is the literal alternative. **Recommend A:** 48 balanced entries is a lot already,
and B's 108 would have to be balanced against each other. Policy A also gives every
suit the same shape: a root and two siblings.

There is also a gap in current content: the game has 5 combat suits and A calls for
18 by the last arena. Arena 0 can use Warrior, Guardian and Scout, and arena 1 can
use Berserker and Medic plus one new suit. Beyond that is new design.

## 5. Recommended structure

**Per suit: a root and two sibling variants.** The root is the suit as it is now.
The two variants unlock in the arena after the suit's debut, and each is a *sidegrade*
that trades one strength for another, expressible entirely in the seven stat fields.
For example, a Warrior might become a Vanguard (more HP, slower) or a Raider (faster,
less HP). Because only one variant per suit can be fielded, the player chooses a role.

**Stat-only variants first.** They need no `Simulation/` change: new `SuitStats` rows
only, which the balance hasher already covers. A variant with a *new ability* is a
real Simulation change (new state, hasher, version bump) and starts in plan mode.
Keep abilities for later.

**Data model.** A UnityEngine-free `SuitTree` table, placed in `Backend.Shared` so the
client and the Cloud Code module read the same one. Per node: base suit, variant index
(the existing era slot), parent index, the arena it becomes available, and how it is
unlocked (cost, or drop-only). The server validates ownership and equipping against
this one table. `ClampToArena` becomes "equipped node must be available at the arena".

**Unlock.** Reaching an arena makes a node *available*, not owned. Ownership comes from
a currency cost or a box drop. A rare drop that grants a node directly is fine; a
duplicate should convert to currency so a drop is never dead. Keep a guaranteed route
as well as chance. I did not read `InventoryService`, so how grants are issued today is
unverified.

## 6. The lobby tree

**Shape.** It should look like the game: nodes on a graph. A suit's root is a node and
its variants branch from it, drawn like districts being claimed, so progression looks
like the thing the game is about. This also answers the "too generic" concern.

**Layout (portrait phone).**
- A strip of the arena's suits across the top, with arena tabs to move between them.
- Below it, the selected suit's tree: root, then two children. Arena gates are shown as
  labelled horizontal bands the nodes sit in.
- A bottom sheet for the info panel, the same sheet pattern the node panel uses in
  matches.

**Node states, each needing a shape or icon as well as a colour:** locked (arena not
reached), available (can unlock), owned, equipped. The ring on the equipped node is the
one in a sibling set.

**Info panel.** A role line in plain words; a stats table with each variant's change
against its parent (for example `+2 HP`, `−1 speed`) rather than raw numbers; where it
can be equipped (the districts from `CanEquipSuitAtNode`); how to get it (arena, cost,
or drop-only); and a single primary button (Unlock or Equip). Selecting a node opens the
panel on a tap and does nothing destructive; this matches the tap-select rules in
[touch-input-spec](touch-input-spec.md).

**The base-node question** (which nodes you start with, and choosing them) is a
separate screen and not covered here.

## 7. Order of work and tests

1. `SuitTree` data and its rules in pure C#, with tests in the existing backend test
   projects: parent and arena rules, ownership validation, one equipped per suit,
   replacing the era-based `ClampToArena`, and migration of existing inventories
   (era 0 stays the root).
2. The balance rows for the first arena's variants, through the balance asset in the
   editor.
3. The lobby tree view and info panel.
4. New suits beyond the existing five, one arena at a time.
5. Boxes and the guaranteed route, with the server grant changes.

## 8. Unverified

- How `InventoryService` grants and validates variants today.
- The exact `CatalogIds` variant string format beyond the parse used here.
- How the Workshop currently lists and equips variants.
- That `BalanceHasher` covers every `SuitStats` field.

## 9. Decisions needed

1. **Resolved: sidegrades.** Variants trade one strength for another and are never
   strictly better than the root.
2. **Resolved: policy A.** Two variants per suit, in the arena after it debuts: 18
   suits, 30 variants.
3. Does the suit era slot stop being power creep, with districts keeping theirs? This
   follows from choosing sidegrades; confirm before the balance rows are written.
4. **Resolved: the production suits may get variants.** One catch: how fast a Farmer,
   Miner or Smelter produces is a *district* number (`DistrictStats.productionTicks`),
   not a suit number, and I did not verify that those suits have `SuitStats` rows at
   all. A production-suit variant (say, a faster Farmer) therefore likely needs a new
   per-villager production modifier, which is a Simulation change and starts in plan
   mode, unlike a combat variant that only changes the seven stat fields. Plan on
   combat variants first.
