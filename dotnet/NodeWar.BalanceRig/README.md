# NodeWar.BalanceRig

Headless bot-vs-bot matches over the shipped simulation, one CSV row per
match. Tooling only: nothing in `Simulation/` knows it exists, and it changes
no simulation or balance behaviour.

```powershell
$env:PATH = 'C:\Program Files\dotnet;' + $env:PATH
dotnet run --project dotnet/NodeWar.BalanceRig -- --matches 100 --seed 1 --cap 6000 --out out/rig.csv
```

Options: `--matches N`, `--seed S`, `--cap TICKS`, `--out path.csv`,
`--loadout Barracks,...|none`, `--delay TICKS`, `--swap-seats on|off`,
`--v2-overlay`, `--balance file.json`, `--board file.asset`, `--trace SEED`.

## Seeds, seats and pairs

`--matches N` means N seeds. With `--swap-seats on` (the default) each seed is
played twice from one draft: seat 0 as drawn, then seat 1 with the board and
draft mirrored and the two players' setups swapped. That is 2N matches, rows
ordered seed by seed, seat 0 first, sharing `pair_id` (the seed). Commands
always apply P0 then P1, each in the order that player issued them.

Paired results are reported; equal outcomes are never promised, because an ID
tie-break can favour a seat. Mirroring needs the board's own symmetry, which
is `RigSetup.mirror`: the loader sets a half-turn for the shipped board, and a
test fixture supplies its own.

## Balance hashes

The filename of a balance export must equal its hash, with or without
`--v2-overlay`. The run prints both hashes: *source* (the file as exported)
and *effective* (after the overlay). Only the effective balance is played.

## Timeline output

Every match is observed (commands before each tick, state and the existing
`TickEventLog` after it). The observer reads, it never writes, and sizes its
arrays from the state it is given, so any map shape works. Three files come
out for `--out name.csv`:

| File | One row per | Holds |
|---|---|---|
| `name.csv` | match | the original columns, then the timeline summary columns |
| `name.windows.csv` | match and command window | commands, idle samples, idle villager-ticks and peak, by player |
| `name.flips.csv` | complete owner change | tick, node, core or not, from/to owner, claim or neutralisation, recapture |

Every row of all three carries `schema` (currently 1) and `map`
(`<cols>x<rows>-n<nodes>-<hash of the starting board>`), so files from
different boards cannot be mixed unseen. Windows are 10 s of ticks
(`10 * ticksPerSecond`), keyed by the tick a command was *applied* on, not
issued; the last window is a partial tail, its `end_tick` the ticks played.

Definitions:

- **Commands** are command records applied, refused ones included. Not taps, not
  decisions.
- **Idle** is a living, unconsumed villager in state `Idle`. Output is sample
  count and summed villager-ticks, not whether somebody was idle.
- **Flips** are complete owner changes: neutralisations and claims. A
  **recapture** is a claim by the player who was not the node's last
  non-neutral owner.
- **Opening contested**: a `CombatStarted` at a non-core node in post-ticks
  1..600 inclusive (60 s at 10 Hz); first tick and node recorded.
- **Lead at 1200**: P0 minus P1 non-core districts owned on post-tick 1200
  (cores excluded), taken once.
- **Snowball**: whether the side ahead at 1200 won. Ties, capped matches and
  matches over before 1200 are excluded and each exclusion is printed with the
  eligible denominator; with no eligible match it prints `n/a`.
