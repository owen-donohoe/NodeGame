---
shell: 3
up: "[[Test evidence]]"
down: []
concepts: []
tags: [shell/3, realm/lobby, realm/match, realm/backend, system/tooling, script]
card: docs/systems/tooling.md
file: scripts/compile-check.ps1
sources:
  - scripts/compile-check.ps1
verified: ce9fb6bf
---

# compile-check.ps1

> [!question] The question
> How do you catch a misspelled Unity API or broken HUD call site without opening the Editor?

**Where you are:** [[Home]] › [[Tooling]] › [[Test evidence]] › **compile-check.ps1** (script)

**Open:** [compile-check.ps1 in VS Code](vscode://file/C:/Dev/NodeGame/scripts/compile-check.ps1)

## What it is

A generated-project type check against real Unity and package assemblies. It builds selected current `Assets` source without running Unity or any tests. The header warns that it is not “a faithful reproduction of Unity's assembly graph.”

## Read it in this order

1. **Repository and version resolution.** Paths come from the script location. `ProjectVersion.txt` selects the matching installation under Unity Hub's Editor directory; missing prerequisites fail early.
2. **Source selection.** Discover `.asmdef` directories and exclude their contents, plus Plugins, because these are referenced as assemblies. Backend Shared is a deliberate exception: compile its current source alongside callers rather than trust an older shared DLL.
3. **Directory-prefix comparisons.** Trailing separators keep an asmdef folder from also excluding a similarly named sibling file. The comment records how `OutlineDriver.cs` was once silently dropped beside `Outline/`.
4. **Reference collection.** Prefer already-built ScriptAssemblies, then UnityEngine, plugin and package-cache DLLs, deduplicated by filename. Exclude `Assembly-CSharp.dll` and `NodeWar.Backend.Shared.dll`.
5. **Generated project.** Write under `Library/CompileCheck`, target `netstandard2.1`, pin C# 9, and list sources/references explicitly. Run `dotnet build` and return its exit code.
6. **Output filtering.** Default output focuses on errors; `-ShowAll` displays the compiler output more broadly.

## Lessons to take away

`Assembly-CSharp.dll` is excluded because it is stale output of the very code being checked; referencing it would make project types ambiguous with themselves. Other prebuilt references still matter and can be stale, so this is not a hermetic rebuild of every assembly.

Runtime and Editor code are compiled together. That catches syntax, missing usings, API mismatches and broken calls, but can allow an Editor-only dependency that Unity's real runtime assembly would reject. Scene references, UXML event delivery, clipping and behavior are different questions.

The script writes generated build files under Library. A fresh worktree needs access to the expected Unity-built assemblies; the project documents an ignored Library junction for that purpose. A task restricted to editing only `notes/` should inspect this script, not run it and write outside that scope.

## Check yourself

> [!question]- Why can this pass while a button still does nothing in Play Mode?
> A valid callback signature does not prove the button exists, is bound, receives the pointer event or calls the intended action. Those are runtime/wiring questions.

> [!question]- Does this recompile all `.asmdef` source from the current checkout?
> No. Most asmdef contents are excluded and referenced as built assemblies. Backend Shared is an explicit source exception, so stale references elsewhere remain possible.

## Go deeper ↓

This is the lowest shell. Below it is the code itself. Compare [[Test evidence]] before describing a clean compile as a test result.

## My understanding

<!-- Yours. AI never edits below this line. -->
