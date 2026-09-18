# Node War — Agent Instructions (Codex)

1v1 real-time strategy game on a node graph. Unity 6, namespace `NodeWar`,
lockstep P2P networking migrating to a server-authoritative hybrid.

This file is the Codex-facing counterpart to `CLAUDE.md`. Both files must
describe the same project; if they ever disagree, `CLAUDE.md` is the source
of truth and this file is stale — fix this file, don't follow it over that
one. Full architecture, docs, and skills are not duplicated here: read
`CLAUDE.md` first, then follow its pointers into `docs/` and `.claude/`.

## Simulation Boundary — Non-Negotiable

Both peers run identical simulation from identical inputs. Any violation
desyncs. Full contract in `docs/simulation-rules.md`.

- No UnityEngine references anywhere in `Simulation/`
- Integer-only math — no float, double, decimal
- No `DateTime`, `Time.deltaTime`, or any frame/wall-clock API
- No `UnityEngine.Random`
- Arrays or `List<T>` only — no Dictionary/HashSet iteration
- All sorts need total-order comparators with ID tiebreakers
- Tick order is canonical, never reordered:
  movement → combat → claiming → production → healing → respawns → win-check
- View and UI never write `SimulationState`. All changes go through:
  `GameCommand` → `InputBuffer` → `CommandProcessor` → `SimulateTick`
- Every new `SimulationState` field must be added to `SimulationStateHasher`
- New `GameCommand` types need a `CommandProcessor` case, and `InputSerializer`
  changes in the same commit
- Never hand-edit `.unity` scenes, prefabs, or `.meta` files — that YAML
  corrupts GUIDs and merge state. Change them through a connected editor.

## How to Work

- Read `CLAUDE.md`, then the relevant `docs/*.md` before proposing anything
- For anything touching `Simulation/`: plan before editing
- Prefer the smallest change that satisfies the goal; no unrelated refactors
- Work in an isolated git worktree/branch, not directly on `main`
- Do not merge into `main` automatically — leave that for review
- After `Simulation/` changes: flag which tests should be run

## Checking your work

- `dotnet test dotnet/NodeWar.sln` — Simulation and Lobby assemblies, the two
  that compile without UnityEngine
- `scripts/compile-check.ps1` — type-checks everything else against the real
  Unity assemblies (HUD, network, view, `Assets/UI/`)
- Neither of these replaces manual Unity verification for scene/prefab
  wiring — flag that as a manual step rather than claiming it's covered

## Commit Convention

`done: <task title>` in a commit message marks the matching Notion task done
on the next `/update` (a Claude-side command — Codex doesn't run it, but
should still use the same commit convention so history stays consistent).
