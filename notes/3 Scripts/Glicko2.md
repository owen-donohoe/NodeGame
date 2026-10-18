---
shell: 3
up: "[[Rating and rank]]"
concepts:
  - "[[Rating systems]]"
tags: [shell/3, realm/backend, system/progression, script]
file: dotnet/NodeWar.Progression/Glicko2.cs
sources:
  - dotnet/NodeWar.Progression/Glicko2.cs
verified: ce9fb6bf
---

# Glicko2

> [!question] The question
> What does a published rating algorithm look like when translated into 170 lines of C#?

**Where you are:** [[Home]] › [[Backend]] › [[Progression]] › [[Rating and rank]] › **Glicko2** (script)

**Open:** [Glicko2.cs in VS Code](vscode://file/C:/Dev/NodeGame/dotnet/NodeWar.Progression/Glicko2.cs)

## What it is

A `static class` implementing Mark Glickman's Glicko-2 paper ([[Rating systems]]): `Update`,
`UpdateSingleMatch`, `DecayForInactivity`, `ExpectedScore`, plus the private steps `G` and `UpdatedVolatility`.

## Read it beside the paper

| Code | Paper step |
|---|---|
| `Scale = 173.7178` | Convert between the rating scale (1500-ish) and the internal μ/φ scale: `μ = (R − 1500) / 173.7178` |
| `G(phi)` | The g(φ) function: reduces an opponent's impact when their RD is high |
| `ExpectedScore(a, b)` | E: the expected win probability, using the *opponent's* uncertainty |
| `UpdatedVolatility` | Step 5: the new σ, solved iteratively |
| `DecayForInactivity` | RD grows as `√(φ² + periods·σ²)` |
| `ClampRD` | Keeps RD between 30 and 350 (this project's config) |

## Things to notice

- **Illinois bracketing** for volatility: "Illinois bracketing avoids the initial-guess sensitivity of
  Newton iteration." It's a robust root-finding method, which the paper itself recommends.
- **Iteration limits** (`IterationLimit = 1000`) that throw `ArithmeticException` instead of looping forever on pathological input.
- **"All opponents must be snapshots from the start of the rating period"**: the same principle as [[MatchSettlement]].
- **Doubles are fine here** ([[Why floats break determinism]]): computed once, on the server.

## Check yourself

> [!question]- Why does `ExpectedScore` use the *opponent's* RD rather than your own?
> If your opponent's rating is uncertain, the result tells you less about your own skill, so the
> expected score is pulled towards 50% and the update is damped.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
