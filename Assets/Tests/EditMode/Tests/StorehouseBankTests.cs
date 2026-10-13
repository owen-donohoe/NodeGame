using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Tests
{
    public class StorehouseBankTests
    {
        private static void Timer(SimulationState s, int value)
        { var f = typeof(NodeData).GetField("bankProductionRemaining"); Assert.IsNotNull(f); object n = s.nodes[1]; f.SetValue(n, value); s.nodes[1] = (NodeData)n; }
        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void HealthyStorehouseAlternatesCarriesOvershootAndPauses(bool determinism) => DistrictHealthTests.Repeat(() => {
            var b = DistrictHealthTests.Configure(); var s = DistrictHealthTests.Board(b, DistrictType.Storehouse);
            Timer(s, 80); DistrictHealthTests.Health(s, 0); DistrictHealthTests.Tick(s, 79); Assert.AreEqual(0, BankRules.Total(s.nodes[1]));
            DistrictHealthTests.Health(s, 2999); DistrictHealthTests.Tick(s); Assert.AreEqual(0, BankRules.Total(s.nodes[1]));
            DistrictHealthTests.Tick(s, 79); Assert.AreEqual(0, BankRules.Total(s.nodes[1]));
            DistrictHealthTests.Tick(s); Assert.AreEqual(1, s.nodes[1].bankFood);
            DistrictHealthTests.Tick(s, 80); Assert.AreEqual(1, s.nodes[1].bankMaterials);
            b.tempoStageTicks = new[] { 1 }; b.tempoProductionPercent = new[] { 200 }; MatchFactory.Configure(b, BoardFixtures.LandGrid3x3());
            Timer(s, 1); DistrictHealthTests.Tick(s); Assert.AreEqual(2, s.nodes[1].bankFood);
            Assert.AreEqual(79, typeof(NodeData).GetField("bankProductionRemaining").GetValue(s.nodes[1]));
            s.nodes[1].bankFood = 4; DistrictHealthTests.Tick(s, 5);
            Assert.AreEqual(79, typeof(NodeData).GetField("bankProductionRemaining").GetValue(s.nodes[1]));
            s.nodes[1].bankFood = 0; s.nodes[1].ownerID = -1; DistrictHealthTests.Tick(s);
            Assert.AreEqual(79, typeof(NodeData).GetField("bankProductionRemaining").GetValue(s.nodes[1])); return s;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void SameOwnerReclaimPaysOnceAndProducesAfterHealing(bool determinism) => DistrictHealthTests.Repeat(() => {
            var b = DistrictHealthTests.Configure(); var s = DistrictHealthTests.Board(b, DistrictType.Storehouse);
            Timer(s, 80); DistrictHealthTests.Health(s, 0); s.nodes[1].bankFood = 2; s.nodes[1].claimBar = 1;
            s.villagers = new[] { TestBoardFactory.MakeIdleVillager(0, 1, 1, b) }; DistrictHealthTests.Tick(s);
            Assert.AreEqual(-1, s.nodes[1].ownerID); Assert.AreEqual(2, s.nodes[1].bankFood);
            s.nodes[1].claimBar = b.claimThreshold - 1; s.villagers = new[] { TestBoardFactory.MakeIdleVillager(0, 0, 1, b) };
            DistrictHealthTests.Tick(s); Assert.AreEqual(0, s.nodes[1].ownerID); Assert.AreEqual(2, s.players[0].food); Assert.AreEqual(0, BankRules.Total(s.nodes[1]));
            s.villagers = new VillagerData[0]; DistrictHealthTests.Tick(s, 176); Assert.AreEqual(3000, DistrictHealthTests.Health(s));
            DistrictHealthTests.Tick(s, 80); Assert.AreEqual(1, s.nodes[1].bankFood); Assert.AreEqual(2, s.players[0].food); return s;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void CaptureLootClampsAndPaysOnlyOnce(bool determinism) => DistrictHealthTests.Repeat(() => {
            var b = DistrictHealthTests.Configure(); var s = DistrictHealthTests.Board(b, DistrictType.Storehouse); Timer(s, 80);
            DistrictHealthTests.Health(s, 0); s.nodes[1].ownerID = -1; s.nodes[1].claimBar = -b.claimThreshold + 1;
            s.nodes[1].bankFood = 2; s.nodes[1].bankMaterials = 2; s.nodes[1].bankMetal = 1; s.players[1].food = 29; s.players[1].metal = 10;
            s.villagers = new[] { TestBoardFactory.MakeIdleVillager(0, 1, 1, b) }; DistrictHealthTests.Tick(s);
            Assert.AreEqual(30, s.players[1].food); Assert.AreEqual(2, s.players[1].materials); Assert.AreEqual(10, s.players[1].metal); Assert.AreEqual(0, BankRules.Total(s.nodes[1]));
            DistrictHealthTests.Tick(s); Assert.AreEqual(2, s.players[1].materials); return s;
        }, determinism);
    }
}
