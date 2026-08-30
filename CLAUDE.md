# Node War — Claude Instructions

## Project
1v1 real-time strategy game, Unity 6, lockstep P2P networking.
Namespace: NodeWar. Project: NodeGame/. Scripts: Assets/Scripts/
Current phase: Phase A — two-scene architecture, initialization order fix.
Target: playable shareable build in approximately 3 months.

## Folder Map
Assets/Scripts/
  Lobby/       — menus, player profile, loadout and node data definitions
  Game/
    Core/      — GameManager (state machine), DraftManager, TickRunner, CameraController
    Simulation/— ALL game logic. Pure C#. No UnityEngine. See simulation rules below.
    Network/   — LockstepRunner, NetworkManager, InputSerializer, DraftSerializer
    Input/     — CommandSystem, SelectionSystem, InputBuffer, BotPlayer
    UI/        — HUDManager, panels, menus. Reads SimulationState. Never writes it.
    View/      — NodeView, VillagerView, presentation. Reads SimulationState. Never writes it.

Full architecture: docs/architecture.md

## Simulation Boundary — Non-Negotiable
Both peers run identical simulation from identical inputs.
Any violation causes a desync. These rules are not negotiable.

- No UnityEngine references anywhere in Simulation/
- Integer-only math — no float, no double
- No System.DateTime, Time.deltaTime, or any frame/wall-clock API
- No UnityEngine.Random — use seeded RNG stored in SimulationState only
- Collections: arrays or List<T> only — no Dictionary or HashSet iteration
- All sort operations must have total-order comparators with ID tiebreakers
- Tick order is canonical and must not be reordered:
    movement → combat → claiming → production → healing → respawns → win-check
- View and UI never write to SimulationState. All state changes go through:
    GameCommand → InputBuffer → CommandProcessor → SimulateTick

Detailed rules: docs/simulation-rules.md

## C# Conventions
- Keep [SerializeField] variables in the same file as their MonoBehaviour (single-file principle)
- Do not split a class into separate files unless there is a strong reason
- New SimulationState fields must always be added to SimulationStateHasher
- Follow SpawnBonusVillagers pattern when adding array-backed state that needs view objects

## Key Entry Points
GameSimulation.SimulateTick()  — deterministic tick loop, runs at 10Hz
CommandProcessor               — applies GameCommands to SimulationState
GameManager                    — match lifecycle state machine (PreDraft → Playing → GameOver)
LockstepRunner / TickRunner    — tick timing; shared via ITickProvider
SimulationStateHasher          — desync detection fingerprint (checked every 50 ticks)

## How to Work
- Read relevant files before proposing anything
- For anything touching Simulation/: use plan mode first
- Prefer the smallest change that satisfies the goal
- Do not modify .unity scenes, prefabs, or .meta files without explicit instruction
- When uncertain about intent: ask once, clearly, then proceed
- After Simulation/ changes: flag which tests should be run

## Subagent Usage
- Do not spawn a subagent (Agent tool) for a task that would use 2 or fewer of them.
  Do the work directly instead.
- Subagent overhead (fresh context, full tool-schema load, re-reading files) usually
  costs more time and tokens than just doing a small task inline.
- Reserve subagents for work that genuinely parallelizes across 3+ independent
  angles, or where isolating large tool output from the main context matters.
- This does not apply to managed skills (e.g. /code-review, /security-review) that
  spawn their own internal agents as part of a fixed procedure -- that fan-out is
  not something this file can override.

## Skills
Skills are markdown files in .claude/skills/.
To use a skill, read the file and follow its procedure.
There is no formal invocation syntax.
Reference skills by file path:
  .claude/skills/session-summary.md
  .claude/skills/determinism-guard.md
  .claude/skills/write-sim-test.md
  .claude/skills/cs-review.md
  .claude/skills/phase-plan.md

## Response Style
Do not end responses with trailing additions:
"one more thing", "also worth noting", "additionally I should mention",
"one last note", "before I finish", or similar patterns.
If something is important, say it once in the right place.
A Stop hook enforces this. Treat it as strict.

## Feature Implementation
docs/adding-a-feature.md — checklist for any new feature
