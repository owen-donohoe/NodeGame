---
shell: 3
up:
  - "[[Match launch]]"
  - "[[Match flow]]"
tags: [shell/3, realm/lobby, system/match-flow, script]
file: Assets/Scripts/Game/Core/MatchConnection.cs
sources:
  - Assets/Scripts/Game/Core/MatchConnection.cs
verified: ce9fb6bf
---

# MatchConnection

> [!question] The question
> How does information cross from the Lobby scene into the Gameplay scene, in 68 lines?

**Where you are:** [[Home]] › [[Lobby]] › [[Match flow]] › **MatchConnection** (script)

**Open:** [MatchConnection.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Core/MatchConnection.cs)

## What it is

A MonoBehaviour **singleton** marked `DontDestroyOnLoad`, created by the lobby, read by `GameManager.Awake`, destroyed by `Shutdown()` when returning to the lobby.

```csharp
public NetworkManager networkManager;   // the live socket, parented under this object
public int localPlayerID;               // seat 0 or 1
public bool isNetworked, isBotMatch, isSpectator;
public LoadoutData loadout;             // frozen at launch
public bool isRanked; public string matchId; public string[] playerIds;   // server order
```

## Unity patterns in 68 lines

- **`DontDestroyOnLoad`**: objects normally die with their scene; this one survives scene loads.
- **The singleton guard in `Awake`**: a second instance destroys itself, so there's never two.
- **`[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` reset**: clears the static `instance` when entering Play Mode with domain reload off ([[LocalBuildIdentity]] has the same fix).
- **Explicit `Shutdown()`**: closes networking *then* destroys itself, so the socket is never leaked.

The ranked comment matters: the log "must carry the server's match ID and both player IDs in the record's order (slot 0 hosts as simulation player 0), or the referee refuses it" ([[MatchEligibility]]).

## Check yourself

> [!question]- What would happen if two `MatchConnection`s existed after a quick back-and-forth between scenes?
> `GameManager` could read the stale one: wrong seat, wrong loadout, or a dead socket. The guard in `Awake` prevents that.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
