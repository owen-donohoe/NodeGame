---
shell: pattern
seen-in:
  - "[[Tick packets]]"
  - "[[InputSerializer]]"
  - "[[Commands]]"
tags: [pattern, topic/serialization]
---

# 🔁 Numbers on the wire are forever

> [!question] The question
> Why are enum values like `Recruit = 5` written out explicitly, and why can't you ever
> renumber them?

**Kind:** pattern. An idea that recurs at different shells of this codebase.

## The pattern

**Once a number has been written to a packet, a save file, a log or a server record, its
meaning is frozen. Add new numbers; never reorder, reuse or change old ones. Version the
format when the layout changes.**

## Where you've seen it

| Number | Persisted in | Rule |
|---|---|---|
| `CommandType` values | Packets, match logs | Pinned explicitly, new values appended |
| `PacketType` values | Every packet's first byte | Bytes 0–2 of the handshake "never change meaning" |
| `ProtocolVersion` (6) | Handshake, server ticket checks | Bumped on any layout change |
| Match-log chunk tags | Saved logs | New payload means a new tag. "A known tag never changes meaning" |
| Input slot IDs | Player settings | Append-only. `LassoSelect = 2` is still "Paint select" |
| Catalog IDs | Server inventory | Retire, don't reuse |

## What it buys

Old logs replay, old settings migrate, and mismatched builds refuse each other cleanly
instead of misreading each other.

## What it costs

Formats accumulate history: gaps, retired values and migration code.

## My understanding

<!-- Yours. AI never edits below this line. -->
