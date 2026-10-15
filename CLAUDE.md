# Node War — Claude Instructions

1v1 real-time strategy on a node graph (2v2 and free-for-all planned). Unity 6,
namespace `NodeWar`. Deterministic lockstep peer-to-peer matches, with a Unity
Gaming Services backend: Cloud Code referee and settlement, Cloud Save, Matchmaker.

This file is read every turn, so it holds only what every agent needs on every
turn. Everything else is loaded by role and by system. `scripts/doc-budget.ps1`
caps this file's size in CI.

## Where truth lives

| What | Where |
|---|---|
| Behaviour | the code |
| How each system works, why, and how it connects | `docs/systems/<system>.md` (one card per system) |
| How the systems fit together | `docs/map.md` |
| What the game is | `docs/game.md` |
| Future, now, done | Notion **Roadmap** (written by `/update` and `/orchestrate`) |

Nothing is mirrored. Docs say what is true now; history is git and Notion.

## Loading context: load your slice, not the project

- **Planning, splitting or delegating work:** use the `orchestrate` skill. It
  starts from `docs/map.md`, which shows every connector without the code.
- **Working on a system:** `powershell -File scripts/context.ps1 -Systems <a>,<b>`
  prints those cards in full and their neighbours by interface only. That is the
  whole context for the task. If you need something outside it, ask rather than
  explore. `-List` shows every system.
- **Changing a card, or adding a system:** use the `system-docs` skill.

## Simulation Boundary — Non-Negotiable

Both peers run identical simulation from identical inputs; any violation desyncs.
This stays here because it governs the decision to touch `Simulation/` at all.
The full contract is `docs/contracts/determinism.md`.

<!-- boundary:start -->
- No UnityEngine references anywhere in `Simulation/`
- Integer-only math — no float, double, decimal
- No `DateTime`, `Time.deltaTime`, or any frame/wall-clock API
- No `UnityEngine.Random`. Derive a seed from replicated state; a stored RNG lives
  on `SimulationState` and advances only inside `SimulateTick`
- Arrays or `List<T>` only — no Dictionary/HashSet iteration
- All sorts need total-order comparators with ID tiebreakers
- Tick order is canonical, never reordered: movement → combat → claiming →
  production → healing → respawns → win-check (sub-steps in the contract)
- View and UI never write `SimulationState`. All changes go through:
  `GameCommand` → `InputBuffer` → `CommandProcessor` → `SimulateTick`
- Every new `SimulationState` field must be added to `SimulationStateHasher`
- New `GameCommand` types need a `CommandProcessor` case and `InputSerializer`
  changes in the same commit
- Never hand-edit `.unity` scenes, prefabs or `.meta` files; change them through a
  connected editor, and only with instruction
<!-- boundary:end -->

## How to work

- Read the card before the code. Use plan mode for anything touching `Simulation/`,
  and flag which tests to run afterwards.
- Prefer the smallest change that satisfies the goal.
- When uncertain about intent: ask once, clearly, then proceed.
- Do not spawn a subagent for work that would use two or fewer of them (managed
  skills that fan out internally are exempt). Fan-out goes through the
  `orchestrate` skill, which carries the budget rules.
- Keep `[SerializeField]` fields in the same file as their MonoBehaviour; do not
  split a class across files without a strong reason.

## Checking your work

- `dotnet test dotnet/NodeWar.sln`: the simulation, lobby and wire formats, view
  maths, match log, progression, lockstep core, balance rig and Cloud Code module.
  Details and receipt rules: `docs/skills/run-dotnet-tests.md`.
- `scripts/compile-check.ps1`: type-checks everything else against the real Unity
  assemblies. It cannot see prefab or scene wiring and is not a test run.
- If `unity status` reports a ready Editor, `docs/skills/drive-the-editor.md` closes
  the scene-wiring gap. No Editor is normal, never a precondition for finishing.

## Keeping the docs true

- **Re-verify at commit time.** When your commit moves a card's sources into REVIEW
  (`scripts/okf-stale.ps1`, also shown at session start), re-read that card against
  the diff, fix what drifted, and stamp `verified: <sha>` in a follow-up
  `docs: reverify <card> against <sha>` commit on the same branch.
- **Size is a build failure.** `scripts/doc-budget.ps1` blocks in CI: word budgets,
  the card template, no dates, PR numbers or line numbers in cards. Cut detail the
  code already says rather than raising a budget.
- **A hash that differs between CI legs is a finding about the simulation, not a CI
  problem. Never re-pin a baseline to make it green.**
- `scripts/sim-guard.ps1` is only the mechanical subset of the `determinism-guard`
  skill; tiebreakers, tick order, hasher registration, command/serializer pairing
  and the view boundary still need the checklist.

## Commit convention

Conventional prefixes (`feat:`, `fix:`, `docs:`, `refactor:`, `chore:`). Roadmap
items move to Done when their pull request merges; `/update` reads that from git.

## Response style

No trailing additions: "one more thing", "also worth noting", "before I finish",
or similar. If something is important, say it once in the right place.
