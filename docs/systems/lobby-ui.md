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
  - Assets/UI/Scripts/LanRoomState.cs
  - Assets/UI/Scripts/LanRoomSheet.cs
  - Assets/UI/Scripts/RoomView.cs
  - Assets/UI/Scripts/QueueSearchPolicy.cs
  - Assets/UI/Scripts/TeammateInviteRoster.cs
  - Assets/UI/Scripts/TeammateInviteSheet.cs
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
  - Assets/UI/Scripts/SocialPage.cs
  - Assets/UI/Scripts/HistoryOpponentNames.cs
  - Assets/UI/Scripts/SegmentedSwitch.cs
  - Assets/UI/Scripts/SegmentedSwitchMath.cs
  - Assets/UI/Scripts/UnlockState.cs
  - Assets/UI/Scripts/WorkshopInventorySession.cs
  - Assets/UI/Scripts/WorkshopPage.cs
verified: 852bc029
---

# Lobby UI

## Purpose
Present account, loadout and match entry while separating local preferences from server progression.

## Interface
`LobbyUIController` exposes Navigation, Routes, Claims, Badges, Crates and ShowShot. `NavigationController` registers pages and drives five tabs; `LobbyPage` defines Root/ID/OnShow/OnHide. `LobbyStage` supplies shots and loadout picking. Fullscreen routes hide stage/pills; Searching preserves a visible, inert player/purse band and frames the full loadout through StageShot.Queue with shared pan timing.

`LobbyOverlayRouter` owns close callbacks through `LobbyRouteStack`. Back delegates to optional BackHandler, then closes the top, goes Home or confirms exit. `LobbySheet` shares MkSheet content. `LobbyContextMenu.ShowBubble` anchors player actions above rooms. Panels may confirm over sheets. Overlays and lb-no-swipe block navigation swipes.

`LobbyMotion` supplies springs, progressive overscroll and critical return. `Reduced` snaps springs, clamps scrolling, fades panels 80ms, stills villagers and pans 150ms. `LobbyMouseDrag` bounds pointer/wheel lists; mode/shop snapping follows edge return. `NavigationController.Tick/MotionChanged` shares page motion with stage/pills and preserves retarget velocity. `LobbyRouteStack.HidesPills` aggregates fullscreen routes.
Match entry:
- `HomePage` exposes PlayRequested, ModeRequested, InviteTeammateRequested, CrateRequested, SetMode and Refresh. BATTLE gates incomplete loadouts with a toast; private selections open PrivateLobbyPanel, LAN opens LanRoomSheet, queued modes open SearchingScreen. Only non-private 2v2 shows Invite teammate.
- `ModeDefinition` owns capabilities, queue keys, seat positions and colours. `LobbyModeSelection.FromProfile/Store` persists selection. Private converts Ranked to Unranked 1v1. `LobbyModePicker` fits 10:8 cards/empty slots into a half-height strip that snaps groups to centre.
- `SearchingScreen.Open/Close/Update` owns a fullscreen queue attempt, loadout window, animated status, hour-aware timer, RR/Era cards and pinned Cancel. `QueueSearchPolicy` leaves 2v2/FFA searching forever without tickets; Ranked retains its real presenter/rendezvous. Every queued mode offers the real local bot fallback after the shared delay; ticket cancellation precedes launch. Retry resets timer/actions. Only 2v2 offers Invite. Back cancels Home; duplicate Open is ignored and other overlays are rejected.
- `TeammateInviteSheet.Open` reads Social friends then Guild members into separate ledgers. `TeammateInviteRoster` maps presence, caps names while preserving tags and orders lobby/battle/offline then name/ID. Friends take precedence over shared guild members; offline invites are disabled. Logged requests show Invited until cancel/mode change; title/Done stay fixed while ledgers scroll; delivery/incoming invites are unavailable.
- `PrivateLobbyPanel.Open/Join` owns Relay rooms. `RoomView` shares board/seats, team colours, host drag swaps and player bubbles with LAN. `PrivateLobbyModel` owns roster/revision/start authority. Start freezes the roster and waits for acknowledgement before releasing the handshake; only 1v1 launches.
- `LanRoomSheet.Open/Update` presents Host/Join, IP entry, connection status and retryable errors. `LanRoomState` returns Leave/Kick to choice. Host switches 1v1/2v2/FFA using the same seat table; real peers occupy seats, remaining seats stay empty. Host Play launches 1v1; four-player Play shows a placeholder toast. Player bubbles offer profile and confirmed host-only Kick.
- `MatchLauncher.HostLanRoom/JoinLanRoom/ChangeLanRoomMode` reuse DirectUDP and the existing private-room envelope without packet changes. `LobbyPlayFlow.OpenPrivateInvite` shares/copies private codes; invite delivery is stubbed.

Other surfaces:
- `WorkshopPage` preserves camera/scroll while browsing; USE returns to the top; equipped items show Remove instead. `LoadoutScreenModel.Use` equips or begins replacement; `WorkshopStageBinding` maps editable picks. Locked taps preserve replacement.
- `ShopPage.Bind` shares claims/badges; BindStage projects offers. `ClaimPanel` confirms purchases/claims; `CrateScreen` owns opening sequences. `LocalCrateInventory` tracks Small progress/stock; Big maps to Medium.
- `SocialPage.Bind` supplies TV, friends/guild and history ledgers. Offline friends expand separately; unknown historical handles use cached profiles or Unknown. Requests cannot cross visits/accounts.
- `GuildPage` raises search on Enter/typing pause/caret, preserving result taps. Filters constrain RR/membership. Membership supplies announcements, sortable members and chat; officer actions confirm kicks and log reports.
- `ProfilePage.Open/OpenFor/Close` presents self/other sheets through IProfileService. BindPanels wires reporting/confirmed Kick; InviteRequested opens Multiplayer. Null stats show dashes. Rename uses Handles.SetHandleAsync.
- `SettingsPage.Changed/Flush` persists preferences; ControlsPanel shares match bindings. MatchHistoryPage reads recorded outcomes without replay playback. AccountFlow handles linking/notifications; LobbyDeepLink routes pages, profiles and stalls.

`LoadoutCatalog.CurrentLoadout` resolves equipment/choices. `LoadoutRules` shares completeness and missing-item messages. LoadoutEditor protects locks; clearing confirms. Failed saves retain state with retry/backoff. `PlayerProfile` persists preferences/presets; `LoadoutData.Normalized` maintains 3/5 slots. LobbyChrome renders server handle, rank and balances; RankDisplay/RankTable supply progress. Premium displays as Laurels.

`UnlockStates.ForBase/ForVariant/ForSkin` supply ownership and CanUse; `LoadoutPresets` stores active-preset edits; `LoadoutTypes.WithEquipment` overlays server equipment.

## Connects to
- `backend-client`: account, inventory, social/guild, history and ranked services.
- `progression`: rank, capacity, ownership and catalog eligibility.
- `match-flow`: frozen loadout and ordered ranked identity enter MatchConnection.
- `netcode`: Relay/LAN transport and ranked connection adapters.
- `draft`: normalized human loadout pools.
- `match-ui`: shared settings/Controls and scene transitions.
- `input`: persisted bindings.
- `ui-art`: shared icons, district art and theme.
- `bots`: accepted fallback launches an unranked bot match.

## Invariants
Backend owns rank/inventory. Local changes never resize opponents' decks. Preserve frozen LoadoutRecord.nodeIDs identity. Preview never equips. Delayed account/equipment responses cannot redraw another account/selection. Launch gates share completeness; canonical arrays retain blanks. Placeholders never claim backend delivery or match success.

## How it works
LobbyManager selects Toolkit or legacy fallback; UIToolkitLobbySetup authors layouts/PanelSettings through Editor tools. Era locks constrain editable choices; missing ownership never trims choices. Launch refreshes/freezes state and rechecks completeness. Ranked requires returned rank; offline/private uses arena 1. Rendezvous supplies record/slot order. Only confirmed resizing persists.

## Decisions
- Share navigation, room views, sheet and menu components rather than duplicate presentation logic.
- Freeze launch data rather than reshape in-flight matches after rank updates.
- Preserve compiling legacy fallback during migration.

## Pressure points
Local social/guild fakes and invite stubs remain. LAN rooms transport one real peer even with four-seat layouts. Interaction, animation, scene wiring and live peer handoff need manual verification.

## Change recipes
- Add a page: implement lifecycle/register navigation; overlays register cleanup with router; layouts load from Resources/Lobby.
- Change loadouts: update pure rules, capacity/ownership, migration and frozen-launch tests together.
- Change room layouts: update ModeDefinition and shared model/view tests; preserve roster revisions and start gating.

## Tests
NodeWar.Lobby.Tests covers queue policy, invite ordering/presence, LAN leave/kick/layouts, private roster packets/start, loadouts, routes, claims and presentation maths. Compile-check covers Unity adapters. Editor/device verification covers rendering and real transport.
