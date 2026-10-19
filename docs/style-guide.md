# UI style guide

How Node War's menus look. Layout follows Clash Royale; style follows the paper-craft look of
Cult of the Lamb's *art* (not its UI). Tokens and components in `Assets/UI/Styles/` implement
this file; if they disagree, fix one of them.

## Layout (Clash Royale)

- Portrait, one-handed. Primary actions sit in the bottom third; nothing interactive is smaller
  than 48 dp; the safe area is respected top and bottom.
- Top: two strips. A thin currency strip (Coins, Laurels, each with "+"), then a player strip
  (avatar, Name#TAG, era badge, thin RR bar; burger and bell as small round buttons at its
  right). Hidden on Guild, which shows the guild strip instead.
- Bottom: a full-width tab bar (Shop, Loadout, Home, Social, Guild). The selected tab is raised
  and wider, with a larger icon and a label; the others show their icon only. Red dots mark news.
- Home: the 3D stage fills the middle. Bottom right: mode label above PLAY, next-crate bar
  below; crate buttons to PLAY's left.
- Panels open as bottom sheets (lists, pickers) or centred cut-out cards (claims, confirmations).

## Material: paper and ink

- **Cut-out card.** Every panel, button and chip is a paper cut-out: a parchment fill, a
  thick (about 3 px) warm dark-brown ink outline that wobbles slightly like a brush pen, a soft
  offset shadow beneath, and a rotation of up to ±1° (seeded per element, so it is stable).
- **Grain.** A faint paper-fibre grain is multiplied over every panel fill. It should be barely
  visible: it reads as paper, not as noise.
- **Ink.** Outlines, icons and text share one ink colour. Icons are hand-inked glyphs: wobbly
  strokes with a flat fill, drawn by the procedural icon painter.
- **Colour.** Warm parchment and earth: cream and tan papers, ink brown, muted greens, a
  warm gold for the main action, and a red only for urgent marks (badges, Spectate, VS).
  Pages read meaning tokens (`Tokens.uss`), never palette tokens or raw colours.
- **Type.** Patrick Hand for everything; size and weight carry hierarchy. Numbers are large.
- **Main action.** PLAY is a big gold cut-out with the ink outline and hand-lettered text,
  pressing down on tap.

## The 3D stage

- Cult of the Lamb's 2.5D approach: flat paper cut-out sprites standing in a 3D world, with ink
  outlines baked into the sprite. Districts are front-facing flats; villagers and marketmen are
  cut-out figures. The ocean is the unplaceable edge.
- All stage materials come from one factory so a later dither shader (circular mask; objects
  too close to the camera dither) can replace them.

## Motion

- Short and physical: things slide, drop and settle like paper (about 150–350 ms, ease-out).
  Presses squash slightly; nothing glows or fades in from nothing unless it is a reward.
