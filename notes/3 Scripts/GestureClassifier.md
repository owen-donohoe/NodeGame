---
shell: 3
up: "[[Gesture recognition]]"
concepts:
  - "[[State machines]]"
tags: [shell/3, realm/match, system/input, script]
file: Assets/Scripts/Game/Input/GestureClassifier.cs
sources:
  - Assets/Scripts/Game/Input/GestureClassifier.cs
verified: ce9fb6bf
---

# GestureClassifier

> [!question] The question
> How is a gesture state machine written in plain C# so that a test can replay "finger
> down, wait 0.31 s, drag 6 mm, release" and get exactly the same answer every time?

**Where you are:** [[Home]] › [[Match]] › [[Input]] › [[Gesture recognition]] › **GestureClassifier** (script)

**Open:** [GestureClassifier.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Input/GestureClassifier.cs)

## What it is

A `sealed class`, about 590 lines, with **no device, scene or clock of its own**. It takes
`PointerSample`s (time, position in mm, pointer ID, button, phase, over-UI flag, target)
and publishes `GestureEvent`s through one C# event, `Published`. `PointerGestureSource` is
its Unity adapter.

> [!info] The file in one breath
> Enums and structs at the top (the *vocabulary*) · constructor and `Configure` ·
> `ProcessFrame` (entry) → `Continue` (finger held) → `End` (finger released) ·
> `EmitTap` (single, double, node chains) · `AdvanceTime` · `Cancel`.

## Read it in this order

### 1. The vocabulary (top 70 lines)

`GestureState` (12 states), `PointerPhase`, `GestureEventKind` (23 kinds), `GestureTargetClass`,
and the `readonly struct`s `GesturePoint`, `PointerSample`, `GestureEvent`. **Read these
first.** The rest of the file is transitions between them.

`readonly struct` means immutable value types: cheap to pass, impossible to change by accident.

### 2. The fields (about 40)

One `State` plus remembered facts: `downPos`, `downTime`, `pathLength`, `tapExceededSlop`,
`lastTap…`, `nodeTap…`. Each is a fact the machine needs in order to decide later.

### 3. `Continue`: the `Pending` case is the heart

Read the `case GestureState.Pending:` block slowly. It's a priority list of transitions:
1. tap-then-drag → **OneHandZoom** (if enabled)
2. press on a *selected* villager and moved > slop → **Ordering**
3. moved > slop (or held, then moved) → **Panning** or **Lassoing**
4. held, still → armed **lasso/pan**, or **HoldFired**

Each transition does `Emit(GestureEventKind.Cancelled)` *first*. That tells listeners
"forget any tap you were expecting" before the new gesture begins.

### 4. `End` → `EmitTap`

A release while still `Pending` and inside the slop and time limits is a tap. `EmitTap` then
branches: node taps join a **chain** (count 1, 2, 3, resolved later or immediately on the third),
while ground and villager taps fire now and may be **upgraded** to a double tap by the next press.

### 5. `AdvanceTime(now)`

Called every frame, even with no pointer. That's how a pending node tap "expires" into a
single or double after 0.22 s: *time passing* is an input too. Because time is a parameter,
tests control it exactly.

## Why the design is testable

```csharp
var c = new GestureClassifier(bindings, tapSlopMm: 4, holdTime: 0.3f, ...);
c.Published += e => events.Add(e.Kind);
c.ProcessFrame(new[] { Sample(t: 0.00f, x: 0, y: 0, PointerPhase.Began) });
c.ProcessFrame(new[] { Sample(t: 0.10f, x: 6, y: 0, PointerPhase.Held) });   // 6 mm > slop
// events now: PointerDown, Cancelled, PanBegin, PanUpdate
```

(Illustrative. The real tests in `dotnet/NodeWar.View.Tests` use synthetic traces like this.)

## Check yourself

> [!question]- Why is `GesturePoint.Distance` allowed to use `float` and `Math.Sqrt`?
> The classifier is outside `Simulation/`. Its result is *which command to create*, and the
> command is integers. Floats on this side can't desync anything.

> [!question]- What is the job of `Emit(GestureEventKind.Cancelled)` right before `PanBegin`?
> To tell subscribers that the press they saw (`PointerDown`) won't become a tap, so any
> touch-down feedback or pending tap state can be abandoned before the pan starts.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
