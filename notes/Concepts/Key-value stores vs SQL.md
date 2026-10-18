---
shell: concept
used-by:
  - "[[Backend]]"
  - "[[Cloud]]"
  - "[[Player records in Cloud Save]]"
  - "[[CloudSavePlayerRecordStore]]"
tags: [concept, topic/databases]
---

# Key-value stores vs SQL

> [!question] The question
> Node War stores player data without SQL. What *is* SQL, what would this game's data look
> like in a SQL database, and when would switching be worth it?

**Kind:** concept. True outside Node War. Used by [[Backend]], [[Cloud]], [[Player records in Cloud Save]] and [[CloudSavePlayerRecordStore]].

## Two ways to store data

| | Relational (SQL) | Key-value / document |
|---|---|---|
| Examples | PostgreSQL, MySQL, SQLite, SQL Server | **UGS Cloud Save**, Redis, DynamoDB, Firestore |
| Shape | **Tables** of rows with fixed columns (a schema) | **Keys** pointing to values (often JSON documents) |
| Relationships | **Foreign keys and JOINs** link tables | You store IDs and look them up yourself |
| Asking questions | **SQL queries**: filter, sort, group, join anything | Get by key (plus whatever indexes the product offers) |
| Multi-record changes | **Transactions** (ACID): many rows change together or not at all | Usually per key or per batch. Cross-entity atomicity is rare |
| Schema changes | Migrations (`ALTER TABLE`) | Just write new JSON. Readers must handle old shapes |
| Scaling | Vertical first. Sharding is hard work | Horizontal by design |
| Operations | You (or a managed service) run the database | Fully managed |

**ACID** in one line each: **A**tomic (all or nothing), **C**onsistent (rules hold), **I**solated
(concurrent transactions don't see each other half-done), **D**urable (once committed, it stays).

## Node War's data, imagined in SQL

```sql
CREATE TABLE players (
  id            TEXT PRIMARY KEY,
  rating_r      DOUBLE PRECISION, rating_rd DOUBLE PRECISION, rating_sigma DOUBLE PRECISION,
  rr            INT NOT NULL DEFAULT 0,
  arena         INT NOT NULL DEFAULT 0,
  highest_arena INT NOT NULL DEFAULT 0
);
CREATE TABLE owned_items (
  player_id TEXT REFERENCES players(id),
  item_id   TEXT,                               -- e.g. 'suit.archer.e2'
  PRIMARY KEY (player_id, item_id)
);
CREATE TABLE matches (
  id TEXT PRIMARY KEY, state TEXT, created_at TIMESTAMPTZ,
  player0 TEXT REFERENCES players(id), player1 TEXT REFERENCES players(id)
);
CREATE TABLE match_reports (
  match_id TEXT REFERENCES matches(id), player_index INT,
  accepted BOOL, winner INT, end_tick INT, final_hash INT
);
```

Settlement would become **one transaction across both players**:

```sql
BEGIN;
  SELECT * FROM matches WHERE id = $1 FOR UPDATE;          -- lock the match row
  UPDATE players SET rr = rr + 40, ... WHERE id = $winner;
  UPDATE players SET rr = GREATEST(rr - 8, 0), ... WHERE id = $loser;
  UPDATE matches SET state = 'Settled' WHERE id = $1;
COMMIT;                                                    -- all four changes, or none
```

And a leaderboard is one query:

```sql
SELECT id, rr FROM players ORDER BY rr DESC LIMIT 100;     -- needs an index on rr
```

## How Node War does the same things with Cloud Save

| Need | SQL way | Node War's Cloud Save way |
|---|---|---|
| Update two players together | One transaction | Per-player batches plus [[Idempotency]] guards ([[Match settlement]]) |
| Don't lose concurrent updates | Row locks / isolation | [[Optimistic concurrency]] with write locks ([[Write locks]]) |
| Owned items | A table with one row per item | A JSON list inside the `inventory` key |
| Match history | A query on `match_reports` | A capped list of 20 IDs plus the match items |
| Leaderboard | `ORDER BY rr` | Not possible directly. Use UGS Leaderboards |
| Analytics ("win rate by suit") | A SQL query | Export or Analytics events |

## Why Node War doesn't use SQL (today)

- **No server to run.** A SQL database needs hosting, backups, patching, connection limits and security. UGS is managed.
- **The access pattern is "one player's records by ID".** Key-value stores are ideal for exactly that.
- **The hard part, cross-player atomicity, has a workable substitute** (idempotency plus pre-match snapshots), at the cost of more careful code.

## When it would be worth it

- You need **queries across players** beyond what Leaderboards offer (social features, guild membership, search).
- **Transactions across players** start multiplying (trading, gifting, guild banks). Every one of them needs idempotency logic in a key-value world.
- You need **analytics on live data**. (Often the answer is a separate SQL or warehouse copy, not moving the primary store.)

A common hybrid: keep the key-value store for per-player state and add a SQL database for
the relational, social part.

## Traps

- **Modelling relations in a key-value store** (a "guild" document listing members *and* each member listing guilds). You get two copies that drift apart.
- **Unbounded documents.** Values have size limits. Cap lists or split them into keys.
- **Assuming a batch is a transaction across players.** In Cloud Save, it isn't.

## Check yourself

> [!question]- In SQL, "own an item" is a row. In Node War, it's an entry in a JSON list. Name one advantage of each.
> Row: you can query "who owns item X?" and add or remove one item without rewriting the
> rest. JSON list: one read gets the whole inventory, and one conditional write updates it.

> [!question]- What does Node War use instead of a cross-player transaction, and what extra rule makes it safe?
> Per-player atomic batches with an idempotency guard (`SettledMatchIds`), computed from frozen
> pre-match snapshots. The one-active-match claim stops another match from changing the same records in between.

## Learn more

- [SQLBolt](https://sqlbolt.com/): free interactive SQL lessons. The best first step.
- [Use The Index, Luke](https://use-the-index-luke.com/): how indexes make queries fast.
- Martin Kleppmann, *Designing Data-Intensive Applications*: *the* book on storage trade-offs
  (chapters 2 and 7 cover data models and transactions).
- Unity, *Cloud Save* documentation: access classes, write locks, limits.

## My understanding

<!-- Yours. AI never edits below this line. -->
