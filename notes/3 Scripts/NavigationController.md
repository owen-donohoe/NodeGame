---
shell: 3
up: "[[Lobby UI]]"
tags: [shell/3, realm/lobby, system/lobby-ui, script]
file: Assets/UI/Scripts/NavigationController.cs
sources:
  - Assets/UI/Scripts/NavigationController.cs
verified: 56ab2583
---

# NavigationController

> [!question] The question
> How do the lobby's tabs slide between pages, and keep each page's scroll position, in under 200 lines?

**Where you are:** [[Home]] › [[Lobby]] › [[Lobby UI]] › **NavigationController** (script)

**Open:** [NavigationController.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/UI/Scripts/NavigationController.cs)

## What it is

A plain class (not a MonoBehaviour) that "owns which tab page is showing and keeps the tab bar in step with it". API: `Register(LobbyPage)`, `BindNavButton(id, button)`, `BindSwipes`, `Show(id)`, `Has`, `CurrentPageID`, `PageShown`, `MotionChanged`, `Tick`. It is one physical strip, sampled once per frame, so pages, the nav pills and the stage camera share the same motion and swipes can be interrupted and retargeted.

## The design, from its comments

- **Pages sit side by side in one track**, in registration order; showing a page *slides the track*. "Every page stays in the tree, which also keeps each page's scroll position and element state between visits for free."
- **Pages are sized in code, not USS**, "because the page count is decided by registration, not by a stylesheet."
- **First layout isn't animated**, "so the lobby opens on Home rather than sliding to it from Shop."
- **Forgiving API:** binding a button before its page exists is safe, and showing an unregistered page is ignored "so a half-built lobby stays usable".
- **Profile, Settings and the crate screen aren't pages here:** they're overlays above the chrome, owned by the shell. The five pages (Shop, Workshop, Home, Social, Guild) have fixed slots in the strip.

## UI Toolkit lesson

Hiding with `display: none` destroys layout state, and rebuilding pages loses scroll. Keeping everything in the tree and *translating* a container is cheaper and preserves state. It's a common mobile-UI technique.

## Check yourself

> [!question]- What's the cost of keeping every page in the tree?
> Memory and per-frame layout for pages you can't see. That's fine for four tabs; with dozens you'd virtualise.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
