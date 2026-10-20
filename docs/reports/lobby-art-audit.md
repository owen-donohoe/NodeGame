# Lobby art coverage

Scope: every serialized DistrictDefinition (including its former NodeDefinition
name), every SuitDefinition, runtime catalog districts, production suits and all
SuitTree side variants. This audits lobby paper coverage; it does not replace board
prefabs or change the card design.

## Resolution paths

- `LoadoutCatalog` retains the five suit assets and creates missing active district
  definitions from `CatalogKeys.CatalogDistrictTypes`. Legacy district definitions
  outside that roster are excluded from the Workshop. All serialized definition
  `icon` references are empty.
- `UIArtTheme.asset` has no icon overrides. Its `districtVisuals` points to
  `DistrictVisualTable.asset`. Every DistrictVisual icon/sticker is empty. Board
  prefabs exist for several districts, listed below; these belong to world-board
  presentation, not the lobby paper renderer.
- `WorkshopPage.MakeArt` draws ItemTint monograms in cards/sheets. Those are
  intentional UI placeholders, not failing sprite loads.
- `StageLoadoutTypes` translates chosen IDs to types. `LobbyStage.SetLoadout`
  resolves `StageArt`, then `StagePaperArt.TextureFor`, then
  `PaperRasterizer.RasterizeCoverage`. The renderer generates tinted paper buildings
  and villagers with ink details and a five-bit identity badge. Tint uses the
  canonical lobby ID through `ItemTint.IndexFor`.
- `DistrictCluster` controls selection/replacement only; it does not resolve art.
  Empty chosen slots deliberately show ground without a building. Equipped slots
  always receive a building. Suits always receive a villager.

## Inventory

"Asset" means a serialized definition in `Assets/Data/Lobby/Nodes` or `Suits`.
"Generated" means LoadoutCatalog supplies the active district definition.
"Key only" means a production/historical catalog identity, not an offered asset.
Every row has **missing authored lobby sprite art**; the paper column is its
nonempty deterministic replacement. A board prefab does not imply a lobby sprite.

| District ID | Definition / availability | Authored board prefab | Lobby paper |
|---|---|---|---|
| node_farm | Generated, active | Farm | Tower |
| node_mine | Generated, active | Mine | Workshop |
| node_village | Generated, active | Missing | House |
| node_barracks | Asset, active | Barracks | Tower |
| node_core | Key only, not a choice | Core | Workshop |
| node_forge | Generated, active | Missing | House |
| node_camp | Asset, retired | Camp | Tower |
| node_shrine | Asset, retired | Shrine | Workshop |
| node_arsenal | Asset, retired | Arsenal | House |
| node_sanctuary | Asset, retired | Sanctuary | Tower |
| node_watchtower | Asset, retired | Missing | Workshop |
| node_rampart | Asset, retired | Rampart | House |
| node_market | Asset, retired | Market | Tower |
| node_pier | Generated, active | Missing; generic board fallback | Workshop |
| node_town | Generated, active | Village is empty; generic board fallback | House |
| node_infirmary | Generated, active | Reuses Shrine, then Sanctuary | Tower |
| node_fortress | Generated, active | Reuses Rampart | Workshop |
| node_storehouse | Generated, active | Reuses Market | House |
| node_workshop | Generated, active | Missing; generic board fallback | Tower |
| node_crossroads | Asset, deliberately excluded; no simulation/catalog type | Missing | Not equippable; no stage slot |

The unclaimed board `None` prefab is scenery, not a DistrictDefinition or catalog
item. Retired assets remain listed for audit completeness; no new gameplay choice
is introduced for them.

| Suit ID / catalog variants | Definition / availability | Authored lobby art | Lobby paper |
|---|---|---|---|
| suit_farmer | Key only, production | Missing | Villager + type badge |
| suit_miner | Key only, production | Missing | Villager + type badge |
| suit_warrior; suit.warrior.e0, .e1, .e2 | Warrior asset; root and both side variants | Missing for all three | Villager + Warrior badge; variants inherit base art |
| suit_smelter | Key only, production | Missing | Villager + type badge |
| suit_guardian; suit.guardian.e0, .e1, .e2 | Guardian asset; root and both side variants | Missing for all three | Villager + Guardian badge; variants inherit base art |
| suit_scout; suit.scout.e0, .e1, .e2 | Scout asset; root and both side variants | Missing for all three | Villager + Scout badge; variants inherit base art |
| suit_berserker; suit.berserker.e0, .e1, .e2 | Berserker asset; root and both side variants | Missing for all three | Villager + Berserker badge; variants inherit base art |
| suit_medic; suit.medic.e0, .e1, .e2 | Medic asset; root and both side variants | Missing for all three | Villager + Medic badge; variants inherit base art |
| suit_merchant | Key only, production/unused | Missing | Villager + type badge |
| suit_acolyte | Key only, production/unused | Missing | Villager + type badge |
| suit_watcher | Key only, production/unused | Missing | Villager + type badge |

## Bugs and evidence

| Finding | Classification | Fix / evidence |
|---|---|---|
| Catalog base/variant IDs silently disappear in StageLoadoutTypes | Resolution bug: generated art exists but is never reached | Accept catalog bases and parsed variants as well as lobby IDs. StageArtTests traverses every catalog key and all SuitTree variants, checking nonempty paper masks/alpha. |
| Stage tints hash district_1 / suit_3 rather than node_farm / suit_warrior | Identity resolution bug | StageArt.Identity now supplies the same IDs as ItemTint and Workshop. |
| Definition icons, DistrictVisual icon/sticker and theme overrides are empty | Missing authored art | Generated buildings/villagers remain nonempty; canonical tint and binary badge distinguish base items. |
| Front-edge feet can leave the camera's fitted bounds after wandering | Framing bug, not missing art | Sweep actual renderer bounds over the walking area, yaw, lift, shake and pop; fit with 8% window margins. Projection tests cover front corners and portrait/landscape aspects. |

No scene, prefab or meta files are edited. Pure tests verify identity lookup and
generated texture coverage. Unity compile-check verifies runtime code. Editor
captures, occlusion, transparent sorting and actual two-column layout still need
visual verification when a connected Editor is available.
