# Skills

Executor instructions — how to actually run the procedures declared in
[../computations](../computations/index.md), and what evidence a run leaves behind.

These are distinct from `.claude/skills/*.md`, which are review procedures for a person or an agent
to follow. A skill here is pointed at by an Attested Computation's `executor.resource` and describes
a mechanical run that produces a receipt.

* [run-editmode-tests](run-editmode-tests.md) — the two ways to run the EditMode suite through
  Unity, and the `TestResults/results.xml` receipt both produce.
* [run-dotnet-tests](run-dotnet-tests.md) — the same suite run by `dotnet test` over the projects in
  `dotnet/`, with no Unity and no licence. The runner CI uses.

All three write the same receipt, because they run the same test sources.
