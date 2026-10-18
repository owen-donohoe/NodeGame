---
shell: 2
up:
  - "[[Lobby UI]]"
  - "[[Match UI]]"
down:
  - "[[GameplayHUDController]]"
  - "[[DraftScreenController]]"
tags: [shell/2, realm/lobby, realm/match, system/lobby-ui, system/match-ui]
sources:
  - Assets/Scripts/Lobby/LobbyManager.cs
  - Assets/Scripts/Game/Core/GameManager.cs
verified: 41a0df69
---

# Three UI stacks

> [!question] The question
> Why does the project contain two complete lobbies, two HUDs and two draft screens, and how do you know which one is actually running?

**Where you are:** [[Home]] › [[Lobby]] › [[Lobby UI]] › **Three UI stacks** (mechanism)

## The problem

The UI is being migrated from Unity's older **uGUI** (Canvas, GameObjects, `RectTransform`) to **UI Toolkit** (UXML layout files + USS stylesheets, HTML/CSS-like). Rewriting everything at once would leave the game unplayable mid-migration, and a broken new screen with no fallback would block testing.

## The idea

**Run both, switched by a serialized checkbox per screen, with the old one "one tick away" until the new one is proven.**

| Scene value | Chooses | Fallback |
|---|---|---|
| `LobbyManager.useUIToolkitLobby` | New lobby shell vs `Assets/Legacy/` uGUI lobby | Missing Toolkit root → warn, use legacy |
| `GameManager.useUIToolkitHUD` | `GameplayHUDController` vs uGUI `HUDManager` | Hides the legacy HUD only after the new one initialises |
| `GameManager.useUIToolkitDraft` | `DraftScreenController` vs uGUI `DraftUI` prefab | "A draft you cannot see is a match you cannot start" |

"Deliberately a serialized bool rather than a #define or a build flag: the point of the toggle is that it can be flipped in the inspector mid-session." The third tree is the retired uGUI lobby under `Assets/Legacy/`, still compiled and scene-referenced.

## uGUI vs UI Toolkit, briefly

| | uGUI | UI Toolkit |
|---|---|---|
| Model | GameObjects with components | A retained-mode visual tree from UXML |
| Styling | Per-component Inspector values | USS stylesheets (selectors, classes) |
| Layout | Anchors, `RectTransform`, layout groups | Flexbox |
| Good for | World-space UI, legacy code | Complex screens, theming, reuse |

## What it costs

Every presentation change must know which stack runs; features drift (uGUI draft is mouse-only, uGUI HUD lacks the hold overlay); and the legacy tap arbiter (`NodePanelManager`) stays in charge of taps even under the Toolkit HUD.

## Check yourself

> [!question]- Why is "exactly one draft presenter" an invariant?
> Two presenters would both report parked pieces and both call `ConfirmLocalPlacement`, so the draft could receive conflicting placements. `CreateDraftPresenter` returns one and instantiates nothing it didn't choose.

## Go deeper ↓

- [[GameplayHUDController]] · [[DraftScreenController]]

## My understanding

<!-- Yours. AI never edits below this line. -->
