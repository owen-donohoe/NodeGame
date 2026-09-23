---
type: Inventory
title: UI Art Manifest — icons, surfaces, and the slots waiting for them
description: Every icon the UI draws as a placeholder vector, every UI Toolkit surface and what art it is missing, and the empty Sprite slots already wired and waiting. The drawing list for the screen layer.
tags: [art, inventory, ui, ui-toolkit, icons]
generated: { by: claude-opus-5, at: 2026-09-23T00:00:00Z }
status: historical
snapshot_of_commit: 256d33e
# No `sources:`, deliberately - see docs/index.md, "Historical snapshots".
# A drawing list, not a description of live code. It goes out of date by art
# landing, which is the point.
---

# UI Art Manifest

The screen-layer half of [art-manifest](art-manifest.md). Which tree this is
about, and why there are three of them, is
[architecture](architecture.md#where-the-ui-lives) — the live one is
`Assets/UI/`, UI Toolkit, and everything below is about that.

The state of play in one line: **the UI has almost no art in it.** There is
exactly one `background-image` in the whole of `Assets/UI/Styles/`, and it is
`none`. Everything on screen is USS — borders, radii, colours, and 29 glyphs
drawn at runtime with `Painter2D`.

That is a good position to draw into. Nothing has to be unpicked.

---

## 1. Icons — 29 glyphs, all placeholders

`LobbyIconKind` in `Assets/UI/Scripts/LobbyIcon.cs` is the icon inventory, and
it lives in code rather than in any document. Each glyph is drawn once with
`Painter2D` into a white `VectorImage`, cached, and shown as the element's
background image; colour comes from `-unity-background-image-tint-color`, so a
drawn sprite can replace a vector without touching a stylesheet.

The file says so itself: `TODO(art): these stand in for icon sprites that do
not exist yet.`

**Design space is a 24×24 box.** Tint is applied as a flat colour over the
whole glyph, so these want to be **single-colour silhouettes**, not full-colour
drawings — or the tinting has to go, which costs the tab-colour transitions.

| Group | Glyphs | Count |
|---|---|---|
| Resources | Food, Materials, Metal | 3 |
| In-match indicators | Alert, Swords, Capture, Sleep, Respawn, Pointer | 6 |
| Emotes | Smile, Frown, Angry, Flag, Speaker | 5 |
| Lobby chrome | Shop, Spark, Tools, Gear, Envelope, Mouth, Tv, Back, Hat, Diamond, District, Suit, Lock, Pip, Close | 15 |

The three resource glyphs are the highest-value set: they appear in the HUD's
bottom sheet, inside the resource rings, and again as cost chips on the node
sheet, so they are on screen for the whole match.

`Pointer` is drawn pointing right and rotated in code — draw it pointing right.

---

## 2. Slots already wired and empty

Fourteen `Sprite` fields exist, are serialised, and hold `{fileID: 0}`. These
need no code to accept art.

| Asset | Field | Count |
|---|---|---|
| `Assets/Data/Lobby/Nodes/*.asset` | `NodeDefinition.icon` | 9 |
| `Assets/Data/Lobby/Suits/*.asset` | `SuitDefinition.icon` | 5 |

Note the lobby has **9** node definitions against the simulation's 14 districts
— the two sets were never reconciled. `DistrictVisual`
(`Assets/Data/Game/Districts/`) is the newer, complete one and is the slot to
fill; see [art-manifest](art-manifest.md) §3.

`DraftScreenController.stickerMappings` is a third `DistrictType`-to-`Sprite`
table, populated from the uGUI draft prefab by
**Tools > Node War > Set Up UI Toolkit Draft**. Three tables for one question
is the thing `DistrictVisual` exists to end; until a migration lands, art put
in one is not visible from the others.

---

## 3. Surfaces — 12 layouts, 13 stylesheets

| Layout | Art it is missing |
|---|---|
| `GameplayHUD.uxml` | breach wall fill treatment, resource ring faces, emote bubbles, the burger and recentre glyphs (currently USS bars and a `◎` character) |
| `NodeSheet.uxml` | sheet paper texture, cost chip frames, district header art |
| `DraftScreen.uxml` | **district stickers**, placed-piece frame, grid cell treatment |
| `LobbyRoot.uxml` | tab bar icons (5), nav chrome |
| `HomePage.uxml` | **the Home villager** — currently `lb-villager__body` + `__hat` + a `Mouth` glyph, built from USS boxes |
| `WorkshopPage.uxml` | district grid cards (14), suit grid cards (5), detail sheet art |
| `ShopPage.uxml` | **box/parcel art** for the opening takeover, shelf and stall art |
| `ProfilePage.uxml` | identity strip, trophy and box meter icons |
| `SocialPage.uxml` | avatar placeholders |
| `SettingsPage.uxml` | — |
| `MatchHistoryPage.uxml` | — |
| `PlayPopup.uxml` | mode cards |

### Two named looks with nothing behind them

Both come from the lobby brief and are called out in
[ui-inventory](ui-inventory.md) §5:

- **Nine-slice paper frames.** `-unity-slice-*` is supported and has nothing to
  slice. Borders and radii from `Theme.uss` carry the sticker look instead.
  This is the single highest-leverage UI asset: one nine-slice frame restyles
  every card in the project.
- **Paper textures, four eras.** Described in the brief as the strongest
  identity idea in it. Flat `--paper` tints stand in; the mechanic reads
  without them.

---

## 4. Sizing

Cards render at roughly **104×132** (Workshop) and **132×196** (Shop) against
source art of 500–1600px, so existing atlas slices are downscaled 5–10×. Import
settings on the current atlases are the node world's 300/200 PPU, not UI
values, and mipmaps are off — which is why downscaled UI art aliases today.

Draw UI art at **2× the on-screen size**, import at UI PPU with **mipmaps on**,
and expect to set explicit sizing in USS regardless.

---

## 5. Fonts

Fredoka SDF in three weights (Medium, SemiBold, Bold) at
`Assets/UI/Fonts/`, referenced from `Tokens.uss`. Fredoka carries none of the
symbols the prototype used, which is the whole reason `LobbyIcon` exists rather
than a second fallback font. Adding a font is not an alternative to drawing the
29 glyphs — it was considered and rejected.
