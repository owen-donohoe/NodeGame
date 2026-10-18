---
shell: 3
up: "[[Match log format]]"
concepts:
  - "[[Endianness]]"
tags: [shell/3, realm/backend, system/match-log, script]
file: Assets/Scripts/MatchLog/MatchLogFormat.cs
sources:
  - Assets/Scripts/MatchLog/MatchLogFormat.cs
verified: ce9fb6bf
---

# MatchLogFormat

> [!question] The question
> How do you write a reader for *untrusted* binary files that never crashes, never over-allocates, and still opens files written by future versions?

**Where you are:** [[Home]] › [[Backend]] › [[Match log]] › [[Match log format]] › **MatchLogFormat** (script)

**Open:** [MatchLogFormat.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/MatchLog/MatchLogFormat.cs)

## What it is

A ~550-line `static class` with `Write(MatchLog) → byte[]` and
`TryRead(byte[], out MatchLog, out string error)`. The tag constants (`HeaderTag = 1` …
`SetupTag = 11`) are the table in [[Match log format]].

## Read it in this order

1. **The header comment:** "Chunk lengths allow unknown tags to be skipped. A changed known layout
   needs a new tag; FormatVersion changes only when the framing changes. All integers are explicitly little-endian."
2. **`Write`:** for each chunk, write the payload into a sub-writer, then `tag`, `length`, payload.
   Writing the payload first is how you know the length.
3. **`TryRead`'s doc comment:** "Reads untrusted bytes without exposing a partial log. Every count
   is bounded by its payload before allocation, including nested commands." That's the same
   defensive discipline as [[InputSerializer]]: check before you allocate.
4. **Required chunks as a bitmask:**
   ```csharp
   const int required = (1 << HeaderTag) | (1 << LoadoutsTag) | (1 << TicksTag);
   // as each chunk is read: seen |= 1 << tag;  at the end: (seen & required) == required
   ```
   One `int` tracks which of up to 32 tags have appeared. It's a compact, classic trick.
5. **`ReadChunk(tag, …)`:** a `switch` on tag. Unknown tags fall through and are skipped by length.
6. **Loadout merging:** LOADOUTS, ERAS and SKINS each fill part of the same two loadouts, "in
   whatever order the chunks arrive".

## Check yourself

> [!question]- Why check a count against the remaining payload *before* allocating an array of that size?
> A hostile file could claim 2 billion commands. Allocating first would exhaust memory and
> crash the server. Bounding first turns it into a clean refusal.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
