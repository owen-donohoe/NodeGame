using System;
using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    internal static class CoreRulesFixture
    {
        public static GameBalanceData Balance(int tempo = 100)
        {
            var b = GameBalanceData.Default(); b.baseClaimPerTick = 10; b.claimThreshold = 1000;
            b.maxClaimersPerNode = 4; b.maxWorkersPerNode = 4; b.breachBarDecayPerTick = 7;
            b.tempoStageTicks = new[] { 1 }; b.tempoClaimPercent = new[] { tempo };
            b.tempoProductionPercent = b.tempoRespawnPercent = null;
            return b;
        }
        public static SimulationState Board(GameBalanceData b)
        {
            GameSimulation.SetBalance(b); CommandProcessor.SetBalance(b);
            var s = new SimulationState { nodes = new NodeData[8], villagers = new VillagerData[0],
                players = new[] { new PlayerData { playerID = 0, coreNodeID = 6, nextBreacherID = -1 },
                    new PlayerData { playerID = 1, coreNodeID = 7, nextBreacherID = -1 } } };
            for (int i = 0; i < 8; i++) s.nodes[i] = new NodeData { nodeID = i, ownerID = -1, links = new Link[0], terrain = TerrainType.Land };
            for (int i = 6; i < 8; i++) { s.nodes[i].districtType = DistrictType.Core; s.nodes[i].ownerID = i - 6; s.nodes[i].claimBar = i == 6 ? 1000 : -1000; }
            return s;
        }
        public static VillagerData Body(GameBalanceData b, int id, int owner, int node, VillagerState activity = VillagerState.Idle)
        {
            var v = TestBoardFactory.MakeIdleVillager(id, owner, node, b); v.state = activity;
            if (activity == VillagerState.Working) { v.suit = SuitType.Farmer; v.productionTicksMax = v.productionTicksRemaining = 5; }
            if (activity == VillagerState.Moving) { v.movePath = new[] { node, 5 }; v.moveSpeedTicks = 100; v.targetNodeID = 5; }
            if (activity == VillagerState.Dead) { v.hp = 0; v.respawnTicksRemaining = 100; }
            return v;
        }
        public static void Neighbours(SimulationState s, int target, int friends, int enemies, int attacker = 0)
        {
            var links = new Link[friends + enemies];
            for (int i = 0; i < links.Length; i++) { links[i] = new Link { toNodeID = i + 1, travelWeight = 1 }; s.nodes[i + 1].ownerID = i < friends ? attacker : 1 - attacker; }
            s.nodes[target].links = links;
        }
        public static int Fold(int hash, SimulationState s) => unchecked(hash * 31 + SimulationStateHasher.ComputeHash(s));
        public static void Determinism(Func<int> run) => Assert.AreEqual(run(), run());
    }
    public class RestoreTests
    {
        private static int Worker()
        {
            var b = CoreRulesFixture.Balance(); var s = CoreRulesFixture.Board(b);
            s.nodes[0].ownerID = 0; s.nodes[0].districtType = DistrictType.Farm; s.nodes[0].claimBar = 700;
            s.villagers = new[] { CoreRulesFixture.Body(b, 0, 0, 0, VillagerState.Working) };
            GameSimulation.SimulateTick(s);
            Assert.AreEqual(710, s.nodes[0].claimBar); Assert.AreEqual(4, s.villagers[0].productionTicksRemaining);
            Assert.AreEqual(VillagerState.Working, s.villagers[0].state); Assert.AreEqual(SuitType.Farmer, s.villagers[0].suit);
            return CoreRulesFixture.Fold(0, s);
        }
        private static int Cap()
        {
            int hash = 0;
            foreach (int owner in new[] { 0, 1 }) foreach (int start in new[] { 700, 990 })
            {
                var b = CoreRulesFixture.Balance(); var s = CoreRulesFixture.Board(b); int sign = owner == 0 ? 1 : -1;
                s.nodes[0].ownerID = owner; s.nodes[0].claimBar = sign * start;
                s.villagers = new VillagerData[6];
                for (int i = 0; i < 6; i++) s.villagers[i] = CoreRulesFixture.Body(b, i, owner, 0);
                s.villagers[0].suit = SuitType.Warrior;
                GameSimulation.SimulateTick(s);
                Assert.AreEqual(sign * (start == 700 ? 740 : 1000), s.nodes[0].claimBar);
                Assert.AreEqual(SuitType.Warrior, s.villagers[0].suit); hash = CoreRulesFixture.Fold(hash, s);
            }
            return hash;
        }
        private static int Mixed()
        {
            var b = CoreRulesFixture.Balance(); var s = CoreRulesFixture.Board(b);
            s.nodes[0].ownerID = 0; s.nodes[0].districtType = DistrictType.Farm; s.nodes[0].claimBar = 700;
            s.villagers = new[] { CoreRulesFixture.Body(b, 0, 0, 0, VillagerState.Working), CoreRulesFixture.Body(b, 1, 1, 0), CoreRulesFixture.Body(b, 2, 0, 6) };
            s.nodes[6].claimBar = 700;
            s.players[0].breachBar = 100; GameSimulation.SimulateTick(s);
            Assert.AreEqual(700, s.nodes[0].claimBar); Assert.AreEqual(93, s.players[0].breachBar);
            Assert.AreEqual(700, s.nodes[6].claimBar); return CoreRulesFixture.Fold(0, s);
        }
        private static int Excluded()
        {
            var b = CoreRulesFixture.Balance(); var s = CoreRulesFixture.Board(b); s.nodes[0].ownerID = 0; s.nodes[0].claimBar = 700;
            s.nodes[0].links = new[] { new Link { toNodeID = 5, travelWeight = 1 } };
            s.villagers = new[] { CoreRulesFixture.Body(b, 0, 0, 0, VillagerState.Moving), CoreRulesFixture.Body(b, 1, 0, 0, VillagerState.Dead), CoreRulesFixture.Body(b, 2, 0, 0) };
            s.villagers[2].isConsumed = true; GameSimulation.SimulateTick(s); Assert.AreEqual(700, s.nodes[0].claimBar);
            int hash = CoreRulesFixture.Fold(0, s);
            s = CoreRulesFixture.Board(b); s.nodes[0].ownerID = 0; s.nodes[0].claimBar = 700;
            s.nodes[0].links = new[] { new Link { toNodeID = 5, travelWeight = 1 } };
            s.villagers = new[] { CoreRulesFixture.Body(b, 0, 0, 0), CoreRulesFixture.Body(b, 1, 1, 0, VillagerState.Moving) };
            GameSimulation.SimulateTick(s); Assert.AreEqual(700, s.nodes[0].claimBar); return CoreRulesFixture.Fold(hash, s);
        }
        private static int Pending()
        {
            var b = CoreRulesFixture.Balance(); var s = CoreRulesFixture.Board(b);
            s.nodes[0].ownerID = 0; s.nodes[0].districtType = DistrictType.Farm; s.nodes[0].claimBar = 700;
            s.villagers = new[] { CoreRulesFixture.Body(b, 0, 0, 0) }; s.villagers[0].targetNodeID = 2;
            // Restores while the dangling target is held; resume then drops it (D48)
            // and the villager starts work, producing nothing this tick.
            GameSimulation.SimulateTick(s); Assert.AreEqual(710, s.nodes[0].claimBar);
            Assert.AreEqual(-1, s.villagers[0].targetNodeID); Assert.AreEqual(VillagerState.Working, s.villagers[0].state);
            Assert.AreEqual(s.villagers[0].productionTicksMax, s.villagers[0].productionTicksRemaining);
            Assert.AreEqual(0, s.players[0].food); return CoreRulesFixture.Fold(0, s);
        }
        private static int Tempo()
        {
            var b = CoreRulesFixture.Balance(150); var s = CoreRulesFixture.Board(b); s.nodes[0].ownerID = 0; s.nodes[0].claimBar = 700;
            CoreRulesFixture.Neighbours(s, 0, 2, 0); s.villagers = new[] { CoreRulesFixture.Body(b, 0, 0, 0) };
            GameSimulation.SimulateTick(s); Assert.AreEqual(715, s.nodes[0].claimBar); return CoreRulesFixture.Fold(0, s);
        }
        [Test] public void WorkerRestoresWithoutLosingProduction() => Worker();
        [Test] public void WorkerRestoresWithoutLosingProduction_Determinism() => CoreRulesFixture.Determinism(Worker);
        [Test] public void Restore_CapsFourIncludesIdleSoldier() => Cap();
        [Test] public void Restore_CapsFourIncludesIdleSoldier_Determinism() => CoreRulesFixture.Determinism(Cap);
        [Test] public void MixedPresenceAndCore_DoNotRestore() => Mixed();
        [Test] public void MixedPresenceAndCore_DoNotRestore_Determinism() => CoreRulesFixture.Determinism(Mixed);
        [Test] public void MovingDeadConsumed_DoNotRestore() => Excluded();
        [Test] public void MovingDeadConsumed_DoNotRestore_Determinism() => CoreRulesFixture.Determinism(Excluded);
        [Test] public void DanglingUnreachableTarget_RestoresThenWorks() => Pending();
        [Test] public void DanglingUnreachableTarget_RestoresThenWorks_Determinism() => CoreRulesFixture.Determinism(Pending);
        [Test] public void RestoreUsesTempoButNoFrontier() => Tempo();
        [Test] public void RestoreUsesTempoButNoFrontier_Determinism() => CoreRulesFixture.Determinism(Tempo);
    }
}
