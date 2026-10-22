---
shell: 3
up: "[[Lobby UI]]"
tags: [shell/3, realm/lobby, system/lobby-ui, script]
file: Assets/UI/Scripts/LoadoutEditor.cs
sources:
  - Assets/UI/Scripts/LoadoutEditor.cs
verified: 56ab2583
---

# LoadoutEditor

> [!question] The question
> When you tap "Use" on a suit in the Workshop, which slot does it go into, and where is that rule written so it can be tested?

**Where you are:** [[Home]] › [[Lobby]] › [[Lobby UI]] › **LoadoutEditor** (script)

**Open:** [LoadoutEditor.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/UI/Scripts/LoadoutEditor.cs)

## What it is

"The Workshop's working copy of a loadout, and every rule about what may go in it." It has no UI and no UnityEngine, and is linked into `NodeWar.Lobby.Tests`.

## Read it in this order

1. **The header:** why it's separate: "GroupSelectionPanel expresses the same rules across five [SerializeField] slot displays and a visibility pass, where they cannot be tested at all."
2. **Constructors:** `LoadoutEditor(source)` is the offline arena-1 case; `LoadoutEditor(source, arena)` restores that era's locked defaults (e.g. Warrior and Farm/Mine/Town in era 0) into the three suit and five district slots. Locked slots can be neither cleared nor replaced.
3. **`EquipSuit(id)`:** first empty slot, or `NoSlot` if blank, duplicate or full. The duplicate rule is explained: "two slots spent on one suit is indistinguishable in the draft from one, so it is a slot silently thrown away… the rule belongs with the data rather than with the view."
4. **`ReplaceSuitSlot`/`ReplaceDistrictSlot`, `ClearSuitSlot`, district equivalents, `ClearChoices`** (resets to the locked defaults), **and `DropUnavailable`**, which drops IDs no longer offered.
5. **`ToLoadout`:** back to `LoadoutData`.

## Lesson

**Extract the rules, leave the pixels.** This is [[Pure core, Unity shell]] applied to lobby UI.

## Check yourself

> [!question]- Why return `NoSlot` instead of throwing when the slots are full?
> A full loadout is a normal user situation, not a bug. The page shows a message; there's nothing exceptional about it.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
