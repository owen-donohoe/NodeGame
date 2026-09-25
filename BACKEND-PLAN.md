# Backend, progression and shop: working plan

> **This file is temporary. Delete it.** It holds the context from the
> 2026-09-25 planning session so the work can start without that conversation.
> Once the stages below are entered as Notion **Phases** and **Tasks** (through
> `/update`), and the lasting architecture is written into `docs/`, remove this
> file in its own commit. Nothing here is a source of truth. The code, `docs/`
> and Notion own everything, as CLAUDE.md says. If this file and one of those
> disagree, this file is wrong.

---

## 1. What we are building

Two kinds of progression, and a shop with two currencies:

- **Ranked progression.**
  - **Hidden MMR** (Glicko-2) sets who you play against.
  - **Visible RR** (Valorant-style rank points) moves you through **arenas**.
  - **Arenas are a display and reward tier.** Matchmaking uses MMR, not arena.
  - **Streaks move you fast.** A win streak pushes hidden MMR above your visible rank, so each win pays more RR and you climb quickly. A player winning about 50% stays roughly where they are.
- **Collection progression.**
  - Boxes and box progress unlock new **suits** as you play.
  - **Skins** are owned, previewed in their own panel, and equipped.
- **Shop.**
  - Pages: purchases, daily rewards, deals.
  - **Soft currency** is earned in play; **hard currency** is premium.
  - In-game-currency purchases work for real, using test values.
  - Real-money buttons go nowhere for now.

**All of it is server-authoritative from the start.** The client never decides a balance, a reward, a roll or a rating.

---

## 2. Decisions already made, and why

| Decision | Why |
|---|---|
| **Build on UGS now, not local-first then port** | The costly part of a later port isn't the storage. It's moving *authority*: synchronous "client decides" code becomes async "client asks, server may refuse". Every UI call site would change. The project already had UGS anonymous auth + Relay. |
| **No custom server, no VPS** | Unity hosts everything. The free tiers cover development and a soft launch. A custom server means writing auth, security and operations ourselves. |
| **No UGS Economy** | **Economy stopped accepting new projects on 2026-09-08.** Currencies and purchases are built on **Cloud Code + Cloud Save** instead. Economy had no random rewards or daily timers anyway, so Cloud Code was needed regardless. |
| **Matches stay P2P lockstep; the server referees** | Clients upload the command log at match end, and the server replays it headless to decide the winner. We get authoritative results, replays and dispute evidence without paying for game servers. Server-authoritative *simulation* stays the long-term direction and is still possible later, since the sim is headless and deterministic. |
| **Verify after the match, not every 0.5s** | Cloud Code handles one request at a time, not a persistent stream. The log is tiny (a few bytes per command, most ticks empty), and a replay takes well under a second. The P2P hash check (every 50 ticks) still catches desyncs mid-match. |
| **Glicko-2 for MMR, RR on top** | Glicko-2 tracks uncertainty (RD) and volatility (σ). New players calibrate fast; streaks keep step sizes large; stable players settle. It suits 1v1. Each match is treated as its own rating period. |
| **Environment chosen by build type** | Editor: Project Settings value (`development`). Development build: `development`. Release build: `production`. A test build must never write into production. |
| **Rules in UnityEngine-free C#** | The same code runs in the Cloud Code module and in `dotnet test`. |
| **Client talks to async service interfaces** | Each has a UGS implementation and a **local fake**, used for offline editor work and tests. The fake is permanent tooling, not a temporary backend we later port. |

---

## 3. Current state (end of 2026-09-25)

### Branch

`feat/backend`, based on `feat/rating`, which is based on `main` at `b4cc316f`.
Not pushed. `feat/present-outline` (3 outline/balance commits) is separate and
not in this branch.

| Commit | Contents |
|---|---|
| `ec48892a`, `6f78fd91` | `dotnet/NodeWar.Progression` (netstandard2.1): `Glicko2.cs`, `RankPoints.cs`. `dotnet/NodeWar.Progression.Tests`: 41 tests, including Glickman's worked example (1464.06 / 151.52 / 0.05999). Built by Codex; I reviewed it against the paper. |
| `0a125fd2` | `Assets/Scripts/Backend/GameServices.cs` (`NodeWar.Backend`): the single `EnsureReadyAsync()` for UGS init + anonymous sign-in, with the environment chosen by build type. `NetworkManager` now uses it for both Relay paths. Adds packages `com.unity.services.cloudcode` 2.10.4, `com.unity.services.cloudsave` 3.4.1, `com.unity.remote-config` 4.2.5. The Editor environment is set to `development`. |

The whole `dotnet test dotnet/NodeWar.sln` run is **505 passing** (118 sim, 157
lobby, 189 view, 41 progression). CLAUDE.md still says 464 and doesn't list
the progression project; update it when this branch merges.
`compile-check.ps1` is clean and the open Editor compiled clean.

### UGS project

- Project **"Node"**, id `b0178b5c-011c-4e8c-8913-6ffe4286c614`, org `owendonohoe2020`.
- Environments:
  - `production`: `57a165d3-0f39-40a3-9cc5-30aa0a010663`
  - `development`: `43bfa2d2-5974-435a-b82d-b0af55911d8c`
- UGS CLI 1.9.0 is installed and logged in with a service account. Its config is set to the project above and `environment-name development`.
- Service account roles: **Unity Environments Viewer** (Admin group), plus every Cloud Code, Cloud Save and Remote Config role (LiveOps group). The role picker is multi-select.
- Verified from the CLI (all empty, none refused):
  - `ugs cloud-code modules list`
  - `ugs cloud-save data player list`
  - `ugs fetch <dir> --services remote-config`
- Remote Config has no settings yet. The dashboard's "key" prompt asks for the first **setting name**, not a credential.
- Matchmaker is **not** enabled. It isn't on Unity's list of services usable without a payment method, so enabling it may ask for a card. It isn't needed until Stage 5.
- **Never paste a service-account secret into a chat.** `ugs login` is run by the user in their own terminal.

### Existing local data this replaces

`Assets/Scripts/Lobby/PlayerProfile.cs` saves `player_profile.json` locally,
with `trophies`, `unlockedSuitIDs`, `unlockedNodeIDs`, `boxesAvailable`,
`boxProgress` (a float) and `loadout`, plus local-only `settings` and
`workshopTabIndex`.

- The progression fields move to the server.
- `PlayerProfile` becomes a local copy of the server state, plus settings that stay local.
- `TrophyBarLogic` (the sliding bar display) can be reused for RR.

---

## 4. Architecture

```
dotnet/NodeWar.Progression/      netstandard2.1  rules: Glicko-2, RR, arenas,
                                                 (next) daily-reward schedule,
                                                 box tables, purchase checks
dotnet/NodeWar.Progression.Tests/ net8.0         tests over the rules
<cloud code module project>/     net8.0          Cloud Code C# module, references
                                                 NodeWar.Progression (and later
                                                 NodeWar.Simulation for replays).
                                                 Deployed with the UGS CLI.
Assets/Scripts/Backend/          NodeWar.Backend client side: GameServices,
                                                 service interfaces, UGS impls,
                                                 local fakes
Assets/UI/...                    UI Toolkit      shop / rank / skins / boxes pages
```

- **Cloud Code** runs standard .NET (up to .NET 9). Its Core package targets netstandard2.1, so the rules library loads as-is. It cannot use UnityEngine.
- **Client service interfaces.** All async and all can fail. For example:
  - `IPlayerStateService.GetAsync()`
  - `IShopService.PurchaseAsync(itemId)`
  - `IDailyRewardService.ClaimAsync()`
  - `IBoxService.OpenAsync()`
  - `IMatchReportService.ReportAsync(log)`

  Every call first awaits `GameServices.EnsureReadyAsync()`. The UI shows what the server *returns*, never an optimistic guess.
- **Cloud Save layout.** Player data in the **protected** access class: the player can read it, only the server can write it.
  - `rating` (r, rd, sigma, lastMatchPeriod)
  - `rank` (rr, arena)
  - `wallet` (soft, hard)
  - `inventory` (suits, skins, equipped)
  - `boxes` (available, progress as an integer)
  - `daily` (lastClaimUtc, streak)
- **Catalog in Remote Config.** Prices, box drop tables, daily reward amounts and deal rotation, so tuning needs no build.
- **Server time only.** Daily resets, deal windows and inactivity periods are computed in Cloud Code. The client clock is never trusted.
- **Randomness only on the server.** Box rolls happen in Cloud Code.
- **Nothing here touches `Simulation/`.** Replay verification *reads* the Simulation assembly; it never changes it.

---

## 5. Stages

Rough sizes. "Session" means one focused working window.

### Stage 0: environment. **DONE** (see §3)

### Stage 1: backend skeleton (about one session)

1. Cloud Code module project, net8.0, referencing `NodeWar.Progression`. Find out how the module is packaged and deployed (`ugs deploy` and its module reference / `.ccmr` form), and prove it with a hello-world endpoint called from the Editor.
2. Rules: a daily-reward schedule (streaks, reset time as a pure function of server time and last claim), with tests.
3. Cloud Code function `ClaimDaily`: reads `daily` + `wallet`, applies the rule, writes both, returns the new state. Refuses a claim made too early.
4. Cloud Code function `GetPlayerState`: creates a first-time player's defaults and returns the whole state.
5. Client: interfaces + UGS implementation + local fake in `Assets/Scripts/Backend/`.
6. End to end: claim a daily reward in the Editor against `development`, see it in the dashboard, and see a second claim refused.

### Stage 2: shop and collection (logic small, UI large)

- Catalog in Remote Config: items, prices in soft/hard, deals.
- `Purchase(itemId)`: validate against the catalog, check and deduct the balance, grant the item, all in one server call.
- `OpenBox()`: a server roll against the drop table, granting suits and skins. Box progress is earned from match results (Stage 3).
- Test values for soft and hard currency: a dev-only Cloud Code function to grant currency, or seeded defaults in `development`.
- UI Toolkit pages: shop (purchases, daily rewards, deals), skins panel with preview, box opening, suit unlocks.
- Real-money buttons are placeholders. Unity IAP, with receipts validated in Cloud Code, comes later.
- Migrate `PlayerProfile`'s progression fields to server state.

### Stage 3: match log and referee (one to two sessions; plan mode first)

**Do a spike first:** can a Cloud Code module reference `NodeWar.Simulation`, and does a full match replay finish inside Cloud Code's execution-time limit? The sim already builds under plain `dotnet`, so probably yes. Prove it before building on it.

- **Headless match runner:** builds a `SimulationState` from a match description and runs `SimulateTick` to completion. This is the same component as the headless `MatchFactory` that `docs/direction.md` plans for RL; build it once and use it for both.
- **Match log contents:**
  - simulation version
  - match seed
  - board (as data, not a hardcoded layout)
  - both loadouts
  - draft placements
  - every command with its tick
  - periodic state hashes
- **Upload:** both clients upload at match end. The server replays, gets the winner and final hash, then applies Glicko-2 + RR and box progress.
- **Rage-quitter who never uploads:** the honest player's log is enough, because it holds both players' inputs as that player received them.
- **Lone cheater uploading a made-up log:** at match start the server issues each player a session key, and each player signs their own commands. Any single log is then verifiable.
- **Disagreement:** if two logs don't match, flag it and store both.
- **Desync mid-match:** the existing P2P hash check still halts on a mismatch.

**What the referee does NOT fix.**
- **Hidden information stays exposed.** Both clients hold full state, so if fog of war is ever added, a modified client can reveal it. Only a server-run sim that sends each player just what they can see fixes that.
- **Bots and macros go undetected.** They send valid commands.

These are the reasons server-authoritative *simulation* remains the long-term direction.

### Stage 4: rating wired in

- `ReportMatch` → verified result → `Glicko2.UpdateSingleMatch` for both players → `RankPoints.RRDelta` → floor RR at 0 → arena → rewards.
- **Inactivity:** `Glicko2.DecayForInactivity(idlePeriods)`, with periods counted by the server from the last match time.
- Profile and rank UI; reuse the `TrophyBarLogic` sliding bar for RR.

### Stage 5: matchmaking

- Enable Matchmaker (it may require a payment method on file).
- **Ticket:** MMR (+ region).
- **Rules:** 1v1, MMR gap ≤ N, **relaxing with wait time**. For example: ≤100 at 0s, ≤250 at 20s, anything at 60s. Arena-locked queues would starve while the player base is small.
- **Result:** the multiplayer package's `MatchmakeSessionAsync` returns a Session with Relay already allocated, which feeds the existing Relay path. No game servers.
- **Trust issue:** ticket attributes come from the client, so a client can lie about MMR. Create tickets through Cloud Code (which reads the real MMR from Cloud Save), or check whether Matchmaker rules can read player data server-side.

---

## 6. Rating details

### Glicko-2 in short

- **Rating (r):** best guess at skill, 1500-based.
- **Rating deviation (RD):** how sure the system is. It starts at 350, shrinks with play (floor 30), and grows with inactivity (cap 350).
- **Volatility (σ):** how erratic results are. It rises during streaks, keeping RD, and therefore step size, larger.
- **Update:** roughly RD² × (actual − expected). Expected score discounts the opponent's rating by *their* RD.

### RR in short (`RankPoints.RRDelta`)

- **Implied rating of a rank:** `ImpliedRatingAtZero + RatingPerRR × RR`.
- **Win:** `BaseGain + Divergence × (hiddenR − implied)`.
- **Loss:** the mirror image, so a hidden rating above rank means smaller losses and bigger wins.
- **Rounding:** clamped to [MinGain, MaxGain], then rounded away from zero.

### Placeholder numbers (all to be tuned)

| Setting | Value |
|---|---|
| Arena thresholds | 0 / 300 / 700 / 1200 / 1800 / 2500 |
| BaseGain | 20 |
| MinGain / MaxGain | 8 / 40 |
| Divergence | 0.04 |
| Implied rating | 1000 + 0.5 per RR point |
| Tau | 0.5 |

### Open design questions

- **Minimum loss.** Should a loss always cost at least `MinGain` (8)? Currently yes, even for a hidden rating far above rank.
- **Demotion protection** at arena boundaries? None yet.
- **Placement matches** for new players, or just high initial RD?
- **Arena count and names, and per-arena rewards.**
- **Inactivity period length** (a day? a week?).

---

## 7. Replays and versioning

- **A replay is inputs only, so a simulation change breaks it.** A balance patch (e.g. `07c6021b` halving the claim threshold) makes the same inputs produce a different game. **Converting the format cannot fix that**: those inputs were decisions made against the old rules.
- **Required from day one:** a simulation version stamped into every match log.
- **For saved replays that must survive a breaking patch,** either:
  - **Keep old simulation builds** (separate DLLs) to play old replays. This is cheap because the sim is Unity-free. Or:
  - **Convert to a snapshot replay on demand**, behind a button so unwatched saves are never converted. Run the replay once on the matching old sim and store states at intervals. Playback then needs no simulation.
- **Accepted by the user:** the packet/log format should be forward-compatible enough to change only at major versions, and breaking replays at major versions is acceptable.

---

## 8. Effect on RL

- **The server has no direct effect.** Training runs the headless sim directly.
- **It has two indirect effects, both helpful:**
  - **Shared runner.** The Stage 3 headless match runner *is* most of the RL environment (`docs/direction.md` §3).
  - **Training data.** Ranked match logs can pre-train a bot to imitate human play before reinforcement learning, and can be used to evaluate it.
- **Board redesign matters more to RL than the server does.** The board is moving from 4×7 to something smaller and non-rectangular, and its shape fixes the size and layout of what the model sees and can do. Finish the board before designing RL observations and actions. For the server, the only requirement is that **the board is data carried in the match log**.

---

## 9. Pitfalls checklist

- **Anonymous sign-in is tied to the device.** A reinstall loses the account. Link Google/Apple/Steam sign-in before anything is worth losing.
- **Never trust client-supplied values.** That covers MMR in matchmaking tickets, match results, balances, time and rolls.
- **Cloud Code execution limits:** check them against a full replay (Stage 3 spike).
- **Matchmaker may need a payment method.** Unity's no-card list is Analytics, Cloud Code, Cloud Save, Economy, Relay, Lobby and Vivox.
- **Box progress must be an integer on the server.** It's a float in the local profile today.
- **Keep secrets out of the repo and out of chats.**
- **Don't commit with `done:`** unless a Notion task is actually finished (`/update` marks it Done).

---

## 10. How to run the work

- **Budgets.** Follow `.claude/skills/delegation.md`: at most two Codex agents in flight, in waves, and estimate about 1M tokens per bounded task (about 1.8M for audits) against what's left.
- **What goes to Codex:** mechanical, well-specified pieces with files named in the prompt. Rules with tests (daily schedule, box tables, purchase validation), local fakes, and the Cloud Code hello-world once its layout is known.
- **What stays with Claude:** Cloud Code/UGS integration, the match-log format, the replay verifier, and anything that touches `Network/` or the lockstep path.
- **Stage 3 starts in plan mode** because it touches the lockstep path and reads `Simulation/`.
- **Checks:**
  - `dotnet test dotnet/NodeWar.sln` for rules and lobby changes.
  - `scripts/compile-check.ps1` for Unity-side code.
  - The Editor via the `unity` CLI, when `unity status` shows it ready.
  - `ugs` CLI commands to verify deployed state.
- **Order after this file:**
  1. Stage 1 in full.
  2. Stage 3 spike, which can run in parallel with Stage 2 logic.
  3. Stage 2 UI.
  4. Stage 3.
  5. Stage 4.
  6. Stage 5.

---

## 11. Sources

- UGS billing FAQ (free tiers, no-card services): https://support.unity.com/hc/en-us/articles/6821475035412-Billing-FAQ-Unity-Gaming-Services
- UGS pricing estimator: https://unity-player-services-pricing-estimator.ds.unity3d.com/
- Economy sunset notice: https://discussions.unity.com/t/clarification-on-long-term-support-and-sunset-notice-for-existing-unity-economy-projects/1734744
- Cloud Code C# modules: https://docs.unity.com/ugs/en-us/manual/cloud-code/manual/modules
- Cloud Code .NET version (up to .NET 9): https://discussions.unity.com/t/cloud-code-c-updates-net-version-upgrade-and-module-details-page/952579
- Cloud Code module cost: https://docs.unity.com/en-us/cloud-code/modules/reference/cost
- Cloud Code via UGS CLI: https://docs.unity.com/ugs/en-us/manual/cloud-code/manual/modules/how-to-guides/write-modules/cli
- UGS CLI project roles: https://docs.unity.com/legacy-services-docs/guides/ugs-cli/latest/general/troubleshooting/project-roles/
- UGS CLI login: https://services.docs.unity.com/guides/ugs-cli/latest/general/base-commands/login/
- Unity environments: https://docs.unity.com/en-us/services/service-environments
- Remote Config package: https://docs.unity3d.com/6000.4/Documentation/Manual/com.unity.remote-config.html
- Glicko-2 paper: https://glicko.net/glicko/glicko2.pdf
