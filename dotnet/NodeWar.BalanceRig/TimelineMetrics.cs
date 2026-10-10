using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NodeWar.Simulation;

namespace NodeWar.BalanceRig
{
    public enum OwnerTransitionKind { Claim, Neutralisation }

    /// <summary>A node changing hands completely: neutralised, or claimed.</summary>
    public struct OwnerTransition
    {
        public int tick;
        public int nodeID;
        public bool isCore;
        public int fromOwner;
        public int toOwner;
        public OwnerTransitionKind kind;

        /// <summary>A claim by the player who was not the node's last non-neutral owner.</summary>
        public bool recapture;
    }

    /// <summary>One command window: applied ticks [startTick, end), the end trimmed to the ticks actually played.</summary>
    public sealed class WindowStats
    {
        public int index;
        public int startTick;

        /// <summary>Command records applied in the window, by player: attempted, including ones the simulation refuses.</summary>
        public int[] commands = new int[2];

        /// <summary>Ticks sampled in the window; a sample is the state after one tick.</summary>
        public int idleSamples;

        /// <summary>Body-ticks spent living, unconsumed and Idle, excluding collector minions, by player.</summary>
        public int[] idleVillagerTicks = new int[2];

        /// <summary>Most villagers a player had idle on any one sample.</summary>
        public int[] idlePeak = new int[2];
    }

    /// <summary>
    /// Watches a match without touching it. Rig tooling only: nothing here is
    /// in <c>Simulation/</c> and nothing in the simulation reads it.
    ///
    /// Commands are seen as the applied batch before the tick, state and
    /// events after it. Every array is sized from the state it is given, so
    /// no map shape is assumed.
    ///
    /// Tick numbers: a command is applied on tick t when the state's
    /// tickCount is t before <c>SimulateTick</c>; the state it produces is
    /// post-tick t+1. Windows are by applied tick (so a command held by an
    /// input delay counts where it applied, not where it was issued), and
    /// the sample taken after applied tick t belongs to t's window.
    /// </summary>
    public sealed class TimelineMetrics
    {
        public const int SchemaVersion = 1;

        /// <summary>Opening contest: a CombatStarted at a non-core node in post-ticks 1..60 s inclusive.</summary>
        public const int ContestSeconds = 60;

        /// <summary>Lead snapshot: post-tick at 120 s.</summary>
        public const int LeadSeconds = 120;

        public readonly int TicksPerSecond;
        public readonly int WindowTicks;
        public readonly int ContestTicks;
        public readonly int LeadTick;
        public readonly string MapIdentity;
        public readonly int NodeCount;

        public readonly List<WindowStats> Windows = new List<WindowStats>();
        public readonly List<OwnerTransition> Transitions = new List<OwnerTransition>();

        public int TicksPlayed { get; private set; }

        public int ClaimCount { get; private set; }
        public int NeutralisationCount { get; private set; }
        public int RecaptureCount { get; private set; }
        public readonly int[] ClaimsBy = new int[2];
        public readonly int[] NeutralisedFrom = new int[2];
        public readonly int[] RecapturesBy = new int[2];

        public bool OpeningContested { get; private set; }
        public int FirstContestTick { get; private set; } = -1;
        public int FirstContestNode { get; private set; } = -1;

        public bool HasLead { get; private set; }
        public int LeadNonCore { get; private set; }
        public readonly int[] NonCoreOwned = new int[2];

        private int[] owners;
        private int[] lastNonNeutral;

        public TimelineMetrics(SimulationState initial, int ticksPerSecond)
        {
            if (ticksPerSecond <= 0) throw new ArgumentException("ticksPerSecond must be positive.");
            TicksPerSecond = ticksPerSecond;
            WindowTicks = 10 * ticksPerSecond;
            ContestTicks = ContestSeconds * ticksPerSecond;
            LeadTick = LeadSeconds * ticksPerSecond;

            NodeCount = initial.nodes.Length;
            owners = new int[NodeCount];
            lastNonNeutral = new int[NodeCount];
            int cols = 0, rows = 0;
            uint hash = 2166136261;
            for (int n = 0; n < NodeCount; n++)
            {
                NodeData node = initial.nodes[n];
                owners[n] = node.ownerID;
                lastNonNeutral[n] = node.ownerID >= 0 ? node.ownerID : -1;
                if (node.gridX + 1 > cols) cols = node.gridX + 1;
                if (node.gridZ + 1 > rows) rows = node.gridZ + 1;
                foreach (int v in new[] { node.gridX, node.gridZ, (int)node.districtType, node.ownerID })
                    hash = (hash ^ (uint)v) * 16777619;
            }
            MapIdentity = cols + "x" + rows + "-n" + NodeCount + "-" + hash.ToString("x8", CultureInfo.InvariantCulture);
        }

        /// <summary>The window's exclusive end: its full length, or the ticks played if it is the partial tail.</summary>
        public int WindowEnd(int index) => Math.Min(Windows[index].startTick + WindowTicks, TicksPlayed);

        /// <summary>The batch about to be applied on <paramref name="appliedTick"/>, in applied order.</summary>
        public void ObserveCommands(int appliedTick, GameCommand[] applied)
        {
            WindowStats w = Window(appliedTick);
            for (int i = 0; i < applied.Length; i++)
            {
                int p = applied[i].playerID;
                if (p == 0 || p == 1) w.commands[p]++;
            }
        }

        /// <summary>The state a tick produced, and the events it logged (null: none).</summary>
        public void ObserveTick(SimulationState post, TickEventLog log)
        {
            int post1 = post.tickCount;
            TicksPlayed = Math.Max(TicksPlayed, post1);
            WindowStats w = Window(Math.Max(0, post1 - 1));

            // Minions deliberately idle while collecting; they are not unemployed bodies.
            int[] idle = new int[2];
            for (int v = 0; v < post.villagers.Length; v++)
            {
                VillagerData vil = post.villagers[v];
                if (vil.isConsumed || vil.state != VillagerState.Idle || !NodeActionRules.IsBody(vil)) continue;
                if (vil.ownerID == 0 || vil.ownerID == 1) idle[vil.ownerID]++;
            }
            w.idleSamples++;
            for (int p = 0; p < 2; p++)
            {
                w.idleVillagerTicks[p] += idle[p];
                if (idle[p] > w.idlePeak[p]) w.idlePeak[p] = idle[p];
            }

            // Complete owner changes: neutralisations and claims.
            for (int n = 0; n < post.nodes.Length && n < owners.Length; n++)
            {
                int now = post.nodes[n].ownerID;
                int was = owners[n];
                if (now == was) continue;

                var t = new OwnerTransition
                {
                    tick = post1,
                    nodeID = n,
                    isCore = post.nodes[n].districtType == DistrictType.Core,
                    fromOwner = was,
                    toOwner = now
                };
                if (now < 0)
                {
                    t.kind = OwnerTransitionKind.Neutralisation;
                    NeutralisationCount++;
                    if (was == 0 || was == 1) NeutralisedFrom[was]++;
                }
                else
                {
                    t.kind = OwnerTransitionKind.Claim;
                    ClaimCount++;
                    if (now == 0 || now == 1) ClaimsBy[now]++;
                    if (lastNonNeutral[n] >= 0 && lastNonNeutral[n] != now)
                    {
                        t.recapture = true;
                        RecaptureCount++;
                        if (now == 0 || now == 1) RecapturesBy[now]++;
                    }
                    lastNonNeutral[n] = now;
                }
                Transitions.Add(t);
                owners[n] = now;
            }

            // Opening contest: the first non-core CombatStarted inside the window.
            if (log != null && !OpeningContested && post1 >= 1 && post1 <= ContestTicks)
            {
                for (int e = 0; e < log.Count; e++)
                {
                    TickEvent ev = log[e];
                    if (ev.type != TickEventType.CombatStarted) continue;
                    if (ev.nodeID < 0 || ev.nodeID >= post.nodes.Length) continue;
                    if (post.nodes[ev.nodeID].districtType == DistrictType.Core) continue;
                    OpeningContested = true;
                    FirstContestTick = post1;
                    FirstContestNode = ev.nodeID;
                    break;
                }
            }

            // Lead: non-core ownership on the snapshot tick, cores excluded.
            if (post1 == LeadTick && !HasLead)
            {
                for (int n = 0; n < post.nodes.Length; n++)
                {
                    int o = post.nodes[n].ownerID;
                    if (post.nodes[n].districtType == DistrictType.Core || (o != 0 && o != 1)) continue;
                    NonCoreOwned[o]++;
                }
                LeadNonCore = NonCoreOwned[0] - NonCoreOwned[1];
                HasLead = true;
            }
        }

        /// <summary>Copies what the match report needs onto the result and attaches this timeline to it.</summary>
        public void Complete(MatchResult result)
        {
            result.timeline = this;
            result.hasLead = HasLead;
            result.twoMinuteLead = LeadNonCore;
        }

        private WindowStats Window(int appliedTick)
        {
            int index = Math.Max(0, appliedTick) / WindowTicks;
            while (Windows.Count <= index)
                Windows.Add(new WindowStats { index = Windows.Count, startTick = Windows.Count * WindowTicks });
            return Windows[index];
        }
    }

    public struct SnowballStats
    {
        public int eligible, correct, ties, endedBefore, capped;
    }

    /// <summary>
    /// Whether the side ahead on non-core districts at post-tick 1200 went on
    /// to win. Ties, capped games and games over before the snapshot are not
    /// predictions, so they are counted out and reported, never folded in.
    /// </summary>
    public static class Snowball
    {
        public static SnowballStats Evaluate(IList<MatchResult> results)
        {
            var s = new SnowballStats();
            foreach (MatchResult r in results)
            {
                if (r.capped || r.winner < 0) { s.capped++; continue; }
                if (!r.hasLead) { s.endedBefore++; continue; }
                if (r.twoMinuteLead == 0) { s.ties++; continue; }
                s.eligible++;
                int leader = r.twoMinuteLead > 0 ? 0 : 1;
                if (r.winner == leader) s.correct++;
            }
            return s;
        }

        public static string Format(SnowballStats s)
        {
            string correct = s.eligible == 0
                ? "n/a (no eligible matches)"
                : s.correct + "/" + s.eligible + " (" + (100.0 * s.correct / s.eligible).ToString("0.0", CultureInfo.InvariantCulture) + "%)";
            return "2-minute leader won: " + correct + "; excluded: ties " + s.ties
                + ", ended before the snapshot " + s.endedBefore + ", capped/no winner " + s.capped;
        }
    }

    /// <summary>
    /// The timeline's CSV output. Every row carries the schema version and the
    /// map identity, so files from different boards cannot be mixed unseen.
    /// Matches without a timeline contribute no window or flip rows.
    /// </summary>
    public static class TimelineReport
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public const string SummaryHeader =
            "schema,map,commands_p0,commands_p1,idle_villager_ticks_p0,idle_villager_ticks_p1,idle_peak_p0,idle_peak_p1,"
            + "claims_p0,claims_p1,neutralised_p0,neutralised_p1,recaptures_p0,recaptures_p1,"
            + "opening_contested,first_contest_tick,first_contest_node,lead_1200,noncore_p0_1200,noncore_p1_1200";

        private const int SummaryColumnCount = 20;

        public static string[] SummaryColumns(MatchResult r)
        {
            var cols = new string[SummaryColumnCount];
            for (int i = 0; i < cols.Length; i++) cols[i] = "";
            TimelineMetrics m = r.timeline;
            if (m == null) return cols;

            int[] commands = new int[2], idle = new int[2], peak = new int[2];
            foreach (WindowStats w in m.Windows)
                for (int p = 0; p < 2; p++)
                {
                    commands[p] += w.commands[p];
                    idle[p] += w.idleVillagerTicks[p];
                    if (w.idlePeak[p] > peak[p]) peak[p] = w.idlePeak[p];
                }

            string[] values =
            {
                TimelineMetrics.SchemaVersion.ToString(Inv), m.MapIdentity,
                commands[0].ToString(Inv), commands[1].ToString(Inv), idle[0].ToString(Inv), idle[1].ToString(Inv),
                peak[0].ToString(Inv), peak[1].ToString(Inv),
                m.ClaimsBy[0].ToString(Inv), m.ClaimsBy[1].ToString(Inv),
                m.NeutralisedFrom[0].ToString(Inv), m.NeutralisedFrom[1].ToString(Inv),
                m.RecapturesBy[0].ToString(Inv), m.RecapturesBy[1].ToString(Inv),
                m.OpeningContested ? "1" : "0", m.FirstContestTick.ToString(Inv), m.FirstContestNode.ToString(Inv),
                m.HasLead ? m.LeadNonCore.ToString(Inv) : "",
                m.HasLead ? m.NonCoreOwned[0].ToString(Inv) : "", m.HasLead ? m.NonCoreOwned[1].ToString(Inv) : ""
            };
            return values;
        }

        private const string KeyHeader = "schema,map,seed,pair_id,seat";

        private static string Key(MatchResult r) =>
            TimelineMetrics.SchemaVersion.ToString(Inv) + "," + r.timeline.MapIdentity + ","
            + r.seed.ToString(Inv) + "," + r.pairID.ToString(Inv) + "," + r.seat.ToString(Inv);

        public static string WindowsCsv(IList<MatchResult> results)
        {
            var sb = new StringBuilder();
            sb.Append(KeyHeader).Append(",window,window_ticks,start_tick,end_tick,commands_p0,commands_p1,idle_samples,")
              .Append("idle_villager_ticks_p0,idle_villager_ticks_p1,idle_peak_p0,idle_peak_p1\n");
            foreach (MatchResult r in results)
            {
                TimelineMetrics m = r.timeline;
                if (m == null) continue;
                for (int i = 0; i < m.Windows.Count; i++)
                {
                    WindowStats w = m.Windows[i];
                    sb.Append(Key(r)).Append(',')
                      .Append(w.index.ToString(Inv)).Append(',').Append(m.WindowTicks.ToString(Inv)).Append(',')
                      .Append(w.startTick.ToString(Inv)).Append(',').Append(m.WindowEnd(i).ToString(Inv)).Append(',')
                      .Append(w.commands[0].ToString(Inv)).Append(',').Append(w.commands[1].ToString(Inv)).Append(',')
                      .Append(w.idleSamples.ToString(Inv)).Append(',')
                      .Append(w.idleVillagerTicks[0].ToString(Inv)).Append(',').Append(w.idleVillagerTicks[1].ToString(Inv)).Append(',')
                      .Append(w.idlePeak[0].ToString(Inv)).Append(',').Append(w.idlePeak[1].ToString(Inv)).Append('\n');
                }
            }
            return sb.ToString();
        }

        public static string FlipsCsv(IList<MatchResult> results)
        {
            var sb = new StringBuilder();
            sb.Append(KeyHeader).Append(",tick,node,is_core,from_owner,to_owner,kind,recapture\n");
            foreach (MatchResult r in results)
            {
                if (r.timeline == null) continue;
                foreach (OwnerTransition t in r.timeline.Transitions)
                    sb.Append(Key(r)).Append(',')
                      .Append(t.tick.ToString(Inv)).Append(',').Append(t.nodeID.ToString(Inv)).Append(',')
                      .Append(t.isCore ? "1" : "0").Append(',')
                      .Append(t.fromOwner.ToString(Inv)).Append(',').Append(t.toOwner.ToString(Inv)).Append(',')
                      .Append(t.kind == OwnerTransitionKind.Claim ? "claim" : "neutralisation").Append(',')
                      .Append(t.recapture ? "1" : "0").Append('\n');
            }
            return sb.ToString();
        }
    }
}
