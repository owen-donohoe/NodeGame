---
shell: 3
up: "[[Command pipeline]]"
tags: [shell/3, realm/match, system/commands, script]
file: Assets/Scripts/Game/Input/InputBuffer.cs
sources:
  - Assets/Scripts/Game/Input/InputBuffer.cs
verified: ce9fb6bf
---

# InputBuffer

> [!question] The question
> The one door into the game is 33 lines long. Why is something so small so important?

**Where you are:** [[Home]] › [[Match]] › [[Commands]] › [[Command pipeline]] › **InputBuffer** (script)

**Open:** [InputBuffer.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Input/InputBuffer.cs)

## What it is

The entire file:

```csharp
public class InputBuffer {
    private List<GameCommand> pendingCommands = new List<GameCommand>();
    public void EnqueueCommand(GameCommand c) { pendingCommands.Add(c); }
    public GameCommand[] DrainCommands() {
        if (pendingCommands.Count == 0) return new GameCommand[0];
        var result = pendingCommands.ToArray(); pendingCommands.Clear(); return result;
    }
}
```

Producers (input, UI panels, the bot) call `EnqueueCommand`. A tick driver calls `DrainCommands`
once per tick ([[TickRunner]]) or once per generated input ([[LockstepCore]]).

## Why it matters

- **It's the boundary.** Everything on one side may use floats, frames and Unity. Everything
  after `DrainCommands` is deterministic. Rule: *UI writes a `GameCommand` to `InputBuffer`;
  it never calls the simulation* (`.claude/rules/view-ui.md`).
- **Order is preserved.** It's a `List`, not a set: the order commands were enqueued *is* the order they're applied.
- **No logic.** No validation, no deduplication. Those belong to `CommandProcessor`. Keeping the door dumb means nothing can be decided twice.
- **It lives in `NodeWar.Simulation`** and has no Unity dependency, so it's linked into the balance rig's build too.

## Small optimisation to notice

`new GameCommand[0]` allocates on every empty drain, about 10 times a second. `Array.Empty<GameCommand>()`
would allocate nothing. Harmless at this rate, but it's the kind of thing [[Object pooling]] trains you to spot.

## Check yourself

> [!question]- Why must the buffer not reorder commands (for example, sort by villager ID)?
> The order within a tick is a gameplay input. Two orders to the same villager in one tick
> resolve differently depending on which comes last.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
