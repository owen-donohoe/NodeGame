---
type: Executor Skill
title: Run the EditMode test suite
description: The two ways to run Assets/Tests/EditMode/, when each applies, and the TestResults/results.xml receipt both produce.
tags: [testing, executor, unity, receipt]
generated: { by: claude-opus-5, at: 2026-08-31T00:00:00Z }
verified_at_commit: fc94a0f
status: stable
sources:
  - id: batch-runner
    resource: scripts/run-tests.ps1
    title: Batch-mode EditMode runner
    last_modified: 2026-08-30T16:44:10-04:00
  - id: live-runner
    resource: scripts/run-tests-live.ps1
    title: Live-Editor EditMode runner
    last_modified: 2026-08-30T16:44:10-04:00
  - id: bridge
    resource: Assets/Scripts/Editor/TestBridge.cs
    title: TestBridge trigger/done handshake
    last_modified: 2026-08-30T17:10:45-04:00
---

# Run the EditMode test suite

Two runners exist. They differ only in how they reach Unity; both execute the same suite and both
write the same NUnit3 XML to `TestResults/results.xml`. Neither is modified by the OKF layer — this
document describes them, it does not replace them.

## Choosing a runner

| | `scripts/run-tests.ps1` | `scripts/run-tests-live.ps1` |
|---|---|---|
| How it runs | Spawns Unity in `-batchmode` | Drives the already-open Editor |
| Editor must be | **Closed** | **Open**, compiled cleanly |
| Use when | CI, or no Editor session open | Iterating with the project open |
| Timeout | None (Unity blocks) | 300s waiting for the Editor |

Unity locks a project to one process, so batch mode cannot run while the Editor is open — that is
the only reason two runners exist.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\run-tests.ps1        # Editor closed
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\run-tests-live.ps1   # Editor open
```

`-ExecutionPolicy Bypass` is not optional and not a convenience. Windows client defaults to
`Restricted` when no scope sets a policy, and under it PowerShell refuses to load a `.ps1` at all —
the run dies before Unity is ever reached, with `running scripts is disabled on this system`. A
machine that once worked can start refusing after a policy reset, which looks like the test suite
breaking and is not. `scripts/hooks/pre-commit` and the `SessionStart` hook in
`.claude/settings.json` already invoke every script this way; these two are the only entry points a
human types, so they are the only ones a machine policy can stop. `-NoProfile` keeps a user profile
from injecting state into a run whose output is meant to be a receipt.

## How the live runner reaches the Editor

`Assets/Scripts/Editor/TestBridge.cs` is an `[InitializeOnLoad]` watcher, live as soon as the
project finishes compiling. The handshake is a GUID passed through two files:

1. The script writes a fresh GUID to `TestResults/trigger.txt`.
2. `TestBridge` sees the new GUID and waits until the Editor is idle — not compiling, not in Play
   Mode. It waits rather than interrupting either.
3. It runs the suite via `TestRunnerApi`, writes `TestResults/results.xml`, then writes that same
   GUID to `TestResults/done.txt`.
4. The script polls for `done.txt`, confirms the GUID is its own, and only then parses the results.

The GUID round-trip is what stops a stale `done.txt` from a previous run being read as this run's
result.

If nothing consumes the trigger, the Editor is closed, still compiling, or `TestBridge.cs` failed to
compile. The script exits 2 on timeout.

## The live runner can test stale code

**Unity's Auto Refresh reimports changed files when the Editor gains focus.** If the Editor is in
the background, a `.cs` change on disk has not been compiled yet — and `TestBridge`'s idle wait
cannot tell "finished compiling" apart from "has not started". The suite then runs the previous
assemblies and reports a perfectly clean green.

This is observed, not theoretical. Changing `baseHP` and immediately re-running produced 8/8 passed
against code whose real fingerprint had moved to a completely different value; reverting the change
produced the mirror image — 2 failures against source that was already correct.

Practical consequences:

* **Focus the Unity Editor after editing, before triggering a run.** Watch for the compile spinner
  to finish.
* **For a receipt you intend to rely on, prefer `run-tests.ps1`** with the Editor closed. Batch mode
  compiles from source every time, so its receipt cannot be stale in this way.
* `docs/attesters/hash_baseline.ps1` rejects a receipt written *before* the newest source file, which
  catches the common case. It cannot catch the inverse — a receipt written *after* an edit that Unity
  still has not compiled — because nothing in `results.xml` records which assemblies actually ran.

A green live run is good feedback. It is not proof.

## The receipt

Both runners leave `TestResults/results.xml` — NUnit3 XML with a `<test-run>` root carrying `total`,
`passed`, and `failed`, and a `<test-case>` per test with its `fullname`, `result`, and any failure
message.

That file is the **receipt** for
[computations/determinism-baseline](../computations/determinism-baseline.md). Read it with
`docs/attesters/hash_baseline.ps1`, which turns it into a verdict rather than a summary.

Note that a runner's own exit code answers "did the run complete and did every test pass", which is
not the same question as "is the determinism gate satisfied at this commit". The attester answers
the second.

## Exit codes

| Code | Meaning |
|---|---|
| 0 | Run completed, all tests passed |
| 1 | A test failed, or results.xml was missing or malformed |
| 2 | Live runner only: timed out waiting for the Editor |

`run-tests.ps1` deliberately derives pass/fail from the parsed XML rather than Unity's own exit
code, because `-runTests` returns 0 even when tests fail — it reports whether the *run* completed,
not whether the tests passed.
