---
shell: 3
up: "[[Build identity handshake]]"
concepts:
  - "[[State machines]]"
tags: [shell/3, realm/match, system/netcode, script]
file: Assets/Scripts/Game/Network/SetupAgreement.cs
sources:
  - Assets/Scripts/Game/Network/SetupAgreement.cs
verified: ce9fb6bf
---

# SetupAgreement

> [!question] The question
> How do two machines agree on one map over a lossy network, in 150 lines, with no clock and no Unity?

**Where you are:** [[Home]] › [[Match]] › [[Netcode]] › [[Build identity handshake]] › **SetupAgreement** (script)

**Open:** [SetupAgreement.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Network/SetupAgreement.cs)

## What it is

A tiny two-sided protocol object. Create it with `Host(proposal)` or `Guest(catalog, sim,
balance, expectedMapId)`. Then the caller's retry timer calls `NextToSend()`, and every
received packet goes to `Receive(packet)`, which may return a reply. State: `Agreed`, `Refused`, `Error`, `Setup`.

## Read it in this order

1. **The factory methods** `Host` / `Guest`. The constructor is private, so you can't build a half-configured one.
2. **`NextToSend`.** Only the host sends, and only until agreed or refused. Resending is the caller's job, which makes it *loss-tolerant by repetition*.
3. **`Receive` → `ReceiveProposal` (guest).**
   - Already refused: refuse again.
   - Already agreed: the same proposal gets "yes" again (an idempotent answer to a lost ack). A different one is a conflict, so refuse.
   - Otherwise: `MatchSetup.Verify` against the catalog, sim and balance, plus the allocated map check, then agree.
4. **`ReceiveAck` (host).** It must acknowledge *exactly* what was proposed. Late or repeated acks after a decision change nothing.

## Patterns visible here

- 🔁 [[Pure core, Unity shell]]: "Pure data in, bytes out, no clocks and no Unity."
- 🔁 [[Make retries safe]]: repeated proposals and acks give the same answers.
- 🔁 [[Never trust the wire]]: the guest trusts its own catalog, not the host's board.

## Check yourself

> [!question]- The guest's "accepted" ack is lost. What happens next?
> The host is still not agreed, so `NextToSend` keeps returning the proposal. The guest
> receives the same proposal while agreed, and answers "accepted" again. Eventually an ack gets through.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
