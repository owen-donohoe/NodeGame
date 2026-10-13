using System;
using System.Collections.Generic;
using System.Text;
using NodeWar.Simulation;
using NodeWar.Tests;
using NUnit.Framework;

namespace NodeWar.Network.Tests
{
    /// <summary>A frame that takes far longer than it should, like the editor's 100-280 ms ones.</summary>
    internal struct FrameSpike
    {
        public int peer;
        public double at;
        public double duration;
    }

    /// <summary>
    /// Two LockstepCores playing one match across a simulated link, and the
    /// reference they are judged against.
    ///
    /// The reference is the same match on a perfect link with no networking at
    /// all: commands applied P0 then P1, SimulateTick, a hash at every
    /// checkpoint. Commands are scripted by the tick they apply on (forTick),
    /// not by the time they were issued, so a run that stalls, speculates and
    /// replays must still land on the reference's hashes. That is a stronger
    /// claim than "the two peers agree": both could agree on a wrong answer.
    /// </summary>
    internal sealed class LockstepScenario
    {
        private const int InputDelay = 2; // LockstepCore's INPUT_DELAY; ticks below it carry no commands
        private const int CheckInterval = 50; // DESYNC_CHECK_INTERVAL

        public int Seed = 1;
        public double Seconds = 60;
        public double FrameInterval = 1.0 / 60.0;
        /// <summary>Link from peer 0 to peer 1.</summary>
        public LinkProfile ZeroToOne = new LinkProfile();
        /// <summary>Link from peer 1 to peer 0.</summary>
        public LinkProfile OneToZero = new LinkProfile();
        public List<FrameSpike> Spikes = new List<FrameSpike>();
        /// <summary>Reference run applies no commands: used only to prove the oracle can tell the difference.</summary>
        public bool BlankReference;
        /// <summary>When each peer initialises and unpauses, in seconds: the match start is not synchronised, one side can begin before the other.</summary>
        public double[] StartAt = { 0, 0 };

        public HarnessPeer[] Peers;
        public LinkDirection[] Links;
        public Dictionary<int, int> Reference;
        public double Now;

        private int nodeCount;
        public Func<int,int,GameCommand[]> CommandScript;
        public bool CaptureConfirmedTicks;

        public static GameBalanceData Balance;
        /// <summary>Optional balance for scenarios that need more than the shipped defaults.</summary>
        public Func<GameBalanceData> BalanceFactory;

        /// <summary>The board the match is played on. The default is the tiny 3x3 land fixture.</summary>
        public BoardConfigData Board = BoardFixtures.LandGrid3x3();

        /// <summary>The draft both peers and the reference start from, legal on <see cref="Board"/>.</summary>
        public DraftPlacement[] Draft =
        {
            new DraftPlacement { playerID = 0, districtType = DistrictType.Farm, gridX = 0, gridZ = 1 },
            new DraftPlacement { playerID = 1, districtType = DistrictType.Village, gridX = 2, gridZ = 1 }
        };

        public void Configure()
        {
            Balance = BalanceFactory != null ? BalanceFactory() : GameBalanceData.Default();
            MatchFactory.Configure(Balance, Board);
        }

        private SimulationState NewState()
        {
            BoardConfigData board = Board;
            DraftPlacement[] draft = Draft;
            return MatchFactory.Build(Balance, board, draft, new[]
            {
                new PlayerSetup { suits = new[] { (int)SuitType.Warrior }, districts = new int[0] },
                new PlayerSetup { suits = new[] { (int)SuitType.Warrior }, districts = new int[0] }
            });
        }

        /// <summary>
        /// What a player does on the tick their input applies. A pure function of
        /// (player, forTick): a move for one of their three villagers every few
        /// ticks. Some are refused by the rules; the refusal is deterministic, so
        /// it is part of the oracle rather than noise.
        /// </summary>
        public GameCommand[] Script(int player, int forTick)
        {
            if (CommandScript != null) return CommandScript(player, forTick);
            if (forTick < InputDelay || forTick % 5 != player + 1) return null;
            return new[]
            {
                new GameCommand
                {
                    type = CommandType.Move,
                    playerID = player,
                    villagerID = 3 * player + (forTick / 5) % 3,
                    targetNodeID = (forTick * 7 + player * 13) % nodeCount,
                    issuedOnTick = forTick
                }
            };
        }

        public LockstepScenario Run()
        {
            Configure();
            SimulationState probe = NewState();
            nodeCount = probe.nodes.Length;

            Links = new[]
            {
                new LinkDirection(ZeroToOne, Seed * 2 + 1),
                new LinkDirection(OneToZero, Seed * 2 + 2)
            };
            Peers = new[]
            {
                new HarnessPeer(0, NewState(), Links[0], Links[1], Script, () => Now, CaptureConfirmedTicks),
                new HarnessPeer(1, NewState(), Links[1], Links[0], Script, () => Now, CaptureConfirmedTicks)
            };

            int[] spikeUsed = new int[Spikes.Count];
            Now = 0;
            bool[] started = new bool[2];
            foreach (HarnessPeer peer in Peers)
            {
                if (StartAt[peer.Player] > 0) continue;
                started[peer.Player] = true;
                peer.Start(Now);
            }

            while (Now < Seconds)
            {
                foreach (HarnessPeer peer in Peers)
                {
                    if (!started[peer.Player])
                    {
                        if (Now < StartAt[peer.Player]) continue;
                        started[peer.Player] = true;
                        peer.Start(Now);
                    }
                    if (Now < peer.NextFrameAt) continue;
                    double interval = FrameInterval;
                    for (int i = 0; i < Spikes.Count; i++)
                    {
                        if (spikeUsed[i] == 0 && Spikes[i].peer == peer.Player && Now >= Spikes[i].at)
                        {
                            spikeUsed[i] = 1;
                            interval = Spikes[i].duration;
                        }
                    }
                    peer.Frame(Now, interval);
                }
                Now += FrameInterval;
            }

            Reference = ComputeReference((int)(Seconds * 10) + 50);
            return this;
        }

        /// <summary>The same match with no network: the hash at every checkpoint up to <paramref name="ticks"/>.</summary>
        private Dictionary<int, int> ComputeReference(int ticks)
        {
            var hashes = new Dictionary<int, int>();
            SimulationState state = NewState();
            for (int tick = 0; tick < ticks && !state.gameOver; tick++)
            {
                for (int player = 0; player < 2; player++)
                {
                    GameCommand[] commands = BlankReference ? null : Script(player, tick);
                    if (commands == null) continue;
                    for (int i = 0; i < commands.Length; i++) CommandProcessor.ProcessCommand(state, commands[i]);
                }
                GameSimulation.SimulateTick(state);
                if (tick > 0 && tick % CheckInterval == 0) hashes[state.tickCount] = SimulationStateHasher.ComputeHash(state);
            }
            return hashes;
        }

        // ===== Assertions =====

        /// <summary>
        /// No desync was reported and every confirmed checkpoint either peer
        /// recorded equals the reference. At least <paramref name="minimum"/>
        /// were recorded, or the comparison proves nothing.
        /// </summary>
        public void AssertMatchesReference(int minimum)
        {
            foreach (HarnessPeer peer in Peers)
            {
                Assert.IsEmpty(peer.DesyncTicks, Describe("P" + peer.Player + " reported a desync"));
                Assert.GreaterOrEqual(peer.Hashes.Count, minimum,
                    Describe("P" + peer.Player + " recorded too few checkpoints to compare"));
                foreach (KeyValuePair<int, int> pair in peer.Hashes)
                {
                    Assert.IsTrue(Reference.TryGetValue(pair.Key, out int expected),
                        Describe("no reference hash at tick " + pair.Key));
                    Assert.AreEqual(expected, pair.Value,
                        Describe("P" + peer.Player + " diverged from the reference at tick " + pair.Key));
                }
            }
        }

        /// <summary>The slower peer's highest checkpoint tick; how far the match really got.</summary>
        public int ConfirmedTicks()
        {
            int lowest = int.MaxValue;
            foreach (HarnessPeer peer in Peers)
            {
                int highest = 0;
                foreach (int tick in peer.Hashes.Keys) if (tick > highest) highest = tick;
                if (highest < lowest) lowest = highest;
            }
            return lowest;
        }

        public string Describe(string what)
        {
            return what + " (seed " + Seed + ")\n" + Summary();
        }

        public string Summary()
        {
            var text = new StringBuilder();
            foreach (HarnessPeer p in Peers)
            {
                LinkDirection sent = Links[p.Player];
                text.Append("P").Append(p.Player)
                    .Append(": tick ").Append(p.State.tickCount)
                    .Append(", shown ").Append(p.TicksShown)
                    .Append(", holds ").Append(p.Holds)
                    .Append(", spec starts ").Append(p.SpeculationStarts)
                    .Append(" (").Append(p.SpeculatingFrames).Append(" frames)")
                    .Append(", rollbacks ").Append(p.Rollbacks)
                    .Append(", frozen ").Append(p.FrozenSeconds.ToString("F2")).Append(" s")
                    .Append(", worst gap ").Append(p.WorstTickGap.ToString("F2")).Append(" s")
                    .Append(", dup ").Append(p.DuplicateDeliveries).Append("/").Append(p.InputDeliveries)
                    .Append(", sent ").Append(sent.Sent).Append(" lost ").Append(sent.Lost)
                    .Append(" (").Append(sent.BytesSent).Append(" B)")
                    .AppendLine();
            }
            return text.ToString();
        }

        /// <summary>The run's numbers in the test output, for the characterization tests.</summary>
        public void Report(string name)
        {
            TestContext.Out.WriteLine("[" + name + "] seed " + Seed + "\n" + Summary());
        }
    }
}
