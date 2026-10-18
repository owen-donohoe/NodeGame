---
shell: 3
up: "[[Rating and rank]]"
tags: [shell/3, realm/backend, system/progression, script]
file: dotnet/NodeWar.Progression/RankPoints.cs
sources:
  - dotnet/NodeWar.Progression/RankPoints.cs
verified: ce9fb6bf
---

# RankPoints

> [!question] The question
> The +/− RR number on the end card: where is it computed, in 51 lines?

**Where you are:** [[Home]] › [[Backend]] › [[Progression]] › [[Rating and rank]] › **RankPoints** (script)

**Open:** [RankPoints.cs in VS Code](vscode://file/C:/Dev/NodeGame/dotnet/NodeWar.Progression/RankPoints.cs)

## What it is

`RankConfig` ("These numbers are placeholders pending design") plus `RRDelta(currentRR, hiddenAfterMatch, won, cfg)`.
The formula and worked examples are in [[Rating and rank]].

## Read `RRDelta`

1. **Validate the config:** six checks, each with `ArgumentOutOfRangeException`. Bad tuning fails loudly instead of producing nonsense RR.
2. `implied = ImpliedRatingAtZero + RatingPerRR * max(0, RR)`
3. `correction = Divergence * (hiddenR − implied)`
4. `magnitude = BaseGain ± correction`, clamped to `[MinGain, MaxGain]`
5. `Math.Round(magnitude, MidpointRounding.AwayFromZero)`. C#'s default is *banker's rounding*
   (2.5 rounds to 2), so naming the mode makes 2.5 round to 3 as players expect.

## Check yourself

> [!question]- Why is the returned value negative for every loss, even though RR itself never drops below 0?
> The *award* is reported honestly (−8). The zero floor is applied when adding it to the stored
> total (`Arenas.ApplyRRDelta`), so a player at RR 3 who loses shows −8 but ends at 0.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
