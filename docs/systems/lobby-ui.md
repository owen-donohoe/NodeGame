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
  - Assets/UI/Scripts/ShopProjection.cs
  - Assets/UI/Scripts/GuildPage.cs
  - Assets/UI/Scripts/GuildChatScreen.cs
  - Assets/UI/Scripts/GuildDataView.cs
  - Assets/UI/Scripts/GuildViews.cs
  - Assets/UI/Scripts/GuildSheetHost.cs
  - Assets/UI/Scripts/GuildReportScreen.cs
  - Assets/UI/Scripts/LobbyScrollbars.cs
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
  - Assets/UI/Scripts/LobbyMode.cs
  - Assets/UI/Scripts/LobbyModePicker.cs
  - Assets/UI/Scripts/LobbyPlayFlow.cs
  - Assets/UI/Scripts/PartyWaitModel.cs
  - Assets/UI/Scripts/PartyWaitScreen.cs
  - Assets/UI/Scripts/RecentOpponentList.cs
  - Assets/UI/Scripts/StageSlotMap.cs
  - Assets/UI/Scripts/WorkshopStageBinding.cs
  - Assets/UI/Scripts/LobbyCurrencyNames.cs
  - Assets/UI/Scripts/LobbyBadgeDot.cs
  - Assets/UI/Scripts/LobbyOverlayPanel.cs
  - Assets/UI/Scripts/LocalCrateInventory.cs
  - Assets/UI/Scripts/StageLoadoutTypes.cs
  - Assets/Scripts/Lobby/Stage/
  - Assets/UI/Scripts/LoadoutCatalog.cs
  - Assets/UI/Scripts/LoadoutChoices.cs
  - Assets/UI/Scripts/LoadoutEditor.cs
  - Assets/UI/Scripts/LoadoutInfoModel.cs
  - Assets/UI/Scripts/StatHistogram.cs
  - Assets/UI/Scripts/LoadoutRules.cs
  - Assets/UI/Scripts/LoadoutScreenModel.cs
  - Assets/UI/Scripts/LoadoutPresets.cs
  - Assets/UI/Scripts/LoadoutBrowse.cs
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
  - Assets/UI/Scripts/ProfileViews.cs
  - Assets/UI/Scripts/PlayerPanelPolicy.cs
  - Assets/UI/Scripts/SearchingScreen.cs
  - Assets/UI/Scripts/MultiplayerSheet.cs
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
verified: 023d3245
---

# Lobby UI

## Purpose
The lobby edits loadouts, manages account/settings and enters matches. Server progression stays separate from preferences, commerce stubs and private play.

## Interface
`LobbyUIController` exposes `Navigation`, `Routes`, `Claims`, `Badges`, `Crates` and `ShowShot`. `NavigationController.Register/BindNavButton/Show/Has/CurrentPageID/PageShown` drives Shop, Workshop, Home, Social, Guild; Home opens first. `LobbyPage.Root/ID/OnShow/OnHide` defines tab lifetime. `ProfilePage.Open(source)` expands self; `OpenFor(playerId)/Close` operates a player sheet. `LobbyPushPage.Open/Close/Update` supports Settings/MatchHistory. `LobbyStage.ShowShot(StageShot)` retargets eased Home/Districts/Suits/Ocean/Market moves; `TryPick` returns compact `StagePick` indices. Slots fill front-left/middle/right, back-left/right; suits use front-middle. Fullscreen overlays hide the stage; Searching keeps it visible.

`LobbyOverlayRouter` registers close callbacks on one `LobbyRouteStack`. An optional `LobbyOverlay.BackHandler` consumes Back by returning true; otherwise Back closes the top, goes Home, or opens quit confirmation. `CrateScreen.Open(size, inventory)` runs `CrateOpeningSequence` as a fullscreen overlay. `LobbyDeepLink.TryParse` accepts nodewar routes; page/stall links navigate, bell opens notifications, profile IDs open panels, remaining targets log. Overlays declare chrome visibility. `SwipeDecision` gates navigation by axis/travel/flick; overlays and `lb-no-swipe` block it. `BadgeBoard` sums dotted keys. `AsyncState.Reduce` retains stale content; `AsyncStateView.Render` delays loading by 300ms. `ClaimPanel.Open(ClaimRequest)/Cancel` raises Confirmed/Cancelled and shows plain-text disclosures.

`LobbySheet.Open(content, large)/Close`, `IsShowing`, `Closed` and `Lift(pageRoot, contentName)` serve shared bottom-sheet content. `LobbyContextMenu.Attach/Show/Close` displays `LobbyMenuItem(label, action)` rows; `LobbyToast.Show(message)` supplies transient feedback. Pages receive the shell's shared instances. `SafeAreaBinder` applies screen-safe insets.

Pages:
- `WorkshopPage` scrolls a stage spacer and two mk cards using `LoadoutBrowse`. `WorkshopStageBinding` maps uncovered taps and Districts/Suits shots. `WorkshopInventorySession` gates equipment/replacement with Cancel/Back. Its 78% MkSheet shows variants, skins and `StatHistogram` comparisons against base districts/suits and villagers.
- `HomePage` exposes `PlayRequested`, `LoadoutRequested`, `ModeRequested`, `CrateRequested`, `SetMode`, `Refresh`. BATTLE opens Searching; incomplete loadouts disable it and show a Workshop tag. Big/Small open crates. `LocalCrateInventory` backs Small stock/progress; Big maps to Medium and has zero local stock.
- `ShopPage.Bind(ClaimPanel, BadgeBoard)` shares claims/badges; `BindStage(LobbyStage)` projects. `ScrollTo(stall)` animates daily/crates/weekly/bundle/bank (coins/laurels aliases). `ShopSnap/ShopProjection` govern motion/culling/order. Daily anchors below header, others centrally. `LobbyStage.SetShopScroll(float)` dollies; `ShopCamera/ShopAnchor` project.
- `SocialPage.Bind(social, profiles, history, openProfile, sheet, guild)` supplies independent TV/Online/history `AsyncStateView`s. TV has four slots. Friends precede online guild members; offline friends expand behind a toggle. History uses settled outcomes and signed RR. Watch/replay/spectate log. Requests cannot cross visits/accounts.
- `ProfilePage` reads self/others through `IProfileService`: null stats show dashes; self has a 60/40 stats/loadout and RR-ladder layout, closing via header/swipe/Back. Players have stats, loadout, newest-ten W/L and Block/Report. `BindPanels(router, host)` wires reporting/confirmed Kick; `InviteRequested(playerId)` opens Multiplayer. `PlayerPanelPolicy` requires officer status and target membership for Kick/report removal.
- `GuildPage` receives shared sheets/profile callbacks. Search/browse open guild details. Home shows rewards, announcements and sortable members. `HasGuild`/`MembershipChanged` drive chrome. Officers choose View/Kick/Report; kicks confirm, fullscreen reports log. `GuildViews` builds mk components; `GuildSheetHost` adapts sheets. `OpenChatAsync` checks membership; chat polls and clamps text to 200 characters.
- `SettingsPage.Changed(GameSettingsData)` and `Flush` persist preferences; `ControlsPanel`/`ControlsViewModel` share bindings, warnings and Reset with match settings.
- `MatchHistoryPage` uses `IMatchHistoryService` and `MatchHistoryRow` for recorded server outcomes; replay playback is unavailable.

`LobbyModePicker.Open(selection)` supplies two-column cards and Private; `LobbyModeSelection.FromProfile/Store` preserves mode preferences. `SearchingScreen.Open/Close/Update` owns `RankedQueuePresenter`/launcher, visible inert chrome and Home shot. Back cancels to Home; failures offer Retry. Ranked and Bot launch; LAN/private/party searches log stubs. `MultiplayerSheet.Open(playerId)` logs an invite and enters Searching for Private/2v2/FFA. Legacy `PlayPopup` and `LobbyPlayFlow` retain transport/testing entry points.

`AccountFlow` coordinates account sheets, busy/result state and change notifications. `AccountLinkPrompt`/`LinkPromptPolicy` avoid treating starter items as progress.

`LoadoutCatalog.CurrentLoadout(PlayerState)` resolves eligible local choices. `LoadoutRules` supplies capacity, grants and completeness. Pure `LoadoutEditor` fills, clears and replaces chosen slots, refuses duplicates and preserves canonical shape; the arena overload explicitly resizes local edits.

`LoadoutChoices.Variants/Skins` returns sorted choices. `UnlockStates.ForBase/ForVariant/ForSkin` supplies ownership, unlock arena and CanUse. `LobbyChrome` shows the server handle with rich text disabled, era progress and Coins/Premium balances; purse buttons route to shop stalls. Burger/bell open `LobbyOverlayPanel`s; Testing is development-only and bell clears its badge. `LobbyBadgeDot` observes dotted-key counts. The player header hides for guild members; visitors retain it. `SkinBand.SetChoices/Stop`, `CenteredIndex` and `CenterChanged(int)` render the looping strip through `SkinBandMath`.

`PlayerProfile.Instance` persists local loadout/settings/mode. `LoadoutPresets` migrates current choices into preset 1; switches sanitize through `LoadoutEditor`, edits store the active preset. `LoadoutBrowse` filters ownership and sorts owned-first/era/name, name or role with ID ties. `LoadoutData.CreateEmpty/Normalized` preserve source shape or use an explicit arena; `LoadoutTypes.WithEquipment` overlays server equipment. `RankDisplay`/`RankTable` supply era progress. `LobbyCurrencyNames.Premium` labels Premium as Laurels in purses and purchase text.

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
`LobbyManager.useUIToolkitLobby` selects Toolkit or legacy; missing roots warn and fall back. `UIToolkitLobbySetup.SetUp` authors the root/layouts/PanelSettings through Editor tools. Scene-referenced legacy panels remain compiled.

Low arenas choose two suits/two districts with grant locks; arena 2 onward chooses three/five without grants. Workshop shows five stage nodes. Use fills or replaces; taps inspect. Variants are tree suits; skins cover both kinds.

Launch refreshes state, freezes rank/choices/equipment, then rechecks completeness. Ranked requires returned rank; private/offline uses arena 1. Rendezvous carries record ID/slot order. Confirmed resizing persists; closing an unconfirmed fallback cannot trim saved high decks.

## Decisions
- Use shared navigation/sheet/menu instances rather than independent modal stacks per page.
- Freeze launch data rather than let asynchronous rank updates reshape an in-flight match.
- Keep legacy code compiling behind a scene toggle rather than delete the fallback during migration.

## Pressure points
Legacy progression coexists with authoritative data. Shop/social/guild lack production backends. Placeholder art/layouts require manual verification.

## Change recipes
- Add a page: implement lifecycle and register with navigation; overlays register close callbacks with the shared router. New layouts load from Resources/Lobby.
- Change loadouts: update pure editor/rules, shared capacity/ownership, persistence migration and frozen-launch tests together.

## Tests
`NodeWar.Lobby.Tests` guards loadouts, histograms, settings, presenters, route/state/claim/swipe models, crate sequences, paper masks and motion/projection. Compile-check covers Unity pages/launchers. Sign-in, transitions, layouts, device Back, swipe feel and OS deep links require Editor/device verification.
