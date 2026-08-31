# Node War — Claude Instructions

1v1 real-time strategy game on a node graph. Unity 6, namespace `NodeWar`,
lockstep P2P networking migrating to a server-authoritative hybrid.

## Ownership — nothing is mirrored

| Owns | Where |
|---|---|
| Current behaviour | the code |
| Current architecture | `docs/` (see below) |
| Future work | Notion **Phases** |
| Current work | Notion **Tasks** |

Live state is never cached into markdown. Do not restate `docs/` in this file,
and do not copy Notion content into the repo. Write to Notion only during `/update`.

## Notion identifiers

- Page **Node** — `3cde745f-f2df-8139-8e3d-e93fac741de0`
- DB **Phases** — `5b7c3aa7-93a1-4eb3-ba25-29b86e743514`
  data source `3664290f-3ad4-4308-8deb-d4c5b057090a`
- DB **Tasks** — `8706a60b-0572-4fb7-b933-2c48c275607d`
  data source `b8f545c7-316b-4d66-9d2e-19f42d5d27f5`
- View **Tasks · Open** — `ee6d187b-368d-473a-bd80-8b75ccf84958`
- View **Tasks · Loose** — `3cde745f-f2df-814a-bfcf-000c6ae4e1d7`
- View **Tasks · Quick wins** — `3cde745f-f2df-81ab-8526-000cd4865ae4`
- View **Phases · Roadmap** — `3cde745f-f2df-816a-ab45-000cac100ef7`

## Schemas

**Phases** — books and chapters. A *Book* is a long-lived area (Networking,
Drafting) and carries no status. A *Chapter* is a specific system or panel and
carries the plan, status, and tasks. The plan lives in the page body, never in
properties.

| Property | Type | Values |
|---|---|---|
| `Phase` | Title | — |
| `Type` | Select | `Book` · `Chapter` |
| `Status` | Select | `Half-formed idea` · `Idea with a plan` · `Audit plan against code` · `Active` · `Shipped` · `Parked` |
| `Parent Phase` | Relation → Phases (self, dual, other side `Sub-phases`) | — |
| `Last Reviewed Commit` | Text | short SHA |

Chapter body: `## Goal` · `## Approach` · `## Open questions` · `## Notes`.
For `Half-formed idea`, one line is enough — that is the point of the status.

**Tasks**

| Property | Type | Values |
|---|---|---|
| `Task` | Title | — |
| `Phase` | Relation → Phases (dual, other side `Tasks`) | may be empty |
| `Status` | Select | `Todo` · `In Progress` · `Blocked` · `Done` · `Dropped` |
| `Size` | Select | `S` · `M` · `L` |
| `Priority` | Select | `ASAP` · `Normal` · `Later` |

Tasks with no Phase are legitimate — bugs hit while doing something else.
`ASAP` is the quick-win lane: small, cheap, disproportionately useful.

## Which doc to read for what

Read the file, do not ask me to summarise it here.

- `docs/architecture.md` — the seven layers, information flow, scene structure,
  persistent objects, key classes per layer, networking model. Start here.
- `docs/simulation-rules.md` — the full determinism contract.
- `docs/adding-a-feature.md` — 11-step checklist for any new feature.
- `.claude/rules/{simulation,network,view-ui}.md` — boundary rules per layer.
- `docs/design-history/` — the v2.1 design document. Historical. Notion is
  authoritative for future work.
- `docs/index.md` — OKF v0.2 bundle root. Entry point for the doc graph and its
  freshness signal (`scripts/okf-stale.ps1`).

## Simulation Boundary — Non-Negotiable

Both peers run identical simulation from identical inputs. Any violation
desyncs. Full contract in `docs/simulation-rules.md`.

- No UnityEngine references anywhere in `Simulation/`
- Integer-only math — no float, double, decimal
- No `DateTime`, `Time.deltaTime`, or any frame/wall-clock API
- No `UnityEngine.Random` — only seeded RNG stored in `SimulationState`
- Arrays or `List<T>` only — no Dictionary/HashSet iteration
- All sorts need total-order comparators with ID tiebreakers
- Tick order is canonical, never reordered:
  movement → combat → claiming → production → healing → respawns → win-check
- View and UI never write `SimulationState`. All changes go through:
  `GameCommand` → `InputBuffer` → `CommandProcessor` → `SimulateTick`
- Every new `SimulationState` field must be added to `SimulationStateHasher`

## Key Entry Points

- `GameSimulation.SimulateTick()` — deterministic tick loop, 10Hz
- `CommandProcessor` — applies `GameCommand`s to `SimulationState`
- `GameManager` — match lifecycle (PreDraft → Drafting → PostDraft → Countdown → Playing)
- `LockstepRunner` / `TickRunner` — tick timing, shared via `ITickProvider`
- `SimulationStateHasher` — desync fingerprint, checked every 50 ticks

## C# Conventions

- Keep `[SerializeField]` fields in the same file as their MonoBehaviour
- Do not split a class across files without a strong reason
- Follow `SpawnBonusVillagers` when adding array-backed state needing view objects

## How to Work

- Read relevant files before proposing anything
- For anything touching `Simulation/`: use plan mode first
- Prefer the smallest change that satisfies the goal
- Do not modify `.unity` scenes, prefabs, or `.meta` files without instruction
- When uncertain about intent: ask once, clearly, then proceed
- After `Simulation/` changes: flag which tests should be run

## Automatic Guards

Two checks run without being asked. Neither replaces the reading they point at.

- **SessionStart** runs `scripts/okf-stale-hook.ps1`. It is silent when every
  document is current, and injects the suspect list when one is not. A suspect
  document is one whose sources moved after it was last verified — read it
  against those sources before trusting it.
- **pre-commit** (`scripts/hooks/`, enabled by
  `git config core.hooksPath scripts/hooks`) blocks on
  `scripts/sim-guard.ps1` when the commit touches `Simulation/`, and reports
  `scripts/okf-stale.ps1` without blocking.

`sim-guard.ps1` is the mechanical subset of `determinism-guard.md` — the items
a regex can settle. Sort tiebreakers, tick order, hasher registration,
command/serializer pairing and the view boundary are not in it and still need
the checklist.

Never stamp `verified:` or bump `verified_at_commit` on the user's behalf.

## Subagent Usage

Do not spawn a subagent for work that would use 2 or fewer of them — the
overhead of fresh context and re-reading files costs more than doing it inline.
Reserve them for 3+ genuinely independent angles, or where isolating large tool
output matters. Managed skills that fan out internally (`/code-review`,
`/security-review`) are exempt.

## Commit Convention

`done: <task title>` in a commit message marks that task **Done** on the next
`/update`. Without it, tasks move to **In Progress** at most — a commit that
touches a system is not work that finished.

## Commands and Skills

- `/update` — reconcile Notion against new commits. The only time Notion is written.
- `/audit` — re-check a chapter's plan against the code before it goes Active.

Skills are markdown files in `.claude/skills/`. Read the file and follow its
procedure; there is no invocation syntax. Reference them by path:
`determinism-guard.md` · `write-sim-test.md` · `cs-review.md` ·
`session-summary.md` · `phase-plan.md` (largely superseded by Notion Phases).

## Response Style

No trailing additions: "one more thing", "also worth noting", "before I finish",
or similar. If something is important, say it once in the right place.
Nothing enforces this mechanically — treat it as strict anyway.
