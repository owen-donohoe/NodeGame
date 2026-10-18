---
shell: concept
used-by:
  - "[[NetworkManager]]"
  - "[[Netcode]]"
tags: [concept, topic/networking]
---

# NAT and relay servers

> [!question] The question
> Two phones on home Wi-Fi want to talk directly. Why can't they just send packets to each
> other's IP address, and what does Unity Relay do about it?

**Kind:** concept. True outside Node War. Used by [[NetworkManager]] and [[Netcode]].

## The problem

Most devices don't have a public IP address. A home router uses **NAT** (Network Address
Translation): every device in the house shares one public address, and the router lets a reply
come *in* only after a device inside sent something *out*. So when phone A sends to phone B's
public address, B's router has no idea which device inside the house it's for, and drops the packet.

## The options

| Option | How | Trade-off |
|---|---|---|
| Port forwarding | The user configures the router | Unrealistic for players |
| **NAT punch-through** | Both sides send to each other simultaneously, coordinated by a server, so each router "expects" the reply | Free bandwidth, but fails on some router types (symmetric NAT, mobile carriers) |
| **Relay server** | Both connect *out* to a public server, which forwards packets between them | Always works. Adds a hop of latency and costs the host money per bandwidth |
| Dedicated game server | Both connect to a server that runs the game | Always works. Expensive and a different architecture |

## In Node War

**Unity Relay**, with DTLS encryption:
1. The host asks Relay for an *allocation* and gets a short **join code**.
2. The join code reaches the guest through the backend (`Rendezvous` writes it into the match record).
3. The guest joins the allocation with that code. Both are now connected *outwards* to Relay, which forwards packets.

The game stays peer-to-peer *logically* (lockstep, no server simulation), while the packets
physically travel through Relay. DirectUDP remains for LAN and testing.

## Learn more

- Unity docs, *Relay overview*
- Wikipedia, [NAT traversal](https://en.wikipedia.org/wiki/NAT_traversal)

## My understanding

<!-- Yours. AI never edits below this line. -->
