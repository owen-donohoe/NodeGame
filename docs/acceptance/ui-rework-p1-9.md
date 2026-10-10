# UI rework P1.9 Editor acceptance — 2026-10-10

Checkout: `feat/loadout-rework`, starting at `9d79c5e7`, Unity 6000.6.1f1.
Primary Game view: **1080×2340 (19.5:9 portrait)**. Supplementary stress view:
720×1280, including temporary 24 UI-unit top/bottom safe insets. The shorter
view is supplementary, not the target phone proportions.

Runtime checks used the existing Lobby scene, local in-memory backend fakes,
UI Toolkit submit events for buttons and injected mouse pointer events for
SkinBand. These do not establish physical mouse/touch or device acceptance.

| Check | Result | Evidence |
|---|---|---|
| 1. Workshop portrait layout | Pass after fix | Persistent header showed Build and 0 RR. Five district/three suit positions; Farm, Mine, Village (display name Recruit) and Warrior disabled/dimmed; 2+2 chosen capacity. Districts/Suits tabs and Unlocked/Not unlocked sections present. Guardian histogram has zero lines; district facts, description and locked variant era notice render. Info width/lower width = 0.5395753. Art containers clip; scrollable content stays inside the lower frame. |
| 2. Preview and chosen slots | Pass via UI event dispatch | Guardian preview left the serialized deck unchanged. Full Use left it unchanged and showed “Loadout full. Tap a slot to clear it”. Disabled Warrior submit left it unchanged. Clearing the first chosen suit yielded `,suit_scout`; Use yielded `suit_guardian,suit_scout`. Arena-2 Farm also filled the first empty district position. |
| 3. Variants | Pass | Guardian sheet remained inside the lower frame, below both rows and persistent header. At 1080×2340, lower bounds were (7.94,357.14,374.11,391.81), sheet bounds (7.94,362.92,374.11,386.03). Back and tab switch both cleared the panel host. District Variants was hidden. Variant 2 displayed its era notice. |
| 4. Skins | Pass for automated mouse path | Suit and Storehouse sheets opened locally. Back closed them; Buy another box showed “Coming soon”. A single skin remained centred at index 0 after drag. Two-skin, memory-only fixtures in both families moved to position 1.25 on a 70-unit drag, targeted 1 and settled at 1; adjacent taps targeted 2 and wrapped to 0. Use equipped supported default skins through local inventory and the returned snapshot. Physical touch could not be verified in this session. |
| 5. Home completeness | Pass via UI event dispatch | Four visible chosen chips at arena 1. Empty slot produced “Fill your loadout” and Battle opacity 0.5. Battle submit selected Workshop; scene remained Lobby. |
| 6. Arena 2 | Pass for local UI | Changed only the in-memory rank, then reopened Workshop. Five district/three suit positions were all enabled; arrays resized to 5+3. Farm and Warrior could fill chosen positions. No backend writes. |
| 7. Theme and lobby warnings | Pass | Editor-loaded UIArtTheme had zero icon entries, hence no context 11 or retired tree glyph entries. Visited Shop, Home, Workshop, Social, Profile, Settings and Match history. Console since cursor 80945 returned no new entries, including no missing-USS or unknown-UXML warnings; compilationFailed=false and consoleErrors=0. Existing importer/development-directive warnings were outside this pass. |

## Fix and validation

Before the fix, Workshop actions ended at y=837.06 and overlapped the floating
navigation (y=764.83–819.00). `Workshop.uss` now reserves 96px below its content,
matching the lobby's existing navigation clearance. Final action bottom is
y=748.94, with local sheets above the navigation as well. No C# or serialized
scene/asset changes were needed.

- `dotnet test dotnet/NodeWar.sln`: 3,948 passed, zero failed. The initial sandbox
  invocation failed on Windows SDK-directory access; the user-environment rerun
  passed. Explicit Sweep/export cases were not run.
- `powershell -ExecutionPolicy Bypass -File scripts/compile-check.ps1`: passed,
  260 sources against 330 Unity references.

## Outstanding P1.9 manual coverage

Physical mouse/touch and phone safe-area behavior remain unverified: this pass
used dispatched/injected Editor events and synthetic insets. No actual matches
were launched, so low/high/mixed-arena launch, draft/base-pool suppression and
no-Warrior/short-deck match behavior remain Editor acceptance work. No old-save
restart cycle was performed (startup logged a migration). Live busy/retry/stale
inventory responses, every ownership/gate combination and locked-skin gestures
were not comprehensively exercised; local synchronous fakes cannot establish
those asynchronous cases. Phase-wide visual acceptance is therefore incomplete.

## Cleanup

Exited Play mode. Restored the profile snapshot taken at the start of this
runtime pass, the prior local-fakes preference and the original Game view
selection (D-E portrait, 540×1200). Removed the P1.9 custom resolutions.
`AssetDatabase.DeleteAsset("Assets/Temp/shots")` returned true; neither that
capture directory nor `Temp/shots` remains. No screenshots are committed.
Unrelated Settings/font/ProjectSettings changes and untracked `.claude/plans/`
were left untouched. No push, merge, branch switch or child agents.
