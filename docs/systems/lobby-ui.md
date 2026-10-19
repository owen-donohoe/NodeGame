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
  - Assets/UI/Scripts/LoadoutRules.cs
  - Assets/UI/Scripts/LoadoutScreenModel.cs
  - Assets/UI/Scripts/LoadoutPresets.cs
  - Assets/UI/Scripts/LoadoutBrowse.cs
  - Assets/UI/Scripts/LobbyBackdrop.cs
  - Assets/UI/Scripts/LobbyChrome.cs
  - Assets/UI/Scripts/LobbyContextMenu.cs
  - Assets/UI/Scripts/LobbyPage.cs
  - Assets/UI/Scripts/LobbyPushPage.cs
  - Assets/UI/Scripts/MkCard.cs
  - Assets/UI/Scripts/MkSheet.cs
  - Assets/UI/Scripts/MkPlayerRow.cs
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
verified: 380d53b3
---

# Lobby UI

## Purpose
The lobby edits loadouts, manages account/settings and enters matches. Server progression stays separate from preferences, commerce stubs and private play.

## Interface
`LobbyUIController` creates shared services/pages and exposes `Navigation`, `Routes`, `Claims`, `Badges` and `Crates`; Home crate taps open `CrateScreen`. `NavigationController.Register(LobbyPage)`, `BindNavButton`, `Show(LobbyPageID)`, `Has`, `CurrentPageID` and `PageShown` drive the track: Shop, Workshop (Loadout), Home, Social, Guild. Home opens first. `LobbyPage.Root/ID/OnShow/OnHide` define tab lifetime. `ProfilePage.Open(source)` expands self from the strip; `OpenFor(playerId)/Close` operates a shared sheet; `LobbyPushPage.Open/Close/Update` supports Settings and MatchHistory. `LobbyStage.ShowShot(StageShot)` and the controller facade retarget one eased move: Home/Districts, Suits, Ocean, Market. Home/Workshop use Districts; Social/Guild/self-profile use Ocean. Slots fill front-left, front-middle, front-right, back-left, back-right; suits occupy front-middle; `TryPick` returns compact slot/suit `StagePick` indices on layer 2. Figures billboard around Y; fullscreen overlays hide the stage.

`LobbyOverlayRouter` registers close callbacks on one `LobbyRouteStack`. An optional `LobbyOverlay.BackHandler` consumes Back by returning true; otherwise Back closes the top, goes Home, or opens quit confirmation. `CrateScreen.Open(size, inventory)` runs `CrateOpeningSequence` as a fullscreen overlay. `LobbyDeepLink.TryParse` accepts nodewar routes; page/stall links navigate, bell opens notifications, profile IDs open panels, remaining targets log. Overlays declare chrome visibility. `SwipeDecision` gates navigation by axis/travel/flick; overlays and `lb-no-swipe` block it. `BadgeBoard` sums dotted keys; `AsyncStateView` retains stale content and delays loading. `ClaimPanel.Open(ClaimRequest)/Cancel` raises Confirmed/Cancelled and shows plain-text disclosures.

`MkSheet.Open(large)/Close`, `Body`, `Surface`, `Handle`, `Scrim`, `Opened/Closed` and `BindRouter(router, id)` supply every sheet, with drag-down/scrim dismissal and router Back. `LobbySheet.Open(content, large)/Close`, `IsShowing`, `Closed` and `Lift(pageRoot, contentName)` preserve page-owned content. `LobbyContextMenu.Attach/Show/Close` displays `LobbyMenuItem(label, action)` rows; `LobbyToast.Show(message)` supplies transient feedback. Pages receive the shell's shared instances. `SafeAreaBinder` applies screen-safe insets.

Pages:
- `HomePage` exposes `PlayRequested`, `LoadoutRequested`, `ModeRequested`, `CrateRequested(CrateSize)`, `SetMode`, `Refresh`; incomplete PLAY opens Workshop. `LocalCrateInventory` backs Small crates/progress with profile data, returns fixed placeholder rewards, and exposes no Medium stock.
- `WorkshopPage` loads `Resources/Lobby/WorkshopPage`: presets, stage window, filtered/sorted tiles. `ViewChanged(bool suits)` selects the stage shot; `BindOverlayRouter` hosts variant/skin sheets. `WorkshopStageBinding` maps taps to storage, selects districts/freezes suits and opens sheets. `LoadoutScreenModel`/`WorkshopInventorySession` gate edits. Replacement uses a banner and Cancel/Back, rechecking eligibility/busy state. Tile menus remove choices.
- `ShopPage.Bind(ClaimPanel, BadgeBoard)` shares claims/badges; `BindStage(LobbyStage)` projects. `ScrollTo(stall)` animates daily/crates/weekly/bundle/bank (coins/laurels aliases). `ShopSnap/ShopProjection` govern motion/culling/order. Daily anchors below header, others centrally. `LobbyStage.SetShopScroll(float)` dollies; `ShopCamera/ShopAnchor` project.
- `SocialPage.Bind(social, profiles, history, openProfile, sheet)` supplies independent TV/history/friends `AsyncStateView`s. Cards open profiles; watch/replay/spectate log. Friends sort by presence, handle and ID. Late responses cannot cross visits or accounts.
- `ProfilePage` shows handle, era/RR, six stats, loadout chips and recent matches. Null stats display dashes. Other-player Add friend/Invite and in-battle Spectate log; self renames through `BackendServices.Handles`, retaining cost hints.
- `GuildPage` searches/filters and caches collapsed public browse results for the session. Members see guild header, weekly progress, pinned posts and feed. `OpenChatAsync` verifies membership; `guild/chat` falls back to the tab without membership. `CurrentGuild/GuildChanged` drive the chrome guild strip. `GuildChatScreen` hides pills, retains chrome, sends trimmed 200-character plain text and polls messages. Join/Spectate log.
- `SettingsPage.Changed(GameSettingsData)` and `Flush` persist preferences; `ControlsPanel`/`ControlsViewModel` share bindings, warnings and Reset with match settings.
- `MatchHistoryPage` uses `IMatchHistoryService` and `MatchHistoryRow` for recorded server outcomes; replay playback is unavailable.

`LobbyModePicker.Open(selection)` supplies mode cards and Private; `LobbyModeSelection.FromProfile/Store` preserves legacy GameMode values. `LobbyPlayFlow.Play` routes ranked/Bot/LAN and stub invite/party panels. `PartyWaitModel` gates random queue at 15 seconds; `PartyWaitScreen` owns party lifetime. `PlayPopup.OpenRankedFlow/OpenJoinCode` retains queue/transport views. `MatchLauncher` owns preflight, launch and cleanup; `RankedQueuePresenter`/`RankedRendezvous` own ranked attempts.

`AccountFlow` coordinates account sheets, busy/result state and change notifications. `AccountLinkPrompt`/`LinkPromptPolicy` avoid treating starter items as progress.

`LoadoutCatalog.CurrentLoadout(PlayerState)` resolves eligible local choices. `LoadoutRules` supplies capacity, grants and completeness. Pure `LoadoutEditor` fills, clears and replaces chosen slots, refuses duplicates and preserves canonical shape; the arena overload explicitly resizes local edits.

`LoadoutChoices.Variants/Skins` returns sorted choices. `UnlockStates.ForBase/ForVariant/ForSkin` supplies ownership, unlock arena and CanUse. `LobbyChrome` shows the server handle with rich text disabled, era progress and Coins/Premium balances; purse buttons route to shop stalls. Settings/bell open Menu/Notifications `LobbyOverlayPanel` sheets; Testing is development-only and bell clears its badge. `LobbyBadgeDot` observes dotted-key counts. The member Guild tab/chat substitutes a guild card in the strip. Sheets hide pills. `SkinBand.SetChoices/Stop`, `CenteredIndex` and `CenterChanged(int)` render the looping strip through `SkinBandMath`.

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
Rank/inventory is server-authoritative; local edits never resize opponents. Preserve frozen `LoadoutRecord.nodeIDs` storage identity. Previews never equip. Delayed equipment/account responses cannot redraw a different selection/account. Missing slots block player-side launch; simulation setup itself does not enforce completeness.

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
`NodeWar.Lobby.Tests` guards loadouts, settings, presenters, route/state/claim/swipe models, crate sequences, paper masks and motion/projection. Compile-check covers Unity pages/launchers. Sign-in, transitions, layouts, device Back, swipe feel and OS deep links require Editor/device verification.
