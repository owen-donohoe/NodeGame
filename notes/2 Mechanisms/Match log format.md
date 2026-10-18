---
shell: 2
up: "[[Match log]]"
concepts:
  - "[[Endianness]]"
tags: [shell/2, realm/backend, system/match-log]
sources:
  - Assets/Scripts/MatchLog/
verified: ce9fb6bf
---

# Match log format

> [!question] The question
> How do you design a binary file that next year's build can still open, even though next
> year's build will have added fields this one has never heard of?

**Where you are:** [[Home]] › [[Backend]] › [[Match log]] › **Match log format** (mechanism)

## The problem

Formats change. If version 2 adds a field, version 1 readers shouldn't crash, and version 2
readers must still open version 1 files. A plain "struct dumped to disk" breaks as soon as
anything moves.

## The idea

**A *chunked* (tag-length-value) format: a short magic header, then a list of chunks, each
`[tag][length][payload]`. A reader handles the tags it knows and *skips* unknown ones by
their length. New information gets a new tag, and old tags never change meaning.**

## How it works here

```
"NWML"            4 bytes  magic: "this is a Node War match log"
FormatVersion     u16      1 (changes only if the framing itself breaks)
┌ tag u16 │ length u32 │ payload … ┐   ← repeated
└─────────┴────────────┴───────────┘
```

| Tag | Chunk | Holds |
|---|---|---|
| 1 | HEADER | protocol, sim, content, matchId, player IDs, local player, tier, seed, start time, kind |
| 2 | BOARD | board configuration (older logs) |
| 3 | LOADOUTS | both players' suits and districts |
| 4 | DRAFT | placements in order |
| 5 | TICKS | `{tick, GameCommand[]}`, empty ticks omitted |
| 6 | HASHES | `{tick, hash}` checkpoints |
| 7 | RESULT | reason, winner, end tick, final hash, first desync tick |
| 8 | ERAS | per-type era tables (augments loadouts) |
| 9 | SKINS | cosmetics (augments loadouts) |
| 10 | BOARD_V2 | terrain, slot mask and draft pools (sim ≥ 3) |
| 11 | SETUP | the agreed match setup (sim ≥ 3, with BOARD_V2) |

Parsing is strict where it must be: truncation, duplicate known chunks, missing required
data or conflicting boards all fail. Unknown tags are skipped.

## Why this, not that

| Option | Gain | Pay | Verdict |
|---|---|---|---|
| **Tag-length-value chunks** (chosen) | Forward and backward compatible. Unknown data is skippable | Hand-written reader and writer | Chosen. The same idea as PNG chunks or RIFF/WAV |
| JSON | Readable, flexible | Several times bigger (every number becomes text plus a field name), and slower to parse | Too big for thousands of ticks |
| Protobuf / FlatBuffers | Schema evolution built in | A dependency and code generation, in both Unity and the server | Reasonable later, but not needed for 11 chunk types |
| Raw struct dump | Trivial | Breaks on any layout change | No |

## What it costs later

- **Two version numbers** to reason about: `FormatVersion` (framing) and the simulation
  version in HEADER (meaning). *"Framing compatibility does not imply simulation compatibility."*
- **Every new payload is a new tag forever.** BOARD and BOARD_V2 coexist.

## Check yourself

> [!question]- A version-1 reader opens a file containing tag 12, which it has never heard of. What happens?
> It reads tag 12's length and skips that many bytes. Whether the log can still be *replayed*
> depends on whether tag 12 was required to rebuild the match. The simulation version decides that.

> [!question]- Why is GameCommand's layout change a new TICKS-style tag rather than a change to tag 5?
> Old logs with tag 5 must keep their meaning. If tag 5's payload changed, every old log would be misread.

## My understanding

<!-- Yours. AI never edits below this line. -->
