---
description: Re-check a chapter's plan against the code before it goes Active.
---

# The Audit procedure

Runs when a phase moves to `Audit plan against code`, before it goes Active. The plan may have been
written months and dozens of commits ago.

1. Read the phase body.
2. Read only the code the plan actually concerns.
3. For each item in the Approach: still valid · already done · now impossible or wrong · unchanged
   but needs re-scoping.
4. Report the drift. Propose an amended Approach. **Do not rewrite the plan without my sign-off.**
5. On sign-off: update the body, break the phase into sub-phases if it has grown past a week, create
   the tasks, set `Status = Active`.
