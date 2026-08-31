---
okf_version: "0.2"
---

# Node War knowledge bundle

This directory is an [Open Knowledge Format](https://github.com/GoogleCloudPlatform/knowledge-catalog/tree/main/okf)
v0.2 bundle. Every concept below is a markdown file whose YAML frontmatter records what it
describes, what it derives from, and when it was last checked against that source.

`CLAUDE.md` remains the entry point for working in this repo and points at these documents
directly. This index exists so the set can be traversed as a graph, and so
`scripts/okf-stale.ps1` has something to walk.

## Concepts

* [game-model](game-model.md) — what Node War *is*: the match model, districts, suits, resources,
  and the win condition.
* [architecture](architecture.md) — the seven layers, information flow, scene structure, key
  classes, networking model.
* [simulation-rules](simulation-rules.md) — the determinism contract `Simulation/` must uphold.
* [adding-a-feature](adding-a-feature.md) — the 11-step checklist for any new feature.

## Subdirectories

* [computations](computations/index.md) — sanctioned procedures declared as Attested Computations.
* [skills](skills/index.md) — executor instructions for running those computations.
* [design-history](design-history/README.md) — the v2.1 master design document. Historical, and
  still substantially the plan.

`attesters/` holds the deterministic verification code the computations point at. It contains no
markdown and is not part of the concept graph.

## External members

`.claude/skills/*.md` live outside this bundle root but carry the same frontmatter and are covered
by the same freshness check. They stay where they are because `CLAUDE.md` addresses them by path.
OKF's conformance rules require consumers to tolerate a member outside the root, and
`scripts/okf-stale.ps1` reads that directory alongside `docs/`.

**Two directories are deliberately left out of the signal layer:**

* `.claude/rules/*.md` carry a `glob:` key, and `.claude/commands/*.md` carry a `description:` key.
  Both are Claude Code's own frontmatter schema, and both are load-bearing — `glob:` scopes a rule
  to matching paths, and a rules file that fails to parse would silently stop enforcing the
  determinism boundary in every session. Adding unrecognised keys to them is not worth that risk
  for freshness metadata alone.
* `scripts/okf-stale.ps1` still walks both directories. It reports them as carrying no sources and
  moves on, so adding OKF keys later is a change of mind, not a migration.

The ground those files cover is checked anyway: `.claude/rules/simulation.md` restates the contract
in [simulation-rules](simulation-rules.md), which does declare its sources.

## Conventions used here

* `sources[].resource` is a **repo-root-relative path**, so a source reference means the same thing
  from any file regardless of its own depth. A document's sources are the code files it describes —
  that is what makes staleness computable rather than a matter of remembering.
* `verified_at_commit` is a producer-defined key holding the short SHA a document was last confirmed
  against. It is the same idea as the `Last Reviewed Commit` property on Notion Phases, at file
  granularity instead of phase granularity. The two are independent and neither mirrors the other.
* Only a `human:` actor may write `verified:`. An agent that checks a document writes an agent-tier
  entry (`claude-opus-5`); that is the machine-confirmed tier, not human-reviewed. See the trust
  tiers in the OKF spec.
* Body links between documents are ordinary relative markdown links.
