---
shell: 3
up:
  - "[[Semantic icons and fallbacks]]"
  - "[[Shared UI theme]]"
down: []
concepts: []
tags: [shell/3, realm/lobby, realm/match, system/ui-art, script]
card: docs/systems/ui-art.md
file: Assets/UI/Scripts/LobbyIcon.cs
sources:
  - Assets/UI/Scripts/LobbyIcon.cs
  - Assets/UI/Scripts/LobbyIconUsage.cs
verified: ce9fb6bf
---

# LobbyIcon

> [!question] The question
> Why is a little icon a custom VisualElement with drawing code instead of a Unicode character in a Label?

**Where you are:** [[Home]] › [[UI art]] › [[Semantic icons and fallbacks]] › **LobbyIcon** (script)

**Open:** [LobbyIcon.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/UI/Scripts/LobbyIcon.cs)

## What it is

A UXML-enabled `VisualElement` used by the lobby and in-match indicators/emotes/resources. Its header explains the original problem: “The prototype uses Unicode symbols and emoji, which Fredoka does not carry.” It draws white vectors once and lets normal image tint styling color them, while theme sprites can replace them.

## Read it in this order

1. **`Kind` and `Context`.** Each UXML attribute setter refreshes art. The two-argument constructor sets context before assigning Kind.
2. **`RefreshArt`.** Ask [[UIArtTheme]] for a sprite; otherwise call `ImageFor`. Then choose white for original-color art, the stored resource tint if present, or clear the inline tint override so styling can apply.
3. **The default constructor.** Add `lb-icon`, ignore picking, fit the image, and register attachment-time validation in Editor/development builds.
4. **`ValidateUsage`.** None is intentionally unset. Other unlisted kind/context usages warn once per pair. Attachment timing avoids warning while UXML has assigned only one of the two attributes.
5. **`ImageFor`.** None returns null; the kind cache avoids redrawing known glyphs. Painter2D draws inside a 24×24 design box; transparent corner dots pin the bounds so glyphs keep a common scale and center.
6. **The switch, then one drawer.** Several semantic kinds share a fallback drawing. Unknown values get a placeholder. You can understand the lifecycle before reading every path coordinate.

## Lessons to take away

Art does not need to become interaction. `PickingMode.Ignore` keeps the icon from stealing a hit intended for its parent control. Replacing its background image should preserve that behavior.

`SetResourceTint` respects original-color sprites instead of blindly recoloring them. Cached glyphs and warned-usage sets reset on subsystem registration. This UI layer can use floats and dictionaries: the simulation-only restrictions do not ban those from presentation code.

The pure lookup tests prove selection policy, not the quality of Painter2D output. Compile-check catches type errors; tint, clipping and device rendering still need Unity.

## Check yourself

> [!question]- Why draw the vector white rather than in the resource's final color?
> White lets background-image tint supply the color, including style transitions. The generated shape can be shared rather than regenerated per color.

> [!question]- Why wait for attachment before validating initial UXML usage?
> Kind and Context are assigned separately. Checking too early could report a temporary combination that is never the attached element's actual usage.

## Go deeper ↓

This is the lowest shell. Below it is the code itself. Compare [[UIArtLookup]] for policy and [[Shared UI theme]] for styling.

## My understanding

<!-- Yours. AI never edits below this line. -->
