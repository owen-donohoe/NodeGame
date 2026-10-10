using System;
using System.Reflection;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Tests
{
    public class DistrictHealthTests
    {
        internal static GameBalanceData Configure()
        {
            var b = GameBalanceData.Default();
            b.tempoStageTicks = b.tempoClaimPercent = b.tempoProductionPercent = b.tempoRespawnPercent = null;
            b.captureBonusPercentPerStep = 0;
            MatchFactory.Configure(b, BoardFixtures.LandGrid3x3());
            return b;
        }
        internal static SimulationState Board(GameBalanceData b, DistrictType type)
        {
            var s = TestBoardFactory.BuildThreeNodeBoard(b);
            s.villagers = new VillagerData[0];
            s.nodes[1].districtType = s.nodes[1].baseDistrictType = type;
            s.nodes[1].ownerID = 0; s.nodes[1].claimBar = b.claimThreshold;
            return s;
        }
        // Reflection allows the new behavior tests to fail before the state is introduced.
        internal static void Health(SimulationState s, int value)
        {
            FieldInfo f = typeof(NodeData).GetField("districtHealth"); Assert.IsNotNull(f);
            object n = s.nodes[1]; f.SetValue(n, value); s.nodes[1] = (NodeData)n;
        }
        internal static int Health(SimulationState s) => (int)typeof(NodeData).GetField("districtHealth").GetValue(s.nodes[1]);
        internal static void Tick(SimulationState s, int count = 1)
        { for (int i = 0; i < count; i++) GameSimulation.SimulateTick(s); }
        internal static void Repeat(Func<SimulationState> run, bool determinism)
        { int h = SimulationStateHasher.ComputeHash(run()); if (determinism) Assert.AreEqual(h, SimulationStateHasher.ComputeHash(run())); }
        private static bool Healthy(SimulationState s)
        {
            var t = typeof(GameSimulation).Assembly.GetType("NodeWar.Simulation.DistrictHealth"); Assert.IsNotNull(t);
            return (bool)t.GetMethod("Healthy").Invoke(null, new object[] { s, s.nodes[1] });
        }

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void ForgeMinionOnStorehouseRefusedAndHashNeutral(bool determinism) => Repeat(() => {
            var s = Board(Configure(), DistrictType.Storehouse); s.players[0].metal = 3;
            Assert.IsTrue(CommandTypes.IsKnown((CommandType)8));
            int h = SimulationStateHasher.ComputeHash(s);
            CommandProcessor.ProcessCommand(s, new GameCommand { type = (CommandType)8, playerID = 0, targetNodeID = 1, villagerID = -1 });
            Assert.AreEqual(h, SimulationStateHasher.ComputeHash(s)); return s;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void DrainConsumesRateBeforeBarForBothOwners(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b, DistrictType.Storehouse);
            for (int owner = 0; owner <= 1; owner++) {
                s = Board(b, DistrictType.Storehouse); s.nodes[1].ownerID = owner;
                s.nodes[1].claimBar = owner == 0 ? b.claimThreshold : -b.claimThreshold;
                Health(s, 100); // Rate 17 * decrement 4 = 68; full bar allows +17 regen on tick 1.
                s.villagers = new[] { TestBoardFactory.MakeIdleVillager(0, 1 - owner, 1, b) };
                Tick(s); Assert.AreEqual(49, Health(s)); Assert.AreEqual(owner == 0 ? 10000 : -10000, s.nodes[1].claimBar);
                Tick(s); Assert.AreEqual(0, Health(s)); Assert.AreEqual(owner == 0 ? 9981 : -9981, s.nodes[1].claimBar);
            }
            return s;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void RegenRequiresOwnedFullBarAndIsLinearClamped(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b, DistrictType.Storehouse); Health(s, 0);
            Tick(s, 2); Assert.AreEqual(34, Health(s)); Assert.IsFalse(Healthy(s));
            s.nodes[1].claimBar--; Tick(s, 2); Assert.AreEqual(34, Health(s));
            s.nodes[1].ownerID = -1; s.nodes[1].claimBar = b.claimThreshold; Tick(s); Assert.AreEqual(34, Health(s));
            s.nodes[1].ownerID = 1; s.nodes[1].claimBar = -b.claimThreshold;
            Health(s, 2980); Tick(s); Assert.AreEqual(2997, Health(s)); Assert.IsFalse(Healthy(s));
            Tick(s); Assert.AreEqual(3000, Health(s)); Assert.IsTrue(Healthy(s)); return s;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void OwnershipTransitionsResetHealth(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b, DistrictType.Storehouse);
            Health(s, 1); s.nodes[1].claimBar = 1;
            s.villagers = new[] { TestBoardFactory.MakeIdleVillager(0, 1, 1, b) };
            Tick(s); Assert.AreEqual(-1, s.nodes[1].ownerID); Assert.AreEqual(0, Health(s));
            Health(s, 50); s.nodes[1].claimBar = -b.claimThreshold + 1;
            Tick(s); Assert.AreEqual(1, s.nodes[1].ownerID);
            // Reset in claiming, then the first linear regen increment in healing.
            Assert.AreEqual(17, Health(s)); return s;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void NoHealthDistrictAlwaysHealthy(bool determinism) => Repeat(() => {
            var s = Board(Configure(), DistrictType.Farm); Health(s, 123);
            Assert.IsTrue(Healthy(s)); Tick(s); Assert.AreEqual(123, Health(s)); return s;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void FortressAuraUsesTickStartHealth(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b, DistrictType.Fortress); s.nodes[1].fortressLevel = 3;
            Health(s, 0); GameSimulation.BuildResistanceSnapshot(s, new[] { 0, 0, 1 }, out var r, out _); Assert.AreEqual(0, r[1]);
            Health(s, 2999); GameSimulation.BuildResistanceSnapshot(s, new[] { 0, 0, 1 }, out r, out _); Assert.AreEqual(0, r[1]);
            Health(s, 3000); s.villagers = new[] { TestBoardFactory.MakeIdleVillager(0, 1, 1, b) };
            Tick(s); Assert.AreEqual(2972, Health(s)); // floor(68/1.5)=45, followed by 17 regen while full bar.
            Tick(s); Assert.AreEqual(2921, Health(s)); // next tick has no aura: -68 +17.
            return s;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void InfirmaryHealingBoostAndPriceRequireFullHealth(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b, DistrictType.Infirmary);
            s.villagers = new[] { TestBoardFactory.MakeIdleVillager(0, 0, 1, b), TestBoardFactory.MakeIdleVillager(1, 0, 0, b) };
            s.villagers[0].hp = 1; s.villagers[0].state = VillagerState.Working; s.villagers[0].suit = SuitType.Acolyte;
            s.villagers[1].state = VillagerState.Dead; s.villagers[1].hp = 0; s.villagers[1].respawnTicksRemaining = 50;
            s.players[0].paidRespawns = 4;
            Health(s, 0); Assert.AreEqual(0, GameSimulation.CountInfirmaryWorkers(s, 1, 0));
            Assert.AreEqual(5, CommandProcessor.GetRespawnCost(s, 0));
            s.tickCount = 9; Tick(s); Assert.AreEqual(1, s.villagers[0].hp); Assert.AreEqual(49, s.villagers[1].respawnTicksRemaining);
            Health(s, 2982); s.tickCount = 19; Tick(s); Assert.AreEqual(1, s.villagers[0].hp); Assert.AreEqual(48, s.villagers[1].respawnTicksRemaining);
            Health(s, 3000); Assert.AreEqual(1, GameSimulation.CountInfirmaryWorkers(s, 1, 0)); Assert.AreEqual(4, CommandProcessor.GetRespawnCost(s, 0));
            s.tickCount = 39; Tick(s); Assert.AreEqual(2, s.villagers[0].hp); Assert.AreEqual(46, s.villagers[1].respawnTicksRemaining); return s;
        }, determinism);
    }
}
