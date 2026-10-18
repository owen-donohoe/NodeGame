---
shell: 3
up: "[[Match launch]]"
tags: [shell/3, realm/lobby, system/lobby-ui, script]
file: Assets/UI/Scripts/MatchLauncher.cs
sources:
  - Assets/UI/Scripts/MatchLauncher.cs
verified: ce9fb6bf
---

# MatchLauncher

> [!question] The question
> The Play popup's buttons all end up here. How does one class handle bot, LAN, Relay and ranked launches, and fail with useful messages?

**Where you are:** [[Home]] › [[Lobby]] › [[Lobby UI]] › [[Match launch]] › **MatchLauncher** (script)

**Open:** [MatchLauncher.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/UI/Scripts/MatchLauncher.cs)

## What it is

"The networked match connection flow, with no UI attached." About 610 lines, with a `Phase` enum, `FailureMessage` + a recovery hint, and a `Changed` event the UI redraws from. Entry points: `PrepareAsync(ranked)`, `LaunchLocalAsync(bot)`, `HostRelay`, `JoinRelay(code)`, `HostLan`, `JoinLan(address)`, `HostRanked`, `JoinRanked`, `Update`, `Cancel`, `Dispose`.

## Read it in this order

1. **The header comment:** it explains why this deliberately *duplicates* the legacy modal's handshake ("the duplication is temporary and dies with NetworkingModal"), and why failures here are split into phases: `async void` Relay start-up "throws into nothing", so a deadline is the only signal.
2. **`PrepareAsync`:** the freeze (see [[Match launch]]). Notice `int version = ++preparationVersion; … if (version != preparationVersion) return false;`. That is a *generation counter*: a slow older call can't overwrite a newer one.
3. **`LaunchLocalAsync`:** the simplest complete path: prepare → recheck → `new GameObject("MatchConnection")` → fill → `SceneTransition.Load("Gameplay")`.
4. **The constants:** `RelaySetupTimeout = 12`, `HandshakeTimeout = 30`, `HandshakeRetryInterval = 0.3`, and `NoDeadline` for the private host wait.
5. **`Dispose`:** "so a socket is never orphaned."

## Lessons

- **Messages paired with actions:** every `Fail(message, whatToDo)` tells the player what to do next.
- **Generation counters** are a cheap, general way to ignore stale async results.

## Check yourself

> [!question]- Why no deadline for the private host?
> "Someone reading a code out to a friend should not lose the room because they took thirty seconds. Cancel is the way out."

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
