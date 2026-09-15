---
type: Inventory
title: UI migration inventory
description: What the lobby and in-match UI actually consist of today, what references each piece, and what the phone-UI rebuild would have to move, keep or replace.
tags: [ui, lobby, hud, migration, ugui, ui-toolkit]
generated: { by: claude-opus-5, at: 2026-09-03T00:00:00Z }
status: historical
snapshot_of_commit: d0f4420
# No `sources:`, deliberately. This is a frozen snapshot of the UI layer as it
# stood before the rebuild, not a description of live code. Sources moving is
# the expected outcome here, not a staleness signal, so putting this document
# under the freshness check would produce a permanent false alarm. What it was
# taken against is recorded in the body instead. See docs/index.md.
---

# UI migration inventory

> **HISTORICAL — do not read this for what the UI is now.**
>
> This is the S0 snapshot taken at `d0f4420`, *before* the rebuild it exists to
> inform. Every uGUI lobby panel it describes now lives in `Assets/Legacy/`, and
> the UI Toolkit stack it calls "present, unused" is the live lobby. Its claim
> that the project contains no `.uxml`/`.uss`/`.tss` was true when written and
> is now emphatically false.
>
> For the current UI, read **[architecture](architecture.md) → *Where the UI
> lives***. Read this document only for why the migration was scoped the way it
> was.

Taken at `d0f4420`, against a working tree carrying uncommitted prefab work
(see [Working tree](#working-tree-at-the-time-of-this-audit)).

This is the S0 deliverable of the phone-UI rebuild brief. It records what
existed, what pointed at it, and where the brief's own description of the
project diverged from the code. It takes no position on the Option A/B/C
decision; it existed so that decision could be made against real numbers.

## What this was taken against

These were the document's declared sources while it tracked live code. They are
recorded here as history; the paths are as they were at `d0f4420`, and one of
them has since moved.

| Path at `d0f4420` | Why it mattered |
|---|---|
| `Assets/Scripts/Lobby/LobbyManager.cs` | panel navigation |
| `Assets/Scripts/Lobby/PlayerProfile.cs` | persistence and unlock stubs |
| `Assets/Scripts/Lobby/Data/LoadoutData.cs` | slot layout and `GameMode` |
| `Assets/Scripts/Game/Simulation/SimulationState.cs` | `DistrictType` and `SuitType` |
| `Assets/Scripts/Game/Core/GameManager.cs` | loadout→enum mapping, `UI_Manager` instantiation |
| `Assets/Scripts/Game/Network/NetworkManager.cs` | relay and direct-UDP transport API |
| `Assets/Scripts/Game/UI/SafeAreaFitter.cs` | safe-area insetting — **now `Assets/Legacy/Game/UI/SafeAreaFitter.cs`** |
| `Assets/Scenes/Lobby.unity` | lobby scene UI wiring |
| `Assets/Prefabs/Game/UI/UI_Manager.prefab` | in-match HUD prefab |

## Environment

| Fact | Value |
|---|---|
| Unity | `6000.5.9f1` — i.e. Unity 6.5.9 |
| Claude Code | 2.1.259 (>= 2.1.257, so `CLAUDE_CODE_SUBAGENT_MODEL_FORCE` is available) |
| uGUI | `com.unity.ugui` 2.5.0 |
| UI Toolkit | `com.unity.modules.uielements` 1.0.0 — present, unused |
| Existing `.uxml` / `.uss` / `.tss` | **none** |
| Render pipeline | URP 17.5.0 |
| Scenes in build | `Lobby.unity`, `Gameplay.unity`. `GFX Testing.unity` exists but is not in build settings |

### Assembly layout

Only three `.asmdef` files exist:

- `NodeWar.Simulation` (`noEngineReferences: true`) — the determinism boundary
- `NodeWar.TestBridge` (Editor only)
- `NodeWar.Simulation.Tests` (Editor only)

**Everything else, including all of `Assets/Scripts/Lobby/`, compiles into
`Assembly-CSharp`.** Confirmed by the definition assets, which serialise as
`m_EditorClassIdentifier: Assembly-CSharp::NodeWar.Lobby.SuitDefinition`.

New UI code therefore needs no `.asmdef` and no reference wiring. Adding one
would be a change, not a continuation — it would cut the new code off from
`Assembly-CSharp` types unless every reference were declared.

## Lobby — blast radius

Reference counts are exact: GUID occurrences in the scene, in every prefab /
`Resources` / `Data` asset, and whole-word class-name matches in other `.cs`
files.

| Script | Lines | GUID | LobbyScn | Prefabs | Other .cs |
|---|---:|---|---:|---:|---:|
| `LobbyManager` | 182 | `4a3ef4d00de388643b1c7d0b018d8ea8` | 1 | 0 | 2 |
| `LobbyPanel` (abstract base) | 29 | `06ecdbdd8352b5748ab9d9ca5f6f0b56` | 0 | 0 | 6 |
| `HomepagePanel` | 156 | `50c69578a9e1f3344a08e0885d201c5d` | 1 | 0 | 1 |
| `GamemodePanel` | 84 | `5a348db2f1a5dc54b8ae788a93281252` | 1 | 0 | **0** |
| `GroupSelectionPanel` | 335 | `55a0216778c52084596df34e76c2ffcb` | 1 | 0 | **0** |
| `ProfilePanel` | 116 | `d12c300edfb3f8d4183e2ba81de3daa2` | 1 | 0 | **0** |
| `ShopPanel` | 104 | `a22b86dde3a1cae4d9ec42e82c21faff` | 1 | 0 | **0** |
| `NetworkingModal` | 330 | `3d6efc4de986b7b44a9a367b22d0a5f7` | 1 | 0 | 1 |
| `RenameModal` | 125 | `0d4e03eae95b2c346aff1efa7d45f316` | 1 | 0 | 1 |
| `GroupSlotDisplay` | 82 | `76fb471711935e04e81cd3a50c9384c5` | 5 | 1 | 1 |
| `SelectableItemDisplay` | 247 | `63a478651b9000b459a9d279d81dcacf` | 0 | 1 | 1 |
| `TrophyBarDisplay` | 158 | `1b9746415c2617d45a02ee012b156cc7` | 1 | 0 | 1 |
| `TrophyBarLogic` | 82 | `3e9fd1484c9837748b8ec763fe422400` | 0 | 0 | 1 |

Data carriers, out of scope for replacement:

| Script | Lines | Referenced by |
|---|---:|---|
| `PlayerProfile` | 167 | 6 `.cs` files; instantiated at runtime, not scene-placed |
| `LoadoutData` | 24 | 6 `.cs` files |
| `NodeDefinition` | 14 | 9 `.asset` files |
| `SuitDefinition` | 14 | 5 `.asset` files |

**What breaks if `HomepagePanel` disappears:** one `SerializeField` on
`LobbyManager` in `Lobby.unity`, and one other `.cs` file. Nothing else. The
same is true of every panel — and four of the five panels have *zero*
references from other C# at all. They are reached only through
`LobbyManager`'s five `[SerializeField] LobbyPanel` slots.

The lobby is far more separable than a 1,700-line UI layer usually is. This is
the single most important number for the Option A/B/C decision.

### Lobby prefabs

`Assets/Prefabs/Lobby/` holds five prefabs: `Items/GroupSlot.prefab`,
`Items/SelectableItem.prefab`, `Items/StatIcon.prefab`, `Shop/DailyOffer.prefab`,
`Utility/Tick.prefab`.

`Lobby.unity` is 18,814 lines with 191 `RectTransform`s and one `Canvas`.

## In-match UI — where it actually lives

`Gameplay.unity` is 758 lines and contains **zero** `RectTransform`s and no
`Canvas`. The entire in-match UI is one prefab,
`Assets/Prefabs/Game/UI/UI_Manager.prefab` (4,917 lines, 48 `RectTransform`s,
56 `MonoBehaviour`s), instantiated at runtime by `GameManager` from a
`[SerializeField] GameObject uiManagerPrefab` (`GameManager.cs:53`, `:564`).

That is a clean seam. Any Option B work can swap the prefab behind a flag
without touching the scene at all.

| Script | Lines | UI_Manager | Other prefabs | Other .cs |
|---|---:|---:|---:|---:|
| `NodePanelManager` | 734 | 1 | 0 | 5 |
| `HUDManager` | 128 | 1 | 0 | 1 |
| `PanelSwipeDismiss` | 151 | 1 | 0 | 0 |
| `GameOverPanel` | 65 | 1 | 0 | 1 |
| `WheelDisplay` | 110 | 3 | 0 | 1 |
| `BreachDisplay` | 71 | 2 | 0 | 1 |
| `DistrictPanelPolicy` | 83 | 0 | 0 | 1 |
| `EquipEntryDisplay` | 121 | 0 | 1 | 1 |
| `RespawnEntryDisplay` | 87 | 0 | 1 | 1 |
| `AllocationWheel` | 121 | 0 | 1 | 1 |
| `*PanelContent` (5 files) | 564 | 0 | 0–1 each | 1 each |
| Draft UI (6 files) | 1,048 | 0 | 1–2 each | 1–5 each |
| `SafeAreaFitter` | 88 | **0** | **0** | **0** |

## Findings that change the brief

Each of these was verified against the code named beside it.

### 1. `SafeAreaFitter` is orphaned — safe-area handling is not implemented

The brief's §3.3 says deleting it "silently breaks notch handling everywhere."
It cannot: **nothing references it.** Zero occurrences of GUID
`3de237137ac36be4aaefd2cf14ba54b5` in any `.unity`, `.prefab` or `.asset`,
and zero whole-word matches in any other `.cs` file.

It is a correct, complete, `[ExecuteAlways]` `MonoBehaviour` that has never
been attached to anything. Its own doc comment — "No other project script reads
`Screen.safeArea`" — is true, and `docs/architecture.md:245` repeats it, but
both describe a component that is inert at runtime.

**Consequence:** safe areas are an *unmet* requirement, not a met one. Whichever
option is chosen, something has to attach this (or its UI Toolkit equivalent).
It is also the one item on the brief's do-not-delete list that is currently
free to delete.

### 2. Three of the "four mobile requirements met" — actual status

| Requirement | Script | Status |
|---|---|---|
| Gesture sizing in mm | `GestureThresholds` | **Met.** Used by 5 scripts |
| Constant-size touch target | `VillagerTouchTarget` | **Met.** Used by `GameManager`, `MovementPathRenderer` |
| Swipe-to-dismiss sheet | `PanelSwipeDismiss` | **Met.** Attached in `UI_Manager.prefab` |
| Safe area | `SafeAreaFitter` | **Not met.** Orphaned (finding 1) |

`DistrictPanelPolicy` is referenced only from `NodePanelManager.cs` — code-driven,
not scene-wired, so it survives any prefab change.

### 3. The Workshop's draftable list is 9 districts, not 12

The brief's Part 5 says "lists draftable districts from the fourteen; exclude
`None` and `Core`," implying 12. The real source is
`GroupSelectionPanel.allNodes` / `.allSuits` — `[SerializeField]` arrays
populated in `Lobby.unity`, holding exactly the assets in
`Assets/Data/Lobby/`:

**Districts (9):** Arsenal, Barracks, Camp, **Crossroads**, Market, Rampart,
Sanctuary, Shrine, Watchtower.
**Suits (5):** Warrior, Guardian, Scout, Berserker, Medic.

Farm, Mine, Village, Forge and Core have no `NodeDefinition` asset — consistent
with them being base-pool / `NodeSlotType.Fixed`.

A UI Toolkit Workshop must either replicate that inspector wiring or move to
`Resources.LoadAll`. The assets are in `Assets/Data/`, which is **not** a
`Resources` folder, so `LoadAll` would require moving them or an
`AssetDatabase`/Addressables path.

### 4. `Crossroads` is draftable in the UI and silently discarded

`Crossroads.asset` (`nodeID: node_crossroads`) is wired into
`GroupSelectionPanel.allNodes`, so it appears in the picker. But
`GameManager.MapNodeIDToDistrict` has its line commented out:

```csharp
//if (lower.Contains("crossroads")) return DistrictType.Crossroads;
```

It returns `DistrictType.None`, and `AddNodeFromID` drops `None` without
comment. A player can spend a district slot on Crossroads and get nothing.

This is a live bug independent of any UI work. `DistrictType` has no
`Crossroads` member at all — `None` is documented as "empty connector /
crossroads" — so the fix is to remove the asset from the picker, not to
uncomment the line.

### 5. `LoadoutData` slot counts cannot be read at runtime

The brief says three times to "read the counts from `LoadoutData`, not from
constants." `LoadoutData` is five flat named fields:

```csharp
public string suitID0, suitID1, suitID2;
public string nodeID0, nodeID1;
```

There is nothing to count without reflection. `GroupSelectionPanel` matches
this with five separate `[SerializeField] GroupSlotDisplay` fields, and
`GameManager` with three hardcoded `AddSuitFromID` calls and two
`AddNodeFromID` calls.

Three honest options, none of which is "read the count":

1. Keep flat fields; put the count in one named constant beside the struct and
   have the UI build slots from it. Smallest change.
2. Convert to `string[] suitIDs` / `string[] nodeIDs`. Genuinely data-driven,
   but `LoadoutData` is persisted through `JsonUtility` in
   `player_profile.json` and carried across the scene load by
   `MatchConnection`, so this needs a migration path for existing saves.
3. Reflect over the struct. Works, but is worse than option 1 in every respect.

**This needs a decision before the Workshop is built either way.** Option 2 is
the only one that makes the open 2-vs-3 balance question cheap to answer later.

### 6. Sprite coverage is much thinner than the brief assumes

The brief's Part 4 says sprites exist and that "ugly coloured squares" no
longer apply. Complete inventory of `Assets/Sprites/`:

| File | Mode | Sub-sprites | Tracked? |
|---|---|---:|---|
| `UI/UI_Shop.png` | Multiple | 29 | **untracked** |
| `UI/Circle.png` | Single | — | yes |
| `UI/Ring.png` | Single | — | yes |
| `UI/RingBG.png` | Single | — | yes |
| `UI/Square.png` | Single | — | yes |
| `Nodes/Houses/AssetsSpriteSheet.png` | Multiple | 89 | yes |
| `Nodes/Houses/HousesSpriteSheet.png` | Multiple | 19 | yes |

`Assets/Sprites/Icons/`, `Assets/Sprites/Suits/` and `Assets/Sprites/Villagers/`
are **empty directories**.

Two consequences:

- **Every `icon` field on every definition asset is `{fileID: 0}` — null.** All
  9 `NodeDefinition`s and all 5 `SuitDefinition`s. There are no district icons
  and no suit icons. The Workshop cannot show real art for the things it exists
  to pick, whichever UI framework it is built in.
- **The slice names carry no meaning.** They are `UI_Shop_0`…`UI_Shop_28`,
  `HousesSpriteSheet_0`…`_18`. The brief's plan to "hand that list to subagents
  in their spec" does not work: no spec written from names alone can pick the
  right slice. Choosing sprites requires looking at them, which makes it an
  orchestrator-or-human step, not a subagent step.

### 7. Direct-IP networking exists and works

The brief says LAN "does not exist" and to show only a join code. Half right.
`NetworkManager` has two transports (`TransportMode { DirectUDP, UnityRelay }`):

- `StartAsRelayHost()` / `StartAsRelayClient(joinCode)` — Relay, plus
  `JoinCode` and `RelayReady` properties
- `StartAsHost(port = 7777)` / `StartAsClient(remoteIP, port)` — direct UDP
- `GetLocalIPAddress()` — a static helper that only makes sense for an IP field

What does not exist is *discovery* (auto-finding peers on the subnet). Manual
IP entry is fully implemented. Whether to keep surfacing it is a product call,
not a "it isn't there" call.

**Wiring note:** `StartAsRelayHost` is `async void`, so `JoinCode` is not
populated when it returns. The UI must show a pending state and poll
`RelayReady`. Any spec that says "call it, then display the code" produces a
blank field.

### 8. More dead fields than the brief lists

The brief flags `boxesAvailable` / `boxProgress` as written and read by nothing.
Confirmed — and two more join them:

| Field | Declared | Read anywhere |
|---|---|---|
| `PlayerProfile.data.boxesAvailable` | `PlayerProfile.cs:25` | no |
| `PlayerProfile.data.boxProgress` | `PlayerProfile.cs:26` | no |
| `SuitDefinition.isGlobal` | `SuitDefinition.cs:13` | **no** |
| `NodeDefinition.category` (`NodeCategory`) | `NodeDefinition.cs:13` | **no** |

`isGlobal` matters: `GameManager.BuildDraftedSuits` hardcodes
`suits.Add((int)SuitType.Warrior)` for every player regardless of loadout, so
**Warrior is always granted**. The field that would express that is present,
unset (absent from the serialised `.asset`, so `false`), and ignored.

A Workshop that shows five equally-selectable suits is misrepresenting the
game: one of the three slots is being spent on something the player already
has. This should be surfaced — pinned, marked "always available", or excluded —
but which is a design call, not an implementation detail.

### 9. Unity version — revision 1 was right

The brief's correction #8 says "`6.5.9` is not a Unity version string."
`ProjectSettings/ProjectVersion.txt` reads `m_EditorVersion: 6000.5.9f1`. Unity 6
uses `6000.x` internally for what is marketed as Unity 6.5.9. The original
brief's value was correct and the correction is wrong.

## Working tree at the time of this audit

`git status` is **not clean**: 43 deletions, 9 modifications, 13 untracked.
The brief's S0 exit condition assumes a clean tree.

This is in-flight prefab reorganisation, not damage:

- `Assets/Prefabs/Game/Nodes/` (13 `Node_*` prefabs) deleted; the tree now has
  `Assets/Prefabs/Game/RevisedNodes/` with variants.
- `Assets/Prefabs/Game/UI/PanelContent/` — 5 `*Content.prefab` + 2 `Entry/`
  prefabs deleted; `CorePanel`, `EquipPanel`, `ForgePanel` and `Entries/`
  are untracked replacements.
- Modified: both scenes, `UI_Manager.prefab`, 3 lobby prefabs,
  `SpriteOrientationOffset.cs`.
- Untracked: `Assets/Shaders/`, `Sprite.mat`, `UI_Shop.png`.

**Every deleted prefab GUID was checked against all scenes, prefabs and assets.
All 20 return zero references** — the re-pointing was done properly, so there
are no dangling GUIDs.

This work should be committed or stashed before any UI migration begins.
Mixing it with a migration diff would make the one-commit-per-removal rule of
§3.2 unenforceable.

## Reference method

Reproducible from the repo root:

```bash
# GUID of a script
grep -m1 'guid:' Assets/Scripts/Lobby/HomepagePanel.cs.meta

# every asset that references it
grep -rl "<guid>" Assets --include='*.unity' --include='*.prefab' --include='*.asset'

# every other C# file naming the class
grep -rlw "HomepagePanel" Assets/Scripts --include=*.cs | grep -v '/HomepagePanel\.cs$'
```

The GUID grep is the step §3.2 calls out as the one that gets skipped. Note
that `find | while read` breaks on `Assets/Scripts/Game/UI/Panel/Content UI/`
and `Display Elements/` — both contain spaces. Use `-print0` / `read -d ''`.

## Decisions taken at the close of S0

Taken by DonohoeCUA, 2026-09-03. Recorded here so S1 has a written scope;
**these belong in Notion under UI & Panels and must be carried across on the
next `/update` run.**

### 1. Scope — Option B, everything

Both scenes move to UI Toolkit. The audit's case for it over Option A: the
in-match UI is a single runtime-instantiated prefab
(`GameManager.uiManagerPrefab`), so the gameplay half is a prefab swap behind
a flag rather than a scene rewrite — materially cheaper than the brief assumed.

The brief's S6 (HUD) and S7 (node panel content) are in scope. They still run
only after S5 proves the migration pattern.

### 2. `LoadoutData` becomes array-backed

```csharp
public string[] suitIDs;   // length 3 today
public string[] nodeIDs;   // length 2 today
```

This makes the open 2-vs-3 balance question a data change. It is not free:

- `PlayerProfile` persists `LoadoutData` through `JsonUtility` into
  `player_profile.json`. Existing saves carry `suitID0`…`nodeID1` and will
  deserialise to empty arrays. **A migration path is required**, or existing
  players silently lose their loadout.
- `GameManager.BuildDraftedSuits` / `BuildDraftedNodes` have five hardcoded
  `Add*FromID` calls to replace with loops.
- `GroupSelectionPanel` has five `[SerializeField] GroupSlotDisplay` fields.
  Superseded by the new Workshop, but it must keep compiling until S5.
- **`DraftSerializer` is a hard blocker.** It writes and reads all five fields
  by name across the wire — `SerializeDraftLoadout` at
  `DraftSerializer.cs:61-65`, `DeserializeDraftLoadout` at `:109-125`. This is
  a **wire-format change**: both peers must agree, so it cannot ship
  incrementally, and a version mismatch desyncs the draft.

`LoadoutData` is a lobby type, not a `SimulationState` field — no hasher
change and no determinism impact on `SimulateTick`. But
`.claude/rules/network.md` requires a serializer and the struct it encodes to
change in the same commit, and that rule applies here in spirit even though it
names `InputSerializer`: **`LoadoutData` and `DraftSerializer` change
together, in one commit, or not at all.**

This makes the array conversion a networking-layer change, not a UI change.
It should land as its own commit, ahead of the Workshop work that depends on
it, and not be bundled into a UI session.

### 3. Phone HUD — resources and breach survive; villagers collapse

| Group | Source | Phone treatment |
|---|---|---|
| 3 resource wheels | local player only | **Always visible** |
| 2 breach bars | both players | **Always visible** — win condition |
| villager count | both players | **Collapses** behind a tap / into the Core panel |

Reasoning: breach is the win condition and a player must be able to watch it
approach; resources drive every spend decision; villager count is the one
group already legible from the board.

`HUDManager.RefreshVillagerCount` hardcodes `"/25"` in a format string (`HUDManager.cs:126`). That
cap belongs in `BoardConfig` (`startingVillagersPerPlayer` and bonus spawns
already live there) and should move when the text does.

### 4. Player identity — shipped colours win, plus a non-colour channel

`--player-0` is **blue**, `--player-1` is **red**, matching
`HUDManager.p0Color` / `p1Color`. **The brief's Part 4 palette is inverted and
is wrong** — correct it rather than the code.

```
--player-0: rgb(102, 153, 255)   /* blue, as shipped */
--player-1: rgb(255, 102, 102)   /* red,  as shipped */
```

Every ownership cue additionally carries a second, non-colour channel — shape,
end-cap, pattern or label. This closes the open *colourblind-safe way to tell
the two players apart* card.

The immediate instance: `BreachDisplay` has its label commented out at
`BreachDisplay.cs:18` and `:37` —

```csharp
//[SerializeField] private TextMeshProUGUI labelText;
//labelText.text = "P" + playerID;
```

so today the two bars are identical but for hue. That is the exact failure the
card describes, and it is live in the shipped build.

**Note for S1:** `p0Color` / `p1Color` are `[SerializeField]`, so
`UI_Manager.prefab` carries its own serialised values that override the C#
defaults. Read the prefab's values, not the source defaults, when porting.

## Still open

- Arena tier themes (Notion, unresolved) — placeholders only, and no data
  structure may assume the four names are final.
- Whether Warrior stays hardcoded-global, and how the Workshop shows that
  (finding 8).
- What to do about Crossroads in the picker (finding 4).
- Who makes the district and suit icons (finding 6) — this now blocks the
  Workshop and the district panels from showing anything real.

## For the next `/update` run

- Record the four decisions above under **UI & Panels**.
- **Lobby panels** and **Mobile UI adaptation** chapters go stale the moment
  S5 lands; `Last Reviewed Commit` on both needs re-pointing.
- Sprites now exist, which contradicts *Decide who makes the art* — but only
  three sheets and no icons at all (finding 6), so the card is narrowed, not
  closed.
- Findings 1, 4 and 8 are defects found during audit and are candidates for
  loose Tasks with no Phase.
- The node-panel-versus-takeover question stays closed as bottom sheet.
