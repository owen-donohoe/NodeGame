# Lobby panel-language review

Implemented against `panel-language.html`; routes, paging, stage code and Shop
assets stay unchanged. This is a source audit; rendering remains unverified.

| Surface | Text sizes (px) | Edges (px) | Spacing (px) |
|---|---|---|---|
| Chrome / Home | purse 16, name/RR 22, caption/nav 13, BATTLE 32, mode 15 | outer 2, panel bottom 7, crate bottom 5, flat nav 2 | 4/8/12/24 |
| Social | header/name 17, Live name 15, Live RR 12, meta 13, VS 19, history action 15/RR 22 | card 2/7, player 2/5, VS 2/6, separators 1 | 4/8/12; 56px ledger rows, 76px Live rows |
| Guild search / bubble | stamped title 34, field 16, heading/name 17, body 16, meta 13, actions 15 | title stroke 2, panels 2/7, field 2/5, sliders 2/4, separators 1 | 4/8/12/16/32 |
| Guild home / members / announcements / details / chat / report | headings 17/24, names 17, body 16, meta 13, actions 15, RR 22 | panels 2/7, buttons 2/5, ledger separators 1 | 4/8/12/16/24/32 |
| Workshop | heading 17, tile/filter 15, preset 22, meta 13, monogram 24 | card 2/7, tiles 2/5, teal selection 2, switch 2/5 and light selected lip 3 | 4/8/12/32; 8px grid gutter |
| Item sheet | title 24, skins 17, stats 13/15, description/actions 15 | sheet 2/7, preview 2, skins/buttons 2/5, histogram 2; red loss cells dashed | 4/8/12; 52px skins, ten flexible cells |
| Own profile / player panel | title 24, headings/names 17, numbers 22, meta 13, actions 15 | outer 2/7, flat stat/loadout fills, inner ladder 2 | 4/8/12/16 |
| Menu / notifications / mode / multiplayer / claims | headings 17/24, body 16, meta 13, actions 15 | panel 2/7, pressables 2/5 | 4/8/12/16 |
| Searching / crate / party wait | heading 24, RR 22, body 16, actions 15 | panels 2/7, buttons 2/5, flat party slots | 4/8/12/16/24/32 |
| Settings / account / controls / match history | heading 24, body 16, secondary 14, caption 13, actions 15 | panel 2/7, pressables 2/5, flat grouped rows with hairlines | 4/8/12/16/24/32 |

The explicitly requested Live RR size (12), 46x58 VS, lip widths and radii are
intentional exceptions to the generic size/grid guidance. Text line boxes use
at least 1.2 times the size where constrained; flexible labels use font metrics.
Tracking is a single 0.6px value. Lobby body is Fredoka SemiBold; display text is
Lilita One. Only the stamped page title uses a text outline.

Source checks caught and fixed Label-reset specificity, cramped name line boxes,
oversized skin tiles, nested profile mini outlines, mode cards using the panel
edge, and legacy PaperCard shadows. Dots moved inside the 152px preview; skins
shrink from 80px to 52px; Live cards shrink from 64px to 48px while VS grows from
44px to 58px; ledger avatars grow from 34px to 40px. Existing skin data remains
bound; inert locked previews fill missing catalogue slots without invented IDs.

Phosphor Bold uses white SVGs, tinted by USS. The installed native SVG importer
is configured to produce UI Toolkit Vector Images by an Editor postprocessor. The upstream MIT licence accompanies the
icons. Unity must generate all new asset/folder `.meta` files.

Validation: the full .NET solution passes (4,636 reported passing cases); its
explicit Sweep and export-compatibility cases do not run. Unity-side compile
check, document budget, changed-UXML parsing and diff whitespace checks pass.
No Shop, Stage, Simulation, scene, prefab or `.meta` files are modified.

`unity` is unavailable on PATH. The lead must open this worktree, import assets,
and capture Home, Social, Guild search with its bubble, Workshop and the item
sheet at 390x844. Verify clipping, font metrics, tint, safe areas, panel fit,
slider thumbs, pressed states and the ten-cell loss strokes. Route behaviour,
scene wiring and appearance cannot be confirmed by the compile check.

Shop keeps its previous font/shape tokens and procedural art, as requested.
The match retains its shared root fonts and procedural renderer. Legacy unused
layout/style assets are retained for their existing bindings; active lobby
surfaces receive the shared recipe.
