---
shell: 3
up: "[[Lobby UI]]"
tags: [shell/3, realm/lobby, system/lobby-ui, script]
file: Assets/Scripts/Lobby/PlayerProfile.cs
sources:
  - Assets/Scripts/Lobby/PlayerProfile.cs
verified: 56ab2583
---

# PlayerProfile

> [!question] The question
> What does the game save on *your device*, how, and how does it survive the save format changing between versions?

**Where you are:** [[Home]] › [[Lobby]] › [[Lobby UI]] › **PlayerProfile** (script)

**Open:** [PlayerProfile.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Lobby/PlayerProfile.cs)

## What it is

"Persistent player data singleton… Local JSON persistence now, server-swappable later." It writes `player_profile.json` under `Application.persistentDataPath` with Unity's `JsonUtility`. It holds username, settings, local loadout choices (three presets, one active; `Loadout` reads and writes the active one), the account-link prompt flag, the last Workshop tab, and some legacy fields (trophies, boxes) that "nothing writes yet".

## Read it in this order

1. **`SavePath`, `Save`, `Load`:** `JsonUtility.ToJson(data, true)` → `File.WriteAllText`. It's the simplest possible persistence.
2. **`SetSettings`:** "skipping the disk entirely when nothing actually moved". The page commits on close, "once per visit, not once per pixel".
3. **The legacy migration** (bottom of the file): older saves stored `suitID0..nodeID1` fields. `JsonUtility` "does not error on those — it simply leaves suitIDs/nodeIDs null, which would silently wipe the player's selection". So the old names are read back and converted, then the file is rewritten so it's a one-time cost.

## Lessons

- **Schema migration for local saves** is the same problem as for server records ([[MatchRecord]]): old data must still load. Silent failure is the danger.
- **`JsonUtility` limits:** no dictionaries, and unknown and missing fields are silently ignored. Fine for simple data, but know the trap.
- **Local vs server:** nothing here is authoritative for ranked play ([[Server authority]]).

## Check yourself

> [!question]- Why is "JsonUtility doesn't throw on unknown fields" dangerous rather than convenient here?
> A renamed field reads as empty without any error, so a player's saved loadout would just disappear after an update. The explicit migration catches it.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
