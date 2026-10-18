---
shell: 3
up:
  - "[[The tick]]"
  - "[[Command pipeline]]"
  - "[[Smooth movement between ticks]]"
concepts:
  - "[[Fixed timestep]]"
  - "[[Events and observers]]"
tags: [shell/3, realm/match, system/match-flow, script]
file: Assets/Scripts/Game/Core/TickRunner.cs
sources:
  - Assets/Scripts/Game/Core/TickRunner.cs
  - Assets/Scripts/Game/Core/ITickProvider.cs
verified: ce9fb6bf
---

# TickRunner

> [!question] The question
> What is the smallest complete game loop in this codebase? It's 108 lines. Read this
> file before anything else in the simulation.

**Where you are:** [[Home]] › [[Match]] › [[Simulation]] › [[The tick]] › **TickRunner** (script)

**Open:** [TickRunner.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Core/TickRunner.cs) · [ITickProvider.cs](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Core/ITickProvider.cs)

## What it is

The **local** tick driver, used for practice and bot matches. Online, `LockstepCore` takes
its place. Both implement `ITickProvider`, so views don't care which one is running. It is
the Unity-side shell around the pure `SimulateTick`.

## Read it in this order

### 1. `ITickProvider`: the contract (22 lines)

```csharp
float TickAlpha { get; }
event Action<TickEventLog> TickSimulated;
```

The comments state two rules: the event fires **once per tick, even several per frame**,
and the log **is reused**, so subscribers copy what they need.

### 2. `Update`: the fixed-timestep loop

```csharp
accumulator += Time.deltaTime;              // frame time arrives, in any size
while (accumulator >= tickInterval)         // spend it in exact 0.1 s ticks
{
    botPlayer?.Evaluate();                  // bot decides (emits commands)
    tickEvents.Clear();
    ProcessBufferedCommands();              // drain InputBuffer → CommandProcessor
    GameSimulation.SimulateTick(simState, tickEvents);
    if (simState.tickCount % 50 == 0)       // checkpoint hash
        HashComputed?.Invoke(tickCount, SimulationStateHasher.ComputeHash(simState));
    accumulator -= tickInterval;
    TickSimulated?.Invoke(tickEvents);      // views react
}
```

This is the textbook [[Fixed timestep]] accumulator. `Time.deltaTime` is used *here*, in
the shell, to decide **how many** ticks to run. It never reaches *inside* a tick.

### 3. `TickAlpha`

```csharp
public float TickAlpha => accumulator / tickInterval;
```

That's the leftover time as a fraction of a tick, which is what [[Smooth movement between ticks]] uses.

### 4. `ProcessBufferedCommands`

It drains the buffer, raises `CommandsApplied` (for the [[Match log]] recorder, if there are any commands), then
applies each command in buffer order. The comment explains why: in local play, buffer order
mixes both players, so a match log records the *applied order*, not one list per player.

## Compare with `LockstepCore`

| | `TickRunner` | `LockstepCore` |
|---|---|---|
| Who supplies time | Reads `Time.deltaTime` itself (it's a MonoBehaviour) | Receives clocks from `GameManager` (it's Unity-free) |
| When commands apply | Next tick | Tick + input delay |
| Order | Buffer order | All of player 0's, then all of player 1's |
| Waits for anyone | Never | For the opponent's input ([[Netcode]]) |

## Check yourself

> [!question]- A frame takes 0.25 s (a hitch). What does `Update` do?
> The accumulator goes from, say, 0.03 to 0.28. The loop runs two ticks (0.28 → 0.18 → 0.08)
> and leaves 0.08, so `TickAlpha = 0.8`. `TickSimulated` fires twice.

> [!question]- Why does the bot evaluate *inside* the loop rather than once per frame?
> So it decides once per tick, deterministically aligned with the simulation, however many
> ticks a frame runs. Its commands then go through the same buffer as a human's.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
