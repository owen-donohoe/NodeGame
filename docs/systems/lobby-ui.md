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
  - Assets/UI/Scripts/LobbyMouseDrag.cs
  - Assets/UI/Scripts/LobbyDragMath.cs
  - Assets/UI/Scripts/LobbyDeepLink.cs
  - Assets/UI/Scripts/LobbyOverlayRouter.cs
  - Assets/UI/Scripts/LobbyPageID.cs
  - Assets/UI/Scripts/LobbyRouteStack.cs
  - Assets/UI/Scripts/LobbyMotion.cs
  - Assets/UI/Scripts/LobbyElementMotion.cs
  - Assets/UI/Scripts/SwipeDecision.cs
  - Assets/UI/Scripts/AccountFlow.cs
  - Assets/UI/Scripts/AccountLinkPrompt.cs
  - Assets/UI/Scripts/ControlsPanel.cs
  - Assets/UI/Scripts/ControlsViewModel.cs
  - Assets/UI/Scripts/EraChips.cs
  - Assets/UI/Scripts/HomePage.cs
  - Assets/UI/Scripts/HomeModeDisplay.cs
  - Assets/UI/Scripts/LobbyMode.cs
  - Assets/UI/Scripts/LobbyModeCategory.cs
  - Assets/UI/Scripts/LobbyModePicker.cs
  - Assets/UI/Scripts/LobbyPlayFlow.cs
  - Assets/UI/Scripts/PrivateLobbyModel.cs
  - Assets/UI/Scripts/PrivateLobbyPanel.cs
  - Assets/UI/Scripts/MatchLauncherPrivateLobby.cs
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
  - Assets/UI/Scripts/LobbyToast.cs
  - Assets/UI/Scripts/LobbyToggle.cs
  - Assets/UI/Scripts/LobbyToggleMath.cs
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
  - Assets/UI/Scripts/HistoryOpponentNames.cs
  - Assets/UI/Scripts/SegmentedSwitch.cs
  - Assets/UI/Scripts/SegmentedSwitchMath.cs
  - Assets/UI/Scripts/UnlockState.cs
  - Assets/UI/Scripts/WorkshopInventorySession.cs
  - Assets/UI/Scripts/WorkshopPage.cs
verified: cab32ace
---

# Lobby UI

## Purpose
Loadouts, accounts/settings and match entry.

## Interface
`LobbyUIController` exposes `Navigation`, `Routes`, `Claims`, `Badges`, `Crates`, `ShowShot`. `NavigationController.Register/BindNavButton/Show/Has/CurrentPageID/PageShown` drives the five tabs. `LobbyPage.Root/ID/OnShow/OnHide` defines tab lifetime. `ProfilePage.Open(source)` expands self after layout preparation; `OpenFor(playerId)/Close` operates player sheets. `LobbyPushPage.Open/Close/Update` supports Settings/MatchHistory. `LobbyStage.ShowShot(StageShot)` retargets shots; `TryPick` returns `StagePick` indices. Fullscreen overlays hide the stage; Searching retains it.

`LobbyOverlayRouter` registers close callbacks on `LobbyRouteStack`. An optional `LobbyOverlay.BackHandler` consumes Back by returning true; otherwise Back closes the top, goes Home, or opens quit confirmation. `CrateScreen.Open(size, inventory)` runs `CrateOpeningSequence` as a fullscreen overlay. `LobbyDeepLink.TryParse` accepts nodewar routes; page/stall links navigate, bell opens notifications, profile IDs open panels. Overlays declare chrome visibility. `SwipeDecision` gates navigation by axis/travel/flick; overlays and `lb-no-swipe` block it. `ClaimPanel.Open(ClaimRequest)/Cancel` raises Confirmed/Cancelled in a centered `MkPanel`, including shop purchases.

`LobbyMotion` supplies springs, progressive overscroll and critical return. `Reduced` snaps springs, clamps scrolling, fades panels 80ms, stills villagers and pans 150ms. `LobbyMouseDrag` owns touch/mouse lists; mode/shop snapping follows edge return. `NavigationController.Tick/MotionChanged` shares page motion with stage/pills and preserves retarget velocity. `LobbyRouteStack.HidesPills` aggregates fullscreen routes.

Pages:
- `HomePage` exposes `PlayRequested`, `ModeRequested`, `CrateRequested`, `SetMode`, `Refresh`. BATTLE opens `PrivateLobbyPanel` for private selections, otherwise Searching. Incomplete loadouts dim it and show a toast. `LocalCrateInventory` backs Small stock/progress; Big maps to Medium with zero stock.
- `WorkshopPage` preserves framing and scroll during browsing/inspection. `LoadoutScreenModel.Use` fills or starts replacement; sheets remove editable used items. Locked tags/Fixed actions identify grants. `WorkshopStageBinding` maps editable picks to chosen slots; locked taps preserve replacement.
- `ShopPage.Bind(ClaimPanel, BadgeBoard)` shares claims/badges; `BindStage(LobbyStage)` projects offers. `ShopSnap/ShopProjection` coordinate entrances/exits; LateUpdate projects after the camera.
- `SocialPage.Bind(social, profiles, history, openProfile, sheet, guild)` supplies TV/Online/history ledgers. TV lists 0-4 games. Friends precede online guild members; offline friends expand behind a toggle. Settled history resolves missing handles through cached profiles (Unknown fallback) and shows signed RR. Requests cannot cross visits/accounts.
- `ProfilePage` reads self/others through `IProfileService`: null stats show dashes; self has a fixed header, 60/40 stats/loadout and RR-ladder layout, closing via header/swipe/Back, and a Rename panel (name+tag via `Handles.SetHandleAsync`, `Renamed` redraws chrome). Only internal lists scroll. Others show stats, loadout, recent W/L and Block/Report. `BindPanels(router, host)` wires reporting/confirmed Kick; `InviteRequested(playerId)` opens Multiplayer. `PlayerPanelPolicy` gates Kick on officer status and target membership.
- `GuildPage`: only Enter, a typing pause (300ms) or the caret raises search; focus never moves or collapses it, so result taps survive. Back, or empty background after clearing, centers it. The anchored filter bubble has RR/member sliders and Joinable only; member ranges filter locally. Results, members and announcements use ledgers; public details omit weekly progress. Membership shows progress/announcements/sortable members. `HasGuild`/`MembershipChanged` drive chrome. Officers choose View/Kick/Report; kicks confirm, reports log. Chat polls, capped at 200 characters.
- `SettingsPage.Changed(GameSettingsData)` and `Flush` persist preferences; `ControlsPanel`/`ControlsViewModel` share bindings, warnings and Reset with match settings.
- `MatchHistoryPage` uses `IMatchHistoryService` and `MatchHistoryRow` for recorded server outcomes; replay playback is unavailable.

`LobbyModePicker.Open(selection)` opens a parchment page with a snapping Ranked/Unranked/Other strip. `LobbyModeLayout` supplies two-row columns/empty slots. Private disables Ranked and converts its selection to Unranked 1v1; `LobbyModeSelection.FromProfile/Store` persists preferences. `SearchingScreen.Open/Close/Update` owns queue/launcher; Back cancels Home. Unranked 1v1 reuses ranked ticket/rendezvous without ranked reports; its isolated queue requires backend support. LAN/party searches and `MultiplayerSheet.Open(playerId)` log stubs.

`PrivateLobbyPanel.Open(LobbyModeSelection)` hosts; `Join(code)` receives `PlayPopup` guests. Hosts drag seats to swap ; Start freezes the roster. `MatchLauncher.HostPrivate/JoinPrivate/PrivateAction` synchronize seats and host authority over lobby-only Relay packets; Start then releases the launcher handshake after roster acknowledgment. Only 1v1 starts. Four-seat layouts seat one Relay guest. `LobbyPlayFlow.OpenPrivateInvite(code)` opens the Friends/Guild chooser and copies the code; delivery is stubbed.

`AccountFlow` coordinates account sheets and notifications. `AccountLinkPrompt`/`LinkPromptPolicy` avoid treating starter items as progress.

`LoadoutCatalog.CurrentLoadout(PlayerState)` resolves choices. `LoadoutRules.MissingCounts` counts min(empty slots, owned era-usable unequipped items) per side; `IsComplete` requires zero. `MissingMessage` names a single missing side. `LoadoutEditor` protects locks; `ClearChoices` restores them. Clear confirms in `MkPanel`. Missing/partial ownership never trims or saves choices; failures retain state and retry with backoff and one toast.

`UnlockStates.ForBase/ForVariant/ForSkin` supplies ownership, unlock arena and CanUse. `LobbyChrome` shows the server handle with rich text disabled, era progress and Coins/Premium balances; purse buttons route to shop stalls. Burger/bell open `LobbyOverlayPanel` modals. `SkinBand` renders the looping skin strip through `SkinBandMath`.

`PlayerProfile.Instance` persists loadout/settings/mode. `LoadoutPresets` stores edits in the active preset. `LoadoutData.CreateEmpty/Normalized` produce 3/5 arrays; legacy 2/2 saves gain defaults once. `LoadoutTypes.WithEquipment` overlays server equipment. `RankDisplay/RankTable` supply era progress; Premium displays as Laurels.

## Connects to
- `backend-client`: account, inventory/history services and ranked presenters return authoritative state; `StateChanged` refreshes pages.
- `progression`: rank thresholds, tree eligibility, stable catalog IDs and capacity policy govern choices.
- `match-flow`: launcher transfers frozen loadout and ordered ranked identity into `MatchConnection`.
- `netcode`: Relay/LAN launch and `IRankedConnection` perform peer connection.
- `draft`: complete normalized loadout arrays supply human match pools.
- `match-ui`: shared settings/Controls and scene transition support match entry/return.
- `input`: persisted bindings select intents; settings do not change simulation rules.
- `ui-art`: `LobbyIcon`, district art, family/tint fallbacks and shared theme style pages.
- `bots`: accepted queue fallback launches an unranked local bot match.

## Invariants
Rated rank/inventory belongs to backend state, not local trophies/unlock arrays. Local changes never resize an opponent's deck. Preserve frozen `LoadoutRecord.nodeIDs` storage identity. A preview click does not equip. Delayed equipment/account responses cannot redraw a different selection/account. Launch gates share completeness; canonical arrays retain blanks and match setup skips them.

## How it works
`LobbyManager.useUIToolkitLobby` selects Toolkit or legacy; missing roots warn and fall back. `UIToolkitLobbySetup.SetUp` authors the root/layouts/PanelSettings through Editor tools. Scene-referenced legacy panels remain compiled.

Era 0 locks Farm/Mine/Town and Warrior; era 1 locks Farm/Mine; era 2+ has no locks. Slots always total five districts/three suits. Town and Warrior become ordinary choices when unlocked. Suit positions survive replacement; villagers lift, swap, then lower. Cancel lowers unchanged. Tree suits have variants; both kinds have skins.

Launch freezes refreshed rank/choices/equipment and rechecks completeness. Ranked requires returned rank; private/offline uses arena 1. Rendezvous carries record ID/slot order. Only confirmed resizing persists.

## Decisions
- Use shared navigation/sheet/menu instances rather than independent modal stacks per page.
- Freeze launch data rather than let asynchronous rank updates reshape an in-flight match.
- Keep legacy code compiling behind a scene toggle rather than delete the fallback during migration.

## Pressure points
Legacy progression and backend stubs coexist; placeholder layouts require manual verification.

## Change recipes
- Add a page: implement lifecycle and register with navigation; overlays register close callbacks with the shared router. New layouts load from Resources/Lobby.
- Change loadouts: update pure editor/rules, shared capacity/ownership, persistence migration and frozen-launch tests together.

## Tests
`NodeWar.Lobby.Tests` guards loadouts, histograms, settings, presenters, route/state/claim/swipe models, crate sequences, paper masks and motion/projection. Interaction, rendering and scene wiring require Editor/device verification.
