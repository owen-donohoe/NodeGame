using System;
using System.Collections.Generic;
using NodeWar.Input;
using NodeWar.Simulation;

namespace NodeWar.BalanceRig
{
    /// <summary>One match's outcome. Everything here is a function of the seed and the setup.</summary>
    public sealed class MatchResult
    {
        public int seed;
        public int pairID;
        public int seat;
        public int ticks;
        public int winner = -1;

        /// <summary>The match's timeline when it was observed, else null.</summary>
        public TimelineMetrics timeline;

        /// <summary>Whether the post-tick-1200 lead was taken (the match lasted that long), and P0 minus P1 non-core districts then.</summary>
        public bool hasLead;
        public int twoMinuteLead;
        public bool capped;

        public int[] breaches = new int[2];
        public List<int>[] breachTicks = { new List<int>(), new List<int>() };

        public int[] nodesOwned = new int[2];
        public int[] villagersAlive = new int[2];
        public int[] food = new int[2];
        public int[] materials = new int[2];
        public int[] metal = new int[2];

        // Why a match stalls: what each side could field at all.
        public bool[] ownsBarracksEnd = new bool[2];
        public bool[] everOwnedBarracks = new bool[2];
        public int[] soldiersEnd = new int[2];
        public int[] peakSoldiers = new int[2];
        public int[] ticksThreeIdleSoldiers = new int[2];

        /// <summary>Ticks with a non-soldier idle on its own Barracks: the one place BotPlayer equips from.</summary>
        public int[] ticksIdleOnBarracks = new int[2];

        /// <summary>Owned at the end, by side: the bot expands Village, Farm, Mine, then Barracks, strictly in that order.</summary>
        public int[] villagesEnd = new int[2];
        public int[] farmsEnd = new int[2];
        public int[] minesEnd = new int[2];

        /// <summary>Breaches that landed on the last tick played; 0 if the match stopped on a later, breach-free tick.</summary>
        public int finalTickBreaches;

        /// <summary>Ended in a win with the loser below the base breach threshold: a sudden-death drop decided it.</summary>
        public bool wonBelowBaseThreshold;

        /// <summary>Tick of the first breach by either side, or -1.</summary>
        public int FirstBreachTick
        {
            get
            {
                int first = -1;
                for (int p = 0; p < 2; p++)
                    if (breachTicks[p].Count > 0 && (first < 0 || breachTicks[p][0] < first))
                        first = breachTicks[p][0];
                return first;
            }
        }
    }

    public sealed class PreparedMatch
    {
        public RigSetup setup;
        public DraftPlacement[] draft;
        public PlayerSetup[] players;
        public int seed, pairID, seat;
    }

    /// <summary>What a run reports to, none of it able to change the match.</summary>
    public sealed class RunHooks
    {
        /// <summary>Record the timeline (and hand the simulation a TickEventLog to fill).</summary>
        public bool timeline;

        /// <summary>The batch applied on each tick that applies any, before it is applied.</summary>
        public Action<GameCommand[]> onCommands;

        /// <summary>The state after each tick.</summary>
        public Action<SimulationState> afterTick;
    }

    public static class MatchRunner
    {
        /// <summary>
        /// One match ready to run: the draft and the two players' setups for a
        /// given seat assignment. Seat 0 is the setup as supplied; seat 1 is its
        /// mirrored pair (<see cref="SwapSeats"/>), which shares the seed and pair ID.
        /// </summary>
        public static PreparedMatch Prepare(RigSetup setup, int seed, DraftPlacement[] draft = null)
        {
            int suitCount = (int)SuitType.Watcher + 1;
            int districtCount = (int)DistrictType.Fortress + 1;
            var players = new PlayerSetup[2];
            for (int p = 0; p < 2; p++)
            {
                if (setup.playerSetups != null)
                {
                    PlayerSetup given = setup.playerSetups[p];
                    players[p] = new PlayerSetup
                    {
                        suits = given.suits == null ? null : (int[])given.suits.Clone(),
                        districts = given.districts == null ? null : (int[])given.districts.Clone(),
                        suitEras = given.suitEras == null ? null : (int[])given.suitEras.Clone(),
                        districtEras = given.districtEras == null ? null : (int[])given.districtEras.Clone()
                    };
                    continue;
                }

                // What a lobby loadout resolves to: Warrior is granted to everyone,
                // the loadout's districts ride along, and every era is 0.
                int[] nodes = new int[setup.loadoutNodes.Length];
                for (int i = 0; i < nodes.Length; i++) nodes[i] = (int)setup.loadoutNodes[i];
                players[p] = new PlayerSetup
                {
                    suits = new[] { (int)SuitType.Warrior },
                    districts = nodes,
                    suitEras = new int[suitCount],
                    districtEras = new int[districtCount]
                };
            }

            return new PreparedMatch
            {
                setup = setup,
                draft = draft ?? RandomDraft(setup, new Random(seed)),
                players = players,
                seed = seed,
                pairID = seed,
                seat = 0
            };
        }

        /// <summary>
        /// The same match from the other seat: board placements and draft are
        /// mirrored through <see cref="RigSetup.mirror"/>, the two players'
        /// setups and base draft pools trade places, and ownership of the
        /// mirrored placements follows. The original is left untouched.
        /// </summary>
        public static PreparedMatch SwapSeats(PreparedMatch original)
        {
            RigSetup source = original.setup;
            if (source.mirror == null)
                throw new InvalidOperationException("Swapping seats needs RigSetup.mirror: the board's own symmetry, supplied by whoever built the setup.");

            BoardConfigData board = source.board;
            var placements = new BoardConfigData.InitialDistrictPlacement[source.board.initialPlacements.Length];
            for (int i = 0; i < placements.Length; i++)
            {
                BoardConfigData.InitialDistrictPlacement ip = source.board.initialPlacements[i];
                var cell = source.mirror(ip.gridX, ip.gridZ);
                ip.gridX = cell.x;
                ip.gridZ = cell.z;
                if (ip.ownerID == 0 || ip.ownerID == 1) ip.ownerID = 1 - ip.ownerID;
                ip.claimBar = -ip.claimBar;
                placements[i] = ip;
            }
            board.initialPlacements = placements;

            // Terrain, slots and pools move with the board: cell (x,z) becomes its mirror, and
            // the two players' base pools trade places. On a map that is its own mirror image
            // (hourglass-01) the terrain comes out identical, which the tests assert.
            board.terrain = MirrorCells(source.board.terrain, board.gridCols, board.gridRows, source.mirror);
            board.districtSlots = MirrorCells(source.board.districtSlots, board.gridCols, board.gridRows, source.mirror);
            board.baseDraftDistrictsP0 = source.board.baseDraftDistrictsP1;
            board.baseDraftDistrictsP1 = source.board.baseDraftDistrictsP0;

            var draft = new DraftPlacement[original.draft.Length];
            for (int i = 0; i < draft.Length; i++)
            {
                DraftPlacement dp = original.draft[i];
                var cell = source.mirror(dp.gridX, dp.gridZ);
                dp.gridX = cell.x;
                dp.gridZ = cell.z;
                dp.playerID = 1 - dp.playerID;
                draft[i] = dp;
            }

            var setup = new RigSetup
            {
                balance = source.balance,
                balanceHash = source.balanceHash,
                sourceBalanceHash = source.sourceBalanceHash,
                balanceSource = source.balanceSource,
                overlaidFields = source.overlaidFields,
                board = board,
                boardSource = source.boardSource,
                mapId = source.mapId,
                baseDraft = new[] { source.baseDraft[1], source.baseDraft[0] },
                loadoutNodes = source.loadoutNodes,
                playerSetups = source.playerSetups == null ? null : new[] { source.playerSetups[1], source.playerSetups[0] },
                mirror = source.mirror
            };

            return new PreparedMatch
            {
                setup = setup,
                draft = draft,
                players = new[] { original.players[1], original.players[0] },
                seed = original.seed,
                pairID = original.pairID,
                seat = 1 - original.seat
            };
        }

        private static T[] MirrorCells<T>(T[] cells, int cols, int rows, Func<int, int, (int x, int z)> mirror)
        {
            if (cells == null) return null;
            var result = new T[cells.Length];
            for (int z = 0; z < rows; z++)
                for (int x = 0; x < cols; x++)
                {
                    var m = mirror(x, z);
                    result[m.z * cols + m.x] = cells[z * cols + x];
                }
            return result;
        }

        /// <summary>
        /// Queues this tick's fresh commands for tick (now + inputDelay), then
        /// applies every command that has come due: P0's, then P1's, each in the
        /// order that player issued them, so neither seat's commands lead by
        /// parity or by evaluation order. The observer sees exactly the applied
        /// batch, before it is applied.
        /// </summary>
        public static void ApplyCommands(SimulationState state, GameCommand[] fresh, int inputDelay,
            List<KeyValuePair<int, GameCommand>> delayed, Action<GameCommand[]> observer = null, TickEventLog log = null)
        {
            for (int i = 0; i < fresh.Length; i++)
                delayed.Add(new KeyValuePair<int, GameCommand>(state.tickCount + inputDelay, fresh[i]));

            var due = new List<GameCommand>();
            int kept = 0;
            for (int i = 0; i < delayed.Count; i++)
            {
                if (delayed[i].Key <= state.tickCount) due.Add(delayed[i].Value);
                else delayed[kept++] = delayed[i];
            }
            delayed.RemoveRange(kept, delayed.Count - kept);
            if (due.Count == 0) return;

            var ordered = new GameCommand[due.Count];
            int at = 0;
            for (int p = 0; p < 2; p++)
                for (int i = 0; i < due.Count; i++)
                    if (due[i].playerID == p) ordered[at++] = due[i];
            for (int i = 0; i < due.Count; i++)
                if (due[i].playerID != 0 && due[i].playerID != 1) ordered[at++] = due[i];

            observer?.Invoke(ordered);
            for (int i = 0; i < ordered.Length; i++) CommandProcessor.ProcessCommand(state, ordered[i], log);
        }

        /// <summary>One match from the setup as supplied (seat 0); see <see cref="SwapSeats"/> for its pair.</summary>
        public static MatchResult Run(RigSetup setup, int seed, int capTicks, int inputDelay, System.IO.TextWriter trace = null)
            => Run(Prepare(setup, seed), capTicks, inputDelay, trace, null);

        public static MatchResult Run(PreparedMatch match, int capTicks, int inputDelay, System.IO.TextWriter trace, RunHooks hooks)
        {
            RigSetup setup = match.setup;
            MatchFactory.Configure(setup.balance, setup.board);
            SimulationState state = MatchFactory.Build(setup.balance, setup.board, match.draft, match.players);

            var result = new MatchResult { seed = match.seed, pairID = match.pairID, seat = match.seat };
            var buffer = new InputBuffer();
            var bots = new[]
            {
                new BotPlayer(state, buffer, 0, setup.board.defaultLinkWeight),
                new BotPlayer(state, buffer, 1, setup.board.defaultLinkWeight)
            };

            // Commands held back by an input delay: applied at tick (issued + delay).
            var delayed = new List<KeyValuePair<int, GameCommand>>();
            int[] seenBreaches = new int[2];

            TimelineMetrics timeline = hooks != null && hooks.timeline ? new TimelineMetrics(state, setup.balance.ticksPerSecond) : null;
            TickEventLog log = timeline != null ? new TickEventLog() : null;
            Action<GameCommand[]> observer = commands =>
            {
                timeline?.ObserveCommands(state.tickCount, commands);
                hooks?.onCommands?.Invoke(commands);
            };

            while (!state.gameOver && state.tickCount < capTicks)
            {
                // The live bot path (TickRunner.Update): evaluate, drain the
                // buffer, apply, then SimulateTick. Commands apply P0 then P1
                // every tick whatever order the bots evaluated in.
                bots[0].Evaluate();
                bots[1].Evaluate();
                log?.Clear();
                ApplyCommands(state, buffer.DrainCommands(), inputDelay, delayed, observer, log);

                GameSimulation.SimulateTick(state, log);
                timeline?.ObserveTick(state, log);
                hooks?.afterTick?.Invoke(state);

                TrackFleet(state, result);
                if (trace != null && state.tickCount % 200 == 0) Trace(trace, state);

                for (int p = 0; p < 2; p++)
                {
                    while (seenBreaches[p] < state.players[p].breachCount)
                    {
                        result.breachTicks[p].Add(state.tickCount);
                        seenBreaches[p]++;
                    }
                }
            }

            Finish(state, result, setup.balance);
            timeline?.Complete(result);
            return result;
        }

        public static void Finish(SimulationState state, MatchResult result, GameBalanceData balance)
        {
            result.ticks = state.tickCount;
            result.capped = !state.gameOver;
            result.winner = state.gameOver ? state.winnerID : -1;
            // Breaches that landed on the last tick played. A capped match's
            // earlier breaches do not count: only the tick the match stopped on.
            for (int p = 0; p < 2; p++)
                foreach (int t in result.breachTicks[p])
                    if (t == state.tickCount) result.finalTickBreaches++;
            if (result.winner >= 0)
                result.wonBelowBaseThreshold = state.players[1 - result.winner].breachCount < balance.breachThreshold;

            for (int p = 0; p < 2; p++)
            {
                result.breaches[p] = state.players[p].breachCount;
                result.food[p] = state.players[p].food;
                result.materials[p] = state.players[p].materials;
                result.metal[p] = state.players[p].metal;
            }
            for (int n = 0; n < state.nodes.Length; n++)
            {
                int owner = state.nodes[n].ownerID;
                if (owner == 0 || owner == 1) result.nodesOwned[owner]++;
            }
            for (int v = 0; v < state.villagers.Length; v++)
            {
                VillagerData vil = state.villagers[v];
                if (vil.state == VillagerState.Dead || vil.isConsumed) continue;
                if (vil.ownerID == 0 || vil.ownerID == 1) result.villagersAlive[vil.ownerID]++;
            }
        }

        /// <summary>One line per side: owned districts, bodies by state (excluding minions), soldiers, resources.</summary>
        public static void Trace(System.IO.TextWriter w, SimulationState state)
        {
            for (int p = 0; p < 2; p++)
            {
                var owned = new List<string>();
                for (int n = 0; n < state.nodes.Length; n++)
                    if (state.nodes[n].ownerID == p && state.nodes[n].districtType != DistrictType.Core)
                        owned.Add(state.nodes[n].districtType + "@" + n);
                int[] byState = new int[Enum.GetValues(typeof(VillagerState)).Length];
                int soldiers = 0;
                var where = new List<string>();
                for (int v = 0; v < state.villagers.Length; v++)
                {
                    VillagerData vil = state.villagers[v];
                    if (vil.ownerID != p || vil.isConsumed || !NodeActionRules.IsBody(vil)) continue;
                    byState[(int)vil.state]++;
                    if (vil.state != VillagerState.Dead && GameBalanceData.IsCombatSuit(vil.suit)) soldiers++;
                }
                PlayerData pd = state.players[p];
                w.WriteLine("t=" + state.tickCount + " P" + p + " owned[" + string.Join(" ", owned) + "] idle=" + byState[0]
                    + " moving=" + byState[1] + " working=" + byState[2] + " claiming=" + byState[3] + " fighting=" + byState[4]
                    + " dead=" + byState[(int)VillagerState.Dead] + " breaching=" + byState[(int)VillagerState.Breaching]
                    + " soldiers=" + soldiers + " food=" + pd.food + " mat=" + pd.materials);
            }
        }

        /// <summary>Per-tick bookkeeping for the stall diagnostics.</summary>
        public static void TrackFleet(SimulationState state, MatchResult result)
        {
            int[] soldiers = new int[2];
            int[] idle = new int[2];
            bool[] barracks = new bool[2];
            int[] villages = new int[2], farms = new int[2], mines = new int[2];
            bool[] idleOnBarracks = new bool[2];

            for (int n = 0; n < state.nodes.Length; n++)
            {
                int owner = state.nodes[n].ownerID;
                if ((owner == 0 || owner == 1) && state.nodes[n].districtType == DistrictType.Barracks)
                    barracks[owner] = true;
                if (owner == 0 || owner == 1)
                {
                    DistrictType d = state.nodes[n].districtType;
                    if (d == DistrictType.Village) villages[owner]++;
                    else if (d == DistrictType.Farm) farms[owner]++;
                    else if (d == DistrictType.Mine) mines[owner]++;
                }
            }
            for (int v = 0; v < state.villagers.Length; v++)
            {
                VillagerData vil = state.villagers[v];
                if (vil.ownerID < 0 || vil.ownerID > 1) continue;
                if (vil.state == VillagerState.Dead || vil.isConsumed || !NodeActionRules.IsBody(vil)) continue;
                if (!GameBalanceData.IsCombatSuit(vil.suit))
                {
                    int at = vil.currentNodeID;
                    if (vil.state == VillagerState.Idle && state.nodes[at].districtType == DistrictType.Barracks
                        && state.nodes[at].ownerID == vil.ownerID)
                        idleOnBarracks[vil.ownerID] = true;
                    continue;
                }
                soldiers[vil.ownerID]++;
                switch (vil.state)
                {
                    case VillagerState.Idle: idle[vil.ownerID]++; break;
                    default: break;
                }
            }
            for (int p = 0; p < 2; p++)
            {
                result.ownsBarracksEnd[p] = barracks[p];
                result.everOwnedBarracks[p] |= barracks[p];
                result.soldiersEnd[p] = soldiers[p];
                if (soldiers[p] > result.peakSoldiers[p]) result.peakSoldiers[p] = soldiers[p];
                if (idle[p] >= 3) result.ticksThreeIdleSoldiers[p]++;
                if (idleOnBarracks[p]) result.ticksIdleOnBarracks[p]++;
                result.villagesEnd[p] = villages[p];
                result.farmsEnd[p] = farms[p];
                result.minesEnd[p] = mines[p];
            }
        }

        /// <summary>
        /// A random legal draft, standing in for the live one: players take
        /// turns, P0 first, each placing one of their remaining districts
        /// (base pool plus loadout) on a random free cell. A player with
        /// nothing left passes. Draws come from the match's own seeded Random.
        /// </summary>
        public static DraftPlacement[] RandomDraft(RigSetup setup, Random rng)
        {
            int cols = setup.board.gridCols;
            int rows = setup.board.gridRows;
            var occupied = new bool[cols * rows];
            for (int i = 0; i < setup.board.initialPlacements.Length; i++)
            {
                var ip = setup.board.initialPlacements[i];
                occupied[ip.gridZ * cols + ip.gridX] = true;
            }

            var remaining = new List<DistrictType>[2];
            for (int p = 0; p < 2; p++)
            {
                remaining[p] = new List<DistrictType>(setup.baseDraft[p]);
                remaining[p].AddRange(setup.loadoutNodes);
            }

            var placements = new List<DraftPlacement>();
            var free = new List<int>();
            int turn = 0;
            while (remaining[0].Count > 0 || remaining[1].Count > 0)
            {
                if (remaining[turn].Count > 0)
                {
                    int slot = rng.Next(remaining[turn].Count);

                    // Only cells the pick may legally stand on (PlacementLegality, the same
                    // rule the live draft uses). A pick with no legal cell is skipped, not
                    // forced onto water; the other pick types stay playable.
                    free.Clear();
                    for (int c = 0; c < occupied.Length; c++)
                        if (PlacementLegality.CanPlace(setup.board, occupied, remaining[turn][slot], c % cols, c / cols))
                            free.Add(c);
                    if (free.Count == 0)
                    {
                        remaining[turn].RemoveAt(slot);
                        continue;
                    }
                    int cell = free[rng.Next(free.Count)];
                    placements.Add(new DraftPlacement
                    {
                        playerID = turn,
                        districtType = remaining[turn][slot],
                        gridX = cell % cols,
                        gridZ = cell / cols
                    });
                    remaining[turn].RemoveAt(slot);
                    occupied[cell] = true;
                }
                turn = 1 - turn;
            }
            return placements.ToArray();
        }
    }
}
