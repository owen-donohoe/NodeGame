---
description: Reconcile Notion against new commits. The only time Notion is written.
---

# The Update procedure

Runs when I invoke `/update` and at no other time. Writing to Notion happens only inside an Update.

**Read, in this order, and stop as soon as you have enough:**

1. Notion Phases where `Status` is `Active` or `Audit plan against code`. Nothing else.
2. Notion Tasks · Open, for those phases, plus the Loose view.
3. `git log --oneline <Last Reviewed Commit>..HEAD` — **subjects only.**

**Escalate reluctantly.** These are hard rules, not guidance:

- Do not read any file unless a specific open task or a specific commit requires it.
- A commit subject that plausibly relates to an open task earns `git show --stat <sha>` — file names,
  no content. Nothing else earns it.
- Only if `--stat` confirms the connection do you read the diff, and only for the files that matter.
- Never diff a whole branch. Never walk the repo tree during an Update.
- Never open a `Shipped` or `Parked` phase page.
- If there are more than 20 new commits, report from subjects and stats alone and ask before reading
  any diff.

**Then write:**

- Tasks with clear commit evidence → `In Progress`.
- Tasks named in a `done:` trailer → `Done`.
- **Never mark a task Done on file-touching evidence alone.** A commit that touches a system is not
  work that finished. Report it as probably-done and let me confirm.
- New work visible in commits but absent from Notion → create the task, attached to the active phase.
  This is an ingestion gap, not a decision.
- Work in Notion that the commits contradict → flag it. Do not delete, do not silently re-scope.
- Update `Last Reviewed Commit` on the active phase to HEAD.
- Run `powershell -File scripts/okf-stale.ps1` and report any newly-suspect documents. This is a
  repo-side check and writes nothing to Notion.

**Do not**, during an Update: create or restructure phases, edit a Shipped phase, rewrite a plan, or
change anything I edited by hand. My edits are intentional; confirm before reverting any of them.
Do not stamp `verified:` or bump `verified_at_commit` on any document — re-verification means
reading the doc against its changed sources, which is a separate act from reconciling commits.
Report what looks stale and let me decide.

**Report:** what moved, what is blocked, what needs me. If nothing needs me, say so in one line.
