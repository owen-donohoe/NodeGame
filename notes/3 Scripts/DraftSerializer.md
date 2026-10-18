---
shell: 3
up: "[[Draft turns]]"
concepts:
  - "[[Endianness]]"
tags: [shell/3, realm/lobby, system/draft, script]
file: Assets/Scripts/Game/Network/DraftSerializer.cs
sources:
  - Assets/Scripts/Game/Network/DraftSerializer.cs
verified: ce9fb6bf
---

# DraftSerializer

> [!question] The question
> How do you put variable-length data (string IDs, era tables) into a packet safely, when the tick packets are all fixed-size?

**Where you are:** [[Home]] › [[Lobby]] › [[Draft]] › [[Draft turns]] › **DraftSerializer** (script)

**Open:** [DraftSerializer.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Network/DraftSerializer.cs)

## What it is

"Follows the same byte-level pattern as [[InputSerializer]]." About 385 lines covering the setup and draft packets.

| Packet | Layout | Size |
|---|---|---|
| MatchSetup | `[type][idLen:1][mapId utf8][boardHash:4][sim:2][balance:4]` | variable, *exact* |
| MatchSetupAck | same body + `[accepted:1]` | variable, exact |
| DraftReady | `[type][playerID:4]` | 5 |
| DraftPlacement | `[type][playerID:4][districtType:4][x:4][z:4][wasTimeout:1]` | 18 |
| DraftLoadout | `[type][playerID:4]` + count-prefixed, length-prefixed UTF-8 suit/node IDs + era tables + skins | variable |

## Lessons

- **Length-prefixing:** every string is `[len:1][bytes]` and every array is `[count:1][items]`. The reader always knows how far to go.
- **Clamp rather than corrupt:** one byte per length caps IDs at 255 bytes, so "an over-long one is clamped rather than allowed to corrupt the offset of everything after it".
- **Exact-length refusal** for MatchSetup: "anything shorter or longer than the layout says is refused."
- **Versioned additions:** era tables and skins arrived in protocol 2, MatchSetup in protocol 5 ([[Numbers on the wire are forever]]).

## Check yourself

> [!question]- Why send the district *type* in DraftPlacement rather than the pick's slot index?
> Slots are local presentation order. Each peer resolves "first unspent pick of this type" itself ([[Draft]]).

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
