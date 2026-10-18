# Node War — Agent Instructions (Codex)

1v1 real-time strategy on a node graph. Unity 6, namespace `NodeWar`.
Deterministic lockstep peer-to-peer matches with a Unity Gaming Services backend.

`CLAUDE.md` is the source of truth for project-wide rules; this file carries the
part a Codex agent needs on every turn. If the two disagree, follow `CLAUDE.md`
and report the difference.

## You are usually an engineer

Read `docs/roles/engineer.md` once at the start of a task. It says how to use a
brief, how to stay in scope, how to commit, and what to report.

## Loading context

- Your brief names the systems in scope. Get their context with
  `powershell -File scripts/context.ps1 -Systems <a>,<b>`: full cards for those
  systems, their neighbours by interface only. That is the whole context.
- Need something outside it? Say so in your report instead of exploring. Cost is
  turns times context.
- `docs/map.md` shows how all systems connect; you rarely need it.

## Simulation Boundary — Non-Negotiable

Both peers run identical simulation from identical inputs; any violation desyncs.
Full contract: `docs/contracts/determinism.md`.

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

## Checking your work

- `dotnet test dotnet/NodeWar.sln`. Per-project counts: `docs/skills/run-dotnet-tests.md`.
- `scripts/compile-check.ps1` type-checks Unity-side code. In a fresh worktree it
  needs the Unity assemblies first, once, from the worktree root:
  `cmd /c mklink /J Library C:\Dev\NodeGame\Library` (gitignored).
- Neither covers scene or prefab wiring; flag that as a manual step.
- `scripts/doc-budget.ps1` if you touched docs.

## Commit convention

Conventional prefixes (`feat:`, `fix:`, `docs:`, `refactor:`, `chore:`), one
commit per finished step. If your commit moves a card into REVIEW
(`scripts/okf-stale.ps1`), re-read that card against your diff and stamp it in a
follow-up `docs: reverify <card> against <sha>` commit.
