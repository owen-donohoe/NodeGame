# Remaining direction and controls work

## Coordinate broader feel effects
Add a small FeelDirector consuming tick output to coordinate effects beyond the existing shake director and unit flashes.
Keep per-tick budgets/strongest-wins behavior explicit; use code/prefabs and existing tweening rather than a general event bus or sequencing framework.

## Add game audio
Implement sound playback for match moments through the outward tick-event path, with settings actually controlling audible output.
Connect district-specific sound references to existing DistrictVisual assets when playback needs them; avoid a parallel art lookup system.

## Complete moment feedback
Add floating numbers, claim-flip bursts and death puffs, plus any remaining node/district damage tint needed for readable pressure.
Reuse existing event/state observations; unit touch/order/damage flashes already exist. Extend DistrictVisual effect slots only when concrete effects need them.

## Add presentation-only hitstop
Briefly freeze or slow presentation on selected heavy moments without stopping or retiming simulation/network ticks.
Define recovery, catch-up and reduced-motion behavior so the visual pause cannot alter lockstep or hide subsequent feedback indefinitely.

## Unify camera device input
Move middle-button drag and scroll into PointerGestureSource pan/zoom intents, removing CameraController's parallel direct mouse path.
Preserve current bindings, UI blocking, focus-session behavior and zoom feedback; keyboard shortcuts remain optional additions to the shared vocabulary.

## Add compact and wide layouts
Choose layout--compact/layout--wide from aspect ratio and physical screen width, applying one root class on startup and resize.
Style the same UI tree for bottom/thumb-reachable portrait controls versus wider edge/side-panel layouts; avoid platform-specific layouts.

## Audit desktop presentation and build
Check every screen at desktop widths and resized-window aspects, repairing stretched layouts and confirming equivalence with phone landscape.
Configure/verify a usable Standalone build and launch flow; no shipped desktop-build result is established by the existing headless rig or compile checks.

## Finish desktop cursor feedback
Add cursor changes and audit missing hover highlights for desktop interactions while preserving existing hover outlines and UI states.
Keep this to lightweight feedback; a general tooltip system and controller support are outside this direction.

## Expose a training environment
Add a headless NodeWar.Env process with line-delimited JSON reset/step messages and a Gymnasium wrapper, then connect Stable-Baselines3 PPO.
Reuse MatchFactory, legal random drafts and the existing runner patterns rather than rebuilding the balance rig. Run concurrent environments as separate processes because simulation configuration is static.

## Define observations and training actions
Include per-player suit/district eras and placed district eras so variant strength is observable; begin with movement decisions every ten ticks.
Factor action choice into unit and destination heads with legal-action handling; add other command types only after movement works. Training draft decisions remain a separate later problem.

## Define and validate shaped rewards
Provide dense territory ownership/claim rewards, larger breach rewards and a terminal win reward, with mirrored penalties for opponent gains.
Measure exploitation and tune ratios; a sparse win-only signal is insufficient for an initially random policy to learn useful play.

## Establish training milestones and curriculum
First drive seeded episodes with random actions and verify repeatable reset, step and termination; then verify PPO plumbing and improvement over random play.
Use the shipped scripted bot as the first fixed opponent and track win rate, then past policy checkpoints; attempt full self-play only if useful after those checks.

## Run evidence-led balance passes
Use paired-seat rig runs to measure opening dominance, seat advantage, capped/stalemated matches, district use and match duration before changing tuning.
Extend the scripted policy or scenarios for recruitment, Fortress upgrades, Workshop minions and remote collection before drawing conclusions about mechanics it currently ignores.

## Verify binding persistence across devices
Determine whether PlayerProfile settings sync between device classes and whether that carries an unsuitable phone/desktop binding table.
If shared, separate bindings by device class or exclude them from sync while preserving other preferences and the append-only migration contract.

## Validate physical gesture timing and picking
On real phones, evaluate the 1.5 mm hold-stillness limit, configurable hold duration and 0.22-second node-tap gap, including all binding combinations and cancellation paths.
Measure real unit-slot spacing and nearest-centre picking under crowded/overlapping targets; synthetic traces do not establish comfortable physical interaction.

## Validate paint fidelity and performance
Check brush edges against selected centres across zoom, seat and perspective, including point caps, resizing, cancellation and completion-radius snapshots.
Measure phone allocations and mask fill cost; confirm open-capsule coverage, single-composite overlap and empty-stroke selection preservation visually.

## Validate controls layout on devices
Check physical safe areas/notches, every interface scale, both control sides, selection-bar hit regions and camera-button competition with the node sheet.
Verify the full in-match Controls/Reset list remains reachable and usable without sheet overlap; landed layout code and resource tests do not provide this evidence.
