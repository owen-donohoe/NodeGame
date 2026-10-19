---
system: lobby-ui
summary: Presents account, loadout and ranked entry flows while keeping local preferences separate from server progression.
sources:
  - Assets/Legacy/
  - Assets/Scripts/Lobby/Data/DistrictDefinition.cs
  - Assets/Scripts/Lobby/Data/LoadoutData.cs
  - Assets/Scripts/Lobby/Data/LoadoutMatchSetup.cs
  - Assets/Scripts/Lobby/Data/LoadoutRecord.cs
  - Assets/Scripts/Lobby/Data/LoadoutTypes.cs
  - Assets/Scripts/Lobby/Data/PlayerProfileData.cs
  - Assets/Scripts/Lobby/Data/SuitDefinition.cs
  - Assets/Scripts/Lobby/LobbyManager.cs
  - Assets/Scripts/Lobby/PlayerProfile.cs
  - Assets/Scripts/Lobby/TrophyBarLogic.cs
  - Assets/UI/Layouts/HomePage.uxml
  - Assets/UI/Layouts/LobbyRoot.uxml
  - Assets/UI/Layouts/MatchHistoryPage.uxml
  - Assets/UI/Layouts/PlayPopup.uxml
  - Assets/UI/Layouts/ProfilePage.uxml
  - Assets/UI/Layouts/SettingsPage.uxml
  - Assets/UI/Layouts/ShopPage.uxml
  - Assets/UI/Layouts/SocialPage.uxml
  - Assets/UI/Layouts/WorkshopPage.uxml
  - Assets/UI/Resources/Lobby/
  - Assets/UI/Scripts/AsyncState.cs
  - Assets/UI/Scripts/AsyncStateView.cs
  - Assets/UI/Scripts/BadgeBoard.cs
  - Assets/UI/Scripts/ClaimPanel.cs
  - Assets/UI/Scripts/ClaimRequest.cs
  - Assets/UI/Scripts/GuildPage.cs
  - Assets/UI/Scripts/LobbyDeepLink.cs
  - Assets/UI/Scripts/LobbyOverlayRouter.cs
  - Assets/UI/Scripts/LobbyPageID.cs
  - Assets/UI/Scripts/LobbyRouteStack.cs
  - Assets/UI/Scripts/SwipeDecision.cs
  - Assets/UI/Scripts/AccountFlow.cs
  - Assets/UI/Scripts/AccountLinkPrompt.cs
  - Assets/UI/Scripts/ControlsPanel.cs
  - Assets/UI/Scripts/ControlsViewModel.cs
  - Assets/UI/Scripts/EraChips.cs
  - Assets/UI/Scripts/HomePage.cs
  - Assets/UI/Scripts/LoadoutCatalog.cs
  - Assets/UI/Scripts/LoadoutChoices.cs
  - Assets/UI/Scripts/LoadoutEditor.cs
  - Assets/UI/Scripts/LoadoutInfoModel.cs
  - Assets/UI/Scripts/LoadoutRules.cs
  - Assets/UI/Scripts/LoadoutScreenModel.cs
  - Assets/UI/Scripts/LobbyBackdrop.cs
  - Assets/UI/Scripts/LobbyChrome.cs
  - Assets/UI/Scripts/LobbyContextMenu.cs
  - Assets/UI/Scripts/LobbyPage.cs
  - Assets/UI/Scripts/LobbyPushPage.cs
  - Assets/UI/Scripts/LobbySheet.cs
  - Assets/UI/Scripts/LobbySwitch.cs
  - Assets/UI/Scripts/LobbyToast.cs
  - Assets/UI/Scripts/LobbyUIController.cs
  - Assets/UI/Scripts/LongPressManipulator.cs
  - Assets/UI/Scripts/MatchHistoryPage.cs
  - Assets/UI/Scripts/MatchHistoryRow.cs
  - Assets/UI/Scripts/MatchLauncher.cs
  - Assets/UI/Scripts/MatchLauncherConnection.cs
  - Assets/UI/Scripts/NavigationController.cs
  - Assets/UI/Scripts/PlaceholderPage.cs
  - Assets/UI/Scripts/PlayPopup.cs
  - Assets/UI/Scripts/ProfilePage.cs
  - Assets/UI/Scripts/RankDisplay.cs
  - Assets/UI/Scripts/RankedQueueViewElement.cs
  - Assets/UI/Scripts/SafeAreaBinder.cs
  - Assets/UI/Scripts/SceneTransition.cs
  - Assets/UI/Scripts/SettingsPage.cs
  - Assets/UI/Scripts/ShopPage.cs
  - Assets/UI/Scripts/SkinBand.cs
  - Assets/UI/Scripts/SkinBandMath.cs
  - Assets/UI/Scripts/SocialPage.cs
  - Assets/UI/Scripts/UnlockState.cs
  - Assets/UI/Scripts/WorkshopInventorySession.cs
  - Assets/UI/Scripts/WorkshopPage.cs
verified: e66eeef4
---

# Lobby UI

## Purpose
The lobby lets players understand/edit their loadout, manage account/settings and enter matches. It presents server state without confusing local profile persistence, placeholder commerce or private play with authoritative ranked progression.

## Interface
`LobbyUIController` creates shared services/pages and exposes `Navigation`, `Routes`, `Claims` and `Badges`. `NavigationController.Register(LobbyPage)`, `BindNavButton`, `Show(LobbyPageID)`, `Has`, `CurrentPageID` and `PageShown` drive the sliding track: Shop, Workshop (Loadout), Home, Social, Guild. Home opens first. `LobbyPage.Root/ID/OnShow/OnHide` define tab lifetime. `ProfilePage.Open(source)/Close` expands above chrome; `LobbyPushPage.Open/Close/Update` supports Settings and MatchHistory.

`LobbyOverlayRouter` registers close callbacks on one `LobbyRouteStack`. Back closes its top, goes Home, or opens quit confirmation. `LobbyDeepLink.TryParse` accepts nodewar routes; page links navigate, other targets log. Overlays declare chrome visibility. `SwipeDecision` gates adjacent navigation by axis/travel/flick; overlays and `lb-no-swipe` block it. `BadgeBoard` sums dotted keys. `AsyncState.Reduce` retains stale content; `AsyncStateView.Render` delays loading by 300ms. `ClaimPanel.Open(ClaimRequest)/Cancel` raises Confirmed/Cancelled and shows plain-text disclosures.

`LobbySheet.Open(content, large)/Close`, `IsShowing`, `Closed` and `Lift(pageRoot, contentName)` serve shared bottom-sheet content. `LobbyContextMenu.Attach/Show/Close` displays `LobbyMenuItem(label, action)` rows; `LobbyToast.Show(message)` supplies transient feedback. Pages receive the shell's shared instances. `SafeAreaBinder` applies screen-safe insets.

Pages:
- `HomePage` exposes `PlayRequested`, `LoadoutRequested`, `Refresh`; chosen chips follow rank, and incomplete Battle opens Workshop with a reason.
- `WorkshopPage` previews through `LoadoutScreenModel`, shows `LoadoutInfoModel`/`DistrictInfoFacts`, and uses `WorkshopInventorySession` to gate equipment requests against visit/selection changes.
- `ShopPage` shows illustrative offers/details and placeholder box/commerce interactions; `SocialPage` presents the social surface without a live social backend.
- `ProfilePage` renames through `BackendServices.Handles`, showing Name#TAG and the rename cost, plus server-backed arena progression; detailed match statistics remain placeholders.
- `SettingsPage.Changed(GameSettingsData)` and `Flush` persist preferences; `ControlsPanel`/`ControlsViewModel` share bindings, warnings and Reset with match settings.
- `MatchHistoryPage` uses `IMatchHistoryService` and `MatchHistoryRow` for recorded server outcomes; replay playback is unavailable.

`PlayPopup.Show/Hide/Update/Dispose` hosts Ranked, private Relay/LAN, bot and testing entry. `RankedQueueViewElement` implements `IRankedQueueView`; `RankedQueuePresenter` owns queue attempts. `MatchLauncher.PrepareAsync(ranked)`, `LaunchLocalAsync(bot)`, `HostRelay/HostLan`, `JoinRelay/JoinLan`, `HostRanked/JoinRanked`, `Update/Cancel/Dispose` expose phase, join code, failure/recovery and `Changed`. `MatchLauncherConnection` adapts these to `IRankedConnection` for `RankedRendezvous`.

`AccountFlow.EnsureAsync`, `LinkAsync(isActive)`, `SignInAsync(isActive)`, `SignOutAsync`, `CheckLinkPromptAsync(profile, isActive)`, `CloseDialog`, `Busy/BusyText/Message` and `Changed` coordinate account sheets. `AccountLinkPrompt`/`LinkPromptPolicy` avoid treating starter items as progress.

`LoadoutCatalog` resolves definitions, arena-specific offers, usable base ownership and `CurrentLoadout(PlayerState)`. `LoadoutRules` supplies chosen/display counts, grant locks, completeness, offer checks and fallback rank. Pure `LoadoutEditor` owns slot fill/clear, duplicate/availability handling and `ToLoadout`; its default constructor preserves canonical source shape, arena overload explicitly resizes local edits.

`LoadoutChoices.Variants/Skins` returns sorted `LoadoutChoice[]` with ID/base/label, unlock state, equipped/preview flags and Selectable. `UnlockStates.ForBase/ForVariant/ForSkin` supplies Owned/CanUnlock/CannotUnlock, unlock arena and CanUse; `ForBase(lobbyId, state)` reads server ownership of the base's era-0 variant (the starter kit before state arrives) and `ContentTable` debut eras. `LobbyChrome` shows the server handle (local username until a profile exists, requesting a first handle once per load) and the server wallet in the purses. Player-name labels disable rich text. `SkinBand.SetChoices/Stop`, `CenteredIndex` and `CenterChanged(int)` render a looping thirteen-tile strip through `SkinBandMath`.

`PlayerProfile.Instance` persists local loadout/settings. `LoadoutData.CreateEmpty/Normalized` have shape-preserving and explicit-arena forms; `LoadoutTypes.WithEquipment` overlays server equipment. Arena displays use `RankDisplay`/`RankTable`.

`SceneTransition.Load(sceneName)` guards duplicate requests and shows loading during scene changes.

## Connects to
- `backend-client`: account, inventory/history services and ranked presenters return authoritative state; `StateChanged` refreshes pages.
- `progression`: rank thresholds, tree eligibility, stable catalog IDs and capacity policy govern choices.
- `match-flow`: launcher transfers frozen loadout and ordered ranked identity into `MatchConnection`.
- `netcode`: Relay/LAN launch and `IRankedConnection` perform peer connection.
- `draft`: normalized chosen loadouts determine grants/pools at match setup.
- `match-ui`: shared settings/Controls and scene transition support match entry/return.
- `input`: persisted bindings select intents; settings do not change simulation rules.
- `ui-art`: `LobbyIcon`, district art, family/tint fallbacks and shared theme style pages.
- `bots`: accepted queue fallback launches an unranked local bot match.

## Invariants
Rated rank/inventory belongs to backend state, not local trophies/unlock arrays. Local changes never resize an opponent's deck. Preserve frozen `LoadoutRecord.nodeIDs` storage identity. A preview click does not equip. Delayed equipment/account responses cannot redraw a different selection/account. Missing slots block player-side launch; simulation setup itself does not enforce completeness.

## How it works
`LobbyManager` bootstraps profile and selects stacks through scene value `useUIToolkitLobby`; an unassigned Toolkit root warns and falls back. `UIToolkitLobbySetup.SetUp`, menu `Tools > Node War > Set Up UI Toolkit Lobby`, creates/wires Lobby root, layouts and separate LobbyPanelSettings through the Editor. `Assets/Legacy/` retains compiled, scene-referenced uGUI lobby panels; disabling the toggle can restore them.

Low arenas choose two suits/two districts with display-only grant locks; arena index 2 onward chooses three/five without grants. Workshop keeps three/five display positions while Home shows four/eight chosen chips. Use fills the first empty slot; filled slots clear. Variants are only real tree suits; skins work for both item kinds. Local inventory sheets stay below the header/loadout rows; box purchase only toasts.

Launch refreshes PlayerState; ranked requires returned rank, private/offline falls back to arena 1. It freezes rank, sanitized choices and equipment before connecting, then rechecks completeness against that frozen arena. Ranked rendezvous carries record ID/slot order; private host wait is open-ended. Confirmed local resizing persists; merely closing an unconfirmed fallback Workshop view cannot trim a saved high deck.

## Decisions
- Use shared navigation/sheet/menu instances rather than independent modal stacks per page.
- Freeze launch data rather than let asynchronous rank updates reshape an in-flight match.
- Keep legacy code compiling behind a scene toggle rather than delete the fallback during migration.

## Pressure points
Local profile still carries legacy progression fields beside authoritative backend data. Shop, social, detailed statistics and box actions lack production backing. Runtime and legacy launch paths coexist. Placeholder item art, narrow layouts and long Controls lists need manual verification; hidden legacy dependencies prevent casual deletion.

## Change recipes
- Add a page: implement lifecycle and register with navigation; overlays register close callbacks with the shared router. New layouts load from Resources/Lobby.
- Change loadouts: update pure editor/rules, shared capacity/ownership, persistence migration and frozen-launch tests together.

## Tests
`NodeWar.Lobby.Tests` guards loadouts, settings, ranked presenters and pure route/state/claim/swipe models. Compile-check covers Unity pages/launchers. Sign-in, transitions, layouts, device Back, swipe feel and OS deep links require Editor/device verification.
