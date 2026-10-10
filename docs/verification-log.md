---
type: Reference
title: Verification log
description: The full history of who verified which document and when. Moved out of the documents themselves on 2026-09-20; each document keeps only its most recent entry.
tags: [okf, provenance]
generated: { by: claude-opus-5, at: 2026-09-20T13:20:00Z }
status: stable
---

# Verification log

Every `verified:` entry the documents in this bundle have ever carried.

They used to live in full at the top of each document, which put an
append-only list that only grows into the first thing any reader loads —
`architecture.md` alone carried seven entries, in the file agents read before
anything else. `okf-stale.ps1` never read them; it reads `verified_at_commit`
only. So the list was costing a read on every visit and paying nobody back.

Each document now keeps its single most recent entry, which is the one that
says who last stood behind it. The history is here. Nothing below was added,
edited or re-dated in the move — a verification is a claim about a human act,
and inventing one is the one thing this system exists to prevent.

## `docs/adding-a-feature.md`

- { by: claude-opus-5, at: 2026-08-31T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-02T00:00:00Z }

- { by: claude-opus-5-5, at: 2026-09-29T18:00:00Z }
- { by: claude-opus-5-5, at: 2026-09-30T07:00:00Z }
- { by: gpt-6-sol, at: 2026-09-30T07:00:00Z }
- { by: claude-sonnet-5-5, at: 2026-10-03T00:41:16Z }
- { by: gpt-6-sol, at: 2026-10-06T01:06:51Z }
- { by: gpt-6-sol, at: 2026-10-06T01:08:47Z }
- { by: gpt-6.1-sol, at: 2026-10-08T16:00:47Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T16:13:42Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T16:35:44Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T16:47:09Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T17:01:14Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T17:27:11Z }

- { by: claude-opus-5-5, at: 2026-10-08T17:33:28Z }

- { by: gpt-6, at: 2026-10-08T21:08:43Z }
- { by: gpt-6.1-sol, at: 2026-10-09T00:31:48Z }
- { by: claude-sonnet-5-5, at: 2026-10-09T01:54:09Z }

- { by: claude-sonnet-5-5, at: 2026-10-09T17:14:09Z }
- { by: gpt-6.1-sol, at: 2026-10-09T19:20:20Z }

## `docs/architecture.md`

- { by: claude-opus-5, at: 2026-08-31T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-02T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-02T02:00:00Z }
- { by: claude-opus-5, at: 2026-09-02T04:00:00Z }
- { by: claude-opus-5, at: 2026-09-13T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-13T01:00:00Z }
- { by: claude-opus-5, at: 2026-09-14T00:00:00Z }

- { by: claude-opus-5-5, at: 2026-09-29T18:00:00Z }
- { by: claude-opus-5-5, at: 2026-09-30T07:00:00Z }
- { by: gpt-6-sol, at: 2026-09-30T07:00:00Z }
- { by: claude-sonnet-5-5, at: 2026-10-03T00:41:16Z }
- { by: gpt-6-sol, at: 2026-10-06T01:06:24Z }
- { by: gpt-6-sol, at: 2026-10-06T01:08:27Z }
- { by: gpt-6.1-sol, at: 2026-10-08T16:00:47Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T16:13:42Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T16:35:44Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T16:47:09Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T17:01:14Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T17:27:11Z }

- { by: claude-sonnet-5-5, at: 2026-10-09T01:54:09Z }

- { by: gpt-6.1-sol, at: 2026-10-09T14:51:28Z }

- { by: gpt-6.1-sol, at: 2026-10-09T19:20:20Z }
- { by: gpt-6.1-sol, at: 2026-10-09T19:59:12Z }

## `docs/simulation-rules.md`

- { by: claude-opus-5, at: 2026-08-31T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-02T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-13T00:00:00Z }

- { by: claude-opus-5-5, at: 2026-09-29T18:00:00Z }
- { by: claude-opus-5-5, at: 2026-09-30T07:00:00Z }
- { by: gpt-6-sol, at: 2026-09-30T07:00:00Z }
- { by: claude-sonnet-5-5, at: 2026-10-03T00:41:16Z }
- { by: gpt-6-sol, at: 2026-10-06T01:06:06Z }
- { by: gpt-6-sol, at: 2026-10-06T01:08:27Z }
- { by: gpt-6.1-sol, at: 2026-10-08T16:00:48Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T16:13:42Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T16:35:44Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T16:47:09Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T17:01:14Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T17:27:11Z }

- { by: claude-sonnet-5-5, at: 2026-10-09T01:54:09Z }

## `docs/skills/run-dotnet-tests.md`

- { by: claude-opus-5, at: 2026-09-13T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-14T00:00:00Z }

- { by: claude-opus-5-5, at: 2026-09-29T18:00:00Z }
- { by: claude-opus-5-5, at: 2026-09-30T07:00:00Z }
- { by: gpt-6-sol, at: 2026-09-30T07:00:00Z }
- { by: claude-sonnet-5-5, at: 2026-10-03T00:41:16Z }
- { by: gpt-6-sol, at: 2026-10-06T01:06:25Z }
- { by: gpt-6.1-sol, at: 2026-10-08T16:00:48Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T16:13:42Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T16:35:44Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T16:47:09Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T17:01:14Z }

- { by: claude-sonnet-5-5, at: 2026-10-08T17:27:11Z }

- { by: gpt-6.1-sol, at: 2026-10-09T00:31:48Z }

- { by: gpt-6.1-sol, at: 2026-10-09T00:49:39Z }

- { by: gpt-6.1-sol, at: 2026-10-09T00:59:49Z }
- { by: gpt-6.1-sol, at: 2026-10-09T01:16:59Z }

- { by: claude-sonnet-5-5, at: 2026-10-09T01:54:09Z }

## `.claude/skills/cs-review.md`

- { by: claude-opus-5, at: 2026-08-31T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-02T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-02T02:00:00Z }
- { by: claude-opus-5, at: 2026-09-02T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-13T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-13T01:00:00Z }
- { by: claude-opus-5, at: 2026-09-14T00:00:00Z }
- { by: claude-opus-5-5, at: 2026-09-29T18:00:00Z }
- { by: claude-opus-5-5, at: 2026-09-30T07:00:00Z }
- { by: gpt-6-sol, at: 2026-09-30T07:00:00Z }
- { by: claude-sonnet-5-5, at: 2026-10-03T00:41:16Z }
- { by: gpt-6-sol, at: 2026-10-06T01:06:52Z }
- { by: gpt-6-sol, at: 2026-10-06T01:08:47Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T16:13:42Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T16:35:44Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T16:47:09Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T17:01:14Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T17:27:44Z }
- { by: claude-sonnet-5-5, at: 2026-10-09T01:54:09Z }

- { by: claude-sonnet-5-5, at: 2026-10-09T17:14:09Z }
- { by: gpt-6.1-sol, at: 2026-10-09T19:20:20Z }

## `.claude/skills/determinism-guard.md`

- { by: claude-opus-5, at: 2026-08-31T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-02T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-13T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-13T01:00:00Z }

- { by: claude-opus-5-5, at: 2026-09-29T18:00:00Z }
- { by: claude-opus-5-5, at: 2026-09-30T07:00:00Z }
- { by: gpt-6-sol, at: 2026-09-30T07:00:00Z }
- { by: claude-sonnet-5-5, at: 2026-10-03T00:41:16Z }
- { by: gpt-6-sol, at: 2026-10-06T01:06:52Z }
- { by: gpt-6-sol, at: 2026-10-06T01:08:47Z }
- { by: gpt-6.1-sol, at: 2026-10-08T16:00:48Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T16:13:42Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T16:35:44Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T16:47:09Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T17:01:14Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T17:27:44Z }
- { by: claude-sonnet-5-5, at: 2026-10-09T01:54:09Z }

- { by: claude-sonnet-5-5, at: 2026-10-09T17:14:09Z }
- { by: gpt-6.1-sol, at: 2026-10-09T19:20:20Z }

## `.claude/skills/write-sim-test.md`

- { by: claude-opus-5, at: 2026-08-31T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-02T00:00:00Z }

- { by: claude-opus-5-5, at: 2026-09-29T18:00:00Z }
- { by: claude-opus-5-5, at: 2026-09-30T07:00:00Z }
- { by: gpt-6-sol, at: 2026-09-30T07:00:00Z }
- { by: claude-sonnet-5-5, at: 2026-10-03T00:41:16Z }
- { by: gpt-6-sol, at: 2026-10-06T01:07:09Z }
- { by: gpt-6.1-sol, at: 2026-10-08T16:00:49Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T16:13:42Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T16:35:44Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T16:47:09Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T17:01:14Z }

- { by: claude-sonnet-5-5, at: 2026-10-08T17:27:11Z }

- { by: gpt-6.1-sol, at: 2026-10-09T00:31:48Z }

- { by: gpt-6.1-sol, at: 2026-10-09T00:49:39Z }

- { by: gpt-6.1-sol, at: 2026-10-09T00:59:49Z }
- { by: gpt-6.1-sol, at: 2026-10-09T01:16:59Z }
- { by: claude-sonnet-5-5, at: 2026-10-09T01:54:09Z }

- { by: claude-sonnet-5-5, at: 2026-10-09T17:14:09Z }
- { by: gpt-6.1-sol, at: 2026-10-09T19:20:20Z }

## `docs/game-model.md`


- { by: claude-opus-5-5, at: 2026-09-29T18:00:00Z }
- { by: claude-opus-5-5, at: 2026-09-30T07:00:00Z }
- { by: gpt-6-sol, at: 2026-09-30T07:00:00Z }
- { by: claude-sonnet-5-5, at: 2026-10-03T00:41:16Z }
- { by: gpt-6-sol, at: 2026-10-06T01:06:06Z }
- { by: gpt-6.1-sol, at: 2026-10-08T16:00:48Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T16:13:42Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T16:35:44Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T16:47:09Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T17:01:14Z }

- { by: claude-opus-5-5, at: 2026-10-08T17:33:28Z }
- { by: gpt-6.1-sol, at: 2026-10-09T00:31:48Z }
- { by: claude-sonnet-5-5, at: 2026-10-09T01:54:09Z }

- { by: claude-sonnet-5-5, at: 2026-10-09T17:14:09Z }
- { by: gpt-6.1-sol, at: 2026-10-09T19:20:20Z }

## `docs/computations/determinism-baseline.md`


- { by: claude-opus-5, at: 2026-09-13T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-13T01:00:00Z }
- { by: claude-opus-5-5, at: 2026-09-29T18:00:00Z }
- { by: claude-opus-5-5, at: 2026-09-30T07:00:00Z }
- { by: gpt-6-sol, at: 2026-09-30T07:00:00Z }
- { by: claude-sonnet-5-5, at: 2026-10-03T00:41:16Z }
- { by: gpt-6-sol, at: 2026-10-06T01:06:51Z }
- { by: gpt-6-sol, at: 2026-10-06T01:08:47Z }
- { by: gpt-6.1-sol, at: 2026-10-08T16:00:48Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T16:13:42Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T16:35:44Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T16:47:09Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T17:01:14Z }

- { by: claude-sonnet-5-5, at: 2026-10-08T17:27:44Z }
- { by: gpt-6.1-sol, at: 2026-10-09T00:31:48Z }
- { by: claude-sonnet-5-5, at: 2026-10-09T01:54:09Z }

- { by: claude-sonnet-5-5, at: 2026-10-09T17:14:09Z }
- { by: gpt-6.1-sol, at: 2026-10-09T19:20:20Z }

## `docs/skills/run-editmode-tests.md`

- { by: claude-opus-5, at: 2026-09-13T00:00:00Z }
- { by: claude-opus-5-5, at: 2026-09-29T18:00:00Z }
- { by: claude-opus-5-5, at: 2026-09-30T07:00:00Z }
- { by: gpt-6-sol, at: 2026-09-30T07:00:00Z }
- { by: gpt-6-sol, at: 2026-10-06T01:06:25Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T16:13:42Z }

- { by: claude-sonnet-5-5, at: 2026-10-08T17:01:14Z }
- { by: gpt-6.1-sol, at: 2026-10-09T00:31:48Z }

- { by: claude-sonnet-5-5, at: 2026-10-09T01:54:09Z }

## `docs/design-history/README.md`

- { by: claude-opus-5, at: 2026-08-31T00:00:00Z }
- { by: claude-opus-5-5, at: 2026-09-29T18:00:00Z }
- { by: claude-opus-5-5, at: 2026-09-30T07:00:00Z }
- { by: gpt-6-sol, at: 2026-09-30T07:00:00Z }
- { by: claude-sonnet-5-5, at: 2026-10-03T00:41:16Z }
- { by: gpt-6-sol, at: 2026-10-06T01:06:06Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T16:13:42Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T16:35:44Z }
- { by: claude-sonnet-5-5, at: 2026-10-08T17:01:14Z }
- { by: gpt-6.1-sol, at: 2026-10-09T00:31:48Z }

- { by: claude-sonnet-5-5, at: 2026-10-09T01:54:09Z }

## D2 review backlog at `45eed7f258f0b26fb748f446332817b83ed5fbe1`

The main-checkout `okf-stale.ps1` hard-codes its own repository root. It was
run as requested, then a temporary outside-repo copy of that exact script
with only RepoRoot changed checked the feature worktree. The worktree reported
nine REVIEW documents. `write-sim-test.md` was read and corrected against D2;
`run-dotnet-tests.md` was also read fully and its case counts corrected from
the final 3674-pass run. Only those two documents receive verification stamps.

The following REVIEW documents remain unstamped for lead review. Their whole
document/source claims were not re-read in this bounded D2 task; some sources
also moved in `0c0c3cf` or D1 before D2. Reading the baseline computation alone
does not verify its cited simulation contract, so it is included here too.

- `docs/adding-a-feature.md`
- `docs/architecture.md`
- `docs/game-model.md`
- `docs/simulation-rules.md`
- `docs/computations/determinism-baseline.md`
- `docs/design-history/README.md`
- `.claude/skills/cs-review.md`
- `.claude/skills/determinism-guard.md`

## D3 review backlog at `4e9509aadd1a4436d4f65c5be59c1876ad80d8bb`

Ran the main-checkout freshness script as requested; it hard-codes the main
root. Re-ran the same script in memory with only RepoRoot set to the feature
worktree. It reported ten REVIEW documents. Re-read write-sim-test.md and
run-dotnet-tests.md fully against D3 and the 3708-pass result; updated collection
and raid semantics, six-int Collect=9 coverage, and counts (692 simulation,
595 lobby, 99 match log). Only those two documents are stamped. Their moved
sources since D2 are D3 code/tests and the previously read runner document.

The eight documents already in the D2 backlog remain unstamped. Their older
source changes in 0c0c3cf, D1 or D2 were not fully audited in this bounded D3
task. The combined review read was output-truncated, so it is not claimed as
whole-document verification. Their current source/document claims still need
lead review; the D2 backlog above lists all eight. No baseline re-pin occurred:
EmptyTick=-563755666 and MoveAndCombat=-2013445737, SimulationVersion 4.

The post-docs freshness check also flags write-sim-test.md because this same
follow-up changes its cited run-dotnet-tests.md body. Both documents were
reviewed together; the mandated stamps point to the code SHA, not the later
docs commit. This dependency cascade is reported without a third commit.

## D4 review backlog at `ce96bae0f18f0e9052e2b27fa2d1b810284db8f3`

Ran the main-checkout freshness script from the worktree as requested. Its
RepoRoot resolves to main, so an outside-repo copy with only RepoRoot changed
also audited the feature worktree. It reported eleven REVIEW documents.
Re-read write-sim-test.md and run-dotnet-tests.md fully against the D4 diff and
final result: 3741 passed, with 721 simulation and 1565 view cases. Added Pier
rules/tests, current pins and case counts; only these two receive stamps.

The following remain unstamped. Their wider claims and older source changes
were not fully audited in this bounded D4 task. The baseline table and D4
schema explanation were corrected in the code commit; that does not verify
its older cited contract/source history.

- `docs/adding-a-feature.md`
- `docs/architecture.md`
- `docs/game-model.md`
- `docs/simulation-rules.md`
- `docs/computations/determinism-baseline.md`
- `docs/design-history/README.md`
- `docs/skills/run-editmode-tests.md`
- `.claude/skills/cs-review.md`
- `.claude/skills/determinism-guard.md`

Both stamped documents were reviewed together. The mandated stamp points to
the code SHA; this follow-up's runner body change can still flag write-sim-test.md
through their dependency, without warranting a third commit. Editor wiring,
positive release Pier tuning/export and Linux CI remain lead acceptance steps;
no asset, metadata, scene or prefab content was edited.

## E4 re-verification at `36c57c73087ced5dd842a653f676c83b51c83031`

Re-read the ten initial REVIEW documents against their declared sources in
`git diff ce883747 HEAD`, correcting E1-E3 district health, Storehouse output,
Workshop/minion units, presentation and test totals. Also corrected and verified
`.claude/skills/cs-review.md`. All eleven carry the E4 code SHA and a single
`gpt-6.1-sol` verification entry; their former agent entries are archived above.
Human provenance was preserved.

The same documentation commit changes bodies of documents cited by six of these
members: adding-a-feature, game-model, determinism-baseline, cs-review,
determinism-guard and write-sim-test. The freshness script consequently reports
those cross-document edges as REVIEW after this commit. Those updated bodies
were reviewed together here; the requested stamp remains the E4 code SHA rather
than the later documentation SHA. This is a freshness follow-up for the lead,
not an unread E1-E3 source backlog.

The untracked main-checkout `pr-d.md` remains the lead's backlog. It was not copied
or edited. Editor asset migration/fresh export, the explicit release-content gate,
Unity visual/wiring checks and a Linux CI receipt were not performed by E4.
