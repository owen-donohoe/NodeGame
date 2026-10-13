using System;
using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    public class InfirmaryTests
    {
        private static GameBalanceData Configure(int tempo = 100)
        {
            var b = GameBalanceData.Default(); b.healIntervalTicks = 20; b.respawnCostFood = 5; b.tempoStageTicks = new[] { 1 }; b.tempoRespawnPercent = new[] { tempo };
            foreach (SuitType suit in new[] { SuitType.Warrior, SuitType.Guardian, SuitType.Scout, SuitType.Berserker, SuitType.Medic })
            {
                if (b.suitStats == null) b.suitStats = new SuitStats[0];
                Array.Resize(ref b.suitStats, b.suitStats.Length + 1);
                b.suitStats[b.suitStats.Length - 1] = new SuitStats { suitType = suit, foodCost = 3, materialCost = 2, bonusHP = 1, attackDamage = 1, moveSpeedTicks = 100, attackCooldownMax = 20 };
            }
            GameSimulation.SetBalance(b); CommandProcessor.SetBalance(b); return b;
        }
        private static SimulationState Board(GameBalanceData b, int bodies = 3)
        {
            var s = TestBoardFactory.BuildThreeNodeBoard(b);
            s.nodes[1].districtType = s.nodes[1].baseDistrictType = DistrictType.Infirmary;
            s.nodes[1].districtHealth = 3000; s.nodes[1].ownerID = 0; s.nodes[1].claimBar = b.claimThreshold;
            s.villagers = new VillagerData[bodies + 1];
            s.villagers[0] = TestBoardFactory.MakeIdleVillager(0, 0, 0, b);
            s.villagers[0].state = VillagerState.Dead; s.villagers[0].hp = 0; s.villagers[0].respawnTicksRemaining = 10;
            for (int i = 1; i <= bodies; i++)
            {
                s.villagers[i] = TestBoardFactory.MakeIdleVillager(i, 0, 1, b);
                s.villagers[i].suit = SuitType.Acolyte; s.villagers[i].state = VillagerState.Working;
            }
            s.players[0].food = s.players[0].materials = 30;
            return s;
        }
        private static void Repeat(Func<SimulationState> run, bool determinism)
        {
            int hash = SimulationStateHasher.ComputeHash(run());
            if (determinism) Assert.AreEqual(hash, SimulationStateHasher.ComputeHash(run()));
        }
        [TestCase(false, TestName = "HealsLocallyAtTenTicks_NotTwice")]
        [TestCase(true, TestName = "HealsLocallyAtTenTicks_NotTwice_Determinism")]
        public void Healing(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b, 1); s.villagers[1].hp = 3; s.villagers[1].maxHP = 5;
            s.tickCount = 9; GameSimulation.SimulateTick(s); Assert.AreEqual(4, s.villagers[1].hp);
            GameSimulation.SimulateTick(s); Assert.AreEqual(4, s.villagers[1].hp);
            s.tickCount = 19; GameSimulation.SimulateTick(s); Assert.AreEqual(5, s.villagers[1].hp); return s;
        }, determinism);
        [TestCase("enemy", false)] [TestCase("enemy", true, TestName = "{m}_Determinism{a}")]
        [TestCase("moving", false)] [TestCase("moving", true, TestName = "{m}_Determinism{a}")]
        [TestCase("fighting", false)] [TestCase("fighting", true, TestName = "{m}_Determinism{a}")]
        [TestCase("consumed", false)] [TestCase("consumed", true, TestName = "{m}_Determinism{a}")]
        public void LocalHealing_ExcludesIneligibleOccupants(string condition, bool determinism) => Repeat(() => {
            var b = Configure(); b.baseMoveSpeedTicks = 100; GameSimulation.SetBalance(b); CommandProcessor.SetBalance(b);
            var s = Board(b, 1); s.villagers[1].hp = 3; s.villagers[1].maxHP = 5; s.tickCount = 9;
            if (condition == "enemy") { s.nodes[1].ownerID = 1; s.nodes[1].claimBar = -b.claimThreshold; }
            if (condition == "consumed") s.villagers[1].isConsumed = true;
            if (condition == "moving") CommandProcessor.ProcessCommand(s, new GameCommand { type = CommandType.Move, playerID = 0, villagerID = 1, targetNodeID = 0 });
            if (condition == "fighting") {
                Array.Resize(ref s.villagers, 3); s.villagers[2] = TestBoardFactory.MakeIdleVillager(2, 1, 1, b);
                s.villagers[1].attackCooldownRemaining = s.villagers[2].attackCooldownRemaining = 100;
            }
            GameSimulation.SimulateTick(s); Assert.AreEqual(3, s.villagers[1].hp); return s;
        }, determinism);
        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism{a}")]
        public void CombatOccupant_KeepsSuitAndIdle_InfirmaryProducesNothing(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b); s.villagers[1].suit = SuitType.Medic; s.villagers[1].state = VillagerState.Idle;
            for (int i = 0; i < 50; i++) GameSimulation.SimulateTick(s);
            Assert.AreEqual(SuitType.Medic, s.villagers[1].suit); Assert.AreEqual(VillagerState.Idle, s.villagers[1].state);
            Assert.AreEqual(30, s.players[0].food); Assert.AreEqual(30, s.players[0].materials); Assert.AreEqual(0, s.players[0].metal); return s;
        }, determinism);
        [TestCase(100, 0, 7, false)] [TestCase(100, 0, 7, true, TestName = "{m}_Determinism{a}")]
        [TestCase(50, 0, 8, false)] [TestCase(50, 0, 8, true, TestName = "{m}_Determinism{a}")]
        public void InfirmaryWorkers_AccelerateCoreRespawns(int tempo, int tick, int remaining, bool determinism) => Repeat(() => {
            var b = Configure(tempo); var s = Board(b); s.tickCount = tick;
            GameSimulation.SimulateTick(s); Assert.AreEqual(remaining, s.villagers[0].respawnTicksRemaining);
            Assert.AreEqual(VillagerState.Idle, s.villagers[3].state);
            s.villagers[0].respawnTicksRemaining = 1; GameSimulation.SimulateTick(s);
            Assert.AreEqual(0, s.villagers[0].currentNodeID); Assert.AreEqual(VillagerState.Idle, s.villagers[0].state); return s;
        }, determinism);
        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism{a}")]
        public void TwoInfirmaries_WorkerCapsArePerNode(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b, 6);
            s.nodes[2].districtType = s.nodes[2].baseDistrictType = DistrictType.Infirmary; s.nodes[2].ownerID = 0; s.nodes[2].districtHealth = 3000;
            for (int i = 4; i <= 6; i++) s.villagers[i].currentNodeID = 2;
            GameSimulation.SimulateTick(s); Assert.AreEqual(5, s.villagers[0].respawnTicksRemaining); return s;
        }, determinism);
        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism{a}")]
        public void InfirmaryPaidDiscount_IsInherited(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b); Assert.AreEqual(3, CommandProcessor.GetRespawnCost(s, 0));
            CommandProcessor.ProcessCommand(s, new GameCommand { type = CommandType.Respawn, playerID = 0, villagerID = 0 });
            Assert.AreEqual(27, s.players[0].food); Assert.AreEqual(0, s.villagers[0].currentNodeID);
            b.respawnCostFood = 1; CommandProcessor.SetBalance(b); Assert.AreEqual(2, CommandProcessor.GetRespawnCost(s, 0));
            s.players[0].paidRespawns = 0; Assert.AreEqual(1, CommandProcessor.GetRespawnCost(s, 0)); return s;
        }, determinism);
        [TestCase("lost", false)] [TestCase("lost", true, TestName = "{m}_Determinism{a}")]
        [TestCase("contest", false)] [TestCase("contest", true, TestName = "{m}_Determinism{a}")]
        [TestCase("consumed", false)] [TestCase("consumed", true, TestName = "{m}_Determinism{a}")]
        [TestCase("dead", false)] [TestCase("dead", true, TestName = "{m}_Determinism{a}")]
        public void IneligibleWorkers_AddZero(string reason, bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b, 2);
            if (reason == "lost") { s.nodes[1].ownerID = -1; s.nodes[1].claimBar = 0; }
            if (reason == "contest") { Array.Resize(ref s.villagers, 4); s.villagers[3] = TestBoardFactory.MakeIdleVillager(3, 1, 1, b); }
            for (int i = 1; i <= 2; i++) { if (reason == "consumed") s.villagers[i].isConsumed = true; if (reason == "dead") { s.villagers[i].hp = 0; s.villagers[i].state = VillagerState.Dead; s.villagers[i].respawnTicksRemaining = 20; } }
            Assert.AreEqual(5, CommandProcessor.GetRespawnCost(s, 0)); GameSimulation.SimulateTick(s);
            Assert.AreEqual(9, s.villagers[0].respawnTicksRemaining); return s;
        }, determinism);
        [TestCase(SuitType.Warrior, false)] [TestCase(SuitType.Warrior, true, TestName = "{m}_Determinism{a}")]
        [TestCase(SuitType.Guardian, false)] [TestCase(SuitType.Guardian, true, TestName = "{m}_Determinism{a}")]
        [TestCase(SuitType.Scout, false)] [TestCase(SuitType.Scout, true, TestName = "{m}_Determinism{a}")]
        [TestCase(SuitType.Berserker, false)] [TestCase(SuitType.Berserker, true, TestName = "{m}_Determinism{a}")]
        [TestCase(SuitType.Medic, false)] [TestCase(SuitType.Medic, true, TestName = "{m}_Determinism{a}")]
        public void Barracks_EquipsEveryDraftedCombatSuit(SuitType suit, bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b, 1); s.nodes[1].districtType = DistrictType.Barracks;
            s.villagers[1].state = VillagerState.Idle; s.villagers[1].suit = SuitType.None;
            s.players[0].draftedSuits = new[] { (int)suit }; var cost = b.GetSuitStats(suit, 0);
            CommandProcessor.ProcessCommand(s, new GameCommand { type = CommandType.Equip, playerID = 0, villagerID = 1, value = (int)suit });
            Assert.AreEqual(suit, s.villagers[1].suit); Assert.AreEqual(30-cost.foodCost, s.players[0].food); Assert.AreEqual(30-cost.materialCost, s.players[0].materials); Assert.AreEqual(0, s.players[0].metal); return s;
        }, determinism);
        [TestCase(DistrictType.Infirmary, true)] [TestCase(DistrictType.Sanctuary, true)]
        [TestCase(DistrictType.Shrine, true)] [TestCase(DistrictType.Camp, true)] [TestCase(DistrictType.Arsenal, true)]
        [TestCase(DistrictType.Barracks, false)]
        public void EquipRefusal_PreservesHash(DistrictType district, bool drafted)
        { RunRefusal(district, drafted); }

        [TestCase(DistrictType.Infirmary, true)] [TestCase(DistrictType.Sanctuary, true)]
        [TestCase(DistrictType.Shrine, true)] [TestCase(DistrictType.Camp, true)] [TestCase(DistrictType.Arsenal, true)]
        [TestCase(DistrictType.Barracks, false)]
        public void EquipRefusal_PreservesHash_Determinism(DistrictType district, bool drafted)
        { Assert.AreEqual(SimulationStateHasher.ComputeHash(RunRefusal(district, drafted)), SimulationStateHasher.ComputeHash(RunRefusal(district, drafted))); }

        private static SimulationState RunRefusal(DistrictType district, bool drafted)
        {
            var b = Configure(); var s = Board(b, 1); s.nodes[1].districtType = district; s.villagers[1].state = VillagerState.Idle;
            s.players[0].draftedSuits = drafted ? new[] { (int)SuitType.Medic } : new int[0];
            int hash = SimulationStateHasher.ComputeHash(s);
            CommandProcessor.ProcessCommand(s, new GameCommand { type = CommandType.Equip, playerID = 0, villagerID = 1, value = (int)SuitType.Medic });
            Assert.AreEqual(hash, SimulationStateHasher.ComputeHash(s)); return s;
        }
    }
}
