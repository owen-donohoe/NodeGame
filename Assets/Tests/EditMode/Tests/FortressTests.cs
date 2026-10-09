using System;
using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    public class FortressTests
    {
        private static GameBalanceData Configure(int rate = 100)
        {
            var b = GameBalanceData.Default(); b.baseClaimPerTick = rate; b.decrementMultiplier = 1;
            b.captureBonusPercentPerStep = 0; b.tempoStageTicks = null; b.tempoClaimPercent = null;
            for (int i = 0; i < b.districtStats.Length; i++) b.districtStats[i].healthMax = b.districtStats[i].healthRegenPerTick = 0;
            GameSimulation.SetBalance(b); CommandProcessor.SetBalance(b); return b;
        }
        private static SimulationState Board(GameBalanceData b)
        {
            var s = TestBoardFactory.BuildThreeNodeBoard(b); s.villagers = new VillagerData[0];
            for (int i = 0; i < 3; i++) { s.nodes[i].ownerID = 0; s.nodes[i].claimBar = 1000; s.nodes[i].districtType = DistrictType.None; }
            s.nodes[0].districtType = DistrictType.Fortress;
            s.players[0].materials = 30; s.players[0].metal = 10; return s;
        }
        private static void Level(SimulationState s, int id, int value)
        {
            var field = typeof(NodeData).GetField("fortressLevel"); Assert.IsNotNull(field, "fortressLevel");
            object node = s.nodes[id]; field.SetValue(node, value); s.nodes[id] = (NodeData)node;
        }
        private static int Level(SimulationState s, int id) => (int)typeof(NodeData).GetField("fortressLevel").GetValue(s.nodes[id]);
        private static void Snapshot(SimulationState s, out int[] owners, out int[] percent, out int[] sources)
        {
            owners = Array.ConvertAll(s.nodes, n => n.ownerID);
            var method = typeof(GameSimulation).GetMethod("BuildResistanceSnapshot"); Assert.IsNotNull(method);
            object[] args = { s, owners, null, null }; method.Invoke(null, args);
            percent = (int[])args[2]; sources = (int[])args[3];
        }
        private static long Rate(SimulationState s, int target, int attacker = 1)
        {
            Snapshot(s, out var owners, out var percent, out _);
            var method = typeof(GameSimulation).GetMethod("ClaimRate", new[] { typeof(SimulationState), typeof(int), typeof(int), typeof(int), typeof(int[]), typeof(int[]) });
            Assert.IsNotNull(method); return (long)method.Invoke(null, new object[] { s, target, attacker, 1, owners, percent });
        }
        private static void Upgrade(SimulationState s, int currency, int villager = -1) => CommandProcessor.ProcessCommand(s,
            new GameCommand { type = (CommandType)7, playerID = 0, targetNodeID = 0, villagerID = villager, value = currency });
        private static void Repeat(Func<SimulationState> run, bool determinism)
        {
            int hash = SimulationStateHasher.ComputeHash(run());
            if (determinism) Assert.AreEqual(hash, SimulationStateHasher.ComputeHash(run()));
        }
        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void UpgradeSpendsChosenCurrencyOnly(bool determinism) => Repeat(() => {
            var s = Board(Configure()); Upgrade(s, 0); Assert.AreEqual(26, s.players[0].materials); Assert.AreEqual(10, s.players[0].metal); Assert.AreEqual(1, Level(s, 0));
            Upgrade(s, 1); Assert.AreEqual(26, s.players[0].materials); Assert.AreEqual(8, s.players[0].metal); Assert.AreEqual(2, Level(s, 0));
            Upgrade(s, 0); Assert.AreEqual(14, s.players[0].materials); Assert.AreEqual(8, s.players[0].metal); Assert.AreEqual(3, Level(s, 0));
            int hash = SimulationStateHasher.ComputeHash(s); Upgrade(s, 0); Assert.AreEqual(hash, SimulationStateHasher.ComputeHash(s)); return s;
        }, determinism);
        [TestCase("value", false)] [TestCase("value", true, TestName = "{m}_Determinism{a}")]
        [TestCase("villager", false)] [TestCase("villager", true, TestName = "{m}_Determinism{a}")]
        [TestCase("funds", false)] [TestCase("funds", true, TestName = "{m}_Determinism{a}")]
        [TestCase("enemy", false)] [TestCase("enemy", true, TestName = "{m}_Determinism{a}")]
        [TestCase("owner", false)] [TestCase("owner", true, TestName = "{m}_Determinism{a}")]
        [TestCase("player", false)] [TestCase("player", true, TestName = "{m}_Determinism{a}")]
        [TestCase("node", false)] [TestCase("node", true, TestName = "{m}_Determinism{a}")]
        [TestCase("district", false)] [TestCase("district", true, TestName = "{m}_Determinism{a}")]
        [TestCase("level", false)] [TestCase("level", true, TestName = "{m}_Determinism{a}")]
        [TestCase("missingArrays", false)] [TestCase("missingArrays", true, TestName = "{m}_Determinism{a}")]
        public void UpgradeRefusal_NoMutation(string reason, bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b); Level(s, 0, 0);
            if (reason == "funds") s.players[0].materials = 3;
            if (reason == "enemy") s.villagers = new[] { TestBoardFactory.MakeIdleVillager(0, 1, 0, b) };
            if (reason == "owner") s.nodes[0].ownerID = 1;
            if (reason == "district") s.nodes[0].districtType = DistrictType.None;
            if (reason == "level") Level(s, 0, -1);
            if (reason == "missingArrays") {
                for (int i = 0; i < b.districtStats.Length; i++) {
                    b.districtStats[i].fortressMaterialsCosts = null; b.districtStats[i].fortressMetalCosts = null; b.districtStats[i].fortressResistancePercent = null;
                }
                CommandProcessor.SetBalance(b); GameSimulation.SetBalance(b);
                Assert.IsTrue(b.CoreRulesValid(out _));
            }
            int hash = SimulationStateHasher.ComputeHash(s);
            CommandProcessor.ProcessCommand(s, new GameCommand { type = (CommandType)7, playerID = reason == "player" ? 2 : 0,
                targetNodeID = reason == "node" ? 3 : 0, villagerID = reason == "villager" ? 0 : -1, value = reason == "value" ? 3 : 0 });
            Assert.AreEqual(hash, SimulationStateHasher.ComputeHash(s)); return s;
        }, determinism);
        [TestCase(0, false, false)] [TestCase(0, false, true, TestName = "{m}_Determinism{a}")]
        [TestCase(1, false, false)] [TestCase(1, false, true, TestName = "{m}_Determinism{a}")]
        [TestCase(0, true, false)] [TestCase(0, true, true, TestName = "{m}_Determinism{a}")]
        [TestCase(1, true, false)] [TestCase(1, true, true, TestName = "{m}_Determinism{a}")]
        public void OwnershipLossClearsInvestment(int owner, bool directCapture, bool determinism) => Repeat(() => {
            var b = Configure(directCapture ? 100000 : 100); var s = Board(b);
            s.nodes[0].ownerID = owner; s.nodes[0].claimBar = owner == 0 ? 1 : -1; Level(s, 0, 3);
            s.villagers = new[] { TestBoardFactory.MakeIdleVillager(0, 1 - owner, 0, b) };
            GameSimulation.SimulateTick(s); Assert.AreEqual(directCapture ? 1 - owner : -1, s.nodes[0].ownerID); Assert.AreEqual(0, Level(s, 0)); return s;
        }, determinism);
        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void ClaimResistance_FollowsFrontierAndTempo_UsesEra(bool determinism) => Repeat(() => {
            var b = Configure(10); b.captureBonusPercentPerStep = 50; b.tempoStageTicks = new[] { 1 }; b.tempoClaimPercent = new[] { 150 };
            int index = Array.FindIndex(b.districtStats, d => d.districtType == DistrictType.Fortress && d.era == 2);
            b.districtStats[index].fortressResistancePercent = new[] { 0, 30, 60, 100 }; GameSimulation.SetBalance(b);
            var s = Board(b); s.tickCount = 1; s.nodes[0].districtEra = 2; s.nodes[1].ownerID = 1; Level(s, 0, 3);
            // One attacking neighbour: 10*150/100=15; tempo floors to22; resistance halves to11.
            Assert.AreEqual(11, Rate(s, 0)); return s;
        }, determinism);
        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void HighestAuraWinsAndDiminishes(bool determinism) => Repeat(() => {
            var s = Board(Configure()); int[] expected = { 100, 80, 71, 66 };
            for (int i = 0; i <= 3; i++) { Level(s, 0, i); Assert.AreEqual(expected[i], Rate(s, 0)); }
            s.nodes[2].districtType = DistrictType.Fortress; Level(s, 2, 1); Assert.AreEqual(66, Rate(s, 1));
            Level(s, 2, 3); Snapshot(s, out _, out var percent, out var sources); Assert.AreEqual(50, percent[1]); Assert.AreEqual(0, sources[1]); Assert.AreEqual(66, Rate(s, 1));
            s.nodes[0].links = new[] { new Link { toNodeID = 1, travelWeight = 1 }, new Link { toNodeID = 2, travelWeight = 1 } };
            Array.Reverse(s.nodes[0].links); Array.Reverse(s.nodes[1].links);
            Snapshot(s, out _, out percent, out sources); Assert.AreEqual(0, sources[1]); Assert.AreEqual(66, Rate(s, 1)); return s;
        }, determinism);
        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void AuraOnlyCoversOwnedSelfAndLinkedNodes(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b); Level(s, 0, 3); Assert.AreEqual(66, Rate(s, 1)); Assert.AreEqual(100, Rate(s, 2));
            s.nodes[1].ownerID = 1; Assert.AreEqual(100, Rate(s, 1, 0)); s.nodes[1].ownerID = -1; Assert.AreEqual(100, Rate(s, 1));
            s.nodes[1].ownerID = 0; b = Configure(10); s.villagers = new[] { TestBoardFactory.MakeIdleVillager(0, 0, 1, b) };
            GameSimulation.SimulateTick(s); Assert.AreEqual(1010, s.nodes[1].claimBar); return s;
        }, determinism);
        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void NeutralLeanGetsNoResistance(bool determinism) => Repeat(() => {
            var s = Board(Configure()); Level(s, 0, 3); s.nodes[1].ownerID = -1; s.nodes[1].claimBar = 1000; Assert.AreEqual(100, Rate(s, 1)); return s;
        }, determinism);
        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void TinyHostileRateFloorsToOne(bool determinism) => Repeat(() => {
            var s = Board(Configure(1)); Level(s, 0, 3); Assert.AreEqual(1, Rate(s, 1)); return s;
        }, determinism);
        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void CoreBreach_UsesSameResistance(bool determinism) => Repeat(() => {
            var b = Configure(); b.breachSwarmRate = new[] { 50 }; b.captureBonusPercentPerStep = 50;
            GameSimulation.SetBalance(b); var s = Board(b); s.nodes[2].districtType = DistrictType.Core; s.nodes[2].ownerID = 1;
            s.nodes[1].districtType = DistrictType.Fortress; s.nodes[1].ownerID = 1; Level(s, 1, 3);
            Array.Resize(ref s.nodes, 4); s.nodes[3] = new NodeData { nodeID = 3, ownerID = 0, links = new[] { new Link { toNodeID = 2, travelWeight = 1 } } };
            s.nodes[0].links = new[] { new Link { toNodeID = 2, travelWeight = 1 } }; s.nodes[2].links = new[] { new Link { toNodeID = 0, travelWeight = 1 }, new Link { toNodeID = 1, travelWeight = 1 }, new Link { toNodeID = 3, travelWeight = 1 } };
            s.villagers = new[] { TestBoardFactory.MakeIdleVillager(0, 0, 2, b) }; s.villagers[0].state = VillagerState.Breaching;
            GameSimulation.SimulateTick(s); Assert.AreEqual(50, s.players[1].breachBar); return s;
        }, determinism);
        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void AuraSnapshot_NoSameTickCascade_AndOwnershipLossClearsInvestment(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b); Level(s, 0, 3); s.nodes[0].claimBar = 1;
            s.villagers = new[] { TestBoardFactory.MakeIdleVillager(0, 1, 0, b), TestBoardFactory.MakeIdleVillager(1, 1, 1, b) };
            GameSimulation.SimulateTick(s); Assert.AreEqual(-1, s.nodes[0].ownerID); Assert.AreEqual(0, Level(s, 0)); Assert.AreEqual(934, s.nodes[1].claimBar);
            GameSimulation.SimulateTick(s); Assert.AreEqual(834, s.nodes[1].claimBar);
            s.nodes[0].claimBar = -b.claimThreshold + 1; GameSimulation.SimulateTick(s); Assert.AreEqual(1, s.nodes[0].ownerID); Assert.AreEqual(0, Level(s, 0));
            Assert.AreEqual(b.baseHP, s.villagers[0].maxHP); Assert.AreEqual(b.baseAttackDamage, s.villagers[0].attackDamage); return s;
        }, determinism);
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void Level_HashCopyAndRollback(int level)
        {
            var s = Board(Configure()); Level(s, 0, level); var copy = new SimulationState(); copy.CopyFrom(s);
            Assert.AreEqual(level, Level(copy, 0)); int hash = SimulationStateHasher.ComputeHash(s);
            Level(copy, 0, (level + 1) % 4); Assert.AreNotEqual(hash, SimulationStateHasher.ComputeHash(copy)); Assert.AreEqual(level, Level(s, 0));
            copy.CopyFrom(s); Assert.AreEqual(hash, SimulationStateHasher.ComputeHash(copy));
            if (level > 0) { Level(s, 0, 0); Level(s, 1, level); Assert.AreNotEqual(hash, SimulationStateHasher.ComputeHash(s)); }
        }
    }
}
