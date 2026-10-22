---
shell: 2
up: "[[Cloud]]"
down:
  - "[[CloudSavePlayerRecordStore]]"
concepts:
  - "[[Key-value stores vs SQL]]"
tags: [shell/2, realm/backend, system/cloud]
sources:
  - dotnet/NodeWarCloud/NodeWarCloud/CloudSavePlayerRecordStore.cs
  - dotnet/NodeWarCloud/NodeWarCloud/CloudSaveMatchRecordStore.cs
  - Assets/Scripts/Backend/Shared/PlayerState.cs
verified: 2ef5a5fb
---

# Player records in Cloud Save

> [!question] The question
> What does a player *look like* on the server, as stored data? Why is it split into eight
> keys instead of one, and who may read or write each one?

**Where you are:** [[Home]] › [[Backend]] › [[Cloud]] › **Player records in Cloud Save** (mechanism)

## The problem

A player's server-side state includes rating, rank, inventory, history, wallet, public handle, win/loss stats, the match they're
in, and penalties. Some changes touch one part (equipping a skin changes only inventory).
Others touch several at once (settlement changes rating, rank, inventory and history). The
storage has to let the player *see* their state, stop them *changing* it, and stop two
server calls from overwriting each other.

## The idea

**Cloud Save is a [[Key-value stores vs SQL|key-value store]]: each player has a set of
named keys, and each value is a JSON document. Node War uses one key per *unit of change*,
in the *Protected* access class (the player reads, only the server writes), and every write
carries the *write lock* from the read it was based on.**

## How it works here

### The player's keys (Protected player data)

```
player 7f3a…
├── rating       { R, Rd, Sigma, LastMatchUnixSeconds, SettledMatchIds[≤200] }
├── rank         { RR, Arena, HighestArena }
├── inventory    { owned variants/skins, chosen suits/nodes, Equipped { base → variant/skin } }
├── history      { MatchIds[≤20], Stats, Summaries[≤20], LastLoadout }   ← summaries carry opponent name/tag, RR change, arena; LastLoadout is the verified log's loadout
├── activeMatch  { matchId, expiresUnixSeconds }          ← "I'm in a match" claim
├── discipline   { level, strike/decay/block times, non-reports, struck IDs }
├── wallet       { Coins, Premium, Recent[≤50 transactions] }   ← changed only through WalletLedger
└── profile      { Name, Tag, NameNorm, last free rename }   ← the public identity
```

### The match's item (Private custom data, server-only)

```
custom item "match-<id>"
├── record       MatchRecord: roster, build/map identity, snapshots, state, reports, outcomes…
├── presence-0   slot 0's latest presence
├── presence-1   slot 1's latest presence
├── log-0        base64 log uploaded by slot 0
└── log-1        base64 log uploaded by slot 1
```

### Cloud Save's access classes

| Class | Player can read | Player can write | Used for |
|---|---|---|---|
| Default | ✔ (own) | ✔ (own) | (not used: a player could edit it) |
| Public | ✔ (anyone) | ✔ (own) | (not used) |
| **Protected** | ✔ (own) | ✘ (server only) | Rating, rank, inventory, history, claims, discipline, wallet, profile |
| **Private** (custom items here) | ✘ | ✘ (server only) | Match records, presence, logs |

(Check the current UGS Cloud Save docs for the exact rules. This is the shape that matters here.)

## Why split into keys?

| Choice | Gain | Pay |
|---|---|---|
| **One key per unit of change** (chosen) | Equip locks only `inventory`; a currency grant locks only `wallet`. Presence writes only its own slot key. Fewer conflicts | Settlement must write 4 keys in one batch, with 4 locks |
| One big `player` document | One read, one write | *Every* change conflicts with every other change, and documents grow without limit |
| A key per item owned | Fine-grained | Hundreds of keys. Reading "the inventory" becomes many reads |

**Bounded lists** keep documents small: history keeps 20 IDs and 20 summaries, the wallet 50 transactions, and the settlement guard 200.

## What it costs later

- **No queries across players.** "Top 100 by RR" or "all players in Arena 5" can't be asked of Cloud Save; that needs Leaderboards or an index.
- **No create-if-absent.** The very first write of a key can't be conditional, so the code includes an *existing* key's lock in the same batch to fence it.
- **Schema changes are migrations.** Field names in these JSON documents are a contract between client and server builds.
- **Cross-player atomicity doesn't exist.** See [[Match settlement]].

## Traps

- **Storing anything fairness-relevant in Default (player-writable) data.**
- **Unbounded lists** in a document (full match history forever). Use a separate store or cap them.
- **Renaming a field** without migrating existing records.

## Check yourself

> [!question]- Why does Presence write only `presence-0` or `presence-1`, never `record`?
> So frequent heartbeats from both players never conflict with each other or with reporting
> and settlement, which write `record`.

> [!question]- The client shows a rating. Where did it come from, and can it be stale?
> From `GetPlayerState` (or a result response), which reads the Protected keys. It's a
> snapshot: if settlement runs afterwards, the client must fetch again. The UI shows *returned* state, not a guess.

## Go deeper ↓

- [[CloudSavePlayerRecordStore]]: the code that reads and writes these
- [[Write locks]]: what the locks are and why every write carries one

## My understanding

<!-- Yours. AI never edits below this line. -->
