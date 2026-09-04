# Skills

Executor instructions — how to actually run the procedures declared in
[../computations](../computations/index.md), and what evidence a run leaves behind.

These are distinct from `.claude/skills/*.md`, which are review procedures for a person or an agent
to follow. A skill here is pointed at by an Attested Computation's `executor.resource` and describes
a mechanical run that produces a receipt.

* [run-editmode-tests](run-editmode-tests.md) — the two ways to run the EditMode suite through
  Unity, and the `TestResults/results.xml` receipt both produce.
* [run-dotnet-tests](run-dotnet-tests.md) — the same simulation suite run by `dotnet test` over the
  projects in `dotnet/`, with no Unity and no licence. The runner CI uses.

All three runners write the same receipt from the same simulation test sources — that is what makes
their results comparable rather than merely similar.

One caveat, and it is the reason the receipt is produced by a single project: `dotnet/NodeWar.sln`
also contains `NodeWar.Lobby.Tests`, which the Unity runners do not see and which has nothing to do
with determinism. Run the solution for pass/fail; produce the receipt from
`NodeWar.Simulation.Tests` alone. Pointing `--logger` at the whole solution makes the two projects
overwrite one another's output. See [run-dotnet-tests](run-dotnet-tests.md).
