# Git hooks

Version-controlled, unlike `.git/hooks/`, which is why they live here.

## Install

One command per clone:

```
git config core.hooksPath scripts/hooks
```

This replaces `.git/hooks/` entirely rather than adding to it. Nothing is lost
here — that directory contained only Git's own `.sample` files.

To check it took, or to see whether a clone has it:

```
git config --get core.hooksPath
```

To uninstall: `git config --unset core.hooksPath`.

## What runs

| Hook | Check | Severity |
|---|---|---|
| `pre-commit` | `scripts/sim-guard.ps1` | **Blocks**, and only when the commit touches `Assets/Scripts/Game/Simulation/` |
| `pre-commit` | `scripts/okf-stale.ps1` | Advisory — reports and lets the commit through |

The severity split is the point. A `float` in the simulation layer is an
objective violation of a contract with no legitimate reading, so it blocks. A
document whose sources moved needs a human to re-read it against the code,
which is not work a commit can be compelled to contain — blocking on it would
mean every code change drags a documentation review behind it, and the
predictable outcome is that the hook gets bypassed and stops meaning anything.

`git commit --no-verify` skips both. The guard is the mechanical subset of
`.claude/skills/determinism-guard.md`, not the whole of it, and a regex can be
wrong in ways a reader is not.
