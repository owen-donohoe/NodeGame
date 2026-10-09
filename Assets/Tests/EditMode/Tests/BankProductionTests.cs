using System;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Tests
{
    public class BankProductionTests
    {
        private static GameBalanceData Configure()
        {
            var b = GameBalanceData.Default();
            b.tempoStageTicks = b.tempoProductionPercent = b.tempoClaimPercent = b.tempoRespawnPercent = null;
            b.captureBonusPercentPerStep = 0;
            MatchFactory.Configure(b, BoardFixtures.LandGrid3x3());
            return b;
        }
        private static SimulationState Board(GameBalanceData b, DistrictType type = DistrictType.Farm)
        {
            var s = TestBoardFactory.BuildThreeNodeBoard(b);
            s.nodes[1].districtType = s.nodes[1].baseDistrictType = type;
            s.nodes[1].upgradeCategory = DistrictUpgradeCategory.Fixed;
            s.nodes[1].ownerID = 0; s.nodes[1].claimBar = b.claimThreshold;
            s.villagers = new VillagerData[0];
            s.players[0].food = s.players[0].materials = s.players[0].metal = 0;
            return s;
        }
        private static void Minion(SimulationState s, int timer = 1)
        { s.nodes[1].structureKind = StructureKind.Minion; s.nodes[1].structureHP = 16; s.nodes[1].minionProductionRemaining = timer; }
        private static void Tick(SimulationState s, int count = 1)
        { for (int i = 0; i < count; i++) { GameSimulation.SimulateTick(s); Assert.IsTrue(BankRules.Total(s.nodes[1]) == 0 || s.nodes[1].structureKind == StructureKind.Minion); } }
        private static GameCommand Install(int player = 0, int value = 0) => new GameCommand
        { type = (CommandType)8, playerID = player, targetNodeID = 1, villagerID = -1, value = value, issuedOnTick = 123 };
        private static void Repeat(Func<SimulationState> run, bool determinism)
        { int hash = SimulationStateHasher.ComputeHash(run()); if (determinism) Assert.AreEqual(hash, SimulationStateHasher.ComputeHash(run())); }

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void OrdinaryFarmPaysPool_MinionedFarmPaysBank(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b);
            s.villagers = new[] { TestBoardFactory.MakeIdleVillager(0, 0, 1, b) };
            s.villagers[0].state = VillagerState.Working; s.villagers[0].suit = SuitType.Farmer; s.villagers[0].productionTicksMax = 30; s.villagers[0].productionTicksRemaining = 1;
            Tick(s); Assert.AreEqual(1, s.players[0].food); Assert.AreEqual(0, s.nodes[1].bankFood);
            s = Board(Configure()); Minion(s); Tick(s);
            Assert.AreEqual(0, s.players[0].food); Assert.AreEqual(1, s.nodes[1].bankFood); return s;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void InstallMinion_ChargesThreeMetalAndOneWorkerPosition(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b); s.players[0].metal = 3;
            s.villagers = new[] { TestBoardFactory.MakeIdleVillager(8, 0, 1, b), TestBoardFactory.MakeIdleVillager(4, 0, 1, b) };
            for (int i = 0; i < 2; i++) { s.villagers[i].state = VillagerState.Working; s.villagers[i].suit = SuitType.Farmer; s.villagers[i].productionTicksMax = s.villagers[i].productionTicksRemaining = 30; }
            CommandProcessor.ProcessCommand(s, Install());
            Assert.AreEqual(0, s.players[0].metal); Assert.AreEqual(16, s.nodes[1].structureHP);
            Assert.AreEqual(StructureKind.Minion, s.nodes[1].structureKind);
            Assert.AreEqual(VillagerState.Working, s.villagers[1].state); Assert.AreEqual(VillagerState.Idle, s.villagers[0].state);
            int hash = SimulationStateHasher.ComputeHash(s); CommandProcessor.ProcessCommand(s, Install()); Assert.AreEqual(hash, SimulationStateHasher.ComputeHash(s));
            Tick(s); Assert.AreEqual(VillagerState.Working, s.villagers[1].state); Assert.AreEqual(VillagerState.Idle, s.villagers[0].state);
            foreach (var type in new[] { DistrictType.Infirmary, DistrictType.Pier, DistrictType.Fortress, DistrictType.Core }) {
                var refused = Board(b, type); refused.players[0].metal = 3; hash = SimulationStateHasher.ComputeHash(refused);
                CommandProcessor.ProcessCommand(refused, Install()); Assert.AreEqual(hash, SimulationStateHasher.ComputeHash(refused));
            }
            return s;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void Storehouse_FirstClaimGrantsMinionOnce(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b, (DistrictType)18);
            s.nodes[1].ownerID = -1; s.nodes[1].claimBar = b.claimThreshold - 1;
            s.villagers = new[] { TestBoardFactory.MakeIdleVillager(0, 0, 1, b) }; Tick(s);
            Assert.AreEqual(16, s.nodes[1].structureHP); Assert.IsTrue(s.nodes[1].storehouseInitialised);
            Assert.AreEqual(VillagerState.Idle, s.villagers[0].state); Assert.AreEqual(SuitType.None, s.villagers[0].suit);
            Tick(s, 79); Assert.AreEqual(1, s.nodes[1].bankFood); Assert.AreEqual(0, s.nodes[1].bankMaterials);
            Tick(s, 80); Assert.AreEqual(1, s.nodes[1].bankMaterials); Assert.AreEqual(0, s.players[0].food);
            s.nodes[1] = StructureRules.Destroy(s.nodes[1]);
            Assert.IsTrue(s.nodes[1].storehouseInitialised); Assert.AreEqual(0, s.nodes[1].minionProductionRemaining);
            s.nodes[1].ownerID = -1; s.nodes[1].claimBar = b.claimThreshold - 1; Tick(s);
            Assert.AreEqual(0, s.nodes[1].structureHP); Assert.AreEqual(StructureKind.None, s.nodes[1].structureKind);
            s.players[0].metal = 3; CommandProcessor.ProcessCommand(s, Install()); Assert.AreEqual(16, s.nodes[1].structureHP); return s;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void BankCapacityIsFiveTotal(bool determinism) => Repeat(() => {
            var s = Board(Configure(), DistrictType.Forge); Minion(s); s.nodes[1].bankFood = 3; s.nodes[1].bankMaterials = 2;
            s.nodes[1].materialAllocation = 1; s.players[0].materials = 3;
            Tick(s, 100); Assert.AreEqual(1, s.nodes[1].minionProductionRemaining); Assert.AreEqual(3, s.players[0].materials);
            s.nodes[1].bankFood--; Tick(s); Assert.AreEqual(1, s.nodes[1].bankMetal); Assert.AreEqual(2, s.players[0].materials);
            Assert.AreEqual(50, s.nodes[1].minionProductionRemaining); Tick(s, 100); Assert.AreEqual(50, s.nodes[1].minionProductionRemaining); return s;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void ForgeMinion_RequiresAllocationMaterialAndBankRoom(bool determinism) => Repeat(() => {
            SimulationState s = null;
            for (int missing = -1; missing < 3; missing++) {
                s = Board(Configure(), DistrictType.Forge); Minion(s); s.nodes[1].materialAllocation = missing == 0 ? 0 : 1;
                s.players[0].materials = missing == 1 ? 0 : 1; if (missing == 2) s.nodes[1].bankFood = 5;
                Tick(s); Assert.AreEqual(missing == -1 ? 1 : 0, s.nodes[1].bankMetal);
                Assert.AreEqual(missing == -1 || missing == 1 ? 0 : 1, s.players[0].materials);
                Assert.AreEqual(missing == 2 ? 1 : 50, s.nodes[1].minionProductionRemaining);
            }
            return s;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void MinionNeutralisation_DormantUntilFullClaim(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b); Minion(s); s.nodes[1].structureHP = 8; s.nodes[1].bankFood = 2; s.nodes[1].claimBar = 1;
            s.villagers = new[] { TestBoardFactory.MakeIdleVillager(0, 1, 1, b) }; Tick(s);
            Assert.AreEqual(-1, s.nodes[1].ownerID); Assert.AreEqual(8, s.nodes[1].structureHP); Assert.AreEqual(2, s.nodes[1].bankFood);
            Assert.IsTrue(BankRules.Locked(s, s.nodes[1], b)); Assert.AreEqual(1, s.nodes[1].minionProductionRemaining);
            s.villagers = new VillagerData[0]; Tick(s, 20); Assert.AreEqual(1, s.nodes[1].minionProductionRemaining);
            // The previous owner must also destroy its dormant minion on a full claim.
            s.nodes[1].claimBar = b.claimThreshold - 1; s.villagers = new[] { TestBoardFactory.MakeIdleVillager(0, 0, 1, b) }; Tick(s);
            Assert.AreEqual(StructureKind.None, s.nodes[1].structureKind); Assert.AreEqual(0, s.nodes[1].structureHP);
            Assert.AreEqual(0, s.nodes[1].bankFood); Assert.AreEqual(2, s.players[0].food); return s;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void InstallMinion_InvalidValueOwnerAndLockRefuseWithoutMutation(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b); s.players[0].metal = 3;
            foreach (var command in new[] { Install(0, 1), Install(1), Install(-1) }) {
                int hash = SimulationStateHasher.ComputeHash(s); CommandProcessor.ProcessCommand(s, command); Assert.AreEqual(hash, SimulationStateHasher.ComputeHash(s));
            }
            s.nodes[1].claimBar--; int before = SimulationStateHasher.ComputeHash(s); CommandProcessor.ProcessCommand(s, Install()); Assert.AreEqual(before, SimulationStateHasher.ComputeHash(s));
            s.nodes[1].claimBar++; s.villagers = new[] { TestBoardFactory.MakeIdleVillager(0, 1, 1, b) };
            s.villagers[0].state = VillagerState.Moving; s.villagers[0].movePath = new[] { 1, 2 }; before = SimulationStateHasher.ComputeHash(s);
            CommandProcessor.ProcessCommand(s, Install()); Assert.AreEqual(before, SimulationStateHasher.ComputeHash(s));
            Minion(s); Tick(s); Assert.AreEqual(1, s.nodes[1].bankFood, "Enemy locks cash-out, not production"); return s;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void CaptureLoot_ClampsEveryPoolAndDestroysExactlyOnce(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b, DistrictType.Storehouse); Minion(s);
            s.nodes[1].storehouseInitialised = true; s.nodes[1].ownerID = -1; s.nodes[1].claimBar = -b.claimThreshold + 1;
            s.nodes[1].bankFood = 2; s.nodes[1].bankMaterials = 2; s.nodes[1].bankMetal = 1;
            s.players[1].food = 29; s.players[1].materials = 30; s.players[1].metal = 10;
            s.villagers = new[] { TestBoardFactory.MakeIdleVillager(0, 1, 1, b) }; Tick(s);
            Assert.AreEqual(1, s.nodes[1].ownerID); Assert.AreEqual(StructureKind.None, s.nodes[1].structureKind);
            Assert.AreEqual(0, BankRules.Total(s.nodes[1])); Assert.AreEqual(0, s.nodes[1].minionProductionRemaining);
            Assert.AreEqual(30, s.players[1].food); Assert.AreEqual(30, s.players[1].materials); Assert.AreEqual(10, s.players[1].metal);
            Assert.IsTrue(s.nodes[1].storehouseInitialised); Tick(s); Assert.AreEqual(30, s.players[1].food); return s;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void AttackerDestroysMinion_ClearsAutomationWithLootDeferredToD3(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b); Minion(s); s.nodes[1].structureHP = 1; s.nodes[1].bankFood = 2;
            s.nodes[1].storehouseNextResource = 1; s.nodes[1].minionProductionRemaining = 9;
            s.villagers = new[] { TestBoardFactory.MakeIdleVillager(0, 1, 1, b) }; s.villagers[0].suit = SuitType.Warrior;
            Tick(s); Assert.AreEqual(StructureKind.None, s.nodes[1].structureKind); Assert.AreEqual(0, s.nodes[1].structureHP);
            Assert.AreEqual(0, s.nodes[1].minionProductionRemaining); Assert.AreEqual(0, s.nodes[1].storehouseNextResource);
            Assert.AreEqual(0, BankRules.Total(s.nodes[1])); Assert.AreEqual(0, s.nodes[1].ownerID);
            Assert.AreEqual(b.claimThreshold, s.nodes[1].claimBar); Assert.AreEqual(VillagerState.Idle, s.villagers[0].state); return s;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void MinionEraTempo_CarriesOvershootAndPausesWithoutCatchup(bool determinism) => Repeat(() => {
            var b = Configure(); b.tempoStageTicks = new[] { 1 }; b.tempoProductionPercent = new[] { 200 };
            for (int i = 0; i < b.districtStats.Length; i++)
                if (b.districtStats[i].districtType == DistrictType.Storehouse && b.districtStats[i].era == 3) b.districtStats[i].productionTicks = 90;
            MatchFactory.Configure(b, BoardFixtures.LandGrid3x3()); var s = Board(b, DistrictType.Storehouse);
            s.nodes[1].districtEra = 3; Minion(s); Tick(s);
            Assert.AreEqual(1, s.nodes[1].bankFood); Assert.AreEqual(89, s.nodes[1].minionProductionRemaining);
            s.nodes[1].bankFood = 5; Tick(s, 20); Assert.AreEqual(89, s.nodes[1].minionProductionRemaining);
            s.nodes[1].bankFood = 4; Tick(s); Assert.AreEqual(87, s.nodes[1].minionProductionRemaining); return s;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void OrdinaryWorkersProduceBeforeForgeMinion(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b, DistrictType.Forge); Minion(s); s.nodes[1].materialAllocation = 1;
            s.nodes[0].districtType = DistrictType.Mine;
            s.villagers = new[] { TestBoardFactory.MakeIdleVillager(0, 0, 0, b) };
            s.villagers[0].state = VillagerState.Working; s.villagers[0].suit = SuitType.Miner;
            s.villagers[0].productionTicksMax = 40; s.villagers[0].productionTicksRemaining = 1;
            Tick(s); Assert.AreEqual(0, s.players[0].materials); Assert.AreEqual(1, s.nodes[1].bankMetal); return s;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void WorkerArrival_UsesMinionCapacityAndLowestId(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b); Minion(s, 30);
            s.villagers = new[] { TestBoardFactory.MakeIdleVillager(8, 0, 1, b), TestBoardFactory.MakeIdleVillager(4, 0, 0, b) };
            s.villagers[0].state = VillagerState.Working; s.villagers[0].suit = SuitType.Farmer;
            s.villagers[0].productionTicksMax = s.villagers[0].productionTicksRemaining = 30;
            CommandProcessor.ProcessCommand(s, new GameCommand { type = CommandType.Move, playerID = 0, villagerID = 1, targetNodeID = 1 });
            Tick(s, 4); Assert.AreEqual(VillagerState.Working, s.villagers[1].state); Assert.AreEqual(VillagerState.Idle, s.villagers[0].state); return s;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void InstallMinion_InvalidCapacityAndFundsRefuse(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b); s.players[0].metal = 2;
            int hash = SimulationStateHasher.ComputeHash(s); CommandProcessor.ProcessCommand(s, Install()); Assert.AreEqual(hash, SimulationStateHasher.ComputeHash(s));
            s.players[0].metal = 3; b.bankCapacity = 0; CommandProcessor.SetBalance(b);
            hash = SimulationStateHasher.ComputeHash(s); CommandProcessor.ProcessCommand(s, Install()); Assert.AreEqual(hash, SimulationStateHasher.ComputeHash(s));
            b.bankCapacity = 5; b.maxWorkersPerNode = 0; CommandProcessor.SetBalance(b);
            CommandProcessor.ProcessCommand(s, Install()); Assert.AreEqual(hash, SimulationStateHasher.ComputeHash(s)); return s;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void StructureMaxHP_UsesFortressEraAndGlobalMinionHP(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b, DistrictType.Fortress);
            for (int i = 0; i < b.districtStats.Length; i++)
                if (b.districtStats[i].districtType == DistrictType.Fortress) b.districtStats[i].fortificationHP = 16 + b.districtStats[i].era;
            GameSimulation.SetBalance(b); CommandProcessor.SetBalance(b);
            for (int era = 0; era < 6; era++) {
                s.nodes[1].districtEra = era; s.nodes[1].structureKind = StructureKind.None; s.nodes[1].fortressLevel = 0;
                s.players[0].materials = 30;
                CommandProcessor.ProcessCommand(s, new GameCommand { type = CommandType.UpgradeFortress, playerID = 0, villagerID = -1, targetNodeID = 1 });
                Assert.AreEqual(16 + era, s.nodes[1].structureHP); Assert.AreEqual(16 + era, StructureRules.MaxHP(s.nodes[1], b));
            }
            s.nodes[1].structureKind = StructureKind.Minion; Assert.AreEqual(16, StructureRules.MaxHP(s.nodes[1], b)); return s;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void StationaryCollector_OnlyLivingFriendlyIdleOrWorking(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b); s.villagers = new[] { TestBoardFactory.MakeIdleVillager(0, 0, 1, b) };
            s.villagers[0].targetNodeID = 2;
            foreach (VillagerState state in Enum.GetValues(typeof(VillagerState))) {
                s.villagers[0].state = state;
                Assert.AreEqual(state == VillagerState.Idle || state == VillagerState.Working, BankRules.HasStationaryCollector(s, s.nodes[1]));
            }
            s.villagers[0].state = VillagerState.Idle; s.villagers[0].isConsumed = true;
            Assert.IsFalse(BankRules.HasStationaryCollector(s, s.nodes[1])); s.villagers[0].isConsumed = false;
            s.villagers[0].ownerID = 1; Assert.IsFalse(BankRules.HasStationaryCollector(s, s.nodes[1]));
            Assert.IsTrue(BankRules.Locked(s, s.nodes[1], b)); return s;
        }, determinism);
    }
}
