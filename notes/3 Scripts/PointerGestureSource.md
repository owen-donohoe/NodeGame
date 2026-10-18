---
shell: 3
up: "[[Gesture recognition]]"
concepts:
  - "[[Events and observers]]"
tags: [shell/3, realm/match, system/input, script]
file: Assets/Scripts/Game/Input/PointerGestureSource.cs
sources:
  - Assets/Scripts/Game/Input/PointerGestureSource.cs
verified: ce9fb6bf
---

# PointerGestureSource

> [!question] The question
> The pure [[GestureClassifier]] knows nothing about Unity. What is the Unity half that
> feeds it real fingers and turns its answers into events?

**Where you are:** [[Home]] › [[Match]] › [[Input]] › [[Gesture recognition]] › **PointerGestureSource** (script)

**Open:** [PointerGestureSource.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Input/PointerGestureSource.cs)

## What it is

"The single pointer reader for gameplay selection and move orders." It's a MonoBehaviour that, each frame:
1. reads the Unity Input System (mouse and touch share one path: `Touchscreen` derives from `Pointer`);
2. resolves **what is under the press, once**: UI (EventSystem raycast), a villager (physics raycast on the Villagers layer), or a node;
3. converts pixels to millimetres (`ScreenMetrics`, with a 160-DPI fallback) and builds `PointerSample`s;
4. calls `classifier.ProcessFrame(samples)` and `AdvanceTime(now)`;
5. re-publishes the classifier's `GestureEvent`s as typed C# events: `OnTap`, `OnHold`, `OnOrderDrop`, `OnLassoComplete`, `OnZoomUpdate`…

## Read it in this order

1. **The header comment:** the history of the three-way raycast race this class ended.
2. **The events block:** each event's doc comment is a mini-spec. Note `OnZoomUpdate` publishes
   *scale from the pinch start*, not per-frame deltas, because "an accumulating delta drifts".
3. **`SetVillagerFilter` / `SetSelectedFilter`:** the input layer *asks* `SelectionSystem` what's
   selectable instead of knowing the rule. An enemy villager isn't a tap target, so the
   raycast "falls through" to the node underneath.
4. **The per-frame update:** sample building, target resolution, classifier calls.

## Design lesson

**Adapter.** All the Unity-specific mess (devices, layers, raycasts, DPI) lives here. All the
decision logic lives in the pure classifier. That split is [[Pure core, Unity shell]], and
it's why gesture behaviour can be tested with synthetic traces.

## Check yourself

> [!question]- Why does `OnPointerDown` fire before the gesture is known?
> Feedback (the touch-down flash) must start instantly, before anyone knows whether this
> press becomes a tap, a pan or a lasso. If it turns into something else, `OnGestureCancelled` follows.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
