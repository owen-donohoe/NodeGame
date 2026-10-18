---
shell: concept
used-by:
  - "[[Write locks]]"
  - "[[Match settlement]]"
  - "[[CloudSavePlayerRecordStore]]"
tags: [concept, topic/databases, topic/concurrency]
---

# Optimistic concurrency

> [!question] The question
> When two programs might update the same data at the same time, should they take turns
> (locking), or go ahead and check for a clash at the end?

**Kind:** concept. True outside Node War. Used by [[Write locks]], [[Match settlement]] and [[CloudSavePlayerRecordStore]].

## The idea

**Assume clashes are rare. Read the data along with its version. Work without holding
anything. When writing, say "only if it's still version N". If someone else got there
first, the write fails, so you re-read and try again.**

The alternative, **pessimistic** concurrency, takes a lock first ("I'm editing this; everyone
else wait"), works, then releases.

| | Optimistic | Pessimistic |
|---|---|---|
| Waiting | None. The losers retry | Everyone else waits |
| Best when | Conflicts are rare | Conflicts are frequent, or retries are expensive |
| Failure mode | Repeated retries under heavy contention | Deadlocks, and holders that crash while holding the lock |
| Typical tools | ETags (HTTP `If-Match`), version columns, CAS, Cloud Save write locks | `SELECT … FOR UPDATE`, mutexes, lock services |

## In Node War

Cloud Save write locks *are* optimistic concurrency. Every read returns a `WriteLock`, and a
write with a stale one returns `409 Conflict`. Every server write path is a
read → decide → write → retry-on-conflict loop. See [[Write locks]].

## The rule people forget

**On conflict, redo the decision, not just the write.** The data changed. Your decision might
now be wrong.

## Learn more

- Martin Kleppmann, *Designing Data-Intensive Applications*, chapter 7 (Transactions): lost updates and compare-and-set.
- MDN, [HTTP `If-Match` and ETags](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/If-Match): the same idea on the web.

## My understanding

<!-- Yours. AI never edits below this line. -->
