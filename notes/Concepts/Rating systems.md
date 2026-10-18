---
shell: concept
used-by:
  - "[[Progression]]"
  - "[[Rating and rank]]"
tags: [concept, topic/matchmaking]
---

# Rating systems

> [!question] The question
> How can a number estimate how good someone is from wins and losses alone, and why does
> Node War use Glicko-2 instead of the famous Elo?

**Kind:** concept. True outside Node War. Used by [[Progression]] and [[Rating and rank]].

## Elo in one paragraph

Each player has a rating. The difference between two ratings predicts the win chance (400
points apart means about 10 to 1). After a game, each rating moves by `K × (actual − expected)`.
Beat someone you were expected to beat and you gain little. Beat a favourite and you gain a lot.

## What Glicko-2 adds

| Parameter | Meaning | Effect |
|---|---|---|
| **R** (rating) | Skill estimate (Node War starts at 1500) | Like Elo |
| **RD** (rating deviation) | *How sure* the system is (starts at 350, never below 30 here) | High RD means big updates. A new player places fast |
| **σ** (volatility) | How erratic the player's results are (starts at 0.06) | Streaky players' RD shrinks more slowly |

RD also **grows with inactivity**: after weeks away, the system is less sure and updates you
faster when you return. Node War applies this decay per complete idle week.

## Hidden vs visible

Many modern games (Valorant, League of Legends) keep a *hidden* matchmaking rating and show a
*visible* rank that converges towards it. Node War does the same with RR ([[Rating and rank]]).
That way matchmaking stays honest and the visible number stays motivating.

## Alternatives

| System | Notes |
|---|---|
| Elo | Simple, no uncertainty. Slow to place new players |
| **Glicko-2** | Adds uncertainty and volatility. Standard for 1v1 |
| TrueSkill / TrueSkill 2 (Microsoft) | Designed for *teams* and free-for-all. Relevant for planned 2v2 and FFA |
| OpenSkill | An open-source, TrueSkill-like system for teams |

## Learn more

- Mark Glickman, [The Glicko-2 system (PDF)](http://www.glicko.net/glicko/glicko2.pdf): the original description, with a worked example.
- For 2v2 later: look up TrueSkill and OpenSkill before extending Glicko-2 to teams.

## My understanding

<!-- Yours. AI never edits below this line. -->
