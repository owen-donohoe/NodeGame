---
shell: home
tags: [home]
---

# Node War: learning vault

> [!abstract] What this vault is
> The agent cards in `docs/systems/` say **what** each system does, as tersely as possible.
> This vault says **why**: the problem each part solves, what else was possible, what
> each choice costs later, and the mistakes to avoid. It is written for a person learning the systems.

## Keeping this vault current

[[Merge review]] tracks system cards and notes whose sources changed after verification. The merged-PR hook refreshes it and a GitHub tracking issue. Agents compare prose against source diffs before stamping; personal **My understanding** sections stay yours.

## Shells: zooming in

Every note sits at one zoom level. Each shell explains the one above it in more detail.
**Scripts are the bottom.** Below shell 3 is the code itself.

| Shell | Colour in the graph | Zoom | The question it answers | Example |
|---|---|---|---|---|
| 0 | 🔴 red | Realm | Where am I, and what is this part of the game for? | [[Match]] |
| 1 | 🟠 orange | System | Which part owns this job? | [[Netcode]] |
| 2 | 🟢 green | Mechanism | How does one idea work? | [[Tick packets]] |
| 3 | 🔵 blue | Script | Where is it in the code, and how do I read it? | [[InputSerializer]] |

## Beside the shells: concepts and patterns

Neither is a zoom level. They're the *sideways* links that connect notes across shells.

| | 🟣 Concept | 🟡 Pattern 🔁 |
|---|---|---|
| What | General knowledge, true in **any** project | A design move that recurs **inside this codebase** |
| Example | [[Idempotency]]: "doing it twice = doing it once" | [[Make retries safe]]: packets sent 7×, absolute commands, settlement guards… |
| Has | Links out to the best external reading | A "where you've seen it" table listing shells and places |
| Why read it | To understand a topic beyond Node War | To predict how an unfamiliar part of the code works |

A pattern usually *applies* a concept: [[Make retries safe]] (the habit here) applies [[Idempotency]] (the general idea).

## Every note has the same skeleton

You learn where to look once, then use it everywhere.

1. **The question**: what the note answers
2. **Where you are**: the trail from realm to here
3. **The problem**: what goes wrong without this
4. **The idea**: one sentence, in bold
5. **How it works here**: diagrams first, then the walkthrough
6. **Why this, not that**: the alternatives and what each one gains and costs
7. **What it costs later**: doors this decision opens or closes
8. **Traps**: directions to avoid
9. **Patterns you've seen before**: links that cross shells
10. **Check yourself**: questions with collapsed answers
11. **Go deeper ↓**: the next shell down
12. **My understanding**: yours alone. AI never edits it.

(Script notes swap 3–8 for "What it is" and "Read it in this order".)

## Using the graph

- **Whole vault:** press `Ctrl+G`. Colours follow the table above. Grey nodes are notes not yet written.
- **Shells around one note:** open a note, `Ctrl+P` → *Graph view: Open local graph*, and drag that tab
  into the right sidebar. The **Depth** slider in its settings is the shell control: depth 1 shows the next shell.
- **One shell layer at a time:** type a filter into the graph's search box:
  - `tag:#shell/0 OR tag:#shell/1`: realms and systems only
  - `tag:#shell/0 OR tag:#shell/1 OR tag:#shell/2`: add mechanisms
  - `tag:#realm/backend`: only the backend
  - `-tag:#concept -tag:#pattern`: hide the sideways notes
- **Click to expand:** the optional ExcaliBrain plugin reads the `up:` and `down:` fields every note carries.

## How to read it

- **Answer "Check yourself" before you open the answer.** Retrieving an answer from memory
  is what makes it stick; rereading is not.
- **Write "My understanding" in your own words**, even one line. If you can't, that's the note to reread.

## Threads: reading paths through the shells

Each thread follows one thing from the top shell to the code. Read them in order. Each thread
reuses ideas from the one before, which is where the patterns start to show.

### Thread 1: how packets cross the internet ✅
1. [[Match]] → [[Netcode]] → [[Tick packets]] → [[InputSerializer]] · [[LockstepCore]]
2. [[Netcode]] → [[Input delay]] → [[InputDelayController]]
3. [[Netcode]] → [[Rollback]] → [[LockstepCore]]
4. [[Netcode]] → [[Build identity handshake]] → [[LocalBuildIdentity]] · [[SetupAgreement]]
5. [[Netcode]] → [[NetworkManager]]

Alongside: [[UDP vs TCP]] · [[Endianness]] · [[Deterministic lockstep]] · [[NAT and relay servers]] · [[Threads and the main thread]]

### Thread 2: from your tap to the screen ✅
1. [[Input]] → [[Gesture recognition]] → [[GestureClassifier]] · [[PointerGestureSource]] · [[TapRouter]] · [[SelectionSystem]]
2. [[Commands]] → [[Command pipeline]] → [[CommandSystem]] → [[InputBuffer]] → [[CommandProcessor]]
3. [[Simulation]] → [[The tick]] → [[TickRunner]] → [[GameSimulation]] · [[SimulationState]] · [[Pathfinding]] · [[TickEvents]]
4. [[Simulation]] → [[Desync detection]] → [[SimulationStateHasher]]
5. [[World view]] → [[Smooth movement between ticks]] → [[VillagerView]] → [[PathCurve]]
6. [[World view]] → [[Route lines]] → [[RouteCurveCache]] · [[OrderPresentation]] · [[MovementPathRenderer]]
7. [[World view]] → [[Pending orders]] → [[PendingOrderTracker]] · [[PendingOrderView]]

Alongside: [[State machines]] · [[Fixed timestep]] · [[Why floats break determinism]] · [[Events and observers]] · [[Object pooling]]

### Thread 3: after the match, and where player data lives ✅
1. [[Backend]]: start with its "where every piece of data lives" table
2. [[Match log]] → [[Match log format]] → [[MatchRecorder]] · [[MatchLogFormat]]
3. [[Match log]] → [[Referee]] → [[MatchReporting]] · [[MatchEligibility]] · [[Referee (script)]] · [[MatchReplay]]
4. [[Cloud]] → [[Player records in Cloud Save]] → [[CloudSavePlayerRecordStore]] · [[CloudSaveMatchRecordStore]] · [[MatchRecord]]
5. [[Cloud]] → [[Write locks]] → [[Match settlement]] → [[MatchSettler]]
6. [[Cloud]] → [[Ranked matchmaking]] → [[MatchAllocation]] · [[MatchRendezvous]] · [[RankedQueuePresenter]]
7. [[Cloud]] → [[Disconnect holds]] → [[MatchHold]]
8. [[Progression]] → [[Rating and rank]] → [[MatchSettlement]] · [[Glicko2]] · [[RankPoints]]
9. [[Backend client]] → [[BackendServices]] · [[PendingRankedReports]]

Alongside: [[Key-value stores vs SQL]] (start here for SQL) · [[Optimistic concurrency]] ·
[[Idempotency]] · [[Server authority]] · [[Rating systems]]

### Thread 4: before the match, from Play to tick 0 ✅
1. [[Lobby]] → [[Lobby UI]] → [[Match launch]] → [[MatchLauncher]] · [[MatchConnection]] · [[LoadoutMatchSetup]]
2. [[Lobby UI]] → [[PlayerProfile]] · [[LoadoutEditor]] · [[NavigationController]]
3. [[Board]] → [[Map identity]] → [[MatchSetup]] · [[BoardHasher]] · [[PremadeMaps]]
4. [[Draft]] → [[Draft turns]] → [[DraftState]] · [[DraftManager]] · [[DraftSerializer]]
5. [[Board]] → [[Building tick zero]] → [[PlacementLegality]] · [[MatchFactory]]
6. [[Match flow]] → [[Match startup sequence]] → [[GameManager]]
7. [[Match UI]] → [[Three UI stacks]] → [[GameplayHUDController]] · [[NodeSheetContent]] · [[DraftScreenController]]

### Thread 5: the economy, the bot, the art and the guardrails ✅
1. [[Economy]] → [[Worker production]] → [[GameBalanceData]]
2. [[Economy]] → [[District actions]] → [[NodeActionRules]] · [[DistrictHealth]] · [[PierGate]]
3. [[Economy]] → [[Storehouse collection]] → [[BankRules]]
4. [[Bots]] → [[Bot priorities]] → [[BotPlayer]] · [[BotSuitChoice]]
5. [[Bots]] → [[Headless balance matches]] → [[MatchRunner]]
6. [[UI art]] → [[Semantic icons and fallbacks]] → [[UIArtLookup]] · [[UIArtTheme]] · [[LobbyIcon]]
7. [[UI art]] → [[Shared UI theme]]
8. [[Tooling]] → [[Test evidence]] → [[compile-check.ps1]] · [[sim-guard.ps1]]
9. [[Tooling]] → [[Determinism receipts]]
10. [[Tooling]] → [[Documentation guardrails]] → [[context.ps1]] · [[doc-budget.ps1]] · [[okf-stale.ps1]]

Thread 5's tooling half is also about working with AI: read [[Tooling]] and the pattern [[Evidence has a boundary]] together.

All 18 systems now have notes.

## Patterns 🔁 so far

[[Send intent, not results]] · [[Presentation reads, never writes]] · [[Pure core, Unity shell]] ·
[[Hide latency with honest previews]] · [[Never trust the wire]] ·
[[Treat bad input as a case you already handle]] · [[Constants come from measurements]] ·
[[Numbers on the wire are forever]] · [[Same code, three places]] · [[One source of geometry]] ·
[[Make retries safe]] · [[One rule, many askers]] · [[Evidence has a boundary]]
