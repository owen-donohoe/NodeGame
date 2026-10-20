---
name: game-ui
description: Craft rules for Node War's lobby UI in Unity UI Toolkit (USS/UXML, not web CSS) — readability, spacing, hierarchy, panels vs sheets, overflow. Use before building or restyling any lobby screen, together with .claude/plans/lobby-ui-spec.md.
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

## 1. Readability first (the user's top complaint)

- Patrick Hand has one weight. Make text read as **bold** with
  `-unity-text-outline-width: 0.4px; -unity-text-outline-color: <same as text colour>` on all
  text, 0.6px on titles, numbers and buttons. Never rely on colour alone for emphasis.
- Minimum sizes at the 390×844 reference: body 16, secondary 14, captions/eyebrows 13 (never
  smaller), titles 22–26, big numbers 22+, BATTLE 32. Line height ≥ 1.2× size.
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

- **Panel** (`MkPanel`): a centred modal card (width 340, max-height 80%, radius 16, 2px ink
  border, 3px ink drop), with a dim scrim and a close ×; Back or a scrim tap closes it. Use it for
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
