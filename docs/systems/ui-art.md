---
system: ui-art
summary: Resolves shared UI icons, district thumbnails and procedural gauge styling from an Inspector-authored theme.
sources:
  - Assets/Scripts/Lobby/Stage/StageArt.cs
  - Assets/Scripts/Lobby/Stage/StagePaperArt.cs
  - Assets/Scripts/Lobby/Stage/PaperRasterizer.cs
  - Assets/UI/Editor/UIArtThemeIconEntryDrawer.cs
  - Assets/UI/Fonts/
  - Assets/UI/Resources/UIArtTheme.asset
  - Assets/UI/Scripts/ItemFamily.cs
  - Assets/UI/Scripts/ItemTint.cs
  - Assets/UI/Scripts/ProfileViews.cs
  - docs/style-guide.md
  - Assets/UI/Resources/Lobby/
  - Assets/UI/Scripts/MkCard.cs
  - Assets/UI/Scripts/MkPanel.cs
  - Assets/UI/Scripts/MkSheet.cs
  - Assets/UI/Scripts/LobbyMotion.cs
  - Assets/UI/Scripts/SegmentedSwitch.cs
  - Assets/UI/Scripts/SegmentedSwitchMath.cs
  - Assets/UI/Scripts/LobbySwitch.cs
  - Assets/UI/Scripts/LobbyToggle.cs
  - Assets/UI/Scripts/LobbyElementMotion.cs
  - Assets/UI/Scripts/MkPlayerRow.cs
  - Assets/UI/Scripts/PaperCard.cs
  - Assets/UI/Scripts/PaperButton.cs
  - Assets/UI/Scripts/PaperMath.cs
  - Assets/UI/Scripts/InkPen.cs
  - Assets/UI/Scripts/LobbyIcon.cs
  - Assets/UI/Scripts/LobbyIconContext.cs
  - Assets/UI/Scripts/LobbyIconKind.cs
  - Assets/UI/Scripts/LobbyIconUsage.cs
  - Assets/UI/Scripts/UIArtLookup.cs
  - Assets/UI/Scripts/UiArtMath.cs
  - Assets/UI/Scripts/UIArtTheme.cs
  - Assets/UI/Scripts/UiRequired.cs
  - Assets/UI/Scripts/UiRequiredNames.cs
  - Assets/UI/Styles/Components.uss
  - Assets/UI/Styles/Home.uss
  - Assets/UI/Styles/Lobby.uss
  - Assets/UI/Styles/Profile.uss
  - Assets/UI/Styles/RankedQueue.uss
  - Assets/UI/Styles/SceneTransition.uss
  - Assets/UI/Styles/Settings.uss
  - Assets/UI/Styles/Shop.uss
  - Assets/UI/Styles/Social.uss
  - Assets/UI/Styles/Tokens.uss
  - Assets/UI/Styles/Workshop.uss
  - Assets/UI/Resources/Lobby/Workshop.uss
  - Assets/UI/UnityDefaultRuntimeTheme.tss
verified: da7f2081
---

# UI art

## Purpose
Shared art themes serve lobby, draft and match UI without changing behavior; procedural fallbacks keep missing art usable.

## Interface
Create a `NodeWar > UI Art Theme` ScriptableObject named `UIArtTheme` at `Assets/UI/Resources/UIArtTheme.asset`. `UIArt.Theme` loads `Resources.Load<UIArtTheme>("UIArtTheme")` once per play session; restart Play Mode after reference/entry changes.

`UIArtTheme.icons` is an ordered `IconEntry[]` of `{ LobbyIconKind kind, LobbyIconContext context, Sprite sprite, bool keepOriginalColours }`. `TryIcon(kind, context, out entry)` uses pure `UIArtLookup.SpriteIndex(entries, kind, context, kindOf, contextOf, hasSprite)` to resolve overrides. `districtVisuals` references `DistrictVisualTable`; its per-type `DistrictVisual.icon` supplies node-sheet art and `StickerOrIcon` supplies draft art before fallback mappings. Workshop uses generated paper buildings/villagers, with monograms as fallback.

`LobbyIcon : VisualElement` has independently mutable `Kind` and `Context`, constructors `LobbyIcon()` and `LobbyIcon(kind, context = Anywhere)`, and `SetResourceTint(Color)`. UXML accepts `kind="Tv" context="PageHeader"`. Attached lobby icons load white Phosphor Bold SVG VectorImages from `Resources/Lobby/Icons/Phosphor`, tinted by USS. Shop and non-lobby surfaces retain themed sprites and cached Painter2D glyphs.

`Tokens.uss` adds lobby-only green/purple `--p2/--p3` and edge tokens; the board has no four-player colours.

`LobbyIconUsage.All`, `For(kind)`, `IsUsed(kind, context)`, `IsValidThemePair` and `WhereChoices(kind)` supply semantic groups, labels and valid locations to Inspector/runtime validation. Active kinds distinguish resources, navigation, indicators, emotes and cosmetics; use this inventory.

Contexts are Anywhere, TopBar, PageHeader, NavBar, HeadsUpDisplay, NodeSheet, InlineText, EmotePicker, EmoteBubble, OffScreenIndicator, ShopCard, Workshop, Profile, Home. Context value 11 is reserved for the retired tree location and never matches. Retired kind values remain reserved, including SuitTreeAvailable/Owned/Equipped/Locked; their drawers are not active UI.

`ItemFamily.ForDistrict/NoteFor/ClassFor/SortKey` maps district IDs to Round/Square/Triangle/Combat grouping and fallback shapes; unknown districts use Square. `ItemTint.IndexFor/ClassFor/MonogramFor` provides stable eight-color item placeholders independent of list order. These are presentation identities, not ownership or catalog authority.

Gauge USS properties: `--res-thickness`, `--res-stop-0` through `--res-stop-5` customize rings and sheet readouts; stops override compatible `--ring-critical` through `--ring-rich`. `--dial-thickness`, `--dial-track-color`, `--dial-fill-color` customize `ProgressDial`. `UiArtMath.Geometry(value, fallback, min, max)` bounds procedural dimensions. `.sheet__res-readout-tint` opacity affects only the background, with expanded-bar overrides under `.sheet__res-readout--bar`.

`MkCard : VisualElement` supplies `mk-card`; `MkPlayerRow.SetPlayer(handle, status)` supplies avatar/name/status and `Trailing`. `MkSheet` owns bottom sheets; `SetScrollable` separates fixed panels from lists. `MkPanel` owns centered modals, scrims and router Back. Both open on `LobbyMotion` springs with 120ms fades; panels close in 140ms by scaling, sheets in 180ms by sliding down with only the scrim fading. `Components.uss` exposes `mk-` classes; Tab pages are transparent; full-page surfaces are square and unoutlined. Cards are rounded.

`PaperCard` and `PaperButton` retain UXML types and bindings. Attached lobby surfaces use USS panels and pressables, grain and bottom-edge lips; their procedural underlays are suppressed. Shop and non-lobby surfaces retain seeded paper drawing through `--paper-card-*`, `Seed` and `RotatePaper`. `PaperMath` is UnityEngine-free.

`SegmentedSwitch.Select/Changed` slides one face; `LobbySwitch` and `LobbyToggle` slide a knob; `LobbyToggleMath` fixes toggle travel. All retarget critical `LobbyMotion` springs; segment colours crossfade. `--mk-private` supplies the purple Private ribbon meaning.

`UiRequired` and shared `UiRequiredNames` report missing/wrong behavior-bearing elements in Editor/development builds without aborting binding. Art/layout edits must preserve those names and element types.

## Connects to
- `lobby-ui`: semantic chrome/item icons, district cards, family/tint placeholders and fonts style pages.
- `match-ui`: resources, sheet headers, indicators, emotes and draft stickers resolve through the same theme.
- `world-view`: `DistrictVisualTable` shares district art references with board presentation; UI does not consume district accent color.
- `tooling`: Inspector drawer and setup commands author assets and serialized root wiring through Unity.

## Invariants
Never renumber surviving serialized enum values or reuse retired values. Context defaults to numeric zero/Anywhere for existing entries. First duplicate pair wins, including an empty first entry. Art cannot alter gameplay, element picking or command bindings. Keep environment/player ownership cues distinct from item-family/tint placeholders.

## How it works
Lobby SVG lookup precedes the theme, except within Shop or ShopCard context. Elsewhere lookup tries exact kind/context, then the same kind/Anywhere, then generated glyph. An empty first exact entry permits Anywhere fallback but never a later exact duplicate; empty first Anywhere falls through to glyph. Original-color sprites use white tint; other sprites retain glyph tint. Images fit centered with letterboxing. Unknown kinds get a placeholder; None draws nothing.

`UIArtThemeIconEntryDrawer` offers grouped Icon and context-filtered Where menus. Anywhere is first; single-location icons display Anywhere while preserving valid serialized overrides. Changing kind resets an incompatible context. Retired choices stay visible only for existing rows. Validation warns on duplicates/unused pairs; Editor/development runtime warns once for unlisted active pairs after attachment/UXML attributes.

`UnityDefaultRuntimeTheme.tss` imports Unity defaults, then `Tokens.uss`, then `Components.uss`. Lobby, HUD and Draft have separate PanelSettings with shared theme/reference frame. Layout-imported screen USS overrides theme rules regardless of specificity; put shared palette/role tokens and `ui-` components in the theme, screen-specific rules in page sheets.

Lobby-scoped tokens use Lilita One for display text and Fredoka SemiBold for body/meta, with 0.6px tracking. Shared root and Shop font tokens retain Kalam. Text outlines are zero except the stamped 34px page title. Icons use authored images or generated glyphs. `Assets/UI/Editor/UIToolkitLobbySetup`, `UIToolkitHUDSetup`, `UIToolkitDraftSetup` create roots/PanelSettings and connect layouts via Tools menus; the theme drawer authors icon rows, not scene YAML.

World UI keeps crisp screen-plane text, integer-pixel anchors, constant screen offsets and distance scale clamped to 0.85 to 1. Lobby content insets are 12px; panels clear the top band and pill nav.

`Components.uss` supplies the lobby panel language: outer/inner/light edges, 2px outlines with 7px panel bottoms or 5px pressable bottoms and flat gold nav. Ledgers have 56px rows and 40px avatars. Live cards are 48px with a 46×58 VS.  item sheets place dots inside the preview, 52px skins and ten flexible histogram cells below it.

## Decisions
- Resolve lobby icons from a shared Phosphor mapping; retain semantic theme overrides elsewhere.
- Preserve procedural glyph/gauge fallbacks rather than require a complete art pack to run.
- Share a runtime theme with separate panels rather than duplicate tokens or couple panel tuning.
- Scope Lilita One/Fredoka panel styling to the lobby so Shop and match styling remain independent.

## Pressure points
Icon theme entries are empty. `StagePaperArt.Bake` generates tinted, badged buildings/villagers via `PaperRasterizer`; `LobbyPaperArt` caches them for Workshop tiles and sheet previews, monograms only as fallback. District references coexist with legacy definition/sticker fields. Caching requires a play-session restart. Theme precedence varies. Gauge skins and Addressables are absent.

## Change recipes
- Add an icon: append its stable kind, provide fallback drawing and usage locations/labels, set context at call sites and extend coverage tests; assign sprites through the Inspector.
- Reskin a screen: edit theme entries/tokens/page USS, preserve names/types/picking and test missing-art fallback. Use connected Unity for asset/root wiring.

## Tests
`NodeWar.Lobby.Tests` guards icon usage/labels/context compatibility, lookup order and pure art math; View/Lobby tests cover gauge math and required XML contracts. Compile-check covers drawers/runtime painters. Inspector menus, tint, clipping, font weight and device rendering need manual Unity checks.
