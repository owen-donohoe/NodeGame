---
shell: 2
up: "[[Netcode]]"
down:
  - "[[InputSerializer]]"
  - "[[LockstepCore]]"
concepts:
  - "[[UDP vs TCP]]"
  - "[[Endianness]]"
tags: [shell/2, realm/match, system/netcode]
sources:
  - Assets/Scripts/Game/Network/InputSerializer.cs
  - Assets/Scripts/Game/Network/LockstepCore.cs
  - Assets/Scripts/Game/Simulation/Commands.cs
verified: ce9fb6bf
---

# Tick packets

> [!question] The question
> What exactly travels between the two machines ten times a second, and why is each piece
> of it sent seven times?

**Where you are:** [[Home]] › [[Match]] › [[Netcode]] › **Tick packets** (mechanism)

## The problem

Every 100 ms each machine must get one message to the other: "here is what I did on
tick N." The connection uses [[UDP vs TCP|UDP]], which loses some messages and doesn't
retry. If the message for tick N is lost, the opponent can't run tick N, so their game
freezes until something fills the gap.

## The idea

**Each tick's input is a tiny byte array with a fixed layout, and it is sent again
alongside each of the next six inputs. A lost copy costs nothing, because another copy
is already 100 ms behind it.**

## How it works here

### What's in one packet

```
byte  0       PacketType       2 = TickInput
bytes 1–4     forTick          which tick these commands belong to
bytes 5–8     stateHash        0, except every 50th tick (see Desync detection)
bytes 9–12    commandCount     how many commands follow
byte  13      senderDelay      "my inputs run 2 ticks ahead"
byte  14      requestedDelay   "please run yours 3 ahead" (0 = no opinion)
─────────────  15-byte header ─────────────
then 24 bytes per command:
    type · playerID · villagerID · targetNodeID · issuedOnTick · value
    (six 4-byte integers)
```

Worked sizes:

| What you did that tick | Commands | Bytes |
|---|---|---|
| Nothing (most ticks) | 0 | 15 |
| Sent one villager to a node | 1 | 39 |
| Lassoed 10 villagers onto a node | 10 | 255 |
| Lassoed a full army of 25 | 25 | 615 |

### Seven copies

Each input travels in its own packet (a *datagram*). Every tick, `LockstepCore` sends the
new input **plus a fresh copy of each of the previous six**:

| Sent on tick | Fresh input | Copies riding along |
|---|---|---|
| 100 | for 102 | 101, 100, 99, 98, 97, 96 |
| 101 | for 103 | 102, 101, 100, 99, 98, 97 |
| 102 | for 104 | 103, 102, 101, 100, 99, 98 |

Follow the input for **102** down the table: it leaves on tick 100, again on 101, again
on 102, and so on, seven times in all. If the first one is lost, the next copy is already
100 ms behind it. The receiver keys inputs by `forTick` and ignores duplicates.

### Why six copies

`REDUNDANT_INPUTS = 6` was measured, not guessed. The network tests replay a minute of
play over a simulated bad connection (6 random seeds) and count how long the game froze:

| Copies | Frozen under 30% random loss | Frozen under bursty loss | Bandwidth, clean link |
|---|---|---|---|
| 2 | 9.2 s / min | 12.1 s / min | 0.6 KB/s |
| 4 | 2.1 | 6.9 | 1.0 |
| **6** | **1.2** | **3.4** | **1.4** |
| 8 | 1.2 | 2.0 | 1.7 |

Under random loss, going from 6 to 8 copies bought nothing. Bursty loss still improves
at 8, but six was chosen as the balance point. 1.4 KB/s is tiny by modern standards.

### Reading defensively

Bytes from the network are untrusted. `InputSerializer.TryDeserialize` refuses a packet
unless its length matches its declared command count *exactly*, and it checks the count
by dividing rather than multiplying, so a hostile count can't overflow its way past the
check. It also rejects unknown command types. A refused packet is treated as **lost**,
which the redundancy above already survives. `LockstepCore` then discards ticks that are
too far behind or ahead to be honest, and stamps the sender's slot onto every command.

## Why this, not that

| Option | What you'd gain | What you'd pay | Verdict here |
|---|---|---|---|
| **Hand-written fixed binary layout** (chosen) | Smallest possible packet, no allocations or parsing surprises, and identical bytes on every platform | Any change needs a version bump and lockstep changes in three places | Chosen. The layout is tiny and rarely changes |
| JSON or other text | Readable, easy to extend | A single Move becomes about 100 bytes instead of 24, parsing allocates memory, and text invites number-formatting differences between machines | Too big and too loose for 10 messages a second |
| A serialization library (MessagePack, protobuf) | Built-in schema evolution and less hand-written code | A dependency, and less control over exact bytes | Worth it if the packet types multiply |
| `BitConverter` / copying raw struct memory | Less code | Byte order and struct padding depend on the machine | Banned. Explicit shifts mean the same bytes everywhere ([[Endianness]]) |
| **Copies sent in advance** (chosen) | Recovers a loss with no round trip | 7× the packets | Chosen. The packets are tiny |
| Acknowledge and resend | Fewer packets | Recovery waits a full round trip, *and* the game is stalled while it waits | Still used, but only as a fallback when the game is already stalled (`ResendIfNeeded`) |

## What it costs later

- **The layout is a contract with three parties:** the other peer, the server's version
  check, and every saved match log. Changing `GameCommand` means changing
  `InputSerializer`, bumping `ProtocolVersion` (now 6) and adding a new match-log tag,
  all in one commit.
- **Every command is 24 bytes, whatever its type.** A field added for one command type
  grows all of them.
- **Seven packets where one would do.** The code itself notes that bundling the copies
  into one packet per tick would keep the same protection with a seventh of the packets.
  That is a cheap future optimisation.
- **Open question (not verified):** the 64-commands-per-tick cap allows a
  15 + 64 × 24 = 1,551-byte packet. That is bigger than the classic 1,500-byte Ethernet
  frame, and Unity Transport has its own payload limit. A full lasso (615 bytes) is far
  below it, but nobody has yet checked what happens at the cap.

## Traps

- **Renumbering `CommandType`.** The numbers *are* the wire format and the log format.
  That is why `Recruit = 5` and the rest are pinned explicitly. Add new values at the
  end; never reorder or reuse them.
- **Changing `GameCommand` without the serializer** in the same commit (`.claude/rules/network.md`).
- **Trusting `playerID` from the wire.**
- **Reading before checking the length.** That is how a malformed packet turns into a crash.

## Patterns you've seen before

- 🔁 [[Send intent, not results]]: 24 bytes describe a move, never a position.
- 🔁 [[Treat bad input as a case you already handle]]: malformed = lost.
- 🔁 [[Constants come from measurements]]: the copies table above.
- 🔁 [[Numbers on the wire are forever]]: enum values, packet types and log tags never change meaning.

## Check yourself

> [!question]- How many bytes is the packet on a tick where you did nothing?
> 15: just the header, with `commandCount = 0`. Even an idle player sends every tick,
> because "I did nothing on tick N" is information the opponent needs before running tick N.

> [!question]- The first packet carrying the input for tick 102 is lost. When does the opponent get it, and does the game freeze?
> A copy leaves 100 ms later with tick 101's send. The input was made 200 ms before tick 102
> runs (an input delay of 2), so if the one-way trip is under about 100 ms the copy still
> arrives in time and nobody notices anything.

> [!question]- Why does `Serialize` throw on an unknown command type instead of sending it?
> The receiver would reject the whole packet, so every copy would be refused too, and the
> opponent would stall on that tick. Failing loudly on the sending side turns a mysterious
> network freeze into an obvious local bug.

> [!question]- Why does the receiver check `commandCount > (length − 15) / 24` instead of `15 + commandCount × 24 > length`?
> A hostile `commandCount` near `int.MaxValue` makes the multiplication overflow to a small
> or negative number, which would pass the check. Division can't overflow.

## Go deeper ↓

- [[InputSerializer]] ✅ (pilot): the code that writes and reads these bytes
- [[LockstepCore]]: not yet written (where the copies are sent: `GenerateAndSendLocalInput`, `ResendIfNeeded`)

Sideways → [[UDP vs TCP]] · [[Endianness]]

## My understanding

<!-- Yours. AI never edits below this line. -->
