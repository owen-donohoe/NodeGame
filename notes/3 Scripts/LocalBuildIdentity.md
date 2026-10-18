---
shell: 3
up: "[[Build identity handshake]]"
tags: [shell/3, realm/match, system/netcode, script]
file: Assets/Scripts/Game/Network/LocalBuildIdentity.cs
sources:
  - Assets/Scripts/Game/Network/LocalBuildIdentity.cs
verified: ce9fb6bf
---

# LocalBuildIdentity

> [!question] The question
> How does a build know *what it is*, in three numbers?

**Where you are:** [[Home]] › [[Match]] › [[Netcode]] › [[Build identity handshake]] › **LocalBuildIdentity** (script)

**Open:** [LocalBuildIdentity.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Network/LocalBuildIdentity.cs)

## What it is

A 66-line `static class` that lazily builds `BuildIdentity { protocol, sim, content }`:
- `protocol` = `InputSerializer.ProtocolVersion` (which aliases `Backend.ProtocolVersion.Current`, 6)
- `sim` = `SimulationVersion.Current`
- `content` = `BalanceHasher.Hash` of the shared `GameBalance` asset loaded from Resources

`SetupFor(mapId)` builds the `MatchSetup` the host proposes in [[SetupAgreement]].

## Details worth noticing

- **Lazy plus cached** (`resolved` flag): the balance asset is loaded and hashed once.
- **`[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` resets the cache** when entering
  Play Mode. With "Enter Play Mode Options" (domain reload off), static fields survive between
  play sessions in the Editor, so a stale identity could survive an edit to the balance. This
  attribute is the standard fix. It's a useful Unity lesson in its own right.
- **A missing asset hashes to 0**, logged as an error, so two broken builds still refuse a healthy one.

## Check yourself

> [!question]- You tweak one balance number in the Editor and press Play again. Without the reset attribute, what could go wrong?
> With domain reload disabled, `resolved` stays true from the last session, so the identity
> keeps the *old* content hash. You'd handshake as a build you no longer are.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
