---
system: world-view
summary: Read-only world presentation: board and unit art, movement, routes, status, outlines and event feedback.
sources:
  - Assets/Scripts/Game/Debug/BoardVisualsBench.cs
  - Assets/Scripts/Game/Debug/BoardVisualsBenchDraft.cs
  - Assets/Scripts/Game/View/
  - Assets/Scripts/Game/UI/Selection/
  - Assets/Scripts/Game/UI/Worldspace/
  - Assets/Resources/UI/PaintCoverage.shader
  - Assets/Resources/Water/
  - Assets/Resources/Board/NodeWarPlinth.shader
  - Assets/Art/Board/Plinth.fbx
  - tools/blender/make_plinth.py
verified: 33ad3190
---

# World view

## Purpose
Render deterministic state as a readable board without changing results. Share movement, preview and picking geometry across delay and rollback.

## Interface
`NodeView.Initialize(state, id, claimThreshold)` binds a node; `TriggerHighlight(Color)` acknowledges destinations. `NodePresentation` manages district art/animation. `VillagerPositioner.Initialize(nodeID, nodeScale)`, `SetSpriteWidth` and named work positions supply standing slots; `VillagerLayoutMath` computes crowd spacing from measured sprite width.

`VillagerView.Initialize(state, id)` binds a unit; setters install `ITickProvider`, positioners, `PathCurveSettings` and `BreachCueSettings`. `TryGetSpriteBounds(out Bounds)` excludes health UI/particles/breach lines. Per-tick observations feed `VillagerFlashMath` and `VillagerSwingMath`; presentation interpolation uses `TickAlpha` and the latched physical leg duration. `VillagerHealthMath`/`VillagerTiltMath` supply pure bar/tilt maths; `VillagerTouchTarget` adds constant-screen-size picking.

`PathCurve.Build(waypoints, cornerRadius, cornerSegments)` makes a corner-rounded curve; `PositionOnLeg`, `AppendRemainder` and `CopyTo` expose it. `PathCurveSettings` is shared by walking and drawing. `RouteCurveCache` retains geometry. `OrderPresentation` derives travel/interrupted/gate intent from state; `MovementPathRenderer.Initialize` binds local seat/route settings and `SetTickProvider` binds interpolation.

`PendingOrderView.OnMoveIssued(targetNode, IDs)` draws queued intent before simulation applies it; `BeginHover`, `SetHoverNode`, `EndHover` draw drag previews. `PendingOrderTracker.Issue`, `Cancel`, `Clear`, `Sweep` track `{ villagerID, targetNode, issuedAt, routeNodes }` until acceptance/invalidation/timeout. `OpponentRouteSettings`, `OpponentRouteGate` and `RouteReveal` limit visible enemy-route geometry.

`IndicatorDirector(state, tickProvider, settings, opponentSettings, localPlayer)` exposes `Active` indicators and `TryGetAnchor`; bind positioners/transforms and `Dispose` subscriptions. `IndicatorPlacement` is pure placement maths; HUD `IndicatorLayer` renders/clicks them. `ScreenShakeDirector` subscribes to ticks, invokes a supplied shake callback and is disposable.

`DistrictVisual`/`DistrictVisualTable` share district prefabs, icons and metadata. `BoardArtPlacer` applies offsets/rotation/scale; `DistrictFallback`/`DistrictFallbackArt` supply missing-art glyphs/bridges. `TerrainPresentation.Describe(board, placed, hasPick, pick, draftComplete)` uses resolved terrain; its four-argument overload describes drafting. `BoardTerrainView.ShowDraft`/`ShowMatch` outline unresolved shoals and enable lazy plinths only on Land. `PlinthFactory` loads `Board/DistrictPlinth` from Resources or builds a cube with the plinth shader. `PlinthLayout` supplies top-centre positions; `PlinthMotion` supplies clamped cubic rise maths. Resolved terrain feeds `WaterSurfaceView.SetTerrain`; `ShoreField.Bake` and `WaterMeshBuilder.Build` supply the field and shared-edge mesh.

Outline is assembly `NodeWar.View.Outline`: `OutlineGroup.Attach(root, renderers)` supplies one silhouette; `SetIntent`, `SetTint` and renderer rebuild/exclusion methods update it. `OutlineDriver` alone decides intents from hover, selection, inspected node and presence. `OutlineRegistry` exposes active groups to URP mask/composite passes and allocates IDs only while style is non-None. `OutlineStyle` numeric order is priority; ownership tint is separate. `OutlineRendererFeature` must be installed in both Mobile_Renderer and PC_Renderer assets.

`SelectionLasso.Initialize(gestureSource, camera)` consumes authoritative screen-space paint samples and radius; it draws a coverage mask, not a world-space selection authority.

## Connects to
- `simulation`: reads state/leg clocks and optional tick events; no state writes.
- `match-flow`: constructs views/shared settings, binds providers, handles population growth/rollback and disposes directors.
- `input`: provides transforms/positioners for picking; consumes gestures and camera `POVChanged`; paint membership and rendering share samples.
- `commands`: `MoveIssued` starts pending feedback; applied state eventually replaces it.
- `match-ui`: IndicatorLayer and status UI consume view models; breach/accessibility settings and camera focus are shared.
- `ui-art`: district icons/palettes come from shared visual/theme assets with independent board/UI fallbacks.
- `board`: terrain and node mappings drive code-built ground and placements.
- `tooling`: editor builders author visual assets; pure view and Outline suites guard maths/lifecycle.
- `draft`: camera/terrain and grid-to-world placement support presenter ghosts and placed pieces.
- `netcode`: tick interpolation/cues and rollback cleanup follow confirmed/speculative driver events.

## Invariants
Rendering and effects never mutate simulation. Tick logs are reused output; retain copies only. Walking/routes share curve shape and leg boundaries. Interpolation anchors to `movePath[movePathIndex]`, including reversal, never a guessed sprite position. Hidden enemy route tails cannot influence visible grouping. Outline priority and ID lifecycle remain deterministic presentation contracts.

## How it works
State supplies conditions; `TickSimulated` supplies per-tick moments. HP decreases flash; fighting cooldown resets swing the Billboard child. Reduced motion disables swing.

Sprites default to 20-degree pitch, while canvases/floor items stay untilted. Camera POV changes billboard facing and horizontal CustomAxis; node/draft art uses SortingGroups, internal order `height * 256 + depth rank`, ties by index. Recompute on POV/art changes rather than every frame; ground moves to its own group.

Damaged living units show camera-facing health bars above sprite bounds; next-breacher ring is separate. Node indicators show claim/district-health, bank pips and Core breach progress. District health hides at full; bank pips sit outside district sorting at 402/403, Core bars at 400/401. Ocean/open Lake have no collider; legal draft cells use fill plus outline.

Local routes merge and fade with age; interrupted/Pier-gated intent is amber/dashed. Enemy routes default to two revealed legs within three hops of local territory, with screen/distance fades; there is no general fog of war.

Indicator events/conditions use debounce and grace. HUD placement uses central-view hysteresis, dock/sheet exclusion, edge clamping and merged counts. Screen shake reacts to breach/capture/game-over, with strongest-wins camera composition. Paint uses an opaque union mask composited once, preventing overlap darkening; its shader loads from Resources.

Water owns blended fields, meshes and floor; reduced motion snaps the 0.4-second blend. Shore R encodes distance, G static swash phase, B lake palette and A body mix. Textures hold swell height/derivatives and foam/noise. Shared-fetch noise breaks depth/body bands; solid shore foam precedes phased swash. Noise/Gerstner waves share damping, clearance and fades; crests only lighten. Far submesh uses fetch-free vertex fresnel matching the seam. PC SSAO requires DepthNormals.

`WaterFeatures` defines budgets and Off/Noise/Gerstner 2/4 modes. Low compiles optional paths out. `WaterQuality.Apply()` refreshes water, plinths, floor, probes and `GraphicsTierRenderer`'s runtime URP clone; HDR stays authored for bloom. `RefreshAll` preserves blends. Tuning copies migrate shipped defaults without asset writes. Water sorts at -10; High/Ultra probes capture terrain and settled plinths.

Plinth tops stay at GroundY (-0.12), extending down 0.6 cell; water sits 0.45 lower. Initial land snaps; later Land rises over 0.35 unscaled seconds; reduced motion snaps. Shared `Board/DistrictPlinth` owns colours; renderer blocks hold water height/fade. Runtime resources are disposed.

`BoardVisualsBench` replays drafting/shoals without simulation. Its scrolling F1 panel groups runtime sliders/reset, presets/Custom, replay/pause and frame time.

## Decisions
- Runtime-added feedback components avoid prefab edits for outlines, rings and touch targets.
- Pure maths separates testable geometry from Unity rendering.

## Pressure points
Per-tick state diffs miss damage cancelled by healing. Multiple visual systems need coherent POV, bounds and rollback invalidation. Outline renderer installation is asset wiring beyond compilation. PathCurve uses shared scratch state; callers must copy retained geometry. Paint fill cost and icon overlap require device checks.

## Change recipes
- Add visuals: read state/events, bind/unbind provider, support restored/shrunk arrays and reduced motion.
- Change movement drawing: update shared curve/leg maths and reversal tests together.
- Add district art: update the visual table, fallback descriptors and independent UI icon path through editor tools.
- Add outlines: use driver intents and test priority, ID release/reuse and both renderer assets.

## Tests
`NodeWar.View.Tests` covers presentation maths, shore encoding/body blends, mesh seams/counts, texture bakes, wave clearance/density, swell derivatives, swash phase and presets. `Assets/Tests/EditMode/Outline/` covers outline lifecycle/priority. Compile-check covers Unity components; Editor/device checks verify shaders, sorting, materials, paint coverage and wiring.
