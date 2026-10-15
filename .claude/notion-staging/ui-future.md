# UI and art future work

Unbuilt work and outstanding verification from the retiring UI/input rework and art inventories. Lobby rework, paint/node gestures, controls, count/clear, villager readability and the semantic art-theme pipeline are implemented and excluded. Dropped ownership-edge/dither and automatic camera-centering proposals stay excluded. Historical art lists are reconciled with the active roster; they do not authorize restoring retired gameplay.

## Perimeter claim-bar geometry and rendering
Replace the floating half-and-half bar with one shared rounded-square footprint stroke, beginning bottom-middle and traversing bottom-right, top-right, top-left, bottom-left and back. Reveal progress in a shader through per-node MaterialPropertyBlock values; avoid per-node meshes and per-frame allocations.
Dependencies: view-only NodeClaimBar replacement, reusable mesh/texture/material and connected Editor wiring; simulation timing and hash baselines remain unchanged.

## Direction-to-edge mapping decision
Define how diagonal links, Pier links and multiple neighbours in one direction map onto four perimeter edges. Nearest-side mapping is the proposed starting point, but aggregation and ties need an explicit rule before building the speed remap.
Dependencies: actual board/link geometry and presentation design; this is the rework's unresolved pre-build question.

## View-only claim-speed remap
Map absolute claim progress to perimeter distance with normalized edge weights: owned-neighbour edges accelerate the visible point, enemy edges emphasize resistance, and Fortress effects temporarily alter weights. Preserve monotonicity, exact endpoints and total claim time; do not describe extra visual slowdown as simulation behavior.
Dependencies: edge-mapping decision and pure tests for equal weights, zero weights, single-edge cases, normalization and endpoints.

## Claim-point sparks
Add short-lived sparks at the leading point of actively claimed, on-screen perimeters. Pool the ParticleSystems so the effect does not scale into per-frame allocation or off-screen cost.
Dependencies: perimeter geometry/point evaluation, effect art and visibility/pool lifecycle.

## Fortress pulse presentation
Draw a wave from a healthy Fortress across linked nodes and lower the visible point's edge weight during the crossing. Explain resistance visually while leaving the authoritative claiming rate untouched.
Dependencies: perimeter remap, read-only Fortress/link state, timing and reusable effect assets.

## Footprint district-health readout
Move district health from the old claim bar to a short arc or mini-bar on the footprint. Retain StructurePresentation.HealthSegment maths and damaged-only eligibility rather than create a second health rule.
Dependencies: perimeter replacement, chosen footprint composition and phone readability checks.

## Lobby and match device acceptance
Verify Workshop grant locks, grouping, preview/Use, unlocked/locked styling, Variants/Skins sheets, looping band and header/navigation clearance at phone widths. Check low and high loadout shapes rather than the plan's obsolete fixed Home-chip count.
Dependencies: connected Editor/device, representative ranks/inventory, current authored roots and both orientations; landed code does not establish visual acceptance.

## Input, safe-area and readability acceptance
Verify physical paint radius/coverage across zoom and viewpoints, node double/triple timing, hold stillness, count/X cancellation, controls-side mirroring, all UI scales/notches and sheet overlap. Check phone-sized villager spacing/ownership, damage flashes, swings and damaged-only health bars; assess paint-mask resize/cancel and fill/allocation cost.
Dependencies: physical devices and separate recorded evidence; these are acceptance checks, not requests to rebuild landed gestures/readability.

## Real unlock progression and purchases
Replace the local all-content-unlocked bypass and display-only CanUnlock seams with a deliberate progression/economy flow. Decide acquisition/costs without mistaking existing tree eligibility and server equipment validation for an implemented purchase system.
Dependencies: progression/variant policy, authoritative inventory and backend economy; coordinate with backend future-work staging.

## Real box action
Replace Workshop's “Buy another box” toast with a real authorized action only when boxes/currency exist. The visible placeholder remains until the backend can validate and grant outcomes.
Dependencies: backend shop/box stage, server randomness and retry-safe purchases; do not implement local grants for UI convenience.

## District illustration and visual-table coverage
Author board art, flat icons and draft stickers for the current roster: Core, Farm, Mine, Village, Forge, Barracks, Storehouse, Pier, Workshop, Town, Infirmary and Fortress. Prioritize the manifest's still-active missing Forge/Village art and audit new district gaps; create/update DistrictVisual assets through the Editor instead of assuming the historical fourteen-entry list is complete.
Dependencies: current DistrictRoster, DistrictVisualTable, world/flat-art style and per-surface fallback checks. Watchtower, Camp, Arsenal, Shrine, Sanctuary, Rampart and Market are historical entries, not new active-art commissions.

## Workshop district-art consumption
Connect Workshop cards, previews and local sheets to the shared district visual source while retaining family/monogram fallback and visible locks. Current MakeArt still draws shapes/letters, so filling DistrictVisual.icon alone does not illustrate Workshop.
Dependencies: district icons, UIArt theme/table, current preview/selection model and greyscale/navy styling.

## Base villager and suit illustrations
Draw a billboard-flat base villager and five readable combat suits: Warrior, Guardian, Scout, Berserker, Medic; supply matching Workshop/detail art and actual side-variant appearances where defined. Draw active production-role accessories Farmer, Miner and Smelter, and account for the current Minion fallback rather than restore retired jobs.
Dependencies: current variant/suit vocabulary, world ownership outlines/tints and UI preview sizing. Merchant, Acolyte and Watcher belong to historical production lists and need no active-art task without a gameplay decision.

## Villager state and Home character art
Develop pose/accessory reads for Idle, Moving, Working, Claiming, Fighting and Dead so state is not communicated only through color. Create the Home villager figure and any loadout-dependent appearance; existing USS body/hat/Mouth is placeholder presentation, not an appearance editor.
Dependencies: final base/suit art, animation/state presentation, four-view readability and ownership remaining unmistakable.

## Resource, indicator and emote glyph art
Populate theme sprites for Food, Materials, Metal, MagicResource; Swords, Sleep, Respawn, Pointer, IndicatorEffect, IndicatorThreatToCore, IndicatorThreatToTerritory, IndicatorNodeUnderAttack, IndicatorNodeContested; EmoteHappy, Frown, Angry, EmoteWhiteFlag and Speaker. Draw Pointer facing right for runtime rotation and test picker/bubble/board-edge contexts separately.
Dependencies: LobbyIconUsage semantic locations and UIArtTheme assignments; procedural glyphs and context resolution already exist, so no new icon system is needed.

## Lobby and cosmetic glyph art
Draw Tools, Gear, Mouth, Tv, Back, District, Suit, Lock, Close, NavBarHome, NavBarSocial, NavBarShop, DailyBox, VictoryBox, ShopBundle, GoldLeaf, ProfileYouAreHere, CosmeticTinRoof, CosmeticPaperBanner and CosmeticStrawHat. Audit any separate trophy/box-meter symbols against actual consumers instead of commissioning the old generic five-tab set.
Dependencies: current LobbyIconUsage table, tint/original-color choices and Inspector theme population. Retired Shop/Spark/Smile/Envelope/Flag/Hat/Diamond/Pip/Alert/Capture and suit-tree-state kinds are excluded.

## Paper frames, textures and screen surfaces
Author reusable nine-slice paper/card frames, cost-chip frames, sheet paper/district headers, emote bubbles, breach-wall treatment, HUD settings/recentre glyphs and play-mode cards. Supply draft placed-piece frames and grid treatment, lobby navigation chrome, Profile identity/trophy/box-meter art and Social avatars; gauges stay procedural unless a separate design explicitly changes that decision.
Dependencies: current layouts and required names/picking contracts, UIArt slots/USS and device sizing. Decide how the brief's four paper-era textures map to current progression before commissioning the set.

## Shop parcel, shelf and stall art
Create box/paper-parcel art for the opening takeover and supporting shelf/stall/offer art. Review existing UI_Shop slices first; their autogenerated names do not identify which drawings can be reused.
Dependencies: atlas identification, current Shop layout and backend box scope; polished art must not imply purchases already work.

## Identify and name atlas slices
Inspect AssetsSpriteSheet, HousesSpriteSheet and UI_Shop in the Sprite Editor, identify unused figures/buildings/props and assign semantic slice names while preserving serialized references. Resolve the ambiguous Shrine building layers only if maintaining that historical asset; do not guess an illustration's identity from dimensions.
Dependencies: human visual inspection and Editor import workflow; the historical sixty-unmapped count is a starting inventory, not a verified current count.

## Historical Market missing-sprite references
Assess whether to repair or retire the two missing-GUID sprite references in Market_Node Variant.prefab. The references remain in compiled legacy content, but Market is not an active district; maintenance should not silently revive it.
Dependencies: decision to preserve historical assets, locating/replacing the missing source and connected Editor prefab editing.

## Opaque Feather environment art
Author non-interactable scenery as opaque/alpha-clipped Feather meshes, keeping touched villagers/items as billboard sprites. Target a shared vertex-color material and roughly two thousand triangles per node; test sprite/mesh intersection and occlusion before building a full pack.
Dependencies: art-direction approval, importer/decimation pipeline and representative camera views. Ownership cannot become mesh-only; add tint multiplication only if an owned mesh actually requires it.

## Line-art rendering and import pass
Evaluate enabling atlas mipmaps and mobile 2x MSAA against current 0.8 render scale/off MSAA, then reassess bloom/depth-of-field with real line art. Author UI near twice displayed size with explicit sizing and semantic slices; confirm PPU/mipmap choices visually rather than inherit world-atlas settings.
Dependencies: representative art, connected Editor import/render configuration and mobile profiling; do not hand-edit meta/scene assets.

## Remaining event-effect art and callers
Create claim-flip bursts, death puffs, breach effects and floating-number art with pooled event-driven callers where still absent. Damage/touch/order flashes already exist and must not become duplicate effects; the old manifest's blanket “nothing has a caller” claim is stale.
Dependencies: confirmed/speculative event policy, world-view feel architecture, effect budgets and real art export. Audit existing breach cues before choosing an additional breach treatment.

## Retire the UI source plans and inventories
Move reviewed unfinished items into the authorized Notion workflow and remove retiring documents once cards and durable art tasks retain their useful contracts. Do not preserve obsolete roster counts, scene history or superseded implementation phases as current requirements.
Dependencies: card review, staging review and authorization for external Notion writes/deletion; neither occurs in this documentation wave.
