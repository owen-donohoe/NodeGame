---
shell: 2
up: "[[Board]]"
down:
  - "[[MatchSetup]]"
  - "[[BoardHasher]]"
  - "[[PremadeMaps]]"
tags: [shell/2, realm/lobby, system/board]
sources:
  - Assets/Scripts/Game/Simulation/MatchSetup.cs
  - Assets/Scripts/Game/Simulation/BoardHasher.cs
  - Assets/Scripts/Game/Simulation/BalanceHasher.cs
verified: ce9fb6bf
---

# Map identity

> [!question] The question
> There are four separate version-like numbers (board hash, balance hash, simulation version, protocol version). What does each one protect against, and why not merge them into one?

**Where you are:** [[Home]] › [[Lobby]] › [[Board]] › **Map identity** (mechanism)

## The problem

"Are we playing the same game?" has several independent answers. Same code but different numbers? Same numbers but different rules? Same rules but a different map? Same everything but a different packet layout? Each breaks things differently and changes for different reasons.

## The idea

**One fingerprint per independent thing that can differ, each owned by one system, all checked before the draft.**

| Identity | Covers | Changes when | Checked by |
|---|---|---|---|
| **Board hash** (`BoardHasher`) | Dimensions, terrain, slot mask, fixed placements, both draft pools, starting numbers, link/path tuning | Someone edits a map | `SetupAgreement`, referee, match record |
| **Balance hash** (`BalanceHasher`) | Every numeric tuning field, suit/district era entries | Balance numbers change (no code change needed) | Handshake `content`, setup, server `BalanceCatalog` |
| **Simulation version** | Rule *behaviour*: the same inputs now give a different result | A rule or tick-0 construction changes | Handshake, setup, replay refusal |
| **Protocol version** (6) | Wire layout | A packet layout changes | Handshake, server ticket checks |

**Why not one number?** A balance-only tweak shouldn't need a simulation-version bump (old replays stay valid under their own balance export). A wire change shouldn't invalidate replays. Separate identities let each change ripple only as far as it must.

## Two hash functions, two choices

| | `SimulationStateHasher` | `BoardHasher` |
|---|---|---|
| Algorithm | Polynomial: `h = h × 31 + v` | **FNV-1a** over whole integers: `h = (h ^ v) × 16777619` |
| Why | Fast, fixed forever because pinned baselines and logs depend on it | "Changing any one value, with everything else fixed, always changes the result: each step is a bijection" |
| Null vs empty | Tags for newer fields | `-1` length for null, so "a board that lost its terrain can never pass for one that was authored empty" |

Neither is cryptographic, and neither needs to be: they detect *disagreement*, not forgery. Forgery is handled by checking against the local catalog ([[Never trust the wire]]).

## Check yourself

> [!question]- A designer changes Farm production from 3 to 4. Which identities change, and can old replays still be verified?
> Only the balance hash. The server keeps the old balance export (content-addressed by hash), so old logs replay under their own numbers. No simulation-version bump is needed.

## Go deeper ↓

- [[MatchSetup]] · [[BoardHasher]] · [[PremadeMaps]]

## My understanding

<!-- Yours. AI never edits below this line. -->
