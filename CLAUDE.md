# Node War — Claude Instructions

1v1 real-time strategy game on a node graph. Unity 6, namespace `NodeWar`,
lockstep P2P networking migrating to a server-authoritative hybrid.

This file is read every turn. It holds only what is true every turn: the
boundary that must never be crossed, and where to find everything else. If a
thing is needed on some turns and not others, it belongs in a document this
file points at. Do not restate those documents here.

## Ownership — nothing is mirrored

| Owns | Where |
|---|---|
| Current behaviour | the code |
| Current architecture | `docs/` |
| Future work | Notion **Phases** |
| Current work | Notion **Tasks** |

Live state is never cached into markdown. Notion content is never copied into
the repo. Notion is written only during `/update`.

## Simulation Boundary — Non-Negotiable

Both peers run identical simulation from identical inputs. Any violation
desyncs. This stays here rather than behind a pointer because it governs the
decision to touch `Simulation/` at all, which happens before any reading.
Full contract in `docs/simulation-rules.md`.

- No UnityEngine references anywhere in `Simulation/`
- Integer-only math — no float, double, decimal
- No `DateTime`, `Time.deltaTime`, or any frame/wall-clock API
- No `UnityEngine.Random`. `SimulationState` holds no RNG today; derive a seed
  from replicated state, and if one is ever stored it lives on
  `SimulationState` and advances only inside `SimulateTick`
- Arrays or `List<T>` only — no Dictionary/HashSet iteration
- All sorts need total-order comparators with ID tiebreakers
- Tick order is canonical, never reordered:
  movement → combat → claiming → production → healing → respawns → win-check
- View and UI never write `SimulationState`. All changes go through:
  `GameCommand` → `InputBuffer` → `CommandProcessor` → `SimulateTick`
- Every new `SimulationState` field must be added to `SimulationStateHasher`
- New `GameCommand` types need a `CommandProcessor` case, and `InputSerializer`
  changes in the same commit

## Key Entry Points

- `GameSimulation.SimulateTick()` — deterministic tick loop, 10Hz
- `CommandProcessor` — applies `GameCommand`s to `SimulationState`
- `GameManager` — match lifecycle (PreDraft → Drafting → PostDraft → Countdown → Playing)
- `LockstepRunner` / `TickRunner` — tick timing, shared via `ITickProvider`
- `SimulationStateHasher` — desync fingerprint, checked every 50 ticks

## Which doc to read for what

Read the file. Do not ask me to summarise it here.

- `docs/architecture.md` — the seven layers, information flow, scene structure,
  the networking model, and where a villager is mid-edge. Read the section you
  need; reading it end to end is how agents spend a fifth of a task's budget
  on a catalogue they had no use for. Its *Where the UI lives* section is
  required before touching any UI: presentation spans three trees and which
  one runs is a scene value.
- `docs/class-map.md` — what each named class is for, layer by layer. For
  looking one up, not for learning the shape of the project.
- `docs/game-model.md` — what the game *is*: districts, suits, resources, the
  win condition. Read this before any gameplay or balance question; the code
  will tell you what happens, not what it is for.
- `docs/simulation-rules.md` — the full determinism contract.
- `docs/direction.md` — where the architecture is going next: the feel layer,
  desktop/mobile as one input vocabulary and two layouts, and the headless
  match environment. Read before starting art, feel, platform or RL work.
- `docs/adding-a-feature.md` — 11-step checklist for any new feature.
- `docs/notion-workspace.md` — Notion identifiers and the Phases/Tasks
  schemas. Needed by `/update` and `/audit`, and by nothing else.
- `docs/index.md` — OKF bundle root. The doc graph, and the full account of
  how the freshness and determinism guards are wired.
- `docs/design-history/` — the v2.1 design document. Historical. Notion is
  authoritative for future work.
- `.claude/rules/{simulation,network,view-ui}.md` — boundary rules per layer.
  Path-scoped via `paths:`, so they load themselves when you touch those
  paths. If all three ever appear in context at session start, the scoping
  has stopped working and they are loading unconditionally — check with
  `/context` after any change to their frontmatter.

## Checking your work

- `dotnet test dotnet/NodeWar.sln` — 115 cases, 43 over `Simulation/` and 72
  over the lobby. Those are the two assemblies that compile without
  UnityEngine. A lobby change has real tests; run them rather than settling
  for a type-check. Details and the receipt rules: `docs/skills/run-dotnet-tests.md`.
- `scripts/compile-check.ps1` — type-checks everything else (HUD, network,
  view, `Assets/UI/`) against the real Unity assemblies. Catches syntax,
  missing usings, wrong API names, broken call sites. Unity need not be
  running and an open editor need not be closed. It cannot see prefab or scene
  wiring, and it is not a test run. Run it after any edit you could not
  otherwise compile.
- `docs/skills/drive-the-editor.md` — if `unity status` reports a ready
  instance, the Unity CLI can drive it and close that prefab-and-scene blind
  spot. Read that file before using it. If `unity status` reports nothing,
  this option does not exist and that is normal — never a precondition for
  finishing work.
- `docs/skills/run-editmode-tests.md` — the suite through Unity, for when the
  question is whether it works *in the Editor*.

## How to Work

- Read relevant files before proposing anything
- For anything touching `Simulation/`: use plan mode first
- Prefer the smallest change that satisfies the goal
- Never hand-edit `.unity` scenes, prefabs, or `.meta` files. Editing that YAML
  corrupts GUIDs and merge state. Change them through a connected editor, and
  only with instruction — the ban is on the text, the permission is on the file
- When uncertain about intent: ask once, clearly, then proceed
- After `Simulation/` changes: flag which tests should be run
- Do not spawn a subagent for work that would use 2 or fewer of them. Managed
  skills that fan out internally (`/code-review`, `/security-review`) are exempt

## C# Conventions

- Keep `[SerializeField]` fields in the same file as their MonoBehaviour
- Do not split a class across files without a strong reason
- Follow `SpawnBonusVillagers` when adding array-backed state needing view objects

## Automatic Guards

Three checks run without being asked, and none replaces the reading they point
at. **SessionStart** injects the list of documents whose sources moved since
they were last verified. **pre-commit** blocks on `scripts/sim-guard.ps1` when
the commit touches `Simulation/`, and reports staleness without blocking.
**CI** (`.github/workflows/determinism.yml`) blocks on the simulation suite and
the pinned hash baselines across Linux and Windows. How all of it is wired:
`docs/index.md`.

Two rules those guards cannot enforce on themselves:

- **Never stamp `verified:` or bump `verified_at_commit` on my behalf.**
  Re-verification is reading a document against its changed sources. It is a
  human act, and reconciling commits is not it.
- **A hash that differs between CI legs is a finding about the simulation, not
  a CI problem. Never re-pin a baseline to make it green.**

`sim-guard.ps1` is only the mechanical subset of `.claude/skills/determinism-guard.md`.
Sort tiebreakers, tick order, hasher registration, command/serializer pairing
and the view boundary are not in it and still need the checklist.

## Commit Convention

`done: <task title>` in a commit message marks that task **Done** on the next
`/update`. Without it, tasks move to **In Progress** at most — a commit that
touches a system is not work that finished.

## Commands and Skills

- `/update` — reconcile Notion against new commits. The only time Notion is written.
- `/audit` — re-check a chapter's plan against the code before it goes Active.

`.claude/skills/*.md` are review procedures with no invocation syntax — read
the file by path and follow it: `determinism-guard.md` · `write-sim-test.md` ·
`cs-review.md` · `session-summary.md` · `phase-plan.md` (largely superseded by
Notion Phases). `docs/skills/*.md` are a different thing: executor
instructions for running something, listed above.

## Response Style

No trailing additions: "one more thing", "also worth noting", "before I finish",
or similar. If something is important, say it once in the right place.
Nothing enforces this mechanically — treat it as strict anyway.
