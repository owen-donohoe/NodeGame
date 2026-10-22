---
shell: 2
up:
  - "[[Lobby UI]]"
  - "[[Match flow]]"
down:
  - "[[MatchLauncher]]"
  - "[[MatchConnection]]"
  - "[[LoadoutMatchSetup]]"
tags: [shell/2, realm/lobby, system/lobby-ui, system/match-flow]
sources:
  - Assets/UI/Scripts/MatchLauncher.cs
  - Assets/Scripts/Game/Core/MatchConnection.cs
  - Assets/Scripts/Lobby/Data/LoadoutMatchSetup.cs
verified: 56ab2583
---

# Match launch

> [!question] The question
> You press Play. What exactly is captured at that instant, and how does it survive a scene change into the Gameplay scene?

**Where you are:** [[Home]] › [[Lobby]] › [[Lobby UI]] › **Match launch** (mechanism)

## The problem

Unity destroys every object in a scene when it loads another. The lobby's knowledge (your loadout, your rank, the network socket, the ranked match ID) would vanish. And the lobby keeps changing while you connect (rank refreshes, Workshop edits), so "what the match should use" has to be pinned down at one moment.

## The idea

**Prepare → freeze → connect → hand over. `MatchLauncher.PrepareAsync` refreshes server state, sanitises and freezes the loadout; the connection is made; a `MatchConnection` object marked `DontDestroyOnLoad` carries the frozen setup into the Gameplay scene, where `GameManager.Awake` reads it.**

## How it works here

1. **Prepare** (`PrepareAsync(ranked)`): fetch `PlayerState`. A version counter (`++preparationVersion`) discards an answer if a newer prepare started meanwhile. Refuse if the account changed. Ranked *requires* a returned rank; private and offline fall back to the low arena.
2. **Freeze:** copy rank and inventory into `launchState`; build the loadout from the catalog for that arena; refuse if incomplete ("Fill your loadout"); `frozenLoadout = LoadoutTypes.WithEquipment(...)`.
3. **Connect:**
   - Local/bot: no network.
   - Relay/LAN private: host waits *open-ended* (someone reading a code to a friend shouldn't time out); a join by code has separate deadlines for relay setup (12 s) and handshake (30 s). A relay setup that reports failure ends the phase at once rather than waiting out the deadline, and one the launcher has since cancelled is ignored. A wrong code fails fast with a useful message.
   - Ranked: slot 0 hosts as simulation player 0 ("the referee reads the log's player order as the record's"), with deadlines everywhere; failure re-queues.
   - The handshake swaps [[Build identity handshake|build identities]] (acks sent ×3).
4. **Recheck** the loadout against the frozen arena, then create `MatchConnection` (seat, networked/bot flags, frozen loadout, ranked `matchId` and ordered `playerIds`), parent the `NetworkManager` under it, and `SceneTransition.Load("Gameplay")`.
5. **In Gameplay:** `LoadoutMatchSetup` turns the lobby's string IDs into simulation data: drafted suits and districts, and the draft pool. Nothing is added at this point: the era's locked default suits and districts already sit inside the loadout arrays, after sanitising duplicates and inactive types "so both peers get identical results".

## Why this, not that

| Decision | Alternative | Why the alternative lost |
|---|---|---|
| Freeze at launch | Read live state during connect | An async answer could reshape an in-flight match |
| `DontDestroyOnLoad` carrier | Static fields / PlayerPrefs | The socket is a live object; it has to move as one |
| Separate deadlines per phase | One 30 s "Timed out." | A wrong join code and a slow friend look identical; separate phases give "a message and a recovery action" |

## Check yourself

> [!question]- Why does ranked *require* a returned rank, while private play can fall back to arena 1?
> Ranked eligibility is checked by the server against the pre-match snapshot. Guessing an arena could build a loadout the referee then refuses. Private play is never rated, so a safe low-arena fallback is fine.

## Go deeper ↓

- [[MatchLauncher]] · [[MatchConnection]] · [[LoadoutMatchSetup]]

## My understanding

<!-- Yours. AI never edits below this line. -->
