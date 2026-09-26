---
type: Guide
title: Architectural decision map
description: The major choices, their costs, and the systems they make possible.
tags: [guide, decisions, architecture]
status: draft
sources:
  - id: simulation
    resource: Assets/Scripts/Game/Simulation/MatchFactory.cs
    title: MatchFactory.cs
  - id: replay
    resource: Assets/Scripts/MatchLog/MatchReplay.cs
    title: MatchReplay.cs
  - id: services
    resource: Assets/Scripts/Backend/BackendServices.cs
    title: BackendServices.cs
  - id: architecture
    resource: docs/architecture.md
    title: architecture.md
---

# Architectural decision map

[Atlas](README.md) · Previous: [Engineering workflow](engineering-workflow.md) · Next: [Future extensions](future-extensions.md)

These are the choices a new engineer should understand before changing the structure. The reasons summarize source comments and existing architecture docs; the future consequences are engineering implications, not promises about a roadmap. This is not a claim to recover the historical intent behind every line of code.

## Rules and execution

| Decision | Benefit | Cost or constraint | Connected systems |
|---|---|---|---|
| Represent the battlefield as graph data | Navigation and occupation can run without physics or rendering | World visuals must map back to node/edge identity | [World](world-and-rules.md), [presentation](world-presentation.md) |
| Keep simulation pure, integer-based, and ordered | Peers, tests, and replay execute the same rules | New rules must respect determinism and stable ties | [Simulation](simulation.md), [networking](networking.md) |
| Exchange commands through lockstep | Small input stream and identical local execution | Input delay, stalls, and strict compatibility | [Networking](networking.md), [replay](logs-and-replay.md) |
| Centralize normal tick-zero setup in MatchFactory | Honest live and replay matches start identically | Factory changes carry gameplay/compatibility risk | [Lifecycle](match-lifecycle.md), [replay](logs-and-replay.md) |
| Retain static rule configuration for now | Small existing engine API and shared setup | One configured match at a time per process | [Simulation](simulation.md), [workflow](engineering-workflow.md) |
| Fingerprint state and separately identify rules/content | Detect divergence and reject incompatible peers early | Hash coverage and version discipline need human review | [Simulation](simulation.md), [networking](networking.md) |

The static-configuration constraint is particularly easy to miss. Extracting the factory improved reuse without making the engine reentrant. Moving route multipliers into state would close one source of hidden configuration, but static balance references would remain. A concurrency redesign has to cover every implicit input.

## Interaction and presentation

| Decision | Benefit | Cost or constraint | Connected systems |
|---|---|---|---|
| Resolve pointer gestures centrally | One physical action gets one target and interpretation path | UI interception and eligibility must stay coordinated | [Input](input-and-interface.md) |
| Use commands for humans, sheets, and bots | One enforcement path and reproducible intent | Immediate UI feedback cannot assume command success | [Input](input-and-interface.md), [simulation](simulation.md) |
| Put frame timing and interpolation outside rules | Smooth motion independent of simulation frequency | What is drawn can lag or interpolate authoritative state | [Presentation](world-presentation.md) |
| Emit output-only tick moments beside state | Effects share a reliable trigger without affecting gameplay | Subscribers must respect the reused log's lifetime | [Simulation](simulation.md), [presentation](world-presentation.md) |
| Migrate UI through parallel presenters | Each phase can move independently with fallbacks | Three trees remain compiled and scene switches matter | [Input](input-and-interface.md), [lifecycle](match-lifecycle.md) |
| Centralize POV and camera-relative sorting | Players and spectators share one spatial model | A POV change must update several visual consumers together | [Presentation](world-presentation.md) |
| Allocate outline IDs for active silhouettes | Work and cleanup track actual visible outlines | Registry lifetimes and style priority need tests | [Presentation](world-presentation.md) |

The recurring theme is ownership: one gesture resolver, one command enforcement path, one starting-board implementation, one camera-side model. Sharing ownership is more valuable than merely sharing helper functions. A new helper that leaves two independent policy decisions intact does not solve the underlying drift risk.

## Persistence and future authority

| Decision | Benefit | Cost or constraint | Connected systems |
|---|---|---|---|
| Use service interfaces with local fakes | UI can be developed and tested offline | Fakes do not establish production behavior or permissions | [Backend](backend-and-progression.md) |
| Share backend records/rules across client and module | Avoid duplicate schema and equip-policy implementations | Public field names become persistent compatibility contracts | [Backend](backend-and-progression.md) |
| Separate hidden rating, visible rank, and owned items | Skill estimation, reward pacing, and entitlement can evolve independently | Settlement must update the correct records consistently | [Backend](backend-and-progression.md) |
| Keep catalog IDs stable | Old inventory and logs remain interpretable | Content retirement/migration must be explicit | [Backend](backend-and-progression.md), [replay](logs-and-replay.md) |
| Carry gameplay eras but exclude skins from simulation | Appearance can change without altering outcomes | Transport/logs still need cosmetic data for reconstruction | [World](world-and-rules.md), [backend](backend-and-progression.md) |
| Validate matches by replay as a foundation | Reuse the engine without hosting every live tick | Consistency alone cannot authenticate a competitive result | [Replay](logs-and-replay.md), [future](future-extensions.md) |
| Link shared sources into plain .NET projects | Tests and Cloud Code use the actual game rules | Compatible APIs, language level, and assembly seams matter | [Workflow](engineering-workflow.md) |

## Questions to ask before a structural change

1. Which owner makes this decision today: match rules, interaction, presentation, persistent account rules, or orchestration?
2. Is the proposed field an input to an outcome, an output describing an event, or a local display preference?
3. Can the live match and replay obtain the same value without Unity, wall time, or an undocumented singleton?
4. Is this a new semantic rule, a content change, a wire change, or only a visual change?
5. What would an old save, old log, mismatched peer, or refused server operation do?

Answers to those questions determine the implementation path more reliably than the folder where a similar-looking class happens to live.

## Vocabulary across boundaries

| Term | Meaning here |
|---|---|
| Node | Gameplay graph vertex carrying district/ownership/occupation state |
| District | The capability a node provides |
| Suit | A villager role/equipment type, distinct from cosmetic appearance |
| Era | Gameplay variant selected per suit/district type |
| Skin | Cosmetic variant, outside simulation state |
| Loadout | Selected types plus equipment information carried into a match |
| Draft | Pre-match placement process that helps define the starting board |
| Tick | One deterministic rules step; nominally one tenth of a second live |
| Tick event | Output-only moment for presentation, not a persistent command log |
| Match log | Versioned setup, applied commands, checkpoints, and result evidence |
| Referee | Headless consistency checker over a supplied log |
| Authority | Must be qualified: live gameplay, protected account writes, or trusted result settlement |

Use these terms consistently in new docs and PRs. Calling skins “eras,” Relay “the game server,” or a referee verdict “proof of a legitimate ranked win” hides exactly the boundaries that future work needs to preserve.

## Source landmarks

[MatchFactory.cs](../../Assets/Scripts/Game/Simulation/MatchFactory.cs) | [MatchReplay.cs](../../Assets/Scripts/MatchLog/MatchReplay.cs) | [BackendServices.cs](../../Assets/Scripts/Backend/BackendServices.cs) | [architecture.md](../../docs/architecture.md)
