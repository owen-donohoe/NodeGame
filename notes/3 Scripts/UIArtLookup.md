---
shell: 3
up: "[[Semantic icons and fallbacks]]"
down: []
concepts: []
tags: [shell/3, realm/lobby, realm/match, system/ui-art, script]
card: docs/systems/ui-art.md
file: Assets/UI/Scripts/UIArtLookup.cs
sources:
  - Assets/UI/Scripts/UIArtLookup.cs
  - dotnet/NodeWar.View.Tests/UIArtLookupTests.cs
verified: ce9fb6bf
---

# UIArtLookup

> [!question] The question
> How can sprite precedence be tested without loading a Unity asset?

**Where you are:** [[Home]] › [[UI art]] › [[Semantic icons and fallbacks]] › **UIArtLookup** (script)

**Open:** [UIArtLookup.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/UI/Scripts/UIArtLookup.cs)

## What it is

A pure static helper returning an array index, or `-1` for generated art. Its summary calls the job “Theme sprite precedence, independent of Unity asset types.” Generic entries and accessor delegates let runtime code pass real theme rows while tests pass tiny structs with a `HasSprite` boolean.

## Read it in this order

1. **`ForPair`.** A forward array scan returns at the first matching kind/context pair. The conditional returns either that index or `-1`; it never searches past an empty duplicate.
2. **`SpriteIndex`.** Resolve the requested pair first. A found sprite returns immediately. If the requested context was already Anywhere, stop; otherwise try Anywhere once.
3. **The test `EmptyFirstExactFallsBackToAnywhereWithoutUsingLaterExactDuplicate`.** Its long name captures the easy-to-misread branch.
4. **The tests for missing art and exact precedence.** They show null arrays, absent kinds, empty rows and an Anywhere row preceding a more specific one.

## Lessons to take away

The return value is a decision, not an image. This helper does not draw, load resources, choose tints or validate whether an icon has an active usage. Those jobs belong elsewhere.

An alternative “first nonempty sprite” scan would make incomplete earlier rows transparent. This implementation deliberately treats array order as authoritative within each pair. The gain is a simple, pinned policy; the cost is that duplicate authoring mistakes remain meaningful.

## Check yourself

> [!question]- Anywhere sprite A is array element 0; exact-context sprite B is element 7. Which wins?
> B. Exact and Anywhere are separate scans, so whole-array order does not outrank context specificity.

> [!question]- Does a return value of `-1` mean the icon should be invisible?
> It means use generated art. `LobbyIcon` decides what that means for the kind; None intentionally has no generated image, while unknown kinds get a placeholder.

## Go deeper ↓

This is the lowest shell. Below it is the code itself. Its tests are in `dotnet/NodeWar.View.Tests/UIArtLookupTests.cs`.

## My understanding

<!-- Yours. AI never edits below this line. -->
