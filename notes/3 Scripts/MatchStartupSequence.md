---
shell: 3
up: "[[Match startup sequence]]"
tags: [shell/3, realm/match, system/match-flow, script]
file: Assets/Scripts/Game/Core/MatchStartupSequence.cs
sources:
  - Assets/Scripts/Game/Core/MatchStartupSequence.cs
  - dotnet/NodeWar.View.Tests/MatchStartupSequenceTests.cs
verified: 20bb7952
---

# MatchStartupSequence

> [!question] The question
> How do all startup paths guarantee that the match starts ticking once?

**Where you are:** [[Home]] › [[Match flow]] › [[Match startup sequence]] › **MatchStartupSequence**

## What it is

A plain C# startup coordinator. [[GameManager]] supplies the unpause callback and keeps all Unity objects, serialized fields and animations. The sequence owns the phase and a flag that makes completing twice harmless.

## Read it in this order

1. `StartDraft` and `CompleteDraft` move through Drafting and PostDraft.
2. `BeginPostDraftTransition` enters Countdown, invokes presentation, and completes immediately if no event is expected.
3. `SkipDraftAndInitialize` enters Playing for initialization, invokes the board setup and optional animation-only wave, then completes. The driver stays paused during that callback.
4. `CompleteTransition` enters Playing and invokes unpause once, even if completion arrives again.

## What the tests prove

Eight View.Tests cases exercise draft transition/fallback, both skip-draft variants, paused phases and repeated completion. Successful paths advance real simulation ticks through a fake pause gate. They cannot prove Unity event wiring or animation behavior; those require Play mode.

## My understanding

<!-- Yours. AI never edits below this line. -->
