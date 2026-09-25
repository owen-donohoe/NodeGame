# Backend, accounts, match logs, progression: working plan

> **Temporary file. Delete it** once its stages are entered as Notion
> **Phases**/**Tasks** (through `/update`) and the lasting architecture is in
> `docs/`. Remove it in its own commit. Nothing here is a source of truth: the
> code, `docs/` and Notion own everything (CLAUDE.md). If this file disagrees
> with them, this file is wrong.
>
> Written 2026-09-25, second revision. Read all of it before starting any stage.
> Where a question is still open, §13 gives the default to build towards.

---

## 1. What is being built

| Area | Summary |
|---|---|
| Backend | Unity Gaming Services (UGS): Cloud Code + Cloud Save + Remote Config. No custom server. |
| Accounts | Unity Player Accounts (UPA) now, Steam linked later, all resolving to one UGS Player ID. |
| Match integrity | P2P lockstep stays. A server **referee** replays the uploaded match log headless and decides the result. |
| Match log | One versioned, forward-compatible file format. It is the referee's input, the replay, and RL data. |
| Ranked | Hidden Glicko-2 MMR for matching; visible RR moves players through **arenas**. |
| Progression | Arenas are **eras**. Reaching an arena unlocks that era's **variants** of suits and districts. Variants **change gameplay** (deliberate power creep tied to arena). **Skins** are cosmetic only. |
| Matchmaking | Hard cap: ±1 arena. Inside that, an MMR window that widens with queue time. |
| Replays | Stored server-side once, listed for both players, watched in a dedicated scene with play/pause/speed/seek/rewind. |
| Shop + boxes | **On hold.** They come last, as one stage, on top of the catalog and inventory built for progression. |

**Everything that matters is server-authoritative**: balances, unlocks, rolls, ratings, results. The client asks; the server may refuse.

---

## 2. Decisions made, and why

| Decision | Why |
|---|---|
| Build on UGS now, not local-first | The costly part of a later port is moving *authority* (sync "client decides" becomes async "server may refuse"). The project already used UGS auth + Relay. |
| No custom server or VPS | Unity hosts it all. The free tiers cover development and a soft launch. |
| No UGS Economy | Economy stopped accepting new projects on 2026-09-08. Currencies/inventory are built on Cloud Code + Cloud Save. |
| Unity Player Accounts for sign-in | One integration gives cross-device accounts on every platform. Steam is added later by **linking**, so no migration is needed. |
| P2P lockstep + end-of-match referee | Authoritative results, replays and dispute evidence without paying for game servers. Server-authoritative simulation stays possible later (§9). |
| Verify after the match, not live | Cloud Code is request/response, not a stream. The P2P hash check (every 50 ticks) still catches desyncs mid-match. |
| Glicko-2 MMR + RR on top | Glicko-2 tracks uncertainty (RD) and volatility (σ); it suits 1v1. Each match is its own rating period. |
| Variants change gameplay; skins do not | Power creep by era is the progression. Consequently variants live in `Simulation/`, the state hash and the match log; skins never touch any of them. |
| Matchmaking capped to ±1 arena | Power differs between arenas (eras), and MMR does not measure it. |
| Live packets: detect mismatch. Stored logs: stay compatible. | Two different simulations can never play each other, so the wire only needs a clear refusal. Replays outlive builds, so the file format needs versioning. |
| Replay viewer is its own scene | Seeking and rewinding need snapshots and a playback tick provider that the live match does not have. |
| Rules in UnityEngine-free C# | The same code runs in Cloud Code and `dotnet test`. |
| Client uses async service interfaces | Each has a UGS implementation and a **local fake**. The fake is permanent tooling for offline Editor work and tests. |
| Environment chosen by build type | Editor: Project Settings value (`development`). Development build: `development`. Release: `production`. Test builds never write to production. |

---

## 3. Current state (end of 2026-09-25)

### Branch

`feat/backend`, based on `feat/rating`, based on `main` at `b4cc316f`. Not
pushed. `feat/present-outline` (3 outline/balance commits) is separate.

| Commit | Contents |
|---|---|
| `ec48892a`, `6f78fd91` | `dotnet/NodeWar.Progression` (netstandard2.1): `Glicko2.cs`, `RankPoints.cs`. `dotnet/NodeWar.Progression.Tests`: 41 tests, including Glickman's worked example (1464.06 / 151.52 / 0.05999). |
| `0a125fd2` | `Assets/Scripts/Backend/GameServices.cs` (`NodeWar.Backend`): the one `EnsureReadyAsync()` for UGS init + **anonymous** sign-in, environment chosen by build type. `NetworkManager` uses it for both Relay paths. Adds `com.unity.services.cloudcode` 2.10.4, `com.unity.services.cloudsave` 3.4.1, `com.unity.remote-config` 4.2.5. |

- `dotnet test dotnet/NodeWar.sln`: **505 passing** (118 sim, 157 lobby, 189 view, 41 progression). CLAUDE.md still says 464 and doesn't list the progression project. Fix that when the branch merges.
- `compile-check.ps1` is clean.

### UGS project

- Project **"Node"**, id `b0178b5c-011c-4e8c-8913-6ffe4286c614`, org `owendonohoe2020`.
- Environments: `production` `57a165d3-0f39-40a3-9cc5-30aa0a010663`; `development` `43bfa2d2-5974-435a-b82d-b0af55911d8c`.
- UGS CLI 1.9.0 is logged in with a service account, configured to this project and `environment-name development`.
- Service-account roles: Unity Environments Viewer, plus every Cloud Code, Cloud Save and Remote Config role.
- Verified empty and not refused: `ugs cloud-code modules list`, `ugs cloud-save data player list`, `ugs fetch <dir> --services remote-config`.
- Remote Config has no settings. Matchmaker is **not enabled** (it may require a payment method on file).
- **Never paste a service-account secret into a chat.** The user runs `ugs login` in their own terminal.

### Code this work replaces or extends

| Code | Today | Becomes |
|---|---|---|
| `Assets/Scripts/Lobby/PlayerProfile.cs` | Local `player_profile.json`: `username`, `uuid`, `trophies`, `unlockedSuitIDs`, `unlockedNodeIDs`, `boxesAvailable`, `boxProgress` (float), `loadout`, plus local `settings`, `workshopTabIndex` | A local cache of server state, plus settings that stay local. `uuid` becomes the UGS Player ID; `username` becomes the UGS player name; `trophies` becomes RR. |
| `TrophyBarLogic` | Sliding trophy bar | Reused for RR. |
| `Assets/Scripts/Lobby/Data/LoadoutData.cs` | `suitIDs[3]`, `nodeIDs[2]`, strings | Gains a variant (era) per entry and a skin per entry. |
| `Assets/Scripts/Game/Network/InputSerializer.cs` | `Handshake` is 1 byte, no version. `TickInput` is 24 bytes/command with an exact-length check, so a peer on another build is silently dropped as loss. | Versioned handshake with an explicit refusal (Stage 3). |
| `Assets/Scripts/Game/Network/DraftSerializer.cs` | `DraftLoadout` carries suit and node IDs | Also carries variants and skins. |
| `SimulationState.PlayerData.draftedSuits/draftedNodes` | `int` of `SuitType`/`DistrictType` | Needs the era per entry (Stage 6, plan mode). |
| `Assets/UI/Scripts/MatchHistoryPage.cs` | Stub (TV button beside the gear); always empty | The list of stored replays (Stage 7). |
| `Assets/UI/Scripts/SettingsPage.cs` | No account section | Account section (Stage 2). |
| `Assets/UI/Scripts/ShopPage.cs` | Layout pass, fake offers | Untouched until Stage 10. |

---

## 4. Architecture

```
dotnet/NodeWar.Progression/        netstandard2.1  rules: Glicko-2, RR, arenas,
                                                   matchmaking window, catalog
                                                   validation, era unlocks
dotnet/NodeWar.Progression.Tests/  net8.0
<match log library>                netstandard2.1  read/write the match log (§8).
                                                   UnityEngine-free; used by the
                                                   client, Cloud Code and tests
<cloud code module project>/       net8.0          references Progression, the
                                                   match log library, and (after
                                                   the Stage 5 spike) Simulation
Assets/Scripts/Backend/            NodeWar.Backend: GameServices, accounts,
                                                   service interfaces, UGS impls,
                                                   local fakes
Assets/UI/...                      UI Toolkit: account, rank, history, workshop
                                                   pages; replay scene UI
```

- Where the match log library lives: follow the existing pattern in `dotnet/` that compiles Unity-free sources from `Assets/` (look at how the sim and lobby test projects include their sources) rather than inventing a second one.
- **Cloud Code** runs .NET (up to .NET 9) and cannot use UnityEngine.
- **Service interfaces**, all async and all able to fail: `IAccountService`, `IPlayerStateService`, `IInventoryService`, `IMatchReportService`, `IReplayService`, `IMatchmakingService`. Every call awaits `GameServices.EnsureReadyAsync()` first. The UI shows what the server returns, never an optimistic guess.
- **Cloud Save**: player data in the **protected** access class (player reads, only the server writes):
  - `rating` (r, rd, sigma, lastMatchUtc)
  - `rank` (rr, arena, highestArena)
  - `inventory` (variants owned, skins owned, equipped loadout)
  - `history` (last N match IDs)
  - later, for Stage 10: `wallet`, `boxes`, `daily`
- **Catalog**: authored in the Editor (ScriptableObjects). An export step writes it as data that Cloud Code and Remote Config read. **Item IDs are stable strings, never renamed or reused.** If the server doesn't know an ID, it refuses it.
- **Server time only** for anything timed. **Randomness only on the server.**
- **Simulation boundary**: the backend only *reads* `Simulation/` (the referee). Variants (Stage 6) and snapshots (Stage 9) are the two stages that *change* it. Both start in plan mode.

---

## 5. Stages

Order agreed with the user. "Session" means one focused working window.
Matchmaking may move earlier once rating exists (after Stage 7); keep it after reporting.

### Stage 0: environment. **DONE** (§3)

### Stage 1: backend skeleton (about one session)

1. Cloud Code module project (net8.0) referencing `NodeWar.Progression`. Find out how it is packaged and deployed (`ugs deploy` and its module-reference / `.ccmr` form).
2. Cloud Code `GetPlayerState`: creates first-time defaults in Cloud Save and returns the whole state. This is the hello-world. **Do not build `ClaimDaily`**: daily rewards belong to the shop, which is on hold.
3. Client: `IPlayerStateService` + UGS impl + local fake in `Assets/Scripts/Backend/`.
4. End to end: call from the Editor against `development`, see the data in the dashboard and through `ugs cloud-save data player list`.
- Done when: the round trip works and the fake passes the same client-side tests.

### Stage 2: accounts (Unity Player Accounts)

- **Dashboard:** enable Unity Player Accounts as an Authentication identity provider for both environments. The user does this if the CLI cannot.
- **Flow:**
  - First launch stays anonymous (current behaviour).
  - "Link account": `PlayerAccountService.StartSignInAsync()` → on sign-in, `AuthenticationService.LinkWithUnityAsync(accessToken)`.
  - New device: `SignInWithUnityAsync(accessToken)` gives the same Player ID, and so the same Cloud Save data.
  - Verify these API names against the installed `com.unity.services.authentication` version before writing code.
- **Conflict** (the UPA account is already linked to another Player ID): show a dialog with "Switch to that account (this device's anonymous progress is abandoned)" or "Cancel". Never merge.
- **Sign out:** clear the cached credentials. Otherwise the next launch silently signs in as the old anonymous player.
- **Steam, later:** Steamworks.NET → web API auth ticket → `LinkWithSteamAsync` / `SignInWithSteamAsync`. It links to the same Player ID alongside UPA. It needs a Steam App ID configured in the dashboard. If a player links both, either one signs in.
- **Profile identity:** `PlayerProfile.uuid` becomes the UGS Player ID, and `username` becomes the UGS player name.
- **UI, basic first:**
  - `SettingsPage` Account section: status (Guest / signed in as X), Link, Sign out.
  - Conflict dialog.
  - A one-time "link your account" prompt, shown once there is progress worth losing (first ranked result or first unlock).

  The style pass afterwards uses `Tokens.uss` / `Components.uss`. UXML/USS are text and may be edited. Assigning a new layout in the lobby scene is scene wiring: do it through a connected Editor, never by editing `.unity` text.

### Stage 3: versioned handshake (plan mode: touches `Network/`)

- **Constants:**
  - `ProtocolVersion` (wire layout).
  - `SimVersion`: bumped whenever the same inputs would produce a different game.
  - `ContentHash`: over `GameBalanceData` + variant tables.
- **Rule:** when a pinned determinism baseline changes on purpose, bump `SimVersion` in the same commit.
- **Handshake:** carries all three. On mismatch, send `HandshakeReject` with a reason. The UI says "Opponent is on a different version."
- **Lobby:** put the same values in lobby data, so incompatible players never reach Relay.
- **Pairing:** `GameCommand` / `InputSerializer` changes stay paired in one commit (CLAUDE.md), and any wire change bumps `ProtocolVersion`.

### Stage 4: match log format (plan mode)

Format defined in §8. The deliverables:
- A writer that records during a live match.
- A reader.
- Tests: round-trip; an older reader skipping a newer chunk; a truncated file refused.

### Stage 5: referee spike, then headless runner (plan mode: reads `Simulation/`)

1. **Spike first.** Can a Cloud Code module reference the Simulation sources, and does a full match replay finish within Cloud Code's execution-time and memory limits?
   - If yes: continue.
   - If no: fall back to verifying hash checkpoints only, with a full replay run offline on disputes. Record which one and why.
2. **Headless match runner:** builds a `SimulationState` from a match log header and runs `SimulateTick` to completion. It is the same component as the headless `MatchFactory` in `docs/direction.md` (RL). Build it once.

### Stage 6: progression — catalog, eras, skins, inventory (plan mode: changes `Simulation/`)

- **Eras:** arena N = era N (e.g. early → metal → … → magic in arena 4, mastered in 5). Each suit and district has one variant per era, and each variant is a balance entry.
- **Unlock:** reaching arena N grants era-N variants (server-side, in `ReportMatch`). What happens on demotion is §13.
- **Simulation:**
  - Drafted suits/nodes carry their era.
  - Balance lookups become per (type, era).
  - New state fields are registered in `SimulationStateHasher`.
  - Follow `docs/adding-a-feature.md` and `.claude/skills/determinism-guard.md`.
- **Wire:** `DraftLoadout` carries variants (gameplay) and skins (cosmetic). Bump `ProtocolVersion` and `SimVersion`.
- **Skins:** never in `SimulationState`, never hashed, never read by the simulation. They are in the match log only so replays look right, and they travel to the opponent only for display.
- **Server validation:** the referee refuses a result whose loadout used a variant the player doesn't own, or isn't allowed at the match's tier. Better still: the server issues a signed loadout at match start (it ties into §8's session keys), so a modified client can't field unowned variants at all.
- **Catalog:** Editor-authored, exported for the server (§4).
- **Inventory/equip:** Cloud Code `Equip(loadout)` validates ownership and writes `inventory.equipped`.
- **UI:** the Workshop shows variants and locked eras. A skins panel handles preview and equip.

### Stage 7: match reporting, rating, replay storage

- **Flow:** both clients upload the match log at match end. The server replays it, gets the winner and final hash, then:
  `Glicko2.UpdateSingleMatch` both players → `RankPoints.RRDelta` → floor RR at 0 → arena → era unlock → store replay → append to both players' `history`.
- **If only one log arrives** (rage-quit): the honest log is enough, since it holds both players' inputs, each signed by its sender.
- **If two logs disagree:** flag the match, store both, apply no rating change.
- **Inactivity:** `Glicko2.DecayForInactivity(idlePeriods)`, with periods counted by the server from `lastMatchUtc`.
- **Replay storage:** one copy per match ID, not one per player. Both histories point at it. Keep the last N (default 20) per player. Choose between Cloud Save player files and game data in this stage, after checking size and quota limits.
- **UI:**
  - Rank page with the RR bar (reuse `TrophyBarLogic`).
  - `MatchHistoryPage` lists the stored matches. Each opens the Stage 9 replay scene; until then, a "replay coming" state.

### Stage 8: matchmaking

- Enable Matchmaker. This may need a payment method: ask the user first.
- **Tickets are created by Cloud Code**, which reads the real MMR, arena and version from Cloud Save. The client never supplies its own MMR.
- Rules in §7. `MatchmakeSessionAsync` returns a Session with Relay allocated, which feeds the existing Relay path.

### Stage 9: replay viewer scene (plan mode: snapshots touch `Simulation/`)

- **Separate scene.** It reuses the View layer, driven by a playback `ITickProvider` that feeds logged commands instead of network input.
- **Controls:** play/pause, 0.5×/1×/2×/4×, step, seek bar, rewind.
- **Seek and rewind:**
  - Restore the nearest keyframe snapshot at or before the target tick, then simulate forward.
  - Keyframes are taken every K ticks (default 50, aligned with hash checks) and built on load or on first playback, not stored in the log.
- **Snapshot = full `SimulationState` copy.** Every `SimulationState` field must be in both the hasher and the snapshot. Add a test: snapshot → restore → hash equal, and simulate-forward from a restore equals the uninterrupted run.
- **Verification:** the log's hash checkpoints confirm playback matches the original match. On a mismatch, show "replay out of sync" and stop.
- **View:** when seeking, snap and skip interpolation/tweens.
- **Old replays:** a replay whose `SimVersion` differs from the running build is handled per §8 (old sim DLL or snapshot conversion). Until that exists, the history list marks it "unavailable in this version."

### Stage 10: shop + boxes (**ON HOLD**, one stage)

- Wallet (soft/hard), catalog prices in Remote Config, `Purchase(itemId)` (validate, deduct, grant in one call), `OpenBox()` (server roll against a drop table), daily rewards and deals.
- Box progress is an integer on the server.
- Real-money buttons are placeholders; Unity IAP with receipts validated in Cloud Code comes later.
- **Constraint to settle first:** variants change gameplay. If boxes or purchases can grant variants, the game becomes pay-to-win. Default: variants come only from arena progression; boxes and the shop sell skins only.

### Side spike: server-authoritative simulation (after Stage 5, optional)

A local headless `dotnet` host that receives both players' inputs, orders them, runs the sim and broadcasts. No hosting cost. The purpose is to measure feel and effort against §9 before paying for anything.

---

## 6. Rating details

### Glicko-2 (per player, stored)

| Variable | Meaning |
|---|---|
| **r** | Skill estimate, 1500-based |
| **RD** | Certainty. Starts 350, shrinks with play (floor 30), grows with inactivity (cap 350) |
| **σ** | Volatility. Rises on streaks, keeping RD and step size larger |

- **Expected score E** is computed per match, not stored: win probability against this opponent, discounted by *their* RD.
- **Update** ≈ RD² × (actual − E).
- Use `double` in `NodeWar.Progression`. That is fine: it runs on the server, never in `Simulation/`.

### RR (`RankPoints.RRDelta`)

- Implied rating of a rank = `ImpliedRatingAtZero + RatingPerRR × RR`.
- Win = `BaseGain + Divergence × (hiddenR − implied)`. A loss is the mirror image.
- Clamped to [MinGain, MaxGain], rounded away from zero.
- Effect: a hidden rating above the rank means bigger wins and smaller losses. A player at ~50% holds position.

### Placeholder numbers (to be tuned)

| Setting | Value |
|---|---|
| Arena thresholds (RR) | 0 / 300 / 700 / 1200 / 1800 / 2500 |
| BaseGain | 20 |
| MinGain / MaxGain | 8 / 40 |
| Divergence | 0.04 |
| Implied rating | 1000 + 0.5 per RR |
| Tau | 0.5 |

---

## 7. Matchmaking rules

- **Required:** same `ProtocolVersion`, `SimVersion` and `ContentHash`.
- **Hard cap:** the two players' arenas differ by at most 1. Arena here = `rank.arena`. If §13's smurf question is decided otherwise, use `max(arena, arena implied by MMR)`.
- **MMR window**, widening with wait: ≤100 at 0s, ≤250 at 20s, ≤500 at 60s, then unbounded *within the arena cap*.
- **Priority:** prefer same-arena opponents at equal wait. A cross-arena match is the fallback, not the norm.
- **Nobody in range:** keep waiting. After X seconds (default 90), offer an unranked bot match. Never break the arena cap.
- **Why a "bottom half of the arena above / top half below" band adds little:**
  - Power follows the era a player *fields*, and eras step at arena boundaries.
  - An RR band only makes cross-arena matches rarer. When one happens, the power gap is the same full era.
  - MMR already pairs by skill. What it can't see is the era gap.
- **Two real levers for the era gap:**
  - (a) Same-arena preference with a stricter wait before crossing. This is the default above.
  - (b) **Era sync:** a cross-arena match is played at the lower arena's era; the higher player's variants are clamped down. It is fair, but the higher player loses their edge. This is an open decision (§13).
- If (b) is adopted, the arena cap could later relax to ±2, since power no longer differs.

---

## 8. Match log format, replays, versioning

### Why a format of its own

The live wire only needs "same version or refuse". A log outlives builds: it must be readable by later builds, extendable without breaking older readers, and must name the exact simulation that produced it.

### Layout

- **Framing:** little-endian integers, like the existing serializers.
- **File header:** magic `NWML`, `FormatVersion` (u16), then chunks until EOF.
- **Chunk:** `tag (u16) | length (u32) | payload`. A reader skips unknown tags by length. Known tags never change meaning; to change a payload, add a new tag. Bump `FormatVersion` only for a break in the framing itself.

| Chunk | Contents |
|---|---|
| `HEADER` | `ProtocolVersion`, `SimVersion`, `ContentHash`, match ID, both UGS Player IDs, arena/era tier, seed, start time (server-issued) |
| `BOARD` | Board as data (layout, nodes, edges). Never "board #3": the board is being redesigned |
| `LOADOUTS` | Both loadouts: suits, districts, variants, skins |
| `DRAFT` | Draft placements in order |
| `TICKS` | Per tick with commands: tick, count, commands. Empty ticks omitted |
| `HASHES` | (tick, state hash) every 50 ticks |
| `RESULT` | Winner, end tick, final hash, reason (win / surrender / disconnect) |
| `SIGNATURES` | Per player: session-key signature over that player's commands |

- **Signing:** at match start the server issues each player a session key. Each player signs their own commands. Any single uploaded log is then verifiable: a cheater cannot forge the opponent's inputs.
- **Size:** a few bytes per command, with most ticks empty. Compression is optional; measure first.

### Replays across versions

- A replay is inputs only. **A `SimVersion` change breaks it**, and format conversion cannot fix that: the inputs were decisions made against the old rules.
- Options for replays that must survive:
  - **Keep old simulation builds** (separate DLLs, cheap because the sim is Unity-free).
  - **Convert to a snapshot replay on demand** (a button): run once on the matching old sim, store states at intervals, and playback needs no sim.
- The user accepts that replays break at major versions.
- **Default:** mark old replays unavailable. Build conversion only when asked.

---

## 9. Referee vs server-authoritative simulation

| | P2P lockstep + referee (building this) | Server-authoritative simulation |
|---|---|---|
| Cost | ~Free: one Cloud Code call per match | Game-server hosting per match-minute. Cloud Code cannot host it |
| Trusted result | Yes, after the match | Yes, live |
| Rage-quit | Honest log suffices (signed commands) | Server continues; reconnect is easy |
| Hidden info (fog of war) | **Exposed**: both clients hold full state | Protected: each client gets only what it can see |
| Bots/macros | Undetected | Undetected |
| Feel | Unchanged | Similar: lockstep already has input delay; 10Hz suits a server |
| Operations | None | Regions, scaling, uptime |

- **Direction:** the referee now. Server-authoritative simulation later, if fog of war or live anti-cheat becomes a requirement.
- **What carries over:** the match log format and headless runner are shared by both, so a later switch changes the transport, not the game.
- **Before paying for hosting:** run the side spike (§5).

---

## 10. Effect on RL

- **The server has no direct effect:** training runs the headless sim.
- **Two indirect benefits:**
  - The Stage 5 runner *is* most of the RL environment (`docs/direction.md` §3).
  - Ranked match logs are imitation-learning and evaluation data.
- **Eras add a dimension** to what a model sees and does (variant per slot). Design RL observations after the board redesign and the era model settle.

---

## 11. Pitfalls

- **Account linking must work before any progress is worth losing.** An anonymous account dies with a reinstall.
- **Never trust client values:** MMR in tickets, results, loadouts/variants, balances, time, rolls.
- **Variants are simulation changes.** Each one goes through the full determinism checklist, and each one bumps `SimVersion`.
- **Skins must never reach `SimulationState`.** If a skin ever affects an outcome, the design is broken.
- **Replay snapshots must cover every `SimulationState` field.** A missed field makes rewind silently wrong. The snapshot round-trip test guards this.
- **Cloud Code limits** (execution time, memory, payload size) decide the referee design. The spike comes first.
- **Matchmaker may need a payment method.** Ask before enabling it.
- **No secrets in the repo or in chats.**
- **Don't commit with `done:`** unless a Notion task is finished.
- **Don't stamp `verified:` / `verified_at_commit`** on docs. That is the user's act.

---

## 12. How to run the work

**To be agreed with the user before any stage starts.** They want a deliberate
split between the main session and subagents: avoid re-reading and re-planning
costs, and avoid over-delegating (hallucination, context compaction). Until
that is settled, `.claude/skills/delegation.md` governs: at most two agents in
flight, in waves, with cost estimated first.

Standing constraints:
- **Plan mode first:** Stages 3, 4, 5, 6, 9 (network path, match log, `Simulation/` reads or changes).
- **Checks:**
  - `dotnet test dotnet/NodeWar.sln` for rules, lobby, log format and sim.
  - `scripts/compile-check.ps1` for Unity-side code.
  - The Editor via the `unity` CLI when `unity status` is ready. Scene wiring is done this way, never by editing YAML.
  - `ugs` CLI for deployed state.
- **Well suited to delegation** (mechanical, specified, testable): rules with tests (matchmaking window, era unlocks, catalog validation), local fakes, log reader/writer tests once the format is fixed, UXML/USS style passes.
- **Keep in the main session:** Cloud Code/UGS integration, account flow, handshake, match log format design, referee, anything in `Network/` or `Simulation/`.

---

## 13. Open questions, with defaults to build towards

| Question | Default until decided |
|---|---|
| Era sync in cross-arena matches (§7 b)? | No. Same-arena preference, ±1 cap |
| Demotion below an arena: keep that era's variants? | Keep ownership; usable only while in or above that arena |
| Smurfs (high MMR, low arena): which arena counts for the cap? | `rank.arena` |
| Minimum RR loss (currently always ≥ MinGain)? | Keep |
| Demotion protection at arena boundaries? | None |
| Placement matches, or just high starting RD? | High starting RD only |
| Arena count, names, era themes, per-arena rewards | 6 arenas (thresholds §6); names/themes TBD by the user |
| Inactivity period length | 1 week |
| Replay retention per player | Last 20 |
| Bot fallback in queue | Unranked, offered after 90s |
| Can boxes/shop grant variants? | No: skins only |
| Replay conversion across `SimVersion` | Not built; old replays marked unavailable |

---

## 14. Sources

- UGS billing FAQ (free tiers, no-card services): https://support.unity.com/hc/en-us/articles/6821475035412-Billing-FAQ-Unity-Gaming-Services
- UGS pricing estimator: https://unity-player-services-pricing-estimator.ds.unity3d.com/
- Economy sunset notice: https://discussions.unity.com/t/clarification-on-long-term-support-and-sunset-notice-for-existing-unity-economy-projects/1734744
- Cloud Code C# modules: https://docs.unity.com/ugs/en-us/manual/cloud-code/manual/modules
- Cloud Code .NET version: https://discussions.unity.com/t/cloud-code-c-updates-net-version-upgrade-and-module-details-page/952579
- Cloud Code module cost: https://docs.unity.com/en-us/cloud-code/modules/reference/cost
- Cloud Code via UGS CLI: https://docs.unity.com/ugs/en-us/manual/cloud-code/manual/modules/how-to-guides/write-modules/cli
- UGS CLI project roles: https://docs.unity.com/legacy-services-docs/guides/ugs-cli/latest/general/troubleshooting/project-roles/
- UGS CLI login: https://services.docs.unity.com/guides/ugs-cli/latest/general/base-commands/login/
- Unity environments: https://docs.unity.com/en-us/services/service-environments
- Remote Config package: https://docs.unity3d.com/6000.4/Documentation/Manual/com.unity.remote-config.html
- Unity Authentication (identity providers, Unity Player Accounts, Steam, linking): https://docs.unity.com/ugs/en-us/manual/authentication/manual/overview
- Glicko-2 paper: https://glicko.net/glicko/glicko2.pdf
