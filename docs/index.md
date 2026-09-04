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

## Historical snapshots

* [ui-migration-inventory](ui-migration-inventory.md) — the UI layer as it stood at `d0f4420`,
  before the phone-UI rebuild replaced it.

A snapshot carries `status: historical` and a `snapshot_of_commit:` instead of `sources:` and
`verified_at_commit:`. It is frozen on purpose, so its ground moving is expected rather than
suspect, and the freshness check has nothing to say about it. Do not add sources to one to
"fix" a warning — that would make every later commit report a false alarm. If a snapshot starts
describing live code again, it has stopped being a snapshot.

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

## How the check runs

Nobody has to remember to ask. That was the point — a freshness check you invoke by hand has the
same failure mode as the documents it polices.

* **SessionStart** runs `scripts/okf-stale-hook.ps1`, wired in `.claude/settings.json`. It is
  silent when every document is current and injects the suspect list when one is not. Silence
  when clean is deliberate: a check that reports "all current" every time trains the reader to
  skip it.
* **pre-commit** runs the same check as advisory — it reports and lets the commit through.
  Re-reading a document against moved sources is human work a commit cannot be compelled to
  contain, and blocking on it would mean every code change dragged a documentation review behind
  it until the hook got bypassed. It also runs `scripts/sim-guard.ps1`, which **does** block, and
  only when the commit touches `Simulation/`. See `scripts/hooks/README.md`; install per clone
  with `git config core.hooksPath scripts/hooks`.
* **CI** runs `.github/workflows/determinism.yml` on every push and pull request: `sim-guard.ps1`,
  then the simulation suite, then `docs/attesters/hash_baseline.ps1` — on `ubuntu-latest` and
  `windows-latest`. It **blocks**, and it is the only guard that blocks on determinism itself
  rather than on the mechanical regex.
* **`/audit`** runs the check as its step 0, so a chapter's plan is re-read against the code
  starting from the documents whose ground already moved.
* **`/update`** reports it, and is explicitly forbidden from acting on it. Reconciling commits and
  re-verifying a document are different acts.

`sim-guard.ps1` is the mechanical subset of `.claude/skills/determinism-guard.md` — the contract
items a regex can settle. Sort tiebreakers, tick order, hasher registration, command/serializer
pairing and the view boundary are not in it and still need the checklist and a reader.

`docs/attesters/hash_baseline.ps1` is now wired to that gate. Executing the simulation no longer
means Unity: `dotnet/` holds hand-written .NET projects that compile the same sources under
`Assets/` that Unity does, so the suite runs with no Editor and no licence. See
[skills/run-dotnet-tests](skills/run-dotnet-tests.md).

The matrix is the substantive part. The baselines assert exact integers, so two green legs assert
that the simulation computes bit-identical results across operating systems and runtimes — the
property lockstep depends on, and one nothing verified until the gate existed. A hash that differs
between legs is a finding about the simulation, not a CI problem.

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
