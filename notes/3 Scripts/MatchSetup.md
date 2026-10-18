---
shell: 3
up: "[[Map identity]]"
tags: [shell/3, realm/lobby, system/board, script]
file: Assets/Scripts/Game/Simulation/MatchSetup.cs
sources:
  - Assets/Scripts/Game/Simulation/MatchSetup.cs
verified: ce9fb6bf
---

# MatchSetup

> [!question] The question
> The four values two peers must agree on before drafting: how are they packaged, compared and verified?

**Where you are:** [[Home]] › [[Lobby]] › [[Board]] › [[Map identity]] › **MatchSetup** (script)

**Open:** [MatchSetup.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Simulation/MatchSetup.cs)

## What it is

110 lines: the `IBoardCatalog` interface (`TryGet(mapId, out board)`, returning a fresh copy) and the immutable `MatchSetup { MapId, BoardHash, SimulationVersion, BalanceHash }`.

## Read it in this order

1. **`IBoardCatalog`:** "the server checks logs against its own copy, never against a board or hash a client supplies."
2. **The immutable class:** get-only properties set in the constructor. "Explicit constructor and getters so it stays C# 9 and needs no record support." (Unity's C# version lags behind .NET, so newer features like `record` aren't always usable.)
3. **`ForShippedMap(mapId, balanceHash)`:** builds this build's claim; throws for an unknown map.
4. **`Verify(received, catalog, sim, balance, out error)`:** checks, in order: known map, board hash equals *the catalog's* board, same simulation version, same balance. Each failure gets a specific message.
5. **`Equals` / `GetHashCode`:** value equality, used by [[SetupAgreement]] to recognise a repeated proposal.

## C# lesson

If you override `Equals`, override `GetHashCode` consistently. Equal objects must have equal hash codes, or dictionaries and sets break. Here both are written by hand (note the `unchecked` `* 31` mixing again).

## Check yourself

> [!question]- A peer sends a real map ID with a board hash it computed from a modified board. Which check catches it?
> `BoardHasher.Hash(catalog board) != received.BoardHash`: the guest hashes *its own* copy of that map.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
