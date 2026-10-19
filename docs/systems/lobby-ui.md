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
  - Assets/UI/Scripts/CrateOpeningSequence.cs
  - Assets/UI/Scripts/CrateScreen.cs
  - Assets/UI/Scripts/ShopResets.cs
  - Assets/UI/Scripts/ShopCountdown.cs
  - Assets/UI/Scripts/ShopSnap.cs
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
  - Assets/UI/Scripts/HomeModeDisplay.cs
  - Assets/UI/Scripts/LobbyBadgeDot.cs
  - Assets/UI/Scripts/LobbyOverlayPanel.cs
  - Assets/UI/Scripts/LocalCrateInventory.cs
  - Assets/UI/Scripts/StageLoadoutTypes.cs
  - Assets/Scripts/Lobby/Stage/
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
  - Assets/UI/Scripts/ProfileStatsFormat.cs
  - Assets/UI/Scripts/FriendPresenceOrder.cs
  - Assets/UI/Scripts/PlayerCardViews.cs
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
verified: da870622
---

# Lobby UI

## Purpose
The lobby lets players understand/edit their loadout, manage account/settings and enter matches. It presents server state without confusing local profile persistence, placeholder commerce or private play with authoritative ranked progression.

## Interface
`LobbyUIController` creates shared services/pages and exposes `Navigation`, `Routes`, `Claims`, `Badges` and `Crates`; Home crate taps open `CrateScreen`. `NavigationController.Register(LobbyPage)`, `BindNavButton`, `Show(LobbyPageID)`, `Has`, `CurrentPageID` and `PageShown` drive the track: Shop, Workshop (Loadout), Home, Social, Guild. Home opens first. `LobbyPage.Root/ID/OnShow/OnHide` define tab lifetime. `ProfilePage.Open(source)` opens self; `OpenFor(playerId)/Close` operates a Panel overlay; `LobbyPushPage.Open/Close/Update` supports Settings and MatchHistory. `LobbyStage` follows page shots and equipped types, hiding during fullscreen overlays.

`LobbyOverlayRouter` registers close callbacks on one `LobbyRouteStack`. An optional `LobbyOverlay.BackHandler` consumes Back by returning true; otherwise Back closes the top, goes Home, or opens quit confirmation. `CrateScreen.Open(size, inventory)` runs `CrateOpeningSequence` as a fullscreen overlay. `LobbyDeepLink.TryParse` accepts nodewar routes; page/stall links navigate, bell opens notifications, profile IDs open panels, remaining targets log. Overlays declare chrome visibility. `SwipeDecision` gates navigation by axis/travel/flick; overlays and `lb-no-swipe` block it. `BadgeBoard` sums dotted keys. `AsyncState.Reduce` retains stale content; `AsyncStateView.Render` delays loading by 300ms. `ClaimPanel.Open(ClaimRequest)/Cancel` raises Confirmed/Cancelled and shows plain-text disclosures.

`LobbySheet.Open(content, large)/Close`, `IsShowing`, `Closed` and `Lift(pageRoot, contentName)` serve shared bottom-sheet content. `LobbyContextMenu.Attach/Show/Close` displays `LobbyMenuItem(label, action)` rows; `LobbyToast.Show(message)` supplies transient feedback. Pages receive the shell's shared instances. `SafeAreaBinder` applies screen-safe insets.

Pages:
- `HomePage` exposes `PlayRequested`, `LoadoutRequested`, `ModeRequested`, `CrateRequested(CrateSize)`, `SetMode`, `Refresh`; incomplete PLAY opens Workshop. `LocalCrateInventory` backs Small crates/progress with profile data, returns fixed placeholder rewards, and exposes no Medium stock.
- `WorkshopPage` previews through `LoadoutScreenModel`, shows `LoadoutInfoModel`/`DistrictInfoFacts`, and uses `WorkshopInventorySession` to gate equipment requests against visit/selection changes.
- `ShopPage.Bind(ClaimPanel, BadgeBoard)` shares claims and sets `shop.daily`; `ScrollTo(stall)` targets daily, crates, weekly, bundle, coins or laurels. Stalls snap/shrink offers; countdowns refresh each second. Confirmations log; daily claims are visit-local.
- `SocialPage.Bind(social, profiles, history, openProfile, sheet)` supplies independent TV/history/friends `AsyncStateView`s. Cards open profiles; watch/replay/spectate log. Friends sort by presence, handle and ID. Late responses cannot cross visits or accounts.
- `ProfilePage` shows handle, era/RR, six stats, loadout chips and recent matches. Null stats display dashes. Other-player Add friend/Invite and in-battle Spectate log; self renames through `BackendServices.Handles`, retaining cost hints.
- `SettingsPage.Changed(GameSettingsData)` and `Flush` persist preferences; `ControlsPanel`/`ControlsViewModel` share bindings, warnings and Reset with match settings.
- `MatchHistoryPage` uses `IMatchHistoryService` and `MatchHistoryRow` for recorded server outcomes; replay playback is unavailable.

`PlayPopup.Show(GameMode)/Hide/Update/Dispose` hosts Ranked, private Relay/LAN and bot; `ModeChanged` updates Home. The burger opens Testing in development builds. `RankedQueueViewElement` implements `IRankedQueueView`; `RankedQueuePresenter` owns queue attempts. `MatchLauncher.PrepareAsync(ranked)`, `LaunchLocalAsync(bot)`, `HostRelay/HostLan`, `JoinRelay/JoinLan`, `HostRanked/JoinRanked`, `Update/Cancel/Dispose` expose phase, join code, failure/recovery and `Changed`. `MatchLauncherConnection` adapts these to `IRankedConnection` for `RankedRendezvous`.

`AccountFlow.EnsureAsync`, `LinkAsync(isActive)`, `SignInAsync(isActive)`, `SignOutAsync`, `CheckLinkPromptAsync(profile, isActive)`, `CloseDialog`, `Busy/BusyText/Message` and `Changed` coordinate account sheets. `AccountLinkPrompt`/`LinkPromptPolicy` avoid treating starter items as progress.

`LoadoutCatalog` resolves definitions, arena-specific offers, usable base ownership and `CurrentLoadout(PlayerState)`. `LoadoutRules` supplies chosen/display counts, grant locks, completeness, offer checks and fallback rank. Pure `LoadoutEditor` owns slot fill/clear, duplicate/availability handling and `ToLoadout`; its default constructor preserves canonical source shape, arena overload explicitly resizes local edits.

`LoadoutChoices.Variants/Skins` returns sorted choices. `UnlockStates.ForBase/ForVariant/ForSkin` supplies ownership, unlock arena and CanUse. `LobbyChrome` shows the server handle with rich text disabled, era progress and Coins/Premium balances; purse buttons route to shop stalls. Burger/bell open `LobbyOverlayPanel`s; Testing is development-only and bell clears its badge. `LobbyBadgeDot` observes dotted-key counts. The player header hides on Guild. `SkinBand.SetChoices/Stop`, `CenteredIndex` and `CenterChanged(int)` render the looping strip through `SkinBandMath`.

`PlayerProfile.Instance` persists local loadout/settings. `LoadoutData.CreateEmpty/Normalized` have shape-preserving and explicit-arena forms; `LoadoutTypes.WithEquipment` overlays server equipment. Arena displays use `RankDisplay`/`RankTable`.

`SceneTransition.Load(sceneName)` guards duplicate requests and shows loading during scene changes.

`CrateScreen.Open(CrateSize, ICrateInventory)` calls `TryOpen` and shows a fullscreen overlay only for a held crate. `CrateOpeningSequence` lands, waits for Tap, opens, reveals every returned reward, then requires Accept. Back acts as Tap or accepts Summary; no fake rewards appear.

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

Low arenas choose two suits/two districts with display-only grant locks; arena index 2 onward chooses three/five without grants. Workshop keeps three/five display positions. Use fills the first empty slot; filled slots clear. Variants are real tree suits; skins cover both item kinds. Home's mode, PLAY, held crates and progress sit over the stage; crate taps log for the screen caller.

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
`NodeWar.Lobby.Tests` guards loadouts, settings, presenters, route/state/claim/swipe models, crate sequences and shop time/snap models. Compile-check covers Unity pages/launchers. Sign-in, transitions, layouts, device Back, swipe feel and OS deep links require Editor/device verification.
