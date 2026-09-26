---
type: Guide
title: Future extensions and architectural constraints
description: What the existing seams enable, what remains unbuilt, and the evidence needed to extend them.
tags: [guide, direction, architecture]
status: draft
sources:
  - id: direction
    resource: docs/direction.md
    title: direction.md
  - id: referee
    resource: dotnet/NodeWarCloud/NodeWarCloud/Referee.cs
    title: Referee.cs
  - id: replay
    resource: Assets/Scripts/MatchLog/MatchReplay.cs
    title: MatchReplay.cs
  - id: visual
    resource: Assets/Scripts/Game/View/DistrictVisual.cs
    title: DistrictVisual.cs
  - id: shake
    resource: Assets/Scripts/Game/View/ScreenShakeDirector.cs
    title: ScreenShakeDirector.cs
---

# Future extensions and architectural constraints

[Atlas](README.md) · Previous: [Decision map](decision-map.md)

This chapter explains architectural opportunities and missing pieces. It does not assign priorities, dates, or task status; Notion owns the roadmap. “Can reuse the engine” means a boundary makes an extension possible, not that its product experience has already been built.

## Trusted results before ranked settlement

The [referee](logs-and-replay.md) can reconstruct a supplied log and check its consistency. Persistent rating and inventory records exist, as do rating calculations. The missing connection is a trustworthy, duplicate-safe process that decides which real match happened and may affect which players.

That process needs a trusted match/session identity, authenticated participants, accepted loadouts, and evidence about command origin. It must distinguish a legitimate resubmission from a second reward and define how disconnects or conflicting reports are handled. Ownership checks on a Workshop request are not proof that a later uploaded era table was authorized.

Replay verification can then become one step in settlement, alongside identity, eligibility, and persistence. Tests of the finished reporting flow should include duplicate submissions, partial failure, conflicting claims, changed inventory, and concurrent requests. This is an architectural implication of the current trust boundary, not a claim that those endpoints exist today.

## Matchmaking connects policy to operations

The pure ticket rules already encode compatible versions, rating windows, and a hard arena-gap limit. A real queue still needs authenticated ticket creation, storage/lifetime, cancellation, pairing coordination, connection assignment, and UI states for waiting or failure.

Keep the policy separate from that infrastructure. It is useful to test “which opponent is eligible?” without a network, while testing “can two requests claim the same ticket?” requires the actual coordination model. The arena limit exists because progression can change power in ways hidden rating alone cannot capture. Future tuning should preserve that design reason even if rating-window numbers change.

## Replay viewing is another consumer

The headless replay runner provides reconstruction; the client can eventually render it through existing node/villager views and spectator POV. Playback controls, seeking, history lookup, and version handling remain separate concerns.

Snapshots for seeking must include all outcome-relevant state and configuration, restore IDs/timers/paths correctly, and be checked against continued execution from tick zero. A screenshot or a set of transforms is not a simulation snapshot. Older balance exports help reconstruct old content, but different simulation versions may need retained engines or an explicit unsupported-version experience.

Cosmetic skin data already travels in logs, creating a place for a viewer to recover appearance without making skins deterministic rule inputs. Camera position and local selection can remain viewer choices.

## Balance experiments and learning agents

`MatchFactory`, `GameCommand`, and `SimulateTick` provide the core reset/step shape of a training or batch-evaluation environment. `MatchReplay` demonstrates that the engine runs outside Unity. A proper environment still needs observation encoding, legal action handling, episode termination, opponent control, and a process/protocol wrapper.

The current scripted `BotPlayer` and `InputBuffer` are already plain C# without Unity imports. Their live integration is in the client tick runner, not a Gymnasium environment or standalone training service. A headless host can reuse this policy, but must supply its tick cadence, command draining, and episode lifecycle through the ordinary command path.

Eras must appear in observations because identical district names can imply different statistics. Reward design must distinguish actual progress toward winning from behavior that merely farms a proxy. Deterministic replay provides a useful debugging tool: a suspect episode can be reproduced before changing the learning algorithm.

Start by proving generated matches reset, advance, and terminate correctly under controlled inputs. Batch evaluation can then answer opening advantage, duration, stalemate, and district-use questions even before a learned bot is effective. With the current static balance configuration, isolate concurrent environments in separate processes or redesign the implicit configuration first.

## Art, feedback, and platform support

The event log, shake director, indicator system, and district visual asset types are already useful foundations. Broader audio/effect coordination and runtime adoption of the district art table remain work. Migrate one consumer at a time so an empty new asset slot does not silently override a populated older mapping.

Desktop/mobile share a command vocabulary and much pointer interpretation. Remaining work concerns layout, screen-space affordances, remaining direct device reads, and validation on actual surfaces. Compact and wide layouts should change where actions appear, not introduce a separate mobile rule implementation.

More feedback can follow the same outward event path as screen shake. A coordination layer may impose effect budgets or accessibility preferences. Presentation speed and reduced motion must never alter simulation steps. The existing separation lets art and feel improve without making every animation change a replay compatibility event.

## Moving live authority later

A server-hosted live simulation could reuse the deterministic library, but the client/server interaction model still has to be designed. Input admission, latency, disconnect semantics, state delivery, deployment, and operating cost are not supplied by the referee endpoint. Moving a current static-configured engine into a multi-match host also requires deliberate isolation.

The present hybrid direction buys a lower-cost intermediate step: clients play the match, server-owned services protect progression, and replay can validate computation. Its unresolved authenticity problem should remain explicit until reporting closes it. That clarity avoids building the next layer on a stronger guarantee than the implementation provides.

## How to tell an extension is ready

A sound extension keeps its ownership clear and supplies evidence at the boundary it changes. A new effect needs visual and lifetime checks. A new command needs deterministic execution and serialization evidence. A result service needs authenticity and idempotency evidence. A map generator needs live/replay tick-zero equivalence. A training runner needs reproducible episodes and process isolation.

The most durable future investment is preserving these separations while finishing concrete product flows. They are what let the same small rules engine support a richer game, more reliable competition, and useful engineering tools.

## Source landmarks

[direction.md](../../docs/direction.md) | [Referee.cs](../../dotnet/NodeWarCloud/NodeWarCloud/Referee.cs) | [MatchReplay.cs](../../Assets/Scripts/MatchLog/MatchReplay.cs) | [DistrictVisual.cs](../../Assets/Scripts/Game/View/DistrictVisual.cs) | [ScreenShakeDirector.cs](../../Assets/Scripts/Game/View/ScreenShakeDirector.cs)
