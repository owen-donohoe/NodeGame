---
type: Inventory
title: Art Manifest — the board, the villagers, the world
description: Everything that has to be drawn for the world layer, the render constraints the art has to survive, and the hybrid billboard/mesh arrangement the board is moving to. The drawing list for the art-and-feel phase.
tags: [art, inventory, view, feather3d, mobile]
generated: { by: claude-opus-5, at: 2026-09-23T00:00:00Z }
status: historical
snapshot_of_commit: 256d33e
# No `sources:`, deliberately - see docs/index.md, "Historical snapshots".
# This is a drawing list, not a description of live code. It goes out of date
# by art landing, which is the point, and the freshness check should stay
# quiet about that.
---

# Art Manifest

The companion to [ui-inventory](ui-inventory.md). That document says **what
exists**; this one says **what has to be drawn**, and what it has to survive
once drawn. [ui-art-manifest](ui-art-manifest.md) covers the screen layer.

Written against a decision to author in **Feather 3D**, whose grease-pencil
stroke look is the intended style. Feather exports PNG/JPG, MP4/GIF turntables,
and OBJ/GLTF with baked vertex painting.

---

## 1. The arrangement: billboards for what you touch, meshes for what you do not

**Interactables stay 2D billboard sprites. Environment becomes Feather mesh
geometry.** This is not a new architecture — it is half-running already.
`GroundMat`, `GhostMat`, `ConfirmedMat` and `PlacementTilesMat` are all URP/Lit
opaque meshes sharing the scene with the billboarded sprite stacks today.

Why it works without a fight:

- Opaque meshes render in the opaque queue **with depth write**. Sprites are
  `Queue=Transparent`, `ZWrite Off`, and render after. So a sprite villager
  **depth-tests against** the environment: walk behind a drawn building and the
  sprite is correctly occluded. That is an improvement on the layered sprite
  fake, not a regression from it.
- `SpriteDepthSorter.Refresh` already collects `MeshRenderer` alongside
  `SpriteRenderer` into its `SortingGroup`. Whoever wrote it left the door open.
- `ViewSide` gives four POVs from an exact quarter-turn table with no float
  noise, and `SortHeight` gives authored vertical layering. Both are
  POV-change-driven, not per-frame.

### The three things that do change

1. **`Renderer.sortingOrder` does nothing for an opaque material.** The
   `SortingGroup` ordering `SpriteDepthSorter` computes only binds transparent
   renderers. Opaque environment meshes sort by depth buffer and ignore it —
   which is correct, but it means **environment must not be authored as
   transparent** or it re-enters the sprite sort and the two systems disagree.
   Keep environment opaque, alpha-clip if a stroke needs a hole.

2. **Billboard quads intersect mesh geometry.** A sprite is a flat quad facing
   a shared angle (`Billboard.cs`), and the camera sits at ~60° pitch. A quad
   standing at a building's base can slice into it, producing a hard clipped
   edge through the villager. Three mitigations, cheapest first: keep
   environment geometry low and away from where villagers stand; push the
   sprite a small distance camera-ward; or give villagers an alpha-clipped
   material with `ZWrite On` so they resolve against meshes properly.

3. **Vertex colours carry no tint channel.** Feather's GLTF bakes colour into
   the mesh. `VillagerView` drives per-player, per-state colour through
   `SpriteRenderer.color`, and `OutlineDriver` rides an owner tint. Environment
   does not need either — it is scenery, not owned — so this only bites if a
   mesh ever needs to show ownership. If one does, it needs a shader with a
   tint multiply over the vertex stream. **Do not let ownership become
   mesh-only.** It must stay readable on the sprites.

### Budgets

| Thing | Budget | Why |
|---|---|---|
| Environment mesh, per node | **~2k tris** | 28 nodes on the board |
| Villager sprite | 1 quad | it stays a sprite |
| Environment materials | **one, shared** | vertex-colour URP shader, so the SRP batcher can batch. Per-mesh materials mean ~80 draw calls |

Feather will export well over that budget. Decimate on import; a grease-pencil
silhouette survives decimation far better than a smooth surface does.

---

## 2. What the art has to survive

Four settings currently work against fine linework. Worth fixing **before**
judging any drawing, or line weight gets tuned against a lie.

| Setting | Where | Effect on line art |
|---|---|---|
| `m_RenderScale: 0.8` | `Assets/Settings/Mobile_RPAsset.asset` | thin lines rendered below native and upscaled — crawl under pan/zoom |
| `m_MSAA: 1` (off) | same | no edge coverage; stroke ends stipple |
| `enableMipMap: 0` | both sprite atlas `.meta` files | downscaled sprite line art aliases hard |
| Bloom `1.54` + Bokeh DoF | Gameplay global volume | eats fine linework; tuned against flat sprites |

Recommended: **mipmaps on**, **MSAA 2×** (cheap on tile-based mobile GPUs and a
far better spend than render scale), and re-judge bloom once real art is in.

**Author line weight thick.** A stroke that survives 0.8× render scale reads as
style; one that was right at 1× reads as a rendering bug.

---

## 3. Districts — 14, and three have nothing

`DistrictVisual` assets live at `Assets/Data/Game/Districts/`, one per
`DistrictType`, created by **Tools > Node War > Create District Visuals**. Each
is a named empty slot: board prefab, icon, sticker, accent colour. Re-run the
command after adding a district to the enum.

| District | Slot | Board art | Note |
|---|---|---|---|
| None (Crossroads) | Fixed | prefab | not a district; connector only |
| Core | Fixed | prefab | the breach target. The one place real mesh depth would pay |
| Farm | Fixed | prefab | |
| Mine | Fixed | prefab | |
| **Forge** | Fixed | **nothing** | |
| **Village** | Fixed | **nothing** | |
| Camp | Army | prefab | |
| Barracks | Army | prefab | |
| Arsenal | Army | prefab | |
| Shrine | Healing | prefab | **ambiguous layers** — `_13` or `_14` is the building; a human must pick |
| Sanctuary | Healing | prefab | |
| **Watchtower** | Affect | **nothing** | **default-unlocked**, so a new player sees a blank district on first launch |
| Rampart | Affect | prefab | |
| Market | Special | prefab | **two sprite refs to a GUID not in the project** |

Existing districts are authored as **a building plus 6–18 prop and ground
layers**. That decomposition suits Feather well: draw the building as one
group, props as another, export separately.

**Draw first:** Forge, Village, Watchtower. They are the only three that render
blank, and Watchtower is on screen for every new player.

---

## 4. Villagers — nothing exists at all

`VillagerPrefab` is `Circle` and `Square` sprites. There is no villager drawing
anywhere in the project.

- **Base villager body** — 1. Billboard-flat; the shared facing means one
  drawing serves all four POVs.
- **Combat suits** — 5: Warrior, Guardian, Scout, Berserker, Medic. Permanent
  until death, so they must read at a glance from across the board.
- **Production suits** — 6: Farmer, Miner, Smelter, Merchant, Acolyte, Watcher.
  Auto-assigned on arrival and stripped on leaving, so these change often and
  need to read as *temporary*.
- **Six states** — Idle, Moving, Working, Claiming, Fighting, Dead. These are
  currently colour-only (`VillagerView`). Pose or accessory would carry them
  better than colour, and colour is already spoken for by ownership.

The hard constraint: **whose villager it is must never depend on suit art.**
Ownership is the one read that cannot degrade.

---

## 5. Effects — when the feel layer lands

`TickEventLog` exists; `FeelDirector` does not yet (see
[direction](direction.md)). Nothing here has a caller today.

- Claim-flip burst · death puff · hit flash · breach effect · floating numbers

Feather's GIF turntable export is a natural source for short frame sequences.

---

## 6. Authoring specs

| | Value |
|---|---|
| Board sprite PPU | 300 (`AssetsSpriteSheet`, `HousesSpriteSheet`) |
| UI sprite PPU | 200 (`UI_Shop`) |
| Atlas max size | 2048 default, 8192 on one override |
| Sprite mode | Multiple, auto-sliced |

**Name the slices.** 60 of the project's 137 existing slices are referenced by
nothing and carry auto-generated names like `AssetsSpriteSheet_47`, so nobody
can tell what they depict without opening the sprite editor. Nothing in the
project loads a sprite by name; `DistrictVisual` is the first thing that could.
Do not repeat the unnamed-atlas mistake with new art.
