---
type: Executor Skill
title: Drive a connected Unity Editor from the CLI
description: How to check for a connected Editor and drive it — hierarchy, scene and game view captures, console, EditMode tests, C# evaluation — and what to do when it is not there.
tags: [unity, cli, executor, editor]
generated: { by: claude-opus-5, at: 2026-09-13T00:00:00Z }
status: draft
---

# Drive a connected Unity Editor

Everything here is conditional. Establish that first:

```bash
unity status
```

An instance in state `ready` means the CLI can drive that Editor. **Anything
else means this document does not apply.** That is a normal state, not a
fault — do not debug it, do not ask for it to be fixed, and never make it a
precondition for finishing work. Fall back to
[run-dotnet-tests](run-dotnet-tests.md) and `scripts/compile-check.ps1`, which
need no Editor at all.

## Why it is worth using when present

`scripts/compile-check.ps1` declares its own blind spot: it cannot see prefab
or scene wiring. A connected Editor can. That is the gap this closes, and it
is the reason to prefer driving the Editor over guessing at a `.unity` file.

It also means scene and prefab changes can be made *correctly* — Unity does
the serializing, so GUIDs and merge state stay intact. Hand-editing that YAML
remains forbidden regardless.

## What it can do

`unity command` lists everything the Editor exposes. The ones that matter here:

| Command | Use |
|---|---|
| `capture_scene_view` / `capture_game_view` | Render a PNG of what the Editor is showing. The fastest way to answer "does this look right" |
| `console` / `console_status` | Read Unity's console, including whether compilation failed |
| `run_tests` / `list_tests` | The EditMode suite through Unity — see [run-editmode-tests](run-editmode-tests.md) |
| `create_gameobject`, `add_component`, `attach_script` | Scene edits that go through Unity rather than through the YAML |
| `eval` | Arbitrary C# in the Editor |

`eval` runs on this machine, in the user's own account, against their own open
Editor. It is not remote access and grants nothing they do not already have at
their terminal. It is still arbitrary code execution against live project
state: use it to read and to make changes that were asked for, not to explore
destructively.

## Targeting the right Editor

Without `--project-path`, the CLI targets the Editor whose project contains the
current directory — so the target follows the shell's cwd. With more than one
Editor open, pass it explicitly or the command fails with `AMBIGUOUS_EDITOR`:

```bash
unity command editor_play --project-path /path/to/NodeGame
```

## When a connected Editor stops answering

Almost always Safe Mode. A C# compile error keeps `com.unity.pipeline` from
loading, and `unity status`, `unity command` and `unity list` then cannot
connect at all. Confirm with `unity pipeline list`, which reads the project
rather than the Editor and reports the Safe Mode flag.

**Fix the compile error and restart Unity.** Do not fall back to editing scene
or asset files by hand — that is the failure mode this whole path exists to
avoid.

## What it needs

- The Unity CLI installed and authenticated per-machine (`unity auth status`).
  Not part of this repo.
- `com.unity.pipeline` in `Packages/manifest.json`. This *is* in the repo, so
  a collaborator who has the CLI gets the project half for free.

Both are beta and may move. A collaborator may have neither, which is the
reason for the conditional framing at the top rather than a standing
assumption.

## Why this file declares no sources

Its subject is a CLI and an Editor package, both of which live outside this
repo and version independently of it. `Packages/manifest.json` pins the
package but does not describe the behaviour, so declaring it as a source would
report staleness on every unrelated dependency change. There is nothing here
whose truth a commit to this repo can settle.
