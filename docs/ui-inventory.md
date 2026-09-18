---
type: Inventory
title: UI Inventory
description: Every sprite asset in the project, the district-to-sprite mapping derived from the node prefabs, and the art that does not exist. The art inventory for the art-and-feel phase.
tags: [ui, sprites, art, inventory]
generated: { by: claude-opus-5, at: 2026-09-04T00:00:00Z }
status: historical
snapshot_of_commit: b72fc6d
# No `sources:`, deliberately - see docs/index.md, "Historical snapshots".
# The sprite sections were re-checked on 2026-09-15; see the note below.
---

> **Snapshot note.** Derived at `b72fc6d`; sprite sections re-checked at
> `9d08dae` on 2026-09-15. No commit since has touched `Assets/Sprites/` or
> `Assets/Prefabs/Game/RevisedNodes/`: the seven PNGs and their slice counts
> (89 / 19 / 29) hold; `Icons/`, `Suits/` and `Villagers/` are still empty; no
> prefab for Forge, Village or Watchtower, and Watchtower still
> default-unlocked; Market's two references to the missing GUID are still
> present. Still no `.uss`, `.uxml` or `.cs` that loads a sprite by name.
>
> Two sections describing `Assets/UI/` before the rebuild were deleted on
> 2026-09-17, along with a subagent handoff block for a brief that is not in
> the repo. For what `Assets/UI/` is now, read
> [architecture](architecture.md), "Where the UI lives".
>
> References below to "the brief" mean a lobby brief that was never kept in
> the repo. Its section numbers point at nothing; the facts around them were
> checked against the files and stand on their own.

# UI Inventory

**This is the art inventory.** What exists, what each district's art is
actually made of, and what has to be drawn or commissioned before the board
can look like anything. Derived at commit `b72fc6d`, Unity `6000.5.9f1`.

The short version: there are three large auto-sliced atlases with
machine-generated sub-sprite names, consumed only through scene and prefab
serialisation. Nothing loads a sprite by name anywhere in the project, so
doing so is new work rather than a pattern to copy.

---

## 1. Source files

Seven PNGs. That is the whole of the project's 2D art.

| Path | Pixels | Mode | PPU | Sub-sprites | GUID |
|---|---|---|---|---|---|
| `Assets/Sprites/Nodes/Houses/AssetsSpriteSheet.png` | 6000×6000 | Multiple | 300 | 89 | `4529beb66b7c32c4c9d18ee8685cbc32` |
| `Assets/Sprites/Nodes/Houses/HousesSpriteSheet.png` | 6000×6000 | Multiple | 300 | 19 | `dc2324a36ca4bda4fb2c52752e40942e` |
| `Assets/Sprites/UI/UI_Shop.png` | 4000×4000 | Multiple | 200 | 29 | `7dcd961a93804e74aa425fe1c7cec89d` |
| `Assets/Sprites/UI/Circle.png` | 256×256 | Single | 256 | — | `7994e45c1fa7a644f8af076843fdf643` |
| `Assets/Sprites/UI/Ring.png` | 256×256 | Single | 256 | — | `be8321c59884e4c40baff06bdc40a706` |
| `Assets/Sprites/UI/RingBG.png` | 256×256 | Single | 256 | — | `63309efd77f650547aba8eb40c185af1` |
| `Assets/Sprites/UI/Square.png` | 256×256 | Single | 256 | — | `fefc708e8da89224abecc6caf6417093` |

`Assets/Sprites/Icons/`, `Assets/Sprites/Suits/` and `Assets/Sprites/Villagers/`
exist and are **empty**. No files at all.

Sub-sprite names are entirely auto-generated — `AssetsSpriteSheet_0` … `_89`,
`HousesSpriteSheet_0` … `_18`, `UI_Shop_0` … `_28`. Two AssetsSpriteSheet
entries carry the manual-looking names `_13.1` and `_14.1`; the numbering is not
contiguous. **No sub-sprite name says what it depicts.**

No `.cs` file anywhere under `Assets/Scripts/`, `Assets/UI/` or `Assets/Legacy/`
references a sprite by name, and no `.uss` or `.uxml` under `Assets/UI/`
contains a `url(`. Every sprite in the project is wired through scene and prefab
serialisation only. Loading one from a UI controller is new work, not a pattern
to copy.

---

## 2. District → sprite layers

A district's visual is a **composed prefab, not an icon.** Each
`Assets/Prefabs/Game/RevisedNodes/*_Node Variant.prefab` stacks 6–18
`SpriteRenderer` layers. Table derived by joining each prefab's `m_Sprite`
`fileID` values against the `internalID` of each named slice in the sheet
`.meta` files.

The useful regularity: **each district prefab references exactly one
`HousesSpriteSheet` slice, and that slice is the building itself.** The
`AssetsSpriteSheet` layers are props, ground and decor. Shrine is the exception.

| District | Building slice (`HousesSpriteSheet`) | Prop layers (`AssetsSpriteSheet`) |
|---|---|---|
| Arsenal | `_16` [1210×1670] | `_28, _29, _47, _26, _31, _29, _38, _73, _27, _85` |
| Barracks | `_17` [2106×681] | `_29, _34, _26, _12, _35, _32, _31, _30, _27` |
| Camp | `_1` [970×678] | `_47, _85, _42, _78, _83, _45, _27, _73` |
| Core | `_15` [1194×1351] | `_77, _73, _72, _83, _69` |
| Farm | `_10` [1019×936] | `_17, _78, _47, _16, _30, _68, _33, _80, _79` |
| Market | `_0` [872×976] | `_13.1, _48, _70, _51, _58, _49, _74, _68, _78, _38, _75, _63, _14, _85, _13` **+ 2 broken refs, see below** |
| Mine | `_2` [944×962] | `_49, _8, _24, _67, _64, _9, _56, _62, _28, _28, _62` |
| Rampart | `_5` [1445×1096] | `_26, _26, _76, _44, _22, _25, _70, _29, _27, _71` |
| Sanctuary | `_9` [1144×1025] | `_71, _56, _74, _85, _68, _37, _69, _76` |
| Shrine | **ambiguous** — `_13` [750×837], `_14` [1106×652], `_11` ×2 [170×215], `_12` [172×215] | `_38, _56, _67, _31, _62, _66` |
| *Crossroads* | **none** | `_71, _21, _66, _67, _72, _20, _80, _76, _57, _81` |

Notes:

- **Shrine** is the only district with more than one `HousesSpriteSheet` layer.
  `_13` or `_14` is the building; `_11` and `_12` are small paired props. A
  human has to pick. Do not guess in a UI.
- **Crossroads** has a prefab but is *not* a district — `GameManager.MapNodeIDToDistrict`
  has it commented out and unresolved (brief §1.2). Listed for completeness only.
  Ignore it.
- **Market has two broken sprite references.** Both point at GUID
  `04bc98338f6b220499c4ea6aa04b1f04`, which appears **nowhere else in the
  project** — no `.meta` declares it. The asset is missing. This is a
  pre-existing defect, out of scope for the lobby rebuild, and worth a Task.
- `BaseNode.prefab` (the non-variant root) uses `Square.png` twice and no atlas
  slice.

### Districts with no art at all

The brief §1.2 lists fourteen districts. Twelve are draftable (`None` and `Core`
excluded). Prefabs exist for ten of the twelve.

**`Forge`, `Village` and `Watchtower` have no prefab and no sprite.**

`Watchtower` is in `PlayerProfile`'s default `unlockedNodeIDs` (brief §1.4), so
a new player's Workshop grid shows a district with no art on first launch.

---

## 3. UI_Shop atlas

29 slices. The Lobby scene currently uses five:

| Slice | Rect (x, y, w×h) |
|---|---|
| `UI_Shop_4` | 159, 1656, 726×277 |
| `UI_Shop_8` | 165, 2396, 619×216 |
| `UI_Shop_12` | 692, 1987, 108×381 |
| `UI_Shop_24` | 2177, 886, 244×343 |
| `UI_Shop_28` | 1870, 894, 174×191 |

The scene's `Image` components sit on unnamed GameObjects, so which slice is the
market stall and which is the villager is not determinable from the files. Open
`Lobby.unity` in the Editor and look — this is a two-minute job for a human and
an impossible one from disk. The brief calls the stall art (§6.5) and the
villager (§6.1) the best assets in the project; both are somewhere in these 29.

The remaining 24 unused slices are listed in §4.

---

## 4. Unmapped slices

60 of the 137 slices are referenced by nothing. Names and pixel dimensions only
— contents unidentified. A human pass over the atlases in the sprite editor is
the only way to name these.

**AssetsSpriteSheet (33 unused):**
`_0` 68×205 · `_1` 538×534 · `_2` 50×154 · `_3` 501×470 · `_4` 57×169 ·
`_5` 626×497 · `_6` 601×384 · `_7` 295×210 · `_11` 56×226 · `_14.1` 48×134 ·
`_15` 360×294 · `_23` 597×600 · `_36` 182×231 · `_39` 80×395 · `_40` 880×847 ·
`_41` 513×336 · `_43` 270×254 · `_46` 419×271 · `_50` 262×226 · `_52` 264×201 ·
`_53` 321×263 · `_54` 524×549 · `_55` 415×297 · `_59` 104×117 · `_60` 231×181 ·
`_61` 307×210 · `_65` 46×42 · `_82` 245×607 · `_84` 272×551 · `_86` 261×390 ·
`_87` 233×254 · `_88` 138×407 · `_89` 259×423

The tall narrow ones in the `_82`–`_89` range (≈250×400–600) are the right
aspect for standing figures. **That is a guess, not a fact** — verify before
using any of them as a villager or suit.

**HousesSpriteSheet (3 unused):** `_6` 307×152 · `_8` 673×948 · `_18` 336×209

`_8` at 673×948 is building-shaped and unclaimed — a plausible candidate for one
of Forge, Village or Watchtower. Verify before assuming.

**UI_Shop (24 unused):**
`_0` 88×122 · `_1` 447×492 · `_2` 337×473 · `_3` 510×482 · `_5` 448×552 ·
`_6` 342×540 · `_7` 435×545 · `_9` 588×639 · `_10` 594×609 · `_11` 112×396 ·
`_13` 147×76 · `_14` 192×168 · `_15` 323×287 · `_16` 356×348 · `_17` 144×76 ·
`_18` 90×117 · `_19` 146×191 · `_20` 74×98 · `_21` 500×273 · `_22` 164×103 ·
`_23` 200×127 · `_25` 486×256 · `_26` 278×228 · `_27` 379×326

---

## 5. What does not exist

Every item below is required by a named section of the brief and has **no asset
in the project**. Each is a placeholder in Phases 3–5, per brief §11.

| Missing | Required by | Consequence |
|---|---|---|
| Suit art — all five combat suits | §6.3 Workshop suit grid, §6.3 suit detail sheet | Suit cards are text + coloured block |
| Villager figure as a UI asset | §6.1 the Home villager, costumed per loadout | Either extract from `UI_Shop` (see §3) or placeholder. `VillagerPrefab` itself uses only `Circle` and `Square` — there is no villager sprite in the project |
| Icons — trophy, box, food, material, settings gear, tab bar ×5 | §3.2 tab bar, §6.1 identity strip, §6.1 box meter | `Assets/Sprites/Icons/` is empty. Unicode or drawn-in-USS shapes until commissioned |
| Paper textures, four eras | §6.4 "the strongest identity idea in this brief" | Flat `--paper` tints per segment, TODO'd. The mechanic still reads |
| Box / paper parcel art | §6.7 box opening | The full-screen takeover has nothing to open |
| Forge, Village, Watchtower district art | §6.3 Workshop districts grid | Three of twelve draftable districts render blank |
| Nine-slice paper frames | §2.4 the sticker look | `-unity-slice-*` has nothing to slice. Borders and radii from `Theme.uss` carry the look instead |

Cards ~104×132 (§6.3) and ~132×196 (§6.5) against source art 500–1600px means
every atlas slice is downscaled 5–10×. Import settings are the node-world's
300/200 PPU, not UI. Expect to need per-slice sizing in USS regardless.

---

## 6. Still open

1. **Suit art and villager art do not exist.** Both are placeholder-only until
   drawn or commissioned. `VillagerPrefab` itself uses only `Circle` and
   `Square` — there is no villager sprite in the project at all.
2. **Forge, Village and Watchtower have no district art**, and Watchtower is
   default-unlocked, so a new player sees a blank district on first launch.
3. **`Market_Node Variant.prefab` has two sprite references to a GUID that is
   not in the project.** A pre-existing defect, still present.
4. **Nobody has named the 60 unmapped slices** (§4). A human pass over the
   atlases in the sprite editor is the only way; it is also the cheapest art
   win available, because the art may already exist and just be unfindable.
