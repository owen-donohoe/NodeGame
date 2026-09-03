# Legacy — the uGUI lobby, kept rather than deleted

Everything here was replaced by the UI Toolkit stack in `Assets/UI/` during
sessions S1–S4 of the phone-UI rebuild. It is **still live code**: it compiles,
it is still attached to GameObjects in `Assets/Scenes/Lobby.unity`, and
unticking `LobbyManager.useUIToolkitLobby` still brings all of it back exactly
as it was.

Moving it here is a staging step, not a deletion. The point is that the folder
you work in only contains the stack you are working on, while changing your mind
stays a `git mv` away rather than a `git revert`.

## Why moving is safe

Unity resolves script and prefab references by the GUID in the `.meta` file, not
by path. Every file here moved together with its `.meta`, so every scene
reference, prefab reference and inspector assignment still points at exactly the
same asset. Nothing was reserialised and no scene was edited.

Two things would break this and neither was done: moving a `.cs` without its
`.meta` (Unity would mint a new GUID and every reference would go missing), and
naming a folder something Unity treats specially — `Editor`, `Resources`,
`Plugins`, `StreamingAssets`, `Gizmos`. `Legacy` is an ordinary folder, so
everything in it still compiles into `Assembly-CSharp` exactly as before.

## What is here

| Path | Replaced by |
|---|---|
| `Lobby/Panels/HomepagePanel.cs` | `Assets/UI/Scripts/HomePage.cs` |
| `Lobby/Panels/GroupSelectionPanel.cs` | `Assets/UI/Scripts/WorkshopPage.cs` |
| `Lobby/Panels/ProfilePanel.cs` | `Assets/UI/Scripts/ProfilePage.cs` |
| `Lobby/Panels/ShopPanel.cs` | `Assets/UI/Scripts/ShopPage.cs` |
| `Lobby/Panels/GamemodePanel.cs` | the mode list inside `PlayPopup.cs` |
| `Lobby/Panels/NetworkingModal.cs` | `PlayPopup.cs` + `MatchLauncher.cs` |
| `Lobby/Panels/RenameModal.cs` | the inline editor on `ProfilePage` |
| `Lobby/Panels/LobbyPanel.cs` | `Assets/UI/Scripts/LobbyPage.cs` |
| `Lobby/UI/GroupSlotDisplay.cs` | `WorkshopPage.SlotView` |
| `Lobby/UI/SelectableItemDisplay.cs` | `WorkshopPage.ItemCell` |
| `Lobby/UI/TrophyBarDisplay.cs` | the trophy bar on `ProfilePage` |
| `Game/UI/SafeAreaFitter.cs` | `Assets/UI/Scripts/SafeAreaBinder.cs` |
| `Prefabs/Lobby/**` | nothing — the new stack uses UXML and no prefabs |

`GamemodePanel.cs` declares class `GameModePanel`, capital M. The filename and
the class name have never matched. Unity does not care, but a case-sensitive
search does.

## What deliberately did NOT move

- **`Assets/Scripts/Lobby/TrophyBarLogic.cs`** — the new `ProfilePage` uses it.
  It changed sides rather than being replaced.
- **`LobbyManager.cs`** — it survives the migration. It owns match launching,
  the profile bootstrap and the stack toggle.
- **`PlayerProfile.cs`, `LoadoutData.cs`, `NodeDefinition.cs`,
  `SuitDefinition.cs`** — data, used by both stacks.
- The in-match uGUI HUD (`HUDManager`, `WheelDisplay`, `BreachDisplay` and
  `UI_Manager.prefab`). S6 wrote a replacement but it has not been run, and
  `GameManager` still initialises the old one on every match.

## Changing your mind

Untick `LobbyManager.useUIToolkitLobby` in `Lobby.unity`. That is the whole
procedure — nothing here needs moving back for the old lobby to work.

To move a file back into the live tree, move the `.cs` **and** its `.meta`
together, and nothing else has to change.

## Deleting it for real

There is a verified order this has to happen in, and doing it out of order
leaves missing-script warnings or a project that will not compile — three of
these files are still referenced by `LobbyManager.cs` by name, and two of the
prefabs carry script GUIDs in their own file rather than only in the scene. Ask
before deleting; the sequence is recorded outside the repo.
