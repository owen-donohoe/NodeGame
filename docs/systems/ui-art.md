---
system: ui-art
summary: Resolves shared UI icons, district thumbnails and procedural gauge styling from an Inspector-authored theme.
sources:
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
verified: 013527f0
---

# UI art

## Purpose
Art can replace placeholder glyphs and district thumbnails without changing screen behavior. One semantic theme serves lobby, draft and match surfaces while procedural defaults keep missing art usable.

## Interface
Create a `NodeWar > UI Art Theme` ScriptableObject named `UIArtTheme` at `Assets/UI/Resources/UIArtTheme.asset`. `UIArt.Theme` loads `Resources.Load<UIArtTheme>("UIArtTheme")` once per play session; restart Play Mode after reference/entry changes.

`UIArtTheme.icons` is an ordered `IconEntry[]` of `{ LobbyIconKind kind, LobbyIconContext context, Sprite sprite, bool keepOriginalColours }`. `TryIcon(kind, context, out entry)` uses pure `UIArtLookup.SpriteIndex(entries, kind, context, kindOf, contextOf, hasSprite)` to resolve overrides. `districtVisuals` references `DistrictVisualTable`; its per-type `DistrictVisual.icon` supplies node-sheet art and `StickerOrIcon` supplies draft art before fallback mappings. Workshop uses ink monograms on paper rather than those sprites.

`LobbyIcon : VisualElement` has independently mutable `Kind` and `Context`, constructors `LobbyIcon()` and `LobbyIcon(kind, context = Anywhere)`, and `SetResourceTint(Color)`. UXML accepts `kind="Tv" context="PageHeader"`. It resolves themed sprites or cached `VectorImage` glyphs drawn by seeded `InkPen`/`PaperMath` with Painter2D in a 24×24 design box.

District and suit icons accept Profile context for loadout minis.

`LobbyIconUsage.All`, `For(kind)`, `IsUsed(kind, context)`, `IsValidThemePair` and `WhereChoices(kind)` supply semantic groups, labels and valid locations to Inspector/runtime validation. Active kinds distinguish resources, navigation, indicators, emotes and cosmetics; use this table rather than the obsolete generic icon inventory.

Contexts are Anywhere, TopBar, PageHeader, NavBar, HeadsUpDisplay, NodeSheet, InlineText, EmotePicker, EmoteBubble, OffScreenIndicator, ShopCard, Workshop, Profile, Home. Context value 11 is reserved for the retired tree location and never matches. Retired kind values remain reserved, including SuitTreeAvailable/Owned/Equipped/Locked; their drawers are not active UI.

`ItemFamily.ForDistrict/NoteFor/ClassFor/SortKey` maps district IDs to Round/Square/Triangle/Combat grouping and fallback shapes; unknown districts use Square. `ItemTint.IndexFor/ClassFor/MonogramFor` provides stable eight-color item placeholders independent of list order. These are presentation identities, not ownership or catalog authority.

Gauge USS properties: `--res-thickness`, `--res-stop-0` through `--res-stop-5` customize rings and sheet readouts; stops override compatible `--ring-critical` through `--ring-rich`. `--dial-thickness`, `--dial-track-color`, `--dial-fill-color` customize `ProgressDial`. `UiArtMath.Geometry(value, fallback, min, max)` bounds procedural dimensions. `.sheet__res-readout-tint` opacity affects only the background, with expanded-bar overrides under `.sheet__res-readout--bar`.

`MkCard : VisualElement` supplies `mk-card`; `MkPlayerRow.SetPlayer(handle, status)` supplies avatar/name/status and `Trailing`. `MkSheet` owns bottom sheets; `SetScrollable` keeps fixed panels separate from internal lists. `MkPanel` owns centered modal cards with scrim, close and router Back. `Components.uss` exposes the mockup `mk-` classes; `Tokens.uss` keeps raw `--pal-*` values separate from meaning tokens. Lobby pages are transparent; cards carry content.

`PaperCard : VisualElement` (`[UxmlElement]`) draws parchment, cached grain, offset shadows and seeded wobbling ink. `Seed`/`RotatePaper` control identity/rotation; `--paper-card-*` properties read meaning tokens. Geometry/style events rebuild it; presses drop the shadow. `PaperButton : Button` preserves binding contracts, drawing paper behind its caption. `PaperMath` is UnityEngine-free.

`UiRequired` and shared `UiRequiredNames` report missing/wrong behavior-bearing elements in Editor/development builds without aborting binding. Art/layout edits must preserve those names and element types.

## Connects to
- `lobby-ui`: semantic chrome/item icons, district cards, family/tint placeholders and fonts style pages.
- `match-ui`: resources, sheet headers, indicators, emotes and draft stickers resolve through the same theme.
- `world-view`: `DistrictVisualTable` shares district art references with board presentation; UI does not consume district accent color.
- `tooling`: Inspector drawer and setup commands author assets and serialized root wiring through Unity.

## Invariants
Never renumber surviving serialized enum values or reuse retired values. Context defaults to numeric zero/Anywhere for existing entries. First duplicate pair wins, including an empty first entry. Art cannot alter gameplay, element picking or command bindings. Keep environment/player ownership cues distinct from item-family/tint placeholders.

## How it works
Lookup tries exact kind/context, then the same kind/Anywhere, then generated glyph. An empty first exact entry permits Anywhere fallback but never a later exact duplicate; empty first Anywhere falls through to glyph. Original-color sprites use white tint; other sprites retain glyph tint. Images fit centered with letterboxing. Unknown kinds get a placeholder; None intentionally draws nothing.

`UIArtThemeIconEntryDrawer` offers grouped Icon and context-filtered Where menus. Anywhere is first; single-location icons display Anywhere while preserving valid serialized overrides. Changing kind resets an incompatible context. Retired choices stay visible only for existing rows. Validation warns on duplicates/unused pairs; Editor/development runtime warns once for unlisted active pairs after attachment/UXML attributes.

`UnityDefaultRuntimeTheme.tss` imports Unity defaults, then `Tokens.uss`, then `Components.uss`. Lobby, HUD and Draft have separate PanelSettings with shared theme/reference frame. Layout-imported screen USS overrides theme rules regardless of specificity; put shared palette/role tokens and `ui-` components in the theme, screen-specific rules in page sheets.

`--font-display` resolves Lilita One for titles, numbers, buttons and tabs; `--font-label` resolves Kalam Bold for labels/chips/row names; `--font-body` resolves Kalam Regular. Type tokens are body 16, secondary 14, caption 13, title 24 and number 22. Outline tokens are zero; real faces carry weight. Kalam line boxes allow its tall ascenders. Icons use glyph drawing rather than fallback-font characters. `Assets/UI/Editor/UIToolkitLobbySetup`, `UIToolkitHUDSetup`, `UIToolkitDraftSetup` create roots/PanelSettings and connect layouts via Tools menus; the theme drawer authors icon rows, not scene YAML.

## Decisions
- Resolve semantic kind/context overrides rather than replace sprites independently at every call site.
- Preserve procedural glyph/gauge fallbacks rather than require a complete art pack to run.
- Share a runtime theme with separate panels rather than duplicate tokens or couple panel tuning.
- Use Lilita One and Kalam, parchment/umber meaning tokens and outlined mockup components across the lobby.

## Pressure points
The current icon theme has no assigned sprite entries; much UI remains procedural/monogram art. Workshop still needs district-sprite consumption. District references coexist with legacy definition/sticker fields. Caching requires a play-session restart. Theme precedence can surprise CSS users. Gauge sprite skins, theme switching and Addressables are absent. Import sizing and device aliasing need visual assessment, not historical asset counts.

## Change recipes
- Add an icon: append its stable kind, provide fallback drawing and usage locations/labels, set context at call sites and extend coverage tests; assign sprites through the Inspector.
- Reskin a screen: edit theme entries/tokens/page USS, preserve names/types/picking and test missing-art fallback. Use connected Unity for asset/root wiring.

## Tests
`NodeWar.Lobby.Tests` guards icon usage/labels/context compatibility, lookup order and pure art math; View/Lobby tests cover gauge math and required XML contracts. Compile-check covers drawers/runtime painters. Inspector menus, tint, clipping, font weight and device rendering need manual Unity checks.
