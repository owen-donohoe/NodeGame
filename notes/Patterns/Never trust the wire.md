---
shell: pattern
seen-in:
  - "[[Netcode]]"
  - "[[Tick packets]]"
  - "[[Commands]]"
  - "[[CommandProcessor]]"
tags: [pattern, topic/security]
---

# 🔁 Never trust the wire

> [!question] The question
> Your opponent's machine could be running modified code. Which of its claims does Node War
> believe, and which does it check?

**Kind:** pattern. An idea that recurs at different shells of this codebase.

## The pattern

**Anything that crosses a trust boundary (network, file, server request) is untrusted data,
even from your own code. Identity comes from *how* something arrived, never from what it says
about itself. Every action is validated where it takes effect.**

## Where you've seen it

| Where | The claim | What's believed instead |
|---|---|---|
| [[Tick packets]] | "I contain 1,000,000 commands" | Exact length check, division-safe |
| [[Netcode]] `FromPeer` | `playerID` in each command | The slot of the connection it came from |
| [[Netcode]] tick range | `forTick` | Only ticks an honest peer could send |
| [[CommandProcessor]] | "Move villager 7" | Is 7 yours, alive, with a route? |
| Referee (server) | "I won" | A replay of the log |
| Cloud Save | Player records | Only the server writes them; clients read their own |

## What it buys

Cheating reduces to *desync* (caught by hashes) or a *dispute* (caught by the referee),
instead of a silent win.

## What it costs

Checks everywhere, and a known gap: commands aren't signed, so a client could fabricate
*both* sides' logs. Per-player signatures would close it.

## My understanding

<!-- Yours. AI never edits below this line. -->
