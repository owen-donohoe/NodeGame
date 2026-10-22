---
shell: 3
up: "[[Netcode]]"
concepts:
  - "[[UDP vs TCP]]"
  - "[[NAT and relay servers]]"
  - "[[Threads and the main thread]]"
tags: [shell/3, realm/match, system/netcode, script]
file: Assets/Scripts/Game/Network/NetworkManager.cs
sources:
  - Assets/Scripts/Game/Network/NetworkManager.cs
  - Assets/Scripts/Game/Network/NetworkManagerTransport.cs
verified: 56ab2583
---

# NetworkManager

> [!question] The question
> Where do bytes actually leave the machine? And why are there two completely different ways of sending them?

**Where you are:** [[Home]] › [[Match]] › [[Netcode]] › **NetworkManager** (script)

**Open:** [NetworkManager.cs in VS Code](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Network/NetworkManager.cs) · [NetworkManagerTransport.cs](vscode://file/C:/Dev/NodeGame/Assets/Scripts/Game/Network/NetworkManagerTransport.cs)

## What it is

The Unity-side transport, a MonoBehaviour with two modes:

| Mode | How | When |
|---|---|---|
| **DirectUDP** | .NET `UdpClient` on port 7777, a background receive thread, a locked queue | LAN or testing, when the two machines can reach each other directly |
| **UnityRelay** | Unity Transport `NetworkDriver` through a Relay allocation, **DTLS-encrypted** | Online play, so phones behind home routers can connect ([[NAT and relay servers]]) |

`NetworkManagerTransport` (48 lines) adapts it to the `ILockstepTransport` interface that the
Unity-free [[LockstepCore]] needs: `Send`, `ReceiveAll`, `Flush`.

## Read it in this order

1. **Fields:** `incomingQueue` + `queueLock`, `outgoingQueue` (sends attempted before the
   connection is ready are buffered, up to 256), `relayDriver`, `JoinCode`.
2. **`StartAsRelayHost`:** `CreateAllocationAsync(1)` → `GetJoinCodeAsync` → `ToRelayServerData("dtls")`
   → `NetworkDriver.Create` → `Bind` → `Listen`. The join code is what the backend passes to the guest ([[Backend]] rendezvous).
3. **`StartAsRelayClient(code)`:** `JoinAllocationAsync(code)` → `Connect`.
4. **`StartAsHost` / `StartAsClient` (DirectUDP):** a `UdpClient` and `StartReceiveThread`. The
   receive thread blocks on the socket and pushes packets into the locked queue. The main thread drains it ([[Threads and the main thread]]).

## Things to notice

- **The relay Start methods return `Task<bool>`** (false means setup failed or was cancelled; `RelayError` says why). An `AttemptGate` counts attempts and each one checks it after every `await`, so `Shutdown` or a newer start leaves a stale setup building nothing. The launcher observes the task and fails its phase at once instead of waiting out a deadline. The old `async void` hid failures; the cost now is remembering the check after every new `await`.
- **Sends before ready are queued, not dropped**, so the handshake can be "sent" before Relay finishes connecting.
- **DirectUDP reads only the connected endpoint**: packets from other addresses are ignored (netcode card invariant).

## Check yourself

> [!question]- Why does DirectUDP need a background thread while LockstepCore never touches threads?
> `UdpClient.Receive` *blocks* until a packet arrives. On the main thread that would freeze the
> game. The thread waits instead, queues the bytes, and the main thread collects them each
> frame, so the core stays single-threaded and deterministic.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
