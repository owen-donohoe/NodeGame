---
description: Reconcile the Notion Roadmap with git (Now and Done), and report doc freshness.
---

# The Update procedure

Notion holds three things only: **Future** (plans), **Now** (work with commits
landing), **Done** (history, one row per shipped milestone). This keeps Now and Done
true; it never rewrites Future plans.

Identifiers: page **Node** `3cde745f-f2df-8139-8e3d-e93fac741de0`; database
**Roadmap**, data source `collection://9aa1895c-262d-4843-80a0-6bda9d3fcfbd`
(properties: Item, Status Future/Now/Done, Area, Release, Systems, Order, Shipped).
The Node page's first line records `Last update: <sha>`.

## Read (stop when you have enough)

1. The Node page's `Last update` sha.
2. Roadmap rows with Status **Now**.
3. `gh pr list --state merged --search "merged:>=<date of that sha>"` and
   `git log --oneline <sha>..origin/main` (subjects only).
4. Branches with commits not on `main`: `git branch -a --no-merged main`.

Read no files and no diffs unless a specific row cannot be judged from PR titles
and commit subjects; then `git show --stat` first.

## Write

- A merged PR that completes a **Now** row: set it **Done**, fill `Shipped`
  (`#<pr> (<date>)`), keep the body.
- A merged PR with no row: create a **Done** row (Item from the PR title, Area,
  Systems, Shipped). Group PRs that are one milestone into one row.
- An unmerged branch with recent commits and no **Now** row: create a **Now** row,
  or move the matching **Future** row to **Now**.
- A **Now** row with no commits for two weeks: report it; do not move it.
- Update the Node page's `Last update: <sha>` to `origin/main`'s head.

Never edit a **Future** row's plan, never delete rows, and never change anything the
user edited by hand without asking.

## Report

Run `powershell -File scripts/okf-stale.ps1` and `powershell -File scripts/doc-budget.ps1`
and include anything they flag (repo-side only; they write nothing to Notion). Then:
what moved, what needs the user. If nothing needs the user, say so in one line.
