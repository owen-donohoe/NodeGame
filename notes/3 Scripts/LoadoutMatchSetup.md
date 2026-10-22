---
shell: 3
up:
  - "[[Match launch]]"
  - "[[Draft]]"
tags: [shell/3, realm/lobby, system/lobby-ui, system/draft, script]
file: Assets/Scripts/Lobby/Data/LoadoutMatchSetup.cs
sources:
  - Assets/Scripts/Lobby/Data/LoadoutMatchSetup.cs
verified: 56ab2583
---

# LoadoutMatchSetup

> [!question] The question
> The lobby stores loadouts as strings like `"suit_warrior"`. The simulation wants integer enums. Where's the translation, and how is it made identical on both machines?

**Where you are:** [[Home]] › [[Lobby]] › [[Draft]] › **LoadoutMatchSetup** (script)

**Open:** [LoadoutMatchSetup.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Lobby/Data/LoadoutMatchSetup.cs)

## What it is

An 84-line static class: "Resolves each player's canonical loadout without changing the authored board."

| Method | Returns |
|---|---|
| `DistrictPool(loadout, mapBasePool)` | Draft pieces: the loadout's districts, each tagged `IsFromLoadout`. The map base pool is no longer added; the era's locked default districts already sit inside the loadout |
| `DraftedSuits(loadout)` | `int[]` of `SuitType`, as stored (a locked Warrior is already one of the slots) |
| `DraftedDistricts(loadout)` | `int[]` of chosen `DistrictType`s |

## The key private method: `Sanitize`

Normalise a *detached copy*, then blank anything invalid: unknown types, Core, inactive districts, and **duplicates after the first**. "Slot order and canonical shape determine identical results on both peers." Each peer runs this on *both* loadouts (its own, and the opponent's received one), so even a slightly odd loadout resolves the same way on both sides.

## Lesson

**Normalise at the boundary.** Data from outside the deterministic world (lobby strings, user choices) is cleaned once, by one function, into a canonical form, so everything downstream sees the same thing.

## Check yourself

> [!question]- A loadout lists Warrior twice. What do both peers get?
> One Warrior. `Sanitize` blanks the second slot, and both peers do it identically.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
