---
shell: concept
used-by:
  - "[[Match settlement]]"
  - "[[MatchSettler]]"
  - "[[Backend client]]"
  - "[[Commands]]"
tags: [concept, topic/distributed-systems]
---

# Idempotency

> [!question] The question
> Your request timed out. Did it happen or not? If you send it again, will the player get
> charged, or rewarded, twice?

**Kind:** concept. True outside Node War. Used by [[Match settlement]], [[MatchSettler]], [[Backend client]] and [[Commands]].

## The idea

**An operation is *idempotent* if doing it twice has the same effect as doing it once. Make
every operation that might be retried idempotent, and retrying becomes safe, which means you
can *always* retry.**

Over a network, "did my request succeed?" often has no answer: the request may have arrived
and the *response* been lost. Idempotency turns that unanswerable question into a harmless one.

## Three ways to get it

| Technique | Example | In Node War |
|---|---|---|
| **Set, don't add** | "Set RR to 340", not "add 20" | `SetAutoRecruit value = 0/1` (absolute toggle). `Collect` start/cancel |
| **Remember what you've done** (idempotency key) | Store processed request IDs and skip repeats | `RatingRecord.SettledMatchIds`: settlement skips a match already applied |
| **Return the existing result** | Second "create" returns the first object | Allocation: "idempotent retries return the existing record" |

Also: `DraftAck` is *cumulative* ("I have N placements"), so a later ack covers any earlier lost one.

## Traps

- **"Add" operations retried blindly**: double rewards, double charges.
- **An idempotency key stored separately from the change.** A crash between the two breaks it.
  Node War keeps the guard *in the same atomic batch* as the change.
- **Bounded memories.** If the "seen" list is capped, very old repeats aren't caught. Make sure another mechanism (terminal states) covers them.

## Learn more

- Stripe engineering, [Designing robust and predictable APIs with idempotency](https://stripe.com/blog/idempotency): the classic explanation.

## My understanding

<!-- Yours. AI never edits below this line. -->
