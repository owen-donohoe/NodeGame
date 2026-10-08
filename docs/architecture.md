---
type: Architecture
title: Architecture
description: The seven layers of Assets/Scripts/, where the three UI trees live and which one runs, how information flows between them, the lockstep networking model, and the backend, match logs and referee beside it.
tags: [architecture, layers, networking, lockstep, ui]
generated: { by: human:DonohoeCUA, at: 2026-08-30T17:15:16-04:00 }
verified:
  # full history: docs/verification-log.md
  - { by: claude-sonnet-5-5, at: 2026-10-08T16:13:42Z }
verified_at_commit: 048597d1
status: stable
sources:
  - id: sim-state
    resource: Assets/Scripts/Game/Simulation/SimulationState.cs
    title: SimulationState
    last_modified: 2026-09-25T22:52:42-04:00
  - id: sim-loop
    resource: Assets/Scripts/Game/Simulation/GameSimulation.cs
    title: GameSimulation.SimulateTick
    last_modified: 2026-09-25T22:52:42-04:00
  - id: game-manager
    resource: Assets/Scripts/Game/Core/GameManager.cs
    title: GameManager match lifecycle
    last_modified: 2026-09-28T15:12:16-04:00
  - id: lockstep
    resource: Assets/Scripts/Game/Network/LockstepCore.cs
    title: LockstepCore
    last_modified: 2026-09-29T12:12:27-04:00
  - id: tick-runner
    resource: Assets/Scripts/Game/Core/TickRunner.cs
    title: TickRunner
    last_modified: 2026-09-25T22:11:57-04:00
  - id: match-connection
    resource: Assets/Scripts/Game/Core/MatchConnection.cs
    title: MatchConnection
    last_modified: 2026-09-28T15:12:16-04:00
  - id: draft-manager
    resource: Assets/Scripts/Game/Core/DraftManager.cs
    title: DraftManager
    last_modified: 2026-09-29T11:55:09-04:00
  - id: draft-presenter
    resource: Assets/Scripts/Game/Core/IDraftPresenter.cs
    title: IDraftPresenter, the seam between the draft and its two UI stacks
    last_modified: 2026-09-17T09:11:22-04:00
  - id: uitk-draft
    resource: Assets/UI/Scripts/Gameplay/DraftScreenController.cs
    title: UI Toolkit draft screen
    last_modified: 2026-09-23T16:02:16-04:00
  - id: lobby-manager
    resource: Assets/Scripts/Lobby/LobbyManager.cs
    title: LobbyManager and the useUIToolkitLobby toggle
    last_modified: 2026-09-26T08:52:10-04:00
  - id: node-panel-manager
    resource: Assets/Scripts/Game/UI/Panel/NodePanelManager.cs
    title: NodePanelManager and SetSuppressed
    last_modified: 2026-09-21T22:57:45-04:00
  - id: uitk-lobby
    resource: Assets/UI/Scripts/LobbyUIController.cs
    title: UI Toolkit lobby shell
    last_modified: 2026-09-26T08:52:10-04:00
  - id: uitk-hud
    resource: Assets/UI/Scripts/Gameplay/GameplayHUDController.cs
    title: UI Toolkit in-match HUD
    last_modified: 2026-09-23T10:16:28-04:00
  - id: uitk-sheet
    resource: Assets/UI/Scripts/Gameplay/NodeSheet.cs
    title: UI Toolkit node sheet
    last_modified: 2026-09-22T11:23:32-04:00
  - id: uitk-sheet-content
    resource: Assets/UI/Scripts/Gameplay/NodeSheetContent.cs
    title: NodeSheetContent.Send, the UI Toolkit command path
    last_modified: 2026-09-23T10:16:28-04:00
  - id: outline-driver
    resource: Assets/Scripts/Game/View/OutlineDriver.cs
    title: OutlineDriver, the only setter of outline intents
    last_modified: 2026-09-21T22:57:46-04:00
  - id: outline-registry
    resource: Assets/Scripts/Game/View/Outline/OutlineRegistry.cs
    title: OutlineRegistry and the outlined-only ID lifecycle
    last_modified: 2026-09-05T10:39:14-04:00
  - id: outline-style
    resource: Assets/Scripts/Game/View/Outline/OutlineStyle.cs
    title: OutlineStyle, whose numeric order is the priority order
    last_modified: 2026-09-05T10:39:14-04:00
  - id: outline-feature
    resource: Assets/Scripts/Game/View/Outline/OutlineRendererFeature.cs
    title: OutlineRendererFeature, the URP entry point
    last_modified: 2026-09-21T22:57:46-04:00
  - id: match-factory
    resource: Assets/Scripts/Game/Simulation/MatchFactory.cs
    title: MatchFactory, how GameManager builds the starting state
    last_modified: 2026-09-25T22:52:42-04:00
  - id: backend-services
    resource: Assets/Scripts/Backend/BackendServices.cs
    title: BackendServices, UGS or local fakes, LastKnownState
    last_modified: 2026-09-28T15:24:28-04:00
  - id: match-log-format
    resource: Assets/Scripts/MatchLog/MatchLogFormat.cs
    title: Match log chunks
    last_modified: 2026-09-26T08:52:10-04:00
  - id: referee
    resource: dotnet/NodeWarCloud/NodeWarCloud/Referee.cs
    title: The Cloud Code referee
    last_modified: 2026-09-25T22:46:13-04:00
  - id: loadout-types
    resource: Assets/Scripts/Lobby/Data/LoadoutTypes.cs
    title: LoadoutTypes, lobby IDs to sim types and catalog bases
    last_modified: 2026-09-27T17:00:22-04:00
  - id: input-serializer
    resource: Assets/Scripts/Game/Network/InputSerializer.cs
    title: InputSerializer, ProtocolVersion and the handshake
    last_modified: 2026-09-28T01:56:48-04:00
  - id: protocol-version
    resource: Assets/Scripts/Backend/Shared/ProtocolVersion.cs
    title: ProtocolVersion, shared wire identity
    last_modified: 2026-09-28T01:56:48-04:00
  - id: ranked-presenter
    resource: Assets/Scripts/Backend/Shared/RankedQueuePresenter.cs
    title: RankedQueuePresenter and IRankedQueueView
    last_modified: 2026-09-29T12:12:27-04:00
  - id: ranked-service
    resource: Assets/Scripts/Backend/UgsRankedQueueService.cs
    title: UGS ranked ticket lifecycle
    last_modified: 2026-09-28T01:19:09-04:00
  - id: ranked-popup
    resource: Assets/UI/Scripts/PlayPopup.cs
    title: PlayPopup ranked view, default mode and bot launch
    last_modified: 2026-09-28T15:25:56-04:00
  - id: ranked-queue
    resource: dotnet/NodeWarCloud/Matchmaker/ranked.mmq
    title: Ranked Matchmaker queue rules
    last_modified: 2026-09-28T01:29:29-04:00
  - id: match-launcher
    resource: Assets/UI/Scripts/MatchLauncher.cs
    title: MatchLauncher private and ranked host/join
    last_modified: 2026-09-28T15:12:16-04:00
  - id: draft-serializer
    resource: Assets/Scripts/Game/Network/DraftSerializer.cs
    title: Draft packet layout and its receive check
    last_modified: 2026-09-29T11:55:09-04:00
  - id: ranked-rendezvous
    resource: Assets/Scripts/Backend/Shared/RankedRendezvous.cs
    title: RankedRendezvous join-code exchange
    last_modified: 2026-09-28T15:24:28-04:00
  - id: match-rendezvous
    resource: dotnet/NodeWarCloud/NodeWarCloud/MatchRendezvous.cs
    title: Rendezvous, ConfirmConnected and Leave rules
    last_modified: 2026-09-28T15:23:50-04:00
  - id: match-settler
    resource: dotnet/NodeWarCloud/NodeWarCloud/MatchSettler.cs
    title: MatchSettler, the one settlement path
    last_modified: 2026-09-28T15:22:39-04:00
  - id: player-state-module
    resource: dotnet/NodeWarCloud/NodeWarCloud/PlayerStateModule.cs
    title: GetPlayerState and Equip endpoints
    last_modified: 2026-09-25T22:41:20-04:00
  - id: referee-module
    resource: dotnet/NodeWarCloud/NodeWarCloud/RefereeModule.cs
    title: VerifyMatch endpoint
    last_modified: 2026-09-27T17:07:18-04:00
  - id: reporting-module
    resource: dotnet/NodeWarCloud/NodeWarCloud/MatchReportingModule.cs
    title: ReportMatch endpoint
    last_modified: 2026-09-27T18:23:22-04:00
  - id: match-reporting
    resource: dotnet/NodeWarCloud/NodeWarCloud/MatchReporting.cs
    title: Report agreement and progression settlement
    last_modified: 2026-09-28T15:23:50-04:00
  - id: match-eligibility
    resource: dotnet/NodeWarCloud/NodeWarCloud/MatchEligibility.cs
    title: Log eligibility against match snapshots
    last_modified: 2026-09-27T17:05:41-04:00
  - id: matchmaker-module
    resource: dotnet/NodeWarCloud/NodeWarCloud/MatchmakerAllocatorModule.cs
    title: Matchmaker allocation and polling callbacks
    last_modified: 2026-09-28T01:56:48-04:00
  - id: match-allocation
    resource: dotnet/NodeWarCloud/NodeWarCloud/MatchAllocation.cs
    title: Version checks and claimed match creation
    last_modified: 2026-09-28T01:56:48-04:00
  - id: match-record
    resource: dotnet/NodeWarCloud/NodeWarCloud/MatchRecord.cs
    title: Match roster, snapshots and outcomes
    last_modified: 2026-09-28T15:23:50-04:00
  - id: match-record-store
    resource: dotnet/NodeWarCloud/NodeWarCloud/MatchRecordStore.cs
    title: Match record contracts and active-match claims
    last_modified: 2026-09-28T15:22:39-04:00
  - id: cloud-match-store
    resource: dotnet/NodeWarCloud/NodeWarCloud/CloudSaveMatchRecordStore.cs
    title: Private match records and submitted logs
    last_modified: 2026-09-28T01:18:13-04:00
  - id: cloud-player-store
    resource: dotnet/NodeWarCloud/NodeWarCloud/CloudSavePlayerRecordStore.cs
    title: Protected player records and conditional writes
    last_modified: 2026-09-28T01:56:48-04:00
  - id: history-module
    resource: dotnet/NodeWarCloud/NodeWarCloud/MatchHistoryModule.cs
    title: GetMatchHistory endpoint
    last_modified: 2026-09-27T18:26:46-04:00
  - id: match-history
    resource: dotnet/NodeWarCloud/NodeWarCloud/MatchHistory.cs
    title: Caller history and stored outcomes
    last_modified: 2026-09-27T18:26:28-04:00
  - id: inventory-rules
    resource: dotnet/NodeWarCloud/NodeWarCloud/InventoryRules.cs
    title: Catalog grants and equipment eligibility
    last_modified: 2026-09-25T22:41:20-04:00
  - id: disconnect-hold
    resource: Assets/Scripts/Backend/Shared/DisconnectHold.cs
    title: DisconnectHold, the three-stage hold
    last_modified: 2026-09-30T02:00:00-04:00
  - id: ranked-result-tracker
    resource: Assets/Scripts/Backend/Shared/RankedResultTracker.cs
    title: RankedResultTracker, the end card's server result
    last_modified: 2026-09-30T01:30:00-04:00
  - id: match-hold
    resource: dotnet/NodeWarCloud/NodeWarCloud/MatchHold.cs
    title: Presence, ResolveHold and GetMatchResult rules
    last_modified: 2026-09-30T01:40:00-04:00
  - id: emote-panel
    resource: Assets/UI/Scripts/Gameplay/EmotePanel.cs
    title: Emote controls on the resource sheet
    last_modified: 2026-09-23T09:37:50-04:00
---

# Architecture

Node War is a 1v1 real-time strategy game built in Unity 6 (namespace
`NodeWar`), with lockstep peer-to-peer networking. Gameplay code lives
under `Assets/Scripts/`, split into seven layers.

Presentation is mid-migration and does **not** all live there. Two sibling
trees hold the rest, and both are live code: see
[Where the UI lives](#where-the-ui-lives).

## The seven layers

```
Assets/Scripts/
  Lobby/       Layer 1
  Game/
    Core/      Layer 2
    Simulation/Layer 3
    Network/   Layer 4
    Input/     Layer 5
    UI/        Layer 6      uGUI, in-match
    View/      Layer 7
      Outline/              own assembly, NodeWar.View.Outline
    Config/    not a layer   GameBalance / BoardConfig ScriptableObjects
    Debug/     not a layer   development aids
  Backend/     beside        accounts, progression, inventory, match reports/history, ranked queue (UGS)
  MatchLog/    beside        match log format, recorder, headless replay; own assembly
  Editor/      not a layer   TestBridge and other editor-only tooling

Assets/UI/                  UI Toolkit — the replacement for layers 1 and 6
Assets/Legacy/              retired uGUI lobby, still compiled
dotnet/                     .NET projects over the same sources, plus the Cloud Code module
```

The three marked *not a layer* carry no gameplay rules and sit outside the
information flow below. `Config/` matters anyway: it is where a new tunable
number goes, and the only place the `GameBalance` and `BoardConfig`
`ScriptableObject`s exist. The two marked *beside* sit outside the match's
information flow too, but hold rules of their own: see
[Backend, match logs and the referee](#backend-match-logs-and-the-referee).

**1. Lobby/** — Pre-match menu flow: game mode selection, player profile,
loadout/district/suit selection. Runs entirely in the Lobby scene, before a
`SimulationState` exists. This layer is now data and state only
(`LobbyManager`, `PlayerProfile`, `LoadoutData`, the definition assets, among them
`DistrictDefinition`); the persisted lobby IDs (`node_rampart`, `suit_warrior`) are
frozen strings held in explicit tables, never derived from an enum name;
its uGUI panels moved to `Assets/Legacy/Lobby/` and its live presentation
is `Assets/UI/`.

**2. Core/** — Match lifecycle orchestration. Owns the top-level state
machine (`GameManager`), the pre-match draft (`DraftManager`), local tick
timing (`TickRunner`), and camera/transition control. This is the layer
that starts a match and wires every other layer together. It builds the
starting `SimulationState` for drafted matches through
`Simulation/MatchFactory`, the same builder the referee and headless
replays use. Testing mode (skip-draft) takes the same route: a planned set of
placements on the shipped map, handed to the factory.

**3. Simulation/** — All gameplay rules and the entire mutable match
state. Pure C#, no `UnityEngine` dependency (see `docs/simulation-rules.md`
for the full contract this layer must uphold, since it must produce
identical results on both peers).

**4. Network/** — Lockstep transport. Turns `GameCommand`s and heartbeats
into packets, drives the networked tick loop, and detects
desyncs/disconnects.

**5. Input/** — Captures player (or bot) intent and turns it into
`GameCommand`s queued for the next tick. Never mutates `SimulationState`
directly.

`PointerGestureSource` is the single pointer reader for live selection and
move orders. It resolves a primary press into a tap, a pan or a long-press
lasso and publishes the target. Desktop right-clicks publish a node-only
destination to `CommandSystem`, after a current-position UI raycast blocks
both uGUI and UI Toolkit (including the HUD and node sheet). Consumers do
not repeat the world raycast. `GameManager` enables gesture routing for
selection, commands and panel arbitration; command keyboard shortcuts
remain independent. `CameraController` still reads desktop middle-drag
and scroll directly; those camera controls are outside this routing.
Thresholds are authored in millimetres and converted against screen
density, so they mean the same thing to a finger on any device.

What counts as a tap target is asked, not assumed: an opponent's villager
is not one, and the press falls through it to the node beneath. The rule
lives with the selection owner rather than in the gesture source, so the
input layer never learns game ownership.

**6. UI/** — HUD, panels, menus during a match, in uGUI. Reads
`SimulationState` to render; writes nothing to it. Being replaced by
`Assets/UI/`, which is not a subset of this layer — see below.

**7. View/** — World-space presentation of nodes and villagers
(sprites/animation/interpolation). Reads `SimulationState` to render;
writes nothing to it.

## Replacing UI art

Create an Inspector-authored **NodeWar > UI Art Theme** asset named `UIArtTheme`
at `Assets/UI/Resources/UIArtTheme.asset`. The UI loads it once per play session
through `Resources.Load("UIArtTheme")`; restart Play Mode after changing its
reference or entries. Each icon entry has two choices: **Icon**, a grouped
menu such as `Indicators/Threat to core` or `Resources/Food`, then **Where**,
which lists `Anywhere` first and only that icon's actual locations with
human-readable names. An icon used in one location offers only `Anywhere`.
Assign **Sprite** and **Keep original colours** below those choices.
Changing Icon resets an incompatible Where to Anywhere. Opening the Inspector
does not rewrite saved values; a valid specific override for a single-location
icon displays Anywhere with a tooltip explaining the preserved location.

Retired generic kinds are hidden from the Icon menu unless the current row
already uses one, in which case its `Retired/…` choice remains visible. Saved
pairs with no active use show an error explaining that they will never match;
choose an active icon and a supported Where to repair them. The shared
UnityEngine-free `LobbyIconUsage` table supplies groups, labels and locations.
Editor/development builds warn once per unlisted runtime pair after initial
UXML attributes have been applied and the icon attaches, skipping intentional
unset None icons. Dotnet tests guard coverage, valid locations and labels.

Underneath the picker, entries still store `LobbyIconKind` and
`LobbyIconContext`. Resolution tries
the exact kind/context pair, then that kind with `Anywhere`, then its generated
glyph. The first duplicate **pair** wins; validation warns. An empty exact
sprite falls back to Anywhere, without taking a later exact duplicate. An
empty first Anywhere entry falls back to the glyph. Original-colour sprites
use white tint; other sprites
use the existing glyph tint. Images scale to fit, centred, with letterboxing.
Missing entries keep the generated vectors; unknown kinds get a placeholder.

Existing enum names and values are unchanged; semantic kinds are appended.
Every new kind shares its predecessor's drawing until its own art is assigned.
Existing theme entries without a context deserialize to `Anywhere` (explicit
enum value zero; the serialized field has no nonzero initializer). No asset
re-save is needed to preserve their fallback behaviour. In UXML use, for
example, `kind="Tv" context="PageHeader"`; in C# the constructor accepts an
optional context, and either property can change independently.

Contexts are `Anywhere` (default fallback), `TopBar`, `PageHeader`, `NavBar`,
`HeadsUpDisplay`, `NodeSheet`, `InlineText`, `EmotePicker`, `EmoteBubble`,
`OffScreenIndicator`, `ShopCard`, `SuitTree`, `Workshop`, `Profile`, and `Home`.
The indicator layer tags both its pointer and event glyph `OffScreenIndicator`,
including when that same indicator is projected over the board.

| Kind | Current use | Context(s) |
|---|---|---|
| None | Intentional absence of a glyph | Anywhere |
| Shop | Legacy generic fallback drawer; no active use | — |
| Spark | Legacy generic fallback drawer; no active use | — |
| Tools | Workshop navigation | NavBar |
| Smile | Legacy generic fallback drawer; no active use | — |
| Gear | Lobby settings button | TopBar |
| Envelope | Legacy generic fallback drawer; no active use | — |
| Mouth | Home villager face | Home |
| Tv | History button and history page header | TopBar, PageHeader |
| Back | Page back buttons, controls row, suit-tree back | PageHeader, InlineText, SuitTree |
| Flag | Legacy generic fallback drawer; no active use | — |
| Hat | Legacy generic fallback drawer; no active use | — |
| Diamond | Legacy generic fallback drawer; no active use | — |
| District | Workshop district tabs and picker | Workshop |
| Suit | Workshop suit tabs and picker | Workshop |
| Lock | Workshop locked cards | Workshop |
| Pip | Legacy generic fallback drawer; no active use | — |
| Close | Node-sheet close button | NodeSheet |
| Alert | Legacy generic fallback drawer; no active use | — |
| Swords | Battle indicator | OffScreenIndicator |
| Capture | Legacy generic fallback drawer; no active use | — |
| Sleep | Idle indicator | OffScreenIndicator |
| Respawn | Respawn indicator | OffScreenIndicator |
| Pointer | Indicator direction arrow | OffScreenIndicator |
| Frown | Sad emote | EmotePicker, EmoteBubble |
| Angry | Angry emote | EmotePicker, EmoteBubble |
| Speaker | HUD/settings mute status and emote-picker mute | HeadsUpDisplay, EmotePicker |
| Food | HUD, sheet readout, inline costs/rewards | HeadsUpDisplay, NodeSheet, InlineText |
| Materials | HUD, sheet readout, inline costs/rewards | HeadsUpDisplay, NodeSheet, InlineText |
| Metal | HUD, sheet readout, inline costs/rewards | HeadsUpDisplay, NodeSheet, InlineText |
| NavBarHome | Home navigation | NavBar |
| NavBarSocial | Social navigation | NavBar |
| NavBarShop | Shop navigation | NavBar |
| DailyBox | Home daily box | Home |
| VictoryBox | Home victory box | Home |
| ShopBundle | Shop bundle offers | ShopCard |
| GoldLeaf | Shop gold-leaf offer | ShopCard |
| MagicResource | HUD magic bar and sheet readout | HeadsUpDisplay, NodeSheet |
| SuitTreeAvailable | Available suit-tree node | SuitTree |
| SuitTreeOwned | Owned suit-tree node | SuitTree |
| SuitTreeEquipped | Equipped suit-tree node | SuitTree |
| SuitTreeLocked | Locked suit-tree node | SuitTree |
| ProfileYouAreHere | Profile arena-track marker | Profile |
| IndicatorEffect | Generic effect indicator | OffScreenIndicator |
| IndicatorThreatToCore | Threat-to-core indicator | OffScreenIndicator |
| IndicatorThreatToTerritory | Threat-to-territory indicator | OffScreenIndicator |
| IndicatorNodeUnderAttack | Node-under-attack indicator | OffScreenIndicator |
| IndicatorNodeContested | Contested-node indicator | OffScreenIndicator |
| EmoteHappy | Happy emote | EmotePicker, EmoteBubble |
| EmoteWhiteFlag | White-flag emote | EmotePicker, EmoteBubble |
| CosmeticTinRoof | Shop Tin Roof card and item detail | ShopCard |
| CosmeticPaperBanner | Shop Paper Banner card and item detail/default | ShopCard |
| CosmeticStrawHat | Shop Straw Hat card and item detail | ShopCard |

Assign the theme's `districtVisuals` reference to the existing
`DistrictVisualTable` (no move into Resources). Populate each `DistrictVisual`'s
`icon` for Workshop cards and node-sheet thumbnails, and `sticker` for draft
pieces. Draft uses sticker, then icon, then its existing mapping. Missing flat
art keeps each screen's own fallback; Workshop locks remain visible. The UI
does not consume the district accent colour.

Gauge skins remain procedural. Set USS custom properties on matching elements
or their ancestors: `--tree-edge-color`, `--tree-edge-owned-color`,
`--tree-line-width`, `--tree-disc-size` on `.st-canvas`; `--res-thickness` and
`--res-stop-0` through `--res-stop-5` on `.hud__res-ring` (including sheet
readouts); `--dial-thickness`, `--dial-track-color`, `--dial-fill-color` on
the progress dial. Geometry properties are unitless pixel numbers, bounded
by the painter and its host; omitted properties retain today's defaults.
Resource stops override the compatible `--ring-critical` through
`--ring-rich` palette. Target metal/magic classes separately when changing
those gradients. Sheet background alpha is USS opacity on
`.sheet__res-readout-tint`, with the expanded override under
`.sheet__res-readout--bar`. This fades only the background.

Keep behaviour-bearing UXML element names and types when replacing art.
`UiRequired` reports missing/wrong types in Editor/development builds without
stopping binding, and dotnet tests check the shared required-name lists against
the HUD and Settings layouts. No theme switcher, addressables, or gauge sprite
skins are introduced.

## Where the UI lives

Three trees, deliberately. The phone-UI rebuild runs the UI Toolkit stack
*alongside* the uGUI one rather than replacing it in place, so either can
be selected without a branch and nothing is lost while the new one is
still being proven.

| Tree | Holds | State |
|---|---|---|
| `Assets/Scripts/Game/UI/` | in-match uGUI: `HUDManager`, `NodePanelManager`, draft UI, world-space bars | compiled; `NodePanelManager` still owns tap arbitration, but the uGUI HUD band and the uGUI draft are both off |
| `Assets/UI/` | UI Toolkit: the whole lobby, plus the in-match HUD, node sheet, countdown, end screen and draft | live; all three toggles are on |
| `Assets/Legacy/` | the retired uGUI lobby panels | compiled, unreachable when the new lobby is on |

**Which one runs is a scene value, not a code value.** All three toggles are
`[SerializeField]` booleans, so their live setting exists only in scene
and prefab serialisation — reading the code will not tell you which UI is
on screen:

- `LobbyManager.useUIToolkitLobby` — swaps the uGUI lobby panels for
  `Assets/UI/`. Falls back to uGUI with a warning if the root is
  unassigned.
- `GameManager.useUIToolkitHUD` — activates the UI Toolkit HUD and, when
  that HUD carries a node sheet, calls `NodePanelManager.SetSuppressed`
  so the two panels never race one tap. Only after the controller resolves
  and initialization succeeds does it hide `HUD_Canvas` and disable the
  `HUDManager` component. Failure keeps the legacy canvas and manager on;
  the UI_Manager root stays enabled for tap arbitration.
- `GameManager.useUIToolkitDraft` — activates the UI Toolkit draft screen.
  Deliberately **separate from the HUD toggle**: the draft and the match
  never overlap, so there is no reason a half-finished migration has to
  move them together. Falls back to the uGUI draft with a warning if the
  root is unassigned, because a draft you cannot see is a match you cannot
  start.

The two in-match toggles pick between presenters rather than between
prefabs. `DraftManager` holds an `IDraftPresenter`, the same shape as
`ICountdownPresenter` and for the same reason: the turn machine owns the
rules and must not know which stack is drawing them. The two are turned
off differently — the uGUI draft is a prefab that simply is not
instantiated, the UI Toolkit one a scene object that is not activated — so
`GameManager.CreateDraftPresenter` is the single place that guarantees
exactly one of them exists.

All three roots are built by editor commands under **Tools > Node War**, not
by hand (`Assets/UI/Editor/`). A `PanelSettings` asset and a scene object
carrying a `UIDocument` are Unity-serialised, and hand-written YAML with
guessed GUIDs is how scenes get quietly corrupted. The draft setup goes one
step further and copies the ghost prefab, the placed-piece prefab and the
sticker table off the uGUI draft prefab `GameManager` already points at,
rather than looking them up by path — a second answer to "which prefab is
the draft's" is a second thing to keep in step.

`Assets/UI/` is not a fourth layer. It sits exactly where layers 1 and 6
sit in the information flow, under the same rule as every other consumer:
it reads `SimulationState` and sends gameplay changes by enqueuing a
`GameCommand` on `InputBuffer`. `NodeSheetContent.Send` is the sheet's
choke point; it rejects detached controls and district bindings made stale
by a capture or rollback. UI command eligibility can read
`CommandProcessor` helpers, including the escalated paid-respawn price.
The local playtest debug exception is orchestrated by `GameManager`:
in Editor/development local or bot play only, it installs a balance copy
through `GameSimulation.SetBalance` to move sudden death to tick + 50.
It never writes state from the view, is blocked in networked play, and
warns that the asset/handshake/export hash and recorded replay no longer
match. `GameplayHUDController.SetDebugBalance` receives the same copy.

The in-match HUD keeps both breach walls at the top, with player marks and
breach counts below the bars. The match timer is a rounded rectangle between
them; a three-bar settings button sits directly below it in the same column.
The settings card drops down from beneath that button. The recentre/zoom
handle defaults to the bottom right, with the emote dock at the bottom left;
Controls settings can change the handle's side, visibility and zoom behaviour.

Resources sit near the bottom of the safe area, above the control docks
and emote stack. Food and materials use concentric segmented semicircles,
flat side down, with a glyph and live count. Metal and display-only magic
use thin bars below them with amount/cap labels; magic's source currently
returns zero. Visibility follows the arena/debug rules, with held metal
always visible. `ResourceCaps` reads typed balance fields directly and
uses finite display defaults for nonpositive caps. `ResourceRingMath`
owns fill maths; `ResourceRingColors` blends the shared colour stops.
Full resources use a shared global phase for pulse and sweep, including
sheet bars; reduced motion keeps a steady brighter tint instead.

The open node sheet can cover those cards, so it carries its own food,
materials, metal and optional magic readouts just above its top edge.
They show the controlled player's live totals and move with the sheet.
Each `NodeSheetContent` declares consumed and produced `InvolvedResources`;
involved resources expand into stacked thin bars, while unused resources
remain pills in a row above them. Amount increases and decreases bounce,
unless reduced motion is on. A district-type change reselects and rebinds
content even when both types share `EquipContent`; unsupported types use
the normal close path. `LobbyIcon` supplies shared resource glyphs for
the HUD, sheet and text. `NodeSheetContent.SetResourceText` turns `{food}`,
`{materials}` and `{metal}` templates into inline icons beside text spans.

Legacy code is kept compiling rather than commented out or deleted, so
that a break in it is a compiler error rather than a discovery made later.
See `Assets/Legacy/README.md`.

## Information flow

```
Pointer (mouse / touch)        or  BotPlayer
        │
        ▼
 PointerGestureSource                   (Input/)
        │  one press -> tap | pan | lasso, resolved once
        ▼
 TapRouter / SelectionSystem            (Input/)
        │  decides what the gesture meant; tracks selection
        ▼
   CommandSystem / BotPlayer            (Input/)
        │  produces GameCommand
        ▼
     InputBuffer                        (Input/)
        │  queued until next tick
        ▼
 TickRunner (local) / LockstepCore (networked)   (Core/ / Network/)
     (GameManager drives either every frame)
        │  drains buffer, in order
        ▼
 CommandProcessor.ProcessCommand        (Simulation/)
        │  validates, then mutates
        ▼
     SimulationState                    (Simulation/)
        │
        ▼
 GameSimulation.SimulateTick            (Simulation/)
        │  advances the tick: movement → combat → claiming (breach → claim) →
        │  production → healing → respawns → win-check
        ▼
     SimulationState  (updated)
        │
        ▼
   UI/ and View/  read SimulationState and render
```

`SimulationState` is the single source of truth. During play, commands and
ticks mutate it inside `Simulation/`; presentation only reads it.
`Core/` still initializes state before play, through `MatchFactory` on every
path, Testing mode included. See `docs/simulation-rules.md` for the in-match boundary.
Rampart bonuses follow movement; order resume follows win-check;
the derived `nextBreacherID` refresh is last. Claiming and breaching read each node's
owner as it stood when the tick began. Simulation version 2 adds
`Breaching` after `Dead` and player breach progress, next candidate and
paid-respawn count, all covered by hashing and rollback copy. Breach wins
require a new breach at the current threshold; a sudden-death drop alone
does not lose a match, and simultaneous losses cancel.

Recruit and SetAutoRecruit are node commands validated through `NodeActionRules`.
The command processor spends food and appends recruits; the automatic pass calls
the same recruit path after ordinary production and before healing, in ascending
node ID. Player `recruitCount` persists for the match; each Village's
`recruitReadyTick` and `autoRecruit` reset on ownership loss. All three fields are
hashed and copied. Village capture itself no longer spawns bonus villagers.

A move order is sticky: `VillagerData.targetNodeID` keeps the destination through
a fight or a blocked route, and `TickOrderResume` replans from the villager's
current node once a tick, after every rule pass. Simulation version 3 adds terrain:
`NodeData.terrain` and `SimulationState.boardHash` (the `BoardHasher` fingerprint of
the board) are hashed, and the capture bonus, restore and the retired Watchtower and
Rampart claim effects are described in `docs/game-model.md`.

### What a tick did

Beside the state it produces, a tick can report the moments it passed through,
into a `TickEventLog` (`Simulation/TickEvents.cs`):

```
SimulationState   what the world IS.        Hashed. Replicated.
TickEventLog      what just HAPPENED.       Not hashed. Output only.
```

A `TickEvent` is flat and integer-only, in the style of `GameCommand`: a type
plus a node, villager, player and value, `-1` where unused. The types are
`CombatStarted`, `VillagerDied`, `VillagerRespawned` (value 1 if paid),
`NodeNeutralised`, `NodeClaimed`, `Breach`, `TempoStage` and `SuddenDeath`.
The HUD reads the last two for banners and uses tick count for its
sudden-death countdown. Breach walls show an active Core at/over threshold
as needing one more breach, rather than drawing it empty on the drop.

- **Moments only.** A fight still going, or a node still being pushed, is read
  off `SimulationState` the way the claim bar always was. A log that had to say
  "still happening" every tick would just be the state again.
- **Output only, and off the state.** The simulation appends and never reads
  back, so a wrong entry is a cosmetic bug. The log is not a `SimulationState`
  field, so it is not in `SimulationStateHasher` and never replicates. Both
  peers write the same entries anyway, from integer state in tick order.
- **Passed in, null by default.** `SimulateTick(state, log)` and
  `ProcessCommand(state, command, log)` record only when handed a log, so tests
  and any headless run are unchanged. `ProcessCommand` takes it because a paid
  respawn happens there, outside `SimulateTick`.
- **The driver owns it.** `TickRunner` and `LockstepCore` clear one log before
  each tick, and after the tick raise `ITickProvider.TickSimulated` with it.
  They raise it inside their catch-up loop, so a frame that runs three ticks
  raises it three times. The log is reused, so subscribers copy what they need.

## Scene structure

Three `.unity` scenes exist under `Assets/Scenes/`:

- **`Lobby.unity`** — menu flow (`LobbyManager` and its panels). No match
  or `SimulationState` exists yet.
- **`Gameplay.unity`** — an active match. `GameManager.Awake()` reads
  `MatchConnection.Instance` to decide whether to run the draft, a bot
  match, or a networked match, then builds `SimulationState` and starts
  the tick loop.
- **`GFX Testing.unity`** — a separate scene, not part of the lobby →
  match flow; used for isolated visual/graphics iteration.

Three objects are carried across the Lobby → Gameplay scene load via
`DontDestroyOnLoad`:

- **`MatchConnection`** — created when a match is started from the lobby
  (local play, bot match, or a networked connection). Holds
  `networkManager`, `localPlayerID`, `isNetworked`, `isBotMatch`, the
  chosen `LoadoutData`, and for a ranked match `isRanked`, the server's
  `matchId` and the record's `playerIds` (slot 0 is simulation player 0).
  Read once by `GameManager.Awake()` in the Gameplay
  scene, then shut down (`MatchConnection.Shutdown()`) when returning to
  the lobby.
- **`PlayerProfile`** — the persistent player-identity singleton
  (username, uuid, trophies, unlocked suits/districts, selected loadout),
  loaded from/saved to local JSON. Survives every scene transition for
  the life of the application.

- **`SceneTransition`** (`Assets/UI/Scripts/`) — created on demand by
  `SceneTransition.Load` for bot, local and online match starts and return to
  Lobby. Its full-screen sheet slides from below, loads asynchronously in
  Single mode while covered, then exits above after load completion and at
  least one destination frame (minimum cover time 0.25 s). The temporary
  `DontDestroyOnLoad` root and runtime `PanelSettings` are destroyed after
  reveal. The panel matches HUD's 390×844 width-based scaling and uses sort
  order 32768, above the other panels and uGUI. Resources UXML and a TSS
  wrapper import the existing shared theme; no Editor setup is needed.
  A full-screen picking shield and consumption of control-device input
  events block UI and direct polling during the transition without pausing
  the frame loop or network. Peers load independently; MatchConnection's
  NetworkManager ownership and the draft ready handshake are unchanged.
  Lobby tab/page navigation does not use this entry point.

## Key classes per layer

**Lobby/**
- `LobbyManager` — panel navigation and startup (Homepage, GameMode,
  Profile, Shop, GroupSelection).
- `PlayerProfile` — persistent player identity/progression singleton.
  Its JSON holds local profile, settings and loadout choices; backend
  player state owns rated progression and inventory.
- `LoadoutData`, `DistrictDefinition`, `SuitDefinition` — data describing a
  player's drafted districts/suits. `LoadoutData` also carries the player's
  era per suit and district type and their equipped skin IDs; those are
  stamped on at match launch from the server's equipped state
  (`LoadoutTypes.WithEquipment`), not chosen in the lobby's local data.
- `LoadoutTypes` — the one translation between lobby item IDs
  (`suit_warrior`), simulation types and catalog base IDs (`suit.warrior`).

**Core/**
- `GameManager` — match lifecycle state machine (`PreDraft → Drafting →
  PostDraft → Countdown → Playing`); builds `SimulationState` and spawns
  node/villager views.
- `DraftManager` — runs the pre-match district-placement draft as its own
  turn-based phase machine (`WaitingForReady → InitialReveal →
  ActiveDraft → Complete`). Draws nothing itself: it drives an
  `IDraftPresenter`, and asks it one question back — whether a piece is
  parked but unconfirmed, so a turn that times out takes the cell the
  player already chose rather than a random one.
- `IDraftPresenter` / `ICountdownPresenter` — the two places a phase has
  to be drawn by whichever UI stack is on. Same shape for the same reason:
  the phase owns the rules, the presenter owns the pixels.
- `TickRunner` — local (non-networked) fixed-tick driver.
- `MatchConnection` — persists match configuration across the Lobby →
  Gameplay scene load.
- `CameraController` — camera rig, framing, and the one place the viewing
  side changes. See [Camera POV and sprite order](#camera-pov-and-sprite-order).
- `MatchTransitionController` — scripted transition sequences (startup
  wave, post-draft reveal, breakdown-on-game-over).
- `ITickProvider` — shared interface exposing tick-interpolation alpha so
  View code doesn't need to know whether `TickRunner` or `LockstepCore`
  is driving the match.

**Simulation/**
- `SimulationState` — the entire mutable match state: `NodeData[]`,
  `VillagerData[]`, `PlayerData[]`, tick count, game-over/winner.
- `GameSimulation.SimulateTick` — the deterministic tick loop.
- `TickEventLog` — what a tick did, output only and unhashed. See
  [What a tick did](#what-a-tick-did).
- `CommandProcessor` — validates and applies a `GameCommand` to
  `SimulationState`.
- `Commands.cs` — `GameCommand` struct and `CommandType` enum.
- `NodeActionRules` — read-only Village recruitment and repeat-toggle eligibility,
  population counting (dead included, consumed excluded), and enemy presence.
- `Pathfinding` — Dijkstra over the node graph with ownership-based
  integer cost multipliers.
- `MatchFactory` — builds a match's tick-0 state from board, draft and
  per-player setup; `Configure` sets the simulation's statics separately
  from `Build`/`Fill`. Every path uses it, Testing mode included. It refuses an
  illegal board or draft (`RequireBuildable`) and builds a sparse node array,
  numbering the cells that carry a node. See
  `docs/simulation-rules.md`, *The starting board*.
- `GameBalanceData` / `BoardConfigData` — the plain tuning structs read at
  match start. `GameBalanceData` is behind the `GameBalance` asset in `Config/`.
  `BoardConfigData` is a map as plain data (grid, `TerrainType` per cell, district
  slot mask, `InitialDistrictPlacement`s, base draft pools); the `BoardConfig` asset
  names a map by ID and `PremadeMaps` builds it. Suits and districts have one stats
  entry per era (`SuitStats.era`, `DistrictStats`); a lookup for a missing era falls
  back to era 0.
- `PremadeMaps` / `IBoardCatalog` — the shipped maps (today `hourglass-01`) and the
  catalog a build vouches for. The live game, the rig and the referee all build
  their board here.
- `MatchSetup` — map ID, board hash, simulation version and balance hash: what two
  peers, a log and the server compare. `BoardHasher` fingerprints a board.
- `PlacementLegality` / `MapAuthoringRules` — the one spatial rule for placing a
  district (terrain, slot, occupancy), and the checks a board must pass to be built
  or shipped. Every placement or preview path asks them.
- `BalanceHasher` / `SimulationVersion` — the content hash and version a
  build is identified by in the handshake and the match log.
- `DraftState` — grid occupancy, each player's picks and the legal cells for a
  piece during the draft phase, all through `PlacementLegality`; `DraftPlanner` plans
  the skip-draft placements.
- `SimulationStateHasher` — deterministic integer fingerprint of
  `SimulationState`, used for desync detection.

**Network/**
- `LockstepCore` — networked tick driver, plain C# with no UnityEngine in
  it: the caller passes the clocks in and logging goes to a sink. Stalls a
  tick until both local and remote inputs exist for it, and raises
  `HoldStarted` / `HoldEnded` when a stall lasts. `GameManager` owns it and
  calls `Update` (first thing in its own `Update`, before any early return) and
  `Flush` (`LateUpdate`) every frame. It never ends a match itself;
  `GameManager` calls `EndMatch`, after which the core neither ticks nor sends.
  It is the match's `ITickProvider` and `IEmoteChannel`.
- `InputDelayController` — decides what input delay to ask the peer for
  (see *Input delay adapts* below). Plain C#.
- `NetworkManagerTransport` — adapts `NetworkManager` to the core's
  `ILockstepTransport`, and treats a destroyed manager as silence.
  `UnityLockstepLog` sends the core's log lines to the console.
- `NetworkManager` — transport abstraction (send/receive raw packets).
- `InputSerializer` — wire format for tick inputs, heartbeats and the
  versioned handshake. It refuses any `CommandType` that `CommandTypes.IsKnown` does not list. `InputSerializer.ProtocolVersion` aliases
  `NodeWar.Backend.ProtocolVersion.Current`, declared in
  `Assets/Scripts/Backend/Shared/ProtocolVersion.cs` and shared with Cloud
  Code. It changes with any packet layout.
- `LocalBuildIdentity` — this build's `BuildIdentity` (protocol, simulation
  version, balance content hash). Peers compare it in the handshake and
  refuse a mismatch rather than desync.
- `SetupAgreement` — the pre-draft map and rules agreement (protocol 5): the host
  proposes a `MatchSetup` until the guest verifies it against `PremadeMaps.Catalog` and
  acknowledges, and no draft packet is honoured before then. Pure data, no clocks.
- `DraftSerializer` — wire format for draft-phase packets (ready,
  placement, loadout) and the `MatchSetup`/`MatchSetupAck` packets, which are exact
  length. The loadout carries eras and skins (protocol 2).
  `TryDeserializeDraftLoadout` checks the whole layout beside the writer,
  so the receive check cannot fall behind a new section again.

**Input/**
- `PointerGestureSource` — the shared pointer reader for selection and
  commands; publishes taps, pans, lassos, pinches and right-click destinations.
- `TapRouter` — the tap priority ladder: villager, then node-with-selection
  (move), then node (panel), then empty (clear).
- `SelectionSystem` — tracks selected villagers; applies lasso results.
- `CommandSystem` — turns player actions into `GameCommand`s.
- `InputBuffer` — queue of commands awaiting the next tick.
- `BotPlayer` — generates commands for an AI-controlled side.
- `HitFlashRouter` — the single bridge from gesture events to renderers,
  so the input layer never touches a `SpriteRenderer` itself.
- `LassoGeometry` / `ScreenMetrics` / `GestureThresholds` — pure helpers:
  polygon containment and smoothing, millimetre-to-pixel conversion, and
  the tunable thresholds.

**UI/** (uGUI, `Assets/Scripts/Game/UI/`)
- `HUDManager` — top-level in-match HUD.
- `NodePanelManager` — per-node detail/action panel. Can be told to stand
  down by `SetSuppressed` when the UI Toolkit sheet is serving instead.
  `NodeInspected` tracks every inspected node, independently of whether
  `NodeOpened`/`NodeClosed` show a panel.
- `DistrictPanelPolicy` — decides which districts open a panel at all.
  UI Toolkit uses `HasSheet`, including owned Farms, Mines and Markets;
  the uGUI path still uses the older `IsFunctional` rule.
- `DraftUI` — draft-phase interface, with `DraftPlacementController`
  (drag/park/confirm state machine), `DraftPickUI` and
  `DraftConfirmPresenter`. Implements `IDraftPresenter`. Off by default
  now, and mouse-only: it reads `Mouse.current` and cancels on right-click
  or Escape, neither of which a phone has.
- `GameOverPanel` — end-of-match result display.
- `SelectionLasso` — draws the in-progress lasso stroke.
- `LassoArmedCue` — ring pulse confirming the long press armed.

**`Assets/UI/`** (UI Toolkit)
- `LobbyUIController` / `NavigationController` / `LobbyPage` — the lobby
  shell. The tab pages (`ShopPage`, `HomePage`, `WorkshopPage`,
  `SocialPage`) sit side by side in one track that slides between them.
  `ProfilePage` (expands from the trophy strip) and the `LobbyPushPage`s
  (`SettingsPage`, `MatchHistoryPage`) are overlays above the chrome, not
  tabs.
- `LobbySheet`, `LobbyContextMenu`, `LobbyToast` — one of each for the
  whole lobby, handed to pages rather than built per page. `PlayPopup` is
  content shown in the sheet. Its ranked view uses `RankedQueueViewElement`
  through `IRankedQueueView`; `Backend/Shared/RankedQueuePresenter` owns
  the whole ranked attempt (see [Backend](#backend-match-logs-and-the-referee)).
  The sheet opens on Ranked; private modes stay one tap away.
- `LoadoutCatalog` — what a loadout slot may hold and what the player owns
  (not globally granted, not Crossroads, unlocked). The Workshop, Home and
  the battle sheet all ask it; the slot rules themselves are in the
  UnityEngine-free `LoadoutEditor`, which `dotnet/NodeWar.Lobby.Tests`
  covers.
- `MatchLauncher` — the lobby's route into a match. Private play hosts or
  joins by code with an open-ended host wait; `HostRanked`/`JoinRanked`
  add deadlines and carry the match ID and roster into `MatchConnection`.
  `MatchLauncherConnection` adapts it to the UnityEngine-free
  `IRankedConnection` that `RankedRendezvous` drives.
- `SettingsPage` account section and `AccountFlow` — guest / link / sign
  in / sign out, the conflict and warning sheets, and the one-time link
  prompt (`LinkPromptPolicy`: starter items are not progress).
- `WorkshopPage` era and skin chips — per item, one chip per era from the
  server's `PlayerState` (owned, usable at the current arena, equipped).
  Equipping calls `IInventoryService` and shows what the server returns.
  The chip rules are the UnityEngine-free `EraChips`.
- `GameplayHUDController` — the in-match HUD, bound by `GameManager`.
  It also carries the disconnect-hold overlay, the end card's ranked result
  block, and the ranked surrender row in `MatchSettingsPanel`.
- `EmotePanel` — the emote button, sheet, bubbles, rate limit and mute. Its
  layer is brought to the front so a closing emote shows over the end card, and
  its button sits on the resource sheet, covered by an open node sheet.
  Opening the node sheet closes the emote options. The popup mute is reversible
  and match-local; the Settings toggle is saved across matches and takes precedence.
  Either mute hides bubbles and disables/dims outgoing emotes. Speaker icons turn
  from white to red; the popup stays reachable during mute and cooldown.
- `IndicatorLayer` — draws `IndicatorDirector`'s list over the board. The
  first child of `hud-root`, so every readout, dock, sheet and card draws over
  it. See [In-match indicators](#in-match-indicators).
- `NodeSheet` — the node panel as a bottom sheet. It does not decide when
  to open; `NodePanelManager` still owns that.
- `NodeSheetContent` and its four subclasses — `ForgeContent`,
  `CoreContent`, `EquipContent` cover all six actionable districts;
  `ProductionContent` shows an owner's Farm, Mine or Market without actions.
  Each declares `InvolvedResources`; `Send` is the only path to the simulation.
- `DraftScreenController` — the draft screen, and the one place in this
  tree that owns an interaction end to end. The chrome and the placement
  cannot be separated here: a drag can begin on a UI Toolkit card or on
  the 3D board, so it reads `Pointer.current` (mouse *or* touch) for
  everything past the card press, positions the Confirm pair from the
  parked cell's world position each frame, and instantiates the same
  world-space ghost prefab the uGUI draft used. It writes nothing to
  `SimulationState` — `GameManager` has allocated the state, but the
  match board and players are filled only after the draft.
- `DraftPieceInfo` — a district's name, monogram and tint for the draft
  cards. Names come from the lobby's `DistrictDefinition` assets rather than a
  switch statement, so the draft and the Workshop cannot disagree about
  what a player picked; the enum name is the fallback for the four base
  draft districts no loadout slot can hold.
- `SafeAreaBinder` — the UI Toolkit reader of `Screen.safeArea`.
- Layouts in `Assets/UI/Layouts/*.uxml`, styles in `Assets/UI/Styles/*.uss`.
  One theme serves both scenes: `Tokens.uss` (palette, then tokens named for
  their job) and `Components.uss` (the `ui-` classes both draw - button,
  sheet, bar, chip, toast, player mark, monogram tile). Neither is imported by
  a layout; both arrive through `UnityDefaultRuntimeTheme.tss`, which the
  Lobby and HUD `PanelSettings` share. So a token edit changes both scenes.
  Page stylesheets (`Lobby.uss`, `HUD.uss`, `NodeSheet.uss`, one per lobby
  page) hold only what that screen draws, plus any token only it uses.
  Precedence runs the other way from CSS intuition: a rule arriving through
  the theme loses to any rule in a stylesheet a layout imports, whatever the
  specificity.

  Three `PanelSettings` assets, not one — Lobby, HUD and Draft. They share
  the theme and the 390x844 reference frame, but each is a separate surface
  with its own sort order, and one asset tuned for the match is one asset
  that has to be re-checked whenever the draft changes.

**View/**
- `NodeView` / `NodePresentation` / `VillagerPositioner` — node visuals,
  where villagers stand on a node (its work positions, named by `WorkPositionNames`).
- `BoardTerrainView` / `TerrainPresentation` — the board's ground as code-built
  placeholder geometry: ocean and lake tiles, a Pier drawn as a bridge, and the
  legal-cell highlight (fill plus outline, so it never rests on colour alone) during
  the draft. Ocean and open lake have no collider. `TerrainPresentation` is the
  UnityEngine-free description of each cell. `GameManager` creates the view and
  shows the match board once the draft is placed.
- `OrderPresentation` — read-only: which orders to draw and how, including the
  amber dashed route of an order interrupted by a fight, derived from
  `targetNodeID` and so correct after a rollback.
- `DistrictVisualTable` — per-district art shared with UI through the theme.
  `GameManager` selects board prefabs from the table, then its per-district
  slot, then the default; `BoardArtPlacer` applies offset, rotation and scale.
  Missing entries preserve existing board art. Board and flat UI art fall
  back independently.
- `VillagerView` — villager visuals and movement interpolation.
- `NodeClaimBar`, `VillagerHealthRing` — world-space status indicators.
- `CoreBreachBar` — runtime-added Core progress bar when the channel is
  enabled; `BreachCueSettings` is shared with villager highlighting and
  the HUD's accessibility flags.
- `NodeHighlight` — the expanding ring used for move-order destinations
  and, configured smaller, for the lasso-armed cue.
- `VillagerTouchTarget` — constant-screen-size tap collider, built at
  runtime so the villager prefab needs no edit.
- `VillagerFlash` — touch-down white flash amount, composed over the
  per-state tint by `VillagerView`.
- `PathCurve` — rounds a node path into the curve a route is drawn along and
  a villager walks. Corner rounding rather than Chaikin, because the
  waypoints are leg boundaries the sprite has to arrive on.
- `PathCurveSettings` — the one shared instance of that curve shape, owned by
  `GameManager` and handed to both consumers so they cannot disagree.
- `MovementPathRenderer` — dotted routes for the local player, one line per
  distinct remaining route so a squad reads as one, fading as an order ages.
- `OpponentRouteSettings` — the rules of the one information gate in the
  game. Everything else is fully visible to both players, so an opponent
  route hands the player something new rather than withholding it.
- `ViewSide` — the UnityEngine-free maths behind the camera POV. Tested by
  `dotnet/NodeWar.View.Tests`.
- `IndicatorDirector` / `IndicatorSettings` / `IndicatorPlacement` — which
  in-match indicators exist, their tunables on `GameManager`, and the
  UnityEngine-free placement maths. See
  [In-match indicators](#in-match-indicators).
- `SortHeight` / `SpriteDepthSorter` — height and depth order inside a node or
  draft piece.
- `OutlineDriver` — the only thing that sets outline intents. Reads hover,
  villager presence and selection and the inspected node, even when no sheet opens,
  and is read-only against the simulation. `GameManager` builds it before
  the views that register with
  it, and hands it the same `SelectionSystem` the tap path uses, so hover
  and selection agree about ownership by construction rather than by two
  copies of the same rule.

**View/Outline/** — its own assembly, `NodeWar.View.Outline`

The group-silhouette outline system. It is separate from `View/` proper
because its ID lifecycle is the part with real edge cases in it, and an
assembly of its own is what lets those cases be unit tested against a plain
object with no GameObject, no scene and no render pipeline — the reason
`IOutlineGroup` exists at all.

- `OutlineRendererFeature` — the entry point into URP, and the project's
  first custom renderer feature. It must be listed in **both**
  `Assets/Settings/Mobile_Renderer.asset` and `PC_Renderer.asset`.
- `OutlineMaskPass` / `OutlineCompositePass` — draw the registered groups as
  IDs into an offscreen target, then dilate ID boundaries into outline colour.
- `OutlineRegistry` / `OutlineIdAllocator` — ID allocation and the draw list.
  **A group holds an ID only while it is actually outlined**; registration is
  not what grants one, a style other than `None` is. So "nothing is outlined"
  is an empty list and both passes are skipped, and nothing needs cleanup when
  a node or villager goes away.
- `OutlineGroup` / `IOutlineGroup` — marks a node or villager root as one
  silhouette, so seams inside it grow no line. Added at runtime, like
  `NodeHighlight` and `VillagerTouchTarget`, so no prefab needs editing.
- `OutlineStyle` — the states an outline expresses: `Present`, the thin line
  every living villager carries all match, then the transient ones (hover,
  contested, selected, command ack). **The numeric
  order is the priority order**, so reordering the enum silently changes which
  state wins a conflict; `OutlineStyleTests` pins it so a reorder fails a test
  instead of changing the game. Player ownership is not a style:
  `OutlineDriver` tints the inspected node's outline from its claim bar
  (or its owner for a Core), and a villager's `Present` line with its owner's
  colour, independently of the style priority.
- `OutlineSettings` / `OutlineScreenBounds` — the palette and thickness asset
  (`Assets/Settings/OutlineSettings.asset`), and the screen-space scissor.

Tested by `Assets/Tests/EditMode/Outline/`, its own test assembly.

### Camera POV and sprite order

The camera looks from one of four sides. A **view side** is 0..3 quarter turns,
yaw = 90 x side. It is a viewing direction, not a player ID: nothing maps a
player to a yaw by table.

- **One way in.** `CameraController.SetPOV(side, pitch)` is the only thing that
  turns the camera. The draft, a networked or bot match, the local-test player
  switch and a spectator all reach it (through `SetDraftMode` or `SetViewer`),
  and it is where the sort axis, the shared `Billboard` facing and the
  `POVChanged` event change together. `RotateView(±1)` steps a spectator through
  the same call; Q and E do it from the keyboard while `MatchConnection.isSpectator`.
- **One place picks the side.** `ResolveViewer(mode, playerID)` puts a player
  behind their own core, snapped to the nearest quarter turn from the board
  centre, so it holds for two or four players and any layout. On the default
  board that gives P0 yaw 180 and P1 yaw 0. A spectator gets side 1, side-on for
  a board that runs along Z. Core positions come from the board's
  `initialPlacements` (so the draft can resolve before any node exists) and are
  refined by `SetHomeAnchor`. `BoardFraming` (UnityEngine-free) supplies the board centre
and the pan bounds, widened to the node grid whenever the asset's numbers would clip
it, so the opening zoom shows every column of whichever map is loaded.
- **Framing follows the side.** The per-side default, the home anchor and its
  push toward the opponent, and the draft's rig offset are all computed along the
  side's forward direction, not from a player slot. Pan, momentum and focus work
  from ground points under the pointer, so they turn with the camera. Bounds are
  the exception on purpose: world-axis rectangles on the rig position, which say
  where on the board the camera may look and do not depend on the side.
- **Sort mode is `CustomAxis`, not `Orthographic`**, because orthographic
  sorting measures along the live camera direction and flickers between close
  sprites as the camera moves. The axis is the side's horizontal forward, so it
  points away from the camera and Unity draws the nearer sprite last. It changes
  in `SetPOV` and nowhere else, never while panning.
- **Height, then depth.** Each node (its `GFX`) and each draft piece is one
  `SortingGroup`, ordered against its neighbours by that axis. Inside it,
  `SpriteDepthSorter` sets `sortingOrder = height x 256 + depth rank`. Height is
  the authored `SortHeight` (default 0): a sprite physically on top of another
  always draws in front of it. Equal heights order by depth along the axis, ties
  by index. It recomputes on `POVChanged` and when sprites are added, never per
  frame. The ground quad is hoisted out of `GFX` at runtime so it stays its own
  group on the Ground layer.
- **Draft pieces** (`DraftPlacementPreview`, ghost and confirmed) take the
  camera's yaw and follow it if the side changes; the sticker is one height above
  the cube.

### In-match indicators

Popups that tell the player something happened somewhere: a fight, a node of
theirs being taken, an enemy headed for their Core. Two halves, split along
the layer line.

- **`IndicatorDirector` (View/) decides what exists.** It is a plain class
  driven by `ITickProvider.TickSimulated`, so a paused or finished match changes
  nothing. Moments come from the tick's `TickEventLog` (`CombatStarted` starts a
  battle; `NodeNeutralised` turns "under attack" into a brief "lost" pulse).
  Conditions are read off `SimulationState` once per tick. Each kind has a
  debounce before it shows and a grace before it goes (`IndicatorSettings`, on
  `GameManager`), so a one-tick flicker neither pops nor re-pops. It is
  read-only against the simulation, and its timing is wall-clock presentation
  only.
- **`IndicatorLayer` (UI Toolkit HUD) draws it.** The in-view zone is the central
  60% of the board the player can see: the HUD's `hud__board-space`, less the
  node sheet while it is open. It has hysteresis, entering at 0.60 and leaving
  at 0.66. Inside it a subject takes its in-view form (smaller, or nothing).
  Outside it the icon sits on the subject clamped inside the board, clear of the
  control docks, with an arrow when the subject is off screen. The two
  rectangles differ on purpose: measuring the zone against the dock-inset area
  would pull the centre of attention off the centre of the screen. Overlapping
  edge icons merge into the most important one with a count, up to a cap.
- **Battle icon lift is separate from arrow aim.** Its default lift is 0.75
  world units (`battleHeight`); other node indicators use `nodeHeight`.
  A clamped battle arrow aims at the projected node centre with `atan2`, using
  one extra projection only when it points, so changing lift cannot skew its aim.
- **Only visible edge icons pick.** A tap eases the camera to the subject through
  `CameraController.FocusOnWorldPoint`, which counts as a manual move so closing
  a sheet does not undo it. The gesture source's EventSystem check stops the
  same press reaching the board.
- **Motion is USS.** Adding `.ind--in` pops an icon with `ease-out-back`;
  removing it falls back to `.ind`'s `ease-in` taper. The target style's
  transition is the one in force, so one scale property overshoots in and
  tapers out, and reduced motion drops the overshoot.
- **Timers run on the layer, not on an element.** An element's scheduled items
  pause while it is detached and resume when it is attached again, so a recycle
  timer on a pooled element fires into its next life. That detached a freshly
  reused indicator in play; the layer's scheduler plus a generation stamp is
  the fix.
- **Icons are themed `LobbyIcon` sprites with Painter2D glyph fallbacks.**
  Indicator icons use the `OffScreenIndicator` context. Fredoka carries no
  symbol glyphs, and a fallback font would differ by platform.

### Where a villager is, mid-link

A villager in transit has no position of its own. `currentNodeID` is the node
it last stood on, and how far it has come is `moveProgress` counted in ticks
along the leg `movePath[movePathIndex]` to `movePath[movePathIndex + 1]`,
against `linkWeight * moveSpeedTicks`.

Normally `movePath[movePathIndex]` and `currentNodeID` are the same node. The
one exception carries meaning: when they differ, the villager is **walking a
reversal** -- it was retargeted part-way across a link, and is returning to
`currentNodeID` from the node named at `movePathIndex`, which it turned around
before ever reaching. Expressing the return as forward travel along the
reversed leg is what lets the tick loop stay ignorant of it: `TickMovement`
counts up and arrives exactly as it does on any other leg.

Two consequences a reader needs. Anything that zeroes `moveProgress` while
keeping `movePath` -- combat, above all -- must call `CollapseReversalLeg`
first, or the villager is left standing on a node it never reached. And the
view must anchor its interpolation to `movePath[movePathIndex]` rather than to
the sprite, or it draws a line the simulation is not walking.

## Networking model

Node War uses **lockstep**: peers never send simulation state. They exchange
tick inputs (the tick's `GameCommand`s plus a checkpoint hash), and beside
them handshake, draft, heartbeat and emote packets. Both machines run the identical deterministic simulation
(`GameSimulation.SimulateTick`) from the identical sequence of commands
and must therefore arrive at identical results every tick.

- **`ITickProvider`** — the shared interface (`TickAlpha` property)
  implemented by both tick drivers, so `View/` can read
  tick-interpolation progress without caring which one is active.
- **`TickRunner`** — used for local (non-networked) play, including
  bot matches. Accumulates `Time.deltaTime`, drains `InputBuffer` each
  tick, calls `CommandProcessor` then `GameSimulation.SimulateTick`
  directly with no network wait.
- **`LockstepCore`** — used for networked matches. Same accumulator
  loop as `TickRunner`, but a tick only executes once both the local and
  the remote `TickInput` for that tick number have arrived; it enforces a
  fixed command-processing order (all of P0's commands, then all of P1's)
  and applies an input delay so local input for tick *N* is generated and
  sent ahead of when tick *N* actually simulates, to hide network latency.
  While stalled, speculating or holding it re-sends every local input the
  peer may still lack (from `PEER_LAG_TICKS` behind to the newest), not only
  the last one: with inputs in flight ahead, one lost packet would otherwise
  stop both clocks for good while heartbeats kept the link alive. The resend
  interval runs on its own clock; generating an input must not reset it, or a
  speculating side (which generates one every tick) resends nothing.
- **Every input goes out with the six before it** (`REDUNDANT_INPUTS`), one
  datagram each, so a loss shorter than six ticks is bridged by a later copy
  with no round trip. Six comes from the lossy-link sweep in
  `NodeWar.Network.Tests` (`SweepTests`); the constant's comment has the
  numbers. A side that has just fallen behind its peer's clock runs at 1.5x
  until it catches up, and the threshold is the peer's own reported delay
  plus 3 ticks.
- **Input delay adapts.** A match starts at 2 ticks (200 ms) on both sides,
  and the first 2 ticks are pre-seeded empty, so the start needs no
  agreement. After that each side watches the *peer's* inputs arrive: three
  late ticks in 30 and it asks the peer for one more tick; 20 s calm with 140
  ms of slack spare and it asks for one fewer (`InputDelayController`). The
  request travels in two header bytes of every `TickInput`
  (`senderDelay`, `requestedDelay`, protocol 4) and is absolute, not
  relative, so duplicate copies do nothing. The peer applies it to its own
  stamping, between 2 and 6 ticks, at most once a second, ignoring anything
  out of range and any copy older than a request it already read. Raising the
  delay generates the extra due inputs at once (empty after the first), so
  the peer never waits on a tick that does not exist; lowering needs no code.
  Ticks that ran during a speculation, a hold, a catch-up or a silent peer
  are ignored, because there lateness is an outage, not a slow link. The
  delay is not part of the simulation and is not logged: commands are
  applied on the tick they were stamped for.
- **Tested against a simulated link.** `LockstepCore` takes its clocks and
  transport from the caller, so `dotnet/NodeWar.Network.Tests` runs two of
  them over an in-memory link with loss, burst loss, duplication,
  reordering, delay, jitter, outages and frame spikes. Each run is judged
  against the same match on a perfect link with no networking: commands are
  scripted by the tick they apply on, and the confirmed state hashes must
  equal that reference, which also catches both peers agreeing on a wrong
  rollback. `SweepTests` (explicit) prints the numbers behind the tuning
  constants.
- **Emotes ride beside lockstep, not inside it.** They are cosmetic, so they
  are never a `GameCommand`: `PacketType.Emote` (8) is a 5-byte packet sent
  twice over UDP and de-duplicated on its sequence number. It never waits for a
  tick and never reaches `CommandProcessor`. `LockstepCore` is the networked
  `IEmoteChannel`, and a local or bot match gets a `LocalEmoteChannel`. After
  game over the core keeps pumping emotes and heartbeats, with no disconnect
  check, so a closing emote still arrives. An older build ignores type 8,
  because the packet switch has no default. `EmotePanel` (HUD) applies the
  rate limit (under 5 per 1 s and under 10 per 5 s) on send and again on
  receive, and owns mute.
- **Recording** — both tick drivers raise `CommandsApplied` (the tick count
  before, and the commands in the order they were applied — lockstep's P0
  then P1, the local driver's buffer order) and `HashComputed` (the tick
  count after, and the hash). `GameManager` feeds both to a
  `MatchRecorder` for drafted matches; Testing mode is not recorded.
  The drivers know nothing about logs. Finished logs are saved locally.
  A ranked match's header carries the server's match ID and the record's
  player order, which the referee checks. `GameManager` hands its log to `PendingRankedReports`, which keeps it per player and uploads it (after the match, at lobby load, before a ranked queue) until the server answers. Other matches use a
  local ID and are never reported.
- **Desync detection** — every 50 ticks
  (`LockstepCore.DESYNC_CHECK_INTERVAL`), each peer computes
  `SimulationStateHasher.ComputeHash(simState)` and includes it in its
  next outgoing packet; the receiving peer compares a non-zero received
  hash against its most recent stored local hash and fires `OnDesync` on
  mismatch. The packet does not name the checkpoint tick; lockstep keeps
  the two in step (see `docs/simulation-rules.md`, *Desync detection*).
- **A command's player comes from its sender.** `LockstepCore` stamps the
  peer's commands with the peer's slot and its own with its own, whatever
  the wire said, and holds each player to 64 commands a tick. Tick inputs
  outside what an honest peer could send are ignored. Relay runs over DTLS
  (protocol 3). Protocol 4 added the two delay bytes to `TickInput`. Protocol 5 added the
  pre-draft setup exchange: the host sends `MatchSetup` (map ID, board hash, simulation
  version, balance hash), the guest checks it against the shipped catalog
  (`PremadeMaps.Catalog`) and answers `MatchSetupAck`, and `SetupAgreement` ignores every
  draft packet until that agreement holds. Match logs carry it as the SETUP chunk (tag 11)
  beside BOARD_V2 (tag 10); the referee checks the board against its own catalog. DirectUDP
  reads only the connected peer's endpoint.
  In the Editor and Development Builds, F8/F9/F10 simulate a 1/5/20 s drop
  on that copy, and its ranked server calls fail for as long
  (`BackendServices.SimulatedOffline`), as a real lost connection would.
  Shift drops only outgoing peer packets.
- **A short blip plays on** (8.2e). An input a little late is ordinary
  jitter (Relay latency against the input delay) and waits a frame, as
  plain lockstep did. Once the opponent's input is 300 ms late,
  `LockstepCore` copies the state (`SimulationState.CopyFrom`) and keeps
  simulating for up to 20 ticks, predicting the opponent idle. The HUD
  shows "Opponent's connection is unstable" (in a ranked match, "Reconnecting…"
  when the server cannot be reached either) only once a speculation has
  run 5 ticks. Speculative ticks
  raise `TickSimulated` but are never recorded or hashed. Once the real
  inputs for the whole span arrive, it rolls back and replays the span with
  them. That replay is the only pass that records and hashes, and it plays
  no cues. It always rolls back, even when the inputs were empty, so the
  path runs on every blip. Past 20 ticks, or on a speculative game over, it
  rolls back and holds. `RolledBack` lets `GameManager` despawn villager
  views past the confirmed count (the state respawns them) and drop
  selections of villagers that no longer exist. After an abandoned span,
  local inputs already sent for future ticks are not generated again, so
  input delay does not grow.
- **Disconnects hold, they do not end the match** (8.2c). `LockstepCore`
  starts a hold when no tick has advanced for 2 s while unpaused, or when
  a speculative span runs out. It
  measures ticks rather than packets because with one-way loss a side keeps
  receiving heartbeats while it waits on an input that never comes. During
  a hold it keeps receiving, resending (every 250 ms) and sending
  heartbeats, and resumes with a fresh clock when the missing input
  arrives. `GameManager` runs a `DisconnectHold` (Backend/Shared) on those
  events and shows it in the HUD's hold overlay. The stages are: 0-10 s
  wait; 10-60 s the player may claim the win (ranked) or leave (private);
  at 60 s the hold resolves itself. In a ranked match every decision is
  the server's (see *Holds and presence* below); the local clock only
  drives the countdown. A ranked hold never ends the match without a server
  answer. After 90 s with the server unreachable, it offers "Leave match"
  instead. A phone returning from the background asks for the match's
  result once, in case it was decided while away. A resolved hold stops
  the lockstep core and words the end card for its cause. The uGUI HUD has no overlay and keeps the old
  immediate end. The draft still has its own disconnect end in
  `DraftManager`: 5 s without a packet, or 60 s for a peer that has sent
  nothing yet, such as one still loading the scene (the draft shows a
  waiting cue until both are ready).

## Backend, match logs and the referee

Accounts, progression, inventory and ranked match records use Unity
Gaming Services: Authentication, Cloud Code (C#), Cloud Save and
Matchmaker, with no custom game server. Matches stay peer-to-peer
lockstep. Cloud Code verifies submitted logs and settles eligible,
agreeing reports, and a ranked match uploads its log when it ends. The staged detail is in [BACKEND-PLAN.md](../BACKEND-PLAN.md)
(temporary; Notion **Phases** own future work).

```
Assets/Scripts/Backend/          client services, NodeWar.Backend
  GameServices                   UGS init + sign-in; the environment follows the build type
  BackendServices                picks the UGS service or its local fake (Tools > Node War >
                                 Backend > Use Local Fakes); remembers state for the current account
  Ugs*Service / Local*Service    accounts (Unity Player Accounts), player state, inventory,
                                 match reports, match history, ranked queue (tickets) and
                                 ranked match (rendezvous, confirm, leave)
  PendingRankedReports           ranked logs kept per player and retried until the server answers
  Shared/                        DTOs and rules compiled by Unity AND linked into Cloud Code:
                                 player records, catalog/equip rules, protocol version and service contracts
  Shared/RankedQueuePresenter     UnityEngine-free ranked attempt controller and IRankedQueueView
  Shared/RankedRendezvous         UnityEngine-free join-code exchange around IRankedConnection
  Shared/DisconnectHold           UnityEngine-free three-stage hold: presence, claim, resolution
  Shared/RankedResultTracker      UnityEngine-free end-card follower of the server's result
  Catalog/                       CatalogDefinition asset + editor Generate / Export
  Editor/BalanceExport           writes the shared balance for the server, named by content hash
  LocalMatchLogStore             finished logs on disk, newest 20
Assets/Scripts/MatchLog/         NodeWar.MatchLog: format, MatchRecorder, MatchReplay
dotnet/NodeWarCloud/             the Cloud Code module (deploy: ugs deploy dotnet/NodeWarCloud -e development)
dotnet/NodeWar.Progression/      rating, RR, arenas, catalog validation, era unlocks, match settlement
dotnet/NodeWarCloud/Matchmaker/  ranked.mmq, the deployed queue rules (the matchmaking rules live here)
```

- **Services are async and may refuse.** Every client call can fail, and
  the UI shows what the server returned, never an optimistic guess. Each
  service has a local fake for offline Editor work and tests. Player
  state and inventory share rules with the server; report, history and
  queue fakes provide configurable results rather than server settlement.
  `BackendServices` keeps `LastKnownState` scoped to the current account,
  ignores calls that finish after an account switch, and raises
  `StateChanged` when accepted state arrives.
- **Cloud Code entry points** in `NodeWarCloud` are `GetPlayerState`,
  `Equip`, `VerifyMatch`, `ReportMatch`, `GetMatchHistory`, `Rendezvous`,
  `ConfirmConnected`, `LeaveMatch`, `GetMatchResult`, `Presence`,
  `ResolveHold`, `Matchmaker_Allocate` and `Matchmaker_Poll`. The last two
  are allocator callbacks and refuse calls carrying a player identity; the
  six ranked-match functions refuse calls without one.
- **Player data** has four protected Cloud Save state records (`rating`,
  `rank`, `inventory`, `history`) plus an `activeMatch` claim and a `discipline`
  record: the player reads them, only Cloud Code writes.
  `GetPlayerState` creates missing state records and grants catalog
  variants through the highest arena reached, plus default skins. It
  fills missing equipment with era-0 variants and default skins; match
  allocation creates the active-match claim.
- **Catalog and eras.** Arena N permits variants up to era N; ownership
  persists after demotion, while equipped variants are clamped to the
  current arena. Every suit and district type
  is a catalog base (`suit.warrior`, `district.rampart`) with one variant
  per era (`suit.warrior.e3`) and skins (`skin.suit.warrior.default`).
  Item IDs are never renamed or reused: the export refuses a catalog that
  breaks `CatalogValidation.ValidateAgainstPrevious`. `Equip` checks
  ownership, the base, and that the era is usable at the current arena.
  A match launches with the equipped eras and skins (`LoadoutData`), the
  simulation plays each district at its placer's era, and skins never
  reach the simulation.
- **Match log** (`.nwml`): magic, format version, then tagged,
  length-prefixed chunks — HEADER, BOARD, LOADOUTS, DRAFT, TICKS, HASHES,
  RESULT, ERAS, SKINS, BOARD_V2 (tag 10) and SETUP (tag 11). A reader skips
  tags it does not know; a known tag never changes meaning, so a changed payload
  gets a new tag, which is why terrain went into BOARD_V2 and left BOARD alone.
  A log of simulation version 3 or later carries BOARD_V2 (the cell terrain, the
  district slot mask and both base draft pools beside the BOARD fields) and SETUP, always
  together; older logs carry BOARD and no SETUP, and a log with both boards or a
  mismatched pair is refused. The header carries protocol, simulation version and
  content hash.
- **Referee.** `VerifyMatch` rebuilds the match with `MatchFactory`,
  replays the logged commands with `MatchReplay`, and checks every logged
  hash, the final hash and a declared winning result. For a current log it first
  requires the SETUP map ID to be in its own catalog and the recorded board to hash
  to the catalog's board, never to a hash the log supplies. It proves a log is
  consistent, not that its commands are authentic; reporting does not add
  command signatures. Replays share a process-wide lock because
  `MatchFactory.Configure` sets simulation statics.
- **Match records and settlement.** `MatchAllocation` stores a private
  Cloud Save custom item `match-<id>` with the roster, build identity, the
  map ID and board hash (`RankedMap`: the server assigns it from its own catalog,
  today `hourglass-01`, and neither player proposes one) and
  pre-match rating, rank and owned-variant snapshots. `ActiveMatchClaims`
  allows one active match per player, with expiring claims and conditional
  writes. `ReportMatch` checks membership and log eligibility against that
  record, including that the log's SETUP and board are the record's map, then runs the referee. Two accepted reports must agree on
  winner, end tick and final hash before rating, RR, arena and inventory
  updates settle. Retries are idempotent; disputed or expired matches do
  not settle. `GetMatchHistory` reads the caller's history and stored match
  outcomes. `MatchSettler` is the one settlement path, called with the
  agreed winner or, for a forfeit, the opponent of `forfeitedBy`, which is
  committed under the record's write lock before any player write.
- **Leaving a match.** `MatchRendezvous.Leave` voids a match that never
  started (neither both players' `ConfirmConnected` nor an accepted report), an expired one, or a pending one
  past its 10-minute timeout, releasing both claims. It settles a forfeit or an agreement already committed to the record. A played match needs an explicit forfeit; a caller who has already reported waits.
  An in-match **surrender** (ranked only, behind a confirm in the settings
  card) is that same forfeit, sent mid-match; the match ends on this side
  once the server has it, and the opponent's hold learns it from the server.
- **Holds and presence** (`MatchHold`). Every ranked client calls
  `Presence` for the whole match, from the draft on: every 4 s as a
  heartbeat (`PresenceHeartbeat`) and every second while holding or speculating. A hold
  is self-declared, so absence has to be something the server observed:
  a player who is playing is always "seen". `Presence` writes only the
  caller's own `presence-0`/`presence-1` key in the match's custom item,
  with no lock, and never the `record` key, so the two players' writes
  never conflict with each other or with settlement. `ResolveHold` needs
  three things: the match started at least 15 s ago, so heartbeats have
  landed; the caller's server-measured hold is at least 10 s; and the
  opponent has not been seen for more than 10 s. It then commits
  `forfeitedBy = abandonedBy = opponent` under the record lock before
  settling. A void needs **both** players seen and holding (60 s / 50 s),
  which is what an honest broken link produces in lockstep; a one-sided
  hold never voids. A committed decision is finished before any claim
  expiry could void it. Every answer carries the terminal result once
  there is one. That is how a returning player learns they lost, and how
  an opponent learns of a surrender. `GetMatchResult` is read-only; its
  `cause` is Played, Forfeit or Abandoned.
- **Present but not holding.** A seen opponent that has not held while the
  caller held 30 s counts as absent. An honest client holds within seconds
  of its peer, so this one is keeping the match from advancing.
- **Both players left** (D23). A log that replays cleanly but stops short
  is kept as unfinished, with each Core's breaches. Once both players have
  one, the earlier end tick decides: fewer breaches wins, a tie voids, no
  strikes (`MatchEndCause.BothLeft`).
- **Strikes and non-reports** (`MatchDiscipline`, `DisconnectPenalty`,
  8.2d). A hold settled against `abandonedBy` strikes that player. A
  Pending match voided by its timeout with one accepted report adds a
  non-report for the silent player, and the second within 7 days is a
  strike. The ladder:
  - Levels 1-2: no block.
  - Levels 3-4: blocked for 2 min.
  - Level 5: blocked for 1 h.
  - Level 6: blocked for 1 day.
  - Level 7 and above: blocked for 2 days.
  - One level decays per 16 h without a strike.

  The record lives in its own protected key, `discipline`, outside the
  settlement batch. Each match records `disciplineApplied`, and claims are
  released only after it is set, so a failed write is retried by the next
  terminal call and never applied twice. `Allocate` refuses a blocked
  player. The queue shows the countdown and does not search.
- **The result on the end card** (7.4). The first report to arrive leaves
  a match Pending, so the uploader rarely learns the result from
  `ReportMatch`. `RankedResultTracker` asks `GetMatchResult` every 2 s for
  up to 40 s after a ranked match ends, and the end card's rank block shows
  the RR change and any promotion, a void, a dispute, "still waiting" or
  "offline". A settled answer is remembered through `BackendServices`, so
  the lobby strip is current on return.
- **Ranked matchmaking.** The `ranked` Matchmaker queue uses protected
  Cloud Save rating and arena data, with a widening rating window and an
  arena cap. Ticket build identities must match; the allocator also
  requires the server's protocol and simulation versions and a known
  balance hash. `UgsRankedQueueService` initializes player records before
  creating a ticket, polls for a match ID and deletes cancelled tickets.
  `PlayPopup` drives it through `RankedQueuePresenter` and
  `IRankedQueueView`: preflight (leave or forfeit a held match), queue,
  then `RankedRendezvous`. Record slot 0 hosts a Relay room and publishes
  its join code with `Rendezvous`; slot 1 polls for it and joins. Both
  confirm the connection, then the draft starts as in private play. A
  failed ticket or rendezvous voids the match and re-queues, up to three
  times in a row; a bot match, unranked, is offered after 90 s of search.
