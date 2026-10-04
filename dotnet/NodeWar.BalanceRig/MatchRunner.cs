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
        public int ticks;
        public int winner = -1;
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

        /// <summary>Breaches that landed on the last tick played.</summary>
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

    public static class MatchRunner
    {
        public static MatchResult Run(RigSetup setup, int seed, int capTicks, int inputDelay, bool swapSeats = true, System.IO.TextWriter trace = null)
        {
            var rng = new Random(seed);
            DraftPlacement[] draft = RandomDraft(setup, rng);

            int suitCount = (int)SuitType.Watcher + 1;
            int districtCount = (int)DistrictType.Market + 1;
            int[] nodes = new int[setup.loadoutNodes.Length];
            for (int i = 0; i < nodes.Length; i++) nodes[i] = (int)setup.loadoutNodes[i];

            // What a lobby loadout resolves to: Warrior is granted to everyone,
            // the loadout's districts ride along, and every era is 0.
            var players = new PlayerSetup[2];
            for (int p = 0; p < 2; p++)
                players[p] = new PlayerSetup
                {
                    suits = new[] { (int)SuitType.Warrior },
                    nodes = (int[])nodes.Clone(),
                    suitEras = new int[suitCount],
                    districtEras = new int[districtCount]
                };

            MatchFactory.Configure(setup.balance, setup.board);
            SimulationState state = MatchFactory.Build(setup.balance, setup.board, draft, players);

            var result = new MatchResult { seed = seed };
            var buffer = new InputBuffer();
            var bots = new[]
            {
                new BotPlayer(state, buffer, 0, setup.board.defaultEdgeWeight),
                new BotPlayer(state, buffer, 1, setup.board.defaultEdgeWeight)
            };

            // Commands held back by an input delay: applied at tick (issued + delay).
            var delayed = new List<KeyValuePair<int, GameCommand>>();
            int[] seenBreaches = new int[2];

            while (!state.gameOver && state.tickCount < capTicks)
            {
                // The live bot path (TickRunner.Update): evaluate, drain the
                // buffer, process in buffer order, then SimulateTick. P0 first,
                // as a human's commands would sit ahead of the bot's. With
                // swapSeats the first seat alternates by tick parity, so
                // neither side's commands always lead the buffer.
                bool flip = swapSeats && (state.tickCount & 1) == 1;
                if (!flip) { bots[0].Evaluate(); bots[1].Evaluate(); }
                else { bots[1].Evaluate(); bots[0].Evaluate(); }

                GameCommand[] fresh = buffer.DrainCommands();
                if (inputDelay <= 0)
                {
                    for (int i = 0; i < fresh.Length; i++)
                        CommandProcessor.ProcessCommand(state, fresh[i]);
                }
                else
                {
                    for (int i = 0; i < fresh.Length; i++)
                        delayed.Add(new KeyValuePair<int, GameCommand>(state.tickCount + inputDelay, fresh[i]));
                    int kept = 0;
                    for (int i = 0; i < delayed.Count; i++)
                    {
                        if (delayed[i].Key <= state.tickCount)
                            CommandProcessor.ProcessCommand(state, delayed[i].Value);
                        else
                            delayed[kept++] = delayed[i];
                    }
                    delayed.RemoveRange(kept, delayed.Count - kept);
                }

                GameSimulation.SimulateTick(state);

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

            result.ticks = state.tickCount;
            result.capped = !state.gameOver;
            result.winner = state.gameOver ? state.winnerID : -1;
            // The win is read one tick after the breach that decides it, so the
            // decisive tick is the last tick a breach landed on.
            int lastBreach = -1;
            for (int p = 0; p < 2; p++)
                foreach (int t in result.breachTicks[p]) if (t > lastBreach) lastBreach = t;
            for (int p = 0; p < 2; p++)
                foreach (int t in result.breachTicks[p])
                    if (t == lastBreach) result.finalTickBreaches++;
            if (result.winner >= 0)
                result.wonBelowBaseThreshold = state.players[1 - result.winner].breachCount < setup.balance.breachThreshold;

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
            return result;
        }

        /// <summary>One line per side: owned districts, villagers by state, soldiers, resources.</summary>
        private static void Trace(System.IO.TextWriter w, SimulationState state)
        {
            for (int p = 0; p < 2; p++)
            {
                var owned = new List<string>();
                for (int n = 0; n < state.nodes.Length; n++)
                    if (state.nodes[n].ownerID == p && state.nodes[n].districtType != DistrictType.Core)
                        owned.Add(state.nodes[n].districtType + "@" + n);
                int[] byState = new int[6];
                int soldiers = 0;
                var where = new List<string>();
                for (int v = 0; v < state.villagers.Length; v++)
                {
                    VillagerData vil = state.villagers[v];
                    if (vil.ownerID != p || vil.isConsumed) continue;
                    byState[(int)vil.state]++;
                    if (vil.state != VillagerState.Dead && GameBalanceData.IsCombatSuit(vil.suit)) soldiers++;
                }
                PlayerData pd = state.players[p];
                w.WriteLine("t=" + state.tickCount + " P" + p + " owned[" + string.Join(" ", owned) + "] idle=" + byState[0]
                    + " moving=" + byState[1] + " working=" + byState[2] + " claiming=" + byState[3] + " fighting=" + byState[4]
                    + " dead=" + byState[5] + " soldiers=" + soldiers + " food=" + pd.food + " mat=" + pd.materials);
            }
        }

        /// <summary>Per-tick bookkeeping for the stall diagnostics.</summary>
        private static void TrackFleet(SimulationState state, MatchResult result)
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
                if (vil.state == VillagerState.Dead || vil.isConsumed) continue;
                if (!GameBalanceData.IsCombatSuit(vil.suit))
                {
                    int at = vil.currentNodeID;
                    if (vil.state == VillagerState.Idle && state.nodes[at].districtType == DistrictType.Barracks
                        && state.nodes[at].ownerID == vil.ownerID)
                        idleOnBarracks[vil.ownerID] = true;
                    continue;
                }
                soldiers[vil.ownerID]++;
                if (vil.state == VillagerState.Idle) idle[vil.ownerID]++;
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
                    free.Clear();
                    for (int c = 0; c < occupied.Length; c++)
                        if (!occupied[c]) free.Add(c);
                    if (free.Count == 0) break;

                    int slot = rng.Next(remaining[turn].Count);
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
