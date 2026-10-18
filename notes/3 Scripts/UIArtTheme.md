---
shell: 3
up: "[[Semantic icons and fallbacks]]"
down: []
concepts: []
tags: [shell/3, realm/lobby, realm/match, system/ui-art, script]
card: docs/systems/ui-art.md
file: Assets/UI/Scripts/UIArtTheme.cs
sources:
  - Assets/UI/Scripts/UIArtTheme.cs
  - Assets/UI/Resources/UIArtTheme.asset
verified: ce9fb6bf
---

# UIArtTheme

> [!question] The question
> Where does an artist put icon overrides, and how does runtime code find them?

**Where you are:** [[Home]] › [[UI art]] › [[Semantic icons and fallbacks]] › **UIArtTheme** (script)

**Open:** [UIArtTheme.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/UI/Scripts/UIArtTheme.cs)

## What it is

A `ScriptableObject` containing ordered `IconEntry` rows and a `DistrictVisualTable` reference. The same file also contains static `UIArt`, the resource-loading access point. Policy lives in [[UIArtLookup]]; this file adapts that policy to real Sprite references.

## Read it in this order

1. **`IconEntry`.** Kind, context, sprite and `keepOriginalColours` describe an override. Context zero preserves Anywhere for older entries.
2. **`TryIcon`.** It passes delegates for kind/context/sprite presence to the pure lookup, then returns the selected entry. The comment is the contract: “Array order is authoritative, including an empty first entry.”
3. **`OnValidate`.** In the Editor, nested loops warn about duplicate pairs. They do not reorder or delete the rows.
4. **`UIArt.Theme`.** A `loaded` flag makes `Resources.Load<UIArtTheme>("UIArtTheme")` a one-time attempt until reset. Missing themes warn in Editor/development builds and leave callers to use defaults.
5. **`Reset`.** Subsystem registration clears both the flag and cached reference.

## Lessons to take away

The asset holds authoring data; the lookup owns precedence; the element draws the result. Keeping those jobs separate makes it possible to test precedence without manufacturing Unity Sprites.

The checked-in `Assets/UI/Resources/UIArtTheme.asset` has an empty icon array. Adding rows is an art-authoring step through Unity, not a requirement for screens to draw. Use a Play Mode restart after theme authoring changes; this is not a live theme-switching service.

## Check yourself

> [!question]- Why use a separate `loaded` flag instead of only testing whether `theme` is null?
> A missing resource also produces null. The flag remembers that the load was attempted, avoiding a fresh resource lookup on every access.

> [!question]- Does `OnValidate` make duplicate rows harmless?
> No. It warns. First-pair precedence still determines the result, so a bad first row must be fixed deliberately.

## Go deeper ↓

This is the lowest shell. Below it is the code itself. Read [[UIArtLookup]] next, then [[LobbyIcon]].

## My understanding

<!-- Yours. AI never edits below this line. -->
