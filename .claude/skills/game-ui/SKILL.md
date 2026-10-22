---
name: game-ui
description: Craft rules for Node War's lobby UI in Unity UI Toolkit (USS/UXML, not web CSS) — the approved panel language, readability, spacing, hierarchy, panels vs sheets, overflow. Use before building or restyling any lobby screen, together with .claude/plans/lobby-ui-spec.md.
---

# Game UI craft (UI Toolkit)

The layout contract is `.claude/plans/lobby-ui-spec.md`; this file is how to build it well.
For USS/UXML mechanics read the installed Unity UI Toolkit skill:
`C:\Users\owend\.claude\plugins\cache\claude-plugins-official\unity\0.1.6-beta\skills\ui-uitk\`
(`references/uss-guide.md` and `references/common-issues.md` are mandatory; `painter2d.md` and
`custom-elements.md` when drawing or making components). USS is not CSS: no grid, no `gap` on
older paths, no `calc()`, no `box-shadow`, no `:nth-child` — use flex, explicit margins,
percentages and child elements instead. Selector specificity still bites: prefer one class per
concern and avoid descendant chains that cancel each other.

## 0. The panel language (default, user-approved)

The visual contract is `docs/design-history/panel-language.html`; open it before restyling. In
summary:

- **One panel recipe**, taken from the top player card: a parchment fill, a 2 px outline, and a
  thicker bottom edge as the only shadow (`border-bottom-width` 7 for panels, 5–6 for buttons, rows
  and tiles). Pressing a button collapses that edge. Subtle paper grain, with no blur or glass.
  Corners are 14 for panels, 12 for fields, and 8–10 for buttons and avatars.
- **Three edge colours**, as tokens:
  - *outer* `#2d2319`: panels that sit on the 3D world;
  - *inner* `#4d3d2d`: anything outlined inside a panel;
  - *light* `#c3aa85`: only for a lip that sits inside a shaded region.
  Never use any other edge colour.
- **One outline level.** No outlined pill inside an outlined box. Inside a panel, things are flat:
  filled, sunk, or divided by hairlines. Only pressables (buttons, tiles, switches) carry an
  outline there, and it uses the inner colour.
- **Lists are ledgers**: one card with a darker header strip (title plus small meta on the right)
  and flat 56 px rows (40 px avatar, name over a meta line, trailing button), separated by
  hairlines. They replace the thin name panels.
- **2–3 option switch**:
  - the outer box has the outline and lip;
  - the shaded inner track has no lip;
  - the selected face is lighter, with a *light* lip, inset 4 px so the outer border always shows.
  A switch lives inside the panel it filters (Owned/All sits with the Workshop cards).
- **Pill nav is flat.** The selected tab is a gold face with an ink outline, with no lip or raise.
- **Page titles** (GUILDS) are stamped straight onto the world in Lilita with an ink outline, with
  no panel behind them.
- **The world shows through.** Pages have no full-screen backdrop, and scrims are 25%.
- **Live TV rows**: 48 px player cards (name over a 12 px RR, a 6 px player-colour edge on the
  outside) and a 46×58 red VS that stands taller than both.
- **Item sheet**: the variant dots sit inside the preview window. Below it are a Skins row of 52 px
  tiles (owned, selected, locked), then 10-cell stat histograms that fill the sheet width.
- **Icons: Phosphor Bold** only (MIT). Tint them with `-unity-background-image-tint-color`: ink on
  paper, paper on dark buttons.
- **Shop**: not covered by this contract yet; leave its look alone.

## 1. Readability first (the user's top complaint)

- Display (names, numbers, titles, buttons): **Lilita One**. Body and meta: **Fredoka SemiBold**.
  Every piece of text has `letter-spacing` of 0.04 em (about 0.6 px at 15 px). It should read
  lighter but stay just as bold. Do not use Patrick Hand or Kalam, and do not fake weight with text
  outlines.
- UI Toolkit measures text WITHOUT letter-spacing. A label sized to its own text (`flex-grow: 0`)
  is therefore too narrow and elides its last glyphs. Either let it grow into spare room, or
  size it with `TrackedText.Track` + `TrackedText.SetText` (see the player-card name).
- Sizes: title 34, panel heading 17–24, names 15–17, body 15–16, meta 13 (never smaller), BATTLE
  32. Line height at least 1.2× the size.
- Contrast: text on paper is full ink (`--paper-ink`); muted text is no lighter than 70% ink.
  No text on raw 3D.
- One line per label: `white-space: nowrap; overflow: hidden; text-overflow: ellipsis` on names,
  handles, guild names and offer titles. Long content must never change a container's height or
  make a fixed screen scroll.

## 2. Spacing on a 4/8 grid

- Allowed values: 4, 8, 12, 16, 24, 32. Card padding 12–16; gap between cards 12; gap inside a
  row 8; screen side margin 12 (matches the mockup).
- Align to edges: every card on a screen shares the same left/right margin. Text inside a card
  starts at the card's padding, not ad-hoc offsets.
- Touch targets ≥ 44×44 with ≥ 8 between neighbours. Primary actions in the bottom third.

## 3. Hierarchy

- One primary action per screen (gold). Secondary actions are outlined `mk-btn`; tertiary are
  chips or text. Destructive (Kick, Report) uses urgent red and always confirms.
- Size and weight carry importance, then colour. Titles > numbers > labels > captions.
- Structure is information: borders, dividers and eyebrows only where they group things.

## 4. Containers: panel vs sheet vs screen

- **Panel** (`MkPanel`): a centred modal card (width 340, max-height 80%, the section 0 recipe
  ), with a 25% scrim and a close ×; Back or a scrim tap closes it. Use it for
  Settings/Menu, Notifications, the Game-mode picker, the Multiplayer picker, and **every
  purchase or claim confirmation**.
- **Sheet** (`MkSheet`): bottom-anchored, handle, drag down to close. Use it for content you
  browse or edit in context: the item sheet, player panel, guild panel and filters.
- **Screen**: full-screen flows (Searching, Crate, Report, Guild chat).
- Never stack two sheets; a panel may open over a sheet.

## 5. Scroll and overflow

- Only lists scroll. Fixed screens (profile header, panels with short content, Searching) must
  not scroll. Size them to content, clamp with max-height, and ellipsize text.
- ScrollViews: `touchScrollBehavior = Elastic`, both scrollers hidden, and a content bottom
  padding equal to the pill nav's height plus 12 so the last item clears the nav.
- Clip masks: when a fixed header card overlaps scrolling content, put the clip edge at the
  header card's vertical middle so content corners never peek out behind it.

## 6. Self-check before you report

- Read every changed USS rule against sections 1–5.
- For each screen, list its text sizes, outline widths and the spacing values used. Anything off
  the 4/8 grid or under the minimum sizes is a bug.
- The lead captures 540×1200 screenshots. Write the report so each fix can be verified visually
  (what moved, by how much).
