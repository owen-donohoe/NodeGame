---
shell: concept
used-by:
  - "[[Simulation]]"
  - "[[The tick]]"
  - "[[GameSimulation]]"
tags: [concept, topic/determinism]
---

# Why floats break determinism

> [!question] The question
> A `float` is a precisely specified IEEE-754 number. So why can the same calculation give
> different answers on two machines, and why does Node War ban floats from the simulation entirely?

**Kind:** concept. True outside Node War. Used by [[Simulation]], [[The tick]] and [[GameSimulation]].

## The problem

IEEE-754 says what *one* operation must return. It doesn't say which operations your
program actually performs, in what order, or at what precision. Compilers, CPUs and math
libraries all get a say:

| Source of difference | Example |
|---|---|
| **Order of operations** | Float addition isn't associative: `(1e20f + -1e20f) + 1f == 1f`, but `1e20f + (-1e20f + 1f) == 0f`. A compiler that reorders a sum changes the answer |
| **Fused multiply-add** | `a * b + c` computed as one operation (one rounding) instead of two. Some CPUs and compilers do this, others don't |
| **Intermediate precision** | Old x87 FPUs kept 80-bit intermediates. Whether a value was rounded to 32 bits depended on register pressure |
| **Math libraries** | `Math.Sin`, `Math.Pow` and `Math.Sqrt` can differ in the last bit between runtimes (Mono, IL2CPP, .NET on the server) and platforms (ARM phone vs x86 PC) |
| **JIT vs AOT** | The same C# compiled differently on different builds |

A one-bit difference in tick 1 becomes a different villager position by tick 500. The two
peers then disagree about who won.

## The idea

**Use integers, whose behaviour is exact and identical everywhere, and choose units so that
integers are natural: ticks instead of seconds, percent instead of fractions, health points
instead of 0–1.**

## In Node War

- **Time is in ticks.** A leg takes `linkWeight × moveSpeedTicks` ticks, not 1.7 seconds.
- **Positions aren't stored as coordinates at all.** A villager is "on leg A→B, 6 of 15 ticks along". Coordinates exist only in the view.
- **Rounding is a decision.** Ceiling division `(a + b − 1) / b` where rounding up matters;
  multiply before divide (`covered * to / from`) to keep precision.
- **Overflow is guarded.** Multiply into `long`, then clamp back to `int`.
- **Floats are welcome elsewhere:** input gestures, interpolation, curves, UI and camera.
  The ratings in `NodeWar.Progression` use `double` too, and that's safe because they're
  computed once, on the server, not replayed by two peers.

## Why not the alternatives

| Option | Gain | Pay |
|---|---|---|
| **Plain integers in domain units** (chosen) | Simple, fast, exact | You think about units and rounding up front |
| Fixed-point (an integer scaled by 1,000 or 65,536) | Feels like decimals, still exact | Easy to overflow on multiply, and you need your own sqrt and trig |
| Software floats (emulated IEEE) | Real floats, deterministic | Much slower, and it's a big dependency |
| "Careful" floats (strict compiler flags, same build everywhere) | Familiar | Fragile across platforms and runtimes. One library update can break it |

## Traps

- **"Just one float"** for a percentage. The rule has no exceptions inside `Simulation/`.
- **`Math.Sqrt` on an int** returns a `double`. Even "integer" maths can sneak floats in.
- **Assuming integer division rounds.** It truncates towards zero: `-7 / 2 == -3`.
- **Silent overflow.** C# integers wrap by default.

## Check yourself

> [!question]- Is `int damage = (int)(baseDamage * 1.5f);` deterministic?
> Probably on most machines, but not guaranteed: the multiply happens in floating point.
> Write `baseDamage * 3 / 2` and choose how it rounds.

> [!question]- Why may ratings use `double` when the simulation may not?
> The rating is computed in one place (the server) and the *result* is stored. Nobody needs
> to re-derive it bit for bit on another machine. Determinism matters only for computations
> that are *repeated* independently and must agree.

## Learn more

- Glenn Fiedler, [Floating Point Determinism](https://gafferongames.com/post/floating_point_determinism/)
- Bruce Dawson, [Floating-Point Determinism](https://randomascii.wordpress.com/2013/07/16/floating-point-determinism/): a deep, practical tour
- The repo's own contract: `docs/contracts/determinism.md`

## My understanding

<!-- Yours. AI never edits below this line. -->
